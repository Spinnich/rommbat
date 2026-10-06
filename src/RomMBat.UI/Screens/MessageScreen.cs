using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// One thing to say, and the way on.
/// </summary>
/// <remarks>
/// <b>Two uses, and they leave differently.</b> Inside the app it is a refusal a screen pushed,
/// and both the bottom and right buttons close it back to that screen. It was built for the
/// first use only, where every button left RomMBat, and was then pushed as an in-app message,
/// so pressing the confirm button on "this game cannot be put on this device" closed the whole
/// app. <see cref="Fatal"/> is the other use: what a refusal looks like when there is no
/// console, for the three states <see cref="Core.InstallSession"/> refuses on (no tree, a
/// RetroBat below the floor, a store written by a newer build), where there is nothing to go
/// back to.
/// </remarks>
public sealed class MessageScreen(string title, string message) : IScreen
{
    private bool _fatal;

    /// <summary>The only screen there is, which every button closes RomMBat from.</summary>
    public static MessageScreen Fatal(string title, string message) => new(title, message) { _fatal = true };

    public string Title { get; } = title;

    public string Message { get; } = message;

    public IReadOnlyList<FooterHint> Hints => _fatal
        ? [new FooterHint(NavAction.Back, "Back to EmulationStation")]
        : [new FooterHint(NavAction.Accept, "OK"), new FooterHint(NavAction.Back, "Back")];

    // Accept leaves too: there is nothing here to accept, and a button that does nothing on the
    // only thing on screen reads as a hang.
    public ScreenCommand Handle(NavAction action) => action switch
    {
        NavAction.Back or NavAction.Accept or NavAction.Start =>
            _fatal ? ScreenCommand.Exit : ScreenCommand.Pop,
        _ => ScreenCommand.Stay,
    };
}
