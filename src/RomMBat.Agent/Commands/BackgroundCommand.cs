using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>background &lt;event&gt;</c>: the pass an EmulationStation hook spawns.
/// </summary>
/// <remarks>
/// <b>This is what closes the loop.</b> Until now the hooks wrote a spool file and exited and
/// nothing drained it except <c>sync</c> or a person typing <c>flush</c>, so a user who never
/// opened a terminal accumulated saves and play sessions indefinitely. The
/// <c>FlushCommand</c> doc said so and <c>docs/ARCHITECTURE.md</c> called it M7's call.
/// <para>
/// <b>Named apart from <c>flush</c> for two reasons.</b> It does more than flush, and a pass
/// nobody asked for should be greppable as one: <c>background</c> in a log or a process list
/// means a hook started it, and <c>flush</c> means a person did.
/// </para>
/// <para>
/// <b>Only <c>start</c> and <c>quit</c> reach here, and that is CLAUDE.md rule 4 narrowed
/// rather than bent.</b> The rule forbids the ES hooks touching the network, and gives its
/// reason in the next sentence: they run inside the game-launch path. <c>game-start</c> and
/// <c>game-end</c> do; <c>start</c> fires when EmulationStation starts and <c>quit</c> when it
/// exits, and neither is in that path. The hook still writes its spool file and exits either
/// way, and it is this separate process that opens a socket.
/// </para>
/// <para>
/// <b>Output goes to a file, because there is nobody to print to.</b> The hook spawns this
/// with no window, so anything written to the console is discarded. The first user question
/// after this ships is "why did my save not go up", and answering it needs a record.
/// </para>
/// </remarks>
internal static class BackgroundCommand
{
    /// <summary>The events a hook spawns a pass for. Not the four events the hook serves.</summary>
    public static IReadOnlyList<string> Events { get; } = ["start", "quit"];

    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        var hookEvent = command.Positional.Count > 0 ? command.Positional[0] : string.Empty;

        if (!Events.Contains(hookEvent, StringComparer.Ordinal))
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

        using var log = BackgroundLog.Open(context.Install, hookEvent);

        try
        {
            log.Write($"background {hookEvent} started");

            if (hookEvent == "quit")
            {
                ApplyQueuedConfig(context, log);
            }

            // The flush pass, quiet, in both cases. It never touches a file ES owns, so it is
            // safe whether or not the wait above found ES gone.
            var flush = await FlushCommand
                .RunAsync(context, CommandLine.Parse(["flush", "--quiet"]), cancellationToken)
                .ConfigureAwait(false);

            log.Write($"background {hookEvent} finished, flush exit {flush}");
            return flush;
        }
        catch (OperationCanceledException)
        {
            log.Write($"background {hookEvent} cancelled");
            return ExitCode.Cancelled;
        }
    }

    /// <summary>
    /// Waits for EmulationStation to be gone, then makes the changes queued for exactly this
    /// moment.
    /// </summary>
    /// <remarks>
    /// <b>If ES never exits, the queue stays queued and the flush runs anyway.</b> Those are
    /// two different risks and only one of them is real. The flush writes nothing ES owns, so a
    /// live ES has no bearing on it, and holding the user's saves hostage to a shutdown that
    /// stalled would lose the thing this whole stage exists to deliver. A config change written
    /// under a live ES, by contrast, is discarded without saying so, so that one waits for the
    /// next quit.
    /// </remarks>
    private static void ApplyQueuedConfig(AgentContext context, BackgroundLog log)
    {
        var queued = context.Store.PendingConfig.ListOutstanding();

        if (queued.Count == 0)
        {
            // Nothing to do, so nothing to wait for. The ordinary case, and it keeps the
            // quit pass off the process list entirely on a machine that never queues one.
            return;
        }

        var wait = EmulationStationProcess.WaitForExit(context.Install);

        if (!wait.Gone)
        {
            log.Write($"{queued.Count} queued change(s) left queued after waiting "
                + $"{wait.Waited.TotalSeconds:F1}s: {wait.Detail}");
            return;
        }

        log.Write($"EmulationStation gone after {wait.Waited.TotalMilliseconds:F0} ms, "
            + $"applying {queued.Count} queued change(s)");

        var converter = new SaveConverter(context.Install, context.Store);

        foreach (var change in queued)
        {
            var result = converter.ApplyQueued(change);

            var outcome = result.Status switch
            {
                ConversionStatus.Converted or ConversionStatus.Reverted or ConversionStatus.NoChange =>
                    PendingConfigResult.Applied,
                ConversionStatus.Refused => PendingConfigResult.Refused,
                _ => PendingConfigResult.Failed,
            };

            context.Store.PendingConfig.RecordResult(
                change.Id,
                outcome,
                result.Detail,
                DateTimeOffset.UtcNow);

            log.Write($"  {change.System}/{change.FsName}: {outcome} - {result.Detail}");
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

    private readonly TextWriter? _writer;
    private readonly string _event;

    private BackgroundLog(TextWriter? writer, string hookEvent)
    {
        _writer = writer;
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

            // Shared, because two hooks can be in flight at once and the loser of that race
            // should still start. Each line is one write, which is what keeps them legible.
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            return new BackgroundLog(new StreamWriter(stream) { AutoFlush = true }, hookEvent);
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
        try
        {
            _writer?.WriteLine($"{DateTimeOffset.UtcNow:u}  {_event,-5}  {line}");
        }
        catch (IOException)
        {
            // Same trade as above.
        }
    }

    public void Dispose() => _writer?.Dispose();
}
