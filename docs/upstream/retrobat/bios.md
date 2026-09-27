---
summary: What RetroBat ships for firmware and who owns `bios/`.
read-when: Before fetching or placing firmware.
---

# RetroBat: BIOS

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-379. There is no `batocera-systems.json` either (M5)

[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/62552faa4/docs/PLAN.md) said the BIOS requirements manifest was "present in the tree". A recursive
search of a real 8.2.0 install finds **no file by that name at all**. The data ships as a
.NET string resource named `batocera_systems` inside
`emulationstation/batocera-systems.exe`, 40,644 bytes at offset 7,250 of a 50,688-byte
executable, and it is `reference/batocera-systems.json` **byte for byte apart from a trailing
newline** (99 systems, 353 entries, no system added, removed or changed).

So the `es_systems.cfg` precedent, read the live copy and treat the vendored one as a
template, cannot apply: there is no live copy to read, and prising a string out of an
executable at runtime would bind RomMBat to one build's resource layout. The manifest is
bundled instead. `tools/m5-probes/m5-probe1-manifest-in-install.py` re-derives all of this.

## RB-380. `bios/` is a shared tree, and RomMBat owns almost none of it (M5)

Before RomMBat writes anything, `bios/` on a real install holds **4,683 files and 373 MB**,
nearly all of it emulator data rather than firmware:

| Under `bios/`      | Files |
| ------------------ | ----- |
| `dolphin-emu/`     | 2,508 |
| `mame/`            | 858   |
| `nxengine/`        | 436   |
| `Machines/`        | 296   |
| `scummvm/`         | 208   |
| `PPSSPP/`          | 167   |
| `dinothawr/`       | 144   |
| flat at `bios/`    | 6     |
| 15 more subfolders | 60    |

Exactly **3** of those files sit at a path `batocera-systems.json` names carrying the md5 it
names, and **none** sits at a named path with a different md5. `bios/mame/hash` alone holds
776 software-list XML files, already recorded above as metadata rather than firmware, and
openMSX keeps its whole user-data directory here, save states included. Nothing in this tree
is RomMBat's to remove.
