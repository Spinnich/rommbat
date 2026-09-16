using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What a state push did.</summary>
public sealed record StateSyncOutcome
{
    public int Uploaded { get; init; }

    public int AlreadyInStep { get; init; }

    public int Unattributed { get; init; }

    /// <summary>
    /// States sent with a screenshot the server did not attach.
    /// </summary>
    /// <remarks>
    /// <b>Measured against a live instance and not reproducible on demand.</b> The screenshot
    /// bytes arrive, and are stored against the ROM at the right size and name, but the state
    /// comes back with <c>screenshot: null</c> and stays that way. Roughly a third of attempts
    /// across thirty-five did it, varying the filename, the content type, the part header
    /// order, the handler and connection reuse without finding what separates the two outcomes.
    /// The request is provably well formed: dumped against a local listener it carries both
    /// parts, the whole image payload and a correct <c>Content-Length</c>.
    /// <para>
    /// So this is counted rather than treated as a failure. The state itself is correct and
    /// complete, and a screenshot is best-effort by nature, but silently reporting success for
    /// something that did not happen is the thing worth avoiding.
    /// </para>
    /// </remarks>
    public int ScreenshotsDropped { get; init; }

    public int Failed { get; init; }

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool IsNoOp => Uploaded == 0 && Failed == 0;

    public string Summary
    {
        get
        {
            if (IsNoOp)
            {
                return AlreadyInStep == 0 ? "states: nothing to send" : $"states: {AlreadyInStep} already in step";
            }

            var parts = new List<string>();

            if (Uploaded > 0)
            {
                parts.Add($"{Uploaded} up ({ByteSize.Format(BytesTransferred)})");
            }

            if (ScreenshotsDropped > 0)
            {
                parts.Add($"{ScreenshotsDropped} without the screenshot the server did not keep");
            }

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return "states: " + string.Join(", ", parts);
        }
    }
}

/// <summary>A state the server holds that this device could put back.</summary>
/// <param name="Scope">The emulator, or emulator and core, as <c>ScopeOf</c> wrote it.</param>
/// <param name="System">The RetroBat folder the ROM lives in, which is what decides the template.</param>
/// <param name="Emulator">The emulator half of <paramref name="Scope"/>, which is a column.</param>
/// <param name="Core">The core half, empty where the emulator is not core-scoped.</param>
/// <param name="SlotKey">
/// The local <c>{emulator}:{core}:{slot}</c> identity, read off the destination filename by the
/// same template the scanner uses. Carried because the row written after a restore needs it and
/// the server has no slot field to supply it.
/// </param>
/// <param name="UploadedFileName">
/// The name the server holds it under, which is not the name on disk. Null where the server's
/// name is not one <c>local_state.uploaded_file_name</c> will accept, since the column is
/// nullable and nothing reads it to decide whether a state still needs sending.
/// </param>
public sealed record RestorableState(
    int RomId,
    int StateId,
    RelativePath Destination,
    long SizeBytes,
    string Scope,
    string System,
    string Emulator,
    string Core,
    string SlotKey,
    string? UploadedFileName,
    DateTimeOffset? ServerUpdatedAt);

/// <summary>
/// A state the server holds for a game on this device that a restore cannot write.
/// </summary>
/// <remarks>
/// Reported rather than dropped, for the reason <see cref="UnrestorableSave"/> is: a preview is
/// the answer to "what can I bring back", and a state silently missing from it reads as one the
/// server does not hold. Only the ROM being absent is ordinary enough to drop.
/// </remarks>
public sealed record UnrestorableState(int RomId, string Scope, string Reason);

/// <summary>What the server holds for this device, split by whether it can be placed.</summary>
public sealed record StateRestoreFindings(
    IReadOnlyList<RestorableState> Restorable,
    IReadOnlyList<UnrestorableState> Unrestorable);

