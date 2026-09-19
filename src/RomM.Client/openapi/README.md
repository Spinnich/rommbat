# The pinned RomM schema

`romm-5.3.0-beta.1.json` is a byte-exact copy of `GET /openapi.json` (served at the root,
not under `/api`) from a RomM instance reporting `SYSTEM.VERSION = 5.3.0-beta.1`. The generated DTOs
in [`../Generated/RomMApiSchema.g.cs`](../Generated/RomMApiSchema.g.cs) come from it and
are committed, so an upstream deploy cannot change the contract mid-session.

|                |                                                                    |
| -------------- | ------------------------------------------------------------------ |
| RomM version   | 5.3.0-beta.1, the minimum RomMBat supports                         |
| Source         | a self-hosted 5.3.0-beta.1 instance, host redacted                 |
| Pulled         | 2026-09-19                                                         |
| `info.version` | 5.3.0-beta.1                                                       |
| sha256         | `26ace33006fb2ac44ebaa96fc292547811ed7b2b208c97d1cb445d6827622a96` |
| Paths          | 198                                                                |
| Schemas        | 272                                                                |

**The pin is always the minimum version RomMBat declares support for**, so the generated DTOs
describe the oldest server the client claims to work with. Since RomMBat tracks the newest
stable, or a prerelease ahead of it as it does now, that is also the newest release RomMBat has
adopted: moving the floor and moving the pin are one decision.

**Prefer the public demo at `demo.romm.app` as the source**, because anyone can reproduce the
file from it without an account, a token or a hostname that would have to be scrubbed. The
5.1.0 pin came from there. None of the 5.2.0, 5.3.0-alpha.2 or 5.3.0-alpha.3 pins nor this one
did: the demo reported 5.1.0 on 2026-08-25 and 5.2.0 on 2026-09-14, so each was pulled from a
self-hosted instance of the pinned version instead. A prerelease will not reach the demo at all
until it ships as stable.

The sha256 above is how the capture is checked rather than trusted; a `/openapi.json` from any
stock 5.3.0-beta.1 hashes to it. That holds because `backend/main.py` registers every router
unconditionally at this tag, so the served schema is decided by the version and not by the
instance's configuration. **`SYSTEM.VERSION` was read at capture time rather than assumed**: a
live library can be upgraded underneath the work, which is how upstream's own tag moved from
`alpha.1` to `alpha.2` eight hours after publication. The file was searched for the source
hostname before committing and contains none. When the demo catches up to the pinned version,
re-pull from it and confirm the hash is unchanged.

Development and testing run against the instances in
[DEVELOPER_SETUP.md](../../../DEVELOPER_SETUP.md) section 3; only the pin is discussed here.

## Regenerating

```bash
cd src/RomM.Client/openapi && ./generate.sh
```

Requires `dotnet tool restore` once per clone (NSwag is a local tool, pinned in
[`.config/dotnet-tools.json`](../../../.config/dotnet-tools.json)) and Python 3.10+.

**Only re-run this when deliberately moving the pin**, and review the diff. Moving the pin
is a compatibility decision: it changes which server version the DTOs describe, so the
README compatibility table and `RomMServerVersion.Minimum` move with it.

**Read the operation and schema diff, not just the generated C#.** The 5.1.0 to 5.2.0 move
was additive except for one thing the DTO diff shows as a single character:
`CustomLimitOffsetPage_SimpleRomSchema_.total` became nullable, so `Total` generated as
`int?`. The server returns null only when a caller asks for neither `with_total` nor
`with_rom_id_index`; `CatalogQuery` always sends `with_total=true` and a test asserts it, so
`RomPage.Total` stays a non-nullable `int`. A pin move that silently turned a field nullable
under code that assumes otherwise would throw at deserialisation, not degrade.

The 5.2.0 to 5.3.0-alpha.2 move adds 32 operations and removes none, and retypes no member of
any retained class. Two members leave: `ClaimSessionRequest`, renamed to
`ClaimStreamingSessionRequest`, and `ConfigResponse.DEFAULT_EXCLUDED_DIRS`, which splits into
`DEFAULT_EXCLUDED_MULTI_FILE_DIRS` and `DEFAULT_EXCLUDED_PLATFORM_DIRS`. No hand-written code
names either, so the move is additive where this client reads. `SimpleRomSchema` and
`DetailedRomSchema` each gain fourteen properties, of which `title_id`, `save_target`,
`save_target_layout`, `has_file_on_disk` and `is_physical` are the ones this repo has open
questions against.

