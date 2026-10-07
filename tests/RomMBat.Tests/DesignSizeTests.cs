using RomMBat.UI.Screens;
using Xunit;

namespace RomMBat.Tests;

/// <summary>The design canvas a display is given, which is what keeps 720p on the panel.</summary>
public sealed class DesignSizeTests
{
    [Theory]
    [InlineData(1920, 1080, 1920, 1080)]
    [InlineData(1280, 720, 1920, 1080)]
    [InlineData(3840, 2160, 1920, 1080)]
    [InlineData(1280, 800, 1920, 1200)]
    [InlineData(1024, 768, 1920, 1440)]
    public void A_display_gets_the_1080p_layout_at_its_own_shape(double width, double height, double canvasWidth, double canvasHeight)
    {
        // At 1280x720 the screens, laid out in 1080p pixels, ran off the panel under a
        // scrollbar on the agent tree. Every 16:9 display now gets the same 1920x1080 canvas,
        // and a taller one gets a taller canvas rather than bars.
        var (w, h) = DesignSize.Fit(width, height);

        Assert.Equal(canvasWidth, w, 3);
        Assert.Equal(canvasHeight, h, 3);
    }

    [Fact]
    public void A_window_with_no_size_yet_gets_the_design_size()
    {
        Assert.Equal((DesignSize.Width, DesignSize.Height), DesignSize.Fit(0, 0));
    }
}
