#!/usr/bin/env python3
"""Emit data/retrobat/platforms.json: RomM platform slug to an ordered list of folders.

Layer 3 of the resolution chain in the `platform-mapping` skill, and only layer 3. The
bundled table is what RomM's own resolver would make of a RetroBat folder name, not a
curation pass: a folder neither upstream rule reaches stays unmapped here, and the
normalized-match layer offers it for confirmation at runtime instead.

The direction of the walk is the design decision. It iterates RetroBat's own system list
and asks upstream what slug each folder resolves to, rather than importing upstream's table
and correcting it. Core principle 3 says RetroBat is the authority on what folders exist,
and PLATFORM_FS_ALIASES is a Batocera / RetroBat / ES-DE union whose keys include 44 names
no RetroBat install has. Walking RetroBat's side never sees them.

Two upstream rules, applied in upstream's order (backend/utils/platform_aliases.py,
`resolve_platform_slug`), minus the config-binding layer, which is a server operator's
choice and not a fact about either project:

  * identity, when the folder name is itself a UniversalPlatformSlug value
  * the PLATFORM_FS_ALIASES table, for the names that differ

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

# Slugs that must never resolve on their own. Arcade ROM names are romset-versioned, so the
# right folder depends on where the file came from and cannot be read off the slug.
REQUIRES_EXPLICIT_CHOICE = {
    "arcade": (
        "Which arcade folder is correct depends on the romset the file came from, and "
        "arcade ROM names are romset-versioned. Choose one per sync set."
    ),
}

# The four bindings rommapp/romm's own examples/config.batocera-retrobat.yml suggests for a
# Batocera or RetroBat install, recorded rather than applied. They are the folders upstream
# thinks a server operator should bind away from the default resolution below.
#
# Not applied, because they are a statement about one server's scan and this table is
# consulted only when the server's own answer has already failed to match. A library scanned
# with that config reports platform.fs_slug as the folder name, which layer 2 matches
# against the live es_systems.cfg and which outranks this file.
UPSTREAM_SUGGESTED_BINDINGS = {
    "atari800": "atari8bit",
    "model2": "arcade",
    "model3": "arcade",
    "pico": "sega-pico",
}


def read_slugs(path: Path) -> dict[str, str]:
    """Parse UniversalPlatformSlug into member name -> slug value.

    Read as text rather than imported: the module imports RomM's config manager under
    TYPE_CHECKING and is not runnable outside that tree. The names are what the alias table
    is written in, which is why the values alone are not enough.
    """
    members = dict(re.findall(r'^\s{4}([A-Z0-9_]+)\s*=\s*"([^"]+)"', path.read_text(encoding="utf-8"), re.M))
    if not members:
        sys.exit(f"No UniversalPlatformSlug members found in {path.name}. Re-run reference/refresh.sh.")
    return members


def read_aliases(path: Path, members: dict[str, str]) -> dict[str, str]:
    """Parse PLATFORM_FS_ALIASES into folder name -> slug value."""
    block = re.search(r"PLATFORM_FS_ALIASES.*?=\s*\{(.*?)\n\}", path.read_text(encoding="utf-8"), re.S)
    if block is None:
        sys.exit(f"No PLATFORM_FS_ALIASES table found in {path.name}. Re-run reference/refresh.sh.")

    pairs = re.findall(r'^\s{4}"([^"]+)":\s*UPS\.([A-Z0-9_]+),', block.group(1), re.M)
    if not pairs:
        sys.exit(f"PLATFORM_FS_ALIASES in {path.name} parsed to nothing. Its shape has changed.")

    unknown = sorted({name for _folder, name in pairs if name not in members})
    if unknown:
        sys.exit(
            "PLATFORM_FS_ALIASES names slugs the enum does not carry: "
            + ", ".join(unknown)
            + "\nThe two vendored files are out of step. Re-run reference/refresh.sh."
        )

    return {folder: members[name] for folder, name in pairs}


def order_key(slug: str, folder: str) -> tuple[int, int, str]:
    """Rank folders inside one slug. Exact match, then shortest, then alphabetical."""
    return (0 if folder == slug else 1, len(folder), folder)


def build() -> dict[str, object]:
    systems = [line.strip() for line in (REFERENCE / "systems_names.lst").read_text(encoding="utf-8").splitlines()]
    systems = [s for s in systems if s]
    known = set(systems)

    members = read_slugs(REFERENCE / "romm-platform_slugs.py")
    aliases = read_aliases(REFERENCE / "romm-platform_aliases.py", members)
    values = set(members.values())

    resolved: dict[str, str] = {}
    by_identity = 0
    by_alias = 0

    for folder in sorted(known):
        if folder in values:
            resolved[folder] = folder
            by_identity += 1
        elif folder in aliases:
            resolved[folder] = aliases[folder]
            by_alias += 1

    # Every value here came out of the enum or out of a table keyed on it, so this can only
    # fail if one of the two parsers above has started reading the wrong thing.
    invalid = sorted({slug for slug in resolved.values() if slug not in values})
    if invalid:
        sys.exit("Resolved to slugs RomM does not know: " + ", ".join(invalid))

    platforms: dict[str, list[str]] = {}
    for folder, slug in resolved.items():
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
        "_source": (
            "RetroBat systems_names.lst, each folder resolved by rommapp/romm "
            "backend/utils/platform_aliases.py (identity, then PLATFORM_FS_ALIASES)"
        ),
        "_retrobat_systems": len(known),
        "_resolved_folders": len(resolved),
        "_resolved_by_identity": by_identity,
        "_resolved_by_alias": by_alias,
        "_unresolved_folders": len(known) - len(resolved),
        "_ordering": "exact slug match, then shortest folder name, then alphabetical",
        "_requires_explicit_choice": REQUIRES_EXPLICIT_CHOICE,
        "_upstream_suggested_bindings": UPSTREAM_SUGGESTED_BINDINGS,
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
    print(
        f"{OUTPUT.relative_to(ROOT)}: {len(platforms)} slugs, {folders} folders, "
        f"{document['_resolved_by_identity']} by identity and {document['_resolved_by_alias']} by alias, "
        f"{document['_unresolved_folders']} RetroBat folders unresolved."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
