---
summary: How each emulator RetroBat offers for `psx` behaves, measured, with RB- IDs.
read-when: Before certifying a `psx` row or changing how RomMBat handles a `psx` save, state or firmware.
---

# psx: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-7. Only when DuckStation is the selected emulator

Plan says: PS1 is already per-game via DuckStation `PerGameTitle` (L831, L835)

Measurement says: Only when DuckStation is the selected emulator. This install runs libretro for `psx` and produces plain `.srm`

## RB-9b. `PerGameFileTitle` is the better value: it keys the card by rom filename rather than by DuckStation's

Plan says: DuckStation `PerGameTitle` is treated as sufficient (L831)

Measurement says: `PerGameFileTitle` is the better value: it keys the card by **rom filename** rather than by DuckStation's internal title. **Later withdrawn**: a driven card showed `PerGameTitle` binds a multi-disc set and the filename key splits it, so the plan's original value was correct. See [freegosy-findings.md](../../freegosy-findings.md), F18

## RB-310. What EmulationStation lists for a folder holding discs and a same-named playlist

Question: What EmulationStation lists for a folder holding discs and a same-named playlist

Measured: **One game.** `/systems/psx/games` returned a single entry whose path is `roms/psx/<fs_name>/<fs_name>.m3u`, and the list on screen showed "Metal Gear Solid (USA) (Rev 1)" with no folder and no disc. So RomM's own layout is the one to land, and nothing is hidden or renamed to get there.

## RB-311. All three load both discs

Question: Whether the three `libretro` cores read the playlist, and how they name states

Measured: **All three load both discs**, `Setting disc in tray: 1/2` in the log, and name every state after the playlist: `<fs_name>.stateN` under `libretro.<core>`. Driven with a real swap on `mednafen_psx_hw`: states saved after `Setting disc in tray: 2/2` kept the same name. Booting alone wrote `saves/psx/<fs_name>.srm`, the memory card, and `<fs_name>.ldci`, the disc index whose absolute path `save-sync` already excludes.

## RB-312. By disc serial natively and by playlist in RetroBat's mirror

Question: How DuckStation names a set's states and cards

Measured: **By disc serial natively and by playlist in RetroBat's mirror.** Its own `savestates/` held `SLUS-00594_1.sav` from disc 1 and `SLUS-00776_2.sav` from disc 2; the declared `saves/psx/duckstation/` held the same bytes as `<fs_name>_01.sav` and `_02.sav`. The card is `memcards/Metal Gear Solid (USA)_1.mcd`, per title, and **rom 320307 opened the card rom 320306 created**, so two RomM roms share one card. The `.txt` sidecar held `SLUS-00776`, the serial of the disc in the tray at the last save, where RomM's `title_id` for both roms is `SLUS-00594`.

## RB-313. How standalone mednafen names a set's states

Question: How standalone mednafen names a set's states

Measured: After the playlist and one hash for the set, `<fs_name>.<md5>.mc0` and `.mc1`, across an eject, disc select and insert.

## RB-314. No, and it is not BizHawk's choice

Question: Whether BizHawk reads the playlist

Measured: **No, and it is not BizHawk's choice.** For both `Nymashock` and `Octoshock`, `emulatorLauncher` started EmuHawk on `<fs_name> (Disc 1).cue` in place of the `.m3u`, so disc 2 is unreachable from the game entry whatever the layout. States and the memory card are named after that disc file: `<fs_name> (Disc 1).QuickSave2.State` and `<fs_name> (Disc 1) (v1.0).SaveRAM`. The two cores share that `SaveRAM` name with different sizes, 131,072 B under `Nymashock` and 262,144 B under `Octoshock`, which rewrote it.

## RB-316. Two playlists and no discs, and it was RomMBat

Question: What the first real sync of a disc set left behind

Measured: **Two playlists and no discs, and it was RomMBat.** Synced from a RomM collection through the gamepad UI, each disc landed and verified, then `MediaSync.Discard`, which runs after every game, deleted it: it removed every synced file that was neither the game row, firmware nor a wanted media kind, and a `rom_part` is none of the three. The plan then called both games present, because every remaining row agreed with the disk. Fixed by asking whether a file is artwork rather than whether it is not the game, and by the plan checking every file the playlist names. The re-sync fetched both sets, 2.1 GB, and the next was 0 downloaded, 0 written.

