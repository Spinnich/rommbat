---
summary: How work moves from an issue to a merged PR, which command to run when, and what the agent does without asking.
read-when: Before starting, driving or reviewing a PR, or when deciding whether something needs the maintainer.
---

# Workflow

You have three jobs: **decide, play, merge.** Everything else runs without you. Each command
runs its work to a merge-ready PR and stops only when it needs one of those three.

## Which command

| You want to                                    | Run                 | It stops for you when                                                               |
| ---------------------------------------------- | ------------------- | ----------------------------------------------------------------------------------- |
| Pick something to work on                      | `/next`             | It shows the top four and asks which                                                |
| Work on a known issue                          | `/start-issue <n>`  | The issue looks stale or wrong, a design question comes up, or a save needs playing |
| Move an open PR forward, from any state        | `/drive-pr <n>`     | It is ready to merge, or a design question is left                                  |
| Get one review of a PR without fixing anything | `/review-pr <n>`    | Never                                                                               |
| Certify a system                               | `/certify <system>` | It is time to play                                                                  |

`/start-issue` and `/certify` end by running `/drive-pr`, so you rarely run it by hand. Run it
again on a PR that stopped for you, once you have answered.

## Which model each command runs on

Each command and the reviewer agent set `model` and `effort` in their frontmatter, so a session
switches model when the command starts and returns to your own default on your next message.

| Command or agent | Model  | Effort | Why                                                           |
| ---------------- | ------ | ------ | ------------------------------------------------------------- |
| `/next`          | Sonnet | low    | Ranks a list and hands off                                    |
| `/start-issue`   | Opus   | medium | The design judgement: ruling on the issue and writing the fix |
| `/drive-pr`      | Sonnet | high   | CI logs and scoped fixes; high effort for ruling on findings  |
| `pr-reviewer`    | Opus   | medium | A different, stronger model from the one driving the PR       |
| `/review-pr`     | Sonnet | low    | Spawns the reviewer and relays its comment                    |
| `/certify`       | Sonnet | medium | Mostly launching rows, sending keys and recording             |

Other general-purpose subagents run on Sonnet, from `CLAUDE_CODE_SUBAGENT_MODEL` in
`.claude/settings.json`. The values are family aliases, so each takes the newest model in its
family without a change here. To map an alias to something else on your own machine, set
`ANTHROPIC_DEFAULT_SONNET_MODEL` or `ANTHROPIC_DEFAULT_OPUS_MODEL`.

## Where to look

Nothing needs a terminal watched. When a command stops, you get a push notification and the PR
gets a label, so the queue is a GitHub filter:

- `label:ready-to-merge`: review the PR and merge it.
- `label:needs-decision`: the question is on the PR and in the terminal as a multiple-choice.

Each PR carries its own record. A **review comment** per round lists numbered findings (`R2.3`
is round 2, finding 3). One **ledger comment**, edited in place, holds the ruling on every
finding, its evidence, and the commit that settled it, plus the choices the agent made alone
for you to veto.

## How a PR gets reviewed

A separate reviewer agent (`.claude/agents/pr-reviewer.md`) starts fresh each round with only the
PR to go on, so it does not grade its own work. It finds; the driving session rules. A finding
has to cite a rule or show a concrete failure, and taste does not count.

A PR is **merge-ready** when CI is green, every finding has a ruling, and the latest round
changed nothing. If it takes a third round, the command stops and asks, because that means the
design is in question, not the code.

## What the agent does without asking

- Creates branches and worktrees, opens PRs, comments on them, and pushes to the PR's own branch.
- Makes a reversible choice inside a PR, and lists it on the PR for your veto.
- Gives each PR its type label for the release notes and its `semver:*` label for the bump
  (`CONTRIBUTING.md`, "Labels and release notes"), and lists a label it picked for your veto.
- Closes an issue a later commit already fixed, citing that commit.
- Deploys to, pairs, syncs, resets and drives its own agent tree (`ROMMBAT_AGENT_ROOT`), to do the
  hands-on pass a change owes, `/certify` included, with `tools/handson/`. It takes the screen and
  keyboard only when EmulationStation, every emulator and RomMBat are closed, and during
  `/certify` when nobody has touched the keyboard, mouse or a pad for 30 s. It asks otherwise.
- Merges on green, once review is done, a PR that is docs only, tests only, or a Dependabot patch
  or minor bump.

## What always comes to you

- A design question: anything a doc under `docs/` or a skill would have to change to record.
- Closing an issue for any reason other than "already fixed".
- Any other merge, and anything that touches main directly.
- Writing anywhere else in the RetroBat install, and restoring it from a snapshot.
- Playing: battery saves, name entry, anything a person has to do with a pad.
- Taking the screen while EmulationStation, an emulator or RomMBat is already running.

## What "done" means

CI green, every finding ruled, docs corrected in the same PR, and a plain statement of what was
verified and what was not. A change a user can see or the server can receive also has its
hands-on pass on the deployed build (`pre-pr-verification`, "Hands-on by change type"), or says
which claims are unproven without one.

`tools/pre-pr.ps1` runs every CI gate locally: `pwsh -File tools/pre-pr.ps1`. In a git
worktree it skips trunk, which cannot read one from WSL, and CI's trunk check covers it.
`-Quiet` prints only each gate's name and the tail of a gate that fails, which is how the
commands run it.

## One-time setup

The commands find the agent's RetroBat tree in the repository-root `.env`, which git ignores,
beside the test server and tokens. Every hands-on pass and every `/certify` runs there, and you
play it over RDP:

```bash
ROMMBAT_AGENT_ROOT=D:\retrobat-agent
```

Building the agent tree is in [the hands-on kit](../../tools/handson/README.md#the-agent-tree).

The commands the loop runs most are allowed in `.claude/settings.json`, so they do not prompt.
Merging and closing issues are left off that list on purpose, so
the merges and closes above still raise a permission prompt: that prompt is the last check before
something leaves the branch.
