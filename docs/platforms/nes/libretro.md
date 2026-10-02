---
summary: The certification record for `nes`: the two other `libretro` rows.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: `libretro`/`fceumm` and `libretro`/`mesen`

**Both certified on 2026-09-21**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1, on the same install
and server as [the install `nestopia` was certified on](index.md#the-install-this-was-measured-on). The client was a deploy of `main` at a901af3, the merge of #210,
made by `tools/publish.ps1 -Deploy R:\RetroBat`. So `status`'s `Playtime` block settled step 8,
not a second token. The maintainer played; everything after the quit was checked from the agent,
the store and the server.

|              | `libretro`/`fceumm`                                                     | `libretro`/`mesen`                                                 |
| ------------ | ----------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Selected by  | **Nothing: RetroBat's default.** `nes.emulator` and `nes.core` removed  | `nes.emulator = libretro`, `nes.core = mesen` in `es_settings.cfg` |
| Confirmed by | `-emulator libretro -core fceumm`, ran `fceumm_libretro.dll`            | `-emulator libretro -core mesen`, ran `mesen_libretro.dll`         |
| Game         | Final Fantasy III (Japan) [T-En by Chaos Rush v1.3], rom 190006, no pin | The Legend of Zelda (USA) (Rev 1), rom 158633, no pin              |
| Save made by | Saving from the world-map menu                                          | Registering a name on the file-select screen                       |

**`fceumm` is the row a stock install runs**, because with both keys absent ES falls through to
the first emulator and core `es_systems.cfg` lists for `nes`, `libretro` then `fceumm`. It is the
row most users will actually have. The three unrelated `nes.*` keys recorded for `nestopia` were
still set and do not reach this core.

**Neither game carries a per-game `<emulator>` in `gamelist.xml`**, which is checked because
[eight games on this install do](index.md#the-install-this-was-measured-on): StarTropics and
Ultima, the first two picks, are pinned to `bizhawk` and would have run the wrong row. A pinned game
overrides the system setting without a trace in `es_settings.cfg`, so read the launch line.

**For a faster pass, pick a game whose battery save is quick to make.** Zelda writes one the
moment a name is registered, where Final Fantasy III needed play to the world map.

## Checklist for both

Steps 1, 2 and 3 are the system's and carry from [`nestopia`'s record](libretro-nestopia.md#checklist). Step 6 is N/A
for the same reason.

| #   | `libretro`/`fceumm`                                                                    | `libretro`/`mesen`                                               |
| --- | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 1   | **Pass**, carried: `fs_slug`, the same folder                                          | **Pass**, carried                                                |
| 2   | **Pass**, carried: 228 of 228, nothing excluded. `.zip` observed to launch on this row | **Pass**, carried. `.zip` observed to launch on this row         |
| 3   | **Pass**, carried: RetroBat requires no BIOS for `nes`                                 | **Pass**, carried                                                |
| 4   | **Pass, both directions.** Class A, new `.srm`, equal md5 up and down                  | **Pass, both directions.** Class A, the shared `.srm`, equal md5 |
| 5   | **Pass**, screenshot byte-checked                                                      | **Pass**, screenshot byte-checked                                |
| 6   | **N/A**, no class D                                                                    | **N/A**                                                          |
| 7   | **Pass.** Box art on screen, all five media kinds and `<desc>` in the entry            | **Pass.** Box art on screen                                      |
| 8   | **Pass.** 10:28:21Z to 10:36:59Z, 8m 37s, rom 190006                                   | **Pass.** 10:52:22Z to 10:53:36Z, 1m 14s, rom 158633             |
| 9   | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                       | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical |

**A baseline was taken before either session**: `sync` a no-op with `gamelist.xml` unchanged, and
`flush` with nothing queued. So everything below is the session's and not left over.

## 4. Battery save on both

|                      | `fceumm`                                                 | `mesen`                                                     |
| -------------------- | -------------------------------------------------------- | ----------------------------------------------------------- |
| On disk              | `saves/nes/Final Fantasy III (Japan) [...].srm`, 8,192 B | `saves/nes/Legend of Zelda, The (USA) (Rev 1).srm`, 8,192 B |
| Before the session   | absent, and nothing on the server for the ROM            | `620bd047...`, `nestopia`'s save from 2026-09-20            |
| After it             | `63c189d2...`                                            | `9136743b...`                                               |
| Uploaded as          | save **348**, `libretro:battery`, by the `quit` pass     | save **349**, `libretro:battery`, by the `quit` pass        |
| Deleted and restored | `63c189d2...`, **equal**                                 | `9136743b...`, **equal**                                    |

**Zelda's was checked as content**: the name `SPINNICH` sits at offset 2 in Zelda's own character
encoding and the earlier `LINK` is gone, which is what the maintainer entered. It is the same file
`nestopia` wrote, because the three `libretro` cores share `saves/nes/<rom>.srm`, so this pass
also shows a second core writing into a slot the first one owns: it went up as a new version of
`libretro:battery` with no conflict. The restore named it the newest of 9 server saves and listed
the 8 it did not restore.

**Neither upload needed a terminal.** Both arrived through the detached `quit` pass, which exited 0
each time, before anything was run by hand. A `flush` after each restore reported the restored
files `already in step` and sent nothing.

## 5. State and screenshot on both

Two states each, made on different screens so each has its own image.

| Row      | Slot | On the server | State md5     | Screenshot md5 |
| -------- | ---- | ------------- | ------------- | -------------- |
| `fceumm` | 1    | state **191** | `b46171f3...` | `96f8daa0...`  |
| `fceumm` | 2    | state **192** | `5f97a87b...` | `a55104f5...`  |
| `mesen`  | 1    | state **193** | `3b6d3ec2...` | `758a2549...`  |
| `mesen`  | 2    | state **194** | `b13f8c28...` | `9f4a41bd...`  |

**The declared `<directory>` is where both cores wrote**: `saves/nes/libretro.fceumm/` and
`saves/nes/libretro.mesen/`, per `{{system}}/libretro.{{core}}`, with `{{romfilename}}.state{{slot}}`
and its `.png` beside it. Each uploaded name carries its own core, `[libretro.fceumm]` and
`[libretro.mesen]`, so `mesen`'s Zelda states sit on the server beside `nestopia`'s without
touching them.

On each row, slot 2's state and `.png` were deleted with the `.srm`. The preview named the
screenshot it would bring back, and `--apply` answered `restored 1 save(s) and 1 state(s), failed
0, ... with 1 screenshot(s)`, exit 0. **Every file came back at its own md5**, and slot 2's image
is not slot 1's on either row, so the link is to that state and not to a neighbour.

**`-state_slot` did not pick the slot**, and that corrected the `nestopia` record. `mesen` was
launched with `-state_slot 5` and wrote slots 1 and 2, and RetroArch's log shows it choosing:
`found_last_state_slot: #0` against the empty `libretro.mesen/`. `fceumm`'s launch carried no
`-state_slot` at all. RB-261.

## 7. Launch on both

Box art for both games was confirmed on screen in the NES game list by the maintainer. Final
Fantasy III's entry carries `<image>`, `<thumbnail>`, `<marquee>`, `<video>` and `<manual>`, each
pointing at a file that exists, and `<desc>`.

## 8. Play session on both

Read from `status` rather than inferred from the journal:

```text
Playtime
  server holds:    24 sessions for romm device cf1cc550-5203-4697-94b5-36757ac9a334
  last session:    2026-09-21 10:28:21Z to 2026-09-21 10:36:59Z, 8m 37s
  its rom:         190006
```

and after the `mesen` session, `25 sessions`, `10:52:22Z to 10:53:36Z, 1m 14s`, rom 158633. Both
starts match the launch lines in `emulatorLauncher.log`, which logs local time four hours behind.
Each `start` and `quit` pass in `background.log` exited 0.

## 9. Re-sync on both

After each session, `sync` answered `nothing to do: all 228 games are already present and
verified`, `0 downloaded, 0 written`, `889 already present` and `all 1 unchanged`, exit 0, with
`gamelist.xml` md5'd either side and identical. Across each session it changed, and as on
2026-09-20 the writer was EmulationStation updating `<playcount>` and `<lastplayed>`.

The install was left on `libretro`/`nestopia` afterwards, which is where it was found.
