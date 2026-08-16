using System.Text.Json;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>A play session ready to go up, as it is stored in the outbox payload.</summary>
public sealed record PendingPlaySession(
    long? RomId,
    string? SaveSlot,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime)
{
    /// <summary>Milliseconds played. Always positive: the server refuses anything else.</summary>
    public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;
}

/// <summary>What a correlation pass did.</summary>
/// <param name="Orphans">
/// <c>game-end</c> events with nothing to attribute them to. Discarded, never guessed at.
/// </param>
/// <param name="MenuLaunches">
/// Of those orphans, the ones explained by an ES-menu launch, which includes RomMBat's own
/// exit. Counted separately because they are expected rather than a fault.
/// </param>
public sealed record CorrelationOutcome(
    int Sessions,
    int Orphans,
    int MenuLaunches,
    int Unresolved)
{
    public static CorrelationOutcome Empty { get; } = new(0, 0, 0, 0);

    public bool IsNoOp => Sessions == 0 && Orphans == 0 && Unresolved == 0;
}

/// <summary>
/// Turns journal events plus launch records into play sessions.
/// </summary>
/// <remarks>
/// <b><c>game-end</c> is the trigger and the launch log is the data.</b> The hook that fires on
/// <c>game-end</c> is told nothing at all, so the game it ended has to come from somewhere
/// else, and the <c>game-start</c> hook cannot be that somewhere on its own: it is never told
/// the system, the emulator or the core, and it does not fire for every <c>game-end</c>.
/// <para>
/// <b>An orphan <c>game-end</c> is discarded, not attributed to whatever ran last.</b> ES-menu
/// launches produce one, launches that fail outright produce one, and <b>RomMBat's own exit
/// produces one</b>, since it is launched from that menu. Attributing any of those to the last
/// real game is how a user gets a play session for a game they did not play. M6 measured that
/// the menu case is identifiable rather than merely suspected: 27 launches on a real install
/// carry <c>-system retrobat</c> with a rom under <c>system\es_menu\</c>.
/// </para>
/// <para>
/// <b>The end time is the hook's, never the log's.</b> 187 of 424 launches on the measured
/// install never recorded <c>Process exited with code</c>, so the log cannot say when a
/// session ended even when it says when one began.
/// </para>
/// </remarks>
public sealed class PlaytimeCorrelator
{
    /// <summary>
    /// How far before a <c>game-end</c> a launch may sit and still be its launch.
    /// </summary>
    /// <remarks>
    /// Generous, because a session genuinely can run for hours and the alternative to a long
    /// window is losing a long session. The bound exists so that a <c>game-end</c> with no
    /// launch anywhere near it is discarded rather than matched to something from last week.
    /// </remarks>
    public static TimeSpan LaunchWindow { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Sessions longer than this are recorded but flagged, because <c>game-end</c> cannot say
    /// how a game ended, only that it did.
    /// </summary>
    public static TimeSpan SuspiciouslyLong { get; } = TimeSpan.FromHours(12);

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly LaunchLog _log;
    private readonly TimeProvider _time;

    public PlaytimeCorrelator(
        RetroBatInstall install,
        LocalStore store,
        LaunchLog? log = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _log = log ?? new LaunchLog(install);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Reconciles every open journal entry it can, and queues what it produces.</summary>
    public CorrelationOutcome Correlate()
    {
        var open = _store.Journal.Open();
        if (open.Count == 0)
        {
            return CorrelationOutcome.Empty;
        }

        var now = _time.GetUtcNow();
        var cursor = _store.LaunchCursor.Read();

        // Read from the start of history rather than from the cursor, because a game-end being
        // reconciled now may belong to a launch consumed by an earlier pass. The cursor moves
        // the read position for launches, not the window for matching them.
        var launches = _log.Read();

        var sessions = 0;
        var orphans = 0;
        var menuLaunches = 0;
        var correlated = new List<long>();
        var discarded = new List<long>();

        // Journal order is sequence order, which survives a flat RTC where the wall clock does
        // not, so a game-start is paired with the game-end that followed it rather than with
        // whichever carries the nearest timestamp.
        var openStarts = new Stack<JournalEntry>();

        // A launch answers for exactly one game-end. Without this a second game-end with
        // nothing behind it matches the same launch again and the user is told they played a
        // game twice, which is the naive failure the plan names by hand.
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in open)
        {
            switch (entry.Event)
            {
                case JournalEvent.GameStart:
                    openStarts.Push(entry);
                    break;

                case JournalEvent.GameEnd:
                    var start = openStarts.Count > 0 ? openStarts.Pop() : null;
                    var launch = MatchLaunch(launches, start, entry, consumed);

                    if (launch is not null)
                    {
                        consumed.Add(launch.Signature);
                    }

                    if (launch?.IsMenuLaunch == true)
                    {
                        // An es_menu entry, which includes RomMBat exiting. Not a game.
                        menuLaunches++;
                        orphans++;
                        discarded.Add(entry.Id);
                        if (start is not null)
                        {
                            discarded.Add(start.Id);
                        }

                        break;
                    }

                    var session = Build(start, entry, launch);

                    if (session is null)
                    {
                        orphans++;
                        discarded.Add(entry.Id);
                        if (start is not null)
                        {
                            discarded.Add(start.Id);
                        }

                        break;
                    }

                    Enqueue(session, now);
                    sessions++;
                    correlated.Add(entry.Id);
                    if (start is not null)
                    {
                        correlated.Add(start.Id);
                    }

                    break;

                case JournalEvent.Start:
                case JournalEvent.Quit:
                    // The heartbeat and the flush trigger. Neither produces a session, and
                    // both are closed so they stop being reconsidered every pass.
                    correlated.Add(entry.Id);
                    break;

                default:
                    break;
            }
        }

        // A game-start with no game-end yet is a game that is still running. Left open, which
        // is also what stops SaveGuard evicting it mid-session.
        var unresolved = openStarts.Count;

        _store.Journal.Close(correlated, JournalState.Correlated, now);
        _store.Journal.Close(discarded, JournalState.Discarded, now);

        _store.LaunchCursor.Write(_log.PositionAfter(launches, cursor), now);

        return new CorrelationOutcome(sessions, orphans, menuLaunches, unresolved);
    }

    /// <summary>
    /// Finds the launch a <c>game-end</c> belongs to.
    /// </summary>
    /// <remarks>
    /// The <c>game-start</c> hook's rom path is used to corroborate rather than to decide,
    /// because it carries no system, emulator or core and because it is absent for exactly the
    /// cases that most need explaining. The launch log carries all three.
    /// <para>
    /// <b>A launch already claimed by an earlier <c>game-end</c> is not a candidate.</b> Without
    /// that, a second <c>game-end</c> with nothing behind it matches the same launch and the
    /// user is told they played a game they played once twice.
    /// </para>
    /// </remarks>
    private static LaunchRecord? MatchLaunch(
        IReadOnlyList<LaunchRecord> launches,
        JournalEntry? start,
        JournalEntry end,
        HashSet<string> consumed)
    {
        var earliest = end.RecordedAtUtc - LaunchWindow;

        var candidates = launches
            .Where(launch => launch.At <= end.RecordedAtUtc && launch.At >= earliest)
            .Where(launch => !consumed.Contains(launch.Signature))
            .OrderByDescending(launch => launch.At)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // When the hook did open a record, prefer the launch naming the same rom: two games
        // launched close together are otherwise indistinguishable by time alone.
        if (start?.RomPath is { } romPath)
        {
            var exact = candidates.FirstOrDefault(launch => launch.RomPath == romPath);
            if (exact is not null)
            {
                return exact;
            }
        }

        // Otherwise the most recent launch before the end. A menu launch reaching here is
        // still recognised as one and discarded by the caller.
        return candidates[0];
    }

    private PendingPlaySession? Build(JournalEntry? start, JournalEntry end, LaunchRecord? launch)
    {
        var romPath = start?.RomPath ?? launch?.RomPath;

        if (romPath is not { } path)
        {
            // Nothing names a game. rom_id is genuinely optional on the endpoint, which is not
            // a licence to send a session without one: an unattributed session is not useful.
            return null;
        }

        if (ResolveRomId(path) is not { } romId)
        {
            return null;
        }

        // The launch is when the game started, not when the hook fired, because a game-start
        // hook may not have fired at all. When both exist the hook is earlier and is used.
        var startedAt = start?.RecordedAtUtc ?? launch?.At;

        if (startedAt is not { } from || from >= end.RecordedAtUtc)
        {
            // end_time must be strictly after start_time, enforced server-side with a 422.
            // A clock that went backwards mid-session lands here and is discarded rather than
            // sent to be refused.
            return null;
        }

        return new PendingPlaySession(romId, launch?.Emulator is { } emulator ? $"{emulator}:battery" : null, from, end.RecordedAtUtc);
    }

    private long? ResolveRomId(RelativePath romPath)
    {
        var file = _store.Files.List().FirstOrDefault(entry => entry.Path == romPath);
        return file?.RomId;
    }

    private void Enqueue(PendingPlaySession session, DateTimeOffset now)
    {
        // The payload rather than columns, because a play session is not a file and the outbox
        // columns describe one. Serialised with the property names the endpoint wants, so the
        // flush hands it over rather than rebuilding it.
        var payload = JsonSerializer.Serialize(session);

        _store.Outbox.Enqueue(
            OutboxKind.PlaySession,
            now,
            romId: session.RomId,
            slot: session.SaveSlot,
            payload: payload);
    }
}
