"""What the whole-library firmware join costs and what it answers, re-measured.

F5 measured GET /api/platforms at 656 firmware records over 79 platforms in 424 KB and
0.39 s. M5 builds on that, so it is re-measured here rather than quoted, along with the
three counts that decide code:

  1. is the inlined firmware[] complete, or a preview (against one dedicated call)
  2. how the md5-only join scores against the two joins the plan forbids
  3. the per-platform gap for a BIOS-dependent platform the library holds and one it does not

    python m5-probe2-platform-firmware.py [system ...]

Systems default to psx (held) and saturn (partly held). Read-only.
"""

from __future__ import annotations

import collections
import sys

from _common import get_json, manifest, record, request, requirements

systems = sys.argv[1:] or ["psx", "saturn", "megacd", "dreamcast", "neogeocd"]
lines: list[str] = []

# --- 1. the one request -------------------------------------------------------------------
status, headers, payload, elapsed = request("GET", "/api/platforms")
import json  # noqa: E402  (after the request, so the timing above is the request alone)

platforms = json.loads(payload)
records = [(p, f) for p in platforms for f in (p.get("firmware") or [])]

lines.append("=== GET /api/platforms ===")
lines.append(f"  status {status}   {len(payload)} bytes   {elapsed:.2f} s")
lines.append(f"  platforms: {len(platforms)}")
lines.append(f"  platforms carrying a non-empty firmware[]: {sum(1 for p in platforms if p.get('firmware'))}")
lines.append(f"  firmware records inlined in total:         {len(records)}")
lines.append(f"  of those carrying md5_hash:               {sum(1 for _p, f in records if f.get('md5_hash'))}")
lines.append(f"  of those flagged is_verified:             {sum(1 for _p, f in records if f.get('is_verified'))}")
lines.append(f"  distinct md5s:                            {len({(f.get('md5_hash') or '').lower() for _p, f in records if f.get('md5_hash')})}")
mismatched = [p for p in platforms if p.get("firmware_count") is not None and p["firmware_count"] != len(p.get("firmware") or [])]
lines.append(f"  platforms where firmware_count != len(firmware): {len(mismatched)}")
lines.append("")

# --- 2. complete, or a preview ------------------------------------------------------------
widest = max(platforms, key=lambda p: len(p.get("firmware") or []))
_status, dedicated, dedicated_elapsed = get_json("/api/firmware", params={"platform_id": widest["id"]})
inline_ids = {f["id"] for f in widest.get("firmware") or []}
dedicated_ids = {f["id"] for f in dedicated}

lines.append("=== the inlined array against the dedicated call ===")
lines.append(f"  widest platform: fs_slug={widest['fs_slug']!r}  inlined {len(inline_ids)} records")
lines.append(f"  GET /api/firmware?platform_id=<it>  {dedicated_elapsed:.2f} s   records: {len(dedicated_ids)}")
lines.append(f"  ids only in the dedicated call: {sorted(dedicated_ids - inline_ids)}")
lines.append(f"  ids only inlined:               {sorted(inline_ids - dedicated_ids)}")
lines.append("")

# --- 3. the join, three ways ---------------------------------------------------------------
required = requirements()
required_md5 = {md5 for _s, md5, _f in required if md5}
blank = [(s, f) for s, md5, f in required if not md5]
served_names = {(f.get("file_name") or "").lower() for _p, f in records}
by_md5: dict[str, list] = collections.defaultdict(list)
for platform, firmware in records:
    key = (firmware.get("md5_hash") or "").lower()
    if key:
        by_md5[key].append((platform, firmware))

held = {md5 for md5 in required_md5 if md5 in by_md5}
required_names = {f.rsplit("/", 1)[-1].lower() for _s, md5, f in required if md5}

verified_hits = 0
name_hits = 0
for md5 in held:
    hits = by_md5[md5]
    if any(f.get("is_verified") for _p, f in hits):
        verified_hits += 1
    wanted = {f.rsplit("/", 1)[-1].lower() for _s, m, f in required if m == md5}
    if any((f.get("file_name") or "").lower() in wanted for _p, f in hits):
        name_hits += 1

