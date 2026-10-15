using RomM.Client;
using RomM.Client.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Content;

/// <summary>What a BIOS pass did.</summary>
public sealed record BiosSyncOutcome
{
    /// <summary>Files written, which is destinations rather than downloads.</summary>
    public int Written { get; init; }

    /// <summary>Transfers made, which is one per file however many destinations it owed.</summary>
    public int Downloaded { get; init; }

    public int AlreadyPresent { get; init; }

    /// <summary>Files the user already had at the right path with an accepted hash.</summary>
    public int Adopted { get; init; }

    /// <summary>RomMBat's own files replaced by a better-ranked one.</summary>
    public int Upgraded { get; init; }

    public int Failed { get; init; }

    /// <summary>The worst reason any file failed, which decides whether waiting would help.</summary>
    public FailureCause Cause { get; init; }

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool IsNoOp => Written == 0 && Adopted == 0 && Upgraded == 0 && Failed == 0;

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
            var fresh = Downloaded - Upgraded;

            if (fresh > 0)
            {
                var writes = Written > fresh ? $", written to {Written} paths" : string.Empty;
                parts.Add($"{fresh} fetched ({ByteSize.Format(BytesTransferred)}){writes}");
            }

            if (Upgraded > 0)
            {
                parts.Add($"{Upgraded} upgraded");
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
/// Carries out a <see cref="BiosPlan"/>: fetches what RomM has, and writes it where the slot
/// wants it.
/// </summary>
/// <remarks>
/// <b>Write, verify, rename, exactly as a ROM download does.</b> The transfer goes to
/// <c>emulators/rommbat/partial/</c>, is checked against the md5 the pick chose rather than the
/// one RomM reports, or for a zip fetched by name against the members its readers list, and is
/// moved into <c>bios/</c> only once it passes. A file that arrives wrong is deleted rather than
/// kept: firmware that fails to verify is worse than absent, because an emulator that loads it
/// fails in ways that look like a bad ROM.
/// <para>
/// <b>One download can owe several writes, and one write can answer several systems.</b> One
/// file can be accepted at more than one destination, so the bytes cross the network once and
/// are copied into each path from the verified file. Destinations run the other way too, named
/// by two or more systems and therefore carrying a plan step each: a destination is acted on the
/// first time it is reached and skipped after that, so nothing is ever copied onto itself.
/// </para>
/// <para>
/// <b>Nothing is deleted, and the one overwrite is of RomMBat's own file.</b> A mismatch is
/// reported and left alone, and an adopted file is recorded as the user's. An
/// <see cref="BiosAction.Upgrade"/> replaces a file RomMBat downloaded, and only while the disk
/// still holds the bytes it recorded. <c>bios/</c> holds thousands of files RomMBat did not put
/// there, openMSX's save states among them.
/// </para>
/// </remarks>
public sealed class BiosSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection? _connection;
    private readonly TimeProvider _time;

    /// <param name="connection">
    /// Null when the server was not asked. An offline plan carries no <see cref="BiosAction.Download"/>
    /// step, because nothing was asked about the library, but it can still carry adoptions:
    /// recognizing a file the user copied in by hand is local work and does not need a server.
    /// </param>
    public BiosSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection? connection,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

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
        var upgraded = 0;
        var present = 0;
        var failed = 0;
        var cause = FailureCause.None;
        var bytes = 0L;
        var problems = new List<string>();

        // The verified file for a transfer already made in this pass, and the md5 it carries,
        // so the second destination for the same bytes is a copy rather than a second transfer.
        var fetched = new Dictionary<string, (RelativePath Path, string Md5)>(StringComparer.Ordinal);
        var refused = new HashSet<string>(StringComparer.Ordinal);

        // A destination several systems name carries a plan step each. It is written, recorded
        // and counted once.
        var handled = new HashSet<RelativePath>();

        var destinations = plan.Downloads.Select(step => step.Path).Distinct().Count();
        var index = 0;

        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!handled.Add(step.Path))
            {
                continue;
            }

