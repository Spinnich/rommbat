---
summary: What RomM.Client, Core, the agent, the UI and the two test projects hold, their subcommands or screens, and the rules each keeps.
read-when: Before adding a class, a subcommand, a screen or a test project, to find where it belongs.
---

# Projects

## `src/RomM.Client`

The RomM API, and nothing else. No disk, no SQLite, no RetroBat.

- **DTOs are generated** from `/openapi.json` (served at the root, not under `/api`),
  pinned to a known RomM version and checked in. The published docs have drifted from the
  server, so the backend is the contract. **NSwag**, contracts only: it emits plain POCOs
  with `System.Text.Json` attributes and no runtime package of its own, where Kiota would
  generate a request-builder API over `Microsoft.Kiota.Abstractions` that owns the
  `HttpClient`. Owning the handler is not negotiable here; see the connect timeout below.
  The pin, the normalisation step it needs, and how to move it are in
  [`src/RomM.Client/openapi/README.md`](../../src/RomM.Client/openapi/README.md).
- **Everything else is hand-written** over a client-owned handler: the device pairing poll
  loop, resumable downloads, multipart save upload, sync negotiation.
- Every call takes a `CancellationToken`, and **`SocketsHttpHandler.ConnectTimeout` is set
  explicitly on every handler**, because nothing sets it by default and an absent host on
  the local subnet otherwise stalls for 21 seconds (RB-353). 2 s is the interactive
  budget. `HttpClient.Timeout` is set too, for a different reason: it covers an API call and
  its JSON body, and a slow server that is still reachable, so it cannot be the reachability
  lever. A streamed transfer's body is bounded by the stall watchdog (`StallTimeout`) instead.
- **A timeout and a user cancellation are the same exception type.** Both surface as
  `TaskCanceledException` and differ only in the inner exception (RB-403), so every failure goes
  through `RomMTransportErrors.Classify` rather than a bare `catch`. A naive catch reports
  every offline server as a user action.
- **Never throws on 401.** An expired or revoked token is an expected state, not an
  exception, and it must never cost data. Authenticated calls return `RomMResponse<T>`
  carrying `Unauthorized` or `Forbidden`; only transport failures throw. See [Being offline is the normal case](offline.md).

Prior art to mine, not copy wholesale: the Playnite plugin's `Models/RomM/*` for DTO
shapes and `Downloads/DownloadQueueController.cs` for the queue. Its `RomMRegisterDevice`
carries `mac_address` and `hostname`, which is right for a fixed desktop and **wrong
here**; see [Identity](identity.md).

## `src/RomMBat.Core`

Local state, plus everything that knows RetroBat's disk layout. The largest project and
the one with all the interesting invariants.

| Area             | Responsibility                                                                                                                                                                                                                                                 |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Root discovery   | Walk up from `AppContext.BaseDirectory` to a marker (`retrobat.ini`, `emulationstation/`, `roms/`). Registry and fixed-path lookups are a last-resort fallback, never primary                                                                                  |
| Path resolution  | The single place a relative stored path becomes an absolute one. Nothing else concatenates a root                                                                                                                                                              |
| Local store      | SQLite: file index, sync sets, outbox, cursors, learned bindings                                                                                                                                                                                               |
| RetroBat readers | `es_systems.cfg` (folders and `<extension>`), `es_savestates.cfg` (state schema, with a bundled supplement beneath it), `es_features.cfg` (per-game options), `system/version.info` (version)                                                                  |
| RetroBat writers | `gamelist.xml` and `es_settings.cfg`, both merge-not-clobber and atomic. The second also refuses to run while ES is up, because ES discards writes made underneath it                                                                                          |
| Mapping          | Platform resolution chain, save-directory map, save-shape classification                                                                                                                                                                                       |
| Sync             | Set resolution, disk budget and eviction, negotiation state machine, outbox flush                                                                                                                                                                              |
| Orchestration    | `Sets/`: the console-free services that compose the above. `SyncSetService`, `SetResolveService`, `LibrarySyncService`, `GameSync`, `EvictionService`, `RemovalService`, `RoamingConfigService`. `Sync/SaveFlushService` is the same shape for the saves flush |

