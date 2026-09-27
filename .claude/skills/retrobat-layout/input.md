# Gamepad input

Part of the [retrobat-layout](SKILL.md) skill. How the UI reads the controller and names its buttons.

## Controller input: read `es_input.cfg`, never detect a layout

**EmulationStation has already answered the question a layout lookup only guesses at.**
`.emulationstation/es_input.cfg` is semantic: it does not record that a pad is an Xbox pad, it
records **which physical input is `a` on that pad**. Read it live, for the same reason
`es_systems.cfg` is read live, and because a user who remapped their pad in ES has said what
they want.

Measured against five real controllers on 8.2.1, and each of these refutes a vendor-id table:

- The **8BitDo Ultimate 2** maps `a`/`b`/`x`/`y` **byte-identically to the Xbox 360 pad**. Its
  vendor id is `0x2dc8`, which Argosy's `ControllerDetector` lists as a **Nintendo** layout. The
  table gets a real, common pad backwards.
- The **Switch Pro** genuinely differs (`a`=1, `b`=0), so that is a live difference, not an
  absent one.
- The **Switch Pro reports its d-pad as buttons 11-14** while the other four report a **hat**.
  That is a difference of shape, and no vendor-id table can express it.

Three traps, all measured rather than reasoned about:

- **`x` and `y` do not mean what the pad says they mean.** `a` and `b` match the labels on an
  Xbox-layout pad, but `x` maps to the button printed **Y** and `y` to the button printed **X**.
  So the file is the authority on _which physical input_ a name refers to, and **not** on what
  to print in a button prompt: a footer hint that says "X" and runs on `es_input`'s `x` sends
  the user to the wrong button, which is how this was found. Finding 225.
- **One press can mean two things.** `select` and `hotkey` are the **same button** on both the
  8BitDo and the Xbox pad. A lookup that returns the first match silently drops the hotkey, so
  resolve to **every** name a reading satisfies.
- **A hat is a bitmask**, `up`=1, `right`=2, `down`=4, `left`=8, so a diagonal sets two bits and
  means both directions. Compare bits, never equality.
- **An analog trigger rests at `-32768`, not zero**, on the same pad whose sticks rest at zero.
  An axis binding names a **direction** (`value` is `-1` or `1`) and only a reading of that sign
  is that input. Treating any non-zero axis as pressed reports both triggers permanently held.

**A half-written `es_input.cfg` is an ordinary state, so parsing it must not throw.** ES rewrites
the whole file every time a pad is configured, which makes an interrupted write the normal way to
find one that is empty or unclosed, and `XDocument.Load` on an empty file throws
`XmlException: Root element is missing`. The UI reads this before any window exists, in a
`WinExe`: a throw there is the ES menu entry flashing and returning with nothing on screen, on a
device with no keyboard to diagnose from. `EsInputMap.Read` degrades to an **empty map** and keeps
the reason in `Problem`; `EsInputMap.Load`, which a caller hands a path to, throws
`EsInputException` as `EsSystemsFile` and `GamelistDocument` do for the other two live ES XML
files. Keeping the reason matters: an empty map otherwise reaches the status screen as "your pad
is not configured" for a pad that is. The remedy is the same sentence either way, because
configuring a controller in ES is what rewrites the file.

**The GUID has two spellings and a straight comparison never matches.** SDL 2.0.18+ fills bytes
2-3 of a joystick GUID with a CRC-16 of the device name; ES writes them zeroed. The same 8BitDo
is `0300b155c82d0000...` from the running library and `03000000c82d0000...` in the file.
Normalise before comparing; `EsInputMap.NormalizeGuid` is the one place that does it.

**The ids are SDL _joystick_ indices, so only the same library can read them.** `emulationstation.exe`
imports **`SDL2.dll`** (2.32.8 on 8.2.1) and not `SDL3.dll`, though RetroBat ships both. RomMBat
loads **`emulationstation/SDL2.dll`** rather than bundling its own build: that costs zero
published bytes, and more importantly it makes an index mismatch impossible by construction,
where a different SDL build enumerating some pad differently would mis-map **silently**.
`SDL_Init(SDL_INIT_JOYSTICK)` alone is enough, with no video subsystem and no SDL event loop.
**It does need a Win32 message pump, which is a different thing**, and the failure is silent:
SDL 2.32.8 defaults to the RAWINPUT backend, and in a console process with no pumped window
`SDL_NumJoysticks()` returns **0** while three controllers are attached (finding 226). Avalonia
pumps, so the shipped UI is unaffected; **a console probe of controller state is not**, and has
to set `SDL_JOYSTICK_RAWINPUT=0`, which then changes the GUID it reads and makes it
incomparable to the file (finding 227). If the library is missing or the pad has no
`inputConfig`, say so and name the fix
(configure the controller in EmulationStation first) rather than inventing a default map: a pad
ES cannot drive is one the user's own front end cannot drive either.

