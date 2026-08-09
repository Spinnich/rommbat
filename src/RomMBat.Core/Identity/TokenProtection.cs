using System.Security.Cryptography;
using System.Text;

namespace RomMBat.Core.Identity;

/// <summary>How the access token is protected at rest.</summary>
public enum TokenProtectionMode
{
    /// <summary>
    /// Stored as plaintext inside the RetroBat tree. The default, and stated plainly rather
    /// than dressed up.
    /// </summary>
    None,

    /// <summary>AES-GCM under a key derived from a user passphrase.</summary>
    Passphrase,
}

/// <summary>A token as it sits in the database.</summary>
public sealed record StoredToken(
    TokenProtectionMode Mode,
    byte[] Cipher,
    byte[]? Nonce,
    byte[]? Tag,
    byte[]? Salt,
    int? Iterations,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Protecting the access token at rest, without DPAPI.
/// </summary>
/// <remarks>
/// <b>DPAPI is unavailable to us.</b> <c>DataProtectionScope.CurrentUser</c> binds the
/// ciphertext to one user profile on one machine and <c>LocalMachine</c> binds it to that
/// machine, so either choice makes a portable drive undecryptable on the next PC. M0 probe 7
/// moved a stick between two machines under two different Windows users, which is the case
/// that has to keep working.
/// <para>
/// So the honest position is: <b>on a portable install the token is only as protected as the
/// drive.</b> The mitigations are the ones RomM's own guidance recommends, an expiring
/// scoped token and cheap re-pairing, plus an optional passphrase for anyone who wants it.
/// The passphrase is a real trade rather than a free win: a passphrase-protected install
/// cannot flush its outbox unattended, because nothing can decrypt the token without
/// someone typing it.
/// </para>
/// </remarks>
public static class TokenProtector
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 iterations, at OWASP's current recommendation. Stored alongside the
    /// ciphertext so this can be raised later without stranding an existing database.
    /// </summary>
    public const int DefaultIterations = 600_000;

    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    /// <summary>Wraps a token for storage.</summary>
    /// <param name="token">The raw <c>rmm_</c> token.</param>
    /// <param name="passphrase">Null or empty for <see cref="TokenProtectionMode.None"/>.</param>
    /// <param name="expiresAt">What the server said, so expiry is visible without a call.</param>
    public static StoredToken Protect(string token, string? passphrase, DateTimeOffset? expiresAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var plaintext = Encoding.UTF8.GetBytes(token);

        if (string.IsNullOrEmpty(passphrase))
        {
            return new StoredToken(TokenProtectionMode.None, plaintext, null, null, null, null, expiresAt);
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var key = DeriveKey(passphrase, salt, DefaultIterations);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(key, TagBytes))
        {
            aes.Encrypt(nonce, plaintext, cipher, tag);
        }

        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plaintext);

        return new StoredToken(
            TokenProtectionMode.Passphrase,
            cipher,
            nonce,
            tag,
            salt,
            DefaultIterations,
            expiresAt);
    }

    /// <summary>Unwraps a stored token.</summary>
    /// <exception cref="TokenUnlockException">The passphrase is wrong or missing.</exception>
    public static string Unprotect(StoredToken stored, string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(stored);

        if (stored.Mode == TokenProtectionMode.None)
        {
            return Encoding.UTF8.GetString(stored.Cipher);
        }

        if (string.IsNullOrEmpty(passphrase))
        {
            throw new TokenUnlockException("This install's token is passphrase-protected, and no passphrase was given.");
        }

        if (stored.Nonce is null || stored.Tag is null || stored.Salt is null || stored.Iterations is null)
        {
            throw new TokenUnlockException("The stored token is incomplete. Pair again.");
        }

        var key = DeriveKey(passphrase, stored.Salt, stored.Iterations.Value);
        var plaintext = new byte[stored.Cipher.Length];

        try
        {
            using var aes = new AesGcm(key, stored.Tag.Length);
            aes.Decrypt(stored.Nonce, stored.Cipher, stored.Tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            // AES-GCM authenticates, so a wrong passphrase fails here rather than returning
            // rubbish. Nothing distinguishes a wrong passphrase from tampering, and neither
            // should be reported as the other.
            throw new TokenUnlockException("The passphrase did not unlock the stored token.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);
}

/// <summary>Thrown when a stored token cannot be unlocked.</summary>
public sealed class TokenUnlockException : Exception
{
    public TokenUnlockException(string message)
        : base(message)
    {
    }

    public TokenUnlockException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TokenUnlockException()
        : base("The stored token could not be unlocked.")
    {
    }
}
