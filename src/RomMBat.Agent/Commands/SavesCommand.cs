using System.Globalization;
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

        if (command.Positional is ["restore", ..])
        {
            return await RestoreAsync(context, command, cancellationToken).ConfigureAwait(false);
        }

        if (command.Positional is ["bind", ..])
        {
            return Bind(context, command);
        }

        if (command.Positional is ["convert", ..])
        {
            return Convert(context, command);
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
                ReportNearMisses(states);
            }

            Console.WriteLine();
        }

        ReportSaves(context);
        ReportStates(context);
        ReportConflicts(context);
        ReportBindings(context);
        ReportUnsyncable(context);
        ReportPendingConfig(context);
        ReportQueue(context);

        return ExitCode.Ok;
    }

    /// <summary>
    /// Names what a state directory holds that is neither syncable nor a sidecar.
    /// </summary>
    /// <remarks>
    /// Two lines at most per file, and only for files that nearly are states. Dropping these
    /// silently is the failure #34 and #65 describe: a state an emulator really wrote leaves no
    /// trace anywhere a user could find it.
    /// </remarks>
    private static void ReportNearMisses(StateScanOutcome states)
    {
        if (states.NearMisses.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Save states worth a look");

        foreach (var miss in states.NearMisses)
        {
            Console.WriteLine($"  {miss.FileName}");
            Console.WriteLine($"    {miss.Detail}");
        }
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
    /// The states, and the fact that a sync only ever pushes them.
    /// </summary>
    /// <remarks>
    /// Said in the output rather than only in the docs, because <c>POST /api/states</c> has no
    /// slot, no device and no conflict detection, so a user who assumes a state behaves like a
    /// save is assuming something the API cannot do. <c>saves restore</c> is the only way one
    /// comes back down, and it is asked for rather than automatic.
    /// </remarks>
    private static void ReportStates(AgentContext context)
    {
        var states = context.Store.States.List();

        if (states.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{states.Count} save states on disk (a sync only pushes them; 'saves restore' brings one back):");

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

    /// <summary>
    /// Configuration changes waiting for EmulationStation to close, and how the last few went.
    /// </summary>
    /// <remarks>
    /// The finished half matters more than it looks. A queued change is applied by
    /// <c>background quit</c>, with no interface running and nobody watching, so this is the
    /// only account of it a person ever gets. A refusal in particular is invisible otherwise:
    /// from the ES menu it looks exactly like nothing having happened.
    /// </remarks>
    private static void ReportPendingConfig(AgentContext context)
    {
        var outstanding = context.Store.PendingConfig.ListOutstanding();

        // Only the ones that did not work, and filtered here rather than in the loop below. A
        // success is visible in the game's own saves, and listing every one of those would bury
        // the two that need reading; filtering late would print the section's blank line for a
        // page of nothing but successes and then print nothing under it.
        var finished = context.Store.PendingConfig
            .ListFinished(limit: 5)
            .Where(done => done.Result is not PendingConfigResult.Applied)
            .ToList();

        if (outstanding.Count == 0 && finished.Count == 0)
        {
            return;
        }

        Console.WriteLine();

        if (outstanding.Count > 0)
        {
            Console.WriteLine($"{outstanding.Count} configuration "
                + $"change{(outstanding.Count == 1 ? string.Empty : "s")} "
                + "will be made when EmulationStation next closes:");

            foreach (var queued in outstanding)
            {
                var wants = queued.DesiredState == DesiredSettingState.Set
                    ? $"{queued.SettingKey} = {queued.DesiredValue}"
                    : $"{queued.SettingKey} removed";

                Console.WriteLine($"  {queued.System}/{queued.FsName}");
                Console.WriteLine($"    {wants}  ({queued.Reason}, queued {queued.QueuedAtUtc:u})");
            }
        }

        foreach (var done in finished)
        {
            Console.WriteLine();
            Console.WriteLine($"  {done.System}/{done.FsName}: "
                + $"{(done.Result == PendingConfigResult.Refused ? "refused" : "failed")} "
                + $"at {done.AppliedAtUtc:u}");
            Console.WriteLine($"    {done.Detail}");
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
    /// <c>saves convert &lt;rom id&gt; [--apply|--at-quit|--revert]</c>.
    /// </summary>
    /// <remarks>
    /// <b>Previews by default and writes on <c>--apply</c></b>, which is the convention
    /// <c>bios</c> and <c>evict</c> already set, and it matters more here than for either of
    /// them: this is the only command that changes the user's RetroBat configuration, and what
    /// it changes decides where an emulator writes next time.
    /// <para>
    /// <b><c>--at-quit</c> records the change instead of writing it</b>, and
    /// <c>background quit</c> makes it once EmulationStation is confirmed gone. That is the
    /// form the M7 UI uses and the only one available to it, because the UI is launched from
    /// the ES menu and so always runs under a live ES.
    /// </para>
    /// <para>
    /// <b><c>--revert</c> cancels a queued change before it undoes an applied one.</b> A user
    /// who queued something and changed their mind means the thing that has not happened yet,
    /// and reverting a conversion that was never applied would refuse for a reason that reads
    /// like a bug.
    /// </para>
    /// <para>
    /// The verb is a shell over <see cref="SaveConverter"/> and holds no rule of its own, so
    /// the UI drives the same seam without a redesign.
    /// </para>
    /// </remarks>
    private static int Convert(AgentContext context, CommandLine command)
    {
        var revert = command.Has("revert");
        var atQuit = command.Has("at-quit");
        var apply = command.Has("apply") || revert;

        if (command.Positional.Count < 2)
        {
            Console.Error.WriteLine("Usage: rommbat-agent saves convert <rom id> [--apply|--at-quit]");
            Console.Error.WriteLine("       rommbat-agent saves convert <rom id> --revert [--at-quit]");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Opts one game into a per-game memory card, so its saves can be told apart from");
            Console.Error.WriteLine(
                "every other game sharing the console's card. Previews unless --apply is given.");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "--at-quit records the change and makes it when EmulationStation next closes,");
            Console.Error.WriteLine(
                "which is the only way to change this setting without closing it first.");
            return ExitCode.Usage;
        }

        if (!int.TryParse(
                command.Positional[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var romId))
        {
            Console.Error.WriteLine($"'{command.Positional[1]}' is not a rom id.");
            return ExitCode.Usage;
        }

        // Before anything else: a --revert while something is queued means the queued thing.
        if (revert && CancelQueued(context, romId) is { } cancelled)
        {
            Console.WriteLine(cancelled);
            return ExitCode.Ok;
        }

        var converter = new SaveConverter(context.Install, context.Store);

        var result = atQuit
            ? converter.Queue(romId, revert)
            : revert ? converter.Revert(romId)
            : apply ? converter.Convert(romId)
            : converter.Preview(romId);

        if (result.Status == ConversionStatus.Refused)
        {
            Console.Error.WriteLine(result.Detail);
            return ExitCode.Usage;
        }

        if (result.Warning is { } warning)
        {
            Console.WriteLine(warning);
            Console.WriteLine();
        }

        Console.WriteLine(result.Detail);

        if (result.Status == ConversionStatus.Ready)
        {
            Console.WriteLine();
            Console.WriteLine("Nothing was written. Re-run with --apply to make the change, or");
            Console.WriteLine("with --at-quit to have it made when EmulationStation next closes.");
        }

        if (result.Status == ConversionStatus.Queued)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Nothing has been written yet. Quit EmulationStation and the change is made then.");
            Console.WriteLine($"Call it off with: rommbat-agent saves convert {romId} --revert");
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// Calls off a change that was queued and has not happened, or returns null when there
    /// isn't one.
    /// </summary>
    /// <remarks>
    /// Cancelling leaves no row, because nothing was written and there is nothing for the UI
    /// to report later. An applied change is untouched by this and is undone the ordinary way.
    /// </remarks>
    private static string? CancelQueued(AgentContext context, int romId)
    {
        var outstanding = context.Store.PendingConfig.ListOutstandingForRom(romId);

        if (outstanding.Count == 0)
        {
            return null;
        }

        foreach (var queued in outstanding)
        {
            context.Store.PendingConfig.Cancel(queued.System, queued.FsName, queued.SettingKey);
        }

        return outstanding.Count == 1
            ? $"Called off the change queued for '{outstanding[0].FsName}': {outstanding[0].Reason}. "
                + "Nothing had been written, so nothing had to be undone."
            : $"Called off {outstanding.Count} changes queued for rom {romId}. Nothing had been written.";
    }

    /// <summary>
    /// <c>saves resolve &lt;rom&gt; &lt;slot&gt; --keep-local|--keep-server</c>.
    /// </summary>
    /// <remarks>
    /// The side has to be named. There is no default, because either default silently discards
    /// somebody's progress and the whole reason a conflict exists is that RomMBat cannot tell
    /// which side matters.
    /// <para>
    /// <b>A shell over <see cref="ConflictResolutionService"/>, which holds every rule.</b> The
    /// lock, the refusal to treat a failed acquire as done, and the words of every outcome are
    /// all Core's, because the M7 interface drives the same decision and a sentence that differs
    /// between the console and the couch is two answers to one question. What is left here is
    /// the argument parsing and the mapping onto an exit code.
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

        // Authenticating is inside the factory, not before the call, because the service takes
        // the tree lock first: a resolution that cannot run is not worth a round trip.
        var authenticated = ExitCode.Ok;

        var outcome = await new ConflictResolutionService(context.Install, context.Store)
            .ResolveAsync(
                romId,
                slot,
                keepLocal ? ConflictResolution.KeepLocal : ConflictResolution.KeepServer,
                () => context.Authenticate(command, Console.Error, out authenticated),
                cancellationToken)
            .ConfigureAwait(false);

        switch (outcome.State)
        {
            case ConflictOutcomeState.Resolved:
                Console.WriteLine(outcome.Message);
                return ExitCode.Ok;

            case ConflictOutcomeState.Busy:
                Console.Error.WriteLine(outcome.Message);
                return ExitCode.Refused;

            // Authenticate has already written the reason to stderr, and it is more specific
            // than the service's one sentence for both causes. Its exit code is always
            // NotPaired, which #101 is the note on.
            case ConflictOutcomeState.NotPaired:
                return authenticated;

            // Nothing was said on the way in, because the connection opened. Pairing is still
            // the instruction, so the exit code has to be the one that names it: dropping this
            // through to Partial told a script "some of the work landed" about a message whose
            // whole content is "pair again".
            case ConflictOutcomeState.NoDeviceId:
                Console.Error.WriteLine(outcome.Message);
                return ExitCode.NotPaired;

            // The service never returns this and an unreachable host thrown from inside it
            // unwinds to Program, which answers the same code. Here so the mapping is total.
            case ConflictOutcomeState.Offline:
                Console.Error.WriteLine(outcome.Message);
                return ExitCode.Offline;

            default:
                Console.Error.WriteLine(outcome.Message);
                return ExitCode.Partial;
        }
    }

    /// <summary>
    /// <c>saves restore</c>: puts back a save or state the server holds and this device does not.
    /// </summary>
    /// <remarks>
    /// <b>A preview by default, in the shape of <c>bios</c> and <c>evict</c>.</b> Nothing is
    /// written without <c>--apply</c>.
    /// <para>
    /// <b>Nothing calls this from a sync, and that is the design rather than an omission.</b> A
    /// save that comes back because a flush decided it should is indistinguishable from a bug to
    /// whoever deleted it deliberately, so a restore is always asked for.
    /// </para>
    /// </remarks>
    private static async Task<int> RestoreAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        var exitCode = ExitCode.Ok;
        var connection = context.Authenticate(command, Console.Error, out exitCode);

        if (connection is null)
        {
            return exitCode;
        }

        if (context.Store.Device.Read()?.RomMDeviceId is not { } deviceId)
        {
            Console.Error.WriteLine("This install is paired but has no RomM device id. Pair again.");
            return ExitCode.NotPaired;
        }

        // Parsed before either list is fetched, so a usage error costs no request at all. It used
        // to run after both, which made 'saves restore notanumber' pay for a full GET /api/saves
        // and GET /api/states before saying the word was not a number.
        int? romFilter = null;
        string? slotFilter = null;

        if (command.Positional is [_, var romText, ..])
        {
            if (!int.TryParse(romText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var romId))
            {
                Console.Error.WriteLine($"'{romText}' is not a rom id.");
                return ExitCode.Usage;
            }

            romFilter = romId;
            slotFilter = command.Positional.Count > 2 ? command.Positional[2] : null;
        }

        var sync = new SaveSync(context.Install, context.Store, connection, deviceId);
        var found = await sync.FindRestorableAsync(cancellationToken).ConfigureAwait(false);

        if (!found.IsSuccess || found.Value is not { } findings)
        {
            Console.Error.WriteLine(found.Message ?? "The save list could not be read.");
            return ExitCode.Offline;
        }

        var restorable = findings.Restorable;
        var unrestorable = findings.Unrestorable;

        var states = new StateSync(context.Install, context.Store, connection);
        var foundStates = await states.FindRestorableAsync(cancellationToken).ConfigureAwait(false);

        // <b>A state list this device cannot read does not take the save restore down with it.</b>
        // The two are independent reads, and a token whose scopes do not cover /api/states, or a
        // 500 on that route, would otherwise restore zero saves where it used to restore them all.
        // Reported and carried, and an --apply that could not see the state half ends Partial
        // rather than Ok, because it did not do everything it was asked.
        IReadOnlyList<RestorableState> restorableStates = [];
        IReadOnlyList<UnrestorableState> unrestorableStates = [];
        var statesUnread = false;

        if (foundStates.IsSuccess && foundStates.Value is { } stateFindings)
        {
            restorableStates = stateFindings.Restorable;
            unrestorableStates = stateFindings.Unrestorable;
        }
        else
        {
            statesUnread = true;
            Console.Error.WriteLine(
                (foundStates.Message ?? "The state list could not be read.")
                    + " Saves are unaffected and are restored below.");
        }

        // Narrowed by rom id, and by slot when one is given, so a person who wants one save back
        // is not made to take every save back. <b>The slot narrows saves only</b>, and the help
        // says so: a state's slot lives in its file extension and shares no namespace with a
        // save's key, so matching one against the other would filter on a coincidence.
        if (romFilter is { } wanted)
        {
            restorable = [.. restorable.Where(save =>
                save.RomId == wanted
                && (slotFilter is null || string.Equals(save.Slot, slotFilter, StringComparison.Ordinal)))];

            unrestorable = [.. unrestorable.Where(save =>
                save.RomId == wanted
                && (slotFilter is null || string.Equals(save.Slot, slotFilter, StringComparison.Ordinal)))];

            restorableStates = [.. restorableStates.Where(state => state.RomId == wanted)];
            unrestorableStates = [.. unrestorableStates.Where(state => state.RomId == wanted)];
        }

        // Ahead of the "nothing to restore" line, because a save the server holds and this device
        // cannot place is not nothing, and saying nothing about it is what would send somebody
        // looking for a bug in the server.
        foreach (var save in unrestorable)
        {
            Console.Error.WriteLine(
                $"  rom {save.RomId} slot {(save.Slot.Length == 0 ? "(none)" : save.Slot)}: {save.Reason}");
        }

        foreach (var state in unrestorableStates)
        {
            Console.Error.WriteLine($"  rom {state.RomId} state {state.Scope}: {state.Reason}");
        }

        // Only --apply can end Partial. A preview was not asked to change anything, so anything it
        // named as unplaceable is an advisory rather than a failed attempt.
        var applying = command.Has("apply");
        var incomplete = unrestorable.Count > 0 || unrestorableStates.Count > 0 || statesUnread;

        if (restorable.Count == 0 && restorableStates.Count == 0)
        {
            Console.WriteLine(
                unrestorable.Count > 0 || unrestorableStates.Count > 0
                    ? "Nothing to restore: the rows above are the only ones missing here, and none can be placed."
                    : "Nothing to restore: every save and state the server holds for a game on this device is already here.");

            return applying && incomplete ? ExitCode.Partial : ExitCode.Ok;
        }

        static string When(DateTimeOffset? stamp) => stamp is { } value
            ? value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "unknown";

        foreach (var save in restorable)
        {
            Console.WriteLine(
                $"  save   rom {save.RomId}  {(save.Slot.Length == 0 ? "(no slot)" : save.Slot),-20} "
                    + $"{ByteSize.Format(save.SizeBytes),9}  {When(save.ServerUpdatedAt)}  {save.Destination}");
        }

        foreach (var state in restorableStates)
        {
            Console.WriteLine(
                $"  state  rom {state.RomId}  {state.Scope,-20} "
                    + $"{ByteSize.Format(state.SizeBytes),9}  {When(state.ServerUpdatedAt)}  {state.Destination}");
        }

        Console.WriteLine();

        // Said on the preview as well as after, because both are properties of the thing being
        // offered rather than caveats on the result. RomM publishes no hash for a state, so there
        // is nothing to check what arrives against; a save is checked because the server offers
        // something to check it with.
        //
        // The version line is what makes this not a silent restore. save-sync/SKILL.md:147 and
        // PLAN.md:3656 both say never restore across an emulator version change, and neither side
        // can perform that comparison: a state is uploaded scoped to emulator and core with no
        // version, and StateScanner.ReadEmulatorVersion declines on every emulator measured, so
        // the local column is null too. Saying so is the whole of what "never silently" can mean
        // here.
        if (restorableStates.Count > 0)
        {
            Console.WriteLine(
                "States arrive unverified: RomM publishes no hash for one, so nothing can be checked.");
            Console.WriteLine(
                "Nor can the emulator build be: a state carries its emulator and core but no "
                    + "version, so one made on a different build looks identical here and may "
                    + "refuse to load. Keep a copy of anything you care about.");
            Console.WriteLine();
        }

        if (!applying)
        {
            Console.WriteLine(
                $"{restorable.Count} save(s) and {restorableStates.Count} state(s) to restore. Nothing was "
                    + "written. Run 'saves restore --apply' to bring these in.");
            return ExitCode.Ok;
        }

        // Each half is asked only when it has something to do. Both take the tree lock, and a
        // refusal over an empty list would abort the other half for no work at all.
        var outcome = restorable.Count > 0
            ? await sync.RestoreAsync(restorable, cancellationToken).ConfigureAwait(false)
            : new SaveRestoreOutcome();

        // Nothing was attempted, so there is no count to print and it is not a partial run. The
        // state half is not attempted either: it takes the same lock for the same reason, and
        // writing states after the saves were refused is the half-done outcome the refusal exists
        // to prevent.
        if (outcome.Refused)
        {
            foreach (var problem in outcome.Problems)
            {
                Console.Error.WriteLine("  " + problem);
            }

            return ExitCode.Refused;
        }

        var stateOutcome = restorableStates.Count > 0
            ? await states.RestoreAsync(restorableStates, cancellationToken).ConfigureAwait(false)
            : new StateRestoreOutcome();

        foreach (var problem in outcome.Problems.Concat(stateOutcome.Problems))
        {
            Console.Error.WriteLine("  " + problem);
        }

        Console.WriteLine(
            $"restored {outcome.Restored} save(s) and {stateOutcome.Restored} state(s), "
                + $"failed {outcome.Failed + stateOutcome.Failed}, "
                + $"{ByteSize.Format(outcome.BytesTransferred + stateOutcome.BytesTransferred)}");

        // A refused state half is Partial rather than Refused: the saves did land, so this run is
        // not the "nothing was changed" that Refused promises.
        return outcome.Failed + stateOutcome.Failed > 0 || stateOutcome.Refused || incomplete
            ? ExitCode.Partial
            : ExitCode.Ok;
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
        UnsyncableReason.ManagedElsewhere => "RetroBat is also copying these",
        UnsyncableReason.NoStateDeclaration => "no save-state directory is declared here",
        _ => "unknown",
    };
}
