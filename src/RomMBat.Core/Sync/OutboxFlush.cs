using System.Text.Json;
using RomM.Client;
using RomM.Client.Saves;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What a flush of the outbox did.</summary>
public sealed record OutboxFlushOutcome(int Sent, int Duplicates, int Failed, IReadOnlyList<string> Problems)
{
    public static OutboxFlushOutcome Empty { get; } = new(0, 0, 0, []);

    public bool IsNoOp => Sent == 0 && Duplicates == 0 && Failed == 0;

    public string Summary => IsNoOp
        ? "playtime: nothing queued"
        : $"playtime: {Sent} sent" + (Duplicates > 0 ? $", {Duplicates} already known" : string.Empty)
            + (Failed > 0 ? $", {Failed} still queued" : string.Empty);
}

/// <summary>
/// Sends what the outbox holds, in batches the server will accept.
/// </summary>
/// <remarks>
/// <b>A failed entry stays pending.</b> Being offline is the normal case and a later replay is
/// safe, so the only thing an attempt costs is a counter. This is what makes a week away from
/// the server a bigger payload rather than a lost week.
/// <para>
/// <b>The result array is read per index rather than inferred from the counts.</b> A replayed
/// session comes back <c>"duplicate"</c>, so a batch where half the entries were already known
/// is reconciled exactly: each entry is marked sent or left pending on its own answer. Reading
/// only <c>created_count</c> would leave the client guessing which half was which.
/// </para>
/// <para>
/// <b>Playtime needs no open sync session</b>, so this runs whether or not there is anything to
/// negotiate. That is the case an agent woken by <c>game-end</c> is usually in.
/// </para>
/// </remarks>
public sealed class OutboxFlush
{
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly string _deviceId;
    private readonly TimeProvider _time;

    public OutboxFlush(LocalStore store, RomMConnection connection, string deviceId, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        _store = store;
        _connection = connection;
        _deviceId = deviceId;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Drains queued play sessions.</summary>
    public async Task<OutboxFlushOutcome> FlushPlaySessionsAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        var duplicates = 0;
        var failed = 0;
        var problems = new List<string>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The server's own cap, measured: 101 entries answer 400. A long offline binge is
            // several batches and each one is independently replayable.
            var pending = _store.Outbox
                .Pending(RomMConnection.PlaySessionBatchLimit, OutboxKind.PlaySession)
                .ToList();

            if (pending.Count == 0)
            {
                break;
            }

            var entries = new List<PlaySessionEntry>(pending.Count);
            var indexed = new List<OutboxEntry>(pending.Count);

            foreach (var entry in pending)
            {
                if (Parse(entry) is not { } session)
                {
                    // A payload this build cannot read. Marked sent rather than retried
                    // forever: it will never become readable and a stuck queue blocks
                    // everything behind it.
                    _store.Outbox.MarkSent(entry.Id, _time.GetUtcNow());
                    problems.Add($"outbox entry {entry.Id} could not be read and was dropped.");
                    continue;
                }

                entries.Add(session);
                indexed.Add(entry);
            }

            if (entries.Count == 0)
            {
                continue;
            }

            RomMResponse<PlaySessionOutcome> response;

            try
            {
                response = await _connection
                    .SendPlaySessionsAsync(new PlaySessionBatch(_deviceId, entries), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RomMUnreachableException ex)
            {
                // Offline is a working state. Everything stays pending.
                RecordFailure(indexed, ex.Message);
                return new OutboxFlushOutcome(sent, duplicates, indexed.Count, [.. problems, ex.Message]);
            }

            if (!response.IsSuccess || response.Value is not { } outcome)
            {
                RecordFailure(indexed, response.Message ?? "the server refused the batch.");
                return new OutboxFlushOutcome(
                    sent,
                    duplicates,
                    indexed.Count,
                    [.. problems, response.Message ?? "the server refused the batch."]);
            }

            var (batchSent, batchDuplicates, batchFailed) = Reconcile(indexed, outcome, problems);

            sent += batchSent;
            duplicates += batchDuplicates;
            failed += batchFailed;

            await ClearNowPlayingAsync(entries, cancellationToken).ConfigureAwait(false);

            if (batchFailed == indexed.Count)
            {
                // Nothing moved, so another pass would ask the same question and get the same
                // answer. Stop rather than spin.
                break;
            }
        }

        return new OutboxFlushOutcome(sent, duplicates, failed, problems);
    }

