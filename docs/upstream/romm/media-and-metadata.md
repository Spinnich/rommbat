---
summary: Where RomM's media lives, how it is served, and what its metadata fields mean.
read-when: Before fetching media or converting RomM metadata into a gamelist.
---

# RomM: media and metadata

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-88. A third party, with a third party's credentials

Previously: (not addressed) what `url_cover` and `url_manual` point at

Measurement says: **A third party, with a third party's credentials.** Both are `neoclone.screenscraper.fr` API URLs carrying someone else's `devid` and `devpassword` in the query string. Unusable twice over: off-LAN, which breaks the offline story, and not ours to send

## RB-89. They are static resource paths, and they come in two shapes

Previously: Media downloads reuse M3's `/api/roms/{id}/content` path (plan M4)

Measurement says: **They are static resource paths, and they come in two shapes.** `path_cover_small`/`path_cover_large` are already rooted at `/assets/romm/resources/` and carry a `?ts=` query containing a **raw space**; `path_manual`, `path_video` and the `ss_metadata` image paths are relative to that prefix. Normalise both onto the prefix exactly once

## RB-90. What the wrong prefix does

Previously: (not addressed) what the wrong prefix does

Measurement says: **Answers 200.** Requesting `roms/20/1393/manual/1393.pdf` as given returns **5,826 bytes of the web UI's `index.html`** with an `ETag` and `Accept-Ranges`, and would be written to disk as a PDF. Status is not enough: the content type has to be checked

## RB-91. No token is needed at all

Previously: (not addressed) whether the device token authenticates media

Measurement says: **No token is needed at all.** Bearer and anonymous requests were byte-identical on every media path tried. nginx serves them: `Accept-Ranges: bytes`, an `hex(mtime)-hex(size)` `ETag`, `bytes=0-99` answers 206 with a `Content-Range`, and a range past the end answers 416. M3's resume machinery applies unchanged

## RB-92. How much media a real library has, and how big it is

Previously: (not addressed) how much media a real library has, and how big it is

Measurement says: cover 84.3%, `merged_screenshots` 84.6%, `path_video` **72.1%**, `path_manual` 46.1%, `summary` 81.9%, `metadatum` populated on 100%. Marquee is provider-scoped: `ss_metadata` is present on 1,093 of 1,250 and carries `logo_path` on 994, so **79.5%** of the library, against `marquee_path` on 1,077. Medians: thumbnail 104 KB, cover **525 KB**, logo 445 KB, video **1.99 MB**, manual **2.45 MB**. So a 100-game `nes` set is ~12.8 MB of ROMs against ~550 MB of media, a factor of 43

## RB-92b. Which ScreenScraper asset is EmulationStation's marquee

Previously: (not addressed) which ScreenScraper asset is EmulationStation's marquee

Measurement says: **`logo_path`, not `marquee_path`.** Upstream's own `gamelist_exporter.py` maps `"marquee": [ss.get("logo_path"), gl.get("marquee_path")]`. ES's marquee is game logo art; ScreenScraper's marquee is an arcade cabinet marquee. Now vendored at `reference/romm-gamelist_exporter.py` so the mapping is checkable

## RB-95. What unit `metadatum.first_release_date` uses

Previously: (not addressed) what unit `metadatum.first_release_date` uses

Measurement says: **Milliseconds.** Read as seconds, all 4,108 sampled values land in year 0; read as milliseconds they land in **1983-2026**. No value was negative, so a pre-1970 release is unobserved rather than impossible

## RB-96. What scale `metadatum.average_rating` uses

Previously: (not addressed) what scale `metadatum.average_rating` uses

Measurement says: **0 to 100**, min 5.0, max 100.0, and **all 3,216 sampled values are above 1.0**. A gamelist `<rating>` is 0-1 to two decimals, so it is a divide by 100. A real scraped install's ratings sit on 17 distinct values in 0.05 steps, which is ScreenScraper's /20 score and finer-grained here

## RB-97. It is already the same form

Previously: (not addressed) whether `player_count` maps onto `<players>`

Measurement says: **It is already the same form.** `"1"` 3,406, `"1-2"` 1,008, `"1-4"` 328, up to `"1-16"`. A real install's `<players>` is the identical vocabulary. A straight copy, and the only conversion in this table that is not one

## RB-98. Neither role can be recovered

Previously: `<developer>` and `<publisher>` come from the metadata (plan M4)

Measurement says: **Neither role can be recovered.** `metadatum.companies` is a flat array merging both, **alphabetically sorted on 4,197 of 4,197** rows that have one, so any positional reading is reading the alphabet. Chrono Trigger reads `['Squaresoft', 'Squaresoft']`, the same company twice. 3,959 of 5,000 carry exactly two entries. `igdb_metadata.companies` is unsorted but unlabelled

## RB-99. How an array becomes a single-valued gamelist element

Previously: (not addressed) how an array becomes a single-valued gamelist element

