---
summary: How each emulator RetroBat offers for `ps2` behaves, measured, with RB- IDs.
read-when: Before certifying a `ps2` row or changing how RomMBat handles a `ps2` save, state or firmware.
---

# ps2: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-172. A real PS2 library is 302 single-disc titles against 7 two-disc sets, with no `.m3u` anywhere and no per-game folders

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: recovered from the findings ledger, which recorded it during M6 stage 2c (ad01b2d) on the 8.2.0 install its header names; the method is the text below.
So a set on disk is loose sibling files, and RetroBat's `ps2` listing `.m3u` in `<extension>` neither binds a set nor means anything by its absence. RetroBat's wiki says PCSX2 cannot read one, and the launcher hands it the playlist's first disc instead (RB-435)

## RB-175. A PS2 card's size and mtime cannot say whether it holds a save

Verified: RetroBat 8.3.0, 2026-10-16. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: hashed and counted the byte values of `saves/ps2/pcsx2/memcards/` around Burnout 3 launches under PCSX2 2.8.2 on the agent tree's 8.3.0 install, before and after it saved a profile to its own card.
PCSX2 creates `Mcd001.ps2` and `Mcd002.ps2` unformatted, 8,650,752 B of one byte value. After Burnout 3 formatted its own card and saved, that card was still 8,650,752 B, now 250 distinct byte values, and its mtime matched `Mcd002.ps2`, rewritten in the same instant with no byte changed, to the tenth of a millisecond. So mtime cannot decide whether a card changed, size cannot tell an empty card from one holding a save, and only the contents separate them (RB-43, RB-328).

## RB-176. PCSX2's sidecar is a third format: the serial plus a CRC

Verified: RetroBat 8.3.0, 2026-10-15. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: saved a state in Armored Core 3 under PCSX2 2.8.2 with `F2` and read the files RetroBat mirrored into `saves/ps2/pcsx2/`.
`saves/ps2/pcsx2/Armored Core 3 (USA).txt` holds `SLUS-20435 (FDB4D261)`, beside the mirrored `Armored Core 3 (USA).01.p2s`, while PCSX2's own copy is `emulators/pcsx2/sstates/SLUS-20435 (FDB4D261).01.p2s`. So it is neither the bare serial DuckStation writes nor the underscore-joined form PPSSPP writes, and `GameIdAttributor.FromSidecar`'s first-underscore split leaves it whole rather than parsing it. No `ps2` unit key has that form, since a folder card's is `BASLUS-20435`, so the sidecar never attributes anything on `ps2`: a folder card save attributes through RomM's `save_target` (RB-406), and a state through its file name. Nor does a restore need it (RB-437).

## RB-177. The `K:` test stick cannot stand in for the real install on PS2

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: recovered from the findings ledger, which recorded it during M6 stage 2c (ad01b2d) on the 8.2.0 install its header names; the method is the text below.
`K:\RetroBat` holds **no PS2 ROM, no PCSX2 binary** (`emulators/pcsx2` is `inis/` and `portable.ini` only) **and no PS2 BIOS**, against `E:`'s 319 ROMs, full PCSX2 and six `scph*` images. RetroBat downloads emulators on demand and an uninstalled one raises a modal dialog with no title and no timeout, so standing `K:` up is three steps with a known indefinite hang among them. **The two-writer probe needs none of that** and belongs on `K:`, which has EmulationStation and a real 56-setting file

## RB-182. A converted PCSX2 memory card is `<rom stem>.ps2`, so the extension is replaced and not appended

