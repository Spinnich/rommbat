---
summary: Controllers, `es_input.cfg`, SDL, and how ES behaves while a full-screen app runs.
read-when: Before changing controller input or the gamepad UI.
---

# RetroBat: input and UI

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-218. Enough, and detection is the wrong shape

Question: (not addressed) whether `es_input.cfg` is enough to drive a UI, or whether a controller layout has to be detected

Measured: **Enough, and detection is the wrong shape.** The file is semantic: it records which physical input is `a` on each pad, not what kind of pad it is. All 21 names in the 8BitDo's config resolved from live hardware through the shipped parser. Two of them cannot be expressed by a vendor-id table at all: `select` and `hotkey` are the **same button** (`button6`), and the Switch Pro reports its d-pad as **buttons 11-14** where the 8BitDo and Xbox report a **hat**

## RB-219. It does not, and nothing on our side is needed

Question: (not addressed) whether EmulationStation keeps reading the pad while a full-screen app is in front of it

Measured: **It does not, and nothing on our side is needed.** Instrumented with a stamping hook on `game-selected`: ES logged 63 navigation events up to 23:16:17.984, then **zero** for the 26.5 s RomMBat was in front, while five D-pad presses landed in RomMBat at 23:16:27.7 to 23:16:32.1. ES resumed 0.64 s after our exit and re-announced the unchanged selection. A `.menu` app is suspended exactly as a game is

## RB-220. Nothing, and it does not

Question: (not addressed) what `emulatorLauncher` hands a `.menu` app, and whether PadToKey competes for the pad

Measured: **Nothing, and it does not.** A real ES-menu launch carries **no `-p1*` controller arguments**, where a game launch carries `-p1index`, `-p1guid`, `-p1path`, `-p1name` and the button/axis/hat counts. So `[SdlGameController] connected` and `[PadToKey] Add joystick` / `Start listening` never appear: PadToKey loads `es_padtokey.cfg` and attaches to nothing. The app is launched argument-free (`[Running] ...\RomMBat.exe`) and **has the controller to itself**

## RB-223. `-32768`, not zero, on the same pad whose sticks rest at zero

Question: (not addressed) an analog trigger's resting value

Measured: **`-32768`, not zero, on the same pad whose sticks rest at zero.** L2 and R2 settle fully negative after release and stay there. So "any non-zero axis reading is an input" is wrong: an axis binding names a **direction**, and only a reading of that sign is that input. A naive reader reports both triggers permanently held

## RB-225. `a` and `b` do; `x` and `y` are the other way round

Question: (not addressed) whether `es_input.cfg`'s face-button names match the labels printed on the pad

Measured: **`a` and `b` do; `x` and `y` are the other way round.** On the 8BitDo the file maps `a` to SDL button 0 and `b` to 1, which are the buttons an Xbox-layout pad prints A and B on. But it maps **`x` to button 3 and `y` to button 2**, and 3 is the button printed **Y** while 2 is printed **X**. Found by a person pressing the button a footer told them to: a hint reading "X" ran on `es_input`'s `x` and did nothing until Y was pressed. So a UI that shows button prompts must not use these names as labels. `a` and `b` are safe, `x` and `y` are not, and the file is still the authority on _which physical input_ is meant, just not on what it is called

## RB-226. Not with SDL's default backend, and the failure is silent

Question: (not addressed) whether a console process can enumerate joysticks through RetroBat's SDL2

Measured: **Not with SDL's default backend, and the failure is silent.** `SDL_NumJoysticks()` returned **0** for a continuous 120 s in a console process, while Windows reported three present game controllers at the same moment: the Parsec pad, an Xbox Wireless Controller over Bluetooth LE, and the 8BitDo. SDL 2.32.8 defaults to the **RAWINPUT** joystick backend, which needs a window message pump. Setting `SDL_JOYSTICK_RAWINPUT=0` in the environment made the same process report **1** device immediately. RomMBat's own UI is unaffected, because Avalonia pumps messages; **a console probe of controller state is not**, and one that reports "no controller" is measuring itself rather than the machine

## RB-227. Only under the same SDL backend

Question: (not addressed) whether the GUID a probe reads is the GUID `es_input.cfg` holds

Measured: **Only under the same SDL backend.** Bytes 14-15 are SDL's driver signature and byte 12-13 its version. The same Parsec pad reads `030000005e0400008e02000014017801` under XInput (`0x78`, ASCII `x`) and `es_input.cfg` records `030000005e0400008e02000000007200` under rawinput (`0x72`, ASCII `r`, version zeroed). `EsInputMap.NormalizeGuid` zeroes bytes 2-3, the name CRC, and deliberately nothing else, so those two do not compare equal. Every controller row in the live file ends `7200`, `6800` or `6803` and none ends `78`: EmulationStation writes what its own backend saw. A GUID measured with the backend changed is therefore **not comparable to the file**, which is a trap for the next probe rather than a defect

## RB-228. It has, and two of its bindings were taken by RomMBat for something else

Question: (not addressed) whether EmulationStation has already solved the on-screen keyboard, and what it binds

