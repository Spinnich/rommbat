using RomMBat.Core.RetroBat;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Waiting for EmulationStation to actually be gone before writing a file it owns.
/// </summary>
/// <remarks>
/// <b>The quit hook fires while ES is still alive.</b> Timed across three sessions this stage:
/// ES writes <c>es_settings.cfg</c> 175 to 325 ms after the quit was asked for, fires the hook
/// 200 to 630 ms after that write, and the process is gone 48 to 68 ms later. So a pass that
/// wrote the file the moment it started would be writing inside the window finding 178
/// measured, where ES discards what it finds and says nothing.
/// <para>
/// Driven against a stand-in verdict rather than a real ES, because what is being tested is
/// the waiting and the giving up, and neither needs a front end to be running.
/// </para>
/// </remarks>
public sealed class EmulationStationWaitTests
{
    [Fact]
    public void An_EmulationStation_that_is_already_gone_costs_one_look()
    {
        var looks = 0;

        var wait = EmulationStationProcess.WaitForExit(
            () => { looks++; return EsRunningVerdict.NotRunning; },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(50));

        Assert.True(wait.Gone);
        Assert.Equal(1, looks);
        Assert.Null(wait.Detail);
    }

    [Fact]
    public void One_that_is_on_its_way_out_is_waited_for_rather_than_given_up_on()
    {
        // The measured case: the hook fires and the process goes tens of milliseconds later.
        var looks = 0;

        var wait = EmulationStationProcess.WaitForExit(
            () => ++looks < 3 ? EsRunningVerdict.Running("still shutting down") : EsRunningVerdict.NotRunning,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(10));

        Assert.True(wait.Gone);
        Assert.Equal(3, looks);
    }

    [Fact]
    public void One_that_never_exits_gives_up_and_says_why_rather_than_hanging()
    {
        // A hook-spawned process that waits forever is a process nobody can see holding the
        // tree lock. Giving up is what makes the "leave it queued" branch reachable.
        var wait = EmulationStationProcess.WaitForExit(
            () => EsRunningVerdict.Running("EmulationStation is running from this install (process 1234)."),
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(20));

        Assert.False(wait.Gone);
        Assert.Contains("process 1234", wait.Detail!, StringComparison.Ordinal);
        Assert.True(wait.Waited >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void A_process_whose_location_cannot_be_read_is_waited_out_rather_than_treated_as_gone()
    {
        // Fails closed, the same way Check does. Windows refuses MainModule across a bitness
        // boundary and for anything the caller lacks rights to, and the cost of reading that
        // as "gone" is a config change written under a live ES and silently discarded.
        var wait = EmulationStationProcess.WaitForExit(
            () => EsRunningVerdict.Running(
                "1 EmulationStation process is running and its location could not be read"),
            TimeSpan.FromMilliseconds(80),
            TimeSpan.FromMilliseconds(20));

        Assert.False(wait.Gone);
        Assert.Contains("could not be read", wait.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_budget_is_never_overshot_by_the_last_sleep()
    {
        // A poll interval longer than what is left would push the wait past its budget, which
        // matters because this runs inside a pass EmulationStation's own shutdown spawned.
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var wait = EmulationStationProcess.WaitForExit(
            () => EsRunningVerdict.Running("up"),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(10));

        clock.Stop();

        Assert.False(wait.Gone);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"waited {clock.Elapsed}");
    }

    [Fact]
    public void The_defaults_are_the_measured_ones()
    {
        // Pinned so a later edit to either is a deliberate change to a number that came from a
        // measurement. 30 s is roughly 400 times the observed teardown, sized for a shutdown
        // that has stalled rather than for the ordinary case; 100 ms is well under it, so the
        // usual wait is a single poll.
        Assert.Equal(TimeSpan.FromSeconds(30), EmulationStationProcess.DefaultExitBudget);
        Assert.Equal(TimeSpan.FromMilliseconds(100), EmulationStationProcess.DefaultPollInterval);
    }
}
