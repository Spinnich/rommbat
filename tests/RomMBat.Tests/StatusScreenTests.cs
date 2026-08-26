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
    public void A_fresh_install_says_it_is_not_paired_and_names_the_button_that_fixes_it()
    {
        using var tree = TempRetroBatTree.Create();
        var model = Open(tree, out var session);
        using var _ = session;

        var romm = model.Sections().Single(section => section.Title == "RomM");
        var paired = romm.Rows.Single(row => row.Label == "Paired");

        Assert.Equal("no", paired.Value);

        // No primary flow may require a mouse, so the way forward is named as a button.
        Assert.Contains("Press A", paired.Detail, StringComparison.Ordinal);
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
        var status = new StatusViewModel(session, NoPad) { StartPairing = () => { opened++; return new MessageScreen("x", "y"); } };

        // Unpaired, which is the obvious case.
        Assert.True(status.NeedsPairing);
        Assert.Equal(ScreenCommandKind.Push, status.Handle(NavAction.Accept).Kind);
        Assert.Equal(1, opened);
        Assert.Equal("Pair with RomM", status.Hints.Single(h => h.Button == "A").Label);

        Pair(session, expiresAt: DateTimeOffset.UtcNow.AddDays(90));
        Assert.False(status.NeedsPairing);

        // And the case that was missing: once paired, A used to do nothing at all, so there was
        // no way to move to another server or to recover a token the server had stopped
        // accepting. M1 makes re-pairing cheap on purpose; a screen that hides it strands you.
        Assert.Equal(ScreenCommandKind.Push, status.Handle(NavAction.Accept).Kind);
        Assert.Equal(2, opened);
        Assert.Equal("Pair again", status.Hints.Single(h => h.Button == "A").Label);
    }

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
        var row = controller.Rows.Single();

        Assert.Equal("Some Pad", row.Label);
        Assert.Contains("EmulationStation", row.Detail, StringComparison.Ordinal);
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
}
