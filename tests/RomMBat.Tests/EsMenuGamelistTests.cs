using System.Text;
using RomMBat.Core.RetroBat;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Merging into <c>system/es_menu/gamelist.xml</c>, which is the one gamelist RetroBat ships
/// pre-populated and the only one no other writer ever rewrites.
/// </summary>
/// <remarks>
/// <b>Driven against the stock file, byte for byte.</b> 96 <c>&lt;path&gt;</c> elements, 93
/// live <c>&lt;game&gt;</c> entries, and three commented out, which is how RetroBat disables an
/// entry whose markup it still ships. A synthesized fixture would carry none of that.
/// <para>
/// <b>The stakes here are higher than under <c>roms/</c>.</b> ES rewrites a rom gamelist
/// whenever it has a reason to, so a field RomMBat mangles is one ES would rewrite anyway.
/// This file it leaves alone: measured across three sessions, the last of which had the change
/// in ES's model and still left the md5 and the mtime untouched. So whatever RomMBat writes
/// here is what the user keeps. See <c>docs/retrobat-findings.md</c>, 205 and 207.
/// </para>
/// </remarks>
public sealed class EsMenuGamelistTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "es_menu-gamelist.xml");

    [Fact]
    public void The_stock_file_is_the_one_gamelist_that_carries_a_BOM_and_CRLF()
    {
        // Pinned because the writer's whole convention-preserving branch exists for it, and a
        // fixture quietly normalised by an editor would make every assertion below vacuous.
        var bytes = File.ReadAllBytes(Fixture);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Reading_the_stock_file_and_writing_it_back_settles_two_stray_indents_and_nothing_else()
    {
        // RetroBat's own file is not consistently indented. 91 of its 93 <game> elements open
        // at one tab; ./play.menu's opens at two and ./phoenix.menu's opens at four spaces.
        // IgnoreWhitespace is what makes this writer deterministic, which is what the no-churn
        // rule needs, so re-rendering settles both onto the file's own majority convention.
        // Four bytes, and that is the whole difference: the BOM, every line ending and every
        // other indent survive, where the writer's own default would have arrived as a diff
        // touching all 96 entries.
        var path = CopyToTemp();

        try
        {
            var before = File.ReadAllBytes(path);
            var wrote = GamelistDocument.Load(path).WriteIfChanged(path);
            var after = File.ReadAllBytes(path);

            Assert.True(wrote);
            Assert.Equal(before.Length - 4, after.Length);

            Assert.Equal<byte[]>([0xEF, 0xBB, 0xBF], after[..3]);

            var text = Encoding.UTF8.GetString(after);
            Assert.DoesNotContain("\n\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\t\t<game>", text, StringComparison.Ordinal);
            Assert.Equal(93, CountLiveEntries(path));

            // Nothing moved at the XML level, which is the level a user would notice.
            Assert.Equal(Entries(Fixture), Entries(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Adding_one_entry_leaves_every_entry_already_there_byte_identical()
    {
        // Against the settled file rather than the stock one, because the stray indent above
        // is a one-off that the first write absorbs. From then on a merge is append-only, and
        // that is the property a second sync must not violate.
        var path = CopyToTemp();

        try
        {
            GamelistDocument.Load(path).WriteIfChanged(path);
            var settled = File.ReadAllBytes(path);

            var document = GamelistDocument.Load(path);
            Assert.True(document.Apply(new GamelistEntry("./rommbat.menu",
            [
                new("name", "RomMBat"),
                new("image", "./media/rommbat-logo.png"),
            ])));
            Assert.True(document.WriteIfChanged(path));

            var after = File.ReadAllBytes(path);

            // The settled bytes are a prefix of the new ones up to the closing root element,
            // so nothing ahead of the appended entry moved: not the BOM, not a line ending,
            // not an indent, and not one of the three commented-out entries.
            var closing = Encoding.UTF8.GetBytes("</gameList>\r\n");
            var prefix = settled[..^closing.Length];

            Assert.Equal(closing, settled[^closing.Length..]);
            Assert.Equal(prefix, after[..prefix.Length]);

            // And the entry is there, in the file's own convention rather than the writer's
            // default.
            var text = Encoding.UTF8.GetString(after);
            Assert.Contains("<path>./rommbat.menu</path>\r\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<path>./rommbat.menu</path>\n", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_second_pass_over_a_settled_file_writes_nothing_at_all()
    {
        // The no-churn rule, on the file the ES menu entry merges into. A sync that changed
        // nothing must leave this byte-identical, or every sync shows up as a modification to
        // a file the user's front end owns.
        var path = CopyToTemp();

        try
        {
            GamelistDocument.Load(path).WriteIfChanged(path);

            var document = GamelistDocument.Load(path);
            document.Apply(new GamelistEntry("./rommbat.menu", [new("name", "RomMBat")]));
            document.WriteIfChanged(path);

            var settled = File.ReadAllBytes(path);

            var again = GamelistDocument.Load(path);
            Assert.False(again.Apply(new GamelistEntry("./rommbat.menu", [new("name", "RomMBat")])));
            Assert.False(again.WriteIfChanged(path));

            Assert.Equal(settled, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_three_entries_RetroBat_commented_out_survive_a_merge()
    {
        // RetroBat disables an entry by commenting its markup out rather than deleting it, and
        // ES never rewrites this file, so RomMBat is the only writer that could drop them.
        // Resurrecting citra_canary would put back an entry RetroBat deliberately withdrew.
        var path = CopyToTemp();

        try
        {
            var document = GamelistDocument.Load(path);
            document.Apply(new GamelistEntry("./rommbat.menu", [new("name", "RomMBat")]));
            document.WriteIfChanged(path);

            var text = File.ReadAllText(path);

            foreach (var withdrawn in new[] { "citra_canary", "yuzu-early-access", "zsnes-dos" })
            {
                Assert.Contains($"<!--<game>", text, StringComparison.Ordinal);
                Assert.Contains($"./{withdrawn}.menu", text, StringComparison.Ordinal);
            }

            // Still commented out, not merely present.
            Assert.Equal(93 + 1, CountLiveEntries(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_gamelist_that_never_existed_is_written_the_way_ES_writes_one()
    {
        // No BOM and LF, which is what all 42 roms gamelists across two installs carry. The
        // convention is preserved from the file that was loaded, never guessed at, so a new
        // file has to land on the measured default rather than on es_menu's.
        var path = Path.Combine(Path.GetTempPath(), $"rommbat-new-{Guid.NewGuid():N}.xml");

        try
        {
            var document = GamelistDocument.Empty();
            document.Apply(new GamelistEntry("./Game.zip", [new("name", "Game")]));
            document.WriteIfChanged(path);

            var bytes = File.ReadAllBytes(path);

            Assert.NotEqual<byte>([0xEF, 0xBB, 0xBF], bytes[..3]);
            Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static int CountLiveEntries(string path) => Entries(path).Count;

    /// <summary>Every entry as its path plus its elements, which is what a user would notice.</summary>
    private static List<(string? Path, string Elements)> Entries(string path) =>
        [.. System.Xml.Linq.XDocument.Load(path).Root!.Elements("game")
            .Select(game => (
                game.Element("path")?.Value,
                string.Join('|', game.Elements().Select(child => $"{child.Name.LocalName}={child.Value}"))))];

    private static string CopyToTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rommbat-es_menu-{Guid.NewGuid():N}.xml");
        File.Copy(Fixture, path);
        return path;
    }
}
