using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// SDL2, loaded from the copy EmulationStation itself uses.
/// </summary>
/// <remarks>
/// <b>RetroBat's build, not one of ours, and the reason is correctness before size.</b>
/// <see cref="EsInputMap"/>'s ids are SDL <i>joystick</i> indices written by whatever SDL
/// EmulationStation linked, so they only mean anything to a reader using that same library.
/// Loading <c>emulationstation/SDL2.dll</c> makes an index mismatch impossible by
/// construction; a bundled build that enumerated some pad differently would mis-map
/// <b>silently</b>, which is the worst failure shape available here. It also costs zero
/// published bytes, which is a real benefit and not the argument.
/// <para>
/// Measured on 8.2.1: <c>emulationstation.exe</c> imports <c>SDL2.dll</c> (2.32.8) and not
/// <c>SDL3.dll</c>, though RetroBat ships both. <c>SDL_Init(SDL_INIT_JOYSTICK)</c> alone
/// succeeds with no video subsystem, no window and no message pump of ours.
/// </para>
/// <para>
/// <b>Nothing here throws.</b> A missing or unloadable library is a state the UI reports, not
/// a fault: a pad EmulationStation cannot drive is one the user's own front end cannot drive
/// either, so the honest answer is to say so and name the fix.
/// </para>
/// </remarks>
internal static class SdlLibrary
{
    /// <summary>Where EmulationStation's own copy lives, relative to the RetroBat root.</summary>
    public static RelativePath Location { get; } = RelativePath.Create("emulationstation/SDL2.dll");

    public const uint InitJoystick = 0x00000200;

    private static IntPtr _handle;
    private static bool _resolverInstalled;

    /// <summary>Loads the library, once per process.</summary>
    /// <returns>Null on success, or the reason it could not be loaded.</returns>
    public static string? Load(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        if (_handle != IntPtr.Zero)
        {
            return null;
        }

        var path = install.Resolve(Location);
        if (!File.Exists(path))
        {
            return $"EmulationStation's SDL2 is not at {Location}. RomMBat reads the controller "
                + "through the same library EmulationStation uses, so it cannot read one without it.";
        }

        try
        {
            _handle = NativeLibrary.Load(path);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            return $"{Location} could not be loaded: {ex.Message}";
        }

        if (!_resolverInstalled)
        {
            NativeLibrary.SetDllImportResolver(
                typeof(SdlLibrary).Assembly,
                (name, _, _) => name == "SDL2" ? _handle : IntPtr.Zero);

            _resolverInstalled = true;
        }

        return null;
    }

    public static string Version()
    {
        SDL_GetVersion(out var version);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{version.Patch}");
    }

    public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? string.Empty;

    public static string NameOf(IntPtr joystick) =>
        Marshal.PtrToStringUTF8(SDL_JoystickName(joystick)) ?? string.Empty;

    public static string NameForIndex(int index) =>
        Marshal.PtrToStringUTF8(SDL_JoystickNameForIndex(index)) ?? string.Empty;

    /// <summary>The joystick's GUID as the 32-character hex string SDL renders it in.</summary>
    public static string GuidOf(IntPtr joystick) => Hex(SDL_JoystickGetGUID(joystick));

    /// <summary>The same, for a device that has not been opened.</summary>
    public static string GuidForIndex(int index) => Hex(SDL_JoystickGetDeviceGUID(index));

    private static string Hex(Guid16 guid)
    {
        var text = new StringBuilder(32);
        for (var i = 0; i < 16; i++)
        {
            text.Append(guid[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>
    /// SDL's <c>SDL_JoystickGUID</c>, sixteen bytes returned by value.
    /// </summary>
    /// <remarks>
    /// An inline array rather than a <c>fixed</c> buffer, so Core does not have to be built
    /// with <c>AllowUnsafeBlocks</c> for one struct.
    /// </remarks>
    [System.Runtime.CompilerServices.InlineArray(16)]
    internal struct Guid16
    {
        private byte _element0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SdlVersion
    {
        public byte Major;
        public byte Minor;
        public byte Patch;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_Init(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_QuitSubSystem(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_GetError();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_GetVersion(out SdlVersion version);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_NumJoysticks();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_JoystickOpen(int index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_JoystickClose(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_JoystickName(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_JoystickNameForIndex(int index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern Guid16 SDL_JoystickGetGUID(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern Guid16 SDL_JoystickGetDeviceGUID(int index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumButtons(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumAxes(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumHats(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_JoystickUpdate();

    /// <summary>SDL_TRUE while the device behind an open handle is still present.</summary>
    /// <remarks>
    /// A handle to a pad that has gone away does not fail: every button reads released and
    /// every axis reads centred, so a lost controller is indistinguishable from a still one
    /// without asking.
    /// </remarks>
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickGetAttached(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);
}
