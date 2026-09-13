using RomM.Client;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>How a resolution attempt ended, in words a front end quotes rather than rewords.</summary>
/// <param name="State">Which of the things in <see cref="ConflictOutcomeState"/> happened.</param>
/// <param name="Message">
/// The whole answer, ready to print or draw. Never null: a refusal that said nothing would leave
/// a screen inventing its own sentence, which is how the two front ends drift.
/// </param>
public sealed record ConflictOutcome(ConflictOutcomeState State, string Message)
{
    public bool Resolved => State == ConflictOutcomeState.Resolved;
}

/// <summary>
/// One open conflict, with enough to name the game it belongs to.
/// </summary>
/// <param name="Title">
/// What to call the game. Null only when nothing on this device has ever recorded a name for
/// it, which is a real state: a save can outlive every trace of its ROM.
/// </param>
/// <param name="FileName">
/// The ROM's file name, or null with <paramref name="Title"/>.
/// </param>
/// <remarks>
/// <b>The name is looked up here rather than on the screen.</b> A conflict row carries a rom id
/// and nothing else, so the first version of the conflict screen drew "Game 295079", which from
/// the couch is close to meaningless. Which store answers, and in what order, is a decision with
/// a fallback chain and it is not presentation's to make.
/// </remarks>
public sealed record OpenConflict(SaveConflictRecord Conflict, string? Title, string? FileName);

/// <summary>Why a resolution did or did not happen.</summary>
/// <remarks>
/// <b>Busy is its own state rather than a failure.</b> The agent maps it to
/// <c>ExitCode.Refused</c> and a screen offers the press again; folding it into
/// <see cref="Failed"/> would make both of them say the conflict could not be resolved when
/// what happened is that nothing was tried.
/// <para>
/// <b><see cref="NotPaired"/> and <see cref="Offline"/> are two states because they were one
/// and the one was wrong.</b> A null connection means the store had nothing to sign in with,
/// which <c>InstallSession.Authenticate</c> decides without touching the wire, so calling it
/// offline told a person with a token that would not unlock to try again when they were back
/// on the network. Nothing would ever change, and the screen withheld the pairing route on
/// top of it. Being unreachable is a separate answer and only a caller that catches
/// <c>RomMUnreachableException</c> can give it.
/// </para>
/// </remarks>
public enum ConflictOutcomeState
{
    /// <summary>The side was chosen and the conflict is closed.</summary>
    Resolved,

    /// <summary>A flush or a sweep holds the tree. Nothing was changed.</summary>
    Busy,

    /// <summary>
    /// The game is being played, so its save files are in use. Nothing was changed.
    /// </summary>
    /// <remarks>
    /// Alongside <see cref="Busy"/> rather than inside <see cref="Failed"/>, and for the same
    /// reason: nothing was tried, the conflict is still open, and closing the game is a remedy
    /// the person reading it can carry out. See <see cref="InFlightGuard"/> and issue #155.
    /// </remarks>
    Deferred,

    /// <summary>
    /// There is nothing to sign in with: no pairing row, or a token that would not unlock.
    /// Pairing is the route out, and a front end owes one.
    /// </summary>
    NotPaired,

    /// <summary>
    /// Paired, but no RomM device id was recorded. Pairing again is the route out, so this is
    /// closer to <see cref="NotPaired"/> than to <see cref="Failed"/>.
    /// </summary>
    NoDeviceId,

    /// <summary>
    /// The server could not be reached. <b>Never returned by the service</b>, which never
    /// reaches the wire before one of the states above: it is here for the caller that wraps
    /// the call and catches an unreachable host.
    /// </summary>
    Offline,

    /// <summary>A rule said no, or the transfer did not complete.</summary>
    Failed,
}

