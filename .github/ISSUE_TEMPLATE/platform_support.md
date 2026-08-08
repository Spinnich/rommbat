---
name: Platform support
about: Report a RetroBat system that does not sync correctly, or request one
title: "[Platform]"
labels: platform
assignees: ""
---

<!-- markdownlint-disable MD036 -->
<!-- Bold section labels are the GitHub issue-template convention, not headings. -->

**RetroBat system folder**
The folder name as it appears in `es_systems.cfg` (`snes`, `ps2`, `fbneo`, ...).

**RomM platform**
The platform name and slug in your RomM library.

**What stage fails**
Tick everything that applies. These follow the certification checklist in
`docs/platforms/`.

- [ ] The platform resolves to the wrong RetroBat folder, or to none
- [ ] Games are excluded that should not be, or included that will not launch
- [ ] Required BIOS is not found, or lands in the wrong place
- [ ] Battery saves do not round-trip
- [ ] Save states do not round-trip
- [ ] The game launches but shows no art or metadata
- [ ] Play sessions do not reach RomM
- [ ] Re-syncing is not a clean no-op

**Details**
What you expected, what happened, and the file names involved.

**Versions**

- RomMBat version:
- RetroBat version:
- RomM version:
- Emulator and core, if known:
