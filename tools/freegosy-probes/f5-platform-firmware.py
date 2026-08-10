"""F5: does GET /api/platforms inline every firmware record, with md5?

Freegosy reads `platform.firmware` straight off the platform list rather than calling
GET /api/firmware per platform. If that array is complete and carries md5_hash, M5's
join is one request instead of one per platform, and the platform list carries a payload
cost the M2 guardrails should know about.

Also cross-checks one platform's inlined array against GET /api/firmware?platform_id=,
because "the field is populated" and "the field is complete" are different claims.
"""

from __future__ import annotations

import json

from _common import get_json, record, request

lines: list[str] = []

status, _headers, payload, elapsed = request("GET", "/api/platforms")
platforms = json.loads(payload)
lines.append("GET /api/platforms")
lines.append(f"  status {status}   {len(payload)} bytes   {elapsed:.2f} s")
lines.append(f"  platforms: {len(platforms)}")

with_firmware = [p for p in platforms if p.get("firmware")]
records = [f for p in platforms for f in (p.get("firmware") or [])]
with_md5 = [f for f in records if f.get("md5_hash")]
verified = [f for f in records if f.get("is_verified")]

lines.append(f"  platforms carrying a non-empty firmware[]: {len(with_firmware)}")
lines.append(f"  firmware records inlined in total:         {len(records)}")
lines.append(f"  of those carrying md5_hash:                {len(with_md5)}")
lines.append(f"  of those flagged is_verified:              {len(verified)}")
lines.append(f"  distinct md5s:                             {len({f['md5_hash'] for f in with_md5})}")

if records:
    keys = sorted(records[0].keys())
    lines.append(f"  fields on an inlined record: {', '.join(keys)}")

# firmware_count is a separate field; if it disagrees with len(firmware) the array is
# truncated and cannot be used as the join source.
mismatched = [
    (p["slug"], p.get("firmware_count"), len(p.get("firmware") or []))
    for p in platforms
    if p.get("firmware_count") is not None
    and p.get("firmware_count") != len(p.get("firmware") or [])
]
lines.append(f"  platforms where firmware_count != len(firmware): {len(mismatched)}")
for slug, count, length in mismatched[:5]:
    lines.append(f"    {slug}: firmware_count={count} len(firmware)={length}")

lines.append("")
if with_firmware:
    sample = max(with_firmware, key=lambda p: len(p.get("firmware") or []))
    pid = sample["id"]
    status, direct, direct_elapsed = get_json("/api/firmware", params={"platform_id": pid})
    items = direct if isinstance(direct, list) else (direct or {}).get("items", [])
    lines.append(f"GET /api/firmware?platform_id=<{sample['slug']}>")
    lines.append(f"  status {status}   {direct_elapsed:.2f} s   records: {len(items)}")
    lines.append(f"  same platform inlined on /api/platforms: {len(sample['firmware'])}")
    inline_ids = {f["id"] for f in sample["firmware"]}
    direct_ids = {f["id"] for f in items}
    lines.append(f"  ids only in the dedicated call: {sorted(direct_ids - inline_ids)}")
    lines.append(f"  ids only inlined:               {sorted(inline_ids - direct_ids)}")

record("f5-platform-firmware", lines)
