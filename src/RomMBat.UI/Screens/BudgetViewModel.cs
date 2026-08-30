using RomMBat.Core;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// How much of this drive RomMBat may use, and how much it must leave alone.
/// </summary>
/// <remarks>
/// <b>Two settings, and they are a precondition for syncing rather than a preference.</b>
/// Without a budget, <c>evict</c> has nothing to be over and reports that nothing is over it,
/// so the whole eviction half of M3 is inert. 7b-2b puts a sync on screen and a sync that can
/// fill a handheld's drive with no ceiling is not something to ship.
/// <para>
/// <b>Stepped, not typed.</b> Both values are sizes, and a size is exactly the thing that is
/// miserable to enter on a grid of letters. Left and Right move through a ladder; Accept never
/// adjusts.
/// </para>
/// <para>
/// <b>Written straight to <see cref="SettingStore"/>, and no tree lock.</b> These are rows in
/// SQLite, which is in WAL mode. The lock serialises writers of files in the tree, and taking
/// it here would be the speculative acquire that makes a concurrent flush skip its upload.
/// </para>
/// </remarks>
public sealed class BudgetViewModel : IScreen
{
    /// <summary>The ladder for the disk budget. Null is "no budget", which is the state today.</summary>
    private static readonly long?[] Budgets =
    [
        null,
        8L << 30,
        16L << 30,
        32L << 30,
        64L << 30,
        128L << 30,
        256L << 30,
        512L << 30,
        1024L << 30,
        2048L << 30,
    ];

    /// <summary>
    /// The ladder for the free-space floor, which never offers zero.
    /// </summary>
    /// <remarks>
    /// A floor of nothing is a Windows install with no room to write a save, and the failure
    /// arrives as a mid-write disk-full error rather than as a refusal anyone can act on. The
    /// default is 2 GB and the ladder starts below it rather than at it, so lowering it is
    /// possible and removing it is not.
    /// </remarks>
    private static readonly long[] Floors =
    [
        512L << 20,
        1L << 30,
        2L << 30,
        4L << 30,
        8L << 30,
        16L << 30,
    ];

    private readonly InstallSession _session;
    private int _budget;
    private int _floor;

    public BudgetViewModel(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;

        var settings = session.Store.Settings;

        _budget = NearestBudget(settings.GetInt64(SettingStore.ContentMaxBytes));
        _floor = NearestFloor(
            settings.GetInt64(SettingStore.FreeSpaceFloorBytes) ?? SettingStore.DefaultFreeSpaceFloorBytes);
    }

    public string Title => "Disk space";

    public int Cursor { get; private set; }

    /// <summary>True once something has been changed and not yet saved.</summary>
    public bool IsDirty { get; private set; }

    public IReadOnlyList<EditorRow> Rows =>
    [
        new EditorRow(
            "Budget",
            Budgets[_budget] is { } cap ? ByteSize.Format(cap) : "no budget",
            "The most space RomMBat's own downloads may take up. Games you put there yourself "
                + "are never counted and never removed.",
            true),
        new EditorRow(
            "Always leave free",
            ByteSize.Format(Floors[_floor]),
            "RomMBat stops downloading before the drive gets this empty, so the system and "
                + "your saves still have somewhere to go.",
            true),
    ];

    public IReadOnlyList<FooterHint> Hints =>
        IsDirty
            ? [new FooterHint(NavAction.Start, "Save"), new FooterHint(NavAction.Back, "Discard")]
            : [new FooterHint(NavAction.Back, "Back")];

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Up:
                Cursor = (Cursor - 1 + Rows.Count) % Rows.Count;
                return ScreenCommand.Stay;

            case NavAction.Down:
                Cursor = (Cursor + 1) % Rows.Count;
                return ScreenCommand.Stay;

            case NavAction.Left:
                Step(-1);
                return ScreenCommand.Stay;

            case NavAction.Right:
                Step(1);
                return ScreenCommand.Stay;

            case NavAction.Start when IsDirty:
                Save();
                return ScreenCommand.Pop;

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    private void Step(int direction)
    {
        if (Cursor == 0)
        {
            _budget = Wrap(_budget + direction, Budgets.Length);
        }
        else
        {
            _floor = Wrap(_floor + direction, Floors.Length);
        }

        IsDirty = true;
    }

    private void Save()
    {
        var now = DateTimeOffset.UtcNow;

        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, Budgets[_budget], now);
        _session.Store.Settings.Set(SettingStore.FreeSpaceFloorBytes, Floors[_floor], now);

        IsDirty = false;
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    private static int NearestBudget(long? value)
    {
        if (value is null)
        {
            return 0;
        }

        var exact = Array.IndexOf(Budgets, value);
        if (exact >= 0)
        {
            return exact;
        }

        // Nearest rung at or below, so opening this screen and saving can never quietly raise
        // a budget somebody set from the console.
        var best = 0;
        for (var index = 1; index < Budgets.Length; index++)
        {
            if (Budgets[index] <= value)
            {
                best = index;
            }
        }

        return best;
    }

    private static int NearestFloor(long value)
    {
        var exact = Array.IndexOf(Floors, value);
        if (exact >= 0)
        {
            return exact;
        }

        // At or above for the floor, which is the safe direction for the one setting whose
        // job is to stop the drive filling.
        for (var index = 0; index < Floors.Length; index++)
        {
            if (Floors[index] >= value)
            {
                return index;
            }
        }

        return Floors.Length - 1;
    }
}
