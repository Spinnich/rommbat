using RomMBat.Agent.Commands;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

public sealed class BackgroundSessionTests
{
    [Fact]
    public async Task Session_routes_game_end_to_upload_and_does_not_upload_on_the_receive_timer()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var token = TestContext.Current.CancellationToken;
        var clock = new SessionClock();
        var running = true;
        var waits = 0;
        var passes = new List<FlushMode>();

        async Task<bool> Wait(TimeSpan delay, CancellationToken cancellationToken)
        {
            switch (++waits)
            {
                case 1:
                    Assert.Equal([FlushMode.Background], passes);
                    Assert.Equal(0, await BackgroundSession.RunAsync(install, () => true,
                        (_, _) => throw new InvalidOperationException("Duplicate worker ran."), cancellationToken));
                    Spool.Write(install.RootPath, new SpoolRecord("game-start", DateTimeOffset.UtcNow, 1, []));
                    return true;

                case 2:
                    Assert.Equal([FlushMode.Background], passes);
                    Assert.Equal(TimeSpan.FromSeconds(30), delay);
                    clock.Advance(delay);
                    return false;

                case 3:
                    Assert.Equal([FlushMode.Background, FlushMode.InSessionReceive], passes);
                    Spool.Write(install.RootPath, new SpoolRecord("game-end", DateTimeOffset.UtcNow, 2, []));
                    return true;

                case 4:
                    Assert.Equal([FlushMode.Background, FlushMode.InSessionReceive, FlushMode.InSessionUpload], passes);
                    return true; // A duplicate filesystem notification after the records were drained.

                case 5:
                    Assert.Equal(3, passes.Count);
                    Assert.Equal(TimeSpan.FromSeconds(2), delay);
                    clock.Advance(delay);
                    return false;

                case 6:
                    Assert.Equal(FlushMode.InSessionUpload, passes.Last());
                    Assert.Equal(4, passes.Count);
                    Assert.Equal(TimeSpan.FromSeconds(28), delay);
                    clock.Advance(delay);
                    return false;

                default:
                    Assert.Equal(FlushMode.InSessionReceive, passes.Last());
                    Assert.Equal(5, passes.Count);
                    running = false;
                    return false;
            }
        }

        Task<BackgroundPassResult> Flush(FlushMode mode, CancellationToken _)
        {
            passes.Add(mode);
            var firstUpload = mode == FlushMode.InSessionUpload
                && passes.Count(pass => pass == FlushMode.InSessionUpload) == 1;
            if (firstUpload)
            {
                foreach (var record in Directory.EnumerateFiles(install.Resolve(SpoolDrain.Directory)))
                {
                    File.Delete(record);
                }
            }

            return Task.FromResult(new BackgroundPassResult(0, RetryUpload: firstUpload));
        }

        Assert.Equal(0, await BackgroundSession.RunAsync(install, () => running, Flush, token, Wait, clock));
        Assert.Equal(7, waits);

        // The OS releases the handle independently of whether its file is still there.
        Assert.Equal(42, await BackgroundSession.RunAsync(
            install, () => false, (_, _) => Task.FromResult(new BackgroundPassResult(42)), token));
    }

    [Fact]
    public async Task Startup_recovery_has_a_bounded_retry_budget_even_while_receive_continues()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var clock = new SessionClock();
        var running = true;
        var passes = new List<(FlushMode Mode, double Seconds)>();

        Task<bool> Wait(TimeSpan delay, CancellationToken _)
        {
            clock.Advance(delay);
            running = clock.Seconds <= 90;
            return Task.FromResult(false);
        }

        Task<BackgroundPassResult> Flush(FlushMode mode, CancellationToken _)
        {
            passes.Add((mode, clock.Seconds));
            return Task.FromResult(new BackgroundPassResult(0, RetryUpload: mode != FlushMode.InSessionReceive));
        }

        await BackgroundSession.RunAsync(install, () => running, Flush,
            TestContext.Current.CancellationToken, Wait, clock);

        Assert.Equal([0, 2, 7, 17, 47], passes.Where(pass => pass.Mode != FlushMode.InSessionReceive).Select(pass => pass.Seconds));
        Assert.Equal([30, 60, 90], passes.Where(pass => pass.Mode == FlushMode.InSessionReceive).Select(pass => pass.Seconds));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Receive_reconnect_resumes_only_known_uploads_after_short_retries_expire(bool pending)
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var clock = new SessionClock();
        var running = true;
        var uploads = new List<double>();
        var receives = new List<double>();

        Task<bool> Wait(TimeSpan delay, CancellationToken _)
        {
            clock.Advance(delay);
            running = clock.Seconds <= 120;
            return Task.FromResult(false);
        }

        Task<BackgroundPassResult> Flush(FlushMode mode, CancellationToken _)
        {
            // The outage lasts longer than all four retry delays. No additional game ends.
            var connected = clock.Seconds >= 72;
            if (mode == FlushMode.InSessionReceive)
            {
                receives.Add(clock.Seconds);
                return Task.FromResult(new BackgroundPassResult(0, ServerReachable: connected));
            }

            uploads.Add(clock.Seconds);
            return Task.FromResult(new BackgroundPassResult(
                0, RetryUpload: pending && !connected, ServerReachable: connected));
        }

        await BackgroundSession.RunAsync(install, () => running, Flush,
            TestContext.Current.CancellationToken, Wait, clock);

        Assert.Equal([30, 60, 90, 120], receives);
        Assert.Equal(pending ? [0, 2, 7, 17, 47, 90] : new double[] { 0 }, uploads);
    }

    [Fact]
    public async Task Atomic_spool_commit_wakes_the_real_filesystem_watcher()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var wakeup = new SpoolWakeup(install.Resolve(SpoolDrain.Directory), TimeProvider.System);
        var signal = wakeup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Spool.Write(install.RootPath, new SpoolRecord("game-end", DateTimeOffset.UtcNow, 1, []));

        Assert.True(await signal);
    }

    private sealed class SessionClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public double Seconds => TimeSpan.FromTicks(_timestamp).TotalSeconds;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
