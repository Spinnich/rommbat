---
summary: Firmware counts against the budget and is never evicted, and what starts a BIOS pass.
read-when: Changing how BIOS is budgeted, evicted, or triggered by sync and the bios command.
---

# Firmware is budgeted, never evicted, and fetched before ROMs

**Budget and eviction.** Firmware counts against `content.max_bytes`, so `status` and
`budget` tell the truth about what RomMBat put on the disk, and is **never evicted**. Every
file the measured library can serve totals **18.5 MiB**, against roughly 570 MB of media for
a single 100-game set, so evicting firmware would free nothing measurable while leaving a
platform unable to boot.

**What triggers a pass.** Both: `sync` fetches a platform's BIOS before its ROMs, and a
`bios` command in the shape of `budget` and `evict` reports on its own, writing nothing
without `--apply` and answering with `--offline` too. `--dry-run` is `sync`'s flag and
belongs to no other command: previewing is the default here and writing is the opt-in. The whole-library report is one request, which is what makes the
standalone command cheap.
