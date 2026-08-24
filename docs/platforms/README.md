# Platform certification records

One file per RetroBat system, named `<system>.md` after the folder name in `es_systems.cfg`,
**with a section per emulator inside it**.

**The unit is `(system, emulator, core)`, never the system alone.** Two emulators for one
console differ exactly where it costs a save: `psx` under libretro writes a plain `.srm` and is
class A, while `psx` under DuckStation writes a memory card named from an internal database
title and needs Game-ID attribution. State directories and filenames are per emulator, thirteen
of them, and two of the thirteen declare a directory the emulator does not write to. `libretro`
and `bizhawk` are core-scoped on top of that, so one game under two cores has independent state
sets. So "snes is certified" is not a claim; "`snes` under `libretro`/`snes9x` is certified"
is, and it says nothing about `snes` under `bizhawk`.

A pass is certified when all nine of these hold against a real install. **A pass is not done at
eight of nine**, and none of it can be desk-checked: step 7 requires actually launching a game.

1. Folder mapping resolves, and the record names **which layer** resolved it.
2. `<extension>` list captured; a known-unsupported file is correctly excluded.
3. Required BIOS from `batocera-systems.json` resolved against RomM by md5; gaps listed.
4. Save shape classified (A/B/C/D) **for this emulator**, and the battery save round-trips.
5. Save state round-trips including its screenshot, per this emulator's `es_savestates.cfg`
   entry, with the declared directory confirmed against where it really writes.
6. Where class D applies, the per-game memory card option is verified.
7. A game launches from EmulationStation after sync, with art and metadata.
8. Play session recorded and reaches RomM.
9. Re-sync is a clean no-op.

Steps 1, 2, 3, 7, 8 and 9 are largely per system and can be carried across emulators with a
note saying so. **Steps 4, 5 and 6 have to be redone per emulator**, and they are also the
three where being wrong destroys data rather than costing a re-download.

Load the `platform-certification` skill before starting. Record what failed as well as
what passed; a record that only lists successes is not evidence.

## Rollout order

Second through sixth generation consoles first, which is where the usage is and where the
save shapes stay tractable. Arcade is deliberately last: it is the only wave needing the
explicit folder-choice decision from M2, and it carries romset-version coupling nothing
else does.

| Wave | Systems                                                                                      | Why here                                                         |
| ---- | -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Class A saves, no BIOS, single files. Proves the spine           |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Introduces BIOS and disc formats                                 |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | The hard save shapes: memory cards, GCI folders, VMU             |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Arcade: romset-versioned naming, the ten-folder mapping question |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Long tail, mostly class A                                        |

The order is derivable rather than hand-maintained: `es_systems.cfg` carries
`<manufacturer>`, `<hardware>` and `<release>` per system, so filtering to
`hardware=console` and sorting by release year reproduces roughly this list and stays
correct as RetroBat adds systems.

## Nothing is certified yet, and the rollout starts after M7

The framework has to work end to end on a single platform first, which is M1 through M6. Beyond
that, every pass needs a person at the machine launching real games, and doing that through a
terminal rather than the gamepad UI makes a long job longer, so the waves above start after M7
and finish against an M8 package.

**One thing does not wait.** Steps 4, 5 and 6 are the data-loss steps, and M6 ships them across
three stages. Each stage owes one hands-on pass of the shape it added: one game, one emulator,
one real save or state, through EmulationStation and back. That is not a certification and must
not be filed as one, but "the tests pass" and "an emulator wrote this and RomMBat handled it"
are different claims, and only the second is evidence.

| M6 stage | The one shape to exercise by hand                                     | Done               |
| -------- | --------------------------------------------------------------------- | ------------------ |
| 2a       | A save state, across more than one emulator for one game              | **Yes**, see below |
| 2b       | A PPSSPP `SAVEDATA/` directory, and MAME `nvram/` if convenient       | **Yes**, see below |
| 2c       | A PS2 battery save after opting that game into a per-game memory card | **Yes**, see below |

**2c, done on `ps2` / Armored Core 3 (USA), PCSX2.** Not a certification: one game, one
system, steps 4, 6 and 9 only. Results are findings 182 to 188 in
`docs/retrobat-findings.md`.

