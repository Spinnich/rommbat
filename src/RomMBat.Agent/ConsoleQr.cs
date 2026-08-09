using System.Text;
using RomMBat.Core.Identity;

namespace RomMBat.Agent;

/// <summary>
/// Draws a QR code in a console window.
/// </summary>
/// <remarks>
/// The UI framework is not chosen until M7, so the M1 pairing surface is this console and
/// the QR has to scan from it.
/// <para>
/// Two module rows go into one character cell using the upper-half block, with the
/// foreground painting the top module and the background the bottom one. A console cell is
/// roughly twice as tall as it is wide, so one cell per two rows and one cell per column
/// gives square modules; the obvious alternative, two spaces per module, is also square but
/// twice as tall and a 41-module code then does not fit an 80x30 window.
/// </para>
/// <para>
/// Colours are set explicitly rather than relying on the terminal's own, because a scanner
/// needs dark modules to actually be dark: rendering with block characters alone inverts
/// the code on a dark-background terminal.
/// </para>
/// </remarks>
internal static class ConsoleQr
{
    private const char UpperHalfBlock = '▀';
    private const char FullBlock = '█';

    /// <summary>Writes the code to the console, or a plain-text fallback when it cannot.</summary>
    public static void Write(QrMatrix matrix, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (Console.IsOutputRedirected)
        {
            WriteMonochrome(matrix, writer);
            return;
        }

        TryUseUtf8();

        var previousForeground = Console.ForegroundColor;
        var previousBackground = Console.BackgroundColor;

        try
        {
            for (var row = 0; row < matrix.Size; row += 2)
            {
                for (var column = 0; column < matrix.Size; column++)
                {
                    var top = matrix.IsDark(row, column);

                    // An odd-sized matrix leaves the last row without a partner; the missing
                    // half must read as quiet zone, not as a module.
                    var bottom = row + 1 < matrix.Size && matrix.IsDark(row + 1, column);

                    Console.ForegroundColor = top ? ConsoleColor.Black : ConsoleColor.White;
                    Console.BackgroundColor = bottom ? ConsoleColor.Black : ConsoleColor.White;
                    Console.Write(UpperHalfBlock);
                }

                Console.ForegroundColor = previousForeground;
                Console.BackgroundColor = previousBackground;
                Console.WriteLine();
            }
        }
        finally
        {
            Console.ForegroundColor = previousForeground;
            Console.BackgroundColor = previousBackground;
        }
    }

    /// <summary>
    /// The redirected-output form: block characters, no colour.
    /// </summary>
    /// <remarks>
    /// Two characters per module, so it stays square, and one row per module. Whether it
    /// scans depends on the background of whatever finally displays it, which is exactly why
    /// this is the fallback rather than the default.
    /// </remarks>
    public static void WriteMonochrome(QrMatrix matrix, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(writer);

        var line = new StringBuilder(matrix.Size * 2);
        for (var row = 0; row < matrix.Size; row++)
        {
            line.Clear();
            for (var column = 0; column < matrix.Size; column++)
            {
                line.Append(matrix.IsDark(row, column) ? $"{FullBlock}{FullBlock}" : "  ");
            }

            writer.WriteLine(line.ToString());
        }
    }

    private static void TryUseUtf8()
    {
        try
        {
            if (Console.OutputEncoding.CodePage != Encoding.UTF8.CodePage)
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
        }
        catch (IOException)
        {
            // No console attached. The block characters may render as '?', which costs the
            // QR but not the code beside it.
        }
    }
}