**EmulationStation has already answered the on-screen keyboard, and RomMBat now copies it
rather than resembling it.** `GuiTextEditPopupKeyboard` binds **A** to press the highlighted key,
**Start** to OK, **B** to BACK, **L (`pageup`) to DELETE**, **R (`pagedown`) to SPACE**,
**`y` to SHIFT** and **`x` to RESET**, with the d-pad moving the cursor. Findings 228 and 234:
read off a live 8.2.1 session, corroborated in
`resources/locale/*/LC_MESSAGES/emulationstation2.po`, and then settled against upstream's own
source, which is the only place the layout exists.

- **The tables are compiled into `emulationstation.exe`**, so there is nothing for
  `reference/refresh.sh` to pull and nothing on disk to read. `KeyboardLayouts` holds a
  transcription in upstream's exact shape, which is what makes re-checking it against a newer ES
  a diff. Its provenance comment names the file; keep it accurate or the copy becomes folklore.
- **Three layouts, `kbUs`, `kbFr` and `kbKr`, and no mechanism to add one.** A German install
  types on the US grid upstream and does here too.
- **Four faces per key** (lower, upper, alted, alted-upper) on a **13-column, 5-row** grid.
  `OK` spans two rows, the layer key two columns, and the bottom row 2/7/2/2. A key whose face
  is empty on the current layer is **drawn, holds focus and does nothing**, which is what keeps
  every layer the same shape and the cursor impossible to strand. Do not compact it.
- **The shoulders are not free.** RomMBat first put the case toggle there, which is where a
  RetroBat user's thumb expects delete and space.
- **`:` and `/` are on the upper face**, so a URL costs one shift press. That is upstream's
  layout and not a slip; the grid RetroBat already taught the user beats two presses saved.
- **RESET is the one key whose meaning does not transfer.** Upstream's commits the empty string
  and closes, which is how a setting is cleared there. RomMBat has no field that may be empty,
  so the same key in the same place under the same word restores what the screen opened with.

**The language picks the layout, and on Windows it lives in `es_settings.cfg`.** ES asks
`SystemConf` for `system.language`; on a Windows **release** build `SystemConf` has no config
file of its own, because `Paths.cpp` sets one only under `#if defined(WIN32) && defined(_DEBUG)`,
so it falls back to ES's `Settings` and reads `Language` from `es_settings.cfg`. `batocera.conf`
is absent on the live install, which agrees. `InstallSession.EmulationStationLanguage` is the one
place that reads it: **the UI may not name `EsSettingsFile`** and a structural test says so.
**Absent is the ordinary answer** and means ES's default, because ES prunes any setting equal to
one (finding 170), so never read a null here as "nobody chose". Finding 235.

**Never print a button letter in a prompt; draw its position.** The bottom face button is A on
an Xbox pad, Cross on a DualSense and B on a Switch Pro, so any letter is wrong on two layouts
out of three, and a stock RetroBat `es_input.cfg` routinely has all three configured. ES draws a
four-dot diamond with one dot filled, naming a **position**, which is what `es_input.cfg` already
encodes: `a` is the bottom button, `b` the right, `y` the left, `x` the top. In RomMBat a
`FooterHint` therefore carries a `NavAction` and never a string, so a screen **cannot** name a
button and there is one place to be wrong. Finding 230.

**Closing the hint channel is not the whole rule, because prose is a second channel.** The
first build with position glyphs in its footer still had "Press A to pair" in a status row and
in `README.md`, which on a Switch Pro names the button that is EmulationStation's `b`, which is
back, which on the root screen closes RomMBat: the instruction did the opposite of what it
said. The ban covers **every** user-facing string, not the footer only. Where a sentence has to
point at an action, quote the footer's own **label** ("Pair with RomM"), which the renderer
draws next to the glyph, so the words on the two lines match and neither names a letter.
`StatusScreenTests.No_string_this_screen_shows_names_a_face_button` asserts it, because the
type system cannot.

**ES surfaces controller hotplug itself**, with `%s connected` and `%s disconnected` in that same
string table. A front end living inside it that cannot notice a pad arriving is the odd one out.

**Enumerate more than once, because a controller is not a fixed fact about a session.** A pad
asleep in its cradle at launch, batteries that go mid-session, and a virtual pad from a
streaming host that attaches only once the client sends input are the same shape, and all three
end with a person holding a controller that does nothing and no way to reach the thing that
would restart the app. A lost pad also does not announce itself: reading a handle whose device
has gone away returns released buttons and centred axes, which is exactly what an untouched
controller looks like, so `SDL_JoystickGetAttached` is the only way to tell them apart.
`GamepadReader` asks that per frame and re-enumerates once a second when it holds nothing.

**A UI launched from the ES menu has the controller to itself, and needs no workaround.**
Measured with a stamping hook on `game-selected`: ES fired **zero** navigation events during the
26.5 s a full-screen app was in front of it, while five D-pad presses landed in the app, and it
resumed 0.64 s after the app exited with its selection unchanged. ES suspends a `.menu` app
exactly as it suspends a game. `emulatorLauncher` does not compete either: an ES-menu launch
carries **no `-p1*` controller arguments**, so `PadToKey` loads its config and attaches to
nothing. Findings 218 to 220, 223.

**`es_padtokey.cfg` is not a navigation mechanism.** 153 apps, and 156 of about 170 mappings are
`hotkey start` to close or kill; exactly one maps directions to arrow keys. There is no default
or global section, so an app that is not listed inherits nothing.
