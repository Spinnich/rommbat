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

    /// <summary>
    /// The height of one ordinary row, and of the gap between two.
    /// </summary>
    /// <remarks>
    /// <b>Here rather than in the renderer, because the capacity above is a claim about them
    /// and a claim wants a test.</b> The renderer draws at these sizes; this file is where the
    /// arithmetic that says how many fit can be asserted.
    /// </remarks>
    public const double RowHeight = 78;

    public const double RowSpacing = 14;

    /// <summary>
    /// A row on a list that is read rather than chosen from, whose detail is a whole sentence.
    /// </summary>
    /// <remarks>
    /// Three wrapped lines of detail under the label, sized from the longest sentence a sync
    /// run produces: the stale-record one naming two byte counts, which wraps to two at this
    /// width, so three leaves headroom.
    /// </remarks>
    public const double ReadingRowHeight = 122;

    /// <summary>
    /// How many reading rows fit, which is fewer because each one is half as tall again.
    /// </summary>
    /// <remarks>
    /// <b>Eight of these overflowed the window and Avalonia put a scroll bar on it</b>, which a
    /// gamepad cannot drive and which is not how this interface scrolls: the window is. Found
    /// from the couch, one round after the taller row was introduced without anyone asking how
    /// many of them fit.
    /// <para>
    /// Chosen so a reading block is never taller than an ordinary one, which is a height the
    /// smallest supported display is already known to hold. <see cref="BlockHeight"/> is the
    /// arithmetic and a test compares the two.
    /// </para>
    /// </remarks>
    public const int ReadingCapacity = 5;

    /// <summary>
    /// How wide the detail column is on a pane of facts, in characters.
    /// </summary>
    /// <remarks>
    /// The detail sits beside a 220px label column inside a 980px block at 16px, which is about
    /// ninety characters a line. Estimated from the string rather than measured, because a view
    /// model has no text engine and <c>ARCHITECTURE.md</c>'s rule is that anything in this
    /// project that cannot be tested without a window is in the wrong project. Being a line out
    /// costs a few pixels of a bounded block; measuring would cost the testability of every
    /// screen.
    /// </remarks>
    public const int FactDetailColumns = 90;

    /// <summary>The most lines of detail a fact row is given before it is clipped.</summary>
    public const int FactDetailMaxLines = 3;

    /// <summary>One wrapped line of detail at 16px.</summary>
    public const double FactDetailLineHeight = 22;

    /// <summary>
    /// How tall one row of a pane of facts is drawn.
    /// </summary>
    /// <remarks>
    /// <b>Natural, not uniform, which is a reversal.</b> A pane used to reserve three wrapped
    /// lines under every row whether or not it had one, so a screen of four short facts drew
    /// them 122px apart and a hands-on pass twice called the result too spread out. The uniform
    /// height was there to stop the block growing and shrinking as it scrolled; that is now the
    /// job of the budget in <see cref="ScrolledByHeight"/>, which bounds the whole block instead
    /// of every row in it. The status pane has worked this way since 7b-1 and is the screen the
    /// same pass held up as showing data correctly.
    /// </remarks>
    public static double FactHeight(string? detail)
    {
        if (string.IsNullOrEmpty(detail))
        {
            return StatusRowHeight;
        }

        var lines = Math.Clamp(
            (detail.Length + FactDetailColumns - 1) / FactDetailColumns,
            1,
            FactDetailMaxLines);

        return StatusRowHeight + (lines * FactDetailLineHeight);
    }

    /// <summary>A section heading on the status pane, with the gap above it.</summary>
    public const double StatusTitleHeight = 36;

    /// <summary>A status line that is a label and a value, and nothing else.</summary>
    public const double StatusRowHeight = 32;

    /// <summary>What a wrapped sentence under one adds to it.</summary>
    public const double StatusDetailHeight = 26;

    public const double StatusLineSpacing = 6;

    /// <summary>
    /// How much room a screen has for its rows.
    /// </summary>
    /// <remarks>
    /// Expressed as the height of a full ordinary list rather than as a number of pixels,
    /// because that block is the one already known to fit the smallest supported display: eight
    /// rows at <see cref="RowHeight"/> is what <see cref="Capacity"/> means, and every other
    /// windowed screen is bounded by the same thing.
    /// </remarks>
    public static double ContentBudget => BlockHeight(Capacity, RowHeight);

    /// <summary>
    /// The window a pane of mixed-height lines shows.
    /// </summary>
    /// <remarks>
    /// <b>A flat capacity has to assume every line is the tallest kind, and on the status pane
    /// most of them are not.</b> Stage 7b-3 first fixed that screen's overflow with a count of
    /// twelve, computed from the tallest line it can draw. A hands-on pass then reported the
    /// obvious consequence: a pane whose lines are mostly a label and a value left half the
    /// display empty and scrolled anyway, so the scrolling felt gratuitous. A title is 36px, a
    /// bare row 32 and a row with a sentence under it 58, and pretending they are all 58 throws
    /// away a third of the screen.
    /// <para>
    /// So the window is measured rather than counted. Same contract as
    /// <see cref="Scrolled"/>: an offset, no cursor, clamped at both ends.
    /// </para>
    /// </remarks>
    public static ListView ScrolledByHeight(int offset, IReadOnlyList<double> heights, double budget)
    {
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);

        if (heights.Count == 0)
        {
            return new ListView(0, 0, 0, 0);
        }

        // The furthest the pane can scroll is the first line whose tail still fills the budget,
        // so the last screenful is full rather than a few lines stranded at the bottom.
        var maxStart = heights.Count - 1;
        var tail = 0.0;

        for (var index = heights.Count - 1; index >= 0; index--)
        {
            var added = tail == 0 ? heights[index] : heights[index] + StatusLineSpacing;

            if (tail + added > budget)
            {
                break;
            }

            tail += added;
            maxStart = index;
        }

        var start = Math.Clamp(offset, 0, maxStart);

        var count = 0;
        var used = 0.0;

        for (var index = start; index < heights.Count; index++)
        {
            var added = count == 0 ? heights[index] : heights[index] + StatusLineSpacing;

            if (used + added > budget)
            {
                break;
            }

            used += added;
            count++;
        }

        // At least one, or a single line taller than the budget draws nothing at all.
        count = Math.Max(1, count);

        return new ListView(start, count, start, Math.Max(0, heights.Count - start - count));
    }

    /// <summary>How tall a drawn window of rows is, rows and the gaps between them.</summary>
    public static double BlockHeight(int rows, double rowHeight, double spacing = RowSpacing) =>
        rows <= 0 ? 0 : (rows * rowHeight) + ((rows - 1) * spacing);

    /// <summary>How many rows fit, given how tall this screen draws them.</summary>
    /// <remarks>
    /// <b>One place, because the count and the height were chosen in two files and disagreed.</b>
    /// The count lives in a view model and the height in the renderer, so a screen could compute
    /// a window of eight and be drawn at the 122px reading height, overflowing by exactly the
    /// margin the reading capacity exists to avoid. That is the defect a hands-on round found on
    /// the problems list, fixed there by changing one screen; browse then reintroduced it, which
    /// is what a rule enforced at an instance rather than at its class does.
    /// <para>
    /// Pair this with <see cref="RowHeightFor"/>: a screen says whether it is reading once, and
    /// both the count and the height follow from that answer.
    /// </para>
    /// </remarks>
    public static int CapacityFor(bool reading) => reading ? ReadingCapacity : Capacity;

    /// <summary>How tall this screen's rows are drawn, paired with <see cref="CapacityFor"/>.</summary>
    public static double RowHeightFor(bool reading) => reading ? ReadingRowHeight : RowHeight;

    /// <summary>
    /// The window a scrolled pane of text shows, from a scroll offset rather than a cursor.
    /// </summary>
    /// <remarks>
    /// <b>A reading list is a pane you scroll, not a list you navigate</b>, so it has an offset
    /// and no cursor at all. <see cref="Compute"/> keeps a cursor off the edge where there is
    /// room, which is right for a list of choices and wrong here: with nothing highlighted, the
    /// first two or three presses would move a cursor nobody can see and leave the view where it
    /// was, so the screen would read as ignoring the pad. Moving the offset means every press
    /// shifts what is on screen.
    /// </remarks>
    public static ListView Scrolled(int offset, int total, int capacity = ReadingCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        if (total <= 0)
        {
            return new ListView(0, 0, 0, 0);
        }

        var count = Math.Min(capacity, total);
        var start = Math.Clamp(offset, 0, total - count);

        return new ListView(start, count, start, total - start - count);
    }

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
