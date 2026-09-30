# RomMBat

Sync a self-hosted [RomM](https://github.com/rommapp/romm) library with a
[RetroBat](https://github.com/RetroBat-Official/retrobat) install on Windows.

RomMBat brings the games you choose down from RomM into RetroBat, with their artwork, metadata
and BIOS, and sends your saves, save states and playtime back up. RomM stays in charge of the
library and RetroBat is where you play it, so every device you play on sees the same progress.

The name is a portmanteau of RomM and RetroBat that lands close to "wombat".

> [!WARNING]
>
> **RomMBat is pre-release.** There is no published build yet. A platform is supported only
> for the `(system, emulator, core)` rows [the platform table](https://spinnich.github.io/rommbat/platforms/)
> lists as certified.

## Features

- Pair from the couch. Approve a code or scan a QR in RomM's web interface. The only thing
  you type is the server's address, and no password is ever entered.
- Sync sets. Choose what this device holds by platform, collection, smart collection or
  saved search, capped by game count and size. The catalog is browsed page by page rather than
  mirrored, so a library of 100,000 games is no burden.
- Lands where RetroBat expects it. Games go into `roms\<system>\`, with `gamelist.xml`
  entries, artwork, video and manuals, and BIOS goes into `bios\`, matched by checksum.
- Saves, save states and playtime go back to RomM, including folder saves such as PPSSPP's,
  and a PS2 game you give its own memory card. A conflict between two devices waits for you to
  choose a side.
- A disk budget. RomMBat stays inside the space you give it, and never removes a game whose
  saves have not reached RomM.
- Works offline. Everything runs with the server out of reach, and catches up when it is
  back. Launching a game never waits on the network.
- Portable. Everything lives inside the RetroBat folder: no registry, no service, no admin
  rights and no .NET install. A drive letter change or a move to another PC is a non-event.
- Gamepad first. A full-screen interface opened from EmulationStation's menu, driven with
  the controller you already set up. A [command-line agent](https://spinnich.github.io/rommbat/reference/cli/)
  covers scripts and headless installs.

## Requirements

|          | Version                                   |
| -------- | ----------------------------------------- |
| Windows  | 10 or 11, 64-bit                          |
| RetroBat | 8.2.1 or newer                            |
| RomM     | 5.3.1 or newer, on a server you can reach |

An older RomM or RetroBat is refused at startup, and a newer one works with a warning.
[Compatibility](https://spinnich.github.io/rommbat/reference/compatibility/) names what each
release was tested against. Use an exFAT or NTFS drive for disc-based games, since FAT32 cannot
hold a file over 4 GB.

## Getting started

1. [Install](https://spinnich.github.io/rommbat/getting-started/install/): extract the zip into
   your RetroBat folder.
2. [Pair](https://spinnich.github.io/rommbat/getting-started/pairing/) it with your RomM server,
   granting the permissions that page lists.
3. [Make a sync set and sync it](https://spinnich.github.io/rommbat/getting-started/first-sync/).

## Documentation

- **[The guide](https://spinnich.github.io/rommbat/)**: installing and using RomMBat, how saves
  sync, and a page for each supported system.
- **[Developer docs](docs/design/principles.md)**: the design principles, then
  [architecture](docs/architecture/README.md), the
  [upstream reference](docs/upstream/README.md) of how RomM and RetroBat behave, and the
  [certification records](docs/platforms/README.md).
- **[Developer setup](DEVELOPER_SETUP.md)**: building, testing, and a throwaway RetroBat to test
  against.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md), and report security issues as
[SECURITY.md](SECURITY.md) describes.

> [!IMPORTANT]
>
> **RomMBat is developed primarily with Claude Code, and AI assistance must be disclosed in
> every pull request.** This norm comes from RomM and RomMBat inherits it.

## Related projects

| Project                                                   | What it is                                            |
| --------------------------------------------------------- | ----------------------------------------------------- |
| [RomM](https://github.com/rommapp/romm)                   | The self-hosted ROM manager RomMBat syncs against     |
| [RetroBat](https://github.com/RetroBat-Official/retrobat) | The Windows retro-gaming distro RomMBat installs into |

## Licence

[GPL-3.0](LICENSE), matching the RomM Playnite plugin and Argosy.

RomMBat is not affiliated with either project's maintainers. It ships no ROMs, no BIOS files and
no copyrighted content; it moves files between a server you run and a device you own.
