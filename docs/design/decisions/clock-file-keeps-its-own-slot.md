---
summary: A battery save's clock file stays one slot across formats, and moves into a bundle only when RomM ships bundled units.
read-when: Changing class B slots, the rtc slot, or adopting a RomM save-bundle API.
---

# A clock file keeps its own slot until RomM bundles saves

**Amended by the `gbc` pass, 2026-09-23: a clock file stays one slot even where its format is not
shared.** On `gbc` the loose `<rom>.rtc` is written by four `libretro` cores and Mesen in four
formats (8 B of base time under `gambatte`, 4 B of host time under `tgbdual` and `DoubleCherryGB`,
32 B under `sameboy`, 13 B under Mesen), and every move between them that was observed lost the clock (finding 300). Each row's
own clock round-trips, so a device that stays on one row keeps it. Splitting the slot by core
would need a restore to know which core a single file on disk belongs to, and a device that
switches core can lose the clock on one machine with no RomMBat involved. By the maintainer's ruling
the slot stays `libretro:battery:rtc`, the loss is recorded, and nothing converts a clock. Bundling
the clock with its save was weighed at the same time and not taken: it would make the save
unreadable to RomM's other clients and re-upload the whole save on every launch that moves the
clock.

**Revisited 2026-09-23: bundling is where this goes, on RomM's timetable rather than ours.** RomM's
maintainers are drafting an overhaul of save sync that moves a save and its companion files as one
unit, with the clock excluded from change detection. No work on it has started. Both objections
above belong to today's API: the web player cannot open a bundle, and the server has no way to
leave the clock out of a hash. So the clock keeps its own slot until RomM ships bundled units, and
RomMBat moves the clock into the unit when it adopts that API, not before. Bundling early would
break the web player now and still leave the migration to guess the bundle's shape.
