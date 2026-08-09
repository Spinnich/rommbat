#!/usr/bin/env python3
"""M0 probe 5: what a large RomM library costs to page through.

Sets the default page size, the sync-set warning thresholds and the per-system gamelist
cap. Reads only, so it is safe against a production instance.

Credentials come from the environment and are never written to the repo. The instance
host is redacted from all output.

    ROMM_URL=https://... ROMM_TOKEN=rmm_... python tools/m0-probes/probe5-scale.py
"""

from __future__ import annotations

import json
import os
import statistics
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

# Every sidecar on GET /api/roms that defaults to true. The plan says three; there are four.
SIDECARS = ("with_char_index", "with_filter_values", "with_rom_id_index", "with_total")

LIMITS = (10, 25, 50, 100, 250, 500, 1000)
REPS = 3
PAUSE = 0.25  # be polite to a production instance


def request(url: str, token: str, path: str, params: dict | None = None) -> tuple[float, int, dict]:
    """Return (elapsed_seconds, byte_count, parsed_json)."""
    query = f"?{urllib.parse.urlencode(params, doseq=True)}" if params else ""
    req = urllib.request.Request(
        f"{url}{path}{query}",
        headers={"Authorization": f"Bearer {token}", "Accept": "application/json"},
    )
    start = time.perf_counter()
    with urllib.request.urlopen(req, timeout=120) as response:
        body = response.read()
    elapsed = time.perf_counter() - start
    return elapsed, len(body), json.loads(body)


def median_of(fn, reps: int = REPS) -> tuple[float, int, dict]:
    timings, size, payload = [], 0, {}
    for _ in range(reps):
        elapsed, size, payload = fn()
        timings.append(elapsed)
        time.sleep(PAUSE)
    return statistics.median(timings), size, payload


def main() -> int:
    url = os.environ.get("ROMM_URL", "").rstrip("/")
    token = os.environ.get("ROMM_TOKEN", "")
    if not url or not token:
        print("set ROMM_URL and ROMM_TOKEN", file=sys.stderr)
        return 2

    results = {"limits": [], "collections": {}, "sidecar_detail": {}}

    # Library size, and a baseline single-item request.
    _, _, head = request(url, token, "/api/roms", {"limit": 1})
    total = head.get("total")
    print(f"library size: {total} roms")
    print()

    print(f"{'limit':>6} {'sidecars on':>14} {'sidecars off':>14} {'on KB':>9} {'off KB':>9} {'delta':>8}")
    print("-" * 66)

    for limit in LIMITS:
        on_params = {"limit": limit}
        off_params = {"limit": limit, **{flag: "false" for flag in SIDECARS}}

        t_on, size_on, _ = median_of(lambda: request(url, token, "/api/roms", on_params))
        t_off, size_off, _ = median_of(lambda: request(url, token, "/api/roms", off_params))

        delta = (t_on - t_off) / t_on * 100 if t_on else 0
        results["limits"].append(
            {
                "limit": limit,
                "on_ms": round(t_on * 1000, 1),
                "off_ms": round(t_off * 1000, 1),
                "on_bytes": size_on,
                "off_bytes": size_off,
                "sidecar_overhead_pct": round(delta, 1),
            }
        )
        print(
            f"{limit:>6} {t_on * 1000:>11.0f} ms {t_off * 1000:>11.0f} ms "
            f"{size_on / 1024:>9.0f} {size_off / 1024:>9.0f} {delta:>7.0f}%"
        )

    # Which single sidecar actually costs the time, at a realistic page size.
    print()
    print("per-sidecar cost at limit=100 (each disabled alone):")
    base_ms, _, _ = median_of(lambda: request(url, token, "/api/roms", {"limit": 100}))
    print(f"  {'all on (baseline)':<26} {base_ms * 1000:>8.0f} ms")
    for flag in SIDECARS:
        params = {"limit": 100, flag: "false"}
        ms, size, _ = median_of(lambda: request(url, token, "/api/roms", params))
        saved = (base_ms - ms) * 1000
        results["sidecar_detail"][flag] = {"ms": round(ms * 1000, 1), "saved_ms": round(saved, 1)}
        print(f"  {flag + ' off':<26} {ms * 1000:>8.0f} ms   saves {saved:>6.0f} ms")

    # with_files is opt-in rather than a default-on sidecar, so it is measured separately.
    ms, size, _ = median_of(lambda: request(url, token, "/api/roms", {"limit": 100, "with_files": "true"}))
    print(f"  {'with_files=true':<26} {ms * 1000:>8.0f} ms   {size / 1024:>6.0f} KB")
    results["sidecar_detail"]["with_files_true"] = {"ms": round(ms * 1000, 1), "bytes": size}

    # Collections: the plan wants the response size, since it is fetched whole.
    print()
    for path in ("/api/collections", "/api/collections/virtual", "/api/collections/smart"):
        try:
            ms, size, payload = median_of(lambda: request(url, token, path))
            count = len(payload) if isinstance(payload, list) else payload.get("total", "?")
            results["collections"][path] = {"ms": round(ms * 1000, 1), "bytes": size, "count": count}
            print(f"{path:<30} {ms * 1000:>7.0f} ms  {size / 1024:>8.1f} KB  items={count}")
        except urllib.error.HTTPError as exc:
            results["collections"][path] = {"error": exc.code}
            print(f"{path:<30} HTTP {exc.code}")

    out = "probe-output/probe5-scale.json"
    os.makedirs("probe-output", exist_ok=True)
    results["library_total"] = total
    with open(out, "w", encoding="utf-8") as handle:
        json.dump(results, handle, indent=2)
    print(f"\nwrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
