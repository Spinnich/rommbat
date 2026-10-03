using System.Globalization;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>outbox</c>: the entries the server refused, and the one way to clear them.
/// </summary>
/// <remarks>
/// <b>Only a failed entry can be dropped, and only with <c>--apply</c>.</b> A pending one is waiting for the network and a
/// sent one is already history, so <c>drop</c> names neither. The one exception is
/// <c>--all-pending</c>, for an install whose server is gone and which cannot be pointed at a new
/// one while unsent entries name the old one. Dropping is the only place a
/// queued record is deleted without having reached the server, which is why it says so.
/// </remarks>
internal static class OutboxCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        var verb = command.Positional.Count > 0 ? command.Positional[0] : "list";

        return Task.FromResult(verb switch
        {
            "list" => List(context.Store.Outbox),
            "drop" => Drop(context.Store.Outbox, command),
            _ => Usage($"unknown 'outbox' verb '{verb}'"),
        });
    }

    private static int List(OutboxStore outbox)
    {
        var failed = outbox.Failed();

        if (failed.Count == 0)
        {
            Console.WriteLine("No entry has been refused by the server.");
            return ExitCode.Ok;
        }

        Console.WriteLine("Refused by the server and no longer retried:");

        foreach (var entry in failed)
        {
            Console.WriteLine(
                $"  {entry.Id}  {entry.Kind}  rom {entry.RomId?.ToString(CultureInfo.InvariantCulture) ?? "-"}  "
                + $"{entry.LastError}");
        }

        Console.WriteLine();
        Console.WriteLine("'outbox drop <id> --apply' or 'outbox drop --all-failed --apply' deletes them. A dropped record exists only on this device.");
        return ExitCode.Ok;
    }

    private static int Drop(OutboxStore outbox, CommandLine command)
    {
        long? id = null;

        if (command.Has("all-pending"))
        {
            if (command.Has("all-failed") || command.Positional.Count > 1)
            {
                return Usage("give --all-pending alone, not with --all-failed or an id");
            }

            var pending = outbox.PendingCount();
            if (!command.Has("apply"))
            {
                Console.WriteLine(
                    $"Would drop {pending} unsent {(pending == 1 ? "entry" : "entries")}, which the server has never seen "
                    + "and which would then exist only on this device. Run again with --apply to delete.");
                return pending == 0 ? ExitCode.Refused : ExitCode.Ok;
            }

            Console.WriteLine($"Dropped {outbox.DropPending()} unsent entries.");
            return ExitCode.Ok;
        }

        // A bare flag takes the next word as its value, so `--all-failed 5` would read the id as
        // the flag's value and delete every entry. Refuse the mix rather than guess.
        if (command.Has("all-failed") && (command.Value("all-failed") is not null || command.Positional.Count > 1))
        {
            return Usage("give --all-failed or one entry id, not both");
        }

        if (command.Positional.Count > 1)
        {
            if (!long.TryParse(command.Positional[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return Usage($"'{command.Positional[1]}' is not an entry id");
            }

            id = parsed;
        }
        else if (!command.Has("all-failed"))
        {
            return Usage("'outbox drop' needs an entry id or --all-failed");
        }

        if (!command.Has("apply"))
        {
            var matching = outbox.Failed().Count(entry => id is null || entry.Id == id);

            Console.WriteLine(
                $"Would drop {matching} refused {(matching == 1 ? "entry" : "entries")}, which would then exist only on this device. "
                + "Run again with --apply to delete.");
            return matching == 0 ? ExitCode.Refused : ExitCode.Ok;
        }

        var dropped = outbox.DropFailed(id);

        if (dropped == 0)
        {
            Console.Error.WriteLine("Nothing was dropped: no failed entry matches. Only a refused entry can be dropped.");
            return ExitCode.Refused;
        }

        var noun = dropped == 1 ? "entry" : "entries";
        Console.WriteLine(
            $"Dropped {dropped} refused {noun}. The server never received "
            + (dropped == 1 ? "it, so it exists" : "them, so they exist") + " only on this device.");
        return ExitCode.Ok;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"rommbat-agent outbox: {message}.");
        Console.Error.WriteLine("  outbox [list]                     entries the server refused");
        Console.Error.WriteLine("  outbox drop <id> | --all-failed --apply   delete refused entries");
        Console.Error.WriteLine("  outbox drop --all-pending --apply         delete unsent entries, when the server is gone");
        return ExitCode.Usage;
    }
}
