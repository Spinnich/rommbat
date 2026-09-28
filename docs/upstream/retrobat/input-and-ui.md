---
summary: Controllers, `es_input.cfg`, SDL, and how ES behaves while a full-screen app runs.
read-when: Before changing controller input or the gamepad UI.
---

# RetroBat: input and UI

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-218. `es_input.cfg` is enough to drive a UI, and a vendor-id table could not be

Verified: RetroBat 8.2.1, 2026-08-25, and 2026-09-28. How: resolved the 8BitDo's names from live hardware through the shipped parser, and re-read the file.
The file is semantic: it records which physical input is `a` on each pad, not what kind of pad it
is. All 21 names in the 8BitDo's config resolved. Two cannot be expressed by pad type: the
8BitDo's `select` and `hotkey` are the same `button6`, and the Switch Pro reports its d-pad as
buttons 11 to 14 where the 8BitDo and the Xbox 360 pad report a hat. So `EsInputMap` reads the
file and never detects a layout.

## RB-219. ES stops reading the pad while a full-screen app is in front of it

Verified: RetroBat 8.2.1, 2026-08-25. How: stamped every `game-selected` event from a hook while RomMBat ran from the ES menu.
ES logged 63 navigation events, then none for the 26.5 s RomMBat was in front, while five d-pad
presses landed in RomMBat. It resumed 0.64 s after RomMBat exited, with the selection unchanged.
A `.menu` app is suspended exactly as a game is, so RomMBat needs no input workaround.

## RB-220. An ES-menu launch passes no controller arguments, and PadToKey attaches to nothing

Verified: RetroBat 8.2.1, 2026-08-25. How: compared `emulatorLauncher.log` for a menu launch and a game launch.
A game launch carries `-p1index`, `-p1guid`, `-p1path`, `-p1name` and the button, axis and hat
counts. A menu launch runs `RomMBat.exe` with no arguments, so `[SdlGameController] connected` and
`[PadToKey] Add joystick` never appear: PadToKey loads `es_padtokey.cfg` and attaches to nothing.
RomMBat has the controller to itself.

## RB-223. An analog trigger rests at `-32768`, not zero

Verified: RetroBat 8.2.1, 2026-08-25. How: read L2 and R2 through SDL on the 8BitDo, at rest and after release.
The same pad's sticks rest at zero. So an axis binding names a direction, and only a reading of
that sign is that input. `GamepadReader` checks the sign; a reader that took any non-zero value as
input would report both triggers permanently held.

## RB-225. `es_input.cfg`'s `a` and `b` match the printed labels, and `x` and `y` are swapped

Verified: RetroBat 8.2.1, 2026-08-26 and 2026-08-30, and 2026-09-28. How: a person pressed the button a footer named, then logged each printed button through `GamepadReader`, and re-read the file.
On the 8BitDo, `a` is SDL button 0 and `b` is 1, the buttons it prints A and B on. But `x` is
button 3, printed Y, and `y` is button 2, printed X. The DualSense, PS4 and Xbox 360 rows map the
four the same way.
The file is the authority on which physical input is meant, not on what it is called, so RomMBat
never shows these names as button labels (RB-230).

## RB-226. A console process sees no joysticks through SDL's default backend, silently

Verified: RetroBat 8.2.1, 2026-08-30. How: polled `SDL_NumJoysticks()` in a console process for 120 s with three controllers attached.
SDL 2.32.8 defaults to the RAWINPUT joystick backend, which needs a window message pump. The count
stayed 0 while Windows reported the Parsec pad, an Xbox Wireless Controller and the 8BitDo.
Setting `SDL_JOYSTICK_RAWINPUT=0` made the same process report 1 device at once. The Avalonia UI
pumps messages and is unaffected; a console probe that reports no controller is measuring itself.

## RB-227. A controller GUID depends on the SDL backend that read it

