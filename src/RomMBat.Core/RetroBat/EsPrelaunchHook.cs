using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Installs the synchronous callback only on measured EmulationStation builds.</summary>
public static class EsPrelaunchHook
{
    public static RelativePath Path { get; } =
        EsHooks.ScriptsDirectory.Combine("game-start/rommbat-prelaunch-wait.exe");

    private static readonly RelativePath EmulationStation = RelativePath.Create("emulationstation/emulationstation.exe");

    // GameOS A/B, RetroBat 8.3.1.1-stable-win64, ES 42.2026.1017.0811.
    private const string VerifiedSha256 = "0366AA3A20B15745F35109A6C68D5FF281E099DEA5CC304F28C3758B071E4F2A";
    private static readonly RelativePath Proof = RetroBatInstall.AppDirectory.Combine("prelaunch-proof.txt");

    public static bool IsVerified(RetroBatInstall install) => IsVerified(install, VerifiedSha256);

    internal static bool IsVerified(RetroBatInstall install, string expectedSha256)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var executable = File.OpenRead(install.Resolve(EmulationStation));
            return ReadStamp(executable, expectedSha256) is { } stamp
                && File.ReadAllText(install.Resolve(Proof)) == stamp;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static IDisposable WatchChanges(RetroBatInstall install, Action invalidate)
    {
        var hook = install.Resolve(Path);
        var watcher = new GateFiles(install.Resolve(EmulationStation), hook, invalidate);
        // Verify once after subscribing, so a replacement between installation and subscription
        // cannot silently leave the current session without a receiver.
        if (!IsVerified(install) || !EsHooks.SameBytes(install.Resolve(EsHooks.SourcePath), hook))
        {
            invalidate();
        }

        return watcher;
    }

    private sealed class GateFiles : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = [];

        public GateFiles(string executable, string hook, Action invalidate)
        {
            try
            {
                foreach (var path in new[] { executable, hook })
                {
                    var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path))
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    };
                    _watchers.Add(watcher);
                    watcher.Changed += (_, _) => invalidate();
                    watcher.Created += (_, _) => invalidate();
                    watcher.Deleted += (_, _) => invalidate();
                    watcher.Renamed += (_, _) => invalidate();
                    watcher.Error += (_, _) => invalidate();
                    watcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Dispose();
                invalidate();
            }
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }

    public static EsHookStep? Ensure(RetroBatInstall install) => Ensure(install, VerifyAndRemember(install, VerifiedSha256));

    internal static bool VerifyAndRemember(RetroBatInstall install, string expectedSha256)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var executable = File.OpenRead(install.Resolve(EmulationStation));
            if (ReadStamp(executable, expectedSha256) is not { } stamp
                || Convert.ToHexString(SHA256.HashData(executable)) != expectedSha256)
            {
                File.Delete(install.Resolve(Proof));
                return false;
            }

            var proof = install.Resolve(Proof);
            var staged = proof + $".{Guid.NewGuid():N}.part";
            try
            {
                File.WriteAllText(staged, stamp);
                File.Move(staged, proof, overwrite: true);
            }
            finally
            {
                File.Delete(staged);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ReadStamp(FileStream executable, string expectedSha256)
    {
        if (!GetFileInformationByHandle(executable.SafeFileHandle, out var info)
            || (info.IndexHigh == 0 && info.IndexLow == 0))
        {
            return null;
        }

        // The volume and file index change on normal executable replacement even if the
        // updater preserves the length and timestamp. A rewrite also changes LastWrite.
        return FormattableString.Invariant(
            $"{expectedSha256}:{info.Volume}:{info.IndexHigh}:{info.IndexLow}:{info.SizeHigh}:{info.SizeLow}:{info.Created.dwHighDateTime}:{info.Created.dwLowDateTime}:{info.LastWrite.dwHighDateTime}:{info.LastWrite.dwLowDateTime}");
    }

    // The interop is narrowly scoped to Windows executable identity.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out NativeFileInfo info);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileInfo
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created;
        public System.Runtime.InteropServices.ComTypes.FILETIME Accessed;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWrite;
        public uint Volume;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint IndexHigh;
        public uint IndexLow;
    }

    internal static EsHookStep? Ensure(RetroBatInstall install, bool verified)
    {
        if (!verified)
        {
            return null;
        }

        var source = install.Resolve(EsHooks.SourcePath);
        var target = install.Resolve(Path);
        var staging = System.IO.Path.Combine(install.AppDirectoryPath, $"prelaunch-{Guid.NewGuid():N}.part");
        try
        {
            var exists = File.Exists(target);
            if (exists && EsHooks.SameBytes(source, target))
            {
                return new EsHookStep("prelaunch", Path, EsHookAction.AlreadyCurrent);
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(source, staging);
            File.Move(staging, target, overwrite: true);
            return new EsHookStep("prelaunch", Path, exists ? EsHookAction.Updated : EsHookAction.Installed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EsHookStep("prelaunch", Path, EsHookAction.Failed, ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(staging);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The staged copy is outside the script folder and can never be invoked by ES.
            }
        }
    }
}
