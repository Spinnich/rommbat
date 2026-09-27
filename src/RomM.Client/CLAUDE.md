# RomM.Client

The RomM API and nothing else: no disk, no SQLite, no RetroBat. Core is its only consumer.
Load the `romm-api` skill before changing anything here; its "Traps" section is the one that
costs the most when skipped.

| Where                            | What                                                                        |
| -------------------------------- | --------------------------------------------------------------------------- |
| `Generated/`                     | NSwag DTOs from the pinned `/openapi.json`. Committed, never edited by hand |
| `openapi/`                       | The pinned schema, `generate.sh`, and how to move the pin (`README.md`)     |
| `Catalog/`, `Content/`, `Saves/` | Hand-written calls, grouped by what they fetch                              |
| `DevicePairing.cs`               | The only way RomMBat gets a token                                           |
| `RomMConnection.cs`              | Owns the `HttpClient` and its handler                                       |

## Traps

- **Regenerate DTOs only when moving the RomM floor** (`CLAUDE.md` rule 6), and review the diff.
  Anything the schema gets wrong is fixed in the normalisation step `openapi/README.md`
  describes, never in `Generated/`.
- **Every handler sets `SocketsHttpHandler.ConnectTimeout`** (rule 5). A new handler without it
  stalls 21 s on an absent LAN host.
- **Failures go through `RomMTransportErrors.Classify`**, never a bare `catch`. A timeout and a
  cancellation are both `TaskCanceledException`, and a naive catch reports every offline server
  as a user action.
- **A 401 or 403 is a value, not an exception.** Authenticated calls return `RomMResponse<T>`;
  only transport failures throw.
- **`GET /api/roms` always passes `with_char_index=false`, `with_filter_values=false` and
  `with_rom_id_index=false`.** Each sidecar scans the library. Nothing calls
  `/api/roms/identifiers`, and nothing reads `rom_ids` off a collection.
- Every call takes a `CancellationToken`.
