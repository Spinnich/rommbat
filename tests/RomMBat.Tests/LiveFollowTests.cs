using RomMBat.UI.Input;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Which screens the shell redraws for: the one on top, and every screen a popup draws dimmed
/// behind it (#496).
/// </summary>
public sealed class LiveFollowTests
{
    [Fact]
    public void A_screen_behind_a_popup_still_redraws_the_stack()
    {
        var run = new Live();
        var question = new Popup(run);
        var redraws = 0;
        var follow = new LiveFollow(() => redraws++);

        follow.Follow(question);
        run.Raise();

        Assert.Equal(1, redraws);
    }

    [Fact]
    public void Every_layer_of_a_stacked_popup_is_followed()
    {
        var bottom = new Live();
        var menu = new Popup(bottom);
        var top = new LivePopup(menu);
        var redraws = 0;
        var follow = new LiveFollow(() => redraws++);

        follow.Follow(top);
        bottom.Raise();
        top.Raise();

        Assert.Equal(2, redraws);
    }

    [Fact]
    public void A_screen_no_longer_drawn_stops_redrawing()
    {
        var run = new Live();
        var question = new Popup(run);
        var elsewhere = new Live();
        var redraws = 0;
        var follow = new LiveFollow(() => redraws++);

        follow.Follow(question);
        follow.Follow(elsewhere);
        run.Raise();

        Assert.Equal(0, redraws);
    }

    [Fact]
    public void A_screen_both_on_top_and_underneath_redraws_once_per_change()
    {
        var run = new Live();
        var redraws = 0;
        var follow = new LiveFollow(() => redraws++);

        follow.Follow(new LivePopup(run));
        follow.Follow(new LivePopup(run));
        run.Raise();

        Assert.Equal(1, redraws);
    }

    private class Screen : IScreen
    {
        public string Title => "";

        public IReadOnlyList<FooterHint> Hints => [];

        public ScreenCommand Handle(NavAction action) => ScreenCommand.Stay;
    }

    private sealed class Live : Screen, ILiveScreen
    {
        public event EventHandler? Invalidated;

        public void Raise() => Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Popup(IScreen underneath) : Screen, IPopupScreen
    {
        public IScreen? Underneath => underneath;
    }

    private sealed class LivePopup(IScreen underneath) : Screen, IPopupScreen, ILiveScreen
    {
        public IScreen? Underneath => underneath;

        public event EventHandler? Invalidated;

        public void Raise() => Invalidated?.Invoke(this, EventArgs.Empty);
    }
}
