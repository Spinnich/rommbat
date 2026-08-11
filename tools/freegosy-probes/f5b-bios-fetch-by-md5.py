"""F5 in anger: fetch one required BIOS by joining on md5 alone.

This is M5's flow done by hand, end to end, against a real library:

  1. read the required md5 and its destination path out of reference/batocera-systems.json,
     never a filename typed here
  2. find the firmware record by **md5 only**, from the firmware[] array inlined on
     GET /api/platforms (F5), ignoring both file_name and is_verified
  3. download it and verify the md5 of what actually arrived
  4. write it to the path the manifest specifies, renaming as needed

Point it at a system and a target file. It refuses to overwrite an existing file whose md5
already matches, and refuses to overwrite one that does not, which is the plan's rule: a
mismatch warns and leaves the working file alone.

  python f5b-bios-fetch-by-md5.py <retrobat-root> <system> <bios filename>
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import sys
import urllib.parse
import urllib.request

from _common import base_url, get_json, record, token

if len(sys.argv) != 4:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
system = sys.argv[2]
wanted_name = sys.argv[3]

REPO = pathlib.Path(__file__).resolve().parents[2]
lines: list[str] = []

# --- 1. the requirement, read from the vendored manifest -------------------------------
manifest = json.loads((REPO / "reference" / "batocera-systems.json").read_text(encoding="utf-8"))
entries = [
    b
    for b in manifest.get(system, {}).get("biosFiles", [])
    if b["file"].rsplit("/", 1)[-1].lower() == wanted_name.lower()
]
if not entries:
    raise SystemExit(f"batocera-systems.json lists no {wanted_name!r} for system {system!r}")
required = entries[0]
md5 = required["md5"].lower()
dest_rel = required["file"]

lines.append("=== the requirement, from reference/batocera-systems.json ===")
lines.append(f"  system   {system}")
lines.append(f"  md5      {md5}")
lines.append(f"  destination  {dest_rel}   (relative to the RetroBat root)")
lines.append("")

# --- 2. the join, on md5 alone ----------------------------------------------------------
_status, platforms, elapsed = get_json("/api/platforms")
records = [(p, f) for p in platforms for f in (p.get("firmware") or [])]
matches = [(p, f) for p, f in records if (f.get("md5_hash") or "").lower() == md5]

lines.append("=== the join, on md5 alone ===")
lines.append(f"  GET /api/platforms  {elapsed:.2f} s, {len(records)} firmware records inlined")
lines.append(f"  records whose md5_hash matches: {len(matches)}")
if not matches:
    lines.append("  NOT IN THIS LIBRARY. This is exactly what M5 step 5 reports to the user.")
    record("f5b-bios-fetch-by-md5", lines)
    raise SystemExit(1)

platform, firmware = matches[0]
served = firmware["file_name"]
lines.append(f"  found on platform fs_slug={platform['fs_slug']!r}")
lines.append(f"  served file_name : {served!r}")
lines.append(f"  wanted file name : {wanted_name!r}")
lines.append(f"  names agree      : {served.lower() == wanted_name.lower()}")
lines.append(f"  is_verified      : {firmware.get('is_verified')}  (deliberately ignored)")
lines.append("")

# --- 3. the destination, checked before anything is written ------------------------------
dest = root / dest_rel
lines.append("=== the destination ===")
lines.append(f"  {dest_rel}")
if dest.exists():
    have = hashlib.md5(dest.read_bytes()).hexdigest()
    if have == md5:
        lines.append("  already present and the md5 matches, nothing to do")
        record("f5b-bios-fetch-by-md5", lines)
        raise SystemExit(0)
    lines.append(f"  present but md5 is {have}, which is not what RetroBat requires.")
    lines.append("  Refusing to overwrite: the plan warns rather than replacing a working file.")
    record("f5b-bios-fetch-by-md5", lines)
    raise SystemExit(1)
lines.append("  not present")
lines.append("")

# --- 4. download, verify, then write ------------------------------------------------------
url = base_url() + f"/api/firmware/{firmware['id']}/content/{urllib.parse.quote(served)}"
req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token()}"})
with urllib.request.urlopen(req, timeout=180) as response:
    blob = response.read()
    status = response.status

got = hashlib.md5(blob).hexdigest()
lines.append("=== download ===")
lines.append(f"  GET /api/firmware/{firmware['id']}/content/<name> -> {status}, {len(blob)} bytes")
lines.append(f"  md5 of what arrived: {got}")
lines.append(f"  matches the requirement: {got == md5}")
if got != md5:
    lines.append("  REFUSED. Nothing written.")
    record("f5b-bios-fetch-by-md5", lines)
    raise SystemExit(1)

dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_bytes(blob)
lines.append(f"  written to {dest_rel}, renamed from {served!r}" if served.lower() != wanted_name.lower() else f"  written to {dest_rel}")

record("f5b-bios-fetch-by-md5", lines)
