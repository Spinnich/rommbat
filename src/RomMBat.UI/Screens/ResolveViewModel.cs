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

    private SetResolveProgress? _progress;
    private bool _disposed;

    /// <param name="connect">
    /// How the screen reaches the server. Taken so a test can stand a stub in its place, the
    /// way <see cref="Screens.PairingViewModel"/> already does.
    /// </param>
    public ResolveViewModel(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sets);

        _session = session;
        _sets = sets;

        Start(connect);
    }

    /// <summary>Resolving a single set, which is what the detail screen asks for.</summary>
    public ResolveViewModel(
        InstallSession session,
        SyncSetDefinition set,
        Func<Uri, RomMConnection>? connect = null)
        : this(session, [set], connect)
    {
        ArgumentNullException.ThrowIfNull(set);
    }

    public event EventHandler? Invalidated;

    public string Title => _sets.Count == 1
        ? $"Resolving '{_sets[0].Name}'"
        : $"Resolving {_sets.Count} sync sets";

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
                $"{progress.Scanned:N0} of {progress.Total:N0} games looked at")
            : _progress is { } started
                ? string.Create(CultureInfo.CurrentCulture, $"{started.Scanned:N0} games looked at")
                : null;

    public IReadOnlyList<FooterHint> Hints => Stage switch
    {
        // Named for what it does rather than for what it stops. "Cancel" reads as though the
        // work is thrown away, and it is not: the walk resumes where it stopped.
        ResolveStage.Working => [new FooterHint(NavAction.Back, "Stop for now")],
        _ => [new FooterHint(NavAction.Back, "Back")],
    };

    public ScreenCommand Handle(NavAction action) => action switch
    {
        NavAction.Back => ScreenCommand.Pop,
        _ => ScreenCommand.Stay,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancelled, never disposed. A walk still unwinding registers on this token.
        _run.Cancel();
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

        _ = Task.Run(() => WalkAsync(connection), CancellationToken.None);
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
            Detail = cancelled.Reports.Count > 0
                ? $"Stopped. {cancelled.Reports[0].Summary}"
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
