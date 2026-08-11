"""F18c: how DuckStation's own database names a save, across discs, regions and revisions.

The memory card filename under any per-game mode comes from this database, so it decides
both whether a multi-disc set shares a card and whether a user holding several regional
releases of one game can have their saves told apart.

Two fields matter and they disagree. `name` collapses regional variants onto one string,
while `saveName` is region-qualified and very nearly unique across the database. A driven card
matched `saveName` with the disc marker removed, which is why the last section here measures
how far that stripping rule generalises: the two sets that were driven had nothing after the
disc marker, and 130 stems in the database do.

Reads the database DuckStation ships. Nothing is launched and nothing is written.

  python f18c-duckstation-gamedb.py <path to gamedb.yaml> <serial> [<serial> ...]

Find it at emulators/duckstation/resources/gamedb.yaml in a RetroBat tree.
"""


import io
import re
import sys

path = sys.argv[1]
wanted = set(sys.argv[2:])

entries = {}
serial = None
fields = {}
with io.open(path, encoding="utf-8", errors="replace") as handle:
    for line in handle:
        top = re.match(r"^([A-Za-z0-9][^:]*):\s*$", line)
        if top:
            if serial and fields:
                entries[serial] = fields
            serial = top.group(1).strip("\"'")
            fields = {}
            continue
        sub = re.match(r"^\s+([a-zA-Z_]+):\s*(.*)$", line)
        if sub and serial:
            key, value = sub.group(1), sub.group(2).strip().strip("\"'")
            if key not in fields:
                fields[key] = value
if serial and fields:
    entries[serial] = fields

print("serial          name                                          saveName")
print("-" * 100)
for s in sys.argv[2:]:
    f = entries.get(s)
    if not f:
        print(f"{s:15} (not in database)")
        continue
    print(f"{s:15} {f.get('name', ''):45} {f.get('saveName', '(none)')}")

print()
# Across the whole database: how often do several serials share one saveName, and how often
# does a multi-disc pair share one?
by_save = {}
for s, f in entries.items():
    key = f.get("saveName")
    if key:
        by_save.setdefault(key, []).append(s)
shared = {k: v for k, v in by_save.items() if len(v) > 1}
print(f"entries carrying a saveName: {sum(1 for f in entries.values() if f.get('saveName'))}")
print(f"distinct saveName values:    {len(by_save)}")
print(f"saveName values shared by more than one serial: {len(shared)}")
biggest = sorted(shared.items(), key=lambda kv: -len(kv[1]))[:6]
for k, v in biggest:
    print(f"   {k!r} shared by {len(v)} serials: {sorted(v)[:6]}")

print()
# The question that decides F18: do the discs of one set share a saveName?
for label, pair in [
    ("MGS USA", ("SLUS-00594", "SLUS-00776")),
    ("FF7 USA", ("SCUS-94163", "SCUS-94164")),
]:
    a, b = (entries.get(pair[0], {}), entries.get(pair[1], {}))
    print(f"{label}: disc1 saveName={a.get('saveName')!r}  disc2 saveName={b.get('saveName')!r}")
    print(f"         same saveName: {a.get('saveName') == b.get('saveName') and a.get('saveName') is not None}")
    print(f"         disc1 name={a.get('name')!r}  disc2 name={b.get('name')!r}")

print()
# How far the measured rule generalises. Both driven sets read "Title (Region) (Disc N)" with
# nothing after the marker, so removing it merged their discs onto one stem. The marker is not
# always last: "Biohazard 2 (Japan) (Disc 1) (Leon-hen)" keeps a subtitle behind it, and those
# discs do not merge. Whether DuckStation still treats them as one set is unmeasured; this only
# counts how large that population is.
DISC = re.compile(r"\((?:Disc|Disk|CD)\s*(\d+)\)", re.I)
sets = {}
marked = 0
for s, f in entries.items():
    save = f.get("saveName")
    if not save:
        continue
    found = DISC.search(save)
    if not found:
        continue
    marked += 1
    stem = re.sub(r"\s*\((?:Disc|Disk|CD)\s*\d+\)", "", save).strip()
    sets.setdefault(stem, set()).add((int(found.group(1)), s))

split = {k: v for k, v in sets.items() if 1 not in {n for n, _ in v}}
gaps = [k for k, v in sets.items()
        if sorted(n for n, _ in v)[:1] == [1]
        and sorted(n for n, _ in v) != list(range(1, len(v) + 1))]
print(f"saveName values carrying a disc marker: {marked}")
print(f"  collapsing to distinct card stems:    {len(sets)}")
print(f"  stems whose lowest disc is not 1:     {len(split)}  (the marker was not last)")
print(f"  stems starting at 1 with a gap:       {len(gaps)}")
for k, v in sorted(split.items())[:8]:
    print(f"     {k!r}: discs {sorted(n for n, _ in v)}")
