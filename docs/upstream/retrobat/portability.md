---
summary: Root discovery, absolute paths, drive-letter moves, a second host, and the FAT32 and exFAT limits.
read-when: Before persisting a path, locating the RetroBat root, or writing to a removable volume.
---

# RetroBat: portability

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-1. No `build.ini` exists

Plan says: Read the RetroBat version from `build.ini` (L331)

Measurement says: No `build.ini` exists. It is `system/version.info`, value `8.2.0-stable-win64`

## RB-24. Sound, but untested: the stick under test is NTFS, so neither constraint was exercised

Plan says: Portable installs may be FAT32 or exFAT, with a 4 GB ceiling and coarse mtimes (L170-180)

Measurement says: Sound, but **untested**: the stick under test is NTFS, so neither constraint was exercised. Needs a differently formatted volume

## RB-46. exFAT is identical: 2 s

Plan says: exFAT is listed with FAT32 but FAT32's 2 s granularity is the one quoted (L188-190)

Measurement says: **exFAT is identical: 2 s.** Its format allows 10 ms, Windows does not use it. Treat the two as the same for timestamps

## RB-47. Coarse and rounded up, so a file's mtime lands up to 2 s in the future

Plan says: mtime is coarse, so treat it as an ordering tiebreak (L188-190)

Measurement says: Coarse **and rounded up**, so a file's mtime lands up to 2 s in the **future**. A "timestamp ahead of the clock" skew check needs a 2 s tolerance or every FAT install trips it

## RB-48. Confirmed, and the failure is `ERROR_DISK_FULL`, "There is not enough space on the disk", on a volume with

Plan says: FAT32 cannot hold a file larger than 4 GB, so skip or refuse (L184-186)

Measurement says: Confirmed, and the failure is **`ERROR_DISK_FULL`, "There is not enough space on the disk"**, on a volume with 14.6 GB free. Never surface that message; pre-flight against `fs_size_bytes`

## RB-86. How much of a real library FAT32 cannot hold

Previously: (not addressed) how much of a real library FAT32 cannot hold

Measurement says: **3.05%**, 61 of 2,000 sampled ROMs over the 4 GB ceiling. The longest `fs_name` was **174 characters**, which with a deep portable root and an `images/` sibling is inside `MAX_PATH` reach. **No `fs_name` in 2,000 carried a character Windows refuses**, so the Linux-to-Windows name hazard is real in principle and unobserved here

## RB-109. The 255-character file name is the ceiling, and `\\?\` does not lift it

Previously: Long paths are the hazard for constructed media names (plan, principle 4)

Measurement says: **The 255-character file name is the ceiling, and `\\?\` does not lift it**: 255 wrote, 256 failed `IOException` both plain and prefixed, because it is a filesystem component limit rather than `MAX_PATH`. Total path reached 306 characters fine on this machine (`LongPathsEnabled=1`). The longest `fs_name` in the sample is 156 characters, so a suffix plus a folder is well inside it

## RB-110. Which characters a constructed name must lose

Previously: (not addressed) which characters a constructed name must lose

Measurement says: `<`, `>`, `"`, `|`, `?`, `*` raise `IOException` and `/`, `\` raise `DirectoryNotFoundException`, all loud. **`:` does not**: it writes an **NTFS alternate data stream**, so the call succeeds, the directory lists a file called `probe`, and the file the gamelist names is not there. A trailing dot or space is silently stripped. `CON.png`, `PRN.png`, `COM1.png` all wrote on Windows 11 26200

## RB-377. There is no `build.ini`

**RetroBat 8.2.0 has no `build.ini` anywhere in the tree.** The version lives in
`system/version.info`, as a single line:

```text
8.2.0-stable-win64
```

This contradicts [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/4fa916583/docs/PLAN.md) line 331, `DEVELOPER_SETUP.md` section 4, and the
compatibility rule in `CLAUDE.md`, all of which name `build.ini`. The string also carries a
channel and an architecture suffix, so it is not directly parseable as a semantic version;
the comparison logic has to split on `-` first.

## RB-378. Root markers

Present at the root of a stock install: `retrobat.ini`, `emulationstation/`, `roms/`,
`saves/`, `bios/`, `system/`, `emulators/`, `user/`. The plan's proposed marker set
(`retrobat.ini`, `emulationstation/`, `roms/`) is sound; `build.ini` must be dropped from any
marker list it appears in.

## RB-381. The registry fallback exists, and is exactly as stale as the plan assumes

Checked while building M1, not during M0, and recorded here because the code cites it.
RetroBat does write a registry key:

