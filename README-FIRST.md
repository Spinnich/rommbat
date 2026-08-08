# RomMBat starter bundle

Drop-in scaffolding for the new repo. Copy the contents into an empty `rommbat/` checkout
on the Windows machine, then start Claude Code there.

```text
CLAUDE.md                    the agent's entry point; indexes everything else
docs/PLAN.md                 the design of record (the full plan)
.claude/skills/*/SKILL.md    seven skills, loaded on demand
reference/                   vendored upstream data + a script that re-derives the numbers
```

## Why the reference directory exists

Several design decisions rest on measured facts about the two upstream projects. Those
facts are vendored here and `reference/verify.py` re-derives every number the plan quotes,
so a future session can check them instead of trusting them, offline.

```bash
cd reference && ./refresh.sh    # re-pull upstream, then verify
```

It already caught one error while being written: locale-aware `sort -u` silently collapsed
two punctuation-differing slugs and undercounted RomM's platform list by one. That is the
class of bug this guards against.

**A drift report is a signal to revisit `docs/PLAN.md`, not to update the expected number.**

## What is deliberately not here

`DEVELOPER_SETUP.md`, `CONTRIBUTING.md`, `README.md`, `.github/`, `.trunk/`, `LICENSE` and
the solution skeleton. All are first-session work, because they depend on choices best made
on the target machine (project layout, UI framework, CI runner). `CLAUDE.md` already says
what each must contain, and `rommapp/template-repo` plus `rommapp/playnite-plugin` are the
models to copy.

## Before the first session

### On the Windows machine

- .NET 10 SDK, git, Claude Code, `gh auth login`
- Clone for reference reading: `rommapp/romm`, `rommapp/grout`, `rommapp/playnite-plugin`,
  `RetroBat-Official/retrobat`, `RetroBat-Official/emulatorlauncher`
- A pristine RetroBat 8.2 extracted somewhere, cloned per test run

### On the RomM instance

- A dedicated non-admin account for RomMBat. The dev instance is production with ~85,000
  games; RomMBat's writes must never touch the primary account's data.
- Be ready to approve a pairing request in the web UI. Nothing in M1 is testable without
  this, and it cannot be automated away from the first run.

### Content

- ROMs on the test RetroBat for wave 1 (`nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`,
  `mastersystem`). Every M0 probe requires launching real games; none of it can be
  desk-checked.

## Session order

1. **Scaffolding only.** The files listed under "not here". No feature code. The structure
   is what keeps later sessions cheap, since the plan is far too large to re-feed each time.
2. **M0 probes.** Requires the RetroBat install and ROMs. Results land in
   `docs/retrobat-findings.md`.
3. **M1 onward**, per the plan.

Expect M0 to change the plan. Several milestones rest on assumptions it exists to test: if
ES hooks block game launch, or `es_settings.cfg` does not survive a restart, M6 and the
class-D handling need rework. `docs/retrobat-findings.md` amends the plan; it is not a
checkbox.

## Assumed destination

This is written on the assumption it eventually lands under `rommapp`: GPL-3.0, Trunk,
`rommapp/template-repo`'s `.github` layout, and `rommapp/playnite-plugin` as the structural
analogue for a C# repo in the org. Worth a word with the maintainers early, so naming,
licence and CI do not need redoing later.
