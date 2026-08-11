"""F18: read a driven psx save tree and say what each file is keyed on.

Answers the questions the F18 launch probe leaves on disk once a person has actually played
far enough for the emulator to write something:

  * is the DuckStation memory card named from the rom file, from gamedb `name`, or from
    gamedb `saveName`, and is the disc marker still in it
  * do the discs of a set share one card or get one each
  * is a card that exists actually holding a save, or an empty one the emulator made anyway
  * does anything in the save tree carry an absolute path

Every path printed is relative to the RetroBat root, and `.ldci` contents are reported by
shape rather than quoted, because the path inside one names the machine it was written on.

Usage: python tools/freegosy-probes/f18d-psx-save-tree.py <retrobat-root> [system]
"""

import collections
import json
import os
import re
import sys

# A freshly formatted PS1 card is mostly one repeated pattern; a card holding a save is not.
# The gap measured between the two on a real pair was 14 distinct byte values against 124, so
# anything near the bottom of that range is empty rather than small.
BLANK_DISTINCT_BYTES = 32


def gamedb_titles(root):
    """serial -> (name, saveName), read from the copy DuckStation ships."""
    path = os.path.join(root, "emulators", "duckstation", "resources", "gamedb.yaml")
    if not os.path.exists(path):
        return {}
    entries, serial, fields = {}, None, {}
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            top = re.match(r"^([A-Za-z0-9][^:]*):\s*$", line)
            if top:
                if serial and fields:
                    entries[serial] = fields
                serial, fields = top.group(1).strip("\"'"), {}
                continue
            sub = re.match(r"^\s+(name|saveName):\s*(.*)$", line)
            if sub and serial and sub.group(1) not in fields:
                fields[sub.group(1)] = sub.group(2).strip().strip("\"'")
    if serial and fields:
        entries[serial] = fields
    return entries


def strip_disc(title):
    return re.sub(r"\s*\((?:Disc|Disk|CD)\s*\d+\)", "", title).strip()


def classify(path):
    with open(path, "rb") as handle:
        blob = handle.read()
    distinct = len(set(blob))
    return len(blob), distinct, "empty" if distinct <= BLANK_DISTINCT_BYTES else "holds a save"


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    root = os.path.abspath(sys.argv[1])
    system = sys.argv[2] if len(sys.argv) > 2 else "psx"

    saves = os.path.join(root, "saves", system)
    roms = os.path.join(root, "roms", system)
    if not os.path.isdir(saves):
        print(f"no saves/{system} on this install; nothing has been driven yet")
        return 1

    print(f"== saves/{system}")
    cards, ldci = [], []
    for base, _, names in os.walk(saves):
        for name in sorted(names):
            full = os.path.join(base, name)
            rel = os.path.relpath(full, saves).replace(os.sep, "/")
            size = os.path.getsize(full)
            note = ""
            if name.lower().endswith((".mcd", ".srm", ".mcr", ".ps2")):
                size, distinct, verdict = classify(full)
                note = f"  {distinct} distinct bytes, {verdict}"
                cards.append((rel, name))
            elif name.lower().endswith(".ldci"):
                ldci.append((rel, full))
            print(f"   {size:>9} B  {rel}{note}")

    # Which strings were available to name a card, and which one the card matched.
    stems = set()
    for base, _, names in os.walk(roms):
        for name in names:
            if name.lower().endswith((".m3u", ".chd", ".cue", ".pbp", ".iso")):
                stems.add(os.path.splitext(name)[0])

    titles = gamedb_titles(root)
    by_name = collections.defaultdict(list)
    for serial, fields in titles.items():
        for field in ("name", "saveName"):
            if fields.get(field):
                by_name[fields[field]].append((field, serial, False))
                by_name[strip_disc(fields[field])].append((field, serial, True))

    print(f"\n== what each save file's stem matches ({len(titles)} gamedb entries)")
    for rel, name in cards:
        stem = re.sub(r"_\d+$", "", os.path.splitext(name)[0])
        slot = "_<slot>" if stem != os.path.splitext(name)[0] else ""
        print(f"   {rel}")
        print(f"      stem{slot}: {stem!r}")
        print(f"      matches a rom or playlist filename : {stem in stems}")
        hits = by_name.get(stem, [])
        for field, serial, stripped in sorted(set(hits))[:4]:
            how = "with the disc marker removed" if stripped else "verbatim"
            print(f"      matches gamedb {field} {how}: {serial}")
        if not hits:
            print("      matches no gamedb title")

    if ldci:
        print("\n== disc-index sidecars, checked for absolute paths")
        for rel, full in ldci:
            try:
                with open(full, encoding="utf-8") as handle:
                    blob = json.load(handle)
            except (OSError, ValueError) as exc:
                print(f"   {rel}: unreadable ({exc})")
                continue
            recorded = blob.get("image_path", "")
            absolute = bool(re.match(r"^([A-Za-z]:[\\/]|[\\/]{2}|/)", recorded))
            inside = os.path.normcase(root) in os.path.normcase(recorded)
            print(f"   {rel}")
            print(f"      keys present  : {sorted(blob)}")
            print(f"      image_index   : {blob.get('image_index')}")
            print(f"      absolute path : {absolute}   names this install: {inside}")
            print("      (path itself withheld; it identifies the machine that wrote it)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
