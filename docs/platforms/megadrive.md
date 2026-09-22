# megadrive

Sega Mega Drive / Genesis. RetroBat calls the folder `megadrive`, which is what this file is
named after.

**Seven of the eleven rows `megadrive` declares are certified**, at RomM `5.3.0` and RetroBat
8.2.1, on 2026-09-21, all nine steps with step 6 N/A because `megadrive` has no class D:

- `libretro`/`genesis_plus_gx`, **the row a stock install gives a user**, selected with no override
- `libretro`/`genesis_plus_gx_wide` and `libretro`/`picodrive`
- `bizhawk`/`Genplus-gx`, `jgenesis`, `mednafen`/`megadrive` and `ares`/`MegaDrive`

**Four are driven and not certified**, and each says why in its own section:

- `libretro`/`fbneo` **never boots this library.** FBNeo finds a Mega Drive game by its own set
  name, taken from the file name, and every ROM here carries its No-Intro name (finding 278).
- `kega-fusion`/`auto`, `kega-fusion`/`genesis` and `kega-fusion`/`megadrive` **fail step 4**:
  Kega Fusion writes its battery saves into `emulators/kega-fusion/`, outside `saves/`, because
  RetroBat's template `Fusion.ini` sends them there and `emulatorLauncher` never redirects them
  (finding 283). Their states sync, and the pad works only once remapped in Kega's own menu
  (finding 284).

**It certifies those seven rows and nothing wider.** The last four needed code first, as on
`nes`: a battery rule each and, for `mednafen` and `ares`, a state declaration in RomMBat's
bundled supplement. Every one of those is scoped to `nes` and `megadrive`, the two systems they
were measured on.

**This file is in five parts.** Steps 1, 2 and 3, which are the system's and were staged before
anyone sat down. Then the four `libretro` rows, the four rows that needed code, and the three
`kega-fusion` rows. Last, what the pass turned up that is not a row: the fix for another client's
`null` save, which landed in the same PR.

## The install this was measured on

|           |                                                                       |
| --------- | --------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                             |
| RomM      | `5.3.0`, the supported floor, read back by `status` as Supported      |
| Root      | `R:\RetroBat`, found by walking up from the executable                |
| Store     | schema 16 of 16, WAL                                                  |
| Client    | a deploy of the `certify-megadrive` branch by `tools/publish.ps1`     |
| Budget    | `none`, as for `nes`, so a missing cover at step 7 cannot be headroom |
| Test game | Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination)   |

**The client was deployed three times, and which build a result was taken on is named.** The
first carried the `null` save refusal and the `--help` fix, and drove the four `libretro` rows.
The second added the megadrive battery rules and state declarations, and is the one the four
rows needing code were certified on. The third added the `kega-fusion` state declaration.

**The test game was picked for its save.** Sonic 3 & Knuckles writes battery RAM the moment a
data-select slot is chosen, so each session made a new save in seconds, and a slot the previous
row used shows on screen as used, which tells whether two emulators read one file. It carries
no `<emulator>` pin in `gamelist.xml`, and no game in the megadrive list does.

**The maintainer played over RDP, and the agent drove the second state on each non-`libretro`
row** from its session on the RetroBat machine: `emulatorLauncher` started with the row's
`-system`, `-emulator`, `-core` and `-rom`, and the keys sent by `keybd_event` with hardware scan
codes. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Sega Genesis Favorites           |
| Scope    | `smart_collection 7`                        |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 252 games, 199.6 MB, into `megadrive`       |

## Steps 1, 2 and 3, for every row

Staged at `5.3.0` before the sessions and re-confirmed on the day. None needs an emulator, so all
eleven rows carry them.

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `megadrive`,
which is already a folder in this install. As on `nes`, that is a property of this instance.

| `fs_slug`              | Resolved by | What `platforms list` says                                                  |
| ---------------------- | ----------- | --------------------------------------------------------------------------- |
| `megadrive`            | `fs_slug`   | RomM's fs_slug 'megadrive' is already a folder in this install              |
| `megadrive-unofficial` | `bundled`   | the bundled table offers `megadrive`, `megadrive-msu`, and picked the first |

### 2. Extensions

From the live `es_systems.cfg`:

```text
.68k .sgd .smd .bin .gen .md .sg .wad .zip .7z
```

