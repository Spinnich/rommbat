---
summary: How each emulator RetroBat offers for `gamecube` behaves, measured, with RB- IDs.
read-when: Before certifying a `gamecube` row or changing how RomMBat handles a `gamecube` save, state or firmware.
---

# gamecube: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-8. True, but with a region subdirectory, several files per game, and `.gci.deleted` litter to exclude

Plan says: GameCube GCI folder gives "individual `.gci` files" per game (L832)

Measurement says: True, but with a region subdirectory, several files per game, and `.gci.deleted` litter to exclude

## RB-189. `dolphin_sync_saves` is GameCube only, runs once per launch, and reconciles a region folder with its own `Card A/`

The claim being checked: `dolphin_sync_saves` is RetroBat copying save files between the dolphin and libretro-dolphin folders **on its own schedule**, and must be detected before either location is trusted (**RB-123, [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/366b5f6bf/docs/PLAN.md), the `retrobat-layout` skill**)

What was measured: **Wrong in all three parts, and the code was going to be built against it.** Read from `emulatorlauncher`, `Dolphin.Generator.cs`: it is **GameCube only** (declared twice in `es_features.cfg`, both under `gamecube`, and the `wii` branch never calls `SyncGCSaves`), it runs **once per launch inside emulatorlauncher before Dolphin starts** rather than on any schedule, and the two locations are **`GC/<REGION>/` and its own `Card A/` subdirectory**, not two emulator folders. Nothing moves while RomMBat is running, which is what makes it detectable at all

## RB-190. With `dolphin_sync_saves` on, `Card A` holds the region root as it was before the launch, one session behind

Driven on `K:`: with the option on, launching wrote `Card A/41-G3SE-BUST A MOVE 3000.gci` at md5 `6242a2ff`, the previous session's save, and Dolphin then wrote `6bca9b1a` over the region root. `Card A` is a snapshot taken by emulatorlauncher, not a mirror kept in step

## RB-191. With `dolphin_sync_saves` on, a save RomMBat removes comes back stale from `Card A`

The region-root `.gci` was deleted, as a transfer dropping a member does, and the next launch **copied it back out of `Card A`** holding `6242a2ff` rather than the `6bca9b1a` that was removed. The whole trace is one line, `[INFO] GameCube saves have been synced.` This is the one-sided branch of `SyncGCSaves`, and it is the real hazard: the mtime branch cannot bite, because a save RomMBat restores is written with the current time and always wins

## RB-192. Neither can be, by construction rather than by intent

The claim being checked: `Card A` and the `.old` files a reconciliation leaves would be double-counted by class C discovery

What was measured: **Neither can be, by construction rather than by intent.** `SaveUnitScanner.SafeFiles` is `Directory.EnumerateFiles(path)` with no `SearchOption`, so a `Card A` subdirectory is invisible, and `X.gci.old` fails `KeyOf` exactly as `X.gci.deleted` already does. The existing code fails closed here, so what it needed was the report and not a fix

## RB-193. Class C in slot A only, and only at the default

The claim being checked: GameCube is class C (`save_shapes.json`)

What was measured: **Class C in slot A only, and only at the default.** `dolphin_slotA` is labelled **SAVE FORMAT** in the ES menu, `GCI FOLDER` (8) against `MEMORY CARD` (1); at 1 the container becomes one shared raw `SRAM.<REGION>.raw`, which is class D, the inverse of what conversion does for PS2. **Slot B is worse: RetroBat never rewrites it**, leaving Dolphin's stock relative default, so `SlotB = 1` points at top-level `saves/dolphin/User/GC/SRAM.EUR.raw`, which Dolphin then region-substitutes. A 16 MB `SRAM.USA.raw` appeared there during a GameCube launch on `K:`, and `E:` has carried one since August. Both are outside every container `save_shapes.json` declares, and `NANDRootPath` points into the same tree even for a GameCube launch

## RB-194. The Game-ID launch-window correlation attributes a real GameCube save on hardware

A `.gci` is named `41-G3SE-BUST A MOVE 3000.gci` and carries a game code rather than a rom filename, so nothing else could attribute it. The binding was learned unprompted: `gamecube/G3SE` to `roms/gamecube/Bust-A-Move 3000 (USA).rvz`, `learned_from=journal`, detail _was running when G3SE was last written (16:18:30Z against 16:19:19Z)_. Discovered, attributed, bundled and uploaded as save 181 in one pass

## RB-365. Dolphin GCI folder: confirmed, but harder than the plan assumes

The plan says GameCube is "already per-game in a stock RetroBat" via the GCI folder default.
Confirmed, with three complications:

```text
saves/gamecube/dolphin-emu/User/GC/USA/01-GALE-SuperSmashBros0110290334.gci
saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-game1.ssx.gci
saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-settings.ssx.gci
saves/gamecube/dolphin-emu/User/GC/USA/5D-GUNE-Gauntlet - Dark Legacy.gci.deleted
```

1. **A region subdirectory sits in the path** (`User/GC/USA/`), which no template in the plan
   accounts for.
2. **One game can produce several `.gci` files** (`69-GXBE-` yields both `game1.ssx` and
   `settings.ssx`). So GameCube is per-game but _multi-file_: class B nested inside the
   per-game story, not the clean 1:1 the plan implies.
3. **Dolphin soft-deletes with a `.gci.deleted` suffix** and leaves the file in place. These
   must be excluded or they will sync as live saves.

The filename format is `<makercode>-<gamecode>-<internal name>.gci`, keyed by game code
(`GALE`, `GXBE`), not ROM filename, so the attribution route the plan describes is still
required.
