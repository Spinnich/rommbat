using RomMBat.Core.Identity;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The QR the user scans, which has to encode the joined URL rather than the relative path.
/// </summary>
public class PairingQrTests
{
    [Fact]
    public void A_matrix_is_square_and_big_enough_to_hold_a_URL()
    {
        var matrix = PairingQrCode.Build(new Uri("https://romm.example.lan/pair/device?user_code=K7M2PQRS"));

        Assert.True(matrix.Size >= 21 + (PairingQrCode.QuietZone * 2));
        Assert.False(matrix.IsDark(0, 0));
    }

    [Fact]
    public void The_quiet_zone_is_light_all_the_way_round()
    {
        // A code printed flush against a terminal edge does not scan.
        var matrix = PairingQrCode.Build(new Uri("https://romm.example.lan/pair/device?user_code=K7M2PQRS"));
        var last = matrix.Size - 1;

        for (var i = 0; i < matrix.Size; i++)
        {
            for (var band = 0; band < PairingQrCode.QuietZone; band++)
            {
                Assert.False(matrix.IsDark(band, i));
                Assert.False(matrix.IsDark(last - band, i));
                Assert.False(matrix.IsDark(i, band));
                Assert.False(matrix.IsDark(i, last - band));
            }
        }
    }

    [Fact]
    public void The_finder_pattern_is_where_a_scanner_expects_it()
    {
        var matrix = PairingQrCode.Build("https://romm.example.lan/pair/device?user_code=K7M2PQRS");

        // Top-left finder: a 7x7 dark border with a light ring and a 3x3 dark centre.
        const int offset = PairingQrCode.QuietZone;

        Assert.True(matrix.IsDark(offset, offset));
        Assert.True(matrix.IsDark(offset + 6, offset));
        Assert.False(matrix.IsDark(offset + 1, offset + 1));
        Assert.True(matrix.IsDark(offset + 3, offset + 3));
    }

    [Fact]
    public void A_longer_URL_needs_a_bigger_code()
    {
        var shortUrl = PairingQrCode.Build("https://a.lan/pair/device?user_code=K7M2PQRS");
        var longUrl = PairingQrCode.Build(
            "https://romm.a-rather-long-hostname.example.internal:8443/behind/a/reverse/proxy/subpath"
                + "/pair/device?user_code=K7M2PQRS");

        Assert.True(longUrl.Size > shortUrl.Size);
    }

    [Fact]
    public void Nothing_is_encoded_without_something_to_encode()
    {
        Assert.Throws<ArgumentException>(() => PairingQrCode.Build(string.Empty));
        Assert.Throws<ArgumentNullException>(() => PairingQrCode.Build((Uri)null!));
    }
}