Verified: RetroBat 8.2.1, 2026-08-30, and 2026-09-28. How: read the Parsec pad's GUID under XInput and compared it with `es_input.cfg`, and re-read the file's GUIDs.
Bytes 14 and 15 are SDL's driver signature and bytes 12 and 13 its version. The Parsec pad reads
`...14017801` under XInput (`0x78`, `x`), and the file records `...00007200` under rawinput
(`0x72`, `r`, version zeroed). Every controller row in the file ends `7200`, `6800` or `6803`,
because ES writes what its own backend saw. `EsInputMap.NormalizeGuid` zeroes only bytes 2 and 3,
the name CRC, so a GUID read with the backend changed does not match the file.

## RB-229. ES treats a controller as hotpluggable

Verified: RetroBat 8.2.1, 2026-08-30. How: read ES's string tables in `resources/locale/*/LC_MESSAGES/emulationstation2.po`.
ES carries `%s connected` and `%s disconnected` as its own notifications. A front end launched
from inside ES is expected to notice a pad arriving, so `GamepadReader` re-enumerates (RB-231).

## RB-230. ES's footer draws a button's position, because a letter is wrong on two layouts of three

Verified: RetroBat 8.2.1, 2026-08-30, and 2026-09-28. How: read ES's footer on screen and the pads configured in `es_input.cfg`.
The bottom face button is A on an Xbox pad, Cross on a DualSense and B on a Switch Pro, and the
live file configures a Switch Pro, a DualSense, a PS4 pad, the 8BitDo and an Xbox 360 pad. ES
draws a four-dot diamond with one dot filled, which names a position, and `es_input.cfg` encodes
the same thing: `a` is the bottom button, `b` the right, `y` the left and `x` the top. RomMBat's
`FooterHint` carries a `NavAction`, never a string.

## RB-231. A controller switched off and on reaches `GamepadReader` again

Verified: RetroBat 8.2.1, 2026-08-30. How: ran `GamepadReader` in a probe while switching the 8BitDo off and on.
Started with the pad off, the reader reported `NoDevice`, then `Ready` with the pad's name once it
was switched on. Switched off, it returned to `NoDevice`; switched on again, to `Ready`, and input
resumed. 16 of the 21 configured names read correctly. The 11.1 s between the last two states is
the pad re-pairing and the tester's hands, which the log cannot separate from the reader's 1 s
scan interval.

## RB-234. ES's on-screen keyboard is compiled into `emulationstation.exe`, three layouts of four faces a key

Verified: RetroBat 8.2.1, 2026-08-30 and 2026-08-31. How: read `es-core/src/guis/GuiTextEditPopupKeyboard.cpp` in `batocera-linux/batocera-emulationstation`, and the keyboard on a live session.
`kbUs`, `kbFr` and `kbKr` are the only layouts. Each is a 13-column grid over five rows, with four
faces per key (lower, upper, alted, alted-upper), `DEL`, `OK` and `ALT` down the right edge, and
`SHIFT`, `SPACE`, `RESET` and `CANCEL` along the bottom. `OK` spans two rows, and the bottom row
spans 2, 7, 2 and 2. The bindings: `a` presses the key, `start` is OK, `b` BACK, `pageup` DELETE,
`pagedown` SPACE, `y` SHIFT and `x` RESET, which commits the empty string and closes. Left and
right wrap; up and down go to the text field. A key with an empty face on the current layer holds
focus and does nothing, and `altKeys()` clears shift. The shoulders are therefore taken, and
`OnScreenKeyboard` transcribes these tables rather than designing its own.

## RB-235. ES picks the keyboard layout from `es_settings.cfg`'s `Language`

Verified: RetroBat 8.2.1, 2026-08-31, and 2026-09-28. How: read `SystemConf` and `Paths.cpp` upstream, and checked the live install for `batocera.conf`.
ES asks `SystemConf` for `system.language`. `Paths.cpp` gives `SystemConf` a file only under
`#if defined(WIN32) && defined(_DEBUG)`, so a Windows release build falls back to ES's own
`Settings`, and `batocera.conf` is absent on the live install. The value is split on `_` and the
first part lowercased, so `fr_FR` resolves and a bare `FR` does not; anything but `fr` or `ko`
gets `kbUs`. ES prunes `Language` when it equals the default (RB-170), so absent means default.
