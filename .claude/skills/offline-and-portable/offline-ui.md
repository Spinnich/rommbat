# Offline screens

Part of the [offline-and-portable](SKILL.md) skill. What the sets, mapping and removal surfaces do with the server off.

## Offline-first

- **Which operations work with the server off, on the sets surface.** Listing sets, defining
  one, editing its caps and ordering, deleting it, and setting the disk budget and free-space
  floor are all local and all answerable offline. **Only resolving and syncing need the
  network**, and they are the only screens that say so. A screen that cannot tell those two apart
  is wrong: the whole point of defining a set on a handheld away from its server is that it can
  be done.

  **The platform mapping is offline too, and its repair is install-wide.** `platform_map` is
  written by every resolve, so every row the mapping screen shows, and the
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
  user's, by dropping a sync set. Do not go looking for eviction screens.

  **A resolve is unbounded work over somebody else's network, so a cancel is the ordinary
  case, not a failure path.** A cancel records its offset exactly as an unreachable server does
  so the next run continues; discarding the walk on cancel would make the feature worse than not
  offering it. One platform walk over a library of 95,993 roms measured 22.5 s on
  5.3.0-alpha.2, but that is one platform on one server: a slower host, a larger scope or a
  worse link puts minutes back, and a resume costs nothing.

## Lists and removal, offline

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
