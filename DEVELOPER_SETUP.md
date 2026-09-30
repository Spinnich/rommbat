# Developer setup

The steps to build RomMBat, point it at a RomM instance, and stand up a throwaway RetroBat to
test against. Development happens on Windows, the target platform: RomMBat drives
EmulationStation hooks, reads RetroBat's config files and publishes `win-x64`.

How the code is laid out is in [docs/architecture/](docs/architecture/README.md), and how to run
the tests beyond a plain `dotnet test` is in [docs/contributing/testing.md](docs/contributing/testing.md).

---

## 1. Toolchain

| Tool                   | Why                                 | Get it                                                                  |
| ---------------------- | ----------------------------------- | ----------------------------------------------------------------------- |
| .NET SDK 10.0 or newer | Build and test                      | <https://dotnet.microsoft.com/download>                                 |
| Git                    | Source control                      | <https://git-scm.com/download/win>                                      |
| Python 3.10+           | `reference/verify.py`, docs checks  | <https://www.python.org/downloads/>                                     |
| Trunk                  | Lint and format                     | `curl -fsSL https://trunk.io/releases/trunk -o trunk` (WSL or Git Bash) |
| GitHub CLI             | Reading upstream repos, opening PRs | <https://cli.github.com/>                                               |

Verify:

```bash
dotnet --list-sdks     # 10.0.x or newer
python3 --version      # 3.10+
git --version
```

`global.json` pins the minimum SDK with `rollForward: latestMajor`, so a newer SDK works and no
SDK fails loudly.

### Build

```bash
dotnet restore
dotnet build
dotnet test
```

`dotnet test` is Microsoft.Testing.Platform, not VSTest, and a wrong option makes it report
`Zero tests ran`. Read the `pre-pr-verification` skill before passing it anything.

Packages are managed centrally in `Directory.Packages.props`: add the version there and a bare
`<PackageReference Include="..." />` in the project. `dotnet tool restore` is needed only to
move the pinned OpenAPI schema; the generated DTOs are committed.

### Publish

```powershell
./tools/publish.ps1                          # publish, assemble the seven files, zip
./tools/publish.ps1 -Deploy D:\retrobat-test # and copy into an install
```

