using Avalonia.Input;
using RomMBat.Core.RetroBat;

namespace RomMBat.UI.Input;

/// <summary>
/// What each physical key means, read from <c>es_input.cfg</c>'s keyboard entry.
/// </summary>
/// <remarks>
/// <b>The same file the pad is read from, so a RetroBat user's keys work here as they do in
/// EmulationStation.</b> ES stores a key as an SDL keycode, and a user's remap is in that file;
/// a fixed table here would be a second input system that disagrees with it.
/// <para>
/// <b>Three aliases fill the gaps and never override the file</b>: the arrow keys, Escape as
/// Back and Enter as Accept. Each applies only to a key the file leaves unbound, so ES's own
/// Enter (Start, which opens the menu) is kept, and an install with no keyboard entry still has
/// something to press. With no entry at all, ES's shipped keys apply.
/// </para>
/// </remarks>
public sealed class KeyboardBindings
{
    /// <summary>ES's shipped keyboard, used when the file has no keyboard entry.</summary>
    private static readonly (string Name, int Code)[] Shipped =
    [
        ("a", 'x'), ("b", 'z'), ("x", 'q'), ("y", 's'),
        ("start", 13), ("select", 8),
        ("up", SdlKey.Up), ("down", SdlKey.Down), ("left", SdlKey.Left), ("right", SdlKey.Right),
        ("pageup", SdlKey.PageUp), ("pagedown", SdlKey.PageDown),
        ("l2", SdlKey.Home), ("r2", SdlKey.End),
    ];

    private static readonly (string Name, int Code)[] Aliases =
    [
        ("up", SdlKey.Up), ("down", SdlKey.Down), ("left", SdlKey.Left), ("right", SdlKey.Right),
        ("b", SdlKey.Escape), ("a", SdlKey.Return),
    ];

    private readonly Dictionary<int, List<string>> _names = [];

    private KeyboardBindings(IEnumerable<(string Name, int Code)> bindings)
    {
        foreach (var (name, code) in bindings)
        {
            Add(code, name);
        }

        foreach (var (name, code) in Aliases)
        {
            if (!_names.ContainsKey(code))
            {
                Add(code, name);
            }
        }
    }

    /// <summary>Reads the map's keyboard entry, or ES's shipped keys when it has none.</summary>
    public static KeyboardBindings From(EsInputMap? map)
    {
        var keys = map?.Keyboard?.Bindings
            .Where(binding => binding.Kind == EsInputKind.Key)
            .Select(binding => (binding.Name, binding.Id))
            .ToArray();

        return new KeyboardBindings(keys is { Length: > 0 } ? keys : Shipped);
    }

    /// <summary>The EmulationStation names a key stands for, none when it is unbound.</summary>
    public IReadOnlyList<string> Names(Key key) =>
        SdlKey.From(key) is { } code && _names.TryGetValue(code, out var names) ? names : [];

    /// <summary>The names every key in <paramref name="down"/> stands for, as the pad reports them.</summary>
    public IReadOnlySet<string> Held(IEnumerable<Key> down) =>
        down.SelectMany(Names).ToHashSet(StringComparer.Ordinal);

    /// <summary>The key that performs an action, as a person reads it, or null when none does.</summary>
    public string? KeyFor(NavAction action) => NameOf(action) is { } name ? KeyFor(name) : null;

    private static string? NameOf(NavAction action) => action switch
    {
        NavAction.Up => "up",
        NavAction.Down => "down",
        NavAction.Left => "left",
        NavAction.Right => "right",
        NavAction.Accept => "a",
        NavAction.Back => "b",
        NavAction.Alternate => "y",
        NavAction.Extra => "x",
        NavAction.Start => "start",
        NavAction.PageUp => "pageup",
        NavAction.PageDown => "pagedown",
        NavAction.Options => "select",
        NavAction.PreviousGroup => "l2",
        NavAction.NextGroup => "r2",
        _ => null,
    };

    /// <summary>The key bound to a name, as a person reads it, or null when none is.</summary>
    public string? KeyFor(string name)
    {
        var code = _names.FirstOrDefault(pair => pair.Value.Contains(name)).Key;
        return code == 0 ? null : SdlKey.Label(code);
    }

    private void Add(int code, string name)
    {
        if (!_names.TryGetValue(code, out var names))
        {
            _names[code] = names = [];
        }

        if (!names.Contains(name))
        {
            names.Add(name);
        }
    }
}

/// <summary>SDL's keycodes, which are what <c>es_input.cfg</c> records for a key.</summary>
/// <remarks>
/// A key with a printable character is that character in lowercase; every other key is its
/// scancode with bit 30 set.
/// </remarks>
internal static class SdlKey
{
    private const int Scancode = 1 << 30;

    private const int FirstFunction = Scancode | 58;

    public const int Return = 13;
    public const int Escape = 27;
    public const int Backspace = 8;
    public const int Right = Scancode | 79;
    public const int Left = Scancode | 80;
    public const int Down = Scancode | 81;
    public const int Up = Scancode | 82;
    public const int Home = Scancode | 74;
    public const int PageUp = Scancode | 75;
    public const int End = Scancode | 77;
    public const int PageDown = Scancode | 78;

    /// <summary>The SDL keycode for an Avalonia key, or null for one ES cannot bind.</summary>
    public static int? From(Key key) => key switch
    {
        >= Key.A and <= Key.Z => 'a' + (key - Key.A),
        >= Key.D0 and <= Key.D9 => '0' + (key - Key.D0),
        >= Key.F1 and <= Key.F12 => Scancode | (58 + (key - Key.F1)),
        Key.Enter => Return,
        Key.Escape => Escape,
        Key.Back => Backspace,
        Key.Delete => 127,
        Key.Tab => 9,
        Key.Insert => Scancode | 73,
        Key.Space => 32,
        Key.Right => Right,
        Key.Left => Left,
        Key.Down => Down,
        Key.Up => Up,
        Key.Home => Home,
        Key.PageUp => PageUp,
        Key.End => End,
        Key.PageDown => PageDown,
        Key.LeftCtrl => Scancode | 224,
        Key.LeftShift => Scancode | 225,
        Key.LeftAlt => Scancode | 226,
        Key.RightCtrl => Scancode | 228,
        Key.RightShift => Scancode | 229,
        Key.RightAlt => Scancode | 230,
        _ => null,
    };

    /// <summary>A name for a keycode, for a footer that shows keys.</summary>
    public static string Label(int code) => code switch
    {
        Return => "Enter",
        Escape => "Esc",
        Backspace => "Backspace",
        127 => "Delete",
        9 => "Tab",
        Scancode | 73 => "Insert",
        32 => "Space",
        Right => "Right",
        Left => "Left",
        Down => "Down",
        Up => "Up",
        Home => "Home",
        End => "End",
        PageUp => "PgUp",
        PageDown => "PgDn",
        Scancode | 224 => "LCtrl",
        Scancode | 225 => "LShift",
        Scancode | 226 => "LAlt",
        Scancode | 228 => "RCtrl",
        Scancode | 229 => "RShift",
        Scancode | 230 => "RAlt",
        _ when code is >= FirstFunction and <= FirstFunction + 11 => "F" + (code - FirstFunction + 1),
        // A printable character is its own label. Anything else this table cannot name would
        // otherwise be cast to a wrong letter, so it is said plainly.
        >= 33 and <= 126 => char.ToUpperInvariant((char)code).ToString(),
        _ => "Key " + code,
    };
}
