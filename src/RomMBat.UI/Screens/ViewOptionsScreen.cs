using RomMBat.Core.Sets;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// The library's VIEW OPTIONS, opened by Select as EmulationStation's own are (RB-422).
/// </summary>
/// <remarks>
/// <b>ES's rows, in ES's order</b>: the text search, the jump to a letter stepped with left and
/// right, the sort order and the filters. A popup over the list like the actions menu, and like
/// it every pick closes the popup first and acts on the list, so the keyboard, the sort picker
/// and the filters open over the games they change and backing out of them lands there.
/// <para>
/// <b>Only the letter steps in place.</b> Left and right move through the letters the list
/// actually has, read from RomM's letter index once per view, and the bottom button jumps. Under
/// a sort other than name there is no index, and the row says so rather than vanishing.
/// </para>
/// </remarks>
public sealed class ViewOptionsScreen : IScreen, IWindowedScreen, IPopupScreen, ILiveScreen, IReturnAware, IDisposable
{
    private const string SearchRow = "Search for";
    private const string LetterRow = "Jump to letter";
    private const string SortRow = "Sort by";
    private const string FilterRow = "Filters";

    private readonly BrowseViewModel _browse;
    private readonly ListScreen _list;

    private BrowseLetters? _letters;
    private int _letter = -1;