```text
HKCU\Software\RetroBat
    LatestKnownInstallPath    REG_SZ    K:\RetroBat\
    InstallRootUrl            REG_SZ    http://www.retrobat.ovh/repo/win64
    InstallRootUrlNew         REG_SZ    http://www.retrobat.org/repo/win64
```

`K:` is the letter the probe 7 stick ended on, which is the whole point: the value records
where an install was **last seen**, per Windows user, on one machine. On a portable drive it
is stale the moment the letter changes, and on the second host of probe 7 it would not have
existed at all. So it is usable only as the last-resort fallback the plan already calls for,
after walking up from `AppContext.BaseDirectory`, and the value has to be re-checked against
the root markers before it is trusted.

## RB-391. What survived

| Criterion                                       | Result                                                                                                                                                      |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Root discovery                                  | **pass**. Hooks resolved `%~dp0..\..\..\..` to `K:\RetroBat` after the letter change, and ES passed `K:\RetroBat\roms\...` paths                            |
| RetroBat launching a game on the second machine | **pass**. `emulatorLauncher.log` on the stick records `"D:\RetroBat\...\emulatorLauncher.exe" ... -rom "D:\RetroBat\roms\ports\gong.libretro"`, exit code 0 |
| Writes to the stick from the second machine     | **pass**. ES rewrote `roms/ports/gamelist.xml` at 23:18:38 on exit there                                                                                    |
| Drive-letter change on the same machine         | **pass**. Everything worked on K: exactly as on G:                                                                                                          |
| RetroBat storing absolute paths                 | **pass**. See the pre-move audit below                                                                                                                      |

The second machine ran under a different Windows user profile, confirming the install is not
bound to a Windows account.

## RB-393. The FAT32 and exFAT constraints, measured on a second stick

The RetroBat stick is NTFS, so both filesystem constraints were measured separately on a
14.6 GB USB stick formatted first FAT32, then exFAT, then FAT32 again, with the NVMe system
drive as an NTFS control (`tools/m0-probes/probe7-filesystem.ps1`).

### The 4 GB ceiling fails as "not enough space on the disk"

Writing 4 GB + 64 MB to FAT32 in 8 MB chunks:

| Result           | Value                                                                  |
| ---------------- | ---------------------------------------------------------------------- |
| Stopped at       | **4,286,578,688 bytes** = 4 GiB - 8 MiB, the last chunk that fit whole |
| Exception        | `System.IO.IOException`                                                |
| HRESULT          | `0x80070070`, Win32 **112 `ERROR_DISK_FULL`**                          |
| Message          | **"There is not enough space on the disk."**                           |
| Free at the time | **14.63 GB**                                                           |
| Throughput       | 6.5 MB/s, so the 4 GB write itself took 11 minutes                     |

**The error message is actively misleading and RomMBat must not surface it.** The volume had
14.6 GB free; the file simply could not exceed 4 GiB. A client that reports the OS message
tells the user to free up space, which will not help, and a client that retries after
clearing room retries forever. A seek to `4GiB - 4` followed by an 8-byte write fails the
same way and leaves a **0-byte** file, so the failure is a hard boundary rather than a
partial write.

The 4 GiB - 8 MiB stopping point is an artifact of the chunk size, not a filesystem
boundary: the write that would have crossed 4 GiB failed whole. The real limit is 4 GiB - 1.

**So the pre-flight check is mandatory, exactly as core principle 4 says.** `fs_size_bytes`
from the rom record, compared against a FAT32 target before the download starts, is the only
place this can be caught cheaply. Detection is easy and reliable:
`DriveInfo.DriveFormat` returned `FAT32` and `exFAT` correctly on the removable volume, and
`Get-Volume` agreed.

### mtime: exFAT is no better than FAT32, and both round up

This is the result that changes the design, because the plan assumes exFAT is the finer of
the two. It is not, at least not through Windows' driver:

| Filesystem | Requested +1 ms     | Requested +1999 ms | Granularity | Direction |
| ---------- | ------------------- | ------------------ | ----------- | --------- |
| FAT32      | stored **+2000 ms** | stored +2000 ms    | **2 s**     | **up**    |
| **exFAT**  | stored **+2000 ms** | stored +2000 ms    | **2 s**     | **up**    |
| NTFS       | stored +0 ms        | stored +0 ms       | exact       | -         |

exFAT's on-disk format has a 10 ms increment field, so a finer value is representable; it is
simply not what this Windows build stores. Treat **exFAT and FAT32 as identical** for
timestamp purposes.

