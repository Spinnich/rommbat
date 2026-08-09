using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Agent;

/// <summary>
/// Everything a subcommand needs: the located install, the open store, and the configured
/// origin.
/// </summary>
internal sealed class AgentContext : IDisposable
{
    private AgentContext(RetroBatInstall install, LocalStore store)
    {
        Install = install;
        Store = store;
    }

    public RetroBatInstall Install { get; }

    public LocalStore Store { get; }

    /// <summary>
    /// Locates the install, checks its version and opens the store.
    /// </summary>
    /// <remarks>
    /// Version is checked before the store is opened, so a build that refuses to run against
    /// this RetroBat never creates a database in its tree.
    /// </remarks>
    public static AgentContext? Open(CommandLine command, TextWriter error, out int exitCode)
    {
        exitCode = ExitCode.Ok;

        RetroBatInstall install;
        try
        {
            install = RetroBatRoot.Require(command.Value("root"));
        }
        catch (RetroBatNotFoundException ex)
        {
            error.WriteLine(ex.Message);
            exitCode = ExitCode.Refused;
            return null;
        }

        var version = install.CheckVersion();
        if (version.MustRefuse)
        {
            error.WriteLine(version.Message);
            exitCode = ExitCode.Refused;
            return null;
        }

        if (version.Verdict == CompatibilityVerdict.Untested)
        {
            error.WriteLine($"Warning: {version.Message}");
        }

        try
        {
            return new AgentContext(install, LocalStore.Open(install));
        }
        catch (LocalStoreVersionException ex)
        {
            error.WriteLine(ex.Message);
            exitCode = ExitCode.Refused;
            return null;
        }
    }

    /// <summary>
    /// Works out which origin to call: the one given on the command line, otherwise the one
    /// pairing stored last time.
    /// </summary>
    /// <remarks>
    /// The server URL is the one thing that still has to be typed, and it is the real
    /// gamepad-hostile step. Remembering it is why it only has to be typed once.
    /// </remarks>
    public Uri? ResolveOrigin(CommandLine command, TextWriter error)
    {
        var supplied = command.Value("server");
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            if (!Uri.TryCreate(supplied, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                error.WriteLine($"'{supplied}' is not an http or https URL.");
                return null;
            }

            return parsed;
        }

        var stored = Store.Device.Read()?.ServerOrigin;
        if (stored is not null)
        {
            return stored;
        }

        error.WriteLine("No RomM server is configured. Pass --server https://your-romm-instance.");
        return null;
    }

    /// <summary>Opens an unauthenticated connection, which is all pairing needs.</summary>
    public static RomMConnection Connect(Uri origin, TimeSpan? connectTimeout = null) =>
        new(new RomMClientOptions
        {
            Origin = origin,
            ConnectTimeout = connectTimeout ?? RomMClientOptions.InteractiveConnectTimeout,
            UserAgent = $"RomMBat/{Core.Identity.PairingService.ClientVersion()}",
        });

    /// <summary>Opens a connection carrying the stored token.</summary>
    public static RomMConnection ConnectAuthenticated(Uri origin, string accessToken) =>
        new(new RomMClientOptions
        {
            Origin = origin,
            ConnectTimeout = RomMClientOptions.InteractiveConnectTimeout,
            AccessToken = accessToken,
            UserAgent = $"RomMBat/{Core.Identity.PairingService.ClientVersion()}",
        });

    public void Dispose() => Store.Dispose();
}
