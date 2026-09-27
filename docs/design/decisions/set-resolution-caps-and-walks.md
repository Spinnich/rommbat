---
summary: How a set's caps choose members in two stages, and why only a completed walk retires a member.
read-when: Changing SetResolver, a set's caps or ordering, or how an interrupted resolve resumes.
---

# Set caps select in two stages, and only a completed walk decides

- **The caps select in two stages, because one stage cannot be both bounded and independent
  of arrival order.** A worst-first buffer keeps the ordering-best candidates, then one pass
  over that buffer in the set's own order takes each candidate that still fits. Dropping the
  ordering-worst as a budget fills, on its own, throws away small games a later ordering-best
  candidate would have left room for, and makes the answer depend on the order the ROMs
  happened to be imported in. A game cap bounds the buffer exactly. A byte budget does not,
  since a candidate the budget turns away lets a later one in, so the buffer falls back to
  the same 50,000 ceiling an uncapped scope is refused above. That ceiling, not the library
  size, is what a resolve holds.
- **An interrupted walk accumulates; only a completed one decides.** Each segment writes its
  rows stamped with the walk's start and reads back what earlier segments found, so the caps
  apply to the walk rather than to each segment. Membership is only retired when the walk
  finishes: a departure is an eviction candidate, and half a walk is not evidence that
  anything left. Exclusions are deleted rather than departed, being a fact about the last
  resolution rather than something on disk.
