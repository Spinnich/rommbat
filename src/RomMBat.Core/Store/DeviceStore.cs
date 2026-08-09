using Microsoft.Data.Sqlite;
using RomM.Client;
using RomMBat.Core.Identity;

namespace RomMBat.Core.Store;

/// <summary>What the local store knows about this device and its pairing.</summary>
public sealed record DeviceRecord(
    string ClientDeviceIdentifier,
    Uri? ServerOrigin,
    string? RomMDeviceId,
    string? DeviceName,
    GrantedScopes Scopes,
    StoredToken? Token,
    DateTimeOffset? PairedAt,
    DateTimeOffset? LastSeenAt)
{
    /// <summary>True when there is a server, a device id and a token to use against them.</summary>
    public bool IsPaired => Token is not null && ServerOrigin is not null && !string.IsNullOrEmpty(RomMDeviceId);

    /// <summary>True when the server told us the token expires and that moment has passed.</summary>
    public bool IsTokenExpired(DateTimeOffset now) => Token?.ExpiresAt is { } expiry && now >= expiry;
}

/// <summary>What a successful pairing produced.</summary>
public sealed record PairingResult(
    Uri ServerOrigin,
    string RomMDeviceId,
    string DeviceName,
    GrantedScopes Scopes,
    StoredToken Token);

/// <summary>
/// The singleton device row: identity, pairing and the token at rest.
/// </summary>
public sealed class DeviceStore
{
    private readonly SqliteConnection _connection;

    internal DeviceStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Reads the device row, or null when this install has never had one.</summary>
    public DeviceRecord? Read()
    {
        using var command = _connection.Command(
            """
            SELECT client_device_identifier, server_origin, romm_device_id, device_name,
                   granted_scopes, token_protection, token_cipher, token_nonce, token_tag,
                   token_kdf_salt, token_kdf_iterations, token_expires_at, paired_at, last_seen_at
            FROM device WHERE id = 1;
            """);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var scopes = new GrantedScopes(
            (reader.GetStringOrNull(4) ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var originText = reader.GetStringOrNull(1);
        var origin = Uri.TryCreate(originText, UriKind.Absolute, out var parsed) ? parsed : null;

        return new DeviceRecord(
            reader.GetString(0),
            origin,
            reader.GetStringOrNull(2),
            reader.GetStringOrNull(3),
            scopes,
            ReadToken(reader),
            reader.GetTimestampOrNull(12),
            reader.GetTimestampOrNull(13));
    }

    /// <summary>
    /// Creates the device row with an identity, leaving it unpaired.
    /// </summary>
    /// <remarks>
    /// The identifier is generated once and kept in <c>emulators/rommbat/device.id</c>; this
    /// mirrors it so a single read answers every question <c>status</c> asks.
    /// </remarks>
    public void EnsureIdentity(string clientDeviceIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientDeviceIdentifier);

        using var command = _connection.Command(
            """
            INSERT INTO device (id, client_device_identifier) VALUES (1, $identifier)
            ON CONFLICT (id) DO UPDATE SET client_device_identifier = excluded.client_device_identifier;
            """)
            .With("$identifier", clientDeviceIdentifier);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Remembers which server to call, before there is a pairing to record.
    /// </summary>
    /// <remarks>
    /// The server URL is the one thing that still has to be typed, and it is the real
    /// gamepad-hostile step, so it is remembered as soon as the address is known to answer
    /// rather than only after a pairing succeeds. A retry after a lapsed code should not cost
    /// the user the typing again.
    /// </remarks>
    public void SaveServerOrigin(Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        using var command = _connection
            .Command("UPDATE device SET server_origin = $origin WHERE id = 1;")
            .With("$origin", origin.ToString());

        command.ExecuteNonQuery();
    }

    /// <summary>Records everything a successful pairing produced.</summary>
    public void SavePairing(PairingResult result, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(result);

        using var command = _connection.Command(
            """
            UPDATE device SET
              server_origin        = $origin,
              romm_device_id       = $deviceId,
              device_name          = $deviceName,
              granted_scopes       = $scopes,
              token_protection     = $protection,
              token_cipher         = $cipher,
              token_nonce          = $nonce,
              token_tag            = $tag,
              token_kdf_salt       = $salt,
              token_kdf_iterations = $iterations,
              token_expires_at     = $expiresAt,
              paired_at            = $pairedAt,
              last_seen_at         = $pairedAt
            WHERE id = 1;
            """)
            .With("$origin", result.ServerOrigin.ToString())
            .With("$deviceId", result.RomMDeviceId)
            .With("$deviceName", result.DeviceName)
            .With("$scopes", string.Join(' ', result.Scopes.All))
            .With("$protection", result.Token.Mode == TokenProtectionMode.Passphrase ? "passphrase" : "none")
            .With("$cipher", result.Token.Cipher)
            .With("$nonce", SqliteValues.OrNull(result.Token.Nonce))
            .With("$tag", SqliteValues.OrNull(result.Token.Tag))
            .With("$salt", SqliteValues.OrNull(result.Token.Salt))
            .With("$iterations", SqliteValues.OrNull(result.Token.Iterations))
            .With("$expiresAt", SqliteValues.ToTextOrNull(result.Token.ExpiresAt))
            .With("$pairedAt", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Drops the token and nothing else, which is what a 401 means.
    /// </summary>
    /// <remarks>
    /// The database, the outbox and the identity all stay. Re-pairing on the same
    /// <c>client_device_identifier</c> updates the existing RomM device and the flush picks
    /// up where it left off; losing the outbox here would cost the user data for an expiry
    /// that was expected.
    /// </remarks>
    public void ClearToken()
    {
        using var command = _connection.Command(
            """
            UPDATE device SET
              token_protection     = 'none',
              token_cipher         = NULL,
              token_nonce          = NULL,
              token_tag            = NULL,
              token_kdf_salt       = NULL,
              token_kdf_iterations = NULL,
              token_expires_at     = NULL
            WHERE id = 1;
            """);

        command.ExecuteNonQuery();
    }

    /// <summary>Records that the server answered just now.</summary>
    public void TouchLastSeen(DateTimeOffset now)
    {
        using var command = _connection
            .Command("UPDATE device SET last_seen_at = $now WHERE id = 1;")
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    private static StoredToken? ReadToken(SqliteDataReader reader)
    {
        var cipher = reader.GetBlobOrNull(6);
        if (cipher is null)
        {
            return null;
        }

        var mode = string.Equals(reader.GetStringOrNull(5), "passphrase", StringComparison.Ordinal)
            ? TokenProtectionMode.Passphrase
            : TokenProtectionMode.None;

        return new StoredToken(
            mode,
            cipher,
            reader.GetBlobOrNull(7),
            reader.GetBlobOrNull(8),
            reader.GetBlobOrNull(9),
            (int?)reader.GetInt64OrNull(10),
            reader.GetTimestampOrNull(11));
    }
}
