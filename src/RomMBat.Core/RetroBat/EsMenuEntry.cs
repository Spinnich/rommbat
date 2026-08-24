using System.Reflection;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>What an install or uninstall pass did to one file.</summary>
/// <param name="What">The part of the registration, for a user reading the report.</param>
/// <param name="Path">Where it is, or would be.</param>
/// <param name="Problem">Null when the step succeeded.</param>
public sealed record EsMenuStep(string What, RelativePath Path, EsMenuAction Action, string? Problem = null);

/// <summary>What happened to one part of the registration.</summary>
public enum EsMenuAction
{
    /// <summary>Written where nothing was.</summary>
    Installed,

    /// <summary>Rewritten, because what was there was not what this build ships.</summary>
    Updated,

    /// <summary>Already right.</summary>
    AlreadyCurrent,

    /// <summary>Removed.</summary>
    Uninstalled,

    /// <summary>Nothing to remove.</summary>
    NotPresent,

    /// <summary>There and not RomMBat's to change, so it was left exactly as it was.</summary>
    LeftAlone,

    /// <summary>Could not be written or removed. <see cref="EsMenuStep.Problem"/> says why.</summary>
    Failed,
}

/// <summary>What a whole pass did.</summary>
public sealed record EsMenuOutcome(IReadOnlyList<EsMenuStep> Steps)
{
    public int Installed => Steps.Count(step => step.Action is EsMenuAction.Installed);

    public int Updated => Steps.Count(step => step.Action is EsMenuAction.Updated);

    public int Removed => Steps.Count(step => step.Action is EsMenuAction.Uninstalled);

    public int Failed => Steps.Count(step => step.Action is EsMenuAction.Failed);

    /// <summary>True when nothing was written, which is every pass after the first.</summary>
    public bool IsNoOp => Installed + Updated + Removed + Failed == 0;

    public IReadOnlyList<string> Problems =>
        [.. Steps.Where(step => step.Problem is not null).Select(step => $"{step.What}: {step.Problem}")];
}

/// <summary>
/// Puts RomMBat in the EmulationStation menu, and takes it back out.
/// </summary>
/// <remarks>
/// <b>Registration is two files, not one, and that is measured.</b> M0 probe 4 found that
/// <c>es_menu</c> is an ordinary ES system declared in <c>es_systems.cfg</c> with
/// <c>&lt;extension&gt;.menu&lt;/extension&gt;</c>, so a <c>.menu</c> is a ROM of the
/// <c>retrobat</c> system and the thing that parses it is <b>emulatorLauncher, not ES</b>. The
/// <c>.menu</c> supplies the command; the display name and artwork come from a
/// <c>&lt;game&gt;</c> element in <c>system/es_menu/gamelist.xml</c>. A <c>.menu</c> with no
/// gamelist entry appears under its bare filename, which was driven rather than assumed:
/// writing the file alone took ES from 92 games to 93 in 209 ms, listed as <c>zzprobe7a</c>
/// with no image. See <c>docs/retrobat-findings.md</c>, 203.
/// <para>
/// <b>The executable line cannot escape <c>emulators\</c>.</b> Three variants were installed
/// side by side and launched: <c>..\..\plugins\rommbat\…</c> and <c>\plugins\rommbat\…</c> were
/// both refused by emulatorLauncher with <c>[Generator] Failed. path is null</c> and exit 204,
/// and only <c>\rommbat\…</c>, resolved under <c>emulators\</c>, launched. That is the
/// measurement that forced RomMBat's install location, so the line here and
/// <see cref="RetroBatInstall.AppDirectory"/> are two halves of one fact.
/// </para>
/// <para>
/// <b>Merge, never clobber, and the stakes are higher than under <c>roms/</c>.</b> Someone
/// else's 93 entries live in that gamelist and three more are commented out, which is how
/// RetroBat withdraws one whose markup it still ships. ES rewrites a rom gamelist whenever it
/// has a reason to and it leaves this one alone, measured across three sessions including one
/// where it had the change in its model, so RomMBat is the only writer that could damage it.
/// </para>
/// <para>
/// <b>A field a user changed is never taken back.</b> On an entry that already exists, only
/// elements that are absent are filled in. The name and the artwork are what the user sees on
/// their own front end, and re-asserting them on every sync is the same overreach the class D
/// rule forbids for a setting somebody else wrote.
/// </para>
/// </remarks>
public sealed class EsMenuEntry
{
    /// <summary>The ES menu directory, which is a sibling of <c>roms/</c> rather than inside it.</summary>
    public static RelativePath Directory { get; } = RelativePath.Create("system/es_menu");