## `src/RomMBat.Agent`

Console executable, published as `rommbat-agent.exe`. Short-lived: one pass, then exit.
There is no daemon, because a portable install cannot register a service or a scheduled
task.

| Subcommand   | Network       | Notes                                                                                                  |
| ------------ | ------------- | ------------------------------------------------------------------------------------------------------ |
| `pair`       | yes           | Device pairing from a terminal. The UI pairs through the same Core service                             |
| `sync`       | yes           | Flush first, then resolve sets, BIOS, then each game's ROMs and its artwork, gamelists, and scan saves |
| `bios`       | only if asked | Report what RetroBat requires under `bios/`, and fetch it with `--apply`                               |
| `hooks`      | **never**     | `status`, `install`, `uninstall` the four EmulationStation event hooks                                 |
| `menu`       | **never**     | `status`, `install`, `uninstall` RomMBat's entry in the EmulationStation menu                          |
| `uninstall`  | **never**     | Take hooks, menu entry and conversions back out; `--content`, `--bios` add synced files. `--apply`     |
| `saves`      | only if asked | What is on disk, what went up, what cannot and why, and what is waiting on a decision                  |
| `game-start` | **never**     | Append a start record and exit                                                                         |
| `game-end`   | **never**     | Close the record. Read the launch facts from `emulatorLauncher.log`, exit                              |
| `flush`      | yes           | One pass over everything waiting, then exit. The local half works with no server                       |
| `outbox`     | **never**     | `list` or `drop` the entries not sent: `drop <id>`, `--all-failed` or `--all-pending`, with `--apply`  |
| `background` | yes           | `start` or `quit`: the pass those two hooks spawn. Not a command anyone types                          |
| `status`     | only if asked | Report local state; probes the server unless `--offline`. For support and for scripts                  |

All of these are implemented. Two subcommands need both the network and a decision from a
person: `saves resolve <rom> <slot> --keep-local | --keep-server`, which is also the only caller
of `overwrite=true` anywhere in the codebase, and `saves restore --apply`, which puts back a save or
save state the server holds and this device does not. Neither has a default side and neither is ever reached
from a flush. `saves bind <system> <game id> <rom id>`, and
`--forget`, are the local-only pair that settle or clear a Game-ID binding; nothing else writes
one by hand.

**The `start` and `quit` hooks invoke a pass, as do `sync` and a person typing `flush`**, so an
install nobody opens a terminal on still drains its journal. Spawning the agent costs the launch
contention, not latency: ES spawns hooks fire-and-forget and starts emulatorlauncher without waiting, and the
75.9 MB agent reaches `Main` in 34 ms against the 11 MB hook's 60 ms, since trimming without
`PublishReadyToRun` throws the framework's precompiled code away (RB-195, RB-197).

CLAUDE.md rule 4 forbids a hook touching the network **because** hooks run in the
game-launch path, and only `game-start` and `game-end` do. `start` fires when EmulationStation
starts and `quit` when it exits, so each of those two spawns
`emulators/rommbat/rommbat-agent.exe background <event>` detached, with `UseShellExecute=false`
and `CreateNoWindow=true`, and does not wait. The set lives on `SpoolRecord.BackgroundEvents`,
which the hook compiles rather than references, so the hook and the agent cannot disagree about
it and a test asserts the boundary instead of a comment claiming it.

`background quit` waits for the ES process to exit before applying queued configuration, and
gives up rather than hanging. Measured: ES is gone 48 to 68 ms after the quit hook stamps
itself, and 10 ms after the pass starts looking on a real session. If it never exits, the
configuration stays queued and the flush runs anyway, because the flush touches no file ES owns.

`background start` flushes and nothing else. Config is impossible there for a measured reason:
ES's launch write to `es_settings.cfg` lands 1.6 to 4.9 s **before** the `start` hook fires, so
`start` is inside the discard window rather than outside it.

