---
description: Review a PR against RomMBat's invariants, CI gates and the design of record
argument-hint: "[PR number]"
---

# Review an incoming PR

Run this in a FRESH session, so the review isn't grading its own homework. Run
/code-review separately for the general correctness pass; this prompt covers what is
specific to RomMBat.

Read-only: no edits, commits, pushes, or posted comments without asking.

---

Variables for this run:

- PR = $1 (if that is empty, resolve it from the current branch, or `gh pr list` and
  confirm the match)
- NOTES = round log for this PR, if one exists. See "The ledger".

## Who is who

I am Spinnich, the only human on this repo. Unless the PR comes from a fork owned by
someone else, Claude wrote the branch and Claude is about to fix it based on what you write
here. You are the fresh pass, not the authority.

Review the diff on its own merits. The PR body, linked issue, and any branch notes are
claims, not evidence: use them for intent and for whether the change matches what was
promised, never to talk yourself out of a finding. Claude authorship is the norm here, not
a reason to trust or distrust a branch. Verify against the diff either way.

Your findings get triaged by a fix session that is allowed to rule any of them wrong with
evidence. Write them so that ruling is possible: every finding carries the thing that makes
it true.

## Load context, in this order

1. CLAUDE.md, then only the docs/PLAN.md sections the diff touches. PLAN.md is 1800 lines:
   read the milestone section that governs this change plus "Verification", not the whole
   file.
2. The skill in `.claude/skills/` matching the area touched: romm-api, retrobat-layout,
   platform-mapping, save-sync, offline-and-portable, platform-certification,
   pre-pr-verification.

## What counts as a finding

Both sides of this exchange are the same model, so nothing external stops a review from
manufacturing work. These bound it:

- **Cite the authority or show the failure.** A finding is valid if it names a rule in
  CLAUDE.md, CONTRIBUTING.md, docs/PLAN.md, or a skill, or if it gives a concrete input or
  state that produces a wrong result. One or the other, inside the finding itself.
- **Taste is not a finding.** Naming, structure, or idiom that no rule states and no bug
  follows from is a preference. Leave it out.
- **Only what the branch touched.** Pre-existing problems in surrounding code go in a
  separate follow-up list, not in the findings. **A document the branch's own code falsified
  is not pre-existing**, however untouched the file is: the diff is what made it wrong, so it
  is in scope and blocking. See "Documentation parity".
- **An empty review is a valid result.** "CI green, invariants hold, nothing blocking" is a
  complete answer. Padding it costs a fix session real work.
- **Severity is the honest one, not the flattering one.** A nit labelled as a correctness
  bug wastes the round it triggers.

## The ledger

If NOTES exists, it holds what earlier rounds already ruled on. Read it AFTER forming your
findings, never before: the point of a fresh session is that it has not yet been argued
into anything. Then drop any finding already ruled wrong, unless you have evidence the
earlier ruling did not have, and say what that evidence is.

## Diff scope

```bash
gh pr view $PR --json title,body,author,headRefName,baseRefName,files,commits
gh pr diff $PR
```

Or locally, three dots so you get the merge base and the branch's commits only:

```bash
git fetch origin && git diff origin/main...FETCH_HEAD
```

Say so if the branch is behind main, or if the diff is polluted by a merge of main into
itself rather than a rebase.

## Running anything from the branch

If the PR is a branch on this repo, building it runs my own code, so just run it:

```bash
dotnet build -c Release -warnaserror   # CI's build; a plain build is Debug and hides warnings
dotnet test
trunk check
cd reference && python3 verify.py
```

If it comes from a fork owned by someone else, its code runs as me the moment you build.
Read the diff for changes to `*.csproj`, `Directory.Build.props`,
`Directory.Packages.props`, `global.json`, `.github/workflows`, `.githooks`, or any script
before checking it out. If those are touched, say so, review them by eye, and let CI be the
thing that executes them.

## The six that get got backwards

These are the findings worth a human's attention. Each is cheap to check and expensive to
unwind after it lands.

- **Absolute paths.** Nothing absolute reaches the database. Look for a new path column
  without its CHECK constraint, a path stored as plain string instead of RelativePath, and
  a missing row in the LocalStoreTests bad-values table. Also `Path.GetFullPath`,
  `Environment.GetFolderPath`, and any literal drive letter on a persisted value.
- **Emulator INIs.** Any write to an emulator config file is wrong, because
  `emulatorlauncher` regenerates them on the next launch. Configuration goes through
  `es_settings.cfg`, per-game form `<system>["<rom filename>"].<key>`.
- **Authority for extensions and BIOS.** RetroBat, not RomM. A hardcoded extension list
  instead of `<extension>` from the live `es_systems.cfg` is a finding, as is a firmware
  join on anything other than md5.
- **Hooks never touch the network.** `game-start` and `game-end` run inside the
  game-launch path. Any HttpClient, await on I/O, or retry loop reachable from them is a
  finding, even if it looks fast. They append to the journal and exit.
- **ConnectTimeout on every handler.** Nothing sets it by default and an unreachable LAN
  host stalls for 21 s. Then check the failure classification: a timeout and a user
  cancellation are both `TaskCanceledException` and differ only in the inner exception. A
  catch that treats them alike is a finding.
