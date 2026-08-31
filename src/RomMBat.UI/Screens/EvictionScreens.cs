using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// Freeing space, showing what would go before anything goes.
/// </summary>
/// <remarks>
/// <b>The preview is not a flag here, it is the screen.</b> <c>evict</c> previews by default and
/// writes on <c>--apply</c>; on a gamepad the equivalent is that opening this shows what would
/// happen and one confirmation carries it out. "The dry run" is not the word for it either:
/// hyphenated <c>dry-run</c> names <c>sync</c>'s flag and nothing else.
/// <para>
/// <b>What is kept, and why, sits beside what goes.</b> <see cref="EvictionPlan.Refused"/>
/// carries <c>SaveGuard</c>'s refusals, and a person freeing space from a sofa has no other way
/// to learn that a game was held back because its saves are not up yet. Leaving them off would
/// make the screen say a smaller number than the library can free and never say why.
/// </para>
/// <para>
/// <b>Dead transfers are their own line, reclaimed even when nothing is over budget.</b> Those
/// bytes carry no <c>local_file</c> row, so the budget cannot see them and free space has
/// already lost them: an install inside its budget with a dead transfer under <c>partial/</c>
/// has nothing to evict and space to reclaim.
/// </para>
/// <para>
/// <b>Every sentence naming a reason is Core's.</b> <see cref="EvictionService.Describe(EvictionCandidate)"/>
/// and its overload already word both candidate kinds, and they are quoted rather than
/// reworded, because a second wording is a second thing to keep true.
/// </para>
/// <para>
/// <b>This whole surface works with the server switched off.</b> Nothing here touches the
/// network: the preview is two local scans and a walk of <c>local_file</c>, and carrying it out
/// deletes files and rewrites gamelists from local state.
/// </para>
/// </remarks>
public static class EvictionScreens
{
    /// <summary>What would go, and the one press that carries it out.</summary>
    public static IScreen Preview(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var service = new EvictionService(session);
        var report = service.Preview();

        IReadOnlyList<ListRow> Rows()
        {
            report = service.Preview();
            return PreviewRows(report);
        }

        return new ListScreen(
            "Free up space",
            Rows,
            _ => ScreenCommand.Stay,
            acceptLabel: "Free this space",
            backLabel: "Leave everything",
            new FooterHint(NavAction.Alternate, "Disk space"))
        {
            // Every row is a fact rather than a choice, so the cursor has nowhere to sit and
            // the accept hint would be suppressed while the verb went on handling the press.
            // Same defect the set detail screen had: the action worked and the footer never
            // said so.
            AlwaysOfferAccept = true,
            EmptyMessage = "Nothing to free. RomMBat is inside its budget and there are no "
                + "abandoned transfers to clear up.",
            Note = () => Headline(report),
            Verbs = (action, _) => action switch
            {
                NavAction.Accept when !report.IsEmpty => ScreenCommand.Push(Confirm(session, report)),
                NavAction.Alternate => ScreenCommand.Push(new BudgetViewModel(session)),
                _ => null,
            },
        };
    }

    /// <summary>The sentence above the rows, which says whether there is anything to do at all.</summary>
    private static string Headline(EvictionReport report)
    {
        if (report.IsEmpty)
        {
            return !report.HasBudget
                ? "No limit is set on what RomMBat may use, so nothing is over it. Set one under disk space."
                : "RomMBat is inside its limit.";
        }

        if (report.Plan.BytesToFree <= 0)
        {
            return "Nothing is over the limit. There are abandoned transfers to clear up.";
        }

        return report.Plan.IsShort
            ? $"{ByteSize.Format(report.Plan.BytesToFree)} has to go, and only "
                + $"{ByteSize.Format(report.Plan.BytesFreed)} can be freed."
            : $"{ByteSize.Format(report.Plan.BytesToFree)} has to go.";
    }

    private static List<ListRow> PreviewRows(EvictionReport report)
    {
        var rows = new List<ListRow>();

        foreach (var candidate in report.Plan.Selected)
        {
            rows.Add(new ListRow(
                candidate.File.FileName,
                ByteSize.Format(candidate.Bytes),
                Detail(candidate),
                false));
        }

        // Their own line, and above nothing else, because they are reclaimed whether or not
        // anything is over budget.
        foreach (var candidate in report.Abandoned.Candidates)
        {
            rows.Add(new ListRow(
                candidate.Name,
                ByteSize.Format(candidate.SizeBytes),
                $"Abandoned transfer, {EvictionService.Describe(candidate)}.",
                false));
        }

        // Quoted from SaveGuard through Core's own wording. Without these the screen shows a
        // smaller number than the library holds and never says why.
        foreach (var candidate in report.Plan.Refused)
        {
            rows.Add(new ListRow(
                candidate.File.FileName,
                "kept",
                candidate.Refusal,
                false));
        }

        return rows;
    }

