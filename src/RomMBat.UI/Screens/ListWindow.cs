namespace RomMBat.UI.Screens;

/// <summary>Which slice of a long list is on screen, and what is off it.</summary>
/// <param name="Start">Index of the first visible row.</param>
/// <param name="Count">How many rows are visible.</param>
/// <param name="Above">Rows above the window. Shown as a count, never silently.</param>
/// <param name="Below">Rows below the window.</param>
public readonly record struct ListView(int Start, int Count, int Above, int Below);

/// <summary>
/// Which rows of a list to draw.
/// </summary>
/// <remarks>
/// <b>This exists because a screen that draws every row does not scroll, and a hands-on pass
/// found exactly that.</b> The folder picker offers every system in <c>es_systems.cfg</c>,
/// which is around a hundred rows on a real install. All of them were rendered into one stack,
/// so everything past the height of the display was drawn off it and the cursor moved somewhere
/// invisible. From the couch that is a list that has stopped responding.
/// <para>
/// <b>It is a separate testable type rather than arithmetic inside the renderer</b> for the
/// reason <c>ARCHITECTURE.md</c> gives: if something in the UI project cannot be tested without
/// a window, it is in the wrong project. The suite could not have caught the original bug,
/// because every test asserts on a view model and none of them renders.
/// </para>
/// </remarks>
public static class ListWindow
{
    /// <summary>
    /// How many rows fit, chosen for the smallest display RomMBat is expected on.
    /// </summary>
    /// <remarks>
    /// A row with a detail line is about 74 px at these font sizes and one without is about 46.
    /// Eight of the taller kind fits inside the content area of a 720p handheld with the header
    /// and footer taken off, which is the floor rather than the dev box's 1440p.
    /// </remarks>
    public const int Capacity = 8;

    /// <summary>Picks the window that keeps the cursor visible with context around it.</summary>
    /// <remarks>
    /// <b>The cursor is kept off the very edge where there is room.</b> A selection pinned to
    /// the last visible row gives no sense that anything follows it, which is what makes a
    /// windowed list feel like it has ended when it has not.
    /// </remarks>
    public static ListView Compute(int cursor, int total, int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        if (total <= 0)
        {
            return new ListView(0, 0, 0, 0);
        }

        if (total <= capacity)
        {
            return new ListView(0, total, 0, 0);
        }

        // One row of lookahead at each end, so the row under the cursor is never the last one
        // drawn while more remain.
        var margin = Math.Min(1, (capacity - 1) / 2);
        var start = Math.Clamp(cursor - (capacity / 2), 0, total - capacity);

        if (cursor >= 0)
        {
            start = Math.Min(start, Math.Max(0, cursor - margin));
            start = Math.Max(start, Math.Min(total - capacity, cursor + margin + 1 - capacity));
        }

        start = Math.Clamp(start, 0, total - capacity);

        return new ListView(start, capacity, start, total - start - capacity);
    }
}
