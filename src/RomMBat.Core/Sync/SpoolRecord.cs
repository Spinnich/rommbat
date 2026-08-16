namespace RomMBat.Core.Sync;

/// <summary>
/// One thing a hook saw, in the form it is written to the spool.
/// </summary>
/// <param name="Arguments">
/// The hook's arguments exactly as EmulationStation passed them, uninterpreted. For
/// <c>game-start</c> that is an <b>absolute</b> rom path, its basename and the gamelist
/// display name; for the other three events it is empty. The hook does not relativise the
/// path, because the ingest side is where the root, the ROM index and the launch log all
/// already are, and a value the hook could not interpret is better carried verbatim than
/// guessed at.
/// </param>
public sealed record SpoolRecord(string Event, DateTimeOffset At, int ProcessId, IReadOnlyList<string> Arguments)
{
    /// <summary>The format marker, so a later change is detectable rather than silent.</summary>
    public const string Version = "rommbat-hook-1";

    /// <summary>The four events RomMBat installs a hook for.</summary>
    public static IReadOnlyList<string> Events { get; } = ["start", "game-start", "game-end", "quit"];

    /// <summary>
    /// Reads the event out of the folder the hook sits in.
    /// </summary>
    /// <remarks>
    /// A hook lives at <c>.emulationstation/scripts/&lt;event&gt;/</c>, so the folder is the
    /// event. This is why one built file serves all four: the installer copies it, and each
    /// copy learns what it is from where it landed.
    /// </remarks>
    /// <returns>Null when the directory names no event RomMBat handles.</returns>
    public static string? EventFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));

        return Events.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Renders the record as the spool file's contents.
    /// </summary>
    /// <remarks>
    /// A line-based format rather than JSON, because the hook is trimmed and every reflective
    /// serializer is either trim-unsafe or costs a source-generated context for four fields.
    /// Backslash, carriage return and newline are escaped, so a rom name carrying any of them
    /// cannot forge a second line. Everything else is passed through, which matters: real rom
    /// names carry parentheses, commas and non-ASCII, and those are exactly the characters
    /// that broke the scripted hook forms.
    /// </remarks>
    public string Render()
    {
        var lines = new List<string>
        {
            Version,
            $"event={Event}",
            $"at={At.ToUniversalTime():O}",
            $"pid={ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        };

        lines.AddRange(Arguments.Select(argument => $"arg={Escape(argument)}"));

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Parses what <see cref="Render"/> wrote.</summary>
    /// <returns>Null when the text is not a record this build understands.</returns>
    public static SpoolRecord? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0] != Version)
        {
            return null;
        }

        string? hookEvent = null;
        DateTimeOffset? at = null;
        var processId = 0;
        var arguments = new List<string>();

        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator];
            var value = line[(separator + 1)..];

            switch (key)
            {
                case "event":
                    hookEvent = value;
                    break;
                case "at":
                    at = DateTimeOffset.TryParse(
                        value,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var parsed)
                        ? parsed
                        : null;
                    break;
                case "pid":
                    processId = int.TryParse(
                        value,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var pid)
                        ? pid
                        : 0;
                    break;
                case "arg":
                    arguments.Add(Unescape(value));
                    break;
                default:
                    break;
            }
        }

        if (hookEvent is null || at is null || !Events.Contains(hookEvent, StringComparer.Ordinal))
        {
            return null;
        }

        return new SpoolRecord(hookEvent, at.Value, processId, arguments);
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Unescape(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            i++;
            builder.Append(value[i] switch
            {
                'r' => '\r',
                'n' => '\n',
                '\\' => '\\',

                // An escape this build does not know. Kept verbatim rather than dropped, so a
                // path written by a newer hook degrades to a wrong name rather than a missing one.
                _ => value[i],
            });
        }

        return builder.ToString();
    }
}
