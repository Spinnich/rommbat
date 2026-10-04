---
summary: The certification record for `gba`: `nosgba`, driven and not certifiable.
read-when: When a result for one of these `gba` rows is needed, or before re-driving one.
---

# gba: `nosgba`: driven, and not certifiable

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | `gba.emulator = nosgba`                                           |
| Confirmed by | `no$gba.exe /f "<rom>.zip"` on the ES launch line                 |
| Result       | **Not certifiable on this install**: steps 2, 4 and 5 cannot pass |

**NO\$GBA reads a zipped ROM only through the bare `.gba` beside it.** Launched on the zip alone it
shows "Cartridge not found"; with `<rom>.gba` beside the zip the same launch boots. By the
maintainer's ruling that `.gba` was placed by hand, and the maintainer's ES session loaded the save
and played. **NO\$GBA then deletes that `.gba` itself**: present before each of two launches and
gone after, so every launch after the first fails again. It unzips by running an external
`PKUNZIP.EXE` (its loader prints "LOADING PKUNZIP..."), which its folder does not hold, so it takes
a `.gba` named like the zip for PKUNZIP's output and deletes it as a temp file. Run by hand with
`emulatorLauncher` not involved, it deleted the file while still running. Handed a bare `.gba`
path, it keeps the file. `NosGbaGenerator` passes the zip through and never extracts it. A sync
cannot make this row work, since RomMBat places the ROM RomM serves and never the file inside it.
Reported upstream as
[emulatorlauncher#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377).

**Its saves live outside `saves/`**, in `emulators/nosgba/BATTERY/<rom>.SAV`, as Kega Fusion's do
on `megadrive` (RB-283). NO\$GBA read the raw 131,072 B seed and wrote it back in its own
compressed format, 3,583 B headed `NocashGbaBackup`, and rewrote it on a later launch with no save
made. **No state can be synced**: `F8` is NO\$GBA's Write Snapshot, and it opens a Save As dialog
in the user's `Documents` folder rather than writing anywhere fixed, so where a state lands is the
user's choice each time and no rule can find it. The dialog was cancelled. There is no pad mapping
for it either. **Its pad maps Start and Select differently from every other
row**, as the maintainer found: `NO$GBA.INI` numbers them 3 and 4, and `emulatorLauncher` writes no
mapping for it. RB-286.