## RB-328. A formatted, empty memory card, and it went up as the game's save

Question: What a `libretro` `psx` core leaves when the game has not saved

Measured: **A formatted, empty memory card, and it went up as the game's save.** Under `mednafen_psx_hw` a session that reached the name entry and quit left a 131,072 B loose `<rom>.srm`, `4899f80c...`, with the `MC` header and all fifteen directory frames `0xA0`, never used, and nothing in any data block; RetroArch logs saving it on exit. Creating the name writes nothing to the card: SotN first saves at a save room. The quit flush uploaded the empty card as save 511, and the real save, one frame `0x51` named `BASLUS-00067DRAX00`, became save 512 on the next session. A second device's first boot would have sent its own empty card as the newer save. The scanner now passes over a card whose directory frames are all never used, and a restore treats one as absent and copies it aside before writing

## RB-329. A card per port, named from its own database, and states keyed by serial

Question: Where DuckStation keeps a game's memory card and its states, and what joins them to a rom

Measured: **A card per port, named from its own database, and states keyed by serial.** Under RetroBat's `PerGameTitle` for both ports, SotN wrote `saves/psx/duckstation/memcards/Castlevania - Symphony of the Night (USA)_1.mcd`, 131,072 B, on exit, the stem being `gamedb.yaml`'s `saveName` for `SLUS-00067`; a game that probes port 2 also gets `_2.mcd`, as Metal Gear Solid did, formatted and empty. The raw card is the same image a `libretro` core's `.srm` is, and DuckStation loaded one copied in. No rule covered `memcards/`, so the card was reported as a shape RomMBat does not recognise and never sent; a `display name` rule with a slot per port now carries it, attributed by the DuckStation launch covering the write. States go to `emulators/duckstation/savestates/SLUS-00067_<slot>.sav`, the replaced one kept as `.bak`, and `emulatorLauncher` mirrors them incrementally into `saves/psx/duckstation/<rom>_NN.sav` beside a `.txt` holding the bare serial

## RB-330. Whether standalone mednafen gives a `psx` game a memory card at RetroBat's default

Question: Whether standalone mednafen gives a `psx` game a memory card at RetroBat's default

Measured: **No.** With `mednafen_psx_memcards` unset, `emulatorLauncher` writes `psx.input.port1.memcard 0` through `port8` into `mednafen.cfg`, although mednafen's own default is a card in ports 1 and 2, and Metal Gear Solid's codec found no card to save to. With the option set to `2` in `es_settings.cfg` the launcher wrote ports 1 and 2 as `1` and the game saved. The option's choices are `none`, 1, 2, 3 and 4. So a stock install's mednafen `psx` row cannot keep a battery save at all; RetroBat's to report

## RB-331. The playlist's stem, a layout md5, and the port

Question: What standalone mednafen names a `psx` memory card after

Measured: **The playlist's stem, a layout md5, and the port.** Metal Gear Solid (USA) launched through its `.m3u` wrote loose `<rom>.2f876f4966ab9a14472349c43b3d64a4.0.mcr` for port 1 and `.1.mcr` for port 2, 131,072 B each, raw cards, and its states as `mednafen/sstates/<rom>.<same md5>.mc<n>`. The md5 is of no file: it is over each disc's table of contents in playlist order, the first and last track numbers, the lead-out LBA, and each track's LBA and control data bit, as little-endian `uint32`s. Recomputed from the two `.cue` files and the `.bin` sizes it reproduces `2f876f49...`; disc 1 alone does not. A rule claiming `.mcr` with the hash and a slot per port now carries the cards, and `MednafenRomHash` computes the layout for a `.cue`, or a `.m3u` of them, of one file and one data track each

## RB-332. Disc 1's file, not the playlist

Question: What BizHawk's `psx` rows name after a disc set, and what that did to RomMBat

