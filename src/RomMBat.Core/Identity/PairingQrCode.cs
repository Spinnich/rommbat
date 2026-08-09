using QRCoder;

namespace RomMBat.Core.Identity;

/// <summary>A QR code as a grid of dark and light modules.</summary>
/// <param name="Size">Width and height in modules, quiet zone included.</param>
public sealed class QrMatrix
{
    private readonly bool[,] _modules;

    internal QrMatrix(bool[,] modules, int size)
    {
        _modules = modules;
        Size = size;
    }

    public int Size { get; }

    /// <summary>True when the module at this position is dark.</summary>
    public bool IsDark(int row, int column) => _modules[row, column];
}

/// <summary>
/// The QR the user scans to reach the approval screen.
/// </summary>
/// <remarks>
/// It encodes the <b>configured origin joined with <c>verification_path_complete</c></b>.
/// The server returns a relative path on purpose and stays origin-agnostic, because in
/// development its web UI and API run on different ports, so joining is the client's job.
/// <para>
/// QRCoder is used because it is MIT and pure managed, so it survives a self-contained
/// single-file publish with no native dependency.
/// </para>
/// </remarks>
public static class PairingQrCode
{
    /// <summary>
    /// How many light modules surround the code. The QR specification calls for 4, and a
    /// code printed flush against a terminal edge does not scan.
    /// </summary>
    public const int QuietZone = 4;

    /// <summary>Builds the module grid for a URI, quiet zone included.</summary>
    public static QrMatrix Build(Uri verificationUri)
    {
        ArgumentNullException.ThrowIfNull(verificationUri);
        return Build(verificationUri.ToString());
    }

    /// <summary>Builds the module grid for arbitrary text, quiet zone included.</summary>
    public static QrMatrix Build(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        using var generator = new QRCodeGenerator();

        // Level M: 15% recovery, which is the usual choice for a screen where nothing is
        // going to smudge it, and keeps the code small enough to fit a default console.
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

        var raw = data.ModuleMatrix;

        // QRCoder's matrix already carries a 4-module quiet zone on every side.
        var size = raw.Count;
        var modules = new bool[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                modules[row, column] = raw[row][column];
            }
        }

        return new QrMatrix(modules, size);
    }
}
