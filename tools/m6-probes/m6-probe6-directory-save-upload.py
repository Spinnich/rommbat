"""What POST /api/saves does with an archive, which decides the class C slot and restore.

Stage 2b bundles a class C save unit to one archive and uploads it as a single Save. Six
things about that are unmeasured, and each one changes code:

  1. the name that comes back, since measurement 130 says a save is renamed and a state is not
  2. whether `emulator` is part of the upsert key, which it was NOT for states (127)
  3. whether a byte-identical replay dedups, which is what makes a replayed flush idempotent
  4. whether a DIFFERENT archive of identical logical contents also dedups (it must not,
     which is the whole argument for hashing the contents rather than the bytes)
  5. what the download side hands back
  6. whether the content_hash the server computes is over the bytes we sent

    python m6-probe6-directory-save-upload.py <rom_id>

Writes into the RomM instance named by ROMMBAT_TEST_SERVER. Uses a probe-only slot.
"""

from __future__ import annotations

import hashlib
import io
import sys
import zipfile

import json as _json

from _common import record, request

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

rom_id = int(sys.argv[1])
slot = "rommbat-m6-2b-probe"
lines: list[str] = []


def log(text: str = "") -> None:
    lines.append(text)


# The logical contents of a two-directory unit, which is the shape ps3 and psp really have.
MEMBERS = {
    "UCES01011/PARAM.SFO": b"\x00PSF\x01\x01\x00\x00probe",
    "UCES01011/DATA.BIN": b"save payload, one",
    "UCES01011SYSDATA/PARAM.SFO": b"\x00PSF\x01\x01\x00\x00sysdata",
    "UCES01011SYSDATA/SYSDATA.BIN": b"save payload, two",
}


def logical_hash(members: dict[str, bytes]) -> str:
    folded = hashlib.md5()
    for name in sorted(members):
        folded.update(name.encode("utf-8"))
        folded.update(b"\0")
        folded.update(hashlib.md5(members[name]).hexdigest().encode("ascii"))
        folded.update(b"\n")
    return folded.hexdigest()


def archive(members: dict[str, bytes], *, compress: int, stamp: tuple, order: list[str] | None = None) -> bytes:
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED, compresslevel=compress) as zf:
        for name in order or sorted(members):
            info = zipfile.ZipInfo(name, stamp)
            info.external_attr = 0o644 << 16
            zf.writestr(info, members[name])
    return buffer.getvalue()


def upload(blob: bytes, name: str, *, emulator: str, overwrite: bool = False) -> dict:
    boundary = "----rommbatprobe"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="saveFile"; filename="{name}"\r\n'.encode(),
        b"Content-Type: application/zip\r\n\r\n",
        blob,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    params = {
        "rom_id": rom_id,
        "slot": slot,
        "emulator": emulator,
        "autocleanup": "true",
        "autocleanup_limit": 10,
    }
    if overwrite:
        params["overwrite"] = "true"
    status, _headers, payload, _elapsed = request(
        "POST",
        "/api/saves",
        params=params,
        raw_body=body,
        content_type=f"multipart/form-data; boundary={boundary}",
    )
    try:
        parsed = _json.loads(payload) if payload else {}
    except ValueError:
        parsed = {"raw": payload[:200].decode("utf-8", "replace")}
    return {"status": status, "body": parsed}


def slot_rows() -> list[dict]:
    status, _headers, payload, _elapsed = request("GET", "/api/saves", params={"rom_id": rom_id})
    if status != 200:
        return []
    try:
        rows = _json.loads(payload)
    except ValueError:
        return []
    if isinstance(rows, dict):
        rows = rows.get("items", [])
    if not isinstance(rows, list):
        return []
    return [row for row in rows if row.get("slot") == slot]


canonical = archive(MEMBERS, compress=6, stamp=(1980, 1, 1, 0, 0, 0))
log(f"logical content hash of the unit : {logical_hash(MEMBERS)}")
log(f"canonical archive               : {len(canonical)} B  md5 {hashlib.md5(canonical).hexdigest()}")
log()

log("--- 1. first upload")
first = upload(canonical, "UCES01011.zip", emulator="ppsspp")
log(f"  status {first['status']}")
saved = first["body"] if isinstance(first["body"], dict) else {}
for key in ("id", "file_name", "file_name_no_tags", "file_extension", "file_size_bytes", "content_hash", "emulator", "slot"):
    log(f"    {key} = {saved.get(key)!r}")
save_id = saved.get("id")
log(f"  server content_hash equals md5 of the bytes we sent: {saved.get('content_hash') == hashlib.md5(canonical).hexdigest()}")
log(f"  rows in slot: {len(slot_rows())}")
log()

log("--- 2. byte-identical replay (must dedup, or a replayed flush duplicates)")
before = len(slot_rows())
second = upload(canonical, "UCES01011.zip", emulator="ppsspp")
after = slot_rows()
log(f"  status {second['status']}  id {second['body'].get('id') if isinstance(second['body'], dict) else '?'}")
log(f"  rows before={before} after={len(after)}  same row reused: {second['body'].get('id') == save_id if isinstance(second['body'], dict) else '?'}")
log()

log("--- 3. a DIFFERENT archive of identical logical contents")
log("    Go and .NET differ on ordering, timestamps and compression level. This is the same")
log("    unit re-zipped the way another implementation would, so the bytes differ and the")
log("    logical hash does not. If the server dedups on bytes, this makes a second row, which")
log("    is precisely why content_hash is defined over the contents.")
variant = archive(MEMBERS, compress=9, stamp=(2020, 6, 15, 12, 0, 0), order=list(reversed(sorted(MEMBERS))))
log(f"  variant archive: {len(variant)} B  md5 {hashlib.md5(variant).hexdigest()}")
log(f"  logical hash unchanged: {logical_hash(MEMBERS)}")
third = upload(variant, "UCES01011.zip", emulator="ppsspp")
rows = slot_rows()
log(f"  status {third['status']}  rows now {len(rows)}")
log(f"  new row id {third['body'].get('id') if isinstance(third['body'], dict) else '?'} (was {save_id})")
log()

log("--- 4. is `emulator` part of the upsert key? (it was NOT for states, measurement 127)")
fourth = upload(canonical, "UCES01011.zip", emulator="rpcs3")
rows = slot_rows()
log(f"  status {fourth['status']}  rows now {len(rows)}")
log(f"  ids in slot: {sorted(r.get('id') for r in rows)}")
log(f"  emulators in slot: {sorted({str(r.get('emulator')) for r in rows})}")
log()

log("--- 5. download side")
if save_id:
    status, _headers, blob, _elapsed = request(
        "GET",
        f"/api/saves/{save_id}/content",
        params={"device_id": "m6-2b-probe", "optimistic": "false"},
    )
    log(f"  GET content -> {status}, {len(blob) if isinstance(blob, bytes) else '?'} bytes")
    if isinstance(blob, bytes):
        log(f"  md5 of what came back: {hashlib.md5(blob).hexdigest()}")
        log(f"  identical to what was sent: {blob == canonical}")
        try:
            with zipfile.ZipFile(io.BytesIO(blob)) as zf:
                log(f"  it is a readable zip holding: {sorted(zf.namelist())}")
        except zipfile.BadZipFile:
            log("  NOT a readable zip")
log()

log("--- 6. the slot as it now stands")
for row in slot_rows():
    log(f"  id={row.get('id')} name={row.get('file_name')!r} emulator={row.get('emulator')!r} hash={row.get('content_hash')} size={row.get('file_size_bytes')}")

record("probe6-directory-save-upload", lines)
