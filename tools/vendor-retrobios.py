#!/usr/bin/env python3
"""Vendor the slice of Abdess/retrobios RomMBat's firmware manifest is derived from.

retrobios's metadata is MIT; its firmware files are not licensed to anyone and are never
fetched here. Only metadata comes down, at one pinned commit:

  * emulators/<profile>.yml, verbatim, for every profile a RetroBat row resolves to
    through tools/firmware_sources.py
  * platforms/bizhawk.yml, verbatim, BizHawk's FirmwareDatabase.cs as retrobios scrapes it
  * database-subset.json, the rows of database.json the profiles above, BizHawk and
    RetroBat's own list can reach by a hash or by a dump-catalog name, each row unchanged.
    The full file is 25 MB, almost all of it firmware no RetroBat row reads

The pin is database-subset.json's _ref, so a plain refresh re-pulls the same commit and moving
it is `refresh.sh --ref retrobios=<sha>`. Called by reference/refresh.sh after es_systems.cfg
and batocera-systems.json are written, because the slice depends on both.

Usage: python tools/vendor-retrobios.py <reference dir> [<sha>]
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))
import firmware_sources  # noqa: E402

REPO = "Abdess/retrobios"


def raw(ref: str, path: str) -> bytes:
    url = f"https://raw.githubusercontent.com/{REPO}/{ref}/{urllib.request.quote(path)}"
    with urllib.request.urlopen(url, timeout=120) as response:
        return response.read()


def listing(ref: str) -> list[str]:
    tree = subprocess.run(
        ["gh", "api", f"repos/{REPO}/git/trees/{ref}?recursive=1", "--jq", ".tree[].path"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.split()
    return [path for path in tree if path.startswith("emulators/") and path.endswith(".yml")]


def hashes(value) -> list[str]:
    if isinstance(value, str):
        return [value.lower()]
    return [str(item).lower() for item in value or []]


def names_and_hashes(profiles: dict[str, dict], bizhawk: dict, batocera: dict) -> tuple[set[str], set[str]]:
    """Every file name and every md5 or sha1 the vendored sources mention, lowercased."""
    names: set[str] = set()
    known: set[str] = set()

    def take(entry: dict) -> None:
        names.add(str(entry.get("name", "")).rsplit("/", 1)[-1].lower())
        names.update(str(alias).rsplit("/", 1)[-1].lower() for alias in entry.get("aliases") or [])
        known.update(hashes(entry.get("md5")) + hashes(entry.get("sha1")))

    for profile in profiles.values():
        for entry in profile.get("files") or []:
            if isinstance(entry, dict):
                take(entry)
    for system in (bizhawk.get("systems") or {}).values():
        for entry in system.get("files") or []:
            take(entry)
    for system in batocera.values():
        for entry in system.get("biosFiles", []):
            names.add(entry["file"].rsplit("/", 1)[-1].lower())
            if entry["md5"].strip():
                known.add(entry["md5"].strip().lower())
    names.discard("")
    return names, known


def main() -> int:
    if len(sys.argv) not in (2, 3):
        sys.exit(__doc__)
    reference = Path(sys.argv[1]).resolve()
    out = reference / "retrobios"
    previous = out / "database-subset.json"
    ref = sys.argv[2] if len(sys.argv) == 3 else json.loads(previous.read_text(encoding="utf-8"))["_ref"]

    print(f"retrobios {ref[:12]}:")
    paths = listing(ref)
    with ThreadPoolExecutor(max_workers=16) as pool:
        texts = dict(zip(paths, pool.map(lambda p: raw(ref, p), paths)))
    profiles = {Path(p).stem: yaml.safe_load(text) for p, text in texts.items()}

    rows = firmware_sources.read_rows(reference / "es_systems.cfg")
    resolved, unresolved = firmware_sources.resolve_all(rows, profiles, firmware_sources.read_row_map())
    wanted = sorted(firmware_sources.profiles_named(resolved))

    if (out / "emulators").exists():
        shutil.rmtree(out / "emulators")
    (out / "emulators").mkdir(parents=True)
    for name in wanted:
        (out / "emulators" / f"{name}.yml").write_bytes(texts[f"emulators/{name}.yml"])
    print(f"  emulators/  {len(wanted)} of {len(paths)} profiles")

    (out / "platforms").mkdir(exist_ok=True)
    bizhawk_text = raw(ref, "platforms/bizhawk.yml")
    (out / "platforms" / "bizhawk.yml").write_bytes(bizhawk_text)
    print("  platforms/bizhawk.yml")

    names, known = names_and_hashes(
        {name: profiles[name] for name in wanted},
        yaml.safe_load(bizhawk_text),
        json.loads((reference / "batocera-systems.json").read_text(encoding="utf-8")),
    )
    database = json.loads(raw(ref, "database.json"))
    kept = {}
    for sha1, row in database["files"].items():
        catalog = {
            str(entry.get("name", "")).lower()
            for entry in (row.get("provenance") or {}).values()
            if isinstance(entry, dict)
        }
        if row["md5"].lower() in known or sha1.lower() in known or catalog & names:
            kept[sha1] = row
    subset = {
        "_comment": (
            "Rows of retrobios database.json, unchanged, that a vendored profile, BizHawk's list or "
            "RetroBat's batocera-systems.json reaches by md5, by sha1, or by a file name equal to a "
            "No-Intro, Redump or TOSEC name in the row's provenance. Written by "
            "tools/vendor-retrobios.py; do not hand-edit."
        ),
        "_source": f"https://github.com/{REPO}/blob/{ref}/database.json",
        "_ref": ref,
        "_generated_at": database.get("generated_at"),
        "files": dict(sorted(kept.items())),
    }
    previous.write_text(json.dumps(subset, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"  database-subset.json  {len(kept)} of {len(database['files'])} rows")

    # Reported after the slice is written rather than instead of it, so a refresh at a RetroBat
    # release that adds an emulator still vendors the rest and verify.py still runs.
    if unresolved:
        listed = "\n    ".join(sorted({f"{row.key}  ({row.folder})" for row in unresolved}))
        print(f"  rows no retrobios source describes and data/retrobat/firmware-rows.json does not mark:\n    {listed}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
