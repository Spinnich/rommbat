using RomMBat.Agent.Commands;
using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>saves</c> command's gates.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// </remarks>
[Collection("agent-console")]
public sealed class SavesCommandTests
{
    [Theory]
    [InlineData("saves", "restore", "--help")]
    [InlineData("saves", "restore", "-h")]
    [InlineData("saves", "restore", "42", "--apply", "--help")]
    [InlineData("bios", "nes", "--apply", "--help")]
    public async Task Help_prints_usage_and_runs_nothing(params string[] args)
    {
        // `saves restore --help` used to run a full restore preview, and with --apply on the line
        // the same mistake writes. Held under the tree lock so any handler that did run would
        // refuse, and a refusal is not exit 0.
        using var tree = TempRetroBatTree.Create();

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(tree, args);

            Assert.Equal(ExitCode.Ok, run.ExitCode);
            Assert.True(run.Complained("rommbat-agent <subcommand> [options]"), run.Error);
            Assert.Equal(string.Empty, run.Out);
        }
    }

    [Fact]
    public async Task Resolve_refuses_while_a_flush_holds_the_tree_lock()
    {
        // It runs the same class C restore a flush does, into the same shared container, so two
        // at once leaves the container half swapped. Refused rather than reported as done: a
        // person asked for this one, and exit 0 would read as having resolved it.
        using var tree = TempRetroBatTree.Create();

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(
                tree, "saves", "resolve", "42", "libretro:battery", "--keep-local");

            Assert.Equal(3, run.ExitCode);
            Assert.True(run.Complained("A flush is running"), run.Error);
            Assert.True(run.Complained("Nothing was changed"), run.Error);
        }
    }

    [Fact]
    public async Task The_sidecar_route_attributes_a_directory_save_on_the_first_run()
    {
        // SaveScanner constructs the GameIdAttributor, and the sidecar route reads local_state,
        // which StateScanner writes. All three commands ran SaveScanner first, so on any tree
        // whose local_state was still empty the route read an empty list and answered nothing.
        // It only worked from the second invocation onward.
        //
        // Observed on a real install with a real PPSSPP state and its .txt sidecar: run one
        // said "0 of 3 directory saves attributed" and printed the "no matching ROM" reason
        // with its "their ROMs are not on this device" explanation, which is actively
        // misleading when the ROM is right there and the route that finds it had not run.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);
        AgentRunner.WriteEsSaveStates(tree);

        SeedPspGame(tree);

        var run = await AgentRunner.RunAsync(tree, "saves");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("1 of 1 directory saves attributed"), run.Out);
        Assert.True(run.Wrote("psp/ULES01513"), run.Out);
        Assert.False(run.Wrote("their ROMs are not on this device"), run.Out);
    }

    /// <summary>
    /// A PSP game with a save state, its name sidecar, and the directory save the sidecar names.
    /// </summary>
    /// <remarks>
    /// The shape measured on a real install: <c>ppsspp/3rd Birthday, The (Europe).txt</c> holds
    /// <c>ULES01513_1.00</c>, whose <c>ULES01513</c> prefix joins <c>SAVEDATA/ULES01513SYSDATA</c>
    /// while the stem resolves through <c>RomIndex</c>.
    /// </remarks>
    private static void SeedPspGame(TempRetroBatTree tree)
    {
        const string stem = "3rd Birthday, The (Europe)";
        var install = tree.Install();

        Write(install, $"roms/psp/{stem}.cso", "rom bytes");
        Write(install, $"saves/psp/ppsspp/{stem}_0.ppst", "state bytes");
        Write(install, $"saves/psp/ppsspp/{stem}.txt", "ULES01513_1.00");
        Write(install, "saves/psp/SAVEDATA/ULES01513SYSDATA/DATA.BIN", "the save");

        using var store = LocalStore.Open(install);

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/psp/{stem}.cso"),
            Folder = "psp",
            RomId = 391,
            Kind = LocalFileKind.Rom,
            FileName = $"{stem}.cso",
            SizeBytes = 9,
        });
    }

    private static void Write(RetroBatInstall install, string relativePath, string content)
    {
        var absolute = install.Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
    }

    [Fact]
    public async Task The_report_says_it_could_not_ask_the_server_and_still_answers_locally()
    {
        // #138 made saves ask the server for saves with no slot. The rest of the report is local
        // and the command works offline, so a server that is not there costs one line and the
        // exit code does not move. --offline skips the read altogether.
        using var tree = TempRetroBatTree.Create();
        Pair(tree, new Uri("http://127.0.0.1:9"));

        var online = await AgentRunner.RunAsync(tree, "saves");

        Assert.Equal(0, online.ExitCode);
        Assert.True(online.Wrote("No saves found under saves/."), online.Out);
        Assert.True(online.Wrote("Server saves with no slot: not checked"), online.Out);

        var offline = await AgentRunner.RunAsync(tree, "saves", "--offline");

        Assert.Equal(0, offline.ExitCode);
        Assert.False(offline.Wrote("Server saves with no slot"), offline.Out);
    }

    [Fact]
    public async Task The_report_survives_a_server_answer_it_cannot_read()
    {
        // A proxy's login page, or a newer RomM whose save row no longer deserializes, is a 200
        // whose body is not the list, and the connection throws RomMApiException for it rather
        // than answering a failure. It costs the same one line an unreachable server does.
        using var tree = TempRetroBatTree.Create();
        using var server = new LoginPageServer();
        Pair(tree, server.Origin);

        var run = await AgentRunner.RunAsync(tree, "saves");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("No saves found under saves/."), run.Out);
        Assert.True(run.Wrote("Server saves with no slot: not checked"), run.Out);
    }

    private static void Pair(TempRetroBatTree tree, Uri origin)
    {
        var install = tree.Install();
        using var store = LocalStore.Open(install);
        var now = DateTimeOffset.UtcNow;

        store.Device.EnsureIdentity(RomMBat.Core.Identity.DeviceIdentity.ReadOrCreate(install));
        store.Device.SavePairing(
            new PairingResult(
                origin,
                "device-1",
                "Handheld",
                new RomM.Client.GrantedScopes(["assets.read"]),
                RomMBat.Core.Identity.TokenProtector.Protect("rmm_token", null, now.AddYears(1))),
            now);
    }

    /// <summary>Answers every request on a loopback port with a 200 HTML page.</summary>
    /// <remarks>
    /// A real socket, because the agent builds its own handler from the stored origin and there
    /// is no seam to hand it a stub.
    /// </remarks>
    private sealed class LoginPageServer : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener = new(System.Net.IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;

        public LoginPageServer()
        {
            _listener.Start();
            Origin = new Uri($"http://127.0.0.1:{((System.Net.IPEndPoint)_listener.LocalEndpoint).Port}");
            _serving = ServeAsync(_stop.Token);
        }

        public Uri Origin { get; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();

            try
            {
                _serving.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }

            _stop.Dispose();
        }

        private async Task ServeAsync(CancellationToken cancellationToken)
        {
            const string Body = "<html><body>Sign in</body></html>";
            var response = System.Text.Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n"
                    + $"Content-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}");

            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var head = new System.Text.StringBuilder();

                while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    head.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, read));
                }

                await stream.WriteAsync(response, cancellationToken);
            }
        }
    }

    [Fact]
    public void Restore_ends_partial_only_for_what_the_run_failed_at()
    {
        // #148. The unplaceable rows are not an input at all, which is the rule: measured on nes,
        // 18 states scoped by core pinned every --apply at 7 with "failed 0".
        Assert.Equal(ExitCode.Ok, SavesCommand.RestoreExitCode(true, new(), new(), statesUnread: false));

        Assert.Equal(ExitCode.Partial, SavesCommand.RestoreExitCode(true, new() { Failed = 1 }, new(), false));
        Assert.Equal(ExitCode.Partial, SavesCommand.RestoreExitCode(true, new(), new() { Failed = 1 }, false));
        Assert.Equal(ExitCode.Partial, SavesCommand.RestoreExitCode(true, new(), new() { Refused = true }, false));
        Assert.Equal(ExitCode.Partial, SavesCommand.RestoreExitCode(true, new() { Deferred = 1 }, new(), false));
        Assert.Equal(ExitCode.Partial, SavesCommand.RestoreExitCode(true, new(), new(), statesUnread: true));

        // A preview attempted nothing, so nothing it found is a failure.
        Assert.Equal(ExitCode.Ok, SavesCommand.RestoreExitCode(false, new() { Failed = 1 }, new(), true));
    }

    [Fact]
    public async Task Resolve_still_names_the_side_before_it_looks_at_the_lock()
    {
        // The usage gate stays first. "You must name a side" is the answer to a command line
        // that named none, whatever else is running.
        using var tree = TempRetroBatTree.Create();

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(tree, "saves", "resolve", "42", "libretro:battery");

            Assert.Equal(2, run.ExitCode);
            Assert.False(run.Complained("A flush is running"), run.Error);
        }
    }
}
