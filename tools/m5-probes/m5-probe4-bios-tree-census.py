"""What is already sitting in a real install's bios/, before RomMBat ever writes there.

Adoption meets a user's own files here far more often than it did for ROMs, and bios/ is
not a tree RomMBat owns: openMSX keeps its whole user-data directory under it, save states
included, and bios/mame/hash holds software-list metadata rather than firmware. This counts
what a first BIOS pass would adopt, what it would refuse to overwrite, and what it must not
touch at all.

    python m5-probe4-bios-tree-census.py <retrobat-root>

Reads only.
"""

from __future__ import annotations

import collections
import hashlib
import pathlib
import sys

from _common import record_offline, requirements

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
bios = root / "bios"
lines: list[str] = []

required = requirements()
by_path = {path.lower(): md5 for _system, md5, path in required}
required_md5 = {md5 for _s, md5, _p in required if md5}
systems_for_md5: dict[str, set[str]] = collections.defaultdict(set)
for system, md5, _path in required:
    if md5:
        systems_for_md5[md5].add(system)

files = [p for p in bios.rglob("*") if p.is_file()]
lines.append("=== the bios/ tree as found ===")
lines.append(f"  root: {root}")
lines.append(f"  files: {len(files)}   bytes: {sum(p.stat().st_size for p in files):,}")

top = collections.Counter(
    p.relative_to(bios).parts[0] if len(p.relative_to(bios).parts) > 1 else "(flat)" for p in files
)
lines.append("  by top-level entry under bios/:")
for name, count in top.most_common():
    lines.append(f"    {name:24} {count}")
lines.append("")

# --- what a first pass would do ------------------------------------------------------------
adoptable: list[tuple[str, str]] = []
mismatched: list[tuple[str, str, str]] = []
unknown = 0

for path in files:
    relative = path.relative_to(root).as_posix()
    wanted = by_path.get(relative.lower())
    digest = hashlib.md5(path.read_bytes()).hexdigest()

    if wanted:
        if digest == wanted:
            adoptable.append((relative, digest))
        else:
            mismatched.append((relative, wanted, digest))
    elif digest in required_md5:
        # The right bytes under a name RetroBat does not ask for here. Not a destination, so
        # not adoptable at that path, but worth seeing.
        adoptable.append((relative + "  (right bytes, not a required path)", digest))
    else:
        unknown += 1

lines.append("=== what a first BIOS pass would find ===")
lines.append(f"  at a required path with the required md5 (adopted, never re-downloaded): {len([a for a in adoptable if 'not a required path' not in a[0]])}")
for relative, digest in adoptable:
    lines.append(f"    {relative:60} {digest}")
lines.append(f"  at a required path with a different md5 (warned about, left alone): {len(mismatched)}")
for relative, wanted, digest in mismatched:
    lines.append(f"    {relative:60} wanted {wanted}  found {digest}")
lines.append(f"  files RomMBat has no opinion about at all: {unknown}")
lines.append("")

# --- what must never be touched --------------------------------------------------------------
lines.append("=== files in bios/ that are not firmware ===")
for label, pattern in [
    ("openMSX user data", "openmsx/**/*"),
    ("openMSX save states", "openmsx/savestates/*"),
    ("MAME software lists", "mame/hash/*"),
    ("MAME samples", "mame/samples/*"),
]:
    hits = [p for p in bios.glob(pattern) if p.is_file()]
    lines.append(f"  {label:22} {len(hits)}")
    for hit in hits[:6]:
        lines.append(f"    {hit.relative_to(root).as_posix()}")
    if len(hits) > 6:
        lines.append(f"    ... and {len(hits) - 6} more")

manifest_claims = [p for _s, _m, p in required if p.startswith("bios/openmsx") or p.startswith("bios/openMSX")]
lines.append("")
lines.append(f"  manifest entries that write inside bios/openMSX/: {len(manifest_claims)}")
lines.append(f"  manifest entries under bios/mame/:                {len([p for _s, _m, p in required if p.startswith('bios/mame/')])}")

record_offline("m5-probe4-bios-tree-census", lines)