**252 of 252 resolve and nothing is excluded.** Every ROM is a `.zip`, 252 of them holding a
`.md` and one of those a `.bin` beside it. `.zip` was observed to launch on every row except
`fbneo`, which refuses it for its name rather than its extension. The rest of the list is the
system's union and unproven per row.

### 3. BIOS

```console
$ rommbat-agent bios megadrive
RetroBat requires no BIOS for megadrive.
$ echo $?
0
```

A real system with nothing to fetch, which counts as step 3 passing.

### 6. Per-game memory card

**N/A on every row.** `megadrive` is class A in `save_shapes.json`, and no row wrote anything
that is not one game's own save.

## The four `libretro` rows

|              | `genesis_plus_gx`                          | `genesis_plus_gx_wide`                  | `picodrive`                  | `fbneo`                                 |
| ------------ | ------------------------------------------ | --------------------------------------- | ---------------------------- | --------------------------------------- |
| Selected by  | **Nothing: RetroBat's default**            | `megadrive.core = genesis_plus_gx_wide` | `megadrive.core = picodrive` | `megadrive.core = fbneo`                |
| Confirmed by | `-emulator libretro -core genesis_plus_gx` | `genesis_plus_gx_wide_libretro.dll`     | `-core picodrive`            | `fbneo_libretro.dll ... --subsystem md` |
| Result       | **Certified**                              | **Certified**                           | **Certified**                | **Driven, not certifiable here**        |

**The stock row names its core on the launch line.** With neither key set, ES filled in
`-emulator libretro -core genesis_plus_gx`, the first `es_systems.cfg` lists, and the three
overrides were set in `es_settings.cfg` with ES closed and removed afterwards, which is how the
install was found and left.

### Checklist for the three certified `libretro` rows

| #   | `genesis_plus_gx`                                     | `genesis_plus_gx_wide`                       | `picodrive`                                         |
| --- | ----------------------------------------------------- | -------------------------------------------- | --------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, `.srm`, 980 B     | **Pass, both directions.** The shared `.srm` | **Pass, both directions.** The shared `.srm`, 16 KB |
| 5   | **Pass**, screenshot byte-checked                     | **Pass**, screenshot byte-checked            | **Pass**, screenshot byte-checked                   |
| 7   | **Pass.** Box art and description on screen           | **Pass**, carried                            | **Pass**, carried                                   |
| 8   | **Pass.** 22:59:42Z to 23:00:38Z, 56s                 | **Pass.** 23:06:01Z to 23:06:36Z, 34s        | **Pass.** 23:53:02Z to 23:54:08Z, 1m 5s             |
| 9   | **Pass.** 0 downloaded, 0 written, gamelist identical | **Pass**                                     | **Pass**                                            |

### 4. Battery save on the three

All three write `saves/megadrive/<rom>.srm`, and each session picked a data-select slot the one
before had not, so each shows the next reading the last.

| Row                    | Before        | After         | Size     | Server rows at the restore |
| ---------------------- | ------------- | ------------- | -------- | -------------------------- |
| `genesis_plus_gx`      | `ed4db2dc...` | `22103c18...` | 980 B    | newest of 2                |
| `genesis_plus_gx_wide` | `22103c18...` | `b60c4918...` | 980 B    | newest of 3                |
| `picodrive`            | `b60c4918...` | `6ba79e41...` | 16,384 B | newest of 4                |

Each went up as a new version of `libretro:battery` through the detached `quit` pass, which
exited 0 every time, was moved out of the tree with its slot 2 state, and came back through
`saves restore 203767 --apply` at its own md5, exit 0.

**`picodrive` read the file the other two wrote and wrote it back sixteen times the size.** The
earlier slots showed as used on its data-select screen. Its 16,384 B file matches the 980 B one in
all but the 12 bytes of the new slot, and is zero from byte 980 on: Genesis Plus GX trims the
file at the last used byte (979), and PicoDrive keeps the whole SRAM window. So the three cores
share one save across two sizes, each switch uploads a new version, and that is right, since the
bytes change. Finding 277.

### 5. States on the three

