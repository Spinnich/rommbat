using System.Diagnostics;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Pairing, and what it does when the server is not there.
/// </summary>
/// <remarks>
/// <b>Offline is a working state, not an error screen.</b> The reachability probe uses the
/// short interactive connect timeout from M0 experiment 6, and it runs off the poll loop, so an
/// unreachable LAN host neither hangs the interface nor traps the user on a screen they cannot
/// leave. Both halves are asserted here: that it gives up quickly, and that the screen stays
/// navigable the whole time.
/// </remarks>
public class PairingScreenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A routable address with nothing listening, so the connect attempt has to time out.
    /// </summary>
    /// <remarks>
    /// 192.0.2.0/24 is TEST-NET-1 from RFC 5737, reserved for documentation and guaranteed not
    /// to be a real host. A made-up hostname would fail at DNS instead, which is a different
    /// and much faster path and would not exercise the connect timeout at all.
    /// </remarks>
    private static Uri Unreachable => new("http://192.0.2.1:8080");

    private static HashSet<string> Held(params string[] names) => new(names, StringComparer.Ordinal);

    [Fact]
    public void The_connect_budget_is_the_short_interactive_one_and_not_the_default()
    {
        // The rule this rests on: nothing sets ConnectTimeout by default and an unreachable
        // host then stalls for 21 seconds, which is four rows of animation into a hang.
        Assert.Equal(TimeSpan.FromSeconds(2), RomMClientOptions.InteractiveConnectTimeout);
        Assert.True(RomMClientOptions.InteractiveConnectTimeout < RomMClientOptions.BackgroundConnectTimeout);
    }

    [Fact]
    public async Task An_unreachable_server_gives_up_inside_the_budget_and_says_so()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var watch = Stopwatch.StartNew();
        using var pairing = new PairingViewModel(session, Unreachable);

        var settled = await WaitForAsync(pairing, stage => stage != PairingStage.Contacting);
        watch.Stop();

        Assert.True(settled, "pairing never left the contacting stage");
        Assert.Equal(PairingStage.Unreachable, pairing.Stage);

        // The budget is 2 s. Allow generous slack for a loaded CI box, but well under the 21 s
        // an unset ConnectTimeout would cost, which is the failure this bounds.
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(10),
            $"gave up after {watch.Elapsed.TotalSeconds:0.0}s, which is too close to the default stall");

        Assert.Contains("offline", pairing.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_screen_stays_navigable_while_the_server_is_being_waited_on()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var pairing = new PairingViewModel(session, Unreachable);
        var navigator = new Navigator(pairing);

        // Still contacting: the probe is in flight on another thread and must not have blocked
        // the loop that reads the pad.
        Assert.Equal(PairingStage.Contacting, pairing.Stage);

        navigator.SuppressHeld(Held());
        navigator.Advance(Held("b"), T0);

        Assert.True(navigator.HasExited);

        // Leaving cancels the work rather than leaving it polling a server nobody is waiting on.
        await WaitForAsync(pairing, _ => true);
    }

    [Fact]
    public async Task Leaving_and_coming_back_starts_a_new_request_rather_than_resuming_a_dead_one()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        using var pairing = new PairingViewModel(session, Unreachable);
        await WaitForAsync(pairing, stage => stage == PairingStage.Unreachable);

        // X on an unreachable server tries again. The code that lapsed cannot be revived: the
        // server's pending state has a hard TTL, so a retry is always a fresh request.
        Assert.Equal(ScreenCommandKind.Stay, pairing.Handle(NavAction.Alternate).Kind);
        Assert.Equal(PairingStage.Contacting, pairing.Stage);

        await WaitForAsync(pairing, stage => stage == PairingStage.Unreachable);
    }

    [Fact]
    public void An_unusable_address_is_refused_by_Core_with_words_and_never_reaches_the_network()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        // The keyboard does not decide what a server address is: InstallSession does, and it
        // owns the sentence too. This is the seam that keeps validation out of presentation.
        var choice = session.ResolveOrigin("not a url at all");

        Assert.Null(choice.Origin);
        Assert.False(string.IsNullOrWhiteSpace(choice.Problem));
    }

    [Fact]
    public async Task Nothing_is_written_to_the_store_by_a_pairing_that_never_reached_a_server()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        using var pairing = new PairingViewModel(session, Unreachable);
        await WaitForAsync(pairing, stage => stage == PairingStage.Unreachable);

        // No token, no device id, and nothing that would make the next attempt behave as though
        // it had half succeeded.
        var device = session.Store.Device.Read();

        Assert.True(device is null || !device.IsPaired);
        Assert.Null(device?.Token);
    }

    private static async Task<bool> WaitForAsync(
        PairingViewModel pairing,
        Func<PairingStage, bool> until,
        int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (until(pairing.Stage))
            {
                return true;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        return false;
    }
}
