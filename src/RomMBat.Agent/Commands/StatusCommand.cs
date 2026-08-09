using RomM.Client;
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
        var contact = await ServerProbes.TryContactAsync(connection, store, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine("Server");
        if (contact is null)
        {
            Console.WriteLine($"  reachable:       no ({device.ServerOrigin})");
            Console.WriteLine("  effect:          none. Everything works offline and reconciles on reconnect.");
            return ExitCode.Offline;
        }

        Console.WriteLine($"  reachable:       yes ({device.ServerOrigin})");
        Console.WriteLine($"  version:         {contact.Probe.ReportedVersion ?? "not reported"}");
        Console.WriteLine($"  compatibility:   {contact.Probe.Compatibility.Verdict}");
        Console.WriteLine($"  round trip:      {contact.Probe.RoundTrip.TotalMilliseconds:0} ms");

        return device.IsPaired ? ExitCode.Ok : ExitCode.NotPaired;
    }

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
