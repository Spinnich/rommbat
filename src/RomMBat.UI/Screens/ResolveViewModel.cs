using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>Where a resolve has got to.</summary>
public enum ResolveStage
{
    /// <summary>Walking the scope.</summary>
    Working,

    /// <summary>Finished, and the set knows what it holds.</summary>
    Done,

    /// <summary>Stopped part way. The offset is recorded and the next run continues.</summary>
    Stopped,

    /// <summary>The server refused, or the set needs a folder chosen.</summary>
    Refused,

    /// <summary>There is no pairing to resolve against.</summary>
    NotPaired,
}

/// <summary>
/// Resolving one set, while somebody watches.
/// </summary>
/// <remarks>
/// <b>A resolve is minutes-long work, which is a measurement and not an impression.</b> A
/// platform scope of 9,196 roms took <b>8 minutes 15 seconds</b> against a live 5.2.0 instance
/// at 250 rows a page. That is what decides this screen's shape:
/// <list type="bullet">
/// <item>It shows a count that moves. A screen that cannot show progress is, from a sofa,
/// indistinguishable from a hung one, and eight minutes is long enough to convince anyone.</item>
/// <item>It can be cancelled, because nobody holds a controller for eight minutes.</item>
/// <item>Cancelling <b>resumes</b>. <see cref="SetResolveService"/> records the offset exactly
/// as an unreachable server does, so the next resolve continues rather than starting again. A
/// cancel that threw the paging away would make the feature worse than not having it.</item>
/// </list>
/// <para>
/// <b>Same pattern as pairing.</b> Work starts on entry, the screen owns its cancellation, and
/// it is disposed when it is left. <see cref="ILiveScreen"/> stops being an interface with one
/// implementer here, which is what 7b-1's ledger flagged it as.
/// </para>
/// <para>
/// <b>The source is cancelled and never disposed.</b> A run still unwinding can register on
/// that token and would take an <see cref="ObjectDisposedException"/> on a background thread
/// where nobody sees it. Finding from round 8 of stage 7b-1.
/// </para>
/// </remarks>
public sealed class ResolveViewModel : IScreen, ILiveScreen, IDisposable
{
    private readonly InstallSession _session;
    private readonly IReadOnlyList<SyncSetDefinition> _sets;
    private readonly CancellationTokenSource _run = new();

    private readonly Func<CancellationToken, Task<RoamingPush>> _roam;

    private SetResolveProgress? _progress;
    private Task? _walk;
    private Task? _roaming;
    private bool _disposed;
    private bool _stopping;

    /// <param name="connect">
    /// How the screen reaches the server. Taken so a test can stand a stub in its place, the
    /// way <see cref="Screens.PairingViewModel"/> already does.
    /// </param>
    /// <param name="roam">
    /// How the definitions are mirrored once the walk is over. Taken for the same reason, since
    /// <see cref="RoamingConfigService"/> opens its own connection out of the store.
    /// </param>
    public ResolveViewModel(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect = null,
        Func<CancellationToken, Task<RoamingPush>>? roam = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sets);

        _session = session;
        _sets = sets;
        _roam = roam ?? (token => new RoamingConfigService(session).PushAsync(cancellationToken: token));

