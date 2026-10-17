#!/usr/bin/env python3
"""Open or update one tracking issue per upstream version line above RomMBat's floor.

Run daily by .github/workflows/upstream-watch.yml, and by hand with --dry-run to see what it
would do. A line is <major>.<minor>: every RomM 5.4 prerelease and stable lands on one
issue, "RomM 5.4: scout and adopt", which /upstream works from (the version-adoption skill).

Needs an authenticated `gh`. Only the functions below `# I/O` touch the network.
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "docs"))
import check  # noqa: E402  the floor reader --stale uses, so both agree on what the floor is

PROJECTS = {
    "RomM": "rommapp/romm",
    "RetroBat": "RetroBat-Official/retrobat",
}
LABEL = "upstream"
START = "<!-- upstream-watch:start -->"
END = "<!-- upstream-watch:end -->"
# RomM tags 5.4.0-alpha.1; RetroBat tags 8.2.1 and beta_8.3.0, and once tagged a stable 8.0.
TAG = re.compile(r"^(?:(?P<channel>[a-z]+)_)?v?(?P<core>\d+(?:\.\d+)+)(?:-(?P<suffix>[0-9A-Za-z.]+))?$")
# RetroBat's tag and its build can differ: tag 8.3.1 ships RetroBat-v8.3.1.1-stable-win64-setup.exe,
# and system/version.info, which the startup check compares, follows the build.
SETUP = re.compile(r"^\w+-v(?P<core>\d+(?:\.\d+)+)-[0-9A-Za-z]+-win64-setup\.exe$")
# A table written before the Version column has no second backticked cell, and reads as None.
ROW = re.compile(r"^\| `(?P<tag>[^`]+)` \|(?: `(?P<shipped>[^`]+)` \|)?", re.MULTILINE)


@dataclass(frozen=True)
class Release:
    tag: str
    version: str  # normalized: beta_8.3.0 reads as 8.3.0-beta
    prerelease: bool
    published: str
    shipped: str  # the setup asset's version, or the tag's when there is no single setup asset

    @property
    def line(self) -> str:
        return ".".join(self.version.partition("-")[0].split(".")[:2])

    @property
    def key(self) -> tuple:
        """check.version_key's order, ranked on GitHub's prerelease flag rather than the suffix."""
        return (check.version_key(self.shipped.partition("-")[0], None)[0], not self.prerelease)

    @property
    def differs(self) -> bool:
        return self.shipped.partition("-")[0] != self.version.partition("-")[0]


@dataclass(frozen=True)
class Issue:
    number: int
    title: str
    body: str
    open: bool


@dataclass
class Action:
    kind: str  # "open", "update" or "comment"
    title: str
    number: int | None = None
    body: str = ""


def normalize(
    tag: str, prerelease: bool, published: str, assets: list[str] | None = None
) -> Release | None:
    match = TAG.match(tag)
    if not match:
        return None
    suffix = match["suffix"] or match["channel"]
    version = match["core"] + (f"-{suffix}" if suffix else "")
    setups = [m["core"] for m in map(SETUP.match, assets or []) if m]
    shipped = setups[0] if len(setups) == 1 else version
    return Release(tag, version, prerelease, published, shipped)


def title(project: str, line: str) -> str:
    return f"{project} {line}: scout and adopt"


def rows_in(body: str) -> set[tuple[str, str | None]]:
    table_text = body.partition(START)[2].partition(END)[0]
    return {(m["tag"], m["shipped"]) for m in ROW.finditer(table_text)}


def listed(r: Release, rows: set[tuple[str, str | None]]) -> bool:
    """Whether a table already shows this build. A legacy row knows only its tag, which is
    enough unless the build's version differs from it."""
    return (r.tag, r.shipped) in rows or ((r.tag, None) in rows and not r.differs)


def table(project: str, releases: list[Release]) -> str:
    rows = [
        f"| `{r.tag}` | `{r.shipped}` | {'prerelease' if r.prerelease else '**stable**'} | "
        f"{r.published[:10]} | `/upstream {project.lower()} {r.tag}` |"
        for r in sorted(releases, key=lambda r: r.key, reverse=True)
    ]
    return "\n".join(
        [START, "| Tag | Version | Kind | Published | Run |", "| --- | --- | --- | --- | --- |",
         *rows, END]
    )


