---
summary: The certification record for `nes`: conflict resolution, driven both ways.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: Conflict resolution, driven both ways

Driven on the two `libretro` rows, which are the only ones whose battery saves sync at all. Both
branches of `saves resolve` were exercised, plus the negotiate-driven download.

**Both sides of every conflict here were synthesized, and that bounds the claim.** The server side
was uploaded by hand and the local side was byte-edited, so what this proves is RomMBat's handling
of a divergence, not that a real two-device race produces one. No second device exists on this
install.

## Staging one is harder than it looks, and the reason is a finding

**A save uploaded through RomM's web UI cannot conflict with anything.** `slot` is an optional
query parameter on `POST /api/saves` and the web UI does not set it, so the upload lands with
`slot: null`, negotiate keys on the slot, and this device's record for `libretro:battery` never
goes stale. Measured: a web upload at 12:39 left the next flush uploading cleanly with no 409.

It is visible from the server's side. Kirby's Adventure (USA) (Rev 1), rom 158593, held four saves
for the one `.srm`, and the web upload is the one with no slot:

| Uploaded         | Slot               |
| ---------------- | ------------------ |
| 2026-09-13 11:32 | `libretro:battery` |
| 2026-09-13 11:38 | `libretro:battery` |
| 2026-09-13 12:39 | none               |
| 2026-09-13 12:46 | `libretro:battery` |

That is #138 from a second direction: a null-slot save is not only never fetched by negotiate, it
also **cannot conflict**, so a client that does not speak RomMBat's slot convention can never
collide with one. All four resolve to one destination, so `saves restore` offers one row for
them, the newest, and names the three it folded (#156).

Staging one needs `POST /api/saves?slot=libretro:battery`. Note `device_id` is validated: an
invented one is refused with `404 Device with ID ... not found`, so the upload was made without it.

## What the machinery does

|                          | `--keep-local`, rom 158593            | `--keep-server`, rom 159313         |
| ------------------------ | ------------------------------------- | ----------------------------------- |
| Detection                | `1 conflicted`, nothing overwritten   | `1 conflicted`, nothing overwritten |
| Copy aside before acting | yes                                   | yes                                 |
| Outcome on disk          | local bytes kept                      | the server's `f78ab191` written     |
| On the server            | sent as save 212, **210 still stood** | 213 untouched                       |
| Copy aside afterwards    | pruned                                | kept (#326, driven on rom 158633)   |

The conflict report names both hashes, the time it was first seen and the copy-aside path before
asking for a decision, and neither branch is a default:

```text
  rom 158593, slot libretro:battery, since 2026-09-13 12:57:52Z
    here    saves/nes/Kirby's Adventure (USA) (Rev 1).srm  7fda7607
    server  6799e326  2026-09-13 12:56:42Z
    a copy of the local file is at emulators/rommbat/replaced/20260913T125752-Kirby's ... .srm
```

**So `overwrite` means supersede, and the resolver's remark is confirmed on a second shape.** It
was measured on a `psp` class C unit during 7b-3; this is class A on `nes` and behaves the same.

## The download path works

Leaving one local file untouched while the server moved produced `1 down (8 KB)`: a save from
elsewhere came down through negotiate rather than through `saves restore`. **It also copied the
local file aside before overwriting**, so the copy-aside rule holds on the download path and not
only on conflicts.

That is the mechanism #155 is about, and it works when it is not racing a launch.

## What a download records

**Both class A writers record the save they took in `save_slot`**, the plain download and
keep-server alike, which is how a superseded row returning to the head of a slot is recognised
(#157). Driven on this row with `Destiny of an Emperor (USA)`: keep-server left `save_slot` naming
the save it took, and a plain download moved it to the newer save. Recorded in RM-4; it is not a
re-run of any certification step.

**A download's copy aside is never pruned**, and neither is a keep-server's, which holds the side
the user did not keep. Only keep-local removes its copy. The plain download's copy has no
decision to attach to it and no mechanism that will remove it. A download that would replace a
save this device never sent is a conflict instead (#211), so such a copy always holds bytes the
server already has.

## A save this device never sent, driven (#211)

On `libretro`/`nestopia` with StarTropics (USA), rom 159082, on `R:\RetroBat` (RetroBat 8.2.1,
RomM `5.3.0-beta.1`, 2026-09-21), running PR #214's build with the maintainer at the controller. It is
the hands-on pass a save-logic change owes and re-runs no certification step. **The local side is
real and the server side is staged**: the maintainer's two sessions wrote both saves through
EmulationStation, and the "other device" is a slotted upload with no `device_id`, which is the only
way to put a row in a slot this device has never synced (see "Staging one is harder than it looks"
above).

The hooks were off for the two sessions, because the quit hook's flush would otherwise have sent
the local save first, and case M3 needs a device with no sync record for the slot. The
game's BizHawk override was removed from `gamelist.xml` so it ran on `nes`' default core.

| Step                                                 | Result                                                                              |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------- |
| New game `PEER`, quit                                | `.srm` md5 `514d2818`, copied aside as the other device's save                      |
| Second file `LOCAL`, quit                            | md5 `33dcb0d4`, 10:56:26 local; `saves`: attributed, not sent                       |
| The PEER-only save uploaded as the other device      | save 355, 14:58:02Z, origin null                                                    |
| `flush`                                              | **`1 conflicted`**. The file unchanged in bytes and mtime; a copy under `replaced/` |
| `flush` again                                        | The same one conflict, no rewrite, still one copy                                   |
| `saves resolve 159082 libretro:battery --keep-local` | Sent, copy pruned, file unchanged                                                   |
| `hooks install`, `flush`                             | Nothing up or down; `in step`                                                       |
| Launched through ES                                  | Both `PEER` and `LOCAL` on the file select                                          |

**A download over a save that was sent still works.** With the slot in step, BizHawk's real
StarTropics save bytes (md5 `970db3b8`) were uploaded as the other device, save 358. The flush
answered `1 down` with no conflict, the file became those bytes, the next flush was a no-op, and
the game on `nestopia` showed BizHawk's files and neither `PEER` nor `LOCAL`. Each of nestopia's
rewrites on exit went up as `1 up` with no conflict, once through the quit hook's flush. Nothing
else on the install conflicted in any of the passes.

## A recorded mednafen path a plain save now shadows, driven (#215)

On Final Fantasy (USA), rom 158331, on `R:\RetroBat` (RetroBat 8.2.1, RomM `5.3.0-beta.1`,
2026-09-21), running PR #215's build after its review round. It re-runs no certification step.
The `mednafen:battery` slot was in step at `Final Fantasy (USA).24ae5edf....sav` (md5 `597b2790`,
save 369). The other device was a slotted upload with no `device_id`, and the plain file was a
byte copy of the hashed one, standing in for what mesen standalone writes (RB-273).

| Step                                                  | Result                                                                                                      |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `flush`                                               | Nothing up or down                                                                                          |
| The same save, last byte flipped, as the other device | save 378, origin null                                                                                       |
| `Final Fantasy (USA).sav` added beside it, `flush`    | **`1 up, 1 failed`**: the plain file went up as `mesen:battery`, save 379; the download refused as shadowed |
| After                                                 | The hashed file unchanged in bytes and mtime; `save_slot` still save 369, md5 `597b2790`                    |
| Plain file removed, 378 and 379 deleted, `flush`      | Nothing up or down; both files `in step`                                                                    |
