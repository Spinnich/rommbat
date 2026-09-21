"""S4: what negotiate answers for a local save that changed but is older than the server's row.

Seen on a real install while restoring a save from a backup: the file's content differed from
what this device had last uploaded, its mtime was older than the server row's `updated_at`, and
the flush moved nothing in either direction. This asks the server directly, so the answer
separates negotiate's behaviour from the client's.

Four cases against one ROM and a throwaway slot:

  M1  local content differs, local mtime OLDER than the row this device uploaded
  M2  the same, local mtime NEWER (the ordinary edit)
  M3  the same as M1, but the row was uploaded by a peer this device never synced
  M4  local content IDENTICAL to the row, local mtime NEWER

M4 is the other direction, added when finding 259's reading of it stopped reproducing: an
emulator that rewrites a save with the same bytes moves the mtime and nothing else, and the
finding recorded negotiate answering `upload` for that on 5.3.0-alpha.3, which cost one
pointless upload per flush forever. It is here so the answer is the server's own rather than
inferred from what a flush did.

Writes to the instance. Everything it creates is deleted before it exits.

    python s4-older-mtime.py [rom_id]
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
from datetime import datetime, timedelta, timezone

import _common

SLOT = "probe-s4:battery"
EMULATOR = "libretro"
lines: list[str] = []


def say(text: str = "") -> None:
    lines.append(text)


def md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def multipart(filename: str, content: bytes) -> tuple[bytes, str]:
    boundary = "----rommbats4"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="saveFile"; filename="{filename}"\r\n'.encode(),
        b"Content-Type: application/octet-stream\r\n\r\n",
        content,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    return body, f"multipart/form-data; boundary={boundary}"


def call(method: str, path: str, **kwargs):
    status, _headers, payload, _elapsed = _common.request(method, path, **kwargs)
    try:
        parsed = json.loads(payload) if payload else None
    except ValueError:
        parsed = payload.decode("utf-8", "replace")
    return status, parsed


def upload(rom_id: int, content: bytes, *, device: str | None, slot: str = SLOT):
    body, ctype = multipart("Probe Game.srm", content)
    params = {"rom_id": rom_id, "emulator": EMULATOR, "slot": slot, "device_id": device}
    return call("POST", "/api/saves", params=params, raw_body=body, content_type=ctype)


def negotiate(device: str, rom_id: int, content: bytes, updated_at: str, slot: str = SLOT) -> str:
    save = {"rom_id": rom_id, "file_name": "Probe Game.srm", "slot": slot, "emulator": EMULATOR,
            "content_hash": md5(content), "updated_at": updated_at, "file_size_bytes": len(content)}
    status, body = call("POST", "/api/sync/negotiate",
                        json_body={"device_id": device, "saves": [save], "rom_ids": [rom_id]})
    if status != 200:
        return f"{status} {body}"
    picked = [o for o in body["operations"] if o.get("slot") == slot]
    if not picked:
        return "200, no operation for the probe slot"
    return "; ".join(f"{o['action']} save_id={o.get('save_id')} ({o['reason']})" for o in picked)


def main() -> None:
    if len(sys.argv) > 1:
        rom_id = int(sys.argv[1])
    else:
        _status, page = call("GET", "/api/roms", params={"limit": 1, "with_char_index": "false",
                                                          "with_filter_values": "false"})
        rom_id = page["items"][0]["id"]

    _status, heartbeat = call("GET", "/api/heartbeat")
    say(f"server {heartbeat['SYSTEM']['VERSION']}, rom {rom_id}, slot {SLOT}")

    status, device = call("POST", "/api/devices", json_body={
        "name": "rommbat-probe-s4", "platform": "windows", "client": "rommbat-probe",
        "hostname": f"probe-s4-{int(time.time())}", "allow_duplicate": True})
    this = device["device_id"]
    made: list[int] = []
    slot_e = SLOT.replace("probe-s4", "probe-s4e")

    try:
        uploaded = b"S4 the version this device uploaded"
        status, own = upload(rom_id, uploaded, device=this)
        made.append(own["id"])
        say(f"  upload by this device        {status} id={own['id']} updated_at={own.get('updated_at')}")
        say(f"  negotiate, in step           {negotiate(this, rom_id, uploaded, own['updated_at'])}")

        restored = b"S4 different content, restored from a backup"
        older = (datetime.now(timezone.utc) - timedelta(hours=1)).isoformat()
        newer = (datetime.now(timezone.utc) + timedelta(seconds=5)).isoformat()
        say()
        say("M1  content differs, local mtime older than the row this device uploaded")
        say(f"  negotiate                    {negotiate(this, rom_id, restored, older)}")
        say()
        say("M2  the same content, local mtime newer")
        say(f"  negotiate                    {negotiate(this, rom_id, restored, newer)}")

        say()
        say("M3  M1 against a row from a peer this device never synced")
        status, peer = upload(rom_id, b"S4 peer row", device=None, slot=slot_e)
        made.append(peer["id"])
        say(f"  peer upload, no device       {status} id={peer['id']}")
        say(f"  negotiate, local older       {negotiate(this, rom_id, restored, older, slot_e)}")

        say()
        say("M4  content identical to the row, local mtime newer (the emulator rewrite)")
        say(f"  negotiate                    {negotiate(this, rom_id, uploaded, newer)}")
    finally:
        say()
        if made:
            status, _ = call("POST", "/api/saves/delete", json_body={"saves": made})
            say(f"cleanup: deleted {len(made)} save row(s): {status}")
        status, _ = call("DELETE", f"/api/devices/{this}")
        say(f"cleanup: deleted the device: {status}")
        _common.record("s4-older-mtime", lines)


if __name__ == "__main__":
    main()
