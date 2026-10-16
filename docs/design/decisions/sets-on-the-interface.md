---
summary: Which scopes a sync set can have, and how the gamepad UI defines, names, resolves and roams one, and what it leaves to the console.
read-when: Changing the sets screens, the scope kinds, or how a set made from the UI resolves and roams.
---

# Sets on the interface

**A set holds a platform, a collection or a smart collection, and nothing else.** RomMBat is a
sync client, not a library browser: each of the three is something RomM already defines and
names, resolves by paging `GET /api/roms` with one scoping parameter, and needs no picker
beyond a list of what exists. There is no in-app search, saved filter or per-game pick, and no
screen for looking through the library. Metadata and media still sync into RetroBat's folders,
where EmulationStation shows them.

**Virtual collections are not a scope.** That route needs a `type` parameter the pinned schema
declares as a bare string with no enumeration, so its values could only be guessed, and
inventing a list of likely values is the vendor-id table the input work threw out. They become a
scope if RomM enumerates `type`.

**A set of a removed kind is deleted on upgrade, and its files stay.** Migration 022 drops the
set, its membership and its walk cursor. What it downloaded stays on disk and in `local_file`,
where eviction treats it as a download no set claims, and saves are never a set's to remove.

**Per-set caps are not on the interface, a choice that came from a hands-on pass.** A set made
from a collection or a platform is usually a mirror of something the user already chose, and
capping it to N leaves RomMBat guessing which N; no ordering makes that guess good. The bound a
person sets is the install-wide disk budget, which `ContentPlanner` and `EvictionPlanner`
enforce. `sets add` keeps `--max-games`, `--max-bytes` and `--order`, and a set given caps from
the console keeps them: the editor sends no cap values at all rather than the cleared ones a
hidden row would have produced.

**Eviction is not on the interface either.** Freeing space belongs to the user, who does it by
raising the budget or by dropping a set, which is them saying which games they no longer want.
A sync the budget cut short says so and offers nothing. `evict --apply`, and its sweep of dead
transfers under `partial/`, stay on the console.

**A set is named after what it mirrors.** Every scope already has a name in RomM, so creating a
set is pick, pick, create, and the on-screen keyboard is not on the path at all. A name another
set already has gains what tells the new set apart: its folder when it has one, otherwise its
scope, then a number. A set holding the same scope, value and folder as an existing one is
refused with that set's name, because it would pull the same games twice. Both rules are the
interface's: `sets add` is given its name, refuses a taken one, and takes an exact duplicate.

**A scope that can be picked has to be completable.** A test asserts the general rule: every
scope offered as pickable has something that can produce its value, the platform picker or the
collection picker.

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
