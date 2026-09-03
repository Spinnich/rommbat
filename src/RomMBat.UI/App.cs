using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;

namespace RomMBat.UI;

/// <summary>
/// The application, and the one place the whole thing is wired together.
/// </summary>
/// <remarks>
/// <b>Built in code rather than XAML.</b> There is one window and one theme, so a markup file
/// would add a compiler step and a second place to look for two lines of setup.
/// <para>
/// <b>A refusal is a screen, not a crash.</b> An install RomMBat will not run against, a store
/// from a newer build, or a tree it cannot find are all states <see cref="InstallSession"/>
/// hands back as words. The console agent prints them and exits; here they have to be shown,
/// because there is no console behind a full-screen window and an exit code reaches nobody.
/// </para>
/// </remarks>
internal sealed class App : Application
{
    private InstallSession? _session;
    private GamepadReader? _gamepad;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Build(desktop);
            desktop.Exit += (_, _) => Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ShellWindow Build(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var opened = InstallSession.Open(Program.ExplicitRoot);

        if (opened.Session is null)
        {
            // Nothing else can be shown: without a tree there is no store, no map and no pad.
            return new ShellWindow(
                new Navigator(new MessageScreen("RomMBat cannot start", opened.Message!)),
                gamepad: null,
                () => desktop.Shutdown());
        }

        _session = opened.Session;

        var map = EsInputMap.Read(_session.Install);
        _gamepad = GamepadReader.Open(_session.Install, map);

        // Built once and handed to whatever needs it. A sync the server refuses part way
        // through has exactly one thing a person can do about it, and it is this.
        IScreen StartPairing() => new OnScreenKeyboard(
                "Pair with RomM",
                "Type your RomM server address, then press Start.",
                // https by default: a RomM behind anything but a LAN address wants it, and it
                // is two characters to delete against eight to type on a d-pad.
                _session.Store.Settings.Get(UiSettings.LastServerOrigin) ?? "https://",
                Typed,
                _session.EmulationStationLanguage());

        var root = RootScreens.Menu(_session, () => _gamepad.Status, new RootScreens.RootRoutes
        {
            StartPairing = StartPairing,
            OpenSets = () => SetsScreens.List(_session, connect: null, pair: StartPairing),
            OpenBrowse = () => BrowseViewModel.Start(_session),
            OpenBudget = () => new BudgetViewModel(_session),
            OpenConflicts = () => ConflictScreens.List(_session, connect: null, pair: StartPairing),
            OpenPlatforms = () => PlatformScreens.List(_session),
        });

        return new ShellWindow(new Navigator(root), _gamepad, () => desktop.Shutdown());
    }

    /// <summary>
    /// Turns what was typed into the next screen, or into the reason it cannot be.
    /// </summary>
    /// <remarks>
    /// <b>The rule and its words are Core's.</b> <see cref="InstallSession.ResolveOrigin"/>
    /// already decides what counts as a server address and says why when it does not, and the
    /// console has used the same answer since M1. Re-deciding it here would be the exact shape
    /// of logic leaking into presentation.
    /// <para>
    /// Remembered before it is used, and whether or not pairing then succeeds, so a failed
    /// attempt never makes anyone retype a URL on a d-pad.
    /// </para>
    /// </remarks>
    private TypedResult Typed(string text)
    {
        var choice = _session!.ResolveOrigin(text);

        if (choice.Origin is not { } origin)
        {
            return new TypedResult(null, choice.Problem);
        }

        _session.Store.Settings.Set(UiSettings.LastServerOrigin, text, DateTimeOffset.UtcNow);

        return new TypedResult(new PairingViewModel(_session, origin));
    }

    private void Shutdown()
    {
        _gamepad?.Dispose();
        _session?.Dispose();
    }
}

/// <summary>Keys this front end remembers, in the store that already exists for them.</summary>
/// <remarks>
/// <b>No migration was needed and none was added.</b> <c>setting</c> is free-form key and
/// value, so what the shell remembers fits without a schema change. Deliberately not kept:
/// window geometry, which is full screen and not a choice; a controller-layout override, which
/// <c>es_input.cfg</c> already answers; and the last screen visited, which is a mild
/// anti-feature for a menu entered on purpose.
/// </remarks>
internal static class UiSettings
{
    /// <summary>
    /// The server address as last typed, whether or not pairing then succeeded.
    /// </summary>
    /// <remarks>
    /// Kept apart from <c>device.ServerOrigin</c>, which pairing writes only on success. The
    /// point of this one is that a failed pairing does not make a user retype a URL on a
    /// gamepad, which is the step the risks table calls the hostile one.
    /// </remarks>
    public const string LastServerOrigin = "ui.last_server_origin";
}