Measured: **Disc 1's file, not the playlist.** Given the `.m3u`, the launcher hands EmuHawk disc 1's `.cue` (314), so the state mirror is `saves/psx/bizhawk/sstates/<core>/Metal Gear Solid (USA) (Disc 1).QuickSave<n>.State` beside a sidecar reading `Metal Gear Solid (USA) (Disc 1) (v1.0).<core>`, BizHawk's title, and the card is `bizhawk/<title>.SaveRAM`: 131,072 B under Nymashock, one raw card, and 262,144 B under Octoshock, a card and 128 KB that is not one. **Three things followed.** No state attributed, because the ROM index knew only the `.m3u`'s stem; a disc of a set now answers for the set, after every ROM. The card had no rule on `psx`; the BizHawk rule now lists it, and the card binds through the sidecar once the state is attributed. And a restore named the state after the `.m3u`, `Metal Gear Solid (USA).QuickSave0.State`, with the right bytes, offering the two states already present as missing too; those two failed on `local_state`'s unique `(rom_id, slot)` after their files were written, leaving two stray copies. A restore now takes the sent name's stem when it is one of the ROM's own disc files, and removes a file it placed when recording it fails

## RB-333. It leaves two files for one game in one slot

Question: What a DuckStation memory card type change does to one game's slot

Measured: **It leaves two files for one game in one slot.** Under `PerGame` SotN saved to `duckstation/memcards/SLUS-00067_1.mcd`, named by serial, while the `PerGameTitle` card `Castlevania - Symphony of the Night (USA)_1.mcd` stayed behind. Both bound to rom 280632 through their launches and both are `duckstation:battery`. The flush sent whichever it read first and failed the other, so the old card, sorting first, held the slot and the new save never went up. It now sends the file written most recently and reports the other as superseded, not failed; a restore names the card after the title whose file was written last. **A restore still fills only a slot no file holds**, so deleting the live card while the stale one remains restores nothing, and the flush's message says to remove the stale file. `Shared` writes `shared_card_1.mcd` and `_2.mcd`, which match the per-game `_1` and `_2` names, so all eight `shared_card_<n>.mcd` names are declared shared containers and are reported, never claimed: with SotN and Metal Gear Solid both saved to one card, nothing was recorded and nothing uploaded

## RB-334. It fetched it, and would on every restore

Question: What a restore does with a blank card the server holds

Measured: **It fetched it, and would on every restore.** Save 441, Metal Gear Solid (USA) (Rev 1)'s `.srm` sent on 2026-09-23 as a formatted card with no save, came back when the rom's DuckStation card was restored, since the scan passes over the blank local copy and the slot read as empty. The download now refuses a 131,072 B card whose directory frames are all never used, reported as refused, not a save, as RomM's `null` payload is

## RB-335. Loose `.mcd` cards named like DuckStation's, each port named its own way

Question: What swanstation's own card types write, and what attributing them took

Measured: **Loose `.mcd` cards named like DuckStation's, each port named its own way.** With `swanstation_memcard1` `PerGameTitle` and `swanstation_memcard2` `PerGame`, SotN wrote `saves/psx/Castlevania - Symphony of the Night (USA)_1.mcd` and `saves/psx/SLUS-00067_2.mcd`; `Shared` writes `duckstation_shared_card_1.mcd`, and the default writes RetroArch's `.srm`. `mednafen_psx_hw`'s right card writes `<rom>.1.mcr`; its shared-card option changed nothing, since the left card keeps RetroArch's `.srm`; and `pcsx_rearmed`'s second card is one `pcsx-card2.mcd` for every game. **Three defects followed.** The loose scan never asked the title routes, so the serial-named card attributed to no game, and the title-named one only because its title is the rom's stem. A restore took one title for both ports, putting port 2 under `Castlevania - Symphony of the Night (USA)_2.mcd`, and weighed which title was live by any file whose stem stripped to it, the `.srm` included. That misnamed file then taught a second binding for port 2, cleared with `saves bind --forget`. The loose scan now attributes a display-name file by title, and a restore prefers a title its own port taught and counts only that port's files of that rule, refusing where two names remain and no file says which is live
