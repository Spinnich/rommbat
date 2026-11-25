using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

internal readonly record struct BackgroundPassResult(
    int ExitCode, bool RetryUpload = false, bool? ServerReachable = null, bool RetryAcknowledgement = false,
    FlushState? State = null, bool TransientUploadFailure = false, bool GameEndUploadPending = false)
{
    public static BackgroundPassResult FromReport(int exitCode, FlushReport report) =>
        new(exitCode, report.RetryUpload, report.ServerReachable, report.RetryAcknowledgement, report.State,
            report.TransientUploadFailure, report.GameEndUploadPending);
}

/// <summary>Runs one worker per install for as long as EmulationStation is present.</summary>
internal static class BackgroundSession
{
    private static readonly TimeSpan ReceiveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PendingWorkInterval = TimeSpan.FromSeconds(15);
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
        Func<CancellationToken, Task>? waitForEsExit = null,
        Func<bool>? hasPendingGameEnd = null)
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
        var acknowledgementWake = 0;
        using var wakeup = new SpoolWakeup(spool, timeProvider, path =>
        {
            if (Path.GetExtension(path) == SessionSignal.AcknowledgementExtension)
            {
                Interlocked.Exchange(ref acknowledgementWake, 1);
            }
            else if (Volatile.Read(ref watchingStartup) != 0
                && (Path.GetExtension(path) == SessionSignal.LaunchExtension || IsGameStart(path)))
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
        var gameEndPending = false;
        var retryMode = FlushMode.InSessionUpload;
        TimeSpan? nextUpload = null;
        TimeSpan? nextProbe = null;
        Task? libraryWork = null;

        try
        {
            // Subscribe first, then check for a prelaunch already waiting on the install.
            if (SessionSignal.HasLaunchIntent(spool))
            {
                startup.Cancel();
            }

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
                // Read the durable intent after checking the spool. Another flush records it
                // before deleting the hook file. Only use it to start work, never to reset an
                // upload's existing retry schedule on an unrelated filesystem notification.
                if (spoolChanged && (HasGameEnd(spool)
                    || (nextUpload is null && nextProbe is null && hasPendingGameEnd?.Invoke() == true)))
                {
                    // Preserve the retry budget when another notification refers to the same
                    // still-queued work, but let a fresh game end wake a delayed retry.
                    if (nextUpload is null)
                    {
                        retries = 0;
                    }

                    gameEndPending = true;
                    retryMode = FlushMode.InSessionUpload;
                    nextUpload = now;
                    nextProbe = null;
                }

                if (Interlocked.Exchange(ref acknowledgementWake, 0) != 0 && nextUpload is null && nextProbe is null)
                {
                    retryMode = FlushMode.InSessionAcknowledgement;
                    retries = 0;
                    nextUpload = now;
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
                        nextProbe = timeProvider.GetElapsedTime(origin) + PendingWorkInterval;
                    }
                }

                if (nextUpload <= now)
                {
                    var upload = await RunPassAsync(retryMode, cancellationToken).ConfigureAwait(false);
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
                    if (receive.RetryAcknowledgement && nextUpload is null && nextProbe is null)
                    {
                        ScheduleRetry(receive);
                    }

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
            var pending = pass.RetryUpload || pass.RetryAcknowledgement;
            gameEndPending = pass.RetryUpload && (gameEndPending || pass.GameEndUploadPending);
            retryMode = pass.RetryUpload ? FlushMode.InSessionUpload : FlushMode.InSessionAcknowledgement;
            disconnected = pending && (pass.ServerReachable is { } reachable ? !reachable : disconnected);
            nextUpload = pending && retries < RetryDelays.Length ? now + RetryDelays[retries++] : null;
            if (nextUpload is null && gameEndPending
                && (pass.TransientUploadFailure || pass.State is FlushState.Skipped or FlushState.GameRunning))
            {
                // Recheck only this pending game-end. A heartbeat cannot establish that
                // RomM's save endpoints have recovered from a temporary HTTP failure.
                nextUpload = now + PendingWorkInterval;
            }

            nextProbe = nextUpload is null && (disconnected || pass.RetryAcknowledgement) && probe is not null
                ? now + PendingWorkInterval : null;
        }

        async Task<BackgroundPassResult> RunPassAsync(FlushMode mode, CancellationToken token)
        {
            try
            {
                return await flush(mode, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new BackgroundPassResult(ExitCode.Canceled,
                    RetryUpload: mode is FlushMode.InSessionUpload or FlushMode.Background,
                    RetryAcknowledgement: mode == FlushMode.InSessionAcknowledgement);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                using var log = BackgroundLog.Open(install, "start");
                log.Write($"session flush interrupted: {ex.Message}");
                return new BackgroundPassResult(ExitCode.ServerError,
                    RetryUpload: mode is FlushMode.InSessionUpload or FlushMode.Background,
                    RetryAcknowledgement: mode == FlushMode.InSessionAcknowledgement);
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
        _watcher = new FileSystemWatcher(spool) { NotifyFilter = NotifyFilters.FileName };
        _watcher.Filters.Add("*" + Spool.Extension);
        _watcher.Filters.Add("*" + SessionSignal.LaunchExtension);
        _watcher.Filters.Add("*" + SessionSignal.AcknowledgementExtension);
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
