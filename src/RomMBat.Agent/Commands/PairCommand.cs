using System.Globalization;
using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Server;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>pair</c>: device pairing, driven entirely by a QR or the 8-character code.
/// </summary>
/// <remarks>
/// The UI framework is chosen in M7, so this console is the M1 pairing surface. No UI
/// framework package exists anywhere in the tree yet, deliberately.
/// </remarks>
internal static class PairCommand
{
    /// <summary>How often the countdown redraws while polling.</summary>
    private static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(250);

    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var origin = context.ResolveOrigin(command, Console.Error);
        if (origin is null)
        {
            return ExitCode.Usage;
        }

        using var connection = AgentContext.Connect(origin);

        var contact = await ServerProbes.TryContactAsync(connection, context.Store, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (contact is null)
        {
            Console.Error.WriteLine(
                $"{origin} did not answer within {RomMClientOptions.InteractiveConnectTimeout.TotalSeconds:0} seconds. "
                    + "Pairing needs the server; everything else RomMBat does works offline.");
            return ExitCode.Offline;
        }

        if (contact.MustRefuse)
        {
            Console.Error.WriteLine(contact.Probe.Compatibility.Message);
            return ExitCode.Refused;
        }

        if (contact.Probe.Compatibility.Verdict == CompatibilityVerdict.Untested)
        {
            Console.Error.WriteLine($"Warning: {contact.Probe.Compatibility.Message}");
        }

        if (contact.IsSkewSuspicious && contact.Skew is { } skew)
        {
            Console.Error.WriteLine(
                $"Warning: this device's clock is {skew.TotalSeconds:0} seconds "
                    + $"{(skew > TimeSpan.Zero ? "ahead of" : "behind")} the server's. "
                    + "Saves made offline will still order correctly, but fix the clock when you can.");
        }

        var passphrase = ReadPassphraseIfRequested(command);
        if (passphrase is { Length: 0 })
        {
            return ExitCode.Usage;
        }

        var pairing = new PairingService(context.Install, context.Store);
        pairing.RememberServer(origin);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PairingSession session;
            try
            {
                session = await pairing.BeginAsync(connection, command.Value("name"), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RomMApiException ex)
            {
                Console.Error.WriteLine($"The server refused to start a pairing request: {ex.Message}");
                return ExitCode.Refused;
            }
            catch (RomMUnreachableException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCode.Offline;
            }

            WriteInstructions(session, origin);

            var (completion, restartRequested) =
                await PollWithCountdownAsync(pairing, connection, session, passphrase, cancellationToken)
                    .ConfigureAwait(false);

            if (restartRequested)
            {
                Console.WriteLine();
                Console.WriteLine("Starting a new pairing request.");
                Console.WriteLine();
                continue;
            }

            if (completion is null)
            {
                return ExitCode.Cancelled;
            }

            if (!completion.IsPaired)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(completion.Message);

                if (completion.Outcome == PairingOutcome.Expired && CanReadKeys())
                {
                    Console.Write("Press R to request a new code, or any other key to quit: ");
                    var key = Console.ReadKey(intercept: true);
                    Console.WriteLine();
                    if (key.Key == ConsoleKey.R)
                    {
                        continue;
                    }
                }

                return ExitCode.NotPaired;
            }

            WriteSuccess(completion);
            await VerifyDeviceAsync(pairing, origin, completion, passphrase, cancellationToken).ConfigureAwait(false);
            return ExitCode.Ok;
        }
    }

    private static void WriteInstructions(PairingSession session, Uri origin)
    {
        Console.WriteLine();
        Console.WriteLine($"Pairing with {origin}");
        Console.WriteLine();
        Console.WriteLine("Scan this with a phone, or open the address below and type the code:");
        Console.WriteLine();

        ConsoleQr.Write(PairingQrCode.Build(session.VerificationUri), Console.Out);

        Console.WriteLine();
        Console.WriteLine($"    Address:  {session.VerificationUri}");
        Console.WriteLine($"    Code:     {session.DisplayCode}");
        Console.WriteLine();
        Console.WriteLine("The code is not case sensitive, and the hyphen is only there to read it by.");
        Console.WriteLine();
        Console.WriteLine("RomM will ask which permissions to grant. RomMBat asks for these, and needs");
        Console.WriteLine("all of them for full two-way sync:");
        Console.WriteLine();
        Console.WriteLine($"    {string.Join(", ", RomMScopes.Requested)}");
        Console.WriteLine();
        Console.WriteLine("Granting fewer is supported and turns individual features off. Never grant");
        Console.WriteLine($"{string.Join(", ", RomMScopes.NeverNeeded.Order(StringComparer.Ordinal))}:");
        Console.WriteLine("RomMBat has no use for any of them.");
        Console.WriteLine();
    }

    /// <summary>
    /// Polls for approval while redrawing a countdown and watching for a restart key.
    /// </summary>
    /// <remarks>
    /// The countdown is not decoration. Pending state lives only in Redis with a hard 600 s
    /// TTL, so the code really does lapse and the user needs to see it coming and be able to
    /// ask for another one without restarting the process.
    /// </remarks>
    private static async Task<(PairingCompletion? Completion, bool RestartRequested)> PollWithCountdownAsync(
        PairingService pairing,
        RomMConnection connection,
        PairingSession session,
        string? passphrase,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var pollTask = pairing.CompleteAsync(connection, session, passphrase, progress: null, linked.Token);
        var restartRequested = false;
        var lastDrawn = -1;

        while (!pollTask.IsCompleted)
        {
            if (CanReadKeys() && Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.R)
                {
                    restartRequested = true;
                    await linked.CancelAsync().ConfigureAwait(false);
                    break;
                }

                if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                {
                    await linked.CancelAsync().ConfigureAwait(false);
                    break;
                }
            }

            var remaining = (int)session.RemainingAt(DateTimeOffset.UtcNow).TotalSeconds;
            if (remaining != lastDrawn && !Console.IsOutputRedirected)
            {
                lastDrawn = remaining;
                Console.Write(string.Create(
                    CultureInfo.InvariantCulture,
                    $"\rWaiting for approval. {remaining / 60:0}:{remaining % 60:00} left. Press R for a new code, Q to quit.   "));
            }

            try
            {
                await Task.WhenAny(pollTask, Task.Delay(RedrawInterval, linked.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (!Console.IsOutputRedirected)
        {
            Console.Write('\r');
            Console.Write(new string(' ', 78));
            Console.Write('\r');
        }

        try
        {
            return (await pollTask.ConfigureAwait(false), restartRequested);
        }
        catch (OperationCanceledException)
        {
            return (null, restartRequested);
        }
    }

    private static void WriteSuccess(PairingCompletion completion)
    {
        Console.WriteLine();
        Console.WriteLine("Paired.");
        Console.WriteLine($"    RomM device id:  {completion.RomMDeviceId}");
        Console.WriteLine($"    Granted scopes:  {string.Join(", ", completion.Scopes.All)}");
        Console.WriteLine(
            completion.TokenExpiresAt is { } expiry
                ? $"    Token expires:   {expiry.ToUniversalTime():u}"
                : "    Token expires:   never");

        var overGranted = completion.Scopes.OverGranted;
        if (overGranted.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"This token carries scopes RomMBat never uses: {string.Join(", ", overGranted)}.");
            Console.WriteLine("That usually means an admin account approved it rather than a purpose-made one.");
        }

        var degradations = completion.Scopes.Degradations;
        if (degradations.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("The grant was narrowed, so these features are off:");
        foreach (var (requirement, missing) in degradations)
        {
            Console.WriteLine($"    {requirement.Name}");
            Console.WriteLine($"        missing: {string.Join(", ", missing)}");
            Console.WriteLine($"        effect:  {requirement.WithoutIt}");
        }

        Console.WriteLine();
        Console.WriteLine("Pair again and grant the missing scopes to turn them back on.");
    }

    /// <summary>
    /// Confirms the token works and that this install shows up as one device, not two.
    /// </summary>
    /// <remarks>
    /// The whole point of pairing on a stored GUID rather than a MAC address is that moving
    /// the drive updates the existing device. This is the check that proves it.
    /// </remarks>
    private static async Task VerifyDeviceAsync(
        PairingService pairing,
        Uri origin,
        PairingCompletion completion,
        string? passphrase,
        CancellationToken cancellationToken)
    {
        if (!completion.Scopes.Has(RomMScopes.DevicesRead))
        {
            Console.WriteLine();
            Console.WriteLine("Skipping the device check: devices.read was not granted.");
            return;
        }

        string token;
        try
        {
            token = pairing.UnlockToken(passphrase);
        }
        catch (TokenUnlockException ex)
        {
            Console.Error.WriteLine($"Warning: {ex.Message}");
            return;
        }

        using var authenticated = AgentContext.ConnectAuthenticated(origin, token);

        try
        {
            var devices = await authenticated.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
            if (!devices.IsSuccess || devices.Value is null)
            {
                Console.Error.WriteLine($"Warning: could not read the device list back: {devices.Message}");
                return;
            }

            var matching = devices.Value.Count(device =>
                string.Equals(device.Id, completion.RomMDeviceId, StringComparison.Ordinal));

            Console.WriteLine();
            Console.WriteLine(matching == 1
                ? "Verified: the token works and this install is one device in RomM."
                : $"Warning: expected exactly one matching device, found {matching}.");
        }
        catch (RomMUnreachableException ex)
        {
            Console.Error.WriteLine($"Warning: could not verify the pairing, {ex.Message}");
        }
    }

    /// <summary>
    /// Reads a passphrase without echoing it, when <c>--protect</c> was passed.
    /// </summary>
    /// <returns>Null for no protection, or an empty string when the input was unusable.</returns>
    private static string? ReadPassphraseIfRequested(CommandLine command)
    {
        if (!command.Has("protect"))
        {
            return null;
        }

        if (!CanReadKeys())
        {
            Console.Error.WriteLine("--protect needs an interactive console to read the passphrase from.");
            return string.Empty;
        }

        Console.WriteLine();
        Console.WriteLine("A passphrase encrypts the stored token, which is worth doing on a drive that");
        Console.WriteLine("leaves your hands. It also means unattended syncs cannot run: nothing can");
        Console.WriteLine("decrypt the token without you typing this again.");
        Console.WriteLine();

        var first = ReadHidden("Passphrase: ");
        var second = ReadHidden("Repeat:     ");

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Those did not match.");
            return string.Empty;
        }

        if (string.IsNullOrEmpty(first))
        {
            Console.Error.WriteLine("An empty passphrase protects nothing. Run again without --protect.");
            return string.Empty;
        }

        return first;
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var builder = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }

    private static bool CanReadKeys() => !Console.IsInputRedirected;
}
