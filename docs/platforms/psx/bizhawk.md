---
summary: The certification record for `psx`: the two `bizhawk` rows.
read-when: When a result for one of these `psx` rows is needed, or before re-driving one.
---

# psx: the two `bizhawk` rows

## `bizhawk`/`Nymashock`

Driven on Metal Gear Solid (USA), rom 320307, for the same `.chd` reason as mednafen. Selected with
`psx.emulator` `bizhawk` and `psx.core` `Nymashock`, set with ES closed, and confirmed from
`emulatorLauncher.log`; the launcher hands EmuHawk disc 1's `.cue` (RB-314). Seeded by copying
mednafen's port-1 card to `bizhawk/Metal Gear Solid (USA) (Disc 1) (v1.0).SaveRAM`, the name
BizHawk's own title gives it, since Nymashock keeps one raw 131,072 B card; the game found the save
and a new one was made through the codec. Steps 4 and 5 needed code first (RB-332).

| #   | Result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2   | Carried from the multi-disc pass in [index.md](index.md#2-multi-file-games-and-extensions): disc 1 only                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3   | **Pass, needs the file.** Without it EmuHawk reports "Couldn't find required firmware PSX+U" and offers its firmware manager                                                                                                                                                                                                                                                                                                                                                                     |
| 4   | **Pass, class A.** The `.SaveRAM` changed in blocks 0 and 1, the save's file counter moving from `G0006` to `G0007`. The BizHawk rule now covers `psx`; the card was bound to rom 320307 through the state's sidecar and went up in `bizhawk:battery`. Deleted and restored, it came back byte for byte, `e6bdb931...`, into `bizhawk/`                                                                                                                                                          |
| 5   | **Pass, after a fix.** One state, `QuickSave0`, from the pad at ES's slot; EmuHawk writes `emulators/bizhawk/sstates/psx/<title>.Nymashock.QuickSave0.State` and the launcher mirrors it to `saves/psx/bizhawk/sstates/Nymashock/<disc 1>.QuickSave0.State`. The first restore named it after the `.m3u`, `Metal Gear Solid (USA).QuickSave0.State`, with the right bytes. Fixed, it came back under disc 1's name byte for byte, `1c31d1d0...`. The frame is `Framebuffer.bmp` inside the state |
| 6   | N/A: BizHawk exposes no memory card option                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 7   | Carried: MGS's entry and media were checked in the multi-disc pass                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see [the table in index.md](index.md#step-8-every-row)                                                                                                                                                                                                                                                                                                                                                                                                         |
| 9   | **Pass.** After the second restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                                                                                                                                                                                                                                                         |

## `bizhawk`/`Octoshock`

Selected with `psx.core` `Octoshock`, set with ES closed, and confirmed from `emulatorLauncher.log`.
It shares Nymashock's `.SaveRAM` and **read Nymashock's 131,072 B card**: the game found the save,
and the codec save moved the counter to `G0008`. Octoshock wrote the file back at 262,144 B, the card
followed by 128 KB that holds no card, as it did on 2026-09-23. The previous file went to
`.SaveRAM.bak`, which the rule passes over.

| #   | Result                                                                                                                                                                                               |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass in [index.md](index.md#2-multi-file-games-and-extensions): disc 1 only                                                                                                                                                  |
| 3   | **Pass, needs the file.** Without it EmuHawk reports "Couldn't find required firmware PSX+U"                                                                                                         |
| 4   | **Pass, class A.** Sent as save 518 in `bizhawk:battery`, one slot with Nymashock's. Deleted and restored, it came back byte for byte, `a5110bae...`, the newest of saves 517 and 518                |
| 5   | **Pass.** `QuickSave0` mirrored to `saves/psx/bizhawk/sstates/Octoshock/<disc 1>.QuickSave0.State`, 1,069,352 B. Deleted and restored, it came back under disc 1's name byte for byte, `6cdaac6a...` |
| 6   | N/A: BizHawk exposes no memory card option                                                                                                                                                           |
| 7   | Carried: MGS's entry and media were checked in the multi-disc pass                                                                                                                                   |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see [the table in index.md](index.md#step-8-every-row)                                                                                                             |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                    |
