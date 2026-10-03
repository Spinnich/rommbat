# Install

RomMBat comes as one zip, `rommbat-<version>-win-x64.zip`, which you extract into your RetroBat folder.
There is no installer to run and nothing to install into Windows.

## Put the files in place

1. Download the newest `rommbat-<version>-win-x64.zip` from the
   [Releases page](https://github.com/Spinnich/rommbat/releases).
2. Quit EmulationStation if it is running.
3. Extract the zip into your RetroBat folder, the one that holds `retrobat.ini` and the `roms`
   folder. **Extract it there, not into a new folder beside it.** The zip already contains the
   `emulators\rommbat` path, so the files land in `emulators\rommbat\` inside RetroBat.

When you are done, `emulators\rommbat\` holds seven files, among them `RomMBat.exe`, the app
you use with a controller, and `rommbat-agent.exe`, which does the same work from a terminal.

## Open RomMBat the first time

RomMBat adds itself to EmulationStation's menu during its first sync, so the first time you
open it from the folder:

1. Open `emulators\rommbat\RomMBat.exe`.
2. [Pair it with your RomM server](pairing.md).
3. [Make a sync set and sync it](first-sync.md).

That first sync also adds RomMBat to RetroBat's own entry in EmulationStation's system list,
beside RetroBat's other tools, and sets up the hooks that send your saves and playtime back.
From then on you open RomMBat from EmulationStation, with the controller.

## Update

Quit EmulationStation and RomMBat, then extract the new zip over the old one, the same way.
Your pairing, sync sets, and record of what is on this device are kept: they live beside the
program in `emulators\rommbat\`, and the zip does not contain them.

## Remove RomMBat

Removing RomMBat is done from a terminal, with `rommbat-agent uninstall` (see
[Command line](../reference/cli.md)). Quit EmulationStation first.

1. Run `emulators\rommbat\rommbat-agent.exe uninstall` to see what it would take out. It writes
   nothing.
2. Run it again with `--apply`. It removes RomMBat from EmulationStation's menu, removes the
   hooks, and puts back every memory card setting RomMBat changed. Add `--content` to also
   remove the games, artwork and game list entries RomMBat downloaded, and `--bios` to remove
   the firmware it fetched.
3. Delete the `emulators\rommbat` folder.
4. In RomM, remove this device from your device list if you no longer want it there.

It refuses while any save, play session or other change is still waiting to reach RomM,
because deleting `emulators\rommbat` would lose it. Connect to your server and let it send
first. A record the server has refused outright is not waiting, so it does not stop the
removal; the preview names it, and it exists only on this device until you delete the folder.
Your saves are never removed, and neither is any game you put in RetroBat yourself.
