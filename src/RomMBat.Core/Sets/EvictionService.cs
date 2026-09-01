using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sets;

/// <summary>What eviction would do, before anything is done.</summary>
/// <param name="Abandoned">Dead transfers under <c>partial/</c>, which neither bound can see.</param>
/// <param name="HasBudget">
/// False when no disk budget is set, which is why nothing is over it. The remedy names a
/// subcommand on one front end and a screen on the other, so it is the caller's to word.
/// </param>
public sealed record EvictionReport(
    PartialSweepPlan Abandoned,
    EvictionPlan Plan,
    bool HasBudget)
{
    /// <summary>True when there is nothing to free and nothing to reclaim.</summary>
    public bool IsEmpty => Abandoned.IsEmpty && Plan.BytesToFree <= 0;
}

/// <summary>What eviction actually did.</summary>
/// <param name="Swept">
/// The <c>partial/</c> sweep. <see cref="PartialSweepOutcome.Skipped"/> is set when another
/// RomMBat pass held the tree lock, which is an ordinary outcome carrying its own sentence.
/// </param>
/// <param name="Evicted">Null when the plan had nothing to free.</param>
/// <param name="Gamelists">Null when no folder needed rewriting.</param>
public sealed record EvictionApplied(
    PartialSweepOutcome? Swept,
    EvictionOutcome? Evicted,
    GamelistSyncOutcome? Gamelists);

/// <summary>
/// Freeing space, showing what would go before anything goes.
/// </summary>
/// <remarks>
/// <b>This is <c>EvictCommand</c>'s orchestration with the console taken out.</b> It composes
/// <see cref="StateScanner"/>, <see cref="SaveScanner"/>, <see cref="EvictionPlanner"/>,
/// <see cref="PartialSweep"/> and <see cref="GamelistSync"/>; none of them is reimplemented.
/// <para>
/// <b>The tree lock is not taken here and that is unchanged behaviour.</b>
/// <see cref="PartialSweep.Apply"/> takes it around its own deletions, because one of the
/// things it would delete is a class C restore's staging directory, and it returns
/// <see cref="PartialSweepOutcome.Skipped"/> with the sentence for it when it cannot. That is
/// already the pattern a UI needs, so nothing new is invented for it: a UI-initiated eviction
/// while a background pass holds the lock evicts what it can and reports that <c>partial/</c>
/// was left alone. A test asserts exactly that.
/// </para>
/// </remarks>
public sealed class EvictionService
{
    private readonly InstallSession _session;

