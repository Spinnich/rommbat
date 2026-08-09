#!/usr/bin/env python3
"""M0 probe 2 and 4, static half: what a RetroBat install declares about itself.

Reads a live install without writing to it, and emits three things:

  probe-output/es_systems.json     per system: folder, extensions, emulator/core list
  probe-output/es_savestates.json  the save-state schema, plus anomalies in it
  probe-output/saves_observed.json what the saves/ tree actually contains today

The declared half (the two cfg files) and the observed half (the saves tree) are kept
separate on purpose: probe 2 exists to find where they disagree, so merging them here
would hide the answer.

Usage: python tools/m0-probes/probe2-static.py <retrobat-root>
"""

from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path
from xml.etree import ElementTree

# Slot placeholders RetroBat substitutes into state file templates.
SLOT_TOKENS = ("{{slot2d}}", "{{slot0}}", "{{slot}}")

# Extensions that are save-state output rather than battery saves, so the inventory can
# tell the two apart without launching anything.
STATE_HINT_DIRS = {
    "libretro",
    "pcsx2",
    "duckstation",
    "dolphin",
    "ppsspp",
    "mupen64",
    "bizhawk",
    "flycast",
    "jgenesis",
    "openmsx",
    "gopher64",
    "bigpemu",
    "DeSmuME",
    "sstates",
    "states",
    "stateslots",
}


def parse_systems(es_systems: Path) -> list[dict]:
    root = ElementTree.parse(es_systems).getroot()
    systems = []

    for node in root.findall("system"):
        name = (node.findtext("name") or "").strip()
        raw_path = (node.findtext("path") or "").strip()
        raw_ext = (node.findtext("extension") or "").strip()

        emulators = []
        for emu in node.findall("./emulators/emulator"):
            cores = [c.get("name") for c in emu.findall("./cores/core") if c.get("name")]
            emulators.append({"name": emu.get("name"), "cores": cores})

        systems.append(
            {
                "name": name,
                "path": raw_path,
                # ES accepts a space-separated list; normalise case since Windows is
                # case-insensitive but the strings in the file are not consistent.
                "extensions": sorted({e.lower() for e in raw_ext.split() if e}),
                "emulators": emulators,
                "fullname": (node.findtext("fullname") or "").strip(),
                "platform": (node.findtext("platform") or "").strip(),
            }
        )

    return systems


def parse_savestates(es_savestates: Path) -> dict:
    root = ElementTree.parse(es_savestates).getroot()
    emulators, anomalies = [], []

    for node in root.findall("emulator"):
        name = node.get("name")
        directory = (node.findtext("directory") or "").strip()
        file_tpl = (node.findtext("file") or "").strip()
        image_tpl = (node.findtext("image") or "").strip()

        entry = {
            "name": name,
            "directory": directory,
            "file": file_tpl,
            "image": image_tpl,
            "autosave_file": (node.findtext("autosave_file") or "").strip() or None,
            "autosave_image": (node.findtext("autosave_image") or "").strip() or None,
            "firstslot": node.get("firstslot"),
            "lastslot": node.get("lastslot"),
            "autosave": node.get("autosave"),
            "incremental": node.get("incremental"),
            "core_scoped": "{{core}}" in directory,
            "slot_token": next((t for t in SLOT_TOKENS if t in file_tpl), None),
            "cores": [
                {"name": c.get("name"), "enabled": c.get("enabled"), "directory": c.get("directory")}
                for c in node.findall("core")
            ],
        }
        emulators.append(entry)

        # Anomalies a parser written from the plan's description would trip over.
        if entry["firstslot"] is None or entry["lastslot"] is None:
            anomalies.append(
                {
                    "emulator": name,
                    "kind": "missing-slot-bounds",
                    "detail": "no firstslot/lastslot attribute, so slot range is undefined",
                }
            )
        if image_tpl and image_tpl == file_tpl:
            anomalies.append(
                {
                    "emulator": name,
                    "kind": "image-collides-with-file",
                    "detail": f"<image> and <file> are both {file_tpl!r}; "
                    "uploading <image> as screenshotFile would upload the state itself",
                }
            )
        if entry["firstslot"] and len(entry["firstslot"]) > 1 and entry["firstslot"].startswith("0"):
            anomalies.append(
                {
                    "emulator": name,
                    "kind": "zero-padded-slot-bound",
                    "detail": f"firstslot={entry['firstslot']!r} is a padded string, not an integer",
                }
            )
        if entry["lastslot"] and entry["slot_token"] == "{{slot2d}}" and len(entry["lastslot"]) > 2:
            anomalies.append(
                {
                    "emulator": name,
                    "kind": "slot-width-mismatch",
                    "detail": f"lastslot={entry['lastslot']} needs {len(entry['lastslot'])} digits "
                    "but the file template uses 2-digit {{slot2d}}",
                }
            )
        if not entry["slot_token"] and file_tpl:
            anomalies.append(
                {"emulator": name, "kind": "no-slot-token", "detail": f"file template {file_tpl!r} has no slot placeholder"}
            )

    return {"emulators": emulators, "anomalies": anomalies}


