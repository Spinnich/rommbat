---
summary: How each emulator RetroBat offers for `nes` behaves, measured, with RB- IDs.
read-when: Before certifying a `nes` row or changing how RomMBat handles a `nes` save, state or firmware.
---

# nes: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-262. Two regions of one game share a BizHawk battery save (#151)

`StarTropics (Europe).zip` under `bizhawk`/`NesHawk` offered Continue with the progress the USA copy had saved, and a new character made there was written to the same `bizhawk/StarTropics.SaveRAM`. So one title is one file for every ROM BizHawk gives it. RomMBat read the file as claimed by both, from the USA state's sidecar and the Europe launch, uploaded it under neither, and `saves bind` settled it

## RB-263. BizHawk titles a game that has no save state by dropping region and revision (#151)

`Legend of Zelda, The (USA) (Rev 1).zip` wrote `Legend of Zelda, The.SaveRAM`: region and revision dropped, the trailing article kept. With no state there is no sidecar, and the launch under `bizhawk` covering the write attributed it alone

## RB-264. BizHawk leaves a `.SaveRAM.bak` beside a changed `.SaveRAM` (#151)

The `.SaveRAM.bak` holds the save the new one replaced, written on exit once the save has changed, for `StarTropics` under `NesHawk` and `Ultima - Quest of the Avatar` under `quickerNES`. It is not a save, and counting it reported `bizhawk` as holding a shape nothing covers

## RB-265. A battery save's mtime does not say which session wrote it once a restore has run (#151)

A restore writes the bytes when it runs, so the newest `bizhawk` launch before that mtime, an Ultima session eight days earlier, was credited with a restored `StarTropics.SaveRAM` and contested the sidecar's correct answer. That would have stopped every restored BizHawk save from syncing again. The launch route is now skipped for bytes unchanged since they were attributed, and a session ends at the next launch of anything

## RB-266. A slot's recorded destination does not suit a save its emulator names itself (#151)

Once the device had sent `bizhawk:battery`, `save_slot` derived `saves/nes/StarTropics (USA).SaveRAM` from the ROM's stem, where BizHawk never looks, and a restore preview offered it. A slot never held took the right path, which is why the first test passed. The derivation now leaves a slot a subdirectory rule owns to that rule

## RB-271. `NesHawk` and `quickerNES` read each other's `.SaveRAM` (262)

The Legend of Zelda under `quickerNES` showed `LINK`, the file `NesHawk` had registered, on its file-select screen, and a new name registered there went into the next file slot of the same `Legend of Zelda, The.SaveRAM`, which then held `LINK` and `TEST` side by side. So one `bizhawk:battery` slot for both cores is one save in fact as well as in name, and switching core carries progress, as it does across the three `libretro` cores

## RB-272. Booting a battery-save game under `bizhawk` rewrites the save with no in-game save made, for both games driven

Destiny of an Emperor (USA) rewrote 4 bytes, `0x400` to `0x403`, on a run that sat on the title screen, with the same 361 non-zero bytes and the saved game intact; The Legend of Zelda changed md5 on a title-screen run with both registered names intact. EmuHawk writes `.SaveRAM` on exit and backs the previous one up to `.SaveRAM.bak` (**264**). So every launch of such a game sends a new `bizhawk:battery` version, which is a real change of bytes and correctly sent, and which spends RomM's 50-version slot cap faster than play does

## RB-273. mednafen writes `<rom>.<md5>.sav` on `nes` only when `<rom>.sav` does not already exist (#150, #152)

RetroBat configures `filesys.fname_sav` as `%f.%M%x`, and mednafen's own `Documentation/fname_format.txt` defines `%M` as "Empty for first evaluation per full path construction, then filled with the game's hash followed by a period": the plain name is tried first and used if it is there. Driven: The Legend of Zelda under mednafen loaded the `Legend of Zelda, The (USA) (Rev 1).sav` mesen standalone had written minutes before, and the name registered there went into the next file slot of that same `.sav`; Zelda II, with no plain `.sav`, got `Zelda II - The Adventure of Link (USA).88c0493fb1146834836c0ff4f3e06e45.sav`. States use `%f.%M%X` the same way, and since nothing writes a plain `.mc0` they came out hashed every time. So a plain `<rom>.sav` on `nes` is one save shared by mesen standalone and mednafen, uploaded as `mesen:battery` by rule, and a hashed one exists only for a game mednafen met first. A restore of a `mednafen:battery` save is refused where a plain `<rom>.sav` is present, because mednafen would read that one and never open the hashed file

## RB-274. mednafen's `%M` hash on `nes` is the md5 of the `.nes` inside the zip less its 16-byte iNES header

Measured on three ROMs: Final Fantasy (USA) `24ae5edf...` against `a475798c...` for the whole file, which is what RomM and `local_file` record; The Legend of Zelda (USA) (Rev 1) `d3f45393...`; Zelda II (USA) `88c0493f...`. None of the three has a trainer, so whether a trainer is hashed is unmeasured, and `HeaderlessNesHash` answers null for one rather than guess, which leaves such a save unnameable rather than misnamed

## RB-275. `jgenesis`, `mesen`, `mednafen` and `ares` write no `nes` state image, and none takes ES's slot

Launches carrying `-state_slot` 6, 6, 3 and 6 wrote `jgenesis` `_0`, `mesen` `_1`, `mednafen` `.mc0` and `ares` `.bs1`, and `F7` then `F2`, which steps the slot and saves on all four with no modifier, wrote the next one. `jgenesis` writes `emulators/jgenesis/states/nes/` and `emulatorLauncher` mirrors it into the declared `saves/nes/jgenesis/states/` with a `.txt` sidecar, like BizHawk (**270**); the other three write straight into `saves/nes/mesen/SaveStates/`, `saves/nes/mednafen/sstates/` (RetroBat sets `filesys.path_state` there) and `saves/nes/ares/Famicom/`. `jgenesis` declares `{{romfilename}}_{{slot0}}.png` and wrote none in two sessions. `ares` states are a fixed 21,719 B whatever the screen, so only the md5 tells two apart
