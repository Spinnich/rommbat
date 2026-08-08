# Security Policy

## Reporting Security Issues

Thanks for helping make RomMBat safer for everyone.

If you believe you have found a security vulnerability in RomMBat, please report it to us through coordinated disclosure.

**Do not report security vulnerabilities through public GitHub issues, discussions, pull requests, or on the public Discord server.**

Instead, use the [vulnerability report form](https://github.com/rommapp/rommbat/security/advisories/new) on GitHub.

Please include as much of the information listed below as you can to help us better understand and resolve the issue:

- The type of issue (e.g., credential exposure, path traversal, remote code execution, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue (including how an attacker might exploit the issue)

This information will help us investigate and patch the issue more quickly.

If the issue is in the RomM server rather than in RomMBat, report it against
[rommapp/romm](https://github.com/rommapp/romm/security/advisories/new) instead.

## Scope notes specific to RomMBat

Two properties of this app are deliberate design choices rather than
vulnerabilities, and are documented so a report can skip them:

- **The API token on a portable install is only as protected as the drive.**
  RomMBat runs from a USB stick that moves between machines, so DPAPI is
  unavailable: `CurrentUser` binds the ciphertext to one user profile and
  `LocalMachine` to one machine, and either would make the drive undecryptable
  on the next PC. The mitigations are a scoped, expiring token by default, an
  optional passphrase-derived key, and cheap re-pairing. See core principle 4
  in [docs/PLAN.md](docs/PLAN.md).
- **RomMBat writes into the RetroBat tree**, including `gamelist.xml` and
  `es_settings.cfg`. That is the integration mechanism. Reports about RomMBat
  modifying RetroBat's own configuration are expected behaviour, though a write
  **outside** the RetroBat tree is a real bug and worth reporting.

## Please do report

- Any path by which a token, a server URL or a credential reaches the repository,
  a log file, a crash report or an issue template.
- Any write outside the RetroBat tree.
- Path traversal through a ROM, save or firmware filename supplied by the server.
- Anything that causes RomMBat to trust a server response far enough to execute it.
