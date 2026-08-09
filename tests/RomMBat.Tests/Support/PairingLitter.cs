using System.Net;
using System.Net.Http.Headers;
using RomM.Client;

namespace RomMBat.Tests.Support;

/// <summary>
/// Tracks what a live pairing test created on the server, and removes it afterwards.
/// </summary>
/// <remarks>
/// Every approval mints a real <c>Device</c> row and a bound <c>ClientToken</c> carrying the
/// full RomMBat scope set, and the client's copy of that token dies with the temp tree. A
/// suite that does not clean up leaves live credentials behind on whatever instance it ran
/// against, and they accumulate one set per run.
/// <para>
/// The ordering is forced by which credential holds what. Only the token a pairing just
/// issued carries <c>devices.write</c>; the approver holds <c>me.read</c> and
/// <c>me.write</c>, which is what revokes tokens. So token ids are captured first (while the
/// bindings are still readable), devices are deleted second (while their tokens still work),
/// and revocation happens last against the captured ids.
/// </para>
/// </remarks>
internal sealed class PairingLitter
{
    private readonly List<(string DeviceId, string AccessToken, bool CanDeleteDevice)> _created = [];

    /// <summary>
    /// Records a pairing that has to be undone.
    /// </summary>
    /// <remarks>
    /// A narrowed grant produces a token that cannot delete its own device, so the scopes
    /// are recorded alongside it and teardown picks a capable token when one exists.
    /// </remarks>
    public void Track(string deviceId, string accessToken, IEnumerable<string> grantedScopes) =>
        _created.Add((deviceId, accessToken, grantedScopes.Contains(RomMScopes.DevicesWrite)));

    /// <summary>True when there is nothing to clean up.</summary>
    public bool IsEmpty => _created.Count == 0;

    /// <summary>
    /// Deletes every device and revokes every token these tests created.
    /// </summary>
    /// <remarks>
    /// Best effort, and never throws. A cleanup failure must not turn a passing test red, nor
    /// mask the real assertion failure in a failing one. It reports instead, so leftover
    /// litter is visible rather than silent.
    /// </remarks>
    /// <returns>Anything that could not be removed, for the test to surface.</returns>
    public async Task<IReadOnlyList<string>> CleanUpAsync(
        Uri origin,
        string approverToken,
        CancellationToken cancellationToken = default)
    {
        if (_created.Count == 0)
        {
            return [];
        }

        var problems = new List<string>();
        var deviceIds = _created.Select(entry => entry.DeviceId).ToHashSet(StringComparer.Ordinal);

        using var approver = new ApprovingUser(origin, approverToken);

        // Captured before anything is deleted: once the device row is gone, the binding that
        // identifies its tokens may be gone with it.
        var tokenIds = new List<int>();
        try
        {
            var tokens = await approver.ListTokensAsync(cancellationToken).ConfigureAwait(false);
            tokenIds.AddRange(tokens
                .Where(token => token.Device_id is not null && deviceIds.Contains(token.Device_id))
                .Select(token => token.Id));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            problems.Add($"listing tokens: {ex.Message}");
        }

        // Deduped: a re-pairing test pairs twice onto one device on purpose, and the most
        // recent token is the one certain to still be valid.
        foreach (var deviceId in deviceIds)
        {
            var forDevice = _created
                .Where(entry => string.Equals(entry.DeviceId, deviceId, StringComparison.Ordinal))
                .ToList();

            // Only a token granted devices.write can delete the device it belongs to. A test
            // that deliberately pairs narrow has to re-pair wider before teardown can tidy up.
            var capable = forDevice.LastOrDefault(entry => entry.CanDeleteDevice);
            if (capable.AccessToken is null)
            {
                problems.Add(
                    $"device {deviceId}: no tracked token carries {RomMScopes.DevicesWrite}, so it cannot be deleted");
                continue;
            }

            var problem = await DeleteDeviceAsync(origin, deviceId, capable.AccessToken, cancellationToken)
                .ConfigureAwait(false);

            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        foreach (var tokenId in tokenIds)
        {
            try
            {
                await approver.RevokeTokenAsync(tokenId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                problems.Add($"revoking token {tokenId}: {ex.Message}");
            }
        }

        _created.Clear();
        return problems;
    }

    private static async Task<string?> DeleteDeviceAsync(
        Uri origin,
        string deviceId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        // A raw call rather than a method on RomMConnection: RomMBat never deletes its own
        // device, so the shipped client should not grow the surface for a test's benefit.
        using var http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http
                .DeleteAsync(RomMConnection.JoinOrigin(origin, $"api/devices/{deviceId}"), cancellationToken)
                .ConfigureAwait(false);

            // Already gone is the outcome we wanted.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return $"device {deviceId}: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"device {deviceId}: {ex.Message}";
        }
    }
}