- **Generated DTOs are committed, never generated at build time.** Changes under
  `src/RomM.Client/openapi` need a deliberate schema-pin move, and moving the pin is a
  compatibility decision that moves a README row with it.

Plus: nothing written outside the RetroBat tree (no `%APPDATA%`, registry, service,
scheduled task, admin rights), and no token, secret, server URL, or instance hostname in
the diff, including in test fixtures.

## Gates CI will fail on

Name these if the PR will bounce, but don't spend the review on them.

- `build.yml` builds Release with `-warnaserror`, so any new warning fails the PR.
- `publish-check` does a self-contained single-file win-x64 publish. Reflection, trimming,
  and `Assembly.Location` assumptions compile fine and fail here.
- `trunk-check` runs the pinned formatters and linters, on ubuntu. Line endings against
  `.gitattributes` are the usual Windows divergence: `reference/**` and
  `tests/**/fixtures/**` are byte-exact `-text`, and `.bat`, `.cmd`, `.ps1` are crlf.
- `reference-verify` runs on any change to `reference/**` or `docs/PLAN.md`.

## Reference data

Any diff under `reference/` deserves a hard look. Vendored files are never hand-edited, and
an expected number in `verify.py` is never "fixed" to match drift. If a number moved, the
correct PR revisits `docs/PLAN.md` and says which upstream fact changed. Check that the
files look like the output of `refresh.sh` rather than a targeted edit.

## AI disclosure and the template

The template is `.github/PULL_REQUEST_TEMPLATE.md`. Check the body follows it and that the
disclosure states the extent, not just the fact. "Written primarily by Claude Code" is the
expected answer on my own branches, so a missing disclosure there is a template omission to
fix, not a suspicion to raise. On a fork from someone else, an undisclosed PR that reads as
AI-written is worth a question, asked as a question.

Ticked checkboxes are claims. Flag any the diff contradicts: "added unit tests" with no
test file, or an Invariants box ticked for something the change never touches.

## Tests

New logic gets a test. Save-shape and mapping logic get fixtures from a real install,
checked in. For the areas PLAN.md calls out, check the specific test exists rather than
that some test exists:

- Sync changes: a no-op re-sync asserting zero uploads, zero downloads, no gamelist churn.
- Offline paths: the stub switched to unreachable mid-operation, and a flush that is
  idempotent under replay and partial failure.
- Mapping changes: every bundled mapping resolves to a folder in `systems_names.lst`, and
  the unmapped count stays visible.
- Anything path-shaped: the relocation test (populated install, different root) still
  passing as a clean no-op.

## Platform claims

If the PR claims a platform works, `docs/platforms/<system>.md` must record the
certification checklist result. A platform is not done at eight of nine, and it is not
certified if nobody launched a game on it. Treat an uncertified claim as blocking and say
which step is missing.

## Compatibility

Does this move the minimum RomM (5.2.0) or RetroBat (8.2.1) version? If so, the README table
moves with it and the startup check must still refuse below and warn above.

## Documentation parity

CLAUDE.md's rule is that docs travel with code in the same PR, and the trigger table is in the
`pre-pr-verification` skill. This is the one review dimension where the file you are checking is
usually **not** in the diff, so it is also the one a review skips by default. Work the table
against what the diff changed, then grep the docs for the terms it touches.

The four that go stale, in the order they have actually gone stale here: `README.md` (a stage
table, the pre-release warning, a command block missing a new subcommand), `docs/ARCHITECTURE.md`
(the store table, a migration count, the save model), the skill for the area (a rule that a
measurement produced and the PR left only in a commit message), and `DEVELOPER_SETUP.md`.

Two things are findings, not nits. A doc statement the diff makes false, cited as
`file:line` plus the code that contradicts it. And a rule the branch learned by measurement or a
hands-on pass that landed nowhere a later session will read: the plan records what was decided,
the skill is what gets loaded, and a rule in neither is a rule that will be re-derived or
re-broken. If the docs are in step, say so in one line.

## Style

English only. No em-dashes in code comments, docs, or commit messages. Comments short and
about how the code behaves now, not narrating the change. Flag leftover debug logging and
commented-out code.

## Scope

Is this one coherent change, or two PRs wearing a trenchcoat? Did it grow past what the
issue promised? A PR that reaches into a later milestone's territory isn't automatically
wrong, but say plainly that it did.

## Cleanup (propose, don't do)

List anything that looks like development scratch: throwaway scripts, probe output, temp
files, stray fixtures. Recommend delete or move, then wait for my go-ahead. Never remove a
test covering real behaviour; if you think one is redundant, argue for it and let me
decide.

## Output

Findings ranked by severity, worst first, split into blocking and non-blocking. Each needs
`file:line`, the authority or the concrete failure scenario per "What counts as a finding",
and enough context that a fix session can rule on it without re-reading the whole diff. If
a category is clean, one line saying so. No padding.

Then:

1. A one-line verdict: land it, fix first, or a design question that needs me.
2. Anything belonging in a follow-up issue rather than this PR, listed separately.
3. If threads should go on the PR itself, the text for each as `file:line` plus comment.

Write for the fix session and for the record, not for an audience. No preamble, no summary
of what the PR does, no praise. If a fork from someone else did submit this, then and only
then write the summary comment as something a stranger can receive.
