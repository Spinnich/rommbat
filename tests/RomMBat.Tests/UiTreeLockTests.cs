using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomM.Client.Catalog;
using RomMBat.Core.Sets;
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
/// <b>When a write does happen it is a Core service that takes the lock, never the UI.</b> That
/// is what keeps the structural assertion true rather than deleting it, and it puts the
/// decision where the rest of the decisions are. <see cref="Content.PartialSweep.Apply"/>
/// already works this way and already returns the sentence for it, so stage 7b-2 invented
/// nothing here: it surfaced what was in the tree.
/// </para>
/// <para>
/// <b>Defining a sync set takes no lock at all, and that is a decision rather than an
/// oversight.</b> Every write on <see cref="SyncSetService"/> is a row in SQLite, which is in
/// WAL mode, and the tree lock serialises writers of <i>files in the tree</i>. Taking it to add
/// a set definition would be exactly the speculative acquire this class exists to warn about.
/// A test below asserts a set can be defined while a background pass holds it.
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
    public void Core_still_defines_the_tree_lock_so_the_assertion_above_is_not_vacuous()
    {
        // #100. Without this, renaming or deleting TreeLock makes every assertion in this
        // class pass for the wrong reason: the UI would not reference a type that no longer
        // exists, and the boundary would be disarmed with nothing saying so. The es_settings
        // boundary has carried this companion since 7b-1 and this one did not.
        using var stream = File.OpenRead(typeof(TreeLock).Assembly.Location);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var defined = metadata.TypeDefinitions
            .Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(TreeLock), defined);
    }

    [Fact]
    public void A_set_can_be_defined_while_a_background_pass_holds_the_lock()
    {
        using var tree = TempRetroBatTree.Create();

        // Stand in for a background quit that is mid-flush.
        using var held = TreeLock.TryAcquire(tree.Install());
        Assert.NotNull(held);

        var opened = InstallSession.Open(tree.Root);
        using var session = opened.Session;

        // Succeeds, and that is the assertion. A sets write that waited on this lock would
        // refuse a user's set definition because somebody else was draining the outbox, which
        // is two unrelated things sharing a mutex.
        var outcome = new SyncSetService(session!).Add(
            new SetDraft
            {
                Name = "while-locked",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
            },
            DateTimeOffset.UtcNow);

        Assert.Equal(SetRefusal.None, outcome.Refusal);
        Assert.Single(new SyncSetService(session!).List());
    }

    [Fact]
    public async Task An_eviction_started_while_the_lock_is_held_leaves_partial_alone_and_says_so()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var partial = Path.Combine(tree.AppDirectory, "partial");
        Directory.CreateDirectory(partial);
        File.WriteAllText(Path.Combine(partial, "9.part"), "half a rom no set has heard of");

        var opened = InstallSession.Open(tree.Root);
        using var session = opened.Session;

        var service = new EvictionService(session!);
        var report = service.Preview();

        Assert.False(report.Abandoned.IsEmpty);

        // Taken after the preview, which is the real shape: a person reads what would go, then
        // presses apply, and a background pass can start in between.
        using var held = TreeLock.TryAcquire(install);
        Assert.NotNull(held);

        var applied = await service.ApplyAsync(report, TestContext.Current.CancellationToken);

        // Reported as a value with the sentence already chosen, not as a throw and not as a
        // silent no-op. The bytes are still there and the next pass reclaims them.
        Assert.NotNull(applied.Swept);
        Assert.True(applied.Swept!.Skipped);
        Assert.Contains("another agent is writing there", applied.Swept.Summary, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(partial, "9.part")));
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
