using RomM.Client;
using RomM.Client.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a BIOS pass did.</summary>
public sealed record BiosSyncOutcome
{
    /// <summary>Files written, which is destinations rather than downloads.</summary>
    public int Written { get; init; }

    /// <summary>Transfers made, which is one per md5 however many destinations it owed.</summary>
    public int Downloaded { get; init; }

    public int AlreadyPresent { get; init; }

    /// <summary>Files the user already had at the right path with the right hash.</summary>
    public int Adopted { get; init; }

    public int Failed { get; init; }

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool IsNoOp => Written == 0 && Adopted == 0 && Failed == 0;

    public string Summary
    {
        get
        {
            if (IsNoOp)
            {
                return AlreadyPresent == 0
                    ? "no BIOS to fetch"
                    : $"BIOS: {AlreadyPresent} already present";
            }

            var parts = new List<string>();

            if (Downloaded > 0)
            {
                var writes = Written > Downloaded ? $", written to {Written} paths" : string.Empty;
                parts.Add($"{Downloaded} fetched ({ByteSize.Format(BytesTransferred)}){writes}");
            }

            if (Adopted > 0)
            {
                parts.Add($"{Adopted} already on disk");
            }

            if (AlreadyPresent > 0)
            {
                parts.Add($"{AlreadyPresent} present");
            }

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return "BIOS: " + string.Join(", ", parts);
        }
    }
}

/// <summary>Progress through a BIOS pass, for a console line.</summary>
/// <param name="Step">The file being worked on.</param>
/// <param name="Index">Its position, from one.</param>
/// <param name="Total">How many files are being fetched.</param>
public sealed record BiosSyncProgress(BiosStep Step, int Index, int Total);

