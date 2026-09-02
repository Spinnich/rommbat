using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>One editable line: what it is called, what it says, and how it is changed.</summary>
/// <param name="Steps">True when Left and Right move through choices in place.</param>
public sealed record EditorRow(string Label, string Value, string? Detail, bool Steps);

/// <summary>
/// Defining a set, and changing the limits on one that exists.
/// </summary>
/// <remarks>
/// <b>One screen for both, because they are the same form.</b> Creating differs only in that
/// the scope is still choosable: once a set exists its scope is fixed, since pointing it
/// somewhere else would make its recorded membership an answer to a different question, and
/// <see cref="SyncSetStore.UpdatePolicy"/> refuses to carry that.
/// <para>
/// <b>Nothing here needs the on-screen keyboard except the name and a search term.</b> Caps and
/// ordering step through fixed choices on Left and Right, which is what a d-pad is for; typing
/// "8 GB" on a grid of letters is the interaction this whole stage exists to avoid. Accept
/// opens a picker and never adjusts, which is Argosy's rule and the one most easily got wrong
/// by writing the obvious thing first.
/// </para>
/// <para>
/// <b>The refusal comes from Core and is shown verbatim.</b> This screen does not decide
/// whether a folder is real or a slug resolves; it asks, and it prints the sentence it gets.
/// </para>
/// </remarks>
public sealed class SetEditorViewModel : IScreen
{
    private readonly InstallSession _session;
    private readonly SyncSetDefinition? _existing;

    private string _name;
    private CatalogScopeKind _scope;
    private string? _platformValue;
    private string? _platformLabel;
    private string? _platformFolder;
    private string? _searchTerm;

    /// <summary>
    /// What each multi-select facet currently holds.
    /// </summary>
    /// <remarks>
    /// Kept as sets rather than as a <see cref="CatalogFilter"/> so a picker can toggle one
    /// value without rebuilding the record, and turned into the filter only on save.
    /// </remarks>
    private readonly Dictionary<string, HashSet<string>> _facets =
        FilterFacet.Multi.ToDictionary(
            facet => facet,
            _ => new HashSet<string>(StringComparer.CurrentCultureIgnoreCase),
            StringComparer.Ordinal);

    /// <summary>How each facet's chosen values combine. Any is the default and the common case.</summary>
    private readonly Dictionary<string, FilterLogic> _logic =
        FilterFacet.Multi.ToDictionary(facet => facet, _ => FilterLogic.Any, StringComparer.Ordinal);

    /// <summary>
    /// The yes-or-no properties, three-state because "either" is the default.
    /// </summary>
    /// <remarks>
    /// Null is "do not filter on this", which is not the same as false. Favourites used to be
    /// a two-state toggle here and could therefore only ever say yes or nothing; RomM's own
    /// interface offers all three, and "games I have not favourited" is a real thing to sync.
    /// </remarks>
    private readonly Dictionary<string, bool?> _properties =
        FilterFacet.Properties.ToDictionary(property => property, _ => (bool?)null, StringComparer.Ordinal);

    /// <summary>The facet values this library offers, fetched once when a filter is chosen.</summary>
    /// <remarks>
    /// Internal rather than private so a test can seed it. Every screen here is drivable with
    /// no window and no controller, and the facet pickers were the one exception: their rows
    /// come from the network, so without this the operator row could only be checked by hand.
    /// </remarks>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>>? _facetValues;
    private string? _collectionValue;
    private string? _collectionLabel;

    /// <summary>
    /// True once a person has typed a name themselves.
    /// </summary>
    /// <remarks>
    /// A platform and a collection both already have a name in RomM, so making somebody spell
    /// one out on a d-pad to mirror it is work for nothing. The name follows what was chosen
    /// until it is edited, and then it stops moving, because silently overwriting a name
    /// somebody typed would be worse than asking for it in the first place.
    /// </remarks>
    private bool _namedByHand;
    private string? _folder;
    private readonly Func<Uri, RomMConnection>? _connect;

