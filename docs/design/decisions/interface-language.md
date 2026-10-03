---
summary: The interface stays English while the on-screen keyboard follows EmulationStation's language, and why localising is a milestone.
read-when: Proposing localisation, or changing how the keyboard layout is chosen.
---

# The interface stays English

**Deferred, and worth stating so it is not re-derived: RomMBat does not speak that language,
only types in it.** Reading `Language` to pick a keyboard is one setting and a three-way switch.
Localising the interface is a different thing entirely, and the cost is not the resource files:
Core returns records carrying **pre-written English sentences**, a decision made
deliberately so that a refusal reads identically on both front ends. Every one of those would
have to become a key plus arguments, across Core, the agent and the UI, and the agent's output
is what `sets`, `sync` and `evict` are tested on byte for byte. It also contradicts CLAUDE.md's
"English only outside of localisation files" as written, so the rule moves in the same change or
not at all. **A milestone, not a commit**, and nothing before M8 ships. Note also that only three
keyboards exist upstream, so a German or Japanese install already types on the US grid in ES
itself: matching the interface's language would outrun the keyboard's, not follow it.
