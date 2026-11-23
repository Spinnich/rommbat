using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

internal readonly record struct BackgroundPassResult(
    int ExitCode, bool RetryUpload = false, bool? ServerReachable = null);

/// <summary>Runs one worker per install for as long as EmulationStation is present.</summary>
internal static class BackgroundSession
{
    private static readonly TimeSpan ReceiveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    public static async Task<int> RunAsync(
        RetroBatInstall install,
        Func<bool> isRunning,
        Func<FlushMode, CancellationToken, Task<BackgroundPassResult>> flush,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task<bool>>? wait = null,
        TimeProvider? timeProvider = null)
    {
        using var lease = TryOwnSession(install);
        if (lease is null)
        {
            return ExitCode.Ok;
        }

        timeProvider ??= TimeProvider.System;
        var spool = install.Resolve(SpoolDrain.Directory);
        using var wakeup = new SpoolWakeup(spool, timeProvider);
        wait ??= wakeup.WaitAsync;
        var origin = timeProvider.GetTimestamp();
        var retries = 0;
        var pendingUpload = false;
        var disconnected = false;
        TimeSpan? nextUpload = null;

        try
        {
            // Subscribe before the recovery pass so an event arriving during startup wakes us.
            var initial = await RunPassAsync(FlushMode.Background).ConfigureAwait(false);
            var exitCode = initial.ExitCode;
            ScheduleRetry(initial);
            var nextReceive = timeProvider.GetElapsedTime(origin) + ReceiveInterval;

            // Check once after recovery, including events that were already present before
            // the watcher was enabled or appeared while a different pass held the tree lock.
            var spoolChanged = true;
            while (isRunning())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var now = timeProvider.GetElapsedTime(origin);
                if (spoolChanged && HasGameEnd(spool))
                {
                    // Preserve the retry budget when another notification refers to the same
                    // still-queued work, but let a fresh game end wake a delayed retry.
                    if (nextUpload is null)
                    {
                        retries = 0;
                    }

                    nextUpload = now;
                }

                if (nextUpload <= now)
                {
                    var upload = await RunPassAsync(FlushMode.InSessionUpload).ConfigureAwait(false);
                    exitCode = upload.ExitCode;
                    ScheduleRetry(upload);
                }

                now = timeProvider.GetElapsedTime(origin);
                if (nextReceive <= now)
                {
                    var receive = await RunPassAsync(FlushMode.InSessionReceive).ConfigureAwait(false);
                    exitCode = receive.ExitCode;
                    now = timeProvider.GetElapsedTime(origin);
                    nextReceive = now + ReceiveInterval;
                    if (pendingUpload && receive.ServerReachable is { } reachable)
                    {
                        // Reuse the receive's connectivity result. A transition can wake
                        // known work even after the short upload retry budget ran out.
                        if (reachable && disconnected)
                        {
                            retries = 0;
                            nextUpload = now;
                        }

                        disconnected = !reachable;
                    }
                }

                var next = nextUpload is { } uploadDue && uploadDue < nextReceive ? uploadDue : nextReceive;
                var delay = next - timeProvider.GetElapsedTime(origin);
                spoolChanged = await wait(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken)
                    .ConfigureAwait(false);
            }

            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExitCode.Canceled;
        }

        void ScheduleRetry(BackgroundPassResult pass)
        {
            pendingUpload = pass.RetryUpload;
            disconnected = pendingUpload && (pass.ServerReachable is { } reachable ? !reachable : disconnected);
            nextUpload = pendingUpload && retries < RetryDelays.Length
                ? timeProvider.GetElapsedTime(origin) + RetryDelays[retries++]
                : null;
        }

        async Task<BackgroundPassResult> RunPassAsync(FlushMode mode)
        {
            try
            {
                return await flush(mode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                using var log = BackgroundLog.Open(install, "start");
                log.Write($"session flush interrupted: {ex.Message}");
                return new BackgroundPassResult(ExitCode.ServerError, RetryUpload: mode != FlushMode.InSessionReceive);
            }
        }
    }

    private static bool HasGameEnd(string spool)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(spool, "*" + Spool.Extension))
            {
                try
                {
                    if (SpoolRecord.Parse(File.ReadAllText(file))?.Event == "game-end")
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A concurrent flush may have removed it. Other events still matter.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A removed portable drive may be reopened by the next session.
        }

        return false;
    }

    private static FileStream? TryOwnSession(RetroBatInstall install)
    {
        try
        {
            // Independent of TreeLock so UI and CLI flushes can run between passes.
            return new FileStream(
                Path.Combine(install.AppDirectoryPath, "session.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Coalesces notifications for committed spool records. The files remain the queue.</summary>
internal sealed class SpoolWakeup : IDisposable
{
    private readonly Channel<bool> _changed = Channel.CreateBounded<bool>(1);
    private readonly FileSystemWatcher _watcher;
    private readonly TimeProvider _time;

    public SpoolWakeup(string spool, TimeProvider time)
    {
        _time = time;
        Directory.CreateDirectory(spool);
        _watcher = new FileSystemWatcher(spool, "*" + Spool.Extension) { NotifyFilter = NotifyFilters.FileName };
        _watcher.Created += (_, _) => _changed.Writer.TryWrite(true);
        _watcher.Renamed += (_, _) => _changed.Writer.TryWrite(true);
        // On overflow, rescan committed records once instead of depending on event order.
        _watcher.Error += (_, _) => _changed.Writer.TryWrite(true);
        _watcher.EnableRaisingEvents = true;
    }

    public async Task<bool> WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_changed.Reader.TryRead(out _))
        {
            return true;
        }

        if (delay == TimeSpan.Zero)
        {
            return false;
        }

        using var deadline = new CancellationTokenSource(delay, _time);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await _changed.Reader.ReadAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Dispose() => _watcher.Dispose();
}
