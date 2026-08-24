r"""What a process start costs inside the game-launch path, on the slowest medium we ship to.

Deferred by M6 stages 2, 2a and 2b with the reason recorded each time, and taken here. The
question it answers is the one `docs/ARCHITECTURE.md` and `docs/PLAN.md` both leave open:
whether an ES hook may spawn the agent, given that the hook runs while a game is launching.

**Reads and writes only a scratch tree it makes and removes**, plus one `status` run against
the install named by --install, which opens the store read-only. It never writes a save, a
rom or a configuration file. The scratch tree carries a `retrobat.ini` so `RootMarkers.WalkUp`
finds it, which is what makes the hook write its spool there instead of into the real install.

Three regimes, because they are different numbers and the decision needs all three:

  first    the first run of a freshly copied binary, which pays any one-time cost
  warm     repeated runs with the file in the OS cache, the steady state
  cold     the first run after the volume was remounted, which is the launch-after-boot case

`cold` needs the drive ejected and re-inserted first, so it is its own phase and prints what
to do rather than assuming it.

    python m6-probe10-hook-spawn-cost.py setup   --install K:\RetroBat
    python m6-probe10-hook-spawn-cost.py cold    --install K:\RetroBat
    python m6-probe10-hook-spawn-cost.py run     --install K:\RetroBat [--runs 15]
    python m6-probe10-hook-spawn-cost.py teardown --install K:\RetroBat
"""

from __future__ import annotations

import argparse
import os
import pathlib
import shutil
import statistics
import subprocess
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _common import record_offline  # noqa: E402

SCRATCH = "rommbat-hook-timing"

# What ES passed the game-start hook on this install, read out of a real spool record. Three
# arguments, the third holding spaces and parentheses, which is the shape M0 probe 7b found
# breaks a .bat and a .ps1.
HOOK_ARGS = [
    r"K:\RetroBat\roms\gamecube\Bust-A-Move 3000 (USA).rvz",
    "dolphin-emu",
    "Bust-A-Move 3000 (USA)",
]


def scratch_root(install: pathlib.Path) -> pathlib.Path:
    """Beside the install, not inside it, so nothing here can touch a real tree."""
    return install.parent / SCRATCH


def time_run(argv: list[str], cwd: pathlib.Path | None = None) -> float:
    """Wall milliseconds for one process, start to exit."""
    start = time.perf_counter()
    subprocess.run(
        argv,
        cwd=str(cwd) if cwd else None,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return (time.perf_counter() - start) * 1000.0


def floor_ms(runs: int) -> float:
    """subprocess plus CreateProcess, measured so it can be subtracted rather than assumed."""
    comspec = os.environ.get("COMSPEC", r"C:\Windows\System32\cmd.exe")
    return statistics.median(time_run([comspec, "/c", "exit"]) for _ in range(runs))


def setup(install: pathlib.Path) -> list[str]:
    root = scratch_root(install)
    if root.exists():
        shutil.rmtree(root)

    (root / "emulationstation").mkdir(parents=True)
    (root / "roms").mkdir(parents=True)
    (root / "retrobat.ini").write_text("; scratch root for m6 probe 10\n", encoding="utf-8")
    (root / "game-start").mkdir(parents=True)

    source = install / "emulators" / "rommbat" / "rommbat-hook.exe"
    shutil.copy2(source, root / "game-start" / "rommbat-hook.exe")

    agent = install / "emulators" / "rommbat" / "rommbat-agent.exe"
    return [
        f"scratch root   {root}",
        f"hook           {(root / 'game-start' / 'rommbat-hook.exe').stat().st_size:,} B",
        f"agent          {agent.stat().st_size:,} B (in place, not copied)",
    ]


def one_pass(install: pathlib.Path, label: str) -> list[str]:
    """One run of each binary, for the first-run and cold-cache regimes."""
    root = scratch_root(install)
    hook = root / "game-start" / "rommbat-hook.exe"
    agent = install / "emulators" / "rommbat" / "rommbat-agent.exe"

    return [
        f"{label:<8} hook            {time_run([str(hook), *HOOK_ARGS]):8.1f} ms",
        f"{label:<8} agent --help    {time_run([str(agent), '--help']):8.1f} ms",
        f"{label:<8} agent status    {time_run([str(agent), 'status'], cwd=install):8.1f} ms",
    ]


def series(argv: list[str], runs: int, cwd: pathlib.Path | None = None) -> tuple[float, float, float]:
    samples = [time_run(argv, cwd) for _ in range(runs)]
    return min(samples), statistics.median(samples), max(samples)


def run(install: pathlib.Path, runs: int) -> list[str]:
    root = scratch_root(install)
    hook = root / "game-start" / "rommbat-hook.exe"
    agent = install / "emulators" / "rommbat" / "rommbat-agent.exe"

    floor = floor_ms(runs)
    lines = [f"floor    cmd /c exit     {floor:8.1f} ms  (subtracted below as 'net')", ""]

    for label, argv, cwd in (
        ("hook", [str(hook), *HOOK_ARGS], None),
        ("agent --help", [str(agent), "--help"], None),
        ("agent status", [str(agent), "status"], install),
    ):
        low, mid, high = series(argv, runs, cwd)
        lines.append(
            f"warm     {label:<15} min {low:7.1f}  median {mid:7.1f}  max {high:7.1f} ms"
            f"   net median {mid - floor:7.1f} ms"
        )

    spooled = list((root / "emulators" / "rommbat" / "spool").glob("*.hook"))
    lines += ["", f"spool records the hook wrote into the scratch tree: {len(spooled)}"]
    return lines


def teardown(install: pathlib.Path) -> list[str]:
    root = scratch_root(install)
    if not root.exists():
        return [f"{root} was already gone"]
    shutil.rmtree(root)
    return [f"removed {root}", f"exists now: {root.exists()}"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("setup", "cold", "run", "teardown"))
    parser.add_argument("--install", required=True, help="the RetroBat root to measure against")
    parser.add_argument("--runs", type=int, default=15)
    args = parser.parse_args()

    install = pathlib.Path(args.install)
    if not (install / "retrobat.ini").exists():
        raise SystemExit(f"{install} does not look like a RetroBat root")

    if args.phase == "setup":
        lines = setup(install)
    elif args.phase == "cold":
        lines = one_pass(install, "cold")
    elif args.phase == "run":
        lines = one_pass(install, "first") + [""] + run(install, args.runs)
    else:
        lines = teardown(install)

    record_offline(f"probe10-hook-spawn-{args.phase}", [f"=== probe 10, {args.phase}", ""] + lines)


if __name__ == "__main__":
    main()
