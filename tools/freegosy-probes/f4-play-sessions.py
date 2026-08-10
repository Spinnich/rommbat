"""F4: is POST /api/play-sessions a standalone ingest, and what shape does it take?

docs/PLAN.md routes play sessions through POST /api/sync/sessions/{id}/complete, which ties
flushing playtime to opening a sync session. If the standalone endpoint works on its own,
an offline agent can flush playtime without negotiating saves first, which is a different
and simpler shape for the outbox.

Freegosy posts a **bare array** with device_id on each entry. The pinned 5.1.0 schema
declares an envelope, `{device_id, sessions: [...]}`, with device_id outside the entries.
Only one of those can be right.

Also checks the plan's "at most 100 per call" claim, whether rom_id is genuinely optional
as the schema says, and whether replaying the same session dedups, which is what makes a
failed flush safe to retry.

**This writes to a real library** and deletes every session it creates.
"""

from __future__ import annotations

import json
import os
from datetime import UTC, datetime, timedelta

from _common import record, request

ROM_ID = int(os.environ.get("PROBE_ROM_ID", "1393"))

lines: list[str] = []
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
    body = json.loads(payload)
    device_id = str(body.get("device_id") or body["id"])
    created_devices.append(device_id)
    return device_id


def entry(minutes_ago: int, duration_s: int = 600, rom_id: int | None = ROM_ID) -> dict:
    start = datetime.now(UTC) - timedelta(minutes=minutes_ago)
    end = start + timedelta(seconds=duration_s)
    payload = {
        "start_time": start.replace(microsecond=0).isoformat(),
        "end_time": end.replace(microsecond=0).isoformat(),
        "duration_ms": duration_s * 1000,
    }
    if rom_id is not None:
        payload["rom_id"] = rom_id
    return payload


def post(body, label: str):
    status, _h, payload, elapsed = request("POST", "/api/play-sessions", json_body=body)
    log(f"POST /api/play-sessions  {label} -> {status}  {elapsed:.2f} s")
    text = payload[:400].decode("utf-8", "replace") if payload else ""
    if text:
        log(f"  {text}")
    return status, payload


def sessions_now(scoped: bool = True):
    # Unscoped for teardown: a session posted without a rom_id is invisible to ?rom_id=,
    # so filtering the listing that way strands it.
    params = {"rom_id": ROM_ID} if scoped else None
    status, _h, payload, _t = request("GET", "/api/play-sessions", params=params)
    if status >= 300:
        return status, []
    body = json.loads(payload) if payload else []
    return status, body if isinstance(body, list) else body.get("items", [])


try:
    device = make_device("PS")
    log(f"throwaway device ends {device[-6:]}")
    status, before = sessions_now()
    log(f"GET /api/play-sessions?rom_id -> {status}, {len(before)} existing sessions")
    log()

    log("=== the two candidate shapes ===")
    post([{**entry(60), "device_id": device}], "bare array, Freegosy's shape")
    log()
    post({"device_id": device, "sessions": [entry(50)]}, "envelope, the 5.1.0 schema's shape")
    log()

    status, after = sessions_now()
    log(f"sessions on the rom now: {len(after)}")
    for session in after[:4]:
        log(
            f"  id={session.get('id')} duration_ms={session.get('duration_ms')} "
            f"device={str(session.get('device_id'))[-6:]} start={session.get('start_time')}"
        )
    log()

    log("=== replay: is a retried flush idempotent? ===")
    repeated = {"device_id": device, "sessions": [entry(50)]}
    post(repeated, "the identical envelope again")
    status, replayed = sessions_now()
    log(f"  sessions after the replay: {len(replayed)} (was {len(after)})")
    log()

    log("=== is rom_id really optional? ===")
    post({"device_id": device, "sessions": [entry(40, rom_id=None)]}, "no rom_id on the entry")
    log()

    log("=== the plan's 'at most 100 per call' ===")
    post({"device_id": device, "sessions": [entry(30 + n, 60) for n in range(101)]}, "101 entries")
    log()

    log("=== end_time before start_time ===")
    bad = entry(20)
    bad["start_time"], bad["end_time"] = bad["end_time"], bad["start_time"]
    post({"device_id": device, "sessions": [bad]}, "end_time earlier than start_time")

finally:
    log()
    log("=== teardown ===")
    status, remaining = sessions_now(scoped=False)
    log(f"  sessions to remove: {len(remaining)}")
    for session in remaining:
        status, _h, _p, _t = request("DELETE", f"/api/play-sessions/{session['id']}")
        log(f"    delete session {session['id']} -> {status}")
    _status, left = sessions_now(scoped=False)
    log(f"  sessions left: {len(left)}")
    for device_id in created_devices:
        status, _h, _p, _t = request("DELETE", f"/api/devices/{device_id}")
        log(f"  delete device <{device_id[-6:]}> -> {status}")
    record("f4-play-sessions", lines)
