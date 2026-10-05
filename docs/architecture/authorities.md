---
summary: Why file extensions and firmware requirements come from RetroBat, and why neither gates a sync.
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

**Firmware requirements come from RetroBat too.** `batocera-systems.json` gives 355 BIOS
entries across 100 systems as `{md5, file}`, with the exact destination path. Join it
against RomM's firmware records on **md5 only**: filenames differ between the two projects,
and RomM's `is_verified` is false on files RetroBat requires, `psxonpsp660.bin` among them,
so on a real library filtering on it discards 6 of the 49 required hashes that library
holds. A further 93 of the 156 have no RomM record at all, which is a gap and not a
flag. BIOS is fetched
**before** that platform's ROMs, because on an emulator that needs it a platform without its
BIOS is dead weight in the gallery. **It never gates a platform.** The list has no optional
flag and the emulator decides which files it reads, so a file RomM lacks is reported and the
platform's ROMs sync regardless. Where a row reads a file the list files under another system,
the manifest builder copies that entry onto the system with a `supplement` note saying why: `gb`
takes `sgb`'s four Super Game Boy files and `gbc`'s boot ROM (RB-293). Every hash is still
RetroBat's.

Two shapes follow from measuring it. **RetroBat does not ship that file**, only a copy of it
inside `batocera-systems.exe`, so the manifest is bundled at `data/retrobat/bios.json` rather
than read from the install. And **181 of the 355 entries carry no md5**, so a BIOS report has
three states rather than two: matched, missing from the library, and unverifiable because
RetroBat names no hash. A zip is still joined on md5, which both sides take over the container
(RM-28), so only a byte-identical build matches; a miss where the library holds a zip under the
exact name says so and fetches nothing, since neither side says what is inside. A hashless
requirement not on disk is pointed at a library file under its exact name the same way, and
stays unverifiable. `bios/` is otherwise a tree RomMBat does not own, holding thousands of
files of emulator user data, so nothing there is overwritten or deleted.
