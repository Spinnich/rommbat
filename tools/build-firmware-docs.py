#!/usr/bin/env python3
"""Write docs/platforms/firmware.md, the firmware matrix, from data/retrobat/bios.json.

Per RetroBat system: each firmware slot, whether it is required, the rows that read it, the
files accepted there with md5 and region, and the source that identified each, with the
emulator source lines a retrobios profile cites. Then what is read but never fetched, and why:
files no source gives a hash for, paths outside bios/, firmware an emulator installs itself.

The page is the manifest made readable, so it is generated, and an edit to it is drift:
--check fails until the sources are edited and this is re-run. Trunk leaves the page alone
(.trunk/trunk.yaml) because this script owns its formatting.

Usage: python tools/build-firmware-docs.py [--check]
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import firmware_sources  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
# A scout points this at a scratch refresh, as tools/build-bios-manifest.py takes it.
REFERENCE = Path(os.environ.get("ROMMBAT_REFERENCE_DIR") or ROOT / "reference")
MANIFEST = ROOT / "data" / "retrobat" / "bios.json"
OUTPUT = ROOT / "docs" / "platforms" / "firmware.md"

ORIGINS = {
    "retrobat": "RetroBat's list",
    "bizhawk": "BizHawk's FirmwareDatabase.cs",
    "catalog": "a dump catalog's name",
    "romm": "RomM's known_bios_files",
    "named": "another source's hash for this name",
}


def slug(heading: str) -> str:
    """GitHub's anchor for a heading: msx2+ is #msx2."""
    return re.sub(r"[^\w\- ]", "", heading.lower()).replace(" ", "-")


def cell(text: str) -> str:
    return text.replace("|", "\\|")


def code(text: str) -> str:
    return f"`{text}`"


def origin(tag: str) -> str:
    if tag.startswith("profile:"):
        return f"retrobios `{tag.split(':', 1)[1]}`"
    return ORIGINS.get(tag, tag)


def reader(name: str) -> str:
    return "RetroBat's list" if name == "retrobat" else ("every emulator" if name == "*" else code(name))


def accepted_cell(slot: dict) -> str:
    lines = []
    for entry in slot["accepted"]:
        region = ", ".join(entry.get("region", [])) or "no region"
        sources = ", ".join(origin(tag) for tag in entry["from"])
        priority = f", priority {entry['priority']}" if "priority" in entry else ""
        lines.append(f"{code(entry['md5'])} {region}{priority}; {sources}")
    for group in slot.get("contents", []):
        members = ", ".join(
            f"{code(item['name'])} crc32 {code(item['crc32'])}" + (f" {item['size']} B" if "size" in item else "")
            for item in group["files"]
        )
        lines.append(f"zip, for {', '.join(reader(r) for r in group['readers'])}: {members}")
    return "<br>".join(lines) or "no identity: RetroBat names it without a hash and no source gives one"


def reader_profiles(evidence: firmware_sources.Evidence) -> dict[str, set[str]]:
    """Each reader, as bios.json names it, to the retrobios profiles that describe its row."""
    found: dict[str, set[str]] = {}
    for row, sources in evidence.rows.items():
        found.setdefault(row.key if row.core else row.emulator, set()).update(sources.get("profiles", []))
    return found


def evidence_cell(slot: dict, profiles: dict[str, dict], by_reader: dict[str, set[str]]) -> str:
    lines = []
    names = {tag.split(":", 1)[1] for entry in slot["accepted"] for tag in entry["from"] if tag.startswith("profile:")}
    names |= {name for r in slot["readers"] for name in by_reader.get(r, ())}
    for name in sorted(names):
        refs = firmware_sources.source_refs(profiles.get(name, {}), slot["path"])
        if refs:
            lines.append(f"`{name}`: " + "; ".join(code(ref) for ref in refs))
    if slot.get("supplement"):
        lines.append(f"Measured on our tree: {slot['supplement']}")
    return "<br>".join(lines)


def slot_cell(slot: dict) -> str:
    text = code(slot["path"])
    if slot.get("aliases"):
        text += "<br>also " + ", ".join(code(alias) for alias in slot["aliases"])
    return text


