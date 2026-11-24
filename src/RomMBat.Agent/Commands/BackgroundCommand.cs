using System.Security.AccessControl;
using System.Text;
using RomM.Client;
using RomMBat.Core.Sets;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>background &lt;event&gt;</c>: the worker or quit pass an EmulationStation hook spawns.
/// </summary>
/// <remarks>
/// <b>This is what closes the loop.</b> The hooks write a spool file and exit, and without this
/// pass only <c>sync</c> or a person typing <c>flush</c> would drain it, so a user who never
/// opens a terminal would accumulate saves and play sessions indefinitely.
/// <para>
/// <b>Named apart from <c>flush</c> for two reasons.</b> It does more than flush, and a pass
/// nobody asked for should be greppable as one: <c>background</c> in a log or a process list
/// means a hook started it, and <c>flush</c> means a person did.
/// </para>
/// <para>
/// <b>Only <c>start</c> and <c>quit</c> reach here, which is CLAUDE.md rule 4.</b> The rule
/// keeps <c>game-start</c> and <c>game-end</c> off the network because they run inside the
/// game-launch path; <c>start</c> fires when EmulationStation starts and <c>quit</c> when it
/// exits, and neither is in that path. The hook still writes its spool file and exits either
/// way, and it is this separate process that opens a socket.
/// </para>
/// <para>
/// <b>Output goes to a file, because there is nobody to print to.</b> The hook spawns this
/// with no window, so anything written to the console is discarded. The question a user
/// asks of it is "why did my save not go up", and answering that needs a record.
/// </para>
/// </remarks>
internal static class BackgroundCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        var hookEvent = command.Positional.Count > 0 ? command.Positional[0] : string.Empty;

        // The hook's own predicate, not a second copy of the set. A literal here would be a
        // way for the two binaries to disagree about which events spawn, which is exactly what
        // putting the list on SpoolRecord was for.
        if (!SpoolRecord.SpawnsBackgroundPass(hookEvent))
        {
            Console.Error.WriteLine(
                $"background: '{hookEvent}' is not an event this runs for. Use start or quit.");
            Console.Error.WriteLine(
                "game-start and game-end are journal-only and spawn nothing; see CLAUDE.md rule 4.");
            return ExitCode.Usage;
        }

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        if (hookEvent == "start")
        {
            var prelaunch = EsPrelaunchHook.Ensure(context.Install);
            using var es = prelaunch is { Action: not EsHookAction.Failed }
                ? EmulationStationProcess.FindForInstall(context.Install)
                : null;
            if (prelaunch is { Action: EsHookAction.Failed })
            {
                using var log = BackgroundLog.Open(context.Install, "start");
                log.Write($"using periodic save receive: prelaunch install failed: {prelaunch.Problem}");
            }

            return await BackgroundSession.RunAsync(
                context.Install,
                () => es is null ? EmulationStationProcess.Check(context.Install).IsRunning : !es.HasExited,
                (mode, token) => RunPassAsync(context, hookEvent, mode, token),
                cancellationToken,
                probe: token => ProbeAsync(context, token),
                syncLibrary: token => SyncLibraryAsync(context, token),
                periodicReceive: es is null,
                watchPrelaunch: invalidate => EsPrelaunchHook.WatchChanges(context.Install, invalidate),
                waitForEsExit: es is null ? null : es.WaitForExitAsync)
                .ConfigureAwait(false);
        }

        var shutdown = await RunPassAsync(context, hookEvent, FlushMode.Shutdown, cancellationToken).ConfigureAwait(false);
        return shutdown.ExitCode;
    }

    private static async Task<bool> ProbeAsync(AgentContext context, CancellationToken cancellationToken)
    {
        if (context.Store.Device.Read()?.ServerOrigin is not { } origin)
        {
            return false;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        using var connection = AgentContext.Connect(origin);
        try
        {
            await connection.ProbeAsync(deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is RomMUnreachableException or RomMApiException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task SyncLibraryAsync(AgentContext context, CancellationToken cancellationToken)
    {
        if (!context.Store.SyncSets.List().Any(set => set.Enabled))
        {
            return;
        }

        using var log = BackgroundLog.Open(context.Install, "start");
        try
        {
            // The session worker can receive game-end while library requests are in flight.
            using var library = AgentContext.Open(CommandLine.Parse(["sync", "--root", context.Install.RootPath]),
                TextWriter.Null, out _);
            using var connection = library?.Authenticate(CommandLine.Parse(["sync"]), TextWriter.Null, out _);
            if (connection is null)
            {
                return;
            }

            var report = await new LibrarySyncService(library!.Session)
                .RunAtStartupAsync(connection, new Immediate<SyncEvent>(update =>
                {
                    SyncCommand.Printer.Show(update);
                    switch (update)
                    {
                        case BiosProblem bios:
                            log.Write($"startup library sync BIOS: {bios.Message}");
                            break;

                        case BiosApplied bios:
                            foreach (var problem in bios.Outcome.Problems)
                            {
                                log.Write($"startup library sync BIOS: {problem}");
                            }
                            break;

                        case MediaApplied media:
                            foreach (var problem in media.Outcome.Problems)
                            {
                                log.Write($"startup library sync media: {problem}");
                            }
                            break;

                        case GamelistsWritten menus:
                            if (menus.Outcome.ReloadRequested)
                            {
                                log.Write($"startup menu reload: {menus.Outcome.Reload}");
                            }

                            foreach (var folder in menus.Outcome.Folders.Where(folder => folder.Problem is not null))
                            {
                                log.Write($"startup library sync {folder.Folder}: {folder.Problem}");
                            }
                            break;

                        case SetSynced set:
                            foreach (var problem in set.Outcome.Problems)
                            {
                                log.Write($"startup library sync {set.Set.Name}: {problem}");
                            }
                            break;
                    }
                }), cancellationToken)
                .ConfigureAwait(false);
            log.Write($"startup library sync {report.State}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or Microsoft.Data.Sqlite.SqliteException or RomMUnreachableException or RomMApiException)
        {
            log.Write($"startup library sync interrupted: {ex.Message}");
        }
    }

    private static async Task<BackgroundPassResult> RunPassAsync(
        AgentContext context, string hookEvent, FlushMode mode, CancellationToken cancellationToken)
    {
        using var log = BackgroundLog.Open(context.Install, hookEvent);

        try
        {
            log.Write($"background {hookEvent} started ({mode})");

            if (hookEvent == "quit")
            {
                try
                {
                    ApplyForcedSettings(context, log);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The guarantee below is unconditional, so this guard is too. A full drive or
                    // an es_settings.cfg something else holds open throws out of the writer, and
                    // neither is a reason to drop the user's saves on the floor.
                    log.Write($"settings could not be written: {ex.Message}");
                }
            }

            FlushReport? result = null;
            var flush = await FlushCommand
                .RunAsync(
                    context,
                    CommandLine.Parse(["flush", "--quiet"]),
                    cancellationToken,
                    mode: mode,
                    onReport: report =>
                    {
                        result = report;
                        log.Write($"flush {report.State}, queued {report.Queued}, conflicts {report.Conflicts.Count}"
                            + $", saves up {report.SavesSent?.Uploaded ?? 0}, down {report.SavesSent?.Downloaded ?? 0}");
                        if (report.SavesSent is { LeftForLaunch: > 0 } saves)
                        {
                            log.Write($"saves left for game launch: {saves.LeftForLaunch}");
                        }
                    })
                .ConfigureAwait(false);

            log.Write($"background {hookEvent} finished, flush exit {flush}");
            return result is null ? new BackgroundPassResult(flush) : BackgroundPassResult.FromReport(flush, result);
        }
        catch (OperationCanceledException)
        {
            log.Write($"background {hookEvent} canceled");
            return new BackgroundPassResult(ExitCode.Canceled);
        }
    }

    /// <summary>
    /// Waits for EmulationStation to be gone, then writes the settings the save shapes force.
    /// </summary>
    /// <remarks>
    /// <b>If ES never exits, the settings wait for the next quit and the flush runs anyway.</b>
    /// The flush writes nothing ES owns, so a live ES has no bearing on it, and holding the
    /// user's saves hostage to a shutdown that stalled would lose the thing this pass exists
    /// for. A setting written under a live ES, by contrast, is discarded without saying so.
    /// </remarks>
    private static void ApplyForcedSettings(AgentContext context, BackgroundLog log)
    {
        var forced = new ForcedSettings(context.Install, context.Store);
        var pending = forced.Pending();

        if (pending.Count == 0)
        {
            // The ordinary case once the setting is in, and it keeps the quit pass from waiting
            // on ES at all.
            return;
        }

        var wait = EmulationStationProcess.WaitForExit(context.Install);

        if (!wait.Gone)
        {
            log.Write($"{pending.Count} setting(s) left for the next quit after waiting "
                + $"{wait.Waited.TotalSeconds:F1}s: {wait.Detail}");
            return;
        }

        var result = forced.Apply();

        foreach (var change in result.Written)
        {
            log.Write($"  set {change.Key} = {change.Value} (was {change.Current ?? "absent"})");
        }

        if (result.Problem is { } problem)
        {
            log.Write($"settings not written: {problem}");
        }
    }
}

/// <summary>
/// Where a pass nobody is watching writes what it did.
/// </summary>
/// <remarks>
/// Deliberately not a logging framework. This is the only caller that runs with no console
/// attached, and what it needs is a few lines a person can read when a save did not arrive.
/// The file is capped and rolled once, because a portable drive is the target and an unbounded
/// log on one is a slow way to fill it.
/// </remarks>
internal sealed class BackgroundLog : IDisposable
{
    /// <summary>Rolled at this size, keeping one previous file.</summary>
    private const long MaxBytes = 512 * 1024;

    private readonly FileStream? _stream;
    private readonly string _event;

    private BackgroundLog(FileStream? stream, string hookEvent)
    {
        _stream = stream;
        _event = hookEvent;
    }

    public static BackgroundLog Open(RomMBat.Core.Paths.RetroBatInstall install, string hookEvent)
    {
        try
        {
            Directory.CreateDirectory(install.LogDirectoryPath);
            var path = Path.Combine(install.LogDirectoryPath, "background.log");

            if (new FileInfo(path) is { Exists: true, Length: > MaxBytes })
            {
                File.Move(path, path + ".1", overwrite: true);
            }

            return new BackgroundLog(OpenForAppend(path), hookEvent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A pulled stick or a read-only volume. Not being able to log is never a reason to
            // skip the work being logged.
            return new BackgroundLog(null, hookEvent);
        }
    }

    public void Write(string line)
    {
        if (_stream is null)
        {
            return;
        }

        try
        {
            // One buffer and one write, newline included. A StreamWriter is free to split a line
            // across writes and emits the newline on its own.
            _stream.Write(Encoding.UTF8.GetBytes($"{DateTimeOffset.UtcNow:u}  {_event,-5}  {line}{Environment.NewLine}"));
        }
        catch (IOException)
        {
            // Same trade as above.
        }
    }

    public void Dispose() => _stream?.Dispose();

    /// <summary>
    /// Opens the log so every write lands at the end of the file as it is at that moment.
    /// </summary>
    /// <remarks>
    /// <b><see cref="FileMode.Append"/> alone does not do that.</b> It seeks to the end once, at
    /// open, and a <see cref="FileStream"/> then writes at the offset it tracks itself. Two
    /// hooks in flight each hold their own offset, so the second pass to write lands on top of
    /// the first pass's line: measured as four writes leaving one line, and seen on a real
    /// install as a line missing its first 18 characters (#153). A handle holding only
    /// <see cref="FileSystemRights.AppendData"/> has Windows append every write instead.
    /// <para>
    /// <see cref="FileShare.Delete"/> is what lets another pass roll the file while this one
    /// has it open. Without it the rename threw, and that pass opened no log at all.
    /// </para>
    /// </remarks>
    private static FileStream OpenForAppend(string path)
    {
        const FileShare share = FileShare.ReadWrite | FileShare.Delete;

        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(path, FileMode.Append, FileAccess.Write, share);
        }

        return new FileInfo(path).Create(
            FileMode.Append,
            FileSystemRights.AppendData,
            share,
            bufferSize: 1,
            FileOptions.None,
            fileSecurity: null);
    }
}
