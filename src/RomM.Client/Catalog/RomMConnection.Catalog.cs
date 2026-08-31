using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using RomM.Client.Catalog;
using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>The catalog reads M2 needs: platforms, collections, and paged ROMs.</summary>
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
    /// <b>Expensive, and only for naming.</b> M0 probe 5 measured a single collection at
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

    /// <summary>
    /// Lists virtual collections of one type.
    /// </summary>
    /// <remarks>
    /// <paramref name="type"/> is required by the server: this route answers 422 without it,
    /// which the other two collection routes do not, so the three are not interchangeable.
    /// </remarks>
    public Task<RomMResponse<ICollection<VirtualCollectionSchema>>> ListVirtualCollectionsAsync(
        string type,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var path = "api/collections/virtual?type=" + Uri.EscapeDataString(type);
        if (limit is { } cap)
        {
            path += "&limit=" + cap.ToString(CultureInfo.InvariantCulture);
        }

        return GetAuthenticatedAsync<ICollection<VirtualCollectionSchema>>(path, cancellationToken);
    }

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
    /// Fetches the filter-value sidecar once, for a filter picker.
    /// </summary>
    /// <remarks>
    /// The one call that deliberately turns a sidecar on. It costs 280 KB and describes the
    /// whole library rather than a page, so it is fetched with <c>limit=1</c> and cached for
    /// the session. Nothing that pages ever asks for it.
    /// <para>
    /// <b>Read through <see cref="RomFilterValuesPage"/>, which ignores the row.</b> This used
    /// the generated page type and inherited its <c>int32</c> <c>fs_size_bytes</c>, so one ROM
    /// at or above 2 GiB left every facet empty on a library that had thousands of values.
    /// </para>
    /// </remarks>
    public async Task<RomMResponse<RomFilterValues>> GetFilterValuesAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await GetAuthenticatedAsync<RomFilterValuesPage>(
            "api/roms?" + query.ToQueryString(limit: 1, offset: 0, withFilterValues: true),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            return RomMResponse.Failure<RomFilterValues>(
                response.Status,
                response.Message ?? "The filter values were not returned.");
        }

        // Absent rather than empty is a server that answered without the sidecar, which is a
        // different thing from a library with nothing to filter by and reads differently.
        return response.Value!.FilterValues is { } values
            ? RomMResponse.Success(values)
            : RomMResponse.Failure<RomFilterValues>(
                response.Status,
                "The server answered without the filter values it was asked for.");
    }

    /// <summary>
    /// Writes this device's roaming configuration. Needs <c>devices.write</c>.
    /// </summary>
    /// <remarks>
    /// <c>sync_config</c> is a free-form dictionary shared with anything else that writes to
    /// this device, so callers merge into what is already there rather than replacing it.
    /// <para>
    /// <b>Only <c>sync_config</c> goes on the wire, and the generated
    /// <c>DeviceUpdatePayload</c> is deliberately not used.</b> That type serialises every
    /// property, so the unset ones arrive as explicit nulls and the server writes them.
    /// Measured against a live 5.1.1 instance: the full shape answers <b>500 Internal Server
    /// Error</b> with a plain-text body, while the same request carrying only
    /// <c>sync_config</c> answers 200 and leaves the device's name, platform and client
    /// version intact.
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
