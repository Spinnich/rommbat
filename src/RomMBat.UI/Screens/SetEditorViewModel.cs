using System.Globalization;
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

    private bool _favouritesOnly;

    /// <summary>The facet values this library offers, fetched once when a filter is chosen.</summary>
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? _facetValues;
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

    private SetEditorViewModel(InstallSession session, SyncSetDefinition? existing)
    {
        _session = session;
        _existing = existing;

        _name = existing?.Name ?? string.Empty;
        _scope = existing?.Scope ?? CatalogScopeKind.Platform;
        _folder = existing?.FolderOverride;

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

    public static SetEditorViewModel ForNew(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SetEditorViewModel(session, null);
    }

    public static SetEditorViewModel ForExisting(InstallSession session, SyncSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(set);
        return new SetEditorViewModel(session, set);
    }

    /// <summary>True when this is defining a set rather than changing one.</summary>
    public bool IsNew => _existing is null;

    /// <summary>
    /// True when this set has to be told which RetroBat folder it writes into.
    /// </summary>
    /// <remarks>
    /// Which is rare, and is the only reason the row exists. A platform that already resolves
    /// needs no answer; arcade is the case that does, because which of the ten arcade folders
    /// is right depends on the romset the file came from. A set that already carries an
    /// override keeps showing it, so one made from the console stays visible and changeable.
    /// </remarks>
    /// <summary>True when a filter would match everything, which is worth saying before it does.</summary>
    private bool IsEmptyFilter =>
        _scope == CatalogScopeKind.Filter
        && string.IsNullOrWhiteSpace(_searchTerm)
        && !_favouritesOnly
        && _facets.Values.All(chosen => chosen.Count == 0);

    public bool NeedsFolderChoice =>
        _folder is not null
        || (_scope == CatalogScopeKind.Platform && _platformValue is not null && _platformFolder is null);

    public string Title => IsNew ? "New sync set" : $"Edit '{_existing!.Name}'";

    /// <summary>Which row the cursor is on.</summary>
    public int Cursor { get; private set; }

    /// <summary>The refusal from Core, shown where the eye already is.</summary>
    public string? Problem { get; private set; }

    public IReadOnlyList<EditorRow> Rows
    {
        get
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

                    rows.Add(new EditorRow(
                        "Search for",
                        string.IsNullOrWhiteSpace(_searchTerm) ? "anything" : _searchTerm,
                        null,
                        false));

                    // The facets, so a filter is a saved search rather than a name match. A
                    // facet this library has no values for is left out: a picker that opens on
                    // an empty list is a row that goes nowhere.
                    foreach (var facet in FilterFacet.Multi)
                    {
                        if (_facetValues is { } values
                            && values.TryGetValue(facet, out var available)
                            && available.Count == 0
                            && _facets[facet].Count == 0)
                        {
                            continue;
                        }

                        rows.Add(new EditorRow(facet, Describe(_facets[facet]), null, false));
                    }

                    rows.Add(new EditorRow(
                        FilterFacet.Favourites,
                        _favouritesOnly ? "yes" : "no",
                        IsEmptyFilter ? "A filter with nothing set matches the whole library." : null,
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

        FilterFacet.Favourites => Toggle(),

        _ when FilterFacet.Multi.Contains(label) => ScreenCommand.Push(FacetPicker(label)),

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

    private ScreenCommand Toggle()
    {
        _favouritesOnly = !_favouritesOnly;
        return ScreenCommand.Stay;
    }

    /// <summary>
    /// The values one facet can take, ticked as they are chosen.
    /// </summary>
    /// <remarks>
    /// A multi-select rather than a pick-one, because a filter genuinely means "any of these".
    /// Accept toggles and stays, which is why <see cref="ListScreen"/> re-reads its rows after
    /// a choice that does not navigate.
    /// </remarks>
    private ListScreen FacetPicker(string facet)
    {
        var chosen = _facets[facet];
        IReadOnlyList<string> available =
            _facetValues is { } known && known.TryGetValue(facet, out var seeded) ? seeded : [];

        IReadOnlyList<ListRow> Rows() =>
            [.. available.Select(value => new ListRow(value, chosen.Contains(value) ? "chosen" : null))];

        return new ListScreen(
            facet,
            Rows,
            index =>
            {
                var value = available[index];

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
            Note = $"Games matching any of the {facet.ToLowerInvariant()} chosen here.",

            // Said plainly, because it is slow and the reason is not the user's fault. RomM
            // works the values out across every game in the library, and this is measured in
            // minutes on an 88,000-rom instance rather than seconds.
            LoadingMessage = "Asking RomM what this library can be filtered by. On a large "
                + "library this takes a while: the values are worked out across every game.",
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

    /// <summary>What a facet row shows: nothing, one value, or how many.</summary>
    private static string Describe(HashSet<string> chosen) => chosen.Count switch
    {
        0 => "any",
        1 => chosen.First(),
        _ => string.Create(CultureInfo.CurrentCulture, $"{chosen.Count} chosen"),
    };

    /// <summary>
    /// The systems this install actually has, read live from <c>es_systems.cfg</c>.
    /// </summary>
    /// <remarks>
    /// The list and the validation are the same call into Core, so a picker cannot offer a
    /// folder that the save then refuses. A test drives every offered folder through
    /// <see cref="SyncSetService.Add"/> and requires it to be accepted.
    /// </remarks>
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
            LoadingMessage = "Asking RomM which collections it has.",
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
            // Only the folder. Caps are not shown here any more, and an unset property on
            // SetEdit means "leave it alone", so a set given a cap from the console keeps it.
            // Sending the cleared values a hidden row would have produced would silently wipe
            // somebody's limit for opening a screen.
            var edited = service.Edit(
                _existing!.Name,
                new SetEdit
                {
                    ClearFolderOverride = _folder is null,
                    FolderOverride = _folder,
                },
                now);

            if (edited.IsRefused)
            {
                Problem = edited.Problem;
                return ScreenCommand.Stay;
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
                Filter = _scope == CatalogScopeKind.Filter
                    ? new CatalogFilter
                    {
                        SearchTerm = string.IsNullOrWhiteSpace(_searchTerm) ? null : _searchTerm,
                        Genres = [.. _facets[FilterFacet.Genres]],
                        Regions = [.. _facets[FilterFacet.Regions]],
                        Languages = [.. _facets[FilterFacet.Languages]],
                        Tags = [.. _facets[FilterFacet.Tags]],
                        Franchises = [.. _facets[FilterFacet.Franchises]],
                        Favorite = _favouritesOnly ? true : null,
                    }
                    : null,
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
            SetsScreens.Detail(_session, added.Set!.Name, null),
            SetsScreens.Resolve(_session, [added.Set], null));
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