    /// <summary>The <c>.menu</c> file, which is a ROM of the <c>retrobat</c> system.</summary>
    public static RelativePath MenuPath { get; } = Directory.Combine("rommbat.menu");

    /// <summary>The gamelist RomMBat merges one entry into and owns nothing else in.</summary>
    public static RelativePath GamelistPath { get; } = Directory.Combine("gamelist.xml");

    /// <summary>The artwork, under the <c>media/</c> folder RetroBat's own entries use.</summary>
    public static RelativePath LogoPath { get; } = Directory.Combine("media/rommbat-logo.png");

    /// <summary>The gamelist <c>&lt;path&gt;</c>, which is how the entry is identified.</summary>
    public const string EntryPath = "./rommbat.menu";

    /// <summary>
    /// Line 1 of the <c>.menu</c>: the executable, resolved under <c>emulators\</c>.
    /// </summary>
    /// <remarks>
    /// No trailing newline, matching the 92 shipped entries. Backslashes because
    /// emulatorLauncher reads a Windows path here, which is the one place in this codebase
    /// where a forward slash would be wrong.
    /// </remarks>
    public const string ExecutableLine = @"\rommbat\RomMBat.exe";

    /// <summary>Where the artwork is read from, which is inside this assembly.</summary>
    private const string LogoResource = "RomMBat.Core.data.media.rommbat-logo.png";

    private readonly RetroBatInstall _install;

    public EsMenuEntry(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        _install = install;
    }

