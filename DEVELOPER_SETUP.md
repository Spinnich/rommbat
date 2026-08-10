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

**It is a habit, not a security control**, and it is bypassable on purpose:

```bash
ALLOW_MAIN_PUSH=1 git push
```

The reason it exists locally at all is that GitHub does not offer branch protection or
rulesets on private repositories on the Free plan, so there is nothing enforcing this
server-side. Replace it with a ruleset if the repository goes public.

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
`src/RomM.Client/openapi/romm-5.1.0.json` is a byte-exact `/openapi.json` (served at the
root, not under `/api`) from a server reporting **5.1.0**, the minimum RomMBat supports, so
the generated DTOs describe the oldest server the client claims to work with. It was pulled
from the project's public demo, which means anyone can reproduce it without an account and
without a hostname that would have to be scrubbed from a diff.

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
set -a; . ./.env; set +a; dotnet test --filter "FullyQualifiedName~LivePairingTests"
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

The gamepad UI is chosen in M7, so the pairing surface today is the console agent, ASCII QR
included:

```powershell
dotnet run --project src/RomMBat.Agent -- pair --root D:\retrobat-test --server https://your-romm-instance
dotnet run --project src/RomMBat.Agent -- status --root D:\retrobat-test
```

`--root` is only needed when the agent is not running from inside the tree. Add `--protect`
to encrypt the stored token with a passphrase, and `--offline` to `status` to skip the
reachability probe.

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
hand is safe, and the next sync starts that ROM again.

---

## 4. Stand up a throwaway RetroBat

RetroBat is portable by design, which makes it trivially disposable. That is also the
cleanest way to test the portable-move requirement and the first-run install path.

1. Download RetroBat 8.2 or newer from <https://www.retrobat.org/download/>.
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
# 8.2.0-stable-win64
```

There is no `build.ini` in RetroBat 8.2; M0 confirmed it does not exist anywhere in the
tree. Note the channel and architecture suffix, which has to be split off before the
version is compared.

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

`refresh.sh` is a shell script, so run it from Git Bash or WSL on Windows.

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
  system/version.info     the version string, e.g. 8.2.0-stable-win64
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
