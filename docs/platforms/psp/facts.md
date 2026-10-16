---
summary: How each emulator RetroBat offers for `psp` behaves, measured, with RB- IDs.
read-when: Before certifying a `psp` row or changing how RomMBat handles a `psp` save, state or firmware.
---

# psp: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-36. Neither of PPSSPP's two state copies is stale

Verified: RetroBat 8.3.0, 2026-10-15. How: saved a state in Bust-A-Move Deluxe under PPSSPP 1.20.4 on the agent tree's 8.3.0 install with `F2`, and read both copies' creation and write times.
RetroBat mirrors the native state into the ES-facing directory live, a few milliseconds after PPSSPP finishes writing it, and the two copies are byte-identical. The declared template is correct and `saves/psp/ppsspp/` is authoritative (RB-366).

## RB-37. A downloaded PPSSPP state reaches the emulator through the ES-facing path

Verified: RetroBat 8.3.0, 2026-10-15. How: deleted `PPSSPP_STATE/` and launched Bust-A-Move Deluxe with `-state_slot 0 -state_file` naming the ES-facing state, then read `emulatorLauncher.log` and the native copy.
ES passes `-state_slot` and `-state_file` naming the **ES-facing** path, and the launcher hands it to PPSSPP as `--state=`. Writing there is sufficient; the native copy is rebuilt from it.

## RB-154. Confirmed on a second, independently produced sample

Verified: RetroBat 8.2.0, 2026-08-17. Carried: RetroBat 8.3.0, 2026-10-14, the directory name is the game's own PSP save format, which an emulator build passes through. How: recovered from the findings ledger, which recorded it during M6 stage 2b's hands-on pass (74e9529) on the 8.2.0 install its header names; the method is the text below.
The claim being checked: The PSP unit key is a prefix of the directory name (141, measured on `E:`)

The pass says: **Confirmed on a second, independently produced sample.** PPSSPP wrote `SAVEDATA/ULUS100570000/`, four files and 91,607 B, and the key extracted as `ULUS10057`. Matching the whole segment would have found nothing

## RB-155. True, and it works

Verified: RetroBat 8.2.0, 2026-08-17. Carried: RetroBat 8.3.0, 2026-10-14, it measures RomMBat's launch-window attribution, keyed on launch-log lines RB-118 carries. How: recovered from the findings ledger, which recorded it during M6 stage 2b's hands-on pass (74e9529) on the 8.2.0 install its header names; the method is the text below.
The claim being checked: Route 1 is the only route that can attribute a PSP save (143, 144, 145)

The pass says: **True, and it works.** No `.cso` header and no state sidecar existed, and the launch window bound it: `Bust-A-Move - Deluxe (USA).cso was running when ULUS10057 was last written`. The hook fired all four events and the launch log carried `-system psp -emulator ppsspp`

## RB-159. PPSSPP loads a save unit RomMBat restored

Verified: RetroBat 8.3.0, 2026-10-16. How: saved in Bust-A-Move Deluxe under PPSSPP 1.20.4, took the server's copy with `saves resolve --keep-server`, then loaded it in the game.
The restore put the server's four files into `SAVEDATA/ULUS100570000/`, and the game listed the unit at 89 KB and reported `Load completed.` With the fold proving the bytes identical, a real save round-trips the same way.

## RB-304. `libretro`/`ppsspp` keeps its memory stick in the standalone's, so both rows write one container

Verified: RetroBat 8.3.0, 2026-10-16. How: saved in Bust-A-Move Deluxe under PPSSPP 1.20.4 and then under `libretro`/`ppsspp` from ES, and read `es_launch_stdout.log`, `retroarch.cfg` and `saves/psp/`.
The core's `ms0:/PSP/SAVEDATA/ULUS100570000` is `saves/psp/SAVEDATA/ULUS100570000`: it offered to overwrite the standalone's save, and its write landed in that unit (`[SCEUTIL] Saving file with size 156 in ms0:/PSP/SAVEDATA/ULUS100570000/GAME.DAT`), because RetroArch's `savefile_directory` is `saves/psp`. So both rows write one container and one unit. Its save states go to `saves/psp/libretro.ppsspp/`, RetroArch's `savestate_directory`.

## RB-305. The class C unit's hash is the server's on every transfer, end to end

