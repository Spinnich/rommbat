using System.Text;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// Makes text from a scraper safe to put in a gamelist.
/// </summary>
/// <remarks>
/// <b>A gamelist EmulationStation cannot parse loses the whole system, not one entry.</b> The
/// text here comes from metadata providers by way of RomM, so it carries whatever a scraper
/// happened to store: a 5,000-ROM sample held ampersands in 169 descriptions, an angle
/// bracket, a Unicode format character, and non-ASCII in 298. Escaping is the XML writer's
/// job and it does it correctly; what the writer cannot do is emit a character XML 1.0 has no
/// representation for, and it throws instead. So those are removed here, before anything is
/// written.
/// </remarks>
internal static class XmlText
{
    /// <summary>Written as code points so this file stays ASCII, per the repo's English-only rule.</summary>
    private const char NonCharacterFffe = (char)0xFFFE;

    private const char NonCharacterFfff = (char)0xFFFF;

    /// <summary>
    /// Removes what XML 1.0 cannot carry, and normalises line endings.
    /// </summary>
    /// <remarks>
    /// Control characters below 0x20 other than tab, newline and carriage return have no
    /// representation in XML 1.0 at all, not even as a character reference. Unpaired
    /// surrogates are not characters and make the document invalid UTF-8. Both are dropped
    /// rather than replaced, because a replacement character in a description is noise a user
    /// would read as corruption.
    /// <para>
    /// CRLF becomes LF because an XML parser normalises it on the way back in anyway, so
    /// leaving it would make a round trip change the bytes and churn the file forever.
    /// </para>
    /// </remarks>
    public static string Sanitize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (character == '\r')
            {
                // A lone CR is a line ending too, and a CRLF pair collapses to one LF.
                if (index + 1 < value.Length && value[index + 1] == '\n')
                {
                    index++;
                }

                builder.Append('\n');
                continue;
            }

            if (character is '\t' or '\n')
            {
                builder.Append(character);
                continue;
            }

            if (character < 0x20 || character == 0x7F)
            {
                continue;
            }

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    builder.Append(character).Append(value[index + 1]);
                    index++;
                }

                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                continue;
            }

            // The two permanently unassigned noncharacters at the end of the BMP. XmlWriter
            // refuses them, which would take the whole system's gamelist with it.
            if (character is NonCharacterFffe or NonCharacterFfff)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
