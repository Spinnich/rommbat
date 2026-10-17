---
summary: Running the suite by hand, the process-level tests' publish, and the live suite's tokens, rate limit and cleanup.
read-when: Before running the live tests, chasing a test that skips, or timing the suite.
---

# Running the tests

`dotnet test` is Microsoft.Testing.Platform, and its two option traps are in the
`pre-pr-verification` skill ("Always"). What it costs in CI is under "Test cost" there.

## Per-test timings

Each built test project is an executable running xunit's own console runner, which writes
every test's duration with `-xml <file>` and scopes the run with `-class` or `-method`:

```bash
tests/RomMBat.Tests/bin/Debug/net10.0/RomMBat.Tests.exe -xml timings.xml
```

In CI, every `passed` line of the Test step's log carries its duration too.

## The process-level tests need a default publish

Two tests drive the real binaries and skip until both have been published: the interleaved-hook
one, and the rule-4 boundary that proves `game-start` and `game-end` start nothing. They look in
each project's **default** publish directory, so `tools/publish.ps1` does not satisfy them: it
passes `-o`. Publish without it, which is what CI does in a separate step:

```powershell
dotnet publish src/RomMBat.Hook  -c Release -r win-x64 --self-contained
dotnet publish src/RomMBat.Agent -c Release -r win-x64 --self-contained
```

## Walking screens without a window

Every screen is driven with the gamepad map alone. `SetsScreenTests` does it end to end
against `StubRomMServer`:

```csharp
// The root's Sync sets row; connect stands a stub in for the server.
var navigator = new Navigator(SetsScreens.List(session, connect));
navigator.Handle(NavAction.Accept);                                      // open the set

using var sync = Assert.IsType<SyncViewModel>(
    Navigator.Press(navigator.Current, NavAction.Alternate).Screen);     // Sync, on its shortcut
```

A confirm screen answers `Accept`, not `Start`, and Start only ever opens a screen's actions
menu: drive an action with `Navigator.Press` or `Navigator.Run`, since a screen's own `Handle`
never sees Start or a shortcut. A screen of facts has no cursor and scrolls by an
offset, so assert on `Window.Start`, since `Cursor` is always `-1` there. Everything on the sets
surface but resolving and syncing is local, so most screens need only seeded store rows and no
stub at all.

## The live suite