    /// <summary>Why a game is a candidate, and what goes out with it.</summary>
    private static string Detail(EvictionCandidate candidate)
    {
        var reason = EvictionService.Describe(candidate);

        // A game whose ROM is 128 KB can be carrying 3 MB of artwork out with it, which is the
        // sentence `evict` has always printed and the one that explains a surprising number.
        return candidate.Media.Count == 0
            ? $"{reason}. The game only."
            : string.Create(
                CultureInfo.CurrentCulture,
                $"{reason}. The game and its {candidate.Media.Count} artwork files.");
    }

    /// <summary>
    /// The one confirmation, saying what goes and what does not.
    /// </summary>
    /// <remarks>
    /// The sentence matters more than the press. What a person needs to know before freeing
    /// space is that their saves are already up and that nothing they put there themselves is
    /// touched, and there is nowhere else on a gamepad to find that out.
    /// </remarks>
    public static IScreen Confirm(InstallSession session, EvictionReport report)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(report);

        var freed = report.Plan.BytesFreed + report.Abandoned.BytesToFree;

        return new ListScreen(
            "Free up space?",
            [
                new ListRow(
                    $"Free {ByteSize.Format(freed)}",
                    null,
                    "The games listed are removed from this device and stay in your RomM library. "
                        + "Games you put here yourself are never touched, and nothing is removed "
                        + "while its saves are still waiting to go up."),
            ],
            _ => ScreenCommand.Push(new EvictionRunViewModel(session, report)),
            acceptLabel: "Free this space",
            backLabel: "Keep everything");
    }
}

/// <summary>
/// Carrying out an eviction, which writes and therefore is not instant.
/// </summary>
/// <remarks>
/// <b>Its own screen because it rewrites gamelists, which talks to EmulationStation.</b>
/// Deleting the files is quick; the gamelist pass afterwards is the part with a network client
/// in it, and a confirmation screen that froze while it ran would be the hung-screen problem
/// the resolve screen was shaped to avoid.
/// <para>
/// <b>Not cancellable, deliberately.</b> A half-carried-out eviction is a set of gone files and
/// a set of gamelists still naming them, and there is nothing to resume: the work is seconds
/// and the press was already confirmed. Offering to stop would promise a rollback that does not
/// exist.
/// </para>
/// </remarks>
public sealed class EvictionRunViewModel : IScreen, ILiveScreen
{
    private readonly EvictionReport _report;

    private volatile string _detail = "Freeing space.";
    private volatile string? _outcome;

    public EvictionRunViewModel(InstallSession session, EvictionReport report)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(report);

        _report = report;
        Task.Run(() => RunAsync(session), CancellationToken.None);
    }

    public event EventHandler? Invalidated;

    public string Title => "Free up space";

    /// <summary>The sentence under the title. Always set.</summary>
    public string Detail => _detail;

    /// <summary>True once there is nothing left to wait for.</summary>
    public bool IsDone => _outcome is not null;

    public IReadOnlyList<FooterHint> Hints => IsDone
        ? [new FooterHint(NavAction.Back, "Back")]
        : [];

    public ScreenCommand Handle(NavAction action) => action switch
    {
        // Back only once it is over, and then it closes the confirmation underneath as well:
        // that screen offers to free space that has already gone.
        NavAction.Back when IsDone => ScreenCommand.PopMany(2),
        _ => ScreenCommand.Stay,
    };

    private async Task RunAsync(InstallSession session)
    {
        try
        {
            var applied = await new EvictionService(session)
                .ApplyAsync(_report, CancellationToken.None)
                .ConfigureAwait(false);

            _outcome = Describe(applied);
            _detail = _outcome;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows refuses a file operation two ways and only one of them is an
            // IOException. Either way a partly-freed install is a correct install: the rows
            // went with the bytes, and the next pass sees what is left.
            _outcome = $"Some of it could not be removed: {ex.Message}";
            _detail = _outcome;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What actually happened, in Core's own words wherever it has them.</summary>
    private static string Describe(EvictionApplied applied)
    {
        var parts = new List<string>();

        if (applied.Evicted is { } evicted)
        {
            parts.Add(evicted.Summary);
        }

        // Its own sentence, including the ordinary one where another pass held the tree lock
        // and partial/ was left for next time.
        if (applied.Swept is { } swept)
        {
            parts.Add(swept.Summary);
        }

        return parts.Count == 0 ? "Nothing needed removing." : string.Join(". ", parts) + ".";
    }
}
