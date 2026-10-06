# Attribution and hashing

Part of the [save-sync](SKILL.md) skill. Which ROM a save belongs to, and what gets hashed.

## Attribution

Class A and B match by filename, **and which filename is a per-`(system, emulator)` rule, not a
global.** `save_rules.json`'s `battery_saves` gives each rule a directory under
`saves/<system>/`, its extensions and a `named_after`, and the emulator in it is the slot.
Loading refuses two rules claiming one extension in one directory, and one emulator with two
rules on a system, because either is two saves in one slot. The one exception to the second is
class B: an emulator may hold two rules on a system when one is class B and no extension is in
both, because then no two files share a slot. `gba` is the measured case, `libretro`'s `.srm`
beside mednafen_gba's `libretro:battery:sav`. Both class B with different `slot_qualifier`s
also passes, since the qualifier sits in the slot: `snes`'s two `libretro` `.rtc` rules. The table replaced one extension
list plus one `loose_emulator`, which is the trap #152 recorded: adding mesen's loose `.sav`
would have given it `libretro:battery` and collided with libretro's `.srm` for the same ROM.
On `nes`, jgenesis (`jgenesis/nes/`), mesen standalone (loose `.sav`), mednafen and ares
(`ares/Famicom/*.ram`) each have one since, all measured there and scoped to it. On `megadrive`,
jgenesis (`jgenesis/md/`) and ares (`ares/Mega Drive/*.ram`) have their own rules, since the
directory is the emulator's name for the system, and the bizhawk and mednafen rules name both
systems because their layout did not change (RB-279 to RB-282). On `sega32x`, ares keeps a
cartridge's `.ram` or `.eeprom` in `ares/Mega 32X/` and its states in the parent's
`ares/Mega Drive/`, and jgenesis saves to `jgenesis/32x/` (RB-413).

**mednafen names a save `<rom>.<md5>.sav` only when `<rom>.sav` is absent** (RB-273): its `%M`
is empty on the first try, so an existing plain `.sav`, mesen's included, is the file it reads and
writes. The hash is the system's: on `nes` the `.nes` less its 16-byte iNES header (RB-274), on
`megadrive` the whole `.md` (RB-280), and `MednafenRomHash` picks by system and answers null
for any system or format not measured. So mednafen's rule is
`named_after: "rom file and content md5"`, which may share an extension in one directory with a
plain rule, the hash on the stem deciding. On `nes`, a plain `<rom>.sav` goes up as
`mesen:battery` whoever wrote it, and a restore computes the hash from the ROM and refuses to write
a hashed save where a plain one would shadow it, including onto a path this device recorded before
the plain one appeared. `HeaderlessNesHash` answers null outside what was measured: a trainer, a
length other than header plus declared PRG and CHR, no PRG, or NES 2.0 size bits in byte 9. **Do
not refuse NES 2.0 as such**: all 232 ROMs on the test install carry a NES 2.0 header with byte 9
clear, the three measured ones included.

`named_after: "archive member and content md5"` is narrower still,
`<rom>.zip#<member>.<md5>.sav` for mednafen_gba (RB-290), and the loader asks rules narrowest
first and refuses two of one narrowness. The ROM's own name may hold a `#`, so the match anchors
on the first `.zip#` or `.7z#`. `named_after: "archive member"` drops the hash,
`<rom>.zip#<member>.rtc` for bsnes-jg on `snes`, and ranks between the two; a restore names it
from a zip holding one file. On `gba` that is three owners for one loose `.sav` extension:
mednafen_gba's `#` name, mednafen's hashed one, and the plain one, which mGBA, Mesen and mednafen
all open and which uploads as `mgba:battery`. **mednafen refuses mGBA's 131,088 B file** (finding
289), so a device where mGBA standalone ran cannot play the game under mednafen until it moves;
the hash is the whole `.gba` there.

**A clock beside a save is class B.** Mesen, jgenesis and ares keep a cartridge's real-time clock
in `<rom>.rtc` next to the save, and each gets `{emulator}:battery:rtc`, so the clock travels
with it. Mesen's, jgenesis's and ares's change on every launch (RB-291 and RB-300), so a
session uploads a version whether or not the game was saved. mGBA and BizHawk keep the clock
inside the save, 16 bytes on `gba`, and on `gbc` 48 under mGBA and BizHawk's SameBoy and 22 under
its Gambatte, and those change on every launch the same way.

