using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>Runs one upload worker per install for as long as EmulationStation is present.</summary>
internal static class BackgroundSession
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    public static async Task<int> RunAsync(
        RetroBatInstall install,
        Func<bool> isRunning,
        Func<bool, CancellationToken, Task<int>> flush,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? wait = null,
        TimeProvider? timeProvider = null)
    {
        using var lease = TryOwnSession(install);
        if (lease is null)
        {
            return ExitCode.Ok;
        }

        timeProvider ??= TimeProvider.System;
        wait ??= token => Task.Delay(PollInterval, timeProvider, token);

        try
        {
            // Preserve the full startup pass, including saves offered by another install.
            var exitCode = await RunPassAsync(sessionUpload: false).ConfigureAwait(false);
            var previous = NewestEvent(install);
            string? attempted = null;
            var lastAttempt = timeProvider.GetTimestamp();

            while (isRunning())
            {
                await wait(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!isRunning())
                {
                    return exitCode;
                }

                // Let a new hook batch settle for one poll. The guard inside the flush reads the
                // spool again under TreeLock, and retries also cover files that are still busy.
                var newest = NewestEvent(install);
                var eventArrived = newest is not null && newest != attempted;
                if (newest == previous && (eventArrived || timeProvider.GetElapsedTime(lastAttempt) >= RetryInterval))
                {
                    exitCode = await RunPassAsync(sessionUpload: true).ConfigureAwait(false);
                    lastAttempt = timeProvider.GetTimestamp();
                    attempted = newest;
                }

                previous = newest;
            }

            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExitCode.Canceled;
        }

        async Task<int> RunPassAsync(bool sessionUpload)
        {
            try
            {
                return await flush(sessionUpload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                using var log = BackgroundLog.Open(install, "start");
                log.Write($"session flush interrupted: {ex.Message}");
                return ExitCode.ServerError;
            }
        }
    }

    private static string? NewestEvent(RetroBatInstall install)
    {
        try
        {
            return Directory.EnumerateFiles(install.Resolve(SpoolDrain.Directory), "*" + SpoolDrain.Extension)
                .Max(StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileStream? TryOwnSession(RetroBatInstall install)
    {
        try
        {
            // This is separate from TreeLock so UI and CLI flushes can run between worker passes.
            // The OS releases the handle when the process dies, even if the file stays behind.
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
