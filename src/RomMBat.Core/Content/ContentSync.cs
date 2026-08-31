using RomM.Client;
using RomM.Client.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What one step of a running sync is doing, for a progress display.</summary>
/// <param name="Step">The game being worked on.</param>
/// <param name="Index">Its position in the plan, from one.</param>
/// <param name="Total">How many steps the plan has.</param>
/// <param name="Progress">Transfer progress, or null between files.</param>
public sealed record ContentSyncProgress(ContentStep Step, int Index, int Total, RomContentProgress? Progress);

/// <summary>What a sync actually did.</summary>
public sealed record ContentSyncOutcome
{
    public int Downloaded { get; init; }

    public int Resumed { get; init; }

    public int Adopted { get; init; }

    public int AlreadyPresent { get; init; }

    public int Blocked { get; init; }

    public int Failed { get; init; }

    public long BytesTransferred { get; init; }

    /// <summary>One line per game that did not work, in the words the user should see.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>True when the run wrote nothing at all, which is what an unchanged set should do.</summary>
    public bool IsNoOp => Downloaded == 0 && Resumed == 0 && Adopted == 0 && Failed == 0;

    /// <summary>
    /// Two outcomes as one, for a caller that applies a plan in pieces.
    /// </summary>
    /// <remarks>
    /// Interleaving artwork means one call per game rather than one per set, so the set's own
    /// line has to be assembled from forty of these. Here rather than at the caller so a field
    /// added above is one a reader is looking at when they wonder whether it sums.
    /// </remarks>
    public static ContentSyncOutcome Merge(ContentSyncOutcome first, ContentSyncOutcome second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return new ContentSyncOutcome
        {
            Downloaded = first.Downloaded + second.Downloaded,
            Resumed = first.Resumed + second.Resumed,
            Adopted = first.Adopted + second.Adopted,
            AlreadyPresent = first.AlreadyPresent + second.AlreadyPresent,
            Blocked = first.Blocked + second.Blocked,
            Failed = first.Failed + second.Failed,
            BytesTransferred = first.BytesTransferred + second.BytesTransferred,
            Problems = [.. first.Problems, .. second.Problems],
        };
    }

