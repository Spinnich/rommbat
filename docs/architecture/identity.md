---
summary: How the device identity follows the drive rather than the host, and how the token is kept at rest.
read-when: Before touching pairing, the device identifier or token storage.
---

# Identity

Device identity follows the drive, not the host, and the backend has a trap in it.

`POST /api/devices` dedups via `get_device_by_fingerprint`, which matches on
**`mac_address` alone**, then falls back to `ip_address + platform`, then
`hostname + platform`. For a drive that moves between machines that is actively wrong: the
same install would fingerprint differently on each host, and could collide onto a
_different_ RomM client that happens to share a MAC or a DHCP lease.

The pairing path does the right thing already. `POST /api/auth/device/approve` looks the
device up with `get_device_by_client_identifier(user_id, client_device_identifier)` and
records no `ip_address`, `mac_address` or `hostname` at all.

So:

- Generate a GUID **once**, store it in the tree, send it as `client_device_identifier`
  on `POST /api/auth/device/init`.
- Let pairing own device creation. **Never call `POST /api/devices` with host fingerprint
  fields.**
- Re-pairing with the same identifier updates the existing device rather than duplicating
  it, which is what makes "move the drive to another PC" a non-event.

The GUID lives in **`emulators/rommbat/device.id`**, a plain text file, and is mirrored into
the `device` table. The file is the authority on purpose: identity has to outlive the
database, or a rebuilt store would turn into a second device in the RomM UI.

Pairing a paired install with a different origin clears `save_slot` and `save_conflict` in the
same transaction that stores the new pairing (`PairingService.CompleteAsync`), because their rom
and save ids belong to the old server and a rebuilt one restarts them. Typing the address
(`RememberServer`) changes nothing for a paired install, so a typo costs no slots, but it is
refused while the outbox holds unsent entries, which name the old server's rom ids and are never
dropped silently; `outbox drop --all-pending --apply` is the way out. A re-pair against the same
origin changes nothing. Other tables keyed on rom id are not cleared.

Sync-set definitions persist to the free-form `Device.sync_config` dict via
`PUT /api/devices/{id}`, so a reimaged or re-paired device gets its configuration back and
the config is visible from the RomM web UI.

## The token at rest

**DPAPI is unavailable.** `DataProtectionScope.CurrentUser` binds the ciphertext to one user
profile on one machine and `LocalMachine` binds it to that machine, so either makes the drive
undecryptable on the next PC. RB-391 moved a stick between two machines under two
different Windows users, which is precisely the case that has to keep working.

So the honest position is that **on a portable install the token is only as protected as the
drive**, and that is what the docs say rather than implying otherwise. The mitigations are
the ones RomM's own guidance recommends:

| Default                                                               | Optional                                              |
| --------------------------------------------------------------------- | ----------------------------------------------------- |
| Stored as written inside the tree, with a scoped and expiring token   | AES-GCM under a PBKDF2-SHA256 key from a passphrase   |
| Re-pairing is cheap, so a lost drive is revoked rather than recovered | `--protect` on `pair`; the passphrase is never stored |

The passphrase is a real trade, not a free win: a passphrase-protected install cannot flush
its outbox unattended, because nothing can decrypt the token without someone typing it. The
KDF iteration count is stored with the ciphertext so it can be raised without stranding an
existing database.
