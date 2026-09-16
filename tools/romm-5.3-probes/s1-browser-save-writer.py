"""S1: what the browser's save writer does to a slot this client negotiates on (#170).

5.3.0's `emulatorjs.auto_save_sync` uploads on EmulatorJS's save interval rather than at
Save & Quit. It adds no route. Read at tag 5.3.0-alpha.2, `frontend/src/views/Player/
EmulatorJS/utils.ts` `saveSave` makes one of two calls, and the flag decides how often:

  a save is loaded   PUT  /api/saves/{loaded id}   device_id only, no slot, no overwrite
  none is loaded     POST /api/saves?rom_id&emulator=<core>&device_id, no slot

So the browser session is replayed on the wire rather than driven in a browser: the server
cannot tell the two apart, and the server's answer is the whole question. `device_id` is
omitted on the browser's calls because `current_device_id` is unset for an ordinary web
login, which is the case auto_save_sync meets.

Four cases, each against one ROM and one throwaway slot, played by a device registered here:

  A  the browser writes over the row this device uploaded, and this device did nothing
  B  the same, and this device also changed its copy
  C  this device supersedes the row, then the browser writes over the older one again
  D  no save was loaded, so the browser makes a null-slot row and keeps writing into it

Writes to the instance. Everything it creates is deleted before it exits: the save rows,
their files and both devices.

    python s1-browser-save-writer.py [rom_id]
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
from datetime import datetime, timezone

import _common

SLOT = "probe-s1:battery"
EMULATOR = "libretro"
CORE = "fceumm"
lines: list[str] = []


def say(text: str = "") -> None:
    lines.append(text)


def md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def multipart(filename: str, content: bytes) -> tuple[bytes, str]:
    boundary = "----rommbats1"
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


def upload(rom_id: int, content: bytes, *, slot: str | None, device: str | None,
           overwrite: bool = False, emulator: str = EMULATOR, name: str = "Probe Game.srm"):
    body, ctype = multipart(name, content)
    params = {"rom_id": rom_id, "emulator": emulator, "slot": slot, "device_id": device}
    if overwrite:
        params["overwrite"] = "true"
    return call("POST", "/api/saves", params=params, raw_body=body, content_type=ctype)


def browser_put(save_id: int, file_name: str, content: bytes):
    # utils.ts sends the row's own file_name back, and no device_id without a current device.
    body, ctype = multipart(file_name, content)
    return call("PUT", f"/api/saves/{save_id}", raw_body=body, content_type=ctype)


def negotiate(device: str, rom_id: int, saves: list[dict]):
    return call("POST", "/api/sync/negotiate",
                json_body={"device_id": device, "saves": saves, "rom_ids": [rom_id]})


def client_save(rom_id: int, content: bytes, updated_at: str, slot: str = SLOT) -> dict:
    return {"rom_id": rom_id, "file_name": "Probe Game.srm", "slot": slot,
            "emulator": EMULATOR, "content_hash": md5(content), "updated_at": updated_at,
            "file_size_bytes": len(content)}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def row(save: dict) -> str:
    return (f"id={save['id']} slot={save.get('slot')!r} file_name={save['file_name']!r} "
            f"hash={save.get('content_hash')} updated_at={save.get('updated_at')}")


def ops(result, slot: str = SLOT) -> str:
    status, body = result
    if status != 200:
        return f"{status} {body}"
    picked = [o for o in body["operations"] if o.get("slot") in (slot, None)]
    if not picked:
        return "200, no operation for the probe slot"
    return "; ".join(
        f"{o['action']} save_id={o.get('save_id')} hash={o.get('server_content_hash')} "
        f"({o['reason']})" for o in picked)


def tick() -> None:
    # updated_at is stored at one-second resolution, and every comparison here is on it.
    time.sleep(2.1)


def main() -> None:
    if len(sys.argv) > 1:
        rom_id = int(sys.argv[1])
    else:
        status, page = call("GET", "/api/roms", params={"limit": 1, "with_char_index": "false",
                                                         "with_filter_values": "false"})
        rom_id = page["items"][0]["id"]

    made_saves: list[int] = []
    made_devices: list[str] = []

    status, heartbeat = call("GET", "/api/heartbeat")
    say(f"server {heartbeat['SYSTEM']['VERSION']}, rom {rom_id}, slot {SLOT}")

    status, device = call("POST", "/api/devices", json_body={
        "name": "rommbat-probe-s1", "platform": "windows", "client": "rommbat-probe",
        "hostname": f"probe-s1-{int(time.time())}", "allow_duplicate": True})
    this = device["device_id"]
    made_devices.append(this)
    say(f"registered this device: {status}")

    try:
        # ---- A: browser overwrites in place, this device unchanged
        say()
        say("A  browser PUT over this device's row, local copy unchanged")
        a0 = b"A0 local and uploaded"
        local_mtime = now_iso()
        tick()
        status, s1 = upload(rom_id, a0, slot=SLOT, device=this)
        made_saves.append(s1["id"])
        say(f"  upload by this device       {status} {row(s1)}")
        say(f"  negotiate                    {ops(negotiate(this, rom_id, [client_save(rom_id, a0, local_mtime)]))}")
        tick()
        b1 = b"B1 browser tick one"
        status, p1 = browser_put(s1["id"], s1["file_name"], b1)
        say(f"  browser PUT                  {status} {row(p1)}")
        say(f"  same id, same name, same slot: {p1['id'] == s1['id']}, "
            f"{p1['file_name'] == s1['file_name']}, {p1.get('slot') == s1.get('slot')}")
        say(f"  negotiate, local unchanged   {ops(negotiate(this, rom_id, [client_save(rom_id, a0, local_mtime)]))}")

        # ---- B: both sides changed
        say()
        say("B  the same row, and this device also changed its copy")
        a1 = b"A1 local edit after the browser wrote"
        tick()
        edited = now_iso()
        say(f"  negotiate, local changed     {ops(negotiate(this, rom_id, [client_save(rom_id, a1, edited)]))}")
        status, refused = upload(rom_id, a1, slot=SLOT, device=this)
        say(f"  ordinary upload              {status} {refused if status != 200 else row(refused)}")
        if status == 200:
            made_saves.append(refused["id"])

        # ---- C: keep-local, then the browser writes into the row it still holds
        say()
        say("C  this device supersedes the row, then the browser ticks into the older one")
        tick()
        status, s2 = upload(rom_id, a1, slot=SLOT, device=this, overwrite=True)
        made_saves.append(s2["id"])
        say(f"  keep-local upload            {status} {row(s2)}")
        say(f"  negotiate                    {ops(negotiate(this, rom_id, [client_save(rom_id, a1, edited)]))}")
        tick()
        b2 = b"B2 browser tick two, into the row it loaded"
        status, p2 = browser_put(s1["id"], s1["file_name"], b2)
        say(f"  browser PUT to the old row   {status} {row(p2)}")
        say(f"  negotiate, local unchanged   {ops(negotiate(this, rom_id, [client_save(rom_id, a1, edited)]))}")
        status, listed = call("GET", "/api/saves", params={"rom_id": rom_id, "slot": SLOT})
        say("  slot rows, as listed:")
        for save in listed:
            say(f"    {row(save)}")

        # ---- E: C again, where the superseded row came from a device this one never synced
        say()
        say("E  C, but the row the browser revives was written by a peer this device never synced")
        slot_e = SLOT.replace("probe-s1", "probe-s1e")
        local_e = b"E local copy"
        local_e_mtime = now_iso()
        tick()
        status, peer = upload(rom_id, b"E peer row", slot=slot_e, device=None)
        made_saves.append(peer["id"])
        say(f"  peer upload, no device       {status} {row(peer)}")
        status, refused = upload(rom_id, local_e, slot=slot_e, device=this)
        say(f"  ordinary upload              {status} {refused if status != 200 else row(refused)}")
        if status == 200:
            made_saves.append(refused["id"])
        tick()
        status, kept = upload(rom_id, local_e, slot=slot_e, device=this, overwrite=True)
        made_saves.append(kept["id"])
        say(f"  keep-local upload            {status} {row(kept)}")
        say(f"  negotiate                    "
            f"{ops(negotiate(this, rom_id, [client_save(rom_id, local_e, local_e_mtime, slot_e)]), slot_e)}")
        tick()
        status, pe = browser_put(peer["id"], peer["file_name"], b"E browser tick into the peer row")
        say(f"  browser PUT to the peer row  {status} {row(pe)}")
        say(f"  negotiate, local unchanged   "
            f"{ops(negotiate(this, rom_id, [client_save(rom_id, local_e, local_e_mtime, slot_e)]), slot_e)}")

        # ---- D: no save loaded
        say()
        say("D  no save loaded: the browser's first tick POSTs with no slot")
        status, n1 = upload(rom_id, b"N1 browser, nothing loaded", slot=None, device=None,
                            emulator=CORE, name="Probe Game [browser].srm")
        made_saves.append(n1["id"])
        say(f"  browser POST                 {status} {row(n1)}")
        for n in range(2, 4):
            tick()
            status, pn = browser_put(n1["id"], n1["file_name"], f"N{n} browser tick".encode())
            say(f"  browser PUT {n}                {status} {row(pn)}")
        status, all_rows = call("GET", "/api/saves", params={"rom_id": rom_id})
        nulls = [s for s in all_rows if s.get("slot") is None and s["id"] in made_saves]
        say(f"  null-slot rows this case made: {len(nulls)}")
        status, fresh = call("POST", "/api/devices", json_body={
            "name": "rommbat-probe-s1-fresh", "platform": "windows", "client": "rommbat-probe",
            "hostname": f"probe-s1-fresh-{int(time.time())}", "allow_duplicate": True})
        made_devices.append(fresh["device_id"])
        status, empty = negotiate(fresh["device_id"], rom_id, [])
        offered = [o["save_id"] for o in empty.get("operations", [])] if status == 200 else empty
        say(f"  empty negotiate, fresh device, offers save ids {offered}; "
            f"null-slot row {n1['id']} offered: {n1['id'] in offered if isinstance(offered, list) else '?'}")
    finally:
        say()
        if made_saves:
            status, _ = call("POST", "/api/saves/delete", json_body={"saves": made_saves})
            say(f"cleanup: deleted {len(made_saves)} save row(s): {status}")
        for device_id in made_devices:
            status, _ = call("DELETE", f"/api/devices/{device_id}")
            say(f"cleanup: deleted a device: {status}")
        _common.record("s1-browser-save-writer", lines)


if __name__ == "__main__":
    main()
