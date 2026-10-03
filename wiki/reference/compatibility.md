# Compatibility

Each RomMBat release names the RomM and RetroBat versions it was tested against.

| RomMBat       | RomM tested | RetroBat tested    |
| ------------- | ----------- | ------------------ |
| 0.1.0-alpha.1 | 5.3.1       | 8.2.1-stable-win64 |

The versions a release was tested against are also the oldest it accepts. RomMBat checks both
every time it starts: an older RomM or RetroBat is refused, with a message naming the version it
found and the one it needs, and a newer one works with a warning that it has not been tested yet.

RomMBat follows the newest stable RomM and RetroBat rather than supporting a range. A new
release of either is adopted within one RomMBat release, and the minimum moves up with it, so
update RomM and RetroBat when you update RomMBat. An older row in this table records what that
release was tested against, and is not a promise that it still works.
