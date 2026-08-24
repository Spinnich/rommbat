# The pinned RomM schema

`romm-5.1.0.json` is a byte-exact copy of `GET /openapi.json` (served at the root, not
under `/api`) from a RomM instance reporting `SYSTEM.VERSION = 5.1.0`. The generated DTOs
in [`../Generated/RomMApiSchema.g.cs`](../Generated/RomMApiSchema.g.cs) come from it and
are committed, so an upstream deploy cannot change the contract mid-session.

|                |                                                                    |
| -------------- | ------------------------------------------------------------------ |
| RomM version   | 5.1.0, the minimum RomMBat supports                                |
| Source         | the public demo instance at `demo.romm.app`                        |
| Pulled         | 2026-08-09                                                         |
| `info.version` | 5.1.0                                                              |
| sha256         | `4aa6916af4540c1720187e9b0a8debd13f00d91fe04f7b55ce19c850a821c5e9` |
| Paths          | 170                                                                |
| Schemas        | 211                                                                |

The public demo is the pin source deliberately. It runs the exact minimum version RomMBat
declares support for, so the generated DTOs describe the oldest server the client claims to
work with, and anyone can reproduce the file without an account, a token or a hostname that
would have to be scrubbed from the diff. Development and testing still run against the
instances in [DEVELOPER_SETUP.md](../../../DEVELOPER_SETUP.md) section 3; only the pin comes
from here.

## Regenerating

```bash
cd src/RomM.Client/openapi && ./generate.sh
```

Requires `dotnet tool restore` once per clone (NSwag is a local tool, pinned in
[`.config/dotnet-tools.json`](../../../.config/dotnet-tools.json)) and Python 3.10+.

**Only re-run this when deliberately moving the pin**, and review the diff. Moving the pin
is a compatibility decision: it changes which server version the DTOs describe, so the
README compatibility table moves with it.

## Why the generated file disables four doc-comment warnings

`Directory.Build.props` sets `GenerateDocumentationFile`, so Roslyn checks doc comments
across the solution, and `build.yml` builds Release with `-warnaserror`. That rule exists for
the hand-written code, where the measured rules live in the comments and a `<see cref="..."/>`
is how one is linked to the type it constrains. The generated file has no crefs at all: its
221 doc comments are the schema's `description` strings, which `normalize.py` carries through
verbatim.

Verbatim is the problem. NSwag's header disables CS1573 and CS1591 but not CS1570 or CS1572,
so a RomM release whose description text contains a raw `<` or `&` would fail the build in a
file nobody authored, at pin-move time. `generate.sh` appends the two missing pragmas after
running NSwag, so a regeneration keeps them. If a regenerated file still will not compile on a
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
