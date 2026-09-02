using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sets;

/// <summary>Which of the two things a browse page is showing.</summary>
public enum BrowseSource
{
    /// <summary>A page of the RomM library, read live.</summary>
    Library,

    /// <summary>What this device holds, read from the local store.</summary>
    ThisDevice,
}

/// <summary>One game as a browse row shows it.</summary>
/// <param name="Folders">
/// The RetroBat folders holding it, which is empty when it is not here and can hold two.
/// </param>
/// <param name="Sets">Which sync sets claim it, so a person can see why it is here.</param>
/// <param name="Row">
/// The server row, kept so a pick can write its member from it with nothing more asked of the
/// server. Null on the offline page, where there is nothing to pick: everything on it is
/// already on the device.
/// </param>
public sealed record BrowseGame(
    int RomId,
    string DisplayName,
    string PlatformSlug,
    long SizeBytes,
    IReadOnlyList<string> Folders,
    long BytesOnDevice,
    IReadOnlyList<string> Sets,
    RomRow? Row,
    string FsName = "")
{
    /// <summary>
    /// What tells this release apart from another of the same game.
    /// </summary>
    /// <remarks>
    /// <b>A display name cannot do it, and a library is full of the cases where it matters.</b>
    /// Two rows both reading "Chrono Trigger" may be a USA and a Japan dump, revision 1 and
    /// revision 2, or a translation patch, and picking the wrong one is a download and a
    /// removal to undo. Found on the first hands-on pass of browse.
    /// <para>
    /// Built from <c>fs_name</c> rather than from <c>regions</c> and <c>languages</c>, because
    /// the file name is what actually carries the No-Intro and Redump tags a person recognises,
    /// it is complete where the metadata fields are sparse (languages are present on 18.3% of a
    /// real library), and it is what lands on disk. The parenthesised groups only: the stem
    /// repeats the display name and the extension is already its own column.
    /// </para>
    /// </remarks>
    public string Tags
    {
        get
        {
            var name = Row?.FsName ?? FsName;
            var tags = new List<string>();
            var depth = 0;
            var start = 0;

            for (var index = 0; index < name.Length; index++)
            {
                if (name[index] == '(' || name[index] == '[')
                {
                    if (depth++ == 0)
                    {
                        start = index + 1;
                    }
                }
                else if ((name[index] == ')' || name[index] == ']') && depth > 0 && --depth == 0)
                {
                    var inner = name[start..index].Trim();

                    if (inner.Length > 0)
                    {
                        tags.Add(inner);
                    }
                }
            }

            return string.Join(", ", tags);
        }
    }

    /// <summary>True when this device holds it.</summary>
    public bool IsHere => Folders.Count > 0;

    /// <summary>True when it is in more than one folder, which is legitimate and costs twice.</summary>
    public bool IsDoubled => Folders.Count > 1;
}

/// <summary>One page of a browse, and which of the two things it is.</summary>
/// <param name="IsLastPage">
/// True when there is nothing past this page. What a screen does with that is its own decision;
/// what Core owes it is knowing.
/// </param>
/// <param name="Problem">
/// Why this is the local page when a server was expected, or null. Never a remedy: the wording
/// of what to do about it differs per front end.
/// </param>
public sealed record BrowsePage(
    BrowseSource Source,
    IReadOnlyList<BrowseGame> Games,
    int Offset,
    int Total,
    bool IsLastPage,
    string? Problem = null);

/// <summary>
/// Reading the library a page at a time, and falling back to this device when there is no server.
/// </summary>
/// <remarks>
/// <b>One page in memory, ever.</b> M2's rule is that the catalog is never mirrored wholesale
/// and <c>RomRow</c> and <c>RomPager</c> both say the same thing again: an 83k library is 333
/// pages and the longest description in a 5,000-row sample is 11,719 characters. Nothing here
/// accumulates, and a test asserts the row count never exceeds the page size across several
/// pages.
/// <para>
/// <b>It degrades rather than refusing, and it says which of the two it is showing.</b> With a
/// server it pages <c>GET /api/roms</c>; without one it lists what this device holds, which is
/// what EmulationStation shows anyway. A browse that refused offline would be the one screen on
/// this surface that stopped working away from the server, and offline is a working state.
/// </para>
/// <para>
/// <b>Fifty rows a page, not <see cref="RomPager.DefaultPageSize"/>'s 250.</b> That number was
/// measured for a resolve, which walks a whole scope and wants the fewest requests; a person
/// scrolling wants the shortest wait. A screen shows eight rows at a time, so 250 is 31 screens
/// of scrolling per fetch against six.
/// </para>
/// <para>
/// <b>Measured on the live 96,060-rom instance rather than reasoned from M0.</b> Unscoped, warm:
/// <b>50 rows in 280 ms, 250 rows in 611 ms</b> (cold, 439 ms and 629 ms). So 250 is cheaper per
/// row and more than twice the wait for the page a person is actually looking at, which is the
/// one that decides whether the screen feels instant. The estimate this was first written from,
/// "about half a second at ~10 ms per ROM", was pessimistic; the real figure is better and the
/// choice is unchanged.
/// </para>
/// <para>
/// Marking a page costs almost nothing on top: the whole <see cref="PageAsync"/> call measured
/// 285 ms against the raw page's 280 ms, which is the two aggregate queries #111 is about doing
/// their job.
/// </para>
/// </remarks>
public sealed class BrowseService
{
    /// <summary>
    /// Rows per page.
    /// </summary>
    /// <remarks>
    /// Reasoned from <c>ListWindow.Capacity</c> being 8 rows, then measured on the live
    /// 96,060-rom instance at 280 ms against 250 rows' 611 ms. See this type's remarks.
    /// </remarks>
    public const int PageSize = 50;

