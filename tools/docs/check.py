#!/usr/bin/env python3
"""Check the repository's documentation against the rules that keep it usable.

Two classes of check. An error fails the run: a relative link or anchor that does not
resolve, an em-dash, or a fact ID (`RB-<n>` for RetroBat, `RM-<n>` for RomM) cited but
defined nowhere. A report is printed and does not fail: the size budget, the always-loaded
context ceiling, missing frontmatter, history phrasing, legacy "finding N" citations, and
generic use of `dry-run`. Reports exist for rules the tree does not meet yet; each moves to
errors once the docs overhaul (issue #242) has brought the tree into line with it.

Usage:
  python tools/docs/check.py            check the tree, print errors and reports
  python tools/docs/check.py --quiet    errors only
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
    "reference/",
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
)

EM_DASH = "\u2014"
FACT_ID = re.compile(r"\b(RB|RM)-(\d+)\b")
FACT_HEADING = re.compile(r"^#{2,6}\s+((?:RB|RM)-\d+)\.\s")
LEGACY_CITATION = re.compile(r"\bfindings? \d+", re.IGNORECASE)
# `dry-run` names sync's flag and nothing else; a generic preview is a "preview".
GENERIC_DRY_RUN = re.compile(r"(?<![-`\w])dry-run(?!`)")

INLINE_LINK = re.compile(r"(?<!!)\[(?:[^\]\\]|\\.)*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
IMAGE_LINK = re.compile(r"!\[(?:[^\]\\]|\\.)*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
# `[^1]:` is a footnote, not a link.
REFERENCE_DEF = re.compile(r"^\s{0,3}\[(?!\^)[^\]]+\]:\s*<?(\S+?)>?(?:\s|$)")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
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
                findings.report("size", f"{rel}: {lines} lines, budget {budget}")
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

    history = legacy = dry_run = 0
    for number, line in prose_lines(text):
        history += sum(1 for p in HISTORY_PATTERNS if p.search(line))
        legacy += len(LEGACY_CITATION.findall(line))
        # A quoted "dry-run" is a mention of the word, as in the rule's own statement.
        if GENERIC_DRY_RUN.search(re.sub(r"\"[^\"]*\"", "", line)):
            dry_run += 1
            findings.report("dry-run", f"{rel}:{number}: generic dry-run; say preview")
        if defined_facts is not None and not FACT_HEADING.match(line):
            for prefix, digits in FACT_ID.findall(line):
                if f"{prefix}-{digits}" not in defined_facts:
                    findings.errors.append(f"{rel}:{number}: {prefix}-{digits} is defined nowhere")
    if history:
        findings.report("history", f"{rel}: {history} line(s) of history phrasing")
    if legacy:
        findings.report("legacy citations", f"{rel}: {legacy} bare 'finding N' citation(s)")


def defined_fact_ids(files: list[str]) -> set[str]:
    ids: set[str] = set()
    for rel in files:
        if rel.endswith(".md"):
            for line in (read_text(rel) or "").splitlines():
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
    return run_tree(quiet="--quiet" in argv)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
