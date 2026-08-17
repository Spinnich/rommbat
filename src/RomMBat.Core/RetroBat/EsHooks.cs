using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>What an install or uninstall pass did to one event folder.</summary>
/// <param name="Event">The event folder, which is also the event the hook serves.</param>
/// <param name="Path">Where the hook is, or would be.</param>
/// <param name="Problem">Null when the step succeeded.</param>
public sealed record EsHookStep(string Event, RelativePath Path, EsHookAction Action, string? Problem = null);

/// <summary>What happened to one hook.</summary>
public enum EsHookAction
{
    /// <summary>Written where nothing was.</summary>
    Installed,

    /// <summary>Replaced, because the shipped hook is a different build.</summary>
    Updated,

    /// <summary>Already the right bytes.</summary>
    AlreadyCurrent,

    /// <summary>Removed.</summary>
    Uninstalled,

    /// <summary>Nothing to remove.</summary>
    NotPresent,

    /// <summary>Could not be written or removed. <see cref="EsHookStep.Problem"/> says why.</summary>
    Failed,
}

/// <summary>What a whole pass did.</summary>
public sealed record EsHookOutcome(IReadOnlyList<EsHookStep> Steps)
{
    public int Installed => Steps.Count(step => step.Action is EsHookAction.Installed);

    public int Updated => Steps.Count(step => step.Action is EsHookAction.Updated);

    public int Removed => Steps.Count(step => step.Action is EsHookAction.Uninstalled);

    public int Failed => Steps.Count(step => step.Action is EsHookAction.Failed);

    public IReadOnlyList<string> Problems =>
        [.. Steps.Where(step => step.Problem is not null).Select(step => $"{step.Event}: {step.Problem}")];
}

/// <summary>
/// Installs and removes the EmulationStation event hooks.
/// </summary>
/// <remarks>
/// <b>Executables, not scripts, and that is measured rather than preferred.</b> M0 probe 7b
/// drove a <c>.bat</c>, a <c>.ps1</c> and an <c>.exe</c> side by side through the same three
/// launches. The <c>.bat</c> never started once an argument carried a space, because ES quotes
/// such arguments and cmd's quote-stripping mangles the resulting line; the <c>.ps1</c> lost
/// on a parenthesis, because ES builds <c>powershell &lt;script&gt; &lt;args&gt;</c> with no
/// <c>-File</c> and PowerShell reparses the tail as code. Both failures are completely silent.
/// On a second host neither form could start at all, one because an installer had taken the
/// <c>batfile</c> association and one because the execution policy was <c>Restricted</c>. The
/// <c>.exe</c> took a full No-Intro name as three intact arguments on every host.
/// <para>
/// <b>Added beside whatever is already there, never replacing it.</b> ES runs every file in an
/// event folder, in name order, and M0 watched RetroBat's own <c>updatestores.bat</c> and a
/// probe hook both run 63 ms apart. The <c>zz-</c> prefix keeps RomMBat last, so a shipped
/// script that a user depends on runs first.
/// </para>
/// <para>
/// <b>Four copies of one file.</b> The hook learns which event it serves from the name of the
/// folder it is in, so there is one build and four installs. It is 11.0 MB, which is why it
/// references nothing: the same job done through <c>Microsoft.Data.Sqlite</c> measured 15.1 MB
/// and needed a second file per folder, and installing the agent itself would have been
/// 75.5 MB four times over.
/// </para>
/// </remarks>
public sealed class EsHooks
{
    /// <summary>The file name RomMBat installs, prefixed so ES runs it after anything shipped.</summary>
    public const string FileName = "zz-rommbat-hook.exe";

    /// <summary>The events RomMBat hooks, which is also the set of folder names it writes to.</summary>
    /// <remarks>
    /// <c>start</c> is the heartbeat and the flush trigger, <c>quit</c> the other flush
    /// trigger, <c>game-start</c> opens a play session and <c>game-end</c> closes it.
    /// <c>game-selected</c> and <c>system-selected</c> are deliberately absent: ES fires them
    /// on every cursor move, so hooking them would write a spool file per navigation keypress.
    /// </remarks>
    public static IReadOnlyList<string> Events { get; } = ["start", "game-start", "game-end", "quit"];

    private readonly RetroBatInstall _install;

    public EsHooks(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        _install = install;
    }

    /// <summary>The scripts directory, which is under ES's home rather than the tree root.</summary>
    public static RelativePath ScriptsDirectory { get; } =
        RelativePath.Create("emulationstation/.emulationstation/scripts");

