using RomM.Client;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What a state push did.</summary>
public sealed record StateSyncOutcome
{
    public int Uploaded { get; init; }

    public int AlreadyInStep { get; init; }

    public int Unattributed { get; init; }

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

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return "states: " + string.Join(", ", parts);
        }
    }
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

    /// <summary>Sends every state that has changed since it was last sent.</summary>
    public async Task<StateSyncOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var states = _store.States.List();

        var uploaded = 0;
        var inStep = 0;
        var unattributed = 0;
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
            Failed = failed,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    private async Task<(string? Problem, bool Unreachable)> UploadAsync(
        LocalState state,
        CancellationToken cancellationToken)
    {
        var path = _install.Resolve(state.Path);

        if (!File.Exists(path))
        {
            return ($"{state.Path}: the file is gone since the scan.", false);
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
                    return ($"{state.Path}: {response.Message}", false);
                }

                _store.States.MarkUploaded(state.Path, row.Id, name, state.ContentHash!, _time.GetUtcNow());
                return (null, false);
            }
            finally
            {
                screenshot?.Content.Dispose();
            }
        }
        catch (RomMUnreachableException ex)
        {
            return ($"{state.Path}: {ex.Message}", true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ($"{state.Path}: it could not be read: {ex.Message}", false);
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