    /// <summary>
    /// Tells the server this device has stopped playing what it just reported.
    /// </summary>
    /// <remarks>
    /// <b>Because ingesting a session sets <c>now_playing</c> and nothing else clears it.</b>
    /// Every session in this batch carries an <c>end_time</c>: it is over by construction, and
    /// leaving the flag set makes the user's library claim they are playing every game they have
    /// ever launched. Measured on the live instance during M7 stage 7b-3's hands-on pass: ten
    /// roms all reading <c>now_playing=true</c>, one of them played two days earlier, against a
    /// rom RomMBat had never reported reading false.
    /// <para>
    /// <b>Best effort, and it never fails the flush.</b> The sessions are already accepted and
    /// recorded by the time this runs; a tidy-up that could undo that would be worse than the
    /// untidiness. Offline is the ordinary case and is silent, for the same reason.
    /// </para>
    /// <para>
    /// <b>Once per distinct rom, not once per session.</b> A person who played the same game
    /// four times has four entries in one batch, and the flag is per rom.
    /// </para>
    /// </remarks>
    private async Task ClearNowPlayingAsync(
        List<PlaySessionEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var romId in entries
            .Select(entry => entry.RomId)
            .OfType<int>()
            .Distinct())
        {
            try
            {
                await _connection.ClearNowPlayingAsync(romId, cancellationToken).ConfigureAwait(false);
            }
            catch (RomMUnreachableException)
            {
                // The server went away between the batch and the tidy-up. The sessions are
                // sent, which is what mattered; the flag is corrected by the next flush.
                return;
            }
        }
    }

    /// <summary>
    /// Marks each entry on its own answer, from the per-index array.
    /// </summary>
    /// <remarks>
    /// The reason the endpoint's response shape matters: without it, a batch of 100 where 40
    /// were duplicates and 3 genuinely failed would either be marked wholly sent, losing three,
    /// or wholly pending, re-sending 97 forever.
    /// </remarks>
    private (int Sent, int Duplicates, int Failed) Reconcile(
        List<OutboxEntry> entries,
        PlaySessionOutcome outcome,
        List<string> problems)
    {
        var now = _time.GetUtcNow();
        var sent = 0;
        var duplicates = 0;
        var failed = 0;

        var byIndex = outcome.Results.ToDictionary(result => result.Index);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];

            if (!byIndex.TryGetValue(index, out var result))
            {
                // The server answered about fewer entries than were sent. Left pending, which
                // is the direction that cannot lose a session.
                _store.Outbox.RecordFailure(entry.Id, "the server did not report on this entry.", now);
                failed++;
                continue;
            }

            if (result.IsAccepted)
            {
                _store.Outbox.MarkSent(entry.Id, now);

                if (result.IsDuplicate)
                {
                    duplicates++;
                }
                else
                {
                    sent++;
                }

                continue;
            }

            _store.Outbox.RecordFailure(entry.Id, result.Detail ?? result.Status, now);
            problems.Add($"play session {entry.Id}: {result.Detail ?? result.Status}");
            failed++;
        }

        return (sent, duplicates, failed);
    }

    private void RecordFailure(List<OutboxEntry> entries, string reason)
    {
        var now = _time.GetUtcNow();

        foreach (var entry in entries)
        {
            _store.Outbox.RecordFailure(entry.Id, reason, now);
        }
    }

    private static PlaySessionEntry? Parse(OutboxEntry entry)
    {
        if (entry.Payload is not { } payload)
        {
            return null;
        }

        try
        {
            var session = JsonSerializer.Deserialize<PendingPlaySession>(payload);

            if (session is null || session.EndTime <= session.StartTime)
            {
                // end_time must be strictly after start_time, enforced server-side with a 422.
                // Refused here rather than sent to be refused there.
                return null;
            }

            return new PlaySessionEntry(
                session.RomId is { } romId ? (int)romId : null,
                session.SaveSlot,
                session.StartTime,
                session.EndTime,
                session.DurationMs);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
