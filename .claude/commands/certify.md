---
description: Certify every (emulator, core) row of one RetroBat system, or run a save-logic hands-on pass, to a merge-ready PR
argument-hint: "<system> [--hands-on <PR>]"
model: sonnet
effort: medium
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

The install is the agent tree, `ROMMBAT_AGENT_ROOT` in the main checkout's `.env`, and no other.
It is the agent's to deploy to, sync, reset and write in without asking, so bring SYSTEM's games
and firmware in through a RomMBat set and `Invoke-Agent sync`, the way a user's arrive. Run
`Test-HandsOnEnv -Gui` and `Publish-ToAgentTree` first; with ES already up, its "nothing else has
the screen" line fails by design, so read the others. `tools/handson/` starts ES, launches,
sends keys and takes screenshots; `waves.md` covers what it does not, such as an
`emulatorLauncher` launch with a row's arguments.

The maintainer plays this tree over RDP, in the same desktop session the kit drives, and RDP eats
keyboard combinations. Before taking the keyboard while ES or a game runs, say so in chat, then
call `Assert-TakeoverAllowed -WhilePlaying`, which puts up the kit's "agent is driving" strip.
When the maintainer's turn comes, call `Hide-AgentBanner` and say in chat that the session is
theirs. Never send a key during a step the play sheet gives them. Keep the RomM web player closed. Pick USA or
English releases for anything they have to navigate.

## The issue

Every system still to certify has one open issue, labelled `platform`, titled
`Certify <SYSTEM>: every (emulator, core) row at the current floor`, under its wave's milestone,
with the rows as a task list. Find it with
`gh issue list --label platform --state open --search "Certify <SYSTEM>: in:title"`. If there is
none, file one in that shape before planning. A `--hands-on` pass has no issue.

## 1. Plan the rows

Nothing here needs an emulator running or a person present.

1. List every `(emulator, core)` SYSTEM declares in the live `es_systems.cfg`, and the
   save-affecting options each exposes in `es_features.cfg` (memory card type, pak, clock).
   A `--hands-on` pass keeps only the rows and options that write the changed shape.
2. Check `emulators/` holds an executable for each. One that does not is installed by ES on the
   first launch, which the agent may accept on its own tree. Its modal has no timeout;
   `Start-Game` and `Start-EmulatorLauncher` answer it through `Wait-Emulator` (`tools/handson/`).
3. Run steps 1 and 3, and the inventory half of step 2, for the whole system now.
4. Recommend the test games by `platform-certification`'s "Choosing test games". Research the
   library and the web, build the coverage table, and put the set to the maintainer as one
   `AskUserQuestion` with the recommendation first. Never ask an open "which game?".
5. Boot every row once, with the family's firmware out, and list what each writes where. That
   list decides whether the pass needs code first (a battery rule, a supplement entry), and code
   needed first is its own `/start-issue`, not part of this pass.

## 2. The play sheet

Write `probe-output/<SYSTEM>-play-sheet.md`: every row in order, and for each, what the
maintainer does with the pad (only what needs real play: a battery save, name entry), where each
game's save happens, and what you do around it (launch, state slots, keys, seeding the next row from this one's save). Batch
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

- **Certification**: branch first, with `git worktree add` as `/start-issue` does (`issue-<n>-certify-<SYSTEM>`, which
  `/next` reads as the issue being taken), so the record
  lands on the PR's branch. The record goes in `docs/platforms/<SYSTEM>/`: `index.md` for the
  system's steps and each row's standing at the floor, and a file per emulator or group of rows,
  all nine steps, in `docs/platforms/nes/`'s shape. Then every doc `pre-pr-verification` names for a
  platform changing state. Run `pwsh -File tools/pre-pr.ps1`, open the PR on the template, and
  run `/drive-pr` on it. The PR says `Fixes #<n>` when every row has a result, certified or
  driven and not certified with its reason; a partial pass says `Refs #<n>` and ticks the rows
  it covered in the issue's task list.
- **`--hands-on <PR>`**: post the result on that PR as one comment: each row and option driven,
  what the emulator wrote, what RomMBat did with it, and anything not driven with the reason.
  Add a line to that PR's ledger, then return to its `/drive-pr`.

Leave the agent tree as the pass left it. The next hands-on pass resets what it needs.
