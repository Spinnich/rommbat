# The pinned RomM schema

`romm-5.2.0.json` is a byte-exact copy of `GET /openapi.json` (served at the root, not
under `/api`) from a RomM instance reporting `SYSTEM.VERSION = 5.2.0`. The generated DTOs
in [`../Generated/RomMApiSchema.g.cs`](../Generated/RomMApiSchema.g.cs) come from it and
are committed, so an upstream deploy cannot change the contract mid-session.

|                |                                                                    |
| -------------- | ------------------------------------------------------------------ |
| RomM version   | 5.2.0, the minimum RomMBat supports                                |
| Source         | a self-hosted 5.2.0 instance, host redacted                        |
| Pulled         | 2026-08-25                                                         |
| `info.version` | 5.2.0                                                              |
| sha256         | `d7e54d0f73d0dc65d88c13061606edf48c2592fc9b73d804c1438859dcf80d1d` |
| Paths          | 171                                                                |
| Schemas        | 211                                                                |

**The pin is always the minimum version RomMBat declares support for**, so the generated DTOs
describe the oldest server the client claims to work with. Since RomMBat tracks the newest
stable, that is also the newest release: moving the floor and moving the pin are one decision.

**Prefer the public demo at `demo.romm.app` as the source**, because anyone can reproduce the
file from it without an account, a token or a hostname that would have to be scrubbed. The
5.1.0 pin came from there. The 5.2.0 pin did not: the demo still reported 5.1.0 on 2026-08-25,
five days after 5.2.0 shipped, so the file was pulled from a self-hosted 5.2.0 instead. The
sha256 above is how that is checked rather than trusted; a `/openapi.json` from any stock
5.2.0 hashes to it. The file was searched for the source hostname before committing and
contains none. When the demo catches up to the pinned version, re-pull from it and confirm the
hash is unchanged.

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