This is what CI runs. It writes `publish/rommbat-win-x64.zip`, which extracts at the RetroBat
root. `-Deploy` is also what puts `rommbat-hook.exe` and `rommbat-agent.exe` into
`emulators/rommbat/`, where `hooks install` copies the hook from, so deploy into a fresh tree
before installing hooks there. What the seven files are and how the script guards them is in
[docs/architecture/projects.md](docs/architecture/projects.md#srcrommbatui).

---

## 2. Clone the projects you will be reading

RomMBat is written against two upstream codebases and mines a third for prior art:

```bash
gh repo clone rommapp/romm                     # the server: endpoints are the contract
gh repo clone rommapp/grout                    # mapping file shapes, sync state machine
gh repo clone rommapp/playnite-plugin          # C# DTOs, download queue
gh repo clone RetroBat-Official/retrobat       # systems_names.lst, es_systems.cfg
gh repo clone RetroBat-Official/emulatorlauncher   # batocera-systems.json, es_savestates.cfg
```

What each one settles is in [reference/README.md](reference/README.md). The files the design
depends on are vendored under `reference/`.

Turn the git hooks on, once per clone:

```bash
git config core.hooksPath .githooks
```

Its one `pre-push` hook refuses a direct push to `main`. The GitHub ruleset on `main` enforces
the same thing server-side; the hook only says so sooner.

---

## 3. Point at a RomM instance

RomM does not need to run on Windows. Point the client at instances over the LAN, two of them:

1. **A real library, for reads.** Selective sync exists because libraries reach six figures, and
   a seeded one never reproduces that. Treat it as production: give RomMBat a dedicated
   non-admin account with its own token and device, and grant only the scopes in the
   [guide's table](wiki/getting-started/pairing.md#which-permissions-to-grant).
2. A disposable instance, for writes, in Docker or a VM per
   [RomM's setup docs](https://docs.romm.app). Save conflicts, `POST /api/saves` answering 409,
   token expiry and revocation, and anything that creates devices belong here.

The schema is already pinned; moving it is in
[src/RomM.Client/openapi/README.md](src/RomM.Client/openapi/README.md). The backend is the
contract, not RomM's published docs (`romm-api` skill).

Server URLs and tokens never go in the repository. `.gitignore` covers `.env`, `*.local.json`
and `*.token`.

### The live tests

The live tests pair headlessly through `tests/RomMBat.Tests/Support/ApprovingUser.cs`, which
plays the approving user with a pre-made token. They skip unless both variables are set.

1. On the account the tests will run as (a non-admin account at RomM's write level), create a
   client token with `me.read` and `me.write` and nothing else. Why those two is in
   [the testing doc](docs/contributing/testing.md#the-approver-token).
2. Put both values in a `.env` at the repository root:

   ```bash
   ROMMBAT_TEST_SERVER=https://your-romm-instance
   ROMMBAT_TEST_APPROVER_TOKEN=rmm_...
   ```

   For hands-on passes, add `ROMMBAT_TEST_OWNER_TOKEN`, a token on the account an install is
   paired as ([the owner token](docs/contributing/testing.md#the-owner-token)).

3. Source it for the run. Nothing loads `.env` on its own:

   ```bash
   set -a; . ./.env; set +a; dotnet test
   set -a; . ./.env; set +a; dotnet test --project tests/RomMBat.Tests --filter "FullyQualifiedName~LivePairingTests"
   ```

   ```powershell
   $env:ROMMBAT_TEST_SERVER = "https://your-romm-instance"
   $env:ROMMBAT_TEST_APPROVER_TOKEN = "rmm_..."
   dotnet test
   ```

With the variables exported, every `dotnet test` pairs against the server and is subject to its
rate limit. Read [the live suite](docs/contributing/testing.md#the-live-suite) before looping it.

### Running the agent and the UI

Pair a throwaway tree, then run the UI against it. It runs standalone, with no EmulationStation
and no controller:

```powershell
dotnet run --project src/RomMBat.Agent -- pair --root D:\retrobat-test --server https://your-romm-instance
dotnet run --project src/RomMBat.UI -- --root D:\retrobat-test
```

The desk keyboard map is in [src/RomMBat.UI/CLAUDE.md](src/RomMBat.UI/CLAUDE.md). What each
screen and command does for a user is in the guide under [wiki/](wiki/README.md), which says how
to preview it.

---

## 4. Stand up a throwaway RetroBat

RetroBat is portable, so a copy is disposable.

1. Install one from a PowerShell 7 prompt. It needs `gh` and about 6 GB:

   ```powershell
   ./tools/retrobat-install.ps1 -Path D:\retrobat-pristine                         # newest stable
   ./tools/retrobat-install.ps1 -Version prerelease -Path D:\retrobat-beta          # newest of any kind
   ./tools/retrobat-install.ps1 -Version 8.2.1 -Path D:\retrobat-8.2.1              # an exact tag
   ```

   The installer is cached under `%LOCALAPPDATA%\rommbat-dev\retrobat-installers` and checked
   against the sha256 upstream publishes. Nothing prunes that folder. The script extracts the ZIP
   the setup.exe carries and warns about any optional system-wide prerequisite (Visual C++,
   DirectX, Dokany, WinFsp) it cannot find. By hand instead: run the setup.exe from
   <https://www.retrobat.org/download/> into an empty folder.

2. Nothing needs a first launch. The `es_*.cfg` files ship in
   `emulationstation\.emulationstation\`. Emulators other than RetroArch download the first time
   a game needs them.
3. **Never test against the pristine copy.** Copy the tree per test run:

   ```powershell
   Remove-Item -Recurse -Force D:\retrobat-test -ErrorAction SilentlyContinue
   Copy-Item -Recurse D:\retrobat-pristine D:\retrobat-test
   ```

4. Confirm the version. RomMBat refuses anything below the floor in the root `CLAUDE.md`:

   ```powershell
   Get-Content D:\retrobat-test\system\version.info
   # 8.2.1-stable-win64
   ```

5. Put ROMs on it for the systems you are working on, and for later certification waves their
   BIOS too. `reference/batocera-systems.json` lists the files, with md5s and destination paths.
   Where RomMBat's own files land in the tree is in
   [docs/architecture/writing-into-the-tree.md](docs/architecture/writing-into-the-tree.md#where-rommbats-files-live).

For the portable-move test, install to a USB stick, pair, sync a couple of games, change the
drive letter or move it to another PC, and confirm root discovery, the file index, the ES menu
entry, the hooks and the device identity all still work. A FAT32 stick also exercises the 4 GB
ceiling.

When you are done with a test run, delete the copied tree.

---

## 5. Lint and verify

```bash
pwsh -File tools/pre-pr.ps1     # every gate below, plus the Release build and the tests
trunk fmt && trunk check
python3 tools/docs/check.py
mkdocs build --strict           # the guide, after pip install -r tools/docs/requirements.txt
cd reference && python3 verify.py
```

What `check.py` enforces is in [docs/contributing/writing.md](docs/contributing/writing.md).

Trunk has no Windows-native CLI, so run it from WSL:

```powershell
wsl -d Ubuntu -- bash -lc "cd '/mnt/d/path/to/rommbat' && trunk fmt && trunk check"
```

`trunk check` with no arguments checks modified files only; add `--all` before a release. If WSL
is not an option, the markdown half can be reproduced with the versions pinned in
`.trunk/trunk.yaml`, enough for a docs-only change but no substitute:

```powershell
npx prettier@3.7.4 --write <files>
npx markdownlint-cli@0.45.0 -c .trunk/configs/.markdownlint.yaml <files>
```

To re-pull upstream data, from Git Bash or WSL:

```bash
cd reference && ./refresh.sh
```

It ends by checking `data/retrobat/bios.json` and `data/retrobat/platforms.json` against the new
data and names the generator to run if either has gone stale.

---

## 6. Before you open a PR

See [CONTRIBUTING.md](CONTRIBUTING.md) and the `pre-pr-verification` skill, and disclose AI
assistance.
