---
summary: The four projects, what each talks to, and the one-way dependency rule between them.
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
 |  ES hooks: journal   |   |  full-screen gamepad UI  |
 |  only, no network    |   |  pair, sync sets, browse |
 |  flush: when online  |   |  online browse is paged  |
 +----------------------+   +--------------------------+
```

The dependency direction is strict and one-way:

```text
RomMBat.Agent ─┐
               ├─> RomMBat.Core ─> RomM.Client
RomMBat.UI ────┘
```

`RomM.Client` knows nothing about RetroBat. `RomMBat.Core` knows nothing about the console
or the UI. Neither executable talks to the API except through Core. If a type needs to go
the other way, the design is wrong.
