# Certification passes

Part of the [platform-certification](SKILL.md) skill. What each system certified so far taught, in the order the passes ran.

## When to run this

**All nine `nes` rows are through, and the first is the model for the rest.** `nes` under
`libretro`/`nestopia` on 2026-09-20, then the other two `libretro` cores, both `bizhawk` cores,
`jgenesis`, `mesen`, `mednafen` and `ares` on 2026-09-21, at RomM `5.3.0-beta.1` and RetroBat
8.2.1, all nine steps with step 6 N/A, carried to `5.3.0` with step 9 re-run, and to the `5.3.1` floor with steps 1 and 9 re-run. The two later `libretro` passes took under an hour between
them, which is what a row costs once steps 1, 2 and 3 carry. The last four needed code first: a
battery rule each and, for three, a state declaration in RomMBat's bundled supplement, which is
what a row outside `es_savestates.cfg` will need on every other system too. Read
`docs/platforms/nes/` before starting a pass: it is the only worked example of the whole
checklist, and it carries the two traps that cost the most time, the screenshot byte check at
step 5 and the RomM-side device id at step 8. That pass is what opened #208, and `status` now
reads the sessions back, so step 8 no longer needs the token that record describes.

**`megadrive` is second: seven of its eleven rows certified at `5.3.0` on 2026-09-21**, and
`docs/platforms/megadrive/` is the model for a system whose matrix does not all pass. The three
`libretro` cores that boot the library, `bizhawk`, `jgenesis`, `mednafen` and `ares`, the last four
on a build carrying megadrive rules. `libretro`/`fbneo` and the three `kega-fusion` rows were
driven and are recorded as not certifiable, each with its reason, which is a result and not a
gap. The whole system took one evening, eleven ES sessions and the agent's keyboard launches.

**`gba` is third: nine of its ten rows certified at `5.3.0` on 2026-09-22**, in one morning,
because **each row after the first was seeded with the save the one before made** rather than
played through the intro again: copy the save to where the next emulator looks, launch, save in
the game, so the file measured is still that emulator's. A seed an emulator refuses is a finding,
not a failed pass: mednafen refused mGBA's 131,088 B file (RB-289). **Put an override's
`gba.emulator` in `es_settings.cfg` only with ES closed**, and restore the file from a copy taken
first. **A clock file is a second save file**, class B, and changes on every launch (RB-291).
**An emulator can delete what you place in `roms/`**: NO$GBA took a bare `.gba` put beside its zip
for its own unzip output and deleted it (RB-286), so check a hand-placed file is still there
before each launch. Blame `emulatorLauncher` only once the emulator run by hand keeps the file.

**`gb` is fourth: all fourteen rows certified at `5.3.0` on 2026-09-22**, in one afternoon, on
the same seeding. Six `libretro` cores and Mesen share the loose `.srm`, so those seven needed no
copying. **A boot write is not recognisably blank on every system** (RB-294): Mesen flushes
random bytes, and a game that uses cartridge RAM as scratch space leaves real-looking ones, so
move every boot write out before a flush. **Bring in a clock cartridge if the set has none**:
a filter set with `--folder` put Pokemon Silver into `gb`, and it showed where each row keeps a
real clock, which a clockless game cannot (RB-298). **BizHawk's pad key
follows ES's `-state_slot`, which moves up as states accumulate**, and a `Ctrl+F<n>` sent by
`keybd_event` needs the keys held about 400 ms.

**`gbc` is fifth: all twelve rows certified at `5.3.0` on 2026-09-23**, in one morning, on Pokemon
Crystal, a clock cartridge, so every row showed its clock. **A clock does not survive a change of
row**, because each emulator keeps it in its own format (RB-300): a seed carries the save, and
the game then shows a wrong time or asks for one. That is the emulators, not a failed pass, so
record what the game did and keep going. **One row can split its files across two trees**: ares
on `gbc` saves its battery into `ares/Game Boy` and its states into `ares/Game Boy Color`, so find
each half before declaring either. ares writes on exit only and can outlast a 15 s wait on
`WM_CLOSE`, so end an agent launch with its `QuitEmulator` key, `Esc`, to see where the battery
save goes. **On the
RetroBat machine `Ctrl+F1` never reached EmuHawk from `keybd_event`**, where `Ctrl+F2` and
`Ctrl+F4` always did, so take BizHawk's two slots on those. **ES rewrites `gamelist.xml` when it
quits**, adding `playcount` and moving the entry, so step 9 compares against a copy taken after
the last ES session.

**`snes` is sixth: all fifteen rows certified at `5.3.0` on 2026-09-24**, in one morning, on Zelda: A
Link to the Past, with `docs/platforms/snes/` the record. **Boot a coprocessor game for step 3**,
not only the test game: RetroBat lists no `snes` firmware, and on Super Mario Kart, a DSP-1
cartridge, three rows refused for want of `dsp1b.rom` while Zelda booted everywhere (RB-317).
A title screen does not exercise the chip, so claim no more than the title. **A row whose emulator
is not installed asks to install it on its first launch**, standalone Snes9x here; the maintainer
decides whether to accept. **ares shows nothing when a state is saved or its slot steps**, so the
agent drives ares's states from its own session and checks the files, rather than asking for keys
pressed blind. **The server may already hold the test game's save**, from another client: the first
flush then records a conflict rather than overwrite, and which side wins is the maintainer's call.

