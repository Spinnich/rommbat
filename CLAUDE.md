# RomMBat - Repository Guide for Contributors & Agents

RomMBat syncs a self-hosted [RomM](https://github.com/rommapp/romm) library with a
[RetroBat](https://github.com/RetroBat-Official) install on Windows: it pulls a chosen
subset of ROMs, metadata, media and BIOS into RetroBat's native folder layout, and pushes
saves, states and play sessions back. RomM is the authority, RetroBat is the player.

Name: a portmanteau of RomM and RetroBat, landing near "wombat". Mascot: a wombat.

**Read `docs/PLAN.md` before starting anything.** It is the design of record, and this file
is only its index.

---

## The stack at a glance

|             |                                                                |
| ----------- | -------------------------------------------------------------- |
| Language    | C# / .NET 10 (LTS, supported to Nov 2028)                      |
| Ships as    | Self-contained single-file `win-x64`, no .NET install required |
| Local state | SQLite, inside the RetroBat tree                               |
| UI          | Full-screen gamepad-navigable, launched from EmulationStation  |
| Agent       | Short-lived console process invoked by ES `.bat` hooks         |
| Lint        | Trunk (`trunk fmt && trunk check`)                             |
| Licence     | GPL-3.0                                                        |

| Project         | Role                                                                                                             |
| --------------- | ---------------------------------------------------------------------------------------------------------------- |
| `RomM.Client`   | API client. DTOs generated from `/openapi.json`, plus hand-written pairing, resumable download, sync negotiation |
| `RomMBat.Core`  | Local state and everything that knows RetroBat's disk layout                                                     |
| `RomMBat.Agent` | Console exe: `pair`, `sync`, `game-start`, `game-end`, `flush`, `status`                                         |
| `RomMBat.UI`    | Gamepad-navigable front end                                                                                      |
| `*.Tests`       | xUnit                                                                                                            |

---

## Six rules that override intuition

Each of these is a decision an agent will otherwise get backwards, and each is expensive
to unwind later.

1. **Never persist an absolute path.** RetroBat is portable and the drive letter changes.
   Store paths relative to the RetroBat root and resolve at point of use.
2. **Never edit an emulator INI.** `emulatorlauncher` regenerates emulator configs from ES
   options on every launch. Write `es_settings.cfg` instead, which supports a per-game
   form: `<system>["<rom filename>"].<key>`.
3. **RetroBat is the authority on file extensions and required BIOS**, not RomM. Read
   `<extension>` from the live `es_systems.cfg`; join firmware against
   `batocera-systems.json` on **md5 only**.
4. **The `game-start` and `game-end` hooks never touch the network.** Those two run inside
   the game-launch path: they append to a local journal, exit, and start nothing. **`start`
   and `quit` are outside that path** and each spawns a detached `background <event>` pass,
   which is what drains the journal on a machine where nobody opens a terminal. The set is
   `SpoolRecord.BackgroundEvents` and a test asserts it, because the hook itself cannot say
   which of the four it is serving until it reads the folder it was installed into.

Two more that only bite once there is code:

5. **Set `SocketsHttpHandler.ConnectTimeout` on every handler.** Nothing sets it by default
   and an unreachable LAN host stalls for 21 s. Then classify the failure: a timeout and a
   user cancellation are both `TaskCanceledException` and differ only in the inner exception.
6. **Generated DTOs are committed, never generated at build time.** Regenerate only when
   deliberately moving the pinned schema version, and review the diff.

---

## Skills: load the guide that matches the task

| Skill                    | When                                                                                              |
| ------------------------ | ------------------------------------------------------------------------------------------------- |
| `romm-api`               | Anything calling RomM: auth, pairing, endpoints, scopes, the API's traps                          |
| `retrobat-layout`        | The folder tree, `es_systems.cfg`, `es_savestates.cfg`, `es_settings.cfg`, hooks, ES menu entries |
| `platform-mapping`       | Resolving a RomM platform to a RetroBat folder, or adding/fixing a mapping                        |
| `save-sync`              | Saves, states, slots, the four save shapes, attribution, bundling                                 |
| `offline-and-portable`   | The outbox, relative paths, clock skew, filesystem constraints, portability                       |
| `platform-certification` | Certifying a new platform end to end                                                              |
| `pre-pr-verification`    | Before committing, opening a PR, or claiming done                                                 |

---

## Reference data

`reference/` vendors the upstream files the design depends on, so the numbers in the plan
are reproducible offline and drift shows up in a diff.

```bash
cd reference && ./refresh.sh    # re-pull upstream, re-derive the numbers, check the generated data
```

`verify.py` fails loudly when a value moves. **A drift there is a signal to revisit
`docs/PLAN.md`, not to update the expected number.** Never hand-edit the vendored files.

---

## Repo-wide rules

**This project is developed primarily by Claude Code, and that must be disclosed.**
RomM requires AI-assistance disclosure in pull requests, and RomMBat inherits the norm.
State that AI was used and to what extent. This is non-negotiable.

**Assume this lands under `rommapp`.** Match their conventions from the start:
GPL-3.0, Trunk for linting, `rommapp/template-repo`'s `.github` layout (issue templates,
`CODE_OF_CONDUCT.md`, `SECURITY.md`), and the Playnite plugin as the structural analogue
for a C# repo in the org.

**Declare compatibility, and track the newest stable.** Every release names its minimum
RomM and RetroBat versions. Currently RetroBat 8.2.1, RomM 5.2.0. Check both at startup,
refuse below, warn above.

**The floor moves forward, it does not sit still.** RomMBat adopts a new RomM or RetroBat
stable within one release of it appearing and raises the minimum with it, rather than
supporting the oldest version that happens to work. Every rule in `docs/retrobat-findings.md`
is a measurement of one build, and a supported range means owning that measurement on every
version in it, across a `(system, emulator, core)` matrix that is already two to four passes
per row. Adopting one means: re-run `reference/refresh.sh` and resolve the drift, read the
upstream changelog for anything touching a measured rule, move the floor and the tested row
together, and re-check every open issue in `docs/retrobat-findings.md`. Moving the RomM floor
also moves the pinned OpenAPI schema, which is the minimum version on purpose.

**Tests travel with code.** New logic gets a test. Save-shape and mapping logic get
fixtures from a real install, checked in.

**Docs travel with code, in the same PR.** `docs/PLAN.md` is the design of record, and it is
not the whole of it: `README.md`, `docs/ARCHITECTURE.md`, `DEVELOPER_SETUP.md` and the skills in
`.claude/skills/` are what a user, an operator and the next agent read instead. A statement in
any of them that the merged code contradicts is a defect and not a tidy-up, and the PR that
changed the behaviour owes the correction, because a follow-up PR means everyone reading in
between was told something false. What a given change owes is tabulated in the
`pre-pr-verification` skill. A rule that exists only because something was measured belongs in
the skill for that area, or the next session re-derives it from nothing.

**Verify before handoff.** Never claim a platform works without running the
`platform-certification` checklist against it. **The unit is `(system, emulator, core)`**: two
emulators for one console differ on save shape, state directory and BIOS needs, and `libretro`
and `bizhawk` are core-scoped on top of that, so "snes works" is not a claim. The wave rollout
starts after M7; a change to save logic before then owes one hands-on pass of the shape it
touches, and a session that cannot take one says which claims are unproven rather than letting
the test suite stand in for evidence.

**English only** outside of localisation files.

**No em-dashes** in comments, docs, or commit messages. Use commas, parentheses, or
separate sentences.

**Keep comments short** and focused on _why_, not _what_. Don't narrate the code, and
don't explain why a change was made; describe how the code behaves now.

**Hyphenated `dry-run` names `sync`'s flag and nothing else.** `bios` and `evict` preview
by default and write on `--apply`, so "the dry-run" used as a generic word for a preview
reads as a flag those commands do not have. Say "preview". "A dry run", two words, is
ordinary English and is fine.

**Never commit secrets.** Tokens live in the local store, never in the repo, never in a
config file committed to git.

---

## Quick commands

```bash
dotnet build
dotnet test
dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true

cd reference && ./refresh.sh    # refresh upstream data, verify, check generated data
trunk fmt && trunk check        # lint

# Only when deliberately moving the pinned RomM schema version. Needs `dotnet tool restore`.
cd src/RomM.Client/openapi && ./generate.sh
```

Setup, including how to point at a RomM instance and stand up a throwaway RetroBat, is in
`DEVELOPER_SETUP.md`.
