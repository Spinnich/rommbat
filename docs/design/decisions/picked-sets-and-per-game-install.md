---
summary: Hand-picked games form their own scope kind, and a per-game install runs the passes that make the game launch.
read-when: Changing the picked scope, browse's install, or which passes InstallAsync runs.
---

# Picked sets and per-game install

**A picked set is the sixth `CatalogScopeKind`.** A
hand-picked set is a set: it lists, syncs, roams, evicts and deletes like the other five, so it
is a scope kind with **migration 014** rather than an id list smuggled inside a `Filter` scope,
which overloads one column with two meanings, or an unmanaged download `EvictionPlanner` has to
be taught to ignore, which means storing "this orphan is deliberate" and is a set by another
name with none of a set's machinery.

**`GET /api/roms` has no id-list parameter**, verified against the
`romm-5.3.0-beta.1.json` pin, whose contract `5.3.0` repeats exactly:
its scoping parameters are `platform_ids`, `collection_id`, `virtual_collection_id` and
`smart_collection_id`. That is a property of the scope rather than a defect, so
`CatalogQuery.ToQueryString` **refuses** a picked scope instead of falling through to a query
that walks the whole library. On the device that did the picking there is nothing to resolve:
the browse page already carries every field `sync_set_member` wants, so a pick writes its member
row from the `RomRow` in hand. On a device it roams to, the ids hydrate one at a time through
`GET /api/roms/{id}`, measured at ~0.15 s each, which is why the scope is meant for tens of
games. It roams with no change to `RoamingSyncConfig`, which carries `scope_value` verbatim.

**The picked set's name is fixed and per device**, `Picked on <device>`. Fixed keeps the
commonest path to one press; per device is what stops two devices picking into one RomM account
from colliding on `sync_set.name`, which is UNIQUE.

**Per-game install is one press.** `LibrarySyncService.InstallAsync` takes the set and the
member and runs six of the ten passes: Filesystem, BIOS, Content, Media, Gamelists and
Budget, the last two when the plan touched a folder and when a cap is set. Hooks and
Menu are first-run installs; Resolve does not run because there is nothing to resolve; Flush
does not, because it runs first for eviction's benefit and nothing here evicts. Measured
live before BIOS was added: a 2.6 GB title in 25.8 s.

**BIOS was left out first and that was wrong**, on the reasoning that firmware is per folder so
one game would drag in a platform's whole firmware. That is what it fetches and it is not a
cost: it is the firmware for the one system that game runs on, which is what makes the game
launch. A press promising to put a game on the device and producing one that dies on start has
not kept the promise, and the person pressing it has no way to know which of their games needed
something extra. Raised by Spinnich on the first hands-on pass and reversed.
