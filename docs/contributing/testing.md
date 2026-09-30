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

Every screen is driven with the gamepad map alone. `BrowseScreenTests` does it end to end
against `StubRomMServer`:

```csharp
// BrowseViewModel.Start(session) opens the platform list; this is the list itself.
using var browse = new BrowseViewModel(session, connect);   // connect stands a stub in
await Settled(browse);                                      // poll IsLoading

var navigator = new Navigator(browse);
navigator.Handle(NavAction.Accept);                         // open the game
navigator.Handle(NavAction.Start);                          // install it
```

A confirm screen answers `Accept`, not `Start`. A screen of facts has no cursor and scrolls by an
offset, so assert on `Window.Start`, since `Cursor` is always `-1` there. With no `connect`
factory and no pairing, browse lists what the tree holds, so the offline half needs only seeded
`local_file` rows; `BrowseViewModel.Note` says which of the two it is showing.

## The live suite

The live tests skip unless `ROMMBAT_TEST_SERVER` and `ROMMBAT_TEST_APPROVER_TOKEN` are set.
Setting them up is
[the developer setup](../../DEVELOPER_SETUP.md#the-live-tests). Once they are exported, every `dotnet test`
is a networked operation: 21 tests pair against the real server, minting and revoking real
credentials on the approver's account, with no warning first.

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
env -u ROMMBAT_TEST_SERVER -u ROMMBAT_TEST_APPROVER_TOKEN dotnet test
```

### The approver token

It is a `ClientToken` on a dedicated non-admin account, so it expires on its `expires_in` and
can be revoked from the RomM UI. **When the live tests start failing, check it first**: a lapsed
or revoked token fails `ReadPendingAsync` with a 401, where a missing scope is a 403.

It is not a RomMBat token, and the README scopes table does not apply to it. `/approve` and
`/deny` are `[Scope.ME_WRITE]` routes, while `allowed_scopes` is computed from the account's
permissions, so the two need different scopes:

|                           | Scopes                                     |
| ------------------------- | ------------------------------------------ |
| The approver token        | `me.read` and `me.write`, and nothing else |
| The account it belongs to | All eleven from the README table           |

A token missing `me.write` fails with a bare 403 `Forbidden` before the code is looked up. A
scope-subset rejection says `Approved scopes exceed what's allowed for this user` instead. An
account short of the eleven fails later, on `Assert.Empty(completion.Scopes.Degradations)`.
RomM's `WRITE_SCOPES` tier covers all eleven, so no admin account is needed.

### Cleanup

`PairingLitter` tears down in `IAsyncLifetime.DisposeAsync`: each test deletes the devices it
created and revokes the tokens bound to them, and fails if it cannot, since every approval mints
a genuine `rmm_` credential whose local copy dies with the temp tree. The order is forced by who
holds what: only the token a pairing just issued has `devices.write`, and only the approver can
revoke tokens. So token ids are captured first, devices deleted second, revocation last.

### The owner token

`ROMMBAT_TEST_OWNER_TOKEN` is a token on the account a real install is paired as, for
hands-on passes. No test reads it. Reach for `rommbat-agent status` first, which reads
`GET /api/play-sessions` back for this device and prints the newest ten (`--all-sessions` for
up to 50); the owner token is for when the paired token cannot be used.

- With `roms.user.read` it reads play sessions, the certification step 8 check.
  `GET /api/roms/{id}` is `403` under that scope alone, so read the session row rather than
  `last_played`.
- With `roms.read` and `assets.write` as well it can stage a second device's save: a slotted
  upload with no `device_id` into a slot the install has never synced, as the #211 pass in
  [the nes conflicts record](../platforms/nes/conflicts.md) did.
- It cannot read `GET /api/saves` without `assets.read`.

**Under any other account an install's data looks empty, not forbidden**, because saves, states
and play sessions are per-user. `GET /api/states?rom_id=` and `GET /api/play-sessions?rom_id=`
answer `200` with zero rows, `GET /api/roms/{id}` comes back with `rom_user.user_id: -1`, and
`GET /api/devices/<id>` answers `404`. The `?device_id=` filter takes the RomM-side device
id, the one `status` prints on its `romm device` line.

So read what an install pushed as the install: `status`, `saves`, and a `saves restore` preview.
Never read the token out of `rommbat.db` to call the API yourself; Claude Code's permission
classifier refuses that as credential handling. When only a direct API read will answer, use the
owner token or ask the maintainer to run it.

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
