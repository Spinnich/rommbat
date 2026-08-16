using System.Globalization;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>One launch, as <c>emulatorLauncher.log</c> recorded it.</summary>
/// <param name="RomPath">
/// Null when the path could not be tied to anything inside this tree. Never absolute: the log
/// is full of absolute paths and rule 1 forbids persisting one.
/// </param>
/// <param name="Core">
/// Frequently absent. Measured on a real log: empty on 200 of 424 launches, which is why the
/// plan's <c>{emulator}:{core}:{slot}</c> state slot is not usable as written.
/// </param>
/// <param name="IsMenuLaunch">
/// True for an <c>es_menu</c> entry, which is how RomMBat's own launch is recognised. Those
/// fire a <c>game-end</c> with no <c>game-start</c>, and attributing one to whatever ran last
/// is the naive failure this flag exists to prevent.
/// </param>
/// <param name="Signature">
/// Distinguishes two launches sharing a timestamp, so the cursor can resume exactly.
/// </param>
public sealed record LaunchRecord(
    DateTimeOffset At,
    RelativePath? RomPath,
    string? System,
    string? Emulator,
    string? Core,
    bool IsMenuLaunch,
    string Signature);

/// <summary>Where a read stopped, so the next one resumes without replaying or skipping.</summary>
/// <param name="LiveSizeBytes">
/// The live file's length at the time. The only in-tree signal that a rotation happened: the
/// file getting <i>smaller</i> is what says so.
/// </param>
public sealed record LaunchLogPosition(DateTimeOffset? At, string? Signature, long LiveSizeBytes);

/// <summary>
/// Reads launch facts out of <c>emulationstation/emulatorLauncher.log</c>.
/// </summary>
/// <remarks>
/// <b>The hook cannot supply any of this, which is why the file is the data source.</b>
/// EmulationStation passes a hook three arguments, the rom path, its basename and the gamelist
/// display name, and withholds the system, the emulator and the core, all three of which the
/// launcher is told. <c>game-end</c> receives nothing at all. So <c>game-end</c> is the
/// trigger and this file is the data.
/// <para>
/// <b>Six things about the real file decide the parser, and five of them are traps.</b> All
/// measured on a live install with five months of history (<c>tools/m6-probes/</c>):
/// </para>
/// <list type="number">
/// <item>The file rotates at roughly <b>1 MiB</b> into <c>.log.old</c>, and the two halves do
/// not overlap, so reading <c>.old</c> first yields launches in time order across the
/// boundary. A byte offset would point into the wrong file afterwards; the cursor is a
/// timestamp.</item>
/// <item><b>730 <c>[Startup]</c> lines, of which 424 are a launch.</b> The launcher is also
/// invoked for <c>-updatestores</c> and similar, so the discriminator is <c>-rom</c>.</item>
/// <item><b>The rom path carries whatever drive letter the install had at the time.</b> 295 of
/// 424 read <c>D:\RetroBat</c> and 129 <c>E:\RetroBat</c>, in one log for one install that
/// moved. Relativising by stripping the current root would discard 70% of the history, so the
/// path is cut at its <c>roms\</c> or <c>system\</c> segment instead.</item>
/// <item><b><c>-rom</c> is not a fixed shape.</b> Unquoted once in 424, and not the final flag
/// 19 times with <c>-core</c> written after it 5 times, so neither a quoted-only regex nor a
/// positional read is enough.</item>
/// <item><b>The file opens with a UTF-8 BOM</b> and carries unstamped continuation lines, .NET
/// stack traces among them.</item>
/// <item>An <b>ES-menu launch is identifiable</b>: <c>-system retrobat</c> with a rom under
/// <c>system\es_menu\</c>, 27 of them.</item>
/// </list>
/// <para>
/// Nothing here supplies an end time. <b>187 of the 424 launches never record an exit</b>, so
/// the end of a session is the <c>game-end</c> hook's own timestamp and never this file's.
/// </para>
/// </remarks>
public sealed class LaunchLog
{
    /// <summary>The live file.</summary>
    public static RelativePath LivePath { get; } = RelativePath.Create("emulationstation/emulatorLauncher.log");

