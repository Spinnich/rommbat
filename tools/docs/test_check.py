"""Tests for check.py's link and slug parsing. Run: python3 -m unittest discover -s tools/docs"""

from __future__ import annotations

import io
import json
import unittest
from pathlib import Path
from unittest import mock

import check


def link_errors(rel: str, text: str) -> list[str]:
    findings = check.Findings()
    check.check_links(rel, text, findings)
    return findings.errors


class SlugifyTest(unittest.TestCase):
    def test_matches_github(self) -> None:
        cases = {
            "Six rules that override intuition": "six-rules-that-override-intuition",
            "The **loose** rule": "the-loose-rule",
            "The _loose_ rule": "the-loose-rule",
            "The __loose__ rule": "the-loose-rule",
            "save_rules.json is hand-edited": "save_rulesjson-is-hand-edited",
            "`es_settings.cfg` precedence": "es_settingscfg-precedence",
            "[Linked](docs/architecture/README.md) heading": "linked-heading",
        }
        for heading, slug in cases.items():
            with self.subTest(heading=heading):
                self.assertEqual(check.slugify(heading), slug)


class LinkTest(unittest.TestCase):
    def test_existing_file_and_anchor_resolve(self) -> None:
        self.assertEqual(link_errors("README.md", "[p](docs/architecture/README.md)\n"), [])
        self.assertEqual(
            link_errors("README.md", "[c](CLAUDE.md#six-rules-that-override-intuition)\n"), []
        )

    def test_attr_list_heading_id_is_the_anchor(self) -> None:
        # wiki/platforms/index.md is generated with `## Nintendo Entertainment System - Famicom {#nes}`.
        self.assertEqual(link_errors("wiki/index.md", "[n](platforms/index.md#nes)\n"), [])
        self.assertEqual(
            link_errors(
                "wiki/index.md",
                "[n](platforms/index.md#nintendo-entertainment-system---famicom-nes)\n",
            ),
            [
                "wiki/index.md:1: missing anchor: "
                "platforms/index.md#nintendo-entertainment-system---famicom-nes"
            ],
        )

    def test_attr_list_heading_id_counts_only_under_wiki(self) -> None:
        # GitHub renders `{#gb}` as text, so outside the guide the slug is the anchor.
        with mock.patch.object(check, "read_text", return_value="## Game Boy {#gb}\n"):
            for rel, expected in (("wiki/x.md", {"gb"}), ("docs/x.md", {"game-boy-gb"})):
                with self.subTest(rel=rel):
                    check._anchor_cache.pop(rel, None)
                    self.assertEqual(check.anchors_of(rel), expected)
                    check._anchor_cache.pop(rel, None)

    def test_directory_link_resolves(self) -> None:
        self.assertEqual(link_errors("README.md", "[d](docs/)\n"), [])

    def test_link_casing_must_match(self) -> None:
        self.assertEqual(
            link_errors("README.md", "[p](docs/plan.md)\n"),
            ["README.md:1: broken link: docs/plan.md"],
        )

    def test_gitignored_file_is_broken(self) -> None:
        self.assertEqual(
            link_errors("README.md", "[e](.env)\n"), ["README.md:1: broken link: .env"]
        )

    def test_link_leaving_the_repository(self) -> None:
        self.assertEqual(
            link_errors("README.md", "[x](../elsewhere.md)\n"),
            ["README.md:1: link leaves the repository: ../elsewhere.md"],
        )

    def test_footnote_is_not_a_reference_definition(self) -> None:
        self.assertEqual(link_errors("docs/x.md", "a[^1]\n\n[^1]: See the upstream thread.\n"), [])

    def test_reference_definition_is_checked(self) -> None:
        self.assertEqual(
            link_errors("docs/x.md", "[plan]: ./missing.md\n"),
            ["docs/x.md:1: broken link: ./missing.md"],
        )


class SizeBudgetTest(unittest.TestCase):
    def errors(self, rel: str, lines: int) -> list[str]:
        findings = check.Findings()
        check.check_file(rel, "line\n" * lines, findings, None)
        return findings.errors

    def test_over_budget_fails(self) -> None:
        self.assertEqual(self.errors("wiki/x.md", 501), ["wiki/x.md: 501 lines, budget 500"])
        self.assertEqual(
            self.errors(".claude/skills/x/SKILL.md", 301),
            [".claude/skills/x/SKILL.md: 301 lines, budget 300"],
        )
        self.assertEqual(self.errors("src/X/CLAUDE.md", 61), ["src/X/CLAUDE.md: 61 lines, budget 60"])

    def test_at_budget_passes(self) -> None:
        self.assertEqual(self.errors("wiki/x.md", 500), [])
        self.assertEqual(self.errors("CLAUDE.md", 200), [])


