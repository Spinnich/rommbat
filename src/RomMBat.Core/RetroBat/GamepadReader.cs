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

    private readonly bool _sdlStarted;
    private readonly IntPtr _joystick;
    private readonly EsInputDevice? _config;
    private readonly int _buttons;
    private readonly int _axes;
    private readonly int _hats;

    private bool _closed;

    private GamepadReader(
        GamepadStatus status,
        bool sdlStarted = false,
        IntPtr joystick = default,
        EsInputDevice? config = null,
        int buttons = 0,
        int axes = 0,
        int hats = 0)
    {
        Status = status;
        _sdlStarted = sdlStarted;
        _joystick = joystick;
        _config = config;
        _buttons = buttons;
        _axes = axes;
        _hats = hats;
    }

    public GamepadStatus Status { get; }

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

        var count = SdlLibrary.SDL_NumJoysticks();
        if (count <= 0)
        {
            return Unavailable(
                GamepadAvailability.NoDevice,
                "No controller is connected.",
                sdlStarted: true);
        }

        // First configured pad wins. Player assignment is EmulationStation's business and
        // nothing in this UI is two-player.
        string? attachedName = null;
        string? attachedGuid = null;

        for (var index = 0; index < count; index++)
        {
            var joystick = SdlLibrary.SDL_JoystickOpen(index);
            if (joystick == IntPtr.Zero)
            {
                continue;
            }

            var guid = SdlLibrary.GuidOf(joystick);
            var name = SdlLibrary.NameOf(joystick);
            var config = map.ForGuid(guid);

            if (config is null)
            {
                attachedName ??= name;
                attachedGuid ??= guid;
                SdlLibrary.SDL_JoystickClose(joystick);
                continue;
            }

            return new GamepadReader(
                new GamepadStatus(GamepadAvailability.Ready, name, guid, $"Reading {name}."),
                sdlStarted: true,
                joystick,
                config,
                SdlLibrary.SDL_JoystickNumButtons(joystick),
                SdlLibrary.SDL_JoystickNumAxes(joystick),
                SdlLibrary.SDL_JoystickNumHats(joystick));
        }

        return new GamepadReader(
            new GamepadStatus(
                GamepadAvailability.NotConfigured,
                attachedName,
                attachedGuid,
                $"{attachedName ?? "That controller"} is connected but EmulationStation has no "
                    + "configuration for it. Configure it in EmulationStation first, and RomMBat "
                    + "will use the same buttons."),
            sdlStarted: true);
    }

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

        if (_closed || _config is null || _joystick == IntPtr.Zero)
        {
            return held;
        }

        SdlLibrary.SDL_JoystickUpdate();

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
        new(new GamepadStatus(availability, null, null, detail), sdlStarted);

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
