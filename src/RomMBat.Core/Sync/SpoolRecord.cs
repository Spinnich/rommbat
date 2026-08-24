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

    /// <summary>
    /// The part of the marker that names the format, with the revision after it.
    /// </summary>
    /// <remarks>
    /// <b>The marker is a family plus a revision, and the two are treated differently.</b> A
    /// different family is a different grammar and is refused outright. Within the family, this
    /// build reads its own revision and any older one, because the format only ever gains keys
    /// and the parser already ignores keys it does not know.
    /// <para>
    /// <b>A newer revision is not read, and is not deleted either.</b> That is the half that
    /// matters: this build cannot know what a later revision changed, so guessing at one risks
    /// reading a record wrongly, while deleting it loses the play session it described.
    /// Requiring the whole marker to match exactly did the second of those, permanently, and
    /// reported only a count. Issue #31.
    /// </para>
    /// </remarks>
    public const string VersionFamily = "rommbat-hook";

    /// <summary>The revision this build writes and is the highest it can read.</summary>
    public const int VersionRevision = 1;

    /// <summary>The four events RomMBat installs a hook for.</summary>
    public static IReadOnlyList<string> Events { get; } = ["start", "game-start", "game-end", "quit"];

    /// <summary>
    /// The two events whose hook may start a background pass, and the boundary of CLAUDE.md
    /// rule 4.
    /// </summary>
    /// <remarks>
    /// <b>The rule is narrowed here, not bent.</b> It reads "The ES hooks never touch the
    /// network" and gives its reason in the next sentence: <i>they run inside the game-launch
    /// path</i>. <c>game-start</c> and <c>game-end</c> do, and they still spool and exit
    /// touching nothing. <c>start</c> fires when EmulationStation starts and <c>quit</c> when
    /// it exits, and neither is in that path.
    /// <para>
    /// <b>This lives on the record type on purpose.</b> The hook compiles this file rather than
    /// referencing Core, so the hook and the agent cannot disagree about which events spawn,
    /// and the boundary is a value a test can assert rather than a comment in one binary.
    /// </para>
    /// <para>
    /// The cost objection that kept the hooks inert for six milestones was measured and went
    /// the other way: ES spawns hooks fire-and-forget and starts emulatorlauncher without
    /// waiting, a median of 24 ms before the hook even reaches its own first line. What
    /// remains is rule 4, which is about the network. See <c>docs/retrobat-findings.md</c>,
    /// 195 and 197.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> BackgroundEvents { get; } = ["start", "quit"];

    /// <summary>
    /// Where the agent sits, relative to the RetroBat root.
    /// </summary>
    /// <remarks>
    /// Forward slashes like every other relative path in this codebase, combined against the
    /// discovered root at the point of use. Rule 1: the hook never persists an absolute path
    /// and never assumes a drive letter, because it is the one component that is handed one.
    /// </remarks>
    public const string AgentRelativePath = "emulators/rommbat/rommbat-agent.exe";

    /// <summary>Whether this event's hook may start a background pass.</summary>
    public static bool SpawnsBackgroundPass(string? hookEvent) =>
        hookEvent is not null && BackgroundEvents.Contains(hookEvent, StringComparer.Ordinal);

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

    /// <summary>
    /// The revision on the first line, or null when the line does not name this format at all.
    /// </summary>
    public static int? RevisionOf(string? text)
    {
        if (FirstLine(text) is not { } marker
            || !marker.StartsWith(VersionFamily + "-", StringComparison.Ordinal))
        {
            return null;
        }

        return int.TryParse(
            marker[(VersionFamily.Length + 1)..],
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var revision)
            ? revision
            : null;
    }

    /// <summary>
    /// True when this is one of ours, written by a build that knows something this one does not.
    /// </summary>
    /// <remarks>
    /// Used by the drain to tell "not one of ours, delete it" from "ours but ahead of us, leave
    /// it for a newer agent". Without the distinction the spool would either grow without bound
    /// on rubbish or lose events on an out-of-step update, and both are worse than one file
    /// waiting.
    /// </remarks>
    public static bool IsFromNewerBuild(string? text) => RevisionOf(text) > VersionRevision;

    /// <summary>Parses what <see cref="Render"/> wrote.</summary>
    /// <returns>Null when the text is not a record this build understands.</returns>
    public static SpoolRecord? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        // This build's own revision and any older one. Everything after line 1 is key/value and
        // unknown keys are already ignored, so an older record reads as the subset it carried.
        // A newer one is refused here and kept by the drain, because what a later revision
        // changed is exactly what this build cannot know.
        if (lines.Length == 0 || RevisionOf(text) is not { } revision || revision > VersionRevision)
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

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var end = text.IndexOfAny(['\r', '\n']);
        return end < 0 ? text : text[..end];
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
