"""S3: what 5.3.0-alpha.3's per-slot cap does to a device that was away (finding 11, #4540).

Read at tag 5.3.0-alpha.3, `backend/endpoints/saves.py` `add_save` prunes a slot past the
tighter of `MAX_SAVES_PER_SLOT` (env, default 50) and a client's `autocleanup_limit`, on every
slotted upload whether the client asked or not. `prune_slot` keeps the newest by `updated_at`
then `id`. RomMBat sends `autocleanup=true&autocleanup_limit=10` on every save upload, so 10 is
its own retention and always was; the server cap governs writers that ask for no cleanup, which
is what `upload()` below deliberately imitates.

`add_save` and `prune_slot` are byte-identical from alpha.3 through 5.3.0, the floor now: the
only save change in that span projects ids for `GET /api/saves/identifiers`, at beta.1. So the
run recorded against alpha.3 describes the code 5.3.0 ships, and a re-run would be a
re-measurement of the same source rather than of a change.

Two cases against one ROM and a throwaway slot, in the order they run:

  R1  this device uploads and negotiates, then a peer with no device puts 51 more versions in
      the slot. Is this device's version deleted, what does negotiate tell it, with its copy
      unchanged and with its copy edited, and is the edit's ordinary upload refused?
  R2  a PUT refreshes the oldest surviving version, then one more upload prunes. Does the
      prune rank on updated_at, so the refreshed row survives and the next oldest goes?

A slotted upload is named with a one-second timestamp and keyed on that name, so versions are
spaced a little over a second apart or they collapse into one row. About 55 uploads.

Writes to the instance. Everything it creates is deleted before it exits: the save rows, their
files and the device.

    python s3-slot-retention.py [rom_id]
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
from datetime import datetime, timezone

import _common

SLOT = "probe-s3:battery"
EMULATOR = "libretro"
PEER_VERSIONS = 51
lines: list[str] = []


def say(text: str = "") -> None:
    lines.append(text)


def md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def multipart(filename: str, content: bytes) -> tuple[bytes, str]:
    boundary = "----rommbats3"
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


def upload(rom_id: int, content: bytes, *, device: str | None):
    body, ctype = multipart("Probe Game.srm", content)
    params = {"rom_id": rom_id, "emulator": EMULATOR, "slot": SLOT, "device_id": device}
    return call("POST", "/api/saves", params=params, raw_body=body, content_type=ctype)


def negotiate(device: str, rom_id: int, content: bytes, updated_at: str):
    save = {"rom_id": rom_id, "file_name": "Probe Game.srm", "slot": SLOT, "emulator": EMULATOR,
            "content_hash": md5(content), "updated_at": updated_at, "file_size_bytes": len(content)}
    status, body = call("POST", "/api/sync/negotiate",
                        json_body={"device_id": device, "saves": [save], "rom_ids": [rom_id]})
    if status != 200:
        return f"{status} {body}"
    picked = [o for o in body["operations"] if o.get("slot") in (SLOT, None)]
    if not picked:
        return "200, no operation for the probe slot"
    return "; ".join(f"{o['action']} save_id={o.get('save_id')} ({o['reason']})" for o in picked)


def slot_rows(rom_id: int, device: str | None = None) -> list[dict]:
    status, body = call("GET", "/api/saves", params={"rom_id": rom_id, "slot": SLOT, "device_id": device})
    if status != 200:
        raise SystemExit(f"GET /api/saves answered {status}: {body}")
    return body


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def tick() -> None:
    time.sleep(1.1)


def main() -> None:
    if len(sys.argv) > 1:
        rom_id = int(sys.argv[1])
    else:
        _status, page = call("GET", "/api/roms", params={"limit": 1, "with_char_index": "false",
                                                          "with_filter_values": "false"})
        rom_id = page["items"][0]["id"]

    status, heartbeat = call("GET", "/api/heartbeat")
    say(f"server {heartbeat['SYSTEM']['VERSION']}, rom {rom_id}, slot {SLOT}")
    if slot_rows(rom_id):
        raise SystemExit("the probe slot already holds rows; delete them before running")

    status, device = call("POST", "/api/devices", json_body={
        "name": "rommbat-probe-s3", "platform": "windows", "client": "rommbat-probe",
        "hostname": f"probe-s3-{int(time.time())}", "allow_duplicate": True})
    this = device["device_id"]
    say(f"registered this device: {status}")

    made: set[int] = set()
    try:
        # ---- R1
        say()
        say(f"R1  this device's version, then {PEER_VERSIONS} from a peer with no device")
        mine = b"R1 this device's save"
        mine_mtime = now_iso()
        status, own = upload(rom_id, mine, device=this)
        made.add(own["id"])
        say(f"  upload by this device        {status} id={own['id']} file_name={own['file_name']!r}")
        say(f"  negotiate                    {negotiate(this, rom_id, mine, mine_mtime)}")

        for n in range(1, PEER_VERSIONS + 1):
            tick()
            status, peer = upload(rom_id, f"R1 peer version {n}".encode(), device=None)
            if status != 200:
                say(f"  peer upload {n} answered {status}: {peer}")
                break
            made.add(peer["id"])
        rows = slot_rows(rom_id, this)
        ids = [r["id"] for r in rows]
        say(f"  peer uploads made            {len([i for i in made if i != own['id']])}")
        say(f"  rows left in the slot        {len(rows)}")
        say(f"  this device's version kept   {own['id'] in ids}")
        say(f"  oldest surviving id          {min(ids)}, newest {max(ids)}")
        newest = max(rows, key=lambda r: (r.get('updated_at') or '', r['id']))
        say(f"  newest row's device_syncs    {newest.get('device_syncs')}")
        say(f"  negotiate, copy unchanged    {negotiate(this, rom_id, mine, mine_mtime)}")
        edited = b"R1 this device's save, edited while away"
        say(f"  negotiate, copy edited       {negotiate(this, rom_id, edited, now_iso())}")
        # What the flush does with that answer: an ordinary upload, no overwrite.
        status, sent = upload(rom_id, edited, device=this)
        if status == 200:
            made.add(sent["id"])
        say(f"  ordinary upload of the edit  {status} {('id=' + str(sent['id'])) if status == 200 else sent}")
        rows = slot_rows(rom_id, this)

        # ---- R2
        say()
        say("R2  PUT the oldest surviving version, then one more upload")
        oldest = min(rows, key=lambda r: (r.get('updated_at') or '', r['id']))
        second = sorted(rows, key=lambda r: (r.get('updated_at') or '', r['id']))[1]
        tick()
        body, ctype = multipart(oldest["file_name"], b"R2 rewritten in place")
        status, put = call("PUT", f"/api/saves/{oldest['id']}", raw_body=body, content_type=ctype)
        say(f"  PUT id={oldest['id']}                   {status} updated_at={put.get('updated_at') if isinstance(put, dict) else put}")
        tick()
        status, last = upload(rom_id, b"R2 one more", device=None)
        made.add(last["id"])
        say(f"  one more upload              {status} id={last['id']}")
        ids = [r["id"] for r in slot_rows(rom_id)]
        say(f"  rows left in the slot        {len(ids)}")
        say(f"  rewritten id={oldest['id']} kept          {oldest['id'] in ids}")
        say(f"  next oldest id={second['id']} kept        {second['id'] in ids}")
    finally:
        say()
        remaining = [r["id"] for r in slot_rows(rom_id)]
        if remaining:
            status, _ = call("POST", "/api/saves/delete", json_body={"saves": remaining})
            say(f"cleanup: deleted {len(remaining)} save row(s) left in the slot: {status}")
        say(f"cleanup: rows the server pruned itself: {len(made - set(remaining))}")
        status, _ = call("DELETE", f"/api/devices/{this}")
        say(f"cleanup: deleted the device: {status}")
        say(f"cleanup: slot empty afterwards: {not slot_rows(rom_id)}")
        _common.record("s3-slot-retention", lines)


if __name__ == "__main__":
    main()
