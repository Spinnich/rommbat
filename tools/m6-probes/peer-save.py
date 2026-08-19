"""Play the peer: put a different save into one rom/slot as a second device.

The awkward half of forcing a real conflict by hand: something has to change the server's copy
from a device that is not the one running RetroBat. Checked in because measurement 165 was taken
with it, and a measurement nothing can reproduce is not one.

    python peer-save.py <rom_id> <slot> [text]

Uses the SECOND registered device on the instance, so the save arrives from a device that is not
the one running RetroBat, and this device's sync record for the slot goes stale. That is what
makes the next flush answer 409 and record a conflict.
"""

from __future__ import annotations

import json as _json
import os
import sys
import time

from _common import request

if len(sys.argv) < 3:
    print(__doc__)
    raise SystemExit(2)

rom_id = int(sys.argv[1])
slot = sys.argv[2]
text = (sys.argv[3] if len(sys.argv) > 3 else f"peer copy written at {time.strftime('%H:%M:%S')}")

status, _h, payload, _e = request("GET", "/api/devices")
rows = _json.loads(payload)
rows = rows.get("items", rows) if isinstance(rows, dict) else rows
ids = [str(r["id"]) for r in rows if r.get("id")]

if len(ids) < 2:
    print("Needs at least two registered devices. Pair a throwaway one first.")
    raise SystemExit(2)

# Must not be the device running RetroBat, or the upload would refresh that device's own sync
# record and no conflict could form.
this_install = os.environ.get("ROMMBAT_THIS_DEVICE", "")
candidates = [i for i in ids if i != this_install]
if not candidates:
    print(f"Every registered device is {this_install}. Nothing can play the peer.")
    raise SystemExit(2)
peer = candidates[0]
body_text = text.encode()

boundary = "----rommbatpeer"
disposition = 'Content-Disposition: form-data; name="saveFile"; filename="peer.srm"'
body = b"".join([
    f"--{boundary}\r\n".encode(),
    disposition.encode() + b"\r\n",
    b"Content-Type: application/octet-stream\r\n\r\n",
    body_text,
    f"\r\n--{boundary}--\r\n".encode(),
])

status, _h, payload, _e = request(
    "POST",
    "/api/saves",
    params={
        "rom_id": rom_id,
        "slot": slot,
        "emulator": "libretro",
        "device_id": peer,
        "autocleanup": "true",
        "autocleanup_limit": "10",
        # The peer never synced this slot, so a plain upload is a 409. This is the same thing
        # the peer's own `saves resolve --keep-local` would send.
        "overwrite": "true",
    },
    raw_body=body,
    content_type=f"multipart/form-data; boundary={boundary}",
)

parsed = _json.loads(payload) if payload else {}
print(f"peer device : {peer}")
print(f"status      : {status}")
print(f"row id      : {parsed.get('id')}")
print(f"file_name   : {parsed.get('file_name')!r}")
print(f"content_hash: {parsed.get('content_hash')}")
print(f"payload     : {text!r}")
print()
print("Rows in this slot now:")
status, _h, payload, _e = request("GET", "/api/saves", params={"rom_id": rom_id})
rows = _json.loads(payload)
rows = rows.get("items", rows) if isinstance(rows, dict) else rows
for r in sorted([x for x in rows if x.get("slot") == slot], key=lambda x: x.get("id") or 0):
    print(
        f"  id={r.get('id')} name={r.get('file_name')!r} "
        f"hash={r.get('content_hash')} origin={r.get('origin_device_id')!r}"
    )
