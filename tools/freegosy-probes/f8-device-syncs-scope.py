"""F8: is `device_syncs` a roster of every device, or only the one you asked about?

It matters because the two readings support opposite designs. If the array lists every
device, a client can see what its peers have done and reason about them. If it only ever
mirrors the `device_id` in the query, then the array is a single answer wearing a list's
clothing, and any code that iterates it looking for a peer finds nothing and concludes,
wrongly, that the peer never synced.

Two devices are given genuine, different sync records, then the same save is listed three
ways: as A, as B, and with no device_id at all.

**This writes to a real library** and deletes everything it creates.
"""

from __future__ import annotations

import json
import os

from _common import multipart, record, request

ROM_ID = int(os.environ.get("PROBE_ROM_ID", "1393"))
SLOT = "rommbat-freegosy-probe-3"

lines: list[str] = []
created_devices: list[str] = []
created_saves: list[int] = []


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
    body = json.loads(payload)
    device_id = str(body.get("device_id") or body["id"])
    created_devices.append(device_id)
    return device_id


def listing(device_id: str | None):
    params = {"rom_id": ROM_ID}
    if device_id:
        params["device_id"] = device_id
    _s, _h, payload, _t = request("GET", "/api/saves", params=params)
    body = json.loads(payload)
    return body if isinstance(body, list) else body.get("items", [])


try:
    device_a = make_device("A")
    device_b = make_device("B")
    short = {device_a: "A", device_b: "B"}
    lines.append(f"devices: A ends {device_a[-6:]}, B ends {device_b[-6:]}")

    body, ctype = multipart({"saveFile": ("probe.srm", b"ROSTER" + b"\x77" * 200)})
    status, _h, payload, _t = request(
        "POST",
        "/api/saves",
        params={"rom_id": ROM_ID, "slot": SLOT, "emulator": "rommbat-probe", "device_id": device_a},
        raw_body=body,
        content_type=ctype,
    )
    save = json.loads(payload)
    created_saves.append(save["id"])
    lines.append(f"A uploads -> {status}, save {save['id']}")

    status, _h, _p, _t = request(
        "GET", f"/api/saves/{save['id']}/content", params={"device_id": device_b}
    )
    lines.append(f"B downloads it (optimistic default) -> {status}")
    lines.append("")
    lines.append("Both devices now have a genuine sync record. The same save, listed three ways:")
    lines.append("")

    for label, device_id in [("device_id=A", device_a), ("device_id=B", device_b), ("no device_id", None)]:
        for row in listing(device_id):
            if row["id"] != save["id"]:
                continue
            syncs = row.get("device_syncs") or []
            lines.append(f"  GET /api/saves?rom_id&{label}")
            lines.append(f"    device_syncs holds {len(syncs)} entr{'y' if len(syncs) == 1 else 'ies'}")
            for entry in syncs:
                who = short.get(entry["device_id"], "someone else")
                lines.append(
                    f"      {who}: is_current={entry['is_current']} "
                    f"is_untracked={entry['is_untracked']} last_synced_at={entry['last_synced_at']}"
                )
            lines.append(f"    origin_device_id: {short.get(str(row.get('origin_device_id')), row.get('origin_device_id'))}")
            lines.append("")

finally:
    lines.append("=== teardown ===")
    for save_id in sorted({s["id"] for s in listing(None)} | set(created_saves)):
        status, _h, _p, _t = request("POST", "/api/saves/delete", json_body={"saves": [save_id]})
        lines.append(f"  delete save {save_id} -> {status}")
    lines.append(f"  saves left on rom {ROM_ID}: {len(listing(None))}")
    for device_id in created_devices:
        status, _h, _p, _t = request("DELETE", f"/api/devices/{device_id}")
        lines.append(f"  delete device <{device_id[-6:]}> -> {status}")
    record("f8-device-syncs-scope", lines)