    private readonly InstallSession _session;

    public BrowseService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// Reads one page, from the server when there is one and from this device when there is not.
    /// </summary>
    /// <param name="connection">Null browses this device, which is also what an unreachable server gets.</param>
    /// <param name="platformId">A RomM platform id to narrow to, or null for everything.</param>
    /// <param name="folder">The RetroBat folder the platform maps to, which is how the offline page narrows.</param>
    /// <param name="search">A term typed now.</param>
    public async Task<BrowsePage> PageAsync(
        RomMConnection? connection,
        int offset,
        string? platformId = null,
        string? folder = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            return Local(offset, folder, search);
        }

        var query = new CatalogQuery
        {
            Scope = platformId is null ? CatalogScopeKind.Filter : CatalogScopeKind.Platform,
            ScopeId = platformId,
            SearchTerm = search,
        };

        RomMResponse<RomPage> response;

        try
        {
            response = await connection
                .GetRomPageAsync(query, PageSize, offset, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RomMUnreachableException unreachable)
        {
            // Falls back rather than throwing. Offline is a working state, and the screen has
            // something true to show either way.
            return Local(offset, folder, search) with { Problem = unreachable.Message };
        }

        if (!response.IsSuccess)
        {
            return Local(offset, folder, search) with { Problem = response.Message };
        }

        var page = response.Value!;
        var rows = page.Items;
        var romIds = rows.Select(row => row.Id).ToList();

        var placements = _session.Store.Files.PlacementFor(romIds);
        var claims = _session.Store.SyncSets.SetsClaiming(romIds);

        return new BrowsePage(
            BrowseSource.Library,
            [
                .. rows.Select(row =>
                {
                    var placement = placements.GetValueOrDefault(row.Id, Nowhere);

                    return new BrowseGame(
                        row.Id,
                        row.DisplayName,
                        row.PlatformSlug,
                        row.SizeBytes,
                        placement.Folders,
                        placement.Bytes,
                        claims.GetValueOrDefault(row.Id, []),
                        row);
                }),
            ],
            offset,
            page.Total,

            // An empty page ends it whatever the total says, which is what stops a library that
            // shrank mid-walk from reading as one more page forever. Same rule RomPager uses.
            rows.Count == 0 || offset + rows.Count >= page.Total);
    }

    private static readonly RomPlacement Nowhere = new([], 0);

    /// <summary>What this device holds, which is the page a browse falls back to.</summary>
    private BrowsePage Local(int offset, string? folder, string? search)
    {
        var (total, games) = _session.Store.Files.InstalledGames(folder, search, PageSize, offset);
        var romIds = games.Select(game => game.RomId).ToList();

        var placements = _session.Store.Files.PlacementFor(romIds);
        var claims = _session.Store.SyncSets.SetsClaiming(romIds);

        return new BrowsePage(
            BrowseSource.ThisDevice,
            [
                .. games.Select(game =>
                {
                    var placement = placements.GetValueOrDefault(game.RomId, Nowhere);

                    return new BrowseGame(
                        game.RomId,
                        game.DisplayName,
                        game.PlatformSlug,

                        // What it takes here, because there is no server row to say what RomM
                        // thinks it weighs and inventing one would be worse than the truth.
                        placement.Bytes,
                        placement.Folders,
                        placement.Bytes,
                        claims.GetValueOrDefault(game.RomId, []),
                        Row: null,

                        // The offline page has no server row, and it still has to tell two dumps
                        // of one game apart: the file name is where the tags live either way.
                        FsName: game.FsName);
                }),
            ],
            offset,
            total,
            games.Count == 0 || offset + games.Count >= total);
    }
}