    /// <summary>
    /// True when both halves of the registration are in place.
    /// </summary>
    /// <remarks>
    /// Both, because either alone is a broken entry: a <c>.menu</c> with no gamelist element
    /// shows as a bare filename, and a gamelist element whose <c>.menu</c> is missing is not
    /// listed by ES at all.
    /// </remarks>
    public bool IsInstalled()
    {
        try
        {
            if (!File.Exists(_install.Resolve(MenuPath)))
            {
                return false;
            }

            var gamelist = _install.Resolve(GamelistPath);
            return File.Exists(gamelist) && GamelistDocument.Load(gamelist).Contains(EntryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or GamelistParseException)
        {
            // Unreadable means "not known to be installed", so the caller reinstalls and finds
            // out properly.
            return false;
        }
    }

    /// <summary>Adds the entry, and says which file each step touched.</summary>
    public EsMenuOutcome Install()
    {
        var steps = new List<EsMenuStep>
        {
            WriteMenuFile(),
            WriteLogo(),
        };

        steps.Add(MergeGamelist());
        return new EsMenuOutcome(steps);
    }

    /// <summary>
    /// Takes the entry back out.
    /// </summary>
    /// <remarks>
    /// Only RomMBat's own two files and its own one element, never the gamelist itself, never
    /// the <c>media/</c> folder and never the <c>es_menu</c> folder. All three hold RetroBat's
    /// own content.
    /// </remarks>
    public EsMenuOutcome Uninstall()
    {
        var steps = new List<EsMenuStep>
        {
            Remove("the menu entry", MenuPath),
            Remove("the artwork", LogoPath),
        };

        steps.Add(RemoveFromGamelist());
        return new EsMenuOutcome(steps);
    }

    private EsMenuStep WriteMenuFile()
    {
        var target = _install.Resolve(MenuPath);

        try
        {
            var wanted = System.Text.Encoding.ASCII.GetBytes(ExecutableLine);

            if (File.Exists(target))
            {
                if (File.ReadAllBytes(target).AsSpan().SequenceEqual(wanted))
                {
                    return new EsMenuStep("the menu entry", MenuPath, EsMenuAction.AlreadyCurrent);
                }

                // This file is RomMBat's own and a wrong line is a broken entry rather than a
                // preference, so it is repaired. The gamelist fields are the opposite case and
                // are left alone below.
                WriteAtomically(target, wanted);
                return new EsMenuStep("the menu entry", MenuPath, EsMenuAction.Updated);
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            WriteAtomically(target, wanted);
            return new EsMenuStep("the menu entry", MenuPath, EsMenuAction.Installed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsMenuStep("the menu entry", MenuPath, EsMenuAction.Failed, ex.Message);
        }
    }

    private EsMenuStep WriteLogo()
    {
        var target = _install.Resolve(LogoPath);

        try
        {
            var wanted = ReadLogo();

            if (File.Exists(target))
            {
                return File.ReadAllBytes(target).AsSpan().SequenceEqual(wanted)
                    ? new EsMenuStep("the artwork", LogoPath, EsMenuAction.AlreadyCurrent)
                    : ReplaceLogo(target, wanted);
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            WriteAtomically(target, wanted);
            return new EsMenuStep("the artwork", LogoPath, EsMenuAction.Installed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsMenuStep("the artwork", LogoPath, EsMenuAction.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Replaces artwork that is not this build's.
    /// </summary>
    /// <remarks>
    /// Unlike the gamelist's <c>&lt;image&gt;</c> element, this file is at a path only RomMBat
    /// writes, so different bytes mean a different build rather than a user's choice. A user
    /// who wants their own artwork points <c>&lt;image&gt;</c> somewhere else, and that
    /// element is then left alone.
    /// </remarks>
    private static EsMenuStep ReplaceLogo(string target, byte[] wanted)
    {
        WriteAtomically(target, wanted);
        return new EsMenuStep("the artwork", LogoPath, EsMenuAction.Updated);
    }

    private EsMenuStep MergeGamelist()
    {
        var path = _install.Resolve(GamelistPath);

        try
        {
            var document = GamelistDocument.Load(path);
            var existing = document.ElementNamesOf(EntryPath);
            var isNew = !document.Contains(EntryPath);

            // On an entry that is already there, only what is missing. A user who renamed it,
            // or pointed the artwork at their own file, keeps that across every later sync.
            var fields = Fields
                .Where(field => isNew || !existing.Contains(field.Key, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!isNew && fields.Count == 0)
            {
                return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.AlreadyCurrent);
            }

            var changed = document.Apply(new GamelistEntry(EntryPath, fields));

            if (!changed || !document.WriteIfChanged(path))
            {
                return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.AlreadyCurrent);
            }

            return new EsMenuStep(
                "the gamelist entry",
                GamelistPath,
                isNew ? EsMenuAction.Installed : EsMenuAction.Updated);
        }
        catch (GamelistParseException ex)
        {
            // The file exists and could not be read. Rewriting it would destroy 93 entries and
            // whatever else the user has in there, so it is left exactly as it is.
            return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.Failed, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.Failed, ex.Message);
        }
    }

    private EsMenuStep RemoveFromGamelist()
    {
        var path = _install.Resolve(GamelistPath);

        try
        {
            if (!File.Exists(path))
            {
                return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.NotPresent);
            }

            var document = GamelistDocument.Load(path);

            if (!document.Remove(EntryPath))
            {
                return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.NotPresent);
            }

            document.WriteIfChanged(path);
            return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.Uninstalled);
        }
        catch (GamelistParseException ex)
        {
            return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.Failed, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsMenuStep("the gamelist entry", GamelistPath, EsMenuAction.Failed, ex.Message);
        }
    }

    private EsMenuStep Remove(string what, RelativePath path)
    {
        var target = _install.Resolve(path);

        try
        {
            if (!File.Exists(target))
            {
                return new EsMenuStep(what, path, EsMenuAction.NotPresent);
            }

            File.Delete(target);
            return new EsMenuStep(what, path, EsMenuAction.Uninstalled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsMenuStep(what, path, EsMenuAction.Failed, ex.Message);
        }
    }

    /// <summary>
    /// The elements a new entry gets, in the order RetroBat's own entries write them.
    /// </summary>
    /// <remarks>
    /// <c>marquee</c> beside <c>image</c> because every shipped entry carries both pointing at
    /// the same file, and a theme that reads one and not the other then has something to show.
    /// <c>playcount</c>, <c>lastplayed</c> and <c>gametime</c> are deliberately absent: ES
    /// owns those, and launching RomMBat does record a play event it has to discard.
    /// </remarks>
    private static IReadOnlyList<KeyValuePair<string, string?>> Fields { get; } =
    [
        new("name", "RomMBat"),
        new("desc", "Sync this RetroBat install with a RomM library: pick what to pull down, "
            + "and send saves and play time back."),
        new("image", "./media/rommbat-logo.png"),
        new("marquee", "./media/rommbat-logo.png"),
        new("developer", "RomMBat"),
        new("publisher", "RomMBat"),
        new("genre", "Application"),
        new("lang", "en"),
        new("region", "wr"),
    ];

    private static byte[] ReadLogo()
    {
        using var stream = typeof(EsMenuEntry).GetTypeInfo().Assembly.GetManifestResourceStream(LogoResource)
            ?? throw new InvalidOperationException(
                $"'{LogoResource}' is not embedded in this build, so the ES menu entry has no artwork.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// Writes through a temp file in the same directory and renames.
    /// </summary>
    /// <remarks>
    /// The same discipline the gamelist writer uses. A power cut here would otherwise leave a
    /// truncated <c>.menu</c>, which emulatorLauncher reads as a path it cannot resolve.
    /// </remarks>
    private static void WriteAtomically(string target, byte[] bytes)
    {
        var temporary = target + ".rommbat-tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, target, overwrite: true);
    }
}
