<!-- markdownlint-disable MD033 MD041 -->
<!-- Renders as a PR body, not a document: no H1, and <sup> for the hint text. -->

**Description**
<sup>Explain the changes or enhancements you are proposing with this pull request.</sup>

**AI assistance disclosure**
<sup>Required. See <a href="https://github.com/rommapp/rommbat/blob/main/CONTRIBUTING.md#ai-assistance-notice">CONTRIBUTING.md</a>. State whether AI assistance was used and to what extent, including for the PR text itself. RomMBat is developed primarily with Claude Code, so "written primarily by Claude Code" is the expected answer, not an admission.</sup>

**Checklist**
<sup>Please check all that apply.</sup>

- [ ] I've disclosed any AI assistance above
- [ ] I've tested the changes locally
- [ ] I've updated relevant comments
- [ ] I've assigned reviewers for this PR
- [ ] I've added unit tests that cover the changes
- [ ] `pwsh -File tools/pre-pr.ps1` passes: build, tests, `trunk check`, the docs check and the guide build
- [ ] `cd reference && python3 verify.py` still passes, or the drift is explained below

**Invariants**
<sup>Tick only what the change actually touches. See the <code>pre-pr-verification</code> skill.</sup>

- [ ] No absolute path reaches the database
- [ ] No emulator INI was written; configuration goes through `es_settings.cfg`
- [ ] Nothing is written outside the RetroBat tree
- [ ] No token, secret or instance URL appears in the diff
- [ ] Every new user-visible string is reachable without a mouse

**Hands-on**
<sup>What was driven on a real install, on which build (the line in <code>emulators/rommbat/deployed.txt</code>), and what each screenshot or output showed. See "Hands-on by change type" in the <code>pre-pr-verification</code> skill. "Not owed: docs only" or "Unproven: &lt;reason&gt;" are answers; a blank is not.</sup>

**Docs**
<sup>Which docs moved, and which you read and found already correct. See "Documentation parity" in the <code>pre-pr-verification</code> skill. "Docs unchanged" with nothing read reads as not having looked.</sup>

**Compatibility**
<sup>Does this change the minimum supported RomM or RetroBat version? If so, say which and why, and update the requirements table in the README and the guide's compatibility and requirements pages.</sup>

**Platforms certified**
<sup>If this touches a platform, name the systems you ran the certification checklist against, and link the <code>docs/platforms/&lt;system&gt;/</code> records. A platform is not done at eight of nine.</sup>

#### Screenshots (if applicable)