**And the rounding is up, not to nearest, which puts timestamps in the future.** Natural
writes, timed against the wall clock rather than stamped explicitly:

```text
exFAT   wrote 08:03:16.097 -> stored 08:03:18.000   skew +1903 ms
        wrote 08:03:16.448 -> stored 08:03:18.000   skew +1552 ms
        wrote 08:03:16.804 -> stored 08:03:18.000   skew +1196 ms
        wrote 08:03:17.144 -> stored 08:03:18.000   skew  +855 ms
        wrote 08:03:17.485 -> stored 08:03:18.000   skew  +514 ms
        wrote 08:03:17.840 -> stored 08:03:18.000   skew  +159 ms
NTFS    wrote 08:03:19.418 -> stored 08:03:19.418   skew    +0 ms
```

FAT32 behaved identically after the volume was formatted back. Two consequences, and the
second one is not in the plan:

1. **Six files written across 1.7 seconds carry one identical mtime.** Ordering within a
   2-second window is not recoverable, so mtime cannot break ties between saves written in
   the same moment, which is exactly what a multi-file (class B) save looks like.
2. **A file's recorded mtime can be up to 2 seconds in the future.** Core principle 1's
   clock-skew handling compares local timestamps against the server's `Date` header; on a
   FAT volume a freshly written save legitimately reads as newer than the clock that wrote
   it. **Any "this timestamp is in the future, suspect a bad RTC" check needs a tolerance of
   at least 2 seconds**, or every FAT install trips it.

### The FAT local-time trap did not appear

FAT stores local wall-clock time rather than UTC, which historically shifts timestamps by an
hour across a DST boundary. Stamping a winter date and a summer date and reading both back:

| Stamped             | Stored local        | Stored UTC          | Offset |
| ------------------- | ------------------- | ------------------- | ------ |
| 2026-01-15 12:00:00 | 2026-01-15 12:00:00 | 2026-01-15 17:00:00 | -5 h   |
| 2026-07-15 12:00:00 | 2026-07-15 12:00:00 | 2026-07-15 16:00:00 | -4 h   |

Local time round-tripped exactly in both, and the UTC conversion used the offset in force on
**that** date rather than today's. NTFS produced identical values. So this Windows build
applies per-timestamp DST rules to FAT, and the hour-shift hazard is not live here. Worth
re-checking on a machine in a different timezone before it is called settled.

## RB-401. Probe 7: absolute-path audit, taken before the move

Taken before the move, answering the plan's "note whether RetroBat itself stores any
absolute paths that would constrain us" (`tools/m0-probes/probe7-portable.py`).

**RetroBat's live configuration is genuinely portable.** Of **5,636** config files scanned
across the tree (`.cfg`, `.ini`, `.xml`, `.menu`, `.json`, `.bat`, `.info`), only **9**
contain an absolute path, and **not one of them is a file RetroBat reads as live config**:

| File                                                            | What the absolute path is                                                                                                        |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `bios/mame/hash/ibm5170.xml`, `ibm5170_hdd.xml`, `pico.xml`     | MAME software-list metadata _describing_ historical DOS media (`C:\Windows\system32\diskcopy.dll`), not a path anything resolves |
| `system/templates/gemrb/GemRB.cfg`                              | `H:\GemRB\plugins`, `C:/gemrb-win32-ef32a9a`                                                                                     |
| `system/templates/project64/Config/Project64.cfg`               | `F:\RetroBat-Wip\roms\n64\Mario`                                                                                                 |
| `system/templates/simple64/mupen64plus.cfg`, `simple64-gui.ini` | `C:/retrobat/saves/n64/...`, `C:/retrobat/roms/n64`                                                                              |
| `system/templates/oricutron/oricutron.cfg`                      | `d:/osdk/my`                                                                                                                     |
| `system/templates/pcsx2-16/inis/GSdx.ini`                       | `C:\Windows\Fonts\tahoma.ttf`                                                                                                    |

Every one of the `system/templates/` hits is a **stale developer path baked into a shipped
template**, `F:\RetroBat-Wip\` being the clearest tell. These are the files
`emulatorlauncher` copies and rewrites per launch, which is exactly why rule 2 says never to
edit a generated emulator config: they are treated as disposable, and RetroBat's own authors
evidently treat them that way too.

Confirmed clean, with **zero** absolute paths: `retrobat.ini`, `es_systems.cfg`,
`es_settings.cfg`, `es_savestates.cfg`, `es_features.cfg`, every `gamelist.xml`, and every
`.menu`. `es_systems.cfg` uses `~\..\roms\<system>` and `%HOME%\emulatorLauncher.exe`.
