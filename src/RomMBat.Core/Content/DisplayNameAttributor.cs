using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>
/// Works out which ROM a battery save belongs to when the emulator named it after its own title
/// for the game rather than after the ROM file.
/// </summary>
/// <remarks>
/// <para>
/// <b>BizHawk is the measured case, and no rule over the ROM name recovers its title.</b>
/// <c>StarTropics (USA).zip</c> wrote <c>bizhawk/StarTropics.SaveRAM</c>, and
/// <c>Phantasy Star (Brazil).zip</c> wrote <c>Phantasy Star (B).SaveRAM</c>, so the title is
/// neither the stem nor the stem with its tags stripped. Two routes join it back, and both are
/// asked, for the reason <see cref="GameIdAttributor"/> asks every route:
/// <list type="number">
/// <item><b>The save-state sidecar.</b> RetroBat writes <c>&lt;title&gt;.&lt;core&gt;</c> beside
/// every BizHawk state, <c>StarTropics.NesHawk</c> beside
/// <c>StarTropics (USA).QuickSave0.State</c>, which is the title already joined to a ROM file.
/// It covers only games with a state.</item>
/// <item><b>The launch window.</b> The newest launch of the system under the same emulator at or
/// before the save's mtime, which covers a game played without ever saving a state.</item>
/// </list>
/// </para>
/// <para>
/// <b>The key is the file name</b>, <c>StarTropics.SaveRAM</c>, which is what a person types into
/// <c>saves bind</c> and what the title is recovered from for a download.
/// </para>
/// <para>
/// <b>The cached binding is an answer here, not a short cut.</b> A title is not unique to a ROM:
/// BizHawk gives <c>StarTropics (Europe)</c> and <c>StarTropics (USA)</c> one title, and driven,
/// the Europe copy read the USA copy's progress and wrote over it in one file. So every scan asks the routes again and a launch of the second ROM
/// disagrees with the binding the first one taught, which fails closed and is cached as
/// contested until <c>saves bind</c> settles it. Short-cutting on the cache would keep uploading
/// the shared file under whichever ROM was played first. A binding a person made is the one
/// exception, because that is the settlement.
/// </para>
/// </remarks>
public sealed class DisplayNameAttributor
{
    private readonly LocalStore _store;
    private readonly IReadOnlyList<LaunchRecord> _launches;
    private readonly RomIndex _roms;
    private readonly TimeProvider _time;
    private readonly Dictionary<(string System, string Emulator), Dictionary<string, List<RouteAnswer>>> _sidecars = [];

    public DisplayNameAttributor(
        LocalStore store,
        RomIndex roms,
        IReadOnlyList<LaunchRecord> launches,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(launches);

        _store = store;
        _roms = roms;
        _launches = launches;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Attributes one save, whose file name is the rule's emulator's title for the game.</summary>
    /// <param name="fileName">The file's name, <c>StarTropics.SaveRAM</c>, which is also the key.</param>
    /// <param name="written">
    /// The save's mtime, which the launch-window route needs, or null when the file has not
    /// changed since it was last attributed and so its mtime is not evidence of a new write.
    /// </param>
    public Attribution Attribute(string system, BatteryRule rule, string fileName, DateTimeOffset? written)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var key = fileName;
        var title = Path.GetFileNameWithoutExtension(fileName);
        var cached = _store.GameIdBindings.Find(system, key);

        if (cached is { LearnedFrom: BindingSource.User, RomId: not null })
        {
            return new Attribution(cached.RomId, cached.RomPath, BindingSource.User, cached.Detail ?? "bound with saves bind");
        }

        if (cached is { IsResolved: false })
        {
            return new Attribution(
                null,
                null,
                null,
                cached.Detail ?? $"nothing could attribute {key}",
                AttributionOutcome.Contested);
        }

        var answers = new List<RouteAnswer>(FromSidecar(system, rule, title));

        if (written is { } at
            && GameIdAttributor.CoveringLaunch(_launches, system, rule.Emulator, at) is { } launch
            && !EndedBefore(launch, at)
            && _roms.Find(system, Path.GetFileNameWithoutExtension(launch.RomPath!.Value.Value)) is { } launched)
        {
            answers.Add(new RouteAnswer(
                BindingSource.Journal,
                launched.RomId,
                launched.Path,
                $"{launched.Path.Name} was running under {rule.Emulator} when {key} was last written "
                    + $"({launch.At:u} against {at:u})"));
        }

        if (cached is { RomId: { } boundId, RomPath: { } boundPath })
        {
            answers.Add(new RouteAnswer(cached.LearnedFrom, boundId, boundPath, cached.Detail ?? $"{key} was bound to {boundPath.Name}"));
        }

        if (answers.Count == 0)
        {
            // Not cached, for GameIdAttributor's reason: an absence usually means the game has
            // not been played or given a state here yet, and a cached refusal would outlive it.
            return new Attribution(
                null,
                null,
                null,
                $"{rule.Emulator} names this save after its own title for the game, not the ROM "
                    + $"file, and nothing on this device joins '{title}' to a ROM yet: no "
                    + $"{rule.Emulator} save state names it and no {rule.Emulator} launch covers "
                    + "when it was written",
                AttributionOutcome.NotFound);
        }

        var now = _time.GetUtcNow();

        if (answers.Select(answer => answer.RomId).Distinct().Count() > 1)
        {
            var reason =
                $"more than one game answers to {key}: "
                    + string.Join("; ", answers.Select(answer => answer.Detail).Distinct(StringComparer.Ordinal))
                    + $". {rule.Emulator} keeps one file for every ROM it gives that title, so it is "
                    + "not uploaded under either until `saves bind` settles it";

            Remember(system, key, null, null, BindingSource.Contested, reason, now);
            return new Attribution(null, null, null, reason, AttributionOutcome.Contested);
        }

        var agreed = answers[0];

        // Rewritten only when something changed, so learned_at keeps saying when it was learned.
        if (cached is null)
        {
            Remember(system, key, agreed.RomId, agreed.RomPath, agreed.Source, agreed.Detail, now);
        }

        return new Attribution(agreed.RomId, agreed.RomPath, agreed.Source, agreed.Detail);
    }

