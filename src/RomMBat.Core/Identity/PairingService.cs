using System.Globalization;
using RomM.Client;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Identity;

/// <summary>How a pairing attempt ended, and what it produced.</summary>
/// <param name="Outcome">The last poll's verdict.</param>
/// <param name="Scopes">What was actually granted, which may be less than was asked for.</param>
/// <param name="RomMDeviceId">The device RomM created or updated.</param>
/// <param name="TokenExpiresAt">When the token stops working, or null when it never does.</param>
/// <param name="Message">Ready to show the user.</param>
public sealed record PairingCompletion(
    PairingOutcome Outcome,
    GrantedScopes Scopes,
    string? RomMDeviceId,
    DateTimeOffset? TokenExpiresAt,
    string Message)
{
    public bool IsPaired => Outcome == PairingOutcome.Approved;
}

/// <summary>
/// Device pairing, from generating the identity to writing the token down.
/// </summary>
/// <remarks>
/// Pairing is the only authentication path. There is no password entry, no token pasting,
/// no <c>/api/token</c> flow and not <c>POST /api/client-tokens/exchange</c> either, because
/// a gamepad is a terrible keyboard and the flow exists so the credential never has to be
/// typed.
/// </remarks>
public sealed class PairingService
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly TimeProvider _time;

    public PairingService(RetroBatInstall install, LocalStore store, TimeProvider? timeProvider = null)
    {
        _install = install;
        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Records the identity and the server to call, before any pairing is attempted.
    /// </summary>
    /// <returns>The <c>client_device_identifier</c> this install pairs under.</returns>
    public string RememberServer(Uri origin)
    {
        var identifier = DeviceIdentity.ReadOrCreate(_install);
        _store.Device.EnsureIdentity(identifier);
        _store.Device.SaveServerOrigin(origin);
        return identifier;
    }

    /// <summary>
    /// Starts a pairing request and returns the code and QR target to display.
    /// </summary>
    /// <param name="connection">An unauthenticated connection to the configured origin.</param>
    /// <param name="deviceName">The label to show in the RomM device list.</param>
    public async Task<PairingSession> BeginAsync(
        RomMConnection connection,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var identifier = DeviceIdentity.ReadOrCreate(_install);
        _store.Device.EnsureIdentity(identifier);

        var payload = DevicePairing.BuildPayload(
            identifier,
            string.IsNullOrWhiteSpace(deviceName) ? DeviceIdentity.DefaultDeviceName() : deviceName,
            ClientVersion());

        var pairing = new DevicePairing(connection, _time);
        return await pairing.BeginAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls until the request resolves, and on approval writes the token and scopes down.
    /// </summary>
    /// <param name="passphrase">
    /// Optional. With one, the token is stored under AES-GCM; without one it is stored as
    /// plaintext inside the tree, which is the honest default on a portable drive. See
    /// <see cref="TokenProtector"/>.
    /// </param>
    public async Task<PairingCompletion> CompleteAsync(
        RomMConnection connection,
        PairingSession session,
        string? passphrase = null,
        IProgress<PairingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(session);

        var pairing = new DevicePairing(connection, _time);
        var result = await pairing.AwaitApprovalAsync(session, progress, cancellationToken).ConfigureAwait(false);

        if (result.Outcome != PairingOutcome.Approved || result.Token is null)
        {
            return new PairingCompletion(
                result.Outcome,
                GrantedScopes.None,
                null,
                null,
                result.Message ?? "Pairing did not complete.");
        }

        var scopes = new GrantedScopes(result.Token.Scopes);
        var expiresAt = ParseExpiry(result.Token.Expires_at);
        var protectedToken = TokenProtector.Protect(result.Token.Access_token, passphrase, expiresAt);

        _store.Device.SavePairing(
            new PairingResult(
                connection.Options.Origin,
                result.Token.Device_id,
                session.DeviceName,
                scopes,
                protectedToken),
            _time.GetUtcNow());

        return new PairingCompletion(
            PairingOutcome.Approved,
            scopes,
            result.Token.Device_id,
            expiresAt,
            BuildSummary(scopes, expiresAt));
    }

    /// <summary>
    /// Drops the stored token after a 401, keeping everything else.
    /// </summary>
    /// <remarks>
    /// An expired or revoked token must never cost data. The database, the outbox and the
    /// identity all survive, so re-pairing on the same identifier updates the same RomM
    /// device and the flush resumes.
    /// </remarks>
    public void DropTokenForRepairing() => _store.Device.ClearToken();

    /// <summary>Unlocks the stored token for use on an authenticated call.</summary>
    /// <exception cref="TokenUnlockException">There is no token, or the passphrase is wrong.</exception>
    public string UnlockToken(string? passphrase)
    {
        var device = _store.Device.Read()
            ?? throw new TokenUnlockException("This install has never been paired.");

        return device.Token is null
            ? throw new TokenUnlockException("This install has no stored token. Pair again.")
            : TokenProtector.Unprotect(device.Token, passphrase);
    }

    /// <summary>The client version reported to the server and shown in the device list.</summary>
    public static string ClientVersion() =>
        typeof(PairingService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static DateTimeOffset? ParseExpiry(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string BuildSummary(GrantedScopes scopes, DateTimeOffset? expiresAt)
    {
        var expiry = expiresAt is null
            ? "The token does not expire, which RomM's own guidance calls an anti-pattern for a "
                + "device that can be lost. Consider re-pairing with an expiry set."
            : $"The token expires {expiresAt.Value.ToUniversalTime():u}.";

        var degradations = scopes.Degradations;
        if (degradations.Count == 0)
        {
            return $"Paired with every scope RomMBat asked for. {expiry}";
        }

        var lost = string.Join(", ", degradations.Select(d => d.Requirement.Name));
        return $"Paired, but {degradations.Count} feature(s) are off because the grant was narrowed: {lost}. {expiry}";
    }
}
