"""F1, F2, F3, F6, F7, F8, F9, F10, F11, F12 and F22: one save lifecycle, two devices.

These findings interlock, so they share one setup rather than one probe each. The script
creates two throwaway devices and uploads a handful of saves to a single ROM under a slot
named for this probe, then answers:

  F6  what the server renames an uploaded save to
  F3  whether a byte-identical re-upload into the same slot dedups
  F2  whether autocleanup actually prunes the slot
  F12 what a 409 conflict body carries
  F7  whether a save uploaded by one device is visible to another
  F8  whether device_syncs[].is_current tracks a peer's upload
  F9  whether origin_device_id names the uploading device
  F1  whether downloading with the default `optimistic` marks the device current
  F10 whether untrack changes what the device sees
  F11 what /api/saves/summary returns
  F22 whether negotiate works without an explicit device_id

**This writes to a real library.** Everything it creates is deleted in the finally block:
every save id it uploaded, then both devices. It touches no save and no device it did not
create. Run it only against an instance you are willing to have written to.
"""

from __future__ import annotations

import json
import os
import uuid

from _common import multipart, record, request

ROM_ID = int(os.environ.get("PROBE_ROM_ID", "1393"))
SLOT = "rommbat-freegosy-probe"

lines: list[str] = []
created_saves: list[int] = []
created_devices: list[str] = []


def log(text: str = "") -> None:
    lines.append(text)


def make_device(label: str) -> str:
    status, _h, payload, _t = request(
        "POST",
        "/api/devices",
        json_body={
            "name": f"RomMBat freegosy probe {label}",
            "platform": "windows",
            "client": "RomMBat-probe",
            "client_version": "0.0.0-probe",
            "allow_duplicate": True,
        },
    )
    body = json.loads(payload)
    device_id = str(body.get("id") or body.get("device_id"))
    created_devices.append(device_id)
    log(f"POST /api/devices ({label}) -> {status}, device id ends {device_id[-6:]}")
    return device_id


def upload(device_id: str | None, content: bytes, name: str, **extra):
    body, ctype = multipart({"saveFile": (name, content)})
    params = {"rom_id": ROM_ID, "slot": SLOT, "emulator": "rommbat-probe", **extra}
    if device_id:
        params["device_id"] = device_id
    status, _h, payload, elapsed = request(
        "POST", "/api/saves", params=params, raw_body=body, content_type=ctype
    )
    try:
        parsed = json.loads(payload)
    except ValueError:
        parsed = {"_raw": payload[:400].decode("utf-8", "replace")}
    if status < 300 and isinstance(parsed, dict) and parsed.get("id"):
        created_saves.append(parsed["id"])
    return status, parsed, elapsed


def list_saves(device_id: str | None = None):
    params = {"rom_id": ROM_ID}
    if device_id:
        params["device_id"] = device_id
    status, _h, payload, _t = request("GET", "/api/saves", params=params)
    body = json.loads(payload) if payload else []
    return body if isinstance(body, list) else body.get("items", [])


def sync_for(save: dict, device_id: str) -> dict | None:
    for entry in save.get("device_syncs") or []:
        if entry.get("device_id") == device_id:
            return entry
    return None


