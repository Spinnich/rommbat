using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>One line on the status screen.</summary>
/// <param name="Detail">The second line, when the value needs explaining. Usually null.</param>
public sealed record StatusRow(string Label, string Value, string? Detail = null);

/// <summary>A named group of rows, in the order they are shown.</summary>
public sealed record StatusSection(string Title, IReadOnlyList<StatusRow> Rows);

/// <summary>
/// What this device is, what it is paired to, and what is waiting to happen.
/// </summary>
/// <remarks>
/// <b>Read-only, and it computes nothing.</b> Every value here already exists behind a Core
/// API that the <c>status</c> and <c>saves</c> subcommands read: this arranges them for a
/// screen and formats them, and that is the whole of its job. If a row ever needs a decision
/// Core cannot answer, the fix is an API on Core with a test, not a method here.
/// <para>
/// <b>The network is not touched.</b> Reachability is its own screen concern with its own
/// timeout, because an unreachable LAN host must never be something the status screen waits
/// on: offline is a working state, not an error, and this whole screen is answerable with the
/// server switched off.
/// </para>
/// </remarks>
public sealed class StatusViewModel : IScreen
{
    private readonly InstallSession _session;

    public StatusViewModel(InstallSession session, GamepadStatus gamepad)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(gamepad);

        _session = session;
        Gamepad = gamepad;
    }

    public GamepadStatus Gamepad { get; }

    public string Title => "RomMBat";

    /// <summary>True when this install has never been paired.</summary>
    public bool NeedsPairing => _session.Store.Device.Read()?.IsPaired != true;

    /// <summary>
    /// True when there is a token but it has run out.
    /// </summary>
    /// <remarks>
    /// A scoped, expiring token is the recommended default on a portable drive, so reaching
    /// this is ordinary rather than a fault, and the only thing to do about it is pair again.
    /// </remarks>
    public bool TokenExpired =>
        _session.Store.Device.Read() is { IsPaired: true } device
        && device.IsTokenExpired(DateTimeOffset.UtcNow);

    public IReadOnlyList<FooterHint> Hints =>
    [
        // Pairing is reachable whether or not this install is paired. Re-pairing is how a user
        // moves to a different server, and how they recover an expired or rejected token: M1
        // makes it deliberately cheap, and a screen that hides it strands them.
        new FooterHint("A", NeedsPairing ? "Pair with RomM" : "Pair again", 3),
        new FooterHint("B", "Back to EmulationStation", 2),
    ];

    /// <summary>
    /// Where the pairing flow starts, once there is one.
    /// </summary>
    /// <remarks>
    /// Set by the shell rather than constructed here, because pairing needs a connection and a
    /// cancellation token that this screen has no business owning. Null until 7b-1's pairing
    /// screen is wired, and accept does nothing rather than opening a blank screen.
    /// </remarks>
    public Func<IScreen>? StartPairing { get; init; }

    public ScreenCommand Handle(NavAction action) => action switch
    {
        NavAction.Accept when StartPairing is { } start => ScreenCommand.Push(start()),

        // Back on the root screen leaves RomMBat, which the navigator turns into an exit. The
        // user came from the EmulationStation menu and that is where they go.
        NavAction.Back => ScreenCommand.Pop,

        _ => ScreenCommand.Stay,
    };

    /// <summary>Everything the screen shows, rebuilt on demand rather than cached.</summary>
    public IReadOnlyList<StatusSection> Sections() =>
    [
        Device(),
        Server(),
        Waiting(),
        Controller(),
    ];

    private StatusSection Device()
    {
        var install = _session.Install;
        var store = _session.Store;

        return new StatusSection("This device", [
            new StatusRow("RetroBat", install.ReadVersionString() ?? "not readable"),
            new StatusRow("Compatibility", install.CheckVersion().Verdict.ToString()),
            new StatusRow("Local store", $"schema {store.SchemaVersion} of {LocalStore.ExpectedSchemaVersion}"),
        ]);
    }

    private StatusSection Server()
    {
        var device = _session.Store.Device.Read();
        var clock = _session.Store.Clock.Read();

        if (device is null || !device.IsPaired)
        {
            return new StatusSection("RomM", [
                new StatusRow("Paired", "no", "Press A to pair this device with your RomM server."),
            ]);
        }

        var rows = new List<StatusRow>
        {
            new("Server", device.ServerOrigin?.ToString() ?? "not configured"),
            new("Paired", "yes"),
            new("Device id", device.RomMDeviceId ?? "none"),
            new("Last contact", Describe(clock.LastContactUtc)),
        };

        if (TokenExpired)
        {
            // Said here rather than discovered as a failure the next time something syncs.
            rows.Add(new StatusRow(
                "Token",
                "expired",
                "Press A to pair again. Your saves, states and settings are kept."));
        }

        // Degradations are a granted-scope consequence, worked out by Core. Shown because a
        // feature silently missing is worse than a late 403.
        foreach (var (requirement, missing) in device.Scopes.Degradations)
        {
            rows.Add(new StatusRow(
                "Feature off",
                requirement.Name,
                $"missing {string.Join(", ", missing)}: {requirement.WithoutIt}"));
        }

        if (clock.IsSkewSuspicious && clock.Skew is { } skew)
        {
            rows.Add(new StatusRow(
                "Clock",
                $"{skew.TotalSeconds:0}s out",
                "Saves made offline still order correctly, but fix the clock when you can."));
        }

        return new StatusSection("RomM", rows);
    }

    /// <summary>
    /// What has happened that has not reached the server, or the disk, yet.
    /// </summary>
    /// <remarks>
    /// <b>The queued-configuration rows are the reason migration <c>012</c> exists.</b> Until
    /// this screen there was no reader for them outside the agent, so a user who queued a
    /// change had no way to see that it was waiting or why. A queued row cannot be applied
    /// while EmulationStation is running, and this UI only ever runs while it is, so "waiting
    /// for you to quit EmulationStation" is the honest and permanent answer rather than a
    /// transient one.
    /// </remarks>
    private StatusSection Waiting()
    {
        var store = _session.Store;
        var outbox = store.Outbox.PendingCount();
        var conflicts = store.SaveConflicts.ListOpen().Count;
        var queued = store.PendingConfig.ListOutstanding();

        var rows = new List<StatusRow>
        {
            new(
                "Outbox",
                outbox == 0 ? "empty" : Plural(outbox, "item"),
                outbox == 0 ? null : "Saves, states and play sessions waiting for the server."),
            new(
                "Conflicts",
                conflicts == 0 ? "none" : Plural(conflicts, "save"),
                conflicts == 0 ? null : "Both sides were kept. Nothing was overwritten."),
            new(
                "Queued changes",
                queued.Count == 0 ? "none" : Plural(queued.Count, "change"),
                queued.Count == 0
                    ? null
                    : "Applied when you next quit EmulationStation, which cannot happen while it is running."),
        };

        rows.AddRange(queued.Select(change => new StatusRow(
            "  waiting",
            $"{change.System} / {change.FsName}",
            change.Reason)));

        return new StatusSection("Waiting", rows);
    }

    private StatusSection Controller() =>
        new("Controller", [
            new StatusRow(
                Gamepad.DeviceName ?? "None",
                Gamepad.Availability.ToString(),
                Gamepad.IsReady ? null : Gamepad.Detail),
        ]);

    private static string Plural(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static string Describe(DateTimeOffset? moment) =>
        moment is { } at ? at.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) : "never";
}
