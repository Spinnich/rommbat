using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The <c>es_input.cfg</c> reader, against a live capture holding five real controllers.
/// </summary>
/// <remarks>
/// <b>These tests exist to keep a controller-layout lookup out of this repository.</b> The
/// leads mined from Argosy propose resolving a Nintendo-versus-Xbox face-button layout from a
/// USB vendor id and a table of device-name patterns. The fixture refutes that on the first
/// real device: vendor <c>0x2dc8</c> is 8BitDo, which Argosy's list calls a Nintendo layout,
/// and the 8BitDo Ultimate 2 in this file maps byte-identically to the Xbox 360 pad. The file
/// answers the question the table only guesses at, so the file is what RomMBat reads.
/// </remarks>
public class EsInputMapTests
{
    private const string SwitchProGuid = "030000007e0500000920000000006803";
    private const string EightBitDoGuid = "03000000c82d00000b31000000007200";
    private const string Xbox360Guid = "030000005e0400008e02000000007200";

    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "es_input.cfg");

    private static EsInputMap Map => EsInputMap.Load(Fixture);

    [Fact]
    public void The_capture_holds_a_keyboard_and_five_controllers()
    {
        var map = Map;

        Assert.Equal(6, map.Devices.Count);
        Assert.Equal(5, map.Controllers.Count);
        Assert.NotNull(map.Keyboard);
        Assert.Equal("Keyboard", map.Keyboard!.DeviceName);
    }

    [Fact]
    public void The_face_layout_is_read_from_the_file_and_not_inferred_from_the_vendor_id()
    {
        var map = Map;

        var eightBitDo = map.ForGuid(EightBitDoGuid);
        var xbox = map.ForGuid(Xbox360Guid);
        var switchPro = map.ForGuid(SwitchProGuid);

        Assert.NotNull(eightBitDo);
        Assert.NotNull(xbox);
        Assert.NotNull(switchPro);

        // 8BitDo is vendor 0x2dc8, which Argosy's ControllerDetector calls a Nintendo layout.
        // It is not: it is the Xbox layout, button for button.
        foreach (var face in (string[])["a", "b", "x", "y"])
        {
            Assert.Equal(xbox!.Find(face)!.Id, eightBitDo!.Find(face)!.Id);
        }

        Assert.Equal(0, eightBitDo!.Find("a")!.Id);
        Assert.Equal(1, eightBitDo.Find("b")!.Id);

        // And the Switch Pro really does differ, so the assertion above is not vacuous.
        Assert.Equal(1, switchPro!.Find("a")!.Id);
        Assert.Equal(0, switchPro.Find("b")!.Id);
    }

    [Fact]
    public void The_d_pad_is_a_hat_on_one_pad_and_four_buttons_on_another()
    {
        var map = Map;

        // The difference a vendor-id table cannot express at all, which is the second reason
        // this file is the authority rather than a starting point.
        Assert.Equal(EsInputKind.Hat, map.ForGuid(EightBitDoGuid)!.Find("up")!.Kind);
        Assert.Equal(EsInputKind.Button, map.ForGuid(SwitchProGuid)!.Find("up")!.Kind);
        Assert.Equal(11, map.ForGuid(SwitchProGuid)!.Find("up")!.Id);
    }

    [Fact]
    public void A_running_pads_guid_matches_the_file_only_once_the_name_crc_is_cleared()
    {
        // Left is what SDL 2.32.8 reports at runtime for the 8BitDo, right is what
        // EmulationStation wrote for the same pad. Bytes 2-3 are SDL's CRC-16 of the device
        // name, added in 2.0.18 and left zeroed by ES.
        const string AtRuntime = "0300b155c82d00000b31000000007200";

        Assert.NotEqual(AtRuntime, EightBitDoGuid);
        Assert.Equal(EightBitDoGuid, EsInputMap.NormalizeGuid(AtRuntime));
        Assert.NotNull(Map.ForGuid(AtRuntime));
        Assert.Equal("(8BitDo Ultimate 2 Wireless Controller for PC)", Map.ForGuid(AtRuntime)!.DeviceName);
    }

    [Fact]
    public void The_keyboards_guid_is_left_alone_by_normalisation()
    {
        Assert.Equal(EsInputDevice.KeyboardGuid, EsInputMap.NormalizeGuid(EsInputDevice.KeyboardGuid));
        Assert.Equal("not-a-guid", EsInputMap.NormalizeGuid("not-a-guid"));
    }

    [Fact]
    public void One_button_can_mean_two_things_and_both_are_reported()
    {
        var pad = Map.ForGuid(EightBitDoGuid)!;

        // select and hotkey are the same physical button here, on this pad and on the Xbox
        // one. Returning the first match would drop the hotkey silently.
        Assert.Equal(pad.Find("select")!.Id, pad.Find("hotkey")!.Id);

        var meanings = pad.Meanings(EsInputKind.Button, pad.Find("select")!.Id, 1);

        Assert.Contains("select", meanings);
        Assert.Contains("hotkey", meanings);
    }

    [Fact]
    public void A_hat_pushed_diagonally_satisfies_both_of_its_directions()
    {
        var pad = Map.ForGuid(EightBitDoGuid)!;

        // 1 is up and 2 is right, so 3 is up-and-right and means both.
        var meanings = pad.Meanings(EsInputKind.Hat, 0, 1 | 2);

        Assert.Contains("up", meanings);
        Assert.Contains("right", meanings);
        Assert.DoesNotContain("down", meanings);
        Assert.DoesNotContain("left", meanings);
    }

    [Fact]
    public void An_axis_only_means_its_own_direction()
    {
        var pad = Map.ForGuid(EightBitDoGuid)!;
        var left = pad.Find("joystick1left")!;

        Assert.Equal(EsInputKind.Axis, left.Kind);
        Assert.Equal(-1, left.Value);

        Assert.Contains("joystick1left", pad.Meanings(EsInputKind.Axis, left.Id, -1));
        Assert.DoesNotContain("joystick1left", pad.Meanings(EsInputKind.Axis, left.Id, 1));
    }

    [Fact]
    public void A_trigger_at_rest_reads_fully_negative_and_still_means_nothing()
    {
        var pad = Map.ForGuid(EightBitDoGuid)!;
        var l2 = pad.Find("l2")!;

        // Measured, finding 223: on this pad the sticks rest at 0 but the analog triggers rest
        // at -32768 and stay there after release. So "any non-zero axis reading is an input"
        // reports both triggers permanently held. The binding names a direction, and only a
        // reading of that sign is that input.
        Assert.Equal(EsInputKind.Axis, l2.Kind);
        Assert.Equal(1, l2.Value);

        Assert.Empty(pad.Meanings(EsInputKind.Axis, l2.Id, -1));
        Assert.Contains("l2", pad.Meanings(EsInputKind.Axis, l2.Id, 1));
    }

    [Fact]
    public void A_resting_input_means_nothing()
    {
        var pad = Map.ForGuid(EightBitDoGuid)!;

        Assert.Empty(pad.Meanings(EsInputKind.Button, pad.Find("a")!.Id, 0));
        Assert.Empty(pad.Meanings(EsInputKind.Hat, 0, 0));
        Assert.Empty(pad.Meanings(EsInputKind.Axis, pad.Find("joystick1left")!.Id, 0));
    }

    [Fact]
    public void A_pad_EmulationStation_has_never_been_shown_resolves_to_nothing()
    {
        // The honest state for a pad the user's own front end cannot drive either.
        Assert.Null(Map.ForGuid("03000000ffff0000ffff000000000000"));
        Assert.Null(Map.ForGuid(null));
        Assert.Null(Map.ForGuid("  "));

        // And the keyboard is never returned as a controller match.
        Assert.Null(Map.ForGuid(EsInputDevice.KeyboardGuid));
    }

    [Fact]
    public void The_keyboard_is_bound_by_sdl_keycode()
    {
        var keyboard = Map.Keyboard!;

        Assert.True(keyboard.IsKeyboard);
        Assert.Equal(EsInputKind.Key, keyboard.Find("start")!.Kind);

        // SDLK_RETURN and SDLK_BACKSPACE, which is what ES writes here rather than a scancode.
        Assert.Equal(13, keyboard.Find("start")!.Id);
        Assert.Equal(8, keyboard.Find("select")!.Id);

        // SDLK_DOWN, 1 << 30 | SDL_SCANCODE_DOWN.
        Assert.Equal(1073741905, keyboard.Find("down")!.Id);
    }

    [Fact]
    public void An_install_with_no_configured_input_reads_as_empty_rather_than_failing()
    {
        using var tree = Support.TempRetroBatTree.Create();

        // An ordinary state: EmulationStation writes this file when a pad is first configured.
        var map = EsInputMap.Read(tree.Install());

        Assert.Empty(map.Devices);
        Assert.Null(map.Keyboard);
        Assert.Null(map.ForGuid(EightBitDoGuid));
        Assert.Null(map.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml version=\"1.0\"?>\n<inputList>")]
    [InlineData("not xml at all")]
    public void A_half_written_file_reads_as_empty_with_a_reason_rather_than_throwing(string content)
    {
        using var tree = Support.TempRetroBatTree.Create();

        var path = tree.Install().Resolve(EsInputMap.Location);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        // EmulationStation rewrites this file every time a pad is configured, so an interrupted
        // write is the ordinary way to get one. The reader that follows this call is inside a
        // WinExe with no window open yet, where a throw is an exit with nothing on screen.
        var map = EsInputMap.Read(tree.Install());

        Assert.Empty(map.Devices);
        Assert.NotNull(map.Problem);

        // Naming a path a caller chose is a different contract: it wants to know.
        Assert.Throws<EsInputException>(() => EsInputMap.Load(path));
    }

    [Fact]
    public void An_unreadable_file_is_not_reported_as_a_pad_nobody_has_configured()
    {
        var unreadable = EsInputMap.Read(Broken(out var tree));
        using var _ = tree;

        var connected = new[]
        {
            new GamepadReader.GamepadCandidate(0, "(8BitDo Ultimate 2 Wireless Controller)", EightBitDoGuid),
        };

        var choice = GamepadReader.Choose(connected, unreadable);

        Assert.Equal(GamepadAvailability.NotConfigured, choice.Status.Availability);

        // The two states are indistinguishable on screen without this, and the person reading
        // it has no keyboard to tell them apart with.
        Assert.Contains("es_input.cfg", choice.Status.Detail, StringComparison.Ordinal);
        Assert.Contains("EmulationStation", choice.Status.Detail, StringComparison.Ordinal);
    }

    private static RetroBatInstall Broken(out Support.TempRetroBatTree tree)
    {
        tree = Support.TempRetroBatTree.Create();

        var install = tree.Install();
        var path = install.Resolve(EsInputMap.Location);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);

        return install;
    }
}