The 5.3.0-alpha.2 to 5.3.0-alpha.3 move **removes one operation**, the streaming
`POST /api/streaming/sessions/{platform}/state-frame`, with its `StateFrameResponse`, and adds
none. `SGDBResource` is renamed `CoverResource`, and `MissingRomsCleanupStats.platform_id`
becomes a `platform_ids` list. No hand-written code names any of the three. On a route this
client calls, the only change is `slot` on `POST /api/saves` gaining `maxLength: 255`; the
`GET /api/roms` parameters are the same set in a different order, and the collection ids on
`GET /api/roms/download` gain `minimum: 1`. `docs/romm-5.3-findings.md`, section 11.

The 5.3.0-alpha.3 to 5.3.0-beta.1 move is the smallest of the four: **the same 246 operations
and 272 schemas, none added, removed or renamed.** Three things change, and the generated diff
is four lines.

- `RomFileSchema.last_modified` becomes nullable, so `Last_modified` generates as `string?`.
  This is the nullable retype the 5.2.0 paragraph above warns about, and it is safe here for a
  duller reason than a test: no hand-written line names the member at all.
- `SystemDict` gains `GIT_BRANCH`, a nullable string the server fills only on a `development`
  build. It is `required` in the schema, which would matter if NSwag enforced it; `nswag.json`
  sets `requiredPropertiesMustBeDefined: false` and `generateDataAnnotations: false`, so the
  property is plain and a server that omits it still deserialises.
- The `X-Upload-Total-Size` and `X-Upload-Total-Chunks` headers on `POST /api/roms/upload/start`
  drop from `minimum: 1` to `minimum: 0`, because an empty ROM file is accepted now. RomMBat
  does not upload ROMs.

`docs/romm-5.3-findings.md`, section 12.

## Why the generated file disables four doc-comment warnings

`Directory.Build.props` sets `GenerateDocumentationFile`, so Roslyn checks doc comments
across the solution, and `build.yml` builds Release with `-warnaserror`. That rule exists for
the hand-written code, where the measured rules live in the comments and a `<see cref="..."/>`
is how one is linked to the type it constrains. The generated file has no crefs at all: its
70 doc comments are the schema's `description` strings, which `normalize.py` carries through
verbatim.

Verbatim is the problem. NSwag's header disables CS1573 and CS1591 but not CS1570 or CS1572,
so a RomM release whose description text contains a raw `<` or `&` would fail the build in a
file nobody authored, at pin-move time. `generate.sh` appends the two missing pragmas after
running NSwag, so a regeneration keeps them, and then greps for each one: it used to append
both as a single line, which put `1572` inside `1570`'s `//` comment and disabled only one of
them. If a regenerated file still will not compile on a
doc-comment warning, add the warning there rather than turning the check off for the project.

## Why the schema is normalised first

`normalize.py` writes a derived copy that the generator consumes; the pinned file is never
edited. RomM serves OpenAPI **3.1**, where an optional string is
`anyOf: [{type: string}, {type: "null"}]`. NSwag does not read that as nullability and
emits an empty placeholder class per occurrence, so `DeviceAuthTokenResponse.expires_at`
generates as a class named `Expires_at4` rather than `string?`. Collapsing the idiom to the
3.0 `nullable: true` form takes the output from 812 classes to 208, which is the schema
count plus enums rather than the schema count plus noise.

## Why NSwag, DTOs only

NSwag generates plain POCOs with `System.Text.Json` attributes and no runtime package of
its own. Kiota would generate a fluent request-builder API over
`Microsoft.Kiota.Abstractions`, which owns the `HttpClient` and would fight the one thing
`RomM.Client` cannot delegate: an explicitly set `SocketsHttpHandler.ConnectTimeout` on
every request (`docs/retrobat-findings.md`, probe 6b).

So `generateClientClasses` is off. The schema supplies the wire shapes; every call is
hand-written over a client-owned handler. That is also what
[`docs/ARCHITECTURE.md`](../../../docs/ARCHITECTURE.md) section 2 asks for.
