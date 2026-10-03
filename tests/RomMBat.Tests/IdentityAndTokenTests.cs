using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The device identity and the token at rest.
/// </summary>
public class IdentityAndTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_identifier_is_generated_once_and_then_read_back()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var first = DeviceIdentity.ReadOrCreate(install);
        var second = DeviceIdentity.ReadOrCreate(install);

        Assert.Equal(first, second);
        Assert.True(Guid.TryParse(first, out _));
        Assert.True(File.Exists(install.DeviceIdentityPath));
    }

    [Fact]
    public void The_identifier_lives_in_the_tree_and_nowhere_else()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        DeviceIdentity.ReadOrCreate(install);

        Assert.True(install.Contains(install.DeviceIdentityPath));
        Assert.StartsWith(
            Path.Combine(tree.Root, "emulators", "rommbat"),
            install.DeviceIdentityPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_identifier_survives_the_database_being_deleted()
    {
        // This is why it is a file rather than a row: a rebuilt store must not turn into a
        // second device in the RomM UI.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var identifier = DeviceIdentity.ReadOrCreate(install);

        using (var store = LocalStore.Open(install))
        {
            store.Device.EnsureIdentity(identifier);
        }

        File.Delete(install.DatabasePath);

        using var rebuilt = LocalStore.Open(install);
        rebuilt.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(install));

        Assert.Equal(identifier, rebuilt.Device.Read()?.ClientDeviceIdentifier);
    }

    [Fact]
    public void The_identifier_survives_the_tree_moving_to_a_new_location()
    {
        using var original = TempRetroBatTree.Create();
        var identifier = DeviceIdentity.ReadOrCreate(original.Install());

        using var moved = original.CopyToNewLocation();

        Assert.Equal(identifier, DeviceIdentity.Read(moved.Install()));
    }

    [Fact]
    public void A_corrupt_identity_file_is_replaced_rather_than_read_as_garbage()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        install.EnsureAppDirectories();
        File.WriteAllText(install.DeviceIdentityPath, "not a guid");

        Assert.Null(DeviceIdentity.Read(install));

        var replacement = DeviceIdentity.ReadOrCreate(install);

        Assert.True(Guid.TryParse(replacement, out _));
    }

    [Fact]
    public void An_unprotected_token_round_trips_and_is_stored_as_written()
    {
        // Stated plainly rather than dressed up: on a portable install the token is only as
        // protected as the drive, because DPAPI would bind it to one machine or one profile.
        var stored = TokenProtector.Protect("rmm_" + new string('a', 64), passphrase: null, expiresAt: null);

        Assert.Equal(TokenProtectionMode.None, stored.Mode);
        Assert.Null(stored.Salt);
        Assert.Equal("rmm_" + new string('a', 64), TokenProtector.Unprotect(stored, null));
    }

    [Fact]
    public void A_passphrase_protected_token_round_trips()
    {
        var stored = TokenProtector.Protect("rmm_secret", "correct horse battery staple", Now.AddDays(30));

        Assert.Equal(TokenProtectionMode.Passphrase, stored.Mode);
        Assert.NotNull(stored.Salt);
        Assert.NotNull(stored.Nonce);
        Assert.NotNull(stored.Tag);
        Assert.Equal(TokenProtector.DefaultIterations, stored.Iterations);
        Assert.NotEqual("rmm_secret"u8.ToArray(), stored.Cipher);

        Assert.Equal("rmm_secret", TokenProtector.Unprotect(stored, "correct horse battery staple"));
    }

    [Fact]
    public void A_wrong_passphrase_fails_loudly_rather_than_returning_rubbish()
    {
        var stored = TokenProtector.Protect("rmm_secret", "right", null);

        Assert.Throws<TokenUnlockException>(() => TokenProtector.Unprotect(stored, "wrong"));
        Assert.Throws<TokenUnlockException>(() => TokenProtector.Unprotect(stored, null));
    }

    [Fact]
    public void Tampering_with_the_ciphertext_is_detected()
    {
        var stored = TokenProtector.Protect("rmm_secret", "right", null);
        stored.Cipher[0] ^= 0xFF;

        Assert.Throws<TokenUnlockException>(() => TokenProtector.Unprotect(stored, "right"));
    }

    [Fact]
    public void Two_protections_of_the_same_token_differ()
    {
        var first = TokenProtector.Protect("rmm_secret", "same", null);
        var second = TokenProtector.Protect("rmm_secret", "same", null);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Cipher, second.Cipher);
    }

    [Fact]
    public void A_stored_pairing_reads_back_with_its_scopes_token_and_expiry()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(install));
        store.Device.SavePairing(
            new PairingResult(
                new Uri("https://romm.invalid"),
                "device-9",
                "Handheld",
                new GrantedScopes(RomMScopes.Requested),
                TokenProtector.Protect("rmm_token", "phrase", Now.AddDays(90))),
            Now);

        var device = store.Device.Read();

        Assert.NotNull(device);
        Assert.True(device.IsPaired);
        Assert.Equal(new Uri("https://romm.invalid"), device.ServerOrigin);
        Assert.Equal("device-9", device.RomMDeviceId);
        Assert.Equal(RomMScopes.Requested.Order(StringComparer.Ordinal), device.Scopes.All);
        Assert.Equal(TokenProtectionMode.Passphrase, device.Token!.Mode);
        Assert.False(device.IsTokenExpired(Now));
        Assert.True(device.IsTokenExpired(Now.AddDays(91)));
        Assert.Equal("rmm_token", TokenProtector.Unprotect(device.Token, "phrase"));
    }

    [Fact]
    public void An_unlock_without_a_pairing_says_so_rather_than_throwing_something_opaque()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        var pairing = new PairingService(install, store);

        Assert.Throws<TokenUnlockException>(() => pairing.UnlockToken(null));
    }
}
