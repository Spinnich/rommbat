#!/usr/bin/env python3
"""Check the repository's documentation against the rules that keep it usable.

Two classes of check. An error fails the run: a relative link or anchor that does not
resolve, an em-dash, a fact ID (`RB-<n>` for RetroBat, `RM-<n>` for RomM) cited but defined
nowhere, history phrasing, or a Markdown file over its line budget. A report is printed and does
not fail: the always-loaded context ceiling, missing frontmatter, legacy "finding N" citations,
generic use of `dry-run`, and British spellings in prose. Reports exist for rules the tree does not meet yet, or,
for the context ceiling, a rule that counts the maintainer's local MEMORY.md, which CI never sees.

Usage:
  python tools/docs/check.py            check the tree, print errors and reports
  python tools/docs/check.py --quiet    errors only
  python tools/docs/check.py --stale    list the facts owed a re-check at the current floor: each
                                        fact whose `Verified:` stamp names a RetroBat or RomM
                                        build below it, and each fact with no stamp
  python tools/docs/check.py --hook     PostToolUse hook: read the tool call from stdin and
                                        check the one file it wrote, exit 2 on an error
"""

from __future__ import annotations

import json
import os
import posixpath
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

# Byte-exact or verbatim content that no house rule applies to.
EXCLUDED_PREFIXES = (
    # reference/README.md is hand-written and checked; everything beside it is vendored.
    "reference/batocera-",
    "reference/es_",
    "reference/romm-",
    "reference/systems_",
    "LICENSE",
    "src/RomM.Client/openapi/romm-",
    "src/RomM.Client/Generated/",
)
EXCLUDED_PARTS = ("/fixtures/",)

# Hand-written Markdown line budgets. The first matching rule wins.
LINE_BUDGETS = (
    (re.compile(r"(^|/)SKILL\.md$"), 300),
    (re.compile(r"^CLAUDE\.md$"), 200),
    (re.compile(r"(^|/)CLAUDE\.md$"), 60),
    (re.compile(r"\.md$"), 500),
)

# Everything Claude Code loads into every session, in bytes: the root and nested CLAUDE.md
# files, AGENTS.md, and the maintainer's local MEMORY.md index when it exists.
CONTEXT_CEILING = 32 * 1024

# Only docs/ carries frontmatter; the guide and the agent layer have their own conventions.
FRONTMATTER_SCOPE = "docs/"
FRONTMATTER_KEYS = ("summary", "read-when")

# Phrasing that records how something used to be. Docs describe the present; git is the
# history.
HISTORY_PATTERNS = (
    re.compile(r"\bThe move to\b"),
    re.compile(r"\bSuperseded\b"),
    re.compile(r"\bearlier revision\b", re.IGNORECASE),
    re.compile(r"\bMeasured during M\d"),
    # The findings ledgers' label for a replaced claim. A claim a fact tests is "The claim being checked:".
    re.compile(r"\bPreviously:"),
    # A ledger question since settled. The heading states the answer instead.
    re.compile(r"\(not addressed\)"),
    re.compile(r"^Question:"),
)