Measured: **It has, and two of its bindings were taken by RomMBat for something else.** `GuiTextEditPopupKeyboard` on a live 8.2.1 session binds **A** to press the highlighted key, **Start** to OK, **B** to BACK, **L (`pageup`) to DELETE**, **R (`pagedown`) to SPACE**, a face button to SHIFT, and the d-pad to MOVE CURSOR. Corroborated in `resources/locale/*/LC_MESSAGES/emulationstation2.po`, which carries `MOVE CURSOR`, `SHIFT`, `SPACE`, `DELETE`, `RESET` and `SHIFTS FOR UPPER, LOWER, AND SPECIAL` as ES's own strings. So **the shoulders are not free**: RomMBat had put the case toggle on L1/R1, which is where a RetroBat user's thumb already expects delete and space. That string also says ES's keyboard has **three** layers (upper, lower, special) plus an `ALT GR`, where RomMBat's two suffice only because its one field is a URL. **Superseded on every detail by 234**, which read the source instead of the screen: the face buttons are `y` for SHIFT and `x` for RESET, and RomMBat now carries upstream's layout rather than two layers of its own

## RB-229. It does, and says so on screen

Question: (not addressed) whether EmulationStation itself treats a controller as hotpluggable

Measured: **It does, and says so on screen.** The same string table carries `%s connected` and `%s disconnected` as ES's own notifications. A front end launched from inside ES that cannot notice a pad arriving is the odd one out, which is what RomMBat was until this stage

## RB-230. Because a letter is wrong on two layouts out of three, and the live install has all three

Question: (not addressed) why EmulationStation's footer draws icons rather than naming buttons

Measured: **Because a letter is wrong on two layouts out of three, and the live install has all three.** The bottom face button is A on an Xbox pad, Cross on a DualSense and B on a Switch Pro, and `es_input.cfg` on `K:` configures Switch Pro, DualSense, PS4, 8BitDo and Xbox 360. ES draws a four-dot diamond with one dot filled, which names a **position** rather than a label, and position is the one thing every layout agrees on. It is also what `es_input.cfg` already encodes: `a` is the bottom button, `b` the right, `y` the left and `x` the top. Spinnich's observation, checked against the file rather than taken on trust

## RB-231. All three states, measured on the 8BitDo through the shipped `GamepadReader`

Question: (not addressed) whether RomMBat survives a controller switched off and on, on a locally connected pad

Measured: **All three states, measured on the 8BitDo through the shipped `GamepadReader`.** The probe launched with the pad **off** and reported `NoDevice`; the pad was switched on 24.4 s later and it went `Ready` and named it, which is the case that used to be unrecoverable for the life of the process. **16 of the 21 configured names** then read correctly (`a b x y pageup pagedown l2 r2 l3 r3 up down left right joystick1left joystick2up`). Switched off, it returned to `NoDevice` at 09:00:43.517; switched back on it returned to `Ready` at 09:00:54.609 and input resumed. **The 11.1 s between those two is the pad's own re-pairing plus the tester's hands, not RomMBat's latency**, which the log cannot separate and which is bounded by the 1 s scan interval. The run also re-confirmed **225** from the other side, by a person rather than by reading: the button printed **X** logs `y` and the one printed **Y** logs `x`, because the file maps `y` to button 2 and `x` to button 3. Probe 5

## RB-234. Read from upstream's source, which is where it lives: the tables are compiled in, not shipped as data

Question: **228 said ES ships a keyboard and left what it contains unread, which the layout this stage copies depends on**

Measured: **Read from upstream's source, which is where it lives: the tables are compiled in, not shipped as data.** `es-core/src/guis/GuiTextEditPopupKeyboard.cpp` in `batocera-linux/batocera-emulationstation`, which RetroBat builds as `RetroBat-Official/emulationstation`. **Three layouts exist and no more**, `kbUs`, `kbFr` and `kbKr`, each a 13-column grid of **four faces per key** (lower, upper, alted, alted-upper) over five rows, with `DEL`, `OK` and `ALT` down the right edge and `SHIFT`, `SPACE`, `RESET`, `CANCEL` along the bottom. `OK` spans two rows and the bottom row spans 2/7/2/2. **The buttons**: `a` presses the key, `start` OK, `b` BACK, `pageup` DELETE, `pagedown` SPACE, **`y` SHIFT and `x` RESET**, and RESET means _commit the empty string and close_ rather than clear the field. Left and right wrap; up and down go to the text field. A key whose face is empty on the current layer is drawn, holds focus and does nothing, which is why every layer is the same shape. `altKeys()` clears shift on the way in

## RB-235. `es_settings.cfg`'s `Language`, and only by accident

Question: (not addressed) which language picks the layout, and where a Windows RetroBat keeps it

Measured: **`es_settings.cfg`'s `Language`, and only by accident.** ES asks `SystemConf` for `system.language`; `SystemConf` falls back to ES's own `Settings` when it has no config file of its own, and `Paths.cpp` sets that file **only under `#if defined(WIN32) && defined(_DEBUG)`**, so a Windows release build has none and the fallback is the whole path. `batocera.conf` is **absent on the live 8.2.1 install**, which agrees. The value is split on `_` and the part before it lowercased, so `fr_FR` resolves and a bare `FR` would not, and anything but `fr` or `ko` gets `kbUs`. `Language` is also **pruned when it equals ES's default** (RB-170), so absent is the ordinary reading of "default" rather than evidence nobody chose
