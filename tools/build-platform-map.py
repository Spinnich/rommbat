#!/usr/bin/env python3
"""Emit data/retrobat/platforms.json: RomM platform slug to an ordered list of folders.

Layer 3 of the resolution chain in the `platform-mapping` skill, and only layer 3. The
bundled table is a seed with the known errors taken out, not a curation pass: a folder that
the seed never mentioned stays unmentioned here, and the normalized-match layer offers it
for confirmation at runtime instead.

Three corrections are applied to the seed, all of them derived rather than typed:

  * the seed is keyed folder -> slug and this file is slug -> folders, so it is inverted
  * a seed key naming a folder `systems_names.lst` does not list is either renamed to the
    folder RetroBat does list, or dropped when RetroBat has no such system at all
  * `arcade` is marked as needing an explicit per-sync-set choice rather than resolving,
    because which of its folders is right depends on the romset a file came from

Ordering inside a slug decides which folder wins when several are present in the target's
es_systems.cfg. It is derived, so regenerating cannot silently reshuffle a user's targets:
an exact slug match first, then the shortest name, then alphabetical.

Usage: python tools/build-platform-map.py [--check]
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REFERENCE = ROOT / "reference"
OUTPUT = ROOT / "data" / "retrobat" / "platforms.json"

# Seed keys naming a folder RetroBat does not list, and what to do about each. A rename
# names the folder systems_names.lst actually carries; None drops the entry because
# RetroBat has no equivalent system, which is the ports-and-storefronts case in reverse.
#
# Every one of these is a folder-name error in the seed, not a slug error, so the RomM side
# is left exactly as the seed has it.
STALE_KEYS = {
    "astrocde": "astrocade",  # renamed upstream in RetroBat
    "bbc": "bbcmicro",  # renamed upstream in RetroBat
    "ps": None,  # duplicate: the seed already maps psx -> psx
    "segacd": None,  # duplicate: the seed already maps megacd -> segacd
    "msu-md": None,  # duplicate: the seed already maps megadrive-msu -> genesis
    "ps3-psn": None,  # duplicate: the seed already maps ps3 -> ps3
    "wiiware": None,  # duplicate: the seed already maps wii -> wii
    "namco22": None,  # RetroBat has namco2x6 and namco3xx, neither is System 22
    "commander-x16": None,  # no RetroBat system
    "macintosh": None,  # no RetroBat system
    "pico": None,  # RetroBat's pico8 is PICO-8, not the Sega Pico
    "plugnplay": None,  # no RetroBat system
    "ps5": None,  # no RetroBat system
    "pspminis": None,  # no RetroBat system
    "psvr": None,  # no RetroBat system
    "socrates": None,  # no RetroBat system
    "videopacplus": None,  # no RetroBat system
    "vis": None,  # no RetroBat system
    "export": None,  # not a mapping: scan.gamelist.export, caught by a loose seed parse
}

# Slugs that must never resolve on their own. Arcade ROM names are romset-versioned, so the
# right folder depends on where the file came from and cannot be read off the slug.
REQUIRES_EXPLICIT_CHOICE = {
    "arcade": (
        "Which arcade folder is correct depends on the romset the file came from, and "
        "arcade ROM names are romset-versioned. Choose one per sync set."
    ),
}


def read_seed(path: Path) -> dict[str, str]:
    """Parse system.platforms out of the seed YAML without a YAML dependency.

    The block is a flat list of `folder: slug` pairs, so it is scanned by indentation
    rather than parsed. Anything outside that block is ignored, which is what keeps
    scan.gamelist.export from being read as a platform.
    """
    mapping: dict[str, str] = {}
    in_system = False
    in_platforms = False

    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue

        indent = len(line) - len(line.lstrip())

        if indent == 0:
            in_system = line.startswith("system:")
            in_platforms = False
            continue

        if not in_system:
            continue

        if indent == 2:
            in_platforms = line.strip() == "platforms:"
            continue

        if in_platforms and indent == 4:
            match = re.fullmatch(r"\s{4}([A-Za-z0-9_.\-]+):\s*([A-Za-z0-9_.\-]+)\s*", line)
            if match:
                mapping[match.group(1)] = match.group(2)

    return mapping


def order_key(slug: str, folder: str) -> tuple[int, int, str]:
    """Rank folders inside one slug. Exact match, then shortest, then alphabetical."""
    return (0 if folder == slug else 1, len(folder), folder)


def build() -> dict[str, object]:
    systems = [line.strip() for line in (REFERENCE / "systems_names.lst").read_text(encoding="utf-8").splitlines()]
    systems = [s for s in systems if s]
    known = set(systems)

    seed = read_seed(REFERENCE / "config.batocera-retrobat.yml")

    stale = sorted(set(seed) - known)
    unexpected = sorted(set(stale) - set(STALE_KEYS))
    if unexpected:
        sys.exit(
            "Seed keys name folders RetroBat lacks and this script has no ruling for them: "
            + ", ".join(unexpected)
            + "\nAdd each to STALE_KEYS with a rename or an explicit drop, then rerun."
        )

    corrections: list[dict[str, str]] = []
    corrected: dict[str, str] = {}

    for folder, slug in sorted(seed.items()):
        if folder in known:
            corrected[folder] = slug
            continue

        replacement = STALE_KEYS[folder]
        if replacement is None:
            corrections.append({"seed_folder": folder, "slug": slug, "action": "dropped"})
            continue

        if replacement not in known:
            sys.exit(f"Rename target '{replacement}' for seed key '{folder}' is not a RetroBat system.")

        corrected[replacement] = slug
        corrections.append({"seed_folder": folder, "slug": slug, "action": "renamed", "folder": replacement})

    platforms: dict[str, list[str]] = {}
    for folder, slug in corrected.items():
        platforms.setdefault(slug, []).append(folder)

    for slug, folders in platforms.items():
        folders.sort(key=lambda folder: order_key(slug, folder))

    return {
        "_comment": (
            "RomM platform slug to an ordered list of RetroBat system folders. Layer 3 of the "
            "resolution chain: a user override and a platform.fs_slug match against the live "
            "es_systems.cfg both outrank this, and a normalized-match suggestion sits below it. "
            "The first folder present in the target's es_systems.cfg wins. Folder names are "
            "es_systems.cfg <path> basenames, which are not the same vocabulary as <name>. "
            "Generated by tools/build-platform-map.py; do not hand-edit."
        ),
        "_source": "rommapp/romm examples/config.batocera-retrobat.yml, corrected against RetroBat systems_names.lst",
        "_retrobat_systems": len(known),
        "_seed_pairs": len(seed),
        "_ordering": "exact slug match, then shortest folder name, then alphabetical",
        "_requires_explicit_choice": REQUIRES_EXPLICIT_CHOICE,
        "_corrections": corrections,
        "platforms": dict(sorted(platforms.items())),
    }


def main() -> int:
    document = build()
    rendered = json.dumps(document, indent=2, ensure_ascii=False) + "\n"

    if "--check" in sys.argv:
        # Compared as parsed JSON, not as bytes: Trunk's prettier owns the formatting of
        # everything under data/, and re-indenting the file is not drift. Content is.
        current = json.loads(OUTPUT.read_text(encoding="utf-8")) if OUTPUT.exists() else None
        if current != document:
            print(f"{OUTPUT.relative_to(ROOT)} is stale. Run tools/build-platform-map.py.")
            return 1
        print(f"{OUTPUT.relative_to(ROOT)} is up to date.")
        return 0

    OUTPUT.write_text(rendered, encoding="utf-8")
    platforms = document["platforms"]
    assert isinstance(platforms, dict)
    folders = sum(len(v) for v in platforms.values())
    corrections = document["_corrections"]
    assert isinstance(corrections, list)
    renamed = sum(1 for c in corrections if c["action"] == "renamed")
    print(
        f"{OUTPUT.relative_to(ROOT)}: {len(platforms)} slugs, {folders} folders, "
        f"{renamed} renamed and {len(corrections) - renamed} dropped from the seed."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