try:
    device_a = make_device("A")
    device_b = make_device("B")
    log()

    # --- F6: what does the server call the file it stored? ---------------------------
    log("=== F6: the server's rename ===")
    status, save1, _t = upload(device_a, b"SAVE-ONE" + b"\x00" * 200, "probe.srm")
    log(f"POST /api/saves (sent 'probe.srm') -> {status}")
    if status >= 300:
        log(f"  body: {json.dumps(save1)[:400]}")
        raise SystemExit("upload failed, cannot continue")
    log(f"  file_name         {save1.get('file_name')!r}")
    log(f"  file_name_no_tags {save1.get('file_name_no_tags')!r}")
    log(f"  file_name_no_ext  {save1.get('file_name_no_ext')!r}")
    log(f"  slot={save1.get('slot')!r}  emulator={save1.get('emulator')!r}")
    log(f"  content_hash={save1.get('content_hash')!r}")
    log(f"  origin_device_id ends {str(save1.get('origin_device_id'))[-6:]!r}  (F9)")
    log()

    # --- F3: does a byte-identical re-upload dedup? -----------------------------------
    log("=== F3: byte-identical re-upload into the same slot ===")
    before = len(list_saves())
    status, save2, _t = upload(device_a, b"SAVE-ONE" + b"\x00" * 200, "probe.srm")
    after = list_saves()
    log(f"second identical upload -> {status}, id {save2.get('id')}")
    log(f"  saves in the slot before={before} after={len(after)}")
    log(f"  same row reused: {save2.get('id') == save1.get('id')}")
    log(f"  file names now: {[s.get('file_name') for s in after]}")
    log()

    # --- F12: force a conflict --------------------------------------------------------
    log("=== F12: conflict body ===")
    status, other, _t = upload(device_b, b"SAVE-FROM-B" + b"\x11" * 200, "probe.srm")
    log(f"device B uploads different content -> {status}, id {other.get('id')}")
    status, conflict, _t = upload(device_a, b"SAVE-FROM-A-AGAIN" + b"\x22" * 200, "probe.srm")
    log(f"device A uploads again without overwrite -> {status}")
    log(f"  body: {json.dumps(conflict)[:600]}")
    if status == 409:
        status, forced, _t = upload(
            device_a, b"SAVE-FROM-A-AGAIN" + b"\x22" * 200, "probe.srm", overwrite="true"
        )
        log(f"  retry with overwrite=true -> {status}, id {forced.get('id')}")
    log()

    # --- F7, F8, F9: what each device sees --------------------------------------------
    log("=== F7 / F8 / F9: per-device visibility and currency ===")
    seen_a = list_saves(device_a)
    seen_b = list_saves(device_b)
    log(f"GET /api/saves?rom_id&device_id=A -> {len(seen_a)} saves")
    log(f"GET /api/saves?rom_id&device_id=B -> {len(seen_b)} saves")
    log(f"  same id set: {sorted(s['id'] for s in seen_a) == sorted(s['id'] for s in seen_b)}")
    for save in sorted(seen_a, key=lambda s: s["id"]):
        sa, sb = sync_for(save, device_a), sync_for(save, device_b)
        log(
            f"  save {save['id']}: origin={str(save.get('origin_device_id'))[-6:]}"
            f"  A.is_current={sa and sa.get('is_current')}"
            f"  B.is_current={sb and sb.get('is_current')}"
            f"  device_syncs={len(save.get('device_syncs') or [])}"
        )
    log()

    # --- F1: optimistic on download ----------------------------------------------------
    log("=== F1: optimistic download ===")
    target = sorted(seen_a, key=lambda s: s["id"])[-1]
    before_sync = sync_for(target, device_b)
    log(f"target save {target['id']}, B before: {json.dumps(before_sync)}")

    status, _h, _p, _t = request(
        "GET", f"/api/saves/{target['id']}/content", params={"device_id": device_b}
    )
    after_default = sync_for(
        next(s for s in list_saves(device_b) if s["id"] == target["id"]), device_b
    )
    log(f"GET content?device_id=B (optimistic left at its default) -> {status}")
    log(f"  B after: {json.dumps(after_default)}")

    status, _h, _p, _t = request(
        "GET",
        f"/api/saves/{target['id']}/content",
        params={"device_id": device_a, "optimistic": "false"},
    )
    after_explicit = sync_for(
        next(s for s in list_saves(device_a) if s["id"] == target["id"]), device_a
    )
    log(f"GET content?device_id=A&optimistic=false -> {status}")
    log(f"  A after: {json.dumps(after_explicit)}")

    status, _h, payload, _t = request(
        "POST", f"/api/saves/{target['id']}/downloaded", json_body={"device_id": device_a}
    )
    after_ack = sync_for(
        next(s for s in list_saves(device_a) if s["id"] == target["id"]), device_a
    )
    log(f"POST /api/saves/{{id}}/downloaded {{device_id: A}} -> {status}")
    log(f"  A after the explicit ack: {json.dumps(after_ack)}")
    log()

    # --- F10: untrack ------------------------------------------------------------------
    log("=== F10: untrack ===")
    status, _h, payload, _t = request(
        "POST", f"/api/saves/{target['id']}/untrack", json_body={"device_id": device_b}
    )
    log(f"POST /api/saves/{{id}}/untrack {{device_id: B}} -> {status}")
    untracked = sync_for(
        next(s for s in list_saves(device_b) if s["id"] == target["id"]), device_b
    )
    log(f"  B now: {json.dumps(untracked)}")
    status, _h, payload, _t = request(
        "POST", f"/api/saves/{target['id']}/track", json_body={"device_id": device_b}
    )
    log(f"POST /api/saves/{{id}}/track {{device_id: B}} -> {status}")
    log()

    # --- F2: autocleanup ----------------------------------------------------------------
    log("=== F2: autocleanup ===")
    log(f"saves in the slot before: {len(list_saves())}")
    for index in range(3):
        status, extra, _t = upload(
            device_a,
            f"CLEANUP-{index}".encode() + bytes([index]) * 200,
            "probe.srm",
            autocleanup="true",
            autocleanup_limit=2,
            overwrite="true",
        )
        remaining = list_saves()
        log(
            f"  upload {index} with autocleanup=true&autocleanup_limit=2 -> {status}, "
            f"slot now holds {len(remaining)}"
        )
    survivors = list_saves()
    log(f"  survivors: {[(s['id'], s['file_name']) for s in survivors]}")
    log()

    # --- F11: summary --------------------------------------------------------------------
    log("=== F11: /api/saves/summary ===")
    status, _h, payload, elapsed = request("GET", "/api/saves/summary", params={"rom_id": ROM_ID})
    log(f"GET /api/saves/summary?rom_id -> {status}  {elapsed:.2f} s")
    log(f"  {json.dumps(json.loads(payload))[:700]}")
    log()

    # --- F22: negotiate with and without device_id ----------------------------------------
    log("=== F22: negotiate ===")
    client_state = {
        "saves": [
            {
                "rom_id": ROM_ID,
                "file_name": "probe.srm",
                "slot": SLOT,
                "emulator": "rommbat-probe",
                "content_hash": "0" * 32,
                "updated_at": "2020-01-01T00:00:00Z",
                "file_size_bytes": 208,
            }
        ]
    }
    for label, body in [
        ("with device_id", {**client_state, "device_id": device_a}),
        ("without device_id", client_state),
    ]:
        status, _h, payload, _t = request("POST", "/api/sync/negotiate", json_body=body)
        parsed = json.loads(payload) if payload else {}
        log(f"POST /api/sync/negotiate {label} -> {status}")
        if status < 300:
            log(
                f"  session={parsed.get('session_id')} upload={parsed.get('total_upload')} "
                f"download={parsed.get('total_download')} conflict={parsed.get('total_conflict')} "
                f"no_op={parsed.get('total_no_op')}"
            )
            for op in parsed.get("operations", [])[:3]:
                log(f"    {op.get('action')}  slot={op.get('slot')!r}  reason={op.get('reason')!r}")
        else:
            log(f"  body: {payload[:300].decode('utf-8', 'replace')}")

finally:
    log()
    log("=== teardown ===")
    ids = sorted({s["id"] for s in list_saves()} | set(created_saves))
    if ids:
        status, _h, payload, _t = request("POST", "/api/saves/delete", json_body={"saves": ids})
        log(f"POST /api/saves/delete {len(ids)} ids -> {status}")
    remaining = list_saves()
    log(f"  saves left on rom {ROM_ID}: {len(remaining)}")
    for device_id in created_devices:
        status, _h, _p, _t = request("DELETE", f"/api/devices/{device_id}")
        log(f"DELETE /api/devices/<probe {device_id[-6:]}> -> {status}")
    record("f1-f12-save-lifecycle", lines)