def body(project: str, line: str, releases: list[Release], floor: str) -> str:
    return (
        f"Tracks {project} {line} against RomMBat's floor, {project} {floor}. Opened and kept "
        "current by `tools/upstream_watch.py`; edit outside the marked table freely.\n\n"
        "A prerelease gets a **scout** pass: it reports what adoption will need and lands "
        "forward-compatible fixes, and never moves a floor. A stable gets **adopted**: the floor, "
        "the tested row and, for RomM, the pinned schema move to it, within one RomMBat release. "
        "Both run through `/upstream` and the `version-adoption` skill.\n\n"
        f"{table(project, releases)}\n"
    )


def splice(existing: str, project: str, releases: list[Release]) -> str:
    fresh = table(project, releases)
    if START in existing and END in existing:
        head, _, rest = existing.partition(START)
        return head + fresh + rest.partition(END)[2]
    return existing.rstrip() + "\n\n" + fresh + "\n"


def comment(project: str, new: list[Release], tags: set[str]) -> str:
    """One line per new build. A tag the table already listed is a re-upload or a hotfix build."""
    lines = []
    for r in sorted(new, key=lambda r: r.key):
        run = f"`/upstream {project.lower()} {r.tag}`"
        name = f"`{r.tag}`"
        if r.tag in tags:
            name += f" now ships {project} {r.shipped} and"
        elif r.differs:
            name += f" ships {project} {r.shipped} and"
        if r.prerelease:
            lines.append(f"- {name} is a prerelease. Scout it: {run}.")
        else:
            lines.append(
                f"- {name} is **stable**. Adoption is due within one RomMBat release: {run}."
            )
    return "\n".join(lines)


def plan(project: str, floor: str, releases: list[Release], issues: list[Issue]) -> list[Action]:
    """What to do for one project, given its releases and its existing tracking issues."""
    floor_key = check.floor_entry(floor)[1]
    lines: dict[str, list[Release]] = {}
    for r in releases:
        if r.key > floor_key:
            lines.setdefault(r.line, []).append(r)

    actions: list[Action] = []
    for line, members in sorted(lines.items()):
        name = title(project, line)
        mine = [i for i in issues if i.title == name]
        current = next((i for i in mine if i.open), None)
        if current is None:
            # A closed issue that already lists every release was closed on purpose; respect it
            # until upstream ships something it has not seen.
            seen = set().union(*(rows_in(i.body) for i in mine)) if mine else set()
            if all(listed(r, seen) for r in members):
                continue
            actions.append(Action("open", name, body=body(project, line, members, floor)))
            continue
        rows = rows_in(current.body)
        new = [r for r in members if not listed(r, rows)]
        if not new:
            continue
        tags = {tag for tag, _ in rows}
        actions.append(Action("update", name, current.number, splice(current.body, project, members)))
        actions.append(Action("comment", name, current.number, comment(project, new, tags)))
    return actions


# I/O


def gh(*args: str, stdin: str | None = None) -> str:
    return subprocess.run(
        ["gh", *args], input=stdin, capture_output=True, text=True, encoding="utf-8", check=True
    ).stdout


def fetch_releases(repo: str) -> list[Release]:
    raw = json.loads(gh("api", f"repos/{repo}/releases?per_page=50"))
    found = (
        normalize(
            r["tag_name"], r["prerelease"], r["published_at"] or "", [a["name"] for a in r["assets"]]
        )
        for r in raw
        if not r["draft"]
    )
    return [r for r in found if r]


def fetch_issues() -> list[Issue]:
    raw = json.loads(
        gh("issue", "list", "--label", LABEL, "--state", "all", "--limit", "200",
           "--json", "number,title,body,state")
    )
    return [Issue(i["number"], i["title"], i["body"], i["state"] == "OPEN") for i in raw]


def apply(action: Action) -> None:
    if action.kind == "open":
        gh("issue", "create", "--title", action.title, "--label", LABEL, "--body-file", "-", stdin=action.body)
    elif action.kind == "update":
        gh("issue", "edit", str(action.number), "--body-file", "-", stdin=action.body)
    else:
        gh("issue", "comment", str(action.number), "--body-file", "-", stdin=action.body)


def main(argv: list[str]) -> int:
    dry = "--dry-run" in argv
    floor = check.floors()
    issues = fetch_issues()
    actions = [
        action
        for project, repo in PROJECTS.items()
        for action in plan(project, floor[project][0], fetch_releases(repo), issues)
    ]
    if not dry and actions:
        gh("label", "create", LABEL, "--force", "--color", "5319e7",
           "--description", "A RomM or RetroBat release line to scout or adopt")
    for action in actions:
        target = f"#{action.number}" if action.number else "new"
        print(f"{action.kind:8} {target:6} {action.title}")
        if dry:
            print("    " + action.body.replace("\n", "\n    "))
        else:
            apply(action)
    if not actions:
        print("nothing above the floor that the tracking issues do not already list")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