The pass writes what it did to `emulators/rommbat/logs/background.log`. It runs with no window,
so nothing it prints reaches a person otherwise.

`game-start` and `game-end` run inside the game launch path. They spawn nothing, must not open a
socket and must not wait on a lock. ES spawns them **fire-and-forget**, so they do not
delay the launch (30 ms from hook to launcher, against an 8 s hook, RB-346), but they **do run
concurrently**, with each other and across events.

**The hooks ship as an executable, because only an `.exe` survives a real game name.**
ES fires every event and logs `executing:` for every script in the folder, whatever the name
holds, and a hook that does not run fails **per interpreter**, not per event (RB-396). A `.bat` never starts once any argument is quoted,
because the `batfile` association is `cmd /c "%1" %*`; a `.ps1` never starts once the name
contains a parenthesis, because ES builds `powershell <script> <args>` with no `-File`. An
`.exe` received all three arguments intact on a real No-Intro name, and on the second host
in RB-398 an `.exe` was the **only** form that ran at all.

So the hooks are the agent executable, and `game-start` is usable. Two things follow.
An ES-menu launch fires both events (RB-221), but a launch that fails fires `game-end` with
**no** preceding `game-start` (RB-349), so an orphan `game-end` is normal rather than a fault. And the
hook is never told the system, emulator or core, so
**`emulationstation/emulatorLauncher.log` remains the source for the launch facts**. See
RB-346 to RB-352 and RB-394 to RB-400.

Concurrent invocations are safe: the flush takes a lock file in the tree and a second
process exits rather than queueing. The lock is mandatory, not defensive, because concurrent
hook execution is the normal case. Anything else that writes the same save files takes it
too: `saves resolve`, which runs the same class C restore a flush does, `saves restore --apply`,
which writes into `saves/<system>/` exactly as a download does, and `evict`'s sweep of
`partial/`, which would otherwise delete a restore's staging directory out from under it. A
flush that cannot get the lock is done, because another process is doing the work; the other
three have nobody doing theirs, so both `saves` subcommands refuse and the sweep waits for the
next pass.

**The two that write saves also ask `Sync/InFlightGuard`**, for the same reason they take the
lock: a running emulator holds the file whichever process is about to write it, so a guard on
the flush alone leaves the two routes a person reaches by hand writing under it. Each reports the
deferral and neither counts it as a failure, since nothing was written and nothing was lost.

## `src/RomMBat.UI`

Full-screen, gamepad-navigable, published as `RomMBat.exe`, registered with
EmulationStation through `system/es_menu/*.menu`.

What it covers: pairing behind an on-screen keyboard, and status; the sets surface (listing,
defining, editing and deleting a set, the scope, platform and folder pickers, resolving one with
progress, and the disk budget); the sync run (every set or one, with live progress, a stop that
leaves the tree correct, and the budget as it is spent); browse, per-game install and removal;
conflicts; the platform mapping; and the configuration queue.

**The root is a list of verbs because a screen has only four action buttons**, Accept, Start,
Extra and Alternate, and the root needs more entry points than that. `RootScreens.Menu` is that
list; `StatusViewModel` holds the facts, and each verb is one press behind the row naming it. The counts that motivate a verb (conflicts, unmapped
platforms, queued changes) are on the rows themselves, because burying a number a person has to
act on would mean the interface knew about a stalled sync and did not say so.

**Resolving a conflict is Core's, because the UI can never take `TreeLock`.** It runs the same
class C restore a flush does, and two at once leave a shared container half swapped.
`ConflictResolutionService` holds the lock, refuses rather than treating a failed acquire as
done, and words every outcome; `saves resolve` is a shell over it. It takes a connection factory
rather than a connection, so the lock is still taken before anything is asked of the server.

**Browse holds one page and moves by page**, 50 rows, and it is the only screen that is not a
`ListScreen`: everything else has all its rows the moment it opens. It starts on the platform
list rather than the library, asks for name order, degrades to what this device holds when there
is no server and says which of the two it is showing, and its cursor **stops** at the end of the
last page where every other list wraps, because a paged list that wraps to page one silently
undoes the paging. Both draw through one body in `ScreenView`, so the windowing and the edge
markers cannot be right in one and wrong in the other.

