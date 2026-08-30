using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Why the controller cannot be read, in the words the user gets.</summary>
public enum GamepadAvailability
{
    /// <summary>A configured pad is being read.</summary>
    Ready,

    /// <summary>EmulationStation's SDL2 is missing or would not load.</summary>
    NoLibrary,

    /// <summary>Nothing is plugged in.</summary>
    NoDevice,

    /// <summary>A pad is attached that EmulationStation has never been configured for.</summary>
    NotConfigured,
}

/// <summary>What the reader found, and what to tell the user about it.</summary>
public sealed record GamepadStatus(
    GamepadAvailability Availability,
    string? DeviceName,
    string? DeviceGuid,
    string Detail)
{
    public bool IsReady => Availability == GamepadAvailability.Ready;
}

/// <summary>
/// Reads the controller, and reports which of EmulationStation's input names are held.
/// </summary>
/// <remarks>
/// <b>This decides what a press means; it does not decide what it does.</b> The names it
/// returns are EmulationStation's own vocabulary (<c>a</c>, <c>up</c>, <c>start</c>) read from
/// <see cref="EsInputMap"/>, so there is no controller layout to detect anywhere in RomMBat and
/// no vendor-id table to keep current. Turning a held name into a focus move or an activation,
/// including repeat rate and edge detection, belongs to the front end.
/// <para>
/// <b>A pad reports every name it satisfies, not one.</b> On the pads measured, <c>select</c>
/// and <c>hotkey</c> are the same button and a diagonal hat is two directions at once.
/// </para>
/// <para>
/// <b>Nothing here throws and every failure is a state with words.</b> A pad
/// EmulationStation has never been shown is <see cref="GamepadAvailability.NotConfigured"/>
/// rather than an error, because that pad cannot drive the user's own front end either, and
/// the fix is to configure it there rather than anything RomMBat can do.
/// </para>
/// </remarks>
public sealed class GamepadReader : IDisposable
{
    /// <summary>Past this, a stick or trigger counts as pushed. Half travel.</summary>
    private const short AxisThreshold = 16000;

    /// <summary>The name EmulationStation writes, and the one it leaves to be inferred.</summary>
    public static IReadOnlyList<(string Bound, string Opposite)> StickOpposites { get; } =
    [
        ("joystick1up", "joystick1down"),
        ("joystick1left", "joystick1right"),
        ("joystick2up", "joystick2down"),
        ("joystick2left", "joystick2right"),
    ];

    /// <summary>
    /// How often a reader with no controller looks for one.
    /// </summary>
    /// <remarks>
    /// Enumeration is cheap but not free, and a second is far below what a person waiting for
    /// their pad to wake up would notice. A reader that already has a controller does not scan
    /// at all: it asks that one handle whether it is still attached, which is a single call.
    /// </remarks>
    public static TimeSpan ScanInterval => TimeSpan.FromSeconds(1);

    private readonly bool _sdlStarted;
    private readonly EsInputMap? _map;

    private IntPtr _joystick;
    private EsInputDevice? _config;
    private int _buttons;
    private int _axes;
    private int _hats;
    private DateTimeOffset _nextScan = DateTimeOffset.MinValue;
    private bool _closed;

    private GamepadReader(GamepadStatus status, EsInputMap? map = null, bool sdlStarted = false)
    {
        Status = status;
        _map = map;
        _sdlStarted = sdlStarted;
    }

    /// <summary>
    /// What the reader found, which changes as controllers come and go.
    /// </summary>
    /// <remarks>
    /// Read it again rather than holding onto it: a caller that captured this once will still
    /// be saying "no controller is connected" while the user drives the interface with the pad
    /// they just switched on.
    /// </remarks>
    public GamepadStatus Status { get; private set; }

    /// <summary>Opens the first configured controller, or explains why it could not.</summary>
    /// <param name="map">
    /// The install's <c>es_input.cfg</c>. Passed in rather than read here so a caller that
    /// already has it does not read the file twice.
    /// </param>
    public static GamepadReader Open(RetroBatInstall install, EsInputMap map)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(map);

        if (SdlLibrary.Load(install) is { } problem)
        {
            return Unavailable(GamepadAvailability.NoLibrary, problem);
        }

        if (SdlLibrary.SDL_Init(SdlLibrary.InitJoystick) != 0)
        {
            return Unavailable(
                GamepadAvailability.NoLibrary,
                $"SDL could not start its joystick subsystem: {SdlLibrary.Error()}");
        }

