---
summary: A floor move re-measures only the facts the upstream delta touches, and carries the rest with a stated reason.
read-when: Moving the RomM or RetroBat floor, working the `--stale` list, or changing how a fact's `Verified:` stamp is read.
---

# A floor move re-measures what the delta touches

**A fact is re-measured at a new floor only when the upstream delta reaches it.** Every other fact
is carried: its stamp gains `Carried: <project> <build>, <date>, <why the delta does not reach
it>.`, and `tools/docs/check.py --stale` then counts it at the new build and lists it apart, with
its reason, so a reviewer reads the reasons rather than trusting the count.

**What the delta is.** The sources `version-adoption` already reads, in `delta.md`: the diff of the
vendored `reference/` files, which are the files RomMBat's design depends on; the
emulatorlauncher and EmulationStation commits between the previous and the new pinned ref; the
changelog; and the emulator builds a release downloads, because RetroBat ships more than its repo.
A fact is touched when its heading or body names a system, emulator, core, option, file or path
that one of these changed. A changelog line nobody measured counts as touching, as it does for a
platform record's steps (`platform-certification`, "When the floor moves").

**Why.** Re-measuring every fact at every floor move grows with the number of facts and costs a
hands-on pass per fact, for a delta that touches a handful. The first pass at a floor move found
that most facts held and that the ones that did not were named in the launcher delta. The rule
that already governs a platform record's nine steps was the cheaper one, and the fact stamps were
the outlier.

Rejected:

- **Re-measure every stale fact.** The rule before this one. Its cost scales with the facts and
  buys the least where nothing changed.
- **Carry everything and never re-measure.** A `Verified:` line would then say only that nobody
  has looked, and a floor move would never correct a fact the delta changed.
- **Detect the touched facts by a script.** A fact names its subject in prose, and a token match
  against a diff is noisy in both directions. The reasoning is the part that has to be read.

What stays as it was: a fact with no stamp is still owed a measurement, since there is no build to
carry it from, and a fact whose delta reaches it is re-measured, restamped or deleted as before.