Verified: RetroBat 8.3.0, 2026-10-15. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: set `ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game` on the agent tree's 8.3.0 install, booted the game under PCSX2 2.8.2, and read `PCSX2.ini` and `memcards/`.
`ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game` makes `emulatorlauncher` write `Slot1_Filename = Armored Core 3 (USA).ps2`, and PCSX2 creates `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, 8,650,752 B. **Two naming rules are in play at once**: the `es_settings.cfg` key must carry `.chd` or it is ignored silently (RB-358), while the card PCSX2 writes drops it. So the card is named after one disc's file, and each disc of a set would get its own, which is why RomMBat forces `folder` rather than `game` (RB-406).

## RB-183. A converted game leaves the shared card completely alone, and its own card holds only its saves

Verified: RetroBat 8.3.0, 2026-10-16. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: converted Burnout 3 on the agent tree's 8.3.0 install, saved a profile under PCSX2 2.8.2, and hashed `memcards/` before and after.
Across the converted launch `Mcd001.ps2` kept its mtime and md5, so the setting redirects the writes and a save left on the shared card is stranded there. The new card, `Burnout 3 - Takedown (USA, Canada).ps2`, holds one serial, `BASLUS-21050`, the game's own.

## RB-184. `pcsx2_slot1_memory=game` leaves slot 2 shared, and moves its mtime without changing a byte

Verified: RetroBat 8.3.0, 2026-10-16. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: hashed `memcards/` around converted launches of Armored Core 3 and Burnout 3 under PCSX2 2.8.2 on the agent tree's 8.3.0 install.
`Slot2_Filename` stays `Mcd002.ps2`, and each converted launch wrote it at the same instant as the game's own card, to the tenth of a millisecond, with its md5 unchanged: one byte value, the unformatted card PCSX2 creates, which no game formatted. So under `game` too **mtime moves while content does not**, which is why every save is content-hashed.

## RB-376. Launching a PS2 game rewrites the shared memory cards on its own

Verified: RetroBat 8.3.0, 2026-10-15. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: hashed `saves/ps2/` around two Armored Core 3 launches under PCSX2 2.8.2 on the agent tree's 8.3.0 install in which nothing was saved.
Both shared cards were written when the game closed, without the game being asked to save:

```text
pcsx2\memcards\Mcd001.ps2   8650752 B   15:13:54.7840 -> 15:15:29.8625   md5 unchanged
pcsx2\memcards\Mcd002.ps2   8650752 B   15:13:54.7845 -> 15:15:29.8625   md5 unchanged
```

**So a class-D container's mtime changes merely because a game was launched.** Any sync that
uses modification time to decide whether a shared card needs uploading will upload it after
every session. Content hashing is required, not optional, for class D.

## RB-406. `pcsx2_slot1_memory=folder` makes slot 1 one directory, split per save by PS2 serial

Verified: RetroBat 8.3.0, 2026-10-16. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: set `ps2.pcsx2_slot1_memory=folder` on the agent tree's 8.3.0 install, booted Armored Core 3 and then saved a Burnout 3 profile under PCSX2 2.8.2, and read `PCSX2.ini` and `memcards/`.
`emulatorlauncher` writes `Slot1_Filename = Mcdf01.ps2`, and PCSX2 makes `memcards/Mcdf01.ps2/` a
directory holding a `_pcsx2_superblock`, 0 B until a game formats the card and 8,192 B after.
`McdFolderAutoManage = true` comes from RetroBat's `PCSX2.ini` template. Each save is a directory
named from the save's own name, which starts with the serial: Burnout 3's profile is
`BASLUS-21050/`, a 61,772 B `BASLUS-21050` beside `icon.sys`, `view.ico` and `_pcsx2_index`. So
the card is one fixed directory every game writes into, and the units inside it are per save,
keyed by `B`, a region letter and the serial, not by the ROM stem. That is exactly RomM's
`save_target` for the ROM, `BASLUS-21050` for Burnout 3, so RomMBat forces `folder` and syncs each
directory as a class C unit. `Mcd001.ps2` stopped changing, and slot 2 is still the file
`Mcd002.ps2`, so `saves` names both stock cards as shared.

## RB-434. EmulationStation lists a `ps2` folder holding discs and a same-named playlist as one game

Verified: RetroBat 8.3.0, 2026-10-17. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: placed two generated `.chd` stand-ins in `roms/ps2/Probe Set (USA)/` on the agent tree, read `/systems/ps2/games` with and without `Probe Set (USA).m3u` beside them, and screenshotted the list.
With the playlist, ES returned one entry, `roms/ps2/Probe Set (USA)/Probe Set (USA).m3u`, as on `psx` (RB-310). Without it, each disc was its own game, and only `<hidden>true</hidden>` on disc 2's gamelist entry left one tile. So `ps2` lands a set the way `psx` does, and nothing is hidden.

## RB-435. Both PCSX2 builds are handed a playlist's first disc, the libretro core reads the playlist, and Play! cannot

Verified: RetroBat 8.3.0, 2026-10-17. Carried: RetroBat 8.3.1.1, 2026-10-17, the PCSX2 generator changes only window geometry and bezel placement, never how it is handed a playlist, and the same four rows stay in ps2's element, though play and pcsx2-16 now sit inside libretro's. How: booted a one-line `.m3u` naming `Armored Core 3 (USA).chd` on each of `ps2`'s four rows through `emulatorLauncher` on the agent tree, read `emulatorLauncher.log` and RetroArch's log, and screenshotted each.
`pcsx2` 2.8.2 was started on `Armored Core 3 (USA).chd`, not the playlist, and booted. `pcsx2-16` was started the same way and exited in 3 s with code 0, the same command line a direct launch of the `.chd` produces, so the playlist changes nothing for it. `libretro:pcsx2` was handed the playlist, logged `Disk #1 added from M3U` and booted `SLUS-20435`. `play` was handed the playlist and crashed in 7 s with `0xC0000409`, while the same `.chd` launched directly booted. So a set's playlist starts disc 1 on every row but Play!. What a disc change does to the folder card is RB-436.