/// <summary>
/// Resolving a save conflict, for a caller that cannot take the tree lock itself.
/// </summary>
/// <remarks>
/// <b>This exists because the UI may never name <see cref="TreeLock"/>, and resolving a class C
/// conflict has to hold it.</b> The rule was written in <c>saves resolve</c> and lived there:
/// take the lock before authenticating, refuse rather than treat a failed acquire as done, then
/// run <see cref="SaveConflictResolver"/>. A screen cannot copy that, because the UI's inability
/// to reference <c>TreeLock</c> is asserted structurally against the built assembly, and it is a
/// data-loss guard rather than tidiness: a flush treats a failed acquire as success, so a second
/// caller taking the lock to resolve one conflict would make a concurrent <c>background quit</c>
/// flush skip its upload and call it success.
/// <para>
/// <b>Unlike a flush, a failed acquire is refused.</b> A person asked for this, and silently
/// doing nothing would read as having resolved it. That is the one place the two lock users
/// differ and it is the reason this is a service rather than a flag on the flush.
/// </para>
/// <para>
/// <b>The same body serves both front ends.</b> <c>saves resolve</c> is a shell over this and
/// holds no rule of its own, which is the shape <see cref="SaveFlushService"/> took in stage
/// 7b-2b and for the same reason: a sentence that differs between the console and the couch is
/// two answers to one question.
/// </para>
/// </remarks>
public sealed class ConflictResolutionService
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly TimeProvider _time;

    public ConflictResolutionService(RetroBatInstall install, LocalStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Every conflict still waiting on a decision, oldest first, and what to call it.</summary>
    /// <remarks>
    /// Oldest first because that is the order they happened in, and because a conflict that has
    /// been open longest is the one whose local copy a person is least likely to still recognise.
    /// <para>
    /// <b>Named from <c>rom_metadata</c>, which is the store that outlives the file.</b> A
    /// conflicted save frequently belongs to a ROM that is no longer on the device, since
    /// removing a game never touches its saves, so a lookup through <c>local_file</c> would
    /// answer nothing in exactly the case that matters. <c>GameMetadata.Name</c> is never empty
    /// and already falls back to the file name, so one read covers both halves of what a row
    /// wants to show.
    /// </para>
    /// <para>
    /// One batched read rather than one per conflict, which is #111's lesson applied before it
    /// is a defect: a list is drawn on the thread that draws.
    /// </para>
    /// </remarks>
    public IReadOnlyList<OpenConflict> Open()
    {
        var open = _store.SaveConflicts.ListOpen()
            .OrderBy(conflict => conflict.FirstSeenAtUtc)
            .ToList();

        if (open.Count == 0)
        {
            return [];
        }

        var named = _store.Metadata.ForRoms(open.Select(conflict => (int)conflict.RomId));

        return
        [
            .. open.Select(conflict => named.TryGetValue((int)conflict.RomId, out var meta)
                ? new OpenConflict(conflict, meta.Name, meta.FsName)
                : new OpenConflict(conflict, null, null)),
        ];
    }

    /// <summary>
    /// Carries out one decision, holding the tree for the whole of it.
    /// </summary>
    /// <param name="connect">
    /// <b>A factory rather than a connection, and the order is the point.</b> The lock is taken
    /// first, because a resolution that cannot run is not worth a round trip to the server, and
    /// a caller handed in an already-open connection would have paid for it before finding out.
    /// Returning null is <see cref="ConflictOutcomeState.NotPaired"/> rather than an argument
    /// error, because a handheld whose store holds no usable token is an ordinary state this
    /// design is built for. It is not an unreachable server: nothing on that path has gone
    /// near the wire yet.
    /// <para>
    /// What it returns is disposed here, since deferring its creation is the whole reason it is
    /// a factory.
    /// </para>
    /// </param>
    public async Task<ConflictOutcome> ResolveAsync(
        long romId,
        string slot,
        ConflictResolution resolution,
        Func<RomMConnection?> connect,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ArgumentNullException.ThrowIfNull(connect);

        using var held = TreeLock.TryAcquire(_install);

        if (held is null)
        {
            return new ConflictOutcome(
                ConflictOutcomeState.Busy,
                // The agent's own words, unchanged by the lift. "Flush" rather than "sync" is
                // the accurate one: TreeLock is taken by SaveFlushService, PartialSweep and
                // this, and never by a sync directly, which reaches it only through the flush
                // pass it runs first.
                "A flush is running, and resolving a conflict writes the same save files it "
                    + "does. Nothing was changed. Try again once it has finished.");
        }

        using var connection = connect();

        if (connection is null)
        {
            return new ConflictOutcome(
                ConflictOutcomeState.NotPaired,
                "There is nothing here to sign in to RomM with: this install is either not "
                    + "paired, or its stored token could not be unlocked. Nothing was changed, "
                    + "and the conflict is still here. Pairing again is what fixes it.");
        }

        if (_store.Device.Read()?.RomMDeviceId is not { } deviceId)
        {
            return new ConflictOutcome(
                ConflictOutcomeState.NoDeviceId,
                "This install is paired but has no RomM device id. Pair again.");
        }

        var outcome = await new SaveConflictResolver(_install, _store, connection, deviceId, _time)
            .ResolveAsync(romId, slot, resolution, cancellationToken)
            .ConfigureAwait(false);

        return new ConflictOutcome(
            outcome switch
            {
                { Resolved: true } => ConflictOutcomeState.Resolved,
                { IsDeferred: true } => ConflictOutcomeState.Deferred,
                _ => ConflictOutcomeState.Failed,
            },
            outcome.Message);
    }
}