    /// <summary>The rotated half, which holds the older launches.</summary>
    public static RelativePath RotatedPath { get; } =
        RelativePath.Create("emulationstation/emulatorLauncher.log.old");

    /// <summary>The system name an ES-menu entry launches under.</summary>
    public const string MenuSystem = "retrobat";

    /// <summary>Top-level directories a rom path may be cut at when the drive letter has moved.</summary>
    private static readonly string[] AnchorSegments = ["roms", "system"];

    private readonly RetroBatInstall _install;

    public LaunchLog(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        _install = install;
    }

    /// <summary>True when the log has been written at all.</summary>
    public bool Exists() => File.Exists(_install.Resolve(LivePath));

    /// <summary>
    /// Every launch newer than a position, oldest first.
    /// </summary>
    /// <param name="from">
    /// Where the last read stopped. Null reads everything both files hold, which is what a
    /// first run wants: the history is what attributes a save produced before RomMBat existed.
    /// </param>
    public IReadOnlyList<LaunchRecord> Read(LaunchLogPosition? from = null)
    {
        var records = new List<LaunchRecord>();

        // Rotated first, because the halves do not overlap and the older launches are there.
        foreach (var path in new[] { RotatedPath, LivePath })
        {
            var file = _install.Resolve(path);
            if (!File.Exists(file))
            {
                continue;
            }

            foreach (var line in ReadLines(file))
            {
                if (Parse(line) is { } record && IsAfter(record, from))
                {
                    records.Add(record);
                }
            }
        }

        // Sorted rather than assumed sorted. The measured file is already chronological, all
        // 424 launches with no line out of order, but the cursor's correctness rests on the
        // last record being the newest and that is cheap to guarantee instead of inherit.
        // OrderBy is stable, so launches sharing a timestamp keep their file order.
        return [.. records.OrderBy(record => record.At)];
    }

    /// <summary>
    /// Where a read that consumed <paramref name="records"/> should resume.
    /// </summary>
    /// <remarks>
    /// The newest record, which <see cref="Read"/> guarantees is the last one. Measured on the
    /// real file, every one of 424 launches carries a distinct millisecond, so the signature
    /// tiebreak below has never had to fire; it is kept because a collision would otherwise
    /// silently drop a launch rather than fail.
    /// </remarks>
    public LaunchLogPosition PositionAfter(IReadOnlyList<LaunchRecord> records, LaunchLogPosition? previous = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        var last = records.Count > 0 ? records[^1] : null;
        var live = new FileInfo(_install.Resolve(LivePath));

        return new LaunchLogPosition(
            last?.At ?? previous?.At,
            last?.Signature ?? previous?.Signature,
            live.Exists ? live.Length : 0);
    }

    /// <summary>
    /// True when the live file has rotated since a position was taken.
    /// </summary>
    /// <remarks>
    /// Informational rather than load-bearing: the read is driven by timestamps, so a rotation
    /// between two reads is already handled. This is what lets <c>status</c> say a rotation
    /// happened rather than leaving a gap unexplained.
    /// </remarks>
    public bool HasRotatedSince(LaunchLogPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        var live = new FileInfo(_install.Resolve(LivePath));
        return live.Exists && live.Length < position.LiveSizeBytes;
    }

    /// <summary>
    /// Parses one line, or returns null when it is not a game launch.
    /// </summary>
    /// <remarks>Internal so a test can drive the traps directly against a fixture line.</remarks>
    internal LaunchRecord? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // The first line of each file carries a BOM, and a stack-trace continuation carries no
        // timestamp at all. Both are ordinary rather than exceptional.
        var body = line.TrimStart('\uFEFF');

        if (ReadTimestamp(body) is not { } at)
        {
            return null;
        }

        if (!body.Contains("[Startup]", StringComparison.Ordinal))
        {
            return null;
        }

        var romArgument = ReadRomArgument(body);
        if (romArgument is null)
        {
            // A launcher invocation that is not a game: -updatestores and friends, 306 of the
            // 730 [Startup] lines on the measured install.
            return null;
        }

        var system = ReadFlag(body, "-system");
        var romPath = Relativize(romArgument);
        var isMenu = string.Equals(system, MenuSystem, StringComparison.OrdinalIgnoreCase)
            || (romPath?.Value.Contains("system/es_menu/", StringComparison.OrdinalIgnoreCase) ?? false);