    public EvictionService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// Scans what is on disk and works out what would go.
    /// </summary>
    /// <remarks>
    /// <b>The two scans happen here, before anything is planned</b>, because
    /// <see cref="SaveGuard"/> reads <c>local_save</c> and <c>local_state</c> and nothing else
    /// refreshes either. Without them the guard answers from the last flush: play a game,
    /// evict, and the ROM goes while the save it just wrote is still unsent. The save then
    /// survives as bytes with no ROM to attribute it to, which is permanent.
    /// <para>
    /// <b>States first, then saves, and the order is load-bearing.</b> The sidecar attribution
    /// route reads <c>local_state</c> and <see cref="SaveScanner"/> is what runs it, so
    /// scanning saves first left the route seeing an empty table on any tree whose states had
    /// not been recorded yet, and a class C unit went unattributed until a second invocation.
    /// See #64.
    /// </para>
    /// </remarks>
    public EvictionReport Preview(long? bytesToFree = null)
    {
        var schema = StateScanner.LoadSchema(_session.Install);

        if (schema is not null)
        {
            new StateScanner(_session.Install, _session.Store, schema).Scan();
        }

        new SaveScanner(_session.Install, _session.Store, states: schema).Scan();

        return new EvictionReport(
            new PartialSweep(_session.Install, _session.Store).Plan(),
            new EvictionPlanner(_session.Store).Plan(bytesToFree),
            _session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes) is not null);
    }

    /// <summary>
    /// What removing named games would do, before anything is done.
    /// </summary>
    /// <remarks>
    /// <b>The first thing RomMBat removes because a person asked for it.</b>
    /// <see cref="Preview"/> answers "what should go to get back inside a budget", which is the
    /// question the ruling that took eviction off the interface says RomMBat should not be
    /// answering on its own. This one answers "can these named games go", and the user has
    /// already decided.
    /// <para>
    /// <b>The two scans run here for exactly the reason <see cref="Preview"/>'s do</b>, and the
    /// order is as load-bearing: <see cref="SaveGuard"/> reads <c>local_save</c> and
    /// <c>local_state</c>, and without a scan it answers from the last flush, so a game played
    /// since could be removed while the save it wrote is still unsent. States before saves,
    /// because the sidecar attribution route reads <c>local_state</c>. See #64.
    /// </para>
    /// <para>
    /// <b><c>partial/</c> is deliberately not swept here.</b> A removal that also collected
    /// dead transfers would be doing work the user did not ask for on a path whose whole point
    /// is that they named what goes. <see cref="ApplyAsync"/> skips an empty sweep plan, so
    /// nothing extra happens.
    /// </para>
    /// </remarks>
    /// <param name="romIds">The games to take off this device.</param>
    /// <param name="releasing">
    /// Sets whose claim on those games is being given up, because they are what the games are
    /// being removed from. Every other enabled set's claim still holds a game back.
    /// </param>
    public EvictionReport PreviewRemoval(IReadOnlyList<int> romIds, IReadOnlyList<long>? releasing = null)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        var schema = StateScanner.LoadSchema(_session.Install);

        if (schema is not null)
        {
            new StateScanner(_session.Install, _session.Store, schema).Scan();
        }

        new SaveScanner(_session.Install, _session.Store, states: schema).Scan();

        return new EvictionReport(
            new PartialSweepPlan(),
            new EvictionPlanner(_session.Store).PlanRemoval(romIds, releasing),
            _session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes) is not null);
    }

    /// <summary>
    /// Containers this removal cannot vouch for, named rather than guarded.
    /// </summary>
    /// <remarks>
    /// <b>A class D shared container has no <c>rom_id</c> by definition</b>, and a class C unit
    /// whose attribution failed has a null one, so <see cref="SaveGuard"/> cannot attribute
    /// either to the game being removed and cannot answer for it. A PS2 memory card is the case
    /// that exists.
    /// <para>
    /// <b>Named, and no safety is claimed.</b> Nothing here is deleted, because removal walks
    /// <c>local_file</c> and that table has no save kind, so the container survives the removal
    /// whatever this says. What it cannot survive is the attribution: the ROM going takes with
    /// it the only thing that could ever say which game those bytes belong to. The user decides.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Unvouchable(IReadOnlyList<int> romIds)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        if (romIds.Count == 0)
        {
            return [];
        }

        var systems = _session.Store.SyncSets.List()
            .SelectMany(set => _session.Store.SyncSets.Members(set.Id, state: null))
            .Where(member => romIds.Contains(member.RomId))
            .Select(member => member.Folder)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. _session.Store.Saves.List()
                .Where(save => save.RomId is null && systems.Contains(save.System))
                .Select(save => save.Path.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Carries out a report: sweeps <c>partial/</c>, removes what was selected, rewrites lists.
    /// </summary>
    /// <remarks>
    /// The sweep runs first and unconditionally, including when eviction has nothing to do.
    /// Those bytes carry no <c>local_file</c> row, so the budget cannot count them and they are
    /// gone from free space attributed to nothing: an install inside its budget with dead
    /// transfers under <c>partial/</c> has nothing to evict and space to reclaim.
    /// <para>
    /// A gamelist that still names a removed game is inert, since EmulationStation does not
    /// list an entry whose file is missing, but it survives ES's own rewrite and would sit
    /// there forever. Rewriting needs no server: the entry and its metadata are both local.
    /// </para>
    /// </remarks>
    public async Task<EvictionApplied> ApplyAsync(
        EvictionReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sweep = new PartialSweep(_session.Install, _session.Store);
        var swept = report.Abandoned.IsEmpty ? null : sweep.Apply(report.Abandoned);

        // Nothing selected rather than nothing over budget, which are the same thing on the
        // budget path and are not on the removal one: a removal's BytesToFree is what the
        // removal frees, so a plan that every guard refused is the case this skips.
        if (report.Plan.Selected.Count == 0)
        {
            return new EvictionApplied(swept, null, null);
        }

        var evicted = new EvictionPlanner(_session.Store).Apply(report.Plan, _session.Install);

        if (evicted.FoldersToRewrite.Count == 0)
        {
            return new EvictionApplied(swept, evicted, null);
        }

        using var emulationStation = new EmulationStationClient();

        var gamelists = await new GamelistSync(_session.Install, _session.Store)
            .ApplyAsync(evicted.FoldersToRewrite, emulationStation, cancellationToken)
            .ConfigureAwait(false);

        return new EvictionApplied(swept, evicted, gamelists);
    }

    /// <summary>Why a dead transfer is a candidate, as a person would say it.</summary>
    public static string Describe(PartialCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Reason switch
        {
            PartialReason.Unclaimed => "no set wants it",
            _ => "transfer died",
        };
    }

    /// <summary>Why a game is a candidate, as a person would say it.</summary>
    public static string Describe(EvictionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Reason switch
        {
            EvictionReason.Departed => $"left {candidate.SetName ?? "its set"}",
            EvictionReason.Orphaned => "in no set",
            _ => candidate.Position is { } position
                ? $"#{position} in {candidate.SetName}"
                : $"in {candidate.SetName}",
        };
    }
}
