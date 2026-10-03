# Offline screens

Part of the [offline-and-portable](SKILL.md) skill. What the sets, mapping, browse and removal surfaces do with the server off.

## Offline-first

- **Which operations work with the server off, on the sets surface.** Listing sets, defining
  one, editing its caps and ordering, deleting it, and setting the disk budget and free-space
  floor are all local and all answerable offline. **Only resolving and syncing need the
  network**, and they are the only screens that say so. A screen that cannot tell those two apart
  is wrong: the whole point of defining a set on a handheld away from its server is that it can
  be done.

  **The platform mapping is offline too, and its repair is install-wide.** `platform_map` is
  written by every resolve and every browse, so every row the mapping screen shows, and the
  override that fixes one, are local. A screen that waited on an unreachable LAN host to show
  them would trade the working state for nothing, which is why the screen takes no connection
  at all. The agent's `platforms list` refreshes first because it can; the interface does not,
  and that is a decision rather than a gap.

  **A per-set folder override is not the repair for an unmapped platform**, and reaching for it
  is the mistake this screen exists to stop. The mapping is install-wide and an override mends
  one set while leaving every other set and every future set with the same hole.

  **Eviction is offline too and has no screen**, which are two separate facts. `EvictionService` in Core is a preview from two local scans and a walk of `local_file`,
  and carrying it out deletes files and rewrites gamelists from local state, so `rommbat-agent
evict` works with the server off. What it lacks is an interface: RomMBat guessing
  which games matter least is a bad policy even when a person starts it, so freeing space is the
  user's, by dropping a sync set or a single game. Do not go looking for eviction screens.

  **A resolve is long work, measured rather than assumed:** a platform scope of 9,196 roms
  took **8 minutes 15 seconds** against a live 5.2.0 instance at 250 rows a page. So
  cancelling it is the ordinary case, not a failure path, and a cancel records its offset
  exactly as an unreachable server does so the next run continues. Discarding the walk on
  cancel would make the feature worse than not offering it.

  **The same walk is 22.5 s on 5.3.0-alpha.2**, re-measured at a library grown to 95,993 roms,
  so on a current server it is no longer a screen anybody sits and watches. **The behaviour
  does not change on the strength of that.** A resolve is still unbounded work over somebody
  else's network, a resume still costs nothing, and the reading above is one platform on one
  server: a slower host, a larger scope or a worse link puts the minutes back.

## Browsing and removing, offline

- **Browse degrades, it does not refuse.** With a server it pages `GET /api/roms`; without one
  it lists what the device holds, out of `local_file` joined to `sync_set_member`. That follows the
  rule that the offline browsable set is the locally present subset, which is what
  EmulationStation shows anyway. **It says which of the two it is showing, always**, not only
  when it degraded: a person who never sees the online form cannot otherwise tell the offline
  one apart from a library that has shrunk. `BrowseService` decides which; the screen words it.
- **Nothing holds more than one page.** Moving past the bottom fetches the next offset and
  **replaces** what is held. A screen that appended would look identical for the first few pages
  and hold an 83k library by the end, which is why the assertion is a row count across several
  pages rather than a look at one.
- **50 rows a page, measured, not 250.** `RomPager.DefaultPageSize` is for a resolve, which
  wants the fewest requests for a whole scope. Against the live 96,060-rom instance, warm: 50
  rows 280 ms, 250 rows 611 ms. 250 is cheaper per row and more than twice the wait for the page
  a person is looking at, and at `ListWindow.Capacity`'s eight rows it is 31 screens of scrolling
  per fetch.
- **It starts on the platform list, not on the library.** A live instance holds 96,060 games, so
  opening on all of them is 1,922 pages and somebody after one console is shown another's games
  first. Narrowing is the first thing anyone does, so it is the first thing offered.
- **Ask for name order explicitly.** `CatalogQuery` defaults to ascending id because that is what
  makes a resumable walk survive a library changing underneath it. A person scrolling wants
  alphabetical, and id order is the worst kind of wrong here: a library imported in name order
  carries ids in roughly that order, so the list reads as sorted until it is not. Measured, an
  id-ordered snes page put "3 Ninjas Kick Back" before "3-jigen Kakutou Ballz" and then dropped
  the latter out of sequence. Name it rather than leaving `order_by` empty, which the schema
  documents as relevance ordering on MySQL and name ordering elsewhere: an order that depends on
  the server's database is not one a person can learn.
- **A row needs the title and the whole file name, and both were measured.** 750 rows a platform:
  every arcade file name is a romset code with no tags at all (`10yard.zip`) and 87.3% differ
  from the display name, so the title has to be the label; 69 megadrive and 67 psx titles are
  shared by two or more rows, so the file name has to be under it. Showing tags on some platforms
  and the file name on others makes the rule change under a person's feet.
- **A paged list stops at the end; it does not wrap.** Every other list in the UI wraps. Wrapping
  to page one after nine thousand rows of paging silently undoes them and looks exactly like the
  stall a failed fetch produces. Stopping _silently_ is worse again, so there is a row saying so.
  A library that fits one page still wraps: there is no paging to undo.

