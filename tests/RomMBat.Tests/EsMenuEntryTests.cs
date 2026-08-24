using System.Text;
using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Putting RomMBat in the EmulationStation menu, and taking it back out.
/// </summary>
/// <remarks>
/// Driven against the stock <c>system/es_menu/gamelist.xml</c> rather than an empty one,
/// because the question these tests exist to answer is what happens to the 93 entries
/// RetroBat put there and the three it commented out.
/// </remarks>
public sealed class EsMenuEntryTests : IDisposable
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "es_menu-gamelist.xml");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose()
    {
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Installing_writes_the_two_files_registration_needs_plus_its_artwork()
    {
        // Two files is the registration and the third is what the entry points at. A .menu with
        // no gamelist element shows as a bare filename, measured at 209 ms in finding 203, and
        // an entry with no <image> shows as one too.
        var entry = new EsMenuEntry(_tree.Install());

        var outcome = entry.Install();

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(3, outcome.Installed);
        Assert.True(entry.IsInstalled());

        // The executable line is the one measured fact this file carries: paths resolve under
        // emulators\ and ..\ escapes are refused outright, so anything else is not launchable.
        Assert.Equal(@"\rommbat\RomMBat.exe", File.ReadAllText(At(EsMenuEntry.MenuPath)));

        Assert.True(new FileInfo(At(EsMenuEntry.LogoPath)).Length > 0);
        Assert.Equal<byte[]>([0x89, (byte)'P', (byte)'N', (byte)'G'], File.ReadAllBytes(At(EsMenuEntry.LogoPath))[..4]);

        var document = GamelistDocument.Load(At(EsMenuEntry.GamelistPath));
        Assert.True(document.Contains(EsMenuEntry.EntryPath));
        Assert.Contains("image", document.ElementNamesOf(EsMenuEntry.EntryPath));
    }

    [Fact]
    public void A_second_install_reports_current_and_rewrites_nothing()
    {
        // The no-churn rule. sync calls this on every run, so a second pass that rewrote the
        // gamelist would show up as a modification to the user's front end every single time.
        var entry = new EsMenuEntry(_tree.Install());
        entry.Install();

        var before = File.ReadAllBytes(At(EsMenuEntry.GamelistPath));
        var menuBefore = File.GetLastWriteTimeUtc(At(EsMenuEntry.MenuPath));

        var again = entry.Install();

        Assert.True(again.IsNoOp);
        Assert.All(again.Steps, step => Assert.Equal(EsMenuAction.AlreadyCurrent, step.Action));
        Assert.Equal(before, File.ReadAllBytes(At(EsMenuEntry.GamelistPath)));
        Assert.Equal(menuBefore, File.GetLastWriteTimeUtc(At(EsMenuEntry.MenuPath)));
    }

    [Fact]
    public void Installing_into_a_gamelist_that_already_holds_entries_leaves_every_one_of_them_alone()
    {
        // The stock file, byte for byte: 93 live entries and three RetroBat commented out.
        UseStockGamelist();
        var before = File.ReadAllBytes(At(EsMenuEntry.GamelistPath));

        new EsMenuEntry(_tree.Install()).Install();

        var after = File.ReadAllText(At(EsMenuEntry.GamelistPath));
        var document = GamelistDocument.Load(At(EsMenuEntry.GamelistPath));

        Assert.Equal(93 + 1, document.Count);
        Assert.Equal(93, document.CountExcept([EsMenuEntry.EntryPath]));

        // Every entry that was there, with the elements it had and the values it had.
        foreach (var (path, elements) in Entries(before))
        {
            Assert.Equal(elements, Entries(File.ReadAllBytes(At(EsMenuEntry.GamelistPath)))[path]);
        }

        // And the three withdrawn ones are still withdrawn rather than resurrected.
        foreach (var withdrawn in new[] { "citra_canary", "yuzu-early-access", "zsnes-dos" })
        {
            Assert.Contains($"./{withdrawn}.menu", after, StringComparison.Ordinal);
        }

        Assert.Contains("<!--<game>", after, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_the_user_changed_is_left_exactly_as_they_left_it()
    {
        // The same rule class D conversion follows for a setting somebody else wrote. The name
        // and the artwork are what the user sees on their own front end, so re-asserting them
        // on every sync would take over a choice they made on purpose.
        var install = _tree.Install();
        new EsMenuEntry(install).Install();

        var path = At(EsMenuEntry.GamelistPath);
        var document = GamelistDocument.Load(path);
        document.Apply(new GamelistEntry(EsMenuEntry.EntryPath,
        [
            new("name", "Game Sync"),
            new("image", "./media/my-own-art.png"),
        ]));
        document.WriteIfChanged(path);

        var outcome = new EsMenuEntry(install).Install();

        Assert.Equal(0, outcome.Failed);

        var after = GamelistDocument.Load(path);
        Assert.True(after.Contains(EsMenuEntry.EntryPath));

        var text = File.ReadAllText(path);
        Assert.Contains("<name>Game Sync</name>", text, StringComparison.Ordinal);
        Assert.Contains("<image>./media/my-own-art.png</image>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<name>RomMBat</name>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_field_is_filled_in_without_touching_the_ones_that_are_there()
    {
        // The other half of the same rule: an entry a user hand-wrote with only a name still
        // gets artwork, because nothing is being taken over by adding what is absent.
        var install = _tree.Install();
        var path = At(EsMenuEntry.GamelistPath);

        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry(EsMenuEntry.EntryPath, [new("name", "My RomMBat")]));
        document.WriteIfChanged(path);

        new EsMenuEntry(install).Install();

        var text = File.ReadAllText(path);
        Assert.Contains("<name>My RomMBat</name>", text, StringComparison.Ordinal);
        Assert.Contains("<image>./media/rommbat-logo.png</image>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstalling_removes_RomMBats_own_files_and_its_own_entry_and_nothing_else()
    {
        UseStockGamelist();
        var install = _tree.Install();
        new EsMenuEntry(install).Install();

        // A file beside RomMBat's that is nobody's business but RetroBat's.
        var neighbour = Path.Combine(_tree.Root, "system", "es_menu", "media", "altirra-logo.png");
        File.WriteAllBytes(neighbour, [1, 2, 3]);

        var outcome = new EsMenuEntry(install).Uninstall();

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(3, outcome.Removed);

        Assert.False(File.Exists(At(EsMenuEntry.MenuPath)));
        Assert.False(File.Exists(At(EsMenuEntry.LogoPath)));
        Assert.False(GamelistDocument.Load(At(EsMenuEntry.GamelistPath)).Contains(EsMenuEntry.EntryPath));

        // The folders and the neighbours survive, which is the whole point: es_menu/ and
        // media/ are RetroBat's, and 93 entries live in that gamelist.
        Assert.True(File.Exists(neighbour));
        Assert.True(Directory.Exists(Path.Combine(_tree.Root, "system", "es_menu", "media")));
        Assert.True(File.Exists(At(EsMenuEntry.GamelistPath)));
        Assert.Equal(93, GamelistDocument.Load(At(EsMenuEntry.GamelistPath)).Count);
    }

    [Fact]
    public void Uninstalling_when_nothing_is_installed_reports_absent_rather_than_failing()
    {
        UseStockGamelist();
        var before = File.ReadAllBytes(At(EsMenuEntry.GamelistPath));

        var outcome = new EsMenuEntry(_tree.Install()).Uninstall();

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(0, outcome.Removed);
        Assert.All(outcome.Steps, step => Assert.Equal(EsMenuAction.NotPresent, step.Action));
        Assert.Equal(before, File.ReadAllBytes(At(EsMenuEntry.GamelistPath)));
    }

    [Fact]
    public void Half_a_registration_does_not_count_as_installed()
    {
        // Either file alone is a broken entry: a .menu with no element shows as a bare
        // filename, and an element whose .menu is gone is not listed by ES at all.
        var install = _tree.Install();
        var entry = new EsMenuEntry(install);
        entry.Install();

        File.Delete(At(EsMenuEntry.MenuPath));
        Assert.False(entry.IsInstalled());

        entry.Install();
        Assert.True(entry.IsInstalled());

        var document = GamelistDocument.Load(At(EsMenuEntry.GamelistPath));
        document.Remove(EsMenuEntry.EntryPath);
        document.WriteIfChanged(At(EsMenuEntry.GamelistPath));

        Assert.False(entry.IsInstalled());
    }

    [Fact]
    public void A_gamelist_that_cannot_be_parsed_is_reported_and_left_exactly_as_it_is()
    {
        // It holds 93 entries and whatever the user added. Rewriting a file that could not be
        // read would destroy all of it, so the step fails and says so.
        var path = At(EsMenuEntry.GamelistPath);
        File.WriteAllText(path, "<gameList><game><path>./x.menu</path>");
        var before = File.ReadAllBytes(path);

        var outcome = new EsMenuEntry(_tree.Install()).Install();

        Assert.Equal(1, outcome.Failed);
        Assert.Equal(before, File.ReadAllBytes(path));

        // And the half that could be written was, so the report names both states honestly.
        Assert.True(File.Exists(At(EsMenuEntry.MenuPath)));
    }

    private string At(RomMBat.Core.Paths.RelativePath path) => _tree.Install().Resolve(path);

    private void UseStockGamelist() => File.Copy(Fixture, At(EsMenuEntry.GamelistPath), overwrite: true);

    private static Dictionary<string, string> Entries(byte[] bytes)
    {
        var document = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('﻿'));

        return document.Root!.Elements("game")
            .Where(game => game.Element("path")?.Value is not null)
            .ToDictionary(
                game => game.Element("path")!.Value,
                game => string.Join('|', game.Elements().Select(child => $"{child.Name.LocalName}={child.Value}")));
    }
}
