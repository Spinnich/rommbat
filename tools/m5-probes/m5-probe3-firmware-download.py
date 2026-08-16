"""How GET /api/firmware/{id}/content/{file_name} really behaves.

M3's resume machinery was built against the ROM content route, which nginx serves. Firmware
goes through Starlette's FileResponse instead, so ranges, ETag, Content-Length, a resumed
request and a resume point past the end are all measured here rather than assumed. Names
with a space and with brackets are exercised too, because RomM serves plenty of both and
the URL is built from the served name.

    python m5-probe3-firmware-download.py

Read-only: it downloads a handful of small files into memory and writes nothing but a
transcript.
"""

from __future__ import annotations

import hashlib
import json
import urllib.parse

from _common import record, request, requirements

lines: list[str] = []

_status, _headers, payload, _elapsed = request("GET", "/api/platforms")
platforms = json.loads(payload)
records = [(p, f) for p in platforms for f in (p.get("firmware") or [])]

required_md5 = {md5 for _s, md5, _f in requirements() if md5}


def content_path(firmware: dict, name: str | None = None) -> str:
    return f"/api/firmware/{firmware['id']}/content/{urllib.parse.quote(name or firmware['file_name'])}"


def first(predicate):
    for _platform, firmware in records:
        if predicate(firmware):
            return firmware
    return None


def show_headers(headers: dict, indent: str = "    ") -> list[str]:
    return [f"{indent}{key}: {value!r}" for key, value in sorted(headers.items())]


present = lambda f: not f.get("missing_from_fs")  # noqa: E731

plain = first(lambda f: present(f) and (f.get("md5_hash") or "").lower() in required_md5 and " " not in f["file_name"])
spaced = first(lambda f: present(f) and " " in f["file_name"])
bracketed = first(lambda f: present(f) and ("[" in f["file_name"] or "(" in f["file_name"]))
absent = first(lambda f: f.get("missing_from_fs"))

lines.append("=== missing_from_fs, which decides whether a match is a match ===")
lines.append(f"  firmware records inlined:      {len(records)}")
lines.append(f"  flagged missing_from_fs:       {sum(1 for _p, f in records if f.get('missing_from_fs'))}")
lines.append("")

lines.append("=== the files this probe uses ===")
for label, firmware in [("plain", plain), ("with a space", spaced), ("with brackets", bracketed), ("missing_from_fs", absent)]:
    lines.append(
        f"  {label:16} id={firmware['id']:<6} {firmware['file_name']!r}  "
        f"{firmware.get('file_size_bytes')} bytes  missing_from_fs={firmware.get('missing_from_fs')}"
    )
lines.append("")

# --- 1. a whole file ----------------------------------------------------------------------
status, headers, body, elapsed = request("GET", content_path(plain))
digest = hashlib.md5(body).hexdigest()

lines.append("=== a whole file ===")
lines.append(f"  GET .../content/{plain['file_name']}")
lines.append(f"  status {status}   {len(body)} bytes   {elapsed:.2f} s")
lines.extend(show_headers(headers))
lines.append(f"  md5 of the body:               {digest}")
lines.append(f"  matches the record:            {digest == (plain.get('md5_hash') or '').lower()}")
lines.append(f"  matches what RetroBat requires: {digest in required_md5}")
lines.append("")

# --- 2. a range, which is what a resume is ------------------------------------------------
half = max(1, len(body) // 2)
status, headers, tail, elapsed = request("GET", content_path(plain), headers_extra={"Range": f"bytes={half}-"})
lines.append("=== a resumed request ===")
lines.append(f"  Range: bytes={half}-")
lines.append(f"  status {status}   {len(tail)} bytes")
lines.extend(show_headers(headers))
lines.append(f"  the tail splices back into the whole file: {body[:half] + tail == body}")
lines.append("")

# --- 3. a resume point past the end -------------------------------------------------------
status, headers, over, _elapsed = request("GET", content_path(plain), headers_extra={"Range": f"bytes={len(body)}-"})
lines.append("=== a resume point past the end ===")
lines.append(f"  Range: bytes={len(body)}-   status {status}   {len(over)} bytes")
lines.extend(show_headers(headers))
lines.append("")

# --- 4. an If-Range against a stale validator ----------------------------------------------
status, _headers, answer, _elapsed = request(
    "GET", content_path(plain), headers_extra={"Range": f"bytes={half}-", "If-Range": '"not-the-validator"'}
)
lines.append("=== a stale If-Range ===")
lines.append(f"  status {status}   {len(answer)} bytes   (200 with the whole body is the safe answer)")
lines.append("")

# --- 5. names that need escaping -----------------------------------------------------------
lines.append("=== names with a space and with brackets ===")
for label, firmware in [("space", spaced), ("brackets", bracketed)]:
    encoded = content_path(firmware)
    status, headers, body2, elapsed = request("GET", encoded)
    lines.append(f"  {label}: {firmware['file_name']!r}")
    lines.append(f"    encoded path: {encoded}")
    lines.append(f"    status {status}   {len(body2)} bytes   {elapsed:.2f} s   type {headers.get('Content-Type')!r}")
    lines.append(f"    md5 matches the record: {hashlib.md5(body2).hexdigest() == (firmware.get('md5_hash') or '').lower()}")

    # The same request with the name left raw, which is what a naive client sends.
    raw = f"/api/firmware/{firmware['id']}/content/{firmware['file_name']}"
    try:
        status, _headers, body3, _elapsed = request("GET", raw)
        lines.append(f"    unencoded: status {status}   {len(body3)} bytes")
    except Exception as error:  # noqa: BLE001 - the failure is the measurement
        lines.append(f"    unencoded: refused before the wire, {type(error).__name__}: {error}")
lines.append("")

# --- 6. the file name in the URL, which turns out not to be read ------------------------------
status, _headers, wrong, _elapsed = request("GET", content_path(plain, "not-the-name.bin"))
lines.append("=== the right id under the wrong name ===")
lines.append(f"  status {status}   {len(wrong)} bytes   identical to the real body: {wrong == body}")
lines.append("")

# --- 7. a record whose bytes the server no longer has -----------------------------------------
status, headers, body4, _elapsed = request("GET", content_path(absent))
lines.append("=== a record flagged missing_from_fs ===")
lines.append(f"  id={absent['id']}  {absent['file_name']!r}")
lines.append(f"  status {status}   {len(body4)} bytes   type {headers.get('Content-Type')!r}")
lines.append(f"  body: {body4[:200]!r}")

record("m5-probe3-firmware-download", lines)
