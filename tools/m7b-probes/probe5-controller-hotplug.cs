// M7b probe 5: does RomMBat notice a controller that goes away and comes back?
//
// The one claim in the hotplug fix that no test reaches. Removing the SDL_JoystickGetAttached
// check leaves the whole suite green, because a unit test cannot switch a pad off.
//
// Drives the SHIPPED GamepadReader rather than a copy, so what this measures is what the UI
// does. It has to be a window: SDL 2.32.8 defaults to the RAWINPUT backend, which needs a Win32
// message pump, and a console process reports zero joysticks while three are attached (finding
// 226).
//
// Run it, press a few buttons, switch the controller OFF, wait, switch it back ON, press a few
// more, then close with Escape. The log is the artefact.

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;

namespace Probe5;

internal static class Program
{
    private static string _root = @"K:\RetroBat";
    private static string _logPath = "probe5.log";

    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--root")
            {
                _root = args[i + 1];
            }
            else if (args[i] == "--log")
            {
                _logPath = args[i + 1];
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_logPath))!);
        File.WriteAllText(_logPath, string.Empty);

        AppBuilder.Configure<ProbeApp>()
            .UseWin32()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args);
    }

    public static string Root => _root;

    public static void Log(string line)
    {
        var stamped = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:HH:mm:ss.fff}  {line}");

        Console.WriteLine(stamped);
        File.AppendAllText(_logPath, stamped + Environment.NewLine, Encoding.UTF8);
    }
}

internal sealed class ProbeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ProbeWindow(() => desktop.Shutdown());
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ProbeWindow : Window
{
    private readonly GamepadReader _reader;
    private readonly TextBlock _availability = new() { FontSize = 30, Foreground = Brushes.White };
    private readonly TextBlock _device = new() { FontSize = 20, Foreground = Brushes.Gray };
    private readonly TextBlock _held = new() { FontSize = 24, Foreground = Brushes.Aqua };
    private readonly TextBlock _log = new() { FontSize = 15, Foreground = Brushes.Gray };

    private GamepadAvailability? _lastAvailability;
    private string _lastHeld = string.Empty;
    private readonly List<string> _recent = [];

    public ProbeWindow(Action exit)
    {
        var install = new RetroBatInstall(Program.Root, RootDiscoverySource.Explicit);
        var map = EsInputMap.Read(install);

        Program.Log($"es_input.cfg holds {map.Controllers.Count} controllers");

        _reader = GamepadReader.Open(install, map);

        Title = "RomMBat hotplug probe";
        Width = 900;
        Height = 520;
        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18));

        var stack = new StackPanel { Margin = new Thickness(28), Spacing = 14 };
        stack.Children.Add(new TextBlock
        {
            Text = "Press buttons. Then switch the controller OFF, wait, switch it ON, press again. Escape closes.",
            FontSize = 17,
            Foreground = Brushes.Goldenrod,
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(_availability);
        stack.Children.Add(_device);
        stack.Children.Add(_held);
        stack.Children.Add(_log);
        Content = stack;

        Focusable = true;
        Opened += (_, _) => Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Program.Log("closing");
                _reader.Dispose();
                exit();
            }
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
        timer.Tick += (_, _) => Poll();
        timer.Start();

        Poll();
    }

    private void Poll()
    {
        var held = _reader.Held();
        var status = _reader.Status;

        if (status.Availability != _lastAvailability)
        {
            _lastAvailability = status.Availability;
            Note($"[{status.Availability}] {status.DeviceName ?? "(no device)"} :: {status.Detail}");
        }

        var line = held.Count == 0
            ? string.Empty
            : string.Join(" + ", held.OrderBy(name => name, StringComparer.Ordinal));

        if (!string.Equals(line, _lastHeld, StringComparison.Ordinal))
        {
            _lastHeld = line;

            if (line.Length > 0)
            {
                Note($"held: {line}");
            }
        }

        _availability.Text = status.Availability.ToString();
        _device.Text = $"{status.DeviceName ?? "(none)"}   {status.DeviceGuid ?? string.Empty}";
        _held.Text = line.Length == 0 ? "(nothing held)" : line;
    }

    private void Note(string line)
    {
        Program.Log(line);

        _recent.Add($"{DateTimeOffset.Now:HH:mm:ss.fff}  {line}");
        if (_recent.Count > 12)
        {
            _recent.RemoveAt(0);
        }

        _log.Text = string.Join(Environment.NewLine, _recent);
    }
}
