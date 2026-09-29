using System.Globalization;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>What a removal takes beyond the integration it always reverses.</summary>
/// <param name="Content">Synced ROMs, their media and their gamelist entries.</param>
/// <param name="Firmware">Synced files under <c>bios/</c>.</param>
public sealed record RemovalScope(bool Content = false, bool Firmware = false);

/// <summary>What removing RomMBat would do, before anything is done.</summary>
/// <param name="Blockers">
/// Work that has not reached the server. Any at all refuses the whole removal, because the
/// store that would send it is the thing the user is about to delete.
/// </param>
/// <param name="Hooks">The hook files present, which is what would be removed.</param>
/// <param name="Menu">The parts of the menu registration present.</param>
/// <param name="Conversions">Per-game settings RomMBat wrote into <c>es_settings.cfg</c>.</param>
/// <param name="Queued">Conversions queued for the next EmulationStation quit.</param>
/// <param name="Content">The content removal, or null when the scope leaves content alone.</param>
/// <param name="Firmware">Synced firmware rows, empty when the scope leaves <c>bios/</c> alone.</param>
/// <param name="EmulationStation">Why applying would be refused now, or null when ES is closed.</param>
public sealed record RemovalReport(
    RemovalScope Scope,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<RelativePath> Hooks,
    IReadOnlyList<RelativePath> Menu,
    IReadOnlyList<SaveConversion> Conversions,
    IReadOnlyList<PendingConfig> Queued,
    EvictionReport? Content,
    IReadOnlyList<LocalFile> Firmware,
    string? EmulationStation)
{
    public bool IsBlocked => Blockers.Count > 0;
}

/// <summary>What a removal did.</summary>
/// <param name="Refusal">Set when nothing was done, and says why.</param>
/// <param name="Kept">Firmware left because it changed since RomMBat wrote it. Not a failure.</param>
public sealed record RemovalApplied(
    string? Refusal,
    EsHookOutcome? Hooks = null,
    EsMenuOutcome? Menu = null,
    int Cancelled = 0,
    IReadOnlyList<ConversionResult>? Reverted = null,
    EvictionApplied? Content = null,
    int FirmwareRemoved = 0,
    IReadOnlyList<string>? Kept = null,
    IReadOnlyList<string>? Problems = null)
{
    /// <summary>True when every step that ran finished, and nothing was left half done.</summary>
    public bool Ok => Refusal is null
        && (Hooks?.Failed ?? 0) == 0
        && (Menu?.Failed ?? 0) == 0
        && (Reverted ?? []).All(result => result.Ok)
        && (Content?.Evicted?.Problems.Count ?? 0) == 0
        && (Problems ?? []).Count == 0;
}

/// <summary>
/// Takes the RetroBat tree back to its state before RomMBat, as far as a scope asks.
/// </summary>
/// <remarks>
/// <b>Always reversed: the hooks, the menu entry and the per-game memory card conversions.</b>
/// Those are the three things RomMBat wrote into files RetroBat owns. Downloaded content is the
/// user's library and goes only when asked, and adopted files and saves never go.
/// <para>
/// <b>Any unsent work refuses the whole removal</b>, not just the game it belongs to. After
/// removal the user deletes <c>emulators/rommbat</c>, and the outbox, the journal and the spool
/// go with it: a save that has not reached the server by then is not going to. Half a removal
/// is also worse than none, because the hooks going first would stop the very pass that sends it.
/// </para>
/// <para>
/// <b>Applying refuses while EmulationStation runs.</b> It rewrites <c>es_settings.cfg</c> from
/// the copy it loaded at startup, so a reverted conversion would be put back without a word.
/// </para>
/// <para>
/// <b>RomMBat's own folder and the device on the server are left.</b> A running executable
/// cannot delete itself, and the device is one call away in RomM's own interface. The caller
/// says both.
/// </para>
/// </remarks>
public sealed class RemovalService
{
    private readonly InstallSession _session;
    private readonly Func<EsRunningVerdict> _emulationStation;

    /// <param name="emulationStation">Injectable because the real check reads the process list.</param>
    public RemovalService(InstallSession session, Func<EsRunningVerdict>? emulationStation = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _emulationStation = emulationStation ?? (() => EmulationStationProcess.Check(session.Install));
    }

