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
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    public static async Task<int> RunAsync(
        RetroBatInstall install,
        Func<bool> isRunning,
        Func<FlushMode, CancellationToken, Task<BackgroundPassResult>> flush,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task<bool>>? wait = null,
        TimeProvider? timeProvider = null,
        Func<CancellationToken, Task<bool>>? probe = null,
        Func<CancellationToken, Task>? syncLibrary = null,
        bool periodicReceive = true,
        Func<Action, IDisposable>? watchPrelaunch = null,
        Func<CancellationToken, Task>? waitForEsExit = null)
    {
        using var lease = TryOwnSession(install);
        if (lease is null)
        {
            return ExitCode.Ok;
        }

        timeProvider ??= TimeProvider.System;
        var spool = install.Resolve(SpoolDrain.Directory);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var libraryLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watchingStartup = 1;
        using var wakeup = new SpoolWakeup(spool, timeProvider, path =>
        {
            if (Volatile.Read(ref watchingStartup) != 0 && IsGameStart(path))
            {
                startup.Cancel();
            }
        });
        wait ??= wakeup.WaitAsync;
        var fallback = 0;
        using var prelaunchWatcher = !periodicReceive && watchPrelaunch is not null
            ? watchPrelaunch(UseFallback)
            : null;
        var exitWatcher = waitForEsExit is null ? null : WakeOnExitAsync(waitForEsExit, libraryLifetime.Token);
        var origin = timeProvider.GetTimestamp();
        var retries = 0;
        var disconnected = false;
        TimeSpan? nextUpload = null;
        TimeSpan? nextProbe = null;
        Task? libraryWork = null;

        try
        {
            // Subscribe before the recovery pass so an event arriving during startup wakes us.
            var initial = await RunPassAsync(FlushMode.Background, startup.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref watchingStartup, 0);
            var exitCode = initial.ExitCode;
            ScheduleRetry(startup.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? initial with { RetryUpload = true }
                : initial);
            libraryWork = syncLibrary?.Invoke(libraryLifetime.Token);
            TimeSpan? nextReceive = periodicReceive ? timeProvider.GetElapsedTime(origin) + ReceiveInterval : null;

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
                    nextProbe = null;
                }

                if (nextProbe <= now)
                {
                    if (await probe!(cancellationToken).ConfigureAwait(false))
                    {
                        disconnected = false;
                        retries = 0;
                        nextProbe = null;
                        nextUpload = now;
                    }
                    else
                    {
                        nextProbe = timeProvider.GetElapsedTime(origin) + ReconnectInterval;
                    }
                }

                if (nextUpload <= now)
                {
                    var upload = await RunPassAsync(FlushMode.InSessionUpload, cancellationToken).ConfigureAwait(false);
                    exitCode = upload.ExitCode;
                    ScheduleRetry(upload);
                }

                now = timeProvider.GetElapsedTime(origin);
                if (nextReceive is null && Volatile.Read(ref fallback) != 0)
                {
                    nextReceive = now;
                }

                if (nextReceive <= now)
                {
                    var receive = await RunPassAsync(FlushMode.InSessionReceive, cancellationToken).ConfigureAwait(false);
                    exitCode = receive.ExitCode;
                    nextReceive = timeProvider.GetElapsedTime(origin) + ReceiveInterval;
                }

                var next = Earlier(Earlier(nextUpload, nextProbe), nextReceive);
                var delay = next is { } due ? due - timeProvider.GetElapsedTime(origin) : Timeout.InfiniteTimeSpan;
                spoolChanged = await wait(next is not null && delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken)
                    .ConfigureAwait(false);
            }

            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExitCode.Canceled;
        }

        finally
        {
            Interlocked.Exchange(ref watchingStartup, 0);
            await libraryLifetime.CancelAsync().ConfigureAwait(false);
            if (exitWatcher is not null)
            {
                await exitWatcher.ConfigureAwait(false);
            }

            if (libraryWork is not null)
            {
                try
                {
                    await libraryWork.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (libraryLifetime.IsCancellationRequested)
                {
                    // The joined library sync was stopped with the session.
                }
            }
        }

        void UseFallback()
        {
            Interlocked.Exchange(ref fallback, 1);
            wakeup.Wake();
        }

        async Task WakeOnExitAsync(Func<CancellationToken, Task> observeExit, CancellationToken token)
        {
            try
            {
                await observeExit(token).ConfigureAwait(false);
                wakeup.Wake();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Session shutdown is already processing.
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                UseFallback();
            }
        }

        void ScheduleRetry(BackgroundPassResult pass)
        {
            var now = timeProvider.GetElapsedTime(origin);
            disconnected = pass.RetryUpload && (pass.ServerReachable is { } reachable ? !reachable : disconnected);
            nextUpload = pass.RetryUpload && retries < RetryDelays.Length ? now + RetryDelays[retries++] : null;
            nextProbe = nextUpload is null && disconnected && probe is not null ? now + ReconnectInterval : null;
        }

        async Task<BackgroundPassResult> RunPassAsync(FlushMode mode, CancellationToken token)
        {
            try
            {
                return await flush(mode, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new BackgroundPassResult(ExitCode.Canceled, RetryUpload: mode != FlushMode.InSessionReceive);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                using var log = BackgroundLog.Open(install, "start");
                log.Write($"session flush interrupted: {ex.Message}");
                return new BackgroundPassResult(ExitCode.ServerError, RetryUpload: mode != FlushMode.InSessionReceive);
            }
        }
    }

    private static TimeSpan? Earlier(TimeSpan? left, TimeSpan? right) =>
        left is { } due && (right is null || due < right) ? left : right;

    private static bool IsGameStart(string path)
    {
        try
        {
            return SpoolRecord.Parse(File.ReadAllText(path))?.Event == "game-start";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
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

    public SpoolWakeup(string spool, TimeProvider time, Action<string>? onCommitted = null)
    {
        _time = time;
        Directory.CreateDirectory(spool);
        _watcher = new FileSystemWatcher(spool, "*" + Spool.Extension) { NotifyFilter = NotifyFilters.FileName };
        _watcher.Created += (_, args) => Committed(args.FullPath);
        _watcher.Renamed += (_, args) => Committed(args.FullPath);
        // On overflow, rescan committed records once instead of depending on event order.
        _watcher.Error += (_, _) => _changed.Writer.TryWrite(true);
        _watcher.EnableRaisingEvents = true;

        void Committed(string path)
        {
            onCommitted?.Invoke(path);
            _changed.Writer.TryWrite(true);
        }
    }

    public void Wake() => _changed.Writer.TryWrite(true);

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

        if (delay == Timeout.InfiniteTimeSpan)
        {
            return await _changed.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
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