**On `gb` the loose `.rtc` is `libretro`'s, `libretro:battery:rtc`.** A clock cartridge keeps its
clock there under the stock `gambatte` core, and Mesen writes the same name, as it does the `.srm`
(RB-298). `tgbdual`, `DoubleCherryGB` and `sameboy` write it for every game, and with no clock
on the cartridge it holds only the host time at exit (RB-295), so those three upload a few
bytes of new version per launch. Measure it on a clock cartridge, not a clockless one: on a
clockless game the file looks like noise.

**On `gbc` the loose `.rtc` is `libretro`'s too, and it is one name with four formats**: 8 B of
base time under `gambatte`, 4 B of host time under `tgbdual` and `DoubleCherryGB`, 32 B under
`sameboy` and 13 B under Mesen (RB-300). Each row reads its own back, so a device that stays
on one row keeps its clock; a device that switches row can lose it, with or without RomMBat, and every move observed did. By the
maintainer's ruling the slot stays one, because on disk it is one file, and nothing converts a
clock between formats. ares and jgenesis keep theirs apart, as `ares:battery:rtc` and
`jgenesis:battery:rtc`, and ares's battery save sits in `ares/Game Boy` while its states sit in
`ares/Game Boy Color`. On `gb` ares's `.rtc` has a rule of its own beside the class A `.ram`, as
`libretro`'s does beside the `.srm`, so the `.ram` kept the slot it already had (RB-302).

**A clock slot is a stopgap until RomM can move a save as one unit.** RomM's maintainers are
drafting a save-sync overhaul that bundles a save with its companion files and leaves the clock out
of change detection; no work on it has started. Do not bundle `.srm` and `.rtc` against today's
API, since the web player cannot open a bundle. Move the clock into the unit when RomMBat adopts
that API ([decision](../../../docs/design/decisions/clock-file-keeps-its-own-slot.md)).

**On `gb` the shared files are two**: the loose `<rom>.srm` six `libretro` cores and Mesen write,
as `libretro:battery`, and the loose `<rom>.sav` mGBA and mednafen write, as `mgba:battery`, with
mednafen's hashed name taken only when no plain one is there. BizHawk's three cores share one
`.SaveRAM` named after BizHawk's own title, which on `gb` is not the ROM file's.

**On `snes` the loose `<rom>.srm` is shared by ten rows**: the seven `libretro` cores, Mesen,
standalone Snes9x, and mednafen, and uploads as `libretro:battery`. mednafen reads and saves into
it when it is there and otherwise writes `<rom>.<md5>.srm`, a `.srm` and not the `.sav` of its
other systems, the md5 being of the whole `.sfc` (RB-318), which uploads as
`mednafen:battery`. BizHawk's `.SaveRAM` is named after the ROM file on `snes`, and ares keeps a
DSP-1 cartridge's data RAM as `<rom>.dram` beside the `.ram`, class B, `ares:battery:dram`.
ares keeps an SA-1 cartridge's internal RAM as `<rom>.iram`, `ares:battery:iram` (RB-418).
**An S-RTC cartridge's clock is a second file on eight rows**, each in its own class B slot beside
a save that keeps the slot it had: the loose `<rom>.rtc` the cores and standalone Snes9x write, as
`libretro:battery:rtc`, mednafen's `<rom>.<md5>.rtc` as `mednafen:battery:rtc`, and ares's and
jgenesis's as `ares:battery:rtc` and `jgenesis:battery:rtc`. `libretro`/`bsnes-jg` names its clock
`<rom>.zip#<member>.rtc`, a 16 B clock beside the cores' 20 B `<rom>.rtc`, which takes
`libretro:battery:member.rtc` through the rule's `slot_qualifier`, and BizHawk's Snes9x core appends the clock to the
shared `.SaveRAM` (RB-417). `libretro`/`mednafen_snes` leaves an empty `<rom>.rtc` for a game with
no clock, and `empty_not_a_save` in `save_rules.json` passes over an empty one per system and
extension (RB-319). Zelda writes
its SRAM at boot, so any launch uploads a new `.srm` version whether or not the game was saved.

