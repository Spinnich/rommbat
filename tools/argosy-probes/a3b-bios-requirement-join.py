"""A3b: the join M5 actually performs, one RetroBat requirement at a time.

a3-a10-a12 asked whether any system is left with no overlap at all, and found none.
That is the weaker question. BiosPlanner resolves per required file, so a system can
overlap on one file and still report MissingFromLibrary for the other two. This runs
the real join and reports per requirement, which is what decides whether the md5-only
rule costs this library anything.

Read-only. One GET.
"""

from __future__ import annotations

import json
import pathlib
import sys

import _common

REPO = pathlib.Path(__file__).resolve().parents[2]


def main() -> int:
    lines: list[str] = []
    status, heartbeat, _ = _common.get_json("/api/heartbeat")
    lines.append(f"server version: {(heartbeat or {}).get('SYSTEM', {}).get('VERSION', 'unknown')}")

    status, platforms, _ = _common.get_json("/api/platforms")
    if status != 200:
        raise SystemExit(f"GET /api/platforms answered {status}")

    library_md5s: set[str] = set()
    library_slugs: set[str] = set()
    library_names: set[str] = set()
    for platform in platforms:
        slug = platform.get("slug") or platform.get("fs_slug") or ""
        if platform.get("firmware"):
            library_slugs.add(slug)
        for fw in platform.get("firmware") or []:
            md5 = (fw.get("md5_hash") or "").lower()
            if md5:
                library_md5s.add(md5)
            name = (fw.get("file_name") or "").lower()
            if name:
                library_names.add(name)

    manifest = json.loads((REPO / "data" / "retrobat" / "bios.json").read_text(encoding="utf-8"))
    platform_map = json.loads(
        (REPO / "data" / "retrobat" / "platforms.json").read_text(encoding="utf-8")
    )

    lines.append(f"distinct firmware md5s in the library: {len(library_md5s)}")
    lines.append(f"RomM platforms carrying firmware: {len(library_slugs)}")
    lines.append("")

    satisfied = 0
    missing: list[tuple[str, str]] = []
    unverifiable = 0
    for system, entry in sorted(manifest["systems"].items()):
        for row in entry["files"]:
            md5 = (row.get("md5") or "").lower()
            if not md5:
                unverifiable += 1
            elif md5 in library_md5s:
                satisfied += 1
            else:
                missing.append((system, row["path"]))

    total = satisfied + len(missing) + unverifiable
    lines.append("## Every RetroBat requirement, joined on md5 against the whole library")
    lines.append("")
    lines.append(f"requirements: {total}")
    lines.append(f"  md5 found in the library:      {satisfied}")
    lines.append(f"  md5 named but not in library:  {len(missing)}")
    lines.append(f"  RetroBat names no md5:         {unverifiable}")
    lines.append("")

    by_system: dict[str, list[str]] = {}
    for system, path in missing:
        by_system.setdefault(system, []).append(path)

    lines.append("## Missing, and whether the library even carries that platform")
    lines.append("")
    lines.append("A system RomM has no firmware for at all cannot be a join defect: there is")
    lines.append("nothing to have matched. A system whose firmware RomM does carry, where the")
    lines.append("named md5 still misses, is the case the md5-only rule would be costing.")
    lines.append("")
    lines.append("| system | missing | RomM carries firmware for it |")
    lines.append("| --- | --- | --- |")
    contested = 0
    # platforms.json maps RomM slug -> RetroBat folders; the join here needs the reverse.
    folder_to_slugs: dict[str, list[str]] = {}
    for slug, folders in platform_map["platforms"].items():
        for folder in folders:
            folder_to_slugs.setdefault(folder, []).append(slug)

    for system in sorted(by_system):
        slugs = folder_to_slugs.get(system, [])
        carried = bool({s for s in slugs if s in library_slugs}) or system in library_slugs
        if carried:
            contested += 1
        lines.append(f"| {system} | {len(by_system[system])} | {'yes' if carried else 'no'} |")
    lines.append("")
    lines.append(f"systems where the library carries firmware and the named md5 still misses: {contested}")
    lines.append("")

    zip_rows = [
        (system, row)
        for system, entry in sorted(manifest["systems"].items())
        for row in entry["files"]
        if row["path"].lower().endswith(".zip")
    ]
    with_md5 = [(s, r) for s, r in zip_rows if (r.get("md5") or "").strip()]
    in_library = [(s, r) for s, r in with_md5 if r["md5"].lower() in library_md5s]

    lines.append("## Zip requirements, and how many of them the join can even see")
    lines.append("")
    lines.append("A requirement with no md5 never reaches the library join: BiosPlanner.Inspect")
    lines.append("returns Unverifiable and returns first. So the zip defect surface is the subset")
    lines.append("carrying an md5, not all 84.")
    lines.append("")
    lines.append(f"zip requirements: {len(zip_rows)} of {total}")
    lines.append(f"  carrying an md5, so the join runs:     {len(with_md5)}")
    lines.append(f"    distinct md5s among those:          {len({r['md5'].lower() for _s, r in with_md5})}")
    lines.append(f"    of those, matched in the library:   {len(in_library)}")
    lines.append(f"  no md5, reported Unverifiable:        {len(zip_rows) - len(with_md5)}")
    lines.append("")
    for system, row in sorted(with_md5, key=lambda pair: (pair[0], pair[1]["path"])):
        seen = "matched" if row["md5"].lower() in library_md5s else "no md5 match"
        lines.append(f"  {system}: {row['path']} {row['md5'][:8]} {seen}")
    lines.append("")

    named = [
        (system, row)
        for system, entry in sorted(manifest["systems"].items())
        for row in entry["files"]
        if not (row.get("md5") or "").strip()
        and row["path"].rsplit("/", 1)[-1].lower() in library_names
    ]
    lines.append("## No-md5 requirements whose exact filename the library holds")
    lines.append("")
    lines.append(f"of the {unverifiable} requirements RetroBat names no md5 for, {len(named)} have")
    lines.append("a file of exactly that name in the library:")
    for system, row in sorted(named, key=lambda pair: (pair[0], pair[1]["path"])):
        lines.append(f"  {system}: {row['path']}")
    lines.append("")

    _common.record("a3b-bios-requirement-join", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
