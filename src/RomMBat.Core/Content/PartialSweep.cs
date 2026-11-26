using System.Globalization;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Content;

/// <summary>Why a file under <c>partial/</c> is dead.</summary>
public enum PartialReason
{
    /// <summary>A ROM transfer whose game no enabled set claims any more.</summary>
    Unclaimed,

    /// <summary>A transfer that never resumes, so anything left of it is from a pass that died.</summary>
    Abandoned,
}

/// <summary>One file or staging directory the sweep would remove.</summary>
public sealed record PartialCandidate
{
    public required RelativePath Path { get; init; }

    public required PartialReason Reason { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>True for a staging directory rather than a single file.</summary>
    public bool IsDirectory { get; init; }

    public string Name => Path.Name;
}

/// <summary>What the sweep found, and what removing it would free.</summary>
public sealed record PartialSweepPlan
{
    public IReadOnlyList<PartialCandidate> Candidates { get; init; } = [];

    public long BytesToFree => Candidates.Sum(candidate => candidate.SizeBytes);

    public bool IsEmpty => Candidates.Count == 0;

    public string Summary => IsEmpty
        ? "no abandoned transfers under partial/"
        : $"{Candidates.Count} abandoned {(Candidates.Count == 1 ? "transfer" : "transfers")}, "
            + $"{ByteSize.Format(BytesToFree)}";
}

/// <summary>What the sweep actually removed.</summary>
public sealed record PartialSweepOutcome
{
    public int Removed { get; init; }

    public long BytesFreed { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>True when another agent held the tree lock, so the pass did not run at all.</summary>
    public bool Skipped { get; init; }

    public string Summary => Skipped
        ? "partial/ was left alone: another agent is writing there. The next pass sweeps it."
        : Removed == 0
            ? "nothing was reclaimed from partial/"
            : $"{Removed} abandoned {(Removed == 1 ? "transfer" : "transfers")} removed, "
                + $"{ByteSize.Format(BytesFreed)} freed";
}

/// <summary>
/// Reclaims <c>emulators/rommbat/partial/</c>, which neither the disk budget nor the free-space
/// floor can see.
/// </summary>
/// <remarks>
/// <b>The bytes are invisible to everything else, which is why they need their own pass.</b> The
/// disk budget counts what RomMBat downloaded through <c>local_file</c>, and a partial has no
/// row because a row is only written on commit. The free-space floor is read live from the
/// volume, so the bytes are gone from free space and attributed to nothing.
/// <see cref="EvictionPlanner"/> cannot reach them either: it walks <c>local_file</c>, and the
/// guarantee that it never deletes a file RomMBat did not download is exactly why it does not
/// walk the filesystem.
/// <para>
/// <b>Six producers write here now, not the one this was first described against, and they
/// die differently.</b> Only the ROM transfer resumes, so only it has live state to protect;
/// the other five open with <c>FileMode.Create</c> or delete in a <c>finally</c>, so anything
/// of theirs that outlives its process is from a pass that died.
/// </para>
/// <list type="bullet">
/// <item><c>&lt;rom id&gt;.part</c>, <c>&lt;rom id&gt;-&lt;file id&gt;.part</c>, and
/// <c>pack-&lt;rom id&gt;/</c>, from <see cref="ContentSync"/> and startup publication.
/// Kept while an enabled set still claims the game, regardless of age. Startup and explicit
/// sync restore staged package members to their normal partial paths before planning.</item>
/// <item><c>bios-&lt;md5&gt;.part</c>, from <see cref="BiosSync"/>. No <c>content_download</c>
/// row exists or is wanted, since the fetch never resumes.</item>
/// <item><c>save-&lt;id&gt;.part</c>, <c>resolve-&lt;id&gt;.part</c> and
/// <c>state-&lt;id&gt;.part</c>, from the save download, the conflict resolver and the state
/// restore.</item>
/// <item><c>unit-&lt;guid&gt;.zip</c> and the <c>unit-&lt;guid&gt;/</c> staging directory,
/// from <see cref="SaveUnitTransfer"/>.</item>
/// </list>
/// <para>
/// <b>One of those candidates is live state rather than litter, so the sweep takes the tree
/// lock.</b> <c>unit-&lt;guid&gt;/</c> exists for the window in which a class C restore has
/// extracted a unit and not yet moved it into the container, and nothing holds a handle on it:
/// <see cref="SaveArchive"/> closes each entry's writer inside its own loop. A recursive delete
/// landing in that window succeeds, and the restore then fails partway through its moves with
/// the container half swapped, which is the state the <c>Remove</c>-before-<c>Move</c> ordering
/// in <see cref="SaveUnitTransfer"/> exists to prevent. A sentinel file inside the staging
/// directory does not close it, because a recursive delete removes the siblings before it
/// reaches the sentinel. So <see cref="Apply"/> holds <see cref="TreeLock"/> for the whole pass
/// and does nothing at all when it cannot get it, and the three routes into a restore
/// (<c>flush</c>, <c>saves resolve</c> and <c>saves restore --apply</c>) all hold the same lock.
/// </para>
/// <para>
/// The sweep also takes the library lease before the save lease. This protects the ROM and
/// BIOS producers, including the interval after a member closes its handle and before the group
/// is published. A preview is rechecked under both leases, including the active set claims.
/// </para>
/// <para>
/// <b>Anything else in the directory is left alone.</b> A name this class does not recognize
/// was not written by a producer it knows about, and deleting on the strength of "it is in a
/// directory we own" is how a sweep destroys something it was never asked to judge.
/// </para>
/// </remarks>
public sealed class PartialSweep
{
    private const string RomSuffix = ".part";
    private const string ZipSuffix = ".zip";
    private const string BiosPrefix = "bios-";
    private const string SavePrefix = "save-";
    private const string ResolvePrefix = "resolve-";
    private const string StatePrefix = "state-";
    private const string UnitPrefix = "unit-";