**A list of choices and a pane of facts are drawn differently, and `ListScreen.Reading` is which
one a screen is.** A list of choices has a cursor, wraps, and draws each row as a filled panel
that fills accent when selected. A pane of facts has **no cursor at all**, scrolls by an offset
so every press moves the view, clamps at both ends, and draws its rows as plain lines. Dressing
the second as the first is what a hands-on pass reported twice, as information shown as buttons
that do nothing. `IWindowedScreen` makes the row count follow from the same answer, so a screen
says "am I reading" once and `ListWindow.CapacityFor` follows: told separately, a screen computed
a window of eight and was drawn at the 122 px reading height, which overflows the display by
exactly the margin the reading capacity exists to avoid. **There is no separate reading row
height.** A pane of facts is drawn by the body that draws a status row and its block is bounded by
`ListWindow.ContentBudget`, so there is no second number to disagree with the first.

**A person can free space on the interface, but RomMBat never chooses which games matter
least.** What a person can do is name one: delete a
set and take its games, or take one game off from its detail screen. Both go through
`EvictionService.PreviewRemoval`, behind a preview, and neither can reach a save: `local_file`
has no save kind, enforced by a `CHECK`, so anything that removes content walks a table that
holds no saves. `rommbat-agent evict` is unchanged.

**Two screens run minutes-long work, and they answer Back the same way**: the first press stops
and stays so the screen can say what happened, and a second leaves. The sync screen's stop
removes the game it was in, so its footer says so rather than reading "Stop for now". Both own
their cancellation and are disposed when left.

