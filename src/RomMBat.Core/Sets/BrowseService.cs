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
    /// The file name, which is what tells one release of a game from another.
    /// </summary>
    /// <remarks>
    /// <b>Shown whole and on every row, which took two goes to get right.</b> The first version
    /// put parsed-out tags on arcade rows and the file name only where there were none to parse,
    /// so the rule changed platform to platform and read as arbitrary: only arcade appeared to
    /// show both halves. A person cannot see a rule that fires on 100% of one platform and 0% of
    /// the next, and a list they cannot predict is worse than a long line.
    /// <para>
    /// <b>Both halves are needed and both were measured</b>, 750 rows a platform on the live
    /// library. Every arcade file name is a romset code with no tags at all, <c>10yard.zip</c>
    /// and <c>1943kai.zip</c>, and 87.3% differ from the display name, so a list labelled by file
    /// name is unreadable there and the title has to be the label. And 69 megadrive and 67 psx
    /// display names are shared by two or more rows, about one in eleven, so the title alone
    /// picks the wrong dump often enough to matter and the file name has to be under it.
    /// </para>
    /// <para>
    /// <b>Trimmed rather than shortened.</b> A psx name runs past a hundred characters, and the
    /// part that goes is the tail: a translation credit rather than the region and revision,
    /// which sit early. Taken over <c>regions</c> and <c>languages</c>, which are sparse and
    /// carry no revision, translation or dump flag: languages are on 18.3% of a real library.
    /// </para>
    /// </remarks>
    public string Release => Row?.FsName ?? FsName;

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

            // By name, not by id. `CatalogQuery`'s default is ascending id, and that is right
            // for a resolve: RomM hands out ascending ids, so a ROM added mid-walk lands past
            // the cursor instead of shifting every later page. Browse is not resumable and
            // nobody scrolls a library by id.
            //
            // It looked alphabetical and was not, which is worse than being obviously unsorted.
            // A library imported in name order carries ids in roughly that order, so the list
            // reads as sorted until it is not: measured on the live instance, an id-ordered snes
            // page put '3 Ninjas Kick Back' before '3-jigen Kakutou Ballz' and then dropped the
            // latter out of sequence entirely. Found from the couch as "there's something else
            // sorting the list on top of that".
            //
            // Named rather than left empty, which the schema documents as ordering by search
            // relevance on MySQL and by name everywhere else. A list whose order depends on
            // which database the server runs is not one a person can learn.
            OrderBy = "name",
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
