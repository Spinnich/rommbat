---
summary: Where the test server, tokens and install paths live on a developer machine, what each key is for, and how every kind of session reads them.
read-when: Before a live test, a probe, a hands-on pass or any call to the test RomM, or when a token looks missing or answers 403.
---

# Local secrets

**Everything a session needs to reach the test server or an install is in one file: `.env` at the
root of the main checkout.** Git ignores it (`.gitignore` covers `.env`, `*.local.json` and
`*.token`), so it is never committed, and no doc, comment or PR body quotes a value from it.

A worktree has no `.env`. Read the main checkout's, which is what the tools below do. Run
`python3 tools/localenv.py` first in any session: it names the file it found and prints each key
as set or missing, never a value.

## The keys

| Key                           | Is                                                              | Reach for it when                                                                   |
| ----------------------------- | --------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `ROMMBAT_TEST_SERVER`         | The RomM instance the live tests and probes call                | Any call to the server                                                              |
| `ROMMBAT_TEST_APPROVER_TOKEN` | A token on the approver account: `me.read` and `me.write`, more | Pairing, the live tests, `tools/romm-5.3-probes/`, and reading the library or saves |
| `ROMMBAT_TEST_OWNER_TOKEN`    | A token on the account an install is paired as, narrowly scoped | Reading an install's play sessions, or staging a second device's save (see below)   |
| `ROMMBAT_TEST_DEV_TOKEN`      | A token on the test account holding every scope it can have     | Keeping the [test-game corpus](../platforms/corpus.md) with `tools/corpus.py`       |
| `ROMMBAT_AGENT_ROOT`          | The agent's own RetroBat tree                                   | Every hands-on pass and `/certify`, through `tools/handson/`                        |
| `ROMMBAT_MAINTAINER_ROOT`     | The maintainer's own tree                                       | Read only, and only when the tree is mounted                                        |
| `ROMMBAT_SCOUT_ROOT`          | The scout tree, which runs a RetroBat prerelease                | A scout pass, through `Use-ScoutTree`                                               |

**The owner token answering `403` on `/api/users/me`, `/api/platforms` and `/api/saves` is
correct, not a dead token.** Its scopes are only what a pass needs
([the owner token](testing.md#the-owner-token)). Use the approver token for a general read of the
test account's library, and remember that an install's saves, states and play sessions belong to
the owner's account, so they look empty under the approver's.

## Reading them

| From                         | Do                                                                                                                                                                    |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| PowerShell, the hands-on kit | Nothing. `Import-Module ./tools/handson/HandsOn.psm1` reads the main checkout's `.env`                                                                                |
| Python                       | `import localenv; localenv.apply()` with `tools/` on the path. `_common.py` in the probes does it                                                                     |
| `dotnet test`                | Export the variables first. Nothing in .NET loads `.env`                                                                                                              |
| Bash                         | **Do not `. ./.env`.** A value such as `D:\retrobat-agent` loses its backslashes, and one with a space runs the rest of the line as a command. Read it through Python |

`.env` is the only copy. `.claude/settings.local.json` carries none of these keys, and a
worktree needs no copy of its own, so rotating a token is one edit. A session's environment
therefore starts without them: export them from `.env` before a live `dotnet test`, which is the
one reader that cannot find the file itself.

```powershell
python3 tools/localenv.py --powershell | Invoke-Expression
```

## Rules

- **Never print, log, commit or paste a token or the server's host.** Probes redact both
  (`tools/romm-5.3-probes/_common.py`), and a transcript or PR body names a key, not its value.
- Never read a token out of `rommbat.db`. Claude Code's permission classifier refuses that as
  credential handling, and the paired token is the install's own.
- Never `pip install` anything to get at them. `tools/localenv.py` uses the standard library.
- A key that is missing is the maintainer's to supply. Say which one and which pass it leaves
  unproven, and carry on with the rest.

Setting up a new machine, including creating the approver token, is in
[the developer setup](../../DEVELOPER_SETUP.md#the-live-tests).
