"""F1 and F12, with the controls the first lifecycle pass lacked.

The lifecycle probe answered F12 by accident and F1 without a control:

  - The 409 fired for the device that had **never** synced the save, not for the one whose
    record was stale, so the body it captured was the wrong response.
  - The `optimistic=false` arm was pointed at a device that was already current, so it
    could not show whether the parameter suppresses anything.

This runs three devices. A uploads. B, with no sync record at all, uploads different
content and its 409 body is quoted verbatim. C, also with no record, downloads with
`optimistic=false` and is checked before and after the explicit `/downloaded` ack, which
is the only way to see what the parameter actually suppresses.

**This writes to a real library** and deletes everything it creates. Note the teardown
deletes save ids one at a time: `POST /api/saves/delete` answers 404 for the whole batch
if any single id in it has already gone, which autocleanup can arrange.
"""

from __future__ import annotations

import json
import os

from _common import multipart, record, request

ROM_ID = int(os.environ.get("PROBE_ROM_ID", "1393"))
SLOT = "rommbat-freegosy-probe-2"

lines: list[str] = []
created_saves: list[int] = []
created_devices: list[str] = []


def log(text: str = "") -> None:
    lines.append(text)


def make_device(label: str) -> str:
    _s, _h, payload, _t = request(
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
    # POST answers {device_id, name, created_at}; GET /api/devices keys the same value "id".
    body = json.loads(payload)
    device_id = str(body.get("device_id") or body["id"])
    created_devices.append(device_id)
    return device_id


def upload(device_id: str, content: bytes, **extra):
    body, ctype = multipart({"saveFile": ("probe.srm", content)})
    params = {"rom_id": ROM_ID, "slot": SLOT, "emulator": "rommbat-probe", "device_id": device_id}
    params.update(extra)
    status, _h, payload, _t = request(
        "POST", "/api/saves", params=params, raw_body=body, content_type=ctype
    )
    try:
        parsed = json.loads(payload)
    except ValueError:
        parsed = {"_raw": payload[:600].decode("utf-8", "replace")}
    if status < 300 and isinstance(parsed, dict) and parsed.get("id"):
        created_saves.append(parsed["id"])
    return status, parsed


def sync_for(save_id: int, device_id: str):
    _s, _h, payload, _t = request(
        "GET", "/api/saves", params={"rom_id": ROM_ID, "device_id": device_id}
    )
    body = json.loads(payload)
    items = body if isinstance(body, list) else body.get("items", [])
    for save in items:
        if save["id"] == save_id:
            for entry in save.get("device_syncs") or []:
                if entry.get("device_id") == device_id:
                    return entry
            return "no device_syncs entry for this device"
    return "save not listed"


try:
    device_a, device_b, device_c = (make_device(x) for x in "ABC")
    log(f"three throwaway devices created, ids ending {device_a[-6:]} {device_b[-6:]} {device_c[-6:]}")
    log()

    status, save = upload(device_a, b"BASE" + b"\xaa" * 200)
    log(f"A uploads -> {status}, save {save.get('id')}, file_name {save.get('file_name')!r}")
    save_id = save["id"]
    log(f"  download_path as served: {save.get('download_path')!r}")
    log()

    # --- F12: the 409 that actually fires ------------------------------------------------
    log("=== F12: the conflict body, from the device with no sync record ===")
    status, conflict = upload(device_b, b"FROM-B" + b"\xbb" * 200)
    log(f"B uploads different content into the same slot -> {status}")
    log("  body, verbatim:")
    for line in json.dumps(conflict, indent=2).splitlines():
        log(f"    {line}")
    log()

    log("  and the same upload retried with overwrite=true:")
    status, forced = upload(device_b, b"FROM-B" + b"\xbb" * 200, overwrite="true")
    log(f"    -> {status}, save {forced.get('id')}, file_name {forced.get('file_name')!r}")
    log(f"    is this a new row or the old one? old={save_id} new={forced.get('id')}")
    log()

    # --- F1: does optimistic=false suppress the sync record? ------------------------------
    log("=== F1: optimistic, with a device that has never synced ===")
    log(f"C before any download:            {json.dumps(sync_for(save_id, device_c))}")

    status, _h, payload, _t = request(
        "GET",
        f"/api/saves/{save_id}/content",
        params={"device_id": device_c, "optimistic": "false"},
    )
    log(f"GET content?device_id=C&optimistic=false -> {status}, {len(payload)} bytes")
    log(f"C after optimistic=false download: {json.dumps(sync_for(save_id, device_c))}")

    status, _h, _p, _t = request(
        "POST", f"/api/saves/{save_id}/downloaded", json_body={"device_id": device_c}
    )
    log(f"POST /api/saves/{{id}}/downloaded {{device_id: C}} -> {status}")
    log(f"C after the explicit ack:          {json.dumps(sync_for(save_id, device_c))}")
    log()

    log("  the same sequence on a fresh device, leaving optimistic at its default:")
    device_d = make_device("D")
    log(f"D before any download:            {json.dumps(sync_for(save_id, device_d))}")
    status, _h, payload, _t = request(
        "GET", f"/api/saves/{save_id}/content", params={"device_id": device_d}
    )
    log(f"GET content?device_id=D (default) -> {status}, {len(payload)} bytes")
    log(f"D after the download:             {json.dumps(sync_for(save_id, device_d))}")

finally:
    log()
    log("=== teardown ===")
    _s, _h, payload, _t = request("GET", "/api/saves", params={"rom_id": ROM_ID})
    body = json.loads(payload) if payload else []
    items = body if isinstance(body, list) else body.get("items", [])
    ids = sorted({s["id"] for s in items} | set(created_saves))
    for save_id in ids:
        status, _h, _p, _t = request("POST", "/api/saves/delete", json_body={"saves": [save_id]})
        log(f"  delete save {save_id} -> {status}")
    _s, _h, payload, _t = request("GET", "/api/saves", params={"rom_id": ROM_ID})
    body = json.loads(payload) if payload else []
    left = body if isinstance(body, list) else body.get("items", [])
    log(f"  saves left on rom {ROM_ID}: {len(left)}")
    for device_id in created_devices:
        status, _h, _p, _t = request("DELETE", f"/api/devices/{device_id}")
        log(f"  delete device <{device_id[-6:]}> -> {status}")
    record("f1-f12b-conflict-and-optimistic", lines)