/// <summary>What a state restore did.</summary>
/// <remarks>
/// No verified count, deliberately. Nothing is verified: RomM publishes no hash for a state.
/// </remarks>
public sealed record StateRestoreOutcome
{
    /// <summary>
    /// The tree lock was held elsewhere and nothing was attempted.
    /// </summary>
    /// <remarks>
    /// Distinct from a failure, because nothing was tried and trying again later works. Matches
    /// <see cref="SaveRestoreOutcome.Refused"/>, and exists so that a refusal on the save half
    /// cannot be followed by the state half writing anyway.
    /// </remarks>
    public bool Refused { get; init; }

    /// <summary>States written into the tree.</summary>
    public int Restored { get; init; }

    /// <summary>
    /// States held back because the game they belong to is being played.
    /// </summary>
    /// <remarks>
    /// Matches <see cref="SaveRestoreOutcome.Deferred"/>, and is here for the same reason
    /// <see cref="Refused"/> is: nothing was written and asking again later works. A state goes
    /// into a directory the running emulator is reading, and <see cref="InFlightGuard"/> is the
    /// one thing that knows a game is open.
    /// </remarks>
    public int Deferred { get; init; }

    /// <summary>States that could not be written, each with a line in <see cref="Problems"/>.</summary>
    public int Failed { get; init; }

    /// <summary>Bytes fetched.</summary>
    public long BytesTransferred { get; init; }

    /// <summary>One line per failure.</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];
}

