"""S2: what a memory card record and a card version hold (#169).

5.3.0 adds `/api/memory-cards` for streaming sessions. Read at tag 5.3.0-alpha.2, a card is
scoped by `(user, emulator)` and not by ROM, and its data is a history of versions. Three
questions were parked on a live instance and are asked here without a streaming broker, since
the card routes do not need one:

  what a card record holds          create one and read it back
  whether a version is whole        upload two versions and download the second
  whether a raw card is accepted    a bare Dolphin `SRAM.USA.raw`, which is the class D
                                    container #82 is about, and a zip holding one

Also: whether an identical upload deduplicates (source says the upload route does not), and
what a `.gci` zip reports, since the streaming claim summarises cards by `.gci` game code.

Writes to the instance. The card, and every version with it, is deleted before it exits.

    python s2-memory-card-record.py
"""

from __future__ import annotations

import hashlib
import io
import json
import zipfile

import _common

lines: list[str] = []


def say(text: str = "") -> None:
    lines.append(text)


def call(method: str, path: str, **kwargs):
    status, headers, payload, _elapsed = _common.request(method, path, **kwargs)
    try:
        parsed = json.loads(payload) if payload else None
    except ValueError:
        parsed = payload
    return status, headers, parsed


def multipart(filename: str, content: bytes) -> tuple[bytes, str]:
    boundary = "----rommbats2"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="cardFile"; filename="{filename}"\r\n'.encode(),
        b"Content-Type: application/octet-stream\r\n\r\n",
        content,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    return body, f"multipart/form-data; boundary={boundary}"


def zipped(members: dict[str, bytes]) -> bytes:
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in members.items():
            archive.writestr(name, data)
    return buffer.getvalue()


def upload(card_id: int, filename: str, content: bytes):
    body, ctype = multipart(filename, content)
    return call("POST", f"/api/memory-cards/{card_id}/versions", raw_body=body, content_type=ctype)


def keys(value) -> str:
    return ", ".join(f"{k}={value[k]!r}" for k in sorted(value)) if isinstance(value, dict) else repr(value)


def main() -> None:
    status, _h, heartbeat = call("GET", "/api/heartbeat")
    say(f"server {heartbeat['SYSTEM']['VERSION']}")

    status, _h, card = call("POST", "/api/memory-cards",
                            json_body={"name": "rommbat-probe-s2", "emulator": "dolphin"})
    say(f"create card: {status}")
    if status != 200:
        say(f"  {card}")
        _common.record("s2-memory-card-record", lines)
        return
    card_id = card["id"]
    say(f"  record: {keys(card)}")

    try:
        say()
        status, _h, body = call("GET", f"/api/memory-cards/{card_id}/content")
        say(f"content of a card never synced: {status} {body}")

        # Dolphin's GCI names are <maker>-<gamecode>-<comment>.gci, which is what summarize_card reads.
        one = zipped({"01-GALE-SuperSmashBros0110290334.gci": b"\x01" * 8128})
        two = zipped({
            "01-GALE-SuperSmashBros0110290334.gci": b"\x02" * 8128,
            "8P-GM4E-MarioKart Double Dash!!.gci": b"\x03" * 16320,
        })
        raw = b"\x00" * (16 * 1024 * 1024 // 8)

        say()
        for label, filename, content in [
            ("gci zip, one game", "card.zip", one),
            ("gci zip, two games", "card.zip", two),
            ("the same two-game zip again", "card.zip", two),
            ("bare SRAM.USA.raw", "SRAM.USA.raw", raw),
            ("zip holding SRAM.USA.raw", "card.zip", zipped({"SRAM.USA.raw": raw})),
        ]:
            status, _h, version = upload(card_id, filename, content)
            if status == 200:
                say(f"upload {label}: {status} {keys(version)}")
            else:
                say(f"upload {label}: {status} {version}")

        say()
        status, _h, versions = call("GET", f"/api/memory-cards/{card_id}/versions")
        say(f"versions listed: {status}, {len(versions)} row(s), newest first")
        for version in versions:
            say(f"  id={version['id']} file_name={version['file_name']!r} "
                f"bytes={version['file_size_bytes']} hash={version.get('content_hash')}")

        second = next((v for v in reversed(versions) if v["file_size_bytes"] == len(two)), None)
        if second is not None:
            status, _h, payload = call("GET", f"/api/memory-cards/versions/{second['id']}/content")
            if status == 200 and isinstance(payload, bytes):
                with zipfile.ZipFile(io.BytesIO(payload)) as archive:
                    names = sorted(archive.namelist())
                say()
                say(f"download of the two-game version: {status}, {len(payload)} B, "
                    f"byte-identical to what was sent: {payload == two}, members {names}")
                say(f"  md5 of the bytes {hashlib.md5(payload).hexdigest()}, "
                    f"stored content_hash {second.get('content_hash')}")

        status, _h, now = call("GET", f"/api/memory-cards/{card_id}")
        say()
        say(f"card after uploads: {keys(now)}")
    finally:
        status, _h, _b = call("POST", "/api/memory-cards/delete", json_body={"cards": [card_id]})
        say()
        say(f"cleanup: deleted the card: {status}")
        _common.record("s2-memory-card-record", lines)


if __name__ == "__main__":
    main()