    public ViewOptionsScreen(BrowseViewModel browse)
    {
        ArgumentNullException.ThrowIfNull(browse);

        _browse = browse;

        _list = new ListScreen(
            browse.Title,
            Rows,
            index => Choose(Rows()[index].Label),
            acceptLabel: "Select",
            backLabel: "Close")
        {
            Verbs = (action, cursor) => action switch
            {
                NavAction.Start or NavAction.Options => ScreenCommand.Pop,
                NavAction.Left or NavAction.Right when OnLetter(cursor) => Step(action == NavAction.Left ? -1 : 1),
                _ => null,
            },
            ExtraHints = () => OnLetter(_list!.Cursor) && Letters.Count > 1
                ? [FooterHint.Move("Choose letter")]
                : [],

            // Enriching rather than Started: every row is right before the letters arrive, and
            // the letter row says it is asking while they are on their way.
            Load = async token =>
            {
                _letters = await browse.LettersAsync(token).ConfigureAwait(false);
                _letter = Under(browse.Position);
                return null;
            },
        }.Enriching();

        _list.Invalidated += (_, _) => Invalidated?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Invalidated;

    IScreen? IPopupScreen.Underneath => _browse;

    /// <summary>The list's own title, so the header does not change under the popup.</summary>
    public string Title => _browse.Title;

    public IReadOnlyList<FooterHint> Hints => _list.Hints;

    IReadOnlyList<ListRow> IWindowedScreen.Rows => _list.Rows;

    public int Cursor => _list.Cursor;

    public ListView Window => _list.Window;

    public bool Reading => false;

    public ScreenCommand Handle(NavAction action) => _list.Handle(action);

    public void Returned() => _list.Returned();

    public void Dispose() => _list.Dispose();

    private IReadOnlyList<BrowseLetter> Letters => _letters?.Letters ?? [];

    private bool Offline => _browse.State.Page?.Source == BrowseSource.ThisDevice;

    private IReadOnlyList<ListRow> Rows()
    {
        var view = _browse.State.View;

        // Said once for both rows that need RomM, in the words the list's own note uses.
        const string NeedsRomM = "RomM's library could not be read, so this device's games are shown by name.";

        return
        [
            new ListRow(SearchRow, view.Search ?? "anything"),
            LetterListRow(view),
            new ListRow(SortRow, BrowseViewModel.OrderText(view.Order), Offline ? NeedsRomM : null, !Offline),
            new ListRow(
                FilterRow,
                view.Filtered ? FilterCount(view) : "none",
                Offline ? NeedsRomM : null,
                !Offline),
        ];
    }

    private ListRow LetterListRow(BrowseView view)
    {
        if (_letters is null)
        {
            return new ListRow(LetterRow, "...", "Asking where each letter begins.", false);
        }

        if (Letters.Count == 0)
        {
            return new ListRow(
                LetterRow,
                null,
                !view.ByName && !Offline
                    ? "Sorted by something other than name, so there are no letters to jump between."
                    : _letters.Problem ?? "Nothing to jump between.",
                false);
        }

        return new ListRow(LetterRow, $"‹ {Letters[Math.Max(0, _letter)].Label} ›");
    }

    private static bool OnLetter(int cursor) => cursor == 1;

    /// <summary>The letter the row under the cursor sits under, so the stepper starts where the list is.</summary>
    private int Under(int position)
    {
        var letters = _letters?.Letters ?? [];
        var under = -1;

        for (var index = 0; index < letters.Count; index++)
        {
            if (letters[index].Offset <= position)
            {
                under = index;
            }
        }

        return letters.Count == 0 ? -1 : Math.Max(0, under);
    }

    /// <summary>Left and right through the letters, wrapping, as ES's stepper does.</summary>
    private ScreenCommand Step(int direction)
    {
        if (Letters.Count > 0)
        {
            _letter = (Math.Max(0, _letter) + direction + Letters.Count) % Letters.Count;
        }

        return ScreenCommand.Stay;
    }

    private ScreenCommand Choose(string label) => label switch
    {
        SearchRow => ScreenCommand.Dismiss(() => ScreenCommand.Push(_browse.SearchKeyboard())),

        LetterRow when Letters.Count > 0 => ScreenCommand.Dismiss(() =>
        {
            _browse.JumpTo(Letters[Math.Max(0, _letter)]);
            return ScreenCommand.Stay;
        }),

        SortRow => ScreenCommand.Dismiss(() => ScreenCommand.Push(SortPicker(_browse))),

        FilterRow => ScreenCommand.Dismiss(() => ScreenCommand.Push(Filters(_browse))),

        _ => ScreenCommand.Stay,
    };

    private static string FilterCount(BrowseView view) =>
        string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{FilterChoices.CountOf(view.Filter)} set");

    /// <summary>
    /// The orders on offer, the current one marked, and a pick that reads the list again.
    /// </summary>
    internal static ListScreen SortPicker(BrowseViewModel browse)
    {
        BrowseOrder[] orders =
        [
            BrowseOrder.NameAscending,
            BrowseOrder.NameDescending,
            BrowseOrder.ReleaseNewest,
            BrowseOrder.ReleaseOldest,
            BrowseOrder.RatingHighest,
        ];

        var current = browse.State.View.Order;

        return new ListScreen(
            "Sort games by",
            [.. orders.Select(order => new ListRow(BrowseViewModel.OrderText(order), order == current ? "chosen" : null))],
            index =>
            {
                browse.ApplyOrder(orders[index]);
                return ScreenCommand.Pop;
            },
            acceptLabel: "Sort by this");
    }

    /// <summary>
    /// RomM's facets and properties, picked as a filter set picks them, and applied from the last row.
    /// </summary>
    /// <remarks>
    /// <b>The bottom row applies and the right button discards</b>, which is every editor here:
    /// back never commits, and leaving with changes asks first.
    /// </remarks>
    internal static ListScreen Filters(BrowseViewModel browse)
    {
        const string ClearRow = "Clear every filter";
        const string ApplyRow = "Show these games";

        var choices = new FilterChoices(browse.Session, browse.State.View.Filter, browse.Connect)
        {
            Values = browse.FacetValues,

            // Kept for the next visit, whichever way this one ends.
            Fetched = values => browse.FacetValues = values,
        };

        var opened = choices.Snapshot();
        ListScreen? screen = null;

        IReadOnlyList<ListRow> Rows() =>
        [
            .. choices.Rows().Select(row => new ListRow(row.Label, row.Value, row.Detail)),
            .. choices.IsEmpty ? [] : new[] { new ListRow(ClearRow) },
            new ListRow(ApplyRow, null, choices.IsEmpty ? "Nothing chosen, so every game is shown." : null),
        ];

        screen = new ListScreen(
            "Filters",
            Rows,
            index =>
            {
                var label = Rows()[index].Label;

                switch (label)
                {
                    case ApplyRow:
                        browse.ApplyFilter(choices.IsEmpty ? null : choices.Build());
                        return ScreenCommand.Pop;

                    case ClearRow:
                        choices.Clear();
                        return ScreenCommand.Stay;

                    default:
                        return choices.Open(label);
                }
            },
            acceptLabel: "Change",
            backLabel: "Cancel")
        {
            Note = () => "Games matching every row here, as RomM's own filters combine them.",
            OnBack = () => choices.Snapshot() == opened
                ? ScreenCommand.Pop
                : ScreenCommand.Push(ConfirmScreen.YesNo(
                    "Discard your changes?",
                    "Discard",
                    () => ScreenCommand.PopMany(2),
                    "Keep editing",
                    screen)),
        };

        return screen;
    }
}
