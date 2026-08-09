using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RomMBat.Tests.Support;

/// <summary>
/// Plays the person who approves a pairing request in the RomM web UI.
/// </summary>
/// <remarks>
/// <c>GET /api/auth/device/pending/{user_code}</c> and <c>POST /api/auth/device/approve</c>
/// are ordinary protected routes needing <c>me.read</c> and <c>me.write</c>, so a harness
/// holding a pre-made token can drive the real flow headlessly.
/// <para>
/// This lives in the test project on purpose. Putting approval into the shipped client would
/// give it a second auth-adjacent surface, and the point of pairing being the only path is
/// that there is exactly one.
/// </para>
/// </remarks>
internal sealed class ApprovingUser : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ApprovingUser(Uri origin, string accessToken)
    {
        _http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
            BaseAddress = origin,
            Timeout = TimeSpan.FromSeconds(30),
        };

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>Reads the approval screen's view of a pending request.</summary>
    public async Task<PendingRequest> ReadPendingAsync(string userCode, CancellationToken cancellationToken = default)
    {
        using var response = await _http
            .GetAsync(new Uri($"api/auth/device/pending/{userCode}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PendingRequest>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The pending request came back empty.");
    }

    /// <summary>
    /// Approves a request with exactly these scopes, which is how a narrowed grant is
    /// exercised against the real server.
    /// </summary>
    /// <param name="expiresIn">One of <c>30d</c>, <c>90d</c>, <c>1y</c> or <c>never</c>.</param>
    public async Task ApproveAsync(
        string userCode,
        IEnumerable<string> approvedScopes,
        string? deviceName = null,
        string? expiresIn = "30d",
        CancellationToken cancellationToken = default)
    {
        using var response = await _http
            .PostAsJsonAsync(
                new Uri("api/auth/device/approve", UriKind.Relative),
                new
                {
                    user_code = userCode,
                    approved_scopes = approvedScopes.ToArray(),
                    device_name = deviceName,
                    expires_in = expiresIn,
                },
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Declines a request, for the access_denied path.</summary>
    public async Task DenyAsync(string userCode, CancellationToken cancellationToken = default)
    {
        using var response = await _http
            .PostAsJsonAsync(
                new Uri("api/auth/device/deny", UriKind.Relative),
                new { user_code = userCode },
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();

    /// <summary>What the approval screen is shown about a pending request.</summary>
    /// <param name="Allowed_scopes">
    /// The requested set intersected with the approver's own scopes. A token can never
    /// exceed its owner's, so this is the ceiling on what can be granted.
    /// </param>
    public sealed record PendingRequest(
        string Client_device_identifier,
        string Name,
        string Client,
        string? Platform,
        string? Client_version,
        IReadOnlyList<string> Requested_scopes,
        IReadOnlyList<string> Allowed_scopes,
        string Expires_at);
}
