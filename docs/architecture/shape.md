---
summary: How RomM.Client, Core, the agent, the hook and the UI talk to each other, and the one-way dependency rule between them.
read-when: Before adding a project reference or a type that crosses from one project into another.
---

# The shape of it

```text
                       RomM server  (may be unreachable)
                             |
                   HTTPS, Authorization: Bearer rmm_...
                             |
     +-----------------------+------------------------+
     |           RomM.Client  (net10 library)         |
     |   generated from /openapi.json + hand-written  |
     |   pairing, resumable download, negotiation     |
     +-----------------------+------------------------+
                             |
     +-----------------------+------------------------+
     |                  RomMBat.Core                   |
     |   SQLite: local file index, hashes, cursors,    |
     |   sync sets, OUTBOX (saves/states/sessions)     |
     |   RetroBat root discovery, es_systems reader,   |
     |   platform + save-dir maps, gamelist merger     |
     +------+--------------------------+---------------+
            |                          |
 +----------v-----------+   +----------v---------------+
 |  rommbat-agent.exe   |   |      RomMBat.exe         |
 |  one pass, then exit |   |  full-screen gamepad UI  |
 |  drains the spool    |   |  pair, sync sets, browse |
 |  flush: when online  |   |  online browse is paged  |
 +----------^-----------+   +--------------------------+
            |  spool/*.hook files; start and quit
            |  also spawn `background <event>`
 +----------+-----------+
 |  rommbat-hook.exe    |   run by EmulationStation, one copy
 |  writes one file     |   per event folder; no socket, no
 |  and exits           |   database, no lock
 +----------------------+
```

The dependency direction is strict and one-way:

```text
RomMBat.Agent ─┐
               ├─> RomMBat.Core ─> RomM.Client
RomMBat.UI ────┘

RomMBat.Hook ···> 3 source files from RomMBat.Core, compiled in, no reference
```

`RomM.Client` knows nothing about RetroBat. `RomMBat.Core` knows nothing about the console
or the UI. Neither the agent nor the UI talks to the API except through Core, and the hook
does not talk to it at all. If a type needs to go the other way, the design is wrong.

The hook references no project, because four copies of it are installed and a reference to
Core would put the store and the API client into each. It compiles Core's `RootMarkers`,
`SpoolRecord` and `Spool` instead, so the file it writes and the drain that reads it share
one definition. What it does is in [Projects](projects.md#srcrommbathook).
