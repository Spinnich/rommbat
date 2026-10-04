using RomM.Client;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sets;

/// <summary>One game looked up by id, and why the server's half is missing when it is.</summary>
/// <param name="Game">
/// Null when neither the server nor this device knows the id. Otherwise it carries the server
/// row when there was one to read, exactly as an online browse page would.
/// </param>
/// <param name="Problem">Why the server was not asked or did not answer, or null. Never a remedy.</param>
/// <param name="Status">The server's status when it answered with a refusal, for the caller's exit code.</param>
public sealed record GameLookup(BrowseGame? Game, string? Problem = null, RomMResponseStatus? Status = null);

/// <summary>What pressing install on one game decided, before anything is fetched.</summary>
/// <param name="NothingToFetch">
/// True when the picked set already held the game and it is already on disk, so a pass would
/// fetch nothing and must not claim to have installed it. #116.
/// </param>
public sealed record GamePick(PickOutcome Outcome, bool NothingToFetch);

/// <summary>What taking one game off would do.</summary>
/// <param name="Unvouchable">Shared save containers this removal cannot answer for. See <see cref="EvictionService.Unvouchable"/>.</param>
public sealed record GameRemovalPreview(EvictionReport Report, IReadOnlyList<string> Unvouchable);

/// <summary>
/// One game at a time: look it up, put it on the device, take it off.
/// </summary>
/// <remarks>
/// <b>The decisions the browse detail screen used to make inline</b>, moved here when the
/// console grew the same three verbs. Each composes a service that already existed:
/// <see cref="PickedSetService"/> for membership, <see cref="EvictionService"/> for removal and
/// <see cref="LibrarySyncService.InstallAsync"/> for the fetch, which the caller runs itself
/// because the progress it reports is a screen's or a console's.
/// </remarks>
public sealed class GameService
{
    private static readonly RomPlacement Nowhere = new([], 0);

    private readonly InstallSession _session;

    public GameService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// The game with this id, from the server when there is one and from this device when not.
    /// </summary>
    /// <remarks>
    /// Degrades rather than refusing, as <see cref="BrowseService"/> does: an unreachable server
    /// still leaves what this device holds, which is a true answer about the game.
    /// </remarks>
    /// <param name="connection">Null asks only this device.</param>
    public async Task<GameLookup> FindAsync(
        RomMConnection? connection,
        int romId,
        CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            return new GameLookup(Local(romId));
        }

        RomMResponse<RomM.Client.Catalog.RomRow> response;

        try
        {
            response = await connection.GetRomAsync(romId, cancellationToken).ConfigureAwait(false);
        }
        catch (RomMUnreachableException unreachable)
        {
            return new GameLookup(Local(romId), unreachable.Message);
        }

        if (!response.IsSuccess)
        {
            return new GameLookup(Local(romId), response.Message, response.Status);
        }

        var row = response.Value!;
        var placement = _session.Store.Files.PlacementFor([row.Id]).GetValueOrDefault(row.Id, Nowhere);

        return new GameLookup(new BrowseGame(
            row.Id,
            row.DisplayName,
            row.PlatformSlug,
            row.SizeBytes,
            placement.Folders,
            placement.Bytes,
            _session.Store.SyncSets.SetsClaiming([row.Id]).GetValueOrDefault(row.Id, []),
            row));
    }

    /// <summary>
    /// Puts one game into the picked set, and says whether a pass would have anything to do.
    /// </summary>
    /// <remarks>
    /// Already picked but not on disk still wants a pass: that is the state a stopped or
    /// budget-blocked run leaves behind, and the one a second press is meant to finish. Whether
    /// it is on disk is asked of the store, not of any row in hand, which an install that has
    /// just run makes stale.
    /// </remarks>
    public GamePick Pick(RomM.Client.Catalog.RomRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);

        var outcome = new PickedSetService(_session).Pick(row, now);

        return new GamePick(
            outcome,
            outcome.Member is not null
                && outcome.AlreadyPicked
                && _session.Store.Files.ForRom(row.Id, LocalFileKind.Rom).Count > 0);
    }

    /// <summary>What taking this game off would do, before it does it.</summary>
    /// <remarks>
    /// The picked set is released, so its own claim does not hold the game back against the
    /// person un-picking it. Every other enabled set's claim still does, and says so.
    /// </remarks>
    public GameRemovalPreview PreviewRemoval(int romId)
    {
        var eviction = new EvictionService(_session);
        var releasing = new PickedSetService(_session).Find() is { } set ? new[] { set.Id } : [];

        return new GameRemovalPreview(
            eviction.PreviewRemoval([romId], releasing),
            eviction.Unvouchable([romId]));
    }

    /// <summary>Carries out a removal preview, then takes the game out of the picked set.</summary>
    /// <remarks>
    /// The pick goes whatever the files did. The user said take it off, and a pick left behind
    /// would have the next sync fetch it again; a game another set still wants stays on disk and
    /// that set goes on claiming it, which the preview already said.
    /// </remarks>
    public async Task<EvictionApplied> ApplyRemovalAsync(
        int romId,
        EvictionReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var applied = await new EvictionService(_session).ApplyAsync(report, cancellationToken).ConfigureAwait(false);
        new PickedSetService(_session).Unpick(romId, DateTimeOffset.UtcNow);

        return applied;
    }

    /// <summary>What this device holds of the game, or null when it holds nothing.</summary>
    private BrowseGame? Local(int romId)
    {
        var (_, games) = _session.Store.Files.InstalledGames(null, null, 1, 0, romId);

        if (games.Count == 0)
        {
            return null;
        }

        var game = games[0];
        var placement = _session.Store.Files.PlacementFor([romId]).GetValueOrDefault(romId, Nowhere);

        return new BrowseGame(
            romId,
            game.DisplayName,
            game.PlatformSlug,
            placement.Bytes,
            placement.Folders,
            placement.Bytes,
            _session.Store.SyncSets.SetsClaiming([romId]).GetValueOrDefault(romId, []),
            Row: null,
            FsName: game.FsName);
    }
}
