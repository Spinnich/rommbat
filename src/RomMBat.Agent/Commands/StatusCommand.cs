using System.Globalization;
using RomM.Client;
using RomM.Client.Saves;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Server;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>status</c>: what RomMBat knows locally, and optionally whether the server is up.
/// </summary>
/// <remarks>
/// Local first and network second, on purpose. Everything below the reachability line is
/// answerable with the server switched off, which is the state this app is designed for.
/// Output is one <c>key: value</c> per line so it greps cleanly.
/// </remarks>
internal static class StatusCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var install = context.Install;
        var store = context.Store;
        var device = store.Device.Read();
        var clock = store.Clock.Read();

        Console.WriteLine("RetroBat");
        Console.WriteLine($"  root:            {install.RootPath}");
        Console.WriteLine($"  found by:        {Describe(install.Source)}");
        Console.WriteLine($"  version:         {install.ReadVersionString() ?? "not readable"}");
        Console.WriteLine($"  compatibility:   {install.CheckVersion().Verdict}");
        Console.WriteLine();

        Console.WriteLine("Local store");
        Console.WriteLine($"  database:        {store.DatabasePath}");
        Console.WriteLine($"  schema version:  {store.SchemaVersion} of {LocalStore.ExpectedSchemaVersion}");
        Console.WriteLine($"  journal mode:    {store.JournalMode}");
        Console.WriteLine($"  local sequence:  {store.CurrentSequence()}");
        Console.WriteLine($"  outbox pending:  {store.Outbox.PendingCount()}");

        // Off unless asked for, because it is one File.Exists per row where every other line
        // here is answered from the database alone. Worth having at all because the budget is
        // arithmetic over this table: a row whose file is gone inflates it forever, and nothing
        // else in RomMBat can see the state. Measured on a live install at 5,512 of 5,932 rows
        // and 18.22 GiB. See #113.
        if (command.Has("check-files") || command.Has("repair-files"))
        {
            var sweep = new InventorySweep(install, store);
            var inventory = sweep.Plan();

            Console.WriteLine($"  local files:     {inventory.Summary}");

            foreach (var (folder, count, bytes) in inventory.Folders.Take(5))
            {
                Console.WriteLine($"    {folder,-14} {count,6:N0} missing, {ByteSize.Format(bytes)}");
            }

            // Saves are checked and never repaired (#142). The row may be the last local record
            // of a save only the server still holds, and bringing that back is asked for.
            Console.WriteLine($"  local saves:     {inventory.SavesSummary}");

            foreach (var save in inventory.MissingSaves.Take(5))
            {
                Console.WriteLine(
                    $"    {(save.UnitKey.Length > 0 ? $"{save.Path}/{save.UnitKey}" : save.Path.Value)}");
            }

            if (inventory.MissingSaves.Count > 5)
            {
                Console.WriteLine($"    and {inventory.MissingSaves.Count - 5:N0} more");
            }

            if (inventory.MissingSaves.Count > 0)
            {
                Console.WriteLine(
                    "  saves are left alone by --repair-files. Run 'saves restore' to see what the server still has.");
            }

            if (command.Has("repair-files") && !inventory.NothingFound)
            {
                // Safe by the rollback's own argument: a row must never outlive its bytes, and
                // the row is the claim that is wrong. The next sync re-downloads, which is what
                // would have happened anyway.
                Console.WriteLine($"  repaired:        {sweep.Apply(inventory).Summary}");
            }
            else if (inventory.NothingFound)
            {
                Console.WriteLine("  nothing will be forgotten: not one recorded file is on this drive, so this");
                Console.WriteLine("  does not look like the tree they were written to.");
            }
            else if (!inventory.IsClean)
            {
                Console.WriteLine("  the disk budget is counting those bytes. Run 'status --repair-files' to drop the rows.");
            }
        }

        Console.WriteLine();

        Console.WriteLine("Identity");
        if (device is null)
        {
            Console.WriteLine("  device id:       none yet, run 'pair'");
        }
        else
        {
            Console.WriteLine($"  device id:       {device.ClientDeviceIdentifier}");
            Console.WriteLine($"  paired:          {(device.IsPaired ? "yes" : "no")}");
            Console.WriteLine($"  server:          {device.ServerOrigin?.ToString() ?? "not configured"}");
            Console.WriteLine($"  romm device:     {device.RomMDeviceId ?? "none"}");
            Console.WriteLine($"  token at rest:   {DescribeToken(device)}");
            Console.WriteLine($"  scopes:          {(device.Scopes.All.Count == 0
                ? "none"
                : string.Join(", ", device.Scopes.All))}");

            // Only worth listing once something was granted. On an unpaired install every
            // feature is off, and "paired: no" already said that.
            if (device.Scopes.All.Count > 0)
            {
                foreach (var (requirement, missing) in device.Scopes.Degradations)
                {
                    Console.WriteLine($"  feature off:     {requirement.Name} (missing {string.Join(", ", missing)})");
                }
            }
        }

        Console.WriteLine();

        Console.WriteLine("Clock");
        Console.WriteLine($"  last contact:    {Describe(clock.LastContactUtc)}");
        Console.WriteLine($"  server date:     {Describe(clock.LastServerDateUtc)}");
        Console.WriteLine($"  measured skew:   {(clock.Skew is { } skew ? $"{skew.TotalSeconds:0.0}s" : "unknown")}");
        Console.WriteLine($"  skew suspicious: {(clock.IsSkewSuspicious ? "yes" : "no")}");
        Console.WriteLine(
            $"  mtime tolerance: {ClockSkew.FilesystemTimestampTolerance.TotalSeconds:0}s (FAT and exFAT round up)");
        Console.WriteLine();

        if (command.Has("offline") || device?.ServerOrigin is null)
        {
            Console.WriteLine("Server");
            Console.WriteLine("  not probed:      " + (command.Has("offline")
                ? "--offline was passed"
                : "no server configured yet"));
            return device?.IsPaired == true ? ExitCode.Ok : ExitCode.NotPaired;
        }

        using var connection = AgentContext.Connect(device.ServerOrigin);
        var attempt = await ServerProbes.ContactAsync(connection, store, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine("Server");
        if (attempt.Contact is not { } contact)
        {
            Console.WriteLine($"  reachable:       no ({device.ServerOrigin})");
            if (attempt.Answered)
            {
                Console.WriteLine($"  answer:          {attempt.Failure}");
            }

            Console.WriteLine("  effect:          none. Everything works offline and reconciles on reconnect.");
            return ExitCode.Offline;
        }

        Console.WriteLine($"  reachable:       yes ({device.ServerOrigin})");
        Console.WriteLine($"  version:         {contact.Probe.ReportedVersion ?? "not reported"}");
        Console.WriteLine($"  compatibility:   {contact.Probe.Compatibility.Verdict}");
        Console.WriteLine($"  round trip:      {contact.Probe.RoundTrip.TotalMilliseconds:0} ms");
        Console.WriteLine();

        await WritePlaytimeAsync(context, command, device, cancellationToken).ConfigureAwait(false);

        return device.IsPaired ? ExitCode.Ok : ExitCode.NotPaired;
    }

    /// <summary>
    /// The play sessions the server holds for this device.
    /// </summary>
    /// <remarks>
    /// <b>The only place anything in RomMBat can see the server half of playtime.</b> An
    /// accepted post is dropped from the outbox and never read back, so <c>flush</c>'s
    /// <c>playtime:</c> line says what this device sent and not what RomM holds, and it reads
    /// the same whether every session landed or none was ever written. It is also what makes
    /// step 8 of the platform-certification checklist answerable from the agent (#208).
    /// <para>
    /// <b>Never a failure of <c>status</c>.</b> Nothing above this line needs the token, and a
    /// locked or narrowed one is an ordinary state rather than a fault, so every refusal here is
    /// a line and the exit code stays what the rest of the command decided.
    /// </para>
    /// <para>
    /// <b>A mismatch against the journal is shown and not judged.</b> A session the server
    /// pruned is not a defect, so this reports what came back and leaves the reading to whoever
    /// is looking.
    /// </para>
    /// </remarks>
    private static async Task WritePlaytimeAsync(
        AgentContext context,
        CommandLine command,
        DeviceRecord device,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Playtime");

        // The RomM-side id, never ClientDeviceIdentifier. They are two different values,
        // printed on adjacent lines above, and filtering by the local one answers 200 with
        // zero rows, which is indistinguishable from a session that was never written.
        if (device.RomMDeviceId is not { } rommDevice)
        {
            Console.WriteLine("  not readable:    this install has no RomM device id yet. Run 'pair'.");
            return;
        }

        if (!device.Scopes.Has(RomMScopes.RomsUserRead))
        {
            Console.WriteLine($"  not readable:    this pairing was not granted {RomMScopes.RomsUserRead}.");
            return;
        }

        var attempt = context.Session.Authenticate(command.Value("passphrase"));

        if (attempt.Connection is null)
        {
            Console.WriteLine($"  not readable:    {attempt.Problem}");
            return;
        }

        using var authenticated = attempt.Connection;

        RomMResponse<IReadOnlyList<PlaySessionRow>> answer;

        try
        {
            answer = await authenticated
                .ListPlaySessionsAsync(deviceId: rommDevice, limit: SessionWindow, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RomMUnreachableException ex)
        {
            Console.WriteLine($"  not readable:    {ex.Message}");
            return;
        }
        catch (RomMApiException)
        {
            // A 200 whose body is not the list: a proxy's login page, or a session row a newer
            // RomM serialises in a shape this client cannot read. It throws rather than
            // answering a failed response, and the promise above is that nothing here is a
            // failure of status.
            Console.WriteLine("  not readable:    the server's answer could not be read.");
            return;
        }

        if (!answer.IsSuccess || answer.Value is not { } sessions)
        {
            Console.WriteLine($"  not readable:    {answer.Message ?? "the server refused the read"}");
            return;
        }

        if (sessions.Count == 0)
        {
            Console.WriteLine($"  server holds:    nothing for romm device {rommDevice}");
            Console.WriteLine("  and note:        these rows belong to the paired account and no scope widens that,");
            Console.WriteLine("                   so an empty answer is not evidence that nothing was sent.");
            return;
        }

        var counted = sessions.Count == SessionWindow
            ? $"{SessionWindow} or more sessions"
            : $"{sessions.Count} session{(sessions.Count == 1 ? string.Empty : "s")}";

        // Ordered here because the endpoint promises no order, so the first row is not the last
        // session.
        var last = sessions.MaxBy(session => session.EndTime)!;

        Console.WriteLine($"  server holds:    {counted} for romm device {rommDevice}");
        Console.WriteLine(
            $"  last session:    {Describe(last.StartTime)} to {Describe(last.EndTime)}, "
                + $"{Describe(TimeSpan.FromMilliseconds(last.DurationMs))}");
        Console.WriteLine($"  its rom:         {last.RomId?.ToString(CultureInfo.InvariantCulture) ?? "none recorded"}");
    }

    /// <summary>How many sessions to ask for, which is the server's own default.</summary>
    private const int SessionWindow = 50;

    private static string DescribeToken(DeviceRecord device)
    {
        if (device.Token is null)
        {
            return "none stored";
        }

        var protection = device.Token.Mode == Core.Identity.TokenProtectionMode.Passphrase
            ? "encrypted with a passphrase"
            : "plaintext in the tree (DPAPI cannot be used on a portable install)";

        var expiry = device.Token.ExpiresAt is { } at
            ? $", expires {at.ToUniversalTime():u}{(device.IsTokenExpired(DateTimeOffset.UtcNow) ? " (expired)" : string.Empty)}"
            : ", never expires";

        return protection + expiry;
    }

    private static string Describe(DateTimeOffset? value) =>
        value is { } moment ? moment.ToUniversalTime().ToString("u") : "never";

    /// <summary>A session length, in the units a person reads a play session in.</summary>
    private static string Describe(TimeSpan length) => length.TotalHours >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{(int)length.TotalHours}h {length.Minutes}m {length.Seconds}s")
        : length.TotalMinutes >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{length.Minutes}m {length.Seconds}s")
            : string.Create(CultureInfo.InvariantCulture, $"{length.Seconds}s");

    private static string Describe(Core.Paths.RootDiscoverySource source) => source switch
    {
        Core.Paths.RootDiscoverySource.Explicit => "--root",
        Core.Paths.RootDiscoverySource.Environment => RetroBatRootVariable,
        Core.Paths.RootDiscoverySource.ExecutableDirectory => "walking up from the executable",
        Core.Paths.RootDiscoverySource.WorkingDirectory => "walking up from the working directory",
        Core.Paths.RootDiscoverySource.Registry => "the registry (last resort, and possibly stale)",
        _ => source.ToString(),
    };

    private static string RetroBatRootVariable => Core.RetroBatRoot.OverrideVariable;
}
