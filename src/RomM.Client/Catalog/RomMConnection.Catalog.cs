using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using RomM.Client.Catalog;
using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>The catalog reads: platforms, collections, and paged ROMs.</summary>
public sealed partial class RomMConnection
{
    /// <summary>
    /// Lists platforms. Needs <c>platforms.read</c>.
    /// </summary>
    /// <remarks>
    /// The mapping surface is built from this: one row per platform the user's library
    /// actually has, resolved against the live <c>es_systems.cfg</c>.
    /// </remarks>
    public Task<RomMResponse<IReadOnlyList<PlatformRow>>> ListPlatformsAsync(
        DateTimeOffset? updatedAfter = null,
        CancellationToken cancellationToken = default)
    {
        var path = updatedAfter is { } since
            ? "api/platforms?updated_after=" + Uri.EscapeDataString(since.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
            : "api/platforms";

        return GetAuthenticatedAsync<IReadOnlyList<PlatformRow>>(path, cancellationToken);
    }

    /// <summary>
    /// Lists collections. Needs <c>collections.read</c>.
    /// </summary>
    /// <remarks>
    /// <b>Expensive, and only for naming.</b> RB-355 measured a single collection at
    /// 714.8 KB, 99% of it two inlined arrays of cover-art paths, one entry per member ROM
    /// and duplicated at two sizes. There is no pagination. Call it to let someone pick a
    /// collection by name, then resolve membership by paging <c>GET /api/roms</c>; never read
    /// <c>rom_ids</c> off the payload.
    /// </remarks>
    public Task<RomMResponse<ICollection<CollectionSchema>>> ListCollectionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<ICollection<CollectionSchema>>("api/collections", cancellationToken);

    /// <summary>Lists smart collections, whose membership is a server-side saved search.</summary>
    public Task<RomMResponse<ICollection<SmartCollectionSchema>>> ListSmartCollectionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<ICollection<SmartCollectionSchema>>("api/collections/smart", cancellationToken);

    /// <summary>Fetches one page of ROMs, with every costly sidecar off.</summary>
    public Task<RomMResponse<RomPage>> GetRomPageAsync(
        CatalogQuery query,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GetAuthenticatedAsync<RomPage>("api/roms?" + query.ToQueryString(limit, offset), cancellationToken);
    }

    /// <summary>
    /// Writes this device's roaming configuration. Needs <c>devices.write</c>.
    /// </summary>
    /// <remarks>
    /// <c>sync_config</c> is a free-form dictionary shared with anything else that writes to
    /// this device, so callers merge into what is already there rather than replacing it.
    /// <para>
    /// <b>Only <c>sync_config</c> goes on the wire, and the generated
    /// <c>DeviceUpdatePayload</c> is deliberately not used.</b> That type serializes every
    /// property, so the unset ones arrive as explicit nulls and the server writes them: the
    /// full shape answers <b>500</b>, while <c>sync_config</c> alone answers 200 and leaves
    /// the rest of the device intact. RB-73.
    /// </para>
    /// </remarks>
    public async Task<RomMResponse<DeviceSchema>> UpdateDeviceSyncConfigAsync(
        string deviceId,
        IReadOnlyDictionary<string, object?> syncConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(syncConfig);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<DeviceSchema>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sync_config"] = syncConfig };
        using var request = new HttpRequestMessage(HttpMethod.Put, Resolve("api/devices/" + Uri.EscapeDataString(deviceId)))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var device = await ReadAsync<DeviceSchema>(response, cancellationToken).ConfigureAwait(false);
            return RomMResponse.Success(device!);
        }

        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<DeviceSchema>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),
            HttpStatusCode.Forbidden => RomMResponse.Failure<DeviceSchema>(
                RomMResponseStatus.Forbidden,
                detail ?? "This token was not granted devices.write, so sync sets stay on this device."),
            _ => RomMResponse.Failure<DeviceSchema>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode}."),
        };
    }

    /// <summary>Reads one device, which is how the current <c>sync_config</c> is fetched before a merge.</summary>
    public Task<RomMResponse<DeviceSchema>> GetDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return GetAuthenticatedAsync<DeviceSchema>("api/devices/" + Uri.EscapeDataString(deviceId), cancellationToken);
    }
}
