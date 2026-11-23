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
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Independent_probe_resumes_only_known_uploads_after_short_retries_expire(
        bool pending, bool periodicReceive)
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var clock = new SessionClock();
        var running = true;
        var uploads = new List<double>();
        var receives = new List<double>();
        var probes = new List<double>();

        Task<bool> Wait(TimeSpan delay, CancellationToken _)
        {
            if (delay == Timeout.InfiniteTimeSpan)
            {
                running = false;
                return Task.FromResult(false);
            }

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
                return Task.FromResult(new BackgroundPassResult(0));
            }

            uploads.Add(clock.Seconds);
            return Task.FromResult(new BackgroundPassResult(
                0, RetryUpload: pending && !connected, ServerReachable: connected));
        }

        await BackgroundSession.RunAsync(install, () => running, Flush,
            TestContext.Current.CancellationToken, Wait, clock, probe: _ =>
            {
                probes.Add(clock.Seconds);
                return Task.FromResult(clock.Seconds >= 72);
            }, periodicReceive: periodicReceive);

        Assert.Equal(periodicReceive ? [30, 60, 90, 120] : Array.Empty<double>(), receives);
        Assert.Equal(pending ? [62, 77] : Array.Empty<double>(), probes);
        Assert.Equal(pending ? [0, 2, 7, 17, 47, 77] : new double[] { 0 }, uploads);
    }

    [Fact]
    public async Task Verified_idle_has_no_timer_until_a_gate_file_changes()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var clock = new SessionClock();
        var running = true;
        var passes = new List<FlushMode>();
        var waits = 0;
        Action? invalidate = null;

        Task<bool> Wait(TimeSpan delay, CancellationToken _)
        {
            if (++waits == 1)
            {
                Assert.Equal(Timeout.InfiniteTimeSpan, delay);
                clock.Advance(TimeSpan.FromSeconds(75));
                Assert.Equal([FlushMode.Background], passes);
                invalidate!();
                return Task.FromResult(true);
            }

            Assert.Equal(TimeSpan.FromSeconds(30), delay);
            Assert.Equal([FlushMode.Background, FlushMode.InSessionReceive], passes);
            running = false;
            return Task.FromResult(false);
        }

        await BackgroundSession.RunAsync(install, () => running, (mode, _) =>
        {
            passes.Add(mode);
            return Task.FromResult(new BackgroundPassResult(0));
        }, TestContext.Current.CancellationToken, Wait, clock,
            probe: _ => throw new InvalidOperationException("An idle session probed RomM."),
            periodicReceive: false, watchPrelaunch: signal =>
            {
                invalidate = signal;
                return new MemoryStream();
            });

        Assert.Equal(2, waits);
    }

    [Fact]
    public async Task ES_exit_wakes_a_verified_idle_worker_without_a_timer()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = true;

        var worker = BackgroundSession.RunAsync(install, () => Volatile.Read(ref running), (mode, _) =>
        {
            Assert.Equal(FlushMode.Background, mode);
            entered.TrySetResult();
            return Task.FromResult(new BackgroundPassResult(0));
        }, timeout.Token, periodicReceive: false, waitForEsExit: token => exit.Task.WaitAsync(token));

        await entered.Task.WaitAsync(timeout.Token);
        Assert.False(worker.IsCompleted);
        Volatile.Write(ref running, false);
        exit.SetResult();
        Assert.Equal(0, await worker.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Game_start_cancels_an_in_flight_startup_save_pass()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var yielded = false;

        async Task<BackgroundPassResult> Flush(FlushMode mode, CancellationToken token)
        {
            Assert.Equal(FlushMode.Background, mode);
            Spool.Write(install.RootPath, new SpoolRecord("game-start", DateTimeOffset.UtcNow, 1, []));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(Timeout.Infinite, token));
            yielded = !deadline.IsCancellationRequested;
            return new BackgroundPassResult(0);
        }

        await BackgroundSession.RunAsync(install, () => false, Flush, deadline.Token);
        Assert.True(yielded);
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
