namespace RomMBat.Tests.Support;

/// <summary>
/// A clock the tests drive, whose timers fire at once and advance it by the delay they were
/// asked to wait.
/// </summary>
/// <remarks>
/// The pairing loop polls at a 5 second interval against a 600 second deadline. Testing it
/// against a real clock would take ten minutes per expiry case, so the delay has to be
/// virtual while the elapsed time stays honest, or the countdown and expiry logic would
/// never be exercised at all.
/// </remarks>
internal sealed class TestTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now = _now.Add(by);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        new ImmediateTimer(this, callback, state, dueTime);

    private sealed class ImmediateTimer : ITimer
    {
        private readonly TestTimeProvider _provider;
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public ImmediateTimer(TestTimeProvider provider, TimerCallback callback, object? state, TimeSpan dueTime)
        {
            _provider = provider;
            _callback = callback;
            _state = state;
            Fire(dueTime);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Fire(dueTime);
            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void Fire(TimeSpan dueTime)
        {
            if (dueTime == Timeout.InfiniteTimeSpan || dueTime < TimeSpan.Zero)
            {
                return;
            }

            _provider.Advance(dueTime);

            // Queued rather than invoked inline: Task.Delay assigns its timer field after
            // CreateTimer returns, and a synchronous callback would race that assignment.
            ThreadPool.QueueUserWorkItem(_ => _callback(_state));
        }
    }
}
