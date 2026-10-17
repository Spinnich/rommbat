#!/usr/bin/env python3
"""Keep the test server's corpus collections matching docs/platforms/corpus.md.

    python tools/corpus.py [system ...]            resolve each game, compare it with its
                                                   collection, print the set's line. Read-only
    python tools/corpus.py [system ...] --apply    create a missing collection, add and remove
                                                   members to match

Each system's games are one collection, "RomMBat corpus: <system>", private and owned by the
test account, which is the account the agent tree pairs as. ROMMBAT_TEST_DEV_TOKEN, the token on that
account that holds every scope, reads and writes them; a read-only check falls back to the
approver token when it is not set. A game is
resolved by its file name on its RomM platform, since rom ids differ per server. A game the
server lacks is reported as missing and one whose size or md5 moved as changed; neither stops
the rest of the system, and neither is added.

Exits 0 when every collection matches the games that resolve, 1 when one does not or a game
changed, so a check after --apply says whether the page needs editing.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CORPUS = ROOT / "docs" / "platforms" / "corpus.md"
HEADER = ["Game", "Platform", "Size", "md5", "Covers", "First save"]
NAME_PREFIX = "RomMBat corpus: "
PAGE = 250


@dataclass(frozen=True)
class Entry:
    system: str
    fs_name: str
    platform: str
    size: int
    md5: str


@dataclass
class Outcome:
    system: str
    resolved: dict[int, Entry] = field(default_factory=dict)
    missing: list[Entry] = field(default_factory=list)
    changed: list[tuple[Entry, dict]] = field(default_factory=list)
    collection: dict | None = None
    members: dict[int, str] = field(default_factory=dict)

    @property
    def to_add(self) -> list[int]:
        return sorted(set(self.resolved) - set(self.members))

    @property
    def to_remove(self) -> list[int]:
        # A changed game stays: the page owes the update, and the next pass uses the game as it is.
        kept = set(self.resolved) | {rom["id"] for _, rom in self.changed}
        return sorted(set(self.members) - kept)

    @property
    def matches(self) -> bool:
        return self.collection is not None and not self.to_add and not self.to_remove


def cell(text: str) -> str:
    return text.strip().strip("`").strip()


def parse(markdown: str) -> dict[str, list[Entry]]:
    """The corpus tables by system. Only a table whose header is HEADER under a `## ` heading counts."""
    corpus: dict[str, list[Entry]] = {}
    system = None
    in_table = False
    for line in markdown.splitlines():
        if line.startswith("## "):
            system = line[3:].strip()
            in_table = False
            continue
        if not line.startswith("|"):
            in_table = False
            continue
        cells = [cell(part) for part in line.strip().strip("|").split("|")]
        if cells == HEADER:
            in_table = system is not None
            continue
        if not in_table or set("".join(cells)) <= set("-: "):
            continue
        if len(cells) != len(HEADER):
            raise ValueError(f"{system}: a corpus row needs {len(HEADER)} cells: {line}")
        name, platform, size, md5, _, _ = cells
        if not re.fullmatch(r"[0-9a-f]{32}", md5):
            raise ValueError(f"{system}: {name} has no md5: {md5!r}")
        corpus.setdefault(system, []).append(Entry(system, name, platform, int(size.replace(",", "")), md5))
    return corpus


def stem(fs_name: str) -> str:
    # A multi-file rom's fs_name is a folder and has no extension to strip.
    return re.sub(r"\.[A-Za-z0-9]{1,4}$", "", fs_name)


def judge(entry: Entry, candidates: list[dict]) -> tuple[str, dict | None]:
    """ok, changed or missing, from the roms a search for the entry returned."""
    for rom in candidates:
        if rom.get("fs_name") == entry.fs_name:
            same = rom.get("md5_hash") == entry.md5 and rom.get("fs_size_bytes") == entry.size
            return ("ok" if same else "changed"), rom
    return "missing", None


class Server:
    def __init__(self, writing: bool) -> None:
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        import localenv

        localenv.apply()
        self.base = os.environ.get("ROMMBAT_TEST_SERVER", "").rstrip("/")
        # The approver token stays narrow for the pairing tests, so writes need the dev token.
        self.token = os.environ.get("ROMMBAT_TEST_DEV_TOKEN", "")
        if not self.token and not writing:
            self.token = os.environ.get("ROMMBAT_TEST_APPROVER_TOKEN", "")
        if not self.base or not self.token:
            needed = "ROMMBAT_TEST_DEV_TOKEN" if writing else "ROMMBAT_TEST_DEV_TOKEN or ROMMBAT_TEST_APPROVER_TOKEN"
            raise SystemExit(f"ROMMBAT_TEST_SERVER and {needed} are needed; see python tools/localenv.py.")

    def call(self, method: str, path: str, params: dict | None = None, body: bytes | None = None, content_type: str | None = None):
        url = f"{self.base}{path}" + (f"?{urllib.parse.urlencode(params, doseq=True)}" if params else "")
        headers = {"Authorization": f"Bearer {self.token}", "Accept": "application/json"}
        if content_type:
            headers["Content-Type"] = content_type
        request = urllib.request.Request(url, data=body, method=method, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                raw = response.read()
        except urllib.error.HTTPError as error:
            # The path, never the URL: the host stays out of a transcript.
            hint = " (ROMMBAT_TEST_DEV_TOKEN needs collections.write)" if error.code == 403 and method != "GET" else ""
            raise SystemExit(f"{method} {path} answered {error.code}{hint}")
        except (urllib.error.URLError, TimeoutError) as error:
            raise SystemExit(f"The test server is unreachable: {type(error).__name__}")
        return json.loads(raw) if raw else None

    def json_call(self, method: str, path: str, payload: dict):
        return self.call(method, path, body=json.dumps(payload).encode(), content_type="application/json")

    def platforms(self) -> dict[str, int]:
        return {p["fs_slug"]: p["id"] for p in self.call("GET", "/api/platforms")}

    def search(self, platform_id: int, term: str) -> list[dict]:
        return self.call("GET", "/api/roms", {"platform_ids": platform_id, "search_term": term, "limit": 50})["items"]

    def me(self) -> int:
        return self.call("GET", "/api/users/me")["id"]

    def collections(self) -> list[dict]:
        return self.call("GET", "/api/collections")

    def members(self, collection_id: int) -> dict[int, str]:
        found: dict[int, str] = {}
        offset = 0
        while True:
            page = self.call(
                "GET", "/api/roms", {"collection_id": collection_id, "limit": PAGE, "offset": offset, "with_total": "true"}
            )
            found.update({rom["id"]: rom["fs_name"] for rom in page["items"]})
            offset += PAGE
            if offset >= page.get("total", 0) or not page["items"]:
                return found

    def create(self, name: str, description: str) -> dict:
        boundary = uuid.uuid4().hex
        parts = [
            f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'
            for key, value in (("name", name), ("description", description))
        ]
        body = ("".join(parts) + f"--{boundary}--\r\n").encode()
        return self.call(
            "POST", "/api/collections", {"is_public": "false"}, body, f"multipart/form-data; boundary={boundary}"
        )


def examine(server: Server, system: str, entries: list[Entry], platforms: dict[str, int], owned: dict[str, dict]) -> Outcome:
    outcome = Outcome(system)
    for entry in entries:
        platform_id = platforms.get(entry.platform)
        candidates = server.search(platform_id, stem(entry.fs_name)) if platform_id else []
        verdict, rom = judge(entry, candidates)
        if verdict == "ok":
            outcome.resolved[rom["id"]] = entry
        elif verdict == "changed":
            outcome.changed.append((entry, rom))
        else:
            outcome.missing.append(entry)
    outcome.collection = owned.get(NAME_PREFIX + system)
    if outcome.collection:
        outcome.members = server.members(outcome.collection["id"])
    return outcome


def report(outcome: Outcome) -> None:
    collection = outcome.collection
    where = f"collection {collection['id']}" if collection else "no collection yet"
    count = len(outcome.resolved)
    print(f"{outcome.system}: {where}, {count} game{'' if count == 1 else 's'} resolved")
    for rom_id, entry in sorted(outcome.resolved.items(), key=lambda item: item[1].fs_name):
        state = "in" if rom_id in outcome.members else "to add"
        print(f"  {state:<8} rom {rom_id}  {entry.fs_name}")
    for entry, rom in outcome.changed:
        print(
            f"  changed  rom {rom['id']}  {entry.fs_name}: the server holds {rom.get('fs_size_bytes')} B,"
            f" md5 {rom.get('md5_hash')}; the page says {entry.size} B, {entry.md5}"
        )
    for entry in outcome.missing:
        print(f"  missing  {entry.fs_name} on {entry.platform}")
    for rom_id in outcome.to_remove:
        print(f"  extra    rom {rom_id}  {outcome.members[rom_id]}, in the collection and not the page")
    if collection:
        print(f"  Invoke-Agent sets add corpus-{outcome.system} --scope collection --value {collection['id']} --folder {outcome.system}")


def apply(server: Server, outcome: Outcome) -> None:
    if not outcome.resolved and outcome.collection is None:
        return
    if outcome.collection is None:
        description = f"Test games for {outcome.system}, kept by tools/corpus.py from docs/platforms/corpus.md."
        outcome.collection = server.create(NAME_PREFIX + outcome.system, description)
        print(f"{outcome.system}: created collection {outcome.collection['id']}")
    collection_id = outcome.collection["id"]
    if outcome.to_add:
        server.json_call("POST", f"/api/collections/{collection_id}/roms", {"rom_ids": outcome.to_add})
    if outcome.to_remove:
        server.json_call("DELETE", f"/api/collections/{collection_id}/roms", {"rom_ids": outcome.to_remove})
    outcome.members = server.members(collection_id)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("systems", nargs="*", help="RetroBat systems, as the page's headings name them; all when omitted")
    parser.add_argument("--apply", action="store_true", help="create and update the collections to match")
    args = parser.parse_args()

    corpus = parse(CORPUS.read_text(encoding="utf-8"))
    unknown = [system for system in args.systems if system not in corpus]
    if unknown:
        raise SystemExit(f"No corpus table for {', '.join(unknown)}; the page has {', '.join(corpus)}.")

    server = Server(writing=args.apply)
    platforms = server.platforms()
    me = server.me()
    owned = {c["name"]: c for c in server.collections() if c.get("user_id") == me and c["name"].startswith(NAME_PREFIX)}

    worst = 0
    for system in args.systems or list(corpus):
        outcome = examine(server, system, corpus[system], platforms, owned)
        if args.apply:
            apply(server, outcome)
        report(outcome)
        if not outcome.matches or outcome.changed:
            worst = 1
    return worst


if __name__ == "__main__":
    sys.exit(main())
