"""F21: what does an md5-only firmware join actually answer against a real library?

Two joins, both against the 157 distinct md5s RetroBat requires in
reference/batocera-systems.json:

1. Freegosy's 34 hand-curated md5s, sourced from libretro's BIOS docs. The question is
   whether any of them is an alternative dump of a file RetroBat requires, which an
   md5-only join would miss.
2. The md5s a real RomM library actually holds, read from the inlined firmware[] on
   GET /api/platforms (see f5). This is the number that decides what M5 can deliver, and
   it also puts a live measurement under the plan's "do not trust is_verified" rule.

The RetroBat side is read from the vendored reference file, never typed by hand.
"""

from __future__ import annotations

import json
import pathlib
import re

from _common import record, request

REPO = pathlib.Path(__file__).resolve().parents[2]
FREEGOSY = pathlib.Path(
    __file__
).resolve().parent / "freegosy-bios-md5.txt"

lines: list[str] = []

bios = json.loads((REPO / "reference" / "batocera-systems.json").read_text(encoding="utf-8"))
required = {b["md5"].lower() for v in bios.values() for b in v.get("biosFiles", [])}
required_by_md5: dict[str, list[str]] = {}
for system, value in bios.items():
    for entry in value.get("biosFiles", []):
        required_by_md5.setdefault(entry["md5"].lower(), []).append(
            f"{system}:{entry['file']}"
        )

lines.append(f"RetroBat requires {len(required)} distinct md5s (reference/batocera-systems.json)")
lines.append("")

# --- Join 1: Freegosy's curated list -------------------------------------------------
if FREEGOSY.exists():
    curated = {
        m.lower()
        for m in re.findall(r"[0-9a-fA-F]{32}", FREEGOSY.read_text(encoding="utf-8"))
    }
    lines.append(f"Freegosy carries {len(curated)} distinct md5s")
    lines.append(f"  also required by RetroBat:     {len(curated & required)}")
    lines.append(f"  not required by RetroBat:      {len(curated - required)}")
    for md5 in sorted(curated & required):
        lines.append(f"    hit {md5}  {required_by_md5[md5][0]}")
else:
    lines.append(f"(skipped: {FREEGOSY.name} not present)")
lines.append("")

# --- Join 2: what the live library actually holds -------------------------------------
_status, _headers, payload, elapsed = request("GET", "/api/platforms")
platforms = json.loads(payload)
records = [f for p in platforms for f in (p.get("firmware") or [])]
library = {f["md5_hash"].lower(): f for f in records if f.get("md5_hash")}

lines.append(f"GET /api/platforms   {len(payload)} bytes   {elapsed:.2f} s")
lines.append(f"  firmware records inlined: {len(records)}   distinct md5s: {len(library)}")
held = set(library) & required
lines.append(f"  RetroBat-required md5s this library holds: {len(held)} of {len(required)}")
lines.append(f"  RetroBat-required md5s it does not hold:   {len(required - set(library))}")
lines.append("")

# The plan says to ignore is_verified. This is the live test of that rule: of the files
# RetroBat requires and this library holds, how many would is_verified have thrown away?
verified_hits = [md5 for md5 in held if library[md5].get("is_verified")]
lines.append("Filtering the same set by is_verified, which the plan says never to do:")
lines.append(f"  required-and-held, flagged is_verified:     {len(verified_hits)}")
lines.append(f"  required-and-held, NOT flagged is_verified: {len(held) - len(verified_hits)}")
lines.append("")

# Filename overlap, the other join the plan rules out.
name_matches = 0
name_wrong = 0
for md5 in sorted(held):
    served = library[md5]["file_name"].lower()
    wanted = {p.rsplit("/", 1)[-1].lower() for p in required_by_md5[md5]}
    if served in wanted:
        name_matches += 1
    else:
        name_wrong += 1
        if name_wrong <= 8:
            lines.append(
                f"  filename differs: served {served!r} vs required {sorted(wanted)!r}"
            )
lines.append("")
lines.append("Joining the same set on filename instead of md5:")
lines.append(f"  served name matches a required name: {name_matches}")
lines.append(f"  served name differs:                {name_wrong}")

record("f21-bios-md5-join", lines)
