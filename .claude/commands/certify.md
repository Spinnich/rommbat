---
description: Certify every (emulator, core) row of one RetroBat system, or run a save-logic hands-on pass, to a merge-ready PR
argument-hint: "<system> [--hands-on <PR>]"
---

# Certify a system

SYSTEM = the first word of $ARGUMENTS, in `es_systems.cfg`'s vocabulary (`megacd`, not "Mega CD").

With `--hands-on <PR>` this is not a certification. It is the hands-on pass a save-logic change
owes: drive the changed shape on every emulator and save option that writes it, record the result
on that PR, and stop there. It is never recorded as a certification.

The maintainer's one job here is to play. You plan, launch, send keys, watch the files, record
and open the PR ([workflow](../../docs/contributing/workflow.md)). Load `platform-certification`
before anything else: `SKILL.md` holds the checklist, `passes.md` the traps each system taught,
and `waves.md` how the agent launches rows and sends keys. `docs/platforms/nes/` is the worked example.

## The install

The RetroBat test install is the path in `ROMMBAT_RETROBAT_ROOT`, set in the maintainer's
`.claude/settings.local.json`. If it is unset, ask for it once.

You may write, without asking, inside that install's folders for SYSTEM only: `roms/<SYSTEM>`,
`saves/<SYSTEM>`, the `bios/` files SYSTEM's rows read, and that system's entries in
`gamelist.xml` and `es_settings.cfg`. **Snapshot first**: copy each folder and file you will touch
to `probe-output/certify-<SYSTEM>-<yyyymmdd-hhmm>/` before the first write. Anything else in the
install still asks, and so does restoring from the snapshot.

The maintainer plays over RDP, which eats keyboard combinations. Keep the RomM web player closed.
Pick USA or English releases for anything they have to navigate.

## 1. Plan the rows

Nothing here needs an emulator running or a person present.

1. List every `(emulator, core)` SYSTEM declares in the live `es_systems.cfg`, and the
   save-affecting options each exposes in `es_features.cfg` (memory card type, pak, clock).
   A `--hands-on` pass keeps only the rows and options that write the changed shape.
2. Check `emulators/` holds an executable for each. One that does not is installed by ES on the
   first launch, and accepting that is the maintainer's call.
3. Run steps 1 and 3, and the inventory half of step 2, for the whole system now.
4. Pick test games: one per save medium, one that saves early at a known point, plus a
   coprocessor or multi-disc game where the system has them. Check none is pinned to a row in
   `gamelist.xml`.
5. Boot every row once, with the family's firmware out, and list what each writes where. That
   list decides whether the pass needs code first (a battery rule, a supplement entry), and code
   needed first is its own `/start-issue`, not part of this pass.

## 2. The play sheet

Write `probe-output/<SYSTEM>-play-sheet.md`: every row in order, and for each, what the
maintainer does with the pad (only what needs real play: a battery save, name entry) and what you
do around it (launch, state slots, keys, seeding the next row from this one's save). Batch
everything needing hands into one sitting. Show the sheet, then ask one question: start now, or
later.

## 3. Drive

Row by row, in the sheet's order:

- Launch through `emulatorLauncher` with the row's arguments, never the emulator directly.
- Watch the row's save and state folders with `Monitor`, so the pass moves on as soon as a save
  lands rather than waiting to be told.
- When a key's effect cannot be seen, take a screenshot rather than sending keys blind.
- Confirm what ran from `emulationstation/emulatorLauncher.log`, not from configuration.
- Record each step's result as you go. A behaviour nobody has recorded is a new finding, filed
  the way `platform-certification` says.

A row that cannot pass is recorded with its reason. That is a result, not a gap.

## 4. Record and ship

- **Certification**: branch first, with `EnterWorktree` (`certify-<SYSTEM>`), so the record
  lands on the PR's branch. The record goes in `docs/platforms/<SYSTEM>/`: `index.md` for the
  system's steps and each row's standing at the floor, and a file per emulator or group of rows,
  all nine steps, in `docs/platforms/nes/`'s shape. Then every doc `pre-pr-verification` names for a
  platform changing state. Run `pwsh -File tools/pre-pr.ps1`, open the PR on the template, and
  run `/drive-pr` on it.
- **`--hands-on <PR>`**: post the result on that PR as one comment: each row and option driven,
  what the emulator wrote, what RomMBat did with it, and anything not driven with the reason.
  Add a line to that PR's ledger, then return to its `/drive-pr`.

Put the install back from the snapshot only if the maintainer asks. The snapshot stays in
`probe-output/` until they say otherwise.