lines.append("=== the md5 join against the two joins the plan forbids ===")
lines.append(f"  manifest entries:                     {len(required)}")
lines.append(f"  of those with a blank md5:            {len(blank)}   (unjoinable in either direction)")
lines.append(f"  distinct joinable md5s RetroBat wants: {len(required_md5)}")
lines.append(f"  of those this library holds:           {len(held)}")
lines.append(f"  of those it does not:                  {len(required_md5) - len(held)}")
lines.append("")
lines.append(f"  held and flagged is_verified:         {verified_hits}")
lines.append(f"  held and NOT flagged is_verified:     {len(held) - verified_hits}   <- an is_verified filter loses these")
lines.append(f"  held under a name RetroBat uses:      {name_hits}")
lines.append(f"  held under a different name:          {len(held) - name_hits}   <- a filename join loses these")
lines.append("")
lines.append("  the renames, in full:")
for md5 in sorted(held):
    wanted = sorted({f.rsplit("/", 1)[-1] for _s, m, f in required if m == md5})
    got = sorted({f.get("file_name") for _p, f in by_md5[md5]})
    if not any(g.lower() in {w.lower() for w in wanted} for g in got):
        flags = sorted({bool(f.get("is_verified")) for _p, f in by_md5[md5]})
        lines.append(f"    {md5}  RomM {got}  RetroBat {wanted}  is_verified={flags}")
lines.append("")
lines.append("  held but flagged is_verified nowhere:")
for md5 in sorted(held):
    if not any(f.get("is_verified") for _p, f in by_md5[md5]):
        names = sorted({f.get("file_name") for _p, f in by_md5[md5]})
        wanted = sorted({f for _s, m, f in required if m == md5})
        lines.append(f"    {md5}  RomM {names}  wanted at {wanted}")
lines.append("")

# The same two joins scored per record rather than per file. F21 read one record per md5
# out of a dict, so whichever row happened to land last decided both answers; a client
# joining on md5 sees every row, and a file survives if any row carries it. Both framings
# are printed because they are different questions and their answers differ here.
verified_anywhere = sum(1 for md5 in held if any(f.get("is_verified") for _p, f in by_md5[md5]))
verified_everywhere = sum(1 for md5 in held if all(f.get("is_verified") for _p, f in by_md5[md5]))
named_anywhere = 0
named_everywhere = 0
for md5 in held:
    wanted = {f.rsplit("/", 1)[-1].lower() for _s, m, f in required if m == md5}
    got = [(f.get("file_name") or "").lower() for _p, f in by_md5[md5]]
    named_anywhere += any(name in wanted for name in got)
    named_everywhere += all(name in wanted for name in got)

lines.append("  per record rather than per file, since one md5 sits on several rows:")
lines.append(f"    held, is_verified on every copy:      {verified_everywhere}")
lines.append(f"    held, is_verified on at least one:    {verified_anywhere}")
lines.append(f"    held, named right on every copy:      {named_everywhere}")
lines.append(f"    held, named right on at least one:    {named_anywhere}")
lines.append("")

# The five renames docs/PLAN.md names by hand, checked against this library rather than
# quoted. A pair survives as evidence even when another copy carries the right name.
lines.append("  the plan's named pairs, checked:")
for served_name, wanted_name in [
    ("segacdbios9303.bin", "bios_cd_u.bin"),
    ("flash.bin", "dc_flash.bin"),
    ("sega_100.bin", "saturn_bios.bin"),
    ("pcfxbios.bin", "pcfx.rom"),
    ("bios.col", "coleco.rom"),
    ("psxonpsp660.bin", "psxonpsp660.bin"),
]:
    hits = [(p, f) for p, f in records if (f.get("file_name") or "").lower() == served_name]
    wanted_md5 = {m for _s, m, path in required if path.rsplit("/", 1)[-1].lower() == wanted_name and m}
    agree = [(p, f) for p, f in hits if (f.get("md5_hash") or "").lower() in wanted_md5]
    others = sorted({f.get("file_name") for _p, f in records if (f.get("md5_hash") or "").lower() in wanted_md5})
    lines.append(
        f"    {served_name:22} -> {wanted_name:18} rows {len(hits):2}  md5 agrees on {len(agree):2}"
        f"  is_verified={sorted({bool(f.get('is_verified')) for _p, f in agree})}"
    )
    lines.append(f"      every name this md5 is served under: {others}")
