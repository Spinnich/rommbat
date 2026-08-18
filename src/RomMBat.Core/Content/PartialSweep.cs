using System.Globalization;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

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

    public string Summary => Removed == 0
        ? "nothing was reclaimed from partial/"
        : $"{Removed} abandoned {(Removed == 1 ? "transfer" : "transfers")} removed, "
            + $"{ByteSize.Format(BytesFreed)} freed";
}

/// <summary>
/// Reclaims <c>emulators/rommbat/partial/</c>, which neither of M3's two bounds can see.
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
/// <b>Five producers write here now, not the one this was first described against, and they
/// die differently.</b> Only the ROM transfer resumes, so only it has live state to protect;
/// the other four open with <c>FileMode.Create</c> or delete in a <c>finally</c>, so anything
/// of theirs that outlives its process is from a pass that died.
/// </para>
/// <list type="bullet">
/// <item><c>&lt;rom id&gt;.part</c>, from <see cref="ContentSync"/>. <b>Kept while an enabled
/// set still claims that game</b>, whether or not a <c>content_download</c> row exists, because
/// an interrupted transfer waiting to resume looks exactly like an orphan on disk. Keyed on set
/// membership rather than on age, so a slow transfer is never mistaken for a dead one.</item>
/// <item><c>bios-&lt;md5&gt;.part</c>, from <see cref="BiosSync"/>. No <c>content_download</c>
/// row exists or is wanted, since the fetch never resumes.</item>
/// <item><c>save-&lt;id&gt;.part</c> and <c>resolve-&lt;id&gt;.part</c>, from the save
/// download and the conflict resolver.</item>
/// <item><c>unit-&lt;guid&gt;.zip</c> and the <c>unit-&lt;guid&gt;/</c> staging directory,
/// from <see cref="SaveUnitTransfer"/>.</item>
/// </list>
/// <para>
/// <b>A transfer running right now is protected by the filesystem, not by bookkeeping.</b>
/// Every producer holds its partial with <c>FileShare.None</c>, so deleting one under an
/// in-flight pass throws and the sweep skips that file and reports it. That is what makes this
/// safe without the tree lock, which only <c>flush</c> takes.
/// </para>
/// <para>
/// <b>Anything else in the directory is left alone.</b> A name this class does not recognise
/// was not written by a producer it knows about, and deleting on the strength of "it is in a
/// directory we own" is how a sweep destroys something it was never asked to judge.
/// </para>
/// </remarks>
public sealed class PartialSweep
{
    private const string RomSuffix = ".part";
    private const string BiosPrefix = "bios-";
    private const string SavePrefix = "save-";
    private const string ResolvePrefix = "resolve-";
    private const string UnitPrefix = "unit-";

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
    public static RelativePath Directory { get; } = RetroBatInstall.AppDirectory.Combine("partial");

    /// <summary>Works out what is dead, without touching anything.</summary>
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
            if (Classify(Path.GetFileName(entry), claimed) is not { } reason)
            {
                continue;
            }

            var isDirectory = System.IO.Directory.Exists(entry);

            if (!_install.Contains(entry))
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
    public PartialSweepOutcome Apply(PartialSweepPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var removed = 0;
        var freed = 0L;
        var problems = new List<string>();

        foreach (var candidate in plan.Candidates)
        {
            var absolute = _install.Resolve(candidate.Path);

            try
            {
                if (candidate.IsDirectory)
                {
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
                // Almost always a transfer in flight, since every producer holds its partial
                // with FileShare.None. Reported rather than retried: the next pass sees it
                // again, and by then it has either finished or died.
                problems.Add($"{candidate.Name}: left in place ({ex.Message}).");
            }
        }

        return new PartialSweepOutcome { Removed = removed, BytesFreed = freed, Problems = problems };
    }

    /// <summary>Which name shapes are dead, and why. Null means leave it alone.</summary>
    private static PartialReason? Classify(string name, HashSet<int> claimed)
    {
        if (name.StartsWith(BiosPrefix, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(SavePrefix, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(ResolvePrefix, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(UnitPrefix, StringComparison.OrdinalIgnoreCase))
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

        if (!int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var romId))
        {
            return null;
        }

        return claimed.Contains(romId) ? null : PartialReason.Unclaimed;
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