**`mastersystem` is seventh, and closes wave 1: nine of its ten rows certified**, seven at `5.3.0`
on 2026-09-24, in one day, on Golden Axe Warrior, and both Kega Fusion rows at `5.3.1` on
2026-10-01 once a rule read their `.ssm` from Kega's folder (#381), with
`docs/platforms/mastersystem/` the record. FBNeo is recorded as not certifiable for `megadrive`'s
reason. **Pick a
test game whose save point you know.** Golden Axe Warrior commits its save once, at the first
character, and after that rewrites only a working copy, so every later row's file changed without
new progress (RB-326); diff the SRAM against the boot write before trusting "a save was made".
**Mesen rewrites its save only when the SRAM changes**, so an unchanged file after a session means
no save was made, not that Mesen writes elsewhere. **A seed can block the next row**: mednafen
refuses to start while another emulator's smaller plain save sits beside the ROM (RB-324), so
hold it out for mednafen's row and put it back after. **Keep RomM's web player closed during a
pass**: a launch there writes a save with no device, which the next flush takes as the newer
(RB-327).

**`psx` opens wave 2: all seven rows certified at `5.3.1` on 2026-09-26**, with
`docs/platforms/psx/` the record. **Step 6 is the long step on a memory card system**: drive every
card type the rows expose, boot each through `emulatorLauncher` first to learn the file it writes,
seed it with the game's save, and uninstall the hooks for the sessions so nothing flushes before the
scan has been read. A shared card needs two games saving to it, and a card type change leaves two
files for one game in one slot (RB-333). **Pick a test game
that saves early, and know where.** SotN's first save is at the first save room, well into the
castle, and creating the name writes nothing; Metal Gear Solid saves only through Mei Ling, 140.96,
after the opening. Seed every later row from that save. **A `libretro` core writes a formatted empty
card on exit whether or not the game saved** (RB-328), so read the card's directory frames
before believing a save was made. **Three rows cannot open `.chd`**, standalone mednafen and both
BizHawk cores, so keep a `.bin`/`.cue` set in the pass. **Check the row's card option before
playing**: standalone mednafen has no card at RetroBat's default (330). **A restored DuckStation
state loads only from ES's save-state menu**, which passes `-state_file`; a plain launch copies
nothing back into DuckStation's own directory. **BizHawk names everything after disc 1 of a set**,
so attribution and a restore both have to know the set's discs (332).

**`n64` is second in wave 2: eight of nine rows certified at `5.3.1` on 2026-09-27**, in one
morning, and gopher64 on 2026-09-29 once #239 read its folder, with `docs/platforms/n64/` the
record. **A Mario Kart 64 ghost reaches the pak only when the game saves it**: a lap writes the
EEPROM, and gopher64 rewrites the `.mpk` with the same bytes on every access, so check the hash. **Pick one game per save medium**: Ocarina of Time
covers SRAM, and Mario Kart 64 covers EEPROM plus a Controller Pak ghost, which is step 6. **Boot
every row once before playing and list what it writes where**: four of the nine name their files
with something other than the ROM (an emulator's title, a header name and an md5, a directory per
game), and that list decides the code the pass needs. **A seed is a format conversion on `n64`**:
the same SRAM is word-swapped in one row and big-endian in the next, and sits at a different offset
in each 296,960 B image (RB-338), so convert rather than copy, and confirm the game loads it.
**Four rows have no Controller Pak at RetroBat's default** (339): check the pak option before asking
for a pak save, and record both the default and the set value. **Read an emulator's keys from what
RetroBat writes**, not from its own defaults: Project64 saves on F2 under RetroBat's
`Project64.sc3` and reaches no slot but 0 (337). **An emulator can keep its saves outside `saves/`
with no mirror**, as gopher64 does (341). A battery rule can read it there with `from_root`, which
needs a migration admitting the folder to `local_save` (#239); until one lands, the row is recorded,
not certified.

**Three things `megadrive` taught that transfer.** An emulator lays out its tree per system, not per
emulator: `jgenesis` and `ares` name their save directory after their own name for the console
(`jgenesis/md`, `ares/Mega Drive`), so a rule measured on `nes` says nothing about the next
system's path. An emulator can write outside `saves/`: Kega Fusion's battery saves go where
RetroBat's `Fusion.ini` sends them, `emulators/kega-fusion/`, and that is a RetroBat defect to
report (RB-283, emulatorlauncher#1390). Whether RomMBat reads such a folder meanwhile is the
maintainer's call per emulator, because each one costs a migration: gopher64's is read (#239), and
Kega Fusion's only for `mastersystem`'s `.ssm`, which upstream's fix leaves there, as it does
`megacd`'s `.brm` (#381).
Every issue raised
upstream is tracked in `docs/upstream/issues.md` until RomMBat adopts the release that fixes it.
And a core can refuse the library for its names: FBNeo takes a console game's driver from the file
name, so it boots nothing named by No-Intro (RB-278). **An emulator absent from `emulators/`**
is installed by ES on the first launch under it, so check the folder holds an executable before
planning its rows.
