# Scraped media

Part of the [retrobat-layout](SKILL.md) skill. What RetroBat scrapes, and how RomMBat writes it.

## RetroBat's scraper settings are settings RomMBat should read

**Measured on 8.2.1: `es_settings.cfg` carries exactly three scraper keys**, and only two of
them map onto anything RomMBat fetches.

| Key                | Type                   | Maps to                                                                                           |
| ------------------ | ---------------------- | ------------------------------------------------------------------------------------------------- |
| `ScrapeVideos`     | bool                   | RomMBat's `MediaKind.Video`                                                                       |
| `ScrapeManual`     | bool                   | RomMBat's `MediaKind.Manual`                                                                      |
| `ScrapperImageSrc` | string, e.g. `sstitle` | **nothing.** It picks which ScreenScraper image ES uses as the cover, and RomM serves its own art |

**There is no toggle for the cover, the thumbnail or the marquee.** Do not invent keys for them.

**Never read EmulationStation's compiled defaults as RetroBat's defaults.** RetroBat ships
`system/templates/emulationstation/es_settings.cfg` and seeds the live file from it at install,
so what a user sees on a fresh machine is the template's value and not `Settings.cpp`'s. The two
disagree on exactly the keys that matter here. Read the template, and check a fresh install
rather than reasoning from upstream source. Seeding happens once: a used install whose live file
has lost a key does **not** get it back from the template on the next launch, measured on 8.2.1.

**Both scraper switches ship on, and off is unrepresentable, so an absent key is a deliberate
no.** `Settings::saveMap` drops any key whose value equals its default, and any key with no
registered default whose value is `false`:

| Key            | RetroBat template | ES compiled default                                     | So the file says          |
| -------------- | ----------------- | ------------------------------------------------------- | ------------------------- |
| `ScrapeVideos` | `true`            | `mBoolMap["ScrapeVideos"] = false` in `Settings.cpp`    | `true` on, **absent** off |
| `ScrapeManual` | `true`            | none at all, so `getBool` returns the map's own `false` | `true` on, **absent** off |

Turning either switch off in the scraper menu **deletes the key**. A literal `value="false"`
never occurs, so never write an off branch that depends on seeing one.

Two hands-on rounds were lost to reading absent as something else. First absent was read as
"on", which turned manuals on for every install whose ES had never written the key. The
correction read absent as "RomMBat's own default", which for video is also on, so turning video
off in RetroBat still did nothing at all: measured afterwards on the live install, 389 MB of
video on one platform and 2.05 GB across the tree that no setting could reach.

Use `EsSettingsFile.Has` before `Value`, and treat the absent branch as off.

**"Absent means off" is a rule about a file that is there, and it does not extend to a file
that is not.** `EsSettingsFile.Load` answers a missing path with an empty `<config>`, which is
byte-for-byte the answer a real file with both switches turned off produces, so an install that
has never said anything is indistinguishable from one that said no to everything. Reading that
as a decision is harmless where the answer only picks what to fetch next, and it is not harmless
where the answer decides what to **delete**: `MediaSync.Discard` removes the synced files of any
kind that is not wanted, so a missing `es_settings.cfg` swept a whole library's artwork. The fix
is to gate the destructive half, not the read: the kinds stay the same in all three states and
only the delete asks whether anybody actually said so (`MediaPolicy.Preference`, `FromInstall`).

**A malformed one throws, and nothing on the sync path catches it.** `XDocument.Load` raises
`XmlException`, which is not an `IOException`, so a truncated file after a power cut escapes
`MediaSync`, `GameSync`, `LibrarySyncService` and `SyncViewModel` alike and leaves the screen at
"Working" with a stop footer for ever. Any read of this file on a long-running path catches it.
`SaveScanner.cs`'s catch filter does not list it either, which is untouched and unfixed.

**Three more settings pick a source for a slot rather than adding a kind.** ES's own comments
name the tag each one feeds, so these are not new media:

| Key                | Feeds         | Values                                                                    | ES compiled default | RetroBat template |
| ------------------ | ------------- | ------------------------------------------------------------------------- | ------------------- | ----------------- |
| `ScrapperImageSrc` | `<image>`     | `ss`, `sstitle`, `mixrbv1`, `mixrbv2`, `box-2D`, `box-3D`, `fanart`, `""` | `ss`                | **`sstitle`**     |
| `ScrapperThumbSrc` | `<thumbnail>` | `box-2D`, `box-3D`, `""`                                                  | `box-2D`            | not seeded        |
| `ScrapperLogoSrc`  | `<marquee>`   | `wheel`, `marquee`, `""`                                                  | `wheel`             | not seeded        |

**A stored source value is not necessarily a choice.** `GuiScraperSettings` rebuilds every row
from the currently selected `Scraper`, guarding each on `isMediaSupported(...)`, and when the
stored value is not in the new scraper's list it calls `selectFirstItem()` and writes it on
close. Switching SCRAPE FROM therefore rewrites a source the user never touched. Read the
value, map what is recognised, fall back on anything else, and **ignore `Scraper`**: RomM is
not one of the scrapers it names. Finding 241.

