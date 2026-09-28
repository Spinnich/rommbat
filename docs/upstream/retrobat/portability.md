---
summary: Root discovery, absolute paths, drive-letter moves, a second host, and the FAT32 and exFAT limits.
read-when: Before persisting a path, locating the RetroBat root, or writing to a removable volume.
---

# RetroBat: portability

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-377. The version is in `system/version.info`, and there is no `build.ini`

Verified: RetroBat 8.2.0, 2026-08-09, and 8.2.1, 2026-09-28. How: read the file, and searched the whole tree for `build.ini`.
The file is one line, `8.2.1-stable-win64`, with a channel and an architecture after the version.
`ProductVersion` ignores the suffix rather than reading it as a semantic-versioning prerelease,
which would rank a stock install below its own release.

## RB-378. `retrobat.ini`, `emulationstation/` and `roms/` mark the root

Verified: RetroBat 8.2.0, 2026-08-09, and 8.2.1, 2026-09-28. How: listed the root of a stock install.
The root also holds `saves/`, `bios/`, `system/`, `emulators/` and `user/`. `RootMarkers` takes
`retrobat.ini` alone as decisive, or both folders together, since either folder alone is a common
enough name to match partway up an unrelated tree.

## RB-381. The registry records where the install was last seen, not where it is

Verified: RetroBat 8.2.0, 2026-08-09, and 8.2.1, 2026-09-28. How: read `HKCU\Software\RetroBat`.
`LatestKnownInstallPath` holds the root with a trailing backslash, per Windows user, on one
machine. After a drive-letter change it names the old letter, and a second host has no key.
`RetroBatRoot` walks up from its own folder first and reads the key last, checking it against the
markers (RB-378) before trusting it.

## RB-391. An install survives a new drive letter and a second host

Verified: RetroBat 8.2.0, 2026-08-09. How: moved an NTFS stick G: to D: on a second machine under another Windows user, then back as K:, launching a game on each.
Root discovery found `K:\RetroBat`, ES passed `K:\RetroBat\roms\...` paths, and the second machine
launched a game (exit 0) and rewrote a `gamelist.xml` on the stick. Only the hooks failed there
(RB-398). So RomMBat persists no absolute path, keys identity to the drive, and uses no DPAPI.

## RB-401. RetroBat's live config holds no absolute path

Verified: RetroBat 8.2.0, 2026-08-09, and 8.2.1, 2026-09-28. How: scanned every `.cfg`, `.ini`, `.xml`, `.menu`, `.json`, `.bat` and `.info` in the tree for a drive-rooted path.
`retrobat.ini`, `es_systems.cfg`, `es_settings.cfg`, `es_savestates.cfg`, `es_features.cfg`,
every `gamelist.xml` and every `.menu` are clean; `es_systems.cfg` writes `~\..\roms\<system>`.
The 27 hits of 5,741 files on 8.2.1 are emulator configs `emulatorlauncher` writes at launch
with the current root (`R:\RetroBat\saves\psx` in `mednafen.cfg`), developer paths in
`system/templates/`, MAME software lists and a comment example in `retrobat.ini`.

## RB-48. FAT32's 4 GB ceiling fails as "There is not enough space on the disk"

Verified: Windows 11 26200, 2026-08-09. How: wrote 4 GiB + 64 MiB in 8 MiB chunks to a FAT32 stick with 14.6 GB free.
The write that would cross 4 GiB fails whole with Win32 112 `ERROR_DISK_FULL`; a seek past it
leaves a 0-byte file. The message sends a user to free space that is not the problem.
`DriveInfo.DriveFormat` reports `FAT32` and `exFAT` reliably, so `FilesystemLimits` compares
`fs_size_bytes` before a download and never surfaces the OS message.

## RB-393. FAT32 and exFAT both store mtime to 2 s, rounded up

Verified: Windows 11 26200, 2026-08-09. How: stamped and naturally wrote files on one stick formatted FAT32, exFAT and FAT32 again, with NTFS as control.
exFAT's format allows 10 ms and Windows does not use it. Six files written across 1.7 s shared
one mtime, and a file can read up to 2 s ahead of the clock that wrote it, so mtime is only a
tiebreak and `ClockSkew.FilesystemTimestampTolerance` is 2 s. Local time round-tripped across a
DST boundary on FAT, so no hour shift is corrected for; this was measured in one timezone.

## RB-86. About 3% of a real library is over FAT32's ceiling

Verified: RomM 5.1.1-beta.1, 2026-08-10. How: read 2,000 ROMs as twenty 100-row pages spread across an 83,131-ROM library.
61 of the 2,000 (3.05%) exceed 4 GB, so the pre-flight of RB-48 fires on a real library. The
longest `fs_name` is 174 characters, inside RB-109's limit, and none carries a character Windows
refuses (RB-110).

## RB-109. A file name stops at 255 characters, and `\\?\` does not lift it

Verified: Windows 11 26200, .NET 10, 2026-08-11 and 2026-09-28. How: wrote 255- and 256-character names, plain and prefixed.
The 256-character name fails `IOException` either way: it is a per-component filesystem limit,
not `MAX_PATH`, and a 306-character total path writes with `LongPathsEnabled=1`.
`MediaNaming.Safe` cuts a longer constructed name to fit, splicing in a hash of the original so
two names that differ past the cut stay distinct.

## RB-110. A colon in a file name writes an alternate data stream, silently

Verified: Windows 11 26200, .NET 10, 2026-08-11 and 2026-09-28. How: wrote a name holding each reserved character, a trailing dot or space, and `CON`.
`<`, `>`, `"`, `|`, `?` and `*` fail `IOException`, and `/` and `\` fail
`DirectoryNotFoundException`. `probe:y.png` succeeds, the folder lists `probe`, and the named file
is not there. A trailing dot or space is stripped, and `CON.png`, `PRN.png` and `COM1.png` write.
`MediaNaming.Safe` turns all nine characters into `_` and trims a trailing dot or space.
