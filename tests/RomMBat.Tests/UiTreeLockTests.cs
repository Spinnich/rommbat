using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using RomMBat.UI.Screens;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// What the interface does while a background pass holds the tree lock.
/// </summary>
/// <remarks>
/// <b>In this stage it never takes the lock, and that is a decision rather than an omission.</b>
/// The obvious thing to build is a status row saying whether a pass is running, found by trying
/// to take the lock and seeing whether it comes. That is actively harmful here:
/// the agent's <c>flush</c> treats a failed acquire as <i>success</i> and exits having done
/// nothing, on the correct reasoning that somebody else is already draining the queue. So a UI
/// that grabbed the lock for even an instant to look at it would make a <c>background quit</c>
/// flush starting in that instant skip the upload entirely, and report success while doing it.
/// The user's save would sit in the outbox until the next quit, with nothing anywhere saying
/// why.
/// <para>
/// <b>Reading needs no lock.</b> The store is SQLite in WAL mode, so a reader and a writer
/// coexist, and everything 7b-1 shows is a read. The lock exists to serialise <i>writers</i>,
/// and this stage has none.
/// </para>
/// <para>
/// <b>When the UI does write, in a later stage</b>, it takes the lock for the duration of that
/// write and says "another RomMBat pass is running" when it cannot, staying navigable. What it
/// must never do is take the lock speculatively to answer a question.
/// </para>
/// </remarks>
public class UiTreeLockTests
{
    private static GamepadStatus NoPad =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    [Fact]
    public void The_UI_assembly_never_takes_the_tree_lock()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RomMBat.dll");
        Assert.True(File.Exists(path), $"the UI assembly is not at {path}");

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in metadata.TypeReferences)
        {
            referenced.Add(metadata.GetString(metadata.GetTypeReference(handle).Name));
        }

        foreach (var handle in metadata.MemberReferences)
        {
            if (metadata.GetMemberReference(handle).Parent is { Kind: HandleKind.TypeReference } parent)
            {
                referenced.Add(metadata.GetString(
                    metadata.GetTypeReference((TypeReferenceHandle)parent).Name));
            }
        }

        // Structural, for the same reason the es_settings boundary is: a helper in another
        // namespace, a lambda or a call through an interface would all still leave the type in
        // this table, where a grep over the source would miss them.
        Assert.DoesNotContain(nameof(TreeLock), referenced);
    }

    [Fact]
    public void The_interface_opens_and_reads_while_a_background_pass_holds_the_lock()
    {
        using var tree = TempRetroBatTree.Create();

        // Stand in for a background quit that is mid-flush.
        using var held = TreeLock.TryAcquire(tree.Install());
        Assert.NotNull(held);

        var opened = InstallSession.Open(tree.Root);
        using var session = opened.Session;

        Assert.True(opened.IsOpen);

        var status = new StatusViewModel(session!, NoPad);
        var sections = status.Sections();

        // Every row renders. Nothing waits on the lock, nothing refuses, and there is no error
        // state to get stuck in: a person opening RomMBat during a flush sees the interface.
        Assert.NotEmpty(sections);
        Assert.All(sections, section => Assert.NotEmpty(section.Rows));
    }

    [Fact]
    public void A_second_holder_is_refused_which_is_what_makes_speculative_probing_dangerous()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var first = TreeLock.TryAcquire(install);
        Assert.NotNull(first);

        // This is the mechanism the reasoning above rests on. A flush meeting this answer stops
        // and reports success, so whoever is holding the lock had better be doing the work.
        using var second = TreeLock.TryAcquire(install);
        Assert.Null(second);

        first!.Dispose();

        using var afterRelease = TreeLock.TryAcquire(install);
        Assert.NotNull(afterRelease);
    }
}
