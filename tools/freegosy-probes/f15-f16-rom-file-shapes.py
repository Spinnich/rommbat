"""F15 and F16: what a multi-file ROM actually looks like on a real library.

F15 — M3's finding 83 says every multi-file ROM carries an empty fs_extension and every
extensionless ROM is multi-file, 105 of 105 both ways in a 2,000-ROM sample. Freegosy
carries a distinct case it calls "single file foldered": files.length == 1 with an empty
fs_extension. If those rows exist, an exclusion state that reports them as multi-file is
telling the user something that is not quite true.

F16 — Freegosy treats a multi-disc set as one ROM whose files[] holds a .m3u playlist
alongside per-disc images, and filters .cue/.ccd/.mds/.toc out as non-launchable. RomMBat's
plan is thin on multi-disc, so this measures what the later multi-file milestone faces.

Pages with the sidecar flags off, per the M2 guardrail.
"""

from __future__ import annotations

import collections
import os

from _common import get_json, record

PAGES = int(os.environ.get("PROBE_PAGES", "8"))
LIMIT = 250

lines: list[str] = []

rows = []
for page in range(PAGES):
    status, body, elapsed = get_json(
        "/api/roms",
        params={
            "limit": LIMIT,
            "offset": page * LIMIT,
            "order_by": "id",
            "order_dir": "asc",
            "with_files": "true",
            "with_char_index": "false",
            "with_filter_values": "false",
            "with_rom_id_index": "false",
        },
    )
    items = (body or {}).get("items", [])
    if not items:
        break
    rows.extend(items)

lines.append(f"Sampled {len(rows)} roms over {PAGES} pages of {LIMIT}, with_files=true")
lines.append("")

# --- F15: the fs_extension / multi-file biconditional -----------------------------------
# The schema carries three shape flags, not one. Read all of them rather than assuming
# has_multiple_files is the only one.
FLAGS = ["has_simple_single_file", "has_nested_single_file", "has_multiple_files"]
lines.append(f"Shape flags present on the rom schema: {', '.join(FLAGS)}")
lines.append("")

buckets: collections.Counter[str] = collections.Counter()
one_file_no_ext = []
for rom in rows:
    files = rom.get("files") or []
    ext = (rom.get("fs_extension") or "").strip()
    flag = next((f for f in FLAGS if rom.get(f)), "(none set)")
    count = "1" if len(files) == 1 else ("0" if not files else "n")
    buckets[f"files={count} ext={'yes' if ext else 'no':3s} {flag}"] += 1
    if len(files) == 1 and not ext:
        one_file_no_ext.append(rom)

lines.append("Cross-tabulation of file count against fs_extension and the shape flag:")
for key, count in sorted(buckets.items()):
    lines.append(f"  {key:50s} {count}")
lines.append("")

lines.append("The two claims finding 83 makes, checked separately:")
ext_empty = [r for r in rows if not (r.get("fs_extension") or "").strip()]
multi_flagged = [r for r in rows if r.get("has_multiple_files")]
lines.append(f"  roms with an empty fs_extension:            {len(ext_empty)}")
lines.append(
    f"    of those flagged has_multiple_files:      "
    f"{len([r for r in ext_empty if r.get('has_multiple_files')])}"
)
lines.append(
    f"    of those flagged has_nested_single_file:  "
    f"{len([r for r in ext_empty if r.get('has_nested_single_file')])}"
)
lines.append(f"  roms flagged has_multiple_files:            {len(multi_flagged)}")
lines.append(
    f"    of those with an empty fs_extension:      "
    f"{len([r for r in multi_flagged if not (r.get('fs_extension') or '').strip()])}"
)
lines.append("")

lines.append(f"Rows with exactly one file and an empty fs_extension: {len(one_file_no_ext)}")
for rom in one_file_no_ext[:6]:
    name = (rom.get("files") or [{}])[0].get("file_name", "?")
    flag = next((f for f in FLAGS if rom.get(f)), "(none set)")
    lines.append(f"  id={rom['id']} {flag} fs_name={rom.get('fs_name')!r}")
    lines.append(f"      member={name!r}")
lines.append("")

# --- F16: multi-disc shape ---------------------------------------------------------------
multifile = [r for r in rows if len(r.get("files") or []) > 1]
with_m3u = [
    r
    for r in multifile
    if any((f.get("file_name") or "").lower().endswith(".m3u") for f in r["files"])
]
member_exts: collections.Counter[str] = collections.Counter()
for rom in multifile:
    for entry in rom["files"]:
        name = (entry.get("file_name") or "").lower()
        member_exts[name.rsplit(".", 1)[-1] if "." in name else "(none)"] += 1

lines.append(f"Multi-file roms in the sample: {len(multifile)}")
lines.append(f"  of those carrying a .m3u member: {len(with_m3u)}")
lines.append("  member extensions across all multi-file roms:")
for ext, count in member_exts.most_common(14):
    lines.append(f"    .{ext:<10s} {count}")
lines.append("")

if with_m3u:
    sample = with_m3u[0]
    lines.append(f"One .m3u-bearing rom, id={sample['id']}:")
    lines.append(f"  fs_name={sample.get('fs_name')!r}  fs_extension={sample.get('fs_extension')!r}")
    for entry in sample["files"]:
        lines.append(
            f"    {entry.get('file_name')!r}  {entry.get('file_size_bytes')} bytes  md5={bool(entry.get('md5_hash'))}"
        )
else:
    disc = [
        r
        for r in multifile
        if any("disc" in (f.get("file_name") or "").lower() for f in r["files"])
    ]
    lines.append(f"No .m3u member in the sample. Roms with a 'disc' in a member name: {len(disc)}")
    for rom in disc[:2]:
        lines.append(f"  id={rom['id']} fs_name={rom.get('fs_name')!r}")
        for entry in rom["files"][:6]:
            lines.append(f"    {entry.get('file_name')!r}")

record("f15-f16-rom-file-shapes", lines)
