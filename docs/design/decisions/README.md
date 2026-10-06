---
summary: The index of RomMBat's decision records, one standing decision and its reason per file.
read-when: Before changing behavior a decision may govern, or when asking why the code does something a skill does not explain.
---

# Decisions

Each record is one decision that still stands, and why. A decision that is reversed is rewritten
or deleted, never marked superseded. The rules those decisions produce are in the skills; a
record exists where no skill or [architecture](../../architecture/README.md) file already states it.

| Record                                                                    | Decides                                                           |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| [server-url-is-the-one-typed-value](server-url-is-the-one-typed-value.md) | The server address is the only thing a person types               |
| [set-resolution-caps-and-walks](set-resolution-caps-and-walks.md)         | How caps choose members, and when a walk may retire one           |
| [eviction-order-and-policies](eviction-order-and-policies.md)             | Eviction's preview, its honored policies and its order            |
| [firmware-budget-and-triggers](firmware-budget-and-triggers.md)           | Firmware is budgeted, never evicted, and fetched before ROMs      |
| [hooks-install-on-first-sync](hooks-install-on-first-sync.md)             | Hooks install on the first sync, announced, with no opt-in        |
| [clock-file-keeps-its-own-slot](clock-file-keeps-its-own-slot.md)         | A `.rtc` stays one slot until RomM bundles saves                  |
| [class-b-batch-report](class-b-batch-report.md)                           | Class B siblings report as one batch; `batch_key` stays unwritten |
| [where-a-sentence-lives](where-a-sentence-lives.md)                       | Which of Core or a front end owns a message                       |
| [sets-on-the-interface](sets-on-the-interface.md)                         | How the UI defines, names, filters, resolves and roams a set      |
| [sync-run-is-whole-or-absent](sync-run-is-whole-or-absent.md)             | A sync leaves each game whole or absent, and what follows from it |
| [picked-sets-and-per-game-install](picked-sets-and-per-game-install.md)   | Hand-picked games are a scope kind; one press installs a game     |
| [conflict-and-mapping-screens](conflict-and-mapping-screens.md)           | Two conflict verbs and no resolve-all; unmapped platforms first   |
| [interface-language](interface-language.md)                               | The interface stays English; the keyboard follows ES's language   |
| [packaging-and-release](packaging-and-release.md)                         | The portable zip is the artifact, and who announces a release     |
| [versioning](versioning.md)                                               | SemVer 2.0.0, what each part means, and the schemes rejected      |
| [stable-only-floor](stable-only-floor.md)                                 | The floor names a stable; a prerelease is scouted, not adopted    |
