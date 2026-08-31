using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;

namespace RomMBat.Agent;

/// <summary>
/// The console's view of a <see cref="InstallSession"/>: the same session, with refusals
/// turned into exit codes and lines on stderr.
/// </summary>
/// <remarks>
/// <b>This holds no composition of its own.</b> Locating the install, gating the version and
/// opening the store moved to <see cref="InstallSession"/> in Core when the UI needed them
/// too, because the alternative was two implementations of the rule that refuses to run below
/// the declared floor. What is left here is the mapping from a decision to a console, which is
/// the only part that is genuinely the agent's.
/// </remarks>
internal sealed class AgentContext : IDisposable
{
    private readonly InstallSession _session;

    private AgentContext(InstallSession session) => _session = session;

    public RetroBatInstall Install => _session.Install;

    public LocalStore Store => _session.Store;

    /// <summary>The session itself, for the Core services that take one.</summary>
    public InstallSession Session => _session;

    /// <summary>
    /// Set definitions, the picker data behind them, and the rules they are subject to.
    /// </summary>
    /// <remarks>
    /// Built per access rather than cached: it holds no state of its own, only the session.
    /// </remarks>
    public SyncSetService Sets => new(_session);

    /// <summary>Opens the install, or writes why it will not and sets an exit code.</summary>
    public static AgentContext? Open(CommandLine command, TextWriter error, out int exitCode)
    {
        var opened = InstallSession.Open(command.Value("root"));

        if (opened.Warning is { } warning)
        {
            error.WriteLine($"Warning: {warning}");
        }

        if (opened.Session is null)
        {
            error.WriteLine(opened.Message);
            exitCode = ExitCode.Refused;
            return null;
        }

        exitCode = ExitCode.Ok;
        return new AgentContext(opened.Session);
    }

    /// <summary>The origin to call, or null having said why.</summary>
    public Uri? ResolveOrigin(CommandLine command, TextWriter error)
    {
        var choice = _session.ResolveOrigin(command.Value("server"));
        if (choice.Origin is null)
        {
            error.WriteLine(choice.Problem);
        }

        return choice.Origin;
    }

    /// <summary>Opens an unauthenticated connection, which is all pairing needs.</summary>
    public static RomMConnection Connect(Uri origin, TimeSpan? connectTimeout = null) =>
        InstallSession.Connect(origin, connectTimeout);

    /// <summary>Opens a connection carrying the stored token.</summary>
    public static RomMConnection ConnectAuthenticated(Uri origin, string accessToken) =>
        InstallSession.ConnectAuthenticated(origin, accessToken);

    /// <summary>A connection carrying the stored token, or null having said why.</summary>
    public RomMConnection? Authenticate(CommandLine command, TextWriter error, out int exitCode)
    {
        var attempt = _session.Authenticate(command.Value("passphrase"));
        if (attempt.Connection is null)
        {
            error.WriteLine(attempt.Problem);
            exitCode = attempt.NotPaired ? ExitCode.NotPaired : ExitCode.Refused;
            return null;
        }

        exitCode = ExitCode.Ok;
        return attempt.Connection;
    }

    public void Dispose() => _session.Dispose();
}