EM_DASH = "\u2014"
# A letter suffix (RB-9b) is an ID the findings ledgers had already split before IDs were fixed.
FACT_ID = re.compile(r"\b(RB|RM)-(\d+[a-z]?)\b")
FACT_HEADING = re.compile(r"^#{2,6}\s+((?:RB|RM)-\d+[a-z]?)\.\s")
LEGACY_CITATION = re.compile(r"\bfindings? \d+", re.IGNORECASE)
# `dry-run` names sync's flag and nothing else; a generic preview is a "preview".
GENERIC_DRY_RUN = re.compile(r"(?<![-`\w])dry-run(?!`)")
# The house style is American English. A list, not a dictionary: these are the British forms
# the tree has actually used, and -ise is matched by stem because "advertise" and "exercise"
# are American too.
BRITISH_SPELLING = re.compile(
    r"(?<![\w.$])(?<!--)(?:"
    r"behaviours?|colours?|favour(?:s|ed|ing|ites?|ited|iting)?|flavours?|honour(?:s|ed|ing)?|candour"
    r"|neighbour(?:s|ing)?|licences?|defences?|centres?|centred|centring|catalogues?|analogue"
    r"|artefacts?|judgement|acknowledgement|(?:un)?cancell(?:ed|ing)|(?:mis|un)?labelled"
    r"|journalled|marshalled|modelled|travelled|totalling|whilst|amongst"
    r"|(?:capital|categor|character|deserial|serial|unserial|final|general|ideal|initial"
    r"|uninitial|local|material|normal|denormal|unnormal|optim|organ|reorgan|parameter"
    r"|parenthes|plural|quant|random|recogn|unrecogn|relativ|sanit|summar|synthes|real|util"
    r"|priorit|custom|minim|maxim|author|synchron|special|standard|visual|token|stabil"
    r"|central|emphas|critic|apolog)is(?:e|es|ed|ing|er|ers|ation|ations|able|ably)"
    r")(?!\w)",
    re.IGNORECASE,
)
# Prettier pairs a prose $ with the next one, even one inside a later code span, as inline math
# and strips the spaces around the code spans between them. NO$GBA is the usual source.
BARE_DOLLAR = re.compile(r"(?<!\\)\$")
# Python-Markdown does not unescape \$, so the guide under wiki/ writes &#36; instead.
# Claude Code substitutes $1 and $ARGUMENTS in a slash command's text.
DOLLAR_EXEMPT = ".claude/commands/"