| Row                    | Slot | State md5     | Screenshot md5 |
| ---------------------- | ---- | ------------- | -------------- |
| `genesis_plus_gx`      | 1    | `60fe8ead...` | `92a12709...`  |
| `genesis_plus_gx`      | 2    | `89f0541d...` | `ea0da9e3...`  |
| `genesis_plus_gx_wide` | 1    | `548d62db...` | `191561da...`  |
| `genesis_plus_gx_wide` | 2    | `66e91029...` | `ccbde430...`  |
| `picodrive`            | 1    | `3a3987dc...` | `24e9858f...`  |
| `picodrive`            | 2    | `3d12e9c7...` | `b91e1fad...`  |

**The declared `<directory>` is where each core wrote**, `saves/megadrive/libretro.<core>/`, and
RetroArch's log shows it choosing the slot, `found_last_state_slot: #0` against an empty
directory, as finding 261 describes. On each row slot 2's state and `.png` were moved out with the
`.srm`; the preview named the screenshot it would bring back, the apply answered `with 1
screenshot(s)`, and **every file came back at its own md5**, slot 2's image differing from slot
1's on every row.

### `libretro`/`fbneo`: driven, and not certifiable on this library

**FBNeo showed its own "Unknown Romset" screen and never started the game.** RetroBat launched it
as `fbneo_libretro.dll "<rom>.zip" --subsystem md`, and under that subsystem FBNeo takes the
driver name from the file name: `md_` plus the stem. No-Intro's name is no FBNeo driver.

**The name is the whole barrier**, measured outside the ROM tree with RetroArch started directly
on two copies of one ROM, Sonic The Hedgehog (USA, Europe):

| File                                   | What FBNeo did                                                      |
| -------------------------------------- | ------------------------------------------------------------------- |
| `Sonic The Hedgehog (USA, Europe).zip` | searched for no romset at all, 640x480, the "Unknown Romset" screen |
| `sonic.zip`, the same bytes            | `Romset found`, 320x224, the Mega Drive's resolution: the game ran  |

So this row cannot boot any of the 252 games, and it is not a RomMBat result: FBNeo's set names
are its own dat's, and RomM hands out the library's names. **Recorded as not certifiable on a
No-Intro library, by the maintainer's ruling**, rather than as a failure owed a fix. The one
lock-on driver in this core that could be the test game is `md_sks3`, a two-ROM lock-on set rather
than No-Intro's combined file. Nothing was written under `saves/`; the ES launch left two empty
directories, `saves/megadrive/fbneo/` and `saves/megadrive/libretro.fbneo/`. Finding 278.

## `bizhawk`, `jgenesis`, `mednafen` and `ares`

**All four certified on 2026-09-21, on the second deploy**, the first build to carry their
megadrive rules. Each was first driven on the first deploy, which is how the rules were measured:
until then `bizhawk`'s `.SaveRAM` was reported `not in this release`, and `mednafen`'s and `ares`'s
states were invisible. Its first flush sent **4 saves and 4 states**, everything those sessions had
left unsyncable.

|              | `bizhawk`/`Genplus-gx`                               | `jgenesis`                          | `mednafen`/`megadrive`                               | `ares`/`MegaDrive`                               |
| ------------ | ---------------------------------------------------- | ----------------------------------- | ---------------------------------------------------- | ------------------------------------------------ |
| Selected by  | `megadrive.emulator = bizhawk`, `.core = Genplus-gx` | `megadrive.emulator = jgenesis`     | `megadrive.emulator = mednafen`, `.core = megadrive` | `megadrive.emulator = ares`, `.core = MegaDrive` |
| Confirmed by | `-emulator bizhawk -core Genplus-gx`, `EmuHawk.exe`  | `-emulator jgenesis`, empty `-core` | `-emulator mednafen -core megadrive`                 | `-emulator ares -core MegaDrive`                 |
| ES's slot    | `-state_slot 3`, **taken**                           | `-state_slot 4`, ignored            | `-state_slot 4`, ignored                             | `-state_slot 4`, ignored                         |

**Every one of the four kept its own save**, apart from the `.srm` the `libretro` cores share and
from each other, so the maintainer's data select was new on each.

### Checklist for the four

