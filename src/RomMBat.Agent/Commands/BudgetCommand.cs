using RomMBat.Core.Content;
using RomMBat.Core.Store;
using RomMBat.Core;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>budget</c>: how much of this drive RomMBat may use.
/// </summary>
/// <remarks>
/// Two separate bounds, because one cannot do both jobs. The budget is how much RomMBat's own
/// downloads may occupy, and it counts nothing the user put there themselves: an existing
/// library can be larger than any budget, and counting it would leave the app permanently over
/// its cap, unable to fetch and unable to evict its way out, since it must never delete a file
/// it did not download. The free-space floor is what protects the drive as a whole, read live
/// rather than derived, so something else filling the disk still stops a sync.
/// </remarks>
internal static class BudgetCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (command.Has("max"))
        {
            var value = command.Value("max");

            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                context.Store.Settings.Set(SettingStore.ContentMaxBytes, (string?)null, now);
                changed = true;
            }
            else if (ByteSize.Parse(value) is { } bytes && bytes > 0)
            {
                context.Store.Settings.Set(SettingStore.ContentMaxBytes, bytes, now);
                changed = true;
            }
            else
            {
                Console.Error.WriteLine($"'{value}' is not a size. Try 64GB, 500MB, or none.");
                return Task.FromResult(ExitCode.Usage);
            }
        }

        if (command.Has("free-floor"))
        {
            if (ByteSize.Parse(command.Value("free-floor")) is { } floor && floor >= 0)
            {
                context.Store.Settings.Set(SettingStore.FreeSpaceFloorBytes, floor, now);
                changed = true;
            }
            else
            {
                Console.Error.WriteLine($"'{command.Value("free-floor")}' is not a size. Try 2GB.");
                return Task.FromResult(ExitCode.Usage);
            }
        }

        Report(context, changed);
        return Task.FromResult(ExitCode.Ok);
    }

    private static void Report(AgentContext context, bool changed)
    {
        var limits = FilesystemLimits.Inspect(context.Install.RootPath);
        var planner = new ContentPlanner(context.Install, context.Store, limits);
        var used = planner.ManagedBytes();
        var cap = context.Store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = context.Store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;

        if (changed)
        {
            Console.WriteLine("Saved.");
        }

        Console.WriteLine($"Downloaded content:  {ByteSize.Format(used)}");
        Console.WriteLine($"Budget:              {(cap is { } value ? ByteSize.Format(value) : "unlimited")}");
        Console.WriteLine($"Free-space floor:    {ByteSize.Format(floor)}");
        Console.WriteLine($"Drive:               {limits.Format}, {ByteSize.Format(limits.AvailableFreeBytes)} free");

        if (cap is { } budget && used > budget)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{ByteSize.Format(used - budget)} over budget. Run 'evict' to see what would go.");
        }
    }
}
