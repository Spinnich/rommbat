# Requirements

You need three things: a RetroBat install on Windows, a RomM server you can sign in to, and
enough room on the drive for the games you want on it.

| What     | Version          | Notes                                                        |
| -------- | ---------------- | ------------------------------------------------------------ |
| Windows  | 10 or 11, 64-bit | RetroBat's own requirement                                   |
| RetroBat | 8.2.1 or newer   | Read from `system\version.info` in your RetroBat folder      |
| RomM     | 5.3.1 or newer   | Your own server, reachable from this PC when you want a sync |
| .NET     | Not needed       | RomMBat brings everything it runs on                         |

RomMBat checks both versions every time it starts. An older RetroBat or RomM is refused, with a
message naming the version it found and the one it needs. A newer one works, with a warning
that RomMBat has not been tested against it yet. [Compatibility](../reference/compatibility.md)
names the versions each release was tested against.

RomMBat needs no administrator rights, installs nothing into Windows, and keeps everything inside
your RetroBat folder. A RetroBat on a USB stick or an external drive keeps working when you plug
it into another PC or Windows gives it a different drive letter.

## Your RomM account

You approve RomMBat from RomM's web interface, signed in as the account whose library, saves and
playtime this device should use. [Pairing](pairing.md) lists the permissions to grant.

## The drive RetroBat is on

**Use a drive formatted exFAT or NTFS if you want disc-based games.** A FAT32 drive cannot hold
a file larger than 4 GB, and plenty of PlayStation 2, GameCube and Wii images are larger than
that. On FAT32, RomMBat leaves those games out before downloading them and tells you which ones.

Games take the space RomM reports for them, and their artwork adds to it: on a real library, a
cover, thumbnail, marquee, video and manual, which a new RetroBat fetches, came to about 5.7 MB a
game. [Disk space](../using/disk-budget.md)
explains how to set a limit.

## A controller

RomMBat is built to be used with a controller from the couch, and it reads the buttons you
already set up in EmulationStation. Set up your controller in RetroBat first, and RomMBat uses
the same layout.