        var reader = new GamepadReader(NoDevice, map, sdlStarted: true);
        reader.Scan(DateTimeOffset.UtcNow);
        return reader;
    }

    /// <summary>One controller SDL currently reports, before anything has been opened.</summary>
    public sealed record GamepadCandidate(int Index, string DeviceName, string DeviceGuid);

    /// <summary>Which of them the reader will use, and what to tell the user either way.</summary>
    public sealed record GamepadChoice(GamepadCandidate? Device, GamepadStatus Status);

    /// <summary>
    /// Picks the controller to read from everything connected.
    /// </summary>
    /// <remarks>
    /// <b>Separated from the SDL calls because this is the part with rules in it.</b> Which pad
    /// wins, and what a user is told when none of them is configured, are decisions worth
    /// asserting; enumerating a native library is not.
    /// <para>
    /// <b>First configured pad wins.</b> Player assignment is EmulationStation's business and
    /// nothing in this UI is two-player. A pad EmulationStation has never been shown loses to a
    /// configured one further down the list, because index order is arbitrary and a virtual pad
    /// from a streaming host frequently sorts first.
    /// </para>
    /// </remarks>
    public static GamepadChoice Choose(IReadOnlyList<GamepadCandidate> connected, EsInputMap map)
    {
        ArgumentNullException.ThrowIfNull(connected);
        ArgumentNullException.ThrowIfNull(map);

        if (connected.Count == 0)
        {
            return new GamepadChoice(null, NoDevice);
        }

        foreach (var candidate in connected)
        {
            if (map.ForGuid(candidate.DeviceGuid) is not null)
            {
                return new GamepadChoice(
                    candidate,
                    new GamepadStatus(
                        GamepadAvailability.Ready,
                        candidate.DeviceName,
                        candidate.DeviceGuid,
                        $"Reading {candidate.DeviceName}."));
            }
        }

        var first = connected[0];
        var named = string.IsNullOrWhiteSpace(first.DeviceName) ? "That controller" : first.DeviceName;

        return new GamepadChoice(
            null,
            new GamepadStatus(
                GamepadAvailability.NotConfigured,
                first.DeviceName,
                first.DeviceGuid,
                $"{named} is connected but EmulationStation has no configuration for it. "
                    + "Configure it in EmulationStation first, and RomMBat will use the same "
                    + "buttons."));
    }

    private static GamepadStatus NoDevice =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    /// <summary>
    /// Every EmulationStation input name currently held.
    /// </summary>
    /// <remarks>
    /// A set rather than a sequence, because one physical press can satisfy two names and the
    /// caller wants each of them once.
    /// </remarks>
    public IReadOnlySet<string> Held()
    {
        var held = new HashSet<string>(StringComparer.Ordinal);

        if (_closed || !_sdlStarted)
        {
            return held;
        }

        // Before the count is read, because this is what makes SDL notice a device arriving.
        SdlLibrary.SDL_JoystickUpdate();
        Follow(DateTimeOffset.UtcNow);

        if (_config is null || _joystick == IntPtr.Zero)
        {
            return held;
        }

        for (var button = 0; button < _buttons; button++)
        {
            Add(held, EsInputKind.Button, button, SdlLibrary.SDL_JoystickGetButton(_joystick, button));
        }

        for (var hat = 0; hat < _hats; hat++)
        {
            Add(held, EsInputKind.Hat, hat, SdlLibrary.SDL_JoystickGetHat(_joystick, hat));
        }

        for (var axis = 0; axis < _axes; axis++)
        {
            // An axis binding names a direction, so the reading is reduced to its sign. An
            // analog trigger rests fully negative rather than centred (finding 223), so a
            // resting trigger reads -1 and matches nothing, where "non-zero means pressed"
            // would report both triggers held forever.
            var raw = SdlLibrary.SDL_JoystickGetAxis(_joystick, axis);
            var sign = raw switch
            {
                > AxisThreshold => 1,
                < -AxisThreshold => -1,
                _ => 0,
            };

            Add(held, EsInputKind.Axis, axis, sign);
            AddOppositeStickDirection(held, axis, sign);
        }

        return held;
    }

    /// <summary>
    /// The half of each stick axis EmulationStation does not write down.
    /// </summary>
    /// <remarks>
    /// <b>`es_input.cfg` records one direction per axis, not two.</b> A stick is configured as
    /// `joystick1up` on axis 1 at -1, and pushing the same axis to +1 is down, which the file
    /// never names because ES infers it. Without this a stick can move a menu up and never
    /// down, which reads as a broken pad rather than a missing rule.
    /// <para>
    /// The four synthesised names are not ES vocabulary and no `inputConfig` will ever contain
    /// them, which is deliberate: they say plainly that they were derived rather than read.
    /// </para>
    /// </remarks>
    private void AddOppositeStickDirection(HashSet<string> held, int axis, int sign)
    {
        if (sign == 0)
        {
            return;
        }

        foreach (var (bound, opposite) in StickOpposites)
        {
            if (_config!.Find(bound) is { Kind: EsInputKind.Axis } binding
                && binding.Id == axis
                && Math.Sign(binding.Value) == -sign)
            {
                held.Add(opposite);
            }
        }
    }

    private void Add(HashSet<string> held, EsInputKind kind, int id, int value)
    {
        foreach (var name in _config!.Meanings(kind, id, value))
        {
            held.Add(name);
        }
    }

    /// <summary>
    /// Keeps up with a controller that arrives, leaves, or comes back.
    /// </summary>
    /// <remarks>
    /// <b>Without this the interface is deaf to any pad that was not already awake at launch.</b>
    /// A wireless controller asleep in its cradle, a pad whose batteries went during a session,
    /// and a virtual pad from a streaming host that attaches only once the client sends input
    /// are all the same shape, and all three end with a person on a sofa holding a controller
    /// that does nothing. The recovery cannot be "restart RomMBat", because reaching the thing
    /// that restarts it needs the controller.
    /// <para>
    /// <b>A lost pad does not announce itself.</b> Reading a handle whose device has gone away
    /// is not an error: every button comes back released and every axis centred, which is
    /// exactly what a controller nobody is touching looks like. <c>SDL_JoystickGetAttached</c>
    /// is the only way to tell those two apart, so the ordinary frame pays one call for it and
    /// nothing else.
    /// </para>
    /// </remarks>
    private void Follow(DateTimeOffset now)
    {
        if (_joystick != IntPtr.Zero)
        {
            if (SdlLibrary.SDL_JoystickGetAttached(_joystick) != 0)
            {
                return;
            }

            Release();
            Status = NoDevice;
        }

        if (now < _nextScan)
        {
            return;
        }

        Scan(now);
    }

    private void Scan(DateTimeOffset now)
    {
        _nextScan = now + ScanInterval;

        var count = SdlLibrary.SDL_NumJoysticks();
        var connected = new List<GamepadCandidate>(Math.Max(count, 0));

        for (var index = 0; index < count; index++)
        {
            connected.Add(new GamepadCandidate(
                index,
                SdlLibrary.NameForIndex(index),
                SdlLibrary.GuidForIndex(index)));
        }

        var choice = Choose(connected, _map!);
        Status = choice.Status;

        if (choice.Device is null)
        {
            return;
        }

        var joystick = SdlLibrary.SDL_JoystickOpen(choice.Device.Index);
        if (joystick == IntPtr.Zero)
        {
            // Enumerated and then would not open, which happens while a device is still
            // settling. Do not claim to be reading it; the next scan tries again.
            Status = NoDevice;
            return;
        }

        _joystick = joystick;
        _config = _map!.ForGuid(choice.Device.DeviceGuid);
        _buttons = SdlLibrary.SDL_JoystickNumButtons(joystick);
        _axes = SdlLibrary.SDL_JoystickNumAxes(joystick);
        _hats = SdlLibrary.SDL_JoystickNumHats(joystick);
    }

    private void Release()
    {
        if (_joystick != IntPtr.Zero)
        {
            SdlLibrary.SDL_JoystickClose(_joystick);
        }

        _joystick = IntPtr.Zero;
        _config = null;
        _buttons = 0;
        _axes = 0;
        _hats = 0;
    }

    /// <param name="sdlStarted">
    /// True once <c>SDL_Init</c> has succeeded, which is the only condition under which
    /// <see cref="Dispose"/> may call back into the library. Getting this wrong throws
    /// <c>DllNotFoundException</c> out of a <c>using</c> on an install with no SDL2, which in a
    /// full-screen front end with no console behind it is a black screen.
    /// </param>
    private static GamepadReader Unavailable(
        GamepadAvailability availability,
        string detail,
        bool sdlStarted = false) =>
        new(new GamepadStatus(availability, null, null, detail), sdlStarted: sdlStarted);

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        if (!_sdlStarted)
        {
            // Never loaded, so there is nothing to call and calling anyway throws.
            return;
        }

        if (_joystick != IntPtr.Zero)
        {
            SdlLibrary.SDL_JoystickClose(_joystick);
        }

        // The subsystem only, never SDL_Quit: this process did not necessarily start SDL for
        // this alone, and tearing the whole library down is not ours to do.
        SdlLibrary.SDL_QuitSubSystem(SdlLibrary.InitJoystick);
    }
}