class HistoryPhrasingTest(unittest.TestCase):
    def errors(self, text: str) -> list[str]:
        findings = check.Findings()
        check.check_file("wiki/x.md", text, findings, None)
        return findings.errors

    def test_history_phrasing_fails_on_its_line(self) -> None:
        self.assertEqual(
            self.errors("Present.\n\n**Superseded**: now true\n"),
            ["wiki/x.md:3: history phrasing; state what is true now"],
        )

    def test_quoted_in_a_code_span_passes(self) -> None:
        self.assertEqual(self.errors("Phrasing such as `Superseded` is reported.\n"), [])

    def test_previously_label_fails(self) -> None:
        self.assertEqual(
            self.errors("## True\n\nPreviously: a replaced claim\n"),
            ["wiki/x.md:3: history phrasing; state what is true now"],
        )


class FactIdTest(unittest.TestCase):
    def test_letter_suffix_is_one_id(self) -> None:
        self.assertEqual(check.FACT_ID.findall("RB-9b and RB-92b, not RB-9"), [
            ("RB", "9b"), ("RB", "92b"), ("RB", "9")
        ])
        self.assertEqual(check.FACT_HEADING.match("## RB-92b. Title").group(1), "RB-92b")


FLOOR = {"RetroBat": ("8.2.1", check.version_key("8.2.1", None)),
         "RomM": ("5.3.1", check.version_key("5.3.1", None))}


class StaleTest(unittest.TestCase):
    def below(self, stamp: str) -> list[str]:
        return check.stamps_below(stamp, FLOOR)

    def test_stamp_below_the_floor(self) -> None:
        self.assertEqual(self.below("Verified: RetroBat 8.2.0, 2026-08-16. How: x."), ["RetroBat 8.2.0"])

    def test_bare_version_belongs_to_the_project_before_it(self) -> None:
        self.assertEqual(self.below("Verified: RetroBat 8.2.0, 2026-08-16, and 8.2.1, 2026-09-28."), [])
        self.assertEqual(self.below("Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1 source, 2026-09-29."), [])

    def test_each_project_is_held_to_its_own_floor(self) -> None:
        self.assertEqual(
            self.below("Verified: RomM 5.3.1, 2026-09-29, for the RomM half; RetroBat 8.2.0, 2026-08-11."),
            ["RetroBat 8.2.0"],
        )

    def test_prerelease_of_the_floor_is_below_it(self) -> None:
        self.assertEqual(self.below("Verified: RomM 5.3.1-beta.1, 2026-09-20."), ["RomM 5.3.1-beta.1"])
        self.assertEqual(self.below("Verified: RomM 5.3.1.0, 2026-09-20."), [])

    def test_retrobat_channel_suffix_is_not_a_prerelease(self) -> None:
        self.assertEqual(self.below("Verified: RetroBat 8.2.1-stable-win64, 2026-09-20."), [])

    def test_how_and_dates_and_other_software_are_ignored(self) -> None:
        self.assertEqual(
            self.below("Verified: Windows 11 26200, .NET 10, 2026-08-09. How: on RomM 5.2.0."), []
        )
        self.assertEqual(self.below("Verified: RomM 5.3.1, 2026-09-29. How: also ran on 5.2.0."), [])

    def test_fact_stamps_pairs_each_fact_with_its_own_stamp(self) -> None:
        text = (
            "# Title\n\n## RB-379. One\n\nVerified: RetroBat 8.2.1, 2026-09-28.\nBody.\n\n"
            "## RB-380. Two\n\nMeasured: no stamp.\n\n## Not a fact\n\nVerified: RomM 5.2.0.\n"
        )
        self.assertEqual(
            list(check.fact_stamps(text)),
            [(3, "RB-379", "Verified: RetroBat 8.2.1, 2026-09-28."), (8, "RB-380", None)],
        )

    def test_floor_is_read_from_code(self) -> None:
        floor = check.floors()
        self.assertEqual(set(floor), {"RetroBat", "RomM"})


class HookTest(unittest.TestCase):
    def run_hook(self, file_path: str) -> int:
        stdin = io.StringIO(json.dumps({"tool_input": {"file_path": file_path}}))
        with mock.patch("sys.stdin", stdin):
            return check.run_hook()

    def test_worktree_file_goes_to_the_worktrees_own_checker(self) -> None:
        worktree = check.ROOT / ".claude" / "worktrees" / "issue-1-x"
        edited = worktree / ".claude" / "commands" / "start-issue.md"
        own_checker = worktree / "tools" / "docs" / "check.py"
        real_is_file = Path.is_file
        with (
            mock.patch.object(
                Path, "is_file", lambda p: p == own_checker or real_is_file(p)
            ),
            mock.patch("check.delegate_hook", return_value=2) as delegate,
        ):
            self.assertEqual(self.run_hook(str(edited)), 2)
        checker, call = delegate.call_args.args
        self.assertEqual(checker, own_checker)
        self.assertEqual(call["tool_input"]["file_path"], str(edited))

    def test_main_checkout_file_is_checked_here(self) -> None:
        with mock.patch("check.delegate_hook") as delegate:
            self.assertEqual(self.run_hook(str(check.ROOT / "README.md")), 0)
        delegate.assert_not_called()


if __name__ == "__main__":
    unittest.main()
