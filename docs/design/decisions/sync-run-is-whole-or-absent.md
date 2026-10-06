---
summary: A sync leaves every game wholly present or wholly absent, and how stopping, artwork and the budget follow from that.
read-when: Changing GameSync, LibrarySyncService, the media pass, or what a stopped sync leaves behind.
---

# A sync leaves each game whole or absent

**The invariant the stage is built around.** A sync leaves every game either wholly present,
with its gamelist entry and whatever artwork the server actually had for it, or wholly absent.
Whether it ran to the end, was stopped, or lost the server. `GameSync` is the type that owns
that sentence: it groups a `ContentPlan` into games by `DiscSet`, fetches each game's ROMs and
then its artwork, and takes back any game that did not land whole.

**A stopped sync writes its gamelists, and getting that wrong is what the first hands-on pass
found.** The pass was handed the run's cancellation token, so on a stop it threw before writing
anything and every finished game sat on the drive invisible to EmulationStation. It runs on
`CancellationToken.None`, which is bounded: two local file writes and one reload with a 400 ms
connect timeout. The same defect existed a second time, on the path where a stop lands during a
game's artwork rather than during its ROMs.

**"With its artwork" cannot mean every configured kind, and the reason is not RomMBat's.**
Nothing guarantees a server holds it: the administrator may not have scraped that kind, and the
upstream source may never have had it for that game. `MediaSyncOutcome.Missing` counts exactly
that, no run can fix it, and a rule demanding every kind would declare most real libraries
permanently broken.

**The rollback fires on any incomplete game, not only on a stop.** A multi-disc title whose
second disc fails leaves half a game on disk with nobody pressing anything, and `ContentSync`'s
"a failure is per game, not per run" makes that the ordinary path. It is bounded to the ROMs:
once every ROM of a game has committed the game is playable and listed, and a stop during its
artwork leaves it present for the next run to finish. Three fences keep it to this run's own
writes, each with a test that fails when the fence is removed: only `FileOrigin.Synced` rows,
never a game that entered as `AlreadyPresent`, and the `local_file` row goes with the bytes.

**A stop is returned, not thrown**, because a canceled resolve that throws loses what it
found. The run carries on to write gamelists and report the budget, so a stopped
sync ends with a correct tree rather than with work postponed.

**A reservation was added, for the ROMs and not for the media.** Interleaving broke the cap:
`MediaSync` bounds artwork by `cap - managed`, and `managed` is read when the call is made, so
one call per game sees the budget as it stands before most of the run's ROMs exist. A 1 MB
budget was measured finishing 703 KB over it. `GameSync` now passes the ROM bytes still ahead.
That reservation is possible precisely where a media one is not: a ROM's size is on the member
row and the plan already holds it, and RomM publishes no media size at all.

**Turning a media kind off takes back what was already fetched.** Stopping future downloads
alone would leave the artwork for ever with nothing able to reclaim it: eviction removes whole games under budget pressure and has no notion of a kind. Measured on the
live install, 1.09 GB of video on one platform and 566 MB on another. Only `FileOrigin.Synced`
goes, so a user's own scrape at the same name is untouched, which is the fence the sync rollback
already uses.

**An advertised media path that answers 404 is forgotten rather than re-asked.** Measured on the
live library: 39 of 40 games on one platform advertised a video the server does not serve, so
every sync spent 39 requests and printed 39 problems, for ever. Forgetting the path turns it
into the ordinary `Missing` case and needs no new state, because a resolve rewrites `metadata`
from the server wholesale and puts it back the moment RomM starts serving it.

**A stopped transfer's partial is truncated before its handle closes.** The cancellation is
instant; closing a handle over a large part-written file waits for the drive's write cache, and
the file is deleted immediately afterwards. Measured on the live install: a stop 10.9 s into a
PS2-sized download took **20.1 s** in that close alone, and 0.2 s after the change.