| Step                                       | Result                                                                              |
| ------------------------------------------ | ----------------------------------------------------------------------------------- |
| The shared card, before anything            | `Mcd001.ps2` holding **11 distinct games**, Armored Core 3 among them as `BASLUS-20435` |
| Converted                                   | `ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory = game`, prior state `absent`   |
| Refused while EmulationStation was running  | **yes**, exit 2, nothing written to the file                                        |
| PCSX2 wrote                                 | `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, **one game's saves**, 4 entries |
| The shared card afterwards                  | **untouched**, mtime and md5 both unchanged                                         |
| Discovered and attributed                   | class D, slot `pcsx2:battery`, by the stem through the existing `RomIndex`          |
| Uploaded                                    | save 179, server hash equal to the local one                                        |
| **The game loaded the restored save**       | **yes**                                                                             |
| Eviction with an unsent card                | **refused**, and reported "still short" rather than claiming success                |
| Eviction after a flush                      | offered, so the guard does not block spuriously                                     |
| Reverted                                    | key removed, 57 settings before and after, nothing else disturbed                   |
| Re-sync and re-flush                        | clean no-op, 0 downloaded, 0 written, gamelists unchanged                           |

**Three things this pass did not prove, and they are not small.**

- **The download side of class D is untested anywhere.** The card went up and came back only as
  far as "the server holds it". Nothing has ever written a class D save onto a device from the
  server, and the bundled-slot refusal in `SaveSync.DownloadAsync` branches on class C, so what
  a class D download does is unexercised rather than decided.
- **Only `(ps2, pcsx2)` was driven.** `dreamcast` and `psx` are refused by declaration with
  their measured reasons and neither refusal was exercised against a real emulator, and
  `folder`, PCSX2's third choice, is declared and unmeasured.
- **The ROM was adopted, then re-downloaded, and neither is the ordinary case for a converted
  game.** The first attempt failed verification against a stale server hash (finding 180),
  which is fixed on the instance now but shaped how this pass ran.

**2b, done on `psp` / Bust-A-Move - Deluxe (USA), PPSSPP.** Not a certification: one game, one
system, steps 4 and 9 only. Results are findings 154 to 159 in `docs/retrobat-findings.md`.

| Step                                  | Result                                                                      |
| ------------------------------------- | --------------------------------------------------------------------------- |
| PPSSPP wrote a save                   | `SAVEDATA/ULUS100570000/`, 4 files, 91,607 B, from the game's own save menu |
| The grammar scoped it                 | container `saves/psp/SAVEDATA`, key `ULUS10057` read as a prefix            |
| Attribution                           | route 1, the launch window, with no header and no sidecar available         |
| Upload                                | one archive, 87,559 B, returned as `ULUS10057 [2026-08-18_02-30-06].zip`    |
| Re-sync with no changes               | `no_op`, slot reports in step                                               |
| Both sides diverged                   | reported as a conflict, all four files copied aside                         |
| `saves resolve --keep-server`         | staged restore, 4 files swapped in                                          |
| **The game loaded the restored save** | **yes**                                                                     |

**Four defects came out of the pass**, none reachable from a test: a cached refusal that was
only an absence, a 409 reported as a failure rather than a conflict, a conflict that recorded no
copy aside, and a resolver whose verification could never pass for an archive. All four are
fixed and covered.

**Still not done for 2b.** MAME's short-name join is structurally sound and undemonstrated: the
measured install holds 1,231 nvram directories against 3 mame ROMs, so nothing joins. Wii ships
its grammar on tree structure alone with no game ever launched. And the conflict's server side
was synthetic, so the emulator-loads-it result rests on that plus the fold rather than on one
untouched round trip.

**2a, done on `mastersystem` / Phantasy Star (Brazil), four emulators.** Not a certification:
one game, one system, steps 4 and 5 only. Results are findings 134 to 139 in
`docs/retrobat-findings.md`.

| Emulator                     | On disk                                      | Slot                         | Landed |
| ---------------------------- | -------------------------------------------- | ---------------------------- | ------ |
| `libretro`/`genesis_plus_gx` | `libretro.genesis_plus_gx/….state1`          | `libretro:genesis_plus_gx:1` | yes    |
| `libretro`/`picodrive`       | `libretro.picodrive/….state1`                | `libretro:picodrive:1`       | yes    |
| `bizhawk`/`SMSHawk`          | `bizhawk/sstates/SMSHawk/….QuickSave2.State` | `bizhawk:SMSHawk:2`          | yes    |
| `jgenesis`                   | `jgenesis/states/…_0.jst`                    | `jgenesis::0`                | yes    |

The two libretro cores wrote the **identical** filename and became two server rows, which is
the collision the scoped upload name exists to prevent, proven rather than argued. What did
**not** work is the screenshot: uploaded, stored against the ROM, and not linked to the state.
See finding 138.
