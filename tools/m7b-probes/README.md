# M7b probes: controller input, ES focus, and z-order

Sources only, as `tools/m0-probes/probe6-httpclient.cs` is. They are not in the solution and
CI does not build them: an Avalonia probe would put a UI framework reference in the tree twice
and the shipped one is `RomMBat.UI`. To run one, make a throwaway project outside the repo,
drop the files in, and reference `src/RomMBat.Core` so the probe measures the **shipped**
`EsInputMap` rather than a second copy of it.

```xml
<PackageReference Include="Avalonia" Version="11.3.7" />
<PackageReference Include="Avalonia.Win32" Version="11.3.7" />
<PackageReference Include="Avalonia.Skia" Version="11.3.7" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.7" />
<ProjectReference Include="<repo>/src/RomMBat.Core/RomMBat.Core.csproj" />
```

**Reference `Avalonia.Win32` and `Avalonia.Skia`, never `Avalonia.Desktop`.** The latter drags
in `Tmds.DBus.Protocol` for the X11 backend, which raises `NU1903` for a known high-severity
advisory, and CI builds `-warnaserror`. RomMBat ships win-x64 and has no use for the X11 or
macOS backends. Build the app with `.UseWin32().UseSkia()` rather than `.UsePlatformDetect()`.

| File                           | What it is                                                                        |
| ------------------------------ | --------------------------------------------------------------------------------- |
| `probe1-sdl.cs`                | P/Invoke onto RetroBat's own `emulationstation/SDL2.dll`, joystick subsystem only |
| `probe1-input-and-focus.cs`    | A full-screen Avalonia window that logs input, focus, foreground and ES liveness  |
| `probe1-selected-hook.cs`      | Stamps ES's `game-selected` / `system-selected`, which is what made 219 provable  |
| `probe4-unreachable-server.cs` | Times an unreachable server through the path the UI uses. Needs only Core         |
| `probe5-controller-hotplug.cs` | A controller switched off and on, through the shipped `GamepadReader`             |

## The two that mattered

**219, whether ES keeps reading the pad behind us**, could not be observed at all until the
selection hook existed: ES fires `game-selected` and `system-selected` on every navigation move
and **ships no folder for either**, so creating
`.emulationstation/scripts/game-selected/` is what turns the question from a judgement into a
record. Remove the folders afterwards; they are not part of a RetroBat install.

**218, whether a layout has to be detected**, is answered by `es_input.cfg` and not by the
probe. The probe's job was only to confirm the file's ids resolve against live hardware
through the shipped parser, which they do, for all 21 names on the 8BitDo.

**Probe 5 is the only evidence the hotplug path has**, and it has to be a window for the reason
in finding 226. Run it, switch the controller **off**, wait, switch it **on**, and press
something. It drives `GamepadReader` itself rather than a copy, so a green run is a statement
about the shipped code. Deleting the `SDL_JoystickGetAttached` check leaves the whole unit suite
passing, which is what this exists to cover.

## Three things learned the hard way, so the next session does not repeat them

- **Do not put the exit gesture on a button the sweep asks you to press.** The first two runs
  ended after 6 seconds because `start` both exited the probe and was one of the inputs under
  test. `--no-pad-exit` exists for that reason; the ES-menu run needs the pad exit because
  there is no keyboard in there.
- **A console probe sees no controllers at all, and says so confidently.** SDL 2.32.8 defaults
  to the RAWINPUT backend, which needs a Win32 message pump, so `SDL_NumJoysticks()` returns 0
  in a plain console process while three pads are attached. Run the probe with
  `SDL_JOYSTICK_RAWINPUT=0` in the environment, or put it behind a real window. Either way the
  GUID it then reads carries a different driver byte from the one `es_input.cfg` holds, so a
  console probe can measure _whether_ a pad is there and must not be used to check whether it
  matches the file. Findings 226 and 227.
- **The first observation of any input is its resting value, not a press.** Otherwise every run
  opens with a burst of phantom events, and on this pad two of them would be the triggers,
  which rest at `-32768` rather than zero (finding 223).

`probe4` takes a RetroBat root and a target, and defaults to `192.0.2.1:8080`. That address is
TEST-NET-1 from RFC 5737, reserved for documentation and guaranteed not to be a real host: a
made-up **hostname** would fail at DNS instead, which is a different and far faster path that
never exercises the connect timeout at all. Measured 2046 / 2002 / 2004 ms, against the 21 s an
unset `ConnectTimeout` costs.

Output goes to `probe-output/`, or to `emulators/rommbat/logs/` when the probe is installed
into a real tree as `RomMBat.exe`. Both are gitignored.
