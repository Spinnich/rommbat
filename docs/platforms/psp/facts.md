---
summary: How each emulator RetroBat offers for `psp` behaves, measured, with RB- IDs.
read-when: Before certifying a `psp` row or changing how RomMBat handles a `psp` save, state or firmware.
---

# psp: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-36. Neither is stale

Plan says: PPSSPP's two populated state directories mean the declared template is wrong (probe 2, earlier)

Measurement says: **Neither is stale.** RetroBat mirrors native to ES-facing about 120 ms after each save, live. The declared template is correct and `saves/psp/ppsspp/` is authoritative

## RB-37. A downloaded PPSSPP state reaches the emulator through the ES-facing path

ES passes `-state_slot` and `-state_file` naming the **ES-facing** path, and the launcher hands it to PPSSPP as `--state=`. Writing there is sufficient; the native copy is rebuilt from it

## RB-154. Confirmed on a second, independently produced sample

The claim being checked: The PSP unit key is a prefix of the directory name (141, measured on `E:`)

The pass says: **Confirmed on a second, independently produced sample.** PPSSPP wrote `SAVEDATA/ULUS100570000/`, four files and 91,607 B, and the key extracted as `ULUS10057`. Matching the whole segment would have found nothing

## RB-155. True, and it works

The claim being checked: Route 1 is the only route that can attribute a PSP save (143, 144, 145)

The pass says: **True, and it works.** No `.cso` header and no state sidecar existed, and the launch window bound it: `Bust-A-Move - Deluxe (USA).cso was running when ULUS10057 was last written`. The hook fired all four events and the launch log carried `-system psp -emulator ppsspp`

## RB-159. PPSSPP loads a save unit RomMBat restored

The staged restore (not atomic, #38) put the server's four files into `SAVEDATA/ULUS100570000/` and Bust-A-Move loaded the save. With the fold proving the bytes identical, a real save round-trips the same way

## RB-304. Where `libretro`/`ppsspp` keeps its memory stick on RetroBat

Question: Where `libretro`/`ppsspp` keeps its memory stick on RetroBat

Measured: **In the standalone's.** The core's `ms0:/PSP/SAVEDATA/ULUS10202003` is `saves/psp/SAVEDATA/ULUS10202003`, beside the standalone's `ULUS10202001`, so both rows write one container and one unit. The game offered three in-game slots as `ULUS10202001` to `003`, one folder each, all under the `ULUS10202` prefix. Its save states go to `saves/psp/libretro.ppsspp/`.

## RB-305. Whether the class C unit's hash is the server's, end to end

Question: Whether the class C unit's hash is the server's, end to end

Measured: **Yes, on every transfer.** The `quit` hook's pass uploaded the unit as save 437 with `content_hash` `3f2685c3...`, the local fold and a hand computation of RomM's rule alike; three folders later went up as 439 at `1e1e98ae...`, the same three ways. A peer row staged with no device, 440 holding `001` and `003`, came down at `ab160d10...`, passed the check against the offered hash after extraction, and swapped in with `002` moved to `replaced/`. The next flush was 0 up, 0 down.

## RB-306. Whether `libretro`/`ppsspp` saves this game cleanly

Question: Whether `libretro`/`ppsspp` saves this game cleanly

Measured: **No.** Each in-game save froze the game, and `es_launch_stdout.log` shows the core writing `ULUS10202003/00000000.000` 1,674 times with `[Rewind] Buffer capacity insufficient` between writes. The folders it left are whole: 42,528 B each, `PARAM.SFO` parsing with the player's own id. Recorded against the core, not the save shape.

## RB-307. What RomM's `save_target` says for the game once rehashed

Question: What RomM's `save_target` says for the game once rehashed

Measured: `title_id` and `save_target` `ULUS10202`, layout `folder-prefix`: the key and match rule `save_shapes.json` declares for `psp`, and the binding the launch journal taught. Portable Ops Plus (USA), not rehashed, carries none.

## RB-366. PPSSPP writes states to two places, and RetroBat mirrors between them (resolved)

The same PSP game appears under two different naming schemes at once:

```text
saves/psp/ppsspp/3rd Birthday, The (Europe)_0.ppst     <- matches es_savestates.cfg
saves/psp/PPSSPP_STATE/ULES01513_1.00_0.ppst           <- PPSSPP's native location
```

Static evidence could not say which is current, so it was driven live on a real PSP library
(`tools/m0-probes/probe2-psp-states.ps1`, Patapon, a game with no prior state so nothing
existing was touched). **Both are real, and neither is stale: RetroBat keeps them in sync.**

**Sync-out is live, not at exit.** The launcher sets `saves/psp` as PPSSPP's content path,
so PPSSPP writes natively to `PPSSPP_STATE/<GAMEID>_<version>_<slot>.ppst`. Pressing F2 at
`00:34:43.294` produced:

| Time         | File                                            |
| ------------ | ----------------------------------------------- |
| 00:34:43.411 | both `.ppst` copies, byte-identical, same mtime |
| 00:34:43.416 | `ppsspp/<rom filename>.txt`                     |
| 00:34:43.431 | `PPSSPP_STATE/<GAMEID>_1.00_0.jpg`              |

So the mirror is made **about 120 ms after the save, while the emulator is still running**.
Re-checking at exit showed nothing further changed, so there is no exit-time pass to wait for.

**The `.txt` is a name-mapping sidecar.** It holds exactly the native basename:

```text
saves/psp/ppsspp/Patapon (Europe) (En,Fr,De,Es,It).txt   ->   UCES00995_1.00
```

That is how RetroBat translates between the ROM-filename scheme and the game-ID scheme. It
is not a save, but it is **not disposable either**: treat it as part of the state.

**The ES-facing directory is the authoritative one, and it is the one RomMBat must use.**
ES passes the launcher `-state_slot N -state_file <ES-facing path>` (the same pattern appears
in the log for `gba` and `ps2`), and the launcher then hands the emulator the ES-facing path
directly:

```text
PPSSPPWindows64.exe -fullscreen "<rom>" --state="...\saves\psp\ppsspp\Patapon (Europe) (En,Fr,De,Es,It)_0.ppst"
```

Deleting every native file and relaunching that way **recreated the native copy byte for
byte** from the ES-facing one. So a state that RomMBat downloads and writes into
`saves/psp/ppsspp/` does reach the emulator, which is exactly the case that mattered. Note
the sync-in copies **only the `.ppst`**; the native `.jpg` was not recreated.