Measurement says: The real install already does it: **2,079 of its 4,440 `<genre>` values contain a comma or a slash** (`Racing, Driving`, `Action / Adventure`) out of 111 distinct values. Joining with a comma and a space reproduces the convention rather than departing from it. `franchises` needs deduping first: 18 of 5,000 repeat a name

## RB-100. Whether `regions` and `languages` can be copied

Previously: (not addressed) whether `regions` and `languages` can be copied

Measurement says: **Different vocabularies both ways.** RomM says `Japan`, `USA`, `Europe`, `World`; the real install writes `jp`, `us`, `eu`, `wr`. RomM says `English`, `French`; ES writes `en,fr` comma-joined. 246 of 5,000 rows carry more than one region while `<region>` is single-valued, and `languages` is present on only 18.3%

## RM-7. The gamelist exporter was substantially rewritten (`source`)

`backend/utils/gamelist_exporter.py` on `master` no longer resembles the vendored copy. It now
parses and merges existing gamelists through `defusedxml` (upstream's "merge instead of
overwriting" fix), took `ASSET_DIRS` into `config.PLATFORM_MEDIA_DIRS`, and gained
`HAS_FILE_ON_DISK_FILTERS`, `rel_platform_folder` and `join_rel_path`.

This repo derives two unit conversions from that file, that `first_release_date` is
milliseconds and `average_rating` is on 0 to 100, and `verify.py` asserts them as behaviours
rather than counts precisely so a rewrite like this one is survivable. **Both survive**, read at
`master`: `first_release_date` is still divided by 1000 at line 125, and `average_rating` by 100
at line 340 with the "0-100 scale" comment intact. Asserting behaviours rather than line numbers
is what made that a two minute read instead of a re-derivation.

**Re-pulled and re-derived, 2026-09-14.** `refresh.sh` moved the vendored copy and `verify.py`
came back with exactly one drift, the companies check, which is the one the re-pull was
expected to flip. Everything else held: both unit conversions, the `marquee` / `logo_path`
rule, the `releasedate` and rating format strings, and all seventeen gamelist elements
RomMBat writes. Of the three deliberate divergences, region/lang and genre are untouched.

**One divergence ends, per row rather than outright.** The exporter now reads
`primary_developer` and `primary_publisher`, which is this repo's own upstream follow up 3
([`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/2fadda431/docs/PLAN.md) "Optional follow ups to RomM itself") implemented upstream. But each property
falls back to the indexing it replaced:

```python
return next(iter(self.developers or companies[:1]), None)
```

So the alphabet still reaches `<developer>` on any row whose split is empty, and **a row only
carries the split once it has been rescanned under 5.3.0**. Measured on the live
`5.3.0-alpha.2` library, 3,000 rows across ten platforms: the one platform that had been
rescanned carried `developers` on 398 of 400 rows, and the nine that had not carried it on 0
of 300 each while still carrying `companies`. Where the split is present it is exactly one
developer and one publisher and `companies` is the two of them sorted, so indexing assigns
both roles wrongly on **41% of rows** (163 of 398). `4x4 Evo 2` is
`companies=[Sierra, Terminal Reality]`, which reads Sierra as the developer when Terminal
Reality developed it.

**Re-taken 2026-09-16 over the whole library rather than a sample**, with
`tools/romm-5.3-probes/r4-company-split.py`, because the sample recorded no method and its 41% is a
property of the one platform it happened to find rescanned. Two readings: the numbers below are the
second, and the first differed by 14 split rows because a scan of `nes-unofficial` was running
between them.

| Rows   | `companies`   | Split         | One and one   | Sorted pair   | Wrong role   |
| ------ | ------------- | ------------- | ------------- | ------------- | ------------ |
| 95,993 | 83,037, 86.5% | 18,150, 18.9% | 17,606, 97.0% | 18,065, 99.5% | 3,903, 21.5% |

The last three are shares of split rows. **Two claims above do not hold across the library.** The
wrong-role rate is **21.5%** and not 41%, and it is a per-platform number that ranges from 0 on
`channelf` and `gamate` to 45% on `gamecube` (809 of 1,792) and 83% on `arcadia` (40 of 48).
And a split row is **not always one developer and one publisher**: 3.0% carry more than one of
either, so `companies` is still their sorted concatenation on 99.5% of split rows but a client
that writes `developers[0]` is choosing one of several on the rest. What survives is the shape of
the finding: 53 platforms carry the split and 72 carry none, so one library holds both at once and
the join stays as the fallback. `wii` is the outlier worth knowing, with 79 split rows of which only
32 sort to `companies`.

The operative consequence for M4: writing `developers[0]` and `publishers[0]` is right where
they are populated and empty where they are not, so a client that switches to them
unconditionally loses the field on every un-rescanned row. The join stays as the fallback.
#172

`reference/romm-known_bios_files.json` is **unchanged** on `master`.
