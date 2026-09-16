using RomMBat.Agent.Commands;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// <c>background.log</c> with more than one hook writing it, which is the normal case.
/// </summary>
public sealed class BackgroundLogTests
{
    [Fact]
    public void Two_passes_writing_at_once_keep_every_line_whole()
    {
        // #153. A line lost its first 18 characters on a real install, with three hooks in
        // flight. A handle opened before the other pass wrote still writes where it thinks the
        // end is, and that is on top of the other pass's line.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var quit = BackgroundLog.Open(install, "quit");
        using var start = BackgroundLog.Open(install, "start");

        quit.Write("background quit started");
        start.Write("background start started");
        quit.Write("background quit finished, flush exit 0");
        start.Write("background start finished, flush exit 0");

        var lines = ReadShared(Path.Combine(install.LogDirectoryPath, "background.log"));

        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\dZ  (quit |start)  background ", line));
    }

    [Fact]
    public void Rolling_the_file_does_not_stop_a_pass_that_has_it_open_from_logging()
    {
        // The roll is a rename, and a pass still holding the file must not make it fail: the
        // pass that failed to roll opened no log at all and every line it had was lost.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        var path = Path.Combine(install.LogDirectoryPath, "background.log");

        using var holder = BackgroundLog.Open(install, "quit");
        holder.Write("background quit started");

        // Past the cap while the holder has it open, so the next pass is the one that rolls.
        using (var grow = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            grow.Write(new byte[600 * 1024]);
        }

        using var roller = BackgroundLog.Open(install, "start");
        roller.Write("background start started");

        Assert.Contains(ReadShared(path), line => line.EndsWith("background start started", StringComparison.Ordinal));
    }

    private static string[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
    }
}