        Start(connect);
    }

    /// <summary>Resolving a single set, which is what the detail screen asks for.</summary>
    public ResolveViewModel(
        InstallSession session,
        SyncSetDefinition set,
        Func<Uri, RomMConnection>? connect = null,
        Func<CancellationToken, Task<RoamingPush>>? roam = null)
        : this(session, [set], connect, roam)
    {
        ArgumentNullException.ThrowIfNull(set);
    }

    public event EventHandler? Invalidated;

    /// <summary>
    /// What the screen is called, which is not what the code calls it.
    /// </summary>
    /// <remarks>
    /// "Resolving" is the word the design has used since M2 and it means nothing to a person: a
    /// hands-on pass reported that resolving and syncing read as the same thing. The type keeps
    /// the name, because that is what the operation is called everywhere else in the codebase
    /// and renaming it would cost more than it buys; what a person sees says what it does.
    /// </remarks>
    /// <summary>
    /// What the screen is doing, in the tense it is doing it in.
    /// </summary>
    /// <remarks>
    /// <b>Past tense once the work is over, because the present tense is a claim that it is
    /// still running.</b> A hands-on pass sat on a finished resolve reading "Checking what is
    /// in 'X'" over a full bar and 107 of 107, and could not tell whether the last game was
    /// stuck or the screen was about to move on. The title is the largest thing on the screen
    /// and it was the thing saying the wrong one.
    /// </remarks>
    public string Title => Stage == ResolveStage.Working
        ? _sets.Count == 1
            ? $"Checking what is in '{_sets[0].Name}'"
            : $"Checking {_sets.Count} sync sets"
        : _sets.Count == 1
            ? $"Checked '{_sets[0].Name}'"
            : $"Checked {_sets.Count} sync sets";

    /// <summary>
    /// One word for how it ended, or null while it is still going.
    /// </summary>
    /// <remarks>
    /// <b>Said outright rather than left to be inferred from a full progress bar.</b> A bar at
    /// the end and a bar that has stopped moving look identical, and the second is what a user
    /// fears. This is the line that separates them, and it is here rather than in the renderer
    /// because which word applies is a fact about the outcome.
    /// </remarks>
    public string? Outcome => Stage switch
    {
        ResolveStage.Working => null,
        ResolveStage.Done => "Finished",
        ResolveStage.Stopped => "Stopped",
        _ => "Did not finish",
    };

    public ResolveStage Stage { get; private set; } = ResolveStage.Working;

    /// <summary>The sentence under the title. Always set.</summary>
    public string Detail { get; private set; } = "Asking RomM what this set contains.";

    /// <summary>How many rows have been read, and out of how many, once the server has said.</summary>
    public SetResolveProgress? Progress => _progress;

    /// <summary>
    /// Which set is being walked, and which of how many.
    /// </summary>
    /// <remarks>
    /// Resolving several reported only a running count of games, so from the couch it read as
    /// one long operation that kept starting over rather than as five in a row.
    /// </remarks>
    public string? Progressing =>
        _progress is { } step
            ? step.SetCount > 1
                ? string.Create(
                    CultureInfo.CurrentCulture,
                    $"{step.SetName}  ({step.SetIndex} of {step.SetCount})")
                : step.SetName
            : null;

    /// <summary>The count as a person reads it, or null before the first page.</summary>
    public string? Counted =>
        _progress is { Total: > 0 } progress
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"{progress.Offset:N0} of {progress.Total:N0} games looked at")
            : _progress is { } started
                ? string.Create(CultureInfo.CurrentCulture, $"{started.Offset:N0} games looked at")
                : null;

    public IReadOnlyList<FooterHint> Hints => Stage switch
    {
        // Named for what it does rather than for what it stops. "Cancel" reads as though the
        // work is thrown away, and it is not: the walk resumes where it stopped.
        ResolveStage.Working => [new FooterHint(NavAction.Back, "Stop for now")],

        // "Done" rather than "Back" once there is nothing left running, which is the rule the
        // pairing screen already followed and these two did not: if the footer offers a stop
        // the work is going, and if it says Done it is over.
        _ => [new FooterHint(NavAction.Back, "Done")],
    };

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Back when Stage == ResolveStage.Working:
                // Stop and stay; a second Back leaves. #107: this screen already composed a
                // sentence naming the set that was interrupted, and nothing could ever display
                // it, because Back popped the screen and Dispose was the only thing that
                // cancelled the walk. The stopped summary was written to a screen that had
                // already left the stack.
                //
                // The sync screen answers Back the same way and has to, since its stop removes
                // a part-fetched game. Two minutes-long screens with two different rules for
                // the same press is a rule a user has to learn twice.
                Stop();
                return ScreenCommand.Stay;

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    /// <summary>Asks the walk to stop, and says so at once rather than when it notices.</summary>
    private void Stop()
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        Detail = "Stopping. What has been found so far is kept.";
        Raise();
        _run.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancelled, never disposed. A walk still unwinding registers on this token.
        _run.Cancel();

        // Then wait, briefly, for it to finish writing what it found. The screen underneath is
        // rebuilt the moment this returns, and without the wait it was rebuilt before the
        // cancelled walk had recorded, so it showed the counts from before the resolve ran.
        //
        // This is not a network wait: the walk breaks out of its loop and performs two SQLite
        // writes. The bound is here because a screen that cannot be left is worse than one that
        // is briefly out of date, and a request already in flight is abandoned rather than
        // waited on.
        try
        {
            _walk?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The walk ends by throwing its cancellation, which is the expected way out.
        }
    }

    private void Start(Func<Uri, RomMConnection>? connect)
    {
        var attempt = _session.Authenticate();

        if (attempt.Connection is null)
        {
            Stage = attempt.NotPaired ? ResolveStage.NotPaired : ResolveStage.Refused;
            Detail = attempt.Problem ?? "This install is not paired with a RomM server.";
            return;
        }

        var origin = _session.Store.Device.Read()?.ServerOrigin;
        var connection = connect is not null && origin is not null ? connect(origin) : attempt.Connection;

        if (!ReferenceEquals(connection, attempt.Connection))
        {
            attempt.Connection.Dispose();
        }

        _walk = Task.Run(() => WalkAsync(connection), CancellationToken.None);
    }

    private async Task WalkAsync(RomMConnection connection)
    {
        try
        {
            var reports = await new SetResolveService(_session, connection)
                .ResolveAsync(
                    _sets,
                    new Immediate<SetResolveProgress>(progress =>
                    {
                        _progress = progress;
                        Raise();
                    }),
                    _run.Token)
                .ConfigureAwait(false);

            Settle(reports);
        }
        catch (SetResolveCancelledException cancelled)
        {
            // Not a failure. The offset is recorded and the next resolve continues from it,
            // which is the whole reason stopping is offered at all.
            Stage = ResolveStage.Stopped;

            // The last report is the set that was interrupted, because reports are appended in
            // walk order and the cancel is raised straight after the current set is recorded.
            // Reports[0] named set one of three while set three was the one that stopped.
            Detail = cancelled.Reports.Count > 0
                ? $"Stopped. {cancelled.Reports[^1].Summary}"
                : "Stopped. The next resolve continues from here.";
            Raise();
        }
        catch (OperationCanceledException)
        {
            Stage = ResolveStage.Stopped;
            Detail = "Stopped. The next resolve continues from here.";
            Raise();
        }
        catch (RomMUnreachableException ex)
        {
            // Offline is a working state, so this is a sentence rather than an error screen.
            Stage = ResolveStage.Stopped;
            Detail = ex.Message;
            Raise();
        }
        finally
        {
            connection.Dispose();
        }

        // Mirrored into Device.sync_config exactly as `sets resolve` does. Without this a set
        // defined from the couch never followed its user: `sets add` roamed, the editor did
        // not, and the same action persisted differently depending on which front end took it.
        // Run whatever the walk did, including after a stop, because the definition exists
        // either way and roaming it is not the thing that was stopped.
        _roaming = Task.Run(RoamAsync, CancellationToken.None);
    }

    /// <summary>Mirrors the definitions, and says so only when it could not.</summary>
    /// <remarks>
    /// <b>Not on this screen's token, and not waited on by <see cref="Dispose"/>.</b> The push
    /// is best effort by contract: it opens its own connection, returns every failure as a note
    /// and throws none, so a screen that has been left can abandon it. Dispose's bounded wait
    /// stays what its comment says it is, the walk's two SQLite writes.
    /// </remarks>
    private async Task RoamAsync()
    {
        var push = await _roam(CancellationToken.None).ConfigureAwait(false);

        if (push.Note is { } note)
        {
            Detail = $"{Detail} {note}";
            Raise();
        }
    }

    /// <summary>
    /// Says how it ended, which for several sets is the worst outcome among them.
    /// </summary>
    /// <remarks>
    /// Reporting only the last one would let a refusal on the first set disappear behind four
    /// that worked, and the one a person needs to act on is the one that did not.
    /// </remarks>
    private void Settle(IReadOnlyList<ResolveReport> reports)
    {
        var report = reports.FirstOrDefault(r => r.State is ResolveState.Refused or ResolveState.NeedsFolderChoice)
            ?? reports.FirstOrDefault(r => r.State == ResolveState.Interrupted)
            ?? (reports.Count > 0 ? reports[^1] : null);

        if (report is null)
        {
            Stage = ResolveStage.Stopped;
            Detail = "Nothing was resolved.";
        }
        else if (reports.Count > 1 && report.State == ResolveState.Resolved)
        {
            Stage = ResolveStage.Done;
            Detail = $"{reports.Count} sync sets resolved.";
        }
        else
        {
            (Stage, Detail) = report.State switch
            {
                ResolveState.Resolved => (ResolveStage.Done, report.Summary),
                ResolveState.Refused => (ResolveStage.Refused, report.Problem ?? report.Summary),
                ResolveState.NeedsFolderChoice => (
                    ResolveStage.Refused,
                    (report.Problem ?? report.Summary)
                        + " Set a folder on this set and resolve it again."),
                _ => (
                    ResolveStage.Stopped,
                    report.Problem
                        ?? $"{report.Summary} Stopped at {report.Offset:N0} of {report.Total:N0}; "
                            + "the next resolve continues from there."),
            };
        }

        Raise();
    }

    // Raised from whatever thread did the work. The shell marshals it.
    private void Raise() => Invalidated?.Invoke(this, EventArgs.Empty);
}
