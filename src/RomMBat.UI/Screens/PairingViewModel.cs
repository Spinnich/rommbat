using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Server;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>Where the pairing flow has got to.</summary>
public enum PairingStage
{
    /// <summary>Asking the server whether it is there.</summary>
    Contacting,

    /// <summary>It is not, which is a state and not a fault.</summary>
    Unreachable,

    /// <summary>The server said no, and said why.</summary>
    Refused,

    /// <summary>A code is on screen and the server is being polled.</summary>
    WaitingForApproval,

    /// <summary>Done.</summary>
    Paired,
}

/// <summary>
/// Pairing, from a typed address to a stored token.
/// </summary>
/// <remarks>
/// <b>Every decision here belongs to <see cref="PairingService"/>, which the console has used
/// since M1.</b> This owns the shape of the wait: what is on screen while polling, what the
/// countdown says, and which button starts again. It generates no code, writes no token and
/// decides nothing about scopes.
/// <para>
/// <b>Unreachable is a working state.</b> The reachability probe uses the short interactive
/// connect timeout, so an unreachable LAN host costs about two seconds rather than the 21 the
/// default would; and the wait happens off the poll loop, so the screen stays responsive and
/// the user can still leave.
/// </para>
/// <para>
/// <b>The countdown is not decoration.</b> Pending state lives in Redis with a hard TTL, so the
/// code really does lapse, and a user who cannot see that coming is left staring at a screen
/// that has already failed.
/// </para>
/// </remarks>
public sealed class PairingViewModel : IScreen, ILiveScreen, IDisposable
{
    private readonly InstallSession _session;
    private readonly Uri _origin;
    private readonly Func<Uri, RomMConnection> _connect;

    private CancellationTokenSource _run = new();
    private PairingSession? _pairing;
    private PairingCompletion? _completion;
    private string _detail = "Contacting the server.";
    private bool _disposed;

    public PairingViewModel(InstallSession session, Uri origin)
        : this(session, origin, origin => InstallSession.Connect(origin))
    {
    }

    /// <param name="connect">
    /// How the screen reaches the server. Tests stand a stub in place of one, the way
    /// <see cref="RomMConnection"/>'s own handler constructor exists for. Taken here rather
    /// than as an init property because the first run starts in this constructor.
    /// </param>
    internal PairingViewModel(InstallSession session, Uri origin, Func<Uri, RomMConnection> connect)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(connect);

        _session = session;
        _origin = origin;
        _connect = connect;

