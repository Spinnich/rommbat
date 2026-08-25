// M7b probe 1: does a controller reach an Avalonia window, through RetroBat's own SDL2 and
// RetroBat's own es_input.cfg, and what happens to EmulationStation when this opens over it.
//
// Covers both design-moving probes in one run:
//   P1b  what arrives, under which name, and whether it keeps arriving unfocused
//   P2   z-order, focus, and whether ES keeps reading the pad while it is behind us
//
// Everything it observes goes to a log file as well as the screen, because the screen is
// covered by whatever it is measuring and nobody is holding a terminal.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RomMBat.Core.RetroBat;


namespace Probe1;

internal static class Program
{
    public static readonly Stopwatch Started = Stopwatch.StartNew();

    public static string LogPath { get; private set; } = "probe1.log";

    public static string SdlPath { get; private set; } = @"K:\RetroBat\emulationstation\SDL2.dll";

    public static string EsInputPath { get; private set; } =
        @"K:\RetroBat\emulationstation\.emulationstation\es_input.cfg";

    public static TimeSpan Budget { get; private set; } = TimeSpan.FromSeconds(180);

    /// <summary>
    /// Whether holding start closes the probe.
    /// </summary>
    /// <remarks>
    /// On by default because the ES-menu run has no keyboard to press Escape on. Off for the
    /// input sweep, where start is one of the inputs being swept and an exit gesture sharing a
    /// button with the test ends the run before it starts. It did, twice.
    /// </remarks>
    public static bool PadExit { get; private set; } = true;

