using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// One thing to say, and the way out.
/// </summary>
/// <remarks>
/// <b>What a refusal looks like when there is no console.</b> The agent prints why it will not
/// run and returns an exit code; a full-screen window opened from the EmulationStation menu has
/// neither, so the same words have to be on screen with a button that closes them. Used for the
/// three states <see cref="Core.InstallSession"/> refuses on: no tree, a RetroBat below the
/// floor, and a store written by a newer build.
/// </remarks>
public sealed class MessageScreen(string title, string message) : IScreen
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public IReadOnlyList<FooterHint> Hints =>
    [
        new FooterHint("B", "Back to EmulationStation", 2),
    ];

    // Every action leaves, including accept: there is nothing here to accept, and a button that
    // does nothing on the only screen a user can reach reads as a hang.
    public ScreenCommand Handle(NavAction action) => action switch
    {
        NavAction.Back or NavAction.Accept or NavAction.Start => ScreenCommand.Exit,
        _ => ScreenCommand.Stay,
    };
}