**On `mastersystem` only the two `libretro` cores share the loose `<rom>.srm`**, Genesis Plus GX
trimming it to the last used byte and PicoDrive keeping 32 KB. Mesen writes its own loose
`<rom>.sav`, `mesen:battery`, and rewrites it only when the SRAM changes; mednafen writes
`<rom>.<md5>.sav`, the md5 of the whole `.sms`, and **will not start the game while Mesen's 8 KB
`.sav` is beside the ROM**, since it opens the plain name and expects 32 KB (RB-324). A restore
that brings Mesen's save back therefore stops mednafen for that game, which is the emulators and not
something to route around. BizHawk names its `.SaveRAM` after its own title, `Golden Axe Warrior
(UE)`, bound through the state sidecar. Kega Fusion's `.ssm` is read where `Fusion.ini` leaves
it, in `emulators/kega-fusion/` (RB-283, 323, #381). A game can commit its save once and afterwards rewrite only a working copy, as Golden Axe
Warrior does (RB-326), so on such a game a changed file is not new progress.

**On `psx` a `libretro` core writes a formatted, empty memory card on exit whether or not the game
saved** (RB-328): a 128 KB loose `<rom>.srm` whose fifteen directory frames are all `0xA0`,
never used. It went up as the game's save until `Ps1MemoryCard.IsBlank` made the scanner pass over
it, at the loose level and under an emulator's own directory alike, and made a restore treat it as
absent, so a second device's first boot neither uploads it nor blocks the server's save. The test
reads the format, not the system or extension. A card whose saves were deleted in the game marks
those frames `0xA1` to `0xA3` and still syncs. A download refuses a blank card too (RB-334),
or a slot the scan leaves empty fetches the server's blank copy back on every restore.

**A battery file of nothing but `0xFF` is treated the same way, under every rule on every
system** (`ErasedSave`): ares writes a 32 KB one on `gamegear` for every cartridge, battery or not
(RB-408). `0xFF` is erased SRAM, flash and EEPROM, so a save a game keeps never looks like it.
The accepted cost, by the maintainer's decision, is a game that erases its own save: the
wiped file stays local, and `saves restore` offers the server's last save back, moving the erased
file aside first. An empty file is a different question, settled per extension by
`empty_not_a_save`. **A file of nothing but `0x00` is a save**, by the maintainer's decision on
2026-10-05: BizHawk's PicoDrive writes one at boot for every `sega32x` cartridge, Doom's 16 KB
though it has no battery, and it uploads, because zero is not an erased state (RB-414).

**DuckStation's per-game cards are a display-name rule with a slot per port** (RB-329):
`duckstation/memcards/<saveName>_1.mcd` uploads as `duckstation:battery` and `_2.mcd` as
`duckstation:battery:2`, from the rule's `stem_suffixes`. The title is the stem less the suffix, so
one title names both cards: a restore places card 2 under the title card 1 taught, and the next
scan attributes it through card 1's binding rather than leaving it unattributed. `saveName` comes
from `gamedb.yaml` by serial and is not the rom's stem in general (`Metal Gear Solid (USA)` for the
`(Rev 1)` rom), so it is learned from the launch route; DuckStation's state sidecar is a bare serial
and answers nothing here. A card with no `_<port>` suffix is not claimed.
**A card type change leaves two files for one game in one slot** (RB-333). The flush sends the
file written most recently and reports the rest as superseded, and `LearnedTitle` picks the title
whose file is newest for a rule with `stem_suffixes`. DuckStation's `shared_card_<n>.mcd` names are
declared shared containers, and `ScanBelow` asks that list before any rule claims a file, since
`shared_card_1.mcd` matches the per-game `_1`.
**The `libretro` cores' other `psx` cards are class B slots beside the `.srm`** (RB-335):
swanstation's loose `<serial or title>_1.mcd` and `_2.mcd` take `libretro:battery:mcd` and `:mcd2`,
and mednafen_psx_hw's `<rom>.1.mcr` takes `libretro:battery:mcr`. **Each port can be named its own
way**, so `LearnedTitle` takes the slot, prefers a title learned from the same port, and weighs only
that port's files of that rule. The loose scan attributes a display-name file through the title
routes as `ScanBelow` does.

