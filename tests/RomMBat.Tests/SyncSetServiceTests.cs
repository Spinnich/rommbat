using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Mapping;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The set orchestration, driven without a console.
/// </summary>
/// <remarks>
/// <b>Being able to write these at all is the point of the seam.</b> Every rule here used to
/// live inside <c>SetsCommand</c>, welded to <see cref="Console"/>, so the only way to assert
/// any of it was to redirect the console and read printed strings. That works, and the agent
/// suite still does it, but it could never be reached from the interface, which meant the
/// interface would have needed a second copy of each rule.
/// <para>
/// <b>Nothing here touches the network.</b> Defining, editing, listing and removing a set are
/// all answerable with the server switched off, which is not incidental: a handheld away from
/// its server has to be able to say what it wants to sync.
/// </para>
/// </remarks>
public sealed class SyncSetServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public SyncSetServiceTests()
    {
        // The real 8.2.1 file, so the folder validation and the picker are tested against the
        // vocabulary a live install actually has rather than against two invented systems.
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    private SyncSetService Service => new(_session);

    // ---- the granted-scope refusal ----

    [Theory]
    [InlineData(CatalogScopeKind.Collection)]
    [InlineData(CatalogScopeKind.SmartCollection)]
    [InlineData(CatalogScopeKind.VirtualCollection)]
    public void A_collection_scope_is_refused_when_the_pairing_never_got_collections_read(
        CatalogScopeKind scope)
    {
        PairWith("roms.read", "platforms.read");

        var outcome = Service.Add(new SetDraft { Name = "c", Scope = scope, ScopeValue = "3" }, Now);

        // Refused at the point of definition rather than as a 403 in the middle of a sync,
        // which is what a narrowed grant degrading by feature means.
        Assert.Equal(SetRefusal.MissingScope, outcome.Refusal);
        Assert.Contains("collections.read", outcome.Problem, StringComparison.Ordinal);
        Assert.Null(outcome.Set);
    }

    [Fact]
    public void A_platform_scope_needs_no_collection_grant()
    {
        PairWith("roms.read", "platforms.read");
        SeedPlatform(4, "snes");

        Assert.Equal(
            SetRefusal.None,
            Service.Add(new SetDraft { Name = "p", Scope = CatalogScopeKind.Platform, ScopeValue = "4" }, Now).Refusal);
    }

    [Fact]
    public void The_refusal_sentence_names_the_missing_scope_and_no_command_line()
    {
        PairWith("roms.read");

        var problem = Service
            .Add(new SetDraft { Name = "c", Scope = CatalogScopeKind.Collection, ScopeValue = "3" }, Now)
            .Problem!;

        // The rule is Core's and the remedy is the caller's. A sentence telling someone to
        // pass --scope would be false on a screen that has no command line, so it lives in
        // the agent and this asserts that it did not leak back.
        Assert.DoesNotContain("--", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("rommbat-agent", problem, StringComparison.Ordinal);
    }

    // ---- the fs_slug to platform-id resolution ----

    [Fact]
    public void A_platform_scope_takes_the_fs_slug_a_person_has_in_front_of_them()
    {
        SeedPlatform(42, "megadrive");

        var outcome = Service.Add(
            new SetDraft { Name = "md", Scope = CatalogScopeKind.Platform, ScopeValue = "megadrive" },
            Now);

        // Stored as the id the endpoint accepts, not as the slug that was typed.
        Assert.Equal(SetRefusal.None, outcome.Refusal);
        Assert.Equal("42", outcome.Set!.ScopeValue);
    }

    [Fact]
    public void A_numeric_platform_value_is_taken_as_an_id_and_not_looked_up()
    {
        var outcome = Service.Add(
            new SetDraft { Name = "n", Scope = CatalogScopeKind.Platform, ScopeValue = "7" },
            Now);

        Assert.Equal(SetRefusal.None, outcome.Refusal);
        Assert.Equal("7", outcome.Set!.ScopeValue);
    }

    [Fact]
    public void An_unknown_slug_is_refused_and_the_offending_value_comes_back_for_the_caller_to_name()
    {
        var outcome = Service.Add(
            new SetDraft { Name = "x", Scope = CatalogScopeKind.Platform, ScopeValue = "gbaa" },
            Now);

        Assert.Equal(SetRefusal.UnknownPlatform, outcome.Refusal);
        Assert.Equal("gbaa", outcome.Value);
    }

    [Fact]
    public void A_scope_that_needs_a_value_and_has_none_is_refused()
    {
        var outcome = Service.Add(new SetDraft { Name = "x", Scope = CatalogScopeKind.Platform }, Now);

        Assert.Equal(SetRefusal.MissingValue, outcome.Refusal);
    }

    // ---- the folder validation, against the live es_systems.cfg ----

    [Fact]
    public void A_folder_override_must_name_a_system_this_install_actually_has()
    {
        var outcome = Service.Add(
            new SetDraft
            {
                Name = "arcade",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "9",
                FolderOverride = "nosuchsystem",
            },
            Now);

        // RetroBat is the authority and the file is read live, because RetroBat adds systems
        // every release and users add their own.
        Assert.Equal(SetRefusal.UnknownFolder, outcome.Refusal);
        Assert.Equal("nosuchsystem", outcome.Value);
    }

    [Fact]
    public void A_folder_override_naming_a_real_system_is_taken()
    {
        var folder = Service.FoldersKnownHere()[0];

        var outcome = Service.Add(
            new SetDraft
            {
                Name = "ok",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "9",
                FolderOverride = folder,
            },
            Now);

        Assert.Equal(SetRefusal.None, outcome.Refusal);
        Assert.Equal(folder, outcome.Set!.FolderOverride);
    }

    [Fact]
    public void The_folders_offered_to_a_picker_are_the_ones_the_validation_accepts()
    {
        // The picker and the validator must not be able to disagree, which they could if the
        // picker built its own list. Both read the same live file through the same service.
        foreach (var folder in Service.FoldersKnownHere())
        {
            var outcome = Service.Add(
                new SetDraft
                {
                    Name = $"set-{folder}",
                    Scope = CatalogScopeKind.Platform,
                    ScopeValue = "9",
                    FolderOverride = folder,
                },
                Now);

            Assert.Equal(SetRefusal.None, outcome.Refusal);
        }
    }

    [Fact]
    public void No_folder_a_picker_offers_is_a_path()
    {
        // Rule 1. A folder override is a system name and is resolved at point of use; a value
        // carrying a separator would be a stored path in everything but name.
        Assert.All(Service.FoldersKnownHere(), folder =>
        {
            Assert.DoesNotContain('\\', folder);
            Assert.DoesNotContain('/', folder);
            Assert.False(Path.IsPathRooted(folder));
        });
    }

    // ---- the scope picker's own data ----

    [Fact]
    public void Every_scope_is_offered_and_the_unavailable_ones_carry_their_reason()
    {
        PairWith("roms.read");

        var scopes = Service.Scopes();

        // All five, always. A picker that dropped the unavailable ones would leave a user who
        // knows their RomM has collections concluding RomMBat cannot use them, when the reason
        // is their own pairing and is fixable.
        Assert.Equal(Enum.GetValues<CatalogScopeKind>().Length, scopes.Count);

        var collection = scopes.Single(option => option.Kind == CatalogScopeKind.Collection);
        Assert.False(collection.Available);
        Assert.Contains("collections.read", collection.Unavailable, StringComparison.Ordinal);

        Assert.True(scopes.Single(option => option.Kind == CatalogScopeKind.Platform).Available);
    }

    [Fact]
    public void A_full_grant_leaves_every_listable_scope_pickable()
    {
        PairWith([.. RomMScopes.Requested]);

        // Every scope RomMBat can list the values of. A grant is not the only reason a scope may
        // be unpickable, and conflating the two would make a permanent gap look like something
        // re-pairing would fix.
        //
        // Derived from WhyNotListable rather than excluding a kind by name, which is what makes
        // this cover a scope kind added later: naming the exception here would have left the
        // sixth kind asserting the opposite of what it means.
        Assert.All(
            Service.Scopes().Where(option => CatalogScopeService.WhyNotListable(option.Kind) is null),
            option => Assert.True(option.Available, $"{option.Kind} should be pickable"));
    }

    [Fact]
    public void A_scope_whose_values_cannot_be_listed_is_never_offered_as_pickable()
    {
        PairWith([.. RomMScopes.Requested]);

        // A hands-on pass reached a scope that could be picked and then not completed: the
        // editor had no row to set a value and the only thing the screen could say was that a
        // value was needed. Offering a scope with no way to finish it is worse than not
        // offering it, and the reason has to say so rather than blaming the pairing.
        var virtualCollection = Service.Scopes().Single(o => o.Kind == CatalogScopeKind.VirtualCollection);

        Assert.False(virtualCollection.Available);
        Assert.NotNull(virtualCollection.Unavailable);
        Assert.DoesNotContain("granted", virtualCollection.Unavailable, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_pickable_scope_has_a_way_to_supply_its_value()
    {
        PairWith([.. RomMScopes.Requested]);

        // The generalising form. A scope is pickable only if something can produce its value:
        // the platform picker, the collection picker, or the filter's own fields.
        foreach (var option in Service.Scopes().Where(o => o.Available))
        {
            var completable = option.Kind == CatalogScopeKind.Filter
                || option.Kind == CatalogScopeKind.Platform
                || CatalogScopeService.CanList(option.Kind);

            Assert.True(completable, $"{option.Kind} is offered with no way to set its value");
        }
    }

    [Fact]
    public void What_the_picker_offers_and_what_Add_refuses_are_the_same_rule()
    {
        PairWith("roms.read");

        // The one that matters. A picker deciding availability for itself would be a second
        // copy of the rule inside Add, and the two would drift the first time either moved.
        //
        // Two reasons a scope is unavailable, and they are not interchangeable: the pairing's
        // grant, which Add enforces and which re-pairing fixes, and RomMBat being unable to
        // list what the scope could point at, which Add has no opinion about because it is not
        // a fact about the request. Written as one equality it read as though Add answered
        // both, and the sixth scope kind is the first that is unavailable for only the second
        // reason.
        foreach (var option in Service.Scopes())
        {
            var refusal = Service
                .Add(new SetDraft { Name = $"s-{option.Kind}", Scope = option.Kind, ScopeValue = "3" }, Now)
                .Refusal;

            var grantMissing = refusal == SetRefusal.MissingScope;
            var cannotList = CatalogScopeService.WhyNotListable(option.Kind) is not null;

            Assert.Equal(option.Available, !grantMissing && !cannotList);
            Assert.Equal(grantMissing, SyncSetService.RequiresCollections(option.Kind));
        }
    }

    [Fact]
    public void A_platform_with_no_romm_id_cannot_be_offered_because_a_scope_needs_one()
    {
        SeedPlatform(11, "snes");
        _session.Store.PlatformMap.SetOverride("homebrew", "snes", Now);

        Assert.DoesNotContain(Service.PlatformsKnownHere(), option =>
            string.Equals(option.FsSlug, "homebrew", StringComparison.Ordinal));
    }

    // ---- editing ----

    [Fact]
    public void Editing_changes_the_caps_and_leaves_the_scope_alone()
    {
        SeedPlatform(4, "snes");
        var added = Service.Add(
            new SetDraft
            {
                Name = "e",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "4",
                MaxGames = 40,
            },
            Now).Set!;

        var edited = Service.Edit("e", new SetEdit { MaxGames = 10, Ordering = SetOrdering.SizeAscending }, Now).Set!;

        Assert.Equal(10, edited.MaxGames);
        Assert.Equal(SetOrdering.SizeAscending, edited.Ordering);
        Assert.Equal(added.ScopeValue, edited.ScopeValue);
        Assert.Equal(added.Scope, edited.Scope);
    }

    [Fact]
    public void A_cap_can_be_cleared_rather_than_only_changed()
    {
        SeedPlatform(4, "snes");
        Service.Add(
            new SetDraft { Name = "e", Scope = CatalogScopeKind.Platform, ScopeValue = "4", MaxGames = 40 },
            Now);

        // Distinguishable from "leave it alone", which is what an unset property means.
        Assert.Null(Service.Edit("e", new SetEdit { ClearMaxGames = true }, Now).Set!.MaxGames);
    }

    [Fact]
    public void An_edit_validates_a_new_folder_the_same_way_adding_one_does()
    {
        SeedPlatform(4, "snes");
        Service.Add(new SetDraft { Name = "e", Scope = CatalogScopeKind.Platform, ScopeValue = "4" }, Now);

        Assert.Equal(
            SetRefusal.UnknownFolder,
            Service.Edit("e", new SetEdit { FolderOverride = "nosuchsystem" }, Now).Refusal);
    }

    [Fact]
    public void Editing_a_set_that_is_not_there_says_so()
    {
        Assert.Equal(SetRefusal.NotFound, Service.Edit("ghost", new SetEdit { MaxGames = 1 }, Now).Refusal);
    }

    [Fact]
    public void A_duplicate_name_is_refused()
    {
        SeedPlatform(4, "snes");
        var draft = new SetDraft { Name = "dup", Scope = CatalogScopeKind.Platform, ScopeValue = "4" };

        Assert.Equal(SetRefusal.None, Service.Add(draft, Now).Refusal);
        Assert.Equal(SetRefusal.NameTaken, Service.Add(draft, Now).Refusal);
    }

    [Fact]
    public void Removing_a_set_that_is_not_there_says_so()
    {
        Assert.Equal(SetRefusal.NotFound, Service.Remove("ghost").Refusal);
    }

    // ---- #78, preserved rather than fixed ----

    [Fact]
    public void A_filter_draft_is_built_from_its_fields_and_ignores_a_scope_value()
    {
        // This is #78, still open. A picker builds a filter from fields and has no value to
        // supply, so it cannot trip it; the agent still maps --value here and it is still
        // ignored. Asserted so that fixing it later is a deliberate change to a stated
        // behaviour rather than a silent one.
        var outcome = Service.Add(
            new SetDraft
            {
                Name = "f",
                Scope = CatalogScopeKind.Filter,
                ScopeValue = "this is ignored",
                Filter = new CatalogFilter { SearchTerm = "Mario" },
            },
            Now);

        Assert.Equal(SetRefusal.None, outcome.Refusal);
        Assert.Contains("Mario", outcome.Set!.ScopeValue, StringComparison.Ordinal);
        Assert.DoesNotContain("this is ignored", outcome.Set.ScopeValue, StringComparison.Ordinal);
    }

    // ---- offline ----

    [Fact]
    public void Defining_listing_editing_and_removing_all_work_with_no_server_configured()
    {
        SeedPlatform(4, "snes");

        // Nothing in this test has an origin, a token or a connection, and none of it is a
        // degraded path: offline is a working state and this is the whole of the sets surface.
        Assert.Equal(SetRefusal.None, Service.Add(
            new SetDraft { Name = "offline", Scope = CatalogScopeKind.Platform, ScopeValue = "4" }, Now).Refusal);

        Assert.Single(Service.List());
        Assert.NotNull(Service.Show("offline"));
        Assert.Equal(SetRefusal.None, Service.Edit("offline", new SetEdit { MaxGames = 5 }, Now).Refusal);
        Assert.Equal(SetRefusal.None, Service.Remove("offline").Refusal);
        Assert.Empty(Service.List());
    }

    [Fact]
    public void Selecting_by_name_and_selecting_everything_are_the_same_call()
    {
        SeedPlatform(4, "snes");
        Service.Add(new SetDraft { Name = "a", Scope = CatalogScopeKind.Platform, ScopeValue = "4" }, Now);
        Service.Add(new SetDraft { Name = "b", Scope = CatalogScopeKind.Platform, ScopeValue = "4" }, Now);

        Assert.Equal(2, Service.Select(null).Sets.Count);
        Assert.Single(Service.Select("a").Sets);
        Assert.True(Service.Select("ghost").IsEmpty);
    }

    // ---- no Core sentence names a command line ----

    [Fact]
    public void No_sentence_this_service_returns_names_a_subcommand_or_a_flag()
    {
        PairWith("roms.read");
        SeedPlatform(4, "snes");

        // A sweep rather than a check of one site. Round 8 of stage 7b-1 found a rule that was
        // enforced structurally in one place and broken in the field next to it, and the thing
        // that catches a moved mistake is a test that looks everywhere.
        var sentences = new List<string?>
        {
            Service.Add(new SetDraft { Name = "c", Scope = CatalogScopeKind.Collection, ScopeValue = "1" }, Now).Problem,
            Service.Add(new SetDraft { Name = "v", Scope = CatalogScopeKind.Platform }, Now).Problem,
            Service.Add(new SetDraft { Name = "u", Scope = CatalogScopeKind.Platform, ScopeValue = "zzz" }, Now).Problem,
            Service.Add(
                new SetDraft
                {
                    Name = "f",
                    Scope = CatalogScopeKind.Platform,
                    ScopeValue = "4",
                    FolderOverride = "nope",
                },
                Now).Problem,
            Service.Edit("ghost", new SetEdit(), Now).Problem,
            Service.Remove("ghost").Problem,
            Service.Select("ghost").Problem,
            Service.Select(null).Problem,
        };

        sentences.AddRange(Service.Scopes().Select(option => option.Unavailable));

        Assert.All(sentences.Where(sentence => sentence is not null), sentence =>
        {
            Assert.DoesNotContain("rommbat-agent", sentence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--", sentence, StringComparison.Ordinal);
            Assert.DoesNotContain("Run '", sentence, StringComparison.Ordinal);
        });
    }

    private void SeedPlatform(int id, string folder) =>
        _session.Store.PlatformMap.Record(
            new PlatformResolver(Fixtures.LoadEsSystems(), new Dictionary<string, string>())
                .Resolve(new RomMPlatform(id, folder, folder, folder)),
            Now);

    private void PairWith(params string[] scopes)
    {
        _session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(_session.Install));
        _session.Store.Device.SavePairing(
            new PairingResult(
                new Uri("https://romm.invalid"),
                "device-1",
                "Handheld",
                new GrantedScopes(scopes),
                TokenProtector.Protect("rmm_token", null, Now.AddYears(1))),
            Now);
    }
}
