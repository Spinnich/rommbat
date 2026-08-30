// P4: how long an unreachable server costs through the path the UI actually uses.
using System.Diagnostics;
using RomMBat.Core;
using RomMBat.Core.Server;

var root = args.Length > 0 ? args[0] : ".";
var target = new Uri(args.Length > 1 ? args[1] : "http://192.0.2.1:8080");

using var session = InstallSession.Open(root).Session!;

for (var run = 1; run <= 3; run++)
{
    using var connection = InstallSession.Connect(target);
    var watch = Stopwatch.StartNew();
    var contact = await ServerProbes.TryContactAsync(connection, session.Store);
    watch.Stop();
    Console.WriteLine($"run {run}: {watch.Elapsed.TotalMilliseconds,7:0} ms  contact={(contact is null ? "none" : "reached")}");
}
