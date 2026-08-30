using RomM.Client;
using RomM.Client.Catalog;

namespace RomMBat.Core.Content;

/// <summary>
/// The firmware RomM holds, indexed the way a plan needs to look it up.
/// </summary>
/// <remarks>
/// One <c>GET /api/platforms</c> rather than a request per platform, so covering every folder
/// in a sync costs one round trip.
/// <para>
/// <b>A failure here is not fatal and comes back as a value.</b> The gap report is still worth
/// making from the bundled manifest and what is on disk, which is exactly what an offline run
/// does, so this never throws a reachability failure at its caller.
/// </para>
/// <para>
/// Lifted out of <c>BiosCommand</c> when the sync orchestration moved into Core, because both
/// the console and the interface need the same index and neither is the natural owner of it.
/// </para>
/// </remarks>
public static class BiosCandidates
{
    /// <summary>Reads the firmware index, or says why it could not.</summary>
    public static async Task<(IReadOnlyDictionary<string, FirmwareRow>? Index, string? Problem)> ReadAsync(
        RomMConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            var response = await connection
                .ListPlatformsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? (BiosPlanner.IndexByMd5(response.Value!), null)
                : (null, $"RomM's platform list could not be read: {response.Message}");
        }
        catch (RomMUnreachableException ex)
        {
            return (null, ex.Message);
        }
    }
}
