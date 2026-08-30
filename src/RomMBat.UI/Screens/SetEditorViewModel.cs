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
    /// <summary>
    /// The game caps a d-pad steps through.
    /// </summary>
    /// <remarks>
    /// Null is "no cap" and is first, because it is the honest default: a set with no cap and
    /// no byte budget is refused at resolve time only if the scope is enormous, and most
    /// scopes are not.
    /// </remarks>
    private static readonly int?[] GameCaps = [null, 10, 20, 40, 60, 100, 200, 500, 1000];

    /// <summary>
    /// The byte caps a d-pad steps through.
    /// </summary>
    /// <remarks>
    /// Stops at 512 GB because past that the disk budget is the binding constraint and a set
    /// cap is theatre. Powers of two, because that is what a drive is sold as.
    /// </remarks>
    private static readonly long?[] ByteCaps =
    [
        null,
        1L << 30,
        2L << 30,
        4L << 30,
        8L << 30,
        16L << 30,
        32L << 30,
        64L << 30,
        128L << 30,
        256L << 30,
        512L << 30,
    ];

    /// <summary>
    /// The orderings, with the default first so a new set starts on it.
    /// </summary>
    /// <remarks>
    /// Recent leads because that is what a cap should keep. By name, a set of forty keeps
    /// everything beginning with A.
    /// </remarks>
    private static readonly SetOrdering[] Orderings =
    [
        SetOrdering.RecentlyUpdated,
        SetOrdering.Name,
        SetOrdering.SizeAscending,
        SetOrdering.SizeDescending,
    ];

    private readonly InstallSession _session;
    private readonly SyncSetDefinition? _existing;

    private string _name;
    private CatalogScopeKind _scope;
    private string? _platformValue;
    private string? _platformLabel;
    private string? _platformFolder;
    private string? _searchTerm;
    private int _gameCap;
    private int _byteCap;
    private int _ordering;
    private string? _folder;

    private SetEditorViewModel(InstallSession session, SyncSetDefinition? existing)
    {
        _session = session;
        _existing = existing;

        _name = existing?.Name ?? string.Empty;
        _scope = existing?.Scope ?? CatalogScopeKind.Platform;
        _folder = existing?.FolderOverride;
        _gameCap = Nearest(GameCaps, existing?.MaxGames);
        _byteCap = Nearest(ByteCaps, existing?.MaxBytes);
        _ordering = Math.Max(0, Array.IndexOf(Orderings, existing?.Ordering ?? SyncSetStore.DefaultOrdering));

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
                rows.Add(new EditorRow("Name", _name.Length == 0 ? "not set" : _name, null, false));
                rows.Add(new EditorRow("Scope", SyncSetStore.ScopeText(_scope), null, false));

                if (_scope == CatalogScopeKind.Platform)
                {
                    rows.Add(new EditorRow("Platform", _platformLabel ?? "not chosen", null, false));
                }
                else if (_scope == CatalogScopeKind.Filter)
                {
                    rows.Add(new EditorRow(
                        "Search for",
                        string.IsNullOrWhiteSpace(_searchTerm) ? "anything" : _searchTerm,
                        "A filter with nothing set matches the whole library.",
                        false));
                }
            }

            rows.Add(new EditorRow(
                "Most games",
                GameCaps[_gameCap] is { } games ? games.ToString(CultureInfo.CurrentCulture) : "no limit",
                null,
                true));

            rows.Add(new EditorRow(
                "Most space",
                ByteCaps[_byteCap] is { } bytes ? ByteSize.Format(bytes) : "no limit",
                null,
                true));

            rows.Add(new EditorRow(
                "Keep first",
                SyncSetStore.OrderingText(Orderings[_ordering]),
                "Which games a limit keeps when the set is bigger than it.",
                true));

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

            case NavAction.Left when rows[Cursor].Steps:
                Step(rows[Cursor].Label, -1);
                return ScreenCommand.Stay;

            case NavAction.Right when rows[Cursor].Steps:
                Step(rows[Cursor].Label, 1);
                return ScreenCommand.Stay;

            case NavAction.Accept when !rows[Cursor].Steps:
                return Open(rows[Cursor].Label);

            case NavAction.Start:
                return Save();

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    private void Step(string label, int direction)
    {
        switch (label)
        {
            case "Most games":
                _gameCap = Wrap(_gameCap + direction, GameCaps.Length);
                break;

            case "Most space":
                _byteCap = Wrap(_byteCap + direction, ByteCaps.Length);
                break;

            case "Keep first":
                _ordering = Wrap(_ordering + direction, Orderings.Length);
                break;

            default:
                break;
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
                return new TypedResult(null);
            })),

        "Scope" => ScreenCommand.Push(ScopePicker()),

        "Platform" => ScreenCommand.Push(PlatformPicker()),

        "Search for" => ScreenCommand.Push(new OnScreenKeyboard(
            "Search for",
            "Only games whose name matches this.",
            _searchTerm ?? string.Empty,
            typed =>
            {
                _searchTerm = typed.Trim();
                return new TypedResult(null);
            })),

        "Folder" => ScreenCommand.Push(FolderPicker()),

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
                Cursor = 0;
                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this");
    }

    private ListScreen PlatformPicker()
    {
        var platforms = new SyncSetService(_session).PlatformsKnownHere();

        return new ListScreen(
            "Which platform?",
            [.. platforms.Select(option => new ListRow(
                option.Label,
                option.Folder ?? "no folder yet",
                option.Folder is null ? option.Note : null))],
            index =>
            {
                _platformValue = platforms[index].PlatformId.ToString(CultureInfo.InvariantCulture);
                _platformLabel = platforms[index].Label;
                _platformFolder = platforms[index].Folder;
                return ScreenCommand.Pop;
            },
            acceptLabel: "Use this")
        {
            EmptyMessage = "This device has not seen RomM's platform list yet. Sync once, and "
                + "the platforms it knows appear here.",
        };
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
            var edited = service.Edit(
                _existing!.Name,
                new SetEdit
                {
                    ClearMaxGames = GameCaps[_gameCap] is null,
                    MaxGames = GameCaps[_gameCap],
                    ClearMaxBytes = ByteCaps[_byteCap] is null,
                    MaxBytes = ByteCaps[_byteCap],
                    Ordering = Orderings[_ordering],
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
                ScopeValue = _scope == CatalogScopeKind.Platform ? _platformValue : null,
                Filter = _scope == CatalogScopeKind.Filter
                    ? new CatalogFilter { SearchTerm = string.IsNullOrWhiteSpace(_searchTerm) ? null : _searchTerm }
                    : null,
                MaxGames = GameCaps[_gameCap],
                MaxBytes = ByteCaps[_byteCap],
                Ordering = Orderings[_ordering],
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

        return ScreenCommand.Pop;
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    /// <summary>
    /// The step nearest an existing value, so editing a set never silently changes its cap.
    /// </summary>
    /// <remarks>
    /// A set defined from the console can hold any number, and the steps here are a fixed
    /// ladder. Snapping to the nearest rung and saving would move a cap the user did not
    /// touch, so an exact match is used where there is one and the nearest below otherwise,
    /// which can only ever tighten and never quietly loosen a limit.
    /// </remarks>
    private static int Nearest<T>(T?[] ladder, T? value)
        where T : struct, IComparable<T>
    {
        if (value is null)
        {
            return 0;
        }

        var exact = Array.IndexOf(ladder, value);
        if (exact >= 0)
        {
            return exact;
        }

        var best = 0;
        for (var index = 1; index < ladder.Length; index++)
        {
            if (ladder[index] is { } rung && rung.CompareTo(value.Value) <= 0)
            {
                best = index;
            }
        }

        return best;
    }
}
