# Developer setup

Everything you need to build RomMBat, point it at a RomM instance, and stand up a
throwaway RetroBat to test against.

Development happens on Windows, which is also the target platform. That is not a
compromise: RomMBat drives EmulationStation hooks, reads RetroBat's config files and
publishes `win-x64`, so most of it cannot be meaningfully exercised anywhere else.

---

## 1. Toolchain

| Tool                   | Why                                 | Get it                                                                  |
| ---------------------- | ----------------------------------- | ----------------------------------------------------------------------- |
| .NET SDK 10.0 or newer | Build and test                      | <https://dotnet.microsoft.com/download>                                 |
| Git                    | Obviously                           | <https://git-scm.com/download/win>                                      |
| Python 3.10+           | `reference/verify.py`               | <https://www.python.org/downloads/>                                     |
| Trunk                  | Lint and format                     | `curl -fsSL https://trunk.io/releases/trunk -o trunk` (WSL or Git Bash) |
| GitHub CLI             | Reading upstream repos, opening PRs | <https://cli.github.com/>                                               |

Verify:

```bash
dotnet --list-sdks     # 10.0.x or newer
python3 --version      # 3.10+
git --version
```

`global.json` pins the minimum SDK with `rollForward: latestMajor`, so a newer SDK is
fine and no SDK at all fails loudly rather than silently building against something
unexpected.

### Build

```bash
dotnet restore
dotnet tool restore    # once per clone: NSwag, for regenerating the API DTOs
dotnet build
dotnet test
```

**Two things about `dotnet test` will waste an hour each if nobody says them.** `global.json`
opts this repo into Microsoft.Testing.Platform, so `dotnet test` is MTP's command and not
VSTest's, and it takes a different set of options. Run `dotnet test --help` for the real list.

- **An option MTP does not recognise is forwarded to the test module, which refuses it and
  reports `Zero tests ran` with exit code 5, naming neither the option nor the problem.**
  `--nologo` is the one that catches people, because every other `dotnet` verb takes it. A run
  that reports zero tests has almost certainly been handed a bad option rather than lost its
  tests.
- **A `--filter` that matches nothing in one of the two test projects makes the whole run exit
  non-zero**, because a module running zero tests is an error. Scope the run with `--project` as
  well, or the filtered run fails on the project you were not aiming at.

Packages are managed centrally in `Directory.Packages.props`. Add a version there and a
bare `<PackageReference Include="..." />` in the project, never a version in the `.csproj`.

`dotnet tool restore` is only needed if you are moving the pinned OpenAPI schema. The
generated DTOs are committed, so nothing generates at build time.

### Publish

```bash
dotnet publish src/RomMBat.Agent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/agent
dotnet publish src/RomMBat.UI    -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/ui
```

Self-contained is not optional. RomMBat installs into a portable RetroBat tree and must
not require a machine-wide .NET runtime.

---

## 2. Clone the projects you will be reading

RomMBat is written against two upstream codebases and mines a third for prior art. Keep
local checkouts; you will read them constantly.

```bash
gh repo clone rommapp/romm                     # the server: endpoints are the contract
gh repo clone rommapp/grout                    # mapping file shapes, sync state machine
gh repo clone rommapp/playnite-plugin          # C# DTOs, download queue
gh repo clone RetroBat-Official/retrobat       # systems_names.lst, es_systems.cfg
gh repo clone RetroBat-Official/emulatorlauncher   # batocera-systems.json, es_savestates.cfg
```

What each one settles is listed in [reference/README.md](reference/README.md). The files
whose contents the design actually depends on are vendored under `reference/`, so you can
work offline, but the full checkouts are worth having for the code around them.

### Turn the git hooks on, once per clone

```bash
git config core.hooksPath .githooks
```

Git will not use a checked-in hook until it is told where to look, and `core.hooksPath` is
local config, so every clone has to do this. Today it installs one `pre-push` hook that
refuses direct pushes to `main` and tells you to branch instead.

**It is convenience, not enforcement, and it has no bypass.** `main` is governed by a GitHub
ruleset: pull requests only, and the required status checks have to pass. The hook only says
so in under a second, where pushing anyway costs the upload and comes back as `GH013` with a
link to the rules. A local escape hatch could not get past the server, so there is not one.

---

## 3. Point at a RomM instance

RomM does not run comfortably on Windows and does not need to. Point the client at an
existing instance over the LAN.

You want **two** instances, for two different jobs.

### A real library, for reads