## RB-436. A disc change in `pcsx2` keeps a set's one folder card: disc 2 reads and writes the directory disc 1 made

Verified: RetroBat 8.3.0, 2026-10-17. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: on the agent tree with `pcsx2` 2.8.2 and Devil May Cry 2 (USA) as a two-disc set, rom 320198, Disc 1 `SLUS-20484` and Disc 2 `SLUS-20627`, launched through ES, saved on Disc 1, changed disc from the pause menu in the same session, saved from Disc 2, then read the memory card directory and the flush before and after.
Disc 1 wrote `Mcdf01.ps2/BASLUS-20484/`. Disc 2 loaded (`Serial: SLUS-20627` in PCSX2's log), listed that save on its memory card screen, and a save from it rewrote the files in `BASLUS-20484/` in place. No `BASLUS-20627` directory appeared. So a set is one save unit keyed by the serial its first save used, though its discs carry different serials. `libretro:pcsx2` logged `Disk #1 added from M3U` and `Disk #2 added from M3U` on the same set. Not driven: a cold boot of Disc 2 on its own.

## RB-437. A `pcsx2` state saved after a disc change is named for the launched disc, and loads on a Disc 1 boot with or without its sidecar

Verified: RetroBat 8.3.0, 2026-10-17. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); its only ps2 changes are PCSX2's window geometry, bezel placement and the order of rows (play and pcsx2-16 are now nested in libretro's element), none of which moves a card, save or BIOS path. How: on the agent tree with `pcsx2` 2.8.2, took the state PCSX2 left after RB-436's disc change in Devil May Cry 2 (USA), rom 320198, and launched the set's `.m3u` with `-state_slot 1 -state_file` naming it, once as written and once after deleting it, its sidecar and PCSX2's own copy and restoring it from RomM.
The state is `saves/ps2/pcsx2/Devil May Cry 2 (USA) (Disc 1).01.p2s`, named after the file the launcher ran, while its sidecar holds `SLUS-20627 (08995DEE)`, Disc 2's serial, and PCSX2's own copy is `sstates/SLUS-20627 (08995DEE).01.p2s`. The CRC is not Disc 2's: PCSX2 logs Disc 1 as `SLUS-20484`, CRC `08995DEE`. The launcher passes PCSX2 the ES-facing file itself, `-statefile "...\saves\ps2\pcsx2\Devil May Cry 2 (USA) (Disc 1).01.p2s"` on a Disc 1 boot, so neither the sidecar nor `sstates/` is read. Both times PCSX2 resumed on Disc 2's memory card save screen. A restore writes the state and its screenshot and no sidecar, md5-equal, and the next scan reads it `in step`. So a set's states key on the launched disc's stem, whichever disc was in the drive. Not driven: playing on from the loaded state while Disc 1 is the mounted image.
