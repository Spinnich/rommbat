using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// RomM's facets and yes-or-no properties as a person picks them, for any screen that filters.
/// </summary>
/// <remarks>
/// <b>One copy, shared by the set editor and the library's view options.</b> A filter set and a
/// filtered browse ask the same question of the same endpoint, so they offer the same pickers
/// and say the same words; two copies would drift the first time one of them was fixed.
/// <para>
/// Kept as sets rather than as a <see cref="CatalogFilter"/> so a picker can toggle one value
/// without rebuilding the record, and turned into the filter by <see cref="Build"/>.
/// </para>
/// </remarks>
public sealed class FilterChoices
{
    private readonly InstallSession _session;
    private readonly Func<Uri, RomMConnection>? _connect;

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
    /// Null is "do not filter on this", which is not the same as false. A two-state toggle
    /// could only ever say yes or nothing; RomM's own interface offers all three, and "games I
    /// have not favorited" is a real thing to sync.
    /// </remarks>
    private readonly Dictionary<string, bool?> _properties =
        FilterFacet.Properties.ToDictionary(property => property, _ => (bool?)null, StringComparer.Ordinal);

    /// <param name="connect">How the values are fetched, so a test can stand a stub in its place.</param>
    public FilterChoices(InstallSession session, CatalogFilter? from = null, Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _connect = connect;

        if (from is null)
        {
            return;
        }

        foreach (var facet in FilterFacet.Multi)
        {
            var key = FilterFacet.KeyOf(facet);
            _facets[facet].UnionWith(from.ValuesFor(key));
            _logic[facet] = from.LogicFor(key);
        }

        foreach (var property in FilterFacet.Properties)
        {
            _properties[property] = from.Property(FilterFacet.KeyOf(property));
        }
    }

    /// <summary>The facet values this library offers, fetched once when a picker first opens.</summary>
    /// <remarks>
    /// Settable so a test can seed it. Every screen here is drivable with no window and no
    /// controller, and the facet pickers were the one exception: their rows come from the
    /// network, so without this the operator row could only be checked by hand.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Values { get; set; }

    /// <summary>Told the values once a picker has fetched them, so a caller can keep them.</summary>
    /// <remarks>
    /// Called when the fetch lands, because a picker is pushed before its fetch finishes and
    /// <see cref="Values"/> read on the press that opened it is still empty.
    /// </remarks>
    public Action<IReadOnlyDictionary<string, IReadOnlyList<string>>>? Fetched { get; init; }

    /// <summary>True when nothing is chosen, which matches every game.</summary>
    public bool IsEmpty =>
        _facets.Values.All(chosen => chosen.Count == 0)
        && _properties.Values.All(value => value is null);

    /// <summary>How many facets and properties a filter sets, for a row that summarizes it.</summary>
    public static int CountOf(CatalogFilter? filter) =>
        filter is null
            ? 0
            : FilterFacet.Multi.Count(facet => filter.ValuesFor(FilterFacet.KeyOf(facet)).Count > 0)
                + FilterFacet.Properties.Count(property => filter.Property(FilterFacet.KeyOf(property)) is not null);

    /// <summary>Every choice as text, for an editor's "unsaved" check.</summary>
    public string Snapshot() =>
        string.Join(
            '|',
            [
                .. FilterFacet.Multi.Select(facet =>
                    $"{facet}:{_logic[facet]}:{string.Join(',', _facets[facet].Order(StringComparer.Ordinal))}"),
                .. FilterFacet.Properties.Select(property => $"{property}:{_properties[property]}"),
            ]);

    /// <summary>
    /// The rows a filter screen shows, one per facet and per property.
    /// </summary>
    /// <remarks>
    /// A facet this library has no values for is left out: a picker that opens on an empty list
    /// is a row that goes nowhere. Four properties are answered from RomM's own bookkeeping
    /// rather than from the game, so a filter carrying one resolves differently on another
    /// account or after a scan, and the row says so rather than the documentation saying it
    /// somewhere nobody is looking.
    /// </remarks>
    public IEnumerable<(string Label, string Value, string? Detail)> Rows()
    {
        foreach (var facet in FilterFacet.Multi)
        {
            if (Values is { } values
                && values.TryGetValue(facet, out var available)
                && available.Count == 0
                && _facets[facet].Count == 0)
            {
                continue;
            }

            yield return (facet, Describe(facet), null);
        }

        foreach (var property in FilterFacet.Properties)
        {
            yield return (
                property,
                _properties[property] switch { true => "yes", false => "no", _ => "either" },
                _properties[property] is not null && FilterFacet.DependOnTheServer.Contains(property)
                    ? "RomM answers this from its own records, so this can match differently on "
                        + "another account or after a scan."
                    : null);
        }
    }

    /// <summary>True when the label is one of this class's rows.</summary>
    public static bool Owns(string label) =>
        FilterFacet.Multi.Contains(label) || FilterFacet.Properties.Contains(label);

    /// <summary>
    /// What pressing one of <see cref="Rows"/> does: a picker for a facet, a step for a property.
    /// </summary>
    public ScreenCommand Open(string label)
    {
        if (FilterFacet.Properties.Contains(label))
        {
            Cycle(label);
            return ScreenCommand.Stay;
        }

        return FilterFacet.Multi.Contains(label)
            ? ScreenCommand.Push(Picker(label))
            : ScreenCommand.Stay;
    }

    /// <summary>Clears every facet and property.</summary>
    public void Clear()
    {
        foreach (var facet in FilterFacet.Multi)
        {
            _facets[facet].Clear();
            _logic[facet] = FilterLogic.Any;
        }

        foreach (var property in FilterFacet.Properties)
        {
            _properties[property] = null;
        }
    }

    /// <summary>
    /// Everything chosen, as one record.
    /// </summary>
    /// <remarks>
    /// Driven off <see cref="FilterFacet"/>'s own lists rather than naming each field, so a
    /// facet added there reaches storage without a second edit here. The logic operator is
    /// written only where it is not the default, which keeps a plain filter's stored JSON as
    /// small as it was and lets the default move later.
    /// </remarks>
    public CatalogFilter Build(string? searchTerm = null)
    {
        var filter = new CatalogFilter
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm,
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

    /// <summary>Either, then yes, then no, then either again.</summary>
    /// <remarks>
    /// In that order because "either" is where the row starts and where a person undoing a
    /// choice wants to get back to, and two presses is the whole way round.
    /// </remarks>
    private void Cycle(string property)
    {
        _properties[property] = _properties[property] switch
        {
            null => true,
            true => false,
            false => null,
        };
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
    public ListScreen Picker(string facet)
    {
        var chosen = _facets[facet];
        IReadOnlyList<string> available =
            Values is { } known && known.TryGetValue(facet, out var seeded) ? seeded : [];

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

            // Fetched once for every picker this holds. Opening a second facet is instant.
            Load = Values is not null ? null : async token =>
            {
                using var connection = UiConnection.Open(_session, _connect);

                if (connection is null)
                {
                    return _session.Authenticate().Problem ?? "This install is not paired with a RomM server.";
                }

                Values = await new CatalogScopeService(connection)
                    .ListFilterValuesAsync(token)
                    .ConfigureAwait(false);

                Fetched?.Invoke(Values);

                available = Values.TryGetValue(facet, out var loaded) ? loaded : [];
                return null;
            },
        }.Started();
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
}