| #   | `bizhawk`                                   | `jgenesis`                    | `mednafen`                                 | `ares`                        |
| --- | ------------------------------------------- | ----------------------------- | ------------------------------------------ | ----------------------------- |
| 4   | **Pass, both directions**, the title's name | **Pass, both directions**     | **Pass, both directions**, the hashed name | **Pass, both directions**     |
| 5   | **Pass**, two slots, frame inside the state | **Pass**, two slots, no image | **Pass**, two slots, no image              | **Pass**, two slots, no image |
| 7   | **Pass**, carried                           | **Pass**, carried             | **Pass**, carried                          | **Pass**, carried             |
| 8   | **Pass.** 00:05:17Z, 1m 3s                  | **Pass.** 00:10:21Z, 29s      | **Pass.** 00:13:56Z, 24s                   | **Pass.** 00:17:44Z, 33s      |
| 9   | **Pass**                                    | **Pass**                      | **Pass**                                   | **Pass**                      |

### 4. Battery saves on the four

| Row        | File under `saves/megadrive/`                          | Size     | Slot               | md5           |
| ---------- | ------------------------------------------------------ | -------- | ------------------ | ------------- |
| `bizhawk`  | `bizhawk/Sonic and Knuckles & Sonic 3 (W) [!].SaveRAM` | 16,384 B | `bizhawk:battery`  | `dbdd1cbe...` |
| `jgenesis` | `jgenesis/md/<rom>.sav`                                | 512 B    | `jgenesis:battery` | `1e13c9bf...` |
| `mednafen` | `<rom>.c5b1c655c19f462ade0ac4e17a844d10.sav`           | 1,024 B  | `mednafen:battery` | `e720b429...` |
| `ares`     | `ares/Mega Drive/<rom>.ram`                            | 512 B    | `ares:battery`     | `579678d1...` |

**Each keeps its own file, at its own size.** How much of the cartridge's SRAM window an emulator
keeps is its own choice, and none of these four reads another's file or the `libretro` one, since
each keeps it in its own place. Finding 277 records the sizes beside the two `libretro` ones.

