using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Sets;

namespace RomMBat.UI.Screens;

/// <summary>What the art box beside a game list is showing.</summary>
public enum CoverState
{
    /// <summary>No game is selected, so the box is drawn empty.</summary>
    None,

    /// <summary>A game is selected and its cover is not here yet. The box stays empty.</summary>
    Waiting,

    /// <summary>The cover is in <see cref="Cover.Bytes"/>.</summary>
    Ready,

    /// <summary>Neither this device nor RomM had one, or it could not be read.</summary>
    Missing,
}

/// <summary>One answer for the art box, read once so a draw cannot see half of it.</summary>
/// <param name="Bytes">The encoded image, as the file or RomM held it. Only when ready.</param>
public sealed record Cover(CoverState State, int RomId = 0, byte[]? Bytes = null)
{
    public static Cover Nothing { get; } = new(CoverState.None);
}

/// <summary>
/// The covers read this session, held in memory and dropped when RomMBat exits.
/// </summary>
/// <remarks>
/// <b>Never written to disk</b>, ruled with Spinnich: browse reaches RomM only online, so a
/// cover can always be read again, and a synced game has its own copy in the tree. Bounded by
/// bytes and evicting the least recently shown, so paging through a large library cannot grow
/// it.
/// </remarks>
public sealed class CoverCache(long capacityBytes = CoverCache.DefaultCapacityBytes)
{
    /// <summary>About a thousand small covers at the live library's tens of kilobytes each.</summary>
    public const long DefaultCapacityBytes = 32L * 1024 * 1024;

    /// <summary>The one every screen of a session shares, so leaving browse does not forget them.</summary>
    public static CoverCache Shared { get; } = new();

    private readonly Lock _gate = new();
    private readonly LinkedList<(int RomId, byte[] Bytes)> _order = new();
    private readonly Dictionary<int, LinkedListNode<(int RomId, byte[] Bytes)>> _byRom = [];
    private long _bytes;

    /// <summary>The encoded bytes held now, for a test to bound.</summary>
    public long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    public bool TryGet(int romId, out byte[] bytes)
    {
        lock (_gate)
        {
            if (_byRom.TryGetValue(romId, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                bytes = node.Value.Bytes;
                return true;
            }
        }

        bytes = [];
        return false;
    }

    public void Add(int romId, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        lock (_gate)
        {
            if (_byRom.Remove(romId, out var old))
            {
                _order.Remove(old);
                _bytes -= old.Value.Bytes.Length;
            }

            // One larger than the whole cache is shown and not kept, rather than emptying it.
            if (bytes.Length > capacityBytes)
            {
                return;
            }

            _byRom[romId] = _order.AddFirst((romId, bytes));
            _bytes += bytes.Length;

            while (_bytes > capacityBytes && _order.Last is { } last)
            {
                _order.RemoveLast();
                _byRom.Remove(last.Value.RomId);
                _bytes -= last.Value.Bytes.Length;
            }
        }
    }
}

/// <summary>
/// The art box beside a game list: which game it is for, and its cover once it has one.
/// </summary>
/// <remarks>
/// <b>RomM is asked only once the cursor has rested.</b> Key repeat moves every 90 ms, so a
/// held d-pad never waits out <see cref="DefaultRest"/> and fetches nothing, and a move cancels
/// whatever was pending for the row it left. A cover already on this device or already in the
/// cache is shown at once, because it costs no request.
/// <para>
/// No Avalonia here, so a test drives it by pad. The renderer decodes <see cref="Cover.Bytes"/>.
/// </para>
/// </remarks>
public sealed class CoverSlot : IDisposable
{
    /// <summary>How long the cursor rests on a game before RomM is asked for its cover.</summary>
    public static TimeSpan DefaultRest => TimeSpan.FromMilliseconds(250);

    private readonly InstallSession _session;
    private readonly Func<RomMConnection?> _connection;
    private readonly CoverCache _cache;
    private readonly TimeSpan _rest;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    private Cover _current = Cover.Nothing;
    private CancellationTokenSource? _pending;
    private bool _disposed;

    /// <param name="connection">Null when there is no server, which leaves only this device's copies.</param>
    /// <param name="time">The clock the rest is timed on, so a test can hold it.</param>
    public CoverSlot(
        InstallSession session,
        Func<RomMConnection?> connection,
        CoverCache? cache = null,
        TimeSpan? rest = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connection);

        _session = session;
        _connection = connection;
        _cache = cache ?? CoverCache.Shared;
        _rest = rest ?? DefaultRest;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The cover changed, raised from whatever thread read it.</summary>
    public event EventHandler? Changed;

    public Cover Current => _current;

    /// <summary>Points the box at a game, or at none.</summary>
    /// <remarks>
    /// Called on every state change of the list, so the same game asked twice is a no-op rather
    /// than a second read. Raises nothing itself: the caller is already redrawing for the move,
    /// and <see cref="Changed"/> is for a read that finishes later.
    /// </remarks>
    public void Show(BrowseGame? game)
    {
        BrowseGame? read = null;
        var token = CancellationToken.None;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (game is null)
            {
                if (_current.State == CoverState.None)
                {
                    return;
                }

                CancelPending();
                _current = Cover.Nothing;
            }
            else if (_current.State != CoverState.None && _current.RomId == game.RomId)
            {
                return;
            }
            else
            {
                CancelPending();

                if (_cache.TryGet(game.RomId, out var held))
                {
                    _current = new Cover(CoverState.Ready, game.RomId, held);
                }
                else
                {
                    _current = new Cover(CoverState.Waiting, game.RomId);
                    _pending = new CancellationTokenSource();
                    token = _pending.Token;
                    read = game;
                }
            }
        }

        if (read is not null)
        {
            // Only RomM waits for the rest. This device's own copy costs no request.
            var wait = GameCover.Local(_session, read.RomId) is null ? _rest : TimeSpan.Zero;
            _ = ReadAsync(read, wait, token);
        }
    }

    private async Task ReadAsync(BrowseGame game, TimeSpan wait, CancellationToken token)
    {
        byte[]? bytes;

        try
        {
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time, token).ConfigureAwait(false);
            }

            bytes = await GameCover.ReadAsync(_session, game, _connection(), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            // Broad, for the reason ListScreen's loader is: this reads a disk, a database and a
            // server, and a throw left uncaught would hold the box on Waiting for good. Art is
            // never what a screen fails over, so the box says there is none.
            bytes = null;
        }

        lock (_gate)
        {
            // Moved on while this was reading. What it read is still worth keeping.
            if (bytes is not null)
            {
                _cache.Add(game.RomId, bytes);
            }

            if (_disposed || token.IsCancellationRequested || _current.RomId != game.RomId)
            {
                return;
            }

            _current = bytes is null
                ? new Cover(CoverState.Missing, game.RomId)
                : new Cover(CoverState.Ready, game.RomId, bytes);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CancelPending()
    {
        // Canceled, never disposed: a read still unwinding can register on this token.
        _pending?.Cancel();
        _pending = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CancelPending();
        }
    }
}