def inventory_saves(saves_root: Path) -> dict:
    """Walk saves/ and describe, per system folder, what is actually on disk."""
    systems = {}

    for system_dir in sorted(p for p in saves_root.iterdir() if p.is_dir()):
        files, subdirs = [], []

        for child in sorted(system_dir.iterdir()):
            rel = child.relative_to(saves_root).as_posix()
            if child.is_dir():
                # Count depth-limited contents; a deep walk of a 14 TB tree is not worth it.
                inner = list(child.rglob("*"))
                subdirs.append(
                    {
                        "path": rel,
                        "is_state_dir": child.name in STATE_HINT_DIRS,
                        "file_count": sum(1 for p in inner if p.is_file()),
                        "sample": [p.relative_to(child).as_posix() for p in inner[:5] if p.is_file()],
                    }
                )
            elif child.is_file():
                files.append({"name": child.name, "ext": child.suffix.lower(), "size": child.stat().st_size})

        if not files and not subdirs:
            continue

        # Group loose files by stem to see whether a game produces one file or several.
        by_stem = Counter(Path(f["name"]).stem for f in files)
        multi = {stem: n for stem, n in by_stem.items() if n > 1}

        systems[system_dir.name] = {
            "loose_files": files,
            "loose_extensions": sorted({f["ext"] for f in files}),
            "subdirectories": subdirs,
            "stems_with_multiple_files": multi,
            "observed_shape": classify(files, subdirs, multi),
        }

    return systems


def classify(files: list[dict], subdirs: list[dict], multi: dict) -> str:
    """First-cut A/B/C/D classification from what is on disk.

    Deliberately conservative: this is evidence from one install, not a verdict. The
    live probe pass has to confirm each of these by actually launching the emulator.
    """
    save_subdirs = [d for d in subdirs if not d["is_state_dir"] and d["file_count"]]

    if not files and save_subdirs:
        return "C (directory per game, unconfirmed)"
    if multi:
        return "B (several files per game)"
    if files:
        return "A (one file per game)"
    return "unknown (states only, no battery saves observed)"


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2

    root = Path(sys.argv[1])
    es_home = root / "emulationstation" / ".emulationstation"
    out = Path("probe-output")
    out.mkdir(exist_ok=True)

    systems = parse_systems(es_home / "es_systems.cfg")
    savestates = parse_savestates(es_home / "es_savestates.cfg")
    saves = inventory_saves(root / "saves")

    (out / "es_systems.json").write_text(json.dumps(systems, indent=2), encoding="utf-8")
    (out / "es_savestates.json").write_text(json.dumps(savestates, indent=2), encoding="utf-8")
    (out / "saves_observed.json").write_text(json.dumps(saves, indent=2), encoding="utf-8")

    ext_total = sum(len(s["extensions"]) for s in systems)
    no_ext = [s["name"] for s in systems if not s["extensions"]]

    print(f"systems declared            : {len(systems)}")
    print(f"distinct extensions declared: {ext_total}")
    print(f"systems with no <extension> : {len(no_ext)}  {no_ext[:8]}")
    print(f"state-capable emulators     : {len(savestates['emulators'])}")
    print(f"core-scoped state dirs      : {[e['name'] for e in savestates['emulators'] if e['core_scoped']]}")
    print()
    print("save-state schema anomalies:")
    for a in savestates["anomalies"]:
        print(f"  [{a['kind']}] {a['emulator']}: {a['detail']}")
    print()
    print(f"saves/ folders with content : {len(saves)}")
    for name, info in saves.items():
        print(f"  {name:14s} {info['observed_shape']:38s} ext={','.join(info['loose_extensions']) or '-'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
