---
summary: RomMBat's house style for every doc, skill and comment, and which rules a tool enforces.
read-when: Before writing or editing any Markdown in the repo, or a comment longer than a line.
---

# Writing

Most of RomMBat's readers are agents, and they act best on "the rule, one line of why, the
evidence". People read that shape well too. This page is the house style for every layer.

## Every layer

- **Present tense only.** A doc says what is true now. When that changes, the same PR edits or
  deletes the text. No milestone narratives, no superseded readings, no
  `an earlier revision said`. Git is the history.
- **The rule first, then why.** Lead with what to do or what is true. One sentence of reason is
  usually enough. Link the evidence rather than retelling it.
- **One home per fact.** State a fact once and link to it everywhere else. A second copy goes
  stale on its own schedule.
- **At most one bold phrase per section.** Bold the one thing a skimming reader must not miss.
  When every paragraph opens in bold, nothing stands out.
- **Short files, one topic each, descriptive names.** Headings are stable anchors other files
  link to, so rename one only with its links.
- **American English only**, outside localization files: US spelling and vocabulary in docs,
  comments, commit messages and anything a user reads (behavior, color, canceled, labeled,
  license, catalog, artifact, and -ize rather than -ise). A quotation from upstream keeps its
  own spelling, and so does an identifier or a persisted value until something deliberately
  renames it.

## By layer

| Layer                              | Voice                                                                                          |
| ---------------------------------- | ---------------------------------------------------------------------------------------------- |
| Skills and `CLAUDE.md` files       | Imperative. A rule, one line of why, and the finding that justifies it                         |
| Developer docs (`docs/`)           | Present tense, the rule or the fact first. Open with `summary:` and `read-when:` frontmatter   |
| The guide (end-user docs, `wiki/`) | Second person, task first, written for someone who is not a developer                          |
| Code comments                      | Short, and about why rather than what. Describe how the code behaves now, never why it changed |

## Words with one meaning

Terms such as row, shape, slot, set and floor are defined in
[the glossary](../design/glossary.md). Use them in that sense, or pick another word.

`dry-run` in code formatting names `sync`'s flag and nothing else. `bios` and `evict` preview by
default and write on `--apply`, so a generic preview is a "preview". "A dry run", two words, is
ordinary English and is fine.

## Writing for readers outside the repo

Anything sent to the RomM team, an upstream tracker or any other outside reader holds only
verified claims: measured on a certified `(system, emulator, core)` row, read in upstream source,
or measured live against the server. Verify an unverified point first and add it afterwards;
never send it labeled preliminary, which hands the checking to someone else. Evidence from an
uncertified platform becomes a verification task, not a line in the document.

## Markdown traps

**A bare `$` in prose opens inline math**, in markdownlint, prettier and GitHub's renderer, and
pairs with the next `$` in the paragraph, even one inside a later code span. Every code span
between them breaks: prettier strips the spaces around them, and trunk reports MD038 and MD033
at column 0, which points nowhere useful. Write `\$`, or `&#36;` under `wiki/`, since MkDocs
prints `\$` with its backslash. A `$` inside backticks needs no escape.

## What `tools/docs/check.py` enforces

These rules are mechanical, so a tool checks them and the prose rules above do not repeat them.
The Claude Code hook runs the check on every Markdown file an agent writes, and CI runs it on
the tree.

| Rule                                                                                                   | Enforcement |
| ------------------------------------------------------------------------------------------------------ | ----------- |
| No em-dashes, in docs, comments or commit messages. Use commas, parentheses or separate sentences      | Fails       |
| Relative links and anchors resolve, in exact case, against files git tracks                            | Fails       |
| Every cited `RB-` or `RM-` fact ID is defined exactly once                                             | Fails       |
| Size budget: 500 lines a file, 300 for a `SKILL.md`, 200 for the root `CLAUDE.md`, 60 for a nested one | Fails       |
| Always-loaded context (every `CLAUDE.md`, `AGENTS.md`, the local `MEMORY.md`) under its ceiling        | Reported    |
| `summary:` and `read-when:` frontmatter on every file under `docs/`                                    | Reported    |
| History phrasing, such as `The move to` or `Superseded`                                                | Fails       |
| Generic `dry-run`                                                                                      | Reported    |
| A British spelling from the checker's list, outside code and quotation marks                           | Reported    |
| A `$` in prose written `\$`, or `&#36;` under `wiki/`, which MkDocs does not unescape                  | Fails       |

A reported rule becomes a failing one once the tree meets it. The context ceiling stays reported,
because it counts the maintainer's local `MEMORY.md`, which CI never sees. `check.py --stale` is a
listing rather than a rule: the facts owed a re-check at the current floor, which a floor move
works through (`docs/upstream/README.md`). Commit messages are
outside the checker's reach, so the em-dash rule there is on the author.