**`bizhawk` names the save after its own title for the game**, `Sonic and Knuckles & Sonic 3 (W)
[!]`, and the state sidecar reads `Sonic and Knuckles & Sonic 3 (W) [!].Genplus-gx`, which is what
binds it (#151). The title is unique on this install. **Unlike `nes`, a boot did not change the
save**: the agent's launch left a `.SaveRAM.bak` at the same md5, where finding 272 measured NES
games rewriting theirs on boot.

**`jgenesis` keeps megadrive saves under `jgenesis/md/`**, where `nes` used `jgenesis/nes/`: it
names the directory after its own system. `emulatorLauncher` rewrites `custom_save_path` in
`jgenesis-config.toml` per launch, from `saves\nes\jgenesis` to `saves\megadrive\jgenesis`.
Finding 279.

**`mednafen` hashed the name because the plain one was free**, which is finding 273 again: no
`<rom>.sav` existed, the `libretro` file being `.srm`. **The hash is of the whole `.md`**, which the
file inside the zip hashes to; there is no header to leave off as there is on `nes`. A restore onto
a device that never held the file computes it from the ROM, through `MednafenRomHash`, and this
pass is what drove that: the restore below placed the save under the computed name. Finding 280.

**`ares` names its directory `Mega Drive`, with a space**, where `nes` used `Famicom`. So one
supplement entry per emulator could not describe `ares` any more, and the schema now takes one per
system. Finding 281.

All four went up through the flush on the second deploy, were moved out of the tree with both of
their states, and came back through one `saves restore 203767 --apply`: `restored 4 save(s) and 8
state(s), failed 0, 1.2 MB`, exit 0, **all twelve files at their own md5**. A flush afterwards
reported `49 already in step` and sent nothing.

### 5. States on the four

| Row        | Directory under `saves/megadrive/` | Maintainer's                | Agent's                     | Keys       |
| ---------- | ---------------------------------- | --------------------------- | --------------------------- | ---------- |
| `bizhawk`  | `bizhawk/sstates/Genplus-gx/`      | `QuickSave3`, `7a105de9...` | `QuickSave2`, `0d3425aa...` | `Ctrl+F2`  |
| `jgenesis` | `jgenesis/states/`                 | `_0`, `198c84e4...`         | `_1`, `c0487bc1...`         | `F7`, `F2` |
| `mednafen` | `mednafen/sstates/`                | `.mc0`, `e36f9e9a...`       | `.mc1`, `718b43dc...`       | `F7`, `F2` |
| `ares`     | `ares/Mega Drive/`                 | `.bs1`, `08654beb...`       | `.bs2`, `f03ee96e...`       | `F7`, `F2` |

**`bizhawk` took ES's slot and the other three did not**, as on `nes` (findings 269 and 275).
The maintainer's pad key wrote `QuickSave3` twice, the second replacing the first as a `.bak`, and
`Ctrl+F2` wrote slot 2. `jgenesis` and `bizhawk` write in their own trees and `emulatorLauncher`
mirrors the file into `saves/` in the same second, with a `.txt` sidecar; the other two write
straight into `saves/`.

**None writes a screenshot file.** BizHawk's frame is `Framebuffer.bmp` inside the `.State`, and
the two slots hold different frames, `26a1f298...` and `554f351e...`, which the md5-equal restore
carries. The other three have nothing to carry, which the preview says: `no screenshot: the server
links none to this state`. `ares` states are a fixed 212,543 B, so only the md5 tells two apart.

**`ares` did not close when asked.** The agent's launch sent `WM_CLOSE` and ares was still
running 15 seconds later, so it was killed. Its `.ram` was unchanged by that launch, and the state
it wrote was on disk before the close.

## The three `kega-fusion` rows

**Driven, and not certified: step 4 cannot pass on RetroBat 8.2.1.** The other eight steps pass or
carry.

|              | `kega-fusion`/`auto`                               | `kega-fusion`/`genesis`             | `kega-fusion`/`megadrive`             |
| ------------ | -------------------------------------------------- | ----------------------------------- | ------------------------------------- |
| Selected by  | `megadrive.emulator = kega-fusion`, `.core = auto` | the install launch, `-core genesis` | the agent's launch, `-core megadrive` |
| Confirmed by | `Fusion.exe -auto`                                 | `Fusion.exe -gen`                   | `Fusion.exe -md`                      |

**Kega Fusion was not on the install at the start.** RetroBat ships only its template
`Fusion.ini` into `emulators/kega-fusion/`; the maintainer installed the emulator through ES,
which downloads it on the first launch of a game under it. **The three cores are Kega's command
line flags** for the console's region handling, and all three write to the same places.

| #   | All three rows                                                                                             |
| --- | ---------------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, carried                                                                                          |
| 4   | **Fail on 8.2.1.** A real save was made and lands in `emulators/kega-fusion/`, which RomMBat does not scan |
| 5   | **Pass**, two slots round-tripped at their own md5                                                         |
| 6   | **N/A**                                                                                                    |
| 7   | **Pass** on launch and art. The pad works only after a remap in Kega's own menu, finding 284               |
| 8   | **Pass** for `auto`, 00:33:59Z (44s) and 01:06:41Z (48s). Carried to the other two                         |
| 9   | **Pass.** 0 downloaded, 0 written, gamelist identical                                                      |

### 4. Where Kega Fusion puts a battery save

**`emulators/kega-fusion/<rom>.srm`.** The install launch wrote `Sonic & Knuckles + Sonic The
Hedgehog 3 (USA) (Lock-on Combination).srm` there, 980 B. RetroBat's `Fusion.ini` template sets
`SRMFiles=.\..\..\emulators\kega-fusion` while `StateFiles=.\..\..\saves\megadrive\kega-fusion`,
and `emulatorLauncher` rewrote neither on any of five launches. RomMBat reads saves from `saves/`
and nowhere else, and core principle 2 rules out writing the key itself. **Recorded as a RetroBat
defect, to be reported upstream, by the maintainer's ruling**, rather than widened around.
Finding 283.

**A real save was made there, and it is the libretro cores' format.** After remapping input in
Kega's own menu, the maintainer launched from ES under `-auto` at 01:06:41Z, chose a data-select
slot, and Kega rewrote the file at 980 B, `9f80af5c...`. It matches Genesis Plus GX's file in
layout and length and differs from it in 6 bytes, the slot chosen. So with `SRMFiles` pointed at
`saves\megadrive`, Kega would read and write the same loose `.srm` the three `libretro` cores
share, and its rows would be the libretro family's step 4. That is what the upstream report asks
for. Until then step 4 fails on the location alone, not on the save.

**The agent's own keyboard attempts never reached data select**, under `-auto` and `-gen`: a
blind Start from the title went straight into the game. The maintainer's session did reach it, so
that was the agent's input timing, not Kega's handling of the cartridge.

### 5. Kega Fusion's states

**`saves/megadrive/kega-fusion/<rom>.gs<slot>`**, where `Fusion.ini` sends them. `F5` saves and
`F7` steps the slot down, wrapping from 0 to 9:

| Slot | md5           | Size      |
| ---- | ------------- | --------- |
| 0    | `91ada93f...` | 140,408 B |
| 9    | `0d173d84...` | 140,408 B |

`es_savestates.cfg` declares nothing for `kega-fusion`, so the third deploy adds a supplement
entry for it, scoped to `megadrive`. Its first flush sent both states, and both came back through
`saves restore --apply` at their own md5, exit 0. No image is written. `Fusion.ini` also names
state folders for `mastersystem` and `segacd`; neither was driven, and neither is declared.

### 7. Playable by pad only after a remap in Kega's own menu

**The maintainer's Xbox 360 pad did nothing in Kega Fusion as `emulatorLauncher` configured it.**
It logged `No specific mapping found for megadrive controller` and wrote `Joystick1Using=3` for
player 1 while the pad is device 0, and RetroBat ships no pad-to-key file for `kega-fusion`, so no
pad button saves a state. The keyboard mapping it wrote does reach the game: numpad 8, 2, 4 and 6
for the pad, `J`, `K` and `L` for A, B and C, and Right Ctrl for Start.

**Remapping inside Kega made it playable.** The maintainer set the controls in Kega's own menu,
which Kega saved into `Fusion.ini` on exit as `Joystick1Using=2` and arrow keys for
`Player1Keys`, and then played the save above. Whether `emulatorLauncher` keeps that remap on the
next launch or writes its own mapping back over it was not measured. Finding 284.

## What the pass turned up that is not a row

### Another client's `null` save, refused (finding 276)

**RomMBat downloaded four bytes reading `null` and wrote them as a battery save.** RomM's browser
player uploads the JSON literal when it has no save to send, and the server keeps it as a save.
This install held one at `saves/megadrive/Bare Knuckle III (Japan) [T-En by Twilight Translations
v1.0].srm`, md5 `37a6259cc0c1dae299a7866489dff0bd`, written at 18:10 local by the build before this
branch; earlier ones had landed on `nes` as Ninja Gaiden II and Super Mario Bros. It was moved out of
the tree to `R:\rommbat-evidence\megadrive\` rather than deleted.

**The fix refuses such a save wherever a download is placed**: before the transfer when the server
names that hash, and on the bytes when it does not. It is never acknowledged, it is counted as
`refused, not a save` rather than failed, and it does not move the exit code, because the server
offers it again on every flush and nothing on the device can change that. The restore preview
lists it among the rows it cannot place, with the reason.

**Both paths were driven on the live server.** `saves restore 173367`, Old Towers, lists its
slotless row as the four bytes `null`. Once the Bare Knuckle file was out of the tree, a flush
was offered rom 189465's `libretro:battery` row and answered `1 refused, not a save`, exit 0.
Rom 189465 holds **three** such rows: slotless, `autosave` (save 94, dated 2026-08-01, which the
old build negotiated at 22:10:14Z) and `libretro:battery`. This device's store holds no record of
uploading the third, so another client wrote it too, and which one is not determined here. All
four rows are left on the server until the PR merges.

### `--help` ran the command

`rommbat-agent saves restore --help` ran a full restore preview. Any `--help` or `-h` now prints
the usage and runs nothing, on every subcommand, which matters most where `--apply` is on the
same line.

## What this file will not claim

- **Nothing about `megadrive` under any build but this one.** Every row was measured on RetroBat
  8.2.1 and RomM `5.3.0`.
- **Nothing about another game.** Sonic 3 & Knuckles is one lock-on cartridge; the sizes above are
  its SRAM window's, and another game's differ.
- **Nothing about `mednafen` on a `.bin`, `.gen` or `.smd`.** `MednafenRomHash` answers only for a
  plain `.md`, the one format this library holds, and answers null for the rest.
- **Nothing about FBNeo on a library named for its dat.** The rename test booted one game and
  drove nothing past the title.
