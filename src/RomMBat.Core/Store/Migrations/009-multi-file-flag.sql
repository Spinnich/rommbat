-- Migration 009: carry has_multiple_files on the membership, instead of assuming it.
--
-- ContentSync built its RomContentRequest with IsMultiFile hardcoded to false, because the
-- membership did not carry the flag. That is correct by construction today: SetResolver
-- excludes a multi-file rom before the extension check, and every multi-file rom in the
-- measured 2,000-rom sample carries an empty fs_extension (105 of 105 both ways), so none
-- reaches a plan.
--
-- The cost is that the client's own multi-file guard is unreachable from the shipped path.
-- DownloadRomContentAsync refuses a ranged request for a multi-file rom, and
-- DescribeContentFailureAsync words the nginx 403 that would otherwise surface as an
-- unexplained scope error. Both are exercised only by tests, so a later change to the
-- resolver could re-admit multi-file roms and silently lean on a guard nobody has run
-- against a real one.
--
-- Written for every member, not only the excluded ones. A flag that is only ever set on rows
-- that never reach ContentSync is the same assumption wearing a column.
--
-- NOT NULL DEFAULT 0 rather than nullable: false is what every existing row means, since a
-- row that reached the membership as a member is a rom the resolver already established is
-- not multi-file. The next resolution overwrites all of them with the server's own answer.

ALTER TABLE sync_set_member ADD COLUMN has_multiple_files INTEGER NOT NULL DEFAULT 0
  CHECK (has_multiple_files IN (0, 1));
