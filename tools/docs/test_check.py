"""Tests for check.py's link and slug parsing. Run: python3 -m unittest discover -s tools/docs"""

from __future__ import annotations

import unittest

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


class FactIdTest(unittest.TestCase):
    def test_letter_suffix_is_one_id(self) -> None:
        self.assertEqual(check.FACT_ID.findall("RB-9b and RB-92b, not RB-9"), [
            ("RB", "9b"), ("RB", "92b"), ("RB", "9")
        ])
        self.assertEqual(check.FACT_HEADING.match("## RB-9c. Title").group(1), "RB-9c")


if __name__ == "__main__":
    unittest.main()
