"""Where the BIOS requirements manifest actually lives in a real RetroBat install.

docs/PLAN.md M5 says batocera-systems.json is "in emulatorlauncher, and present in the
tree". This checks that against a real 8.2 install: is there a readable file, and if not,
where is the data, and does its content match the vendored copy the plan's numbers come
from.

    python m5-probe1-manifest-in-install.py <retrobat-root>

Reads only. Writes its transcript and whatever it extracted to probe-output/m5/.
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import sys

from _common import REPO, freegosy, record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
lines: list[str] = []

# --- 1. is there a file? -----------------------------------------------------------------
lines.append("=== a readable batocera-systems.json anywhere in the tree ===")
found = [p for p in root.rglob("batocera-systems.json")]
lines.append(f"  matches: {len(found)}")
for p in found:
    lines.append(f"    {p.relative_to(root).as_posix()}")
if not found:
    lines.append("  NONE. The plan's 'present in the tree' does not mean a file a client can read.")
lines.append("")

# --- 2. where the data is instead ---------------------------------------------------------
exe = root / "emulationstation" / "batocera-systems.exe"
lines.append("=== batocera-systems.exe ===")
lines.append(f"  present: {exe.exists()}")
if not exe.exists():
    record_offline("m5-probe1-manifest-in-install", lines)
    raise SystemExit(1)

blob = exe.read_bytes()
lines.append(f"  size: {len(blob)} bytes")

start = blob.find(b'{\n  "3do"')
lines.append(f"  embedded JSON found at offset: {start}")
if start < 0:
    record_offline("m5-probe1-manifest-in-install", lines)
    raise SystemExit(1)

depth = 0
end = None
in_string = False
escaped = False
for index in range(start, len(blob)):
    char = blob[index]
    if in_string:
        if escaped:
            escaped = False
        elif char == 0x5C:
            escaped = True
        elif char == 0x22:
            in_string = False
        continue
    if char == 0x22:
        in_string = True
    elif char == 0x7B:
        depth += 1
    elif char == 0x7D:
        depth -= 1
        if depth == 0:
            end = index + 1
            break

embedded = blob[start:end]
lines.append(f"  embedded JSON span: [{start}, {end}), {len(embedded)} bytes")

# The .NET resource header sits just in front of the string, and names it in UTF-16LE.
window = blob[max(0, start - 120) : start]
names = []
for offset in (0, 1):
    text = window[offset:].decode("utf-16-le", "replace")
    names.append("".join(c if c.isascii() and c.isprintable() else " " for c in text).strip())
lines.append(f"  resource name nearby: {max(names, key=len)!r}")
lines.append("")

# --- 3. does it agree with the vendored copy ----------------------------------------------
installed = json.loads(embedded.decode("utf-8"))
vendored_bytes = (REPO / "reference" / "batocera-systems.json").read_bytes()
vendored = json.loads(vendored_bytes.decode("utf-8"))

lines.append("=== installed against reference/batocera-systems.json ===")
lines.append(f"  installed  systems {len(installed):3}  entries {sum(len(v.get('biosFiles', [])) for v in installed.values())}")
lines.append(f"  vendored   systems {len(vendored):3}  entries {sum(len(v.get('biosFiles', [])) for v in vendored.values())}")
lines.append(f"  semantically equal: {installed == vendored}")
lines.append(f"  byte identical:     {embedded == vendored_bytes}")
newline_only = embedded + b"\x0a" == vendored_bytes
lines.append(f"  identical but for a trailing newline: {newline_only}")
lines.append(f"  md5 installed: {hashlib.md5(embedded).hexdigest()}")
lines.append(f"  md5 vendored:  {hashlib.md5(vendored_bytes).hexdigest()}")
lines.append(f"  systems only installed: {sorted(set(installed) - set(vendored))}")
lines.append(f"  systems only vendored:  {sorted(set(vendored) - set(installed))}")
lines.append(f"  systems differing:      {sorted(k for k in set(installed) & set(vendored) if installed[k] != vendored[k])}")

freegosy.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
extracted = freegosy.OUTPUT_DIR / "installed-batocera-systems.json"
extracted.write_bytes(embedded)
lines.append(f"  extracted to probe-output/m5/{extracted.name}")

record_offline("m5-probe1-manifest-in-install", lines)