/// <summary>
/// Pushes save states, which is all a state sync can be.
/// </summary>
/// <remarks>
/// <b>States are outside the negotiate protocol and there is no version of this that is not
/// best-effort.</b> <c>POST /api/states</c> takes only <c>rom_id</c> and <c>emulator</c>: no
/// slot, no device, no session, no conflict detection, and the row it returns carries no content
/// hash. So nothing here negotiates, nothing here resolves a conflict, and the only record that
/// a state is in step is the hash this device wrote down when it sent one.
/// <para>
/// <b>The uploaded name is not the name on disk, and that is what stops a state being lost.</b>
/// Measured live, the upsert keys on <c>(rom_id, file_name)</c> with the emulator not part of
/// the key: five posts of one name under five different emulator values reused a single row.
/// Two libretro cores writing <c>Game.state1</c> for one ROM would therefore collapse into one
/// server row, and libretro and gopher64 both serve n64 and both render that same name. The
/// scope goes into the name unconditionally rather than only where a collision is possible,
/// because a conditional rule produces different names on two devices for one state, and two
/// names is two rows.
/// </para>
/// <para>
/// Sent straight from <c>local_state</c> rather than through the outbox, which is what stage 1
/// does with saves. A state is one file with no sibling to tie it to, so it has nothing for
/// <c>batch_key</c> to do.
/// </para>
/// </remarks>
public sealed class StateSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly InFlightGuard _inFlight;
    private readonly TimeProvider _time;

    public StateSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection connection,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);

        _install = install;
        _store = store;
        _connection = connection;
        _inFlight = new InFlightGuard(install, store);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// The emulator this device reports for a state, which is also a directory segment
    /// server-side.
    /// </summary>
    /// <remarks>
    /// <c>libretro.snes9x</c> rather than <c>libretro</c>, mirroring RetroBat's own
    /// <c>saves/&lt;system&gt;/libretro.&lt;core&gt;/</c> naming, so the server's own tree reads
    /// the way the local one does. Measured, the server does not sanitise this field and a value
    /// carrying a separator becomes two path segments there; the schema's CHECK on
    /// <c>local_state.emulator</c> is what keeps one out.
    /// </remarks>
    public static string ScopeOf(string emulator, string? core) =>
        string.IsNullOrEmpty(core) ? emulator : $"{emulator}.{core}";

    /// <summary>
    /// The name a state is uploaded under, which is not the name it has on disk.
    /// </summary>
    /// <remarks>
    /// The scope goes in a bracketed group before the extension, which is the tag convention
    /// RomM already uses on a save. Two names differing only in that group were measured to
    /// produce two rows, so the group really does separate them.
    /// </remarks>
    public static string UploadNameFor(string onDiskName, string emulator, string? core)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(onDiskName);

        var stem = Path.GetFileNameWithoutExtension(onDiskName);
        var extension = Path.GetExtension(onDiskName);

        return $"{stem} [{ScopeOf(emulator, core)}]{extension}";
    }

    /// <summary>
    /// States the server holds for a ROM on this device that are not in the tree.
    /// </summary>
    /// <remarks>
    /// <b>States are outside the negotiate protocol entirely</b>, so there is nothing to ask
    /// and this walks <c>GET /api/states</c> instead. They have no <c>slot</c>, no
    /// <c>/track</c>, no <c>/downloaded</c> and no per-device sync record, which is why
    /// <see cref="RunAsync"/> only ever uploaded.
    /// <para>
    /// <b>Nothing verifies what arrives, and that is the API rather than a shortcut.</b>
    /// <c>StateSchema</c> and <c>UserStateSchema</c> carry no hash field of any kind in the
    /// pinned schema, at 5.2.0 or 5.3.0-alpha.2, where a save carries <c>content_hash</c>. A
    /// save is verified on download because the server offers something to verify against; a
    /// state has nothing. Callers say so rather than implying a check happened.
    /// </para>
    /// <para>
    /// <b>Never automatic</b>, for the reason <c>saves restore</c> is not: a state reappearing
    /// because a sync decided it should is indistinguishable from a bug to whoever deleted it.
    /// </para>
    /// <para>
    /// <b>Every skip but one is reported.</b> A state the server holds for an <i>installed</i>
    /// ROM that this device cannot place is not nothing, and showing nothing is what sends
    /// somebody looking for a bug in the server. Only the ROM being absent is dropped, because a
    /// device holding a subset of the library is the ordinary case.
    /// </para>
    /// </remarks>
    public async Task<RomMResponse<StateRestoreFindings>> FindRestorableAsync(
        CancellationToken cancellationToken = default)
    {
        var schema = StateScanner.LoadSchema(_install);

        if (schema is null)
        {
            return RomMResponse.Success(new StateRestoreFindings([], []));
        }

        var listed = await _connection.ListAllStatesAsync(cancellationToken).ConfigureAwait(false);

        if (!listed.IsSuccess || listed.Value is not { } rows)
        {
            return RomMResponse.Failure<StateRestoreFindings>(
                listed.Status,
                listed.Message ?? "The state list could not be read.");
        }

        var found = new List<RestorableState>();
        var unrestorable = new List<UnrestorableState>();

        foreach (var row in rows)
        {
            if (row.OnDiskFileName is not { } onDisk || row.Emulator is not { } scope)
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    row.Emulator ?? "(none)",
                    "the server row names no file or no emulator, so there is nothing to say "
                        + "where it would go."));
                continue;
            }

            // The scope is what ScopeOf wrote: emulator, or emulator.core. Split on the first
            // separator, because a core name can carry more of them than an emulator name can.
            var dot = scope.IndexOf('.', StringComparison.Ordinal);
            var emulatorName = dot < 0 ? scope : scope[..dot];
            var core = dot < 0 ? null : scope[(dot + 1)..];

            // <b>The core is free text from the server, and it reaches a CHECKed column.</b>
            // `local_state.core` refuses a separator, and the row a restore writes is the reason
            // that matters now: without this the write throws SQLite error 19 after the file is
            // already in the tree, which counts a state that landed as Failed and leaves no row
            // behind it. That is the shape f337d2e fixed on the save side, fixed here at the same
            // point. The emulator half needs no guard because schema.For only answers for a name
            // es_savestates.cfg declares.
            if (core is not null && core.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    scope,
                    $"'{core}' is not a core name this device can record: a core carrying a path "
                        + "separator would not survive being written down."));
                continue;
            }

            if (schema.For(emulatorName) is not { } emulator)
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    scope,
                    $"'{emulatorName}' is not declared in this install's es_savestates.cfg, so "
                        + "RetroBat names no state directory for it here."));
                continue;
            }

            // The ROM decides the system, and its absence is what makes this not a candidate.
            // Ordinary on a device holding a subset, so it is skipped rather than reported.
            var roms = _store.Files.ForRom(row.RomId, LocalFileKind.Rom);

            if (roms.Count == 0 || roms[0].Folder is not { } system)
            {
                continue;
            }

            if (SaveStateTemplate.Create(emulator, system, core) is not { } template)
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    scope,
                    $"nowhere to write it. {emulatorName} declares no state directory for "
                        + $"'{system}' in this install."));
                continue;
            }

            // <b>Built from the ROM on disk, never from the server's name.</b> `es_savestates.cfg`
            // declares `{{romfilename}}.state{{slot}}`, and RomM's `file_name_no_tags` strips
            // anything parenthesised as a tag: measured, "Legend of Zelda, The (USA) (Rev 1)"
            // comes back as "Legend of Zelda, The". Writing that name puts a state where the
            // emulator will never look for it, and the file would read as simply absent. Only
            // the extension is taken from the server, because that is what carries the slot.
            var stem = Path.GetFileNameWithoutExtension(roms[0].FileName);
            var extension = Path.GetExtension(onDisk);

            if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(extension))
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    scope,
                    "the name it would take here is incomplete: the ROM on disk has no stem, or "
                        + "the server's name no extension, and the extension is the slot."));
                continue;
            }

            var name = stem + extension;

            // Read back through the same template the scanner keys on, so the row written after a
            // restore pairs with the one the next scan finds instead of becoming a second row.
            if (template.Match(name) is not { } match)
            {
                unrestorable.Add(new UnrestorableState(
                    row.RomId,
                    scope,
                    $"'{name}' does not match the name {emulatorName} declares for a state here, "
                        + "so this device could not tell which slot it is."));
                continue;
            }

            var destination = template.Directory.Combine(name);

            if (File.Exists(_install.Resolve(destination)))
            {
                continue;
            }

            found.Add(new RestorableState(
                row.RomId,
                row.Id,
                destination,
                row.FileSizeBytes,
                scope,
                system,
                emulator.Name,
                core ?? string.Empty,
                match.SlotKey(emulator.Name, core),
                RecordableName(row.FileName),
                row.UpdatedAt ?? row.CreatedAt));
        }

        return RomMResponse.Success(new StateRestoreFindings(found, unrestorable));
    }

    /// <summary>Fetches the given states into the tree.</summary>
    /// <remarks>
    /// Written through the partial directory and moved into place, so a failure part way leaves
    /// no half state where an emulator would find one. Not verified, for the reason on
    /// <see cref="FindRestorableAsync"/>.
    /// <para>
    /// <b>Holds <see cref="TreeLock"/>, and refuses rather than treating a failed acquire as
    /// done.</b> The same rule and the same reason as <see cref="SaveSync.RestoreAsync"/>: a
    /// <c>background quit</c> pass holds the lock across <c>StateScanner.Scan()</c> over these
    /// exact directories, and nobody else is doing a restore's work. Taken here rather than in a
    /// service because nothing calls this from the flush pass.
    /// </para>
    /// <para>
    /// <b>Each restored state is recorded as being in step with the server.</b> Without the row,
    /// the next scan reads the file as never sent and <see cref="RunAsync"/> uploads it straight
    /// back, which hits every state that came from another device.
    /// </para>
    /// </remarks>
    public async Task<StateRestoreOutcome> RestoreAsync(
        IReadOnlyList<RestorableState> picks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(picks);

        using var held = TreeLock.TryAcquire(_install);

        if (held is null)
        {
            return new StateRestoreOutcome
            {
                Refused = true,
                Problems =
                [
                    "A flush is running, and restoring a state writes the same files it scans. "
                        + "Nothing was changed. Try again once it has finished.",
                ],
            };
        }

        var restored = 0;
        var failed = 0;
        var deferred = 0;
        var bytes = 0L;
        var problems = new List<string>();

        var partialDirectory = _install.Resolve(RetroBatInstall.PartialDirectory);

        foreach (var pick in picks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Ahead of the transfer, so a deferral costs nothing on the wire. A state cannot
            // overwrite one here, since the move below refuses to, but it still puts a file into
            // a directory a running emulator reads its slots from.
            if (_inFlight.Check(pick.RomId, pick.Destination) is { CanWrite: false } verdict)
            {
                deferred++;
                problems.Add($"{pick.Destination}: not written because {verdict.Reason}.");
                continue;
            }

            var part = Path.Combine(partialDirectory, $"state-{pick.StateId}.part");

            try
            {
                Directory.CreateDirectory(partialDirectory);

                long written;

                await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var response = await _connection
                        .DownloadStateAsync(pick.StateId, stream, cancellationToken)
                        .ConfigureAwait(false);

                    if (!response.IsSuccess)
                    {
                        failed++;
                        problems.Add($"{pick.Destination}: {response.Message}");
                        continue;
                    }

                    written = response.Value;
                }

                var absolute = _install.Resolve(pick.Destination);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

                // Not overwrite: true, matching BiosSync. The find checked the destination was
                // empty, and a state has no hash and no conflict record, so anything that landed
                // in the window between would be destroyed with nothing written down anywhere.
                File.Move(part, absolute, overwrite: false);

                RecordRestored(pick, absolute);

                restored++;
                bytes += written;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                problems.Add($"{pick.Destination}: {ex.Message}");
            }
            finally
            {
                if (File.Exists(part))
                {
                    try
                    {
                        File.Delete(part);
                    }
                    catch (IOException)
                    {
                        // A partial nobody can remove is the eviction sweep's problem, not this
                        // pass's, and failing the restore over it would be worse.
                    }
                }
            }
        }

        return new StateRestoreOutcome
        {
            Restored = restored,
            Failed = failed,
            Deferred = deferred,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    /// <summary>Null for anything <c>local_state.uploaded_file_name</c>'s CHECK would refuse.</summary>
    /// <remarks>
    /// The server's own file name, and free text as far as this device is concerned. Recording
    /// null loses nothing: the column is a note about what went up, and what decides whether a
    /// state still needs sending is the hash beside it.
    /// </remarks>
    private static string? RecordableName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.AsSpan().IndexOfAny('/', '\\') >= 0 ? null : name;

    /// <summary>
    /// Records a restored state as being in step with the server.
    /// </summary>
    /// <remarks>
    /// Written with <c>uploaded_content_hash</c> equal to what is now on disk, because both sides
    /// hold the same bytes. Without it <see cref="LocalStateStore.Record"/> writes a row with the
    /// upload columns null on the next scan, <c>NeedsUpload</c> is true, and the very next flush
    /// sends back what was just fetched.
    /// <para>
    /// <c>emulator_version</c> is left null rather than stamped with what is installed now. The
    /// state was made on another build and this device cannot know which, so a number here would
    /// be a claim rather than a record. See <c>StateScanner.ReadEmulatorVersion</c>: a wrong
    /// version is worse than no version for the one job the column has.
    /// </para>
    /// </remarks>
    private void RecordRestored(RestorableState pick, string absolute)
    {
        var now = _time.GetUtcNow();
        var info = new FileInfo(absolute);
        var hash = LogicalContentHash.OfFile(absolute);

        _store.States.Record(
            new LocalState
            {
                Path = pick.Destination,
                System = pick.System,
                Emulator = pick.Emulator,
                Core = pick.Core,
                Slot = pick.SlotKey,
                RomId = pick.RomId,
                ContentHash = hash,
                SizeBytes = info.Length,
                FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                StateId = pick.StateId,
                UploadedFileName = pick.UploadedFileName,
                UploadedContentHash = hash,
                UploadedAtUtc = now,
            },
            now);
    }

    /// <summary>Sends every state that has changed since it was last sent.</summary>
    public async Task<StateSyncOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var states = _store.States.List();

        var uploaded = 0;
        var inStep = 0;
        var unattributed = 0;
        var dropped = 0;
        var failed = 0;
        var bytes = 0L;
        var problems = new List<string>();

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.RomId is null)
            {
                unattributed++;
                continue;
            }

            if (!state.NeedsUpload)
            {
                inStep++;
                continue;
            }

            var attempt = await UploadAsync(state, cancellationToken).ConfigureAwait(false);

            if (attempt.Problem is null)
            {
                uploaded++;
                bytes += state.SizeBytes;

                if (attempt.ScreenshotDropped)
                {
                    dropped++;
                    problems.Add(
                        $"{state.Path}: the state went up but the server did not keep the "
                            + "screenshot sent with it. The state itself is complete.");
                }

                continue;
            }

            failed++;
            problems.Add(attempt.Problem);

            if (attempt.Unreachable)
            {
                // The link is down, so every remaining state would fail the same way and each
                // would cost a connect timeout. They stay recorded as unsent and the next flush
                // sends the same set, which is what "operations complete or queue" means here.
                break;
            }
        }

        return new StateSyncOutcome
        {
            Uploaded = uploaded,
            AlreadyInStep = inStep,
            Unattributed = unattributed,
            ScreenshotsDropped = dropped,
            Failed = failed,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    private async Task<(string? Problem, bool Unreachable, bool ScreenshotDropped)> UploadAsync(
        LocalState state,
        CancellationToken cancellationToken)
    {
        var path = _install.Resolve(state.Path);

        if (!File.Exists(path))
        {
            return ($"{state.Path}: the file is gone since the scan.", false, false);
        }

        try
        {
            await using var content = File.OpenRead(path);

            var name = UploadNameFor(state.Path.Name, state.Emulator, state.Core);
            var screenshot = OpenScreenshot(state);

            try
            {
                var response = await _connection.UploadStateAsync(
                    (int)state.RomId!.Value,
                    ScopeOf(state.Emulator, state.Core),
                    name,
                    content,
                    screenshot,
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccess || response.Value is not { } row)
                {
                    return ($"{state.Path}: {response.Message}", false, false);
                }

                _store.States.MarkUploaded(state.Path, row.Id, name, state.ContentHash!, _time.GetUtcNow());

                // A screenshot was sent and the row came back without one. Reported rather than
                // retried: the bytes do reach the server, so a retry uploads the image again
                // and orphans another copy against the ROM.
                return (null, false, screenshot is not null && row.Screenshot is null);
            }
            finally
            {
                screenshot?.Content.Dispose();
            }
        }
        catch (RomMUnreachableException ex)
        {
            return ($"{state.Path}: {ex.Message}", true, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ($"{state.Path}: it could not be read: {ex.Message}", false, false);
        }
    }

    /// <summary>
    /// Opens the screenshot, if there still is one.
    /// </summary>
    /// <remarks>
    /// The scan already refused a zero-byte or absent image, so this only has to survive the
    /// file going away between the scan and the send. A missing screenshot never fails the
    /// state: it is best-effort everywhere, and the state itself was correct in every observed
    /// case where the image was not.
    /// </remarks>
    private (string FileName, Stream Content)? OpenScreenshot(LocalState state)
    {
        if (state.ScreenshotPath is not { } relative)
        {
            return null;
        }

        var path = _install.Resolve(relative);

        try
        {
            return !File.Exists(path) || new FileInfo(path).Length == 0
                ? null
                : (UploadNameFor(relative.Name, state.Emulator, state.Core), File.OpenRead(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