        return new LaunchRecord(
            at,
            romPath,
            system,
            ReadFlag(body, "-emulator"),
            ReadFlag(body, "-core"),
            isMenu,
            Signature(body));
    }

    /// <summary>
    /// Turns a logged absolute path into a storable one.
    /// </summary>
    /// <remarks>
    /// <see cref="RetroBatInstall.Relativize"/> first, which is right whenever the letter has
    /// not moved. When it has, the path is cut at its <c>roms\</c> or <c>system\</c> segment,
    /// which is the only part of it that is a fact about the tree rather than about whichever
    /// machine was running at the time.
    /// </remarks>
    internal RelativePath? Relativize(string absolute)
    {
        if (_install.Contains(absolute))
        {
            return _install.Relativize(absolute);
        }

        var segments = absolute.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length; i++)
        {
            if (!AnchorSegments.Contains(segments[i], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = string.Join('/', segments[i..]);
            if (RelativePath.TryCreate(candidate, out var path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the <c>-rom</c> value, whichever of its shapes the line uses.
    /// </summary>
    /// <remarks>
    /// Quoted runs to the closing quote; unquoted runs to end of line. Both are observed, and
    /// the unquoted one carries spaces, commas and parentheses, so splitting on whitespace
    /// would lose it.
    /// </remarks>
    internal static string? ReadRomArgument(string line)
    {
        const string Flag = "-rom ";

        var index = line.IndexOf(Flag, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var tail = line[(index + Flag.Length)..].TrimEnd();

        if (!tail.StartsWith('"'))
        {
            return tail.Length == 0 ? null : tail;
        }

        var end = tail.IndexOf('"', 1);
        return end < 0 ? null : tail[1..end];
    }

    /// <summary>
    /// Reads a flag's value wherever it sits on the line.
    /// </summary>
    /// <remarks>
    /// Scanned rather than read positionally, because <c>-core</c> appears both before and
    /// after <c>-rom</c> on the same install. A value that starts with <c>-</c> is the next
    /// flag, which is how a present-but-empty <c>-core</c> reads.
    /// </remarks>
    internal static string? ReadFlag(string line, string flag)
    {
        var index = line.IndexOf(flag + " ", StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var tail = line[(index + flag.Length + 1)..].TrimStart();
        if (tail.Length == 0)
        {
            return null;
        }

        var end = tail.IndexOf(' ');
        var value = end < 0 ? tail : tail[..end];

        return value.StartsWith('-') || value.Length == 0 ? null : value;
    }

    private static bool IsAfter(LaunchRecord record, LaunchLogPosition? from)
    {
        if (from?.At is not { } cutoff)
        {
            return true;
        }

        if (record.At > cutoff)
        {
            return true;
        }

        // Two launches can share a millisecond. Same timestamp and same line is the one we
        // already consumed; same timestamp and a different line is a launch we have not.
        return record.At == cutoff
            && from.Signature is not null
            && !string.Equals(record.Signature, from.Signature, StringComparison.Ordinal);
    }

    private static DateTimeOffset? ReadTimestamp(string line)
    {
        // "2026-08-16 11:22:18.072 [INFO] ..."
        const int Length = 23;

        if (line.Length < Length)
        {
            return null;
        }

        return DateTime.TryParseExact(
            line[..Length],
            "yyyy-MM-dd HH:mm:ss.fff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : null;
    }

    /// <summary>
    /// A short stable fingerprint of the line, used only to tell two launches apart.
    /// </summary>
    /// <remarks>
    /// Not a security boundary and not compared against anything a server sends: it exists so
    /// a cursor sitting on a millisecond that two launches share can resume on the right one.
    /// SHA-256 rather than the MD5 used for content, because nothing outside RomMBat has to
    /// reproduce this value and there is no reason to reach for a weak algorithm.
    /// </remarks>
    private static string Signature(string line)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(line));
        return Convert.ToHexStringLower(bytes)[..16];
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        // Shared read: EmulationStation holds this file open and appends to it while a game is
        // running, which is exactly when a game-end hook wakes an agent to read it.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