    /// <summary>
    /// True when another launch started between <paramref name="launch"/> and the write, so the
    /// session it began was over before the file was written.
    /// </summary>
    /// <remarks>
    /// EmulationStation runs one game at a time, so any later launch, of any system under any
    /// emulator, ends the one before it. Without this bound the newest BizHawk launch claims every
    /// later write to the file, however long after: measured on a real install, a restore rewrote
    /// <c>StarTropics.SaveRAM</c> on 2026-09-21 and the route credited it to an Ultima session
    /// from 2026-09-13, which contested the sidecar's correct answer. The two-second band is the
    /// filesystem's, as in <see cref="GameIdAttributor.LaunchAmbiguity"/>.
    /// </remarks>
    private bool EndedBefore(LaunchRecord launch, DateTimeOffset written) =>
        _launches.Any(other =>
            other.At > launch.At
            && other.At < written - GameIdAttributor.LaunchAmbiguity);

    /// <summary>
    /// The title a rule's saves are named with for a ROM, when this device has learned one.
    /// </summary>
    /// <remarks>
    /// The download half of the join, and it can only use what was learned: the title cannot be
    /// derived from the ROM name, so a device that has never run the game under this emulator
    /// has nowhere to put the save. Two titles bound to one ROM is refused rather than chosen
    /// between, because only one of them is the file the emulator reads.
    /// </remarks>
    public static string? LearnedTitle(LocalStore store, string system, BatteryRule rule, long romId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rule);

        var titles = store.GameIdBindings
            .List()
            .Where(binding =>
                binding.RomId == romId
                && string.Equals(binding.System, system, StringComparison.OrdinalIgnoreCase)
                && rule.IsBindingKey(binding.GameId))
            .Select(binding => Path.GetFileNameWithoutExtension(binding.GameId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return titles.Count == 1 ? titles[0] : null;
    }

    /// <summary>
    /// Every ROM whose save state's sidecar gives it this title.
    /// </summary>
    /// <remarks>
    /// All of them, not the first. <see cref="GameIdAttributor"/>'s sidecar index is first-wins
    /// because two ROMs sharing a game code are a revision pair, either as good as the other; two
    /// ROMs sharing a title here share a file, and taking one would upload a save the other also
    /// writes. The core is stripped only where it is the state's own, because a title can hold a
    /// dot of its own (<c>Dr. Mario</c>) and the last segment is not always the core.
    /// </remarks>
    private List<RouteAnswer> FromSidecar(string system, BatteryRule rule, string title)
    {
        if (!_sidecars.TryGetValue((system, rule.Emulator), out var byTitle))
        {
            byTitle = new Dictionary<string, List<RouteAnswer>>(StringComparer.OrdinalIgnoreCase);

            foreach (var state in _store.States.List())
            {
                if (state.NativeName is not { } native
                    || state.RomId is not { } romId
                    || state.RomPath is not { } romPath
                    || state.Core.Length == 0
                    || !native.EndsWith($".{state.Core}", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(state.System, system, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(state.Emulator, rule.Emulator, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var named = native[..^(state.Core.Length + 1)];

                if (!byTitle.TryGetValue(named, out var roms))
                {
                    roms = [];
                    byTitle[named] = roms;
                }

                if (roms.All(answer => answer.RomId != romId))
                {
                    roms.Add(new RouteAnswer(
                        BindingSource.Sidecar,
                        romId,
                        romPath,
                        $"the {rule.Emulator} save state beside {romPath.Name} names {native}"));
                }
            }

            _sidecars[(system, rule.Emulator)] = byTitle;
        }

        return byTitle.TryGetValue(title, out var found) ? found : [];
    }

    private void Remember(
        string system,
        string key,
        long? romId,
        Paths.RelativePath? romPath,
        BindingSource source,
        string detail,
        DateTimeOffset now) =>
        _store.GameIdBindings.Record(new GameIdBinding(system, key, romId, romPath, source, detail, now));
}
