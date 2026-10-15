#!/usr/bin/env python3
"""Fail when the tree holds a file whose md5 is a known firmware file's.

Firmware is copyrighted, so its names and hashes may be committed and its bytes never
(CLAUDE.md, "Never commit copyrighted game content"). The known md5s are every row of
reference/retrobios/database-subset.json and every file data/retrobat/bios.json accepts.
The files checked are every tracked file plus every untracked one git does not ignore, so
the gate catches a dump before it is staged as well as after.

Usage: python tools/firmware_guard.py
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def known_md5s(root: Path = ROOT) -> dict[str, str]:
    """md5 to a name that says which firmware it is."""
    known: dict[str, str] = {}
    subset = json.loads((root / "reference" / "retrobios" / "database-subset.json").read_text(encoding="utf-8"))
    for row in subset["files"].values():
        known.setdefault(row["md5"].lower(), row.get("name") or row.get("path", ""))
    manifest = json.loads((root / "data" / "retrobat" / "bios.json").read_text(encoding="utf-8"))
    for body in manifest["systems"].values():
        for slot in body["slots"]:
            for entry in slot["accepted"]:
                known.setdefault(entry["md5"].lower(), slot["path"])
    return known


def candidates(root: Path = ROOT) -> list[Path]:
    listed = subprocess.run(
        ["git", "-C", str(root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
        capture_output=True, check=True,
    ).stdout.decode("utf-8")
    return [root / name for name in dict.fromkeys(listed.split("\0")) if name]


def md5_of(path: Path) -> str:
    digest = hashlib.md5()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def find(paths: list[Path], known: dict[str, str]) -> list[tuple[Path, str]]:
    """Each path whose bytes are known firmware, with the name it is known by. A path listed but
    gone from disk, a deletion not yet committed, is skipped."""
    return [(path, known[md5]) for path in paths if path.is_file() and (md5 := md5_of(path)) in known]


def main() -> int:
    paths = candidates()
    hits = find(paths, known_md5s())
    for path, name in hits:
        print(f"{path.relative_to(ROOT).as_posix()}: its md5 is the firmware file {name}. Firmware bytes are never committed.")
    if hits:
        return 1
    print(f"No known firmware among {len(paths)} files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
