"""S5: the content_hash RomM stores for a zipped save.

Read at tag 5.3.0, `compute_content_hash` hashes a zip as the md5 of `<entry>:<md5>` lines,
sorted by entry name, joined with a newline and none trailing (`hash_zip_contents` in
`backend/handler/filesystem/assets_handler.py`). RomMBat's class C fold is that rule, so this
asks the live server whether it really stores that value, and not the md5 of the bytes, for an
archive uploaded now.

One upload into a probe slot, with no device, so no sync record moves. The archive holds two
members in two folders and one member with a non-ASCII name, compressed at two different levels,
because name encoding and framing are what a reimplementation gets wrong.

Writes to the instance. The row is deleted before it exits.

    python s5-archive-content-hash.py [rom_id]
"""

from __future__ import annotations

import hashlib
import io
import json
import sys
import zipfile

import _common

SLOT = "probe-s5:archive"
EMULATOR = "probe"
lines: list[str] = []


def say(text: str = "") -> None:
    lines.append(text)


def md5(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def call(method: str, path: str, **kwargs):
    status, _headers, payload, _elapsed = _common.request(method, path, **kwargs)
    try:
        parsed = json.loads(payload) if payload else None
    except ValueError:
        parsed = payload.decode("utf-8", "replace")
    return status, parsed


def multipart(filename: str, content: bytes) -> tuple[bytes, str]:
    boundary = "----rommbats5"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="saveFile"; filename="{filename}"\r\n'.encode(),
        b"Content-Type: application/octet-stream\r\n\r\n",
        content,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    return body, f"multipart/form-data; boundary={boundary}"


MEMBERS = {
    "ULUS10064DATA00/DATA.BIN": b"probe save data " * 64,
    "ULUS10064SETTINGS/PARAM.SFO": b"\x00PSF" + bytes(range(256)),
    "ULUS10064DATA00/été.bin": b"a member with a non-ASCII name",
}


def archive() -> bytes:
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as zf:
        for index, (name, content) in enumerate(MEMBERS.items()):
            method = zipfile.ZIP_DEFLATED if index % 2 == 0 else zipfile.ZIP_STORED
            zf.writestr(zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0)), content, compress_type=method)
    return buffer.getvalue()


def romm_rule() -> str:
    lines_ = [f"{name}:{md5(MEMBERS[name])}" for name in sorted(MEMBERS)]
    return md5("\n".join(lines_).encode())


def main() -> None:
    if len(sys.argv) > 1:
        rom_id = int(sys.argv[1])
    else:
        _status, page = call("GET", "/api/roms", params={"limit": 1, "with_char_index": "false",
                                                          "with_filter_values": "false"})
        rom_id = page["items"][0]["id"]

    _status, heartbeat = call("GET", "/api/heartbeat")
    say(f"server {heartbeat['SYSTEM']['VERSION']}, rom {rom_id}, slot {SLOT}")

    data = archive()
    body, ctype = multipart("ULUS10064.zip", data)
    status, row = call("POST", "/api/saves", params={"rom_id": rom_id, "emulator": EMULATOR, "slot": SLOT},
                       raw_body=body, content_type=ctype)
    if status not in (200, 201):
        raise SystemExit(f"upload answered {status}: {row}")

    try:
        stored = row.get("content_hash")
        expected = romm_rule()
        say(f"stored content_hash    {stored}")
        say(f"RomM's archive rule    {expected}  {'MATCH' if stored == expected else 'differs'}")
        say(f"md5 of the zip bytes   {md5(data)}  {'MATCH' if stored == md5(data) else 'differs'}")
    finally:
        status, _ = call("POST", "/api/saves/delete", json_body={"saves": [row["id"]]})
        say(f"deleted save {row['id']}: {status}")

    text = _common.redact("\n".join(lines))
    print(text)
    _common.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    (_common.OUTPUT_DIR / "s5-archive-content-hash.txt").write_text(text + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
