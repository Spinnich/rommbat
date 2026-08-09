// M0 probe 6b, second half: what the shipping HTTP stack does with an unreachable host.
//
// probe6-reachability.ps1 measures the raw OS socket behaviour. This measures what
// RomM.Client would actually see, and checks whether SocketsHttpHandler.ConnectTimeout
// caps the 21s OS timeout, which exception type each failure mode surfaces, and whether
// an unreachable host is distinguishable from a user cancellation.
//
// Run: dotnet run tools/m0-probes/probe6-httpclient.cs

using System.Diagnostics;
using System.Net.Sockets;

// 192.0.2.0/24 is TEST-NET-1, reserved for documentation and guaranteed unroutable, so the
// default measures the absent-host case without naming anyone's network. Pass your own.
string target = args.Length > 0 ? args[0] : "http://192.0.2.1:8080/api/heartbeat";

Console.WriteLine($"target: {target}");
Console.WriteLine();
Console.WriteLine($"{"case",-34} {"ms",9}  outcome");
Console.WriteLine(new string('-', 78));

await Run("default handler, no timeouts", null, null);
await Run("HttpClient.Timeout = 5s", null, TimeSpan.FromSeconds(5));
await Run("ConnectTimeout = 1s", TimeSpan.FromSeconds(1), null);
await Run("ConnectTimeout = 2s", TimeSpan.FromSeconds(2), null);
await Run("ConnectTimeout = 3s", TimeSpan.FromSeconds(3), null);
await Run("ConnectTimeout 3s + Timeout 10s", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));

// The ambiguity check: a user-cancelled request and a timed-out request both surface
// TaskCanceledException, so the client cannot tell them apart from the type alone.
await RunCancelled("user cancels after 1s");

async Task Run(string label, TimeSpan? connectTimeout, TimeSpan? overallTimeout)
{
    var handler = new SocketsHttpHandler();
    if (connectTimeout is { } ct) handler.ConnectTimeout = ct;

    using var client = new HttpClient(handler);
    if (overallTimeout is { } ot) client.Timeout = ot;

    var sw = Stopwatch.StartNew();
    string outcome;
    try
    {
        using var response = await client.GetAsync(target);
        outcome = $"HTTP {(int)response.StatusCode}";
    }
    catch (Exception ex)
    {
        outcome = Describe(ex);
    }
    sw.Stop();

    Console.WriteLine($"{label,-34} {sw.Elapsed.TotalMilliseconds,9:N1}  {outcome}");
}

async Task RunCancelled(string label)
{
    using var client = new HttpClient(new SocketsHttpHandler());
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

    var sw = Stopwatch.StartNew();
    string outcome;
    try
    {
        using var response = await client.GetAsync(target, cts.Token);
        outcome = $"HTTP {(int)response.StatusCode}";
    }
    catch (Exception ex)
    {
        outcome = Describe(ex) + (cts.IsCancellationRequested ? "  [token signalled]" : "  [token NOT signalled]");
    }
    sw.Stop();

    Console.WriteLine($"{label,-34} {sw.Elapsed.TotalMilliseconds,9:N1}  {outcome}");
}

static string Describe(Exception ex)
{
    var parts = new List<string>();
    for (var e = ex; e is not null; e = e.InnerException)
    {
        parts.Add(e is SocketException se
            ? $"{e.GetType().Name}({se.SocketErrorCode})"
            : e.GetType().Name);
    }
    return string.Join(" -> ", parts);
}