INLINE_LINK = re.compile(r"(?<!!)\[(?:[^\]\\]|\\.)*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
IMAGE_LINK = re.compile(r"!\[(?:[^\]\\]|\\.)*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
# `[^1]:` is a footnote, not a link.
REFERENCE_DEF = re.compile(r"^\s{0,3}\[(?!\^)[^\]]+\]:\s*<?(\S+?)>?(?:\s|$)")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
# The guide's attr_list heading id, `## Game Boy {#gb}`, which replaces the slug in MkDocs.
# Honored under wiki/ only.
HEADING_ID = re.compile(r"\{\s*#([\w-]+)[^}]*\}\s*$")
HTML_ANCHOR = re.compile(r"<a\s+(?:[^>]*\s)?(?:id|name)=\"([^\"]+)\"", re.IGNORECASE)
CODE_SPAN = re.compile(r"(`+)(.+?)\1")
FENCE = re.compile(r"^\s{0,3}(`{3,}|~{3,})")


@dataclass
class Findings:
    errors: list[str] = field(default_factory=list)
    reports: dict[str, list[str]] = field(default_factory=dict)

    def report(self, kind: str, message: str) -> None:
        self.reports.setdefault(kind, []).append(message)


def tracked_files() -> list[str]:
    out = subprocess.run(
        ["git", "ls-files", "-z"], cwd=ROOT, capture_output=True, check=True
    ).stdout
    return [p for p in out.decode("utf-8").split("\0") if p and is_checked(p)]


_present: set[str] | None = None


def present_paths() -> set[str]:
    """Files git would publish and their folders, in exact case.

    The filesystem is the wrong oracle: Windows matches a link case-insensitively and sees
    ignored files, while ubuntu CI and GitHub see neither. Untracked files count, so the hook
    accepts a link to a doc written moments ago and not yet added.
    """
    global _present
    if _present is None:
        out = subprocess.run(
            ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            cwd=ROOT,
            capture_output=True,
            check=True,
        ).stdout
        paths = {"."}
        for rel in out.decode("utf-8").split("\0"):
            # --cached still lists a file deleted from the working tree.
            if rel and (ROOT / rel).is_file():
                while rel and rel not in paths:
                    paths.add(rel)
                    rel = posixpath.dirname(rel)
        _present = paths
    return _present


def is_checked(rel: str) -> bool:
    if rel.startswith(EXCLUDED_PREFIXES):
        return False
    return not any(part in f"/{rel}" for part in EXCLUDED_PARTS)


def read_text(rel: str) -> str | None:
    try:
        return (ROOT / rel).read_bytes().decode("utf-8")
    except (UnicodeDecodeError, FileNotFoundError, IsADirectoryError):
        return None


def prose_lines(text: str):
    """Yield (line number, line) outside fenced code, with inline code spans blanked."""
    fence: str | None = None
    for number, line in enumerate(text.splitlines(), 1):
        match = FENCE.match(line)
        if match:
            marker = match.group(1)
            if fence is None:
                fence = marker[0] * len(marker)
                continue
            if marker.startswith(fence):
                fence = None
                continue
        if fence is None:
            yield number, CODE_SPAN.sub(lambda m: " " * len(m.group(0)), line)


def slugify(heading: str) -> str:
    """GitHub's heading anchor: rendered text, lowercased, punctuation dropped, spaces to -."""
    text = re.sub(r"!?\[([^\]]*)\]\([^)]*\)", r"\1", heading)
    text = re.sub(r"<[^>]+>", "", text)
    # Underscore emphasis renders away; an intraword underscore, as in save_rules, stays.
    text = re.sub(r"(?<!\w)(_+)(?=\S)(.+?)(?<=\S)\1(?!\w)", r"\2", text)
    text = text.replace("`", "").lower()
    text = re.sub(r"[^\w\- ]", "", text)
    return text.replace(" ", "-")


_anchor_cache: dict[str, set[str]] = {}


def anchors_of(rel: str) -> set[str]:
    if rel in _anchor_cache:
        return _anchor_cache[rel]
    text = read_text(rel) or ""
    anchors: set[str] = set()
    seen: dict[str, int] = {}
    fence: str | None = None
    for line in text.splitlines():
        match = FENCE.match(line)
        if match:
            marker = match.group(1)
            if fence is None:
                fence = marker[0] * len(marker)
            elif marker.startswith(fence):
                fence = None
            continue
        if fence is not None:
            continue
        anchors.update(a.lower() for a in HTML_ANCHOR.findall(line))
        heading = HEADING.match(line)
        if heading:
            # Only MkDocs honors the id; GitHub renders it as text and keeps the slug.
            explicit = rel.startswith("wiki/") and HEADING_ID.search(heading.group(2))
            if explicit:
                anchors.add(explicit.group(1).lower())
                continue
            slug = slugify(heading.group(2))
            count = seen.get(slug, 0)
            seen[slug] = count + 1
            anchors.add(slug if count == 0 else f"{slug}-{count}")
    _anchor_cache[rel] = anchors
    return anchors


def check_links(rel: str, text: str, findings: Findings) -> None:
    base = posixpath.dirname(rel)
    for number, line in prose_lines(text):
        targets = INLINE_LINK.findall(line) + IMAGE_LINK.findall(line)
        ref = REFERENCE_DEF.match(line)
        if ref:
            targets.append(ref.group(1))
        for target in targets:
            if re.match(r"^[a-z][a-z0-9+.-]*:", target, re.IGNORECASE):
                continue
            path_part, _, anchor = target.partition("#")
            if path_part:
                # Path.resolve() would take the on-disk casing on Windows and hide a mismatch.
                target_rel = posixpath.normpath(posixpath.join(base, path_part))
                if target_rel == ".." or target_rel.startswith("../"):
                    findings.errors.append(f"{rel}:{number}: link leaves the repository: {target}")
                    continue
                if target_rel not in present_paths():
                    findings.errors.append(f"{rel}:{number}: broken link: {target}")
                    continue
            else:
                target_rel = rel
            if anchor and target_rel.endswith(".md") and (ROOT / target_rel).is_file():
                if anchor.lower() not in anchors_of(target_rel):
                    findings.errors.append(f"{rel}:{number}: missing anchor: {target}")


def check_file(rel: str, text: str, findings: Findings, defined_facts: set[str] | None) -> None:
    for number, line in enumerate(text.splitlines(), 1):
        if EM_DASH in line:
            findings.errors.append(f"{rel}:{number}: em-dash")

    if not rel.endswith(".md"):
        return

    check_links(rel, text, findings)

    lines = text.count("\n") + (0 if text.endswith("\n") or not text else 1)
    for pattern, budget in LINE_BUDGETS:
        if pattern.search(rel):
            if lines > budget:
                findings.errors.append(f"{rel}: {lines} lines, budget {budget}")
            break

    if rel.startswith(FRONTMATTER_SCOPE):
        header = re.match(r"---\n(.*?)\n---\n", text, re.DOTALL)
        missing = [
            key
            for key in FRONTMATTER_KEYS
            if not header or not re.search(rf"^{key}:", header.group(1), re.MULTILINE)
        ]
        if missing:
            findings.report("frontmatter", f"{rel}: missing {', '.join(missing)}")

    legacy = dry_run = 0
    for number, line in prose_lines(text):
        if any(p.search(line) for p in HISTORY_PATTERNS):
            findings.errors.append(f"{rel}:{number}: history phrasing; state what is true now")
        if not rel.startswith(DOLLAR_EXEMPT) and BARE_DOLLAR.search(line):
            findings.errors.append(
                f"{rel}:{number}: bare $, which prettier reads as inline math; write \\$, or &#36; under wiki/"
            )
        if rel.startswith("wiki/") and "\\$" in line:
            findings.errors.append(
                f"{rel}:{number}: \\$, which MkDocs prints with its backslash; write &#36;"
            )
        legacy += len(LEGACY_CITATION.findall(line))
        # A quoted "dry-run" is a mention of the word, as in the rule's own statement.
        if GENERIC_DRY_RUN.search(re.sub(r"\"[^\"]*\"", "", line)):
            dry_run += 1
            findings.report("dry-run", f"{rel}:{number}: generic dry-run; say preview")
        # A quotation keeps its source's spelling; a dotted or --flag word is a name.
        for word in BRITISH_SPELLING.findall(re.sub(r"\"[^\"]*\"", "", line)):
            findings.report("spelling", f"{rel}:{number}: {word}; the house style is American English")
        if defined_facts is not None and not FACT_HEADING.match(line):
            for prefix, digits in FACT_ID.findall(line):
                if f"{prefix}-{digits}" not in defined_facts:
                    findings.errors.append(f"{rel}:{number}: {prefix}-{digits} is defined nowhere")
    if legacy:
        findings.report("legacy citations", f"{rel}: {legacy} bare 'finding N' citation(s)")


def defined_fact_ids(files: list[str]) -> set[str]:
    ids: set[str] = set()
    for rel in files:
        if rel.endswith(".md"):
            # An example heading inside a code fence defines nothing.
            for _, line in prose_lines(read_text(rel) or ""):
                match = FACT_HEADING.match(line)
                if match:
                    ids.add(match.group(1))
    return ids


def check_code_citations(files: list[str], defined: set[str], findings: Findings) -> None:
    """Fact IDs cited from code comments and scripts resolve too."""
    for rel in files:
        if rel.endswith(".md"):
            continue
        text = read_text(rel)
        if text is None:
            continue
        for number, line in enumerate(text.splitlines(), 1):
            for prefix, digits in FACT_ID.findall(line):
                if f"{prefix}-{digits}" not in defined:
                    findings.errors.append(f"{rel}:{number}: {prefix}-{digits} is defined nowhere")


def memory_index() -> Path | None:
    """The maintainer's auto-memory index, if this checkout has one on this machine."""
    slug = re.sub(r"[^A-Za-z0-9]", "-", str(ROOT))
    home = Path(os.environ.get("USERPROFILE") or Path.home())
    for candidate in (slug, slug[0].lower() + slug[1:]):
        path = home / ".claude" / "projects" / candidate / "memory" / "MEMORY.md"
        if path.is_file():
            return path
    return None


def check_context(files: list[str], findings: Findings) -> None:
    loaded = [ROOT / rel for rel in files if rel.endswith(("CLAUDE.md", "AGENTS.md"))]
    memory = memory_index()
    if memory:
        loaded.append(memory)
    total = sum(p.stat().st_size for p in loaded if p.is_file())
    if total > CONTEXT_CEILING:
        findings.report(
            "context",
            f"always-loaded context is {total:,} bytes across {len(loaded)} file(s), "
            f"ceiling {CONTEXT_CEILING:,}",
        )


def run_tree(quiet: bool) -> int:
    files = tracked_files()
    findings = Findings()
    defined = defined_fact_ids(files)
    for rel in files:
        text = read_text(rel)
        if text is not None:
            check_file(rel, text, findings, defined)
    check_code_citations(files, defined, findings)
    check_context(files, findings)

    if not quiet:
        for kind, messages in sorted(findings.reports.items()):
            print(f"report: {kind} ({len(messages)})")
            for message in messages:
                print(f"  {message}")
    for error in findings.errors:
        print(f"error: {error}", file=sys.stderr)
    print(
        f"{len(files)} files checked, {len(findings.errors)} error(s), "
        f"{sum(len(m) for m in findings.reports.values())} report line(s)"
    )
    return 1 if findings.errors else 0


# The floor lives in code, so a move there moves what --stale reports without a doc edit.
FLOOR_SOURCES = {
    "RetroBat": "src/RomMBat.Core/Diagnostics/RetroBatVersion.cs",
    "RomM": "src/RomM.Client/RomMServerVersion.cs",
}
FLOOR = re.compile(r"\bMinimum\s*\{\s*get;\s*\}\s*=\s*ProductVersion\.Parse\(\"([^\"]+)\"\)")
STAMP = re.compile(r"^Verified:")
# A bare version belongs to the project named before it: "RetroBat 8.2.0, 2026-08-16, and 8.2.1".
STAMP_TOKEN = re.compile(r"\b(RetroBat|RomM)\b|\b(\d+(?:\.\d+)+)(-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?")


PRERELEASE = re.compile(r"^(alpha|beta|rc)\b", re.IGNORECASE)


def version_key(core: str, suffix: str | None) -> tuple:
    """Numeric components, then a release above any prerelease of it.

    Stricter than ProductVersion, which ranks 5.3.1-beta.1 equal to 5.3.1 because a gate should
    be lenient. A fact measured on a prerelease of the floor is owed a re-check on the release.
    RetroBat's own suffix (8.2.1-stable-win64) names a channel, not a prerelease.
    """
    numbers = [int(n) for n in core.split(".")]
    while len(numbers) > 1 and numbers[-1] == 0:
        numbers.pop()
    return (tuple(numbers), not (suffix and PRERELEASE.match(suffix)))


def floor_entry(raw: str) -> tuple[str, tuple]:
    core, _, suffix = raw.partition("-")
    return (raw, version_key(core, suffix))


def floors(overrides: dict[str, str] | None = None) -> dict[str, tuple[str, tuple]]:
    """The floor in code, or a proposed one, so a scout can list what adopting it will owe."""
    result = {}
    for project, rel in FLOOR_SOURCES.items():
        if overrides and project in overrides:
            result[project] = floor_entry(overrides[project])
            continue
        match = FLOOR.search(read_text(rel) or "")
        if not match:
            sys.exit(f"no ProductVersion Minimum in {rel}")
        result[project] = floor_entry(match.group(1))
    return result


def floor_overrides(argv: list[str]) -> dict[str, str]:
    """Each --floor <project>=<version>, the project matched case-insensitively."""
    names = {project.lower(): project for project in FLOOR_SOURCES}
    result = {}
    for i, arg in enumerate(argv):
        if arg != "--floor":
            continue
        value = argv[i + 1] if i + 1 < len(argv) else ""
        name, _, version = value.partition("=")
        if name.lower() not in names or not re.fullmatch(r"\d+(\.\d+)+(-\S+)?", version):
            sys.exit(f"--floor wants <project>=<version>, project one of {', '.join(FLOOR_SOURCES)}")
        result[names[name.lower()]] = version
    return result


def stamps_below(stamp: str, floor: dict[str, tuple[str, tuple]]) -> list[str]:
    """The builds a stamp names that sit below the floor, the newest per project."""
    newest: dict[str, tuple[tuple, str]] = {}
    project = None
    # How: describes the method, and may name an older build a step was also run on.
    for name, core, suffix in STAMP_TOKEN.findall(stamp.partition("How:")[0]):
        if name:
            project = name
        elif project:
            key = version_key(core, suffix[1:] or None)
            if project not in newest or key > newest[project][0]:
                newest[project] = (key, f"{project} {core}{suffix}")
    return [label for project, (key, label) in sorted(newest.items()) if key < floor[project][1]]


def fact_stamps(text: str):
    """Yield (line, fact ID, its Verified line or None) for each fact heading."""
    current: list | None = None
    for number, line in prose_lines(text):
        heading = FACT_HEADING.match(line)
        if heading or HEADING.match(line):
            if current:
                yield tuple(current)
            current = [number, heading.group(1), None] if heading else None
        elif current and current[2] is None and STAMP.match(line):
            current[2] = line
    if current:
        yield tuple(current)


def run_stale(overrides: dict[str, str] | None = None) -> int:
    floor = floors(overrides)
    stale: list[str] = []
    unstamped: list[str] = []
    for rel in tracked_files():
        if not rel.endswith(".md"):
            continue
        for number, fact, stamp in fact_stamps(read_text(rel) or ""):
            if stamp is None:
                unstamped.append(f"{rel}:{number}: {fact}")
            else:
                below = stamps_below(stamp, floor)
                if below:
                    stale.append(f"{rel}:{number}: {fact}, {', '.join(below)}")

    print("floor: " + ", ".join(f"{project} {raw}" for project, (raw, _) in floor.items()))
    print(f"stale ({len(stale)}): stamped below the floor")
    for line in stale:
        print(f"  {line}")
    print(f"unstamped ({len(unstamped)}): no Verified line")
    for line in unstamped:
        print(f"  {line}")
    return 0


WORKTREES = ".claude/worktrees/"


def delegate_hook(checker: Path, call: dict) -> int:
    return subprocess.run(
        [sys.executable, str(checker), "--hook"], input=json.dumps(call), text=True
    ).returncode


def run_hook() -> int:
    try:
        call = json.load(sys.stdin)
    except json.JSONDecodeError:
        return 0
    path = (call.get("tool_input") or {}).get("file_path")
    if not path:
        return 0
    try:
        rel = Path(path).resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return 0
    # The hook runs from the main checkout, so a file in one of its worktrees would resolve its
    # links against the wrong tree. That worktree's own copy of this script checks it instead.
    if rel.startswith(WORKTREES):
        checker = ROOT / WORKTREES / rel[len(WORKTREES) :].split("/")[0] / "tools/docs/check.py"
        return delegate_hook(checker, call) if checker.is_file() else 0
    if not is_checked(rel):
        return 0
    text = read_text(rel)
    if text is None:
        return 0
    findings = Findings()
    # A single file cannot see where facts are defined, so citations are left to the full run.
    check_file(rel, text, findings, None)
    if not findings.errors:
        return 0
    print("tools/docs/check.py found problems in the file just written:", file=sys.stderr)
    for error in findings.errors:
        print(f"  {error}", file=sys.stderr)
    return 2


def main(argv: list[str]) -> int:
    if "--hook" in argv:
        return run_hook()
    if "--stale" in argv:
        return run_stale(floor_overrides(argv))
    return run_tree(quiet="--quiet" in argv)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
