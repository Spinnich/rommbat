using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The UI cannot write <c>es_settings.cfg</c>, asserted structurally.
/// </summary>
/// <remarks>
/// <b>This is the most important test in stage 7b, and it is deliberately not a test of
/// behaviour.</b> A test that drove a screen and checked the file was untouched would pass for
/// a UI that simply had not been asked to write one yet. What has to hold is stronger: there is
/// no code path from the UI to that writer at all.
/// <para>
/// <b>Why the rule cannot be bent.</b> The UI is launched from the EmulationStation menu, so ES
/// is always up by construction, and ES loads <c>es_settings.cfg</c> at startup and serialises
/// its own model over anything written underneath. A key that appears afterwards is discarded,
/// and merging and atomicity do not help: both were tried and the write still vanished
/// (findings 178 and 179). Every change therefore goes through
/// <see cref="PendingConfigStore"/> and is applied by <c>background quit</c> once the process
/// is confirmed gone. <c>docs/PLAN.md</c> says "there is no arrangement under which it can",
/// and this is what makes that a fact about the build rather than an intention.
/// </para>
/// <para>
/// Reading the assembly's own type references is what makes it survive a refactor: a helper
/// added in some other namespace, a lambda, a generic, or a call through an interface all still
/// leave <see cref="EsSettingsFile"/> in the UI's <c>TypeRef</c> table. Grepping the source
/// would not.
/// </para>
/// </remarks>
public class EsSettingsBoundaryTests
{
    /// <summary>The writer the UI may never reach, and its neighbours in the same file.</summary>
    private static readonly string[] Forbidden =
    [
        nameof(EsSettingsFile),
        nameof(EsSetting),
    ];

    [Fact]
    public void The_UI_assembly_never_references_the_es_settings_writer()
    {
        var referenced = TypeReferencesOf(UiAssemblyPath());

        foreach (var name in Forbidden)
        {
            Assert.DoesNotContain(name, referenced);
        }
    }

    [Fact]
    public void A_settings_change_from_the_UI_is_a_queued_row_and_the_queue_is_what_it_can_reach()
    {
        var referenced = TypeReferencesOf(UiAssemblyPath());

        // The other half of the same rule. Asserting only the absence above would pass for a UI
        // that cannot change a setting at all, which is not what was built: it can, through the
        // queue, and background quit applies it once EmulationStation is gone.
        Assert.Contains(nameof(PendingConfigStore), referenced);
    }

    [Fact]
    public void The_agent_does_reference_it_so_the_assertion_above_is_not_vacuous()
    {
        // If EsSettingsFile were renamed or deleted, every assertion in this class would pass
        // for the wrong reason. Something in the tree must still reach it, and Core is where
        // the writer and its only callers live.
        var core = TypeDefinitionsOf(typeof(EsSettingsFile).Assembly.Location);

        Assert.Contains(nameof(EsSettingsFile), core);
    }

    private static string UiAssemblyPath()
    {
        // The UI is referenced by this project, so its assembly is beside the tests.
        var path = Path.Combine(AppContext.BaseDirectory, "RomMBat.dll");

        Assert.True(
            File.Exists(path),
            $"The UI assembly is not at {path}. This test asserts against the built output, so "
                + "it cannot run without it.");

        return path;
    }

    /// <summary>Every type this assembly names outside itself.</summary>
    private static HashSet<string> TypeReferencesOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in metadata.TypeReferences)
        {
            names.Add(metadata.GetString(metadata.GetTypeReference(handle).Name));
        }

        // A type reached only through one of its members still appears here through the
        // member's parent, but take the parents explicitly rather than relying on that.
        foreach (var handle in metadata.MemberReferences)
        {
            var parent = metadata.GetMemberReference(handle).Parent;
            if (parent.Kind == HandleKind.TypeReference)
            {
                var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
                names.Add(metadata.GetString(type.Name));
            }
        }

        return names;
    }

    private static HashSet<string> TypeDefinitionsOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        return [.. metadata.TypeDefinitions
            .Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name))];
    }
}
