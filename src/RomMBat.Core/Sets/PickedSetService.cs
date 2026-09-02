using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>What picking a game did, and the member row it wrote.</summary>
/// <param name="Member">
/// Null when the game could not join the set, which is not a failure: an unmapped platform or a
/// format the folder cannot launch are facts about the library that a pick has to report rather
/// than hide.
/// </param>
/// <param name="Problem">Why there is no member, or null. Never a remedy: see <see cref="SyncSetService"/>.</param>
/// <param name="AlreadyPicked">True when the set already held this game, so nothing changed.</param>
public sealed record PickOutcome(
    SyncSetDefinition Set,
    SyncSetMember? Member,
    string? Problem = null,
    bool AlreadyPicked = false)
{
    public bool IsRefused => Member is null;
}

/// <summary>
/// The one hand-picked set, and putting a game into it.
/// </summary>
/// <remarks>
/// <b>A hand-picked set is a set.</b> It is listed, synced, evicted, roamed, renamed and deleted
/// exactly like the other five kinds, and nothing here is special-cased anywhere those things
/// happen. What is different is only how its membership is arrived at: a person presses once on
/// a browse row instead of a scope being walked.
/// <para>
/// <b>Nothing offers to make a second one and nothing in the schema forbids one.</b> The set is
/// found by kind rather than by name, so renaming it does not lose it, and a second picked set
/// arriving from another device roams in and behaves like any other set.
/// </para>
/// <para>
/// <b>There is nothing to resolve on the device that did the picking.</b> The browse page
/// already carries <c>fs_name</c>, <c>fs_extension</c>, <c>fs_size_bytes</c>, the hashes,
/// <c>has_multiple_files</c>, both platform slugs and the sort key, which is every field
/// <c>sync_set_member</c> wants, so the row is written from the <see cref="RomRow"/> in hand.
/// The resolve path exists only for a device the set roams to, where the ids arrive with no rows
/// behind them.
/// </para>
/// <para>
/// <b>The same exclusion rules a resolve applies are applied here</b>, and by the same code
/// where it exists: an unmapped platform, a format the folder cannot launch, a multi-file ROM
/// and a file this filesystem cannot hold are all refusals with the sentence that states them.
/// A pick that silently wrote a member the sync would then skip would be a press that appears
/// to work and never produces a game.
/// </para>
/// </remarks>
public sealed class PickedSetService
{
    private readonly InstallSession _session;

    public PickedSetService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// What a new picked set is called on this device.
    /// </summary>
    /// <remarks>
    /// <b>Fixed rather than typed, and per device rather than constant.</b> Fixed keeps the
    /// commonest path to one press, where a keyboard on the first pick would put the interface's
    /// slowest interaction at its least welcome moment. Per device is what stops two devices
    /// picking into one RomM account from roaming two sets of the same name at each other, which
    /// a bare constant does: <c>sync_set.name</c> is UNIQUE, so the second would collide.
    /// <para>
    /// The device name is the one RomM knows this device by. An install that has never paired
    /// has none, and falls back to the machine name, which is what pairing would have sent.
    /// </para>
    /// </remarks>
    public string DefaultName()
    {
        var device = _session.Store.Device.Read()?.DeviceName;

        return string.IsNullOrWhiteSpace(device)
            ? $"Picked on {Environment.MachineName}"
            : $"Picked on {device}";
    }

    /// <summary>
    /// The picked set, or null when nothing has been picked yet.
    /// </summary>
    /// <remarks>
    /// Found by kind, so renaming it keeps it. Ordered by name and taking the first, because
    /// nothing forbids a second one arriving from another device and a screen has to be
    /// deterministic about which it writes to.
    /// </remarks>
    public SyncSetDefinition? Find() =>
        _session.Store.SyncSets.List()
            .Where(set => set.Scope == CatalogScopeKind.Picked)
            .OrderBy(set => set.Name, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>The rom ids this device has picked.</summary>
    public IReadOnlyList<int> Picks() =>
        Find() is { } set ? PickedScopeJson.Parse(set.ScopeValue) : [];

    /// <summary>
    /// Puts one game into the picked set, creating the set on the first pick.
    /// </summary>
    /// <remarks>
    /// <b>The pick and the member row are written together</b>, because they are one fact said
    /// twice: <c>scope_value</c> is the definition and <c>sync_set_member</c> is what a sync
    /// reads. A pick that wrote one without the other would be a set whose membership disagreed
    /// with its own scope, which no resolve on this device would ever correct.
    /// </remarks>
    public PickOutcome Pick(RomRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);

        var set = Find();

        if (set is null)
        {
            var created = new SyncSetService(_session).Add(
                new SetDraft
                {
                    Name = DefaultName(),
                    Scope = CatalogScopeKind.Picked,
                    ScopeValue = PickedScopeJson.Write([]),
                },
                now);

            if (created.Set is null)
            {
                return new PickOutcome(
                    new SyncSetDefinition
                    {
                        Name = DefaultName(),
                        Scope = CatalogScopeKind.Picked,
                        ScopeValue = PickedScopeJson.Write([]),
                    },
                    null,
                    created.Problem);
            }

            set = created.Set;
        }

        var picks = PickedScopeJson.Parse(set.ScopeValue);
        var already = picks.Contains(row.Id);

        var member = MemberFor(set, row, now);

        if (member.Member is null)
        {
            return member with { Set = set };
        }

        if (!already)
        {
            _session.Store.SyncSets.UpdatePicks(set.Id, PickedScopeJson.Write([.. picks, row.Id]), now);
        }

        _session.Store.SyncSets.UpsertMember(set.Id, member.Member, now);

        return new PickOutcome(
            _session.Store.SyncSets.Find(set.Name) ?? set,
            member.Member,
            null,
            already);
    }

