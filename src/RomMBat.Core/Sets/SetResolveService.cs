using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>How one set's resolve ended.</summary>
public enum ResolveState
{
    /// <summary>The whole scope was walked and the membership is current.</summary>
    Resolved,

    /// <summary>The walk stopped part way. The offset is recorded and the next run continues it.</summary>
    Interrupted,

    /// <summary>The set names a platform whose folder is ambiguous, and none was chosen.</summary>
    NeedsFolderChoice,

    /// <summary>The scope is unbounded and too large to resolve without a cap.</summary>
    Refused,
}

/// <summary>What one set's resolve produced.</summary>
/// <param name="Summary">The sentence describing the outcome, which both front ends show verbatim.</param>
/// <param name="Problem">Set when the resolve was refused or needs a folder.</param>
/// <param name="Offset">Where a resumed walk restarts. Meaningful when Interrupted.</param>
public sealed record ResolveReport(
    string SetName,
    ResolveState State,
    string Summary,
    string? Problem,
    int Offset,
    int Total,
    IReadOnlyList<ExclusionSummary> Exclusions)
{
    /// <summary>True when the membership recorded is now a complete answer.</summary>
    public bool IsComplete => State == ResolveState.Resolved;
}

/// <summary>
/// Walking a set's scope and recording what it holds.
/// </summary>
/// <remarks>
/// <b>This is <c>SetsCommand.ResolveSetsAsync</c> and its <c>Report</c>, with the console
/// taken out.</b> It composes <see cref="SetResolver"/>, <see cref="RomPager"/> and the cursor
/// store; it does not reimplement any of them.
/// <para>
/// <b>A resolve is minutes-long work and the design has to admit it.</b> Measured against a
/// live instance: a platform scope of 9,196 roms walked in <b>8 minutes 15 seconds</b> at 250
/// rows a page. That is why this reports through <see cref="IProgress{T}"/> and why
/// cancellation is a first-class outcome rather than only a failure path: nobody holds a
/// controller for eight minutes, so a cancelled walk is the ordinary case and it must resume.
/// </para>
/// <para>
/// <b>A cancelled walk is Interrupted, not an error.</b> It records its offset exactly as an
/// unreachable server does, so the next resolve continues from there. That is the whole
/// difference between cancelling and losing eight minutes of paging.
/// </para>
/// <para>
/// <b>Only a completed walk retires membership.</b> A segment is an accumulator, not a
/// statement about what the set holds, so a departure is only recorded when the walk finished.
/// Half a walk is not evidence that anything left.
/// </para>
/// </remarks>
public sealed class SetResolveService
{
    private readonly InstallSession _session;
    private readonly RomMConnection _connection;

    public SetResolveService(InstallSession session, RomMConnection connection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connection);

