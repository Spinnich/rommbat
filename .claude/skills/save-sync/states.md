# Save states

Part of the [save-sync](SKILL.md) skill. How a state is found, named and restored.

## Save states: parse, do not hardcode

`.emulationstation/es_savestates.cfg` gives directory, file, image, autosave templates and
slot bounds per emulator. See the `retrobat-layout` skill. Map `<image>` onto the optional
`screenshotFile`, and derive `{emulator}:{core}:{slot}` as the slot.

**Name the screenshot after the state's upload name, never after the image file.** RomM has no
link column: `State.screenshot` finds an image whose name, or name less extension, equals the
state's name or the state's name less extension, where "extension" is RomM's
`\.(([a-z]+\.)*\w+)$`. Scoping the image's own name put the group after `.state1` and never
matched for the five emulators whose `<image>` is `<file>.png`, while the seven whose `<image>`
replaces the extension happened to match; that mix was RB-138's "a third".
`<upload name><image extension>` matches for every declared emulator (ppsspp's image is `.jpg`).

**A match is not unique, so check the name that comes back.** RomM's pattern strips a run of
lowercase-letter extensions as one: `Game [libretro.snes9x].state.png` loses `.state.png`, so
libretro slot 0's image has the name-less-extension of every slot of that game and core. The
lookup ranks an image whose name less extension is the state's full name first, then takes the
highest id, so a slot with no image of its own is answered with slot 0's. An old-name slot 0
image, `Game.state [libretro.x].png`, likewise answers for the autosave
`Game.state [libretro.x].auto`. `StateSync.IsOwnScreenshot` accepts only an image named
`<state upload name>.<ext>`, or the exact earlier name of the state's own image (which linked
for the seven), on restore and when counting a dropped screenshot on push. `StubRomMServer.Binds` ports the filter and `ScreenshotFor` the
choice among a ROM's images, so a test cannot pass on a name the server would not link or on
another state's image. RB-258.

**That slot never leaves the device.** `POST /api/states` has no slot field, and the row it
returns carries no `content_hash` either, both confirmed live and in the pinned schema. So it
is a local pairing key, and "does this state still need sending" is answerable only from the
hash the device wrote down when it last sent one.

