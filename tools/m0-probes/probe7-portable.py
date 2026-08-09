#!/usr/bin/env python3
"""M0 probe 7: does anything in the tree hard-code the drive letter?

Two modes:

  capture   scan the install for absolute paths and write a manifest
  compare   re-scan after a move and report what changed

The interesting output is not "how many absolute paths exist" but "which of them are
RetroBat's own and would therefore break on a drive-letter change". Caches and logs are
allowed to contain stale absolute paths; configuration is not.

Usage:
  python tools/m0-probes/probe7-portable.py capture G:\\RetroBat probe-output/probe7-before.json
  python tools/m0-probes/probe7-portable.py compare H:\\RetroBat probe-output/probe7-before.json
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

# Config that RetroBat or RomMBat actually reads at runtime. An absolute path here is a
# portability defect; the same string in a .log is merely history.
CONFIG_SUFFIXES = {".cfg", ".ini", ".xml", ".menu", ".json", ".bat", ".info"}

# Directories whose contents are caches, logs or emulator-private state. Scanned, but
# reported separately, because a stale absolute path in them costs nothing.
NOISE_DIRS = {"emulators", "cheats", "decorations", "themes", "tmp", "records", "library", "screenshots"}

# Negative lookbehind keeps "https://" and "MS:/PSP" out of the results; a real drive
# letter is never preceded by another alphanumeric.
DRIVE_RE = re.compile(rb"(?<![A-Za-z0-9])[A-Za-z]:[\\/]")


def scan(root: Path) -> dict:
    config_hits: dict[str, list[str]] = {}
    noise_hits: dict[str, int] = {}
    scanned = 0

    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root)
        top = rel.parts[0] if rel.parts else ""

        if path.suffix.lower() not in CONFIG_SUFFIXES:
            continue
        try:
            if path.stat().st_size > 8_000_000:
                continue
            blob = path.read_bytes()
        except OSError:
            continue

        scanned += 1
        matches = DRIVE_RE.findall(blob)
        if not matches:
            continue

        if top in NOISE_DIRS:
            noise_hits[rel.as_posix()] = len(matches)
            continue

        # Keep a little context so a human can judge whether it matters.
        samples = []
        for m in re.finditer(rb"(?<![A-Za-z0-9])[A-Za-z]:[\\/][^\s\"'<>|]{0,80}", blob):
            samples.append(m.group(0).decode("utf-8", "replace"))
            if len(samples) >= 3:
                break
        config_hits[rel.as_posix()] = samples

    return {
        "root": str(root),
        "files_scanned": scanned,
        "config_files_with_absolute_paths": config_hits,
        "noise_files_with_absolute_paths": noise_hits,
    }


def main() -> int:
    if len(sys.argv) != 4:
        print(__doc__)
        return 2

    mode, root_arg, manifest_arg = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])

    if mode == "capture":
        result = scan(root_arg)
        manifest_arg.parent.mkdir(parents=True, exist_ok=True)
        manifest_arg.write_text(json.dumps(result, indent=2), encoding="utf-8")
        report(result)
        print(f"\nwrote {manifest_arg}")
        return 0

    if mode == "compare":
        before = json.loads(manifest_arg.read_text(encoding="utf-8"))
        after = scan(root_arg)
        print(f"before root : {before['root']}")
        print(f"after root  : {after['root']}")
        print()

        before_files = set(before["config_files_with_absolute_paths"])
        after_files = set(after["config_files_with_absolute_paths"])

        stale = []
        for rel in sorted(after_files):
            for sample in after["config_files_with_absolute_paths"][rel]:
                # An absolute path still naming the OLD root after a move is the failure.
                if sample.upper().startswith(before["root"][:2].upper()):
                    stale.append((rel, sample))

        print(f"config files with absolute paths: before {len(before_files)}, after {len(after_files)}")
        if stale:
            print("\nSTALE references to the old drive letter (these are the defects):")
            for rel, sample in stale:
                print(f"  {rel}\n    {sample}")
        else:
            print("\nNo config file still references the old drive letter.")

        print(f"\nnew config files with absolute paths: {sorted(after_files - before_files) or 'none'}")
        return 0

    print(__doc__)
    return 2


def report(result: dict) -> None:
    hits = result["config_files_with_absolute_paths"]
    print(f"scanned {result['files_scanned']} config files under {result['root']}")
    print(f"config files containing an absolute path: {len(hits)}")
    for rel, samples in sorted(hits.items()):
        print(f"  {rel}")
        for s in samples:
            print(f"      {s}")
    print(f"\ncache/log files containing one (harmless): {len(result['noise_files_with_absolute_paths'])}")


if __name__ == "__main__":
    raise SystemExit(main())