    /// <summary>Scans the tree and works out what would go.</summary>
    /// <remarks>
    /// The scans run first, states before saves, for the reason <see cref="EvictionService"/>
    /// gives: without them the unsent-work question is answered from the last flush.
    /// </remarks>
    public RemovalReport Preview(RemovalScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var install = _session.Install;
        var store = _session.Store;
        var eviction = new EvictionService(_session);

        EvictionReport? content = null;

        if (scope.Content)
        {
            // Every set's claim is being given up, so none holds a game back.
            var sets = store.SyncSets.List().Select(set => set.Id).ToList();
            var roms = store.Files.List(kind: LocalFileKind.Rom)
                .Where(file => file.Origin == FileOrigin.Synced && file.RomId is not null)
                .Select(file => file.RomId!.Value)
                .Distinct()
                .ToList();

            content = eviction.PreviewRemoval(roms, sets);
        }
        else
        {
            Scan();
        }

        var menu = new List<RelativePath>();

        foreach (var path in new[] { EsMenuEntry.MenuPath, EsMenuEntry.LogoPath })
        {
            if (File.Exists(install.Resolve(path)))
            {
                menu.Add(path);
            }
        }

        if (MenuInGamelist())
        {
            menu.Add(EsMenuEntry.GamelistPath);
        }

        return new RemovalReport(
            scope,
            Blockers(),
            [.. EsHooks.Events.Select(EsHooks.PathFor).Where(path => File.Exists(install.Resolve(path)))],
            menu,
            store.SaveConversions.List(),
            store.PendingConfig.ListOutstanding(),
            content,
            scope.Firmware
                ? [.. store.Files.List(kind: LocalFileKind.Firmware).Where(file => file.Origin == FileOrigin.Synced)]
                : [],
            _emulationStation() is { IsRunning: true } running ? running.Detail : null);
    }

    /// <summary>Carries out a report, after asking every question again.</summary>
    /// <remarks>
    /// <b>Conversions are reverted before content goes</b>, so a key never outlives the reason
    /// it was reverted in the same pass. <see cref="SaveConverter.RevertRecorded"/> does not need
    /// the ROM, so the order is for the report's sake rather than correctness.
    /// </remarks>
    public async Task<RemovalApplied> ApplyAsync(RemovalReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (_emulationStation() is { IsRunning: true } running)
        {
            return new RemovalApplied(
                $"{running.Detail} Quit EmulationStation and run this again: it rewrites "
                    + "es_settings.cfg on exit, which would put the reverted settings straight back.");
        }

        using var held = TreeLock.TryAcquire(_session.Install);

        if (held is null)
        {
            return new RemovalApplied(
                "Another RomMBat pass is running. Let it finish, then run this again.");
        }

        // Re-asked under the lock, because a plan can be shown and applied minutes later.
        var fresh = Preview(report.Scope);

        if (fresh.IsBlocked)
        {
            return new RemovalApplied(
                "Something has not reached the server yet, so nothing was removed: "
                    + string.Join(" ", fresh.Blockers));
        }

        var hooks = new EsHooks(_session.Install).Uninstall();
        var menu = new EsMenuEntry(_session.Install).Uninstall();

        var cancelled = 0;
        foreach (var queued in fresh.Queued)
        {
            if (_session.Store.PendingConfig.Cancel(queued.System, queued.FsName, queued.SettingKey))
            {
                cancelled++;
            }
        }

        var converter = new SaveConverter(_session.Install, _session.Store, emulationStation: _emulationStation);
        var reverted = fresh.Conversions.Select(converter.RevertRecorded).ToList();

        EvictionApplied? content = null;

        if (fresh.Content is { } plan)
        {
            content = await new EvictionService(_session)
                .ApplyAsync(plan, cancellationToken)
                .ConfigureAwait(false);
        }

        var kept = new List<string>();
        var problems = new List<string>();
        var firmware = RemoveFirmware(fresh.Firmware, kept, problems);

        return new RemovalApplied(null, hooks, menu, cancelled, reverted, content, firmware, kept, problems);
    }

    /// <summary>States then saves, so the unsent-work queries read the tree as it is now.</summary>
    private void Scan()
    {
        var schema = StateScanner.LoadSchema(_session.Install);

        if (schema is not null)
        {
            new StateScanner(_session.Install, _session.Store, schema).Scan();
        }

        new SaveScanner(_session.Install, _session.Store, states: schema).Scan();
    }

