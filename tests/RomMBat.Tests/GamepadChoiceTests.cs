using RomMBat.Core.RetroBat;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Which connected controller gets read, and what a user is told when none of them can be.
/// </summary>
/// <remarks>
/// <b>The half of the reader that has rules in it.</b> Enumerating SDL needs hardware and is
/// covered by a probe; choosing between what it returned does not, and it is the part that was
/// wrong in a way nobody would see until they were holding a pad that did nothing.
/// <para>
/// <b>Every case here is one a person hit.</b> A controller that arrives after RomMBat has
/// started, and a virtual pad from a streaming host sorting ahead of the real one, both came
/// out of a Parsec session against the live install rather than out of a design document.
/// </para>
/// </remarks>
public class GamepadChoiceTests
{
    /// <summary>The 8BitDo and the Xbox 360 rows as the live es_input.cfg spells them.</summary>
    private static EsInputMap Map => EsInputMap.Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "es_input.cfg"));

    private static GamepadReader.GamepadCandidate Candidate(int index, string name, string guid) =>
        new(index, name, guid);

    [Fact]
    public void Nothing_connected_is_a_sentence_rather_than_a_null()
    {
        var choice = GamepadReader.Choose([], Map);

        Assert.Null(choice.Device);
        Assert.Equal(GamepadAvailability.NoDevice, choice.Status.Availability);
        Assert.False(choice.Status.IsReady);
        Assert.NotEmpty(choice.Status.Detail);
    }

    [Fact]
    public void A_configured_pad_is_read_under_the_guid_the_running_library_reports()
    {
        // Bytes 2-3 are SDL's CRC of the device name, filled in at runtime and zeroed in the
        // file, so this is the spelling that never appears in es_input.cfg and must still match.
        var live = Candidate(0, "8BitDo Ultimate 2 Wireless Controller for PC", "0300b155c82d00000b31000000007200");

        var choice = GamepadReader.Choose([live], Map);

        Assert.NotNull(choice.Device);
        Assert.Equal(0, choice.Device!.Index);
        Assert.Equal(GamepadAvailability.Ready, choice.Status.Availability);
        Assert.True(choice.Status.IsReady);
    }

    [Fact]
    public void An_unconfigured_pad_loses_to_a_configured_one_further_down_the_list()
    {
        // Parsec's virtual pad enumerates at index 0 ahead of the real controller. Taking the
        // first device rather than the first configured one leaves the user's own pad unread
        // while RomMBat reports success.
        var choice = GamepadReader.Choose(
            [
                Candidate(0, "Some Virtual Pad", "03000000000000000000000000000000"),
                Candidate(1, "8BitDo Ultimate 2 Wireless Controller for PC", "0300b155c82d00000b31000000007200"),
            ],
            Map);

        Assert.NotNull(choice.Device);
        Assert.Equal(1, choice.Device!.Index);
        Assert.Equal(GamepadAvailability.Ready, choice.Status.Availability);
    }

    [Fact]
    public void A_pad_EmulationStation_has_never_seen_is_named_and_told_where_to_go()
    {
        var choice = GamepadReader.Choose(
            [Candidate(0, "Some Virtual Pad", "03000000000000000000000000000000")],
            Map);

        // Not readable, so nothing is opened, but it is not NoDevice either: saying "no
        // controller is connected" to somebody holding one sends them to look at the cable.
        Assert.Null(choice.Device);
        Assert.Equal(GamepadAvailability.NotConfigured, choice.Status.Availability);
        Assert.Equal("Some Virtual Pad", choice.Status.DeviceName);
        Assert.Contains("Some Virtual Pad", choice.Status.Detail, StringComparison.Ordinal);

        // The fix is in EmulationStation, because that pad cannot drive the user's own front
        // end either and there is nothing RomMBat can do about it.
        Assert.Contains("EmulationStation", choice.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_nameless_pad_still_produces_a_readable_sentence()
    {
        var choice = GamepadReader.Choose(
            [Candidate(0, string.Empty, "03000000000000000000000000000000")],
            Map);

        Assert.Equal(GamepadAvailability.NotConfigured, choice.Status.Availability);
        Assert.StartsWith("That controller", choice.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_keyboard_row_never_claims_a_controller()
    {
        // es_input.cfg carries the keyboard as deviceGUID="-1". No SDL joystick reports that,
        // and a normalisation that ever made one match would hand the reader 17 key bindings
        // to look for on a pad.
        Assert.NotNull(Map.Keyboard);

        var choice = GamepadReader.Choose(
            [Candidate(0, "Some Virtual Pad", "03000000000000000000000000000000")],
            Map);

        Assert.Null(choice.Device);
        Assert.NotEqual(GamepadAvailability.Ready, choice.Status.Availability);
    }
}
