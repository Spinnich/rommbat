---
summary: The certification record for `nes`: the two `bizhawk` rows.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: `bizhawk`/`NesHawk` and `bizhawk`/`quickerNES`

**Both certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same install
and server as [the re-drive](index.md#the-install-this-was-measured-on). The client was a deploy of `main` at ca2cc4f, the merge of #214, made by
`tools/publish.ps1 -Deploy R:\RetroBat`, so it carries #151's BizHawk battery saves. The
maintainer played over RDP. The second state on each row was made from the agent's session on the
RetroBat machine, which is described under step 5.

|              | `bizhawk`/`NesHawk`                                                 | `bizhawk`/`quickerNES`                                                 |
| ------------ | ------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Selected by  | `nes.emulator = bizhawk`, `nes.core = NesHawk` in `es_settings.cfg` | `nes.emulator = bizhawk`, `nes.core = quickerNES` in `es_settings.cfg` |
| Confirmed by | `-emulator bizhawk -core NesHawk`, ran `EmuHawk.exe`                | `-emulator bizhawk -core quickerNES`, ran `EmuHawk.exe`                |
| Game         | Destiny of an Emperor (USA), rom 158207, no pin                     | The Legend of Zelda (USA) (Rev 1), rom 158633, **its pin removed**     |
| Save made by | The in-game "Record" command                                        | Registering a second name on the file-select screen                    |

**Zelda carries a `bizhawk`/`NesHawk` pin in `gamelist.xml`**, and
[a pin overrides the system setting](index.md#the-install-this-was-measured-on). It was removed for the `quickerNES` session with ES
closed and put back afterwards. The install was left on `libretro`/`nestopia`, where it was found.

**Destiny of an Emperor was picked because its BizHawk title is unique on this install.** BizHawk
names a battery save after its own title for the game (RB-263), and a title two ROMs answer
to is contested rather than synced (RB-262). Zelda's is unique too.

## Checklist for both `bizhawk` rows

Steps 1, 2 and 3 are the system's and carry from `nestopia`'s re-drive. Step 6 is N/A for the
same reason.

| #   | `bizhawk`/`NesHawk`                                                        | `bizhawk`/`quickerNES`                                                 |
| --- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| 1   | **Pass**, carried: `fs_slug`, the same folder                              | **Pass**, carried                                                      |
| 2   | **Pass**, carried: 228 of 228, nothing excluded. `.zip` observed to launch | **Pass**, carried. `.zip` observed to launch                           |
| 3   | **Pass**, carried: RetroBat requires no BIOS for `nes`                     | **Pass**, carried                                                      |
| 4   | **Pass, both directions.** `bizhawk:battery`, equal md5 up and down        | **Pass, both directions.** The file `NesHawk` wrote, read and extended |
| 5   | **Pass**, two slots, screenshot inside the state. See below                | **Pass**, two slots, screenshot inside the state                       |
| 6   | **N/A**, no class D                                                        | **N/A**                                                                |
| 7   | **Pass.** Box art on screen                                                | **Pass.** Box art on screen                                            |
| 8   | **Pass.** 17:19:29Z to 17:21:14Z, 1m 44s, rom 158207                       | **Pass.** 17:37:16Z to 17:38:02Z, 46s, rom 158633                      |
| 9   | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical           | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical       |

**A baseline was taken before either session**: `sync` a no-op with `gamelist.xml` unchanged, and
`flush` with nothing queued.

## 4. Battery save on both `bizhawk` rows

|                       | `NesHawk`                                                  | `quickerNES`                                              |
| --------------------- | ---------------------------------------------------------- | --------------------------------------------------------- |
| On disk               | `saves/nes/bizhawk/Destiny of an Emperor.SaveRAM`, 8,192 B | `saves/nes/bizhawk/Legend of Zelda, The.SaveRAM`, 8,192 B |
| Before the session    | absent                                                     | `d77ad9d3...`, holding `LINK` from `NesHawk`              |
| Bound by              | the name sidecar beside the state                          | a launch covering when the save was written               |
| Uploaded              | `bizhawk:battery`, by the `quit` pass                      | `bizhawk:battery`, by the `quit` pass                     |
| At the restore        | `0b7af58a...`                                              | `2bce4ff7...`, holding `LINK` and `TEST`                  |
| Moved aside, restored | `0b7af58a...`, **equal**                                   | `2bce4ff7...`, **equal**, both names intact               |

**The two cores read each other's file.** `quickerNES` showed `NesHawk`'s `LINK` on Zelda's file-select screen, and the name
registered there went into the next file slot of the same `.SaveRAM`. RB-271.

**The hash at the restore is not the hash the session left**, and the difference is the game's.
The second state on each row came from a later launch that sat on the title screen, and both
games rewrote their save on that boot: Destiny changed 4 bytes at `0x400` to `0x403` with its
saved game intact, and Zelda changed with both names intact. Each rewrite went up as a new
`bizhawk:battery` version, which is right, since the bytes changed. RB-272. The restore
named the newest of 3 server saves on each row and listed the two it did not restore.

A `flush` after each restore reported every restored file `in step` and sent nothing.

## 5. State and screenshot on both `bizhawk` rows

| Row          | Slot | Made by                                         | State md5     | Framebuffer md5 |
| ------------ | ---- | ----------------------------------------------- | ------------- | --------------- |
| `NesHawk`    | 0    | the maintainer, pad key, in a town dialogue     | `0dca16bb...` | `6e27d64b...`   |
| `NesHawk`    | 2    | the agent's session, `Ctrl+F2`, the intro crawl | `ab6c3848...` | `bdeafc1e...`   |
| `quickerNES` | 5    | the maintainer, pad key                         | `97aad3c2...` | `45c1fb40...`   |
| `quickerNES` | 2    | the agent's session, `Ctrl+F2`, the title       | `b6fd46fb...` | `61c5052e...`   |

**BizHawk writes no screenshot file, so the screenshot half of step 5 is the state's own bytes.**
`es_savestates.cfg` declares `{{romfilename}}.QuickSave{{slot0}}.png`, and none of the four
states has one, nor do the two older ones on this install. The frame is inside the `.State`, which is
a zip holding `Framebuffer.bmp` beside the core state. So the step was checked by opening each
state's framebuffer, confirming the two on each row show different screens, and requiring the
restored state to match by md5, which carries the framebuffer with it. RomM holds no screenshot
for a BizHawk state, and the restore preview says `no screenshot: the server links none to this
state`, which is correct. RB-268.

**The declared `<directory>` is where both cores' states land, and it is a mirror.** EmuHawk
writes `emulators/bizhawk/sstates/nes/<title>.<core>.QuickSave<n>.State`, and `emulatorLauncher`
copies it to `saves/nes/bizhawk/sstates/<core>/<rom>.QuickSave<n>.State` in the same second, with
a `.txt` sidecar holding `<title>.<core>`. The copy is `emulatorLauncher`'s, not EmuHawk's: a
state made in an EmuHawk opened directly never reached `saves/`, and the next launch through the
launcher removed it from the native directory. RB-270. Each uploaded name carries its core,
`[bizhawk.NesHawk]` and `[bizhawk.quickerNES]`.

On each row both states and the `.SaveRAM` were moved out of the tree. The preview named both
states at their own slots, `QuickSave0` and `QuickSave2` on `NesHawk`, `QuickSave5` and
`QuickSave2` on `quickerNES`, and `--apply` answered `restored 1 save(s) and 2 state(s), failed
0`, exit 0. **Every file came back at its own md5.** This is the first restore of a `bizhawk` state
since RB-258 changed how a restore reads the slot out of an uploaded name, and the first on
any emulator that keeps the slot in the stem.

**The slot is ES's, the reverse of `libretro`.** The `quickerNES` launch carried `-state_slot 5`
and `emulatorLauncher` set EmuHawk's current slot to 5, so the pad's save key wrote `QuickSave5`;
the `NesHawk` launch carried none and the key wrote `QuickSave0`. The pad has no way to pick a slot
in game. `Ctrl+F1` to `Ctrl+F10` on a keyboard save to a slot outright, and over RDP those did not
reach EmuHawk, so the second state on each row was made from the agent's session on the RetroBat
machine: `emulatorLauncher` started with the row's `-system`, `-emulator`, `-core` and `-rom`, and
`Ctrl+F2` sent by `keybd_event` with hardware scan codes, since EmuHawk reads the keyboard through
DirectInput and ignores `SendKeys`. RB-269. Those two launches skip ES, so the hooks did not
run, and a `flush` by hand sent each state.

## 7. Launch on both `bizhawk` rows

Box art confirmed on screen in the NES game list by the maintainer, for both games.

## 8. Play session on both `bizhawk` rows

From `status`:

```text
Playtime
  server holds:    32 sessions for romm device cf1cc550-5203-4697-94b5-36757ac9a334
  last session:    2026-09-21 17:19:29Z to 2026-09-21 17:21:14Z, 1m 44s
  its rom:         158207
```

and after the `quickerNES` session, `34 sessions`, `17:37:16Z to 17:38:02Z, 46s`, rom 158633. The
33rd is a second, short `NesHawk` session on Destiny in which the keyboard slot keys were tried
over RDP. Every `start` and `quit` pass in `background.log` exited 0, and the battery save and the
first state on each row went up through the `quit` pass before anything was run by hand.

## 9. Re-sync on both `bizhawk` rows

After the `NesHawk` restore, `sync` answered `nothing to do: all 228 games are already present and
verified`, `0 downloaded, 0 written`, `889 already present`, `all 1 unchanged`, exit 0, with
`gamelist.xml` md5'd either side and identical, and a flush after it sent nothing.

**After `quickerNES` it answered 227 and 885, and the missing game is the one `NesHawk` was driven
on.** Destiny of an Emperor (USA) left the set, `departed` at 17:32:32Z. It is not a RomMBat
result. The set is smart collection 9, whose filter is the NES platform and favourite, and a
probe set asking the server for favourites titled "Destiny of an Emperor" now answers only
Destiny of an Emperor II. RomMBat made no write to the server between the `NesHawk` step-9 sync,
which still resolved 228, and 17:32:32Z: one flush that sent nothing, then the restore's reads and
downloads. Nothing in `RomM.Client` writes a collection either. Its one user-side write is
`now_playing: false`, and `RomUserData` has no favourite field. The ROM had been a member since
the set's first sync, 2026-09-12 18:03Z, origin `synced`, and the maintainer does not recall
favouriting it. How it came to be a favourite, and why it stopped, is unexplained.

**Step 9 still passes on `quickerNES`**, because the step is about the re-sync: `0 downloaded, 0
written`, `all 1 unchanged`, `gamelist.xml` identical either side. The departed game kept its
file, its media and its `gamelist.xml` entry, since `RemoveDeparted` drops only entries whose file
has gone, and it is now an eviction candidate rather than a deletion.

## BizHawk's battery save across ROMs, driven (#151)

Driven from EmulationStation on 2026-09-21 against the build carrying #151, with the maintainer
at the controller. It is the hands-on pass that change owed and re-runs no checklist step.
RB-262 to RB-266.

| Pass                                               | What happened                                                                                                                                                                                                      |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| The two saves already on disk, one per core        | Both attributed through their sidecars (`StarTropics.NesHawk`, and Ultima's under `quickerNES`), uploaded, and a second flush a no-op                                                                              |
| Restore, then play                                 | `StarTropics.SaveRAM` restored under its title with the original hash, and **Continue in BizHawk showed the saved progress**. BizHawk's rewrite on exit went up under the USA ROM                                  |
| Restore while another `nes` game runs              | Ultima's restore deferred with the display-name reason while StarTropics ran, and landed with its original hash once it quit                                                                                       |
| No learned title                                   | With the binding forgotten, the restore refused with "Run the game once under bizhawk" and wrote nothing                                                                                                           |
| No save state, `NesHawk`                           | Zelda wrote `Legend of Zelda, The.SaveRAM`; the launch route alone bound it and it went up                                                                                                                         |
| In-game save, `quickerNES`                         | Ultima's changed bytes went up under the same slot and ROM, then a no-op                                                                                                                                           |
| Europe copy of a game already saved in the USA one | **One file for both**: the Europe copy read the USA progress and wrote a new character into `StarTropics.SaveRAM`. Contested, nothing uploaded; `saves bind` settled it to USA and a restore put the USA save back |

The pass turned up three defects, and each has a test: a slot this device had already sent
restored to the ROM's stem rather than the title (RB-266), a restored file was credited to
whichever BizHawk session came last and contested (RB-265), and BizHawk's `.SaveRAM.bak` was
reported as an unknown shape (RB-264).
