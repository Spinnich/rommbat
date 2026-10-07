namespace RomMBat.UI.Shell;

/// <summary>
/// Subscribes to every live screen that is drawn, and calls back when any of them changes.
/// </summary>
/// <remarks>
/// <b>Because a popup draws the screen it came from, so that screen is on display too.</b>
/// The whole chain of <see cref="IPopupScreen.Underneath"/> is followed, so a sync run dimmed
/// behind its own "Stop syncing?" question still shows the moment it finishes.
/// <para>
/// A screen is subscribed once however many times it appears, so one change is one redraw.
/// </para>
/// </remarks>
public sealed class LiveFollow(Action changed)
{
    private readonly List<ILiveScreen> _followed = [];

    /// <summary>Follows <paramref name="top"/> and everything drawn behind it, and stops following the rest.</summary>
    public void Follow(IScreen top)
    {
        var drawn = new List<ILiveScreen>();
        var seen = new HashSet<IScreen>(ReferenceEqualityComparer.Instance);
        for (IScreen? screen = top; screen is not null && seen.Add(screen); screen = (screen as IPopupScreen)?.Underneath)
        {
            if (screen is ILiveScreen live)
            {
                drawn.Add(live);
            }
        }

        foreach (var live in _followed.Where(live => !drawn.Contains(live)).ToList())
        {
            live.Invalidated -= OnInvalidated;
            _followed.Remove(live);
        }

        foreach (var live in drawn.Where(live => !_followed.Contains(live)))
        {
            live.Invalidated += OnInvalidated;
            _followed.Add(live);
        }
    }

    private void OnInvalidated(object? sender, EventArgs e) => changed();
}
