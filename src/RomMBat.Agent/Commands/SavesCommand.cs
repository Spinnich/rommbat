using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>saves</c>: what is on disk, what has gone up, what cannot go up and why, and what is
/// waiting on a decision.
/// </summary>
/// <remarks>
/// <b>The unsyncable half is the point, and it works offline.</b> This release syncs class A and
/// B battery saves and save states; directory saves and shared containers land in the next one.
/// A user whose PS3 saves are not going up is entitled to be told that rather than to find out,
/// and the alternative to this report is silence.
/// <para>
/// <b><c>saves resolve</c> is the only thing in RomMBat that discards a copy of a save</b>, and
/// it does it because a person said so. Everything else keeps both sides.
/// </para>
/// </remarks>
internal static class SavesCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        if (command.Positional is ["resolve", ..])
        {
            return await ResolveAsync(context, command, cancellationToken).ConfigureAwait(false);
        }

        if (!command.Has("no-scan"))
        {
            // Both passes get the schema, so a state is never listed as unsyncable by one while
            // the other is uploading it.
            var schema = StateScanner.LoadSchema(context.Install);

            Console.WriteLine(new SaveScanner(context.Install, context.Store, states: schema).Scan().Summary);

            if (schema is not null)
            {
                Console.WriteLine(new StateScanner(context.Install, context.Store, schema).Scan().Summary);
            }

            Console.WriteLine();
        }

        ReportSaves(context);
        ReportStates(context);
        ReportConflicts(context);
        ReportUnsyncable(context);
        ReportQueue(context);

        return ExitCode.Ok;
    }

    private static void ReportSaves(AgentContext context)
    {
        var saves = context.Store.Saves.List();

        if (saves.Count == 0)
        {
            Console.WriteLine("No saves found under saves/.");
            return;
        }

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

    /// <summary>
    /// The states, and the fact that they are pushed rather than synced.
    /// </summary>
    /// <remarks>
    /// Said in the output rather than only in the docs, because <c>POST /api/states</c> has no
    /// slot, no device and no conflict detection, so a user who assumes a state behaves like a
    /// save is assuming something the API cannot do.
    /// </remarks>
    private static void ReportStates(AgentContext context)
    {
        var states = context.Store.States.List();

        if (states.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{states.Count} save states on disk (pushed one way, never downloaded):");

        foreach (var state in states.OrderBy(state => state.Path.Value, StringComparer.Ordinal))
        {
            var status = state.RomId is null
                ? "no rom"
                : state.IsUnsent ? "not sent" : state.HasChangedSinceUpload ? "changed" : "in step";

            var version = string.IsNullOrEmpty(state.EmulatorVersion) ? string.Empty : $" v{state.EmulatorVersion}";

            Console.WriteLine(
                $"  {status,-9} {ByteSize.Format(state.SizeBytes),8}  {state.Slot,-28}{version}");
            Console.WriteLine($"  {string.Empty,-9} {string.Empty,8}  {state.Path}");
        }
    }

    private static void ReportConflicts(AgentContext context)
    {
        var conflicts = context.Store.SaveConflicts.ListOpen();

        if (conflicts.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{conflicts.Count} saves changed in both places. Nothing was overwritten:");

        foreach (var conflict in conflicts)
        {
            Console.WriteLine($"  rom {conflict.RomId}, slot {conflict.Slot}, since {conflict.FirstSeenAtUtc:u}");
            Console.WriteLine($"    here    {conflict.LocalPath}  {Short(conflict.LocalHash)}");
            Console.WriteLine($"    server  {Short(conflict.ServerHash)}  {conflict.ServerUpdatedAt:u}");

            if (conflict.LocalCopyPath is { } copy)
            {
                Console.WriteLine($"    a copy of the local file is at {copy}");
            }

            Console.WriteLine(
                $"    resolve with: rommbat-agent saves resolve {conflict.RomId} \"{conflict.Slot}\" "
                    + "--keep-local | --keep-server");
        }
    }

    private static void ReportUnsyncable(AgentContext context)
    {
        var unsyncable = context.Store.Unsyncable.List();

        if (unsyncable.Count == 0)
        {
            return;
        }

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

    private static void ReportQueue(AgentContext context)
    {
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
    }

    /// <summary>
    /// <c>saves resolve &lt;rom&gt; &lt;slot&gt; --keep-local|--keep-server</c>.
    /// </summary>
    /// <remarks>
    /// The side has to be named. There is no default, because either default silently discards
    /// somebody's progress and the whole reason a conflict exists is that RomMBat cannot tell
    /// which side matters.
    /// </remarks>
    private static async Task<int> ResolveAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        if (command.Positional.Count < 3
            || !long.TryParse(
                command.Positional[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var romId))
        {
            Console.Error.WriteLine(
                "Usage: rommbat-agent saves resolve <rom id> <slot> --keep-local | --keep-server");
            return ExitCode.Usage;
        }

        var slot = command.Positional[2];
        var keepLocal = command.Has("keep-local");
        var keepServer = command.Has("keep-server");

        if (keepLocal == keepServer)
        {
            Console.Error.WriteLine(
                "Name one side: --keep-local or --keep-server. There is no default, because either "
                    + "one discards somebody's progress.");
            return ExitCode.Usage;
        }

        var connection = context.Authenticate(command, Console.Error, out var exitCode);

        if (connection is null)
        {
            return exitCode;
        }

        using (connection)
        {
            if (context.Store.Device.Read()?.RomMDeviceId is not { } deviceId)
            {
                Console.Error.WriteLine("This install is paired but has no RomM device id. Pair again.");
                return ExitCode.NotPaired;
            }

            var outcome = await new SaveConflictResolver(context.Install, context.Store, connection, deviceId)
                .ResolveAsync(
                    romId,
                    slot,
                    keepLocal ? ConflictResolution.KeepLocal : ConflictResolution.KeepServer,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!outcome.Resolved)
            {
                Console.Error.WriteLine(outcome.Message);
                return ExitCode.Partial;
            }

            Console.WriteLine(outcome.Message);
            return ExitCode.Ok;
        }
    }

    private static string Short(string? hash) =>
        hash is null ? "(no hash)" : hash[..Math.Min(8, hash.Length)];

    private static string Describe(UnsyncableReason reason) => reason switch
    {
        UnsyncableReason.NotInThisVersion => "not in this release",
        UnsyncableReason.UnknownShape => "shape not recognised",
        UnsyncableReason.SharedContainer => "shared by several games",
        UnsyncableReason.Unattributed => "no matching ROM",
        _ => "unknown",
    };
}
