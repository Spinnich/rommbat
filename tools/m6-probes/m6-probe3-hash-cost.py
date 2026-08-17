"""What a logical-content hash costs over the two trees the plan calls hostile.

The plan says RPCS3's 32,451 files make any recursive content hash a real performance
problem. That count is of the emulator's whole data root, so this times the hash over the
root, over the savedata subtree a save unit is actually scoped to, over MAME's nvram, and
over the loose files a class A pass really reads.

    python m6-probe3-hash-cost.py <retrobat-root>

Reads only. Reads every byte of the trees it measures, so it takes minutes.
"""

from __future__ import annotations

import hashlib
import pathlib
import sys
import time

from _common import record_offline

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

root = pathlib.Path(sys.argv[1])
saves = root / "saves"
lines: list[str] = []


def logical_hash(base: pathlib.Path, files: list[pathlib.Path]) -> tuple[str, int]:
    """Sorted relative paths plus each file's own digest, folded into one.

    The shape M6 defines: deterministic across archive implementations, because no archive
    is involved. Ordering is by the forward-slashed relative path.
    """
    folded = hashlib.md5()
    total = 0
    for path in sorted(files, key=lambda p: p.relative_to(base).as_posix()):
        digest = hashlib.md5()
        try:
            with path.open("rb") as handle:
                while chunk := handle.read(1 << 20):
                    digest.update(chunk)
                    total += len(chunk)
        except OSError:
            continue
        folded.update(path.relative_to(base).as_posix().encode("utf-8"))
        folded.update(b"\0")
        folded.update(digest.hexdigest().encode("ascii"))
        folded.update(b"\n")
    return folded.hexdigest(), total


def measure(label: str, base: pathlib.Path, files: list[pathlib.Path] | None = None) -> None:
    if not base.exists():
        lines.append(f"  {label:<44} absent")
        return
    start = time.perf_counter()
    found = files if files is not None else [p for p in base.rglob("*") if p.is_file()]
    walked = time.perf_counter()
    digest, total = logical_hash(base, found)
    done = time.perf_counter()
    lines.append(
        f"  {label:<44} {len(found):>7,} files  {total / 1e6:>10.1f} MB  "
        f"walk {walked - start:>6.2f} s  hash {done - walked:>7.2f} s  {digest}"
    )


lines.append("=== the trees the plan names hostile")
measure("saves/ps3/rpcs3 (the emulator data root)", saves / "ps3" / "rpcs3")
measure(
    "  its savedata subtree only",
    saves / "ps3" / "rpcs3" / "dev_hdd0" / "home" / "00000001" / "savedata",
)
measure("saves/mame/nvram (whole tree)", saves / "mame" / "nvram")

lines.append("")
lines.append("=== what a class A pass really reads: loose files under saves/<system>/")
loose = [p for system in saves.iterdir() if system.is_dir() for p in system.iterdir() if p.is_file()]
measure("every loose file, every system", saves, loose)

record_offline("m6-probe3-hash-cost", lines)
