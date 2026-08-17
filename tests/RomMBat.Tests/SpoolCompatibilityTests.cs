using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// An agent older than the hook beside it, which is the second half of issue #31.
/// </summary>
/// <remarks>
/// <c>SpoolRecord.Parse</c> used to require the version marker to match exactly and
/// <c>SpoolDrain</c> deleted anything it could not parse, so an older agent discarded every
/// event a newer hook had written, permanently, and reported only a count of them. The risk is
/// low while the agent installs the hook and stays in step with it, and it stops being low the
/// moment somebody updates one binary by hand.
/// </remarks>
public class SpoolCompatibilityTests
{
    [Fact]
    public void A_record_from_a_newer_hook_survives_a_drain_that_cannot_read_it()
    {
        using var fixture = SpoolTree.Create();

        // What a hook two revisions ahead would write: a marker this build has never seen and a
        // key it does not know, in a format whose grammar has not changed.
        fixture.Write("newer.hook", """
            rommbat-hook-2
            event=game-start
            at=2026-08-17T12:00:00.0000000+00:00
            pid=4242
            arg=D:\RetroBat\roms\snes\ActRaiser (USA).zip
            arg=ActRaiser (USA)
            arg=ActRaiser
            something-this-build-has-never-heard-of=true
            """);

        var outcome = fixture.Drain();

        // Counted honestly, and not ingested, because this build cannot promise it read the
        // record correctly.
        Assert.Equal(0, outcome.Ingested);
        Assert.Equal(0, outcome.Malformed);
        Assert.Equal(1, outcome.Unreadable);

        // And still on disk. Deleting it costs the play session it described; keeping it costs
        // one file until the agent catches up.
        Assert.True(File.Exists(fixture.Path("newer.hook")));
    }

    [Fact]
    public void A_file_that_is_not_this_format_at_all_is_still_discarded()
    {
        // "Ignore what you do not recognise" is only safe inside one grammar. A different
        // family is a different format, and leaving those would grow the spool without bound.
        using var fixture = SpoolTree.Create();

        fixture.Write("rubbish.hook", "not-a-rommbat-record\nevent=game-end\n");
        fixture.Write("empty.hook", string.Empty);

        var outcome = fixture.Drain();

        Assert.Equal(0, outcome.Ingested);
        Assert.Equal(2, outcome.Malformed);
        Assert.Equal(0, outcome.Unreadable);

        Assert.False(File.Exists(fixture.Path("rubbish.hook")));
        Assert.False(File.Exists(fixture.Path("empty.hook")));
    }

    [Fact]
    public void A_newer_record_does_not_stop_the_readable_ones_around_it()
    {
        using var fixture = SpoolTree.Create();

        fixture.Write("a.hook", new SpoolRecord(
            "game-end",
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
            11,
            []).Render());

        fixture.Write("b.hook", "rommbat-hook-9\nevent=game-end\nat=2026-08-17T12:01:00.0000000+00:00\n");

        fixture.Write("c.hook", new SpoolRecord(
            "quit",
            new DateTimeOffset(2026, 8, 17, 12, 2, 0, TimeSpan.Zero),
            13,
            []).Render());

        var outcome = fixture.Drain();

        Assert.Equal(2, outcome.Ingested);
        Assert.Equal(1, outcome.Unreadable);
        Assert.Equal(0, outcome.Malformed);
        Assert.Equal(2, fixture.Store.Journal.All(limit: 100).Count);
    }

    [Fact]
    public void The_marker_splits_into_a_family_and_a_revision()
    {
        Assert.Equal(1, SpoolRecord.RevisionOf("rommbat-hook-1\n"));
        Assert.Equal(2, SpoolRecord.RevisionOf("rommbat-hook-2\nevent=quit\n"));
        Assert.Equal(99, SpoolRecord.RevisionOf("rommbat-hook-99\r\n"));

        // A different family is a different grammar, so none of this applies to it.
        Assert.Null(SpoolRecord.RevisionOf("rommbat-hook\n"));
        Assert.Null(SpoolRecord.RevisionOf("rommbat-hook-\n"));
        Assert.Null(SpoolRecord.RevisionOf("rommbat-hook-two\n"));
        Assert.Null(SpoolRecord.RevisionOf("other-hook-1\n"));
        Assert.Null(SpoolRecord.RevisionOf(null));
        Assert.Null(SpoolRecord.RevisionOf(string.Empty));

        Assert.True(SpoolRecord.IsFromNewerBuild("rommbat-hook-2\n"));
        Assert.False(SpoolRecord.IsFromNewerBuild("rommbat-hook-1\n"));
        Assert.False(SpoolRecord.IsFromNewerBuild("other-hook-9\n"));
    }

    [Fact]
    public void A_newer_revision_is_refused_by_the_parser_rather_than_read_optimistically()
    {
        // The direction that matters. An older revision can be read as the subset it carried,
        // because the format only gains keys. A newer one cannot, because what changed is
        // exactly what this build does not know.
        Assert.Null(SpoolRecord.Parse(
            "rommbat-hook-2\nevent=quit\nat=2026-08-17T12:00:00.0000000+00:00\n"));

        Assert.NotNull(SpoolRecord.Parse(
            "rommbat-hook-1\nevent=quit\nat=2026-08-17T12:00:00.0000000+00:00\n"));
    }

    [Fact]
    public void An_unknown_key_in_a_readable_record_is_ignored_rather_than_fatal()
    {
        var parsed = SpoolRecord.Parse("""
            rommbat-hook-1
            event=game-end
            at=2026-08-17T12:00:00.0000000+00:00
            pid=9
            something-this-build-has-never-heard-of=true
            """);

        Assert.NotNull(parsed);
        Assert.Equal("game-end", parsed.Event);
        Assert.Equal(9, parsed.ProcessId);
    }

    [Fact]
    public void This_builds_own_records_still_round_trip()
    {
        var record = new SpoolRecord(
            "game-start",
            new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
            77,
            [@"D:\RetroBat\roms\msx1\Gradius 2 (Japan, Europe) (En).zip", "Gradius 2 (Japan, Europe) (En)", "Gradius 2"]);

        var parsed = SpoolRecord.Parse(record.Render());

        Assert.NotNull(parsed);
        Assert.Equal(record.Event, parsed.Event);
        Assert.Equal(record.At, parsed.At);
        Assert.Equal(record.ProcessId, parsed.ProcessId);
        Assert.Equal(record.Arguments, parsed.Arguments);
    }

    /// <summary>A tree with a spool directory and a drain over it.</summary>
    private sealed class SpoolTree : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly RetroBatInstall _install;

        private SpoolTree(TempRetroBatTree tree, RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            _install = install;
            Store = store;
        }

        public LocalStore Store { get; }

        public static SpoolTree Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            install.EnsureAppDirectories();

            Directory.CreateDirectory(install.Resolve(SpoolDrain.Directory));

            return new SpoolTree(tree, install, LocalStore.Open(install));
        }

        public string Path(string name) =>
            System.IO.Path.Combine(_install.Resolve(SpoolDrain.Directory), name);

        public void Write(string name, string contents) => File.WriteAllText(Path(name), contents);

        public SpoolDrainOutcome Drain() => new SpoolDrain(_install, Store).Drain();

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