def reported_rows(reported: list[dict]) -> list[tuple[str, str, str]]:
    """One row per path, its reasons and readers merged: a file six emulators read from their
    own directories is one file the user may have to place, not six."""
    merged: dict[str, dict] = {}
    for entry in reported:
        item = merged.setdefault(entry["path"].lower(), {"path": entry["path"], "why": [], "readers": set()})
        if entry["why"] not in item["why"]:
            item["why"].append(entry["why"])
        item["readers"] |= set(entry["readers"])
    return [
        (code(item["path"]), ", ".join(reader(r) for r in sorted(item["readers"])), "; ".join(item["why"]))
        for item in merged.values()
    ]


def table(header: tuple[str, ...], rows: list[tuple[str, ...]]) -> list[str]:
    lines = ["| " + " | ".join(header) + " |", "| " + " | ".join("---" for _ in header) + " |"]
    lines += ["| " + " | ".join(cell(value) for value in row) + " |" for row in rows]
    return lines


def render() -> str:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    evidence = firmware_sources.Evidence(REFERENCE)
    profiles, by_reader = evidence.profiles, reader_profiles(evidence)
    systems = manifest["systems"]

    out = [
        "---",
        "summary: Every firmware slot each RetroBat system's emulators read, the files accepted there by md5, and the source for each, generated from the BIOS manifest.",
        "read-when: Asking which firmware a system or row reads, what to upload to RomM for it, or why a file is reported rather than fetched.",
        "---",
        "",
        "# Firmware matrix",
        "",
        "<!-- Generated from data/retrobat/bios.json and reference/retrobios/ by tools/build-firmware-docs.py.",
        "     Edit the sources and regenerate; tools/build-firmware-docs.py --check fails on an edit here. -->",
        "",
        "Every firmware file a RetroBat system's emulators read, from the bundled manifest. How the",
        "manifest is built, and why, is [firmware-from-emulator-evidence](../design/decisions/firmware-from-emulator-evidence.md).",
        "`python3 tools/firmware.py` answers the same questions per row, and compares a system against a RomM server.",
        "",
        "- **Slot** is where the file lands, relative to the RetroBat root. Several folders naming one",
        "  path share one file, so they accept the same files.",
        "- **Required** is yes where RetroBat's list names the slot or a reader's source marks it",
        "  required. A missing file is reported and never blocks a sync: the emulator decides what it reads.",
        "- **Readers** are the `emulator/core` rows that read the slot. RetroBat's list stands for the",
        "  system's default emulator.",
        "- **Accepted** is each file RomMBat will fetch for the slot, best ranked first: md5, region,",
        "  and the sources that identify it. A zip is accepted by the crc32 of its members.",
        "- **Evidence** is the emulator source a retrobios profile cites, as `file:line`, and any",
        "  boot measured on our tree.",
        "",
        f"{manifest['_systems']} systems, {manifest['_slots']} slots, {manifest['_distinct_md5s']} distinct md5s, "
        f"{manifest['_slots_without_identity']} slots with no identity. Sources: "
        + "; ".join(manifest["_source"])
        + ".",
        "",
        "Systems: " + ", ".join(f"[{name}](#{slug(name)})" for name in systems) + ".",
    ]

    for name, body in systems.items():
        out += ["", f"## {name}", "", f"{body['name']}: {len(body['slots'])} slots, {len(body['reported'])} files reported."]
        if body["slots"]:
            rows = [
                (
                    slot_cell(slot),
                    "yes" if slot["required"] else "no",
                    ", ".join(reader(r) for r in slot["readers"]),
                    accepted_cell(slot),
                    evidence_cell(slot, profiles, by_reader),
                )
                for slot in body["slots"]
            ]
            out += [""] + table(("Slot", "Required", "Readers", "Accepted", "Evidence"), rows)
        if body["reported"]:
            out += ["", "Read, never fetched:", ""] + table(("File", "Readers", "Why"), reported_rows(body["reported"]))

    return "\n".join(out) + "\n"


def main() -> int:
    rendered = render()
    if "--check" in sys.argv:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else None
        if current != rendered:
            print(f"{OUTPUT.relative_to(ROOT).as_posix()} is stale. Run tools/build-firmware-docs.py.")
            return 1
        print(f"{OUTPUT.relative_to(ROOT).as_posix()} is up to date.")
        return 0
    if os.environ.get("ROMMBAT_REFERENCE_DIR"):
        sys.exit("ROMMBAT_REFERENCE_DIR is set: only --check runs against a scratch refresh.")
    OUTPUT.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"{OUTPUT.relative_to(ROOT).as_posix()}: {rendered.count(chr(10))} lines.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
