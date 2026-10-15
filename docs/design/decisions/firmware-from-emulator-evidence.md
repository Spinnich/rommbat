---
summary: What firmware a sync fetches, where it lands, and how competing files are picked, from RetroBat's list and retrobios's emulator evidence.
read-when: Changing the BIOS manifest, the firmware join, the ranked pick, upgrades, or what a bios report counts as identified.
---

# Firmware comes from every emulator a system offers, ranked

**A sync fetches the union of the firmware every emulator and core RetroBat ships for a system
reads**, not only what RetroBat's `batocera-systems.json` lists. That list covers each system's
default emulator alone (RB-317), so a row RetroBat does not run by default can need a file no
list names. `data/retrobat/bios.json` is the union, built by `tools/build-bios-manifest.py`.

## Two authorities, split by question

- **RetroBat decides where a file goes**, because emulatorlauncher sets each emulator's firmware
  directory. A file RetroBat lists keeps RetroBat's path. Any other file lands in the directory its
  emulator reads, which `data/retrobat/firmware-rows.json` cites to an `RB-` fact: `bios\` for
  `libretro` (RB-317) and BizHawk (RB-317, RB-322), `emulators\mesen\Firmware\` for Mesen (RB-317).
  A standalone emulator with no cited directory gets no firmware slots; what it reads is reported.
- **retrobios's cited emulator source decides which files and hashes each emulator accepts.** Its
  profiles, vendored at a pinned commit under `reference/retrobios/`, cite the emulator's code by
  file and line. A multi-system profile contributes only the files for the folder's own systems,
  through the hand-maintained map in `firmware-rows.json`.
- **A path several folders name is one file**, so every folder's firmware slot on it accepts the
  union of what any of them accepts, members included. `ATARIOSB.ROM` serves `atari5200` and
  `atari800`, and a pass acts on a path once, so two slots that disagreed would let the first
  folder decide for the second.
- **A boot measured on our tree overrides both**, recorded as a `SUPPLEMENT` in the builder with
  its evidence (`gb` takes `sgb`'s four files and `gbc`'s boot ROM, RB-293).

## A file needs an identity to be fetched

A single file's identity comes only from, in this order: RetroBat's md5; a profile's or BizHawk's
md5, or its sha1 converted through retrobios's `database.json`; an exact dump-catalog name with
exactly one hash; RomM's `known_bios_files` naming exactly one hash. retrobios's by-name index
never counts, since it files the Store Display Unit BIOS under `bios_U.sms`.

**A zip is identified by its members, not its container md5**, because a container's md5 depends
on how it was packed (RM-28). A same-named zip in RomM is downloaded aside, and lands only if every
member some reader lists matches by crc32 and size; otherwise it is discarded and reported.

A file with no identity is reported as "read by X, no identity" and never fetched. So is a path
outside `bios/`, a MAME software list, and firmware an emulator installs through its own menu:
Switch NCAs and the PS3's `PS3UPDAT.PUP`. Switch keys under `bios/` are ordinary firmware slots.

## Competing files are ranked, and a tie is reported

When several accepted files compete for one firmware slot, the pick ranks RetroBat's md5 first,
because its ES BIOS check reports the file missing otherwise; then the emulator source's
`priority`, lower first; then the user's region order, `usa,europe,japan,world` unless
`bios --region-order` sets another. A tie left after all three is `Ambiguous`: reported, never
guessed.

## The one overwrite under bios/

**An upgrade replaces only RomMBat's own, unchanged file.** A file is replaced when its
`local_file` row is `Synced`, the disk still holds the md5 recorded, and a better-ranked accepted
file is now in RomM. A file the user placed, or RomMBat adopted, is never touched and carries a
note naming the better file instead. Every other rule of "never overwrite under `bios/`" stands.

## Agents get firmware bytes only from the test RomM server

No tool downloads a retrobios binary. `tools/vendor-retrobios.py` fetches metadata alone, under
retrobios's MIT notice; its firmware files are licensed to no one.
`tools/firmware_guard.py` fails a tree holding any file whose md5 a vendored source or the
manifest names, in `tools/pre-pr.ps1` and on every pull request.

Fetch timing, the budget and eviction are [firmware-budget-and-triggers](firmware-budget-and-triggers.md).