**Standalone mednafen on `psx` names its cards `<rom>.<layout md5>.<port-1>.mcr`, loose** (finding
331), under a `rom file and content md5` rule with `stem_suffixes` `.0` and `.1`, port 2 taking
`mednafen:battery:2`; the pattern is asked of the stem less the port. The md5 is not a file's: it
is mednafen's hash of every disc's table of contents in playlist order, which
`MednafenRomHash.CdLayout` computes for a `.cue` or a `.m3u` of cues with one file and one data
track each, and answers null for anything else, so such a card is unnameable on restore. **At
RetroBat's default the row emulates no card** (RB-330): `mednafen_psx_memcards` unset writes
every port as `0`, so there is nothing to sync until a user sets it.

**A disc of a set answers for the set** (RB-332). BizHawk on `psx` is handed disc 1 rather than
the playlist, so its states are `<disc 1 stem>.QuickSave<n>.State`. `RomIndex` maps a `RomPart`
file's stem to its set after every ROM, so a ROM of that name keeps it, and never adds a disc to
`InFolder`. A state restore keeps its stem-from-disk rule but takes the sent name's stem when it is
one of the ROM's own disc files. BizHawk's `.SaveRAM` on `psx` is Nymashock's one raw card or
Octoshock's card plus 128 KB, one `bizhawk:battery` slot for both.