The whole selective-sync design exists because libraries reach six figures. A seeded dev
library would never reproduce the behaviour that motivated it, so read-side work should
run against a real, large instance.

**Treat that instance as production, because it is:**

- Use a **dedicated non-admin account** for RomMBat, with its own scoped token and its own
  registered device. RomMBat's writes (devices, `sync_config`, saves, play sessions) must
  never touch the primary account's data.
- Grant only the scopes in the [README table](README.md#authentication-and-scopes).
  Anything from `users.*`, `roms.write`, `platforms.write`, `tasks.run` or `logs.read` is
  over-scoped and is a bug in the request, not a convenience.
- Reads are unrestricted. Anything destructive belongs on the disposable instance.

### A disposable instance, for writes

Conflict resolution, overwrite paths, token expiry and revocation all want a server you
can reset. Run one in Docker or on a VM, per
[RomM's own setup docs](https://docs.romm.app).

You will need it for:

- Save conflicts and the `keep_both` path
- `POST /api/saves` returning 409 and the overwrite decision
- Token expiry and revocation, and the 401-is-expected behaviour
- Anything that creates devices you would then want to delete

### Pin the schema

Already pinned, and it does not come from either of the instances above.
`src/RomM.Client/openapi/romm-5.2.0.json` is a byte-exact `/openapi.json` (served at the
root, not under `/api`) from a server reporting **5.2.0**, the minimum RomMBat supports, so
the generated DTOs describe the oldest server the client claims to work with. Since the floor
tracks the newest stable, that is also the newest release. The preferred source is the
project's public demo, which anyone can reproduce from without an account and without a
hostname to scrub; the 5.2.0 pin came from a self-hosted instance because the demo had not
caught up, and the recorded sha256 is how that is checked rather than trusted.

```bash
cd src/RomM.Client/openapi && ./generate.sh    # only when deliberately moving the pin
```

Moving the pin is a compatibility decision, not a refresh: it changes which server version
the DTOs describe and moves a row in the README compatibility table with it. Read
[`src/RomM.Client/openapi/README.md`](src/RomM.Client/openapi/README.md) first, including
why the schema is normalised before NSwag sees it.

The published RomM docs have drifted from the server. **The backend is the contract**; the
schema is generated from it, and the docs are a hint. See
[the romm-api skill](.claude/skills/romm-api/SKILL.md).

### Local configuration

Your server URL and token never go in the repository. RomMBat stores both inside the
RetroBat tree at runtime, and `.gitignore` covers the dev-time equivalents (`.env`,
`*.local.json`, `*.token`). If you find yourself about to commit a hostname, stop.

### Pairing, the first time

Device pairing is the only authentication path, and it cannot be automated away from the
first run: someone has to approve the request in the RomM web UI. Have a browser open on
the same network before you start.

For **automated** tests, you do not need browser automation.
`GET /api/auth/device/pending/{user_code}` and `POST /api/auth/device/approve` are ordinary
protected routes, so a harness holding a pre-made token can play the approving user and
drive the real flow headlessly. Do it that way rather than adding a token-injection
backdoor, so the shipped client keeps exactly one auth path.

That harness is `tests/RomMBat.Tests/Support/ApprovingUser.cs`, and `LivePairingTests` drives
it. Those tests skip unless both variables are set, so a clone with no server still runs
green. Keep them in a `.env` at the repository root, which `.gitignore` already covers:

```bash
ROMMBAT_TEST_SERVER=https://your-romm-instance
ROMMBAT_TEST_APPROVER_TOKEN=rmm_...
```

Then source it for the run. `dotnet test` reads the process environment and nothing loads
`.env` on its own, so this is deliberate every time rather than ambient:

```bash
set -a; . ./.env; set +a; dotnet test
set -a; . ./.env; set +a; dotnet test --project tests/RomMBat.Tests --filter "FullyQualifiedName~LivePairingTests"
```

```powershell
# PowerShell, if you prefer not to keep the file
$env:ROMMBAT_TEST_SERVER = "https://your-romm-instance"
$env:ROMMBAT_TEST_APPROVER_TOKEN = "rmm_..."
dotnet test
```

**When these start failing, check the token first.** It is a `ClientToken` like any other,
so it expires on whatever `expires_in` it was created with and can be revoked from the RomM
UI. A revoked or lapsed token fails on `ReadPendingAsync` with a 401 rather than the 403
that means a missing scope.

**The approver token is not a RomMBat token, and the README scopes table does not apply to
it.** That table is what a RomMBat _device_ requests, and RomMBat never needs `me.write`.
The approving user is the other side of the same flow, and `/approve` and `/deny` are both
`@protected_route(..., [Scope.ME_WRITE])`. So:

|                               | Scopes                                     |
| ----------------------------- | ------------------------------------------ |
| The approver **token**        | `me.read` and `me.write`, and nothing else |
| The **account** it belongs to | All eleven from the README table           |

The split is because `allowed_scopes` is computed from `request.user.oauth_scopes`, the
account's permissions, while the route guard checks the token's. A token missing `me.write`
fails with a bare 403 `Forbidden` **before** the code is even looked up, which is how you
tell it apart from a scope-subset rejection: the latter says
`Approved scopes exceed what's allowed for this user`. An account short of the eleven fails
later and differently, on `Assert.Empty(completion.Scopes.Degradations)`.

RomM's `WRITE_SCOPES` tier covers all eleven, so an ordinary non-admin account at write
level is enough. No admin account is needed and none should be used.

**Run them under a dedicated non-admin account.** That, not the choice of instance, is what
keeps them safe: devices and client tokens are per-user rows, so an account of their own
cannot reach anyone else's data. The disposable instance is still the easier place to work,
but a real instance with a purpose-made account is a legitimate setup.

**The suite cleans up after itself**, in `PairingLitter` via `IAsyncLifetime.DisposeAsync`:
each test deletes the devices it created and revokes the tokens bound to them, and a test
fails if it cannot. That matters because every approval mints a genuine `rmm_` credential
carrying all eleven device scopes, whose local copy dies with the temp tree. Without
teardown a suite run leaves one set behind every time, and they accumulate.

The ordering inside teardown is forced by which credential holds what: only the token a
pairing just issued has `devices.write`, and only the approver can revoke tokens. So token
ids are captured first, devices deleted second, revocation last.

Neither environment value belongs in a file the repository tracks; `.env` at the repo root
is gitignored.

### Pairing by hand, without a UI

The gamepad UI pairs from the couch as of M7 stage 7b-1. The console agent does the same
thing without a window, ASCII QR included, which is what a headless or scripted install uses:

```powershell
dotnet run --project src/RomMBat.Agent -- pair --root D:\retrobat-test --server https://your-romm-instance
dotnet run --project src/RomMBat.Agent -- status --root D:\retrobat-test
```

`--root` is only needed when the agent is not running from inside the tree. Add `--protect`
to encrypt the stored token with a passphrase, and `--offline` to `status` to skip the
reachability probe.

### Running the gamepad UI at a desk

It runs standalone, with no EmulationStation in front of it and no controller plugged in:

```powershell
dotnet run --project src/RomMBat.UI -- --root D:\retrobat-test
```

**A physical keyboard drives it**, which exists so the interface can be worked on at a desk and
is deliberately not a supported user flow: arrows move, Enter is A, Escape is B, Backspace is
X, F5 is Start. With a controller connected it is read through the same `es_input.cfg` a real
install uses, so what you press at a desk is what a user presses on a sofa.

**Running standalone changes nothing about `es_settings.cfg`.** The UI never writes that file,
EmulationStation up or not, because the queue is the only path that exists and a test asserts
the assembly cannot even name the writer. "ES is always up" is a fact about how it is launched,
not a load-bearing assumption.

**A throwaway tree is a separate device in your RomM, and that has a trap in it.** Device
identity is a GUID in `emulators/rommbat/device.id`, so a test tree pairs as its own device.
To re-test pairing without collecting a device per attempt, **delete the store and keep
`device.id`**:

```powershell
Remove-Item D:\retrobat-test\emulators\rommbat\rommbat.db*
```

Pairing anchors on `client_device_identifier` and never on MAC or hostname, so the next pairing
updates the same RomM device rather than creating another. Deleting `device.id` as well is what
mints a new one.

**A store from a completed pairing holds a live token in the clear** unless it was made with
`--protect`. Do not copy one out of a tree, and do not paste the contents of the `device` table
anywhere: `token_cipher` holds the token itself when protection is `none`.

### Pulling content, without filling your disk

```powershell
dotnet run --project src/RomMBat.Agent -- sets add snes --scope platform --value snes --max-games 5 --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- budget --max 2GB --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- sync --dry-run --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- sync --root D:\retrobat-test
```

Start with a small `--max-games` and a `--max-bytes` against a real library, because the
default is the whole platform. `sync --dry-run` prints the plan and writes nothing, and it
works offline, so it is the cheap way to see what a set would cost before it costs it.

`evict` is a dry run unless you pass `--apply`, and it is the only command in the agent that
deletes anything. Partial downloads live in `emulators/rommbat/partial/`; deleting one by
hand is safe, and the next sync starts that ROM again. `evict` also reports what under that
directory is dead, and reclaims it on `--apply`, which is the only thing that ever does:
those bytes carry no database row, so the disk budget cannot count them and eviction proper
cannot reach them. The reclaim needs the tree lock, because one of the things under there is a
save being put back rather than litter, so `evict --apply` during a flush evicts and says the
sweep will happen next time.

### Metadata, media and gamelists

```powershell
dotnet run --project src/RomMBat.Agent -- gamelist --root D:
etrobat-test
dotnet run --project src/RomMBat.Agent -- gamelist snes --no-reload --root D:
etrobat-test
dotnet run --project src/RomMBat.Agent -- gamelist --media all --root D:
etrobat-test
```

`sync` already does all of this. `gamelist` is the same pass on its own, and it needs no
server: everything it writes comes from the local store, which is what lets it run on a
handheld that has been off the network for a week.

### Saves, playtime and the ES hooks

```powershell
dotnet run --project src/RomMBat.Agent -- hooks status --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- hooks install --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- menu status --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- menu install --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- saves --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- flush --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- flush --offline --root D:\retrobat-test

# Picking a side once a slot has conflicted. There is no default side.
dotnet run --project src/RomMBat.Agent -- saves resolve 42 "libretro:battery" --keep-local
dotnet run --project src/RomMBat.Agent -- saves resolve 42 "libretro:battery" --keep-server

# Saying which game a directory save belongs to, when the routes cannot or disagree.
dotnet run --project src/RomMBat.Agent -- saves bind psp ULUS10057 391
dotnet run --project src/RomMBat.Agent -- saves bind psp ULUS10057 --forget
```

`sync` installs the hooks **and the ES menu entry** on its first run and flushes before
anything else it does, so none of this is normally typed. `hooks uninstall` removes exactly
RomMBat's own file from each event folder and nothing else in them, and `menu uninstall`
removes its `.menu`, its artwork and its one `<game>` element, leaving the 93 entries
RetroBat put in that gamelist alone.

**The hook is its own executable and has to be published before it can be installed.** It is
not the agent: four copies are installed, one per event folder, so it is built small and
references nothing.

```powershell
dotnet publish src/RomMBat.Hook -c Release -r win-x64 --self-contained -o publish/hook
```

Copy the result to `<root>\emulators\rommbat\rommbat-hook.exe`, which is where
`hooks install` looks for it.

**The `start` and `quit` hooks trigger a pass; `game-start` and `game-end` do not.** Those
two run inside the game-launch path, so they write a spool file and exit having started
nothing, and the `start` or `quit` that brackets them picks the record up by spawning
`rommbat-agent background <event>`. So the agent has to be published and installed at
`<root>\emulators\rommbat\rommbat-agent.exe` for any of it to happen; a tree with hooks
and no agent simply spools, and the next `sync` drains it.

The pass writes what it did to `<root>\emulators\rommbat\logs\background.log`, which is
the only place to look, since it runs with no console window.

```powershell
dotnet publish src/RomMBat.Agent -c Release -r win-x64 --self-contained -o publish/agent
```

**Two tests skip until both executables have been published**, because they drive the real
binaries: the interleaved-hook one, and the rule-4 boundary that proves `game-start` and
`game-end` start nothing. CI publishes both before it tests, so a local `dotnet test` on its
own is the only place they are reported skipped.

`flush` is the only command that needs the lock. Draining the spool, correlating play sessions
and rescanning saves all work with the server unreachable, so `--offline` is a real mode rather
than a dry run. `saves` is the report of what is on disk, what has gone up, what cannot go up
and why, and what is waiting on a decision.

**Save states are pushed, never pulled.** `POST /api/states` has no slot, no device and no
conflict detection, so there is nothing to negotiate: a state goes up when its content changes
and never comes back down. The uploaded name is not the name on disk. It carries the emulator
and core, because the server keys a state on `(rom_id, file_name)` alone and two libretro cores
writing one filename for one game would otherwise become one row with the second silently
winning.

**A conflict now outlives the flush that found it, and `saves resolve` is how it ends.**
`--keep-local` is the only place in this codebase that sends `overwrite=true`. Both sides prune
the dated copy under `emulators/rommbat/replaced/` once the slot is back in step. It writes the
same save files a flush does, so it takes the same lock: run it while a flush is in flight and
it refuses with exit 3 rather than doing half of one.

**A directory save goes up as one archive, and `saves` names the unit rather than the path.**
Every PSP save on an install shares the container `saves/psp/SAVEDATA`, so the report prints
`<container>/<key>`. The key is a Game ID, worked out from the launch window, the ROM header or
the save-state sidecar, and `saves bind` is the way to correct one or to settle a binding two
routes disagreed on. A binding is local: there is nowhere on the server to put one.

**A shared container is split one game at a time, and `saves convert` is the only command that
changes the user's RetroBat configuration.** It writes
`<system>["<rom filename>"].<option>` into `es_settings.cfg`, which is the durable lever:
`emulatorlauncher` regenerates every emulator INI from ES options at launch, so an INI edit is
undone on the next boot.

```powershell
rommbat-agent.exe saves convert 191723            # preview: what it would set, and what it costs
rommbat-agent.exe saves convert 191723 --apply    # write it
rommbat-agent.exe saves convert 191723 --revert   # put the setting back to what it was
```

Four things about it are worth knowing before you drive it:

- **It refuses while EmulationStation is running.** ES loads `es_settings.cfg` at startup and
  serialises that model on every write, so a key written underneath it is discarded, merged and
  atomic or not. Measured. It matches on the running process's **path**, so an ES belonging to a
  different install on the same machine does not produce a refusal you cannot act on.
- **It re-reads the file after writing** and refuses to record the conversion if the key is not
  there, rather than trusting the rename.
- **The prior state is two states.** "The key was absent" and "the key held the stock value" are
  different files to restore, and `es_settings.cfg` cannot tell you which it was later: ES
  prunes a setting equal to its own default, and it also adds keys on its own. So the record
  stores which, and `--revert` restores absence by removing the key.
- **Reverting does not compare bytes, and neither should you.** ES rewrites `LastSystem` to
  record where the user was in the UI, so the file's hash moves for reasons that are nothing to
  do with RomMBat. Compare the setting set.

The card PCSX2 then writes is `saves/ps2/pcsx2/memcards/<rom stem>.ps2` -- the extension is
replaced, not appended, which is the opposite of the `es_settings.cfg` key, where the extension
is mandatory and omitting it fails silently.

**Artwork is fetched for covers, thumbnails, marquees and videos by default, and manuals are
opt-in.** At the sizes measured on a real library that is about 3.1 MB per game against
5.5 MB with manuals, and it counts against the same disk budget the ROMs do. `--media` takes
a comma-separated list, `all`, or `none`.

After writing, the agent asks EmulationStation to reload over
`http://127.0.0.1:1234/reloadgames`. That only answers while ES is running, and it is
**ignored outright while a game is up**, so a message saying the reload did not happen is
ordinary rather than a fault. `--no-reload` skips the call.

---

## 4. Stand up a throwaway RetroBat

RetroBat is portable by design, which makes it trivially disposable. That is also the
cleanest way to test the portable-move requirement and the first-run install path.

1. Download RetroBat 8.2.1 or newer from <https://www.retrobat.org/download/>. It is the
   declared minimum, and RomMBat refuses to run below it.
2. Extract it somewhere with room, for example `D:\retrobat-pristine\`.
3. Run it once so EmulationStation generates its config files. You need
   `.emulationstation/es_settings.cfg` and `es_savestates.cfg` to exist.
4. **Never test against the pristine copy.** Copy the whole tree per test run:

   ```powershell
   Remove-Item -Recurse -Force D:\retrobat-test -ErrorAction SilentlyContinue
   Copy-Item -Recurse D:\retrobat-pristine D:\retrobat-test
   ```

Confirm the version you are testing against. RomMBat reads it from `system/version.info` at
startup and refuses below the minimum:

```powershell
Get-Content D:\retrobat-test\system\version.info
# 8.2.1-stable-win64
```

There is no `build.ini` in RetroBat 8.2; M0 confirmed it does not exist anywhere in the
tree. Note the channel and architecture suffix, which has to be split off before the
version is compared.

**RomMBat tracks the newest RetroBat and RomM stable rather than supporting a wide range**,
so expect the floor to move. When it does, the work is: re-run `reference/refresh.sh` and
resolve the drift, read the upstream changelog for anything touching a rule in
`docs/retrobat-findings.md`, move `RetroBatVersion.Minimum`, `RetroBatVersion.LastTested`,
`RetroBatRoot.MinimumVersion` and the README compatibility row together, and re-check the
open upstream issues. The reasoning is in `docs/PLAN.md`, "Version compatibility is declared,
checked, and visible".

### Content

M0's probes all require launching real games; none of it can be desk-checked. Put ROMs on
the test install for wave 1 before you start: `nes`, `snes`, `gb`, `gbc`, `gba`,
`megadrive`, `mastersystem`.

Later waves need their BIOS too. `batocera-systems.json` in `reference/` lists exactly
which files, with md5s and destination paths.

### A USB stick

The portable-move test (M0 experiment 7) needs real removable media and a second machine.
Install to the stick, pair, sync a couple of games, change the drive letter, plug it into
another PC, and confirm nothing breaks: not root discovery, not the local file index, not
the ES menu entry, not the hooks, not the device identity.

Record the stick's filesystem and its mtime granularity while you are there. If you can
get hold of a FAT32-formatted one, that exercises the 4 GB ceiling for free.

---

## 5. Lint and verify

```bash
trunk fmt && trunk check
cd reference && python3 verify.py
```

**Trunk has no Windows-native CLI, so run it from WSL**, which is what its own install
instructions assume. From PowerShell:

```powershell
wsl -d Ubuntu -- bash -lc "cd '/mnt/d/path/to/rommbat' && trunk fmt && trunk check"
```

`trunk check` with no arguments checks modified files only; add `--all` before a release.
If WSL is not an option, the markdown half can be reproduced with the versions pinned in
`.trunk/trunk.yaml`, which is enough for a docs-only change but is not a substitute:

```powershell
npx prettier@3.7.4 --write <files>
npx markdownlint-cli@0.45.0 -c .trunk/configs/.markdownlint.yaml <files>
```

`verify.py` re-derives every upstream number `docs/PLAN.md` quotes. **A drift there means
an upstream fact moved, so the fix is to revisit the plan, not to update the expected
number.** Never hand-edit a vendored file under `reference/`; use `./refresh.sh` and
review the diff.

To re-pull upstream data:

```bash
cd reference && ./refresh.sh
```

`refresh.sh` is a shell script, so run it from Git Bash or WSL on Windows. It ends by
checking the two bundled data files derived from this data, `data/retrobat/bios.json` and
`data/retrobat/platforms.json`, and exits non-zero naming the generator to run if either has
gone stale. Regenerating is left to you, because the diff is the point.

---

## 6. Where things live at runtime

Everything RomMBat owns lives inside the RetroBat tree. Nothing goes to `%APPDATA%`, the
registry, a service or a scheduled task. **M0 probe 4 settled the subdirectory and it is
not a free choice**: a `.menu` entry resolves its executable under `emulators\` and
`emulatorLauncher` refuses `..\` escapes, so anything launched from the ES menu must live
there.

```text
<RetroBat root>/
  emulators/rommbat/      forced by the .menu path rules, see retrobat-findings.md probe 4
    rommbat-agent.exe
    RomMBat.exe
    rommbat.db            SQLite: file index, sync sets, outbox, cursors
    device.id             the client_device_identifier GUID
    logs/
    outbox/
  roms/<system>/          ROMs, gamelist.xml, images/, videos/, manuals/
  bios/                   firmware, at the paths batocera-systems.json specifies
  saves/<system>/<emulator>/   emulator save output, two levels deep
  emulationstation/
    emulatorLauncher.exe  what %~dp0..\..\..\ from a hook resolves to
    .emulationstation/
      es_settings.cfg     RetroBat options, including the per-game override form
      es_savestates.cfg   per-emulator save-state schema
      es_features.cfg     the per-game option definitions (memory cards, VMUs)
      scripts/<event>/    the .bat hooks; reach the root with %~dp0..\..\..\..\
  system/es_menu/
    rommbat.menu          line 1 the exe path, relative to emulators/
    gamelist.xml          must also carry a <game> entry or the app shows as a filename
    media/
      rommbat-logo.png    the artwork that entry points at, written by menu install
  system/version.info     the version string, e.g. 8.2.1-stable-win64
```

When you are done with a test run, delete the copied tree. That is the whole uninstall.

---

## 7. Before you open a PR

See [CONTRIBUTING.md](CONTRIBUTING.md) and the `pre-pr-verification` skill.

```bash
dotnet build                    # no new warnings
dotnet test                     # full suite green
trunk fmt && trunk check
cd reference && python3 verify.py
```

And disclose AI assistance. It is not optional here.
