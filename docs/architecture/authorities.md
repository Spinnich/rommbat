---
summary: Why file extensions come from RetroBat, why firmware joins RetroBat to retrobios's emulator evidence, and why neither gates a sync.
read-when: Before changing extension handling, the BIOS manifest or the firmware join.
---

# Two authorities that are easy to get backwards

**File extensions come from RetroBat, and they never gate a sync.** The `<extension>` list in
the live `es_systems.cfg` is a union across every emulator a system offers, so it cannot say
whether the emulator that runs opens a file: one reads `.chd` and another does not, and the
list names the format either way. The one certain thing it says is what EmulationStation
lists, so members it omits sync anyway and are reported as unlisted, on the resolution summary
and on the set's detail. What does gate a game is its shape: a multi-file ROM, or one RomM holds
as a folder, waits until its platform's certification has settled where RetroBat wants it.

**Firmware has two authorities, split by question.** RetroBat's `batocera-systems.json` gives
360 entries across 101 systems as `{md5, file}`, with the exact destination path, for each
system's default emulator alone (RB-317). retrobios's emulator profiles, vendored under
`reference/retrobios/`, say which files and hashes every other emulator RetroBat ships accepts,
cited to its source. The bundled manifest at `data/retrobat/bios.json` is their union, as
firmware slots: RetroBat decides where a file goes and its md5 ranks first, and the profiles
decide what else is accepted and who reads it. A boot measured on our tree overrides both, as a
`supplement` (`gb` takes `sgb`'s four Super Game Boy files and `gbc`'s boot ROM, RB-293).
[firmware-from-emulator-evidence](../design/decisions/firmware-from-emulator-evidence.md) is the
record.

A single file joins RomM's firmware records on **md5 only**: filenames differ between the
projects, and RomM's `is_verified` is false on files RetroBat requires, `psxonpsp660.bin` among
them, so on a real library filtering on it discards 6 of the 49 required hashes that library
holds. A file's identity is a hash a source states, never its name alone. A zip is identified by
its members' crc32 and size, since both sides hash its container (RM-28). Where several accepted
files compete for one slot the pick is ranked, and a tie is reported as `Ambiguous`, never
guessed. BIOS is fetched **before** a platform's ROMs, because on an emulator that needs it a
platform without its BIOS is dead weight in the gallery. **It never gates a platform.** The
sources say which emulator reads a file, not which one the user runs, so a file RomM lacks is
reported and the platform's ROMs sync regardless.

Two shapes follow from measuring it. **RetroBat does not ship its list as a file**, only a copy
inside `batocera-systems.exe` (RB-379), so the manifest is bundled rather than read from the
install. And **71 of the 943 firmware slots have no identity**: RetroBat names them without a
hash and no other source identifies them, so a BIOS report has three states rather than two:
matched, missing from the library, and unverifiable. A file read by some emulator that no
source identifies is reported and never slotted. `bios/` is otherwise a tree RomMBat does not
own, holding thousands of files of emulator user data, so nothing there is deleted, and the one
overwrite is an upgrade of RomMBat's own unchanged download.