    /// <summary>An md5 as <c>BiosPlanner</c> writes it, and a <c>Guid("N")</c>, are both this long.</summary>
    private const int HexDigits = 32;

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;

    public PartialSweep(RetroBatInstall install, LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
    }

    /// <summary>Where the partials live, relative to the RetroBat root.</summary>
    /// <remarks>
    /// <see cref="RetroBatInstall.PartialDirectory"/>, which every producer resolves to, rather
    /// than a path of this class's own. <see cref="Plan"/> answers an empty plan for a
    /// directory that is not there, so a copy that drifted from the producers' would make the
    /// sweep report a clean install over a directory full of dead transfers rather than fail.
    /// </remarks>
    public static RelativePath Directory => RetroBatInstall.PartialDirectory;

    /// <summary>Works out what is dead, without touching anything.</summary>
    /// <remarks>
    /// <b>Nothing outside the tree can reach this list, and the enumeration is what provides
    /// that</b> rather than a per-entry check. Every candidate comes from
    /// <c>EnumerateFileSystemEntries</c> over <see cref="Directory"/> resolved against this
    /// install, so an <c>install.Contains</c> filter here can never be false. There was one, it
    /// was cited as the evidence for the invariant, and a guard that cannot fire is not
    /// evidence: the next session reads the check and believes the question is settled. See #61.
    /// <para>
    /// A reparse point under <c>partial/</c> is the case a real check would have to answer, and
    /// it would have to run against the resolved target rather than the enumerated name.
    /// Untaken: <c>offline-and-portable</c> records that FAT carries no links at all, so this is
    /// speculative on the measured install, and the honest state is one claim rather than one
    /// claim plus a check that does not support it.
    /// </para>
    /// </remarks>
    public PartialSweepPlan Plan()
    {
        var root = _install.Resolve(Directory);

        if (!System.IO.Directory.Exists(root))
        {
            return new PartialSweepPlan();
        }

        var claimed = ClaimedRomIds();
        var candidates = new List<PartialCandidate>();

        foreach (var entry in Entries(root))
        {
            var isDirectory = System.IO.Directory.Exists(entry);
            if (Classify(Path.GetFileName(entry), isDirectory, claimed) is not { } reason)
            {
                continue;
            }

            candidates.Add(new PartialCandidate
            {
                Path = _install.Relativize(entry),
                Reason = reason,
                SizeBytes = Size(entry, isDirectory),
                IsDirectory = isDirectory,
            });
        }

        return new PartialSweepPlan
        {
            Candidates = [.. candidates.OrderBy(candidate => candidate.Path.Value, StringComparer.Ordinal)],
        };
    }

    /// <summary>Removes what a plan chose, and reports what would not go.</summary>
    /// <remarks>
    /// Takes <see cref="TreeLock"/> for the whole pass and does nothing without it, because
    /// <c>unit-&lt;guid&gt;/</c> is a restore's live staging directory and no handle protects it.
    /// The plan is re-checked against the disk under the lock: it was built without one, so a
    /// restore that ran in between has already cleaned up after itself.
    /// </remarks>
    public PartialSweepOutcome Apply(PartialSweepPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using var library = TreeLock.TryAcquireLibrary(_install);
        return library is null ? new PartialSweepOutcome { Skipped = true } : ApplyWithLibrary(plan);
    }

