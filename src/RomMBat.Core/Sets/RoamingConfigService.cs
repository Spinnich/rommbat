using RomM.Client;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>
/// Whether the definitions reached the server, and what to say when they did not.
/// </summary>
/// <param name="Note">
/// Null when there is nothing to say, which is the successful case. Otherwise a parenthetical
/// the caller prints or shows verbatim.
/// </param>
public sealed record RoamingPush(bool Pushed, string? Note);

/// <summary>
/// Mirrors the set definitions into <c>Device.sync_config</c> so they follow the user.
/// </summary>
/// <remarks>
/// <b>Best effort by design.</b> Failing to roam a definition is not a reason to fail the
/// operation that created it: the local store is the authority and the push is a mirror. Every
/// failure here comes back as a <see cref="RoamingPush"/> with a note, never as a throw and
/// never as a non-zero outcome.
/// <para>
/// Lifted out of <c>SetsCommand</c> unchanged. It opens its own connection because the caller
/// that defines a set has no reason to hold one, and pairing may have expired since.
/// </para>
/// </remarks>
public sealed class RoamingConfigService
{
    private readonly InstallSession _session;

    public RoamingConfigService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>Pushes the current definitions, or says why it could not.</summary>
    public async Task<RoamingPush> PushAsync(
        string? passphrase = null,
        CancellationToken cancellationToken = default)
    {
        var device = _session.Store.Device.Read();

        if (device?.RomMDeviceId is null || !device.Scopes.Allows(RomMFeature.DeviceSync))
        {
            return new RoamingPush(false, "(definitions stay on this device: devices.write was not granted)");
        }

        try
        {
            var attempt = _session.Authenticate(passphrase);

            if (attempt.Connection is null)
            {
                return new RoamingPush(false, null);
            }

            using var connection = attempt.Connection;

            var current = await connection
                .GetDeviceAsync(device.RomMDeviceId, cancellationToken)
                .ConfigureAwait(false);

            var document = RoamingSyncConfig.FromStore(_session.Store, DateTimeOffset.UtcNow);
            var merged = document.MergeInto(current.IsSuccess ? current.Value!.Sync_config : null);

            var pushed = await connection
                .UpdateDeviceSyncConfigAsync(device.RomMDeviceId, merged, cancellationToken)
                .ConfigureAwait(false);

            return new RoamingPush(
                pushed.IsSuccess,
                pushed.IsSuccess ? null : $"(definitions stay on this device: {pushed.Message})");
        }
        catch (RomMUnreachableException)
        {
            return new RoamingPush(false, "(definitions stay on this device until the server is reachable)");
        }
    }
}
