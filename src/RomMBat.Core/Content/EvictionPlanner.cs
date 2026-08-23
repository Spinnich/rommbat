using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>Why a file is a candidate for removal.</summary>
public enum EvictionReason
{
    /// <summary>It left the set's scope server-side. The first thing to go, and the least surprising.</summary>
    Departed,

    /// <summary>No enabled set claims it any more.</summary>
    Orphaned,

    /// <summary>It is still a member, but the lowest-ranked one that still fits nothing.</summary>
    LowestRanked,
}

/// <summary>One file eviction would remove, or would have removed.</summary>
public sealed record EvictionCandidate
{
    public required LocalFile File { get; init; }

    public required EvictionReason Reason { get; init; }

    /// <summary>
    /// The artwork, video and manual that go with it.
    /// </summary>
    /// <remarks>
    /// Media follows its ROM out, or the install accumulates orphaned covers no gamelist
    /// references. Only files RomMBat downloaded are here: a user's own scrape writes to the
    /// same names and is never a candidate.
    /// </remarks>
    public IReadOnlyList<LocalFile> Media { get; init; } = [];

    /// <summary>Which set it belonged to, when one still does.</summary>
    public string? SetName { get; init; }

    /// <summary>Its rank in that set, which is what orders candidates sharing a reason.</summary>
    public int? Position { get; init; }

    /// <summary>What removing this candidate frees, the ROM and its media together.</summary>
    public long Bytes => File.SizeBytes + Media.Sum(file => file.SizeBytes);

    /// <summary>Set when something refused to let this one go.</summary>
    public string? Refusal { get; init; }

    public bool IsRefused => Refusal is not null;
}

/// <summary>What eviction would do, in the order it would do it.</summary>
public sealed record EvictionPlan
{
    /// <summary>How much has to go for the install to be inside its budget again.</summary>
    public long BytesToFree { get; init; }

    /// <summary>What would be removed, in order.</summary>
    public IReadOnlyList<EvictionCandidate> Selected { get; init; } = [];

    /// <summary>What was passed over, and why.</summary>
    public IReadOnlyList<EvictionCandidate> Refused { get; init; } = [];

    public long BytesFreed => Selected.Sum(candidate => candidate.Bytes);

    /// <summary>True when nothing needs to go.</summary>
    public bool IsEmpty => Selected.Count == 0;

    /// <summary>True when everything available was selected and it still is not enough.</summary>
    public bool IsShort => BytesFreed < BytesToFree;

    public string Summary
    {
        get
        {
            if (BytesToFree <= 0)
            {
                return "nothing to evict: the install is inside its budget";
            }

            if (IsEmpty)
            {
                return $"{ByteSize.Format(BytesToFree)} over budget, and nothing can be removed"
                    + (Refused.Count > 0 ? $": {Refused.Count} candidates were held back" : string.Empty);
            }

            var text = $"{Selected.Count} {Games(Selected.Count)}, {ByteSize.Format(BytesFreed)}, "
                + $"to get back inside a budget exceeded by {ByteSize.Format(BytesToFree)}";

            if (Refused.Count > 0)
            {
                text += $"; {Refused.Count} held back";
            }

            return IsShort ? text + " (still short)" : text;
        }
    }

    /// <summary>One game is not "1 games". Counts land in front of users here.</summary>
    internal static string Games(int count) => count == 1 ? "game" : "games";
}

/// <summary>
/// Chooses what to remove when the install is over its budget, and refuses to remove the wrong
/// things.
/// </summary>
/// <remarks>
/// <b>Two rules override every ordering.</b> A file RomMBat did not download is never removed:
/// an adopted file is the user's own, and deleting it would be destroying data the app was only
/// ever asked to catalogue. And a game with unflushed local saves is never removed, whatever
/// the budget says, which <see cref="SaveGuard"/> answers and fails closed on.
/// <para>
/// The order within what is left is departed first, then orphaned, then the lowest-ranked
/// members. Departure is the least surprising removal, because the game has already left the
/// scope the user chose; taking a current member is the last resort and takes the one the set's
/// own ordering values least.
/// </para>
/// <para>
/// <b>The <c>keep_favourites</c> policy is inert in this milestone and deliberately not
/// pretended otherwise.</b> RomM has no <c>is_favorite</c> property on a ROM: favourites are
/// membership of a collection, which cannot be answered offline and is not carried on the
/// membership row. The same is true of "keep the last N played", which needs the play sessions
/// M6 owns. Both need a field this schema does not yet hold, and a planner that silently
/// ignored a policy the user set would be worse than one that says so.
/// </para>
/// </remarks>
public sealed class EvictionPlanner
{
    private readonly LocalStore _store;
    private readonly SaveGuard _guard;

