---
summary: Class B siblings are reported as one batch from the flush's own map, and outbox.batch_key stays unwritten.
read-when: Changing how a class B save's sibling files are uploaded or reported, or giving outbox.batch_key a writer.
---

# Class B siblings report as one batch

A class C unit is one (container, key) pair, so it bundles to **one archive, one slot and one
upload**, GameCube's several `.gci` per game code included. There is no second row.

So `batch_key`'s only genuine caller is class B's siblings, and class B is not in the outbox.
Rather than retrofit stage 1's proven upload path onto a queue it does not use, stage 2b delivers
the behaviour the column was a proxy for: `SaveSync` already holds every sibling of a slot in one
map, so a partial result is grouped by `(rom_id, base slot)` and reported as one batch. The column
stays unwritten and is kept, because a future queued-upload design would want it back and the
schema is already shipped. Until then a sibling that fails is simply retried by the
next flush, which is correct but says less than it should.
