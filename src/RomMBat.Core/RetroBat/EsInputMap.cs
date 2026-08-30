using System.Globalization;
using System.Xml.Linq;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>What kind of physical input a binding names.</summary>
public enum EsInputKind
{
    /// <summary>A joystick button. Its <c>value</c> is always 1 and carries no meaning.</summary>
    Button,

    /// <summary>A joystick axis. Its <c>value</c> is the direction, -1 or 1.</summary>
    Axis,

    /// <summary>A joystick hat. Its <c>value</c> is a direction bit: 1 up, 2 right, 4 down, 8 left.</summary>
    Hat,

    /// <summary>A keyboard key, identified by its SDL keycode.</summary>
    Key,
}

/// <summary>One physical input, and what EmulationStation calls it on this device.</summary>
/// <param name="Name">
/// The name from ES's own fixed vocabulary: <c>a</c>, <c>b</c>, <c>x</c>, <c>y</c>, <c>up</c>,
/// <c>start</c>, <c>hotkey</c> and the rest. This is the whole point of the file.
/// </param>
public sealed record EsInputBinding(string Name, EsInputKind Kind, int Id, int Value);

/// <summary>Everything EmulationStation knows about one keyboard or controller.</summary>
public sealed record EsInputDevice(
    string DeviceName,
    string DeviceGuid,
    IReadOnlyList<EsInputBinding> Bindings)
{
    /// <summary>The keyboard entry carries this in place of a GUID.</summary>
    public const string KeyboardGuid = "-1";

    public bool IsKeyboard => string.Equals(DeviceGuid, KeyboardGuid, StringComparison.Ordinal);

    /// <summary>The GUID in the form to compare a running SDL joystick against.</summary>
    public string MatchGuid => EsInputMap.NormalizeGuid(DeviceGuid);

    /// <summary>The physical input this device uses for a name, or null when it has none.</summary>
    public EsInputBinding? Find(string name) =>
        Bindings.FirstOrDefault(binding => string.Equals(binding.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Every name this physical state satisfies.
    /// </summary>
    /// <remarks>
    /// <b>A list rather than one name, because one press really can mean two things.</b> On
    /// both the 8BitDo and the Xbox 360 pad in the measured file, <c>select</c> and
    /// <c>hotkey</c> are the same button, and a hat pushed diagonally reports two direction
    /// bits at once. Returning the first match would silently drop half of that.
    /// </remarks>
    /// <param name="value">
    /// The live reading: pressed or not for a button and a key, the direction bitmask for a
    /// hat, and a sign for an axis. A resting input yields nothing.
    /// </param>
    public IReadOnlyList<string> Meanings(EsInputKind kind, int id, int value)
    {
        if (value == 0)
        {
            return [];
        }

        return [.. Bindings
            .Where(binding => binding.Kind == kind && binding.Id == id && Satisfies(binding, kind, value))
            .Select(binding => binding.Name)];
    }

    private static bool Satisfies(EsInputBinding binding, EsInputKind kind, int value) => kind switch
    {
        // A hat's value is a bitmask, so a diagonal satisfies both of its directions.
        EsInputKind.Hat => binding.Value != 0 && (value & binding.Value) == binding.Value,

        // An axis binding records a direction, and the reading only counts in that direction.
        EsInputKind.Axis => Math.Sign(value) == Math.Sign(binding.Value),

        _ => true,
    };
}

/// <summary>
/// The parsed <c>es_input.cfg</c>: what every button on every configured pad means.
/// </summary>
/// <remarks>
/// <b>This is why RomMBat detects no controller layout at all.</b> The file does not say a pad
/// is an Xbox pad or a Nintendo pad; it says which physical button on that pad is <c>a</c>,
/// which is the question a layout lookup is only ever a guess at. Measured on the live 8.2.1
/// install: the 8BitDo Ultimate 2 maps a=0/b=1/x=3/y=2, byte-identical to the Xbox 360 pad,
/// while the Switch Pro maps a=1/b=0/x=2/y=3 <i>and</i> reports its d-pad as buttons 11-14
/// where the other four report a hat. A vendor-id table cannot express that last difference,
/// and Argosy's <c>ControllerDetector</c> would classify vendor <c>0x2dc8</c> as a Nintendo
/// layout, which is wrong for the 8BitDo it names.
/// <para>
/// Read the <b>live</b> file, never a vendored copy, for the same reason
/// <see cref="EsSystemsFile"/> is: it records the user's own configuration, including any
/// remapping they did, and a user who reconfigured their pad in EmulationStation has said
/// what they want.
/// </para>
/// <para>
/// The ids are SDL <b>joystick</b> indices, as written by the SDL that EmulationStation
/// itself links. They are only meaningful to a reader using the same library, which is why
/// RomMBat opens <c>emulationstation/SDL2.dll</c> rather than shipping its own build.
/// </para>
/// </remarks>
public sealed class EsInputMap
{
    private EsInputMap(IReadOnlyList<EsInputDevice> devices, string? problem = null)
    {
        Devices = devices;
        Problem = problem;
    }

    /// <summary>Where the file lives, relative to the RetroBat root.</summary>
    public static RelativePath Location { get; } =
        RelativePath.Create("emulationstation/.emulationstation/es_input.cfg");

    /// <summary>Every configured device, in file order.</summary>
    public IReadOnlyList<EsInputDevice> Devices { get; }

    /// <summary>
    /// Why this map is empty, when the file was there but could not be read. Null otherwise.
    /// </summary>
    /// <remarks>
    /// Carried so the reason survives the degrade. Without it an unreadable file and a pad
    /// EmulationStation has never been shown look identical from the status screen, and the
    /// person reading it has no keyboard to tell them apart with.
    /// </remarks>
    public string? Problem { get; }

    /// <summary>The keyboard entry, which EmulationStation always writes.</summary>
    public EsInputDevice? Keyboard => Devices.FirstOrDefault(device => device.IsKeyboard);

    /// <summary>Every entry that is not the keyboard.</summary>
    public IReadOnlyList<EsInputDevice> Controllers =>
        [.. Devices.Where(device => !device.IsKeyboard)];

    /// <summary>
    /// The configuration for a running joystick, matched on its GUID.
    /// </summary>
    /// <remarks>
    /// Normalises both sides, because the same pad has two GUID spellings. See
    /// <see cref="NormalizeGuid"/>.
    /// </remarks>
    /// <returns>Null when EmulationStation has never been shown this pad.</returns>
    public EsInputDevice? ForGuid(string? sdlGuid)
    {
        if (string.IsNullOrWhiteSpace(sdlGuid))
        {
            return null;
        }

        var wanted = NormalizeGuid(sdlGuid);

        return Devices.FirstOrDefault(device =>
            !device.IsKeyboard
            && string.Equals(device.MatchGuid, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears bytes 2 and 3 of an SDL joystick GUID, which hold a CRC-16 of the device name.
    /// </summary>
    /// <remarks>
    /// <b>Measured, and a straight comparison never matches without it.</b> SDL 2.0.18 and
    /// later fill that field at runtime; the GUID EmulationStation writes into
    /// <c>es_input.cfg</c> leaves it zeroed. The 8BitDo Ultimate 2 on the live install is
    /// <c>03000000c82d0000...</c> in the file and <c>0300b155c82d0000...</c> from the running
    /// library, identical in every other byte.
    /// <para>
    /// Anything that is not a 32-character GUID, the keyboard's <c>-1</c> included, is
    /// returned unchanged so it can still compare equal to itself.
    /// </para>
    /// </remarks>
    public static string NormalizeGuid(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return identifier.Length == 32 && identifier.All(Uri.IsHexDigit)
            ? string.Concat(identifier[..4], "0000", identifier[8..])
            : identifier;
    }

    /// <summary>
    /// Reads the live file from an install, and never throws.
    /// </summary>
    /// <remarks>
    /// <b>An unreadable file degrades to an empty map, it does not propagate.</b> The front end
    /// that reads this is a full-screen <c>WinExe</c> and calls it before any window exists, so
    /// a throw here is an exit with nothing on screen and no console to say why, on a device
    /// whose whole premise is that there is no keyboard. Half-written is not an exotic state
    /// either: EmulationStation rewrites this file every time a pad is configured, so an
    /// interrupted write is the ordinary way to arrive at one. The reason is kept in
    /// <see cref="Problem"/> rather than dropped.
    /// </remarks>
    /// <returns>An empty map when the file is absent or unreadable. Both are ordinary states.</returns>
    public static EsInputMap Read(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var path = install.Resolve(Location);

        if (!File.Exists(path))
        {
            return new EsInputMap([]);
        }

        try
        {
            return Load(path);
        }
        catch (EsInputException ex)
        {
            return new EsInputMap([], ex.Message);
        }
        catch (IOException ex)
        {
            // A write in progress holds the file, which is the same interrupted-write case.
            return new EsInputMap([], $"es_input.cfg could not be opened: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new EsInputMap([], $"es_input.cfg could not be opened: {ex.Message}");
        }
    }

    /// <summary>Parses one <c>es_input.cfg</c>.</summary>
    /// <exception cref="EsInputException">The content is not readable XML.</exception>
    public static EsInputMap Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new EsInputException($"es_input.cfg is not readable XML: {ex.Message}", ex);
        }

        var devices = document.Root?
            .Elements("inputConfig")
            .Select(ReadDevice)
            .ToList() ?? [];

        return new EsInputMap(devices);
    }

    private static EsInputDevice ReadDevice(XElement config) => new(
        (string?)config.Attribute("deviceName") ?? string.Empty,
        (string?)config.Attribute("deviceGUID") ?? string.Empty,
        [.. config.Elements("input").Select(ReadBinding).OfType<EsInputBinding>()]);

    /// <returns>Null for an input whose type is not one of the four ES writes.</returns>
    private static EsInputBinding? ReadBinding(XElement input)
    {
        var name = (string?)input.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var kind = (string?)input.Attribute("type") switch
        {
            "button" => EsInputKind.Button,
            "axis" => EsInputKind.Axis,
            "hat" => EsInputKind.Hat,
            "key" => EsInputKind.Key,
            _ => (EsInputKind?)null,
        };

        return kind is { } resolved
            ? new EsInputBinding(name, resolved, ReadInt(input, "id"), ReadInt(input, "value"))
            : null;
    }

    private static int ReadInt(XElement input, string attribute) =>
        int.TryParse(
            (string?)input.Attribute(attribute),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
}

/// <summary>Thrown when <c>es_input.cfg</c> cannot be parsed.</summary>
/// <remarks>
/// <see cref="EsInputMap.Read"/> turns this back into an empty map: a caller that named a path
/// wants to know, and the front end reading the live file has nowhere to show it.
/// </remarks>
public sealed class EsInputException : Exception
{
    public EsInputException(string message)
        : base(message)
    {
    }

    public EsInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EsInputException()
        : base("es_input.cfg could not be read.")
    {
    }
}
