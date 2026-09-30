# Playtime

RomMBat records how long you play each game and sends it to RomM, so your playtime adds up across
every device you play on.

## How it is recorded

When you start and quit a game from EmulationStation, RomMBat notes the time on this device. It
works out which game ran from RetroBat's own record of the launch, and the session goes to RomM
with your saves, the next time EmulationStation opens or closes. Recording it never touches the
network, so it never slows a game down, and a session played offline is sent when the server is
next reachable.

RomMBat only sends a session it can tie to one game. A launch that failed, a tool opened from
EmulationStation's menu, and RomMBat itself are not games, and are left out rather than counted
against whatever you played last.

## Seeing it

RomM holds each session against the game and the device that played it. To see what it holds for
this device, run
`rommbat-agent status` in a terminal: it prints how many sessions there are, when the last one
ran, and the ten newest. `--all-sessions` lists up to 50. See [Command line](../reference/cli.md).

Playtime belongs to the account this device is paired as, so another account's sessions never
show here.

## When playtime is missing

Playtime needs RetroBat's hooks, which RomMBat sets up on its first sync. If sessions stop
arriving, `rommbat-agent hooks status` says whether they are still in place, and
`rommbat-agent hooks install` puts them back. A pairing without the `roms.user.write` permission
cannot send playtime at all; see [Pairing](../getting-started/pairing.md#which-permissions-to-grant).