    /// <summary>Where the hook for one event goes.</summary>
    public static RelativePath PathFor(string hookEvent) =>
        ScriptsDirectory.Combine($"{hookEvent}/{FileName}");

    /// <summary>Where the shipped hook is, beside the agent.</summary>
    public static RelativePath SourcePath { get; } =
        RetroBatInstall.AppDirectory.Combine("rommbat-hook.exe");

    /// <summary>True when every event folder holds the current hook.</summary>
    public bool IsInstalled()
    {
        var source = _install.Resolve(SourcePath);

        return Events.All(hookEvent =>
        {
            var target = _install.Resolve(PathFor(hookEvent));
            return File.Exists(target) && (!File.Exists(source) || SameBytes(source, target));
        });
    }

    /// <summary>
    /// Puts the hook in every event folder.
    /// </summary>
    /// <param name="sourceExecutable">
    /// The hook to copy. Defaults to the shipped one beside the agent, and is overridable so a
    /// test can install a stand-in rather than an 11 MB publish artefact.
    /// </param>
    public EsHookOutcome Install(string? sourceExecutable = null)
    {
        var source = sourceExecutable ?? _install.Resolve(SourcePath);
        var steps = new List<EsHookStep>();

        foreach (var hookEvent in Events)
        {
            var path = PathFor(hookEvent);
            var target = _install.Resolve(path);

            try
            {
                if (!File.Exists(source))
                {
                    steps.Add(new EsHookStep(
                        hookEvent,
                        path,
                        EsHookAction.Failed,
                        $"the hook executable is not at {SourcePath}, so there is nothing to install."));
                    continue;
                }

                var existed = File.Exists(target);

                if (existed && SameBytes(source, target))
                {
                    steps.Add(new EsHookStep(hookEvent, path, EsHookAction.AlreadyCurrent));
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);

                steps.Add(new EsHookStep(
                    hookEvent,
                    path,
                    existed ? EsHookAction.Updated : EsHookAction.Installed));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The commonest cause is ES running and holding the file. Reported rather than
                // retried: installing on the next sync is fine, and a hook is not urgent.
                steps.Add(new EsHookStep(hookEvent, path, EsHookAction.Failed, ex.Message));
            }
        }

        return new EsHookOutcome(steps);
    }

    /// <summary>
    /// Takes the hook back out of every event folder.
    /// </summary>
    /// <remarks>
    /// Only RomMBat's own file is removed, never the folder and never anything else in it.
    /// <c>start/</c> holds RetroBat's shipped <c>updatestores.bat</c>, and a user may have
    /// added their own.
    /// </remarks>
    public EsHookOutcome Uninstall()
    {
        var steps = new List<EsHookStep>();

        foreach (var hookEvent in Events)
        {
            var path = PathFor(hookEvent);
            var target = _install.Resolve(path);

            try
            {
                if (!File.Exists(target))
                {
                    steps.Add(new EsHookStep(hookEvent, path, EsHookAction.NotPresent));
                    continue;
                }

                File.Delete(target);
                steps.Add(new EsHookStep(hookEvent, path, EsHookAction.Uninstalled));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                steps.Add(new EsHookStep(hookEvent, path, EsHookAction.Failed, ex.Message));
            }
        }

        return new EsHookOutcome(steps);
    }

    private static bool SameBytes(string left, string right)
    {
        try
        {
            var a = new FileInfo(left);
            var b = new FileInfo(right);

            if (a.Length != b.Length)
            {
                return false;
            }

            // Size then content. The files are 11 MB and this runs once per sync, so the read
            // is worth it: a hook left over from a previous build is exactly the case where
            // sizes match and behaviour does not.
            using var first = a.OpenRead();
            using var second = b.OpenRead();

            Span<byte> bufferA = stackalloc byte[8192];
            Span<byte> bufferB = stackalloc byte[8192];

            while (true)
            {
                var readA = first.ReadAtLeast(bufferA, bufferA.Length, throwOnEndOfStream: false);
                var readB = second.ReadAtLeast(bufferB, bufferB.Length, throwOnEndOfStream: false);

                if (readA != readB || !bufferA[..readA].SequenceEqual(bufferB[..readB]))
                {
                    return false;
                }

                if (readA == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable means "not known to be current", so the caller reinstalls.
            return false;
        }
    }
}
