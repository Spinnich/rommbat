r"""What `dolphin_sync_saves` actually does to a GameCube save tree.

Four documents in this repository describe it as RetroBat copying files between the dolphin
and libretro-dolphin save folders on its own schedule. Reading
`RetroBat-Official/emulatorlauncher`, `Dolphin.Generator.cs`, none of that is right: it runs
once per launch inside emulatorlauncher before Dolphin starts, it is GameCube only, and the
two locations it reconciles are `GC/<REGION>/` and `GC/<REGION>/Card A/`, the second being a
subdirectory of the first. This probe drives it to settle the reading against the install.

**Writes into a real RetroBat install**, and only these three things: the
`gamecube.dolphin_sync_saves` key in es_settings.cfg, a byte copy of that file beside it
before each change, and (in the `stage-hazard` phase) the removal of the region-root `.gci`,
which is what a transfer dropping a member does. Every phase that writes refuses while
EmulationStation is running, for the reason finding 179 gives: ES serialises the model it
loaded at startup, so a key written underneath it is discarded.

    python m6-probe11-dolphin-sync-saves.py snapshot --install K:\RetroBat --label before-on
    python m6-probe11-dolphin-sync-saves.py on       --install K:\RetroBat
    python m6-probe11-dolphin-sync-saves.py report   --install K:\RetroBat --label before-on --against after-on
    python m6-probe11-dolphin-sync-saves.py stage-hazard --install K:\RetroBat
    python m6-probe11-dolphin-sync-saves.py off      --install K:\RetroBat
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _common import record_offline  # noqa: E402

KEY = "gamecube.dolphin_sync_saves"
SETTINGS = "emulationstation/.emulationstation/es_settings.cfg"

# Everything a GameCube launch can touch, including the two locations RetroBat leaves at their
# stock Dolphin defaults. saves/dolphin is deliberately in the list: it is outside every
# container save_shapes.json declares, which is the thing worth noticing.
WATCHED = ("saves/gamecube", "saves/dolphin", "saves/wii")

OUTPUT = pathlib.Path(__file__).resolve().parents[2] / "probe-output" / "m6"


def es_running(install: pathlib.Path) -> str | None:
    """The path of an EmulationStation inside this install, or None."""
    script = (
        "Get-Process emulationstation -ErrorAction SilentlyContinue "
        "| ForEach-Object { $_.Path }"
    )
    out = subprocess.run(
        ["powershell", "-NoProfile", "-Command", script],
        capture_output=True,
        text=True,
        check=False,
    ).stdout
    for line in out.splitlines():
        line = line.strip()
        # Matched on the executable's path, not the process name, so a second install's ES
        # does not produce a refusal the user cannot act on. Same rule as
        # EmulationStationProcess, and Spinnich runs two installs.
        if line and pathlib.Path(line).is_relative_to(install):
            return line
    return None


def digest(path: pathlib.Path) -> str:
    return hashlib.md5(path.read_bytes()).hexdigest()


def read_key(settings: pathlib.Path) -> str | None:
    text = settings.read_text(encoding="utf-8")
    pattern = r'<\w+ name="' + re.escape(KEY) + r'" value="([^"]*)"'
    found = re.search(pattern, text)
    return found.group(1) if found else None


def snapshot(install: pathlib.Path) -> dict:
    out: dict = {"taken": datetime.datetime.now(datetime.timezone.utc).isoformat()}

    for sub in WATCHED:
        directory = install / sub
        if not directory.exists():
            out[sub] = None
            continue
        out[sub] = {
            str(p.relative_to(directory)).replace("\\", "/"): [
                p.stat().st_size,
                digest(p),
                int(p.stat().st_mtime_ns),
            ]
            for p in sorted(directory.rglob("*"))
            if p.is_file()
        }

    settings = install / SETTINGS
    out["_settings"] = [settings.stat().st_size, digest(settings)]
    out["_key"] = read_key(settings)
    return out


def set_key(install: pathlib.Path, value: str | None) -> list[str]:
    """Adds, changes or removes the key, keeping a byte copy of what was there."""
    settings = install / SETTINGS
    was = read_key(settings)
    stamp = datetime.datetime.now().strftime("%Y%m%dT%H%M%S")
    backup = settings.with_name(settings.name + ".probe11-" + stamp)
    shutil.copy2(settings, backup)

    text = settings.read_text(encoding="utf-8")
    pattern = r"[ \t]*<\w+ name=\"" + re.escape(KEY) + r"\" value=\"[^\"]*\"\s*/>\r?\n"
    element = '\t<bool name="' + KEY + '" value="' + (value or "") + '" />\n'

    if value is None:
        text = re.sub(pattern, "", text)
    elif was is None:
        # ES writes one element per line, tab indented. The writer in RomMBat.Core round-trips
        # this file byte for byte against the checked-in fixture, and this matches its output.
        text = text.replace("</config>", element + "</config>")
    else:
        text = re.sub(pattern, element, text)

    settings.write_text(text, encoding="utf-8", newline="")

    return [
        "backup      " + backup.name,
        KEY,
        "  was       " + repr(was),
        "  now       " + repr(read_key(settings)),
        "  settings  " + str(settings.stat().st_size) + " B, md5 " + digest(settings)[:12],
    ]


def stage_hazard(install: pathlib.Path) -> list[str]:
    """Removes the region-root .gci, the way a transfer that drops a member does.

    The hazard is not the one the mtime rule suggests. Restoring a save writes it with the
    current time, so a restored file is always the newest and always wins. What bites is the
    one-sided branch: a file present in Card A and absent from the region root is copied
    *back*. So a save RomMBat removed reappears at the next launch, holding whatever Card A
    captured at some earlier launch, and RomMBat never reads Card A so it cannot see it coming.
    """
    region = install / "saves/gamecube/dolphin-emu/User/GC/USA"
    card_a = region / "Card A"
    live = sorted(region.glob("*.gci"))
    if not live:
        return ["no .gci under " + str(region) + ", nothing to stage"]

    lines = []
    for gci in live:
        stale = card_a / gci.name
        lines.append(
            "region root  " + gci.name + "  " + str(gci.stat().st_size) + " B  md5 "
            + digest(gci)[:12] + "   <- deleting this"
        )
        if stale.exists():
            lines.append(
                "Card A       " + stale.name + "  " + str(stale.stat().st_size) + " B  md5 "
                + digest(stale)[:12] + "   <- what the next launch can put back"
            )
        else:
            lines.append("Card A       (no copy of " + gci.name + ")")
        gci.unlink()

    lines.append("")
    lines.append("region root now holds: " + (", ".join(p.name for p in sorted(region.glob("*"))) or "(nothing)"))
    return lines


def report(before: dict, after: dict) -> list[str]:
    lines = []

    for sub in WATCHED:
        old = before.get(sub) or {}
        new = after.get(sub) or {}
        added = sorted(set(new) - set(old))
        gone = sorted(set(old) - set(new))
        changed = sorted(k for k in set(old) & set(new) if old[k][1] != new[k][1])
        touched = sorted(
            k for k in set(old) & set(new) if old[k][1] == new[k][1] and old[k][2] != new[k][2]
        )

        if not (added or gone or changed or touched):
            lines.append(sub + ": unchanged (" + str(len(new)) + " files)")
            continue

        lines.append(sub + ":")
        for k in added:
            lines.append("   added     " + str(new[k][0]).rjust(9) + " B  " + k)
        for k in gone:
            lines.append("   gone      " + str(old[k][0]).rjust(9) + " B  " + k)
        for k in changed:
            lines.append(
                "   rewritten " + str(new[k][0]).rjust(9) + " B  " + k
                + "  " + old[k][1][:8] + " -> " + new[k][1][:8]
            )
        for k in touched:
            lines.append("   mtime     " + str(new[k][0]).rjust(9) + " B  " + k + "  (same bytes)")

    lines += [
        "",
        KEY + ": " + repr(before.get("_key")) + " -> " + repr(after.get("_key")),
        "es_settings.cfg: " + before["_settings"][1][:12] + " -> " + after["_settings"][1][:12],
    ]
    return lines


def launcher_tail(install: pathlib.Path, count: int = 4) -> list[str]:
    """What emulatorlauncher said about the sync on the most recent launches."""
    log = install / "emulationstation/emulatorLauncher.log"
    if not log.exists():
        return ["(no emulatorLauncher.log)"]

    hits = [
        line.strip()
        for line in log.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        if "GameCube saves" in line
    ]
    return hits[-count:] or ["(emulatorlauncher has never mentioned GameCube saves)"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("snapshot", "on", "off", "stage-hazard", "report"))
    parser.add_argument("--install", required=True)
    parser.add_argument("--label", default="snapshot")
    parser.add_argument("--against")
    args = parser.parse_args()

    install = pathlib.Path(args.install)
    if not (install / "retrobat.ini").exists():
        raise SystemExit(str(install) + " does not look like a RetroBat root")

    OUTPUT.mkdir(parents=True, exist_ok=True)

    if args.phase in ("on", "off", "stage-hazard"):
        where = es_running(install)
        if where is not None:
            raise SystemExit("EmulationStation is running from " + where + ". Close it first.")

    if args.phase == "snapshot":
        state = snapshot(install)
        (OUTPUT / ("probe11-" + args.label + ".json")).write_text(
            json.dumps(state, indent=1), encoding="utf-8"
        )
        lines = [
            sub + ": " + str(len(state[sub]) if state[sub] else 0) + " files" for sub in WATCHED
        ]
        lines += [KEY + ": " + repr(state["_key"]), ""] + launcher_tail(install)
    elif args.phase in ("on", "off"):
        lines = set_key(install, "true" if args.phase == "on" else None)
        state = snapshot(install)
        (OUTPUT / ("probe11-after-" + args.phase + ".json")).write_text(
            json.dumps(state, indent=1), encoding="utf-8"
        )
    elif args.phase == "stage-hazard":
        lines = stage_hazard(install)
    else:
        before = json.loads(
            (OUTPUT / ("probe11-" + args.label + ".json")).read_text(encoding="utf-8")
        )
        after = json.loads(
            (OUTPUT / ("probe11-" + args.against + ".json")).read_text(encoding="utf-8")
        )
        lines = report(before, after) + [""] + launcher_tail(install)

    record_offline("probe11-" + args.phase + "-" + args.label, ["=== probe 11, " + args.phase, ""] + lines)


if __name__ == "__main__":
    main()
