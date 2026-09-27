---
summary: The test strategy: which suites RomMBat keeps, what each asserts, and the end-to-end pass a release owes.
read-when: Adding a test suite, deciding what a change must be tested against, or planning an end-to-end pass.
---

# Verification

- **Unit:** platform and save-directory mapping, gamelist merge (round-trip a real
  RetroBat `gamelist.xml` and assert user fields survive), slot derivation, hash matching,
  sync-set resolution and eviction ordering, outbox replay idempotency. Fixtures from a
  real install, checked in.
- **Mapping coverage, as a checked-in regression:** assert every bundled mapping resolves
  to a folder that exists in `systems_names.lst` (this catches the 18 stale entries today
  and will catch future drift), assert the multi-folder slugs resolve deterministically
  given a fixture `es_systems.cfg`, and assert that two platforms sharing a folder produce
  one merged gamelist rather than two competing writes. Track the unmapped count as a
  visible number so it cannot silently grow.
- **Gamelist merge, against a fixture taken from a real install:** round-trip an ES-written
  `gamelist.xml` and assert `playcount`, `lastplayed`, `gametime`, `scrap` with its
  attributes, `id` and `source` on `<game>`, `cheevosHash` and every other node RomMBat does
  not own survive untouched, while the fields it does own are updated. Assert an ampersand, a
  non-ASCII title and a control character in a description all come back out parseable, since
  a gamelist ES cannot parse loses the whole system rather than one entry.
- **Offline simulation:** the highest-value test suite. Drive the whole client against a
  stubbed handler that can be switched to "unreachable" mid-operation, and assert that
  every operation either completes locally or queues, and that a subsequent flush is
  idempotent under replay and partial failure.
- **Scale simulation:** run sync-set resolution and gamelist generation against a
  synthetic 100k-ROM catalog fixture and assert bounded memory and bounded request count.
- **Portability:** a test that relocates a populated install (different root path,
  simulating a drive-letter change) and asserts the next sync is a clean no-op. Add a
  static check that fails the build if any absolute path reaches the database, and a
  FAT32-constraint test for the 4 GB ceiling and coarse mtime handling.
- **Integration against a live RomM:** run one locally per `DEVELOPER_SETUP.md` and
  exercise pair → resolve set → pull → negotiate → upload → complete end to end. Assert
  the device, `sync_config`, saves and play sessions land in the RomM UI.

  Pairing-only auth does **not** force browser automation here. `GET /api/auth/device/pending/{user_code}`
  and `POST /api/auth/device/approve` are ordinary protected routes needing `me.read` and
  `me.write`, so a test harness holding a pre-made token can play the part of the
  approving user and drive the real flow headlessly. Do it that way rather than adding a
  token-injection backdoor to the app: the shipped client then has exactly one auth path,
  and the tests still cover it. Cover the narrowed-scope grant and the denied and expired
  branches too, since those are reachable from the same harness.

- **End to end on Windows:** a RetroBat VM or spare box is required, there is no
  substitute. Full pass: install, pair, define a set, sync it, confirm ES shows art, launch
  a game, save, exit, **disconnect the network**, play two more games, reconnect, confirm
  all saves and play sessions arrive, then change a save on another client and confirm the
  conflict surfaces. Then **move the drive to a second machine under a different letter**
  and confirm the next sync is a no-op and RomM still shows exactly one device.
- **Regression:** re-run a sync with no changes and assert zero uploads, zero downloads and
  no gamelist churn. That is the single best signal that slots, cursors and set resolution
  are all correct.

  **"No churn" is a claim about the file ES leaves behind, not the one RomMBat wrote.** ES
  leaves a gamelist byte-identical when it has nothing to change, so the test is meaningful;
  but once a game has been played, ES reorders the entries, rewrites that entry's children
  into its own order and drops every comment, so the second write has to be a no-op against
  that file rather than against its own previous output. See findings 103 to 105.
