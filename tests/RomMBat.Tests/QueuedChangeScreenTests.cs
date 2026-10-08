using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The queued-config surface, where a queued change can be canceled from the couch.
/// </summary>
/// <remarks>
/// <b>Queueing is not a convenience here, it is the only mechanism.</b> EmulationStation loads
/// <c>es_settings.cfg</c> at startup and serializes its own model over anything written
/// afterwards, and RomMBat is launched from the ES menu, so it runs under a live ES every single
/// time. That is why there is no apply path on any of these screens and why "waiting for you to
/// quit EmulationStation" is a permanent answer rather than a transient one.
/// </remarks>
public class QueuedChangeScreenTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public QueuedChangeScreenTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_empty_queue_says_why_it_is_almost_always_empty()
    {
        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));

        Assert.Empty(list.Rows);
        Assert.NotNull(list.EmptyMessage);

        // Nothing is queued is the normal state, so the message explains what the screen is for
        // rather than reading as something having gone wrong.
        Assert.Contains("EmulationStation", list.EmptyMessage!, StringComparison.Ordinal);

        // And no note, because there is nothing waiting to explain.
        Assert.Null(list.Note!());
    }

    [Fact]
    public void An_outstanding_change_can_be_canceled_and_the_row_goes()
    {
        _session.Store.PendingConfig.Queue(Request());

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var row = Assert.Single(list.Rows);

        Assert.Equal("waiting", row.Value);
        Assert.Contains("Nothing has been written", list.Note!()!, StringComparison.Ordinal);

        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);

        Assert.Same(list, confirm.Underneath);
        Assert.Equal("Keep it queued", confirm.Buttons[confirm.Selected].Label);
        Assert.Equal("Cancel this change?", confirm.Question);

        // The stored reason is a phrase, so it is made a sentence of its own rather than run
        // into the next one (#448).
        Assert.StartsWith(
            "Queued: a per-game memory card for 'Armored Core 3 (USA).iso'. Nothing",
            Assert.Single(confirm.Rows).Detail,
            StringComparison.Ordinal);

        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        // Canceled deletes, because nothing happened and there is nothing to report. Only an
        // applied change keeps its outcome.
        Assert.Empty(_session.Store.PendingConfig.ListOutstanding());
        Assert.Empty(_session.Store.PendingConfig.ListFinished());

        // Answered once: the box says what happened rather than staying a question over its own
        // answer (#448), and its only button is Done, which finishes rather than re-running a
        // change that has already happened.
        Assert.True(confirm.IsAnswered);
        Assert.StartsWith("Change canceled.", confirm.Question, StringComparison.Ordinal);
        Assert.Empty(confirm.Rows);

        var done = Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ListScreen.DoneLabel, done.Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);

        navigator.Handle(NavAction.Back);
        Assert.Empty(Assert.IsType<ListScreen>(navigator.Current).Rows);
    }

    [Fact]
    public void A_change_something_already_got_to_is_shown_and_cannot_be_acted_on()
    {
        var id = _session.Store.PendingConfig.Queue(Request());

        _session.Store.PendingConfig.RecordResult(
            id,
            PendingConfigResult.Refused,
            "The console's shared card is in use by another game.",
            DateTimeOffset.UtcNow);

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var row = Assert.Single(list.Rows);

        Assert.Equal("refused", row.Value);

        // Shown but not choosable. A refusal is the outcome a person most needs to see and the
        // one that produces no other sign: the setting is simply not what they asked for, weeks
        // later, with the console output that explained it long gone.
        Assert.False(row.Available);
        Assert.Contains("shared card", row.Detail!, StringComparison.Ordinal);

        // Nothing to cancel, so nothing offers it.
        Assert.DoesNotContain(list.Hints, hint => hint.Action == NavAction.Accept);
    }

    [Fact]
    public void A_game_that_cannot_be_converted_is_not_offered_the_verb()
    {
        // Nothing is on this device, so SaveConverter refuses for want of a ROM. The screen
        // asks it rather than working the rule out, which is what stops the gate drifting from
        // the refusals the converter actually applies.
        Assert.False(QueuedChangeScreens.CanConvert(_session, 4242));
    }

    [Fact]
    public void A_game_already_queued_is_not_offered_the_verb_again()
    {
        _session.Store.PendingConfig.Queue(Request());

        // Queue() would replace the outstanding row, which is right for a person changing their
        // mind and reads as nothing happening for a person pressing the same thing twice.
        Assert.False(QueuedChangeScreens.CanConvert(_session, 4242));
        Assert.Single(_session.Store.PendingConfig.ListOutstanding());
    }

    [Fact]
    public void The_convert_screen_never_offers_to_write_the_setting_now()
    {
        var screen = Assert.IsType<ConfirmScreen>(
            QueuedChangeScreens.Convert(_session, 4242, "Armored Core 3"));

        var strings = Text(screen).ToList();

        // There is no apply path from this interface and there cannot be one, so nothing here
        // may offer to write it now. The console has --apply because it can be run with ES
        // closed; this always runs under a live one.
        Assert.DoesNotContain(strings, text => text!.Contains("now", StringComparison.OrdinalIgnoreCase)
            && text.Contains("write", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("Nothing is written now", StringComparison.Ordinal));

        Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
    }

    [Fact]
    public void Queueing_a_card_says_it_is_done_in_the_gamepad_own_words()
    {
        // #448: once queued, the title stayed an offer over "Queued", the row's detail was the
        // console's line with the raw es_settings.cfg key, and the warning ended on a flag.
        AddRom(4242, "ps2", "Armored Core 3 (USA).chd");

        var convert = Assert.IsType<ConfirmScreen>(
            QueuedChangeScreens.Convert(_session, 4242, "Armored Core 3"));

        Assert.EndsWith("?", convert.Question, StringComparison.Ordinal);
        Assert.Contains(convert.Rows, row => row.Label == "Worth knowing");
        Assert.All(Text(convert), text =>
        {
            Assert.DoesNotContain("--", text, StringComparison.Ordinal);
            Assert.DoesNotContain("pcsx2_slot1_memory", text, StringComparison.Ordinal);
        });

        var navigator = new Navigator(convert);
        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        Assert.Single(_session.Store.PendingConfig.ListOutstanding());
        Assert.StartsWith("Memory card change queued for 'Armored Core 3'.", convert.Question, StringComparison.Ordinal);
        Assert.Empty(convert.Rows);
        Assert.Equal(ListScreen.DoneLabel, Assert.Single(convert.Hints, hint => hint.Action == NavAction.Accept).Label);

        Assert.All(Text(convert), text =>
        {
            Assert.DoesNotContain("--", text, StringComparison.Ordinal);
            Assert.DoesNotContain("queued:", text, StringComparison.Ordinal);
            Assert.DoesNotContain("pcsx2_slot1_memory", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_queued_screens_name_no_face_button()
    {
        _session.Store.PendingConfig.Queue(Request());

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);
        var convert = Assert.IsType<ConfirmScreen>(
            QueuedChangeScreens.Convert(_session, 4242, "Armored Core 3"));

        foreach (var screen in new IScreen[] { list, confirm, convert })
        {
            var strings = Text(screen)
                .Append((screen as ListScreen)?.EmptyMessage)
                .Append((screen as ListScreen)?.Note?.Invoke())
                .Where(text => text is not null);

            foreach (var text in strings)
            {
                Assert.DoesNotContain("press ", text!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("button", text!, StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
        }
    }

    /// <summary>Every string the screen draws.</summary>
    private static IEnumerable<string> Text(IScreen screen) =>
        ((screen as IWindowedScreen)?.Rows ?? [])
            .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
            .Concat(screen.Hints.Select(hint => hint.Label))
            .Append(screen.Title)
            .Append((screen as ConfirmScreen)?.Question)
            .Concat((screen as ConfirmScreen)?.Buttons.Select(button => button.Label) ?? [])
            .OfType<string>();

    private void AddRom(int romId, string folder, string fileName)
    {
        var path = RelativePath.Create($"roms/{folder}/{fileName}");
        var absolute = _session.Install.Resolve(path);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, "rom bytes");

        _session.Store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = 9,
        });
    }

    private static PendingConfigRequest Request() => new()
    {
        RomId = 4242,
        System = "ps2",
        FsName = "Armored Core 3 (USA).iso",
        SettingKey = "pcsx2_slot1_memory",
        DesiredState = DesiredSettingState.Set,
        DesiredValue = "game",
        // What SaveConverter stores: a phrase, lower-case and with no full stop.
        Reason = "a per-game memory card for 'Armored Core 3 (USA).iso'",
        QueuedAtUtc = DateTimeOffset.UtcNow,
    };
}
