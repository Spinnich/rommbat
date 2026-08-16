using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>saves</c>: what is on disk, what has gone up, and what cannot go up and why.
/// </summary>
/// <remarks>
/// <b>The unsyncable half is the point, and it works offline.</b> This release syncs class A
/// and B battery saves; directory saves, shared containers and every save state land in the
/// next one. A user whose PS3 saves are not going up is entitled to be told that rather than to
/// find out, and the alternative to this report is silence.
/// </remarks>
internal static class SavesCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        if (!command.Has("no-scan"))
        {
            var scanned = new SaveScanner(context.Install, context.Store).Scan();
            Console.WriteLine(scanned.Summary);
            Console.WriteLine();
        }

        var saves = context.Store.Saves.List();

        if (saves.Count == 0)
        {
            Console.WriteLine("No saves found under saves/.");
        }
        else
        {
            Console.WriteLine($"{saves.Count} saves on disk:");

            foreach (var save in saves.OrderBy(save => save.Path.Value, StringComparer.Ordinal))
            {
                var state = save.IsUnsent
                    ? "not sent"
                    : save.HasChangedSinceUpload ? "changed" : "in step";

                Console.WriteLine(
                    $"  {state,-9} {ByteSize.Format(save.SizeBytes),8}  {save.Slot,-24}  {save.Path}");
            }
        }

        var unsyncable = context.Store.Unsyncable.List();

        if (unsyncable.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Not syncable, and why:");

            foreach (var entry in unsyncable)
            {
                var scope = string.IsNullOrEmpty(entry.Emulator) ? entry.System : $"{entry.System}/{entry.Emulator}";
                var files = entry.FileCount == 1 ? "file " : "files";
                Console.WriteLine($"  {scope,-24} {entry.FileCount,6} {files}  {Describe(entry.Reason)}");
                Console.WriteLine($"  {string.Empty,-24}               {entry.Detail}");
            }
        }

        var pending = context.Store.Outbox.PendingCount();
        var openEvents = context.Store.Journal.OpenCount();

        Console.WriteLine();
        Console.WriteLine($"{pending} items queued, {openEvents} hook events not yet reconciled.");

        // The heartbeat. Both scripted hook forms fail silently on some hosts, so play data
        // with no hook activity behind it is a state worth naming rather than a silent loss.
        if (context.Store.Journal.LastStart() is { } lastStart)
        {
            Console.WriteLine($"EmulationStation last started at {lastStart:u}.");
        }
        else
        {
            Console.WriteLine(
                "No hook has ever fired. If games have been played, check that the hooks are installed "
                    + "('rommbat-agent hooks status').");
        }

        return Task.FromResult(ExitCode.Ok);
    }

    private static string Describe(UnsyncableReason reason) => reason switch
    {
        UnsyncableReason.NotInThisVersion => "not in this release",
        UnsyncableReason.UnknownShape => "shape not recognised",
        UnsyncableReason.SharedContainer => "shared by several games",
        UnsyncableReason.Unattributed => "no matching ROM",
        _ => "unknown",
    };
}