    /// <summary>Takes one game back out of the picked set, membership and all.</summary>
    /// <remarks>
    /// The definition and the membership go together for the same reason they arrive together.
    /// Whether the file goes with them is <see cref="EvictionService"/>'s answer, not this one's:
    /// another set may still claim it.
    /// </remarks>
    public SyncSetDefinition? Unpick(int romId, DateTimeOffset now)
    {
        if (Find() is not { } set)
        {
            return null;
        }

        var remaining = PickedScopeJson.Parse(set.ScopeValue).Where(id => id != romId).ToList();

        _session.Store.SyncSets.UpdatePicks(set.Id, PickedScopeJson.Write(remaining), now);
        _session.Store.SyncSets.RemoveMember(set.Id, romId);

        return _session.Store.SyncSets.Find(set.Name);
    }

    /// <summary>
    /// Turns a browse row into a member row, or says why it cannot be one.
    /// </summary>
    /// <remarks>
    /// The checks are the ones <c>SetResolver</c> applies and in its order, because a pick that
    /// wrote a member the sync would then skip is a press that appears to work and produces
    /// nothing. Where the answer lives in Core already, it is asked rather than repeated:
    /// <c>PlatformResolver</c> answers the folder and <c>EsSystemsFile</c> the extension.
    /// </remarks>
    private PickOutcome MemberFor(SyncSetDefinition set, RomRow row, DateTimeOffset now)
    {
        var folder = set.FolderOverride ?? _session.Store.PlatformMap.Find(row.PlatformFsSlug ?? row.PlatformSlug)?.Folder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new PickOutcome(
                set,
                null,
                $"'{row.PlatformSlug}' has no RetroBat folder on this install, so this game has "
                    + "nowhere to go.");
        }

        if (row.HasMultipleFiles)
        {
            return new PickOutcome(
                set,
                null,
                "RomM holds this game as several files, which this version cannot sync yet.");
        }

        if (!EsSystemsFile.Load(_session.Install).TryGetFolder(folder, out var system) || !system.Accepts(row.FsExtension))
        {
            return new PickOutcome(
                set,
                null,
                $"'{folder}' cannot launch a .{row.FsExtension} file on this install.");
        }

        var limits = FilesystemLimits.Inspect(_session.Install.RootPath);

        if (!limits.CanHold(row.SizeBytes))
        {
            return new PickOutcome(
                set,
                null,
                $"This game is {ByteSize.Format(row.SizeBytes)}, which is more than this drive's "
                    + $"filesystem can hold in one file.");
        }

        return new PickOutcome(
            set,
            new SyncSetMember
            {
                RomId = row.Id,
                State = MemberState.Member,
                Folder = folder,
                PlatformSlug = row.PlatformSlug,
                FsName = row.FsName,
                FsExtension = row.FsExtension,
                SizeBytes = row.SizeBytes,
                IsMultiFile = row.HasMultipleFiles,
                Md5Hash = row.Md5Hash,
                Sha1Hash = row.Sha1Hash,
                DisplayName = row.DisplayName,
                SortKey = row.SortKey,
                RomUpdatedAt = row.UpdatedAtUtc,

                // Appended rather than ranked. A picked set has no ordering to apply, because
                // the user's own order is the order they picked in, and a position that
                // reshuffled on every pick would move what eviction takes first. A game already
                // in the set keeps the position it has: recounting gave it the count plus one
                // without growing the count, so the next new pick collided with it and two
                // members shared the number eviction ranks on.
                Position = PositionFor(set, row.Id),
                ResolvedAt = now,
            });
    }

    /// <summary>Where this game sits in the pick order: its own, or the end of the list.</summary>
    private int PositionFor(SyncSetDefinition set, int romId)
    {
        var members = _session.Store.SyncSets.Members(set.Id);

        return members.FirstOrDefault(member => member.RomId == romId)?.Position
            ?? members.Count + 1;
    }
}