/// <summary>
/// Carries out a <see cref="BiosPlan"/>: fetches what RomM has, and writes it where RetroBat
/// wants it.
/// </summary>
/// <remarks>
/// <b>Write, verify, rename, exactly as M3 does.</b> The transfer goes to
/// <c>emulators/rommbat/partial/</c>, is checked against the md5 RetroBat requires rather than
/// the one RomM reports, and is moved into <c>bios/</c> only once it passes. A file that
/// arrives wrong is deleted rather than kept: firmware that fails to verify is worse than
/// absent, because an emulator that loads it fails in ways that look like a bad ROM.
/// <para>
/// <b>One download can owe several writes.</b> Six required md5s name more than one
/// destination, so the bytes cross the network once and are copied into each path from the
/// verified file.
/// </para>
/// <para>
/// <b>Nothing is ever overwritten and nothing is ever deleted.</b> A mismatch is reported and
/// left alone, and an adopted file is recorded as the user's. <c>bios/</c> holds thousands of
/// files RomMBat did not put there, openMSX's save states among them.
/// </para>
/// </remarks>
public sealed class BiosSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly TimeProvider _time;

    public BiosSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection connection,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);

        _install = install;
        _store = store;
        _connection = connection;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Carries out a plan.</summary>
    public async Task<BiosSyncOutcome> ApplyAsync(
        BiosPlan plan,
        IProgress<BiosSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var written = 0;
        var downloaded = 0;
        var adopted = 0;
        var present = plan.Count(BiosAction.AlreadyPresent);
        var failed = 0;
        var bytes = 0L;
        var problems = new List<string>();

        // The verified file for an md5 already fetched in this pass, so the second destination
        // for the same bytes is a copy rather than a second transfer.
        var fetched = new Dictionary<string, RelativePath>(StringComparer.Ordinal);
        var refused = new HashSet<string>(StringComparer.Ordinal);

        var downloads = plan.Downloads.ToList();
        var index = 0;

        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (step.Action)
            {
                case BiosAction.Adopt:
                    Record(step, FileOrigin.Adopted);
                    adopted++;
                    break;

                case BiosAction.Mismatch:
                    problems.Add($"{step.Path}: {step.Reason}");
                    break;

                case BiosAction.Download:
                    index++;
                    progress?.Report(new BiosSyncProgress(step, index, downloads.Count));

                    var md5 = step.Requirement.Md5!;

                    if (refused.Contains(md5))
                    {
                        // The transfer for these bytes already failed in this pass. Asking again
                        // for a second destination would fail the same way and say so twice.
                        break;
                    }

                    if (fetched.TryGetValue(md5, out var source))
                    {
                        var copied = CopyInto(step, source);
                        if (copied is { } problem)
                        {
                            failed++;
                            problems.Add($"{step.Path}: {problem}");
                            break;
                        }

                        Record(step, FileOrigin.Synced);
                        written++;
                        break;
                    }

                    var result = await FetchAsync(step, cancellationToken).ConfigureAwait(false);

                    if (result.Problem is { } wrong)
                    {
                        failed++;
                        refused.Add(md5);
                        problems.Add($"{step.Requirement.FileName}: {wrong}");
                        break;
                    }

                    Record(step, FileOrigin.Synced);
                    fetched[md5] = step.Path;
                    downloaded++;
                    written++;
                    bytes += result.Bytes;
                    break;

                default:
                    break;
            }
        }

        return new BiosSyncOutcome
        {
            Written = written,
            Downloaded = downloaded,
            AlreadyPresent = present,
            Adopted = adopted,
            Failed = failed,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    /// <summary>Fetches one file, verifies it against RetroBat's hash, and moves it into place.</summary>
    private async Task<(long Bytes, string? Problem)> FetchAsync(BiosStep step, CancellationToken cancellationToken)
    {
        var wanted = step.Requirement.Md5!;
        var part = _install.Resolve(BiosPlanner.PartFor(wanted));
        var target = _install.Resolve(step.Path);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(part)!);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            RomMResponse<FirmwareResult> response;

            await using (var destination = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                response = await _connection
                    .DownloadFirmwareAsync(step.Match!, destination, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!response.IsSuccess)
            {
                SafeDelete(part);
                return (0, response.Message);
            }

            // Against RetroBat's hash, not RomM's. They agree by construction of the join, and
            // checking the one the emulator cares about is what makes that a fact rather than
            // an assumption.
            var found = ContentHasher.ComputeFileMd5(part);

            if (!ContentHasher.Matches(found, wanted))
            {
                SafeDelete(part);
                return (0,
                    $"what arrived hashes to {found}, and RetroBat requires {wanted}. Nothing was written.");
            }

            File.Move(part, target, overwrite: true);
            return (response.Value!.BytesWritten, null);
        }
        catch (RomMUnreachableException ex)
        {
            SafeDelete(part);
            return (0, ex.Message);
        }
        catch (PathTooLongException)
        {
            SafeDelete(part);
            return (0, "the path to it is longer than this machine allows.");
        }
        catch (IOException ex)
        {
            SafeDelete(part);
            return (0, $"it could not be written: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeDelete(part);
            return (0, $"it could not be written: {ex.Message}");
        }
    }

    /// <summary>Writes a second destination for bytes this pass already verified.</summary>
    private string? CopyInto(BiosStep step, RelativePath source)
    {
        var from = _install.Resolve(source);
        var to = _install.Resolve(step.Path);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: false);
            return null;
        }
        catch (IOException ex)
        {
            return $"it could not be copied from {source}: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"it could not be copied from {source}: {ex.Message}";
        }
    }

    private void Record(BiosStep step, FileOrigin origin)
    {
        var info = new FileInfo(_install.Resolve(step.Path));

        _store.Files.Record(new LocalFile
        {
            Path = step.Path,

            // Neither, and the schema enforces it: firmware belongs to a system rather than to
            // a game, and having no rom_id is what keeps eviction away from it.
            Folder = null,
            RomId = null,

            Kind = LocalFileKind.Firmware,
            FileName = step.Requirement.FileName,
            SizeBytes = info.Exists ? info.Length : 0,
            Md5Hash = step.Requirement.Md5,
            ModifiedUtc = info.Exists ? info.LastWriteTimeUtc : null,
            VerifiedAt = _time.GetUtcNow(),
            VerifiedBy = VerifiedBy.Md5,

            // An adopted file is the user's. It does not count against the budget, and eviction
            // must never remove it, which matters more here than anywhere else: bios/ holds
            // emulator user data and save states RomMBat did not put there.
            Origin = origin,
        });
    }

    private static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // One stale partial file, which the next attempt truncates anyway.
        }
    }
}
