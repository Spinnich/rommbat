using RomMBat.Agent.Commands;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

public sealed class BackgroundSessionTests
{
    [Fact]
    public async Task Session_keeps_one_owner_and_flushes_a_new_end_event_before_ES_exits()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var token = TestContext.Current.CancellationToken;
        var running = true;
        var clock = new SessionClock();
        var polls = 0;
        var passes = 0;

        async Task Wait(CancellationToken cancellationToken)
        {
            polls++;
            switch (polls)
            {
                case 2:
                    Assert.Equal(1, passes);
                    var duplicate = await BackgroundSession.RunAsync(
                        install, () => running, (_, _) => throw new InvalidOperationException("Duplicate worker ran."),
                        cancellationToken);
                    Assert.Equal(0, duplicate);
                    Spool.Write(install.RootPath, new SpoolRecord("game-end", DateTimeOffset.UtcNow, 1, []));
                    break;

                case 3:
                    Assert.Equal(1, passes);
                    break;

                case 4:
                    Assert.Equal(2, passes);
                    break;

                case 5:
                    Assert.Equal(2, passes);
                    break;

                case 6:
                    Assert.Equal(2, passes);
                    clock.Advance(TimeSpan.FromSeconds(31));
                    break;

                case 7:
                    Assert.Equal(3, passes);
                    running = false;
                    break;
            }
        }

        Task<int> Flush(bool sessionUpload, CancellationToken _)
        {
            Assert.True(running);
            Assert.Equal(passes > 0, sessionUpload);
            passes++;
            return Task.FromResult(0);
        }

        Assert.Equal(0, await BackgroundSession.RunAsync(install, () => running, Flush, token, Wait, clock));
        Assert.Equal(7, polls);
        Assert.Equal(3, passes);

        // The lock file remains after exit. Its old name must not prevent the next worker.
        Assert.Equal(42, await BackgroundSession.RunAsync(
            install, () => false, (_, _) => Task.FromResult(42), token));
    }

    private sealed class SessionClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
