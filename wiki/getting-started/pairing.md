# Pairing

Pairing connects this RetroBat to your RomM server. You never type a password: RomMBat shows a
code, you approve it in RomM's web interface, and the only thing you type with the controller
is your server's address.

## Pair from the couch

1. In RomMBat, choose Pair with RomM.
2. Type your server's address on the on-screen keyboard, for example
   `https://romm.example.com`, then press Start. The address is remembered for next time.
3. RomMBat shows a QR code and an 8-character code. Scan the QR code with your phone, or open
   the address shown on any device and type the code.
4. RomM asks which permissions to give this device. Grant the ones in
   [the table below](#which-permissions-to-grant), then approve.

RomMBat notices the approval on its own and says it is paired. A code lasts 10 minutes; if it
runs out, choose New code on the footer. If the server cannot be reached, RomMBat says so
and offers Try again. Everything else in RomMBat works without the server, so pairing can
wait until it is back.

The footer shows each button by its position on the controller rather than by a letter,
because the bottom button is A on an Xbox pad, Cross on a PlayStation pad and B on a Switch
Pro. It uses the layout you set up in EmulationStation.

## Which permissions to grant

Grant these, and no others:

| Permission                           | What it is for                              | Without it                         |
| ------------------------------------ | ------------------------------------------- | ---------------------------------- |
| `roms.read`                          | Browsing and downloading games              | Nothing works                      |
| `platforms.read`                     | The platform list, and where each one lands | Nothing works                      |
| `collections.read`                   | Sync sets made from a collection            | Only platform and search sync sets |
| `firmware.read`                      | Fetching BIOS files                         | You copy BIOS files in yourself    |
| `assets.read`                        | Bringing saves and save states down         | Saves only go up                   |
| `assets.write`                       | Sending saves and save states up            | Saves only come down               |
| `devices.read` / `devices.write`     | This device's identity and its save sync    | No save sync at all                |
| `roms.user.read` / `roms.user.write` | Playtime and last played                    | No playtime                        |
| `me.read`                            | Reading your own account while pairing      | Pairing fails                      |

RomMBat never needs `users.read`, `users.write`, `roms.write`, `platforms.write`, `tasks.run` or
`logs.read`. A device can never have more permission than the account that approved it, so the
simplest way to keep it narrow is to approve it from the account you play on rather than an
administrator.

If you grant less, RomMBat still works and turns off what it cannot do. This device, on the
main menu, lists each feature that is off and the permission it needs.

## When you need to pair again

A pairing does not last forever. When it runs out, the main menu's pairing row reads
token expired, and choosing Pair again signs this device back in. You also pair again to
move this device to a different RomM server. Your games, saves, save states and settings stay
where they are, but RomMBat forgets what it knew of the old server's games, saves and any save
conflicts still open, and asks again on the next sync. Your sync set choices are kept. It will not switch servers while saves,
states or play sessions are still waiting to be sent to the old one: let them send, or discard
them with `rommbat-agent outbox drop --all-pending --apply` when the old server is gone.

## The pairing on a portable drive

RomMBat stores its sign-in inside the RetroBat folder, so it travels with the drive. Anyone who
has the drive can use that sign-in until it runs out, with the permissions you granted. That
is why a pairing expires and why pairing again is quick.

If you would rather protect it, pair from a terminal with `rommbat-agent pair --protect` (see
[Command line](../reference/cli.md)). RomMBat then encrypts the sign-in with a passphrase you
choose. The cost is that only a terminal command you type the passphrase into can reach RomM.
Saves and playtime no longer go up on their own, and nothing in the controller app that needs
the server works: it has nowhere to type the passphrase, so it reports this device as not
paired for those actions.
