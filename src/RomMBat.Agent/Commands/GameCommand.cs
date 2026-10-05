using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Sets;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>game</c>: one game by its RomM id. Show it, put it on the device, take it off.
/// </summary>
/// <remarks>
/// <b>The browse detail screen's verbs, from a terminal.</b> A printer over
/// <see cref="GameService"/>, which holds the decisions that screen and this command share, and over
/// <see cref="LibrarySyncService.InstallAsync"/>, which is the same one-game pass the screen
/// runs. The per-game memory card is the screen's third verb and is <c>saves convert</c> here.
/// <para>
/// <b><c>install</c> joins the hand-picked set and fetches straight away</b>, which is what
/// one press means on the screen, and pushes the definitions as <c>sets add</c> does. <b><c>remove</c> previews and writes on <c>--apply</c></b>,
/// like everything else in this agent that deletes.
/// </para>
/// </remarks>
internal static class GameCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        if (command.Positional.Count < 2)
        {
            return Usage(command.Positional.Count == 0 ? "game needs a verb and a rom id" : "game needs a rom id");
        }

        var verb = command.Positional[0];

        if (verb is not ("show" or "install" or "remove"))
        {
            return Usage($"unknown 'game' verb '{verb}'");
        }

        if (!int.TryParse(command.Positional[1], NumberStyles.None, CultureInfo.InvariantCulture, out var romId) || romId <= 0)
        {
            return Usage($"'{command.Positional[1]}' is not a rom id. Use the number in the first column of 'browse'.");
        }

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        return verb switch
        {
            "show" => await ShowAsync(context, command, romId, cancellationToken).ConfigureAwait(false),
            "install" => await InstallAsync(context, command, romId, cancellationToken).ConfigureAwait(false),
            _ => await RemoveAsync(context, command, romId, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<int> ShowAsync(
        AgentContext context,
        CommandLine command,
        int romId,
        CancellationToken cancellationToken)
    {
        RomMConnection? connection = null;

        try
        {
            if (!command.Has("offline"))
            {
                connection = context.Authenticate(command, Console.Error, out var exitCode);
                if (connection is null)
                {
                    return exitCode;
                }
            }

            var lookup = await new GameService(context.Session)
                .FindAsync(connection, romId, cancellationToken)
                .ConfigureAwait(false);

            if (lookup.Problem is { } problem)
            {
                Console.Error.WriteLine(problem);
            }

            if (lookup.Game is not { } game)
            {
                return NotFound(lookup, romId);
            }

            Show(context, game);
            return ExitCode.Ok;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private static async Task<int> InstallAsync(
        AgentContext context,
        CommandLine command,
        int romId,
        CancellationToken cancellationToken)
    {
        using var connection = context.Authenticate(command, Console.Error, out var exitCode);
        if (connection is null)
        {
            return exitCode;
        }

        var lookup = await new GameService(context.Session)
            .FindAsync(connection, romId, cancellationToken)
            .ConfigureAwait(false);

        if (lookup.Game?.Row is not { } row)
        {
            Console.Error.WriteLine(lookup.Problem ?? $"RomM has no game with id {romId}.");
            return NotFound(lookup, romId);
        }

        var pick = new GameService(context.Session).Pick(row, DateTimeOffset.UtcNow);

        if (pick.Outcome.Member is not { } member)
        {
            Console.Error.WriteLine($"{row.DisplayName}: {pick.Outcome.Problem ?? "this game cannot be put on this device."}");
            return ExitCode.Refused;
        }

        if (pick.NothingToFetch)
        {
            Console.WriteLine(
                $"{row.DisplayName} is already on the device, and '{pick.Outcome.Set.Name}' already "
                    + "claims it. Nothing was fetched.");
            return ExitCode.Ok;
        }

        Console.WriteLine(pick.Outcome.AlreadyPicked
            ? $"{row.DisplayName} is in '{pick.Outcome.Set.Name}' but not on the device yet. Fetching it."
            : $"Added {row.DisplayName} to '{pick.Outcome.Set.Name}'. Fetching it.");

        // A pick roams as a resolve does (#444), beside the fetch rather than in front of it,
        // and not on the caller's token: stopping the fetch is not stopping the pick.
        var roaming = new RoamingConfigService(context.Session, AgentContext.ConnectOverride)
            .PushAsync(command.Value("passphrase"), CancellationToken.None);

        var report = await new LibrarySyncService(context.Session)
            .InstallAsync(
                pick.Outcome.Set,
                member,
                connection,
                new Immediate<SyncEvent>(SyncCommand.Printer.Show),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await roaming.ConfigureAwait(false)).Note is { } note)
        {
            Console.WriteLine($"  {note}");
        }

        return SyncCommand.ExitCodeFor(report);
    }

    private static async Task<int> RemoveAsync(
        AgentContext context,
        CommandLine command,
        int romId,
        CancellationToken cancellationToken)
    {
        var games = new GameService(context.Session);
        var picked = new PickedSetService(context.Session);

        var game = (await games.FindAsync(null, romId, cancellationToken).ConfigureAwait(false)).Game;
        var isPicked = picked.Picks().Contains(romId);

        // A pick that never landed is still something to take back, so only a game that is
        // neither here nor picked is a wrong id.
        if (game is null && !isPicked)
        {
            Console.Error.WriteLine($"This device holds no game with id {romId}, and has not picked one.");
            return ExitCode.Usage;
        }

        var name = game?.DisplayName ?? $"game {romId}";
        var preview = games.PreviewRemoval(romId);
        var plan = preview.Report.Plan;

        if (plan.Selected.Count == 0)
        {
            Console.WriteLine(game?.IsHere == true
                ? $"{name} would stay on the device."
                : $"{name} is not on the device.");
        }
        else
        {
            Console.WriteLine($"Taking {name} off frees {ByteSize.Format(plan.BytesFreed)}:");
        }

        foreach (var candidate in plan.Selected)
        {
            var media = candidate.Media.Count > 0 ? $" (+{candidate.Media.Count} media)" : string.Empty;

            Console.WriteLine(
                $"  {ByteSize.Format(candidate.Bytes),10}  {EvictionService.Describe(candidate)}  {candidate.File.FileName}{media}");
        }

        foreach (var candidate in plan.Refused)
        {
            Console.WriteLine($"  {"kept",10}  {candidate.File.FileName}: {candidate.Refusal}");
        }

        foreach (var container in preview.Unvouchable)
        {
            Console.WriteLine(
                $"  {"left",10}  {container}: this save belongs to no one game, so RomMBat cannot say "
                    + "whether removing this game costs anything in it.");
        }

        Console.WriteLine("Saves and save states are never removed.");

        if (isPicked)
        {
            Console.WriteLine($"It also comes out of '{picked.Find()!.Name}', so no sync fetches it again.");
        }

        if (!command.Has("apply"))
        {
            Console.WriteLine();
            Console.WriteLine($"Nothing was removed. Run 'game remove {romId} --apply' to carry this out.");
            return ExitCode.Ok;
        }

        var applied = await games.ApplyRemovalAsync(romId, preview.Report, cancellationToken).ConfigureAwait(false);

        Console.WriteLine();

        if (applied.Evicted is { } evicted)
        {
            Console.WriteLine(evicted.Summary);

            foreach (var problem in evicted.Problems)
            {
                Console.Error.WriteLine($"  {problem}");
            }
        }
        else
        {
            Console.WriteLine("No files were removed.");
        }

        if (applied.Gamelists is { } gamelists)
        {
            Console.WriteLine();
            GamelistCommand.Report(gamelists);
        }

        return ExitCode.Ok;
    }

    /// <summary>The rows the browse detail screen shows, as lines.</summary>
    private static void Show(AgentContext context, BrowseGame game)
    {
        var placement = context.Store.Files.PlacementFor([game.RomId])
            .GetValueOrDefault(game.RomId, new RomMBat.Core.Store.RomPlacement([], 0));

        Console.WriteLine(game.DisplayName);
        Console.WriteLine($"  release:      {game.Release}");
        Console.WriteLine($"  platform:     {game.PlatformSlug}");
        Console.WriteLine($"  size in RomM: {(game.Row is null ? "not known" : ByteSize.Format(game.SizeBytes))}");

        if (placement.IsHere)
        {
            Console.WriteLine($"  {(placement.Folders.Count == 1 ? "in folder:   " : "in folders:  ")} {string.Join(", ", placement.Folders)}");
            Console.WriteLine($"  taking up:    {ByteSize.Format(placement.Bytes)}, the game and its artwork in every folder");

            if (placement.Folders.Count > 1)
            {
                Console.WriteLine("                Two sync sets put it in two folders, which is correct for both. It takes the room twice.");
            }
        }
        else
        {
            Console.WriteLine("  on device:    no");
        }

        // Eviction is only a risk to a game that is here to be taken.
        var unwanted = placement.IsHere ? "no sync set, so the next eviction may take it" : "no sync set";
        Console.WriteLine($"  wanted by:    {(game.Sets.Count == 0 ? unwanted : string.Join(", ", game.Sets))}");

        if (game.Row is { } row)
        {
            Console.WriteLine($"  id:           {row.Id.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"  md5:          {row.Md5Hash ?? "none published"}");
            Console.WriteLine($"  sha1:         {row.Sha1Hash ?? "none published"}");
        }
    }

    /// <summary>
    /// The exit for a lookup that found nothing to act on.
    /// </summary>
    /// <remarks>
    /// An id the server does not have is a wrong command line, as an unknown set name is. A
    /// server that could not be asked is the server's failure, and says which.
    /// </remarks>
    private static int NotFound(GameLookup lookup, int romId)
    {
        if (lookup.Problem is null)
        {
            Console.Error.WriteLine($"This device holds no game with id {romId}. Leave off --offline to ask RomM.");
            return ExitCode.Usage;
        }

        return lookup.Status switch
        {
            null => ExitCode.Offline,
            RomMResponseStatus.NotFound => ExitCode.Usage,
            { } status => ExitCode.For(status),
        };
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"rommbat-agent: {message}");
        Console.Error.WriteLine("  game show <rom-id> [--offline]");
        Console.Error.WriteLine("  game install <rom-id>");
        Console.Error.WriteLine("  game remove <rom-id> [--apply]");
        return ExitCode.Usage;
    }
}