        _session = session;
        _connection = connection;
    }

    /// <summary>Where a set's walk cursor is kept.</summary>
    public static string EndpointFor(SyncSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return $"roms:set:{set.Id}";
    }

    /// <summary>
    /// Walks each set's scope and records what it resolves to.
    /// </summary>
    /// <remarks>
    /// Stops at the first set that could not be reached, because the next one is about to fail
    /// the same way and a user watching wants the reason once.
    /// </remarks>
    public async Task<IReadOnlyList<ResolveReport>> ResolveAsync(
        IReadOnlyList<SyncSetDefinition> sets,
        IProgress<SetResolveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var install = EsSystemsFile.Load(_session.Install);
        var resolver = new SetResolver(
            install,
            new PlatformResolver(install, _session.Store.PlatformMap.Overrides()));

        var reports = new List<ResolveReport>(sets.Count);

        foreach (var set in sets)
        {
            var endpoint = EndpointFor(set);
            var cursor = _session.Store.Cursors.BeginWalk(endpoint, DateTimeOffset.UtcNow);
            var startOffset = cursor.ResumeOffset ?? 0;
            var pager = new RomPager(_connection, SetResolver.QueryFor(set), startOffset: startOffset);

            // Every row this walk writes is stamped with the walk's start, so a segment can
            // tell what this walk has already found from what the last one left behind.
            var walkStartedAt = cursor.WalkStartedAt ?? DateTimeOffset.UtcNow;
            var carried = startOffset > 0
                ? _session.Store.SyncSets.MembersFrom(set.Id, walkStartedAt)
                : [];

            SetResolution resolution;

            try
            {
                resolution = await resolver
                    .ResolveAsync(set, pager, walkStartedAt, carried, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RomMUnreachableException ex)
            {
                _session.Store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, DateTimeOffset.UtcNow);
                reports.Add(Unreachable(set, ex.Message, pager));
                return reports;
            }
            catch (OperationCanceledException)
            {
                // The ordinary way a resolve ends on a handheld. Recorded exactly as an
                // unreachable server is, so the next run continues rather than restarting.
                _session.Store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, DateTimeOffset.UtcNow);
                reports.Add(Cancelled(set, pager));
                throw new SetResolveCancelledException(reports);
            }

            reports.Add(Record(set, resolution, endpoint, pager, walkStartedAt));
        }

        return reports;
    }

    /// <summary>Writes one resolution down and says what it was.</summary>
    private ResolveReport Record(
        SyncSetDefinition set,
        SetResolution resolution,
        string endpoint,
        RomPager pager,
        DateTimeOffset walkStartedAt)
    {
        var now = DateTimeOffset.UtcNow;

        if (resolution.Outcome is ResolutionOutcome.Refused or ResolutionOutcome.NeedsFolderChoice)
        {
            _session.Store.Cursors.AbandonWalk(endpoint, now);

            return new ResolveReport(
                set.Name,
                resolution.Outcome == ResolutionOutcome.Refused
                    ? ResolveState.Refused
                    : ResolveState.NeedsFolderChoice,
                resolution.Summary,
                resolution.Problem,
                pager.Offset,
                pager.Total ?? 0,
                []);
        }

        var complete = resolution.Outcome == ResolutionOutcome.Resolved;

        // A segment of a walk is an accumulator, not a statement about what the set holds, so
        // only a completed walk retires the rows it did not find.
        _session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            walkStartedAt,
            complete);

        // Upserted rather than replaced, and after the membership: a resumed walk only carries
        // metadata for the segment it just read, and the rows an earlier segment wrote are
        // still the only copy of that game's description.
        foreach (var metadata in resolution.Metadata)
        {
            _session.Store.Metadata.Record(metadata);
        }

        if (!complete)
        {
            _session.Store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, now);

            return new ResolveReport(
                set.Name,
                ResolveState.Interrupted,
                resolution.Summary,
                null,
                pager.Offset,
                pager.Total ?? 0,
                _session.Store.SyncSets.Exclusions(set.Id));
        }

        _session.Store.Cursors.CompleteWalk(endpoint, now);

        return new ResolveReport(
            set.Name,
            ResolveState.Resolved,
            resolution.Summary,
            null,
            pager.Offset,
            pager.Total ?? 0,
            _session.Store.SyncSets.Exclusions(set.Id));
    }

    private static ResolveReport Unreachable(SyncSetDefinition set, string message, RomPager pager) =>
        new(set.Name, ResolveState.Interrupted, message, message, pager.Offset, pager.Total ?? 0, []);

    private static ResolveReport Cancelled(SyncSetDefinition set, RomPager pager) =>
        new(
            set.Name,
            ResolveState.Interrupted,
            $"stopped at {pager.Offset} of {pager.Total ?? 0}. The next run continues from there.",
            null,
            pager.Offset,
            pager.Total ?? 0,
            []);
}

/// <summary>
/// A resolve the caller cancelled, carrying what it managed to record.
/// </summary>
/// <remarks>
/// Cancellation has to be observable as cancellation, so the caller can tell "the user pressed
/// back" apart from "the walk finished", while still being able to show where it stopped.
/// Plain <see cref="OperationCanceledException"/> would carry neither.
/// </remarks>
public sealed class SetResolveCancelledException : OperationCanceledException
{
    public SetResolveCancelledException(IReadOnlyList<ResolveReport> reports) =>
        Reports = reports;

    public SetResolveCancelledException()
        : this([])
    {
    }

    public SetResolveCancelledException(string message)
        : base(message) => Reports = [];

    public SetResolveCancelledException(string message, Exception innerException)
        : base(message, innerException) => Reports = [];

    /// <summary>What was recorded before the cancel, one entry per set that was walked.</summary>
    public IReadOnlyList<ResolveReport> Reports { get; } = [];
}
