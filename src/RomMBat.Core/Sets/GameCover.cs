using RomM.Client;
using RomM.Client.Content;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sets;

/// <summary>Where a browsed game's box art comes from, and the bytes of it.</summary>
/// <remarks>
/// <b>The copy on this device first</b>: the thumbnail, else the image, as the store recorded
/// them under <c>roms/&lt;system&gt;/images</c>. A game that is not here falls back to RomM's
/// <c>path_cover_small</c>, which is the file the gamelist's thumbnail is fetched from anyway.
/// <para>
/// <b>Nothing is written.</b> A cover read from RomM is held in memory by whoever asked for it
/// and is gone when RomMBat exits, ruled with Spinnich: browse only reaches RomM online, so the
/// server can always serve it again, and a game that is synced has its own copy on disk.
/// </para>
/// </remarks>
public static class GameCover
{
    /// <summary>The most bytes one cover may run to before it is refused.</summary>
    /// <remarks>
    /// A small cover on the live library is tens of kilobytes. The cap is what keeps a wrong
    /// path, or a server answering with something else entirely, from filling memory.
    /// </remarks>
    public const long MaximumBytes = 4 * 1024 * 1024;

    /// <summary>The cover this device already holds for a game, or null when it holds none.</summary>
    /// <returns>An absolute path, resolved at the point of use and never stored (rule 1).</returns>
    public static string? Local(InstallSession session, int romId)
    {
        ArgumentNullException.ThrowIfNull(session);

        foreach (var kind in (LocalFileKind[])[LocalFileKind.Thumbnail, LocalFileKind.Image])
        {
            foreach (var file in session.Store.Files.ForRom(romId, kind))
            {
                var absolute = session.Install.Resolve(file.Path);

                // Checked, because the store can be behind a folder someone tidied by hand.
                if (File.Exists(absolute))
                {
                    return absolute;
                }
            }
        }

        return null;
    }

    /// <summary>RomM's small cover for a game, or null when the row names none.</summary>
    public static MediaResource? Remote(BrowseGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return string.IsNullOrWhiteSpace(game.Row?.CoverSmallPath)
            ? null
            : new MediaResource { Kind = MediaKind.Thumbnail, SourcePath = game.Row.CoverSmallPath };
    }

    /// <summary>
    /// The bytes of a game's cover, from this device or from RomM, or null when there are none.
    /// </summary>
    /// <param name="connection">Null when there is no server, which leaves only the local copy.</param>
    public static async Task<byte[]?> ReadAsync(
        InstallSession session,
        BrowseGame game,
        RomMConnection? connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(game);

        if (Local(session, game.RomId) is { } path)
        {
            try
            {
                return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Removed or locked between the check and the read. RomM's copy is the fallback
                // for exactly a game whose own is missing.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (connection is null || Remote(game) is not { } resource)
        {
            return null;
        }

        using var buffer = new MemoryStream();

        try
        {
            var response = await connection
                .DownloadMediaAsync(resource, buffer, MaximumBytes, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess ? buffer.ToArray() : null;
        }
        catch (RomMUnreachableException)
        {
            // Art is never the thing a screen fails over. The box says there is none.
            return null;
        }
    }
}