    /// <summary>
    /// Everything waiting to reach the server, as sentences.
    /// </summary>
    /// <remarks>
    /// <b>Saves and states with no ROM are not counted.</b> Nothing can ever send one, since
    /// attribution is what failed, so counting them would refuse forever. They are reported as
    /// unsyncable by <c>saves</c>, and the files themselves are never removed.
    /// </remarks>
    private List<string> Blockers()
    {
        var blockers = new List<string>();

        try
        {
            Add(Count("SELECT COUNT(*) FROM outbox WHERE state <> 'sent';"),
                "save or play record is queued to send. Run 'rommbat-agent flush'.");

            Add(_session.Store.Journal.OpenCount(),
                "game launch has not been worked out yet. Run 'rommbat-agent flush'.");

            Add(SpoolCount(), "hook event is waiting in the spool. Run 'rommbat-agent flush'.");

            Add(Count(Unsent("local_save")), "save file on disk has not reached the server. Run 'rommbat-agent flush'.");

            Add(Count(Unsent("local_state")), "save state on disk has not reached the server. Run 'rommbat-agent flush'.");

            Add(_session.Store.SaveConflicts.ListOpen().Count,
                "save conflict is waiting on you. Settle it with 'rommbat-agent saves resolve'.");
        }
        catch (SqliteException ex)
        {
            // Fail closed, as SaveGuard does: an unreadable store is not evidence of nothing waiting.
            blockers.Add($"The local database could not be read ({ex.Message}).");
        }

        return blockers;

        void Add(int count, string what)
        {
            if (count > 0)
            {
                blockers.Add($"{count} {what}");
            }
        }
    }

    private static string Unsent(string table) =>
        $"""
        SELECT COUNT(*)
        FROM {table}
        WHERE rom_id IS NOT NULL
          AND (uploaded_content_hash IS NULL
               OR content_hash IS NULL
               OR uploaded_content_hash <> content_hash);
        """;

    private int Count(string sql)
    {
        using var command = _session.Store.Connection.Command(sql);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private int SpoolCount()
    {
        var directory = _session.Install.Resolve(SpoolDrain.Directory);

        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*" + Spool.Extension).Count()
            : 0;
    }

    private bool MenuInGamelist()
    {
        var gamelist = _session.Install.Resolve(EsMenuEntry.GamelistPath);

        try
        {
            return File.Exists(gamelist) && GamelistDocument.Load(gamelist).Contains(EsMenuEntry.EntryPath);
        }
        catch (GamelistParseException)
        {
            // Reported by the uninstall step itself, which refuses to rewrite a file it cannot parse.
            return false;
        }
    }

    /// <summary>
    /// Deletes synced firmware that is still the file RomMBat wrote.
    /// </summary>
    /// <remarks>
    /// <b>A file whose size or md5 no longer matches is the user's now</b>, the same rule eviction
    /// applies to media replaced between a preview and an apply. Its row is dropped and the file
    /// stays. An archive-content hash describes the file inside a zip, so only size is compared
    /// there.
    /// </remarks>
    private int RemoveFirmware(IReadOnlyList<LocalFile> firmware, List<string> kept, List<string> problems)
    {
        var removed = 0;

        foreach (var file in firmware)
        {
            var absolute = _session.Install.Resolve(file.Path);

            try
            {
                if (File.Exists(absolute))
                {
                    if (!StillOurs(file, absolute))
                    {
                        kept.Add($"{file.Path.Value}: kept, because it has changed since RomMBat wrote it.");
                        _session.Store.Files.Remove(file.Path);
                        continue;
                    }

                    File.Delete(absolute);
                }

                _session.Store.Files.Remove(file.Path);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{file.Path.Value}: could not be removed ({ex.Message}).");
            }
        }

        return removed;
    }

    private static bool StillOurs(LocalFile file, string absolute)
    {
        if (new FileInfo(absolute).Length != file.SizeBytes)
        {
            return false;
        }

        if (file.HashScope != HashScope.File || file.Md5Hash is null)
        {
            return true;
        }

        return ContentHasher.Matches(ContentHasher.ComputeFileMd5(absolute), file.Md5Hash);
    }
}
