#!/usr/bin/env python3
"""M0 probe 3: can ES pick up new roms without a restart, and is writing gamelist.xml safe?

EmulationStation embeds cpp-httplib and exposes an HTTP API on port 1234, with a
`/reloadgames` route. The setting `PublicWebAccess` is described in the UI as "ENABLE
PUBLIC WEB API ACCESS", and the binary also carries "HttpServerThread : Access disabled
for ", which suggests the server always runs and only gates non-local callers. If loopback
works with the setting off, RomMBat gets a restart-free refresh for free.

Three phases, because two of them need ES in a particular state:

  online   run while ES is at the system list. Probes the API, drops a new rom in, asks ES
           to reload, and checks whether it appeared. Also stamps gamelist.xml so the
           clobber question can be answered afterwards.
  after    run once ES has exited. Reports whether the stamp survived.
  clean    remove the probe rom and restore the gamelist.

Usage:
  python tools/m0-probes/probe3-refresh.py online K:\\RetroBat
  python tools/m0-probes/probe3-refresh.py after  K:\\RetroBat
  python tools/m0-probes/probe3-refresh.py clean  K:\\RetroBat
"""

from __future__ import annotations

import json
import shutil
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

BASE = "http://127.0.0.1:1234"
PROBE_ROM = "zzrommbat-probe.libretro"
STAMP = "RomMBat probe 3 stamp, written while ES was running"

# Route spellings worth trying; ES's own strings show /reloadgames, but the verb is unknown.
ROUTES = ["/", "/caps", "/systems", "/quit", "/reloadgames", "/gamelists", "/games"]


def call(path: str, method: str = "GET", timeout: float = 5.0):
    req = urllib.request.Request(f"{BASE}{path}", method=method)
    try:
        start = time.perf_counter()
        with urllib.request.urlopen(req, timeout=timeout) as r:
            body = r.read()
        return r.status, len(body), body[:400].decode("utf-8", "replace"), (time.perf_counter() - start)
    except urllib.error.HTTPError as e:
        return e.code, 0, e.reason, 0.0
    except Exception as e:  # connection refused, timeout, etc
        return None, 0, f"{type(e).__name__}: {e}", 0.0


def online(root: Path) -> int:
    print("=== 1. is the ES HTTP API reachable on loopback? ===")
    any_ok = False
    for route in ROUTES:
        if route == "/quit":
            print(f"  {'GET':4} {route:14} SKIPPED (would close ES)")
            continue
        status, size, preview, elapsed = call(route)
        marker = "ok " if status and 200 <= status < 300 else "   "
        print(f"  {marker}{'GET':4} {route:14} -> {status}  {size}B  {elapsed * 1000:.0f}ms")
        if status and 200 <= status < 300:
            any_ok = True
            if size:
                print(f"        {preview[:200]!r}")

    if not any_ok:
        print("\n  No route answered. Either ES is not running, the API is off, or loopback is")
        print("  gated too. Check the ES menu: FRONTEND DEVELOPER OPTIONS -> ENABLE PUBLIC WEB API ACCESS.")
        return 1

    print("\n=== 2. drop a new rom in while ES is running ===")
    ports = root / "roms" / "ports"
    probe_rom = ports / PROBE_ROM
    probe_rom.write_text("", encoding="utf-8")
    print(f"  created roms/ports/{PROBE_ROM}")

    print("\n=== 3. ask ES to reload ===")
    for method in ("GET", "POST"):
        status, size, preview, elapsed = call("/reloadgames", method=method, timeout=30.0)
        print(f"  {method:4} /reloadgames -> {status}  {size}B  {elapsed * 1000:.0f}ms  {preview[:120]!r}")

    print("\n=== 4. did ES notice? ===")
    time.sleep(2)
    status, size, preview, _ = call("/systems")
    if status and 200 <= status < 300:
        found = PROBE_ROM.split(".")[0] in preview
        print(f"  /systems responded {size}B; probe rom named in first 400B: {found}")
    print("  Confirm visually too: does 'zzrommbat-probe' appear under Ports without restarting ES?")

    print("\n=== 5. stamp gamelist.xml while ES is running (the clobber test) ===")
    gamelist = ports / "gamelist.xml"
    backup = gamelist.with_suffix(".xml.probe3-backup")
    if not backup.exists():
        shutil.copy2(gamelist, backup)
    text = gamelist.read_text(encoding="utf-8")
    if STAMP not in text:
        stamped = text.replace("</gameList>", f"  <!-- {STAMP} -->\n</gameList>")
        gamelist.write_text(stamped, encoding="utf-8")
        print(f"  wrote a comment stamp into roms/ports/gamelist.xml at {time.strftime('%H:%M:%S')}")
    print("\n  Now quit EmulationStation, then run: probe3-refresh.py after <root>")
    return 0


def after(root: Path) -> int:
    gamelist = root / "roms" / "ports" / "gamelist.xml"
    text = gamelist.read_text(encoding="utf-8")
    survived = STAMP in text
    mtime = time.strftime("%H:%M:%S", time.localtime(gamelist.stat().st_mtime))

    print("=== did the write survive ES exiting? ===")
    print(f"  gamelist.xml mtime : {mtime}")
    print(f"  stamp present      : {survived}")
    print()
    if survived:
        print("  ES did NOT clobber the write. Writing while ES runs preserved the edit,")
        print("  though ES may still have rewritten the file around it.")
    else:
        print("  ES CLOBBERED the write. A gamelist.xml edit made while ES is running is lost")
        print("  on exit, so M4 must write only while ES is idle, or merge after ES quits.")

    probe_rom = root / "roms" / "ports" / PROBE_ROM
    print(f"\n  probe rom still present: {probe_rom.exists()}")
    return 0


def clean(root: Path) -> int:
    ports = root / "roms" / "ports"
    (ports / PROBE_ROM).unlink(missing_ok=True)
    backup = ports / "gamelist.xml.probe3-backup"
    if backup.exists():
        shutil.move(str(backup), str(ports / "gamelist.xml"))
        print("  restored gamelist.xml")
    print("  removed probe rom")
    return 0


def main() -> int:
    if len(sys.argv) != 3 or sys.argv[1] not in ("online", "after", "clean"):
        print(__doc__)
        return 2
    mode, root = sys.argv[1], Path(sys.argv[2])
    return {"online": online, "after": after, "clean": clean}[mode](root)


if __name__ == "__main__":
    raise SystemExit(main())
