using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core;

/// <summary>Why an install could not be opened.</summary>
public enum InstallRefusal
{
    /// <summary>It opened.</summary>
    None,

    /// <summary>No RetroBat tree was found to run inside.</summary>
    NotFound,

    /// <summary>This build refuses to run against that RetroBat version.</summary>
    Version,

    /// <summary>The local store is from a newer build than this one.</summary>
    Store,
}

/// <summary>
/// The outcome of opening an install: a session, or a refusal with the words for it.
/// </summary>
/// <param name="Message">Null exactly when <paramref name="Session"/> is non-null.</param>
/// <param name="Warning">
/// Set when the install opened but something is worth saying: a RetroBat newer than the last
/// tested one. Never a reason to stop.
/// </param>
public sealed record InstallOpen(
    InstallSession? Session,
    InstallRefusal Refusal,
    string? Message,
    string? Warning)
{
    public bool IsOpen => Session is not null;
}

/// <summary>The server URL to call, or why there is not one.</summary>
public sealed record OriginChoice(Uri? Origin, string? Problem);

/// <summary>An authenticated connection, or why there is not one.</summary>
/// <param name="NotPaired">
/// True when the answer is "pair first" rather than "something went wrong", which is an
/// ordinary state on an expiring token rather than a fault.
/// </param>
public sealed record AuthAttempt(RomMConnection? Connection, bool NotPaired, string? Problem);

/// <summary>
/// Everything a front end needs to start: the located install, the open store, and the
/// configured origin.
/// </summary>
/// <remarks>
/// <b>This is the composition root, and it is in Core because there are two front ends.</b>
/// It began as <c>AgentContext</c>, which is <c>internal</c> to <c>RomMBat.Agent</c> and so
/// unreachable from the UI. The alternative was a second implementation of root discovery and
/// the version gate, which would mean two implementations of the rule that refuses to run
/// below the declared floor, and nothing to keep them agreeing.
/// <para>
/// <b>It decides, and it does not report.</b> No <c>TextWriter</c>, no exit code, no console:
/// a refusal comes back as a value with the words already chosen, and the caller decides
/// whether that is a line on stderr or a screen. That split is what lets the UI hold no logic
/// here rather than only intending to.
/// </para>
/// <para>
/// <b>Version is checked before the store is opened</b>, so a build that refuses to run
/// against this RetroBat never creates a database in its tree.
/// </para>
/// </remarks>
public sealed class InstallSession : IDisposable
{
    private InstallSession(RetroBatInstall install, LocalStore store)
    {
        Install = install;
        Store = store;
    }

    public RetroBatInstall Install { get; }

    public LocalStore Store { get; }

    /// <summary>
    /// The language EmulationStation is running in, or null when it is running in its default.
    /// </summary>
    /// <remarks>
    /// <b>Read afresh rather than cached</b>, because ES rewrites this file twice a session and
    /// RomMBat's own process outlives neither event reliably.
    /// <para>
    /// <b>Absent is the ordinary answer and means the default.</b> ES prunes any setting equal
    /// to its own default, measured on this very key, so a null here is not evidence that
    /// nobody chose a language. It is also what ES itself sees: on a Windows release build
    /// <c>SystemConf</c> has no config file of its own and falls back to these settings, which
    /// is why the language that picks a keyboard lives here and not in <c>batocera.conf</c>.
    /// Finding 234.
    /// </para>
    /// </remarks>
    public string? EmulationStationLanguage() =>
        EsSettingsFile.Load(Install.Resolve(EsSettingsFile.Location)).Value("Language");

    /// <summary>Locates the install, checks its version and opens the store.</summary>
    /// <param name="explicitRoot">A root given on the command line, which wins over discovery.</param>
    public static InstallOpen Open(string? explicitRoot = null)
    {
        RetroBatInstall install;
        try
        {
            install = RetroBatRoot.Require(explicitRoot);
        }
        catch (RetroBatNotFoundException ex)
        {
            return new InstallOpen(null, InstallRefusal.NotFound, ex.Message, null);
        }

        var version = install.CheckVersion();
        if (version.MustRefuse)
        {
            return new InstallOpen(null, InstallRefusal.Version, version.Message, null);
        }

        var warning = version.Verdict == CompatibilityVerdict.Untested ? version.Message : null;

        try
        {
            return new InstallOpen(
                new InstallSession(install, LocalStore.Open(install)),
                InstallRefusal.None,
                null,
                warning);
        }
        catch (LocalStoreVersionException ex)
        {
            // The warning is carried even here, so the refactor that moved this out of the
            // agent changes no output on any path.
            return new InstallOpen(null, InstallRefusal.Store, ex.Message, warning);
        }
    }

    /// <summary>
    /// Works out which origin to call: the one supplied, otherwise the one pairing stored.
    /// </summary>
    /// <remarks>
    /// The server URL is the one thing that still has to be typed, and it is the real
    /// gamepad-hostile step. Remembering it is why it only has to be typed once.
    /// </remarks>
    public OriginChoice ResolveOrigin(string? supplied)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return Uri.TryCreate(supplied, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                ? new OriginChoice(parsed, null)
                : new OriginChoice(null, $"'{supplied}' is not an http or https URL.");
        }

        var stored = Store.Device.Read()?.ServerOrigin;

        return stored is not null
            ? new OriginChoice(stored, null)
            : new OriginChoice(
                null,
                "No RomM server is configured. Pass --server https://your-romm-instance.");
    }

    /// <summary>Opens an unauthenticated connection, which is all pairing needs.</summary>
    public static RomMConnection Connect(Uri origin, TimeSpan? connectTimeout = null) =>
        new(new RomMClientOptions
        {
            Origin = origin,
            ConnectTimeout = connectTimeout ?? RomMClientOptions.InteractiveConnectTimeout,
            UserAgent = UserAgent,
        });

    /// <summary>Opens a connection carrying the stored token.</summary>
    public static RomMConnection ConnectAuthenticated(Uri origin, string accessToken) =>
        new(new RomMClientOptions
        {
            Origin = origin,
            ConnectTimeout = RomMClientOptions.InteractiveConnectTimeout,
            AccessToken = accessToken,
            UserAgent = UserAgent,
        });

    /// <summary>
    /// Opens a connection carrying the stored token, or explains why it cannot.
    /// </summary>
    /// <remarks>
    /// A missing or unreadable token is <see cref="AuthAttempt.NotPaired"/> rather than an
    /// error: an expiring token is the recommended default here, so re-pairing is a normal
    /// step and not a fault.
    /// </remarks>
    public AuthAttempt Authenticate(string? passphrase = null)
    {
        var device = Store.Device.Read();
        if (device?.ServerOrigin is null || device.Token is null)
        {
            return new AuthAttempt(
                null,
                NotPaired: true,
                "This install is not paired. Run 'rommbat-agent pair' first.");
        }

        string token;
        try
        {
            token = new PairingService(Install, Store).UnlockToken(passphrase);
        }
        catch (TokenUnlockException ex)
        {
            return new AuthAttempt(null, NotPaired: true, ex.Message);
        }

        return new AuthAttempt(ConnectAuthenticated(device.ServerOrigin, token), NotPaired: false, null);
    }

    private static string UserAgent => $"RomMBat/{PairingService.ClientVersion()}";

    public void Dispose() => Store.Dispose();
}