**On `n64` four rows name their saves with something other than the ROM** (RB-336 to RB-341).
The two `libretro` cores share a loose 296,960 B `.srm` holding EEPROM, four Controller Paks, SRAM
and FlashRAM, so a pak ghost carries between them. **RMG and simple64 share `sram/<title>-<md5
prefix>.<ext>`**, the title being the first 32 characters of mupen64plus's GoodName, under one
`mupen64` display-name rule whose `also_written_by` names simple64, so either's launch binds the
file; class B, `.sra`, `.eep` and a four-pak `.mpk` each take a slot. **Project64 keeps a directory
per game**, `project64/<header>-<md5 of the ROM in its word order>/`, holding `<header>.sra`,
`.eep` and `<header>_Cont_1.mpk`: `per_game_directory` makes the directory the title and the binding
key `<directory><ext>`, and `extension_stems` adds `_Cont_1` for the pak. **Two display-name rules
carrying `.sra` on one system are kept apart by `title_pattern`**, `-<8 hex>` for mupen64's and
`-<32 hex>` for Project64's, so neither claims the other's binding keys and `LearnedTitle` answers
one title. ares keeps `.ram`, `.eeprom` and `.pak` in `ares/Nintendo 64/`, class B. **BizHawk's two
cores share one `.SaveRAM` in formats neither reads from the other**: `Ares64`'s raw big-endian SRAM,
with the pak appended when one is set, and `Mupen64Plus`'s 296,960 B image with SRAM at `0x40800`;
the slot stays one, and a device changing core loses the other's save locally. **A state can be
named through a battery binding too**: simple64's `state/<title>.st<n>` and Project64's
`project64/sstates/<directory>/<its database's name>.pj.zip` are declared with `titled_by` in the
supplement, and a restore names them with the learned title, or for Project64 keeps the name the
state was sent under. gopher64 and Kega Fusion keep battery saves outside `saves/`, so their rules
set `from_root`: the directory is relative to the RetroBat root, the rule must name exactly one
system, and `SaveShapes.SystemOf` gives a path there that system when the rule also claims the
file's name. That last condition is Kega's: its folder holds `Fusion.exe`, `Fusion.ini` and every
system's `.srm` beside the `mastersystem` `.ssm` its rule reads. Migrations 019 and 020 widen
`local_save`'s CHECK to gopher64's folder and to a `.ssm` directly in Kega's, so a new such rule
needs its own migration (#239, #381).

**The grain is per emulator, decided** by the maintainer on 2026-09-21: libretro's cores share one
battery save, and no save migrates between emulators, even where the bytes happen to load. Do not
split a slot by core or merge two emulators' slots without a new decision. mednafen_gba's
`libretro:battery:sav` is not a split by core: it is a second file, which is what class B's
per-extension slot is for (ruled 2026-09-22).

**BizHawk names a battery save after its own title for the game** (`named_after: display
name`), so the filename join cannot match: `StarTropics (USA).zip` wrote
`bizhawk/StarTropics.SaveRAM`, and `Phantasy Star (Brazil).zip` wrote `Phantasy Star (B).SaveRAM`,
so no stripping rule recovers it. `DisplayNameAttributor` asks two routes every scan: the state
sidecar, which RetroBat writes as `<title>.<core>` (strip only the state's own core, since a title
can hold a dot), and the newest launch of the system **under the same emulator** covering the
mtime. The binding is cached in `game_id_binding` keyed on the **file name**
(`StarTropics.SaveRAM`), because that table's CHECK refuses `/` and `:` in a key.

- **The cache is an answer, not a short cut.** A title is not unique to a ROM, and if two
  regions share one, BizHawk keeps one file for both. Re-asking the routes is what lets a launch
  of the second ROM disagree with the binding the first taught, which fails closed as contested
  until `saves bind` settles it. A binding a person made is honoured as the settlement.
- **Two sidecars naming one title for two ROMs is contested too, not first-wins.** The class C
  sidecar index is first-wins because two ROMs sharing a game code are a revision pair; two ROMs
  sharing a title share a **file**.
- **A download is placed only under a learned title.** `ResolveTarget` builds
  `saves/<system>/<rule directory>/<title><ext>`; with no binding for that ROM, or two, the
  operation is failed with its remedy (run the game once under the emulator), in the flush and
  in the restore find alike. The server's tagged name is not used as a fallback: where RomM puts
  its timestamp tag in a name like `Dr. Mario.SaveRAM` is unmeasured.
- **The in-flight guard widens to any running game of the system** for a display-name file,
  because the ROM it is bound to is not the only one that can hold it open.

- **An unchanged file's mtime is not a write.** A restore writes now, so the newest launch
  before that mtime is a session that never touched the bytes: driven, an Ultima launch eight
  days earlier was credited with a restored `StarTropics.SaveRAM` and contested it. The launch
  route is skipped when `local_save` already holds the path with the same hash and a ROM, and a
  session ends at the next launch of anything, since ES runs one game at a time. RB-265.
- **`save_slot`'s derived destination is the ROM's stem, which is wrong for this rule.** Once a
  slot has been sent, `SaveSlotStore` derived `saves/nes/StarTropics (USA).SaveRAM` and
  `ResolveTarget` took it before asking the rule. It now derives nothing for a slot a
  subdirectory rule owns. RB-266.
- **BizHawk leaves `<title>.SaveRAM.bak` on exit**, the save the new one replaced. A rule
  declares its own `not_a_save_extensions` for that. RB-264.

**Driven on both cores on 2026-09-21** (RB-262 to RB-266, `docs/platforms/nes/bizhawk.md`): upload,
restore into the file BizHawk loads, the in-flight deferral, the launch route alone, and the
Europe copy of StarTropics sharing the USA copy's file, contested and then settled by
`saves bind`. **`NesHawk` and `quickerNES` read each other's `.SaveRAM`** (RB-271), so the
one `bizhawk:battery` slot is one save in fact. **A game can rewrite its save on boot with no
in-game save**: Destiny of an Emperor changed 4 bytes on a title-screen run (RB-272), so every
launch sends a new version, and a new hash after a session is not by itself evidence of play. Both
rows are certified on `nes` since.

Class C is keyed by **Game ID** (`UCUS98751`, a PS3
`TITLEID`, a GameCube disc ID). This design was built around **RomM storing no serial, title ID
or product code anywhere**, so that no API lookup existed to ask.

**That held at the 5.2.0 floor and stopped holding at 5.3.0**, and still does not hold at the 5.3.1 floor. RomM
declares `title_id`, `save_target` and `save_target_layout` as ROM columns, and **measured on a
live library it answers for the systems this repo reads 0% of**: 3 of 4 psx `.chd`, 4 of 4 ps2
`.chd`, 4 of 4 psp `.cso`, 4 of 4 ps3, 3 of 3 each for 3ds, dreamcast, xbox and xbox360. The
route this design was built around not existing does exist. It is still a **fourth route, not a
replacement**: it is gated on `TITLE_ID_EXTRACTION_ENABLED`, it answers only for rows scanned
since the feature landed, and the rule below about asking every route is what it joins.

**Three facts about the field decide how to use it, and none are obvious from its name.**

- **`save_target` is computed from `title_id`, not equal to it.** Xbox `MS-100` has a
  `save_target` of `4D530064`, which is `ascii("MS")` plus `100` as a 16-bit hex number; ps2
  `SCUS-97472` becomes `BASCUS-97472`, the folder PCSX2 creates; 3ds splits the id in half and
  lower-cases the tail. **Read `save_target` with `save_target_layout`, never `title_id`**, when
  the question is where a save lives. `title_id` is what came out of the binary.
- **Where both routes answer they agree exactly.** Route 2 reads `head[0x58..0x5C]` as four ASCII
  bytes; RomM's `title_id` holds the same four hex encoded. All 1,601 distinct GameCube ids on a real library
  decode to printable `A-Z0-9` codes, `47553459` being `GU4Y`. So this route corroborates rather
  than competes, which is what the disagreement rule needs to be worth anything.
- **GameCube's `save_target` has two shapes, and a library holds both.** It is the ASCII code,
  `GAFE`, that a Dolphin `.gci` name carries, except on a row no rescan has rewritten, which
  holds the hex id in both fields (RM-2). A consumer of `save_target` for GameCube accepts
  either, decoding eight hex digits to four ASCII characters. Nothing reads the field yet.
- **A serial is not unique per ROM and is not meant to be.** On a GameCube library scanned end to
  end, 167 ids are shared by 359 of 1,793 rows. A third of that is the library rather than the
  field: multi-disc releases stored as loose files are a row per disc, where one folder per game
  would be one row with several files, and folding them back leaves 101 groups over 222 rows
  (104 over 232 with the committed probe's fold, `r5-gamecube-title-ids.py`, RM-2).
  Both kinds are right. Disc 1 and Disc 2 share a memory card, and a revision does not move the
  player's save. **Plan for the larger number**, because a loose multi-disc library is ordinary
  and this client does not get to require otherwise. The first-wins rule below already covers it
  and already gives this as the reason. **Never use a serial to identify a ROM**; that is what
  the hash is for.

**The route runs both ways.** `PUT /roms/{id}/identity` takes the same triple from a client under
scope `roms.write`, described upstream as identity "a client extracted for a ROM that RomM cannot
read itself". Its extractor answers nothing for Switch (encrypted, left out deliberately), PSN
`.pkg` content, or a Vita `.zip`, and this repo reads GameCube at 100% and Wii at 75.5%, so the
two cover different ground in both directions. Writing back is not built and is not assumed; it
is recorded here so the next session does not re-derive that the endpoint exists.

See RM-2 and #168.

**Ask every route, not the first one that answers.** They are cheap next to the scan that
already ran, and their agreement is the only evidence a binding has. One exception comes before
all of them: under `mame` the key _is_ the ROM basename, so that join needs no route and is
never cached.

1. **Correlate with the launch journal.** A save directory touched inside a known launch
   window belongs to that rom. Cache the learned binding. This generalises to every odd case
   and needs no format parsing.

   **Do not source that window from the `game-start` hook's arguments.** The hook is never
   told the system, emulator or core, and a `.bat` hook does not even start when the display
   name contains a space, which is nearly every real rom (an `.exe` hook does; see
   `retrobat-layout`). Build the window from `emulationstation/emulatorLauncher.log`, which
   records rom path, system, emulator and core with a millisecond timestamp on every launch,
   and use the `game-end` hook as the trigger to go read it. See
   RB-346 to RB-352 and RB-394 to RB-400.

2. **Read the ID from the ROM's header**, and know how little that reaches. Measured across
   every image in five systems on a real install: GameCube **100%** and Wii **75.5%** (a `.wad`
   has no disc header and its title id sits behind a variable-length certificate chain), and
   **0% of PSP, PS3 and PSX**, because no constant offset reaches a `.cso`, a `.chd` or an
   ISO9660 filesystem. Check the `.rvz` format version before trusting `0x58`. **`PARAM.SFO`
   adds nothing**: its `SAVEDATA_DIRECTORY` is the directory's own name and its `TITLE` is a
   human string. So this route serves the two systems whose save key _is_ the game code, and
   nothing else.

3. **Read it out of the save-state name sidecar**, which is free and reaches what route 2
   cannot. `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, joining the key of
   `SAVEDATA/ULES01513SYSDATA` to a ROM filename the ordinary index resolves. It needs no ROM
   read and no observed launch, and it covers only games that have a state.

**Both indexes are first-wins, and the two must not diverge.** Within one route a key that two
ROMs answer to takes the first, because either is as good an answer as the other: for the header
that is a revision pair sharing a game code, and for the sidecar it is two states naming one
identifier. Both scans are ordered, by ROM path and by state path, so first is a stable answer
rather than whichever row the database returned last. Were either index last-wins, the two routes
would settle the same question by opposite rules.

**Disagreement fails closed.** Two routes naming different games binds nothing, records the
refusal so it is not recomputed every scan, and reports both candidates. Picking a side uploads
one game's save under another's name and the cache then makes it permanent. `saves bind` is how a
person settles or clears one.

**Cache a decision, never an absence.** "Both routes read something and they disagree" is worth
a row; "nothing had anything to say" is not, because the usual cause is that the ROM has not been
synced yet and a cached refusal outlives its own reason, leaving the unit unattributed behind a
row nothing clears. Recomputing costs one dictionary lookup against indexes the pass already
built. Measured on a real install, where a MAME `nvram/` tree with no ROMs beside it produced
1,231 of these in one scan.

**A save unit is a (container, key) pair, not a directory.** `ps3` keeps three directories under
one title id, `psp`'s key is a _prefix_ of the segment (`ULES01513SYSDATA`), and `gamecube` has
no per-game directory at all: two `.gci` files share a region folder with every other game. The
container is declared in `save_shapes.json` and never discovered, because hashing an emulator's
data root costs 426 s where the scoped subtree costs 0.06 s.

## Hash contents, not the archive

**RomM does the same thing, and the fold is its function, so class C carries one hash.** Its
`content_hash` is the MD5 of the bytes for a plain file and, for an archive, `hash_zip_contents`:
the md5 of `<entry name>:<entry md5>` lines, sorted by name, joined with `\n` and none trailing,
directory entries skipped, confirmed live by `s5-archive-content-hash.py` (RB-303).
`LogicalContentHash.Fold` is that rule, sorted by UTF-8 bytes because Python sorts code points,
so the fold is the local change detector and the wire value.

**A restore is checked against the server's value, and the server's value is over raw names.**
`hash_zip_contents` folds `entry.filename` as stored, and extraction normalises every name through
`RelativePath`, so a peer's zip naming `./SAVEDATA/X/DATA.BIN` or using backslashes folds
differently once unpacked. `SaveArchive.ServerHashOf` reads the archive itself and is what
`SaveUnitTransfer.Restore` compares; the fold over what landed is still what the row records. A
server row written before RomM's own fix (upstream `edb5d1542`, 2026-05-29, in 5.2.0) holds the
raw MD5 of the zip until an admin runs `recompute_save_content_hashes`, so that form is accepted
too. Refusing it would fail that restore on every flush. A mismatch is
`SaveUnitMismatchException`, reported as "not written", never as an archive that would not unpack.

**A save whose bytes the head of its slot holds is in step, whoever wrote the head.** Before 303
the fold was a different function, so every class C `uploaded_content_hash` recorded then reads as
changed once, and negotiate answers `no_op` for it since the hash now matches. `SaveSync.HoldsHead`
records it as sent rather than uploading, for every shape. When the head is not the row this
device last exchanged, `SettleOnHeadAsync` acknowledges it first, with no transfer. **Skipping
the acknowledgement breaks the next edit.** Measured at 5.3.0 (`s4-older-mtime.py` M6): with this
device's row gone and a peer's row holding the same bytes, negotiate answers `no_op (Content is
identical)`, the next edit's upload is refused 409 "Slot has a newer save since your last sync",
and after `POST /api/saves/{id}/downloaded` for the peer's row the same upload lands. The
`AlreadyHeld` download skip settles the same way. A peer's upload of bytes a row in the slot
already holds does not make a new row: it comes back as that row (M5), so this case needs the
original row gone, which slot retention does (`s3-slot-retention.py`, RM-11).

Defining `content_hash` as the MD5 of zip bytes is a trap: Go's `archive/zip` and .NET's
`ZipArchive` differ in entry ordering, timestamps and compression, so RomMBat and Grout
would disagree on identical saves forever, and a library upgrade could do the same to
RomMBat alone. Hash the **logical contents**: sorted relative paths plus each file's own
hash, folded into one digest. The archive is transport only.
