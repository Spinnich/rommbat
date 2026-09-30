# RomMBat - Repository Guide for Contributors & Agents

RomMBat syncs a self-hosted [RomM](https://github.com/rommapp/romm) library with a
[RetroBat](https://github.com/RetroBat-Official) install on Windows: it pulls a chosen
subset of ROMs, metadata, media and BIOS into RetroBat's native folder layout, and pushes
saves, states and play sessions back. RomM is the authority, RetroBat is the player.

Read first: the core principles in [docs/design/principles.md](docs/design/principles.md#core-principles). Then find
your task in the routing table below and load what it names, and nothing more.

## The stack at a glance

|             |                                                                |
| ----------- | -------------------------------------------------------------- |
| Language    | C# / .NET 10 (LTS, supported to Nov 2028)                      |
| Ships as    | Self-contained `win-x64`, no .NET install required             |
| Local state | SQLite, inside the RetroBat tree                               |
| UI          | Full-screen gamepad-navigable, launched from EmulationStation  |
| Agent       | Short-lived console process invoked by ES hooks                |
| Lint        | Trunk (`trunk fmt && trunk check`), plus `tools/docs/check.py` |
| Licence     | GPL-3.0                                                        |

Five projects under `src/` and two test projects under `tests/`, each with its own `CLAUDE.md`
naming what lives there and its traps. Dependencies run one way: Agent and UI depend on Core,
Core on `RomM.Client`, and the hook compiles three Core files rather than referencing it.

An install is seven files, not one, and `tools/publish.ps1` refuses to package an incomplete set.
Why is in [docs/architecture/projects.md](docs/architecture/projects.md#srcrommbatui).

## Routing table

Find the task, load the first column's target, then the section it names. Terms such as row,
shape, slot, set and floor are defined in [docs/design/glossary.md](docs/design/glossary.md).

| Task                                                                                   | Load                                                                                                         |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Calling RomM: `RomM.Client`, pairing, scopes, a 401, 403 or 409                        | `romm-api`: "Traps", "Scopes"                                                                                |
| Reading or writing inside RetroBat: `es_*.cfg`, hooks, the ES menu, `gamelist.xml`     | `retrobat-layout`: the section named after the file                                                          |
| BIOS and firmware                                                                      | `retrobat-layout`: "bios/ is a shared tree", then `platform-mapping` for the manifest's names                |
| A RomM platform landing in the wrong folder, or unmapped                               | `platform-mapping`: "Resolution chain", "Adding or fixing a mapping"                                         |
| Saves, states, slots, conflicts, memory cards: `SaveSync`, `SaveFlushService`, `Save*` | `save-sync`: "Where the flush passes live", then "The four shapes" or "Protocol rules"                       |
| Outbox, journal, spool, `TreeLock`, relative paths, the token, clock skew              | `offline-and-portable`                                                                                       |
| Controller input, `es_input.cfg`, the gamepad UI                                       | [src/RomMBat.UI/CLAUDE.md](src/RomMBat.UI/CLAUDE.md), `retrobat-layout`: "Controller input"                  |
| Certifying a `(system, emulator, core)` row                                            | `platform-certification`: "Checklist", with `docs/platforms/nes/` as the worked example                      |
| How RetroBat or RomM behaves, and the evidence, or an `RB-`/`RM-` ID                   | [docs/upstream/](docs/upstream/README.md): the topic file, or grep the ID                                    |
| How RomMBat's code is laid out and why                                                 | [docs/architecture/](docs/architecture/README.md), the file for the area                                     |
| Why a behaviour was chosen, when no skill says                                         | [docs/design/decisions/](docs/design/decisions/README.md), the record it names                               |
| Moving the supported RomM or RetroBat version                                          | "Version floor" below, then `pre-pr-verification`: "When the change moves..."                                |
| Writing or editing any doc                                                             | [docs/contributing/writing.md](docs/contributing/writing.md)                                                 |
| A scripted edit or a `gh` body edit from Windows                                       | [docs/contributing/windows-agent-hazards.md](docs/contributing/windows-agent-hazards.md)                     |
| Wrapping up: commit, PR, "done"                                                        | `pre-pr-verification`, all of it, then `tools/pre-pr.ps1`                                                    |
| Picking up an issue, driving or reviewing a PR, certifying a system                    | [docs/contributing/workflow.md](docs/contributing/workflow.md), then `/start-issue`, `/drive-pr`, `/certify` |

## Six rules that override intuition

Each of these is a decision an agent will otherwise get backwards, and each is expensive to
unwind later. Code cites them by number, so the numbers do not change.

1. **Never persist an absolute path.** RetroBat is portable and the drive letter changes.
   Store paths relative to the RetroBat root and resolve at point of use.
2. **Never edit an emulator INI.** `emulatorlauncher` regenerates emulator configs from ES
   options on every launch. Write `es_settings.cfg` instead, which supports a per-game
   form: `<system>["<rom filename>"].<key>`.
3. **RetroBat is the authority on file extensions and on which BIOS to fetch**, not RomM, and
   **neither gates a sync.** Read `<extension>` from the live `es_systems.cfg` and report the
   members it omits as unlisted in EmulationStation; never exclude on it, because it is a union
   across every emulator a system offers and cannot say what the running one opens. The work
   that matters is landing multi-disc and multi-file games correctly, per platform. Join
   firmware against `batocera-systems.json` on **md5 only**. That list is what to fetch, not
   what a platform needs: it has no optional flag, and the emulator decides which files it
   reads. So a file RomM lacks is reported and **never blocks a platform**, its sync or its
   certification. Where a row reads a file the list files under another system, the bundled
   manifest copies that entry across with its evidence (`gb` takes `sgb`'s four and `gbc`'s
   boot ROM); it never names a hash of its own.
4. **The `game-start` and `game-end` hooks never touch the network.** Those two run inside
   the game-launch path: they append to a local journal, exit, and start nothing. **`start`
   and `quit` are outside that path** and each spawns a detached `background <event>` pass,
   which is what drains the journal on a machine where nobody opens a terminal. The set is
   `SpoolRecord.BackgroundEvents` and a test asserts it, because the hook itself cannot say
   which of the four it is serving until it reads the folder it was installed into.
5. **Set `SocketsHttpHandler.ConnectTimeout` on every handler.** Nothing sets it by default
   and an unreachable LAN host stalls for 21 s. Then classify the failure: a timeout and a
   user cancellation are both `TaskCanceledException` and differ only in the inner exception.
6. **Generated DTOs are committed, never generated at build time.** Regenerate only when
   deliberately moving the pinned schema version, and review the diff.

## Docs describe the present

Nothing in the tree records how something used to be: no milestone narratives, no superseded
measurements, no version-move sections, no fixed-and-adopted upstream issues, no "an earlier
revision said". When something stops being true, the PR that changes it edits or deletes the
text. `git log` and `git blame` are the history.

Docs travel with code, in the same PR. A statement in `README.md`, `docs/`, `DEVELOPER_SETUP.md`
or a skill that the merged code contradicts is a defect, and the PR that changed the behaviour
owes the correction. What a given change owes is tabulated in `pre-pr-verification`. A rule
that exists only because something was measured belongs in the skill for that area.

## Repo-wide rules

**AI assistance is disclosed.** This project is developed primarily by Claude Code. RomM
requires AI-assistance disclosure in pull requests and RomMBat inherits the norm: state that AI
was used and to what extent.

**Assume this lands under `rommapp`.** GPL-3.0, Trunk, `rommapp/template-repo`'s `.github`
layout, and the Playnite plugin as the structural analogue for a C# repo in the org.

**Version floor.** Every release names its minimum RomM and RetroBat. Currently RetroBat 8.2.1
and RomM 5.3.1, both the newest stable; anything older is refused at startup, and anything newer
warns. The floor moves forward: adopt a new stable (or a prerelease ahead of it) within one
release. Adopting one means re-running `reference/refresh.sh` and resolving the drift, reading
the upstream changelog for anything touching a measured rule, moving the floor and the tested
row together, and re-checking every entry in `docs/upstream/issues.md`. Moving the RomM floor
also moves the pinned OpenAPI schema. Details are in
[docs/design/version-compatibility.md](docs/design/version-compatibility.md).

**Tests travel with code.** New logic gets a test. Save-shape and mapping logic get fixtures
from a real install, checked in: its layout, config and logs, never game content. The suite's
budget is CI's Test step, where disk-bound work runs ten times slower than on a dev box, so
profile before adding a slow test (`pre-pr-verification`: "Test cost").

**Verify before handoff.** Never claim a platform works without running the
`platform-certification` checklist against it. The unit is `(system, emulator, core)`, so "snes
works" is not a claim. A change to save logic owes a hands-on pass of the shape it touches,
on every emulator and save option that writes it (`pre-pr-verification`), and a session that
cannot take one says which claims are unproven rather than letting the test
suite stand in for evidence.

**Ask the maintainer as multiple choice.** A decision that is the maintainer's goes through
`AskUserQuestion`, never prose in a reply. Recommended option first, related decisions batched
into one call, multi-select when the choices are not exclusive. A choice with a conventional
default, or a fact the code can answer, is not a question: make the call and say which way it
went.

**Reference data is vendored.** `reference/` holds the upstream files the design depends on.
Never hand-edit them. A drift reported by `reference/verify.py` is a signal to revisit the
design, not to update the expected number.

**Never commit secrets.** Tokens live in the local store, never in the repo or a committed
config file.

**Never commit copyrighted game content**: no ROM, BIOS or firmware, and nothing an emulator
derived from one (battery saves, save states, framebuffers, scraped media). A test that needs
one records its name, size, md5 and magic bytes and builds a stand-in from generated bytes. The
one carve-out is UI screenshots in the docs, which may show game art.

**Writing.** English only outside localisation files. Comments are short and say why, not what,
and describe how the code behaves now. The rest of the house style, and the mechanical rules
`tools/docs/check.py` enforces, are in [docs/contributing/writing.md](docs/contributing/writing.md).

## Quick commands

```bash
dotnet build
dotnet test
python3 tools/docs/check.py     # docs: links, anchors, fact citations, budgets
trunk fmt && trunk check        # lint
cd reference && ./refresh.sh    # re-pull upstream, re-derive the numbers, check generated data

# Only when deliberately moving the pinned RomM schema version. Needs `dotnet tool restore`.
cd src/RomM.Client/openapi && ./generate.sh
```

Packaging is PowerShell and does not run from Git Bash:

```powershell
./tools/publish.ps1                          # publish, assemble the seven files, zip
./tools/publish.ps1 -Deploy D:\retrobat-test # and copy into an install
```

Setup, including pointing at a RomM instance and standing up a throwaway RetroBat, is in
[DEVELOPER_SETUP.md](DEVELOPER_SETUP.md).