    private SetEditorViewModel(
        InstallSession session,
        SyncSetDefinition? existing,
        Func<Uri, RomMConnection>? connect)
    {
        _session = session;
        _existing = existing;
        _connect = connect;

        _name = existing?.Name ?? string.Empty;
        _scope = existing?.Scope ?? CatalogScopeKind.Platform;
        _folder = existing?.FolderOverride;

        if (existing?.Scope == CatalogScopeKind.Filter)
        {
            // Editing a filter set used to open on a blank filter, so the screen showed
            // "anything" for a set that had one. Nothing was lost, because SetEdit had no way
            // to write a filter either, but a set defined from the couch could never be
            // changed from it.
            var stored = SyncSetService.FilterOf(existing);

            _searchTerm = stored.SearchTerm;

            foreach (var facet in FilterFacet.Multi)
            {
                var key = FilterFacet.KeyOf(facet);
                _facets[facet].UnionWith(stored.ValuesFor(key));
                _logic[facet] = stored.LogicFor(key);
            }

            foreach (var property in FilterFacet.Properties)
            {
                _properties[property] = stored.Property(FilterFacet.KeyOf(property));
            }
        }

        if (existing?.Scope == CatalogScopeKind.Platform)
        {
            var known = new SyncSetService(session).PlatformsKnownHere()
                .FirstOrDefault(option =>
                    option.PlatformId.ToString(CultureInfo.InvariantCulture) == existing.ScopeValue);

            _platformValue = existing.ScopeValue;
            _platformLabel = known?.Label;
            _platformFolder = known?.Folder;
        }
    }

    /// <param name="connect">
    /// How a resolve started from here reaches the server. Carried rather than dropped so the
    /// create-then-resolve path can be driven against a stub: it is the flow that starts
    /// minutes of uninvited network work, and it was the only one a test could not reach past
    /// <c>NotPaired</c>. See #105.
    /// </param>
    public static SetEditorViewModel ForNew(InstallSession session, Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SetEditorViewModel(session, null, connect);
    }

    /// <param name="connect">See <see cref="ForNew"/>.</param>
    public static SetEditorViewModel ForExisting(
        InstallSession session,
        SyncSetDefinition set,
        Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(set);
        return new SetEditorViewModel(session, set, connect);
    }

    /// <summary>True when this is defining a set rather than changing one.</summary>
    public bool IsNew => _existing is null;

    /// <summary>True when a filter would match everything, which is worth saying before it does.</summary>
    private bool IsEmptyFilter =>
        _scope == CatalogScopeKind.Filter
        && string.IsNullOrWhiteSpace(_searchTerm)
        && _facets.Values.All(chosen => chosen.Count == 0)
        && _properties.Values.All(value => value is null);

    /// <summary>
    /// True when this set has to be told which RetroBat folder it writes into.
    /// </summary>
    /// <remarks>
    /// Which is rare, and is the only reason the row exists. A platform that already resolves
    /// needs no answer; arcade is the case that does, because which of the ten arcade folders
    /// is right depends on the romset the file came from. A set that already carries an
    /// override keeps showing it, so one made from the console stays visible and changeable.
    /// </remarks>
    public bool NeedsFolderChoice =>
        _folder is not null
        || (_scope == CatalogScopeKind.Platform && _platformValue is not null && _platformFolder is null);

    public string Title => IsNew ? "New sync set" : $"Edit '{_existing!.Name}'";

    /// <summary>Which row the cursor is on.</summary>
    public int Cursor { get; private set; }

    /// <summary>
    /// Which slice of the rows is on screen.
    /// </summary>
    /// <remarks>
    /// <b>Decided here rather than in the renderer, because the renderer cannot be tested.</b>
    /// The windowing arithmetic lived in <c>ScreenView</c>, so a screen that simply never called
    /// it drew every row it had and everything past the height of the display went off it, with
    /// the cursor moving somewhere invisible. That happened twice, to two screens, and the
    /// second time was found from the couch on a twenty-two row filter editor. A property is
    /// something a test can assert on; a call the renderer might not make is not.
    /// </remarks>
    public ListView Window => ListWindow.Compute(Cursor, Rows.Count);


