using System.Globalization;
using System.Text.RegularExpressions;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The status screen, driven with no window.
/// </summary>
/// <remarks>
/// <b>The queued-configuration rows are the point of this class.</b> Migration <c>012</c>
/// shaped its columns for a reader that did not exist, so until this screen the only thing
/// that could see a queued change was the agent. A user who queued one had no way to learn
/// that it was waiting, or why.
/// </remarks>
public class StatusScreenTests
{
    private static GamepadStatus NoPad =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    private static StatusViewModel Open(TempRetroBatTree tree, out InstallSession session)
    {
        session = InstallSession.Open(tree.Root).Session!;
        return new StatusViewModel(session, NoPad);
    }

    [Fact]
    public void A_fresh_install_says_it_is_not_paired_and_points_at_the_footer_by_its_words()
    {
        using var tree = TempRetroBatTree.Create();
        var model = Open(tree, out var session);
        using var _ = session;

        var romm = model.Sections().Single(section => section.Title == "RomM");
        var paired = romm.Rows.Single(row => row.Label == "Paired");

        Assert.Equal("no", paired.Value);

        // No primary flow may require a mouse, so the way forward has to be on screen, named in
        // words rather than by a letter. This assertion used to require "Press A" and so
        // recorded the wrong rule as correct behaviour; it then named this screen's own Accept
        // hint, which stage 7b-3 moved onto a row of the root menu. The rule is unchanged and
        // the thing it points at is now that row, so the row is what it checks.
        using var pairless = InstallSession.Open(tree.Root).Session!;
        var root = Assert.IsType<ListScreen>(
            RootScreens.Menu(pairless, () => NoPad, new RootScreens.RootRoutes()));

        var route = root.Rows.Single(row => row.Label.StartsWith("Pair", StringComparison.Ordinal));

        Assert.Contains(route.Label, paired.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void No_string_this_screen_shows_names_a_face_button()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        // Both states that offer pairing, because each carries its own sentence.
        AssertNamesNoButton(new StatusViewModel(session, NoPad));

        Pair(session, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        AssertNamesNoButton(new StatusViewModel(session, NoPad));
    }

    /// <summary>
    /// Fails on any user-visible string that tells someone to press a lettered button.
    /// </summary>
    /// <remarks>
    /// <b>A letter is wrong on two layouts out of three.</b> The bottom face button is A on an
    /// Xbox pad, Cross on a DualSense and B on a Switch Pro, and a stock <c>es_input.cfg</c>
    /// routinely has all three configured, so "Press A" reaches a Switch Pro user as the button
    /// that closes RomMBat. <see cref="FooterHint"/> closed that off for the footer by carrying
    /// a <see cref="NavAction"/> and never a string; nothing was stopping a detail line doing it,
    /// and one was. Finding 230.
    /// </remarks>
    private static void AssertNamesNoButton(StatusViewModel model)
    {
        var strings = model.Sections()
            .SelectMany(section => section.Rows)
            .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
            .Concat(model.Hints.Select(hint => hint.Label))
            .Append(model.Title)
            .OfType<string>();

        // "press a", "pressing the b", "the X button". Deliberately narrow: a bare "A" is an
        // article far more often than a button, and a test that cried wolf would be turned off.
        var named = new Regex(
            @"\b(?:press(?:ing|es)?\s+(?:and\s+hold\s+)?(?:the\s+)?[abxy]\b|[ABXY]\s+button\b)",
            RegexOptions.IgnoreCase);

        foreach (var text in strings)
        {
            Assert.False(named.IsMatch(text), $"names a button: \"{text}\"");
        }
    }

    [Fact]
    public void An_empty_install_reports_nothing_waiting_rather_than_blank_rows()
    {
        using var tree = TempRetroBatTree.Create();
        var model = Open(tree, out var session);
        using var _ = session;

        var waiting = model.Sections().Single(section => section.Title == "Waiting");

        Assert.Equal("empty", waiting.Rows.Single(row => row.Label == "Outbox").Value);
        Assert.Equal("none", waiting.Rows.Single(row => row.Label == "Conflicts").Value);
        Assert.Equal("none", waiting.Rows.Single(row => row.Label == "Queued changes").Value);
    }

    [Fact]
    public void A_queued_configuration_change_is_visible_with_its_reason_and_why_it_waits()
    {
        using var tree = TempRetroBatTree.Create();
        var model = Open(tree, out var session);
        using var _ = session;

        session.Store.PendingConfig.Queue(new PendingConfigRequest
        {
            RomId = 191723,
            System = "ps2",
            FsName = "Armored Core 3 (USA).chd",
            SettingKey = "pcsx2_slot1_memory",
            DesiredState = DesiredSettingState.Set,
            DesiredValue = "game",
            Reason = "So this game's memory card is its own rather than shared.",
            QueuedAtUtc = DateTimeOffset.UtcNow,
        });

        var waiting = model.Sections().Single(section => section.Title == "Waiting");
        var summary = waiting.Rows.Single(row => row.Label == "Queued changes");

        Assert.Equal("1 change", summary.Value);

        // The honest answer, and a permanent one rather than a transient one: this UI only ever
        // runs under a live EmulationStation, so the queue can never drain while it is open.
        Assert.Contains("quit EmulationStation", summary.Detail, StringComparison.Ordinal);

        var detail = waiting.Rows.Single(row => row.Value.Contains("Armored Core 3", StringComparison.Ordinal));

        Assert.Contains("ps2", detail.Value, StringComparison.Ordinal);
        Assert.Contains("memory card", detail.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Pairing_is_reachable_from_a_paired_install_too()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var opened = 0;
        var root = Assert.IsType<ListScreen>(RootScreens.Menu(
            session,
            () => NoPad,
            new RootScreens.RootRoutes
            {
                // Not a MessageScreen: that one answers Back by leaving RomMBat, which is right
                // for a refusal with nothing under it and wrong as a stand-in for pairing, which
                // pops back to the menu.
                StartPairing = () => { opened++; return Stub(); },
            }));

        // The verb moved from Accept on the status screen to a row of its own in stage 7b-3.
        // What it promises has not moved, and this is the assertion that says so.
        var navigator = new Navigator(root);

        Assert.True(new StatusViewModel(session, NoPad).NeedsPairing);
        Assert.Equal("not paired", root.Rows.Single(row => row.Label == "Pair with RomM").Value);

        RootMenuDriver.Open(navigator, "Pair with RomM");
        Assert.Equal(1, opened);
        Assert.Equal(2, navigator.Depth);

        // Paired while the pairing screen is open, which is when it really happens, and backed
        // out of. The menu re-reads on the way back, so the row it draws is the one a person
        // returning from a successful pairing actually sees.
        Pair(session, expiresAt: DateTimeOffset.UtcNow.AddDays(90));
        navigator.Handle(NavAction.Back);

        Assert.False(new StatusViewModel(session, NoPad).NeedsPairing);

        // And the case that was missing: once paired, accept used to do nothing at all, so there
        // was no way to move to another server or to recover a token the server had stopped
        // accepting. M1 makes re-pairing cheap on purpose; a screen that hides it strands you.
        Assert.Equal("paired", root.Rows.Single(row => row.Label == "Pair again").Value);

        RootMenuDriver.Open(navigator, "Pair again");
        Assert.Equal(2, opened);
        Assert.Equal(2, navigator.Depth);
    }

    /// <summary>A screen that stands where pairing goes and answers Back the way it does.</summary>
    private static ListScreen Stub() =>
        new("Pairing", [new ListRow("stub")], _ => ScreenCommand.Stay);

    [Fact]
    public void An_expired_token_says_so_and_says_what_is_kept()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        Pair(session, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var status = new StatusViewModel(session, NoPad);

        Assert.True(status.TokenExpired);

        var row = status.Sections()
            .Single(section => section.Title == "RomM")
            .Rows.Single(r => r.Label == "Token");

        Assert.Equal("expired", row.Value);

        // A scoped, expiring token is the recommended default on a portable drive, so this is
        // ordinary rather than a fault, and the worry it has to answer is whether re-pairing
        // costs the user their saves. It does not.
        Assert.Contains("kept", row.Detail, StringComparison.Ordinal);
    }

    /// <summary>Writes a pairing straight into the store, as the M1 suite does.</summary>
    private static void Pair(InstallSession session, DateTimeOffset expiresAt)
    {
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(session.Install));
        session.Store.Device.SavePairing(
            new PairingResult(
                new Uri("https://romm.invalid"),
                "device-9",
                "Handheld",
                new GrantedScopes(RomMScopes.Requested),
                TokenProtector.Protect("rmm_token", null, expiresAt)),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void An_unreadable_controller_is_reported_as_a_state_with_words()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var model = new StatusViewModel(session, new GamepadStatus(
            GamepadAvailability.NotConfigured,
            "Some Pad",
            "03000000ffff0000ffff000000000000",
            "Configure it in EmulationStation first."));

        var controller = model.Sections().Single(section => section.Title == "Controller");

        // Short labels, like every other section. The device name goes in the value column,
        // because the label column is fixed width so the values line up and a real pad name is
        // 45 characters: putting it there truncated it to "(8BitDo Ultimate 2 Wireles".
        var device = controller.Rows.Single(row => row.Label == "Device");
        Assert.Equal("Some Pad", device.Value);

        var state = controller.Rows.Single(row => row.Label == "State");

        // English rather than the enum's own identifier, which reached the screen as
        // "NotConfigured" because only the Ready case was ever looked at.
        Assert.Equal("Not configured", state.Value);
        Assert.DoesNotContain(" ", GamepadAvailability.NotConfigured.ToString(), StringComparison.Ordinal);
        Assert.Contains("EmulationStation", state.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_section_renders_on_an_install_with_nothing_in_it()
    {
        using var tree = TempRetroBatTree.Create();
        var model = Open(tree, out var session);
        using var _ = session;

        // The screen a first-run user sees. Anything that throws here is a black screen.
        var sections = model.Sections();

        Assert.Equal(
            ["This device", "RomM", "Waiting", "Controller"],
            sections.Select(section => section.Title));

        Assert.All(sections, section => Assert.NotEmpty(section.Rows));
    }

    [Fact]
    public void Last_contact_is_shown_on_the_users_own_clock_rather_than_in_UTC()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var contact = new DateTimeOffset(2026, 8, 30, 13, 22, 15, TimeSpan.Zero);

        // The row only exists once paired, which is the only state it means anything in.
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(session.Install));
        session.Store.Device.SavePairing(
            new PairingResult(
                new Uri("https://romm.invalid"),
                "device-1",
                "Handheld",
                new GrantedScopes(RomMScopes.Requested),
                TokenProtector.Protect("rmm_token", "phrase", contact.AddDays(90))),
            contact);

        session.Store.Clock.RecordContact(contact, contact, TimeSpan.Zero);

        var model = new StatusViewModel(session, NoPad);
        var row = model.Sections()
            .SelectMany(section => section.Rows)
            .Single(candidate => candidate.Label == "Last contact");

        // Stored and compared in UTC, which is what makes the outbox survive a timezone change.
        // Rendered on the wall clock in front of the user, because "13:22:15Z" at twenty past
        // nine in the morning reads as a broken program rather than as a considered choice.
        Assert.Equal(
            contact.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            row.Value);

        // Passes in every timezone, including CI's UTC, where local and UTC agree on the digits
        // and disagree only on this.
        Assert.DoesNotContain("Z", row.Value, StringComparison.Ordinal);
    }
}