**Three of the remaining switches map onto real RomM fields and two are dead.** `ScrapeBezel`,
`ScrapeBoxBack` and `ScrapeFanart` map onto `bezel_path`, `box2d_back_path` and `fanart_path`.
`ScrapeMap` and `ScrapePadToKey` have no counterpart at all, which is a fact about the schema
rather than about one library, and padtokey is input config rather than media. **Do not rule a
kind out because a library holds none of it**: that number says when the platform was last
scraped. Findings 239 and 240, and #108 for the shape.

**A kind turned off is also a kind removed.** Stopping future downloads and leaving what is
already there makes the setting mean two different things depending on which way it is moved,
and nothing else reclaims it: eviction works on whole games under budget pressure and has no
notion of a kind. Only `FileOrigin.Synced` goes; a user's own scrape at the same name is theirs.

**RomMBat honours the two that exist, as the default rather than as an override.** A hands-on
pass turned video off in RetroBat's scraper and RomMBat carried on downloading it, which is two
switches that look like they should agree and do not. `MediaPolicy.Read(settings, install)`
reads them; an explicit `media.kinds` still wins, because that is what somebody typed. This is
the same rule that makes the on-screen keyboard follow `Language`: **where RetroBat already has
the setting, RomMBat asks it rather than inventing a second one.**

## gamelist.xml

Merge, never clobber; write atomically via temp file plus rename; include only locally
present ROMs. **Key generation by resolved folder, not by platform**, because two RomM
platforms can share one folder.

**Own an allowlist of the fields you write, never a blocklist of ES's.** The four the plan
first named are not the surface. Across 4,531 entries in 32 gamelists from a real scraped
install: `playcount` 115, `lastplayed` 115, **`gametime` 114**, and **no `favorite` and no
`hidden` at all**, plus `scrap` 4,525 (self-closing, `name` and `date` attributes),
`id` on `<game>` 4,493, `cheevosHash` 4,187, `md5` 2,815, `cheevosId` 2,329,
`arcadesystemname` 568, `multidisk` 161, `crc32` 8.

**Media is named after the ROM file**, stem being the file name without its extension:
`images/<stem>-image.png`, `images/<stem>-thumb.png`, `images/<stem>-marquee.png` (marquee
lives under `images/`, not its own folder), `videos/<stem>-video.mp4`,
`manuals/<stem>-manual.pdf`. Those are the exact names a user's own scrape writes, so never
delete one RomMBat did not create.

**After writing, call `GET http://127.0.0.1:1234/reloadgames`.** M0 measured that ES keeps a
stale in-memory model until asked to reload, and rewrites `gamelist.xml` from that model when
it exits. Write-then-reload makes the edit stick and takes effect immediately; write without
reloading and ES can serialise its stale copy over you. ES writes no `<game>` entry for a rom
it has no metadata for, and **does not list a `<game>` whose `<path>` names a file that is not
on disk**, so a stale entry is inert rather than a phantom game.

**Do not depend on ES preserving what it read.** When it has a reason to rewrite the file it
**drops every XML comment**, at document level and inside a `<game>` alike; moves the entry it
changed to the end; rewrites that entry's children into its own order
(`path,name,desc,genre,rating,releasedate,developer,publisher,players,favorite,playcount,lastplayed,gametime,lang,region,...`);
and prunes `<hidden>false</hidden>` as a default, the same behaviour it has on
`es_settings.cfg`. Unknown elements and attributes do survive. When it has no reason, it
leaves the file **byte-identical, mtime included**, so a no-churn assertion is meaningful but
has to be made about the file ES left behind.

**`/reloadgames` returns in 1-2 ms and does the work afterwards**, so its response is not a
completion signal. Time to the change being visible was 269 ms for a 200-entry list and
1.1 s for 100,000. Poll `/systems` (a few KB, carries `totalGames`) rather than
`/systems/<system>/games`, which serialises the whole library.

**And it is ignored outright while a game is running**, 200 in 1 ms with nothing reloaded,
exactly as `/quit` and `/emukill` are. Reload again after the game ends rather than treating
the 200 as done. With ES absent, which is the ordinary case for a background sync, the connect
is **refused after 2.04 s** on loopback, so this client needs a `ConnectTimeout` far below the
2 s used for reachability or every sync pays it.

**Size is not the constraint you would expect.** ES loaded a 100,000-entry, 65 MB gamelist
in 2.07 s from a cold start for 419 MB of working set, roughly 2 MB per 1,000 entries, and
2.93 s with a real image file per entry. **Do not cap a gamelist**, though: ES lists ROM
files it has no entry for, so dropping entries hides no games and only strips their art,
leaving the user the same number of tiles to scroll past. What bounds navigability is the
sync set's own game cap. Report a folder that grows past a threshold; never truncate it.
