"""What the saves/ tree really looks like, against the two bundled files that describe it.

M6 discovers saves from data/retrobat/save_directories.json and save_shapes.json, neither
of which anything has read yet. This checks both against a real install: which top-level
directories are not systems, which second-level directories are not emulators, what sits
loose under a system folder, and how much of what is on disk the bundled shapes classify.

    python m6-probe2-save-tree.py <retrobat-root>

Reads only.
"""

from __future__ import annotations

import json
import pathlib
import sys

from _common import REPO, declared_systems, record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
saves = root / "saves"
lines: list[str] = []

shapes = json.loads((REPO / "data" / "retrobat" / "save_shapes.json").read_text(encoding="utf-8"))
directories = json.loads(
    (REPO / "data" / "retrobat" / "save_directories.json").read_text(encoding="utf-8")
)
systems = declared_systems(root)

lines.append("=== the bundled files")
lines.append(f"  save_shapes.json      classified {len(shapes['shapes'])}, unclassified {len(shapes['_unclassified'])}")
lines.append(f"  save_directories.json entries {len(directories['directories'])}")
lines.append(f"  es_systems.cfg        declares {len(systems)} systems")

top = sorted(p for p in saves.iterdir() if p.is_dir())
loose_at_root = sorted(p.name for p in saves.iterdir() if p.is_file())
not_a_system = [p.name for p in top if p.name not in systems]

lines.append("")
lines.append("=== level one: saves/<x>/")
lines.append(f"  directories                {len(top)}")
lines.append(f"  loose files at saves/ root {loose_at_root}")
lines.append(f"  NOT a declared system      {not_a_system}")

lines.append("")
lines.append("=== level two: saves/<system>/<x>/, and what sits loose beside it")
with_content = []
for path in top:
    subdirectories = sorted(p.name for p in path.iterdir() if p.is_dir())
    files = sorted(p for p in path.iterdir() if p.is_file())
    if not subdirectories and not files:
        continue
    with_content.append(path.name)
    tag = "system" if path.name in systems else "NOT-A-SYSTEM"
    lines.append(f"  {path.name:<16} [{tag:<12}] loose={len(files):<3} subdirs={subdirectories}")

lines.append("")
lines.append("=== every loose file under a system folder, which is where class A lives")
for path in top:
    files = sorted(p for p in path.iterdir() if p.is_file())
    if not files:
        continue
    lines.append(f"  {path.name}:")
    for file in files:
        lines.append(f"     {file.name[:64]:<64} {file.stat().st_size:>12,} B")

present = set(with_content)
classified = set(shapes["shapes"])
unclassified = set(shapes["_unclassified"])
lines.append("")
lines.append("=== coverage of what is actually on disk")
lines.append(f"  on disk and classified:            {len(present & classified)}")
lines.append(f"  on disk but _unclassified:         {sorted(present & unclassified)}")
lines.append(f"  on disk and absent from the file:  {sorted(present - classified - unclassified)}")
lines.append(f"  classified with nothing on disk:   {sorted(classified - present)}")

settings = (root / "emulationstation" / ".emulationstation" / "es_settings.cfg").read_text(
    encoding="utf-8", errors="replace"
)
lines.append("")
lines.append("=== options that change where saves go, as this install has them")
for key in (
    "dolphin_sync_saves",
    "duckstation_memcardtype",
    "pcsx2_slot1_memory",
    "flycast_vmupergame",
    "dolphin_slotA",
):
    hits = [line.strip() for line in settings.splitlines() if key in line]
    lines.append(f"  {key:<24} {hits if hits else 'absent (stock)'}")

record_offline("m6-probe2-save-tree", lines)
