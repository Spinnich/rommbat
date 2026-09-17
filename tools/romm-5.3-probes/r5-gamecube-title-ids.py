"""R5: how unique a GameCube title_id is across a scanned platform (#186).

Finding 2 and save-sync reason from this reading: 1,793 GameCube rows carrying an id, 1,601
distinct, 167 ids shared by 359 rows, and 101 groups over 222 rows once each disc set is
folded back into one game. Folding matters because a library holding multi-disc releases as
loose files has a row per disc, and two discs sharing a memory card sharing an id is not the
same fact as two revisions sharing one.

What it takes, over every row the platform pages back:

  ids            rows carrying a non-empty title_id, and how many distinct values
  shape          whether each id is eight hex digits decoding to a printable A-Z0-9 game code,
                 which is what RomMBat's header route reads, and the leading letters
  shared         ids carried by more than one row, and the rows under them
  folded         the same after removing "(Disc N)" from each name, so what is left sharing an
                 id is a revision or a re-release rather than another disc of one game

Usage: r5-gamecube-title-ids.py [platform slug], defaulting to ngc.

Read-only. Every request is a GET.
"""

from __future__ import annotations

import collections
import re
import sys

import _common

DISC = re.compile(r"\s*\((?:disc|disk)\s*\d+(?:\s*of\s*\d+)?\)", re.IGNORECASE)
CODE = re.compile(r"^[A-Z0-9]{4}$")


def decode(title_id: str) -> str | None:
    if not re.fullmatch(r"[0-9A-Fa-f]{8}", title_id):
        return None
    text = bytes.fromhex(title_id).decode("ascii", errors="replace")
    return text if CODE.match(text) else None


def main() -> int:
    slug = sys.argv[1] if len(sys.argv) > 1 else "ngc"
    version = _common.server_version()
    platform = next((p for p in _common.platforms() if slug in (p.get("slug"), p.get("fs_slug"))), None)
    if platform is None:
        raise SystemExit(f"no platform '{slug}' holding roms")

    rows = 0
    names: dict[str, list[str]] = collections.defaultdict(list)
    for row in _common.walk({"platform_ids": platform["id"]}):
        rows += 1
        title_id = (row.get("title_id") or "").strip()
        if title_id:
            stem = row.get("fs_name_no_ext") or row.get("fs_name") or str(row["id"])
            names[title_id].append(stem)

    carrying = sum(len(stems) for stems in names.values())
    decoded = {tid: decode(tid) for tid in names}
    leading = collections.Counter(code[0] for code in decoded.values() if code)
    shared = {tid: stems for tid, stems in names.items() if len(stems) > 1}

    folded = {}
    for tid, stems in shared.items():
        games = {DISC.sub("", stem).strip() for stem in stems}
        if len(games) > 1:
            folded[tid] = stems

    lines = [
        f"# R5: title_id uniqueness on `{slug}`",
        "",
        f"Server reports `{version}`. The platform pages back {rows} rows.",
        "",
        "| reading | value |",
        "| --- | --- |",
        f"| rows carrying a title_id | {carrying} of {rows} |",
        f"| distinct ids | {len(names)} |",
        f"| ids decoding to a four character A-Z0-9 code | {sum(1 for c in decoded.values() if c)} |",
        f"| leading letters | {', '.join(f'{k} {v}' for k, v in leading.most_common())} |",
        f"| ids shared by more than one row | {len(shared)}, over {sum(len(s) for s in shared.values())} rows |",
        f"| still shared once disc sets are folded | {len(folded)}, over {sum(len(s) for s in folded.values())} rows |",
    ]

    undecoded = [tid for tid, code in decoded.items() if not code]
    if undecoded:
        lines += ["", f"Ids not decoding to a game code: {', '.join(sorted(undecoded)[:20])}"]

    # Listed so a reader can check the fold: a disc naming the expression misses shows up here
    # as two stems that differ only in how they number the disc.
    lines += ["", "## Still shared once disc sets are folded", ""]
    for tid in sorted(folded):
        lines.append(f"- `{tid}` ({decoded[tid]}): " + "; ".join(sorted(folded[tid])))

    _common.record(f"r5-title-ids-{slug}", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
