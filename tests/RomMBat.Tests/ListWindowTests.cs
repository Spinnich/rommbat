using RomMBat.UI.Screens;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Which rows of a long list are drawn.
/// </summary>
/// <remarks>
/// <b>This exists because the suite could not see the bug it is about.</b> The folder picker
/// offers every system in <c>es_systems.cfg</c>, around a hundred rows on a real install, and
/// the renderer drew all of them into one stack with no window. Everything past the height of
/// the display went off it and the cursor moved somewhere invisible, which from the couch is a
/// list that has stopped responding.
/// <para>
/// Every screen test asserts on a view model and none of them renders, so nothing in the suite
/// could have caught it. Pulling the arithmetic out of the renderer is what makes it reachable
/// at all, which is <c>ARCHITECTURE.md</c>'s rule: if something in the UI project cannot be
/// tested without a window, it is in the wrong project.
/// </para>
/// </remarks>
public class ListWindowTests
{
    [Fact]
    public void A_list_that_fits_is_drawn_whole_with_nothing_marked_off_screen()
    {
        var view = ListWindow.Compute(cursor: 2, total: 5, capacity: 8);

        Assert.Equal(0, view.Start);
        Assert.Equal(5, view.Count);
        Assert.Equal(0, view.Above);
        Assert.Equal(0, view.Below);
    }

    [Fact]
    public void An_empty_list_draws_nothing_rather_than_a_negative_window()
    {
        var view = ListWindow.Compute(cursor: -1, total: 0, capacity: 8);

        Assert.Equal(0, view.Count);
        Assert.Equal(0, view.Above);
        Assert.Equal(0, view.Below);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(50)]
    [InlineData(98)]
    [InlineData(99)]
    public void The_cursor_is_always_inside_the_window(int cursor)
    {
        // The whole point. A cursor outside the drawn window is the original bug, and it is
        // invisible from any test that does not do this arithmetic.
        var view = ListWindow.Compute(cursor, total: 100, capacity: 8);

        Assert.InRange(cursor, view.Start, view.Start + view.Count - 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    public void The_window_never_runs_off_either_end(int cursor)
    {
        var view = ListWindow.Compute(cursor, total: 100, capacity: 8);

        Assert.True(view.Start >= 0, $"start {view.Start} is before the list");
        Assert.True(view.Start + view.Count <= 100, $"window ends past the list at {view.Start + view.Count}");
        Assert.Equal(8, view.Count);
    }

    [Fact]
    public void What_is_off_screen_is_counted_so_it_can_be_said_rather_than_implied()
    {
        var view = ListWindow.Compute(cursor: 50, total: 100, capacity: 8);

        // A window with nothing marking its edges reads as the whole list, which is worse than
        // the bug it replaces: the user stops looking rather than keeps scrolling.
        Assert.Equal(view.Start, view.Above);
        Assert.Equal(100 - view.Start - view.Count, view.Below);
        Assert.Equal(100, view.Above + view.Count + view.Below);
    }

    [Fact]
    public void At_the_top_of_a_long_list_nothing_is_above_and_the_rest_is_below()
    {
        var view = ListWindow.Compute(cursor: 0, total: 100, capacity: 8);

        Assert.Equal(0, view.Start);
        Assert.Equal(0, view.Above);
        Assert.Equal(92, view.Below);
    }

    [Fact]
    public void At_the_bottom_of_a_long_list_nothing_is_below()
    {
        var view = ListWindow.Compute(cursor: 99, total: 100, capacity: 8);

        Assert.Equal(92, view.Start);
        Assert.Equal(0, view.Below);
    }

    [Fact]
    public void Walking_the_whole_list_one_row_at_a_time_never_loses_the_cursor()
    {
        // Driven the way a thumb drives it, because an off-by-one that only bites mid-list is
        // exactly what a couple of spot checks miss.
        for (var cursor = 0; cursor < 100; cursor++)
        {
            var view = ListWindow.Compute(cursor, total: 100, capacity: 8);

            Assert.InRange(cursor, view.Start, view.Start + view.Count - 1);
            Assert.Equal(100, view.Above + view.Count + view.Below);
        }
    }

    [Fact]
    public void A_capacity_of_one_still_shows_the_row_the_cursor_is_on()
    {
        for (var cursor = 0; cursor < 5; cursor++)
        {
            var view = ListWindow.Compute(cursor, total: 5, capacity: 1);

            Assert.Equal(1, view.Count);
            Assert.Equal(cursor, view.Start);
        }
    }

    [Fact]
    public void A_capacity_of_zero_or_less_is_refused_rather_than_drawing_nothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ListWindow.Compute(0, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ListWindow.Compute(0, 10, -1));
    }
}
