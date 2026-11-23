using System.Diagnostics;
using Microsoft.Data.Sqlite;
using RomM.Client;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>The optional synchronous ES hook waits for this process before letting the game open.</summary>
internal static class PrepareLaunchCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(4));
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        if (command.Value("rom") is not { } rom || !Path.IsPathFullyQualified(rom) || !context.Install.Contains(rom))
        {
            return ExitCode.Usage;
        }

        using var log = BackgroundLog.Open(context.Install, "prelaunch");
        try
        {
            if (!EsPrelaunchHook.IsVerified(context.Install))
            {
                log.Write("local save retained: the EmulationStation executable has no measured synchronous hook");
                return ExitCode.Ok;
            }

            using var connection = context.Authenticate(command, TextWriter.Null, out _);
            if (connection is null || context.Store.Device.Read()?.RomMDeviceId is not { } device)
            {
                log.Write("local save retained: no usable pairing");
                return ExitCode.Ok;
            }

            var detail = await new SaveSync(context.Install, context.Store, connection, device)
                .PrepareLaunchAsync(context.Install.Relativize(rom), deadline.Token)
                .ConfigureAwait(false);
            log.Write($"{detail} ({Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms)");
            return ExitCode.Ok;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or UnauthorizedAccessException
            or SqliteException or RomMUnreachableException or RomMApiException)
        {
            log.Write($"local save retained after {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms: {ex.Message}");
            return ExitCode.Ok;
        }
    }
}
