---
summary: Where to start and which skill to load for each kind of addition, and three questions to ask first.
read-when: Before writing a new class, table, mapping or platform.
---

# Adding something

| You are adding                                   | Start in                                   | Load the skill           |
| ------------------------------------------------ | ------------------------------------------ | ------------------------ |
| An API call                                      | `RomM.Client`                              | `romm-api`               |
| A table or column                                | `Store/Migrations/NNN-*.sql`, never 001    | `offline-and-portable`   |
| Anything reading or writing the RetroBat tree    | `RomMBat.Core`                             | `retrobat-layout`        |
| A platform mapping fix                           | `data/retrobat/platforms.json` plus a test | `platform-mapping`       |
| Save or state handling                           | `RomMBat.Core`                             | `save-sync`              |
| Anything touching paths, the outbox or the clock | `RomMBat.Core`                             | `offline-and-portable`   |
| A new supported platform                         | `docs/platforms/<system>.md`               | `platform-certification` |
| Wrapping up any change                           |                                            | `pre-pr-verification`    |

Ask three questions before writing the class:

1. Does it persist a path? Then it persists a relative one.
2. Does it run on the game-launch path? Then it does not open a socket.
3. Does it need the server? Then it needs a defined behaviour when the server is gone.
