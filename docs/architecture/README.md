---
summary: How RomMBat's code is laid out, how the pieces talk to each other, and what the local schema holds, one file per area.
read-when: Before adding a class, to find the file for the area it belongs to.
---

# RomMBat architecture

How the code is laid out, how the pieces talk to each other, and what the local schema
holds.

[docs/design/](../design/principles.md) is the design of record and says **why**. These files
say **where**, and are the ones to read before adding a class. Where the two disagree, the
design wins and these files need fixing.

| File                                                             | What it covers                                                                                                                    |
| ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| [The shape of it](shape.md)                                      | How RomM.Client, Core, the agent and the UI talk to each other, and the one-way dependency rule between them.                     |
| [Projects](projects.md)                                          | What RomM.Client, Core, the agent, the UI and the two test projects hold, their subcommands or screens, and the rules each keeps. |
| [Reference data and bundled tables](reference-data.md)           | The difference between reference/ and the tables under data/ that RomMBat ships and reads at runtime.                             |
| [The local store](local-store.md)                                | The SQLite store: its tables, the migrations, how paths are kept relative, and why the sequence number and journal exist.         |
| [Identity](identity.md)                                          | How the device identity follows the drive rather than the host, and how the token is kept at rest.                                |
| [Being offline is the normal case](offline.md)                   | How every operation behaves when the server is unreachable, a transfer is cut, or the clock is wrong.                             |
| [Writing into someone else's tree](writing-into-the-tree.md)     | The discipline for writing files RetroBat or EmulationStation also own, file by file.                                             |
| [Two authorities that are easy to get backwards](authorities.md) | Why file extensions and firmware requirements come from RetroBat, and why neither gates a sync.                                   |
| [Saves](saves.md)                                                | The four save shapes, attribution rules, the flush service, conflicts and save states as the code implements them.                |
| [Adding something](adding-something.md)                          | Where to start and which skill to load for each kind of addition, and three questions to ask first.                               |