    public EvictionPlanner(LocalStore store, SaveGuard? guard = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _guard = guard ?? new SaveGuard(store);
    }

    /// <summary>
    /// How much is over budget right now, or zero.
    /// </summary>
    /// <remarks>
    /// Measured against downloaded content only, for the same reason the planner is: a user's
    /// own library is not RomMBat's to count or to free.
    /// </remarks>
    public long BytesOverBudget()
    {
        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        if (budget is not { } cap)
        {
            return 0;
        }

        var managed = _store.Files.List().Where(file => file.Origin == FileOrigin.Synced).Sum(file => file.SizeBytes);
        return Math.Max(0, managed - cap);
    }

    /// <summary>Chooses what would go, without touching anything.</summary>
    /// <param name="bytesToFree">How much to free. Defaults to whatever is over budget.</param>
    public EvictionPlan Plan(long? bytesToFree = null)
    {
        var target = bytesToFree ?? BytesOverBudget();

        // Nothing to free is the ordinary case, and the walk below is not free: it reads every
        // downloaded file and asks SaveGuard about each, which is four queries against the
        // outbox, the journal and the two save tables. The cost scaled with the size of the
        // local library rather than with the amount to remove. Summary answers from
        // BytesToFree rather than from the candidate lists, so an empty plan here still reads
        // as "inside its budget" and never as "found no candidates".
        if (target <= 0)
        {
            return new EvictionPlan { BytesToFree = target };
        }

        var ordered = Candidates().ToList();
        var selected = new List<EvictionCandidate>();
        var refused = new List<EvictionCandidate>();
        var freed = 0L;

        foreach (var candidate in ordered)
        {
            if (freed >= target)
            {
                break;
            }

            var verdict = _guard.Check(candidate.File.RomId ?? 0, candidate.File.Path);

            if (!verdict.CanRemove)
            {
                refused.Add(candidate with { Refusal = verdict.Reason });
                continue;
            }

            selected.Add(candidate);
            freed += candidate.Bytes;
        }

        return new EvictionPlan
        {
            BytesToFree = target,
            Selected = selected,
            Refused = refused,
        };
    }

    /// <summary>
    /// Removes what a plan chose.
    /// </summary>
    /// <remarks>
    /// The guard is asked again for every file, because a plan can be shown to a user, sat on,
    /// and applied later, and a game played in between must not be evicted on the strength of
    /// an answer given before it was.
    /// </remarks>
    public EvictionOutcome Apply(EvictionPlan plan, RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(install);

        var removed = 0;
        var freed = 0L;
        var problems = new List<string>();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in plan.Selected)
        {
            var verdict = _guard.Check(candidate.File.RomId ?? 0, candidate.File.Path);
            if (!verdict.CanRemove)
            {
                problems.Add($"{candidate.File.FileName}: kept, because {verdict.Reason}");
                continue;
            }

            if (candidate.File.Origin != FileOrigin.Synced)
            {
                problems.Add($"{candidate.File.FileName}: kept, because RomMBat did not download it.");
                continue;
            }

            try
            {
                freed += Delete(candidate.File, install);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The row stays when the file could not go, so the inventory never claims to
                // have removed something that is still on the disk.
                problems.Add($"{candidate.File.FileName}: could not be removed ({ex.Message}).");
                continue;
            }

            // The ROM is gone, so the folder's gamelist names a game this install no longer
            // has however the media below fares. Counted and queued here rather than after
            // the media loop, so one locked image cannot cost the folder its rewrite.
            removed++;
            if (candidate.File.Folder is { } folder)
            {
                folders.Add(folder);
            }

            // The media goes with it. Each is checked for origin again rather than trusted
            // from the plan: a file the user replaced between the dry run and this call is
            // theirs now.
            foreach (var media in candidate.Media.Where(file => file.Origin == FileOrigin.Synced))
            {
                try
                {
                    freed += Delete(media, install);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Reported per file and the rest still go. Its row stays, so the next
                    // pass sees it again.
                    problems.Add($"{media.FileName}: left behind ({ex.Message}).");
                }
            }
        }