lines.append("")

# --- 3b. a record can be a row with no bytes behind it ---------------------------------------
# missing_from_fs means the server has the row and not the file, and its content route answers
# 500 rather than 404 (probe 3). A match on such a row is not a match.
held_present = {md5 for md5 in held if any(not f.get("missing_from_fs") for _p, f in by_md5[md5])}
lines.append("=== records the server no longer has the bytes for ===")
lines.append(f"  firmware records flagged missing_from_fs: {sum(1 for _p, f in records if f.get('missing_from_fs'))}")
lines.append(f"  required md5s held with at least one real copy: {len(held_present)}")
lines.append(f"  required md5s held only as a missing_from_fs row: {len(held - held_present)}")
for md5 in sorted(held - held_present):
    names = sorted({f.get("file_name") for _p, f in by_md5[md5]})
    lines.append(f"    {md5}  {names}")
sizes = sorted(
    max(f.get("file_size_bytes") or 0 for _p, f in by_md5[md5] if not f.get("missing_from_fs"))
    for md5 in held_present
)
lines.append(f"  what fetching every one of them would cost: {sum(sizes):,} bytes")
lines.append(f"    median {sizes[len(sizes) // 2]:,}   largest {sizes[-1]:,}   smallest {sizes[0]:,}")
lines.append("")

# --- 4. multiplicity, which is what dedupe exists for ---------------------------------------
lines.append("=== multiplicity ===")
counts = collections.Counter(len(v) for v in by_md5.values())
lines.append(f"  md5s appearing on more than one platform row: {sum(c for n, c in counts.items() if n > 1)}")
lines.append(f"  md5s appearing once:                          {counts.get(1, 0)}")
owed = collections.defaultdict(set)
for system, md5, path in required:
    if md5:
        owed[md5].add(path)
lines.append(f"  required md5s owing more than one destination path: {sum(1 for v in owed.values() if len(v) > 1)}")
for md5, paths in sorted(owed.items()):
    if len(paths) > 1:
        lines.append(f"    {md5}  ->  {sorted(paths)}   held={md5 in by_md5}")
lines.append("")

# --- 5. the per-platform gap ----------------------------------------------------------------
book = manifest()
lines.append("=== the gap, per system, which is what M5 reports ===")
for system in systems:
    entries = book.get(system, {}).get("biosFiles", [])
    if not entries:
        lines.append(f"  {system}: not in the manifest")
        continue

    joinable = [e for e in entries if e["md5"].strip()]
    unjoinable = [e for e in entries if not e["md5"].strip()]
    matched = [e for e in joinable if e["md5"].strip().lower() in by_md5]
    renamed = [
        e
        for e in matched
        if not any(
            (f.get("file_name") or "").lower() == e["file"].rsplit("/", 1)[-1].lower()
            for _p, f in by_md5[e["md5"].strip().lower()]
        )
    ]
    missing = [e for e in joinable if e["md5"].strip().lower() not in by_md5]

    lines.append(
        f"  {system:10} required {len(entries):3}   joinable {len(joinable):3}   matched {len(matched):3}"
        f"   of those renamed {len(renamed):2}   unjoinable {len(unjoinable):3}   missing {len(missing):3}"
    )
    for entry in missing:
        lines.append(f"      missing   {entry['file']:52} {entry['md5']}")
    for entry in unjoinable:
        lines.append(f"      no md5    {entry['file']}")

record("m5-probe2-platform-firmware", lines)