    [STAThread]
    public static void Main(string[] args)
    {
        // Argument-free by default so it can be dropped in as RomMBat.exe and launched from
        // the ES menu, which passes nothing.
        var root = FindRetroBatRoot() ?? @"K:\RetroBat";

        SdlPath = Path.Combine(root, "emulationstation", "SDL2.dll");
        EsInputPath = Path.Combine(root, "emulationstation", ".emulationstation", "es_input.cfg");
        LogPath = Path.Combine(root, "emulators", "rommbat", "logs", "probe1-input.log");

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--log": LogPath = args[i + 1]; break;
                case "--sdl": SdlPath = args[i + 1]; break;
                case "--es-input": EsInputPath = args[i + 1]; break;
                case "--seconds":
                    Budget = TimeSpan.FromSeconds(double.Parse(args[i + 1], CultureInfo.InvariantCulture));
                    break;
                default: break;
            }
        }

        if (args.Contains("--no-pad-exit", StringComparer.Ordinal))
        {
            PadExit = false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        Log($"=== probe1 start, pid {Environment.ProcessId}, budget {Budget.TotalSeconds:0}s ===");
        Log($"root      {root}");
        Log($"sdl       {SdlPath}");
        Log($"es_input  {EsInputPath}");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        Log($"=== probe1 exit after {Started.Elapsed.TotalSeconds:0.0}s ===");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace();

    private static readonly Lock LogGate = new();

    public static void Log(string line)
    {
        var stamped = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.UtcNow:HH:mm:ss.fff} +{Started.Elapsed.TotalMilliseconds,8:0} {line}");

        lock (LogGate)
        {
            File.AppendAllText(LogPath, stamped + Environment.NewLine);
        }
    }

    /// <summary>Walks up from the executable to a RetroBat marker, as the real app does.</summary>
    private static string? FindRetroBatRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "retrobat.ini"))
                || Directory.Exists(Path.Combine(directory.FullName, "emulationstation")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal sealed partial class App : Application
{
    private readonly List<string> _recent = [];
    private readonly Dictionary<string, int> _state = new(StringComparer.Ordinal);
    private readonly List<Pad> _pads = [];

    private TextBlock? _screen;
    private EsInputMap? _map;
    private string _sdlStatus = "not loaded";
    private string _focus = "unknown";
    private DateTime? _startHeldSince;
    private string _lastForeground = string.Empty;
    private string _lastEs = string.Empty;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private IClassicDesktopStyleApplicationLifetime? _lifetime;

    private sealed record Pad(IntPtr Handle, string Name, string Guid, int Buttons, int Axes, int Hats, EsInputDevice? Config);

    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _lifetime = desktop;

        _screen = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontSize = 17,
            Margin = new Thickness(28),
            Text = "starting",
        };

        var window = new Window
        {
            Title = "RomMBat M7b probe 1",
            WindowState = WindowState.FullScreen,
            SystemDecorations = SystemDecorations.None,
            Topmost = false,
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18)),
            Content = new ScrollViewer { Content = _screen },
        };

        window.Opened += (_, _) => Program.Log($"window opened, first frame at {Program.Started.ElapsedMilliseconds} ms");
        window.Activated += (_, _) => { _focus = "activated"; Program.Log("window ACTIVATED"); };
        window.Deactivated += (_, _) => { _focus = "deactivated"; Program.Log("window DEACTIVATED"); };
        window.Closing += (_, _) => Program.Log("window closing");

        // Avalonia's own keyboard path, logged so we can say what arrives with no extra code.
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            Note($"avalonia key down {e.Key}");
            if (e.Key == Key.Escape)
            {
                Program.Log("escape pressed, shutting down");
                desktop.Shutdown();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        StartSdl();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (_, _) => Poll();
        timer.Start();

        var slow = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        slow.Tick += (_, _) =>
        {
            WatchDesktop();
            CheckExitHold();
            Redraw();
            if (Program.Started.Elapsed > Program.Budget)
            {
                Program.Log("budget reached, shutting down");
                desktop.Shutdown();
            }
        };
        slow.Start();

        desktop.MainWindow = window;
        base.OnFrameworkInitializationCompleted();
    }

    private void StartSdl()
    {
        try
        {
            _map = EsInputMap.Load(Program.EsInputPath);
            Program.Log($"es_input.cfg: {_map.Devices.Count} device(s) configured");
            foreach (var device in _map.Devices)
            {
                Program.Log($"  config: {device.DeviceName}  guid={device.DeviceGuid}  match={device.MatchGuid}");
            }
        }
        catch (Exception ex)
        {
            Program.Log($"es_input.cfg unreadable: {ex.GetType().Name}: {ex.Message}");
        }

        if (!Sdl.Load(Program.SdlPath, out var detail))
        {
            _sdlStatus = $"load failed: {detail}";
            Program.Log(_sdlStatus);
            return;
        }

        Program.Log(detail);

        // The question P1b exists to answer: does it keep reading while we are not focused.
        Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");

        if (Sdl.SDL_Init(Sdl.InitJoystick) != 0)
        {
            _sdlStatus = $"SDL_Init failed: {Sdl.Error()}";
            Program.Log(_sdlStatus);
            return;
        }

        var count = Sdl.SDL_NumJoysticks();
        _sdlStatus = $"{detail}, {count} pad(s)";
        Program.Log($"SDL_NumJoysticks = {count}");

        for (var i = 0; i < count; i++)
        {
            var handle = Sdl.SDL_JoystickOpen(i);
            if (handle == IntPtr.Zero)
            {
                Program.Log($"  pad {i}: open failed: {Sdl.Error()}");
                continue;
            }

            var guid = Sdl.GuidOf(handle);
            var zeroed = EsInputMap.NormalizeGuid(guid);
            var config = _map?.ForGuid(guid);

            var pad = new Pad(
                handle,
                Sdl.NameOf(handle),
                guid,
                Sdl.SDL_JoystickNumButtons(handle),
                Sdl.SDL_JoystickNumAxes(handle),
                Sdl.SDL_JoystickNumHats(handle),
                config);

            _pads.Add(pad);

            Program.Log($"  pad {i}: {pad.Name}");
            Program.Log($"          guid   {guid}");
            Program.Log($"          zeroed {zeroed}");
            Program.Log($"          counts {pad.Buttons}b {pad.Axes}a {pad.Hats}h");
            Program.Log($"          es_input match: {(config is null ? "NONE" : config.DeviceName)}");
        }
    }

    private void Poll()
    {
        if (_pads.Count == 0)
        {
            return;
        }

        Sdl.SDL_JoystickUpdate();

        for (var p = 0; p < _pads.Count; p++)
        {
            var pad = _pads[p];

            for (var b = 0; b < pad.Buttons; b++)
            {
                Change(pad, p, "button", b, Sdl.SDL_JoystickGetButton(pad.Handle, b));
            }

            for (var h = 0; h < pad.Hats; h++)
            {
                Change(pad, p, "hat", h, Sdl.SDL_JoystickGetHat(pad.Handle, h));
            }

            for (var a = 0; a < pad.Axes; a++)
            {
                var raw = Sdl.SDL_JoystickGetAxis(pad.Handle, a);
                Change(pad, p, "axis", a, raw switch { > 16000 => 1, < -16000 => -1, _ => 0 });
            }
        }
    }

    private void Change(Pad pad, int index, string type, int id, int value)
    {
        var key = $"p{index}.{type}{id}";
        var first = !_state.ContainsKey(key);

        if (_state.TryGetValue(key, out var previous) && previous == value)
        {
            return;
        }

        _state[key] = value;

        // The first observation of an input is its resting value, not a press.
        if (first)
        {
            return;
        }

        var meanings = pad.Config is null
            ? []
            : pad.Config.Meanings(ToKind(type), id, value);

        var label = meanings.Count == 0 ? key : $"{key} = {string.Join("+", meanings)}";

        Note($"{label} -> {value}   [{_focus}]");

        foreach (var name in meanings)
        {
            _seen.Add(name);
        }

        // Held, not tapped: start is one of the inputs the sweep asks for, so a press must
        // not be the exit gesture.
        if (meanings.Contains("start"))
        {
            _startHeldSince = value != 0 ? DateTime.UtcNow : null;
        }
    }

    private void CheckExitHold()
    {
        if (Program.PadExit
            && _startHeldSince is { } since
            && DateTime.UtcNow - since > TimeSpan.FromSeconds(2))
        {
            Program.Log("start held for 2s, shutting down");
            _lifetime?.Shutdown();
        }
    }

    /// Records who owns the foreground and whether EmulationStation is still alive.
    /// P2 rests on this: a probe that only says what it received cannot say what ES did.
    private void WatchDesktop()
    {
        var foreground = ForegroundTitle();
        if (!string.Equals(foreground, _lastForeground, StringComparison.Ordinal))
        {
            _lastForeground = foreground;
            Program.Log($"foreground -> {foreground}");
        }

        var processes = Process.GetProcessesByName("emulationstation");
        var es = processes.Length == 0
            ? "emulationstation: not running"
            : $"emulationstation: {processes.Length} process(es), pid {string.Join(",", processes.Select(p => p.Id))}";

        foreach (var process in processes)
        {
            process.Dispose();
        }

        if (!string.Equals(es, _lastEs, StringComparison.Ordinal))
        {
            _lastEs = es;
            Program.Log(es);
        }
    }

    private void Note(string line)
    {
        Program.Log(line);
        _recent.Add($"{DateTime.Now:HH:mm:ss.fff}  {line}");
        if (_recent.Count > 16)
        {
            _recent.RemoveAt(0);
        }
    }

    private void Redraw()
    {
        if (_screen is null)
        {
            return;
        }

        var text = new StringBuilder();
        text.AppendLine("RomMBat  M7b probe 1   input, focus and z-order");
        text.AppendLine(new string('-', 78));
        text.AppendLine(CultureInfo.InvariantCulture, $"SDL        {_sdlStatus}");
        text.AppendLine(CultureInfo.InvariantCulture, $"es_input   {_map?.Devices.Count ?? 0} device(s) configured");
        text.AppendLine(CultureInfo.InvariantCulture, $"window     {_focus}");
        text.AppendLine(CultureInfo.InvariantCulture, $"foreground {ForegroundTitle()}");
        text.AppendLine(CultureInfo.InvariantCulture,
            $"elapsed    {Program.Started.Elapsed.TotalSeconds:0}s of {Program.Budget.TotalSeconds:0}s");
        text.AppendLine();

        if (_pads.Count == 0)
        {
            text.AppendLine("NO PAD SEEN. Connect the controller; this refreshes on its own.");
        }

        foreach (var pad in _pads)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"pad  {pad.Name}  ({pad.Buttons}b {pad.Axes}a {pad.Hats}h)");
            text.AppendLine(CultureInfo.InvariantCulture, $"     guid    {pad.Guid}");
            text.AppendLine(CultureInfo.InvariantCulture,
                $"     mapping {(pad.Config is null ? "NO es_input.cfg ENTRY, names will be blank" : pad.Config.DeviceName)}");
        }

        var config = _pads.FirstOrDefault()?.Config;
        if (config is not null)
        {
            var all = config.Bindings.Select(b => b.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var missing = all.Where(n => !_seen.Contains(n)).ToList();

            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"seen      {_seen.Count} of {all.Count}");
            text.AppendLine(CultureInfo.InvariantCulture,
                $"still to press:  {(missing.Count == 0 ? "nothing, every input has been seen" : string.Join("  ", missing))}");
        }

        text.AppendLine();
        text.AppendLine(Program.PadExit
            ? "HOLD START for 2 seconds to exit, or Escape on a keyboard."
            : "This closes itself when the timer runs out. Start does NOT exit; press it freely.");
        text.AppendLine(new string('-', 78));
        foreach (var line in _recent)
        {
            text.AppendLine(line);
        }

        _screen.Text = text.ToString();
    }

    private static EsInputKind ToKind(string type) => type switch
    {
        "axis" => EsInputKind.Axis,
        "hat" => EsInputKind.Hat,
        _ => EsInputKind.Button,
    };

    private static string ForegroundTitle()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return "(none)";
        }

        var buffer = new char[256];
        var length = GetWindowText(handle, buffer, buffer.Length);
        var title = length > 0 ? new string(buffer, 0, length) : string.Empty;
        _ = GetWindowThreadProcessId(handle, out var pid);

        var name = "?";
        try
        {
            name = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            // Gone between the two calls, which is ordinary.
        }

        return $"{name} (pid {pid}) \"{title}\"";
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, [Out] char[] text, int count);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