            switch (step.Action)
            {
                case BiosAction.AlreadyPresent:
                    present++;
                    break;

                case BiosAction.Adopt:
                    Record(step, FileOrigin.Adopted, step.Found ?? ContentHasher.ComputeFileMd5(_install.Resolve(step.Path)));
                    adopted++;
                    break;

                case BiosAction.Mismatch or BiosAction.ContentsMismatch or BiosAction.Ambiguous:
                    problems.Add($"{step.Path}: {step.Reason}");
                    break;

                case BiosAction.Download or BiosAction.Upgrade:
                    index++;
                    progress?.Report(new BiosSyncProgress(step, index, destinations));

                    var key = step.TransferKey!;

                    if (refused.Contains(key))
                    {
                        // The transfer for these bytes already failed in this pass. Asking again
                        // for a second destination would fail the same way and say so twice.
                        break;
                    }

                    // Replaced only while the disk still holds the bytes RomMBat recorded.
                    // Anything else got there since the plan was built and is the user's now.
                    // A fault of this one destination, so the bytes stay wanted elsewhere.
                    if (step.Action == BiosAction.Upgrade && Changed(step) is { } changed)
                    {
                        failed++;
                        cause = FailureCauses.Worst(cause, FailureCause.Failed);
                        problems.Add($"{step.Path}: {changed}");
                        break;
                    }

                    if (fetched.TryGetValue(key, out var source))
                    {
                        var copied = CopyInto(step, source.Path);
                        if (copied is { } problem)
                        {
                            failed++;
                            cause = FailureCauses.Worst(cause, FailureCause.Failed);
                            problems.Add($"{step.Path}: {problem}");
                            break;
                        }

                        Record(step, FileOrigin.Synced, source.Md5);
                        Count(step);
                        break;
                    }

                    var result = await FetchAsync(step, cancellationToken).ConfigureAwait(false);

                    if (result.Problem is { } wrong)
                    {
                        failed++;
                        cause = FailureCauses.Worst(cause, result.Cause);
                        if (result.Refuse)
                        {
                            refused.Add(key);
                        }

                        problems.Add($"{step.Requirement.FileName}: {wrong}");
                        break;
                    }

                    Record(step, FileOrigin.Synced, result.Md5!);
                    fetched[key] = (step.Path, result.Md5!);
                    downloaded++;
                    bytes += result.Bytes;
                    Count(step);
                    break;

                default:
                    break;
            }
        }

        void Count(BiosStep step)
        {
            if (step.Action == BiosAction.Upgrade)
            {
                upgraded++;
            }
            else
            {
                written++;
            }
        }

        return new BiosSyncOutcome
        {
            Written = written,
            Downloaded = downloaded,
            AlreadyPresent = present,
            Adopted = adopted,
            Upgraded = upgraded,
            Failed = failed,
            Cause = cause,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    /// <summary>Fetches one file, verifies it, and moves it into place.</summary>
    private async Task<(long Bytes, string? Md5, string? Problem, FailureCause Cause, bool Refuse)> FetchAsync(
        BiosStep step,
        CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            return (0, null, "RomM was not asked about it, so there is nothing to fetch from.", FailureCause.Unreachable, true);
        }

        var part = _install.Resolve(BiosPlanner.PartFor(step.TransferKey!));
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
                return (0, null, response.Message, FailureCauses.Of(response.Status), true);
            }

            var found = ContentHasher.ComputeFileMd5(part);

            if (Verify(step, part, found) is { } refusal)
            {
                SafeDelete(part);
                return (0, null, refusal, FailureCause.Failed, true);
            }

            // Checked again after the transfer, which can take long enough for a user to swap the
            // file. The bytes are still good for any other destination, so they are not refused.
            if (step.Action == BiosAction.Upgrade && Changed(step) is { } changed)
            {
                SafeDelete(part);
                return (0, null, changed, FailureCause.Failed, false);
            }

            // Overwrites only an upgrade's target, which holds RomMBat's own bytes. Anywhere else a file that appeared since the plan was built is the user's,
            // and the IOException below reports it rather than losing it.
            File.Move(part, target, overwrite: step.Action == BiosAction.Upgrade);

            return (response.Value!.BytesWritten, found, null, FailureCause.None, false);
        }
        catch (RomMUnreachableException ex)
        {
            SafeDelete(part);
            return (0, null, ex.Message, FailureCause.Unreachable, true);
        }
        catch (PathTooLongException)
        {
            SafeDelete(part);
            return (0, null, "the path to it is longer than this machine allows.", FailureCause.Failed, true);
        }
        catch (IOException ex)
        {
            SafeDelete(part);
            return (0, null, $"it could not be written: {ex.Message}", FailureCause.Failed, true);
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeDelete(part);
            return (0, null, $"it could not be written: {ex.Message}", FailureCause.Failed, true);
        }
    }

    /// <summary>Why what arrived may not land, or null when it may.</summary>
    /// <remarks>
    /// Against the md5 the pick chose, not the one RomM reports. They agree by construction of the
    /// join, and checking the one the emulator accepts is what makes that a fact rather than an
    /// assumption. A zip fetched by name has no chosen md5, so its members decide.
    /// </remarks>
    private static string? Verify(BiosStep step, string part, string found)
    {
        if (step.Chosen is { } chosen)
        {
            return ContentHasher.Matches(found, chosen.Md5)
                ? null
                : $"what arrived hashes to {found}, and the slot accepts {chosen.Md5}. Nothing was written.";
        }

        var satisfied = BiosZip.Satisfied(part, step.Requirement.Contents);

        return satisfied is { Count: > 0 }
            ? null
            : $"RomM's {step.Match!.FileName} holds members none of its readers accepts, so it was discarded.";
    }

    /// <summary>Why an upgrade's target may not be replaced, or null when it still holds RomMBat's bytes.</summary>
    private string? Changed(BiosStep step)
    {
        var target = _install.Resolve(step.Path);

        try
        {
            return File.Exists(target) && ContentHasher.Matches(ContentHasher.ComputeFileMd5(target), step.Found)
                ? null
                : "it changed on disk since the plan was made, so it was left as it is.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"it could not be read to check it is still RomMBat's: {ex.Message}";
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

            // As in FetchAsync: only an upgrade, already checked, replaces what is there.
            File.Copy(from, to, overwrite: step.Action == BiosAction.Upgrade);
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

    private void Record(BiosStep step, FileOrigin origin, string md5)
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

            // What is on the disk: for a ranked slot the file the pick chose, and for a zip the
            // container whose members passed.
            Md5Hash = md5,
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