    /// <summary>The refusal from Core, shown where the eye already is.</summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// The rows, which are never none.
    /// </summary>
    /// <remarks>
    /// <b>The floor is the fix for #106.</b> An existing set that is not filter-scoped and
    /// needs no folder built nothing at all: <c>Hints</c> then indexed <c>Rows[Cursor]</c> on
    /// the first frame and <c>Handle</c> divided by <c>rows.Count</c> on the first press. The
    /// only thing stopping that was <c>SetsScreens.Detail</c> computing the same predicate
    /// before it would push the screen, which is a guard living three files from the thing it
    /// guards. 7b-2b and 7b-2c both add screens that reach this surface.
    /// </remarks>
    public IReadOnlyList<EditorRow> Rows
    {
        get
        {
            var rows = BuildRows();

            return rows.Count > 0
                ? rows
                : [new EditorRow("Nothing to change", "this set is defined by its scope", null, false)];
        }
    }

    private List<EditorRow> BuildRows()
    {
        var rows = new List<EditorRow>();

        if (IsNew)
        {
            rows.Add(new EditorRow("Scope", SyncSetStore.ScopeText(_scope), null, false));

            if (_scope == CatalogScopeKind.Platform)
            {
                rows.Add(new EditorRow("Platform", _platformLabel ?? "not chosen", null, false));
            }
            else if (CatalogScopeService.CanList(_scope))
            {
                rows.Add(new EditorRow(
                    "Collection",
                    _collectionLabel ?? "not chosen",
                    null,
                    false));
            }
            else if (_scope == CatalogScopeKind.Filter)
            {
                // The only scope that needs a name typed, because it is the only one that
                // is not a mirror of something RomM has already named.
                rows.Add(new EditorRow(
                    "Name",
                    _name.Length == 0 ? "not set" : _name,
                    null,
                    false));
            }
        }

        // Outside the block above, so a filter set can be changed and not merely made. The
        // whole scope section used to be new-set-only, which left Edit on a filter set
        // showing one row about folders and nothing about the filter itself.
        if (_scope == CatalogScopeKind.Filter)
        {
            rows.Add(new EditorRow(
                "Search for",
                string.IsNullOrWhiteSpace(_searchTerm) ? "anything" : _searchTerm,
                null,
                false));

            // The facets, so a filter is a saved search rather than a name match. A facet
            // this library has no values for is left out: a picker that opens on an empty
            // list is a row that goes nowhere.
            foreach (var facet in FilterFacet.Multi)
            {
                if (_facetValues is { } values
                    && values.TryGetValue(facet, out var available)
                    && available.Count == 0
                    && _facets[facet].Count == 0)
                {
                    continue;
                }

                rows.Add(new EditorRow(facet, Describe(facet), null, false));
            }

            // The yes-or-no half. Four of them are answered from RomM's own bookkeeping
            // rather than from the game, and a set carrying one resolves differently on
            // another account or after a scan, so the row says so rather than the
            // documentation saying it somewhere nobody is looking.
            foreach (var property in FilterFacet.Properties)
            {
                rows.Add(new EditorRow(
                    property,
                    _properties[property] switch { true => "yes", false => "no", _ => "either" },
                    _properties[property] is not null && FilterFacet.DependOnTheServer.Contains(property)
                        ? "RomM answers this from its own records, so this set can resolve "
                            + "differently on another account or after a scan."
                        : null,
                    false));
            }

            if (IsEmptyFilter)
            {
                rows.Add(new EditorRow(
                    "Matches",
                    "the whole library",
                    "A filter with nothing set matches every game RomM holds.",
                    false));
            }
        }

        // Hidden unless it is actually needed, which is the fix for a hands-on finding.
        // Two rows that look alike were doing different jobs: Platform is the scope's own
        // value ("this set holds Atari 2600 games") and Folder is a RomM-to-RetroBat
        // mapping override. The mapping belongs in platform_map, where platforms list
        // already reads it and where docs/PLAN.md's M2 puts a screen of its own in 7b-3.
        // It survives here only for the case that genuinely needs a per-set answer: an
        // arcade platform resolving to none of the ten possible folders. Offering it on
        // every set made a global setting look like a per-set one, and it is meaningless
        // on a filter or a collection, which can span platforms.
        if (NeedsFolderChoice)
        {
            rows.Add(new EditorRow(
                "Folder",
                _folder ?? "not chosen",
                "RomMBat cannot tell which RetroBat system this platform means, so pick one.",
                false));
        }

        return rows;
    }