        Start();
    }

    public event EventHandler? Invalidated;

    public string Title => "Pair with RomM";

    public PairingStage Stage { get; private set; } = PairingStage.Contacting;

    /// <summary>The sentence under the title. Always set.</summary>
    public string Detail => _detail;

    /// <summary>The address to open, once there is one.</summary>
    public Uri? VerificationUri => _pairing?.VerificationUri;

    /// <summary>The 8-character code, hyphenated for reading aloud.</summary>
    public string? DisplayCode => _pairing?.DisplayCode;

    /// <summary>The QR for <see cref="VerificationUri"/>, built once per pairing request.</summary>
    public QrMatrix? QrCode { get; private set; }

    /// <summary>What RomMBat asked for, so the approver knows before they approve.</summary>
    public static IReadOnlyList<string> RequestedScopes => RomMScopes.Requested;

    /// <summary>How long the code has left, or null when nothing is pending.</summary>
    public TimeSpan? Remaining =>
        Stage == PairingStage.WaitingForApproval && _pairing is { } pairing
            ? pairing.RemainingAt(DateTimeOffset.UtcNow)
            : null;

    /// <summary>Set once pairing succeeded, for the summary.</summary>
    public PairingCompletion? Completion => _completion;

    public IReadOnlyList<FooterHint> Hints => Stage switch
    {
        PairingStage.Paired => [new FooterHint(NavAction.Back, "Done", 3)],
        PairingStage.WaitingForApproval =>
        [
            new FooterHint(NavAction.Alternate, "New code", 2),
            new FooterHint(NavAction.Back, "Cancel", 3),
        ],
        PairingStage.Unreachable or PairingStage.Refused =>
        [
            new FooterHint(NavAction.Alternate, "Try again", 2),
            new FooterHint(NavAction.Back, "Back", 3),
        ],
        _ => [new FooterHint(NavAction.Back, "Cancel", 3)],
    };

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Back:
                return ScreenCommand.Pop;

            case NavAction.Alternate when Stage is PairingStage.WaitingForApproval
                or PairingStage.Unreachable
                or PairingStage.Refused:
                Restart();
                break;

            default:
                break;
        }

        return ScreenCommand.Stay;
    }

    /// <summary>
    /// The token the run in flight was started with, so a test can assert what cancelled it.
    /// </summary>
    internal CancellationToken CurrentRun => _run.Token;

    /// <summary>
    /// Abandons the request in flight and asks for a new one.
    /// </summary>
    /// <remarks>
    /// <b>The old run has to be cancelled, not just forgotten.</b> It is parked inside
    /// <c>AwaitApprovalAsync</c>, which returns only on approval, denial, a server error or
    /// the old code's own expiry. Left running it writes "the pairing code expired" over the
    /// fresh code now on screen, or is approved and saves a second pairing concurrently with
    /// this one, and every further press adds another poller against the same server.
    /// </remarks>
    private void Restart()
    {
        var superseded = _run;
        _run = new CancellationTokenSource();
        superseded.Cancel();

        _pairing = null;
        QrCode = null;
        Move(PairingStage.Contacting, "Contacting the server.");
        Start();
    }

    /// <summary>
    /// Runs the whole flow off the UI thread.
    /// </summary>
    /// <remarks>
    /// Deliberately fire-and-forget: the screen reports progress by raising
    /// <see cref="Invalidated"/>, and every failure below is turned into a stage rather than
    /// thrown, so there is nothing for a caller to await or catch.
    /// </remarks>
    private void Start() => _ = RunAsync(_run);

    private async Task RunAsync(CancellationTokenSource run)
    {
        var cancellationToken = run.Token;

        // True once this run has been superseded or the screen closed. Nothing a run learns
        // after that reaches the screen, whichever of the two happened.
        bool Stale() => !ReferenceEquals(_run, run) || cancellationToken.IsCancellationRequested;

        void Update(PairingStage stage, string detail)
        {
            if (!Stale())
            {
                Move(stage, detail);
            }
        }

        try
        {
            using var connection = _connect(_origin);

            var contact = await ServerProbes
                .TryContactAsync(connection, _session.Store, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (contact is null)
            {
                Update(
                    PairingStage.Unreachable,
                    $"{_origin} did not answer. Everything else RomMBat does works offline, "
                        + "and this can wait until the server is back.");
                return;
            }

            if (contact.MustRefuse)
            {
                Update(PairingStage.Refused, contact.Probe.Compatibility.Message);
                return;
            }

            var service = new PairingService(_session.Install, _session.Store);
            service.RememberServer(_origin);

            var pairing = await service.BeginAsync(connection, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (Stale())
            {
                return;
            }

            _pairing = pairing;
            QrCode = PairingQrCode.Build(pairing.VerificationUri);

            Update(
                PairingStage.WaitingForApproval,
                "Scan the code with a phone, or open the address and type the code. "
                    + "RomM will ask which permissions to grant.");

            var completion = await service
                .CompleteAsync(connection, pairing, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (Stale())
            {
                return;
            }

            _completion = completion;

            Update(
                completion.IsPaired ? PairingStage.Paired : PairingStage.Refused,
                completion.Message);
        }
        catch (OperationCanceledException)
        {
            // The user left, or pressed for a new code. Nothing to say and nobody to say it to.
        }
        catch (RomMUnreachableException ex)
        {
            Update(PairingStage.Unreachable, ex.Message);
        }
        catch (RomMApiException ex)
        {
            Update(PairingStage.Refused, ex.Message);
        }
    }

    private void Move(PairingStage stage, string detail)
    {
        Stage = stage;
        _detail = detail;
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Cancels the run in flight. The source itself is deliberately not disposed.
    /// </summary>
    /// <remarks>
    /// A run is still unwinding when this returns, and disposing a source whose token that run
    /// may still register on throws <see cref="ObjectDisposedException"/> on the background
    /// thread, where it becomes an unobserved task exception rather than anything anyone sees.
    /// Cancelling releases the registrations; the source itself is a few bytes for the GC.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _run.Cancel();
    }
}