The live tests skip unless `ROMMBAT_TEST_SERVER` and `ROMMBAT_TEST_DEV_TOKEN` are set.
Setting them up is
[the developer setup](../../DEVELOPER_SETUP.md#the-live-tests). Once they are exported, every `dotnet test`
is a networked operation: the tests pair against the real server, minting and revoking real
credentials on the test account, with no warning first.

**Pairing is limited to 10 per minute per IP.** One run of all four `Live*` classes pairs about
nine times, so two runs inside a minute share the budget and the server answers
`Too many authorize attempts. Try again later.` `LivePairingTests` skips on that rather than
failing, naming the limit and the wait, so four skips with that message mean wait a minute.
When looping the suite to chase an intermittent, leave 75 s between full runs; back to back, 15
of 19 runs failed on the limit and proved nothing. `LiveContentTests` alone pairs once and needs
no gap.

Before looping any live test, check that none of its requests does server work that outlives a
client timeout. An abandoned `GET /api/roms/identifiers` keeps loading the whole library in a
web worker, and sixty looped runs of one took the server's container from 2 GB to 20.9 GiB
(rommapp/romm#4577). No test calls it.

A run that touches nothing unsets both:

```bash
env -u ROMMBAT_TEST_SERVER -u ROMMBAT_TEST_DEV_TOKEN dotnet test
```

### The test tokens

Both are `ClientToken`s on one dedicated non-admin account, the test account, so each expires on
its `expires_in` and can be revoked from the RomM UI. **When the live tests start failing, check
the dev token first**: a lapsed or revoked token fails `ReadPendingAsync` with a 401, where a
missing scope is a 403.

|                         | Scopes                                                                                                         | Is for                                                                                                      |
| ----------------------- | -------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| The dev token           | Every scope the test account can hold                                                                          | Approving pairings, the live tests, the probes, `tools/handson/`, `tools/firmware.py` and `tools/corpus.py` |
| The user token          | The eleven in [the guide's table](../../wiki/getting-started/pairing.md#which-permissions-to-grant), no others | Reading or writing as an install would, from outside one                                                    |
| The test account itself | At least the eleven, and `me.write`                                                                            | Being what the agent tree and the live tests pair as                                                        |

The dev token is not a RomMBat token. `/approve` and `/deny` are `[Scope.ME_WRITE]` routes, a
scope no device is granted, while `allowed_scopes` is computed from the account's permissions,
so the approving token needs `me.write` and its account needs the eleven. A token missing
`me.write` fails with a bare 403 `Forbidden` before the code is looked up. A scope-subset
rejection says `Approved scopes exceed what's allowed for this user` instead. An account short
of the eleven fails later, on `Assert.Empty(completion.Scopes.Degradations)`. RomM's
`WRITE_SCOPES` tier covers all eleven, so no admin account is needed.

The live tests that read a library pair first and read with the device token they are issued,
not the dev token, because the dev token holds more than RomMBat asks for and would hide a route
that needs a scope a device is never granted.

### Cleanup

`PairingLitter` tears down in `IAsyncLifetime.DisposeAsync`: each test deletes the devices it
created and revokes the tokens bound to them, and fails if it cannot, since every approval mints
a genuine `rmm_` credential whose local copy dies with the temp tree. Each device is deleted with
the token its own pairing issued, as an install would, and the dev token's `me.write` revokes
tokens. So token ids are captured first, devices deleted second, revocation last.

### The user token

Where it lives and how to read it: [local secrets](local-secrets.md).

`ROMMBAT_TEST_USER_TOKEN` holds what a paired device holds, on the test account the agent tree
is paired as, for hands-on passes. No test reads it. Reach for `rommbat-agent status` first,
which reads `GET /api/play-sessions` back for this device and prints the newest ten
(`--all-sessions` for up to 50); the user token is for when the paired token cannot be used.

- It reads play sessions, the certification step 8 check, and saves and states.
- It can stage a second device's save: a slotted upload with no `device_id` into a slot the
  install has never synced, as the #211 pass in [the nes conflicts record](../platforms/nes/conflicts.md)
  did.
- A route that answers it `403` is one a paired install cannot reach either.

**Under any other account an install's data looks empty, not forbidden**, because saves, states
and play sessions are per-user. `GET /api/states?rom_id=` and `GET /api/play-sessions?rom_id=`
answer `200` with zero rows, `GET /api/roms/{id}` comes back with `rom_user.user_id: -1`, and
`GET /api/devices/<id>` answers `404`. The `?device_id=` filter takes the RomM-side device
id, the one `status` prints on its `romm device` line.

So read what an install pushed as the install: `status`, `saves`, and a `saves restore` preview.
Never read the token out of `rommbat.db` to call the API yourself; Claude Code's permission
classifier refuses that as credential handling. When only a direct API read will answer, use the
user token or ask the maintainer to run it.

### The agent tree

Hands-on passes run on a RetroBat tree of the agent's own, named by `ROMMBAT_AGENT_ROOT` and
paired as the test account, so its saves and play sessions never mix with the maintainer's.
`tools/handson/` pairs it headlessly, approving with the dev token, and re-pairs it keeping
`device.id`. [Its README](../../tools/handson/README.md) has the functions and
how to rebuild the tree; what a change owes is in `pre-pr-verification`.

### Re-pairing a throwaway tree

A test tree pairs as its own device, since identity is the GUID in
`emulators/rommbat/device.id`. To re-test pairing without collecting a device per attempt,
delete the store and keep `device.id`:

```powershell
Remove-Item D:\retrobat-test\emulators\rommbat\rommbat.db*
```

The next pairing updates the same RomM device ([identity](../architecture/identity.md)). Deleting
`device.id` as well mints a new one. A store from a completed pairing holds a live token in the
clear unless it was made with `--protect`, so never copy one out of a tree or paste its `device`
table anywhere.
