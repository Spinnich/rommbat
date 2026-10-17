"""Reads the repository's local `.env`, from the main checkout or any worktree of it.

`.env` holds the test server, the tokens and the install roots. Git ignores it, and a worktree
does not have a copy, so this looks in the main checkout first. Bash cannot source it safely
(`ROMMBAT_AGENT_ROOT=D:\\retrobat-agent` loses its backslashes), so Python reads it here and
`tools/handson/HandsOn.psm1` reads it for PowerShell. Both parse a line as `KEY=value`, drop a
trailing ` # note` (even inside quotes, so a path holding ` #` is cut) and trim the
double quotes around the value.

    import localenv; localenv.apply()      # fill os.environ, never overriding a variable already set
    python tools/localenv.py               # which keys are set, which file, never a value
    python tools/localenv.py --powershell | Invoke-Expression   # export the ROMMBAT_TEST_ keys, for dotnet test

`docs/contributing/local-secrets.md` says what each key is.
"""

from __future__ import annotations

import os
import pathlib
import re
import subprocess
import sys

LINE = re.compile(r"^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$")
EXPECTED = {
    "ROMMBAT_TEST_SERVER": "the RomM instance the live tests and probes call",
    "ROMMBAT_TEST_APPROVER_TOKEN": "approver account token: pairs, and what the probes use",
    "ROMMBAT_TEST_OWNER_TOKEN": "owner account token, narrowly scoped: reads an install's play sessions",
    "ROMMBAT_TEST_CORPUS_TOKEN": "approver account token with collections.write: keeps the test-game corpus",
    "ROMMBAT_AGENT_ROOT": "the agent's RetroBat tree, for hands-on passes and /certify",
    "ROMMBAT_MAINTAINER_ROOT": "the maintainer's own tree, read only",
    "ROMMBAT_SCOUT_ROOT": "the scout tree, for a RetroBat prerelease",
}


def find() -> pathlib.Path | None:
    """The first `.env` in the main checkout, then in this checkout."""
    here = pathlib.Path(__file__).resolve().parent
    roots = [here.parent]
    try:
        common = subprocess.run(
            ["git", "-C", str(here), "rev-parse", "--path-format=absolute", "--git-common-dir"],
            capture_output=True, text=True, check=True,
        ).stdout.strip()
        roots.insert(0, pathlib.Path(common).parent)
    except (OSError, subprocess.CalledProcessError):
        pass
    return next((r / ".env" for r in roots if (r / ".env").is_file()), None)


def load() -> dict[str, str]:
    path = find()
    values: dict[str, str] = {}
    if path is None:
        return values
    for line in path.read_text(encoding="utf-8").splitlines():
        match = LINE.match(line)
        if match:
            values[match.group(1)] = re.sub(r"\s+#.*$", "", match.group(2)).strip().strip('"')
    return values


def apply() -> None:
    """Sets each key that the environment lacks, so a variable already exported wins."""
    for key, value in load().items():
        os.environ.setdefault(key, value)


def powershell() -> int:
    """`$env:KEY='value'` lines for the keys `dotnet test` reads. Prints values, so pipe it, never display it."""
    for key, value in load().items():
        if key.startswith("ROMMBAT_TEST_"):
            print(f"$env:{key}='{value.replace(chr(39), chr(39) * 2)}'")
    return 0


def main() -> int:
    if "--powershell" in sys.argv[1:]:
        return powershell()
    path = find()
    print(f".env: {path or 'not found'}")
    values = load()
    for key, meaning in EXPECTED.items():
        state = "set" if values.get(key) else ("in the environment only" if os.environ.get(key) else "MISSING")
        print(f"  {state:<23} {key}  ({meaning})")
    return 0 if path else 1


if __name__ == "__main__":
    sys.exit(main())
