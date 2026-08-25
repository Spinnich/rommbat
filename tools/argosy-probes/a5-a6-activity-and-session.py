"""A5 and A6: the activity heartbeat, and what completing a session twice answers.

A5 POST /api/activity/heartbeat is declared in the pinned 5.1.0 schema and RomMBat has
   never mentioned it in the plan, the skill or the client. Argosy posts it during play
   and says the server holds it for 90 seconds. Established here, then cleared.

A6 Argosy drops its local session rows on any 4xx from
   POST /api/sync/sessions/{id}/complete, on the reasoning that a session the server has
   already finalized is a zombie if the client keeps retrying. RomMBat never retries: it
   awaits the call once and catches only RomMUnreachableException, so every other HTTP
   failure to close a session is swallowed and the pass still reports success. What a
   second complete actually answers decides which status that swallowing is hiding.

THIS PROBE WRITES. It posts one activity heartbeat and deletes it, and it opens one sync
session with an empty inventory and completes it twice. It uploads no save and touches no
save row. Run it against a throwaway instance if that matters to you.
"""

from __future__ import annotations

import json
import sys

import _common


def pick_rom() -> int:
    status, body, _ = _common.get_json(
        "/api/roms", params={"limit": 1, "order_by": "id", "order_dir": "asc"}
    )
    if status != 200 or not body.get("items"):
        raise SystemExit(f"could not pick a rom: HTTP {status}")
    return body["items"][0]["id"]


def pick_device() -> tuple[str, str]:
    status, body, _ = _common.get_json("/api/devices")
    if status != 200 or not body:
        raise SystemExit(f"could not list devices: HTTP {status}")
    device = body[0]
    return device["id"], device.get("name") or "?"


def probe_a5(lines: list[str], rom_id: int, device_id: str) -> None:
    lines.append("## A5: POST /api/activity/heartbeat")
    lines.append("")
    payload = {"rom_id": rom_id, "device_id": device_id}
    lines.append(f"request body: {json.dumps({'rom_id': rom_id, 'device_id': '<device>'})}")
    status, _headers, body, elapsed = _common.request(
        "POST", "/api/activity/heartbeat", json_body=payload
    )
    lines.append(f"-> {status} in {elapsed:.2f}s")
    lines.append(f"   body: {body[:400].decode('utf-8', 'replace')}")
    lines.append("")

    for path in ("/api/activity", f"/api/activity/rom/{rom_id}"):
        status, parsed, elapsed = _common.get_json(path)
        rendered = json.dumps(parsed)[:600] if parsed is not None else "-"
        lines.append(f"GET {path} -> {status} in {elapsed:.2f}s")
        lines.append(f"   {rendered}")
    lines.append("")

    # device_id rides in the query string here, not in the body the POST takes. Sent as a
    # body it answers 422 naming the missing query field.
    status, _headers, body, elapsed = _common.request(
        "DELETE", "/api/activity/heartbeat", params={"device_id": device_id}
    )
    lines.append(f"DELETE /api/activity/heartbeat?device_id= -> {status} in {elapsed:.2f}s")
    lines.append(f"   body: {body[:300].decode('utf-8', 'replace')}")

    status, parsed, _ = _common.get_json("/api/activity")
    lines.append(f"GET /api/activity after the delete -> {status}")
    lines.append(f"   {json.dumps(parsed)[:400] if parsed is not None else '-'}")
    lines.append("")


def probe_a6(lines: list[str], device_id: str) -> None:
    lines.append("## A6: completing one sync session twice")
    lines.append("")
    payload = {"device_id": device_id, "saves": []}
    status, headers, body, elapsed = _common.request(
        "POST", "/api/sync/negotiate", json_body=payload
    )
    lines.append(f"POST /api/sync/negotiate with an empty inventory -> {status} in {elapsed:.2f}s")
    if status != 200:
        lines.append(f"   body: {body[:400].decode('utf-8', 'replace')}")
        lines.append("   no session to complete; A6 unresolved")
        lines.append("")
        return
    negotiated = json.loads(body)
    session_id = negotiated.get("session_id")
    lines.append(
        f"   session_id={session_id} operations={len(negotiated.get('operations') or [])} "
        f"upload={negotiated.get('total_upload')} download={negotiated.get('total_download')} "
        f"conflict={negotiated.get('total_conflict')} no_op={negotiated.get('total_no_op')}"
    )
    lines.append("")
    if session_id is None:
        lines.append("   server returned no session_id; A6 unresolved")
        lines.append("")
        return

    complete_body = {"operations_completed": 0, "operations_failed": 0, "play_sessions": []}
    for attempt in (1, 2, 3):
        status, _headers, body, elapsed = _common.request(
            "POST", f"/api/sync/sessions/{session_id}/complete", json_body=complete_body
        )
        lines.append(f"complete attempt {attempt} -> {status} in {elapsed:.2f}s")
        lines.append(f"   body: {body[:400].decode('utf-8', 'replace')}")
    lines.append("")

    status, parsed, _ = _common.get_json(f"/api/sync/sessions/{session_id}")
    lines.append(f"GET /api/sync/sessions/{{id}} after -> {status}")
    lines.append(f"   {json.dumps(parsed)[:500] if parsed is not None else '-'}")
    lines.append("")


def main() -> int:
    lines: list[str] = []
    status, heartbeat, _ = _common.get_json("/api/heartbeat")
    lines.append(f"server version: {(heartbeat or {}).get('SYSTEM', {}).get('VERSION', 'unknown')}")
    rom_id = pick_rom()
    device_id, device_name = pick_device()
    lines.append(f"rom under test: {rom_id}")
    lines.append(f"device under test: <device> ({device_name})")
    lines.append("")
    probe_a5(lines, rom_id, device_id)
    probe_a6(lines, device_id)
    _common.record("a5-a6-activity-and-session", lines)
    return 0


if __name__ == "__main__":
    sys.exit(main())
