using RomM.Client;
using RomMBat.Core.Paths;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Store;
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
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, System.Net.HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, System.Net.HttpStatusCode.Forbidden)]
    public async Task Game_end_upload_recovers_from_temporary_failures_only(
        bool holdSaveLock, System.Net.HttpStatusCode? negotiateFailure)
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;
        using var stub = new StubRomMServer();
        var origin = new Uri("https://romm.invalid");
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = "rmm_test" }, stub);
        var install = session.Install;
        var token = TestContext.Current.CancellationToken;
        var rom = RelativePath.Create("roms/snes/ActRaiser (USA).zip");
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(rom))!);
        File.WriteAllText(install.Resolve(rom), "rom bytes");
        session.Store.Files.Record(new LocalFile
        {
            Path = rom, RomId = 42, FileName = rom.Name, Folder = "snes", Kind = LocalFileKind.Rom,
        });
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(install));
        session.Store.Device.SavePairing(new PairingResult(origin, "device", "Handheld",
            new GrantedScopes(["assets.read", "assets.write"]), TokenProtector.Protect("rmm_test", null, null)),
            DateTimeOffset.UtcNow);
        stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        using var library = TreeLock.TryAcquireLibrary(install);
        Assert.NotNull(library);
        // Take the save lock only after startup, so this work comes from game-end alone.
        TreeLock? contention = null;
        var clock = new SessionClock();
        var running = true;
        var gameEnded = false;
        var skipped = new List<double>();
        var uploads = new List<double>();
        var attempted = new List<double>();
        var requestsAfterUpload = 0;
        var now = DateTimeOffset.UtcNow;
        try
        {
            await BackgroundSession.RunAsync(install, () => running, async (mode, ct) =>
            {
                stub.NegotiateRefusal = gameEnded && clock.Seconds < 67 ? negotiateFailure : null;
                var report = await new SaveFlushService(session).RunAsync(new FlushOptions { Mode = mode }, connection, ct);
                if (mode != FlushMode.Background)
                {
                    Assert.Equal(FlushMode.InSessionUpload, mode);
                    attempted.Add(clock.Seconds);
                    if (negotiateFailure == System.Net.HttpStatusCode.Forbidden)
                    {
                        Assert.False(report.GameEndUploadPending);
                        Assert.False(report.TransientUploadFailure);
                    }

                    if (report.State == FlushState.Skipped)
                    {
                        Assert.True(report.RetryUpload);
                        Assert.Null(report.ServerReachable);
                        skipped.Add(clock.Seconds);
                    }

                    if (report.SavesSent?.Uploaded == 1)
                    {
                        uploads.Add(clock.Seconds);
                        requestsAfterUpload = stub.RequestLog.Count;
                    }
                }

                return BackgroundPassResult.FromReport(0, report);
            }, token, (delay, _) =>
            {
                if (!gameEnded)
                {
                    Assert.Equal(Timeout.InfiniteTimeSpan, delay);
                    contention = holdSaveLock ? TreeLock.TryAcquire(install) : null;
                    Assert.Equal(holdSaveLock, contention is not null);
                    var save = install.Resolve("saves/snes/ActRaiser (USA).srm");
                    Directory.CreateDirectory(Path.GetDirectoryName(save)!);
                    File.WriteAllText(save, "synthetic battery bytes");
                    Spool.Write(install.RootPath, new SpoolRecord("game-start", now, 1, [install.Resolve(rom)]));
                    Spool.Write(install.RootPath, new SpoolRecord("game-end", now.AddSeconds(1), 2, []));
                    gameEnded = true;
                    return Task.FromResult(true);
                }

                if (delay == Timeout.InfiniteTimeSpan)
                {
                    clock.Advance(TimeSpan.FromMinutes(2));
                    running = false;
                }
                else
                {
                    Assert.True(clock.Seconds < 90);
                    if (clock.Seconds < 67 && clock.Seconds + delay.TotalSeconds >= 67)
                    {
                        var untilRelease = TimeSpan.FromSeconds(67 - clock.Seconds);
                        clock.Advance(untilRelease);
                        contention?.Dispose();
                        clock.Advance(delay - untilRelease);
                    }
                    else
                    {
                        clock.Advance(delay);
                    }
                }

                return Task.FromResult(false);
            }, clock, probe: _ => throw new InvalidOperationException("A local lock should not probe RomM."), periodicReceive: false);
        }
        finally
        {
            contention?.Dispose();
        }

        Assert.Equal(holdSaveLock ? [0, 2, 7, 17, 47, 62] : Array.Empty<double>(), skipped);
        if (negotiateFailure == System.Net.HttpStatusCode.Forbidden)
        {
            Assert.DoesNotContain(attempted, at => at > 47);
            Assert.Empty(uploads);
            Assert.Empty(stub.Saves);
            return;
        }

        Assert.Equal(holdSaveLock || negotiateFailure is not null ? [77] : new double[] { 0 }, uploads);
        Assert.Equal("synthetic battery bytes"u8.ToArray(), Assert.Single(stub.Saves.Values).Bytes);
        Assert.Equal(requestsAfterUpload, stub.RequestLog.Count);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("scan lock")]
    [InlineData("upload lock")]
    [InlineData("state scan lock")]
    [InlineData("state upload lock")]
    [InlineData("manual without event")]
    public async Task Pending_game_end_bytes_recover_without_another_event(string obstacle)
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var stub = new StubRomMServer();
        var origin = new Uri("https://romm.invalid");
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = "rmm_test" }, stub);
        var token = TestContext.Current.CancellationToken;
        var rom = RelativePath.Create("roms/snes/ActRaiser (USA).zip");
        var state = obstacle.StartsWith("state ", StringComparison.Ordinal);
        var scanLock = obstacle.EndsWith("scan lock", StringComparison.Ordinal);
        var uploadLock = obstacle.EndsWith("upload lock", StringComparison.Ordinal);
        var manualOnly = obstacle == "manual without event";
        var recovered = obstacle == "restart" || manualOnly;
        var save = install.Resolve(state
            ? "saves/snes/libretro.snes9x/ActRaiser (USA).state1"
            : "saves/snes/ActRaiser (USA).srm");
        if (state)
        {
            var schema = install.Resolve(RomMBat.Core.RetroBat.SaveStateSchema.ConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "es_savestates.template.cfg"), schema);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(rom))!);
        File.WriteAllText(install.Resolve(rom), "synthetic rom");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        var now = DateTimeOffset.UtcNow;
        stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        void CloseGame()
        {
            File.WriteAllText(save, "synthetic battery bytes");
            Spool.Write(install.RootPath, new SpoolRecord("game-start", now, 1, [install.Resolve(rom)]));
            Spool.Write(install.RootPath, new SpoolRecord("game-end", now.AddSeconds(1), 2, []));
        }

        using (var first = InstallSession.Open(tree.Root).Session!)
        {
            first.Store.Files.Record(new LocalFile
            {
                Path = rom, RomId = 42, FileName = rom.Name, Folder = "snes", Kind = LocalFileKind.Rom,
            });
            first.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(install));
            first.Store.Device.SavePairing(new PairingResult(origin, "device", "Handheld",
                new GrantedScopes(["assets.read", "assets.write"]), TokenProtector.Protect("rmm_test", null, null)), now);

            if (recovered)
            {
                if (manualOnly)
                {
                    File.WriteAllText(save, "synthetic battery bytes");
                }
                else
                {
                    CloseGame();
                }

                stub.NegotiateRefusal = System.Net.HttpStatusCode.ServiceUnavailable;
                var manual = await new SaveFlushService(first).RunAsync(new FlushOptions(), connection, token);
                Assert.True(manual.RetryUpload);
                Assert.Equal(!manualOnly, manual.GameEndUploadPending);
                Assert.False(Directory.Exists(install.Resolve(SpoolDrain.Directory))
                    && Directory.EnumerateFiles(install.Resolve(SpoolDrain.Directory), "*" + Spool.Extension).Any());
                Assert.Single(first.Store.Saves.PendingUploads());
            }
        }

        using var reopened = InstallSession.Open(tree.Root).Session!;
        var clock = new SessionClock();
        var running = true;
        var gameEnded = recovered;
        var uploads = new List<double>();
        var requestsAfterUpload = 0;
        FileStream? held = null;
        if (uploadLock)
        {
            stub.BeforeNegotiate = () =>
            {
                if (gameEnded && clock.Seconds < 67)
                {
                    held = File.Open(save, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
            };
        }

        try
        {
            await BackgroundSession.RunAsync(install, () => running, async (mode, ct) =>
            {
                if (recovered && clock.Seconds >= 67)
                {
                    stub.NegotiateRefusal = null;
                }

                var report = await new SaveFlushService(reopened).RunAsync(new FlushOptions { Mode = mode }, connection, ct);
                if (uploadLock && held is not null)
                {
                    var hash = state ? Assert.Single(reopened.Store.States.List()).ContentHash
                        : Assert.Single(reopened.Store.Saves.List()).ContentHash;
                    Assert.NotNull(hash);
                    var problems = state ? report.StatesSent!.Problems : report.SavesSent!.Problems;
                    Assert.Contains(problems, problem => problem.Contains("could not be read", StringComparison.Ordinal));
                    held.Dispose();
                    held = null;
                }

                if (scanLock && held is not null)
                {
                    Assert.Null(state ? Assert.Single(reopened.Store.States.List()).ContentHash
                        : Assert.Single(reopened.Store.Saves.List()).ContentHash);
                }

                if ((state ? report.StatesSent?.Uploaded : report.SavesSent?.Uploaded) == 1)
                {
                    Assert.False(report.GameEndUploadPending);
                    uploads.Add(clock.Seconds);
                    requestsAfterUpload = stub.RequestLog.Count;
                }

                return BackgroundPassResult.FromReport(0, report);
            }, token, (delay, _) =>
            {
                if (!gameEnded)
                {
                    Assert.Equal(Timeout.InfiniteTimeSpan, delay);
                    CloseGame();
                    gameEnded = true;
                    if (scanLock)
                    {
                        held = File.Open(save, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    }

                    return Task.FromResult(true);
                }

                if (delay == Timeout.InfiniteTimeSpan)
                {
                    running = false;
                }
                else
                {
                    Assert.True(clock.Seconds < 90);
                    clock.Advance(delay);
                    if (clock.Seconds >= 67)
                    {
                        held?.Dispose();
                        held = null;
                    }
                }

                return Task.FromResult(false);
            }, clock, probe: _ => throw new InvalidOperationException("This recovery does not require a heartbeat."), periodicReceive: false);
        }
        finally
        {
            held?.Dispose();
        }

        if (manualOnly)
        {
            Assert.Empty(uploads);
            Assert.Empty(stub.Saves);
            Assert.Equal(47, clock.Seconds);
            return;
        }

        Assert.Equal([77], uploads);
        var bytes = state ? Assert.Single(stub.States.Values).Bytes : Assert.Single(stub.Saves.Values).Bytes;
        Assert.Equal("synthetic battery bytes"u8.ToArray(), bytes);
        Assert.Equal(requestsAfterUpload, stub.RequestLog.Count);
    }

    [Theory]
    [InlineData(false, FlushState.Skipped, true)]
    [InlineData(true, FlushState.Partial, true)]
    [InlineData(true, FlushState.Done, false)]
    [InlineData(true, FlushState.GameRunning, true)]
    public async Task Slow_local_retries_require_a_game_end_and_a_local_deferral(
        bool gameEnd, FlushState state, bool retry)
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var clock = new SessionClock();
        var running = true;
        var attempts = new List<double>();
        var initialWait = true;
        var delays = new List<TimeSpan>();
        var result = await BackgroundSession.RunAsync(install, () => running, (mode, _) =>
        {
            if (gameEnd && mode == FlushMode.Background)
            {
                return Task.FromResult(new BackgroundPassResult(0));
            }

            Assert.NotEqual(FlushMode.InSessionReceive, mode);
            attempts.Add(clock.Seconds);
            return Task.FromResult(clock.Seconds >= 77
                ? new BackgroundPassResult(0)
                : new BackgroundPassResult(0, RetryUpload: retry, State: state));
        }, TestContext.Current.CancellationToken, (delay, _) =>
        {
            if (initialWait && gameEnd)
            {
                initialWait = false;
                Assert.Equal(Timeout.InfiniteTimeSpan, delay);
                Spool.Write(install.RootPath, new SpoolRecord("game-end", DateTimeOffset.UtcNow, 1, []));
                return Task.FromResult(true);
            }

            delays.Add(delay);
            if (delay == Timeout.InfiniteTimeSpan)
            {
                running = false;
            }
            else
            {
                Assert.True(clock.Seconds < 90);
                clock.Advance(delay);
            }

            return Task.FromResult(false);
        }, clock, probe: _ => throw new InvalidOperationException("Local retries should not probe RomM."), periodicReceive: false);

        Assert.Equal(0, result);
        double[] expected = state switch
        {
            FlushState.GameRunning => [0, 2, 7, 17, 47, 62, 77],
            FlushState.Done => [0],
            _ => [0, 2, 7, 17, 47],
        };
        Assert.Equal(expected, attempts);
        Assert.Equal(Timeout.InfiniteTimeSpan, delays[^1]);
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
    public async Task Prelaunch_releases_the_startup_lock_before_the_journal_hook_runs()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        using var store = LocalStore.Open(install);
        using var stub = new StubRomMServer();
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("https://romm.invalid"), AccessToken = "rmm_test" }, stub);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var rom = RelativePath.Create("roms/nes/Zelda II.nes");
        var path = install.Resolve(rom);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "rom");
        store.Files.Record(new LocalFile { Path = rom, RomId = 42, FileName = rom.Name, Folder = "nes" });
        SelectLibretro(install);
        stub.Saves[26] = PeerSave();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = BackgroundSession.RunAsync(install, () => false, async (_, token) =>
        {
            using var held = TreeLock.TryAcquire(install);
            Assert.NotNull(held);
            locked.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new BackgroundPassResult(0);
        }, deadline.Token);

        try
        {
            await locked.Task.WaitAsync(deadline.Token);
            var result = await new SaveSync(install, store, connection, "device")
                .PrepareLaunchAsync(rom, deadline.Token);
            Assert.Equal("incoming save ready", result);
            Assert.Equal("peer progress", File.ReadAllText(install.Resolve("saves/nes/Zelda II.srm")));
            Assert.Empty(Directory.GetFiles(install.Resolve(SpoolDrain.Directory), "*.hook"));
        }
        finally
        {
            await deadline.CancelAsync();
            await worker;
        }
    }

    [Fact]
    public async Task Pending_ack_wakes_only_its_verified_install_without_a_receive_tick()
    {
        using var tree = TempRetroBatTree.Create();
        using var otherTree = TempRetroBatTree.Create();
        var install = tree.Install();
        var other = otherTree.Install();
        install.EnsureAppDirectories();
        other.EnsureAppDirectories();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var modes = new List<FlushMode>();
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherUnexpected = false;
        var worker = BackgroundSession.RunAsync(install, () => true, (mode, _) =>
        {
            modes.Add(mode);
            if (mode != FlushMode.Background)
            {
                ran.TrySetResult();
            }

            return Task.FromResult(new BackgroundPassResult(0));
        }, deadline.Token, periodicReceive: false);
        var otherWorker = BackgroundSession.RunAsync(other, () => true, (mode, _) =>
        {
            otherUnexpected |= mode != FlushMode.Background;
            return Task.FromResult(new BackgroundPassResult(0));
        }, deadline.Token, periodicReceive: false);

        try
        {
            using (File.Open(Path.Combine(install.Resolve(SpoolDrain.Directory), "receipt.ack"),
                new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.DeleteOnClose }))
            {
            }

            await ran.Task.WaitAsync(deadline.Token);
        }
        finally
        {
            await deadline.CancelAsync();
            await Task.WhenAll(worker, otherWorker);
        }

        Assert.Equal([FlushMode.Background, FlushMode.InSessionAcknowledgement], modes);
        Assert.False(otherUnexpected);
    }

    [Fact]
    public async Task Verified_worker_recovers_an_ack_on_reconnect_then_stops_requesting()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;
        var install = session.Install;
        using var stub = new StubRomMServer();
        var origin = new Uri("https://romm.invalid");
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = "rmm_test" }, stub);
        var token = TestContext.Current.CancellationToken;
        var rom = RelativePath.Create("roms/nes/Zelda II.nes");
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(rom))!);
        File.WriteAllText(install.Resolve(rom), "rom");
        session.Store.Files.Record(new LocalFile { Path = rom, RomId = 42, FileName = rom.Name, Folder = "nes" });
        SelectLibretro(install);
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(install));
        session.Store.Device.SavePairing(new PairingResult(origin, "device", "Handheld",
            new GrantedScopes(["assets.read", "devices.write"]), TokenProtector.Protect("rmm_test", null, null)),
            DateTimeOffset.UtcNow);
        stub.Saves[26] = PeerSave();
        stub.FailAcknowledge = System.Net.HttpStatusCode.ServiceUnavailable;
        await new SaveSync(install, session.Store, connection, "device").PrepareLaunchAsync(rom, token);
        Assert.Empty(stub.Acknowledged);
        stub.FailAcknowledge = null;

        var clock = new SessionClock();
        var running = true;
        var attempts = new List<double>();
        var probes = new List<double>();
        var unrelatedAfterStartup = 0;
        var requestsAtSuccess = 0;
        await BackgroundSession.RunAsync(install, () => running, async (mode, ct) =>
        {
            stub.IsReachable = clock.Seconds >= 72;
            var before = stub.RequestLog.Count;
            var report = await new SaveFlushService(session).RunAsync(new FlushOptions { Mode = mode }, connection, ct);
            Assert.Equal(0, report.Queued);
            if (mode != FlushMode.Background)
            {
                Assert.Equal(FlushMode.InSessionAcknowledgement, mode);
                attempts.Add(clock.Seconds);
                unrelatedAfterStartup += stub.RequestLog.Skip(before)
                    .Count(path => !path.EndsWith("/downloaded", StringComparison.Ordinal));
                if (!report.RetryAcknowledgement)
                {
                    requestsAtSuccess = stub.RequestLog.Count;
                }
            }

            return BackgroundPassResult.FromReport(0, report);
        }, token, (delay, _) =>
        {
            if (delay == Timeout.InfiniteTimeSpan)
            {
                clock.Advance(TimeSpan.FromMinutes(2));
                running = false;
            }
            else
            {
                clock.Advance(delay);
            }

            return Task.FromResult(false);
        }, clock, probe: _ =>
        {
            probes.Add(clock.Seconds);
            return Task.FromResult(clock.Seconds >= 72);
        }, periodicReceive: false);

        Assert.Equal([2, 7, 17, 47, 77], attempts);
        Assert.Equal([62, 77], probes);
        Assert.Equal([26], stub.Acknowledged);
        Assert.Equal(0, unrelatedAfterStartup);
        Assert.Equal(requestsAtSuccess, stub.RequestLog.Count);
        Assert.Equal("peer progress", File.ReadAllText(install.Resolve("saves/nes/Zelda II.srm")));
    }

    private static StubRomMServer.StubSave PeerSave() => new()
    {
        Id = 26, RomId = 42, Slot = "libretro:battery", Emulator = "libretro",
        FileNameNoTags = "Zelda II", FileExtension = "srm", Bytes = "peer progress"u8.ToArray(),
    };

    private static void SelectLibretro(RetroBatInstall install)
    {
        var settings = install.Resolve("emulationstation/.emulationstation/es_settings.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """<config><string name="nes.emulator" value="libretro" /></config>""");
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
