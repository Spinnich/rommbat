---
summary: The upstream reference, how RomM and RetroBat behave as RomMBat relies on it, one fact per heading with an RB- or RM- ID.
read-when: Before relying on, citing or adding a fact about RomM's or RetroBat's behavior, or when a skill cites an RB- or RM- ID.
---

# Upstream reference

How RomM and RetroBat behave, as RomMBat relies on it. Skills hold RomMBat's rules and cite the
facts here that justify them. `docs/architecture/` holds how RomMBat's own code works. None of
the three restates another.

## Layout

| Where                              | What                                                   |
| ---------------------------------- | ------------------------------------------------------ |
| [retrobat/](retrobat/)             | RetroBat, EmulationStation and the emulators, by topic |
| [romm/](romm/)                     | RomM's API and server, by topic                        |
| [issues.md](issues.md)             | Their bugs, and what RomMBat does meanwhile            |
| `docs/platforms/<system>/facts.md` | Facts about one emulator on one system                 |

RetroBat topics: [hooks](retrobat/hooks.md), [EmulationStation](retrobat/emulationstation.md),
[es_settings.cfg](retrobat/es-settings.md), [systems and ROMs](retrobat/systems-and-roms.md),
[gamelists and media](retrobat/gamelists-and-media.md),
[save locations](retrobat/save-locations.md), [save states](retrobat/save-states.md),
[emulators and the launcher](retrobat/emulators.md), [portability](retrobat/portability.md),
[BIOS](retrobat/bios.md), [input and UI](retrobat/input-and-ui.md).

RomM topics: [auth and pairing](romm/auth-and-pairing.md), [connectivity](romm/connectivity.md),
[catalog](romm/catalog.md), [downloads](romm/downloads.md),
[media and metadata](romm/media-and-metadata.md), [saves and states](romm/saves-and-states.md),
[timestamps](romm/timestamps.md).

## A fact

Each fact is a heading carrying its ID, then the behavior and what RomMBat does because of it:

```markdown
## RB-310. A PS1 disc set in a folder with an .m3u lists as one game

Verified: RetroBat 8.2.1, 2026-09-26. How: synced MGS (2 discs), read ES's list.
<two to six lines of the behavior, and what RomMBat does because of it>
```

Facts moved from the findings ledgers keep their full text. One the repo can date takes the build
and date of the ledger section that recorded it, with `How:` naming that section's commit, and is
classified like any other at a floor move; one it cannot date has no `Verified:` line and is owed
a measurement. A row from a ledger table keeps both of its columns, each under the table's own label, unless the
first holds only a question since settled or a measurement since replaced: that line is dropped
and the heading states what is true now. Condensing a
topic file gives each of its facts the stamp and a one-line `How:`, and deletes the evidence
narrative.

**The stamp is what a floor move is checked against.** `python3 tools/docs/check.py --stale`
lists every fact whose stamp names a RetroBat or RomM build below the floor in code, and every
fact with no stamp. A stamp naming several builds of one project counts its newest, and text after
`How:` is not read, so name the build a fact was re-measured on before it.

**A fact the delta does not touch is carried, not re-measured**
([decision](../design/decisions/delta-directed-recheck.md)). Add a clause before `How:`:

```markdown
Verified: RetroBat 8.2.1, 2026-10-04. Carried: RetroBat 8.3.0, 2026-10-14, no launcher commit or reference diff names `ares` or `GameGear`. How: ...
Verified: RetroBat 8.2.1 and RomM 5.3.0, 2026-09-23. Carried: RetroBat 8.3.0, 2026-10-14, <reason>. Carried: RomM 5.3.1, 2026-10-14, <reason>. How: ...
```

The stamp is one line, since the checker reads only the `Verified:` line. Carry clauses go last, before `How:`, and
each one's reason runs to the next clause or `How:` and is not read for builds. A later carry replaces an earlier one for the
same project. A fact stamped for both projects carries each in a clause of its own, and a clause
with no reason carries neither.

The reason says what was read and why it does not reach the fact; "unchanged" is not one, and a
clause without a reason carries nothing and stays on the stale list. `--stale` lists the carried
facts apart so the reasons can be read. A fact is touched, and re-measured, when the delta names what its heading or body
names and the change could alter what the fact says ([delta.md](../../.claude/skills/version-adoption/delta.md), "Which facts a delta touches").

## IDs

- `RB-` IDs came from the RetroBat ledger and `RM-` IDs from the RomM one. A fact lives with its
  subject, so the RomM topics hold some `RB-` facts. A new fact takes the prefix of the project it
  describes.
- An ID never changes. Cite it as `RB-310` in docs and code comments. `tools/docs/check.py`
  fails on a citation that resolves to no heading.
- A fact is edited in place when the behavior changes on a version move, keeping its ID. A fact
  that stops being true is deleted with every citation of it.
- An ID is never reused. The next free numbers are **432** for `RB-` and **29** for `RM-`.
  Take one and raise the number here in the same change.
