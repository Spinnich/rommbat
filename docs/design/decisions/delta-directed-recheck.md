---
summary: A floor move re-measures only the facts the upstream delta touches, and carries the rest with a stated reason.
read-when: Moving the RomM or RetroBat floor, working the `--stale` list, or changing how a fact's `Verified:` stamp is read.
---

# A floor move re-measures what the delta touches

**A fact is re-measured at a new floor only when the upstream delta reaches it.** Every other fact
is carried: its stamp gains `Carried: <project> <build>, <date>, <why the delta does not reach
it>.`, and `tools/docs/check.py --stale` then counts it at the new build and lists it apart, with
its reason, so a reviewer reads the reasons rather than trusting the count.

**What the delta is.** The sources `version-adoption` reads, in `delta.md`. For RetroBat: the
diff of the vendored `reference/` files, which are the files RomMBat's design depends on; the
emulatorlauncher and EmulationStation commits between the previous and the new pinned ref; the
changelog; the emulator builds a release downloads, because RetroBat ships more than its repo; and
RetroBat's own tree between the two tags, whose templates seed a fresh install's emulator configs.
For RomM: the source diff between the two tags and the diff of the pinned OpenAPI schema. A fact is
touched when its heading or body names a system, emulator, core, option, file, path, endpoint,
field, scope or config key that one of these changed. A changelog line nobody measured counts as
touching, as it does for a platform record's steps (`platform-certification`, "When the floor
moves").

**Why.** Re-measuring every fact at every floor move grows with the number of facts and costs a
hands-on pass per fact, for a delta that touches a handful. A platform record's nine steps are
already re-run only where the move touches them, and a fact's stamp follows the same rule.

Rejected:

- **Re-measure every stale fact.** Its cost scales with the facts and buys the least where nothing
  changed.
- **Carry everything and never re-measure.** A `Verified:` line would then say only that nobody
  has looked, and a floor move would never correct a fact the delta changed.
- **Detect the touched facts by a script.** A fact names its subject in prose, and a token match
  against a diff is noisy in both directions. The reasoning is the part that has to be read.

A fact with no stamp has no build to carry from. One moved from a findings ledger takes the build
and date its ledger section recorded, with `How:` naming the commit, rather than a hands-on pass
for a measurement the repo already holds; one the repo cannot date is owed a measurement. A fact the
delta reaches is re-measured and restamped, or deleted with its citations when it stopped being true.
