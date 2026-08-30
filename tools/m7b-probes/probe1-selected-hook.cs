// M7b probe: stamps every ES game-selected / system-selected event.
//
// ES fires these on every navigation move and ships no folder for either, so creating the
// folder is what makes them observable. Used to answer whether EmulationStation keeps
// reading the pad while RomMBat is in front of it: a stamp inside RomMBat's window is ES
// navigating underneath us, and silence is ES having released its input.

using System.Globalization;

var log = Path.Combine(
    Path.GetDirectoryName(Environment.ProcessPath)!,
    "..", "..", "..", "..", "emulators", "rommbat", "logs", "probe1-selected.log");

var line = string.Create(
    CultureInfo.InvariantCulture,
    $"{DateTime.UtcNow:HH:mm:ss.fff}  {Path.GetFileName(Environment.CurrentDirectory)}  args=[{string.Join(" | ", args)}]");

for (var attempt = 0; attempt < 20; attempt++)
{
    try
    {
        // ES spawns these fire-and-forget and concurrently, so several can race one file.
        using var stream = new FileStream(
            Path.GetFullPath(log), FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.WriteLine(line);
        return 0;
    }
    catch (IOException)
    {
        Thread.Sleep(10);
    }
    catch (UnauthorizedAccessException)
    {
        // The other way Windows refuses, which is not an IOException. See PR #95.
        Thread.Sleep(10);
    }
}

return 1;
