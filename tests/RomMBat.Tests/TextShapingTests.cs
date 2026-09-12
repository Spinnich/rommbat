using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// That the app builder still registers a text shaper.
/// </summary>
/// <remarks>
/// <b>This is here because the compiler cannot see it.</b> Avalonia 12 moved text shaping out
/// of <c>Avalonia.Skia</c> into <c>Avalonia.HarfBuzz</c>, so dropping <c>UseHarfBuzz</c>
/// leaves a build that compiles clean, passes every other test in this suite, and throws
/// "No text shaping system configured" at <c>AppBuilder.Setup</c> before a window is shown.
/// Nothing short of starting the app catches that, and the suite cannot start the app.
/// <para>
/// Structural rather than a grep, for the reason the tree lock boundary is: the call could
/// move into a helper, a lambda or another namespace and still land in this table.
/// </para>
/// </remarks>
public class TextShapingTests
{
    [Fact]
    public void The_UI_assembly_registers_a_text_shaper()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RomMBat.dll");
        Assert.True(File.Exists(path), $"the UI assembly is not at {path}");

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var called = metadata.MemberReferences
            .Select(handle => metadata.GetString(metadata.GetMemberReference(handle).Name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("UseHarfBuzz", called);
    }

    [Fact]
    public void Avalonia_still_defines_that_call_so_the_assertion_above_is_not_vacuous()
    {
        // The #100 companion. Without it, Avalonia renaming or retiring UseHarfBuzz would
        // leave the assertion above failing for a reason nobody could read, or a future
        // rewrite of it passing against a call that no longer exists.
        var extensions = Assembly.Load("Avalonia.HarfBuzz")
            .GetType("Avalonia.HarfBuzzApplicationExtensions");

        Assert.NotNull(extensions);
        Assert.NotNull(extensions!.GetMethod("UseHarfBuzz", BindingFlags.Public | BindingFlags.Static));
    }
}
