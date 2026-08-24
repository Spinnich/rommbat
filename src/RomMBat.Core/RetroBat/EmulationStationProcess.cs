using System.Diagnostics;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Whether EmulationStation is running, and how confidently that was decided.</summary>
/// <param name="IsRunning">True means do not write <c>es_settings.cfg</c>.</param>
/// <param name="Detail">What to tell the user. Null only when nothing is running.</param>
public sealed record EsRunningVerdict(bool IsRunning, string? Detail)
{
    public static EsRunningVerdict NotRunning { get; } = new(false, null);

    public static EsRunningVerdict Running(string detail) => new(true, detail);
}

/// <summary>
/// Finds a running EmulationStation, so nothing writes <c>es_settings.cfg</c> underneath it.
/// </summary>
/// <remarks>
/// <b>This exists because a write made while ES runs is discarded.</b> Driven on a real
/// install: two custom keys were merged in atomically, confirmed on disk, and gone after ES's
/// next write. ES loads the file at startup and serialises that model every time it writes, so
/// a key present at load survives and one that appears afterwards does not. Merging and
/// atomicity do not help; both were done. See <c>docs/retrobat-findings.md</c>, 178 and 179.
/// <para>
/// <b>Matched on the executable's path, not on the process name.</b> Two RetroBat installs can
/// sit on one machine, and ES running out of the other one has no bearing on this install's
/// file. Refusing for it would be a refusal the user cannot act on, since quitting the ES they
/// can see would not change the answer.
/// </para>
/// <para>
/// <b>The ES HTTP API is deliberately not used for this.</b> It answers on loopback and says
/// nothing about which install it belongs to, so it cannot tell those two cases apart, and
/// this is the one question where that distinction is the whole point. The API's other trap
/// does not apply here either way: a 200 is not evidence an action happened, but this only
/// asks whether anything answers at all.
/// </para>
/// <para>
/// <b>Fails closed.</b> A process whose path cannot be read counts as running. Windows refuses
/// <c>MainModule</c> across a bitness boundary and for anything the caller lacks rights to, and
/// the cost of guessing wrong is a conversion the user is told succeeded while the emulator
/// carries on writing to the shared container.
/// </para>
/// </remarks>
public static class EmulationStationProcess
{
    /// <summary>The process name ES runs under, without the extension.</summary>
    public const string ProcessName = "emulationstation";

    /// <summary>Whether an EmulationStation belonging to this install is running.</summary>
    public static EsRunningVerdict Check(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        Process[] candidates;

        try
        {
            candidates = Process.GetProcessesByName(ProcessName);
        }
        catch (InvalidOperationException)
        {
            // The process list itself was unavailable. Nothing was ruled out.
            return EsRunningVerdict.Running(
                "the running processes could not be listed, so it is not safe to say "
                    + "EmulationStation is closed");
        }

        var unreadable = 0;

        try
        {
            foreach (var process in candidates)
            {
                switch (PathOf(process))
                {
                    case null:
                        unreadable++;
                        break;

                    case { } path when install.Contains(path):
                        return EsRunningVerdict.Running(
                            $"EmulationStation is running from this install (process {process.Id}).");
                }
            }
        }
        finally
        {
            foreach (var process in candidates)
            {
                process.Dispose();
            }
        }

        return unreadable == 0
            ? EsRunningVerdict.NotRunning
            : EsRunningVerdict.Running(
                $"{unreadable} EmulationStation process is running and its location could not be "
                    + "read, so it may be this install's.");
    }

    private static string? PathOf(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