        return new EvictionOutcome
        {
            Removed = removed,
            BytesFreed = freed,
            Problems = problems,

            // The gamelists that now name games this install no longer has. Reported rather
            // than rewritten here, so eviction stays a thing that deletes files and the
            // gamelist writer stays the one thing that writes gamelists.
            FoldersToRewrite = [.. folders.Order(StringComparer.OrdinalIgnoreCase)],
        };
    }

    /// <summary>Removes one file and its row, and reports what that freed.</summary>
    private long Delete(LocalFile file, RetroBatInstall install)
    {
        var absolute = install.Resolve(file.Path);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        _store.Files.Remove(file.Path);
        return file.SizeBytes;
    }

    /// <summary>Everything that could go, worst first.</summary>
    private IEnumerable<EvictionCandidate> Candidates()
    {
        // Only what RomMBat downloaded. An adopted file is the user's own and is never a
        // candidate, which is also why it never counted towards the budget.
        var synced = _store.Files.List()
            .Where(file => file.Origin == FileOrigin.Synced && file.RomId is not null)
            .ToList();

        // Keyed on the ROM row specifically. Since M4 a ROM can have six rows, five of them
        // media, and keying on rom_id alone would throw on the second.
        var files = synced
            .Where(file => file.Kind == LocalFileKind.Rom)
            .ToDictionary(file => file.RomId!.Value);

        var mediaByRom = synced
            .Where(file => file.Kind != LocalFileKind.Rom)
            .GroupBy(file => file.RomId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LocalFile>)[.. group]);

        var claims = new Dictionary<int, (string SetName, MemberState State, int? Position)>();

        foreach (var set in _store.SyncSets.List().Where(set => set.Enabled))
        {
            foreach (var member in _store.SyncSets.Members(set.Id, state: null))
            {
                if (!files.ContainsKey(member.RomId))
                {
                    continue;
                }

                // A ROM in two sets is kept for the best claim either of them makes, so a set
                // being trimmed cannot take a game another set still wants near the top.
                if (claims.TryGetValue(member.RomId, out var existing)
                    && Rank(existing.State, existing.Position) <= Rank(member.State, member.Position))
                {
                    continue;
                }

                claims[member.RomId] = (set.Name, member.State, member.Position);
            }
        }

        var candidates = new List<EvictionCandidate>();

        foreach (var (romId, file) in files)
        {
            var media = mediaByRom.GetValueOrDefault(romId, []);

            if (!claims.TryGetValue(romId, out var claim))
            {
                candidates.Add(new EvictionCandidate
                {
                    File = file,
                    Media = media,
                    Reason = EvictionReason.Orphaned,
                });
                continue;
            }

            candidates.Add(new EvictionCandidate
            {
                File = file,
                Media = media,
                Reason = claim.State == MemberState.Member ? EvictionReason.LowestRanked : EvictionReason.Departed,
                SetName = claim.SetName,
                Position = claim.Position,
            });
        }

        // Departed and orphaned first, then members from the bottom of their set's own order.
        // The ROM id breaks every tie, so the same install always answers the same way and a
        // preview matches the run that follows it.
        return candidates
            .OrderBy(candidate => candidate.Reason switch
            {
                EvictionReason.Departed => 0,
                EvictionReason.Orphaned => 1,
                _ => 2,
            })
            .ThenByDescending(candidate => candidate.Position ?? int.MaxValue)
            .ThenBy(candidate => candidate.File.RomId ?? 0);
    }

    /// <summary>Lower is a stronger claim to stay.</summary>
    private static int Rank(MemberState state, int? position) =>
        state == MemberState.Member ? position ?? int.MaxValue - 1 : int.MaxValue;
}

/// <summary>What an eviction actually removed.</summary>
public sealed record EvictionOutcome
{
    public int Removed { get; init; }

    public long BytesFreed { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>Folders whose gamelist now names a game that is gone.</summary>
    public IReadOnlyList<string> FoldersToRewrite { get; init; } = [];

    public string Summary =>
        Removed == 0 && Problems.Count == 0
            ? "nothing was removed"
            : $"{Removed} {EvictionPlan.Games(Removed)} removed, {ByteSize.Format(BytesFreed)} freed"
                + (Problems.Count > 0 ? $", {Problems.Count} kept" : string.Empty);
}
