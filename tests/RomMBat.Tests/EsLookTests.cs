using RomMBat.Tests.Support;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The fonts and help icons the shell borrows from the live install (RB-425, RB-426).
/// </summary>
/// <remarks>
/// The icons below are written for the test in the shapes ES's own files use: four outlined
/// circles with one filled, a d-pad of outlined paths and polygons, and a pill whose label
/// is a group's fill. Drawing them needs a window; what reaches the drawing does not.
/// </remarks>
public class EsLookTests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\" width=\"64\" height=\"64\">";

    [Fact]
    public void A_face_button_icon_is_four_outlines_and_one_filled_dot()
    {
        var shapes = EsIcon.Parse(Svg + """
            <metadata><title>ignored</title></metadata>
            <defs />
            <circle stroke-width="2" stroke="#fff" fill="none" r="10" cy="32" cx="50" />
            <circle stroke-width="2" stroke="#fff" fill="none" r="10" cy="50" cx="32" />
            <circle stroke-width="2" stroke="#fff" fill="none" r="10" cy="14" cx="32" />
            <circle stroke-width="2" stroke="#fff" fill="none" r="10" cy="32" cx="14" />
            <path style="fill:#ffffff" d="m 32,40 c -5.5,0 -10,4.5 -10,10 0,5.5 4.5,10 10,10 z" />
            </svg>
            """);

        Assert.NotNull(shapes);
        Assert.Equal(5, shapes.Count);
        Assert.All(shapes.Take(4), shape => Assert.False(shape.Filled));
        Assert.All(shapes.Take(4), shape => Assert.Equal(2, shape.StrokeWidth));

        // A circle becomes two half arcs, since one arc back to its own start draws nothing.
        Assert.Equal("F1 M40,32 A10,10 0 1 0 60,32 A10,10 0 1 0 40,32 Z", shapes[0].Data);

        // Filled through style, which outranks the default of black and so counts as a fill.
        Assert.True(shapes[4].Filled);
        Assert.Equal("F1 m 32,40 c -5.5,0 -10,4.5 -10,10 0,5.5 4.5,10 10,10 z", shapes[4].Data);
    }

    [Fact]
    public void A_shape_both_filled_and_stroked_is_drawn_twice_fill_first()
    {
        var shapes = EsIcon.Parse(Svg + """
            <polygon points="27,14 37,14 32,6.2" fill="#fff" stroke="#fff" stroke-width="2" />
            <polygon points="50,27 50,37 57.8,32" fill="none" stroke="#fff" stroke-width="2" />
            </svg>
            """);

        Assert.NotNull(shapes);
        Assert.Collection(
            shapes,
            filled => Assert.Equal(("F1 M27,14 L37,14 L32,6.2 Z", true), (filled.Data, filled.Filled)),
            stroked => Assert.Equal(("F1 M27,14 L37,14 L32,6.2 Z", false), (stroked.Data, stroked.Filled)),
            outline => Assert.Equal(("F1 M50,27 L50,37 L57.8,32 Z", false), (outline.Data, outline.Filled)));
    }

    [Fact]
    public void A_shape_inside_a_group_takes_the_group_s_fill()
    {
        var shapes = EsIcon.Parse(Svg + """
            <line x1="1" y1="19" x2="63" y2="19" stroke="#fff" stroke-width="2" />
            <g fill="none"><path d="M0,0 L10,10" stroke="#fff" /></g>
            <g id="label" fill="#fff"><path d="M1,1 L2,2 L1,2 Z" /></g>
            </svg>
            """);

        Assert.NotNull(shapes);
        Assert.Collection(
            shapes,
            line => Assert.Equal(("F1 M1,19 L63,19", false, 2.0), (line.Data, line.Filled, line.StrokeWidth)),
            unfilled => Assert.Equal((false, 1.0), (unfilled.Filled, unfilled.StrokeWidth)),
            label => Assert.True(label.Filled));
    }

    [Theory]
    [InlineData("<g transform=\"translate(0,16)\"><path d=\"M0,0 L1,1\" fill=\"#fff\" /></g>")]
    [InlineData("<path clip-path=\"url(#c)\" d=\"M0,0 L1,1\" fill=\"#fff\" />")]
    [InlineData("<text x=\"1\" y=\"1\">L</text>")]
    [InlineData("<circle cx=\"1\" cy=\"1\" fill=\"#fff\" />")]
    [InlineData("<polygon points=\"1,2,3\" fill=\"#fff\" />")]
    public void An_icon_using_anything_the_reader_does_not_draw_faithfully_is_refused_whole(string body)
    {
        var shapes = EsIcon.Parse(Svg + "<circle r=\"10\" cx=\"32\" cy=\"32\" fill=\"#fff\" />" + body + "</svg>");

        Assert.Null(shapes);
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<html><body /></html>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><circle r=\"10\" fill=\"#fff\" /></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\"></svg>")]
    public void A_file_that_is_not_an_icon_in_ES_s_box_is_refused(string text) =>
        Assert.Null(EsIcon.Parse(text));

    [Fact]
    public void A_missing_icon_file_reads_as_none_rather_than_throwing() =>
        Assert.Null(EsIcon.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "buttons_south.svg")));

    [Fact]
    public void A_tree_with_the_carbon_theme_draws_in_Cabin_and_ES_s_help_font()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var cabin = Touch(install, ThemeFiles.CarbonFonts.Combine("Cabin-Regular.ttf"));
        var bold = Touch(install, ThemeFiles.CarbonFonts.Combine("Cabin-Bold.ttf"));
        var es = Touch(install, ThemeFiles.EsResources.Combine("opensans_hebrew_condensed_regular.ttf"));
        Directory.CreateDirectory(install.Resolve(ThemeFiles.EsResources.Combine("help")));

        var files = ThemeFiles.Find(install);

        Assert.Equal(cabin, files.MenuFont);
        Assert.Equal(bold, files.TitleFont);
        Assert.Equal(es, files.HelpFont);
        Assert.Equal(install.Resolve(ThemeFiles.EsResources.Combine("help")), files.HelpIcons);
    }

    [Fact]
    public void Without_the_theme_every_face_falls_back_to_ES_s_own_font()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var es = Touch(install, ThemeFiles.EsResources.Combine("opensans_hebrew_condensed_regular.ttf"));

        var files = ThemeFiles.Find(install);

        Assert.Equal(es, files.MenuFont);
        Assert.Null(files.TitleFont);
        Assert.Equal(es, files.HelpFont);
        Assert.Null(files.HelpIcons);
    }

    [Fact]
    public void A_bare_tree_borrows_nothing_and_the_shell_draws_its_own()
    {
        using var tree = TempRetroBatTree.Create();

        Assert.Equal(ThemeFiles.None, ThemeFiles.Find(tree.Install()));
        Assert.Equal(ThemeFiles.None, ThemeFiles.Find(null));
    }

    [Fact]
    public void The_files_are_found_under_whatever_drive_the_tree_is_on_now()
    {
        using var tree = TempRetroBatTree.Create();
        Touch(tree.Install(), ThemeFiles.CarbonFonts.Combine("Cabin-Regular.ttf"));

        using var moved = tree.CopyToNewLocation();

        var files = ThemeFiles.Find(moved.Install());

        Assert.StartsWith(moved.Root, files.MenuFont, StringComparison.OrdinalIgnoreCase);
    }

    private static string Touch(RomMBat.Core.Paths.RetroBatInstall install, RomMBat.Core.Paths.RelativePath path)
    {
        var full = install.Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, [0]);
        return full;
    }
}