Verified: RomM 5.3.0, 2026-09-23. Carried: RomM 5.3.1, 2026-10-14, the 5.3.0-to-5.3.1 source diff (225 files, from the trees) touches nothing under backend/endpoints/saves and no backend line naming `content_hash`. How: recovered from the findings ledger, which recorded it while driving `psp`'s directory save (d88eb42) against RomM 5.3.0; the method is the text below.
The `quit` hook's pass uploaded the unit as save 437 with `content_hash` `3f2685c3...`, the local fold and a hand computation of RomM's rule alike; three folders later went up as 439 at `1e1e98ae...`, the same three ways. A peer row staged with no device, 440 holding `001` and `003`, came down at `ab160d10...`, passed the check against the offered hash after extraction, and swapped in with `002` moved to `replaced/`. The next flush was 0 up, 0 down.

## RB-306. `libretro`/`ppsspp` writes an in-game save once, with rewind off at RetroBat's default

Verified: RetroBat 8.3.0, 2026-10-16. How: saved in Bust-A-Move Deluxe under `libretro`/`ppsspp` from ES, and read `retroarch.cfg`, `es_launch_stdout.log` and `saves/psp/SAVEDATA/`.
RetroBat leaves `psp`'s rewind off (`rewind_enable = "false"`). The save wrote `GAME.DAT` once, the log has no `[Rewind]` line, the game did not stall, and the unit it left is whole: four files, 91,607 B. A save with rewind turned on for `psp` is unmeasured.

## RB-307. RomM's `save_target` for a rehashed PSP game is the unit key, layout `folder-prefix`

Verified: RomM 5.3.0, 2026-09-23. Carried: RomM 5.3.1, 2026-10-14, the 5.3.0-to-5.3.1 source diff (225 files, from the trees) changes no backend line naming `save_target`, `save_target_layout` or `title_id`. How: recovered from the findings ledger, which recorded it while driving `psp`'s directory save (d88eb42) against RomM 5.3.0; the method is the text below.
`title_id` and `save_target` `ULUS10202`, layout `folder-prefix`: the key and match rule `save_shapes.json` declares for `psp`, and the binding the launch journal taught. Portable Ops Plus (USA), not rehashed, carries none.

## RB-366. PPSSPP writes states to two places, and RetroBat mirrors between them (resolved)

Verified: RetroBat 8.3.0, 2026-10-15. How: saved a state in Bust-A-Move Deluxe under PPSSPP 1.20.4 on the agent tree's 8.3.0 install with `F2`, read every file's creation and write time, then deleted the native copies and relaunched with `-state_file`.
The same PSP state appears under two naming schemes at once:

```text
saves/psp/ppsspp/Bust-A-Move - Deluxe (USA)_0.ppst   <- matches es_savestates.cfg
saves/psp/PPSSPP_STATE/ULUS10057_1.00_0.ppst         <- PPSSPP's native location
```

**Both are real, and neither is stale: RetroBat keeps them in sync.** The launcher sets
`saves/psp` as PPSSPP's content path, so PPSSPP writes natively to
`PPSSPP_STATE/<GAMEID>_<version>_<slot>.ppst`, and the mirror follows live:

| Time         | File                                                                         |
| ------------ | ---------------------------------------------------------------------------- |
| 15:21:27.209 | the native `.ppst` finishes writing                                          |
| 15:21:27.212 | the ES-facing `.ppst` is created, byte-identical and with the native's mtime |
| 15:21:27.215 | `ppsspp/<rom filename>.txt`                                                  |
| 15:21:27.216 | `PPSSPP_STATE/<GAMEID>_1.00_0.jpg`                                           |

So the mirror is made **a few milliseconds after the save, while the emulator is still
running**. After exit no file had changed, so there is no exit-time pass to wait for.

**The `.txt` is a name-mapping sidecar.** It holds exactly the native basename:

```text
saves/psp/ppsspp/Bust-A-Move - Deluxe (USA).txt   ->   ULUS10057_1.00
```

That is how RetroBat translates between the ROM-filename scheme and the game-ID scheme. It
is not a save, but it is **not disposable either**: treat it as part of the state.

**The ES-facing directory is the authoritative one, and it is the one RomMBat must use.**
ES passes the launcher `-state_slot N -state_file <ES-facing path>`, and the launcher then
hands the emulator the ES-facing path directly:

```text
PPSSPPWindows64.exe -fullscreen "<rom>" --state="...\saves\psp\ppsspp\Bust-A-Move - Deluxe (USA)_0.ppst"
```

Deleting every native file and relaunching that way **recreated the native copy byte for
byte** from the ES-facing one. So a state that RomMBat downloads and writes into
`saves/psp/ppsspp/` does reach the emulator, which is exactly the case that mattered. Note
the sync-in copies **only the `.ppst`**; the native `.jpg` was not recreated.
