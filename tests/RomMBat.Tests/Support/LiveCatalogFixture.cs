using System.Net.Http.Headers;
using System.Text.Json;
using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Store;
using Xunit;

namespace RomMBat.Tests.Support;

/// <summary>
/// One throwaway paired install, shared by every test in a live catalog class.
/// </summary>
/// <remarks>
/// <c>POST /api/auth/device/init</c> is rate limited to 10 per minute per IP. Pairing once
/// per test would put the live suite over that limit by itself, and the failure looks like a
/// broken client rather than a busy one.
/// <para>
/// The device and its token are removed in <see cref="DisposeAsync"/>, the same way
/// <see cref="LivePairingTests"/> does it. Every approval mints a real credential carrying
/// the full RomMBat scope set, so leaving one behind is leaving a live credential behind.
/// </para>
/// </remarks>
public sealed class LiveCatalogFixture : IAsyncLifetime, IDisposable
{
    private const string ServerVariable = "ROMMBAT_TEST_SERVER";
    private const string TokenVariable = "ROMMBAT_TEST_APPROVER_TOKEN";

    private readonly PairingLitter _litter = new();

    private TempRetroBatTree? _tree;
    private LocalStore? _store;
    private LiveSession? _session;

    internal static string? Server => Environment.GetEnvironmentVariable(ServerVariable);

    internal static string? ApproverToken => Environment.GetEnvironmentVariable(TokenVariable);

    /// <summary>True when both environment variables are set, so live tests can run.</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(ApproverToken);

    /// <summary>The paired install, its store, and a connection carrying its token.</summary>
    internal LiveSession Session =>
        _session ?? throw new InvalidOperationException("The live fixture is not configured.");

    public async Task InitializeAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        var origin = new Uri(Server!);
        _tree = TempRetroBatTree.Create();
        var install = _tree.Install();
        _store = LocalStore.Open(install);

        using var unauthenticated = new RomMConnection(new RomMClientOptions { Origin = origin });
        var pairing = new PairingService(install, _store);
        var session = await pairing.BeginAsync(unauthenticated, "RomMBat catalog test");

        using var approver = new ApprovingUser(origin, ApproverToken!);
        var polling = pairing.CompleteAsync(unauthenticated, session);
        await approver.ApproveAsync(session.UserCode, RomMScopes.Requested);
        var completion = await polling;

        Assert.True(completion.IsPaired, "Live pairing did not complete.");

        var token = pairing.UnlockToken(null);
        _litter.Track(completion.RomMDeviceId!, token, completion.Scopes.All);
        _session = new LiveSession(_store, completion.RomMDeviceId!, origin, token);
    }

    /// <summary>xUnit calls <see cref="DisposeAsync"/>; this exists so the analyzer agrees.</summary>
    public void Dispose()
    {
        _session?.Dispose();
        _store?.Dispose();
        _tree?.Dispose();
    }

    public async Task DisposeAsync()
    {
        Dispose();

        if (!IsConfigured || _litter.IsEmpty)
        {
            return;
        }

        var problems = await _litter.CleanUpAsync(new Uri(Server!), ApproverToken!);

        Assert.True(
            problems.Count == 0,
            "Live test litter was left on the server: " + string.Join("; ", problems));
    }
}

/// <summary>A paired install, its store, and a connection carrying its token.</summary>
internal sealed class LiveSession(LocalStore store, string deviceId, Uri origin, string token) : IDisposable
{
    public LocalStore Store => store;

    public string DeviceId => deviceId;

    /// <summary>The origin this session paired against.</summary>
    public Uri Origin => origin;

    /// <summary>
    /// The device token, for a test that has to build its own request.
    /// </summary>
    /// <remarks>
    /// Content downloads are measured on headers (<c>Range</c>, <c>ETag</c>, <c>Content-Range</c>)
    /// that no typed response surfaces, so those tests hold the credential directly.
    /// </remarks>
    public string Token => token;

    public RomMConnection Connection { get; } =
        new(new RomMClientOptions { Origin = origin, AccessToken = token });

    /// <summary>Reads a response as raw JSON, for asserting on fields the typed row drops.</summary>
    public async Task<JsonDocument> RawAsync(string path)
    {
        using var http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return JsonDocument.Parse(
            await http.GetStringAsync(RomMConnection.JoinOrigin(origin, path)).ConfigureAwait(false));
    }

    public void Dispose() => Connection.Dispose();
}
