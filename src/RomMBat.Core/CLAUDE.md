# RomMBat.Core

Local state, and everything that knows RetroBat's disk layout. Agent and UI are shells over it:
a rule that lives in either of them lives in the wrong place.

| Where                       | What                                                       | Skill                               |
| --------------------------- | ---------------------------------------------------------- | ----------------------------------- |
| `Store/`                    | SQLite: file index, sets, journal, outbox, slots, bindings | `offline-and-portable`              |
| `Paths/`, `RetroBatRoot.cs` | Root discovery, `RelativePath`, resolving a stored path    | `offline-and-portable`              |
| `RetroBat/`                 | Readers and writers for `es_*.cfg`, `gamelist.xml`, hooks  | `retrobat-layout`                   |
| `Mapping/`                  | RomM platform to RetroBat system                           | `platform-mapping`                  |
| `Content/`                  | ROM, media and BIOS planning and download; save scanning   | `save-sync` for `Save*`             |
| `Sync/`                     | Save and state sync, the flush, `TreeLock`, the spool      | `save-sync`, `offline-and-portable` |
| `Sets/`                     | Console-free services the Agent and UI both call           | per the area it composes            |

## Traps

- **No path is stored absolute** (`CLAUDE.md` rule 1). Store APIs take `RelativePath`, every
  path column has a `CHECK`, and `RetroBatInstall.Resolve` is the one place a stored path becomes
  absolute.
- **Migrations are append-only.** Add the next numbered script under `Store/Migrations/`; never
  edit a shipped one. A database from a newer build is refused, not opened.
- **Writes into the tree are merge-not-clobber and atomic**: write aside, then rename. The
  `es_settings.cfg` writer also refuses while EmulationStation is running, because ES discards
  a write made underneath it.
- **Every writer of save files takes `TreeLock` and asks `InFlightGuard`.** A flush that cannot
  get the lock exits as done; anything else refuses.
- **`data/retrobat/save_rules.json` is hand-edited** alongside its generator; regenerating it
  from a live install corrupts the loose rule.
- Timestamps are the file's real mtime and carry a local sequence number, never the sync time.
