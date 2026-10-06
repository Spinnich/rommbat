---
summary: How the gamepad UI defines, names, filters, resolves and roams a sync set, and what it leaves to the console.
read-when: Changing the sets screens, the filter scope, or how a set made from the UI resolves and roams.
---

# Sets on the interface

**Per-set caps are not on the interface, a choice that came from a hands-on pass.** A set made from a collection or a platform is usually a mirror of something the
user already chose, and capping it to N leaves RomMBat guessing which N; no ordering makes that
guess good. The bound a person sets is the install-wide disk budget, which already existed and
which `ContentPlanner` and `EvictionPlanner` already enforce. `sets add` keeps `--max-games`,
`--max-bytes` and `--order`, and a set given caps from the console keeps them: the editor sends
no cap values at all rather than the cleared ones a hidden row would have produced.

**Eviction is not on the interface either.** Freeing space belongs to the user, who does it by
raising the budget, or by dropping a set or taking one game off this device, which is them
saying which games they no longer want.
A sync the budget cut short says so and offers nothing. `evict --apply`, and its sweep of dead
transfers under `partial/`, stay on the console.

**A set is named after what it mirrors.** A platform and a collection both already have a name
in RomM, so a platform or collection set is pick, pick, create, and the on-screen keyboard is
off the common path entirely.

**A filter scope is a saved search rather than a name match, and it is RomM's whole search.**
Eleven multi-selects, each with the `any` / `all` / `none` operator RomM's own `*_logic`
parameters take, and ten yes-or-no properties. The values come from the live library through
`with_filter_values`, which is the single job that sidecar exists for and the one
`GetFilterValuesAsync` serves.

**Two of the eleven are not in the sidecar and are not derived from the library.** Statuses are
a vocabulary the user assigns, taken from the pinned schema's `RomUserStatus`. Metadata
providers have no enumeration in the schema at all, and the server **silently ignores** a value
it does not recognize, so a wrong entry would hand somebody the whole library while looking
like a filter: they were probed one at a time against a live instance (RB-236). Deriving
them from the rom row's `*_id` fields would have been wrong, which the probe is how we know.

**Four properties answer from RomM's records rather than from the game**, so a set carrying one
resolves differently on another account or after a scan. That is said on the row, once it is
set, rather than left in a document. A set is re-resolved on demand and is expected to move,
which is why this is a caveat and not a reason to withhold them.

**A filter can be changed after the set exists, which narrows "scope is not updatable".** That
rule stands for a scope's kind and its target: a set pointed at a different platform is a
different set. A filter's scope value is a query rather than an identity, and the rule's own
reason, that answering the new question means a re-resolve, was written when a resolve was a
terminal command and now costs one press. Changing one clears the resolution stamp and lands
on the set resolving, exactly as creating one does. The membership is deliberately **not**
deleted: that would orphan whatever is on disk and hand it to the next eviction pass on the
strength of an edit.

**A scope that can be picked has to be completable.** Virtual collections are offered and
disabled, because that route needs a `type` parameter the pinned schema declares as a bare
string with no enumeration, at 5.2.0 and still at 5.3.0-alpha.2, and inventing a list of likely values is the vendor-id table
the input work threw out. A test asserts the general rule: every scope offered as pickable has
something that can produce its value.

**Creating a set lands on it and starts resolving.** A set that has never resolved holds
nothing and can do nothing, so returning to the list left the person one press short of what
they had just described. Starting minutes of network work uninvited is only reasonable because
stopping costs one press and keeps what it found.

**A set defined from the interface roams, and the resolve is what mirrors it.** `sets add` and
`sets resolve` both push `Device.sync_config`, and the interface does too, so the same action
persists the same way whichever front end takes it. The push hangs off the resolve
screen rather than off the save, because creating and editing both land there and it is the one
place with somewhere to say the push failed. Best effort as everywhere else: its own connection,
never on the screen's cancellation token, and a failure is a note appended to the result rather
than an error. Roaming is how set definitions reach other devices, and the front end with no
prompt is the one that needs it most.

**A pick roams too**, because the picked set's ids are its definition. Browse's install and
`game install` both push beside the one-game fetch, on the same best-effort terms. On the
interface the install screen is where a failure is said, as one of its problems rather than in
the detail line the finished install replaces. Taking a picked game off pushes too, from the
removal screen and `game remove --apply`, after the unpick so the push carries the shorter
list. The removal screen waits for it, since nothing else there takes long, and a failure is
one of its problem rows.