    internal PartialSweepOutcome ApplyWithLibrary(PartialSweepPlan plan)
    {
        using var held = TreeLock.TryAcquire(_install);
        if (held is null)
        {
            return new PartialSweepOutcome { Skipped = true };
        }

        var removed = 0;
        var freed = 0L;
        var problems = new List<string>();

        var claimed = ClaimedRomIds();
        foreach (var candidate in plan.Candidates)
        {
            var absolute = _install.Resolve(candidate.Path);

            try
            {
                if (candidate.Path != Directory.Combine(candidate.Name)
                    || !Path.Exists(absolute)
                    || System.IO.Directory.Exists(absolute) != candidate.IsDirectory
                    || Classify(candidate.Name, candidate.IsDirectory, claimed) != candidate.Reason
                    || File.GetAttributes(absolute).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (candidate.IsDirectory)
                {
                    if (!System.IO.Directory.Exists(absolute))
                    {
                        continue;
                    }

                    System.IO.Directory.Delete(absolute, recursive: true);
                }
                else
                {
                    File.Delete(absolute);
                }

                removed++;
                freed += candidate.SizeBytes;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A transfer running outside the tree lock, holding its partial with
                // FileShare.None. Reported rather than retried: the next pass sees it again,
                // and by then it has either finished or died.
                problems.Add($"{candidate.Name}: left in place ({ex.Message}).");
            }
        }

        return new PartialSweepOutcome { Removed = removed, BytesFreed = freed, Problems = problems };
    }

    /// <summary>Which name shapes are dead, and why. Null means leave it alone.</summary>
    /// <remarks>
    /// Every branch matches the whole name against the shape its producer writes, never a
    /// prefix. A prefix would make <c>partial/save-notes.txt</c> and a directory called
    /// <c>partial/unit-tests/</c> into candidates, which is the opposite of the rule in the
    /// class remarks that a name no producer writes is left alone.
    /// </remarks>
    private static PartialReason? Classify(string name, bool isDirectory, HashSet<int> claimed)
    {
        if (isDirectory && PackedRomStage.TryGetRomId(name, out var packedRom))
        {
            return claimed.Contains(packedRom) ? null : PartialReason.Unclaimed;
        }

        if (IsHexPart(name, BiosPrefix) || IsIdPart(name, SavePrefix) || IsIdPart(name, ResolvePrefix)
            || IsIdPart(name, StatePrefix) || IsUnit(name))
        {
            return PartialReason.Abandoned;
        }

        if (!name.EndsWith(RomSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The whole stem has to be the id. "12abc.part" is not a ROM transfer this wrote, and
        // int.TryParse over a prefix would take it for one.
        var stem = name[..^RomSuffix.Length];

        var separator = stem.IndexOf('-');
        if (separator >= 0)
        {
            if (!int.TryParse(stem.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var owner)
                || !int.TryParse(stem.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var file)
                || owner <= 0 || file <= 0 || name != ContentPlanner.PartFor(owner, file).Name)
            {
                return null;
            }

            return claimed.Contains(owner) ? null : PartialReason.Unclaimed;
        }

        if (!int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var romId))
        {
            return null;
        }

        return claimed.Contains(romId) ? null : PartialReason.Unclaimed;
    }

    /// <summary><c>bios-&lt;md5&gt;.part</c>, the one name <c>BiosPlanner</c> builds.</summary>
    private static bool IsHexPart(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && name.EndsWith(RomSuffix, StringComparison.OrdinalIgnoreCase)
        && IsHex(name.AsSpan()[prefix.Length..^RomSuffix.Length]);

    /// <summary><c>save-&lt;id&gt;.part</c> and <c>resolve-&lt;id&gt;.part</c>, where the id is a save row.</summary>
    private static bool IsIdPart(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && name.EndsWith(RomSuffix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(
            name.AsSpan()[prefix.Length..^RomSuffix.Length],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out _);

    /// <summary>The <c>unit-&lt;guid&gt;</c> staging directory, and the <c>.zip</c> bundle beside it.</summary>
    private static bool IsUnit(string name)
    {
        if (!name.StartsWith(UnitPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = name.EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase)
            ? name.AsSpan()[..^ZipSuffix.Length]
            : name.AsSpan();

        return stem.Length > UnitPrefix.Length && IsHex(stem[UnitPrefix.Length..]);
    }

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        if (value.Length != HexDigits)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Games an enabled set still wants.
    /// </summary>
    /// <remarks>
    /// Current members only. A departed member is the first thing eviction removes, so keeping
    /// its half-transferred bytes against a re-add that may never come is holding disk for a
    /// game the user's own set has already stopped asking for.
    /// </remarks>
    private HashSet<int> ClaimedRomIds()
    {
        var claimed = new HashSet<int>();

        foreach (var set in _store.SyncSets.List().Where(set => set.Enabled))
        {
            foreach (var member in _store.SyncSets.Members(set.Id))
            {
                claimed.Add(member.RomId);
            }
        }

        return claimed;
    }

    private static IEnumerable<string> Entries(string root)
    {
        try
        {
            return [.. System.IO.Directory.EnumerateFileSystemEntries(root)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A directory that cannot be listed is one this pass knows nothing about, which is
            // the same outcome as it not being there.
            return [];
        }
    }

    private static long Size(string entry, bool isDirectory)
    {
        try
        {
            if (!isDirectory)
            {
                return new FileInfo(entry).Length;
            }

            return System.IO.Directory
                .EnumerateFiles(entry, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported as zero rather than skipped. The file is still dead and still worth
            // removing; only the number is unknown.
            return 0;
        }
    }
}
