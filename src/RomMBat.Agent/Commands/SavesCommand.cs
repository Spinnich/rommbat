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

        if (command.Positional is ["bind", ..])
        {
            return Bind(context, command);
        }

        if (!command.Has("no-scan"))
        {
            // Both passes get the schema, so a state is never listed as unsyncable by one while
            // the other is uploading it.
            //
            // The state scan runs first and is printed second. The sidecar attribution route
            // reads local_state and SaveScanner is what runs it, so scanning saves first left
            // the route reading an empty table on a first invocation, and a class C unit stayed
            // unattributed until a second one (#64). The order of the two summaries is what a
            // reader expects and is independent of the order the passes run in.
            var schema = StateScanner.LoadSchema(context.Install);

            var states = schema is null
                ? null
                : new StateScanner(context.Install, context.Store, schema).Scan();

            Console.WriteLine(new SaveScanner(context.Install, context.Store, states: schema).Scan().Summary);

            if (states is not null)
            {
                Console.WriteLine(states.Summary);
            }

            Console.WriteLine();
        }

        ReportSaves(context);
        ReportStates(context);
        ReportConflicts(context);
        ReportBindings(context);
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

        var ordered = saves
            .OrderBy(save => save.Path.Value, StringComparer.Ordinal)
            .ThenBy(save => save.UnitKey, StringComparer.Ordinal)
            .ToList();

        // Anything still to send is what a person is looking for, so it is listed first and in
        // full before the cap applies to the rest.
        foreach (var save in ordered.Where(save => save.IsUnsent || save.HasChangedSinceUpload).Take(MaxListedSaves))
        {
            WriteSave(save);
        }

        var pending = ordered.Count(save => save.IsUnsent || save.HasChangedSinceUpload);

        if (pending > MaxListedSaves)
        {
            // A real install reached 1,231 of these in one directory: a MAME nvram tree whose
            // ROMs are not on the device. Listing them all buries everything else in the report.
            foreach (var group in ordered
                .Where(save => save.IsUnsent || save.HasChangedSinceUpload)
                .Skip(MaxListedSaves)
                .GroupBy(save => (save.System, save.Slot))
                .OrderByDescending(group => group.Count()))
            {
                Console.WriteLine(
                    $"  {"and",-9} {ByteSize.Format(group.Sum(save => save.SizeBytes)),8}  "
                        + $"{group.Key.Slot,-24}  {group.Count()} more not listed");
            }
        }

        foreach (var save in ordered.Where(save => !save.IsUnsent && !save.HasChangedSinceUpload).Take(MaxListedSaves))
        {
            WriteSave(save);
        }

        var settled = ordered.Count(save => !save.IsUnsent && !save.HasChangedSinceUpload);

        if (settled > MaxListedSaves)
        {
            Console.WriteLine($"  {"and",-9} {settled - MaxListedSaves} more already in step, not listed.");
        }
    }

    /// <summary>
    /// One line for one save, naming the unit rather than only its container.
    /// </summary>
    /// <remarks>
    /// A class C row's path is a container shared by every game on the system, so the path alone
    /// is not an identity: a real install printed 1,231 rows all reading
    /// <c>saves/mame/nvram</c>. The key is what tells them apart.
    /// </remarks>
    private static void WriteSave(LocalSave save)
    {
        var state = save.IsUnsent
            ? "not sent"
            : save.HasChangedSinceUpload ? "changed" : "in step";

        var where = save.UnitKey.Length > 0 ? $"{save.Path}/{save.UnitKey}" : save.Path.Value;

        Console.WriteLine(
            $"  {state,-9} {ByteSize.Format(save.SizeBytes),8}  {save.Slot,-24}  {where}");
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

    /// <summary>
    /// What is still waiting on the user, and what was already decided.
    /// </summary>
    /// <remarks>
    /// Decided rows are kept rather than deleted, which is migration 007's own decision, and this
    /// is what reads them back: without it a user has no record of which side they picked once
    /// the console output has scrolled away.
    /// </remarks>
    private static void ReportConflicts(AgentContext context)
    {
        var all = context.Store.SaveConflicts.List();
        var open = all.Where(conflict => conflict.IsOpen).OrderBy(conflict => conflict.FirstSeenAtUtc).ToList();

        if (open.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{open.Count} saves changed in both places. Nothing was overwritten:");

            foreach (var conflict in open)
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

        var decided = all.Where(conflict => !conflict.IsOpen).OrderBy(conflict => conflict.ResolvedAtUtc).ToList();

        if (decided.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Conflicts already decided:");

        foreach (var conflict in decided)
        {
            var side = conflict.Resolution == ConflictResolution.KeepLocal
                ? "kept this device's copy"
                : "took the server's copy";

            Console.WriteLine(
                $"  rom {conflict.RomId}, slot {conflict.Slot}: {side} on {conflict.ResolvedAtUtc:u}");
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
    /// <c>saves bind &lt;system&gt; &lt;game id&gt; &lt;rom id&gt;</c>, or <c>--forget</c>.
    /// </summary>
    /// <remarks>
    /// <b>The answer to "a wrong binding is permanent, because the cache makes it so".</b>
    /// Attribution caches what it learns so an odd case costs one lookup rather than one per
    /// scan, and that same cache is what would keep a mistake alive forever. This is how a
    /// person corrects or clears one, and it is the only writer of <c>learned_from = 'user'</c>.
    /// <para>
    /// It is also how a refusal is settled. Two routes naming different games leaves a binding
    /// with no rom, which is deliberate and permanent until somebody who knows which game it is
    /// says so.
    /// </para>
    /// <para>
    /// Local only, and deliberately so: a binding is this device's understanding of its own save
    /// tree, and there is nowhere on the server to put one.
    /// </para>
    /// </remarks>
    private static int Bind(AgentContext context, CommandLine command)
    {
        var forget = command.Has("forget");

        if (command.Positional.Count < 3 || (!forget && command.Positional.Count < 4))
        {
            Console.Error.WriteLine(
                "Usage: rommbat-agent saves bind <system> <game id> <rom id>");
            Console.Error.WriteLine(
                "       rommbat-agent saves bind <system> <game id> --forget");
            return ExitCode.Usage;
        }

        var system = command.Positional[1];
        var gameId = command.Positional[2];

        if (forget)
        {
            if (context.Store.GameIdBindings.Forget(system, gameId))
            {
                Console.WriteLine(
                    $"Forgot the binding for {gameId} under {system}. The next scan works it out "
                        + "again from scratch.");
                return ExitCode.Ok;
            }

            Console.Error.WriteLine($"No binding for {gameId} under {system}.");
            return ExitCode.Usage;
        }

        if (!long.TryParse(
                command.Positional[3],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var romId))
        {
            Console.Error.WriteLine($"'{command.Positional[3]}' is not a rom id.");
            return ExitCode.Usage;
        }

        // The rom has to be one this device holds, because the binding's whole job is to name a
        // local file. Binding to a rom that is not here would record something no scan could act
        // on and no user could see the effect of.
        var rom = context.Store.Files
            .List()
            .FirstOrDefault(file => file.Kind == LocalFileKind.Rom && file.RomId == romId);

        if (rom is null)
        {
            Console.Error.WriteLine(
                $"This device holds no rom with id {romId}, so there is nothing to bind {gameId} to.");
            return ExitCode.Usage;
        }

        context.Store.GameIdBindings.Record(new GameIdBinding(
            system,
            gameId,
            romId,
            rom.Path,
            BindingSource.User,
            $"bound by hand to {rom.FileName}",
            DateTimeOffset.UtcNow));

        Console.WriteLine($"{gameId} under {system} is now {rom.FileName}.");
        Console.WriteLine("The next scan attributes its saves to that game.");

        return ExitCode.Ok;
    }

    /// <summary>
    /// <c>saves resolve &lt;rom&gt; &lt;slot&gt; --keep-local|--keep-server</c>.
    /// </summary>
    /// <remarks>
    /// The side has to be named. There is no default, because either default silently discards
    /// somebody's progress and the whole reason a conflict exists is that RomMBat cannot tell
    /// which side matters.
    /// <para>
    /// <b>Under <see cref="TreeLock"/>, like a flush.</b> Resolving a class C conflict runs the
    /// same <c>SaveUnitTransfer.Restore</c> a flush does, extracting into
    /// <c>partial/unit-&lt;guid&gt;/</c> and swapping members into a shared container one at a
    /// time. Two of those at once, or one racing <c>PartialSweep</c>, leaves the container half
    /// swapped. Unlike a flush, failing to acquire is refused rather than treated as done: a
    /// person asked for this and silently doing nothing would read as having resolved it.
    /// </para>
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

        // Before authenticating, because a resolution that cannot run is not worth a round trip
        // to the server first.
        using var held = TreeLock.TryAcquire(context.Install);

        if (held is null)
        {
            Console.Error.WriteLine(
                "A flush is running, and resolving a conflict writes the same save files it does. "
                    + "Nothing was changed. Try again once it has finished.");
            return ExitCode.Refused;
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

    /// <summary>
    /// How many unsettled bindings are worth printing before the list stops being useful.
    /// </summary>
    private const int MaxListedBindings = 20;

    /// <summary>How many saves are worth listing before the report stops being readable.</summary>
    private const int MaxListedSaves = 25;

    /// <summary>
    /// Shows what directory-save attribution currently rests on, including where it gave up.
    /// </summary>
    /// <remarks>
    /// Worth showing rather than hiding, because a binding is a claim about which game owns a
    /// save and the user is the only one who can tell when it is wrong. An unresolved row is
    /// listed too: it is a decision that nothing could name the game, and it stays until
    /// <c>saves bind</c> settles it.
    /// </remarks>
    private static void ReportBindings(AgentContext context)
    {
        var bindings = context.Store.GameIdBindings.List();

        if (bindings.Count == 0)
        {
            return;
        }

        Console.WriteLine("Game ID bindings");

        foreach (var binding in bindings.Where(entry => entry.IsResolved))
        {
            Console.WriteLine(
                $"  {binding.System}/{binding.GameId} -> {binding.RomPath?.Name ?? "?"} "
                    + $"(learned from {Describe(binding.LearnedFrom)})");
        }

        // Contested ones are listed rather than summarised, because each needs a person to
        // settle it and the command that does is per key. Capped all the same: a report nobody
        // can scroll through is a report nobody reads.
        var contested = bindings.Where(entry => !entry.IsResolved).ToList();

        foreach (var binding in contested.Take(MaxListedBindings))
        {
            Console.WriteLine($"  {binding.System}/{binding.GameId} -> not bound");
            Console.WriteLine($"    {binding.Detail}");
            Console.WriteLine(
                $"    settle it with: rommbat-agent saves bind {binding.System} {binding.GameId} <rom id>");
        }

        if (contested.Count > MaxListedBindings)
        {
            Console.WriteLine(
                $"  and {contested.Count - MaxListedBindings} more unsettled bindings, not listed.");
        }

        Console.WriteLine();
    }

    private static string Describe(BindingSource source) => source switch
    {
        BindingSource.Journal => "a launch covering when the save was written",
        BindingSource.RomHeader => "the game code in the ROM's header",
        BindingSource.Sidecar => "the name sidecar beside a save state",
        BindingSource.User => "you, with saves bind",
        BindingSource.Contested => "nothing: two routes named different games",
        _ => source.ToString(),
    };

    private static string Describe(UnsyncableReason reason) => reason switch
    {
        UnsyncableReason.NotInThisVersion => "not in this release",
        UnsyncableReason.UnknownShape => "shape not recognised",
        UnsyncableReason.SharedContainer => "shared by several games",
        UnsyncableReason.Unattributed => "no matching ROM",
        _ => "unknown",
    };
}
