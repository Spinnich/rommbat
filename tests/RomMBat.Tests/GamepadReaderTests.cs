using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// What the controller reader does when there is no controller to read.
/// </summary>
/// <remarks>
/// <b>Every one of these is a state the UI renders, not an exception it catches.</b> The
/// reader is the only part of RomMBat that touches a native library, and the front end it
/// serves is full-screen with no console behind it, so a throw here is a black screen. The
/// happy path needs hardware and is covered by the M7b probe rather than by this suite; what
/// is asserted here is that the unhappy paths all arrive as words.
/// </remarks>
public class GamepadReaderTests
{
    [Fact]
    public void An_install_without_EmulationStations_SDL2_says_so_and_names_the_file()
    {
        using var tree = TempRetroBatTree.Create();
        using var reader = GamepadReader.Open(tree.Install(), EsInputMap.Read(tree.Install()));

        Assert.False(reader.Status.IsReady);
        Assert.Equal(GamepadAvailability.NoLibrary, reader.Status.Availability);
        Assert.Contains("SDL2", reader.Status.Detail, StringComparison.Ordinal);

        // The reason the UI can say something useful rather than "input failed".
        Assert.Contains("EmulationStation", reader.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reader_that_never_opened_a_pad_holds_nothing_rather_than_throwing()
    {
        using var tree = TempRetroBatTree.Create();
        using var reader = GamepadReader.Open(tree.Install(), EsInputMap.Read(tree.Install()));

        // Polled every frame by the front end, so this is the call that must never throw.
        Assert.Empty(reader.Held());
        Assert.Empty(reader.Held());
    }

    [Fact]
    public void Disposing_twice_is_harmless()
    {
        using var tree = TempRetroBatTree.Create();
        var reader = GamepadReader.Open(tree.Install(), EsInputMap.Read(tree.Install()));

        reader.Dispose();
        reader.Dispose();

        Assert.Empty(reader.Held());
    }
}