    public string Summary
    {
        get
        {
            if (IsNoOp && Blocked == 0)
            {
                return $"nothing to do: {AlreadyPresent} games already present, 0 downloaded, 0 written";
            }

            var parts = new List<string>();

            if (Downloaded > 0)
            {
                parts.Add($"{Downloaded} downloaded ({ByteSize.Format(BytesTransferred)})");
            }

            if (Resumed > 0)
            {
                parts.Add($"{Resumed} resumed");
            }

            if (Adopted > 0)
            {
                parts.Add($"{Adopted} adopted from disk");
            }

            if (AlreadyPresent > 0)
            {
                parts.Add($"{AlreadyPresent} already present");
            }

            if (Blocked > 0)
            {
                parts.Add($"{Blocked} blocked by the budget");
            }

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Carries out a <see cref="ContentPlan"/>: fetches, verifies, and only then puts a file where
/// EmulationStation can see it.
/// </summary>
/// <remarks>
/// <b>Write, verify, rename, in that order, always.</b> The transfer goes to
/// <c>emulators/rommbat/partial/</c>, is checked against what the server says it should be, and
/// is moved into <c>roms/</c> only once it passes. A power cut at any point leaves either
/// nothing or a complete file, never something ES will list and try to launch.
/// <para>
/// A failure is per game, not per run. A drive that goes away mid-transfer, a ROM the server no
/// longer has, a file that verifies wrong: each is recorded against that game and the run
/// carries on, because a set of forty games should not be abandoned over one of them.
/// </para>
/// </remarks>
public sealed class ContentSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly TimeProvider _time;

    public ContentSync(
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
    public async Task<ContentSyncOutcome> ApplyAsync(
        ContentPlan plan,
        IProgress<ContentSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var downloaded = 0;
        var resumed = 0;
        var adopted = 0;
        var present = 0;
        var blocked = 0;
        var failed = 0;
        var bytes = 0L;
        var problems = new List<string>();

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = plan.Steps[index];
            progress?.Report(new ContentSyncProgress(step, index + 1, plan.Steps.Count, null));

            switch (step.Action)
            {
                case ContentAction.AlreadyPresent:
                    present++;
                    break;

                case ContentAction.Blocked:
                    blocked++;
                    problems.Add($"{step.Member.DisplayName}: {step.Reason}");
                    break;

                case ContentAction.Adopt:
                    Adopt(step);
                    adopted++;
                    break;

                case ContentAction.Download:
                case ContentAction.Resume:
                    var result = await TransferAsync(step, index, plan.Steps.Count, progress, cancellationToken)
                        .ConfigureAwait(false);

                    if (result.Problem is { } problem)
                    {
                        failed++;
                        problems.Add($"{step.Member.DisplayName}: {problem}");
                        break;
                    }

                    bytes += result.Bytes;

                    if (step.Action == ContentAction.Resume)
                    {
                        resumed++;
                    }
                    else
                    {
                        downloaded++;
                    }

                    break;

                default:
                    break;
            }
        }

        return new ContentSyncOutcome
        {
            Downloaded = downloaded,
            Resumed = resumed,
            Adopted = adopted,
            AlreadyPresent = present,
            Blocked = blocked,
            Failed = failed,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    /// <summary>Records a file that was already there, so it is never fetched again.</summary>
    private void Adopt(ContentStep step)
    {
        var absolute = _install.Resolve(step.TargetPath);
        var info = new FileInfo(absolute);
        var fingerprint = ContentHasher.Compute(absolute);

        _store.Files.Record(new LocalFile
        {
            Path = step.TargetPath,
            Folder = step.Member.Folder!,
            RomId = step.Member.RomId,
            FileName = step.Member.FsName,
            SizeBytes = info.Length,
            Md5Hash = fingerprint.Md5,
            Sha1Hash = fingerprint.Sha1,
            CrcHash = fingerprint.Crc32,
            HashScope = fingerprint.Scope,
            ModifiedUtc = info.LastWriteTimeUtc,
            VerifiedAt = _time.GetUtcNow(),
            VerifiedBy = VerificationOf(step.Member, fingerprint),

            // Never 'synced'. An adopted file is the user's, it does not count against the
            // budget, and eviction must never delete it.
            Origin = FileOrigin.Adopted,
        });
    }

    private async Task<(long Bytes, string? Problem)> TransferAsync(
        ContentStep step,
        int index,
        int total,
        IProgress<ContentSyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        var member = step.Member;
        var part = ContentPlanner.PartFor(member.RomId);
        var partAbsolute = _install.Resolve(part);
        var targetAbsolute = _install.Resolve(step.TargetPath);
        var now = _time.GetUtcNow();

        Directory.CreateDirectory(Path.GetDirectoryName(partAbsolute)!);
        Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolute)!);

        var existing = step.Action == ContentAction.Resume ? new FileInfo(partAbsolute) : null;
        var resumeFrom = existing is { Exists: true } ? existing.Length : 0;

        if (step.Action != ContentAction.Resume && File.Exists(partAbsolute))
        {
            // A partial file nothing vouches for. Starting again costs a download; continuing
            // from it would produce a file that verifies wrong at best.
            File.Delete(partAbsolute);
        }

        if (resumeFrom > 0 && member.SizeBytes > 0 && resumeFrom >= member.SizeBytes)
        {
            // The whole file is already here, because the power went out between the last byte
            // landing and the rename. Asking to resume past the end is refused 416 on every run
            // from here on, so what this needs is the verify and rename it never got.
            var (finished, wrong) = Verify(partAbsolute, member);

            if (wrong is null)
            {
                Commit(step, partAbsolute, targetAbsolute, finished!);
                return (0, null);
            }

            SafeDelete(partAbsolute);
            _store.Downloads.Remove(member.RomId);
            resumeFrom = 0;
        }

        var record = _store.Downloads.Begin(new ContentDownload
        {
            RomId = member.RomId,
            PartPath = part,
            TargetPath = step.TargetPath,
            ExpectedSize = member.SizeBytes,
            Validator = _store.Downloads.Find(member.RomId)?.Validator,
            UpdatedAt = now,
        });

        try
        {
            RomMResponse<RomContentResult> response;

            await using (var destination = new FileStream(
                partAbsolute,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                response = await _connection.DownloadRomContentAsync(
                    new RomContentRequest
                    {
                        RomId = member.RomId,
                        FsName = member.FsName,
                        IsMultiFile = member.IsMultiFile,
                        ResumeFrom = resumeFrom,
                        Validator = record.Validator,
                    },
                    destination,
                    new Progress<RomContentProgress>(value =>
                        progress?.Report(new ContentSyncProgress(step, index + 1, total, value))),
                    validator => _store.Downloads.RecordValidator(member.RomId, validator, _time.GetUtcNow()),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!response.IsSuccess)
            {
                if (response.Status == RomMResponseStatus.RangeNotSatisfiable)
                {
                    // Keeping it would resume into the same refusal forever, and the message
                    // says it is discarded, so discard it. The next run downloads whole.
                    SafeDelete(partAbsolute);
                    _store.Downloads.Remove(member.RomId);
                    return (0, response.Message);
                }

                _store.Downloads.Fail(member.RomId, response.Message ?? "the download failed", _time.GetUtcNow());
                return (0, response.Message);
            }

            var transferred = response.Value!.BytesWritten;
            var verification = Verify(partAbsolute, member);
            if (verification.Problem is { } wrong)
            {
                // Deleted rather than kept: a partial file that verifies wrong would be resumed
                // forever, and each attempt would end the same way.
                SafeDelete(partAbsolute);
                _store.Downloads.Remove(member.RomId);
                return (transferred, wrong);
            }

            Commit(step, partAbsolute, targetAbsolute, verification.Fingerprint!);
            return (transferred, null);
        }
        catch (RomMUnreachableException ex)
        {
            // The partial file stays: the whole point of writing one is that the next run
            // continues rather than starting again.
            _store.Downloads.Fail(member.RomId, ex.Message, _time.GetUtcNow());
            return (0, ex.Message);
        }
        catch (PathTooLongException)
        {
            _store.Downloads.Fail(member.RomId, "the path is too long", _time.GetUtcNow());
            return (0,
                $"the full path to '{member.FsName}' is longer than this machine allows. Move the RetroBat "
                    + "install closer to the root of the drive, or turn on long path support in Windows.");
        }
        catch (IOException ex)
        {
            _store.Downloads.Fail(member.RomId, ex.Message, _time.GetUtcNow());
            return (0, $"the file could not be written: {ex.Message}");
        }
    }

    /// <summary>Checks a finished transfer against what the server says it should be.</summary>
    /// <remarks>
    /// The hash is taken as though the file already had its real name, because a single-entry
    /// zip is hashed inside and the <c>.part</c> extension would send it down the wrong path.
    /// </remarks>
    private static (ContentFingerprint? Fingerprint, string? Problem) Verify(string partAbsolute, SyncSetMember member)
    {
        var length = new FileInfo(partAbsolute).Length;

        if (member.SizeBytes > 0 && length != member.SizeBytes)
        {
            return (null,
                $"the download is {ByteSize.Format(length)} but the server said it would be "
                    + $"{ByteSize.Format(member.SizeBytes)}.");
        }

        var fingerprint = ContentHasher.Compute(partAbsolute, member.FsName);

        // An archive this cannot see inside is hashed as its own bytes while the server's hash
        // describes the content, so a mismatch there is not evidence of anything and size is the
        // check that remains. VerificationOf still records a hash that happens to agree.
        if (fingerprint.DescribesLibraryContent)
        {
            if (member.Md5Hash is not null && !ContentHasher.Matches(fingerprint.Md5, member.Md5Hash))
            {
                return (null, "the downloaded file does not match the md5 the server reported.");
            }

            if (member.Md5Hash is null
                && member.Sha1Hash is not null
                && !ContentHasher.Matches(fingerprint.Sha1, member.Sha1Hash))
            {
                return (null, "the downloaded file does not match the sha1 the server reported.");
            }
        }

        return (fingerprint, null);
    }

    /// <summary>Moves a verified file into place and records it, so neither can outlive the other.</summary>
    private void Commit(ContentStep step, string partAbsolute, string targetAbsolute, ContentFingerprint fingerprint)
    {
        File.Move(partAbsolute, targetAbsolute, overwrite: true);

        var info = new FileInfo(targetAbsolute);

        _store.InTransaction(() =>
        {
            _store.Downloads.Remove(step.Member.RomId);
            _store.Files.Record(new LocalFile
            {
                Path = step.TargetPath,
                Folder = step.Member.Folder!,
                RomId = step.Member.RomId,
                FileName = step.Member.FsName,
                SizeBytes = info.Length,
                Md5Hash = fingerprint.Md5,
                Sha1Hash = fingerprint.Sha1,
                CrcHash = fingerprint.Crc32,
                HashScope = fingerprint.Scope,
                ModifiedUtc = info.LastWriteTimeUtc,
                VerifiedAt = _time.GetUtcNow(),
                VerifiedBy = VerificationOf(step.Member, fingerprint),
                Origin = FileOrigin.Synced,
            });
        });
    }

    /// <summary>Which check actually ran, which is not always the one that was wanted.</summary>
    private static VerifiedBy VerificationOf(SyncSetMember member, ContentFingerprint fingerprint)
    {
        if (ContentHasher.Matches(fingerprint.Md5, member.Md5Hash))
        {
            return VerifiedBy.Md5;
        }

        return ContentHasher.Matches(fingerprint.Sha1, member.Sha1Hash) ? VerifiedBy.Sha1 : VerifiedBy.Size;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving it costs one stale partial file, which the next plan discards anyway.
        }
    }
}