**Reverse the templates; do not expand a slot range.** Compiling `<file>` into an anchored
expression and matching what is on disk reads the slot off the filename, which answers
`libretro`'s trap, the one entry declaring no bounds, and settles a question that is not one of
the four: whether `{{slot}}` renders empty at slot zero becomes "accept zero digits".
**`bigpemu` is not answered**: it declares `001`/`999` against a two-digit `{{slot2d}}`, which
compiles to `\d{2}`. **That reads as a contradiction and is not one** (RB-166, driven):
the bounds describe what **BigPEmu** writes in its own tree, three-digit and keyed by an internal
game id under `emulators/bigpemu/userdata/`, and the template describes **RetroBat's mirror**
under `saves/jaguar/bigpemu/`, two-digit and rom-named. Reading the declared path is right and
six real states came back as slots 1 to 6 with nothing reported. The edges are still reported and
never refused (#65): a `StateScanner` near-miss covers a name matching a `<file>` template except
for the width of its slot, and a slot outside the declared range. Only the slot widens, so the
`.txt` sidecar and the screenshots stay silent, confirmed against a real install's whole state
tree. A mirror name past slot 99 is what #34 now stands on, and reaching it needs ~94 saves of
one game.

**`bigpemu` is a third emulator whose native tree is not under `saves/`, and its battery save
never leaves it.** `game<ID>_eeprom.bigpeep` sits in `emulators/bigpemu/userdata/` with no
counterpart anywhere under `saves/jaguar/` (RB-167), so a client reading only the
declared tree concludes the game has no battery save. That is the concrete reason `jaguar` stays
in `save_shapes.json`'s `_unclassified` list. Its `.txt` sidecar holds the same internal game id
its native filenames use (168), so it is the mapping between the two naming schemes, the same job
PPSSPP's `ULES01513_1.00` does. The same reversal on `<directory>` recovers the system and the
core from the tree, which answers `bizhawk`'s core scoping and is the only sound reading when
neither level of the save tree is positional. `desmume` still needs handling: nothing makes its
`<image>` differ from its `<file>`.

**The uploaded name is not the name on disk, and getting this wrong loses a state silently.**
Measured: the upsert keys on `(rom_id, file_name)` and the **emulator is not part of the key**,
so five posts of one name under five different emulator values reused a single row. `libretro`
declares `{{romfilename}}.state{{slot}}` and `gopher64` declares `{{romfilename}}.state{{slot0}}`,
which render identically for slots 1 to 9 and both serve `n64`; two libretro cores do the same
for one game. So upload as `<stem> [<emulator>[.<core>]]<ext>`, **unconditionally** rather than
only where a collision is possible: a conditional rule gives two devices two names for one
state, and two names is two rows.

**Suppress a zero-byte screenshot.** The server accepts one and stores it as a real screenshot
row, and RetroBat's mirror produces one by racing the emulator, so the client is the only place
that case gets caught.

**Read and write the declared directory, not the emulator's native one.** Several emulators
write states under their own naming and RetroBat mirrors them into the declared path a moment
later, live. Measured on PPSSPP: native `psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst` is
mirrored to `psp/ppsspp/<rom filename>_<slot>.ppst` about 120 ms after each save, and ES hands
the launcher the **declared** path via `-state_file`, which reaches the emulator as `--state=`.
So writing a downloaded state into the declared directory is what makes it loadable.

**The native location can be outside `saves/` altogether.** BizHawk writes
`emulators/bizhawk/sstates/<system>/<internal title>.<core>.QuickSave0.State` and openMSX
writes `bios/openmsx/savestates/<name>.oms`. For BizHawk only the mirror lands under `saves/`,
and deleting the native copy and relaunching rebuilt it from the ES-facing one, so the declared
path is authoritative in both directions. The mirror is `emulatorLauncher`'s: a native state
written by an EmuHawk started any other way never reaches `saves/`, and the next launch removes
it (RB-270). **openMSX's declared directory stayed empty**, so do
not assume every emulator is mirrored. Do not assume everything beside a state travels either:
BizHawk's `.State.rap` sibling is native-only and is not recreated on sync-in.

Four traps, all confirmed across the eleven emulators M0 drove:

- A **`.txt` sidecar** often sits beside the state holding the native basename
  (`UCES00995_1.00`, `SLUS-00404`, `GW7E69`). It is the mapping between the two naming schemes,
  and where it holds a serial it is the Game ID that directory-save attribution would otherwise
  read out of a ROM.

  **It is not emitted unconditionally, and an earlier reading here said it was.** Driven on a
  real install: `libretro` writes none at all, under either of two cores. `jgenesis` wrote one
  holding the plain rom filename, and `bizhawk` wrote `Phantasy Star (B).SMSHawk`, which is
  BizHawk's own truncated name plus the core. So its absence means nothing and its presence
  means nothing; only its **contents** are worth anything, and only sometimes.

- **`<image>` is absent more often than present**: missing outright for most emulators driven,
  and correct, zero-byte and missing across three runs of the same PPSSPP game. `screenshotFile`
  is best-effort everywhere; absent and empty are both normal and say nothing about the state.
  **BizHawk declares an `<image>` and never writes one**: its frame is `Framebuffer.bmp` inside
  the `.State` zip, so the state carries its own screenshot and RomM holds none for it (finding
  268).
- **The declared `<directory>` is wrong for one of the twelve emulators launched.**
  **`openmsx` writes to `bios/openmsx/savestates/`, a different top-level tree** from the
  declared `saves/msx1/openmsx`, and that is unfixed. `flycast` was the second until
  **RetroBat 8.2.1 fixed it** (`emulatorlauncher#1336`): it still writes
  `dreamcast/reicast/states` first, but the state is now mirrored into the declared
  `dreamcast/flycast/sstates` in the same millisecond, confirmed by hand over three runs, so
  Dreamcast states sync from the declaration like any other emulator's. An empty declared
  directory is never evidence that a game has no states; cross-check against the emulator's
  generated config.
- **Anchor the slot placeholder as a single digit when expanding a template.** DeSmuME declares
  `{{romfilename}}.ds{{slot0}}` and writes its battery save as `{{romfilename}}.dsv`, so a
  `<rom>.ds*` glob picks up the battery save as slot "v".

A declaration is not a promise that the emulator is usable. RetroBat downloads emulators on
demand, so six of the thirteen had no executable at all; installing one raises a **modal dialog
with no title and no timeout** that blocks the launch until answered. And `bizhawk` crashes in
RetroBat's controller generator unless the launcher is given `-core`. Check for the binary, and
do not promise state sync on the strength of the config alone.

Record emulator, core and version with every state, and never silently restore one made by
a different version. RetroBat's own wiki warns that states break across emulator updates.
