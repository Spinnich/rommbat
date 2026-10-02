using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Which emulator ES would launch a game with: the game's gamelist entry, then the system's
/// <c>es_settings.cfg</c> key, then the first emulator <c>es_systems.cfg</c> lists.
/// </summary>
public sealed class LaunchEmulatorTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose() => _tree.Dispose();

    [Fact]
    public void With_nothing_configured_it_is_the_first_emulator_the_system_lists()
    {
        WriteSystems("mastersystem", "mastersystem", "libretro", "mednafen");

        Assert.Equal("libretro", For("mastersystem", "Game.zip"));
    }

    [Fact]
    public void The_system_key_outranks_the_default_and_is_keyed_by_name_not_folder()
    {
        // gw writes to gameandwatch (RB-68), and es_settings.cfg keys it gw.
        WriteSystems("gw", "gameandwatch", "libretro", "mame");
        WriteSettings("""<string name="gw.emulator" value="mame" /><string name="gameandwatch.emulator" value="ignored" />""");

        Assert.Equal("mame", For("gameandwatch", "Game.zip"));
    }

    [Fact]
    public void The_games_own_gamelist_entry_outranks_the_system_key()
    {
        WriteSystems("gba", "gba", "libretro", "mgba", "mednafen");
        WriteSettings("""<string name="gba.emulator" value="mgba" />""");
        Write("roms/gba/gamelist.xml", """<?xml version="1.0"?><gameList><game><path>./Game.zip</path><emulator>mednafen</emulator></game><game><path>./Other.zip</path></game></gameList>""");

        Assert.Equal("mednafen", For("gba", "Game.zip"));
        Assert.Equal("mgba", For("gba", "Other.zip"));
    }

    [Fact]
    public void With_no_es_files_at_all_it_is_unknown()
    {
        Assert.Null(For("nes", "Game.zip"));
    }

    private string? For(string folder, string fileName) =>
        new LaunchEmulator(_tree.Install()).For(
            folder,
            [new LocalFile
            {
                Path = RelativePath.Create($"roms/{folder}/{fileName}"),
                Folder = folder,
                RomId = 1,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 1,
            }]);

    private void WriteSystems(string name, string folder, params string[] emulators)
    {
        var listed = string.Concat(emulators.Select(emulator => $"<emulator name=\"{emulator}\"/>"));

        Write(
            "emulationstation/.emulationstation/es_systems.cfg",
            $"""<?xml version="1.0"?><systemList><system><name>{name}</name><path>~\..\roms\{folder}</path><emulators>{listed}</emulators></system></systemList>""");
    }

    private void WriteSettings(string body) =>
        Write("emulationstation/.emulationstation/es_settings.cfg", $"""<?xml version="1.0"?><config>{body}</config>""");

    private void Write(string relative, string contents)
    {
        var absolute = Path.Combine(_tree.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, contents);
    }
}
