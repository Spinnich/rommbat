// SDL2 loaded from RetroBat's own emulationstation/SDL2.dll, so the joystick indices this
// reports are the same ones EmulationStation wrote into es_input.cfg. Bundling a different
// SDL build would make an index mismatch possible and silent.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Probe1;

internal static class Sdl
{
    public const uint InitJoystick = 0x00000200;

    private static IntPtr _library;

    public static bool Load(string path, out string detail)
    {
        try
        {
            _library = NativeLibrary.Load(path);
            NativeLibrary.SetDllImportResolver(
                typeof(Sdl).Assembly,
                (name, _, _) => name == "SDL2" ? _library : IntPtr.Zero);

            SDL_GetVersion(out var version);
            detail = $"SDL {version.Major}.{version.Minor}.{version.Patch} from {path}";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Guid16
    {
        public unsafe fixed byte Data[16];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Version
    {
        public byte Major;
        public byte Minor;
        public byte Patch;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_Init(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_Quit();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_GetError();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_GetVersion(out Version version);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_NumJoysticks();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_JoystickOpen(int index);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_JoystickName(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern Guid16 SDL_JoystickGetGUID(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumButtons(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumAxes(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_JoystickNumHats(IntPtr joystick);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_JoystickUpdate();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_SetHint(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? string.Empty;

    public static string NameOf(IntPtr joystick) =>
        Marshal.PtrToStringUTF8(SDL_JoystickName(joystick)) ?? "(null)";

    public static unsafe string GuidOf(IntPtr joystick)
    {
        var guid = SDL_JoystickGetGUID(joystick);
        var text = new StringBuilder(32);
        for (var i = 0; i < 16; i++)
        {
            text.Append(guid.Data[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>
    /// Clears bytes 2-3, SDL's CRC-16 of the device name.
    /// </summary>
    /// <remarks>
    /// SDL 2.0.18+ fills that field at runtime; the GUID EmulationStation writes into
    /// es_input.cfg leaves it zeroed. The same pad therefore has two spellings and a
    /// straight string comparison never matches.
    /// </remarks>
    public static string ZeroNameCrc(string guid) =>
        guid.Length == 32 ? string.Concat(guid[..4], "0000", guid[8..]) : guid;
}