### Two kinds of list, and drawing one as the other

- **A list of choices** has a cursor, wraps, and draws each row as a panel that fills when
  selected. **A pane of facts has no cursor at all**, scrolls by an offset, clamps at both ends,
  and draws its rows as plain lines. `ListScreen.Reading` is which one a screen is.
- **Dressing the second as the first was reported twice on one pass**, first as a highlight
  walking rows that do nothing and then, with the highlight gone, as the rows still being drawn
  as buttons. Both are the same mistake and the fix is at the class, not the screen.
- **The offset matters rather than being a detail.** A cursor is kept off the edge where there is
  room, which is right for choices and wrong with nothing highlighted: the first presses would
  move something invisible and leave the view still, so the screen reads as ignoring the pad.
- **Pair the row count with the row height.** They were chosen in two files, so a screen could
  compute a window of eight and be drawn at the taller reading height, overflowing by exactly the
  margin the reading capacity exists to avoid. A screen answers "am I reading" once and both
  follow from it.

### A footer offers a verb exactly when that verb works

Both halves are one rule, and three screens got it wrong three different ways in a single
hands-on pass: two answered a press and never offered it, so the footer named nothing but Back
while the verb quietly worked, and one offered it always, including when the preview had just
said nothing would happen. A footer promising an action that does nothing and a footer silent
about one that does are the same defect pointed two ways.

- **A verb that depends on loaded state needs a hint that does too.** `ListScreen.ExtraHints` and
  `OfferAcceptWhen` are functions for the reason `Note` became one.
- **Sweep every action in both directions, not just Accept.** The sweep that existed checked one
  action and one direction, which is why all three shipped.
- **"Did something" means navigated or changed what the screen shows.** A form that answers a
  press by staying put and saying why has plainly done something.

### The claim rule: a game another enabled set still wants is held back

**One method, `EvictionPlanner.Claims`, and both paths call it.** The budget path uses it so
trimming one set cannot take a game another set wants near the top; the removal path uses it so
deleting one set cannot silently take a game a set the user never touched still claims, **only
for the next sync to fetch it again**. Written twice it would have been two rules with one name.

- **The sets being removed from are released**, or their own membership would hold every game
  back against the person removing it. Everything else enabled still counts, and the refusal
  names the set: `still in '<name>'`.
- **A disabled set makes no claim.** That is what "enabled" in the rule means, and it has a test.
- **The order matters.** "Another set still wants this" is reported ahead of a `SaveGuard`
  refusal when both are true, because the second is temporary and the first is the user's own
  other set.

### Removing content

- **The flush runs first.** The commonest `SaveGuard` refusal is a save that has not reached the
  server, and flushing resolves it rather than blocking the removal. **Offline it is skipped and
  said so**, and the unsent save then keeps its game, which is the correct answer.
- **`Plan(bytesToFree)` cannot serve a removal at all.** It returns early when nothing is over
  budget, and its whole ordering answers "which games matter least", which is the question the
  ruling that took eviction off the interface says RomMBat should not answer.
  `PlanRemoval(romIds, releasing)` is the entry point.
- **`local_file` has no save kind.** Its seven are `rom`, `image`, `thumbnail`, `marquee`,
  `video`, `manual` and `firmware`, enforced by a `CHECK`; saves live in `local_save` and
  `local_state`. Anything that removes content walks `local_file`, so it _cannot_ delete a save.
  That is schema-level rather than careful coding, and it belongs in what the confirmation says.

### `local_file` rows outlive their bytes, and the budget counts them forever

Measured on the live install: **5,512 of 5,932 rows pointed at files that were not there,
claiming 18.22 GiB against 1.41 GiB of real content.** An 8 GB cap read as permanently 10 GB
over, so every sync blocked every game with 334 problems and nothing pointing at the cause. It
took a database diff to explain. `ContentPlanner` re-downloads a row whose file is gone, so it
self-heals for a game somebody re-syncs and never for one nobody does.

`InventorySweep` counts it and offers to forget the rows, which is safe by the rollback's own
argument that a row must never outlive its bytes.

**It reads `local_save` as well, and reports without repairing** (#142). A ROM row that is gone
is the wrong claim and the next sync re-downloads it. A save row may be the last local record of
a save only the server still has, and bringing that back is `saves restore`, which is asked for.
A class C row is looked up as its `(container, key)` unit, because `File.Exists` on
`saves/psp/SAVEDATA` stays true while any PSP game has a save.

**The guard that matters was found by a probe, after the first argument for it turned out to be
wrong.** The claim was that an unplugged drive cannot reach the sweep, since a tree that does not
open has no session. True, and not enough: **a tree carrying `retrobat.ini`,
`system/version.info` and the database but no `roms/` opens perfectly and reports every row
missing.** A copied install, a restored backup and a `roms/` on a second volume all reach it, and
a repair there empties the whole inventory and costs a re-download of the entire library.
`InventoryReport.NothingFound` refuses it, and one surviving file is enough to trust the tree,
because the real state looks nothing like that: 420 rows were still there.