    public IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = new List<FooterHint>();

            if (Rows[Cursor] is { Steps: false })
            {
                hints.Add(new FooterHint(NavAction.Accept, "Change"));
            }

            hints.Add(new FooterHint(NavAction.Start, IsNew ? "Create set" : "Save changes"));
            hints.Add(new FooterHint(NavAction.Back, IsNew ? "Discard" : "Cancel"));

            return hints;
        }
    }

    public ScreenCommand Handle(NavAction action)
    {
        var rows = Rows;

        switch (action)
        {
            case NavAction.Up:
                Cursor = (Cursor - 1 + rows.Count) % rows.Count;
                return ScreenCommand.Stay;

            case NavAction.Down:
                Cursor = (Cursor + 1) % rows.Count;
                return ScreenCommand.Stay;

            // Nothing on this screen steps any more. Every row opens something, which is what
            // Accept is for; the caps that used to move on Left and Right are gone, because the
            // bound a person sets is the install-wide disk budget.
            case NavAction.Accept:
                return Open(rows[Cursor].Label);

            case NavAction.Start:
                return Save();

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    private ScreenCommand Open(string label) => label switch
    {
        "Name" => ScreenCommand.Push(new OnScreenKeyboard(
            "Name this set",
            "What do you want to call it?",
            _name,
            typed =>
            {
                _name = typed.Trim();
                _namedByHand = _name.Length > 0;
                return new TypedResult(null);
            },
            _session.EmulationStationLanguage())),

        "Scope" => ScreenCommand.Push(ScopePicker()),

        "Platform" => ScreenCommand.Push(PlatformPicker()),

        "Collection" => ScreenCommand.Push(CollectionPicker()),

        "Search for" => ScreenCommand.Push(new OnScreenKeyboard(
            "Search for",
            "Only games whose name matches this.",
            _searchTerm ?? string.Empty,
            typed =>
            {
                _searchTerm = typed.Trim();
                return new TypedResult(null);
            },
            _session.EmulationStationLanguage())),

        "Folder" => ScreenCommand.Push(FolderPicker()),

        _ when FilterFacet.Properties.Contains(label) => Cycle(label),

        _ when FilterFacet.Multi.Contains(label) => ScreenCommand.Push(FacetPicker(label)),

        // Nothing to open. It is a sentence, and it goes away the moment anything is set.
        "Matches" => ScreenCommand.Stay,

        _ => ScreenCommand.Stay,
    };

    /// <summary>
    /// Every scope, with the ones this pairing cannot use shown and unavailable.
    /// </summary>
    /// <remarks>
    /// <b>Offered rather than hidden, and the reason is on the row.</b> Hiding them is tidier
    /// and teaches nothing: a user who knows their RomM has collections would conclude RomMBat
    /// cannot use them, where the truth is their own pairing and it is fixable by pairing
    /// again. Refusing at the end instead would cost them a whole definition typed on a d-pad.
    /// The availability and the sentence both come from Core.
    /// </remarks>
    private ListScreen ScopePicker()
    {
        var scopes = new SyncSetService(_session).Scopes();

        return new ListScreen(
            "What should this set hold?",
            [.. scopes.Select(option => new ListRow(
                option.Label,
                null,
                option.Available ? null : option.Unavailable,
                option.Available))],
            index =>
            {
                _scope = scopes[index].Kind;
                _platformValue = null;
                _platformLabel = null;
                _platformFolder = null;
                _collectionValue = null;
                _collectionLabel = null;
                Cursor = 0;

                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this");
    }

    private ListScreen PlatformPicker()
    {
        var platforms = new SyncSetService(_session).PlatformsKnownHere();
        IReadOnlyDictionary<int, (int Games, long Bytes)> facts = new Dictionary<int, (int, long)>();

        IReadOnlyList<ListRow> Rows() =>
        [
            .. platforms.Select(option => new ListRow(
                option.Label,
                facts.TryGetValue(option.PlatformId, out var fact)
                    ? $"{fact.Games:N0} games, {ByteSize.Format(fact.Bytes)}"
                    : option.Folder ?? "no folder yet",
                option.Folder is null
                    ? option.Note
                    : facts.ContainsKey(option.PlatformId) ? $"into {option.Folder}" : null)),
        ];

        return new ListScreen(
            "Which platform?",
            Rows,
            index =>
            {
                _platformValue = platforms[index].PlatformId.ToString(CultureInfo.InvariantCulture);
                _platformLabel = platforms[index].Label;
                _platformFolder = platforms[index].Folder;
                Suggest(platforms[index].Label);
                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this")
        {
            EmptyMessage = "This device has not seen RomM's platform list yet. Sync once, and "
                + "the platforms it knows appear here.",

            // Enriching rather than Started: the rows are right from the first frame, read from
            // platform_map with no network, and the counts only make them richer. Hiding a
            // working list behind a spinner to wait for a decoration is a bad trade.
            Load = async token =>
            {
                var attempt = _session.Authenticate();

                if (attempt.Connection is null)
                {
                    return null;
                }

                using var connection = attempt.Connection;

                facts = await new CatalogScopeService(connection)
                    .ListPlatformFactsAsync(token)
                    .ConfigureAwait(false);

                return null;
            },
        }.Enriching();
    }

    /// <summary>Either, then yes, then no, then either again.</summary>
    /// <remarks>
    /// In that order because "either" is where the row starts and where a person undoing a
    /// choice wants to get back to, and two presses is the whole way round.
    /// </remarks>
    private ScreenCommand Cycle(string property)
    {
        _properties[property] = _properties[property] switch
        {
            null => true,
            true => false,
            false => null,
        };

        return ScreenCommand.Stay;
    }

    /// <summary>
    /// The values one facet can take, ticked as they are chosen, above how they combine.
    /// </summary>
    /// <remarks>
    /// A multi-select rather than a pick-one, because a filter genuinely means "any of these".
    /// Accept toggles and stays, which is why <see cref="ListScreen"/> re-reads its rows after
    /// a choice that does not navigate.
    /// <para>
    /// <b>The operator is the first row rather than a row of its own in the editor.</b> It
    /// belongs to this facet and means nothing without it, and putting all eleven in the
    /// editor would double a list that is already long. It reads as a sentence with the values
    /// under it: "any of", then the things.
    /// </para>
    /// </remarks>
    private ListScreen FacetPicker(string facet)
    {
        var chosen = _facets[facet];
        IReadOnlyList<string> available =
            _facetValues is { } known && known.TryGetValue(facet, out var seeded) ? seeded : [];

        // No values, no operator: combining nothing is not a choice, and a picker holding one
        // unusable row would never show its empty message.
        bool HasLogicRow() => available.Count > 0;

        IReadOnlyList<ListRow> Rows() =>
        [
            .. HasLogicRow()
                ? (ListRow[])[new ListRow("Match", FilterFacet.Says(_logic[facet]))]
                : [],
            .. available.Select(value => new ListRow(value, chosen.Contains(value) ? "chosen" : null)),
        ];

        return new ListScreen(
            facet,
            Rows,
            index =>
            {
                if (HasLogicRow() && index == 0)
                {
                    _logic[facet] = _logic[facet] switch
                    {
                        FilterLogic.Any => FilterLogic.All,
                        FilterLogic.All => FilterLogic.None,
                        _ => FilterLogic.Any,
                    };

                    return ScreenCommand.Stay;
                }

                var value = available[HasLogicRow() ? index - 1 : index];

                if (!chosen.Remove(value))
                {
                    chosen.Add(value);
                }

                // Stays, so several can be picked without leaving and coming back.
                return ScreenCommand.Stay;
            },
            acceptLabel: "Add or remove",
            backLabel: "Done")
        {
            // Names the operator, and follows it. Printing all three choices on the right of
            // the row read as three things being on at once, and a fixed note went on saying
            // "any of" after the operator had been changed to none.
            Note = () => $"Games matching {FilterFacet.Says(_logic[facet])} the "
                + $"{facet.ToLowerInvariant()} chosen here.",

            // Said plainly, because it is slow and the reason is not the user's fault. RomM
            // works the values out across every game in the library, and this is measured in
            // minutes on an 88,000-rom instance rather than seconds.
            LoadingMessage = "Asking RomM what this library can be filtered by. On a large "
                + "library this takes a while: the values are worked out across every game...",
            EmptyMessage = $"This library reports no {facet.ToLowerInvariant()} to filter by.",

            // Fetched once for the whole editor. Opening a second facet is instant.
            Load = _facetValues is not null ? null : async token =>
            {
                var attempt = _session.Authenticate();

                if (attempt.Connection is null)
                {
                    return attempt.Problem ?? "This install is not paired with a RomM server.";
                }

                using var connection = attempt.Connection;

                _facetValues = await new CatalogScopeService(connection)
                    .ListFilterValuesAsync(token)
                    .ConfigureAwait(false);

                available = _facetValues.TryGetValue(facet, out var loaded) ? loaded : [];
                return null;
            },
        }.Started();
    }

    /// <summary>
    /// Everything the filter rows currently say, as one record.
    /// </summary>
    /// <remarks>
    /// Driven off <see cref="FilterFacet"/>'s own lists rather than naming each field, so a
    /// facet added there reaches storage without a second edit here. The logic operator is
    /// written only where it is not the default, which keeps a plain filter's stored JSON as
    /// small as it was and lets the default move later.
    /// </remarks>
    private CatalogFilter BuildFilter()
    {
        var filter = new CatalogFilter
        {
            SearchTerm = string.IsNullOrWhiteSpace(_searchTerm) ? null : _searchTerm,
            Logic = FilterFacet.Multi
                .Where(facet => _facets[facet].Count > 0 && _logic[facet] != FilterLogic.Any)
                .ToDictionary(FilterFacet.KeyOf, facet => _logic[facet], StringComparer.Ordinal),
        };

        foreach (var facet in FilterFacet.Multi)
        {
            filter = filter.WithValues(FilterFacet.KeyOf(facet), [.. _facets[facet]]);
        }

        foreach (var property in FilterFacet.Properties)
        {
            filter = filter.WithProperty(FilterFacet.KeyOf(property), _properties[property]);
        }

        return filter;
    }

    /// <summary>
    /// What a facet row shows: nothing, one value, or how many, and how they combine.
    /// </summary>
    /// <remarks>
    /// The operator is named only when it is not the default, so a plain filter reads the way
    /// it always did and the two rows that were set to something unusual stand out.
    /// </remarks>
    private string Describe(string facet)
    {
        var chosen = _facets[facet];

        var what = chosen.Count switch
        {
            0 => "any",
            1 => chosen.First(),
            _ => string.Create(CultureInfo.CurrentCulture, $"{chosen.Count} chosen"),
        };

        return chosen.Count == 0 || _logic[facet] == FilterLogic.Any
            ? what
            : $"{what}, {FilterFacet.Says(_logic[facet])}";
    }

    /// <summary>
    /// The collections this RomM holds, fetched when the picker opens.
    /// </summary>
    /// <remarks>
    /// <b>The one screen in this stage that reaches the network to be built.</b> Everything else
    /// on the sets surface is answerable offline, and this is not: a collection is the server's
    /// to name. An unreachable server is a message on the screen rather than an empty list,
    /// because an empty list would read as "you have no collections".
    /// </remarks>
    private ListScreen CollectionPicker()
    {
        IReadOnlyList<ScopeValueOption> options = [];

        IReadOnlyList<ListRow> Rows() =>
            [.. options.Select(option => new ListRow(option.Label, option.Detail))];

        var scope = _scope;

        return new ListScreen(
            "Which collection?",
            Rows,
            index =>
            {
                _collectionValue = options[index].Value;
                _collectionLabel = options[index].Label;
                Suggest(options[index].Label);
                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this")
        {
            LoadingMessage = "Asking RomM which collections it has...",
            EmptyMessage = $"This RomM has no {SyncSetStore.ScopeText(scope)} to choose from.",
            Load = async token =>
            {
                var attempt = _session.Authenticate();

                if (attempt.Connection is null)
                {
                    return attempt.Problem ?? "This install is not paired with a RomM server.";
                }

                using var connection = attempt.Connection;

                var values = await new CatalogScopeService(connection)
                    .ListAsync(scope, token)
                    .ConfigureAwait(false);

                options = values.Options;
                return values.Problem;
            },
        }.Started();
    }

    /// <summary>
    /// The systems this install actually has, read live from <c>es_systems.cfg</c>.
    /// </summary>
    /// <remarks>
    /// The list and the validation are the same call into Core, so a picker cannot offer a
    /// folder that the save then refuses. A test drives every offered folder through
    /// <see cref="SyncSetService.Add"/> and requires it to be accepted.
    /// </remarks>
    private ListScreen FolderPicker()
    {
        var folders = new SyncSetService(_session).FoldersKnownHere();
        var rows = new List<ListRow> { new("Choose automatically", null, "Let RomMBat decide from the platform.") };
        rows.AddRange(folders.Select(folder => new ListRow(folder)));

        return new ListScreen(
            "Which RetroBat system?",
            rows,
            index =>
            {
                _folder = index == 0 ? null : folders[index - 1];
                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this");
    }

    private ScreenCommand Save()
    {
        var service = new SyncSetService(_session);
        var now = DateTimeOffset.UtcNow;

        if (!IsNew)
        {
            // The folder, and a filter set's filter. Caps are not shown here any more, and an
            // unset property on SetEdit means "leave it alone", so a set given a cap from the
            // console keeps it. Sending the cleared values a hidden row would have produced
            // would silently wipe somebody's limit for opening a screen.
            var edited = service.Edit(
                _existing!.Name,
                new SetEdit
                {
                    ClearFolderOverride = _folder is null,
                    FolderOverride = _folder,
                    Filter = _scope == CatalogScopeKind.Filter ? BuildFilter() : null,
                },
                now);

            if (edited.IsRefused)
            {
                Problem = edited.Problem;
                return ScreenCommand.Stay;
            }

            // A changed filter clears the resolution stamp, so the set now holds membership
            // that answers the old question. Resolving straight away closes that window,
            // which is the same reasoning that puts a new set into a resolve.
            if (edited.Set is { LastResolvedAt: null } stale && _scope == CatalogScopeKind.Filter)
            {
                return ScreenCommand.ReplaceThenOpen(
                    SetsScreens.Detail(_session, stale.Name, _connect),
                    SetsScreens.Resolve(_session, [stale], _connect));
            }

            return ScreenCommand.Pop;
        }

        if (_name.Length == 0)
        {
            Problem = "Give the set a name first.";
            return ScreenCommand.Stay;
        }

        var added = service.Add(
            new SetDraft
            {
                Name = _name,
                Scope = _scope,
                ScopeValue = _scope switch
                {
                    CatalogScopeKind.Platform => _platformValue,
                    CatalogScopeKind.Filter => null,
                    _ => _collectionValue,
                },
                Filter = _scope == CatalogScopeKind.Filter ? BuildFilter() : null,
                // No caps from here. The disk budget is the bound a person sets, and it is
                // install-wide; a per-set cap made an optional refinement look like a decision
                // every set needs, and no ordering makes "which 10 of 9,196" a good guess.
                // SetDraft's defaults carry the rest, and sets add keeps every flag it has.
                FolderOverride = _folder,
            },
            now);

        if (added.IsRefused)
        {
            // Verbatim from Core, which states the rule. The remedy is not appended, because
            // on this screen the remedy is the row above and the user is already on it.
            Problem = added.Problem;
            return ScreenCommand.Stay;
        }

        // Onto the set that was just made, and straight into resolving it. A set that has never
        // resolved holds nothing and can do nothing, so stopping at a screen reading "0 games,
        // never resolved" asks for one more press to get the thing that was just described.
        // Starting minutes of network work uninvited is only reasonable because stopping it
        // costs one press and keeps what it found.
        //
        // The editor is replaced rather than pushed over, so backing out of the resolve reaches
        // the set, and backing out of that reaches the list exactly once.
        return ScreenCommand.ReplaceThenOpen(
            SetsScreens.Detail(_session, added.Set!.Name, _connect),
            SetsScreens.Resolve(_session, [added.Set], _connect));
    }

    /// <summary>Names the set after what it points at, unless somebody named it themselves.</summary>
    private void Suggest(string label)
    {
        if (!_namedByHand)
        {
            _name = label;
        }
    }

}