**A screen that has finished says so three times, because a full progress bar and a stalled one
are the same picture.** The title turns past tense ("Queried 'X'", "Synced 'X'"), an outcome
word sits above the sentence ("Finished", "Stopped", "Finished with problems", "Did not
finish"), and the footer reads **Done** instead of offering a stop. That last one is the rule:
**if the footer offers a stop the work is running, and if it says Done it is over**, which is
the only thing a person has to learn to know whether to keep waiting. The sets screen's
footer offers **Query** rather than Check, beside Sync, because "Query" names the act of asking
the server and so says which of the two reaches the network.

**The on-screen keyboard is EmulationStation's own, key for key.** `KeyboardLayouts` holds a
transcription of the three grids compiled into `emulationstation.exe`, in upstream's shape, and
`OnScreenKeyboard` builds them into a 13-column grid of spanning keys with four faces each. The
layout follows the language ES is running in, which
`InstallSession.EmulationStationLanguage` reads from `es_settings.cfg` because the UI may not
name `EsSettingsFile`. RomMBat's interface itself stays English: see
[interface-language](../design/decisions/interface-language.md) for why that is a milestone rather
than a follow-up.

**The framework is Avalonia**, and the deciding argument is size on a portable drive rather than either start time or
cross-platform reach. WPF cannot be trimmed at all, so it has a floor nothing moves, and it
needs the Windows Desktop runtime inside a self-contained publish on top of the agent's
76 MB. Avalonia trims, and renders through Skia, so what a handheld shows does not depend on
that machine's Windows Desktop stack.

**What that decision costs.** Referenced as `Avalonia`,
`Avalonia.Win32`, `Avalonia.Skia`, `Avalonia.Themes.Fluent` and `Avalonia.HarfBuzz`, never
`Avalonia.Desktop`, which drags in `Tmds.DBus.Protocol` for the X11 backend and raises
`NU1903` for a known high-severity advisory that `-warnaserror` turns into a failed build,
for a backend this win-x64 build cannot use. Published untrimmed at **99.7 MB across five
files**.

**`Avalonia.HarfBuzz` is in that list because Avalonia 12 split text shaping out of
`Avalonia.Skia`.** It is the one dependency here whose absence the compiler cannot see:
`UseSkia` alone builds clean, passes the suite, and throws `No text shaping system
configured` at `AppBuilder.Setup`, before a window is shown. `TextShapingTests` asserts the
`UseHarfBuzz` call structurally for that reason.

**These numbers move with the toolchain, so compare them only against a build taken the same
day on the same machine.** The same commit re-published months apart has measured 4.6 MB
smaller, which is the SDK moving underneath it, and that dwarfs what a change to the UI costs.
So a change is measured as a **delta**: its base commit and its head, both published and timed
the same day, median of five with the cold run discarded. Start time varies by about 60 ms
between runs, so a difference inside that spread is no measurable change rather than a gain or a
loss. A figure recorded on another day is never the baseline.

**Trimming is not switched on, and that is measured rather than lazy.** A trimmed,
ReadyToRun, single-file publish measured 61.1 MB and 517 ms, an indication rather than a delta
since it was not timed beside its untrimmed base, and trimming raises 16 `IL2026` warnings across twelve reflection-based
`System.Text.Json` call sites in Core and `RomM.Client`, whose failure mode is a runtime
deserialisation fault in a build that linked cleanly. `SaveShapes` classifies every save, so
that is not a risk to carry for a size win. Tracked as #98.

**The publish is five files, and bundling them into one is refused on principle 4.**
`PublishSingleFile` bundles managed code; Avalonia's native libraries sit beside the exe, as
`e_sqlite3.dll` already does for the agent:

```text
RomMBat.exe            79.9 MB
libSkiaSharp.dll       11.1 MB
av_libglesv2.dll        5.1 MB
e_sqlite3.dll           1.9 MB
libHarfBuzzSharp.dll    1.7 MB
```

`IncludeNativeLibrariesForSelfExtract=true` does produce one file, and it was measured at
61 MB trimmed. It is not used, because self-extraction unpacks the natives into the **host's**
temp directory rather than the tree, which is the thing core principle 4 forbids and would
happen afresh on every machine a portable drive is carried to.

**An install is seven files, not five.** These five are the UI's publish; the agent adds
`rommbat-agent.exe` and the hook adds `rommbat-hook.exe`, and the agent's own `e_sqlite3.dll`
is byte-identical to the UI's, so one copy in the shared directory serves both. `tools/publish.ps1`
assembles exactly those seven and refuses to package a set missing any, because losing one
breaks the app at launch with nothing a user can read.

Every entry in the zip is prefixed `emulators/rommbat/`, so it extracts at the RetroBat root,
where `RetroBatInstall.AppDirectory` and `hooks install` expect the files; a flat archive would
leave the ES menu entry unable to resolve its executable. The script cleans each project's
output directory first, because a publish over a warm one missing a native considers the copy
up to date, skips every native and still reports success. That clean also republishes a deleted
native before the check can see it is gone, so `-NoPublish`, which packages whatever the last
publish left, is the way to exercise the refusal. `-Deploy` writes only the seven files, so
`rommbat.db` and `device.id` survive and the install stays paired, and it refuses a path that is
not a RetroBat root.

**Input is read, never detected.** The controller map comes from the live `es_input.cfg`,
which records which physical input is `a` on that pad rather than what kind of pad it is, and
it is read through `emulationstation/SDL2.dll` because those ids are SDL joystick indices and
only the same library can interpret them. There is no vendor-id table anywhere in RomMBat.
See `EsInputMap` and `GamepadReader`, and RB-218, RB-225, RB-226 and RB-227.

**The UI can never write `es_settings.cfg`.** It is launched from the ES menu, so it runs
under a live EmulationStation every time, and ES discards a key written underneath it.
Anything it wants to change there goes into `pending_config` and is applied by
`background quit`.

**Both halves of that are asserted structurally, against the built assembly rather than the
source**, so a helper in another namespace or a call through an interface is caught where a
grep would miss it: `RomMBat.UI` never references `EsSettingsFile`, and it never references
`TreeLock` either.

**The lock one is not obvious and is worth reading twice.** A flush treats a failed acquire as
success and exits having done nothing, so a UI that took the lock for an instant merely to
report whether a pass was running would make a concurrent `background quit` flush skip the
upload and call it success. Reading needs no lock: the store is WAL. See the
`offline-and-portable` skill.

**It holds for the screens that write, too.** Defining,
editing and deleting a set, and setting the budget, are all rows in SQLite, and the tree lock
serialises writers of _files in the tree_; taking it for a set definition would be the
speculative acquire above wearing a different hat. Where a lock genuinely is needed the Core
service takes it and returns the refusal as a value, as `PartialSweep.Apply` does, and
`EvictionService` surfaces that refusal rather than reimplementing it.
Both halves are asserted: a set is definable while a background pass holds the lock, and an
eviction under a held lock leaves `partial/` alone and says so.

Presentation owns no logic. Set resolution, mapping, conflict handling and the outbox all live
in Core, and the UI project holds views and view models over them. Screens carry no Avalonia
types at all, so every screen is walked in tests by the gamepad map alone with no window; only
`ShellWindow` knows about both the framework and the pad. If something in the UI project cannot
be tested without a window, it is in the wrong project.

No primary flow may require a mouse.

## `tests/RomMBat.Tests`

xUnit, covering Core and Client.

## `tests/RomMBat.Agent.Tests`

xUnit, covering the Agent's subcommands. Its own project rather than a reference added to
the one above, because the Agent is an `Exe` carrying an `app.manifest` and pulling that
into the existing test host would put a Windows manifest behind every unit test in the
repo. `TempRetroBatTree` is linked from `RomMBat.Tests` rather than copied, so both suites
agree on what a RetroBat tree looks like. `TempTreeLeakCheck` is linked with it: an assembly
fixture that fails either run when a tree is still in `%TEMP%\rommbat-tests` after the last
test, because the tree's own teardown swallows a failed delete so that one open handle cannot
fail an unrelated test.

**The commands are where the pieces meet**, each wiring a planner to a sync to a store to
an exit code, and that is the layer a defect survives a full green suite in. A command that
returns early can make a planned action unreachable while the planner and the sync are each
covered on their own: gating the BIOS apply on there being something to download would leave
`BiosAction.Adopt` writing no row, for a user who copied their BIOS in by hand.
`A_plan_that_only_adopts_is_still_a_pass_worth_running` holds that gate open.

The suite drives `Program.DispatchAsync` rather than a command class, because the handlers
that turn an exception into an exit code live there and a test that calls the command
directly runs straight past them. A RetroBat that has been unzipped and never launched is the
case that seam covers: a root is accepted on `retrobat.ini` alone, it has no `es_systems.cfg`
for the `bios` argument gate to read, and the command refuses with the exception's own message
rather than throwing.

Fixtures come from a real install and are checked in under `tests/**/fixtures/`, byte
exact and excluded from linting. Save-shape and mapping logic without a fixture is not
finished.

The highest-value suite is the **offline simulation**: drive the whole client against a
stubbed handler that can be switched to "unreachable" mid-operation, and assert that every
operation either completes locally or queues, and that a later flush is idempotent under
replay and partial failure. It exists as `OfflineSimulationTests` over
`Support/StubRomMServer`, whose unreachable mode throws exactly what `SocketsHttpHandler`
throws on a connect timeout, because that shape is the thing the code has to tell apart from
a user cancellation.

Anything needing a live RomM calls `Assert.SkipUnless` on environment variables, so a clone
with no server still runs green. Those tests drive the **real** pairing flow headlessly:
`GET /api/auth/device/pending/{user_code}` and `POST /api/auth/device/approve` are ordinary
protected routes, so `Support/ApprovingUser` holds a pre-made token and plays the approving
user. That harness lives in the test project on purpose. Putting approval or token injection
into the shipped client would give it a second auth-adjacent surface, and the whole point of
pairing being the only path is that there is exactly one.

The token that harness holds is not a RomMBat token and needs other scopes than a device
asks for; [the testing doc](../contributing/testing.md#the-approver-token) has which, and why.
