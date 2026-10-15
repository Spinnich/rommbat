---
summary: Every firmware slot each RetroBat system's emulators read, the files accepted there by md5, and the source for each, generated from the BIOS manifest.
read-when: Asking which firmware a system or row reads, what to upload to RomM for it, or why a file is reported rather than fetched.
---

# Firmware matrix

<!-- Generated from data/retrobat/bios.json and reference/retrobios/ by tools/build-firmware-docs.py.
     Edit the sources and regenerate; tools/build-firmware-docs.py --check fails on an edit here. -->

Every firmware file a RetroBat system's emulators read, from the bundled manifest. How the
manifest is built, and why, is [firmware-from-emulator-evidence](../design/decisions/firmware-from-emulator-evidence.md).
`python3 tools/firmware.py` answers the same questions per row, and compares a system against a RomM server.

- **Slot** is where the file lands, relative to the RetroBat root. Several folders naming one
  path share one file, so they accept the same files.
- **Required** is yes where RetroBat's list names the slot or a reader's source marks it
  required. A missing file is reported and never blocks a sync: the emulator decides what it reads.
- **Readers** are the `emulator/core` rows that read the slot. RetroBat's list stands for the
  system's default emulator.
- **Accepted** is each file RomMBat will fetch for the slot, best ranked first: md5, region,
  and the sources that identify it. A zip is accepted by the crc32 of its members.
- **Evidence** is the emulator source a retrobios profile cites, as `file:line`, and any
  boot measured on our tree.

149 systems, 943 slots, 352 distinct md5s, 71 slots with no identity. Sources: RetroBat-Official/emulatorlauncher batocera-systems/Resources/batocera-systems.json; Abdess/retrobios 527526a8e2be1b251e18c2ee9f3e2dcbb5adac06.

Systems: [3do](#3do), [3ds](#3ds), [actionmax](#actionmax), [adam](#adam), [advision](#advision), [amiga1200](#amiga1200), [amiga4000](#amiga4000), [amiga500](#amiga500), [amigacd32](#amigacd32), [amigacdtv](#amigacdtv), [apfm1000](#apfm1000), [apple2](#apple2), [apple2gs](#apple2gs), [aquarius](#aquarius), [arcadia](#arcadia), [archimedes](#archimedes), [astrocade](#astrocade), [atari5200](#atari5200), [atari7800](#atari7800), [atari800](#atari800), [atarist](#atarist), [atom](#atom), [atomiswave](#atomiswave), [bbcmicro](#bbcmicro), [bk](#bk), [c128](#c128), [c20](#c20), [c64](#c64), [camplynx](#camplynx), [cave](#cave), [cdi](#cdi), [channelf](#channelf), [chihiro](#chihiro), [coco](#coco), [colecovision](#colecovision), [cps1](#cps1), [cps2](#cps2), [cps3](#cps3), [crvision](#crvision), [daphne](#daphne), [dos](#dos), [dragon32](#dragon32), [dreamcast](#dreamcast), [ecwolf](#ecwolf), [electron](#electron), [enterprise](#enterprise), [fbneo](#fbneo), [fds](#fds), [flash](#flash), [fm7](#fm7), [fmtowns](#fmtowns), [fpinball](#fpinball), [gamate](#gamate), [gamecom](#gamecom), [gamecube](#gamecube), [gamegear](#gamegear), [gamepock](#gamepock), [gb](#gb), [gb2players](#gb2players), [gba](#gba), [gbc](#gbc), [gbc2players](#gbc2players), [gmaster](#gmaster), [gp32](#gp32), [hbmame](#hbmame), [hikaru](#hikaru), [intellivision](#intellivision), [j2me](#j2me), [jaguar](#jaguar), [jaguarcd](#jaguarcd), [laseractive](#laseractive), [lindbergh](#lindbergh), [loopy](#loopy), [lynx](#lynx), [mame](#mame), [mastersystem](#mastersystem), [megacd](#megacd), [megadrive](#megadrive), [megadrive-msu](#megadrive-msu), [msx1](#msx1), [msx2](#msx2), [msx2+](#msx2), [msxturbor](#msxturbor), [n64](#n64), [n64dd](#n64dd), [namco2x6](#namco2x6), [naomi](#naomi), [naomi2](#naomi2), [nds](#nds), [neogeo](#neogeo), [neogeo64](#neogeo64), [neogeocd](#neogeocd), [nes](#nes), [ngage](#ngage), [ngp](#ngp), [ngpc](#ngpc), [odyssey2](#odyssey2), [oricatmos](#oricatmos), [pc88](#pc88), [pc98](#pc98), [pcengine](#pcengine), [pcenginecd](#pcenginecd), [pcfx](#pcfx), [pegasus](#pegasus), [pokemini](#pokemini), [prboom](#prboom), [ps2](#ps2), [ps3](#ps3), [ps4](#ps4), [psp](#psp), [psvita](#psvita), [psx](#psx), [pv1000](#pv1000), [samcoupe](#samcoupe), [satellaview](#satellaview), [saturn](#saturn), [scummvm](#scummvm), [scv](#scv), [sega32x](#sega32x), [segastv](#segastv), [sgb](#sgb), [sgb-msu1](#sgb-msu1), [singe](#singe), [snes](#snes), [snes-msu1](#snes-msu1), [spectravideo](#spectravideo), [sufami](#sufami), [supergrafx](#supergrafx), [supracan](#supracan), [switch](#switch), [ti99](#ti99), [triforce](#triforce), [tutor](#tutor), [vc4000](#vc4000), [vectrex](#vectrex), [vg5k](#vg5k), [vircon32](#vircon32), [vpinball](#vpinball), [vsmile](#vsmile), [wii](#wii), [wiiu](#wiiu), [x1](#x1), [x68000](#x68000), [xbox](#xbox), [xbox360](#xbox360), [xegs](#xegs), [zinc](#zinc), [zx81](#zx81), [zxspectrum](#zxspectrum).

## 3do

3DO: 13 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/3do_arcade_saot.bin` | no | `libretro/opera` | `8970fc987ab89a7f64da9f8a8c4333ff` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:123-134` |
| `bios/goldstar.bin` | yes | `libretro/opera`, RetroBat's list | `8639fd5e549bd6238cfee79e3e749114` no region; retrobios `opera`, RetroBat's list | `opera`: `libopera/opera_bios.c:97-108` |
| `bios/panafz1-kanji.bin` | no | `libretro/opera` | `b8dc97f778a6245c58e064b0312e8281` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:141-152` |
| `bios/panafz1.bin` | yes | `libretro/opera`, RetroBat's list | `f47264dd47fe30f73ab3c010015c155b` no region; retrobios `opera`, RetroBat's list | `opera`: `libopera/opera_bios.c:6-17` |
| `bios/panafz10-norsa.bin` | no | `libretro/opera` | `1477bda80dc33731a65468c1f5bcbee9` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:58-69` |
| `bios/panafz10.bin` | yes | `libretro/opera`, RetroBat's list | `51f2f43ae2f3508a14d9f56597e2d3ce` no region; retrobios `opera`, RetroBat's list | `opera`: `libopera/opera_bios.c:45-56` |
| `bios/panafz10e-anvil-norsa.bin` | no | `libretro/opera` | `cf11bbb5a16d7af9875cca9de9a15e09` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:84-95` |
| `bios/panafz10e-anvil.bin` | yes | `libretro/opera` | `a48e6746bd7edec0f40cff078f0bb19f` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:71-82` |
| `bios/panafz10ja-anvil-kanji.bin` | no | `libretro/opera` | `428577250f43edc902ea239c50d2240d` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:167-178` |
| `bios/panafz1j-kanji.bin` | no | `libretro/opera` | `c23fb5d5e6bb1c240d02cf968972be37` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:154-165` |
| `bios/panafz1j-norsa.bin` | no | `libretro/opera` | `f6c71de7470d16abe4f71b1444883dc8` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:32-43` |
| `bios/panafz1j.bin` | yes | `libretro/opera` | `a496cfdded3da562759be3561317b605` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:19-30` |
| `bios/sanyotry.bin` | yes | `libretro/opera` | `35fa1a1ebaaeea286dc5cd15487c13ea` no region; retrobios `opera` | `opera`: `libopera/opera_bios.c:110-121` |

## 3ds

3ds: 1 slots, 26 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/sysdata/keys.txt` | no | `libretro/azahar` | `cb52d323b13129a2f0bc3e3e911e567f` no region; retrobios `azahar` | `azahar`: `src/core/hw/aes/key.cpp:283-300, src/common/common_paths.h:83, src/core/hw/default_keys.h:4-5` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/aes_keys.txt` | `bizhawk/Encore` | no identity: no source states a hash for it |
| `bios/nand/private/movable.sed` | `libretro/azahar` | no identity: no source states a hash for it |
| `bios/nand/rw/sys/LocalFriendCodeSeed_B` | `libretro/azahar` | no identity: no source states a hash for it |
| `bios/nand/rw/sys/SecureInfo_A` | `libretro/azahar` | no identity: no source states a hash for it |
| `bios/seeddb.bin` | `bizhawk/Encore` | no identity: no source states a hash for it |
| `bios/sysdata/otp.bin` | `libretro/azahar` | no identity: no source states a hash for it |
| `bios/sysdata/seeddb.bin` | `libretro/azahar` | no identity: no source states a hash for it |
| `bios/sysdata/shared_font.bin` | `libretro/azahar` | no identity: no source states a hash for it |
| `Citra/sysdata/aes_keys.txt` | `citra/citra-qt` | citra has no cited firmware directory |
| `Citra/sysdata/boot9.bin` | `citra/citra-qt` | citra has no cited firmware directory |
| `Citra/sysdata/seeddb.bin` | `citra/citra-qt` | citra has no cited firmware directory |
| `Citra/sysdata/shared_font.bin` | `citra/citra-qt` | citra has no cited firmware directory |
| `nand/private/movable.sed` | `azahar` | azahar has no cited firmware directory |
| `nand/rw/sys/LocalFriendCodeSeed_B` | `azahar`, `mandarine` | azahar has no cited firmware directory; mandarine has no cited firmware directory |
| `nand/rw/sys/SecureInfo_A` | `azahar`, `mandarine` | azahar has no cited firmware directory; mandarine has no cited firmware directory |
| `sysdata/aes_keys.txt` | `mandarine` | mandarine has no cited firmware directory |
| `sysdata/boot9.bin` | `mandarine` | mandarine has no cited firmware directory |
| `sysdata/CTCert.bin` | `mandarine` | mandarine has no cited firmware directory |
| `sysdata/keys.txt` | `azahar` | azahar has no cited firmware directory |
| `sysdata/otp.bin` | `azahar` | azahar has no cited firmware directory |
| `sysdata/seeddb.bin` | `azahar`, `mandarine` | azahar has no cited firmware directory; mandarine has no cited firmware directory |
| `sysdata/shared_font.bin` | `azahar`, `mandarine` | azahar has no cited firmware directory; mandarine has no cited firmware directory |

## actionmax

actionmax: 0 slots, 71 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `ace.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `aceeuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astron.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astronp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlandp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlands.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `bega.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `begar1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `blazer.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff_ox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobra.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraab.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraconv.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobram3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cputest.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle11.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle20.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle21.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `esh.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefoxa.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxy.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxyp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gpworld.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gtg.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `interstellar.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_211.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_300.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_314.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_315.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_316_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_318.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_span.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_b.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_c.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_e.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_f.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_ita.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_n1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_x.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `laireuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lgp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `mach3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `roadblaster.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sae.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshort.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshortalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `timetrav.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_swear.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `uvt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |

## adam

Coleco Adam: 6 slots, 19 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/adam.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `alf @1 rev 57 e3d5.u8` crc32 `565b364a` 8192 B, `alf @2 rev 57 ae6a.u20` crc32 `44a1cff4` 8192 B, `alf @3 rev 57 8534.u21` crc32 `77657b90` 8192 B, `eos 6 rev 57 08dd.u22` crc32 `ef6403c5` 8192 B, `master rev a 174b.u6` crc32 `035a7a3d` 2048 B, `os7.u2` crc32 `3aa93ef3` 8192 B, `wp_r80.rom` crc32 `58d86a2a` 32768 B | `mame`: `src/mame/coleco/adam.cpp:1114-1128` |
| `bios/adam_ddp.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `tape rev a 8865.u24` crc32 `6b9ea1cf` 2048 B | `mame`: `src/devices/bus/adamnet/ddp.cpp:35-38` |
| `bios/adam_fdc.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `adam disk u10 ad 31 rev a 09-27-84.u10` crc32 `4b0b7143` 4096 B | `mame`: `src/devices/bus/adamnet/fdc.cpp:65-69` |
| `bios/adam_kb.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `keyboard.u2` crc32 `ef204746` 2048 B | `mame`: `src/devices/bus/adamnet/kb.cpp:33-36` |
| `bios/adam_prn.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `printer.u2` crc32 `e8db783b` 2048 B | `mame`: `src/devices/bus/adamnet/printer.cpp:33-36` |
| `bios/writer.rom`<br>also `bios/wp.rom`, `bios/wp_r80.rom` | yes | `libretro/gearcoleco` | `4fe4f6800076ea3d897d4285653447bd` no region; retrobios `gearcoleco` | `gearcoleco`: `platforms/libretro/libretro.cpp:880-941 (load_adam_firmware), platforms/libretro/libretro.cpp:2371-2372, platforms/libretro/libretro.cpp:2428-2432, platforms/libretro/libretro.cpp:2445-2446, platforms/libretro/libretro.cpp:2529-2533, src/Adam.cpp:37-41, src/Adam.h:55, src/Adam.cpp:139-148, src/Adam.cpp:168-190, src/GearcolecoCore.cpp:410-415, src/Adam.cpp:531-540` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `adam.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_ddp.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_fdc.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_fdc_320kb.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_fdc_a720dipi.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_fdc_fp720at.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_fdc_mihddd.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_kb.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_prn.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `adam_spi.zip` | `mame64/adam` | mame64 has no cited firmware directory |
| `bios/adam_fdc_320kb.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/adam_fdc_a720dipi.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/adam_fdc_fp720at.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/adam_fdc_mihddd.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/adam_spi.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/eos.rom` | `libretro/gearcoleco` | no identity: no source states a hash for it |
| `bios/mame/hash/adam_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/adam_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/adam_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## advision

Adventure Vision: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/advision.zip` | yes | `libretro/mame`, RetroBat's list | `1c08606408029751fd30ac91d2bf30d5` no region; RetroBat's list<br>zip, for `libretro/mame`: `cop411l-kcn_n.u8` crc32 `81e95975` 512 B, `ins8048-11kdp_n.u5` crc32 `279e33d1` 1024 B | `mame`: `src/mame/entex/advision.cpp:471` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `advision.zip` | `mame64/advision` | mame64 has no cited firmware directory |
| `bios/mame/hash/advision.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## amiga1200

Amiga 1200: 3 slots, 43 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kick34005.A500`<br>also `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV).rom`, `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom`, `bios/amiga-os-130.rom` | yes | `libretro/puae` | `82a21c1890cae844b3df741f2762d48d` europe, north-america; RetroBat's list, RomM's known_bios_files | `puae`: `libretro/libretro-core.h:187-190` |
| `bios/kick40063.A600`<br>also `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000).rom`, `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000)[!].rom`, `bios/amiga-os-310-a600.rom` | no | `libretro/puae` | `e40a5dfb3d017ba8779faba30cbd1c8e` no region; RomM's known_bios_files | `puae`: `libretro/libretro-core.h:203-206` |
| `bios/kick40068.A1200`<br>also `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200).rom`, `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200)[!].rom`, `bios/amiga-os-310-a1200.rom` | yes | `libretro/amiberry`, `libretro/puae`, RetroBat's list | `646773759326fbac3b2311fd8c8793ee` no region; RetroBat's list, RomM's known_bios_files | `amiberry`: `libretro/libretro.cpp:767 (option label), libretro/libretro.cpp:1802 (candidates_a1200)`<br>`puae`: `libretro/libretro-core.h:214-217` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `6570-036.rom` | `amiberry` | amiberry has no cited firmware directory |
| `68HC05.rom` | `amiberry` | amiberry has no cited firmware directory |
| `aros-ext.bin` | `amiberry` | amiberry has no cited firmware directory |
| `aros-rom.bin` | `amiberry` | amiberry has no cited firmware directory |
| `bios/6570-036.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/68HC05.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-ext.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-rom.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/D8039HLC.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick12.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick13.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick20.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick205.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick32034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick33180.A500` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick37175.A500` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick37350.A600` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A1200` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A4000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick40068.A4000` | `libretro/amiberry`, `libretro/puae` | no identity: no source states a hash for it |
| `bios/mt32-roms/cm32l_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/mt32_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_cm32l.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_mt32.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/rom.key` | `libretro/amiberry`, `libretro/puae` | no identity: no source states a hash for it |
| `bios/WHDLoad.prefs` | `libretro/puae` | no identity: no source states a hash for it |
| `D8039HLC.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick12.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick13.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick20.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick205.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick31.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A1200` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A4000` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/cm32l_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/mt32_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_cm32l.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_mt32.rom` | `amiberry` | amiberry has no cited firmware directory |
| `rom.key` | `amiberry` | amiberry has no cited firmware directory |

## amiga4000

Amiga 4000: 4 slots, 42 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kick34005.A500`<br>also `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV).rom`, `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom`, `bios/amiga-os-130.rom` | yes | `libretro/puae` | `82a21c1890cae844b3df741f2762d48d` europe, north-america; RetroBat's list, RomM's known_bios_files | `puae`: `libretro/libretro-core.h:187-190` |
| `bios/kick40063.A600`<br>also `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000).rom`, `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000)[!].rom`, `bios/amiga-os-310-a600.rom` | no | `libretro/puae` | `e40a5dfb3d017ba8779faba30cbd1c8e` no region; RomM's known_bios_files | `puae`: `libretro/libretro-core.h:203-206` |
| `bios/kick40068.A1200`<br>also `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200).rom`, `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200)[!].rom`, `bios/amiga-os-310-a1200.rom` | yes | `libretro/amiberry`, `libretro/puae` | `646773759326fbac3b2311fd8c8793ee` no region; RetroBat's list, RomM's known_bios_files | `amiberry`: `libretro/libretro.cpp:767 (option label), libretro/libretro.cpp:1802 (candidates_a1200)`<br>`puae`: `libretro/libretro-core.h:214-217` |
| `bios/kick40068.A4000`<br>also `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A4000).rom`, `bios/amiga-os-310-a4000.rom` | yes | `libretro/amiberry`, `libretro/puae`, RetroBat's list | `9bdedde6a4f33555b4a270c8ca53297d` no region; RetroBat's list | `amiberry`: `libretro/libretro.cpp:768 (option label), libretro/libretro.cpp:1803 (candidates_a4000)`<br>`puae`: `libretro/libretro-core.h:225-228` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `6570-036.rom` | `amiberry` | amiberry has no cited firmware directory |
| `68HC05.rom` | `amiberry` | amiberry has no cited firmware directory |
| `aros-ext.bin` | `amiberry` | amiberry has no cited firmware directory |
| `aros-rom.bin` | `amiberry` | amiberry has no cited firmware directory |
| `bios/6570-036.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/68HC05.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-ext.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-rom.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/D8039HLC.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick12.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick13.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick20.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick205.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick32034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick33180.A500` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick37175.A500` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick37350.A600` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A1200` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A4000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/mt32-roms/cm32l_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/mt32_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_cm32l.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_mt32.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/rom.key` | `libretro/amiberry`, `libretro/puae` | no identity: no source states a hash for it |
| `bios/WHDLoad.prefs` | `libretro/puae` | no identity: no source states a hash for it |
| `D8039HLC.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick12.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick13.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick20.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick205.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick31.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A1200` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A4000` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/cm32l_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/mt32_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_cm32l.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_mt32.rom` | `amiberry` | amiberry has no cited firmware directory |
| `rom.key` | `amiberry` | amiberry has no cited firmware directory |

## amiga500

Amiga 500: 5 slots, 41 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kick33180.A500`<br>also `bios/Kickstart v1.2 rev 33.180 (1986)(Commodore)(A500-A1000-A2000).rom`, `bios/Kickstart v1.2 rev 33.180 (1986)(Commodore)(A500-A2000)[!].rom`, `bios/amiga-os-120.rom` | yes | `libretro/puae`, RetroBat's list | `85ad74194e87c08904327de1a9443b7a` north-america, europe; RetroBat's list | `puae`: `libretro/libretro-core.h:182-185` |
| `bios/kick34005.A500`<br>also `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV).rom`, `bios/Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom`, `bios/amiga-os-130.rom` | yes | `libretro/puae`, RetroBat's list | `82a21c1890cae844b3df741f2762d48d` europe, north-america; RetroBat's list, RomM's known_bios_files | `puae`: `libretro/libretro-core.h:187-190` |
| `bios/kick37175.A500`<br>also `bios/Kickstart v2.04 rev 37.175 (1991)(Commodore)(A500+).rom`, `bios/Kickstart v2.04 rev 37.175 (1991)(Commodore)(A500+)[!].rom`, `bios/amiga-os-204.rom` | yes | `libretro/puae`, RetroBat's list | `dc10d7bdd1b6f450773dfb558477c230` north-america, europe; RetroBat's list | `puae`: `libretro/libretro-core.h:192-195` |
| `bios/kick40063.A600`<br>also `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000).rom`, `bios/Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000)[!].rom`, `bios/amiga-os-310-a600.rom` | no | `libretro/puae` | `e40a5dfb3d017ba8779faba30cbd1c8e` no region; RomM's known_bios_files | `puae`: `libretro/libretro-core.h:203-206` |
| `bios/kick40068.A1200`<br>also `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200).rom`, `bios/Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200)[!].rom`, `bios/amiga-os-310-a1200.rom` | yes | `libretro/amiberry`, `libretro/puae` | `646773759326fbac3b2311fd8c8793ee` no region; RetroBat's list, RomM's known_bios_files | `amiberry`: `libretro/libretro.cpp:767 (option label), libretro/libretro.cpp:1802 (candidates_a1200)`<br>`puae`: `libretro/libretro-core.h:214-217` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `6570-036.rom` | `amiberry` | amiberry has no cited firmware directory |
| `68HC05.rom` | `amiberry` | amiberry has no cited firmware directory |
| `aros-ext.bin` | `amiberry` | amiberry has no cited firmware directory |
| `aros-rom.bin` | `amiberry` | amiberry has no cited firmware directory |
| `bios/6570-036.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/68HC05.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-ext.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/aros-rom.bin` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/D8039HLC.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick12.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick13.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick20.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick205.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/kick31034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick32034.A1000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick37350.A600` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A1200` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick39106.A4000` | `libretro/puae` | no identity: no source states a hash for it |
| `bios/kick40068.A4000` | `libretro/amiberry`, `libretro/puae` | no identity: no source states a hash for it |
| `bios/mt32-roms/cm32l_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/mt32_control.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_cm32l.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/mt32-roms/pcm_mt32.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/rom.key` | `libretro/amiberry`, `libretro/puae` | no identity: no source states a hash for it |
| `bios/WHDLoad.prefs` | `libretro/puae` | no identity: no source states a hash for it |
| `D8039HLC.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick12.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick13.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick20.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick205.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick31.rom` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A1200` | `amiberry` | amiberry has no cited firmware directory |
| `kick40068.A4000` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/cm32l_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/mt32_control.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_cm32l.rom` | `amiberry` | amiberry has no cited firmware directory |
| `mt32-roms/pcm_mt32.rom` | `amiberry` | amiberry has no cited firmware directory |
| `rom.key` | `amiberry` | amiberry has no cited firmware directory |

## amigacd32

Amiga CD32: 2 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kick40060.CD32`<br>also `bios/Kickstart v3.1 rev 40.60 (1993)(Commodore)(CD32).rom`, `bios/amiga-os-310-cd32.rom` | yes | `libretro/puae`, RetroBat's list | `5f8924d013dd57a89cf349f4cdedc6b1` no region; RetroBat's list | `puae`: `libretro/libretro-core.h:236-239` |
| `bios/kick40060.CD32.ext`<br>also `bios/CD32 Extended-ROM rev 40.60 (1993)(Commodore)(CD32).rom`, `bios/amiga-os-310-cd32-ext.rom` | yes | `libretro/puae`, RetroBat's list | `bb72565701b1b6faece07d68ea5da639` no region; RetroBat's list | `puae`: `libretro/libretro-core.h:241-244` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/cd32.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/cd32ext.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `cd32.rom` | `amiberry` | amiberry has no cited firmware directory |
| `cd32ext.rom` | `amiberry` | amiberry has no cited firmware directory |

## amigacdtv

Amiga CDTV: 2 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kick34005.CDTV`<br>also `bios/CDTV Extended-ROM v1.0 (1991)(Commodore)(CDTV)[!].rom`, `bios/CDTV Extended-ROM v1.0 (1992)(Commodore)(CDTV).rom`, `bios/amiga-os-130-cdtv-ext.rom` | yes | `libretro/puae`, RetroBat's list | `89da1838a24460e4b93f4f0c5d92d48d` no region; RetroBat's list | `puae`: `libretro/libretro-core.h:231-234` |
| `bios/kick40060.CD32.ext` | yes | RetroBat's list | `bb72565701b1b6faece07d68ea5da639` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/cdtv.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `bios/cdtvext.rom` | `libretro/amiberry` | no identity: no source states a hash for it |
| `cdtv.rom` | `amiberry` | amiberry has no cited firmware directory |
| `cdtvext.rom` | `amiberry` | amiberry has no cited firmware directory |

## apfm1000

APF M-1000: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/apfm1000.zip` | yes | `libretro/mame`, RetroBat's list | `b197db12f7a13827717bbeb7cfd9542a` no region; RetroBat's list<br>zip, for `libretro/mame`: `apf_4000.rom` crc32 `cc6ac840` 2048 B, `mod_bios.bin` crc32 `f320aba6` 4096 B, `trash-ii.bin` crc32 `3bd8640a` 4096 B | `mame`: `src/mame/apf/apf.cpp:642` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `apfm1000.zip` | `mame64/apfm1000` | mame64 has no cited firmware directory |
| `bios/mame/hash/apfm1000.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## apple2

Apple II: 16 slots, 16 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/a2diskiing.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2-asoft-auto.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2-int-auto.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2e.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2ee.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/apple2p.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/AppleIIe.rom` | yes | `bizhawk/AppleII`, RetroBat's list | `38063e08c778503fc03ecebb979769e9` no region; BizHawk's FirmwareDatabase.cs, another source's hash for this name |  |
| `bios/AppleIIe_DiskII.rom` | yes | `bizhawk/AppleII`, RetroBat's list | `2020aa1413ff77fe29353f3ee72dc295` no region; BizHawk's FirmwareDatabase.cs, another source's hash for this name |  |
| `bios/d2fdc.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/disk2-13boot.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/disk2-13seq.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/disk2-16boot.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/disk2-16seq.rom` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/votrsc01.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/votrsc01a.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `apple2-asoft-auto.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2-int-auto.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e-enh-kb.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e-enh-video.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e-enh.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e-kb.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e-video.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `apple2e.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `bios/mame/hash/apple2_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/apple2_flop_clcracked.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/apple2_flop_misc.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/apple2_flop_orig.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `disk2-13boot.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `disk2-13seq.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `disk2-16boot.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |
| `disk2-16seq.rom` | `mednafen/apple2` | mednafen has no cited firmware directory |

## apple2gs

Apple II GS: 2 slots, 6 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/APPLE2GS.ROM` | yes | RetroBat's list | `20a0334c447cb069a040ae5be1d938df` no region; RetroBat's list |  |
| `bios/apple2gs.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `341-0728` crc32 `8d410067` 131072 B, `341-0748` crc32 `18190283` 131072 B, `341s0632-2.bin` crc32 `e1c11fb0` 4096 B, `344s0047.bin` crc32 `2d541944` 16384 B | `mame`: `src/mame/apple/apple2gs.cpp:3865-3879` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `apple2gs.zip` | `mame64` | mame64 has no cited firmware directory |
| `bios/mame/hash/apple2gs_flop_clcracked.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/apple2gs_flop_misc.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/apple2gs_flop_orig.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `c600.rom` | `gsplus/gsplus` | gsplus has no cited firmware directory |
| `ROM` | `gsplus/gsplus` | gsplus has no cited firmware directory |

## aquarius

Aquarius: 4 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/aquarius.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/aquarius2.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/aquarius_ar.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/aquariusp.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/aquarius_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/aquarius_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## arcadia

Arcadia 2001: 1 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/ar_bios.zip` | yes | RetroBat's list | `37e9746f4491aa2df9a83729d1a93620` no region; RetroBat's list<br>zip, for `libretro/mame`: `315093-01.u2` crc32 `a6ce1636` 262144 B, `gcp-1-hi` crc32 `67d44523` 65536 B, `gcp-1-lo` crc32 `65d9b9cf` 65536 B, `gcp-2-hi` crc32 `1d7594ae` 65536 B, `gcp-2-lo` crc32 `e776198d` 65536 B, `gcp-3-hi` crc32 `3e7364be` 65536 B, `gcp-3-lo` crc32 `87229e0d` 65536 B, `gcp_v311_1-hi.u16` crc32 `0b486a85` 65536 B, `gcp_v311_1-lo.u11` crc32 `80e8e863` 65536 B, `gcp_v311_2-hi.u17` crc32 `d20a4d7f` 65536 B, `gcp_v311_2-lo.u12` crc32 `5bf4c74c` 65536 B, `gcp_v400_1-hi.u16` crc32 `69295167` 65536 B, `gcp_v400_1-lo.u11` crc32 `504c2171` 65536 B, `gcp_v400_2-hi.u17` crc32 `13fb4e2d` 65536 B, `gcp_v400_2-lo.u12` crc32 `a5cc4515` 65536 B, `scpa_01-hi_v2.11.u12` crc32 `be9dbdc5` 65536 B, `scpa_01-hi_v2.20.u12` crc32 `79450b4b` 65536 B, `scpa_01-hi_v3.0.u12` crc32 `2d8e1a06` 65536 B, `scpa_01-lo_v2.11.u16` crc32 `95b84504` 65536 B, `scpa_01-lo_v2.20.u16` crc32 `d2825511` 65536 B, `scpa_01-lo_v3.0.u16` crc32 `e4f38fab` 65536 B |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/arcadia.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## archimedes

Acorn Archimedes: 2 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/aa310.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/archimedes_keyboard.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/archimedes.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## astrocade

Astrocade: 3 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/astrocde.zip` | yes | `libretro/mame`, RetroBat's list | `f58a3823ab308ef0a02b498d126e5d96` no region; RetroBat's list<br>zip, for `libretro/fbneo`, `libretro/mame`: `astro.bin` crc32 `ebc77f3a` 8192 B | `mame`: `src/mame/bally/astrohome.cpp:244-247` |
| `bios/astrocdl.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `ballyhlc.bin` crc32 `d7c517ba` 8192 B | `mame`: `src/mame/bally/astrohome.cpp:249-251` |
| `bios/astrocdw.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `bioswhit.bin` crc32 `6eb53e79` 8192 B | `mame`: `src/mame/bally/astrohome.cpp:254-257` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `astrocde.zip` | `mame64/astrocde` | mame64 has no cited firmware directory |
| `astrocdl.zip` | `mame64/astrocde` | mame64 has no cited firmware directory |
| `astrocdw.zip` | `mame64/astrocde` | mame64 has no cited firmware directory |
| `bios/mame/hash/astrocde.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## atari5200

Atari 5200: 7 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/5200.rom`<br>also `bios/5200.bin`, `bios/atar5200.rom`, `bios/atari5200.rom`, `bios/atari_5200.rom` | yes | `libretro/a5200`, `libretro/atari800`, RetroBat's list | `281f20ea4320404ec820fb7ec0693b38` north-america; retrobios `atari800`, RetroBat's list | `a5200`: `libretro/libretro.c:58-59, libretro/libretro.c:416-507, emu/memory.c:46`<br>`atari800`: `atari800/src/sysrom.c:103-104, atari800/src/sysrom.c:290` |
| `bios/ATARIBAS.ROM`<br>also `bios/atari_basic.rom`, `bios/ataribasic.rom`, `bios/basic.rom` | no | `libretro/atari800` | `0bac0c6a50104045d902df4503a4c30b` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:105-107, atari800/src/sysrom.c:292` |
| `bios/ATARIOSA.ROM`<br>also `bios/atari_os_a.rom`, `bios/atari_osa.rom` | no | `libretro/atari800` | `eb1f32f5d9f382db1bbfb8d7f9cb343a` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:89-90, atari800/src/sysrom.c:285` |
| `bios/ATARIOSB.ROM`<br>also `bios/atari_os_b.rom`, `bios/atari_osb.rom` | no | `libretro/atari800` | `a3e8d617c95d08031fe1b20d541434b2` no region; RetroBat's list<br>`4177f386a3bac989a981d3fe3388cb6c` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:91, atari800/src/sysrom.c:286` |
| `bios/ATARIXL.ROM`<br>also `bios/atari_xl_xe.rom`, `bios/atari_xlxe.rom`, `bios/atarixlxe.rom` | no | `libretro/atari800` | `06daac977823773a3eea3422fd26a703` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:92-101, atari800/src/sysrom.c:288` |
| `bios/BB01R4_OS.ROM` | no | `libretro/atari800` | `b7a2a04677d34f069eeb643d5238bf86` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:100` |
| `bios/XEGAME.ROM` | no | `libretro/atari800` | `d7eb37aec6960cba36bc500e0e5d00bc` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:108` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `atari-5200-os-2port.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-5200-os-4port.rom` | `altirra` | altirra has no cited firmware directory |

## atari7800

Atari 7800: 4 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/7800 BIOS (E).rom` | yes | `bizhawk/A78`, `libretro/prosystem`, RetroBat's list | `397bb566584be7b9764e7a68974c4263` europe; BizHawk's FirmwareDatabase.cs, RetroBat's list | `prosystem`: `core/libretro.c:583` |
| `bios/7800 BIOS (U).rom` | yes | `bizhawk/A78`, `libretro/prosystem`, RetroBat's list | `0763f1ffb006ddbe32e52d497ee848ae` north-america; BizHawk's FirmwareDatabase.cs, RetroBat's list | `prosystem`: `core/libretro.c:585` |
| `bios/A78_highscore.bin` | yes | `bizhawk/A78`, RetroBat's list | `c8a73288ab97226c52602204ab894286` no region; BizHawk's FirmwareDatabase.cs, another source's hash for this name |  |
| `bios/ProSystem.dat` | yes | RetroBat's list | `ce6a86574d0c9de9075705f14e99d090` no region; RetroBat's list |  |

## atari800

Atari 800: 7 slots, 103 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/5200.rom`<br>also `bios/5200.bin`, `bios/atar5200.rom`, `bios/atari5200.rom`, `bios/atari_5200.rom` | no | `libretro/atari800` | `281f20ea4320404ec820fb7ec0693b38` north-america; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:103-104, atari800/src/sysrom.c:290` |
| `bios/ATARIBAS.ROM`<br>also `bios/atari_basic.rom`, `bios/ataribasic.rom`, `bios/basic.rom` | yes | `libretro/atari800`, RetroBat's list | `0bac0c6a50104045d902df4503a4c30b` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:105-107, atari800/src/sysrom.c:292` |
| `bios/ATARIOSA.ROM`<br>also `bios/atari_os_a.rom`, `bios/atari_osa.rom` | yes | `libretro/atari800`, RetroBat's list | `eb1f32f5d9f382db1bbfb8d7f9cb343a` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:89-90, atari800/src/sysrom.c:285` |
| `bios/ATARIOSB.ROM`<br>also `bios/atari_os_b.rom`, `bios/atari_osb.rom` | yes | `libretro/atari800`, RetroBat's list | `a3e8d617c95d08031fe1b20d541434b2` no region; RetroBat's list<br>`4177f386a3bac989a981d3fe3388cb6c` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:91, atari800/src/sysrom.c:286` |
| `bios/ATARIXL.ROM`<br>also `bios/atari_xl_xe.rom`, `bios/atari_xlxe.rom`, `bios/atarixlxe.rom` | yes | `libretro/atari800`, RetroBat's list | `06daac977823773a3eea3422fd26a703` no region; retrobios `atari800`, RetroBat's list | `atari800`: `atari800/src/sysrom.c:92-101, atari800/src/sysrom.c:288` |
| `bios/BB01R4_OS.ROM` | no | `libretro/atari800` | `b7a2a04677d34f069eeb643d5238bf86` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:100` |
| `bios/XEGAME.ROM` | no | `libretro/atari800` | `d7eb37aec6960cba36bc500e0e5d00bc` no region; retrobios `atari800` | `atari800`: `atari800/src/sysrom.c:108` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `1050-duplicator.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo-ii-3.4.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo-ii-3.5.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo.rom` | `altirra` | altirra has no cited firmware directory |
| `1400xl-handler-v1.rom` | `altirra` | altirra has no cited firmware directory |
| `1400xl-handler-v2.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-controller.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-post-rev-f.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-rev-f.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-tong-disk-controller.rom` | `altirra` | altirra has no cited firmware directory |
| `810-archiver.rom` | `altirra` | altirra has no cited firmware directory |
| `810-turbo-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `amdek-amdc.rom` | `altirra` | altirra has no cited firmware directory |
| `ape-warp-os.rom` | `altirra` | altirra has no cited firmware directory |
| `astra-1001-1620.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1025.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1029-english.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-download.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-external.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-internal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-h.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-j.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-k.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-l.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1090-charset.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1090-firmware-rev-9.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1200xl-os-rev-10.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1200xl-os-rev-11.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-a-ntsc.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-a-pal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-ntsc-xformer.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-ntsc.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-pal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-b.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-c.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-815.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-820.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-835-internal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-850-early.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-850.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-a.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-b.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-c.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-1.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-2.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-3.rom` | `altirra` | altirra has no cited firmware directory |
| `atr8000-3.02.rom` | `altirra` | altirra has no cited firmware directory |
| `bit3-full-view-80-charset-4k.rom` | `altirra` | altirra has no cited firmware directory |
| `bit3-full-view-80-charset.rom` | `altirra` | altirra has no cited firmware directory |
| `bit3-full-view-80-firmware.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-1.34.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-1.41.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-2.16.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-floppy-board-1.5.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-1050-rev-1.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-1050-rev-2.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-3k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-4k-nonoriginal.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-4k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-6k-modified.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-6k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-8k.rom` | `altirra` | altirra has no cited firmware directory |
| `indus-gt-1.1.rom` | `altirra` | altirra has no cited firmware directory |
| `indus-gt-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `is-plate.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide2-main.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide2-sdx.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.1k-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.1m-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-256kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-64kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `myide2.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at-88-1.2-damaged.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at-88-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.01.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.11.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.21.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.00.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.10.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.20.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-2.10.rom` | `altirra` | altirra has no cited firmware directory |
| `rapidus-core-pbi.rom` | `altirra` | altirra has no cited firmware directory |
| `rapidus-flash.rom` | `altirra` | altirra has no cited firmware directory |
| `side.rom` | `altirra` | altirra has no cited firmware directory |
| `side2.rom` | `altirra` | altirra has no cited firmware directory |
| `side3.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-xf-1.7.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-xf-1.8.rom` | `altirra` | altirra has no cited firmware directory |
| `super-archiver.rom` | `altirra` | altirra has no cited firmware directory |
| `toms-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `tygrys-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `ultimate1mb.rom` | `altirra` | altirra has no cited firmware directory |
| `us-doubler-rev-l.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.4.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7-720k.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7-density-hack.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7.rom` | `altirra` | altirra has no cited firmware directory |

## atarist

Atari ST: 11 slots, 7 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/hatari/tos/emutos.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatari/tos/tos102.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatari/tos/tos104.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatari/tos/tos106.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatari/tos/tos206.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatarib/emutos.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatarib/tos102.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatarib/tos104.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatarib/tos106.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/hatarib/tos206.img` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/tos.img` | yes | `libretro/hatari`, `libretro/hatarib`, RetroBat's list | `c1c57ce48e8ee4135885cee9e63a68a2` no region; RomM's known_bios_files | `hatari`: `src/retro/main_retro.c:157-207, src/retro/main_retro.c:250-261, src/options.c:1937-1945, src/tos.c:60-66, src/tos.c:976-1097, src/tos.c:838-969, src/file.c:312-353`<br>`hatarib`: `core/core_file.c:791-851, core/core_config.c:68-92, core/core_config.c:1112-1135, core/core_config.c:1325-1340, hatari/src/tos.c:1028-1160` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/capsimg.dll` | `libretro/hatarib` | no identity: no source states a hash for it |
| `bios/capsimg.so` | `libretro/hatarib` | no identity: no source states a hash for it |
| `bios/cartridge ROM` | `libretro/hatari`, `libretro/hatarib` | no identity: no source states a hash for it |
| `bios/hatari/BOOT.ST` | `libretro/hatari` | no identity: no source states a hash for it |
| `cartridge ROM` | `hatari` | hatari has no cited firmware directory |
| `hatari/BOOT.ST` | `hatari` | hatari has no cited firmware directory |
| `tos.img` | `hatari` | hatari has no cited firmware directory |

## atom

Acorn Atom: 1 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/atom.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/atom_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/atom_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/atom_rom.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## atomiswave

Atomiswave: 1 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dc/awbios.zip` | yes | `libretro/flycast`, RetroBat's list | `0ec5ae5b5a5c4959fa8b43fcf8687f7c` no region; RetroBat's list | `flycast`: `core/hw/naomi/naomi_roms.h:87-94` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `awbios.zip` | `demul/atomiswave` | demul has no cited firmware directory |
| `dc/awbios.zip` | `flycast/atomiswave` | flycast has no cited firmware directory |
| `jvs13551.zip` | `demul/atomiswave` | demul has no cited firmware directory |
| `mie.zip` | `demul/atomiswave` | demul has no cited firmware directory |

## bbcmicro

BBC: 9 slots, 8 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bbc_acorn8271.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbc_bitstik1.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbc_bitstik2.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbc_tube_80186.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbcb.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbcm.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/bbcmc.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/mame/samples/bbc.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/saa5050.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/bbc_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbc_rom.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcb_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcb_flop_orig.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcb_flop_us.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcm_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcm_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/bbcmc_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## bk

Elektronika BK: 8 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bk/B11M_BOS.ROM`<br>also `bios/bk/b11m_bos.rom` | yes | `libretro/bk`, RetroBat's list | `fe4627d1e3a1535874085050733263e7` no region; RetroBat's list | `bk`: `main.c:55, boot.c:98` |
| `bios/bk/B11M_EXT.ROM`<br>also `bios/bk/b11m_ext.rom` | yes | `libretro/bk`, RetroBat's list | `dc52f365d56fa1951f5d35b1101b9e3f` no region; RetroBat's list | `bk`: `main.c:56, boot.c:102` |
| `bios/bk/BAS11M_0.ROM`<br>also `bios/bk/bas11m_0.rom` | yes | `libretro/bk`, RetroBat's list | `946f6f23ded03c0e26187f0b3ca75993` no region; RetroBat's list | `bk`: `main.c:57, boot.c:100` |
| `bios/bk/BAS11M_1.ROM`<br>also `bios/bk/bas11m_1.rom` | yes | `libretro/bk`, RetroBat's list | `1e6637f32aa7d1de03510030cac40bcf` no region; RetroBat's list | `bk`: `main.c:58, boot.c:101` |
| `bios/bk/BASIC10.ROM`<br>also `bios/bk/basic10.rom` | yes | `libretro/bk`, RetroBat's list | `3fa774326d75410a065659aea80252f0` no region; RetroBat's list | `bk`: `libretro.c:16, boot.c:110` |
| `bios/bk/DISK_327.ROM`<br>also `bios/bk/disk_327.rom` | yes | `libretro/bk`, RetroBat's list | `5015228eeeb238e65da8edcd1b6dfac7` no region; RetroBat's list | `bk`: `main.c:54, boot.c:99,113` |
| `bios/bk/FOCAL10.ROM`<br>also `bios/bk/focal10.rom` | yes | `libretro/bk`, RetroBat's list | `5737f972e8638831ab71e9139abae052` no region; RetroBat's list | `bk`: `libretro.c:15, boot.c:110` |
| `bios/bk/MONIT10.ROM`<br>also `bios/bk/monit10.rom` | yes | `libretro/bk`, RetroBat's list | `95f8c41c6abf7640e35a6a03cecebd01` no region; RetroBat's list | `bk`: `main.c:53, boot.c:107` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/bk/TERAK.ROM` | `libretro/bk` | no identity: no source states a hash for it |

## c128

c128: 0 slots, 6 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/JiffyDOS_1541-II.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |
| `bios/JiffyDOS_1571_repl310654.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |
| `bios/JiffyDOS_1581.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |
| `bios/JiffyDOS_C128.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |
| `bios/JiffyDOS_C64.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_C128.bin` | `libretro/vice_x128` | no identity: no source states a hash for it |

## c20

c20: 2 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/chargen-901460-02.bin` | no | `libretro/vice_xvic` | `7e6e41b5b60fec798743afd7063b42ed` japan; retrobios `vice_xvic` | `vice_xvic`: `vice/src/vic20/vic20rom.h:53, vice/src/vic20/vic20model.c:60, vice/src/vic20/vic20rom.c:173-179` |
| `bios/kernal.901486-02.bin` | no | `libretro/vice_xvic` | `317f93bcbc5ee15da085a51a306f2c61` japan; retrobios `vice_xvic` | `vice_xvic`: `vice/src/vic20/vic20rom.h:49, vice/src/vic20/vic20model.c:60, vice/src/vic20/vic20rom.c:112-120` |

## c64

c64: 2 slots, 16 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kernal-901227-01.bin` | no | `libretro/vice_x64`, `libretro/vice_x64sc` | `1ae0ea224f2b291dafa2c20b990bb7d4` north-america, europe; retrobios `vice_x64` | `vice_x64`: `vice/src/c64/c64-resources.c:253, vice/src/c64/c64model.c:131`<br>`vice_x64sc`: `vice/src/c64/c64-resources.c:253, vice/src/c64/c64scmodel.c:141` |
| `bios/kernal-901227-02.bin` | no | `libretro/vice_x64`, `libretro/vice_x64sc` | `7360b296d64e18b88f6cf52289fd99a1` north-america, europe; retrobios `vice_x64` | `vice_x64`: `vice/src/c64/c64-resources.c:254, vice/src/c64/c64rom.h:51`<br>`vice_x64sc`: `vice/src/c64/c64-resources.c:254, vice/src/c64/c64scmodel.c:123` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/1541 ROM` | `libretro/frodo` | no identity: no source states a hash for it |
| `bios/Basic ROM` | `libretro/frodo` | no identity: no source states a hash for it |
| `bios/Char ROM` | `libretro/frodo` | no identity: no source states a hash for it |
| `bios/JiffyDOS_1541-II.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/JiffyDOS_1571_repl310654.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/JiffyDOS_1581.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/JiffyDOS_C64.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/JiffyDOS_SX-64.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/Kernal ROM` | `libretro/frodo` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_1541-II.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_1571_repl310654.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_1581.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_C64.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/JiffyDOS_SX-64.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/SCPU64/scpu-dos-1.4.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |
| `bios/vice/SCPU64/scpu-dos-2.04.bin` | `libretro/vice_x64`, `libretro/vice_x64sc` | no identity: no source states a hash for it |

## camplynx

Camputers Lynx: 3 slots, 11 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/lynx128k.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `dosrom.rom` crc32 `011e106a` 8192 B, `lynx128-1.ic1` crc32 `65d292ce` 8192 B, `lynx128-2.ic2` crc32 `23288773` 8192 B, `lynx128-3.ic3` crc32 `9827b9e9` 8192 B | `mame`: `src/mame/camputers/camplynx.cpp:1020-1026` |
| `bios/lynx48k.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `lynx48-1.ic46` crc32 `56feec44` 8192 B, `lynx48-2.ic45` crc32 `d894562e` 8192 B, `lynx4811.ic46` crc32 `a933e577` 8192 B, `lynx4812.ic45` crc32 `3d3fdd0e` 8192 B | `mame`: `src/mame/camputers/camplynx.cpp:995-1004` |
| `bios/lynx96k.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `danish96k3.ic44` crc32 `795c22ea` 8192 B, `dosrom.rom` crc32 `011e106a` 8192 B, `lynx9644.ic44` crc32 `4b96b0de` 4096 B, `lynx9645.ic45` crc32 `f596b9a3` 8192 B, `lynx9646.ic46` crc32 `f86c5514` 8192 B, `skorprom.ic44` crc32 `698d3de9` 8192 B | `mame`: `src/mame/camputers/camplynx.cpp:1006-1018` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/camplynx_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/camplynx_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `emulators/jynx/lynx48-1.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `emulators/jynx/lynx48-2.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `emulators/jynx/lynx96-1.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `emulators/jynx/lynx96-2.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `emulators/jynx/lynx96-3-scorpion.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `emulators/jynx/lynx96-3.rom` | RetroBat's list | outside bios/, where RomMBat never writes |
| `lynx128k.zip` | `mame64/camplynx` | mame64 has no cited firmware directory |
| `lynx48k.zip` | `mame64/camplynx` | mame64 has no cited firmware directory |
| `lynx96k.zip` | `mame64/camplynx` | mame64 has no cited firmware directory |

## cave

cave: 0 slots, 1 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/hiscore.dat` | `libretro/fbalpha`, `libretro/fbalpha2012` | no identity: no source states a hash for it |

## cdi

Philips CD-I: 8 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/cdibios.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `cdi200.rom` crc32 `40c4e6b9` 524288 B, `cdi220b.rom` crc32 `279683ca` 524288 B | `mame`: `src/mame/philips/cdi.cpp:922` |
| `bios/cdimono1.zip` | yes | RetroBat's list | zip, for RetroBat's list: `cdi200.rom` crc32 `40c4e6b9` 524288 B, `cdi220.rom` crc32 `584c0af8` 524288 B, `cdi220b.rom` crc32 `279683ca` 524288 B, `vmpega.rom` crc32 `db264e8b` 262144 B, `zx405037p__cdi_servo_2.1__b43t__llek9215.mc68hc705c8a_withtestrom.7201` crc32 `7a3af407` 8192 B, `zx405042p__cdi_slave_2.0__b43t__zzmk9213.mc68hc705c8a_withtestrom.7206` crc32 `688cda63` 8192 B |  |
| `bios/cdimono2.zip` | yes | RetroBat's list | zip, for RetroBat's list: `philips__cdi-220_ph3_r1.2__mb834200b-15__02f_aa__9402_z04.tc574200-le._1.7211` crc32 `17d723e7` 524288 B, `zc405351p__servo_cdi_4.1__0d67p__lluk9404.mc68hc705c8a.7490` crc32 `2bc8e4e9` 8192 B, `zc405352p__slave_cdi_4.1__0d67p__lltr9403.mc68hc705c8a.7206` crc32 `5b19da07` 8192 B |  |
| `bios/same_cdi/bios/cdi490a.zip` | no | `libretro/same_cdi` | zip, for `libretro/same_cdi`: `cdi490a.rom` crc32 `e2f200f6` 524288 B, `impega.rom` crc32 `84d6f6aa` 262144 B, `vmpega.rom` crc32 `db264e8b` 262144 B | `same_cdi`: `src/mame/drivers/cdi.cpp:716` |
| `bios/same_cdi/bios/cdi910.zip` | no | `libretro/same_cdi` | zip, for `libretro/same_cdi`: `cdi910.rom` crc32 `2f3048d2` 524288 B, `philips__cd-i_2.1__mb834200b-15__26b_aa__9224_z01.tc574200.7211` crc32 `4ae3bee3` 524288 B, `ti_portugal_206xf__tibpal20l8-15cnt__m7205n.7205.bin` crc32 `dd167e0d` 324 B, `ti_portugal_774_206xf__tibpal16l8-10cn_m7204n.7204.bin` crc32 `04e6bd37` 260 B, `zx405037p__cdi_servo_2.1__b43t__llek9215.mc68hc705c8a_withtestrom.7201` crc32 `7a3af407` 8192 B, `zx405042p__cdi_slave_2.0__b43t__zzmk9213.mc68hc705c8a_withtestrom.7206` crc32 `688cda63` 8192 B | `same_cdi`: `src/mame/drivers/cdi.cpp:715` |
| `bios/same_cdi/bios/cdibios.zip` | yes | RetroBat's list | zip, for RetroBat's list: `cdi200.rom` crc32 `40c4e6b9` 524288 B, `cdi220b.rom` crc32 `279683ca` 524288 B |  |
| `bios/same_cdi/bios/cdimono1.zip` | yes | `libretro/same_cdi`, RetroBat's list | zip, for `libretro/same_cdi`, RetroBat's list: `cdi200.rom` crc32 `40c4e6b9` 524288 B, `cdi220.rom` crc32 `584c0af8` 524288 B, `cdi220b.rom` crc32 `279683ca` 524288 B, `vmpega.rom` crc32 `db264e8b` 262144 B, `zx405037p__cdi_servo_2.1__b43t__llek9215.mc68hc705c8a_withtestrom.7201` crc32 `7a3af407` 8192 B, `zx405042p__cdi_slave_2.0__b43t__zzmk9213.mc68hc705c8a_withtestrom.7206` crc32 `688cda63` 8192 B | `same_cdi`: `src/mame/drivers/cdi.cpp:713` |
| `bios/same_cdi/bios/cdimono2.zip` | yes | `libretro/same_cdi`, RetroBat's list | zip, for `libretro/same_cdi`, RetroBat's list: `philips__cdi-220_ph3_r1.2__mb834200b-15__02f_aa__9402_z04.tc574200-le._1.7211` crc32 `17d723e7` 524288 B, `zc405351p__servo_cdi_4.1__0d67p__lluk9404.mc68hc705c8a.7490` crc32 `2bc8e4e9` 8192 B, `zc405352p__slave_cdi_4.1__0d67p__lltr9403.mc68hc705c8a.7206` crc32 `5b19da07` 8192 B | `same_cdi`: `src/mame/drivers/cdi.cpp:714` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/cdi.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `cdibios.zip` | `mame64/cdi` | mame64 has no cited firmware directory |

## channelf

Fairchild ChannelF: 3 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/sl31253.bin` | yes | `bizhawk/ChannelFHawk`, `libretro/freechaf`, RetroBat's list | `ac9804d4c0e9d07e33472e3726ed15c3` north-america; BizHawk's FirmwareDatabase.cs, RetroBat's list | `freechaf`: `src/libretro.c:192` |
| `bios/sl31254.bin` | yes | `bizhawk/ChannelFHawk`, `libretro/freechaf`, RetroBat's list | `da98f4bb3242ab80d76629021bb27585` north-america; BizHawk's FirmwareDatabase.cs, RetroBat's list | `freechaf`: `src/libretro.c:202` |
| `bios/sl90025.bin` | yes | `bizhawk/ChannelFHawk`, `libretro/freechaf`, RetroBat's list | `95d339631d867c8f1d15a5f2ec26069d` north-america; BizHawk's FirmwareDatabase.cs, RetroBat's list | `freechaf`: `src/libretro.c:186` |

## chihiro

Sega Chihiro: 5 slots, 7 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/chihiro/chihiro_xbox_bios.bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/chihiro/fpr21042_m29w160et.bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/chihiro/ic10_g24lc64` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/chihiro/ic11_24lc024.bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/chihiro/pc20_g24lc64.bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `EmuMediaBoard/Chihiro/backup_ram.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/Chihiro/ic10_g24lc64.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/Chihiro/ic11_24lc024.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/Chihiro/pc20_g24lc64.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/fpr21042_m29w160et.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/Partition2.bin` | `cxbx` | cxbx has no cited firmware directory |
| `EmuMediaBoard/Partition3.bin` | `cxbx` | cxbx has no cited firmware directory |

## coco

Color Computer: 5 slots, 23 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/coco.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/coco2.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/coco2b.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/coco3.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/coco3p.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `adv071_u24.rom` | `xroar` | xroar has no cited firmware directory |
| `adv072_u24.rom` | `xroar` | xroar has no cited firmware directory |
| `adv073-2_u24.rom` | `xroar` | xroar has no cited firmware directory |
| `bas10.rom` | `xroar` | xroar has no cited firmware directory |
| `bas11.rom` | `xroar` | xroar has no cited firmware directory |
| `bas12.rom` | `xroar` | xroar has no cited firmware directory |
| `bas13.rom` | `xroar` | xroar has no cited firmware directory |
| `bios/mame/hash/coco_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/coco_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `coco3.rom` | `xroar` | xroar has no cited firmware directory |
| `coco3p.rom` | `xroar` | xroar has no cited firmware directory |
| `cp400bas.rom` | `xroar` | xroar has no cited firmware directory |
| `cp400ext.rom` | `xroar` | xroar has no cited firmware directory |
| `cp450dsk.rom` | `xroar` | xroar has no cited firmware directory |
| `deluxe.rom` | `xroar` | xroar has no cited firmware directory |
| `disk11.rom` | `xroar` | xroar has no cited firmware directory |
| `extbas10.rom` | `xroar` | xroar has no cited firmware directory |
| `extbas11.rom` | `xroar` | xroar has no cited firmware directory |
| `hdbdw3bck.rom` | `xroar` | xroar has no cited firmware directory |
| `mx1600bas.rom` | `xroar` | xroar has no cited firmware directory |
| `mx1600extbas.rom` | `xroar` | xroar has no cited firmware directory |
| `orch90.rom` | `xroar` | xroar has no cited firmware directory |
| `yados.rom` | `xroar` | xroar has no cited firmware directory |

## colecovision

ColecoVision: 5 slots, 19 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/coleco.rom` | yes | `bizhawk/Coleco`, `libretro/jollycv`, RetroBat's list | `2c66f5911e5b42b8ebe113403548eee7` europe, north-america; BizHawk's FirmwareDatabase.cs, retrobios `jollycv`, RetroBat's list | `jollycv`: `libretro/libretro.c:697-703, src/jollycv.c:406-432, src/jcv_coleco.c:402-410, jg.c:151-153` |
| `bios/coleco.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `coleco.rom` crc32 `3aa93ef3` 8192 B | `fbneo`: `src/burn/drv/coleco/d_coleco.cpp:1070`; `src/burn/drv/coleco/d_coleco.cpp:1071`; `src/burn/drv/coleco/d_coleco.cpp:1072`; `src/burn/drv/coleco/d_coleco.cpp:1073` |
| `bios/colecovision.rom`<br>also `bios/coleco.rom`, `bios/os7.u2` | yes | `libretro/gearcoleco`, RetroBat's list | `2c66f5911e5b42b8ebe113403548eee7` europe, north-america; retrobios `gearcoleco`, RetroBat's list | `gearcoleco`: `platforms/libretro/libretro.cpp:772-833 (find_firmware), platforms/libretro/libretro.cpp:851-878 (load_colecovision_firmware), platforms/libretro/libretro.cpp:2398-2402, platforms/libretro/libretro.cpp:2203-2204, src/Adam.cpp:28-32, src/Adam.h:53, src/Memory.cpp:310-319, src/Memory.cpp:246-249` |
| `bios/Machines/COL - ColecoVision/coleco.rom` | yes | `libretro/bluemsx` | `2c66f5911e5b42b8ebe113403548eee7` north-america, europe; retrobios `bluemsx` | `bluemsx`: `system/bluemsx/Machines/COL - ColecoVision/config.ini` |
| `bios/openMSX/share/systemroms/coleco.rom` | yes | RetroBat's list | `2c66f5911e5b42b8ebe113403548eee7` north-america, europe; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `COLECO.ROM` | `openmsx` | openmsx has no cited firmware directory |
| `colecovision.rom` | `ares/ColecoVision` | ares has no cited firmware directory |
| `emulators/mesen/Firmware/bios.col` | `mesen` | outside bios/, where RomMBat never writes |
| `fmpac.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1gt_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_basic-bios2p.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_disk.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_fmbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjibasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjifont.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_msx2psub.rom` | `openmsx` | openmsx has no cited firmware directory |
| `ide250.dat` | `openmsx` | openmsx has no cited firmware directory |
| `NMS8245SystemROM1.08.bin` | `openmsx` | openmsx has no cited firmware directory |
| `phc-70fd2_basickun.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_basic-bios1.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_pbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `svi-3x8_v111.rom` | `openmsx` | openmsx has no cited firmware directory |
| `yrw801.rom` | `openmsx` | openmsx has no cited firmware directory |

## cps1

cps1: 0 slots, 2 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/fbalpha2012/hiscore.dat` | `libretro/fbalpha2012_cps1` | no identity: no source states a hash for it |
| `bios/hiscore.dat` | `libretro/fbalpha`, `libretro/fbalpha2012` | no identity: no source states a hash for it |

## cps2

cps2: 0 slots, 2 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/fbalpha2012/hiscore.dat` | `libretro/fbalpha2012_cps2` | no identity: no source states a hash for it |
| `bios/hiscore.dat` | `libretro/fbalpha`, `libretro/fbalpha2012` | no identity: no source states a hash for it |

## cps3

cps3: 27 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/jojo.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_euro.29f400.u2` crc32 `513e40ec` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:799` |
| `bios/jojoa.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_asia.29f400.u2` crc32 `789aa72a` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:829` |
| `bios/jojoba.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_euro_nocd.29f400.u2` crc32 `1ee2d679` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1120` |
| `bios/jojobajr1.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_japan.29f400.u2` crc32 `3085478c` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1199` |
| `bios/jojoban.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_japan_nocd.29f400.u2` crc32 `4dab19f5` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1133` |
| `bios/jojobar1.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_euro.29f400.u2` crc32 `63cc8800` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1184` |
| `bios/jojoj.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_japan.29f400.u2` crc32 `02778f60` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:844` |
| `bios/jojon.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_asia_nocd.29f400.u2` crc32 `05b4f953` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:859` |
| `bios/jojou.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_usa.29f400.u2` crc32 `8d40f7be` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:814` |
| `bios/redearth.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `redearth_euro.29f400.u2` crc32 `02e0f336` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1362` |
| `bios/redearthn.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `redearth_asia_nocd.29f400.u2` crc32 `7a4f0851` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1392` |
| `bios/sfiii.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_euro.29f400.u2` crc32 `27699ddc` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:336` |
| `bios/sfiii2.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_usa.29f400.u2` crc32 `75dd72e0` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:475` |
| `bios/sfiii2h.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_hispanic.29f400.u2` crc32 `5c799526` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:505` |
| `bios/sfiii2j.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_japan.29f400.u2` crc32 `faea0a3e` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:490` |
| `bios/sfiii2n.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_asia_nocd.29f400.u2` crc32 `fd297c0d` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:520` |
| `bios/sfiii3.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_euro.29f400.u2` crc32 `30bbf293` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:579` |
| `bios/sfiii3j.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_japan.29f400.u2` crc32 `63f23d1f` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:609` |
| `bios/sfiii3n.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_japan_nocd.29f400.u2` crc32 `1edc6366` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:624` |
| `bios/sfiii3na.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_asia_nocd.29f400.u2` crc32 `a12ebcd1` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:637` |
| `bios/sfiii3u.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_usa.29f400.u2` crc32 `ecc545c1` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:594` |
| `bios/sfiiia.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_asia_region_bd.29f400.u2` crc32 `cbd28de7` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:366` |
| `bios/sfiiih.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_hispanic.29f400.u2` crc32 `d2b3cd48` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:396` |
| `bios/sfiiij.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_japan.29f400.u2` crc32 `74205250` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:381` |
| `bios/sfiiin.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_asia_nocd.29f400.u2` crc32 `ca2b715f` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:411` |
| `bios/sfiiiu.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_usa_region_b1.29f400.u2` crc32 `fb172a8e` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:351` |
| `bios/warzard.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `warzard_japan.29f400.u2` crc32 `f8e2f0c6` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1377` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/hiscore.dat` | `libretro/fbalpha`, `libretro/fbalpha2012`, `libretro/fbalpha2012_cps3` | no identity: no source states a hash for it |

## crvision

Creativision: 5 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bioscv.rom` | yes | `libretro/jollycv`, RetroBat's list | `3b1ef759d8e3fb4071582efd33dd05f9` asia, europe; retrobios `jollycv`, RetroBat's list | `jollycv`: `libretro/libretro.c:707-713, src/jollycv.c:406-432, src/jcv_crvision.c:316-324, jg.c:154-155` |
| `bios/crvision.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `crvision.u20` crc32 `c3c590c6` 2048 B | `mame`: `src/mame/vtech/crvision.cpp:934-937` |
| `bios/fnvision.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `funboot.rom` crc32 `05602697` 2048 B | `mame`: `src/mame/vtech/crvision.cpp:939-942` |
| `bios/lasr2001.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `laser2001.rom` crc32 `4dc35c39` 16384 B | `mame`: `src/mame/vtech/crvision.cpp:950-953` |
| `bios/manager.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `01` crc32 `702f4cf5` 8192 B, `23` crc32 `46489d88` 8192 B | `mame`: `src/mame/vtech/crvision.cpp:955-959` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/crvision.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `crvision.zip` | `mame64/crvision` | mame64 has no cited firmware directory |
| `fnvision.zip` | `mame64/crvision` | mame64 has no cited firmware directory |
| `lasr2001.zip` | `mame64/crvision` | mame64 has no cited firmware directory |
| `manager.zip` | `mame64/crvision` | mame64 has no cited firmware directory |

## daphne

daphne: 0 slots, 71 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `ace.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `aceeuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astron.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astronp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlandp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlands.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `bega.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `begar1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `blazer.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff_ox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobra.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraab.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraconv.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobram3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cputest.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle11.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle20.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle21.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `esh.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefoxa.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxy.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxyp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gpworld.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gtg.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `interstellar.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_211.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_300.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_314.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_315.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_316_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_318.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_span.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_b.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_c.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_e.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_f.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_ita.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_n1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_x.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `laireuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lgp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `mach3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `roadblaster.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sae.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshort.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshortalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `timetrav.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_swear.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `uvt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |

## dos

dos: 5 slots, 68 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/CM32L_CONTROL.ROM` | no | `libretro/dosbox_pure` | `099552dcbc6a94bb0d2abf1281873fa1` no region; retrobios `dosbox_pure`<br>`bfff32b6144c1d706109accb6e6b1113` no region; retrobios `dosbox_pure` | `dosbox_pure`: `src/gui/mt32emu.h:5122, src/gui/mt32emu.h:5139`; `src/gui/mt32emu.h:5123, src/gui/mt32emu.h:5140` |
| `bios/CM32L_PCM.ROM`<br>also `bios/CM32LN_PCM.ROM` | no | `libretro/dosbox_pure` | `08cdcfa0ed93e9cb16afa76e6ac5f0a4` no region; retrobios `dosbox_pure` | `dosbox_pure`: `src/gui/mt32emu.h:5127, src/gui/mt32emu.h:5144` |
| `bios/CM32LN_CONTROL.ROM` | no | `libretro/dosbox_pure` | `88f459121acf32923b1d85742c67e9c9` no region; retrobios `dosbox_pure` | `dosbox_pure`: `src/gui/mt32emu.h:5124, src/gui/mt32emu.h:5141` |
| `bios/MT32_CONTROL.ROM` | no | `libretro/dosbox_pure` | `5626206284b22c2734f3e9efefcd2675` no region; retrobios `dosbox_pure`<br>`5de47ca37a3712ed32993f865ee9fb5b` no region; retrobios `dosbox_pure`<br>`615a866337ecb752768ac03ed4ddf85c` no region; retrobios `dosbox_pure`<br>`8c4b2c7708be8e88a3ac14777575629c` no region; retrobios `dosbox_pure`<br>`9513fec4f09a7d327748340ce3a2a59b` no region; retrobios `dosbox_pure`<br>`abe0982e4f662f881affa7d8a932c739` no region; retrobios `dosbox_pure`<br>`c5233e4594f9b7bc4f773ae6ba5aa87d` no region; retrobios `dosbox_pure`<br>`daa3ea8e557b85e1ae25a1348db37343` no region; retrobios `dosbox_pure`<br>`ef4978c0a278f284d12b1632aa0b804b` no region; retrobios `dosbox_pure` | `dosbox_pure`: `src/gui/mt32emu.h:5112, src/gui/mt32emu.h:5129`; `src/gui/mt32emu.h:5113, src/gui/mt32emu.h:5130`; `src/gui/mt32emu.h:5114, src/gui/mt32emu.h:5131`; `src/gui/mt32emu.h:5115, src/gui/mt32emu.h:5132`; `src/gui/mt32emu.h:5116, src/gui/mt32emu.h:5133`; `src/gui/mt32emu.h:5118, src/gui/mt32emu.h:5135`; `src/gui/mt32emu.h:5119, src/gui/mt32emu.h:5136`; `src/gui/mt32emu.h:5120, src/gui/mt32emu.h:5137`; `src/gui/mt32emu.h:5121, src/gui/mt32emu.h:5138` |
| `bios/MT32_PCM.ROM` | no | `libretro/dosbox_pure` | `89e42e386e82e0cacb4a2704a03706ca` no region; retrobios `dosbox_pure` | `dosbox_pure`: `src/gui/mt32emu.h:5126, src/gui/mt32emu.h:5143` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/DOSBOX.SF2` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/ROM1.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/ROM2.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/ROM_SM.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/WAVEROM1.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/WAVEROM2.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `bios/SC-55/WAVEROM3.BIN` | `libretro/dosbox_pure` | no identity: no source states a hash for it |
| `CM32L_CONTROL.ROM` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `CM32L_PCM.ROM` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `CM32LN_CONTROL.ROM` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `DOSBOX.SF2` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `IBM PC Cassette BASIC ROM` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `ibm-vga.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_cm32l_1_00.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_cm32l_1_02.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_cm32ln_1_00.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_04.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_04_a.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_04_b.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_05.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_05_a.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_05_b.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_06.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_06_a.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_06_b.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_07.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_07_a.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_1_07_b.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_2_03.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_2_04.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_2_06.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_2_07.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_bluer.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_bluer_a.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/ctrl_mt32_bluer_b.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/pcm_cm32l.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/pcm_cm32l_h.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/pcm_mt32.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/pcm_mt32_h.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `mt32-roms/pcm_mt32_l.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `MT32_CONTROL.ROM` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `MT32_PCM.ROM` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/ROM1.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/ROM2.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/ROM_SM.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/WAVEROM1.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/WAVEROM2.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `SC-55/WAVEROM3.BIN` | `dosbox-pure` | dosbox-pure has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.00/sc55_rom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.00/sc55_rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.10/sc55_rom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.10/sc55_rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.20/sc55_rom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.20/sc55_rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.21/sc55_rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.21/sc55_waverom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.21/sc55_waverom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v1.21/sc55_waverom3.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v2.00/sc55_rom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55-v2.00/sc55_rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55mk2-v1.01/rom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55mk2-v1.01/rom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55mk2-v1.01/rom_sm.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55mk2-v1.01/waverom1.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundcanvas-roms/SC-55mk2-v1.01/waverom2.bin` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundfonts/FluidR3_GM.sf2` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `soundfonts/GeneralUser-GS.sf2` | `dosbox-staging` | dosbox-staging has no cited firmware directory |
| `system.rom` | `dosbox-staging` | dosbox-staging has no cited firmware directory |

## dragon32

Dragon: 3 slots, 17 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dragon/d32.rom` | yes | RetroBat's list | `3420b96031078a4ef408cad7bf21a33f` no region; RetroBat's list |  |
| `bios/dragon/d64_1.rom` | yes | RetroBat's list | `5f0bee59710e55f5880e74890912ed78` no region; RetroBat's list |  |
| `bios/dragon/d64_2.rom` | yes | RetroBat's list | `fd91edce7be5e7c2d88e46b76956a8aa` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `alpha-basic.rom` | `xroar` | xroar has no cited firmware directory |
| `alpha-boot-v0.4.rom` | `xroar` | xroar has no cited firmware directory |
| `alpha-boot-v1.0.rom` | `xroar` | xroar has no cited firmware directory |
| `cdos20.rom` | `xroar` | xroar has no cited firmware directory |
| `d200e_1.rom` | `xroar` | xroar has no cited firmware directory |
| `d200e_2.rom` | `xroar` | xroar has no cited firmware directory |
| `d200e_26.rom` | `xroar` | xroar has no cited firmware directory |
| `d32.rom` | `xroar` | xroar has no cited firmware directory |
| `d64_1.rom` | `xroar` | xroar has no cited firmware directory |
| `d64_2.rom` | `xroar` | xroar has no cited firmware directory |
| `ddos12a.rom` | `xroar` | xroar has no cited firmware directory |
| `delta2.rom` | `xroar` | xroar has no cited firmware directory |
| `dplus49b.rom` | `xroar` | xroar has no cited firmware directory |
| `dragonfly-1.3.rom` | `xroar` | xroar has no cited firmware directory |
| `dragonfly-2.3.rom` | `xroar` | xroar has no cited firmware directory |
| `sdbdos.rom` | `xroar` | xroar has no cited firmware directory |
| `sdose6.rom` | `xroar` | xroar has no cited firmware directory |

## dreamcast

Dreamcast: 3 slots, 9 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dc/dc.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/dc/dc_boot.bin`<br>also `bios/dc/dc_bios.bin`, `bios/dc/dc_bios.bin.bin`, `bios/dc/dc_boot.bin.bin` | yes | `libretro/flycast`, RetroBat's list | `e10c53c2f8b90bab96ead2d368858623` no region; RetroBat's list | `flycast`: `core/hw/holly/sb_mem.cpp:219,55-57` |
| `bios/dc/dc_flash.bin` | yes | RetroBat's list | `0a93f7940c455905bea6e392dfde92a4` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/dc/dc_nvmem.bin` | `libretro/flycast` | no identity: no source states a hash for it |
| `bios/dc/font.bin` | `libretro/flycast` | no identity: no source states a hash for it |
| `dc/dc_boot.bin` | `flycast/dreamcast` | flycast has no cited firmware directory |
| `dc/dc_nvmem.bin` | `flycast/dreamcast` | flycast has no cited firmware directory |
| `dc/font.bin` | `flycast/dreamcast` | flycast has no cited firmware directory |
| `dc_bios.bin` | `redream/redream` | redream has no cited firmware directory |
| `dc_boot.bin` | `demul/dreamcast` | demul has no cited firmware directory |
| `dc_flash.bin` | `demul/dreamcast`, `redream/redream` | demul has no cited firmware directory; redream has no cited firmware directory |

## ecwolf

ECWolf: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/ecwolf.pk3` | yes | RetroBat's list | `dbaa5c4e20a11000d1c4db3a2a28f374` no region; RetroBat's list |  |

## electron

Acorn Electron: 4 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/electron.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/electron64.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/electron_plus1.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/electron_plus3.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/electron_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/electron_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/electron_flop.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## enterprise

enterprise: 20 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/ep128emu/roms/basic20.rom` | no | `libretro/ep128emu_core` | `8e18edce4a7acb2c33cc0ab18f988482` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:249, core/core.cpp:267, core/core.cpp:277`<br>`ep128emu_core`: `core/core.cpp:247, core/core.cpp:265, core/core.cpp:275, roms/roms.hpp:2064-3432` |
| `bios/ep128emu/roms/basic21.rom` | no | `libretro/ep128emu_core` | `e972fe42b398c9ff1d93ff014786aec6` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:251, core/core.cpp:279`<br>`ep128emu_core`: `core/core.cpp:249, core/core.cpp:267, core/core.cpp:277, roms/roms.hpp:3433-4801` |
| `bios/ep128emu/roms/brd.rom` | no | `libretro/ep128emu_core` | `6af0402906944fd134004b85097c8524` germany; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:263-264`<br>`ep128emu_core`: `core/core.cpp:261, roms/roms.hpp:42409-43777` |
| `bios/ep128emu/roms/cpc464.rom` | no | `libretro/ep128emu_core` | `a993f85b88ac4350cf4d41554e87fe4f` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:353-356`<br>`ep128emu_core`: `core/core.cpp:353-356, roms/roms.hpp:4802-7535` |
| `bios/ep128emu/roms/cpc6128.rom` | no | `libretro/ep128emu_core` | `b96280dc6c95a48857b4b8eb931533ae` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:370-373`<br>`ep128emu_core`: `core/core.cpp:370-373, roms/roms.hpp:10270-13003` |
| `bios/ep128emu/roms/cpc664.rom` | no | `libretro/ep128emu_core` | `5a384a2310f472c7857888371c00ed66` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:361-364`<br>`ep128emu_core`: `core/core.cpp:361-364, roms/roms.hpp:7536-10269` |
| `bios/ep128emu/roms/cpc_amsdos.rom`<br>also `bios/ep128emu/roms/AMSDOS_0.5.ROM` | no | `libretro/ep128emu_core` | `25629dfe870d097469c217b95fdc1c95` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:377`<br>`ep128emu_core`: `core/core.cpp:377-378, roms/roms.hpp:24630-25998` |
| `bios/ep128emu/roms/epdos16f.rom` | no | `libretro/ep128emu_core` | `6593dff00ab32a4b1fc084674ededf2b` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:252-256, roms/roms.hpp:43780 (built-in data, MD5 verified)`<br>`ep128emu_core`: `core/core.cpp:253-256, roms/roms.hpp:43779-46512` |
| `bios/ep128emu/roms/epfileio.rom` | no | `libretro/ep128emu_core` | `a68ebcbc73a4d2178d755b7755bf18fe` no region; retrobios `ep128emu_core` | `ep128emu`: `core/core.cpp:286-287`<br>`ep128emu_core`: `core/core.cpp:284-285, roms/roms.hpp:9-1377` |
| `bios/ep128emu/roms/exdos13.rom`<br>also `bios/ep128emu/roms/EXDOS14.ROM` | no | `libretro/ep128emu_core` | `ddff70c014d1958dc75378b6c9aab6f8` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:306-323`<br>`ep128emu_core`: `core/core.cpp:304-307, roms/roms.hpp:13004-15737` |
| `bios/ep128emu/roms/exdos14isdos10uk.rom` | no | `libretro/ep128emu_core` | `f91c4a507cc6895bdd9c43df4f021df3` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:294-298, roms/roms.hpp:30104 (built-in data, MD5 verified)`<br>`ep128emu_core`: `core/core.cpp:296-299, roms/roms.hpp:30103-32836` |
| `bios/ep128emu/roms/exos20.rom`<br>also `bios/ep128emu/roms/EXOS20.ROM` | no | `libretro/ep128emu_core` | `5ad3baaad3b5156d6b60b34229a676fb` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:229-232`<br>`ep128emu_core`: `core/core.cpp:227-230, roms/roms.hpp:15738-18471` |
| `bios/ep128emu/roms/exos21.rom`<br>also `bios/ep128emu/roms/EXOS21.ROM` | no | `libretro/ep128emu_core` | `f36f24cbb87745fbd2714e4df881db09` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:237-240`<br>`ep128emu_core`: `core/core.cpp:235-238, roms/roms.hpp:18472-21205` |
| `bios/ep128emu/roms/exos24uk.rom` | no | `libretro/ep128emu_core` | `55af78f877a21ca45eb2df68a74fcc60` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:217-224`<br>`ep128emu_core`: `core/core.cpp:215-222, roms/roms.hpp:32838-38302` |
| `bios/ep128emu/roms/hun.rom` | no | `libretro/ep128emu_core` | `22167938f142c222f40992839aa21a06` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:245-246`<br>`ep128emu_core`: `core/core.cpp:243, roms/roms.hpp:41039-42407` |
| `bios/ep128emu/roms/tvc22_ext.rom`<br>also `bios/ep128emu/roms/TVC22_D7.64K` | no | `libretro/ep128emu_core` | `5ce95a26ceed5bec73995d83568da9cf` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:330`<br>`ep128emu_core`: `core/core.cpp:330-331, roms/roms.hpp:22575-23260` |
| `bios/ep128emu/roms/tvc22_sys.rom`<br>also `bios/ep128emu/roms/TVC22_D6_D4.64K` | no | `libretro/ep128emu_core` | `8c54285f541930cde766069942bad0f2` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:328-329`<br>`ep128emu_core`: `core/core.cpp:328-329, roms/roms.hpp:21206-22574` |
| `bios/ep128emu/roms/tvc_dos12d.rom`<br>also `bios/ep128emu/roms/VT-DOS12-DISK.ROM` | no | `libretro/ep128emu_core` | `88dc7876d584f90e4106f91444ab23b7` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:339`<br>`ep128emu_core`: `core/core.cpp:339-340, roms/roms.hpp:23261-24629` |
| `bios/ep128emu/roms/tvcfileio.rom` | no | `libretro/ep128emu_core` | `a2cf86ba8e7fc58b242137fe59036832` no region; RomM's known_bios_files | `ep128emu`: `core/core.cpp:334`<br>`ep128emu_core`: `core/core.cpp:334-335, roms/roms.hpp:1378-2063` |
| `bios/ep128emu/roms/zt19uk.rom` | no | `libretro/ep128emu_core` | `228540b6be83ae2acd7569c8ff0f91d0` no region; retrobios `ep128emu` | `ep128emu`: `core/core.cpp:290-293`<br>`ep128emu_core`: `core/core.cpp:288-291, roms/roms.hpp:38304-41037` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/ep128emu/roms/zx128.rom` | `libretro/ep128emu_core` | no identity: no source states a hash for it |
| `bios/ep128emu/roms/zx48.rom` | `libretro/ep128emu_core` | no identity: no source states a hash for it |

## fbneo

fbneo: 44 slots, 47 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/astrocde.zip` | yes | `libretro/fbneo` | `f58a3823ab308ef0a02b498d126e5d96` no region; RetroBat's list<br>zip, for `libretro/fbneo`, `libretro/mame`: `astro.bin` crc32 `ebc77f3a` 8192 B | `fbneo`: `src/burn/drv/pre90s/d_astrocde.cpp:3034` |
| `bios/atarisy1.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`, `libretro/mame`: `136032.101.e3` crc32 `7e84972a` 256 B, `136032.101.e3` crc32 `7e84972a` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.105.l13` crc32 `79021d3c` 16384 B, `136032.106.l12` crc32 `76ee86c4` 16384 B, `136032.107.b2` crc32 `7a29dc07` 8192 B, `136032.114.j11` crc32 `195c54ad` 16384 B, `136032.115.j10` crc32 `7275b4dc` 16384 B, `136032.205.l13` crc32 `88d0be26` 16384 B, `136032.206.l12` crc32 `3c79ef05` 16384 B | `fbneo`: `src/burn/drv/atari/d_atarisy1.cpp:1196`; `src/burn/drv/atari/d_atarisy1.cpp:1197`; `src/burn/drv/atari/d_atarisy1.cpp:1198`; `src/burn/drv/atari/d_atarisy1.cpp:1199`; `src/burn/drv/atari/d_atarisy1.cpp:1200`; `src/burn/drv/atari/d_atarisy1.cpp:1201`; `src/burn/drv/atari/d_atarisy1.cpp:1205`; `src/burn/drv/atari/d_atarisy1.cpp:1206`; `src/burn/drv/atari/d_atarisy1.cpp:1207`; `src/burn/drv/atari/d_atarisy1.cpp:1208`; `src/burn/drv/atari/d_atarisy1.cpp:1209`; `src/burn/drv/atari/d_atarisy1.cpp:1210`; `src/burn/drv/atari/d_atarisy1.cpp:1214`; `src/burn/drv/atari/d_atarisy1.cpp:1215`; `src/burn/drv/atari/d_atarisy1.cpp:1216` |
| `bios/bubsys.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`, `libretro/mame`: `400a1.2b` crc32 `5827b1e8` 256 B, `400a2.1b` crc32 `2f44f970` 256 B, `400b03.8g` crc32 `85c2afc5` 8192 B, `boot.bin` crc32 `f0774fc2` 480 B | `fbneo`: `src/burn/drv/konami/d_nemesis.cpp:4526`; `src/burn/drv/konami/d_nemesis.cpp:4528`; `src/burn/drv/konami/d_nemesis.cpp:4530`; `src/burn/drv/konami/d_nemesis.cpp:4531` |
| `bios/cchip.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `cchip_upd78c11.bin` crc32 `43021521` 4096 B | `fbneo`: `src/burn/drv/taito/d_taitof2.cpp:4392` |
| `bios/decocass.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B<br>zip, for `libretro/mame`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `dsp-3_p0-a.m9` crc32 `2541e34b` 2048 B, `dsp-3_p0-b.m9` crc32 `b67a91d9` 2048 B, `dsp-3_p0-c.m9` crc32 `c76c4057` 2048 B, `dsp-3_p0-d.m9` crc32 `4b7d72bc` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v0b-.7e` crc32 `23d929b7` 4096 B, `v0c-.7e` crc32 `9f505709` 4096 B, `v0d-.7e` crc32 `1e0c22b1` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B<br>zip, for `libretro/mame2000`: `rms8.cpu` crc32 `23d929b7` 4096 B, `rms8.snd` crc32 `b66b2c2a` 2048 B | `fbneo`: `src/burn/drv/dataeast/d_decocass.cpp:3596`; `src/burn/drv/dataeast/d_decocass.cpp:3599`; `src/burn/drv/dataeast/d_decocass.cpp:3601`; `src/burn/drv/dataeast/d_decocass.cpp:3602`; `src/burn/drv/dataeast/d_decocass.cpp:3603`; `src/burn/drv/dataeast/d_decocass.cpp:3608`; `src/burn/drv/dataeast/d_decocass.cpp:3609`; `src/burn/drv/dataeast/d_decocass.cpp:3611`; `src/burn/drv/dataeast/d_decocass.cpp:3613`; `src/burn/drv/dataeast/d_decocass.cpp:3614`; `src/burn/drv/dataeast/d_decocass.cpp:3615`; `src/burn/drv/dataeast/d_decocass.cpp:3620`; `src/burn/drv/dataeast/d_decocass.cpp:3632`; `src/burn/drv/dataeast/d_decocass.cpp:3644`; `src/burn/drv/dataeast/d_decocass.cpp:3656`; `src/burn/drv/dataeast/d_decocass.cpp:3668`; `src/burn/drv/dataeast/d_decocass.cpp:3680`; `src/burn/drv/dataeast/d_decocass.cpp:3692` |
| `bios/gba.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `gba_bios.bin` crc32 `81977335` 16384 B | `fbneo`: `src/burn/drv/gba/d_gba.cpp:540` |
| `bios/isgsm.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`, `libretro/mame`: `ism2006v00.u1` crc32 `2292585c` 131072 B | `fbneo`: `src/burn/drv/sega/d_sys16b.cpp:11270` |
| `bios/jojo.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_euro.29f400.u2` crc32 `513e40ec` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:799` |
| `bios/jojoa.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_asia.29f400.u2` crc32 `789aa72a` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:829` |
| `bios/jojoba.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_euro_nocd.29f400.u2` crc32 `1ee2d679` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1120` |
| `bios/jojobajr1.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_japan.29f400.u2` crc32 `3085478c` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1199` |
| `bios/jojoban.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_japan_nocd.29f400.u2` crc32 `4dab19f5` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1133` |
| `bios/jojobar1.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojoba_euro.29f400.u2` crc32 `63cc8800` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1184` |
| `bios/jojoj.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_japan.29f400.u2` crc32 `02778f60` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:844` |
| `bios/jojon.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_asia_nocd.29f400.u2` crc32 `05b4f953` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:859` |
| `bios/jojou.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `jojo_usa.29f400.u2` crc32 `8d40f7be` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:814` |
| `bios/midssio.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `82s123.12d` crc32 `e1281ee9` 32 B | `fbneo`: `src/burn/drv/pre90s/d_mcr.cpp:1630` |
| `bios/namcoc69.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `c69.bin` crc32 `349134d9` 16384 B | `fbneo`: `src/burn/drv/pst90s/d_namcona1.cpp:1346` |
| `bios/namcoc70.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `c70.bin` crc32 `b4015f23` 16384 B | `fbneo`: `src/burn/drv/pst90s/d_namcona1.cpp:1367` |
| `bios/namcoc75.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `c75.bin` crc32 `42f539a5` 16384 B | `fbneo`: `src/burn/drv/pst90s/d_namconb1.cpp:1978` |
| `bios/neogeo.zip` | yes | `libretro/fbneo` | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `fbneo`: `src/burn/drv/neogeo/d_neogeo.cpp:1624`; `src/burn/drv/neogeo/d_neogeo.cpp:1625`; `src/burn/drv/neogeo/d_neogeo.cpp:1626`; `src/burn/drv/neogeo/d_neogeo.cpp:1627`; `src/burn/drv/neogeo/d_neogeo.cpp:1628`; `src/burn/drv/neogeo/d_neogeo.cpp:1629`; `src/burn/drv/neogeo/d_neogeo.cpp:1630`; `src/burn/drv/neogeo/d_neogeo.cpp:1631`; `src/burn/drv/neogeo/d_neogeo.cpp:1632`; `src/burn/drv/neogeo/d_neogeo.cpp:1633`; `src/burn/drv/neogeo/d_neogeo.cpp:1634`; `src/burn/drv/neogeo/d_neogeo.cpp:1635`; `src/burn/drv/neogeo/d_neogeo.cpp:1636`; `src/burn/drv/neogeo/d_neogeo.cpp:1637`; `src/burn/drv/neogeo/d_neogeo.cpp:1638`; `src/burn/drv/neogeo/d_neogeo.cpp:1640`; `src/burn/drv/neogeo/d_neogeo.cpp:1641`; `src/burn/drv/neogeo/d_neogeo.cpp:1642`; `src/burn/drv/neogeo/d_neogeo.cpp:1648`; `src/burn/drv/neogeo/d_neogeo.cpp:1649`; `src/burn/drv/neogeo/d_neogeo.cpp:1650`; `src/burn/drv/neogeo/d_neogeo.cpp:1651`; `src/burn/drv/neogeo/d_neogeo.cpp:1652`; `src/burn/drv/neogeo/d_neogeo.cpp:1653`; `src/burn/drv/neogeo/d_neogeo.cpp:1654`; `src/burn/drv/neogeo/d_neogeo.cpp:1655`; `src/burn/drv/neogeo/d_neogeo.cpp:1656`; `src/burn/drv/neogeo/d_neogeo.cpp:1657`; `src/burn/drv/neogeo/d_neogeo.cpp:1658`; `src/burn/drv/neogeo/d_neogeo.cpp:1659`; `src/burn/drv/neogeo/d_neogeo.cpp:1660`; `src/burn/drv/neogeo/d_neogeo.cpp:1661`; `src/burn/drv/neogeo/d_neogeo.cpp:1662`; `src/burn/drv/neogeo/d_neogeo.cpp:1663`; `src/burn/drv/neogeo/d_neogeo.cpp:1665`; `src/burn/drv/neogeo/d_neogeo.cpp:1672`; `src/burn/drv/neogeo/d_neogeo.cpp:1673`; `src/burn/drv/neogeo/d_neogeo.cpp:1674`; `src/burn/drv/neogeo/d_neogeo.cpp:1764`; `src/burn/drv/neogeo/d_neogeo.cpp:1765`; `src/burn/drv/neogeo/d_neogeo.cpp:1811`; `src/burn/drv/neogeo/d_neogeo.cpp:1899`<br>`geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267` |
| `bios/nmk004.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `nmk004.bin` crc32 `8ae61a09` 8192 B | `fbneo`: `src/burn/drv/pst90s/d_nmk16.cpp:6123` |
| `bios/pgm.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B<br>zip, for `libretro/mame`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p01s.u20` crc32 `e42b166e` 131072 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B | `fbneo`: `src/burn/drv/pgm/d_pgm.cpp:880`; `src/burn/drv/pgm/d_pgm.cpp:881`; `src/burn/drv/pgm/d_pgm.cpp:883`; `src/burn/drv/pgm/d_pgm.cpp:884`; `src/burn/drv/pgm/d_pgm.cpp:886`; `src/burn/drv/pgm/d_pgm.cpp:887`; `src/burn/drv/pgm/d_pgm.cpp:928` |
| `bios/redearth.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `redearth_euro.29f400.u2` crc32 `02e0f336` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1362` |
| `bios/redearthn.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `redearth_asia_nocd.29f400.u2` crc32 `7a4f0851` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1392` |
| `bios/sfiii.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_euro.29f400.u2` crc32 `27699ddc` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:336` |
| `bios/sfiii2.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_usa.29f400.u2` crc32 `75dd72e0` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:475` |
| `bios/sfiii2h.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_hispanic.29f400.u2` crc32 `5c799526` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:505` |
| `bios/sfiii2j.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_japan.29f400.u2` crc32 `faea0a3e` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:490` |
| `bios/sfiii2n.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii2_asia_nocd.29f400.u2` crc32 `fd297c0d` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:520` |
| `bios/sfiii3.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_euro.29f400.u2` crc32 `30bbf293` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:579` |
| `bios/sfiii3j.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_japan.29f400.u2` crc32 `63f23d1f` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:609` |
| `bios/sfiii3n.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_japan_nocd.29f400.u2` crc32 `1edc6366` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:624` |
| `bios/sfiii3na.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_asia_nocd.29f400.u2` crc32 `a12ebcd1` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:637` |
| `bios/sfiii3u.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii3_usa.29f400.u2` crc32 `ecc545c1` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:594` |
| `bios/sfiiia.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_asia_region_bd.29f400.u2` crc32 `cbd28de7` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:366` |
| `bios/sfiiih.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_hispanic.29f400.u2` crc32 `d2b3cd48` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:396` |
| `bios/sfiiij.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_japan.29f400.u2` crc32 `74205250` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:381` |
| `bios/sfiiin.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_asia_nocd.29f400.u2` crc32 `ca2b715f` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:411` |
| `bios/sfiiiu.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sfiii_usa_region_b1.29f400.u2` crc32 `fb172a8e` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:351` |
| `bios/skns.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `sknsj1.u10` crc32 `7e2b836c` 524288 B<br>zip, for `libretro/mame`: `sknsa1.u10` crc32 `745e5212` 524288 B, `sknse2.u10` crc32 `e2b9d7d1` 524288 B, `sknsj1.u10` crc32 `7e2b836c` 524288 B, `sknsk1.u10` crc32 `ff1c9f79` 524288 B, `sknsu1.u10` crc32 `384d21ec` 524288 B, `supernova-modbios-korea.u10` crc32 `1d90517c` 524288 B, `supernova_modbios-japan.u10` crc32 `b8d3190c` 524288 B | `fbneo`: `src/burn/drv/pst90s/d_suprnova.cpp:1846`; `src/burn/drv/pst90s/d_suprnova.cpp:1847`; `src/burn/drv/pst90s/d_suprnova.cpp:1848`; `src/burn/drv/pst90s/d_suprnova.cpp:1849`; `src/burn/drv/pst90s/d_suprnova.cpp:1850` |
| `bios/st018.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `st018.bin` crc32 `5c403cc5` 163840 B | `fbneo`: `src/burn/drv/snes/d_snes.cpp:612` |
| `bios/warzard.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `warzard_japan.29f400.u2` crc32 `f8e2f0c6` 524288 B | `fbneo`: `src/burn/drv/cps3/d_cps3.cpp:1377` |
| `bios/ym2608.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`, `libretro/mame`: `ym2608_adpcm_rom.bin` crc32 `23c9e0d8` 8192 B | `fbneo`: `src/burn/drv/pst90s/d_pipedrm.cpp:876` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `astrocde.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `atarisy1.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `bios/fbneo/hiscore.dat` | `libretro/fbneo` | no identity: no source states a hash for it |
| `bios/hiscore.dat` | `libretro/fbalpha`, `libretro/fbalpha2012` | no identity: no source states a hash for it |
| `bubsys.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `cchip.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `decocass.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `fbneo/hiscore.dat` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `gba.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `isgsm.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojo.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojoa.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojoba.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojobajr1.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojoban.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojobar1.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojoj.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojon.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `jojou.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `midssio.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `namcoc69.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `namcoc70.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `namcoc75.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `neogeo.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `nmk004.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `pgm.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `redearth.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `redearthn.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii2.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii2h.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii2j.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii2n.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii3.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii3j.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii3n.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii3na.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiii3u.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiiia.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiiih.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiiij.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiiin.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `sfiiiu.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `skns.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `st018.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `warzard.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |
| `ym2608.zip` | `fbneo/arcade` | fbneo has no cited firmware directory |

## fds

Nintendo Family Computer Disk System: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/disksys.rom`<br>also `bios/FdsBios.bin` | yes | `libretro/fceumm`, `libretro/mesen`, `libretro/nestopia`, RetroBat's list | `ca30b50f880eb660a320674ed365ef7a` japan; retrobios `nestopia`, RetroBat's list | `fceumm`: `src/fds.c:680-715 (FDSLoad), src/general.c:65-67 (FCEUMKF_FDSROM)`<br>`mesen`: `Core/FdsLoader.cpp:45-59 (LoadBios tries FdsBios.bin then disksys.rom), Core/FdsLoader.cpp:143-146 (LoadRom validates size == 0x2000)`<br>`nestopia`: `libretro/libretro.cpp:2169-2197 (FDS load), source/core/NstFds.cpp:57-127 (Bios class, CRC32 validation)` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `disksys.rom` | `ares/FamicomDiskSystem` | ares has no cited firmware directory |
| `emulators/mesen/Firmware/disksys.rom` | `mesen` | outside bios/, where RomMBat never writes |

## flash

flash: 0 slots, 8 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `video/libopenh264-2.4.1-linux-arm.7.so` | `ruffle` | ruffle has no cited firmware directory |
| `video/libopenh264-2.4.1-linux-arm64.7.so` | `ruffle` | ruffle has no cited firmware directory |
| `video/libopenh264-2.4.1-linux32.7.so` | `ruffle` | ruffle has no cited firmware directory |
| `video/libopenh264-2.4.1-linux64.7.so` | `ruffle` | ruffle has no cited firmware directory |
| `video/libopenh264-2.4.1-mac-arm64.dylib` | `ruffle` | ruffle has no cited firmware directory |
| `video/libopenh264-2.4.1-mac-x64.dylib` | `ruffle` | ruffle has no cited firmware directory |
| `video/openh264-2.4.1-win32.dll` | `ruffle` | ruffle has no cited firmware directory |
| `video/openh264-2.4.1-win64.dll` | `ruffle` | ruffle has no cited firmware directory |

## fm7

FM-7: 3 slots, 6 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/fm7.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `boot_bas.rom` crc32 `c70f0c74` 512 B, `boot_dos_a.rom` crc32 `bf441864` 512 B, `fbasic300.rom` crc32 `87c98494` 31744 B, `kanji.rom` crc32 `62402ac9` 131072 B, `subsys_c.rom` crc32 `24cec93f` 10240 B | `mame`: `src/mame/fujitsu/fm7.cpp:2184-2205` |
| `bios/fm77av.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `fbasic30.rom` crc32 `a96d19b6` 31744 B, `initiate.rom` crc32 `785cb06c` 8192 B, `subsys_a.rom` crc32 `e8014fbb` 8192 B, `subsys_b.rom` crc32 `9be69fac` 8192 B, `subsyscg.rom` crc32 `e9f16c42` 8192 B | `mame`: `src/mame/fujitsu/fm7.cpp:2207-2228` |
| `bios/fmnew7.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `boot_dos.rom` crc32 `198614ff` 512 B, `fbasic302.rom` crc32 `a96d19b6` 31744 B | `mame`: `src/mame/fujitsu/fm7.cpp:2168-2183` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/fm77av.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/fm7_cass.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/fm7_disk.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `fm7.zip` | `mame64/fm7` | mame64 has no cited firmware directory |
| `fm77av.zip` | `mame64/fm7` | mame64 has no cited firmware directory |
| `fmnew7.zip` | `mame64/fm7` | mame64 has no cited firmware directory |

## fmtowns

FMTowns: 7 slots, 15 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/fmtowns.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/fmtowns/FMT_DIC.ROM` | yes | RetroBat's list | `8fa4e553f28cfc0c30a0a1e589799942` no region; RetroBat's list |  |
| `bios/fmtowns/FMT_DOS.ROM` | yes | RetroBat's list | `0585b19930d4a7f4c71bcc8a33746588` no region; RetroBat's list |  |
| `bios/fmtowns/FMT_F20.ROM` | yes | RetroBat's list | `ac0c7021e9bf48ca84b51ab651169a88` no region; RetroBat's list |  |
| `bios/fmtowns/FMT_FNT.ROM` | yes | RetroBat's list | `b91300e55b70227ce98b59c5f02fa8dd` no region; RetroBat's list |  |
| `bios/fmtowns/FMT_SYS.ROM` | yes | RetroBat's list | `86fb6f7280689259f0ca839dd3dd6cde` no region; RetroBat's list |  |
| `bios/fmtownsux.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/fmtowns_cd.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/fmtowns_flop_cracked.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/fmtowns_flop_misc.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/fmtowns_flop_orig.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `FMT_ALL.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `FMT_DIC.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `FMT_DOS.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `FMT_F20.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `FMT_FNT.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `FMT_SYS.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `MAR_EX0.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `MAR_EX1.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `MAR_EX2.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `MAR_EX3.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |
| `MYTOWNS.ROM` | `tsugaru/tsugaru` | tsugaru has no cited firmware directory |

## fpinball

fpinball: 0 slots, 2 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `Help/Script56.chm` | `fpinball/bam`, `fpinball/fpinball` | fpinball has no cited firmware directory |
| `NPClient.dll` | `fpinball/bam`, `fpinball/fpinball` | fpinball has no cited firmware directory |

## gamate

Gamate: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gamate.zip` | yes | `libretro/mame`, RetroBat's list | `320bf3d4142fa1043255db7ff87d17c7` no region; RetroBat's list<br>zip, for `libretro/mame`: `gamate_bios_bit.bin` crc32 `03a5f3a7` 4096 B, `gamate_bios_umc.bin` crc32 `07090415` 4096 B | `mame`: `src/mame/bitcorp/gamate.cpp:228-234` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/gamate.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `gamate.zip` | `mame64/gamate` | mame64 has no cited firmware directory |

## gamecom

Game.com: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gamecom.zip` | yes | `libretro/mame`, RetroBat's list | `f2087ed09f7fa8d8856cfafb20dd00dc` no region; RetroBat's list<br>zip, for `libretro/mame`: `external.bin` crc32 `e235a589` 262144 B, `internal.bin` crc32 `a0cec361` 4096 B | `mame`: `src/mame/tiger/gamecom.cpp:293-299` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/gamecom.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `gamecom.zip` | `mame64/gamecom` | mame64 has no cited firmware directory |

## gamecube

gamecube: 1 slots, 49 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/GBA/gba_bios.bin` | no | `libretro/dolphin` | `a860e8c0b6d573d191e4ec7db1b1e4f6` world; RomM's known_bios_files | `dolphin`: `Source/Core/Common/CommonPaths.h:145, Source/Core/Core/HW/GBACore.cpp:346-363` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/codehandler.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_coef.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_rom.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/EUR/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_japanese.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_western.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/JAP/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/USA/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/keys.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Load/WiiSD.raw` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/nand.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientcakey.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/rootca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/shared2/sys/SYSCONF` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/title/00000001/00000002/data/setting.txt` | `libretro/dolphin` | no identity: no source states a hash for it |
| `codehandler.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GBA/gba_bios.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/dsp_coef.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/dsp_rom.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/EUR/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/font_japanese.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/font_western.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/JAP/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/USA/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `keys.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723d_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821c_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8851bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852btu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8922au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/WiiSD.raw` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `nand.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii System Menu` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/clientca.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/clientcakey.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/rootca.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/shared2/sys/SYSCONF` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/title/00000001/00000002/data/setting.txt` | `dolphin/dolphin` | dolphin has no cited firmware directory |

## gamegear

gamegear: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bios.gg` | yes | `bizhawk/SMSHawk`, `libretro/gearsystem`, `libretro/genesis_plus_gx` | `672e104c3be3a238301aceffc3b23fd6` north-america; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `gearsystem`: `platforms/libretro/libretro.cpp:737, platforms/libretro/libretro.cpp:731-743 (load_bootroms)`<br>`genesis_plus_gx`: `core/loadrom.c:480` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios.gg` | `ares/GameGear`, `jgenesis` | ares has no cited firmware directory; jgenesis has no cited firmware directory |

## gamepock

Game Pocket: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gamepock.zip` | yes | `libretro/mame`, RetroBat's list | `887479fe3c69a0b935c57a7a738e26c4` no region; RetroBat's list<br>zip, for `libretro/mame`: `egpcboot.bin` crc32 `ee1ea65d` 4096 B | `mame`: `src/mame/epoch/gamepock.cpp:248-251` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/gamepock.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `gamepock.zip` | `mame64/gamepock` | mame64 has no cited firmware directory |

## gb

Game Boy: 24 slots, 12 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/BS-X.bin` | no | `libretro/mesen-s` | `fed4d8242cfbed61343d53d48432aced` japan; RetroBat's list, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:39-47,96-113, Core/BaseCartridge.cpp:65` |
| `bios/cgb_boot.bin`<br>also `bios/gbc_bios.bin` | no | `libretro/mesen-s`, `libretro/sameboy` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:149, Core/Gameboy.cpp:108-119, Core/GbBootRom.h:83`<br>`sameboy`: `libretro/libretro.c:524, libretro/libretro.c:535, libretro/libretro.c:556, Core/gb.c:1613-1618` |
| `bios/dmg_boot.bin`<br>also `bios/gb_bios.bin` | no | `libretro/mesen-s`, `libretro/sameboy` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:148, Core/Gameboy.cpp:108-114, Core/GbBootRom.h:7`<br>`sameboy`: `libretro/libretro.c:519, libretro/libretro.c:530, libretro/libretro.c:556 (boot_rom_load), Core/gb.c:1594 (request_boot_rom)` |
| `bios/dsp1.data.rom` | no | `libretro/mesen-s` | `3d81b45fa0c2aa8b852dfb1ece7c0971` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1.program.rom` | no | `libretro/mesen-s` | `ae209fbe789fbf11a48aea5ab1197321` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1b.data.rom` | no | `libretro/mesen-s` | `1e3f568634a7d8284020dddc0ae905bc` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp1b.program.rom` | no | `libretro/mesen-s` | `d10f446888e097cbf500f3f663cf4f6d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp2.data.rom` | no | `libretro/mesen-s` | `e9417e29223b139c3c4b635a2a3b8744` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp2.program.rom` | no | `libretro/mesen-s` | `aa6e5922a3ed5ded54f24247c11143c5` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp3.data.rom` | no | `libretro/mesen-s` | `0a81210c0a940b997dd9843281008ee6` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp3.program.rom` | no | `libretro/mesen-s` | `d99ca4562818d49cee1f242705bba6f8` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp4.data.rom` | no | `libretro/mesen-s` | `ee4990879eb68e3cbca239c5bc20303d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/dsp4.program.rom` | no | `libretro/mesen-s` | `a151023b948b90ffc23a5b594bb6fef2` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/gb_bios.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy`, `libretro/gambatte`, RetroBat's list | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `gambatte`: `libgambatte/libretro/libretro.cpp:1303-1363 (get_bootloader_from_file), libgambatte/src/bootloader.cpp:20-55 (load)` |
| `bios/GBC_agb.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy` | `e6cefb5f7d352fab6681989763917c73` world; BizHawk's FirmwareDatabase.cs |  |
| `bios/gbc_bios.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy`, `libretro/gambatte`, RetroBat's list | `dbfce9db9deaa2567f6a84fde55f9680` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `gambatte`: `libgambatte/libretro/libretro.cpp:1303-1363 (get_bootloader_from_file), libgambatte/src/bootloader.cpp:20-55 (load)`<br>Measured on our tree: gb under bizhawk/GBHawk on 8.2.1 runs a cartridge whose header flags Game Boy Color support in Color mode, and refused Pokemon Yellow with Couldn't find required firmware GBC+World until gbc_bios.bin was present. It takes gb_bios.bin for a mono cartridge. RetroBat offers no option for GBHawk's console mode |
| `bios/SGB1.sfc` | yes | `libretro/bsnes`, `libretro/mesen-s`, RetroBat's list | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638`<br>Measured on our tree: RetroBat's Game Boy wiki page lists these beside gb_bios.bin. gb under libretro/bsnes on 8.2.1 runs a .gb as a Super Game Boy cartridge: without SGB1.sfc RetroArch logged Failed to load content, and with it Pokemon Yellow booted in its SGB border |
| `bios/SGB2.sfc` | yes | `libretro/bsnes`, `libretro/mesen-s`, RetroBat's list | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638`<br>Measured on our tree: RetroBat's Game Boy wiki page lists these beside gb_bios.bin. gb under libretro/bsnes on 8.2.1 runs a .gb as a Super Game Boy cartridge: without SGB1.sfc RetroArch logged Failed to load content, and with it Pokemon Yellow booted in its SGB border |
| `bios/sgb2_boot.bin`<br>also `bios/sgb_bios.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy`, `libretro/mesen-s`, `libretro/sameboy`, RetroBat's list | `e0430bca9925fb9882148fd2dc2418c1` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `mesen-s`: `Core/FirmwareHelper.h:151, Core/Gameboy.cpp:108-127, Core/GbBootRom.h:57`<br>`sameboy`: `libretro/libretro.c:522, libretro/libretro.c:533, libretro/libretro.c:556, Core/gb.c:1607`<br>Measured on our tree: RetroBat's Game Boy wiki page lists these beside gb_bios.bin. gb under libretro/bsnes on 8.2.1 runs a .gb as a Super Game Boy cartridge: without SGB1.sfc RetroArch logged Failed to load content, and with it Pokemon Yellow booted in its SGB border |
| `bios/sgb_boot.bin`<br>also `bios/sgb_bios.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy`, `libretro/mesen-s`, `libretro/sameboy`, RetroBat's list | `d574d4f9c12f305074798f54c091a8b4` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `mesen-s`: `Core/FirmwareHelper.h:150, Core/Gameboy.cpp:108-127, Core/GbBootRom.h:32`<br>`sameboy`: `libretro/libretro.c:521, libretro/libretro.c:532, libretro/libretro.c:556, Core/gb.c:1600-1604`<br>Measured on our tree: RetroBat's Game Boy wiki page lists these beside gb_bios.bin. gb under libretro/bsnes on 8.2.1 runs a .gb as a Super Game Boy cartridge: without SGB1.sfc RetroArch logged Failed to load content, and with it Pokemon Yellow booted in its SGB border |
| `bios/st010.data.rom` | no | `libretro/mesen-s` | `254d70762b6f59f99c27c395aba7d07d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st010.program.rom` | no | `libretro/mesen-s` | `1d70019179a59a566a0bb5d3f2845544` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st011.data.rom` | no | `libretro/mesen-s` | `10bd3f4aa949737ab9836512c35bcc29` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st011.program.rom` | no | `libretro/mesen-s` | `95222ebf1c0c2990bcf25db43743f032` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/agb_boot.bin` | `libretro/sameboy` | no identity: no source states a hash for it |
| `cgb_boot.bin` | `jgenesis` | jgenesis has no cited firmware directory |
| `dmg_boot.bin` | `jgenesis` | jgenesis has no cited firmware directory |
| `emulators/mesen/Firmware/cgb_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dmg_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/SGB1.sfc` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/SGB2.sfc` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/sgb2_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/sgb_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `gb_bios.bin` | `mgba/mgba` | mgba has no cited firmware directory |
| `gbc_bios.bin` | `mgba/mgba` | mgba has no cited firmware directory |
| `sgb_bios.bin` | `mgba/mgba` | mgba has no cited firmware directory |

## gb2players

gb2players: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dmg_boot.bin` | no | `libretro/sameboy` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `sameboy`: `libretro/libretro.c:519, libretro/libretro.c:530, libretro/libretro.c:556 (boot_rom_load), Core/gb.c:1594 (request_boot_rom)` |

## gba

Game Boy Advance: 1 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gba_bios.bin` | yes | `bizhawk/mGBA`, `libretro/gpsp`, `libretro/mednafen_gba`, `libretro/mgba`, RetroBat's list | `a860e8c0b6d573d191e4ec7db1b1e4f6` world; BizHawk's FirmwareDatabase.cs, retrobios `beetle_gba`, RetroBat's list | `gpsp`: `libretro/libretro.c:1254, gba_memory.c:3270-3281`<br>`mednafen_gba`: `mednafen/gba/GBA.cpp:2647-2672`<br>`mgba`: `src/platform/libretro/libretro.c:2068` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `emulators/mesen/Firmware/gba_bios.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `gba_bios.bin` | `ares/GameBoyAdvance`, `jgenesis`, `mednafen/gba`, `mgba/mgba` | ares has no cited firmware directory; jgenesis has no cited firmware directory; mednafen has no cited firmware directory; mgba has no cited firmware directory |

## gbc

Game Boy Color: 3 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/cgb_boot.bin` | no | `libretro/sameboy` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `sameboy`: `libretro/libretro.c:524, libretro/libretro.c:535, libretro/libretro.c:556, Core/gb.c:1613-1618` |
| `bios/GBC_agb.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy` | `e6cefb5f7d352fab6681989763917c73` world; BizHawk's FirmwareDatabase.cs |  |
| `bios/gbc_bios.bin` | yes | `bizhawk/GBHawk`, `bizhawk/Gambatte`, `bizhawk/SameBoy`, `libretro/gambatte`, RetroBat's list | `dbfce9db9deaa2567f6a84fde55f9680` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `gambatte`: `libgambatte/libretro/libretro.cpp:1303-1363 (get_bootloader_from_file), libgambatte/src/bootloader.cpp:20-55 (load)` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/agb_boot.bin` | `libretro/sameboy` | no identity: no source states a hash for it |
| `cgb_boot.bin` | `jgenesis` | jgenesis has no cited firmware directory |
| `emulators/mesen/Firmware/cgb_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `gbc_bios.bin` | `mgba/mgba` | mgba has no cited firmware directory |

## gbc2players

gbc2players: 1 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/cgb_boot.bin` | no | `libretro/sameboy` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `sameboy`: `libretro/libretro.c:524, libretro/libretro.c:535, libretro/libretro.c:556, Core/gb.c:1613-1618` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/agb_boot.bin` | `libretro/sameboy` | no identity: no source states a hash for it |

## gmaster

Game Master: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gmaster.zip` | yes | `libretro/mame`, RetroBat's list | `4239d6f44a0d78d27c69f9b95885f48e` no region; RetroBat's list<br>zip, for `libretro/mame`: `d78c11agf_e19.u1` crc32 `05cc45e5` 4096 B | `mame`: `src/mame/handheld/gmaster.cpp:261-263` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/gmaster.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `gmaster.zip` | `mame64/gmaster` | mame64 has no cited firmware directory |

## gp32

Game Park 32: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gp32.zip` | yes | `libretro/mame`, RetroBat's list | `8c392f475834218d88533150d36cc84c` no region; RetroBat's list<br>zip, for `libretro/mame`: `gp32100k.bin` crc32 `d9925ac9` 524288 B, `gp32156k.bin` crc32 `667fb1c8` 524288 B, `gp32157e.bin` crc32 `b1e35643` 524288 B, `gp32166m.bin` crc32 `4548a840` 524288 B, `gp32mfv2.bin` crc32 `7ddaaaeb` 524288 B, `x2c32.jed` crc32 `eeec10d8` 15291 B | `mame`: `src/mame/gamepark/gp32.cpp:1710-1727` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/gp32.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `gp32.zip` | `mame64/gp32` | mame64 has no cited firmware directory |

## hbmame

hbmame: 99 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/airlbios.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `epr-21801.ic27` crc32 `a21bef24` 2097152 B, `epr-21802.ic27` crc32 `a77c6b1c` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10921` |
| `bios/aleck64.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `normpnt.rom` crc32 `e7f2a005` 128 B, `normslp.rom` crc32 `4f2ae525` 128 B, `pifdata.bin` crc32 `5ec82be9` 2048 B | `mame`: `src/mame/nintendo/aleck64.cpp:1417` |
| `bios/alg3do.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `saot_rom2.bin` crc32 `b832da9a` 524288 B | `mame`: `src/mame/misc/3do.cpp:573` |
| `bios/alg_bios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `315093-02.u2` crc32 `c4f0f55f` 262144 B | `mame`: `src/mame/amiga/alg.cpp:923` |
| `bios/allied.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `6530-009.u5` crc32 `e4fb64fb` 1024 B, `6530-010.u6` crc32 `dca980dd` 1024 B, `6530-011.u3` crc32 `13f42789` 1024 B | `mame`: `src/mame/pinball/allied.cpp:772` |
| `bios/ar_bios.zip` | yes | `libretro/mame` | `37e9746f4491aa2df9a83729d1a93620` no region; RetroBat's list<br>zip, for `libretro/mame`: `315093-01.u2` crc32 `a6ce1636` 262144 B, `gcp-1-hi` crc32 `67d44523` 65536 B, `gcp-1-lo` crc32 `65d9b9cf` 65536 B, `gcp-2-hi` crc32 `1d7594ae` 65536 B, `gcp-2-lo` crc32 `e776198d` 65536 B, `gcp-3-hi` crc32 `3e7364be` 65536 B, `gcp-3-lo` crc32 `87229e0d` 65536 B, `gcp_v311_1-hi.u16` crc32 `0b486a85` 65536 B, `gcp_v311_1-lo.u11` crc32 `80e8e863` 65536 B, `gcp_v311_2-hi.u17` crc32 `d20a4d7f` 65536 B, `gcp_v311_2-lo.u12` crc32 `5bf4c74c` 65536 B, `gcp_v400_1-hi.u16` crc32 `69295167` 65536 B, `gcp_v400_1-lo.u11` crc32 `504c2171` 65536 B, `gcp_v400_2-hi.u17` crc32 `13fb4e2d` 65536 B, `gcp_v400_2-lo.u12` crc32 `a5cc4515` 65536 B, `scpa_01-hi_v2.11.u12` crc32 `be9dbdc5` 65536 B, `scpa_01-hi_v2.20.u12` crc32 `79450b4b` 65536 B, `scpa_01-hi_v3.0.u12` crc32 `2d8e1a06` 65536 B, `scpa_01-lo_v2.11.u16` crc32 `95b84504` 65536 B, `scpa_01-lo_v2.20.u16` crc32 `d2825511` 65536 B, `scpa_01-lo_v3.0.u16` crc32 `e4f38fab` 65536 B | `mame`: `src/mame/amiga/arsystems.cpp:1011` |
| `bios/aristmk5.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `a562837.u36` crc32 `1f269234` 343 B, `a562838.u65` crc32 `f2f3c40a` 343 B, `a562840.u22` crc32 `941d4cdb` 343 B, `clear_mk5_u_11_ck_3a6f.u11` crc32 `4cf70173` 524288 B, `clear_mk5_u_7_ck_eb68.u7` crc32 `e8e8dc75` 524288 B, `setchip v1.0.u11` crc32 `a969f12b` 524288 B, `setchip v1.0.u7` crc32 `16c2828d` 524288 B, `setchip v4.02.04.u11` crc32 `def36617` 524288 B, `setchip v4.02.04.u7` crc32 `5a254b22` 524288 B, `setchip v4.02.04_alt.u11` crc32 `254e8b03` 524288 B, `setchip v4.02.04_alt.u7` crc32 `41275334` 524288 B, `setchip v4.03.07.u11` crc32 `637de9b9` 524288 B, `setchip v4.03.07.u7` crc32 `5cf56036` 524288 B, `setchip v4.04.00.u11` crc32 `82dfa12a` 524288 B, `setchip v4.04.00.u7` crc32 `2453137e` 524288 B, `setchip v4.04.01 cs ed54.u11` crc32 `199bacff` 524288 B, `setchip v4.04.05.u11` crc32 `2fc9b2a0` 524288 B, `setchip v4.04.05.u7` crc32 `e7b39a73` 524288 B, `setchip v4.04.08.u11` crc32 `d3234a28` 524288 B, `setchip v4.04.08.u7` crc32 `7c4b7fe4` 524288 B, `setchip v4.04.09.u11` crc32 `ff7a9035` 524288 B, `setchip v4.04.09.u7` crc32 `e8e8dc75` 524288 B | `mame`: `src/mame/acorn/aristmk5.cpp:9298` |
| `bios/aristmk6.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `01.02.02_left.u3` crc32 `1d0f4093` 1048576 B, `01.02.02_right.u2` crc32 `e920e940` 1048576 B, `01.02.08_left.u3` crc32 `f29fd1bf` 1048576 B, `01.02.08_right.u2` crc32 `aaaeac8c` 1048576 B, `01.03.03a_left.u70` crc32 `4ab5dd40` 2097152 B, `01.03.03a_right.u83` crc32 `253415f4` 2097152 B, `01.03.03e_left.u70` crc32 `ea50729a` 2097152 B, `01.03.03e_right.u83` crc32 `2255e263` 2097152 B, `01.03.05_left.u70` crc32 `0095e3f9` 2097152 B, `01.03.05_right.u83` crc32 `2c7f1ec3` 2097152 B, `01.03.06_left.u70` crc32 `2f9d9a29` 2097152 B, `01.03.06_right.u83` crc32 `bd48ca55` 2097152 B, `01.03.07_left.u70` crc32 `a3632da4` 2097152 B, `01.03.07_right.u83` crc32 `2ebccc4e` 2097152 B, `01.03.14_left.u70` crc32 `7138fec4` 2097152 B, `01.03.14_right.u83` crc32 `889ffd82` 2097152 B, `01.03.17_left.u70` crc32 `a88193dc` 2097152 B, `01.03.17_right.u83` crc32 `1582714b` 2097152 B, `01.04.04_left.u70` crc32 `b984a92c` 2097152 B, `01.04.04_right.u83` crc32 `e57ba02d` 2097152 B, `01.04.07_left.u70` crc32 `acfb0fe0` 2097152 B, `01.04.07_right.u83` crc32 `23c28e22` 2097152 B, `01.04.08_left.u70` crc32 `0dfcad10` 2097152 B, `01.04.08_right.u83` crc32 `95333304` 2097152 B, `01.04.10_left.u70` crc32 `9d9d52c1` 2097152 B, `01.04.10_right.u83` crc32 `82ce2fcc` 2097152 B, `01.04.11_left.u70` crc32 `787f2b07` 2097152 B, `01.04.11_right.u83` crc32 `2dae8ca0` 2097152 B, `01040505.u70` crc32 `b9ddea66` 1048576 B, `01040505.u71` crc32 `f56ea77e` 1048576 B, `01040505.u83` crc32 `90f32169` 1048576 B, `01040505.u84` crc32 `cf5a9d1e` 1048576 B, `02010114.u70` crc32 `3ba4379f` 1048576 B, `02010114.u71` crc32 `8f83c3dd` 1048576 B, `02010114.u83` crc32 `945104d7` 1048576 B, `02010114.u84` crc32 `183e3836` 1048576 B, `02010201.u70` crc32 `9b2db442` 1048576 B, `02010201.u71` crc32 `24d5614a` 1048576 B, `02010201.u83` crc32 `5f64a20c` 1048576 B, `02010201.u84` crc32 `0920930f` 1048576 B, `02060913_left.u70` crc32 `d6a6713c` 1048576 B, `02060913_right.u83` crc32 `31068c41` 1048576 B, `02061013_left.u70` crc32 `e70a7007` 1048576 B, `02061013_right.u83` crc32 `7a8619a5` 1048576 B, `03010301.u70` crc32 `5ae69121` 1048576 B, `03010301.u71` crc32 `d793440a` 1048576 B, `03010301.u83` crc32 `c8580554` 1048576 B, `03010301.u84` crc32 `a34a9f16` 1048576 B, `03030708_left.u70` crc32 `4e5ad823` 1048576 B, `03030708_right.u83` crc32 `b4b3c6a5` 1048576 B, `03130334_left.u70` crc32 `02175fde` 2097152 B, `03130334_right.u83` crc32 `bce3d97f` 2097152 B, `04010501_left.u70` crc32 `21182775` 1048576 B, `04010501_right.u83` crc32 `3daefb7a` 1048576 B, `04041205_left.u70` crc32 `dfb9a119` 1048576 B, `04041205_right.u83` crc32 `ca6bc86c` 1048576 B, `05010601_left.u70` crc32 `b3e6b4a0` 1048576 B, `05010601_right.u83` crc32 `c12eac11` 1048576 B, `06.02.04_left.u3` crc32 `117b75f2` 1048576 B, `06.02.04_right.u2` crc32 `1cf5a853` 1048576 B, `06.02.20_left.u70` crc32 `199ed3b9` 1048576 B, `06.02.20_right.u83` crc32 `e4001f60` 1048576 B, `06.03.03_left.u3` crc32 `a6924238` 1048576 B, `06.03.03_right.u2` crc32 `98763498` 1048576 B, `06.03.04_left.u3` crc32 `7034f26b` 1048576 B, `06.03.04_right.u2` crc32 `6f5f5ef1` 1048576 B, `07010801_left.u70` crc32 `8e92af68` 2097152 B, `07010801_right.u83` crc32 `8c148c11` 2097152 B, `09011001_left.u70` crc32 `229c2e63` 2097152 B, `09011001_right.u83` crc32 `8a853f80` 2097152 B, `11011501_left.u70` crc32 `c5cc3461` 2097152 B, `11011501_right.u83` crc32 `de4c3aed` 2097152 B, `11011901_left.u70` crc32 `d3dd2210` 2097152 B, `11011901_right.u83` crc32 `73dcb11c` 2097152 B, `13012001_left.u70` crc32 `38e8f659` 2097152 B, `13012001_right.u83` crc32 `e627dbfa` 2097152 B, `14010152_left.u70` crc32 `2716e1ef` 2097152 B, `14010152_right.u83` crc32 `7e3f61f6` 2097152 B, `14011605_left.u70` crc32 `cd26d4f0` 2097152 B, `14011605_right.u83` crc32 `2bec5b74` 2097152 B, `14011913_left.u70` crc32 `9a4cefdf` 2097152 B, `14011913_right.u83` crc32 `01d13b89` 2097152 B, `15011025_left.u70` crc32 `c02e14b0` 2097152 B, `15011025_right.u83` crc32 `bf21a975` 2097152 B, `19012801_left.u70` crc32 `b03bd17c` 2097152 B, `19012801_right.u83` crc32 `5b20a96c` 2097152 B, `20012305_left.u70` crc32 `ea8961cc` 2097152 B, `20012305_right.u83` crc32 `e436c1f5` 2097152 B, `20012605_left.u70` crc32 `87331111` 2097152 B, `20012605_right.u83` crc32 `045b82ad` 2097152 B, `21012901_left.u70` crc32 `0d271470` 2097152 B, `21012901_right.u83` crc32 `757618f2` 2097152 B, `24010467_left.u70` crc32 `9093d820` 2097152 B, `24010467_right.u83` crc32 `eddeff13` 2097152 B, `24013001_left.u70` crc32 `06ae7e07` 2097152 B, `24013001_right.u83` crc32 `e97afedf` 2097152 B, `25012805_left.u70` crc32 `996f32ce` 2097152 B, `25012805_right.u83` crc32 `2ecd8da8` 2097152 B, `gambs2m_01.05.07.u3` crc32 `58730ed3` 131072 B | `mame`: `src/mame/aristocrat/aristmk6.cpp:6639` |
| `bios/aristmk7.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mk7-bios_590176_01_1.00.7.u1` crc32 `f2fea07e` 1048576 B | `mame`: `src/mame/aristocrat/aristmk7.cpp:233` |
| `bios/atarisy1.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`, `libretro/mame`: `136032.101.e3` crc32 `7e84972a` 256 B, `136032.101.e3` crc32 `7e84972a` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.105.l13` crc32 `79021d3c` 16384 B, `136032.106.l12` crc32 `76ee86c4` 16384 B, `136032.107.b2` crc32 `7a29dc07` 8192 B, `136032.114.j11` crc32 `195c54ad` 16384 B, `136032.115.j10` crc32 `7275b4dc` 16384 B, `136032.205.l13` crc32 `88d0be26` 16384 B, `136032.206.l12` crc32 `3c79ef05` 16384 B | `mame`: `src/mame/atari/atarisy1.cpp:2669` |
| `bios/awbios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `bios0.ic23` crc32 `719b2b0b` 131072 B, `bios1.ic23` crc32 `d3e80a9f` 131072 B, `fpr-24363.ic48` crc32 `82a105f0` 131072 B | `mame`: `src/mame/sega/dc_atomiswave.cpp:1480` |
| `bios/bubsys.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`, `libretro/mame`: `400a1.2b` crc32 `5827b1e8` 256 B, `400a2.1b` crc32 `2f44f970` 256 B, `400b03.8g` crc32 `85c2afc5` 8192 B, `boot.bin` crc32 `f0774fc2` 480 B | `mame`: `src/mame/konami/nemesis.cpp:3140` |
| `bios/cedmag.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `magnet-master-vid-e03.bin` crc32 `86c4a4f0` 8192 B | `mame`: `src/mame/efo/cedar_magnet.cpp:1166` |
| `bios/chihiro.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `315-6351a_epc1pc8.ic2` crc32 `488624ea` 130817 B, `chihiro_xbox_bios.bin` crc32 `66232714` 524288 B, `fpr-23887_29lv160te.ic4` crc32 `13034372` 2097152 B, `fpr21042_m29w160et.bin` crc32 `a4fcab0b` 2097152 B, `ic10_g24lc64.bin` crc32 `cfc5e06f` 8192 B, `ic11_24lc024.bin` crc32 `8dc8374e` 128 B, `pc20_g24lc64.bin` crc32 `7742ab62` 8192 B | `mame`: `src/mame/sega/chihiro.cpp:2614` |
| `bios/coh1000a.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ac01.ic652` crc32 `c425eaf5` 8 B, `m534002c-15.ic353` crc32 `8d8d0764` 524288 B | `mame`: `src/mame/sony/zn.cpp:6015` |
| `bios/coh1000c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp01.ic652` crc32 `bb7ba033` 8 B, `m534002c-13.ic353` crc32 `50033af6` 524288 B, `msm27c402zb.ic353` crc32 `f20f7fe5` 524288 B | `mame`: `src/mame/sony/zn.cpp:5969` |
| `bios/coh1000t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `m534002c-14.ic353` crc32 `e3f23b6e` 524288 B, `tt01.ic652` crc32 `daac13e7` 8 B | `mame`: `src/mame/sony/zn.cpp:5989` |
| `bios/coh1000w.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `msm27c402zb.ic353` crc32 `45e8a4b4` 524288 B, `tw01.ic652` crc32 `1cac067e` 8 B | `mame`: `src/mame/sony/zn.cpp:6024` |
| `bios/coh1001l.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `at01.ic652` crc32 `181668b3` 8 B, `m534002c-17.ic353` crc32 `6721146b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6028` |
| `bios/coh1002e.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `et01.ic652` crc32 `a7dd922e` 8 B, `m27c402cz-54.ic353` crc32 `910f3a8b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6038` |
| `bios/coh1002m.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `m534002c-61.ic353` crc32 `69ffbcb4` 524288 B, `mg01.ic652` crc32 `50dc8322` 8 B | `mame`: `src/mame/sony/zn.cpp:6089` |
| `bios/coh1002t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `flash.u30` crc32 `9100dbf2` 2097152 B, `m534002c-14.ic353` crc32 `e3f23b6e` 524288 B, `tt01.ic652` crc32 `daac13e7` 8 B, `tt99.u17` crc32 `2989b6eb` 8 B | `mame`: `src/mame/sony/taitogn.cpp:1342` |
| `bios/coh1002v.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `coh-1002v.ic353` crc32 `5ff165f3` 524288 B, `kn01.ic652` crc32 `1ce271df` 8 B | `mame`: `src/mame/sony/zn.cpp:6032` |
| `bios/coh3002c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp10.ic652` crc32 `7808fcef` 8 B, `m534002c-59.ic353` crc32 `e860ea8b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6055` |
| `bios/coh3002t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `f35-01_m27c800.bin` crc32 `6225ec11` 1048576 B, `f35-01_m27c800_modbios_mb-2009_dump_by_aje_fr.bin` crc32 `6367aedf` 1048576 B, `f35-01_m27c800_modbios_mb-2011.bin` crc32 `f98ae118` 1048576 B, `f35-01_m27c800_v1.bin` crc32 `cd15cc30` 1048576 B, `flash.u30` crc32 `c48c8236` 2097152 B, `m534002c-60.ic353` crc32 `03967fa7` 524288 B, `tt10.ic652` crc32 `235510b1` 8 B, `tt16.u17` crc32 `6bb167b3` 8 B | `mame`: `src/mame/sony/taitogn.cpp:1341` |
| `bios/crysbios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mx27l1000-alt.u14` crc32 `1e8175c8` 131072 B, `mx27l1000.u14` crc32 `beff39a9` 131072 B | `mame`: `src/mame/misc/crystal.cpp:845` |
| `bios/cubo.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `391640-03.u6a` crc32 `d3837ae4` 1048576 B | `mame`: `src/mame/amiga/cubo.cpp:1435` |
| `bios/ddrfammt.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara_ddr_ha010_81947.bin` crc32 `737d5d1a` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2922` |
| `bios/decocass.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B<br>zip, for `libretro/mame`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `dsp-3_p0-a.m9` crc32 `2541e34b` 2048 B, `dsp-3_p0-b.m9` crc32 `b67a91d9` 2048 B, `dsp-3_p0-c.m9` crc32 `c76c4057` 2048 B, `dsp-3_p0-d.m9` crc32 `4b7d72bc` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v0b-.7e` crc32 `23d929b7` 4096 B, `v0c-.7e` crc32 `9f505709` 4096 B, `v0d-.7e` crc32 `1e0c22b1` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B<br>zip, for `libretro/mame2000`: `rms8.cpu` crc32 `23d929b7` 4096 B, `rms8.snd` crc32 `b66b2c2a` 2048 B | `mame`: `src/mame/dataeast/decocass.cpp:2131` |
| `bios/ekara.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara.bin` crc32 `9b27c4a2` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2904` |
| `bios/ekaraa.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara2.bin` crc32 `3c92d48d` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2905` |
| `bios/ekarag.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaragermany.bin` crc32 `644f06b0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2907` |
| `bios/ekaraj.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekarajapan.bin` crc32 `e459e43b` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2906` |
| `bios/ekaramix.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaramix.bin` crc32 `ee71576e` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2919` |
| `bios/ekaraphs.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaraheadset.bin` crc32 `dd9b3cd7` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2913` |
| `bios/ekaras.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaraspanish.bin` crc32 `77925ab0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2908` |
| `bios/epitch.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekarajapan.bin` crc32 `e459e43b` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2916` |
| `bios/evio.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `evio.bin` crc32 `ee22c764` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2939` |
| `bios/f355bios.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `epr-22849.ic27` crc32 `121e009c` 2097152 B, `epr-22850.ic27` crc32 `28aa539d` 2097152 B, `epr-22851.ic27` crc32 `62483677` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10920` |
| `bios/f355dlx.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `epr-21862.ic27` crc32 `1f630716` 2097152 B, `epr-21862p.ic27` crc32 `f0cd4aaa` 2097152 B, `epr-21863.ic27` crc32 `0615a4d1` 2097152 B, `epr-21864.ic27` crc32 `12ed7c66` 2097152 B, `epr-21864p.ic27` crc32 `05128861` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10919` |
| `bios/galgames.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `galaxy_u1__v1.71_7-8-98.u1` crc32 `604415c8` 1048576 B, `galaxy_u1__v1.80_10-15-98.u1` crc32 `e3ae423c` 1048576 B, `galaxy_u1__v1.90_12-01-98.u1` crc32 `c6d7bc6d` 1048576 B, `galaxy_u2__v1.71_7-8-98.u2` crc32 `d1c3dbb3` 1048576 B, `galaxy_u2__v1.80_10-15-98.u2` crc32 `73cff284` 1048576 B, `galaxy_u2__v1.90_12-01-98.u2` crc32 `e51ff184` 1048576 B | `mame`: `src/mame/ces/galgames.cpp:1301` |
| `bios/gcslottv.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `sammyslotunit.bin` crc32 `2ba6f3ab` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2946` |
| `bios/gp_110.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `a-110.u12` crc32 `ed0d518b` 2048 B, `b1-110.u13` crc32 `a223f2e8` 2048 B | `mame`: `src/mame/pinball/gp_1.cpp:575` |
| `bios/gq863.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `863a03.7b` crc32 `81498f73` 524288 B, `863a05.2x` crc32 `6f42a09e` 524288 B | `mame`: `src/mame/konami/twinkle.cpp:1643` |
| `bios/gts1.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `a1752cf.u5` crc32 `614a3bd9` 2048 B, `a1753ce.u4` crc32 `4cd312dd` 2048 B | `mame`: `src/mame/pinball/gts1.cpp:1070` |
| `bios/hikara.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `hikara.u3` crc32 `6b91102a` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2951` |
| `bios/hikaru.zip` | yes | `libretro/mame` | `aac601811a25d7b31a3d5f3f1f82f338` no region; RetroBat's list<br>zip, for `libretro/mame`: `93c46.ic115` crc32 `737dc714` 128 B, `epr-21904.ic94` crc32 `d96298b6` 2097152 B, `epr-23400.ic94` crc32 `3d557104` 2097152 B, `epr-23400a.ic94` crc32 `2aa906a7` 2097152 B, `prot_bot.ic94` crc32 `7cbf2fb6` 2097152 B | `mame`: `src/mame/sega/hikaru.cpp:1482` |
| `bios/hng64.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `bios_export.bin` crc32 `bbf07ec6` 524288 B, `bios_korea.bin` crc32 `ac953e2e` 524288 B, `bios_us.bin` crc32 `ab5948d6` 524288 B, `brom1.bin` crc32 `a30dd3de` 524288 B, `from1.bin` crc32 `6b933005` 524288 B, `lvs-igx-br9020f.u3` crc32 `af9f4287` 256 B, `lvs-ioj-br9020f.u2` crc32 `78b7020d` 256 B, `lvs-jam-br9020f.u3` crc32 `dabec5d2` 256 B, `rom1.bin` crc32 `4a6832dc` 130866 B, `tmp87ph40an.bin` crc32 `b70df21f` 32768 B | `mame`: `src/mame/snk/hng64.cpp:3238` |
| `bios/hod2bios.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `epr-21329.ic27` crc32 `d99e5b9b` 2097152 B, `epr-21330.ic27` crc32 `9e3bfa1b` 2097152 B, `epr-21331.ic27` crc32 `065f8500` 2097152 B, `epr-21332.ic27` crc32 `bd6ce0ec` 2097152 B, `hotd2biosproto.ic27` crc32 `ea74e967` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10918` |
| `bios/isgsm.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`, `libretro/mame`: `ism2006v00.u1` crc32 `2292585c` 131072 B | `mame`: `src/mame/sega/segas16b_isgsm.cpp:608` |
| `bios/isinger.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `kr_isinger.bin` crc32 `6ad588fe` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2910` |
| `bios/iteagle.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `17s20lpc_sb4.u26` crc32 `62c4af8a` 32768 B, `17s20lpc_sb5.u26` crc32 `c88b9d42` 32768 B, `17s50a_red1.u26` crc32 `f5cf3187` 131072 B, `bbh_u15_v102.u15` crc32 `1fd39e73` 1048576 B, `e2-card1.u22.jed` crc32 `9d1e1ace` 3025 B, `e2-res3.u117.jed` crc32 `4f1ff45a` 3025 B, `eagle_u15_v201.u15` crc32 `e180442b` 1048576 B, `eagle_u15_v204.u15` crc32 `f02e5523` 1048576 B, `eagle_u15_v208.u15` crc32 `772f2864` 1048576 B, `eagle_u15_v209.u15` crc32 `e0fc1a16` 1048576 B, `golf_fore_u15_v101.u15` crc32 `2600bc2b` 1048576 B, `golf_fore_u15_v102.u15` crc32 `4c000c5e` 1048576 B, `golf_fore_u15_v103.u15` crc32 `c35f4cf2` 1048576 B, `golf_fore_u15_v105.u15` crc32 `3870dbe0` 1048576 B, `golf_fore_u15_v106.u15` crc32 `56bc193d` 1048576 B, `golf_fore_u15_v106a.u15` crc32 `9c79b7ad` 1048576 B, `golf_fore_u15_v107.u15` crc32 `97a01fc9` 1048576 B | `mame`: `src/mame/itech/iteagle.cpp:756` |
| `bios/jpopira.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `i2cmem.bin` crc32 `70a05af1` 256 B, `jumpingpopira.bin` crc32 `a7bedbd2` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2936` |
| `bios/konamigv.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `999a01.7e` crc32 `ad498d2d` 524288 B | `mame`: `src/mame/konami/konamigv.cpp:1447` |
| `bios/konamigx.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `300a01.34k` crc32 `d5fa95f5` 131072 B | `mame`: `src/mame/konami/konamigx.cpp:4191` |
| `bios/konendev.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `2v02s502_ifu.u190` crc32 `36122a98` 524288 B | `mame`: `src/mame/konami/konendev.cpp:909` |
| `bios/kpython.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `b22a01.u42` crc32 `98de405e` 524288 B | `mame`: `src/mame/konami/kpython.cpp:376` |
| `bios/kpython2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ps2-0190j-20030822.bin` crc32 `79d60546` 4194304 B | `mame`: `src/mame/konami/kpython2.cpp:1918` |
| `bios/kviper.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `941a01.u25` crc32 `df6f88d6` 262144 B, `941b01.u25` crc32 `233e5159` 262144 B, `ds2430.u3` crc32 `f1511505` 40 B | `mame`: `src/mame/konami/viper.cpp:3213` |
| `bios/lindbios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `6.0.0009.bin` crc32 `5ffdfbf8` 1048576 B, `6.0.0010.bin` crc32 `ea2bf888` 1048576 B, `6.0.0010a.bin` crc32 `10dd9b76` 1048576 B, `fpr-24370b.ic6` crc32 `c3b021a4` 4194304 B, `vid_bios.u504` crc32 `f78d14d7` 65536 B | `mame`: `src/mame/sega/lindbergh.cpp:639` |
| `bios/macsbios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `am_macs_os_hi.u44` crc32 `538b68e4` 524288 B, `am_macs_os_low.u43` crc32 `0b5aed5e` 524288 B, `sx011-01.bin` crc32 `669096c1` 343 B | `mame`: `src/mame/seta/macs.cpp:775` |
| `bios/maxaflex.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `atarixl.rom` crc32 `1f9cd270` 16384 B, `maxaflex.uc` crc32 `fe9cf53c` 2048 B, `maxprom.prm` crc32 `edf5c950` 512 B | `mame`: `src/mame/atari/maxaflex.cpp:447` |
| `bios/megaplay.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ep15294.ic2` crc32 `aa8dc2d8` 131072 B, `epr-a15294.ic2` crc32 `f97c68aa` 131072 B | `mame`: `src/mame/sega/megaplay.cpp:1018` |
| `bios/megatech.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `epr-12263a.20` crc32 `07c3f423` 32768 B, `epr-12263b.20` crc32 `ca26c87a` 32768 B, `epr-12604a.20` crc32 `884e4aa5` 32768 B, `epr-12664.20` crc32 `f71e9526` 32768 B | `mame`: `src/mame/sega/megatech.cpp:1430` |
| `bios/miuchiz.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `otp.dat` crc32 `2ff7ec96` 16384 B | `mame`: `src/mame/misc/miuchiz.cpp:227` |
| `bios/naomi.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `315-6188.ic31` crc32 `7c9fea46` 8244 B, `boot_rom_64b8.ic606` crc32 `7a50fab9` 524288 B, `develop.ic27` crc32 `309a196a` 2097152 B, `develop110.ic27` crc32 `de7cfdb0` 2097152 B, `epr-21576.ic27` crc32 `9dad3495` 2097152 B, `epr-21576a.ic27` crc32 `cedfe439` 2097152 B, `epr-21576b.ic27` crc32 `755a6e07` 2097152 B, `epr-21576d.ic27` crc32 `3b2afa7b` 2097152 B, `epr-21576e.ic27` crc32 `08c0add7` 2097152 B, `epr-21576g.ic27` crc32 `d2a1c6bf` 2097152 B, `epr-21576h.ic27` crc32 `d4895685` 2097152 B, `epr-21576h_multi.ic27` crc32 `cce01f1f` 2097152 B, `epr-21577a.ic27` crc32 `969dc491` 2097152 B, `epr-21577d.ic27` crc32 `60ddcbbe` 2097152 B, `epr-21577e.ic27` crc32 `cf36e97b` 2097152 B, `epr-21577g.ic27` crc32 `25f64af7` 2097152 B, `epr-21577h.ic27` crc32 `fdf17452` 2097152 B, `epr-21578a.ic27` crc32 `6c9aad83` 2097152 B, `epr-21578d.ic27` crc32 `dfd5f42a` 2097152 B, `epr-21578e.ic27` crc32 `087f09a3` 2097152 B, `epr-21578f.ic27` crc32 `628a27fd` 2097152 B, `epr-21578g.ic27` crc32 `55413214` 2097152 B, `epr-21578h.ic27` crc32 `7b452946` 2097152 B, `epr-21579.ic27` crc32 `71f9c918` 2097152 B, `epr-21579d.ic27` crc32 `33513691` 2097152 B, `epr-21580a.ic27` crc32 `46dbe90f` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `naomi_boot_2491.ic27` crc32 `5b28f868` 2097152 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B, `zukinver0930.ic25` crc32 `58e17c23` 2097152 B | `mame`: `src/mame/sega/naomi.cpp:10917` |
| `bios/naomi2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `epr-23605.ic27` crc32 `5731e446` 2097152 B, `epr-23605a.ic27` crc32 `7bc3fc2d` 2097152 B, `epr-23605b.ic27` crc32 `3a3242d4` 2097152 B, `epr-23605c.ic27` crc32 `297ea6ed` 2097152 B, `epr-23605c_multi.ic27` crc32 `353fdbcd` 2097152 B, `epr-23607.ic27` crc32 `2b55add2` 2097152 B, `epr-23607b.ic27` crc32 `f308c5e9` 2097152 B, `epr-23607c.ic27` crc32 `750e254b` 2097152 B, `epr-23608.ic27` crc32 `929cc3a6` 2097152 B, `epr-23608a.ic27` crc32 `e8f884d1` 2097152 B, `epr-23608b.ic27` crc32 `a554b1e3` 2097152 B, `epr-23608c.ic27` crc32 `6ef1dd8e` 2097152 B, `epr-23609b.ic27` crc32 `99e3751f` 2097152 B, `epr-23610b.ic27` crc32 `de8e1267` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10922` |
| `bios/naomigd.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `epr-21576e.ic27` crc32 `08c0add7` 2097152 B, `epr-21576g.ic27` crc32 `d2a1c6bf` 2097152 B, `epr-21576h.ic27` crc32 `d4895685` 2097152 B, `epr-21577e.ic27` crc32 `cf36e97b` 2097152 B, `epr-21577g.ic27` crc32 `25f64af7` 2097152 B, `epr-21577h.ic27` crc32 `fdf17452` 2097152 B, `epr-21578e.ic27` crc32 `087f09a3` 2097152 B, `epr-21578g.ic27` crc32 `55413214` 2097152 B, `epr-21578h.ic27` crc32 `7b452946` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10923` |
| `bios/neogeo.zip` | yes | `libretro/mame` | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267`<br>`mame`: `src/mame/snk/neogeo.cpp:2428` |
| `bios/nichidvd.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `va1a101.u2` crc32 `36135792` 131072 B, `va1b101.u2` crc32 `b92a83c8` 131072 B, `va1b102.u2` crc32 `67374715` 131072 B | `mame`: `src/mame/nichibutsu/hrdvd.cpp:1012` |
| `bios/nss.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `nss-c.ic14` crc32 `a8e202b3` 32768 B, `nss-ic14.02.ic14` crc32 `e06cb58f` 32768 B, `nss-v3.ic14` crc32 `ac385b53` 32768 B | `mame`: `src/mame/nintendo/nss.cpp:1099` |
| `bios/pgm.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B<br>zip, for `libretro/mame`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p01s.u20` crc32 `e42b166e` 131072 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B | `mame`: `src/mame/igs/pgm.cpp:5545` |
| `bios/playch10.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `pch1-c-6d.82s129an.6d` crc32 `1213ebd4` 256 B, `pch1-c-6e.82s129an.6e` crc32 `a2625c6e` 256 B, `pch1-c-6f.82s129an.6f` crc32 `e5414ca3` 256 B, `pch1-c_8m.8m` crc32 `83ebc7a3` 8192 B, `pch1-c_8p-8p` crc32 `90e1b80c` 8192 B, `pch1-c_8te.8t` crc32 `123ffa37` 16384 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8t_e-1.8t` crc32 `88a8f87e` 16384 B, `pch1-c__8t_e-2.8t` crc32 `d52fa07a` 16384 B, `pck1-c.8t` crc32 `503ee8b1` 16384 B, `pck1-c_fix.8t` crc32 `0be8ceb4` 16384 B, `rp2c0x.pal` crc32 `48de65dc` 192 B | `mame`: `src/mame/nintendo/playch10.cpp:2980` |
| `bios/popira.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `popira.bin` crc32 `71d20afc` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2925` |
| `bios/popira2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `popira2.bin` crc32 `6acd8be0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2930` |
| `bios/popirak.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `kr_main.u2` crc32 `785d8e21` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2927` |
| `bios/pumpitup.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mk3_1.0_bios.u22` crc32 `a2b5546b` 131072 B, `mk3_1.1_bios.u22` crc32 `4540c23f` 131072 B, `piu10.u8` crc32 `5911e31a` 2097152 B, `piu10.u9` crc32 `9c436cfa` 2097152 B | `mame`: `src/mame/misc/xtom3d.cpp:997` |
| `bios/qsound.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `dl-1425.bin` crc32 `d6cf5ef5` 24576 B | `mame`: `src/devices/sound/qsound.cpp:121-125` |
| `bios/recel.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `a2361.b1` crc32 `d0c4695d` 1024 B, `a2362.b2` crc32 `39a70611` 1024 B | `mame`: `src/mame/pinball/recel.cpp:546` |
| `bios/sammymdl.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mt201l04.u012` crc32 `c8c6d25f` 524288 B, `vm1201l01.u012` crc32 `21b2ccbd` 524288 B, `vm1211l01.u2` crc32 `c3c74dc5` 524288 B | `mame`: `src/mame/sigma/sammymdl.cpp:1025` |
| `bios/segasp.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `epr-24236a.ic50` crc32 `ca7df0de` 2097152 B, `epr-24328.ic50` crc32 `25f2ef00` 2097152 B, `epr-24328a.ic50` crc32 `03ec3805` 2097152 B, `fpr-24208a.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24329.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24407.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24407_123.ic72` crc32 `a738ea1c` 2097152 B, `mb_eeprom_exp.ic54s` crc32 `947ddfad` 128 B, `mb_eeprom_us.ic54s` crc32 `4186d7ab` 128 B, `mb_serial.ic57` crc32 `e1e3c009` 128 B, `net_eeprom.ic74s` crc32 `77cc5a6c` 512 B, `net_firm_119.ic72` crc32 `a738ea1c` 2097152 B | `mame`: `src/mame/sega/segasp.cpp:1273` |
| `bios/sfcbox.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `atrom-4s-0.rom5` crc32 `ad3ec05c` 524288 B, `krom1.ic1` crc32 `c9010002` 65536 B, `krom2.00.ic1` crc32 `e31b5580` 131072 B | `mame`: `src/mame/nintendo/sfcbox.cpp:586` |
| `bios/shtzone.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `epr10894a.20` crc32 `ea8901d9` 16384 B | `mame`: `src/mame/sega/shtzone.cpp:376` |
| `bios/skns.zip` | yes | `libretro/mame` | zip, for `libretro/fbneo`: `sknsj1.u10` crc32 `7e2b836c` 524288 B<br>zip, for `libretro/mame`: `sknsa1.u10` crc32 `745e5212` 524288 B, `sknse2.u10` crc32 `e2b9d7d1` 524288 B, `sknsj1.u10` crc32 `7e2b836c` 524288 B, `sknsk1.u10` crc32 `ff1c9f79` 524288 B, `sknsu1.u10` crc32 `384d21ec` 524288 B, `supernova-modbios-korea.u10` crc32 `1d90517c` 524288 B, `supernova_modbios-japan.u10` crc32 `b8d3190c` 524288 B | `mame`: `src/mame/kaneko/suprnova.cpp:2135` |
| `bios/slvrball.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `bios29.bin` crc32 `ddbd94f4` 131072 B, `bios30.bin` crc32 `c6e30fb1` 262144 B, `bios31.bin` crc32 `5121b267` 262144 B, `bios32.bin` crc32 `63318d8a` 262144 B, `bios33.bin` crc32 `96658274` 262144 B, `bios34.bin` crc32 `400702fe` 262144 B, `bios35.bin` crc32 `d5a2f966` 262144 B, `bios36.bin` crc32 `83ca05e9` 262144 B, `bios37.bin` crc32 `9f35e2d2` 262144 B, `bios38.bin` crc32 `cb49085b` 262144 B, `bios39.bin` crc32 `e7541a28` 262144 B, `bios40.bin` crc32 `c5ed7d4f` 262144 B, `bios41.bin` crc32 `0940aebd` 262144 B, `bios42.bin` crc32 `820c057e` 262144 B, `bios44.bin` crc32 `a4b2703e` 262144 B, `bios45.bin` crc32 `790d5db0` 262144 B, `bios46.bin` crc32 `90074f2b` 262144 B, `bios47.bin` crc32 `248910ed` 262144 B, `test.bin` crc32 `ddbd94f4` 131072 B | `mame`: `src/mame/misc/silverball.cpp:259` |
| `bios/stvbios.zip` | yes | `libretro/mame` | zip, for `libretro/kronos`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B<br>zip, for `libretro/mame`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B, `stvbios.nv` crc32 `15432ae2` 128 B | `mame`: `src/mame/sega/stv.cpp:3948` |
| `bios/su2000.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `amibios_486dx_isa_bios.u32` crc32 `811d3639` 65536 B, `hmc86304_bios_v208.u7` crc32 `c6c32f1a` 32768 B, `ver_151_03_u16.u16` crc32 `8354d059` 131072 B, `ver_151_03_u17.u17` crc32 `ace4081d` 131072 B, `wfc062_212.u62` crc32 `9a6b553a` 32768 B, `wi01p006.u6` crc32 `528142eb` 2194 B, `wi01p026.u26` crc32 `528142eb` 2194 B, `wi01p044.u44` crc32 `a2ad9cf8` 2194 B, `wi01p045.u45` crc32 `e2a13df7` 2194 B, `wi01p046.u46` crc32 `4c22fba5` 2194 B, `wi01p048.u48` crc32 `b9958131` 2706 B, `wi01p049.u49` crc32 `820d245a` 2706 B, `wi01p050.u50` crc32 `7cb0931f` 2194 B, `wi01p053.u53` crc32 `d2eb2493` 2194 B, `wi01p054.u54` crc32 `96d3506e` 2194 B, `wi01p056.u56` crc32 `8bf57db4` 2194 B, `wi01p058.u58` crc32 `882f6405` 2194 B, `wi01p067.u67` crc32 `a97cff8e` 2194 B, `wi01p070.u70` crc32 `4322291e` 2706 B, `wi01p072.u72` crc32 `fe77b66b` 2706 B, `wi01p073.u73` crc32 `e02fe7c1` 2706 B, `wi01p074.u74` crc32 `fca2823c` 2706 B, `wi01p075.u75` crc32 `3d1adb17` 2194 B, `wi01p079.u79` crc32 `3d3ad152` 2194 B, `wi01p080.u80` crc32 `657a4495` 2194 B, `wi01p083.u83` crc32 `902731aa` 2706 B, `wi01p092.u92` crc32 `f996657e` 2706 B, `wi02p000.u23` crc32 `bab34c4b` 2706 B, `wi02p001.u32` crc32 `b438ce94` 2706 B, `wi02p002.u22` crc32 `bab34c4b` 2706 B, `wi02p003.u31` crc32 `b438ce94` 2706 B, `wi02p004.u6` crc32 `bab34c4b` 2706 B, `wi02p005.u14` crc32 `b438ce94` 2706 B, `wi02p006.u5` crc32 `bab34c4b` 2706 B, `wi02p007.u13` crc32 `b438ce94` 2706 B, `wi02p008.u51` crc32 `7c421890` 2194 B, `wi02p009.u49` crc32 `ff63d60c` 2194 B, `wi02p010.u72` crc32 `68f48053` 2194 B, `wi02p011.u88` crc32 `78701d0f` 2194 B, `wi02p012.u85` crc32 `1c489ff9` 2194 B, `wi02p013.u86` crc32 `19de958f` 2194 B, `wi02p014.u82` crc32 `845e4d48` 2194 B, `wi02p015.u74` crc32 `a10d1876` 2194 B, `wi02p016.u52` crc32 `d84b58c2` 2194 B | `mame`: `src/mame/misc/su2000.cpp:302` |
| `bios/sys246.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `r27v1602f.7d` crc32 `2b2e41a2` 2097152 B | `mame`: `src/mame/namco/namcops2.cpp:1716` |
| `bios/sys256.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `r27v1602f.8g` crc32 `b2a8eeb6` 2097152 B | `mame`: `src/mame/namco/namcops2.cpp:1761` |
| `bios/sys573.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `700a01,gchgchmp.22g` crc32 `39ebb0ca` 524288 B, `700a01.22g` crc32 `11812ef8` 524288 B, `700b01.22g` crc32 `6cf852af` 524288 B, `h8a01.bin` crc32 `131e0359` 64 B, `h8a01.bin` crc32 `131e0359` 64 B, `h8b01.bin` crc32 `508b057d` 64 B | `mame`: `src/mame/konami/ksys573.cpp:6415` |
| `bios/systemy2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `system_y2_m25p40.u801` crc32 `2b19f490` 524288 B | `mame`: `src/mame/sega/y2.cpp:141` |
| `bios/taikodp.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `taikodepopira.bin` crc32 `037a8472` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2933` |
| `bios/taitotz.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `e68-04-1.ic5` crc32 `c7c2dc6b` 524288 B, `e68-05-1.ic6` crc32 `6ad9b006` 524288 B | `mame`: `src/mame/taito/taitotz.cpp:3016` |
| `bios/tak_daig.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `robottransformer_unit.bin` crc32 `02deddaa` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2944` |
| `bios/tourvis.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `v4-40.ic29` crc32 `ba6290cc` 32768 B, `v4-42.ic29` crc32 `0372606d` 32768 B, `v4-43.ic29` crc32 `88da23f3` 32768 B, `v4-51.ic29` crc32 `2de0b946` 32768 B, `v4-52.ic29` crc32 `ffd7b0fe` 32768 B, `v4-53.ic29` crc32 `bccb53c9` 32768 B, `v4-54.ic29` crc32 `125e7a6e` 32768 B, `v4-55.ic29` crc32 `87cf66c1` 32768 B, `v4-60.ic29` crc32 `1fd27e22` 32768 B, `vt_1.1.bin` crc32 `27abbc36` 32768 B, `vt_2.0.bin` crc32 `36012f88` 32768 B | `mame`: `src/mame/pce/tourvis.cpp:448` |
| `bios/triforce.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `triforce_bootrom.bin` crc32 `d1883221` 2097152 B | `mame`: `src/mame/sega/triforce.cpp:1310` |
| `bios/v4bios.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `vid.p1` crc32 `e996bc18` 65536 B | `mame`: `src/mame/barcrest/mpu4vid.cpp:8698` |
| `bios/ym2413.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `ym2413_instruments.bin` crc32 `6f582d01` 144 B | `mame`: `src/devices/sound/ymopl.cpp:218-223` |
| `bios/ym2608.zip` | no | `libretro/mame` | zip, for `libretro/fbneo`, `libretro/mame`: `ym2608_adpcm_rom.bin` crc32 `23c9e0d8` 8192 B | `mame`: `src/devices/sound/ymopn.cpp:76-91` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/galgbios.zip` | `libretro/mame` | no identity: no source states a hash for it |
| `bios/mac2bios.zip` | `libretro/mame` | no identity: no source states a hash for it |

## hikaru

Hikaru: 2 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/hikaru.zip` | yes | RetroBat's list | `aac601811a25d7b31a3d5f3f1f82f338` no region; RetroBat's list<br>zip, for `libretro/mame`: `93c46.ic115` crc32 `737dc714` 128 B, `epr-21904.ic94` crc32 `d96298b6` 2097152 B, `epr-23400.ic94` crc32 `3d557104` 2097152 B, `epr-23400a.ic94` crc32 `2aa906a7` 2097152 B, `prot_bot.ic94` crc32 `7cbf2fb6` 2097152 B |  |
| `bios/mie.zip` | yes | RetroBat's list | `e95415b161121bef35ade12367138c63` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `hikaru.zip` | `demul/hikaru` | demul has no cited firmware directory |
| `jvs13551.zip` | `demul/hikaru` | demul has no cited firmware directory |
| `mie.zip` | `demul/hikaru` | demul has no cited firmware directory |

## intellivision

Mattel Intellivision: 2 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/exec.bin` | yes | `bizhawk/IntelliHawk`, `libretro/freeintv`, RetroBat's list | `62e761035cb657903761800f4437b8af` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `freeintv`: `src/intv.c:51-96, src/libretro.c:1178` |
| `bios/grom.bin` | yes | `bizhawk/IntelliHawk`, `libretro/freeintv`, RetroBat's list | `0cd5946c6473e42e8e4c2137785e427f` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `freeintv`: `src/intv.c:98-144, src/libretro.c:1182` |

## j2me

Java: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/freej2me-lr.jar` | yes | `libretro/freej2me`, RetroBat's list | `a6f31483d1da4558cc19025e21f95c1d` no region; RetroBat's list | `freej2me`: `src/libretro/freej2me_libretro.c:483-486 (GET_SYSTEM_DIRECTORY + fill_pathname_join + java -jar)` |

## jaguar

Jaguar: 4 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/[BIOS] Atari Jaguar (World).j64` | yes | RetroBat's list | `bcfe348c565d9dedb173822ee6850dea` world; RetroBat's list |  |
| `bios/[BIOS] Atari Jaguar CD (World).j64`<br>also `bios/Jaguar CD BIOS.bin`, `bios/Jaguar CD BIOS.rom`, `bios/[BIOS] Atari Jaguar CD (World).bin`, `bios/[BIOS] Atari Jaguar CD (World).rom`, `bios/jagcd.bin`, `bios/jagcd_bios.bin`, `bios/jaguarcd.bin`, `bios/jaguarcd_bios.bin` | no | `libretro/virtualjaguar` | `77cd95c7ad06a39f4c59995094aa10f9` world; a dump catalog's name | `virtualjaguar`: `libretro.c:4964, libretro.c:4971-5048, libretro.c:5049-5158, libretro.c:5159-5175` |
| `bios/[BIOS] Atari Jaguar Developer CD (World).j64`<br>also `bios/[BIOS] Atari Jaguar Developer CD (World).bin`, `bios/[BIOS] Atari Jaguar Developer CD (World).rom` | no | `libretro/virtualjaguar` | `578de34498cb9a40c5368b6f5ca80484` world; a dump catalog's name | `virtualjaguar`: `libretro.c:4965, libretro.c:5049-5158` |
| `bios/jagboot_m.rom` | no | `libretro/virtualjaguar` | `6e844759720226e58d55ecaf33608a13` no region; retrobios `virtualjaguar` | `virtualjaguar`: `libretro.c:5358-5380, src/bios/jagbios_m.c:9` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/jagboot.rom` | `libretro/virtualjaguar` | no identity: no source states a hash for it |

## jaguarcd

jaguarcd: 3 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/[BIOS] Atari Jaguar CD (World).j64`<br>also `bios/Jaguar CD BIOS.bin`, `bios/Jaguar CD BIOS.rom`, `bios/[BIOS] Atari Jaguar CD (World).bin`, `bios/[BIOS] Atari Jaguar CD (World).rom`, `bios/jagcd.bin`, `bios/jagcd_bios.bin`, `bios/jaguarcd.bin`, `bios/jaguarcd_bios.bin` | no | `libretro/virtualjaguar` | `77cd95c7ad06a39f4c59995094aa10f9` world; a dump catalog's name | `virtualjaguar`: `libretro.c:4964, libretro.c:4971-5048, libretro.c:5049-5158, libretro.c:5159-5175` |
| `bios/[BIOS] Atari Jaguar Developer CD (World).j64`<br>also `bios/[BIOS] Atari Jaguar Developer CD (World).bin`, `bios/[BIOS] Atari Jaguar Developer CD (World).rom` | no | `libretro/virtualjaguar` | `578de34498cb9a40c5368b6f5ca80484` world; a dump catalog's name | `virtualjaguar`: `libretro.c:4965, libretro.c:5049-5158` |
| `bios/jagboot_m.rom` | no | `libretro/virtualjaguar` | `6e844759720226e58d55ecaf33608a13` no region; retrobios `virtualjaguar` | `virtualjaguar`: `libretro.c:5358-5380, src/bios/jagbios_m.c:9` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/jagboot.rom` | `libretro/virtualjaguar` | no identity: no source states a hash for it |

## laseractive

Pioneer Laseractive: 5 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/laseractive/[BIOS] LaserActive PAC-N1 (Japan) (v1.02).bin` | yes | RetroBat's list | `f69f173b251d8bf7649b10a9167a10bf` no region; RetroBat's list |  |
| `bios/laseractive/[BIOS] LaserActive PAC-N10 (US) (v1.02).bin` | yes | RetroBat's list | `f0fb8a4605ac7eefbafd4f2d5a793cc8` no region; RetroBat's list |  |
| `bios/laseractive/[BIOS] LaserActive PAC-S1 (Japan) (v1.05).bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/laseractive/[BIOS] LaserActive PAC-S10 (US) (v1.04).bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/laseractive/[BIOS] LaserActive PCE-LP1 (Japan) (v1.02).bin` | yes | RetroBat's list | `761fea207d0eafd4cfd78da7c44cac88` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `mega_ld_bios_J.bin` | `ares/MegaLD`, `ares/NecLD` | ares has no cited firmware directory |
| `mega_ld_bios_U.bin` | `ares/MegaLD`, `ares/NecLD` | ares has no cited firmware directory |
| `pac-n1.pce` | `ares/MegaLD`, `ares/NecLD` | ares has no cited firmware directory |
| `pac-n10.pce` | `ares/MegaLD`, `ares/NecLD` | ares has no cited firmware directory |
| `pce-lp1.pce` | `ares/MegaLD`, `ares/NecLD` | ares has no cited firmware directory |

## lindbergh

lindbergh: 0 slots, 13 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `libCg.so` | `linuxloader` | linuxloader has no cited firmware directory |
| `libCg2.so` | `linuxloader` | linuxloader has no cited firmware directory |
| `libCgGL.so` | `linuxloader` | linuxloader has no cited firmware directory |
| `libcrypto.so.0.9.7` | `linuxloader` | linuxloader has no cited firmware directory |
| `libopenal.so.0` | `linuxloader` | linuxloader has no cited firmware directory |
| `libssl.so.0.9.7` | `linuxloader` | linuxloader has no cited firmware directory |
| `logo.tga` | `linuxloader` | linuxloader has no cited firmware directory |
| `logo_red.tga` | `linuxloader` | linuxloader has no cited firmware directory |
| `LucidaConsole_12.abc` | `linuxloader` | linuxloader has no cited firmware directory |
| `LucidaConsole_12.tga` | `linuxloader` | linuxloader has no cited firmware directory |
| `SEGA_KakuGothic-DB-Roman_12.abc` | `linuxloader` | linuxloader has no cited firmware directory |
| `SEGA_KakuGothic-DB-Roman_12.tga` | `linuxloader` | linuxloader has no cited firmware directory |
| `segaboot` | `linuxloader` | linuxloader has no cited firmware directory |

## loopy

Casio Loopy: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/casloopy.zip` | yes | `libretro/mame`, RetroBat's list | `0e1d8dc0110ecf8201c0cef11ef4a858` no region; RetroBat's list<br>zip, for `libretro/mame`: `hd6437021.lsi302` crc32 `8c57ff9f` 32768 B, `hn62434fa.lsi352` crc32 `8f51fa17` 524288 B | `mame`: `src/mame/casio/casloopy.cpp:2097-2105` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/casloopy.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `casloopy.zip` | `mame64/casloopy` | mame64 has no cited firmware directory |

## lynx

Lynx: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/lynxboot.img` | yes | `bizhawk/Handy`, `libretro/handy`, `libretro/holani`, `libretro/mednafen_lynx`, RetroBat's list | `fcd403db69f54290b51035d82f835e7b` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `beetle_lynx`: `libretro.cpp:191, mednafen/lynx/rom.cpp:50-78, mednafen/lynx/cart.cpp:67`<br>`handy`: `lynx/rom.h:48-49 (ROM_SIZE=0x200, ROM_CRC32=0x0D973C9D), lynx/rom.cpp:76-129 (CRom constructor), libretro/libretro.cpp:1231-1258 (bios path + CSystem init)`<br>`holani`: `src/lib.rs:110-124 (bios load), src/rom.rs:72-79 (size validation)` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/howard.o` | `libretro/handy` | no identity: no source states a hash for it |
| `lynxboot.img` | `mednafen/lynx` | mednafen has no cited firmware directory |

## mame

mame: 100 slots, 218 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/airlbios.zip` | no | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-21801.ic27` crc32 `a21bef24` 2097152 B, `epr-21802.ic27` crc32 `a77c6b1c` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10921`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9468` |
| `bios/aleck64.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `normpnt.rom` crc32 `e7f2a005` 128 B, `normslp.rom` crc32 `4f2ae525` 128 B, `pifdata.bin` crc32 `5ec82be9` 2048 B | `mame`: `src/mame/nintendo/aleck64.cpp:1417`<br>`mame2010`: `src/mame/drivers/aleck64.c`<br>`mame2016`: `src/mame/drivers/aleck64.cpp:1166` |
| `bios/alg3do.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `saot_rom2.bin` crc32 `b832da9a` 524288 B | `mame`: `src/mame/misc/3do.cpp:573` |
| `bios/alg_bios.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `315093-02.u2` crc32 `c4f0f55f` 262144 B | `mame`: `src/mame/amiga/alg.cpp:923`<br>`mame2010`: `src/mame/drivers/alg.c`<br>`mame2016`: `src/mame/drivers/alg.cpp:783` |
| `bios/allied.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `6530-009.u5` crc32 `e4fb64fb` 1024 B, `6530-010.u6` crc32 `dca980dd` 1024 B, `6530-011.u3` crc32 `13f42789` 1024 B | `mame`: `src/mame/pinball/allied.cpp:772`<br>`mame2016`: `src/mame/drivers/allied.cpp:711` |
| `bios/ar_bios.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | `37e9746f4491aa2df9a83729d1a93620` no region; RetroBat's list<br>zip, for `libretro/mame`: `315093-01.u2` crc32 `a6ce1636` 262144 B, `gcp-1-hi` crc32 `67d44523` 65536 B, `gcp-1-lo` crc32 `65d9b9cf` 65536 B, `gcp-2-hi` crc32 `1d7594ae` 65536 B, `gcp-2-lo` crc32 `e776198d` 65536 B, `gcp-3-hi` crc32 `3e7364be` 65536 B, `gcp-3-lo` crc32 `87229e0d` 65536 B, `gcp_v311_1-hi.u16` crc32 `0b486a85` 65536 B, `gcp_v311_1-lo.u11` crc32 `80e8e863` 65536 B, `gcp_v311_2-hi.u17` crc32 `d20a4d7f` 65536 B, `gcp_v311_2-lo.u12` crc32 `5bf4c74c` 65536 B, `gcp_v400_1-hi.u16` crc32 `69295167` 65536 B, `gcp_v400_1-lo.u11` crc32 `504c2171` 65536 B, `gcp_v400_2-hi.u17` crc32 `13fb4e2d` 65536 B, `gcp_v400_2-lo.u12` crc32 `a5cc4515` 65536 B, `scpa_01-hi_v2.11.u12` crc32 `be9dbdc5` 65536 B, `scpa_01-hi_v2.20.u12` crc32 `79450b4b` 65536 B, `scpa_01-hi_v3.0.u12` crc32 `2d8e1a06` 65536 B, `scpa_01-lo_v2.11.u16` crc32 `95b84504` 65536 B, `scpa_01-lo_v2.20.u16` crc32 `d2825511` 65536 B, `scpa_01-lo_v3.0.u16` crc32 `e4f38fab` 65536 B | `mame`: `src/mame/amiga/arsystems.cpp:1011`<br>`mame2010`: `src/mame/drivers/arcadia.c`<br>`mame2016`: `src/mame/drivers/arsystems.cpp:974` |
| `bios/aristmk5.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `a562837.u36` crc32 `1f269234` 343 B, `a562838.u65` crc32 `f2f3c40a` 343 B, `a562840.u22` crc32 `941d4cdb` 343 B, `clear_mk5_u_11_ck_3a6f.u11` crc32 `4cf70173` 524288 B, `clear_mk5_u_7_ck_eb68.u7` crc32 `e8e8dc75` 524288 B, `setchip v1.0.u11` crc32 `a969f12b` 524288 B, `setchip v1.0.u7` crc32 `16c2828d` 524288 B, `setchip v4.02.04.u11` crc32 `def36617` 524288 B, `setchip v4.02.04.u7` crc32 `5a254b22` 524288 B, `setchip v4.02.04_alt.u11` crc32 `254e8b03` 524288 B, `setchip v4.02.04_alt.u7` crc32 `41275334` 524288 B, `setchip v4.03.07.u11` crc32 `637de9b9` 524288 B, `setchip v4.03.07.u7` crc32 `5cf56036` 524288 B, `setchip v4.04.00.u11` crc32 `82dfa12a` 524288 B, `setchip v4.04.00.u7` crc32 `2453137e` 524288 B, `setchip v4.04.01 cs ed54.u11` crc32 `199bacff` 524288 B, `setchip v4.04.05.u11` crc32 `2fc9b2a0` 524288 B, `setchip v4.04.05.u7` crc32 `e7b39a73` 524288 B, `setchip v4.04.08.u11` crc32 `d3234a28` 524288 B, `setchip v4.04.08.u7` crc32 `7c4b7fe4` 524288 B, `setchip v4.04.09.u11` crc32 `ff7a9035` 524288 B, `setchip v4.04.09.u7` crc32 `e8e8dc75` 524288 B | `mame`: `src/mame/acorn/aristmk5.cpp:9298`<br>`mame2016`: `src/mame/drivers/aristmk5.cpp:2191` |
| `bios/aristmk6.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `01.02.02_left.u3` crc32 `1d0f4093` 1048576 B, `01.02.02_right.u2` crc32 `e920e940` 1048576 B, `01.02.08_left.u3` crc32 `f29fd1bf` 1048576 B, `01.02.08_right.u2` crc32 `aaaeac8c` 1048576 B, `01.03.03a_left.u70` crc32 `4ab5dd40` 2097152 B, `01.03.03a_right.u83` crc32 `253415f4` 2097152 B, `01.03.03e_left.u70` crc32 `ea50729a` 2097152 B, `01.03.03e_right.u83` crc32 `2255e263` 2097152 B, `01.03.05_left.u70` crc32 `0095e3f9` 2097152 B, `01.03.05_right.u83` crc32 `2c7f1ec3` 2097152 B, `01.03.06_left.u70` crc32 `2f9d9a29` 2097152 B, `01.03.06_right.u83` crc32 `bd48ca55` 2097152 B, `01.03.07_left.u70` crc32 `a3632da4` 2097152 B, `01.03.07_right.u83` crc32 `2ebccc4e` 2097152 B, `01.03.14_left.u70` crc32 `7138fec4` 2097152 B, `01.03.14_right.u83` crc32 `889ffd82` 2097152 B, `01.03.17_left.u70` crc32 `a88193dc` 2097152 B, `01.03.17_right.u83` crc32 `1582714b` 2097152 B, `01.04.04_left.u70` crc32 `b984a92c` 2097152 B, `01.04.04_right.u83` crc32 `e57ba02d` 2097152 B, `01.04.07_left.u70` crc32 `acfb0fe0` 2097152 B, `01.04.07_right.u83` crc32 `23c28e22` 2097152 B, `01.04.08_left.u70` crc32 `0dfcad10` 2097152 B, `01.04.08_right.u83` crc32 `95333304` 2097152 B, `01.04.10_left.u70` crc32 `9d9d52c1` 2097152 B, `01.04.10_right.u83` crc32 `82ce2fcc` 2097152 B, `01.04.11_left.u70` crc32 `787f2b07` 2097152 B, `01.04.11_right.u83` crc32 `2dae8ca0` 2097152 B, `01040505.u70` crc32 `b9ddea66` 1048576 B, `01040505.u71` crc32 `f56ea77e` 1048576 B, `01040505.u83` crc32 `90f32169` 1048576 B, `01040505.u84` crc32 `cf5a9d1e` 1048576 B, `02010114.u70` crc32 `3ba4379f` 1048576 B, `02010114.u71` crc32 `8f83c3dd` 1048576 B, `02010114.u83` crc32 `945104d7` 1048576 B, `02010114.u84` crc32 `183e3836` 1048576 B, `02010201.u70` crc32 `9b2db442` 1048576 B, `02010201.u71` crc32 `24d5614a` 1048576 B, `02010201.u83` crc32 `5f64a20c` 1048576 B, `02010201.u84` crc32 `0920930f` 1048576 B, `02060913_left.u70` crc32 `d6a6713c` 1048576 B, `02060913_right.u83` crc32 `31068c41` 1048576 B, `02061013_left.u70` crc32 `e70a7007` 1048576 B, `02061013_right.u83` crc32 `7a8619a5` 1048576 B, `03010301.u70` crc32 `5ae69121` 1048576 B, `03010301.u71` crc32 `d793440a` 1048576 B, `03010301.u83` crc32 `c8580554` 1048576 B, `03010301.u84` crc32 `a34a9f16` 1048576 B, `03030708_left.u70` crc32 `4e5ad823` 1048576 B, `03030708_right.u83` crc32 `b4b3c6a5` 1048576 B, `03130334_left.u70` crc32 `02175fde` 2097152 B, `03130334_right.u83` crc32 `bce3d97f` 2097152 B, `04010501_left.u70` crc32 `21182775` 1048576 B, `04010501_right.u83` crc32 `3daefb7a` 1048576 B, `04041205_left.u70` crc32 `dfb9a119` 1048576 B, `04041205_right.u83` crc32 `ca6bc86c` 1048576 B, `05010601_left.u70` crc32 `b3e6b4a0` 1048576 B, `05010601_right.u83` crc32 `c12eac11` 1048576 B, `06.02.04_left.u3` crc32 `117b75f2` 1048576 B, `06.02.04_right.u2` crc32 `1cf5a853` 1048576 B, `06.02.20_left.u70` crc32 `199ed3b9` 1048576 B, `06.02.20_right.u83` crc32 `e4001f60` 1048576 B, `06.03.03_left.u3` crc32 `a6924238` 1048576 B, `06.03.03_right.u2` crc32 `98763498` 1048576 B, `06.03.04_left.u3` crc32 `7034f26b` 1048576 B, `06.03.04_right.u2` crc32 `6f5f5ef1` 1048576 B, `07010801_left.u70` crc32 `8e92af68` 2097152 B, `07010801_right.u83` crc32 `8c148c11` 2097152 B, `09011001_left.u70` crc32 `229c2e63` 2097152 B, `09011001_right.u83` crc32 `8a853f80` 2097152 B, `11011501_left.u70` crc32 `c5cc3461` 2097152 B, `11011501_right.u83` crc32 `de4c3aed` 2097152 B, `11011901_left.u70` crc32 `d3dd2210` 2097152 B, `11011901_right.u83` crc32 `73dcb11c` 2097152 B, `13012001_left.u70` crc32 `38e8f659` 2097152 B, `13012001_right.u83` crc32 `e627dbfa` 2097152 B, `14010152_left.u70` crc32 `2716e1ef` 2097152 B, `14010152_right.u83` crc32 `7e3f61f6` 2097152 B, `14011605_left.u70` crc32 `cd26d4f0` 2097152 B, `14011605_right.u83` crc32 `2bec5b74` 2097152 B, `14011913_left.u70` crc32 `9a4cefdf` 2097152 B, `14011913_right.u83` crc32 `01d13b89` 2097152 B, `15011025_left.u70` crc32 `c02e14b0` 2097152 B, `15011025_right.u83` crc32 `bf21a975` 2097152 B, `19012801_left.u70` crc32 `b03bd17c` 2097152 B, `19012801_right.u83` crc32 `5b20a96c` 2097152 B, `20012305_left.u70` crc32 `ea8961cc` 2097152 B, `20012305_right.u83` crc32 `e436c1f5` 2097152 B, `20012605_left.u70` crc32 `87331111` 2097152 B, `20012605_right.u83` crc32 `045b82ad` 2097152 B, `21012901_left.u70` crc32 `0d271470` 2097152 B, `21012901_right.u83` crc32 `757618f2` 2097152 B, `24010467_left.u70` crc32 `9093d820` 2097152 B, `24010467_right.u83` crc32 `eddeff13` 2097152 B, `24013001_left.u70` crc32 `06ae7e07` 2097152 B, `24013001_right.u83` crc32 `e97afedf` 2097152 B, `25012805_left.u70` crc32 `996f32ce` 2097152 B, `25012805_right.u83` crc32 `2ecd8da8` 2097152 B, `gambs2m_01.05.07.u3` crc32 `58730ed3` 131072 B | `mame`: `src/mame/aristocrat/aristmk6.cpp:6639`<br>`mame2016`: `src/mame/drivers/aristmk6.cpp:2922` |
| `bios/aristmk7.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mk7-bios_590176_01_1.00.7.u1` crc32 `f2fea07e` 1048576 B | `mame`: `src/mame/aristocrat/aristmk7.cpp:233` |
| `bios/atarisy1.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/fbneo`, `libretro/mame`: `136032.101.e3` crc32 `7e84972a` 256 B, `136032.101.e3` crc32 `7e84972a` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.102.e5` crc32 `ebf1e0ae` 256 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.103.f7` crc32 `92d6a0b4` 235 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.104.f5` crc32 `7a29dc07` 8192 B, `136032.105.l13` crc32 `79021d3c` 16384 B, `136032.106.l12` crc32 `76ee86c4` 16384 B, `136032.107.b2` crc32 `7a29dc07` 8192 B, `136032.114.j11` crc32 `195c54ad` 16384 B, `136032.115.j10` crc32 `7275b4dc` 16384 B, `136032.205.l13` crc32 `88d0be26` 16384 B, `136032.206.l12` crc32 `3c79ef05` 16384 B | `mame`: `src/mame/atari/atarisy1.cpp:2669`<br>`mame2010`: `src/mame/drivers/atarisy1.c`<br>`mame2016`: `src/mame/drivers/atarisy1.cpp:2483` |
| `bios/awbios.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `bios0.ic23` crc32 `719b2b0b` 131072 B, `bios1.ic23` crc32 `d3e80a9f` 131072 B, `fpr-24363.ic48` crc32 `82a105f0` 131072 B | `mame`: `src/mame/sega/dc_atomiswave.cpp:1480`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9788` |
| `bios/bubsys.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/fbneo`, `libretro/mame`: `400a1.2b` crc32 `5827b1e8` 256 B, `400a2.1b` crc32 `2f44f970` 256 B, `400b03.8g` crc32 `85c2afc5` 8192 B, `boot.bin` crc32 `f0774fc2` 480 B | `mame`: `src/mame/konami/nemesis.cpp:3140`<br>`mame2016`: `src/mame/drivers/nemesis.cpp:2720` |
| `bios/cedmag.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `magnet-master-vid-e03.bin` crc32 `86c4a4f0` 8192 B | `mame`: `src/mame/efo/cedar_magnet.cpp:1166` |
| `bios/chihiro.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `315-6351a_epc1pc8.ic2` crc32 `488624ea` 130817 B, `chihiro_xbox_bios.bin` crc32 `66232714` 524288 B, `fpr-23887_29lv160te.ic4` crc32 `13034372` 2097152 B, `fpr21042_m29w160et.bin` crc32 `a4fcab0b` 2097152 B, `ic10_g24lc64.bin` crc32 `cfc5e06f` 8192 B, `ic11_24lc024.bin` crc32 `8dc8374e` 128 B, `pc20_g24lc64.bin` crc32 `7742ab62` 8192 B | `mame`: `src/mame/sega/chihiro.cpp:2614`<br>`mame2010`: `src/mame/drivers/chihiro.c`<br>`mame2016`: `src/mame/drivers/chihiro.cpp:1487` |
| `bios/coh1000a.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ac01.ic652` crc32 `c425eaf5` 8 B, `m534002c-15.ic353` crc32 `8d8d0764` 524288 B | `mame`: `src/mame/sony/zn.cpp:6015` |
| `bios/coh1000c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp01.ic652` crc32 `bb7ba033` 8 B, `m534002c-13.ic353` crc32 `50033af6` 524288 B, `msm27c402zb.ic353` crc32 `f20f7fe5` 524288 B | `mame`: `src/mame/sony/zn.cpp:5969` |
| `bios/coh1000t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `m534002c-14.ic353` crc32 `e3f23b6e` 524288 B, `tt01.ic652` crc32 `daac13e7` 8 B | `mame`: `src/mame/sony/zn.cpp:5989` |
| `bios/coh1000w.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `msm27c402zb.ic353` crc32 `45e8a4b4` 524288 B, `tw01.ic652` crc32 `1cac067e` 8 B | `mame`: `src/mame/sony/zn.cpp:6024` |
| `bios/coh1001l.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `at01.ic652` crc32 `181668b3` 8 B, `m534002c-17.ic353` crc32 `6721146b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6028` |
| `bios/coh1002e.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `et01.ic652` crc32 `a7dd922e` 8 B, `m27c402cz-54.ic353` crc32 `910f3a8b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6038` |
| `bios/coh1002m.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `m534002c-61.ic353` crc32 `69ffbcb4` 524288 B, `mg01.ic652` crc32 `50dc8322` 8 B | `mame`: `src/mame/sony/zn.cpp:6089` |
| `bios/coh1002t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `flash.u30` crc32 `9100dbf2` 2097152 B, `m534002c-14.ic353` crc32 `e3f23b6e` 524288 B, `tt01.ic652` crc32 `daac13e7` 8 B, `tt99.u17` crc32 `2989b6eb` 8 B | `mame`: `src/mame/sony/taitogn.cpp:1342` |
| `bios/coh1002v.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `coh-1002v.ic353` crc32 `5ff165f3` 524288 B, `kn01.ic652` crc32 `1ce271df` 8 B | `mame`: `src/mame/sony/zn.cpp:6032` |
| `bios/coh3002c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp10.ic652` crc32 `7808fcef` 8 B, `m534002c-59.ic353` crc32 `e860ea8b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6055` |
| `bios/coh3002t.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `f35-01_m27c800.bin` crc32 `6225ec11` 1048576 B, `f35-01_m27c800_modbios_mb-2009_dump_by_aje_fr.bin` crc32 `6367aedf` 1048576 B, `f35-01_m27c800_modbios_mb-2011.bin` crc32 `f98ae118` 1048576 B, `f35-01_m27c800_v1.bin` crc32 `cd15cc30` 1048576 B, `flash.u30` crc32 `c48c8236` 2097152 B, `m534002c-60.ic353` crc32 `03967fa7` 524288 B, `tt10.ic652` crc32 `235510b1` 8 B, `tt16.u17` crc32 `6bb167b3` 8 B | `mame`: `src/mame/sony/taitogn.cpp:1341` |
| `bios/crysbios.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `mx27l1000-alt.u14` crc32 `1e8175c8` 131072 B, `mx27l1000.u14` crc32 `beff39a9` 131072 B | `mame`: `src/mame/misc/crystal.cpp:845`<br>`mame2010`: `src/mame/drivers/crystal.c`<br>`mame2016`: `src/mame/drivers/crystal.cpp:1415` |
| `bios/cubo.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `391640-03.u6a` crc32 `d3837ae4` 1048576 B | `mame`: `src/mame/amiga/cubo.cpp:1435`<br>`mame2016`: `src/mame/drivers/cubo.cpp:1365` |
| `bios/cvs.zip` | yes | `libretro/mame2003`, `libretro/mame2003_plus` | zip, for `libretro/mame2003`, `libretro/mame2003_plus`: `5b.bin` crc32 `f055a624` 2048 B, `82s123.10k` crc32 `b5221cec` 32 B, `82s185.10h` crc32 `c205bca6` 2048 B | `mame2003`: `src/drivers/cvs.c:475-482,1350`<br>`mame2003_plus`: `src/drivers/cvs.c:475-482,1393` |
| `bios/ddrfammt.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara_ddr_ha010_81947.bin` crc32 `737d5d1a` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2922` |
| `bios/decocass.zip` | yes | `libretro/mame`, `libretro/mame2000`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/fbneo`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B<br>zip, for `libretro/mame`: `cassmcu.1c` crc32 `a6df18fd` 1024 B, `dsp-3_p0-a.m9` crc32 `2541e34b` 2048 B, `dsp-3_p0-b.m9` crc32 `b67a91d9` 2048 B, `dsp-3_p0-c.m9` crc32 `c76c4057` 2048 B, `dsp-3_p0-d.m9` crc32 `4b7d72bc` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `dsp-3_p1-.l9` crc32 `3bfff5f3` 2048 B, `v0a-.7e` crc32 `3d33ac34` 4096 B, `v0b-.7e` crc32 `23d929b7` 4096 B, `v0c-.7e` crc32 `9f505709` 4096 B, `v0d-.7e` crc32 `1e0c22b1` 4096 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v1-.5a` crc32 `b66b2c2a` 2048 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v2.3m` crc32 `238fdb40` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v3.3j` crc32 `51eef657` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B, `v4.10d` crc32 `3b5836b4` 32 B<br>zip, for `libretro/mame2000`: `rms8.cpu` crc32 `23d929b7` 4096 B, `rms8.snd` crc32 `b66b2c2a` 2048 B | `mame`: `src/mame/dataeast/decocass.cpp:2131`<br>`mame2000`: `src/drivers/btime.c:1996-2005`<br>`mame2003`: `src/drivers/decocass.c:1086`<br>`mame2003_plus`: `src/drivers/decocass.c:696-727`<br>`mame2010`: `src/mame/drivers/decocass.c`<br>`mame2016`: `src/mame/drivers/decocass.cpp:1653` |
| `bios/ekara.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara.bin` crc32 `9b27c4a2` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2904` |
| `bios/ekaraa.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekara2.bin` crc32 `3c92d48d` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2905` |
| `bios/ekarag.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaragermany.bin` crc32 `644f06b0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2907` |
| `bios/ekaraj.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekarajapan.bin` crc32 `e459e43b` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2906` |
| `bios/ekaramix.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaramix.bin` crc32 `ee71576e` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2919` |
| `bios/ekaraphs.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaraheadset.bin` crc32 `dd9b3cd7` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2913` |
| `bios/ekaras.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekaraspanish.bin` crc32 `77925ab0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2908` |
| `bios/epitch.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ekarajapan.bin` crc32 `e459e43b` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2916` |
| `bios/evio.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `evio.bin` crc32 `ee22c764` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2939` |
| `bios/f355bios.zip` | no | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-22849.ic27` crc32 `121e009c` 2097152 B, `epr-22850.ic27` crc32 `28aa539d` 2097152 B, `epr-22851.ic27` crc32 `62483677` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10920`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9467` |
| `bios/f355dlx.zip` | no | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-21862.ic27` crc32 `1f630716` 2097152 B, `epr-21862p.ic27` crc32 `f0cd4aaa` 2097152 B, `epr-21863.ic27` crc32 `0615a4d1` 2097152 B, `epr-21864.ic27` crc32 `12ed7c66` 2097152 B, `epr-21864p.ic27` crc32 `05128861` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10919`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9466` |
| `bios/galgames.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `galaxy_u1__v1.71_7-8-98.u1` crc32 `604415c8` 1048576 B, `galaxy_u1__v1.80_10-15-98.u1` crc32 `e3ae423c` 1048576 B, `galaxy_u1__v1.90_12-01-98.u1` crc32 `c6d7bc6d` 1048576 B, `galaxy_u2__v1.71_7-8-98.u2` crc32 `d1c3dbb3` 1048576 B, `galaxy_u2__v1.80_10-15-98.u2` crc32 `73cff284` 1048576 B, `galaxy_u2__v1.90_12-01-98.u2` crc32 `e51ff184` 1048576 B | `mame`: `src/mame/ces/galgames.cpp:1301` |
| `bios/gcslottv.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `sammyslotunit.bin` crc32 `2ba6f3ab` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2946` |
| `bios/gp_110.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `a-110.u12` crc32 `ed0d518b` 2048 B, `b1-110.u13` crc32 `a223f2e8` 2048 B | `mame`: `src/mame/pinball/gp_1.cpp:575`<br>`mame2016`: `src/mame/drivers/gp_1.cpp:531` |
| `bios/gq863.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `863a03.7b` crc32 `81498f73` 524288 B, `863a05.2x` crc32 `6f42a09e` 524288 B | `mame`: `src/mame/konami/twinkle.cpp:1643`<br>`mame2010`: `src/mame/drivers/twinkle.c`<br>`mame2016`: `src/mame/drivers/twinkle.cpp:1334` |
| `bios/gts1.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `a1752cf.u5` crc32 `614a3bd9` 2048 B, `a1753ce.u4` crc32 `4cd312dd` 2048 B | `mame`: `src/mame/pinball/gts1.cpp:1070`<br>`mame2016`: `src/mame/drivers/gts1.cpp:978` |
| `bios/hikara.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `hikara.u3` crc32 `6b91102a` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2951` |
| `bios/hikaru.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | `aac601811a25d7b31a3d5f3f1f82f338` no region; RetroBat's list<br>zip, for `libretro/mame`: `93c46.ic115` crc32 `737dc714` 128 B, `epr-21904.ic94` crc32 `d96298b6` 2097152 B, `epr-23400.ic94` crc32 `3d557104` 2097152 B, `epr-23400a.ic94` crc32 `2aa906a7` 2097152 B, `prot_bot.ic94` crc32 `7cbf2fb6` 2097152 B | `mame`: `src/mame/sega/hikaru.cpp:1482`<br>`mame2010`: `src/mame/drivers/hikaru.c`<br>`mame2016`: `src/mame/drivers/hikaru.cpp:752` |
| `bios/hng64.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `bios_export.bin` crc32 `bbf07ec6` 524288 B, `bios_korea.bin` crc32 `ac953e2e` 524288 B, `bios_us.bin` crc32 `ab5948d6` 524288 B, `brom1.bin` crc32 `a30dd3de` 524288 B, `from1.bin` crc32 `6b933005` 524288 B, `lvs-igx-br9020f.u3` crc32 `af9f4287` 256 B, `lvs-ioj-br9020f.u2` crc32 `78b7020d` 256 B, `lvs-jam-br9020f.u3` crc32 `dabec5d2` 256 B, `rom1.bin` crc32 `4a6832dc` 130866 B, `tmp87ph40an.bin` crc32 `b70df21f` 32768 B | `mame`: `src/mame/snk/hng64.cpp:3238`<br>`mame2010`: `src/mame/drivers/hng64.c`<br>`mame2016`: `src/mame/drivers/hng64.cpp:1994` |
| `bios/hod2bios.zip` | no | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-21329.ic27` crc32 `d99e5b9b` 2097152 B, `epr-21330.ic27` crc32 `9e3bfa1b` 2097152 B, `epr-21331.ic27` crc32 `065f8500` 2097152 B, `epr-21332.ic27` crc32 `bd6ce0ec` 2097152 B, `hotd2biosproto.ic27` crc32 `ea74e967` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10918`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9465` |
| `bios/isgsm.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/fbneo`, `libretro/mame`: `ism2006v00.u1` crc32 `2292585c` 131072 B | `mame`: `src/mame/sega/segas16b_isgsm.cpp:608`<br>`mame2016`: `src/mame/drivers/segas16b.cpp:9463` |
| `bios/isinger.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `kr_isinger.bin` crc32 `6ad588fe` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2910` |
| `bios/iteagle.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `17s20lpc_sb4.u26` crc32 `62c4af8a` 32768 B, `17s20lpc_sb5.u26` crc32 `c88b9d42` 32768 B, `17s50a_red1.u26` crc32 `f5cf3187` 131072 B, `bbh_u15_v102.u15` crc32 `1fd39e73` 1048576 B, `e2-card1.u22.jed` crc32 `9d1e1ace` 3025 B, `e2-res3.u117.jed` crc32 `4f1ff45a` 3025 B, `eagle_u15_v201.u15` crc32 `e180442b` 1048576 B, `eagle_u15_v204.u15` crc32 `f02e5523` 1048576 B, `eagle_u15_v208.u15` crc32 `772f2864` 1048576 B, `eagle_u15_v209.u15` crc32 `e0fc1a16` 1048576 B, `golf_fore_u15_v101.u15` crc32 `2600bc2b` 1048576 B, `golf_fore_u15_v102.u15` crc32 `4c000c5e` 1048576 B, `golf_fore_u15_v103.u15` crc32 `c35f4cf2` 1048576 B, `golf_fore_u15_v105.u15` crc32 `3870dbe0` 1048576 B, `golf_fore_u15_v106.u15` crc32 `56bc193d` 1048576 B, `golf_fore_u15_v106a.u15` crc32 `9c79b7ad` 1048576 B, `golf_fore_u15_v107.u15` crc32 `97a01fc9` 1048576 B | `mame`: `src/mame/itech/iteagle.cpp:756`<br>`mame2016`: `src/mame/drivers/iteagle.cpp:585` |
| `bios/jpopira.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `i2cmem.bin` crc32 `70a05af1` 256 B, `jumpingpopira.bin` crc32 `a7bedbd2` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2936` |
| `bios/konamigv.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `999a01.7e` crc32 `ad498d2d` 524288 B | `mame`: `src/mame/konami/konamigv.cpp:1447`<br>`mame2010`: `src/mame/drivers/konamigv.c`<br>`mame2016`: `src/mame/drivers/konamigv.cpp:848` |
| `bios/konamigx.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `300a01.34k` crc32 `d5fa95f5` 131072 B | `mame`: `src/mame/konami/konamigx.cpp:4191`<br>`mame2003`: `src/drivers/konamigx.c:3369`<br>`mame2003_plus`: `src/drivers/konamigx.c:2404`<br>`mame2010`: `src/mame/drivers/konamigx.c`<br>`mame2016`: `src/mame/drivers/konamigx.cpp:3928` |
| `bios/konendev.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `2v02s502_ifu.u190` crc32 `36122a98` 524288 B | `mame`: `src/mame/konami/konendev.cpp:909` |
| `bios/kpython.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `b22a01.u42` crc32 `98de405e` 524288 B | `mame`: `src/mame/konami/kpython.cpp:376` |
| `bios/kpython2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `ps2-0190j-20030822.bin` crc32 `79d60546` 4194304 B | `mame`: `src/mame/konami/kpython2.cpp:1918` |
| `bios/kviper.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `941a01.u25` crc32 `df6f88d6` 262144 B, `941b01.u25` crc32 `233e5159` 262144 B, `ds2430.u3` crc32 `f1511505` 40 B | `mame`: `src/mame/konami/viper.cpp:3213`<br>`mame2010`: `src/mame/drivers/viper.c`<br>`mame2016`: `src/mame/drivers/viper.cpp:2811` |
| `bios/lindbios.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `6.0.0009.bin` crc32 `5ffdfbf8` 1048576 B, `6.0.0010.bin` crc32 `ea2bf888` 1048576 B, `6.0.0010a.bin` crc32 `10dd9b76` 1048576 B, `fpr-24370b.ic6` crc32 `c3b021a4` 4194304 B, `vid_bios.u504` crc32 `f78d14d7` 65536 B | `mame`: `src/mame/sega/lindbergh.cpp:639`<br>`mame2016`: `src/mame/drivers/lindbergh.cpp:585` |
| `bios/macsbios.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `am_macs_os_hi.u44` crc32 `538b68e4` 524288 B, `am_macs_os_low.u43` crc32 `0b5aed5e` 524288 B, `sx011-01.bin` crc32 `669096c1` 343 B | `mame`: `src/mame/seta/macs.cpp:775`<br>`mame2010`: `src/mame/drivers/macs.c`<br>`mame2016`: `src/mame/drivers/macs.cpp:770` |
| `bios/maxaflex.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `atarixl.rom` crc32 `1f9cd270` 16384 B, `maxaflex.uc` crc32 `fe9cf53c` 2048 B, `maxprom.prm` crc32 `edf5c950` 512 B | `mame`: `src/mame/atari/maxaflex.cpp:447`<br>`mame2010`: `src/mame/drivers/maxaflex.c`<br>`mame2016`: `src/mame/drivers/maxaflex.cpp:538` |
| `bios/megaplay.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `ep15294.ic2` crc32 `aa8dc2d8` 131072 B, `epr-a15294.ic2` crc32 `f97c68aa` 131072 B | `mame`: `src/mame/sega/megaplay.cpp:1018`<br>`mame2003`: `src/drivers/segac2.c:4468`<br>`mame2003_plus`: `src/drivers/segac2.c:5794-5804`<br>`mame2010`: `src/mame/drivers/megaplay.c`<br>`mame2016`: `src/mame/drivers/megaplay.cpp:944` |
| `bios/megatech.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-12263a.20` crc32 `07c3f423` 32768 B, `epr-12263b.20` crc32 `ca26c87a` 32768 B, `epr-12604a.20` crc32 `884e4aa5` 32768 B, `epr-12664.20` crc32 `f71e9526` 32768 B | `mame`: `src/mame/sega/megatech.cpp:1430`<br>`mame2003`: `src/drivers/segac2.c:4390`<br>`mame2003_plus`: `src/drivers/segac2.c:5434-5437`<br>`mame2010`: `src/mame/drivers/megatech.c`<br>`mame2016`: `src/mame/drivers/megatech.cpp:1411` |
| `bios/miuchiz.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `otp.dat` crc32 `2ff7ec96` 16384 B | `mame`: `src/mame/misc/miuchiz.cpp:227` |
| `bios/naomi.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `315-6188.ic31` crc32 `7c9fea46` 8244 B, `boot_rom_64b8.ic606` crc32 `7a50fab9` 524288 B, `develop.ic27` crc32 `309a196a` 2097152 B, `develop110.ic27` crc32 `de7cfdb0` 2097152 B, `epr-21576.ic27` crc32 `9dad3495` 2097152 B, `epr-21576a.ic27` crc32 `cedfe439` 2097152 B, `epr-21576b.ic27` crc32 `755a6e07` 2097152 B, `epr-21576d.ic27` crc32 `3b2afa7b` 2097152 B, `epr-21576e.ic27` crc32 `08c0add7` 2097152 B, `epr-21576g.ic27` crc32 `d2a1c6bf` 2097152 B, `epr-21576h.ic27` crc32 `d4895685` 2097152 B, `epr-21576h_multi.ic27` crc32 `cce01f1f` 2097152 B, `epr-21577a.ic27` crc32 `969dc491` 2097152 B, `epr-21577d.ic27` crc32 `60ddcbbe` 2097152 B, `epr-21577e.ic27` crc32 `cf36e97b` 2097152 B, `epr-21577g.ic27` crc32 `25f64af7` 2097152 B, `epr-21577h.ic27` crc32 `fdf17452` 2097152 B, `epr-21578a.ic27` crc32 `6c9aad83` 2097152 B, `epr-21578d.ic27` crc32 `dfd5f42a` 2097152 B, `epr-21578e.ic27` crc32 `087f09a3` 2097152 B, `epr-21578f.ic27` crc32 `628a27fd` 2097152 B, `epr-21578g.ic27` crc32 `55413214` 2097152 B, `epr-21578h.ic27` crc32 `7b452946` 2097152 B, `epr-21579.ic27` crc32 `71f9c918` 2097152 B, `epr-21579d.ic27` crc32 `33513691` 2097152 B, `epr-21580a.ic27` crc32 `46dbe90f` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `naomi_boot_2491.ic27` crc32 `5b28f868` 2097152 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B, `zukinver0930.ic25` crc32 `58e17c23` 2097152 B | `mame`: `src/mame/sega/naomi.cpp:10917`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9464` |
| `bios/naomi2.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-23605.ic27` crc32 `5731e446` 2097152 B, `epr-23605a.ic27` crc32 `7bc3fc2d` 2097152 B, `epr-23605b.ic27` crc32 `3a3242d4` 2097152 B, `epr-23605c.ic27` crc32 `297ea6ed` 2097152 B, `epr-23605c_multi.ic27` crc32 `353fdbcd` 2097152 B, `epr-23607.ic27` crc32 `2b55add2` 2097152 B, `epr-23607b.ic27` crc32 `f308c5e9` 2097152 B, `epr-23607c.ic27` crc32 `750e254b` 2097152 B, `epr-23608.ic27` crc32 `929cc3a6` 2097152 B, `epr-23608a.ic27` crc32 `e8f884d1` 2097152 B, `epr-23608b.ic27` crc32 `a554b1e3` 2097152 B, `epr-23608c.ic27` crc32 `6ef1dd8e` 2097152 B, `epr-23609b.ic27` crc32 `99e3751f` 2097152 B, `epr-23610b.ic27` crc32 `de8e1267` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10922`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9469` |
| `bios/naomigd.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-21576e.ic27` crc32 `08c0add7` 2097152 B, `epr-21576g.ic27` crc32 `d2a1c6bf` 2097152 B, `epr-21576h.ic27` crc32 `d4895685` 2097152 B, `epr-21577e.ic27` crc32 `cf36e97b` 2097152 B, `epr-21577g.ic27` crc32 `25f64af7` 2097152 B, `epr-21577h.ic27` crc32 `fdf17452` 2097152 B, `epr-21578e.ic27` crc32 `087f09a3` 2097152 B, `epr-21578g.ic27` crc32 `55413214` 2097152 B, `epr-21578h.ic27` crc32 `7b452946` 2097152 B, `main_eeprom.bin` crc32 `fea29cbb` 128 B, `x76f100_eeprom.bin` crc32 `3ea24b6a` 132 B | `mame`: `src/mame/sega/naomi.cpp:10923`<br>`mame2010`: `src/mame/drivers/naomi.c`<br>`mame2016`: `src/mame/drivers/naomi.cpp:9470` |
| `bios/neogeo.zip` | yes | `libretro/mame`, `libretro/mame2000`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267`<br>`mame`: `src/mame/snk/neogeo.cpp:2428`<br>`mame2000`: `src/drivers/neogeo.c:4499-4508`<br>`mame2003`: `src/drivers/neogeo.c:1544-1556,5661-5675`<br>`mame2003_plus`: `src/drivers/neogeo.c:1677-1714`<br>`mame2010`: `src/mame/drivers/neodrvr.c:565-614`<br>`mame2016`: `src/mame/drivers/neogeo.cpp:2033` |
| `bios/nichidvd.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `va1a101.u2` crc32 `36135792` 131072 B, `va1b101.u2` crc32 `b92a83c8` 131072 B, `va1b102.u2` crc32 `67374715` 131072 B | `mame`: `src/mame/nichibutsu/hrdvd.cpp:1012` |
| `bios/nss.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `nss-c.ic14` crc32 `a8e202b3` 32768 B, `nss-ic14.02.ic14` crc32 `e06cb58f` 32768 B, `nss-v3.ic14` crc32 `ac385b53` 32768 B | `mame`: `src/mame/nintendo/nss.cpp:1099`<br>`mame2003`: `src/drivers/nss.c:406`<br>`mame2003_plus`: `src/drivers/nss.c:260-269`<br>`mame2010`: `src/mame/drivers/nss.c`<br>`mame2016`: `src/mame/drivers/nss.cpp:1062` |
| `bios/pgm.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/fbneo`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B<br>zip, for `libretro/mame`: `pgm_m01s.rom` crc32 `45ae7159` 2097152 B, `pgm_p01s.u20` crc32 `e42b166e` 131072 B, `pgm_p02s.u20` crc32 `78c15fa2` 131072 B, `pgm_t01s.rom` crc32 `1a7123a0` 2097152 B | `mame`: `src/mame/igs/pgm.cpp:5545`<br>`mame2003`: `src/drivers/pgm.c:946`<br>`mame2003_plus`: `src/drivers/pgm.c:3459-3468`<br>`mame2010`: `src/mame/drivers/pgm.c:1525-1533`<br>`mame2016`: `src/mame/drivers/pgm.cpp:4518` |
| `bios/playch10.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `pch1-c-6d.82s129an.6d` crc32 `1213ebd4` 256 B, `pch1-c-6e.82s129an.6e` crc32 `a2625c6e` 256 B, `pch1-c-6f.82s129an.6f` crc32 `e5414ca3` 256 B, `pch1-c_8m.8m` crc32 `83ebc7a3` 8192 B, `pch1-c_8p-8p` crc32 `90e1b80c` 8192 B, `pch1-c_8te.8t` crc32 `123ffa37` 16384 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8k.8k` crc32 `9acffb30` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8m_e-1.8m` crc32 `c1232eee` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8p_e-1.8p` crc32 `30c15e23` 8192 B, `pch1-c__8t_e-1.8t` crc32 `88a8f87e` 16384 B, `pch1-c__8t_e-2.8t` crc32 `d52fa07a` 16384 B, `pck1-c.8t` crc32 `503ee8b1` 16384 B, `pck1-c_fix.8t` crc32 `0be8ceb4` 16384 B, `rp2c0x.pal` crc32 `48de65dc` 192 B | `mame`: `src/mame/nintendo/playch10.cpp:2980`<br>`mame2003`: `src/drivers/playch10.c:1680`<br>`mame2003_plus`: `src/drivers/playch10.c:815-827`<br>`mame2010`: `src/mame/drivers/playch10.c`<br>`mame2016`: `src/mame/drivers/playch10.cpp:1660` |
| `bios/popira.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `popira.bin` crc32 `71d20afc` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2925` |
| `bios/popira2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `popira2.bin` crc32 `6acd8be0` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2930` |
| `bios/popirak.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `kr_main.u2` crc32 `785d8e21` 1048576 B | `mame`: `src/mame/tvgames/xavix.cpp:2927` |
| `bios/pumpitup.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `mk3_1.0_bios.u22` crc32 `a2b5546b` 131072 B, `mk3_1.1_bios.u22` crc32 `4540c23f` 131072 B, `piu10.u8` crc32 `5911e31a` 2097152 B, `piu10.u9` crc32 `9c436cfa` 2097152 B | `mame`: `src/mame/misc/xtom3d.cpp:997` |
| `bios/qsound.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `dl-1425.bin` crc32 `d6cf5ef5` 24576 B | `mame`: `src/devices/sound/qsound.cpp:121-125` |
| `bios/recel.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `a2361.b1` crc32 `d0c4695d` 1024 B, `a2362.b2` crc32 `39a70611` 1024 B | `mame`: `src/mame/pinball/recel.cpp:546` |
| `bios/sammymdl.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `mt201l04.u012` crc32 `c8c6d25f` 524288 B, `vm1201l01.u012` crc32 `21b2ccbd` 524288 B, `vm1211l01.u2` crc32 `c3c74dc5` 524288 B | `mame`: `src/mame/sigma/sammymdl.cpp:1025`<br>`mame2016`: `src/mame/drivers/sigmab98.cpp:2937` |
| `bios/segasp.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `epr-24236a.ic50` crc32 `ca7df0de` 2097152 B, `epr-24328.ic50` crc32 `25f2ef00` 2097152 B, `epr-24328a.ic50` crc32 `03ec3805` 2097152 B, `fpr-24208a.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24329.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24407.ic72` crc32 `a738ea1c` 2097152 B, `fpr-24407_123.ic72` crc32 `a738ea1c` 2097152 B, `mb_eeprom_exp.ic54s` crc32 `947ddfad` 128 B, `mb_eeprom_us.ic54s` crc32 `4186d7ab` 128 B, `mb_serial.ic57` crc32 `e1e3c009` 128 B, `net_eeprom.ic74s` crc32 `77cc5a6c` 512 B, `net_firm_119.ic72` crc32 `a738ea1c` 2097152 B | `mame`: `src/mame/sega/segasp.cpp:1273`<br>`mame2016`: `src/mame/drivers/segasp.cpp:540` |
| `bios/sfcbox.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `atrom-4s-0.rom5` crc32 `ad3ec05c` 524288 B, `krom1.ic1` crc32 `c9010002` 65536 B, `krom2.00.ic1` crc32 `e31b5580` 131072 B | `mame`: `src/mame/nintendo/sfcbox.cpp:586`<br>`mame2010`: `src/mame/drivers/sfcbox.c`<br>`mame2016`: `src/mame/drivers/sfcbox.cpp:574` |
| `bios/shtzone.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `epr10894a.20` crc32 `ea8901d9` 16384 B | `mame`: `src/mame/sega/shtzone.cpp:376`<br>`mame2016`: `src/mame/drivers/shtzone.cpp:123` |
| `bios/skns.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/fbneo`: `sknsj1.u10` crc32 `7e2b836c` 524288 B<br>zip, for `libretro/mame`: `sknsa1.u10` crc32 `745e5212` 524288 B, `sknse2.u10` crc32 `e2b9d7d1` 524288 B, `sknsj1.u10` crc32 `7e2b836c` 524288 B, `sknsk1.u10` crc32 `ff1c9f79` 524288 B, `sknsu1.u10` crc32 `384d21ec` 524288 B, `supernova-modbios-korea.u10` crc32 `1d90517c` 524288 B, `supernova_modbios-japan.u10` crc32 `b8d3190c` 524288 B | `mame`: `src/mame/kaneko/suprnova.cpp:2135`<br>`mame2003`: `src/drivers/suprnova.c:1668`<br>`mame2003_plus`: `src/drivers/suprnova.c:1226-1229`<br>`mame2010`: `src/mame/drivers/suprnova.c`<br>`mame2016`: `src/mame/drivers/suprnova.cpp:1785` |
| `bios/slvrball.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `bios29.bin` crc32 `ddbd94f4` 131072 B, `bios30.bin` crc32 `c6e30fb1` 262144 B, `bios31.bin` crc32 `5121b267` 262144 B, `bios32.bin` crc32 `63318d8a` 262144 B, `bios33.bin` crc32 `96658274` 262144 B, `bios34.bin` crc32 `400702fe` 262144 B, `bios35.bin` crc32 `d5a2f966` 262144 B, `bios36.bin` crc32 `83ca05e9` 262144 B, `bios37.bin` crc32 `9f35e2d2` 262144 B, `bios38.bin` crc32 `cb49085b` 262144 B, `bios39.bin` crc32 `e7541a28` 262144 B, `bios40.bin` crc32 `c5ed7d4f` 262144 B, `bios41.bin` crc32 `0940aebd` 262144 B, `bios42.bin` crc32 `820c057e` 262144 B, `bios44.bin` crc32 `a4b2703e` 262144 B, `bios45.bin` crc32 `790d5db0` 262144 B, `bios46.bin` crc32 `90074f2b` 262144 B, `bios47.bin` crc32 `248910ed` 262144 B, `test.bin` crc32 `ddbd94f4` 131072 B | `mame`: `src/mame/misc/silverball.cpp:259` |
| `bios/stvbios.zip` | yes | `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/kronos`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B<br>zip, for `libretro/mame`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B, `stvbios.nv` crc32 `15432ae2` 128 B | `mame`: `src/mame/sega/stv.cpp:3948`<br>`mame2003`: `src/drivers/stv.c:4597`<br>`mame2003_plus`: `src/drivers/stv.c:3741-3767`<br>`mame2010`: `src/mame/drivers/stv.c`<br>`mame2016`: `src/mame/drivers/stv.cpp:3404` |
| `bios/su2000.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `amibios_486dx_isa_bios.u32` crc32 `811d3639` 65536 B, `hmc86304_bios_v208.u7` crc32 `c6c32f1a` 32768 B, `ver_151_03_u16.u16` crc32 `8354d059` 131072 B, `ver_151_03_u17.u17` crc32 `ace4081d` 131072 B, `wfc062_212.u62` crc32 `9a6b553a` 32768 B, `wi01p006.u6` crc32 `528142eb` 2194 B, `wi01p026.u26` crc32 `528142eb` 2194 B, `wi01p044.u44` crc32 `a2ad9cf8` 2194 B, `wi01p045.u45` crc32 `e2a13df7` 2194 B, `wi01p046.u46` crc32 `4c22fba5` 2194 B, `wi01p048.u48` crc32 `b9958131` 2706 B, `wi01p049.u49` crc32 `820d245a` 2706 B, `wi01p050.u50` crc32 `7cb0931f` 2194 B, `wi01p053.u53` crc32 `d2eb2493` 2194 B, `wi01p054.u54` crc32 `96d3506e` 2194 B, `wi01p056.u56` crc32 `8bf57db4` 2194 B, `wi01p058.u58` crc32 `882f6405` 2194 B, `wi01p067.u67` crc32 `a97cff8e` 2194 B, `wi01p070.u70` crc32 `4322291e` 2706 B, `wi01p072.u72` crc32 `fe77b66b` 2706 B, `wi01p073.u73` crc32 `e02fe7c1` 2706 B, `wi01p074.u74` crc32 `fca2823c` 2706 B, `wi01p075.u75` crc32 `3d1adb17` 2194 B, `wi01p079.u79` crc32 `3d3ad152` 2194 B, `wi01p080.u80` crc32 `657a4495` 2194 B, `wi01p083.u83` crc32 `902731aa` 2706 B, `wi01p092.u92` crc32 `f996657e` 2706 B, `wi02p000.u23` crc32 `bab34c4b` 2706 B, `wi02p001.u32` crc32 `b438ce94` 2706 B, `wi02p002.u22` crc32 `bab34c4b` 2706 B, `wi02p003.u31` crc32 `b438ce94` 2706 B, `wi02p004.u6` crc32 `bab34c4b` 2706 B, `wi02p005.u14` crc32 `b438ce94` 2706 B, `wi02p006.u5` crc32 `bab34c4b` 2706 B, `wi02p007.u13` crc32 `b438ce94` 2706 B, `wi02p008.u51` crc32 `7c421890` 2194 B, `wi02p009.u49` crc32 `ff63d60c` 2194 B, `wi02p010.u72` crc32 `68f48053` 2194 B, `wi02p011.u88` crc32 `78701d0f` 2194 B, `wi02p012.u85` crc32 `1c489ff9` 2194 B, `wi02p013.u86` crc32 `19de958f` 2194 B, `wi02p014.u82` crc32 `845e4d48` 2194 B, `wi02p015.u74` crc32 `a10d1876` 2194 B, `wi02p016.u52` crc32 `d84b58c2` 2194 B | `mame`: `src/mame/misc/su2000.cpp:302`<br>`mame2016`: `src/mame/drivers/su2000.cpp:269` |
| `bios/sys246.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `r27v1602f.7d` crc32 `2b2e41a2` 2097152 B | `mame`: `src/mame/namco/namcops2.cpp:1716`<br>`mame2010`: `src/mame/drivers/namcops2.c`<br>`mame2016`: `src/mame/drivers/namcops2.cpp:1582` |
| `bios/sys256.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `r27v1602f.8g` crc32 `b2a8eeb6` 2097152 B | `mame`: `src/mame/namco/namcops2.cpp:1761`<br>`mame2010`: `src/mame/drivers/namcops2.c`<br>`mame2016`: `src/mame/drivers/namcops2.cpp:1622` |
| `bios/sys573.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `700a01,gchgchmp.22g` crc32 `39ebb0ca` 524288 B, `700a01.22g` crc32 `11812ef8` 524288 B, `700b01.22g` crc32 `6cf852af` 524288 B, `h8a01.bin` crc32 `131e0359` 64 B, `h8a01.bin` crc32 `131e0359` 64 B, `h8b01.bin` crc32 `508b057d` 64 B | `mame`: `src/mame/konami/ksys573.cpp:6415`<br>`mame2010`: `src/mame/drivers/ksys573.c`<br>`mame2016`: `src/mame/drivers/ksys573.cpp:4691` |
| `bios/systemy2.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `system_y2_m25p40.u801` crc32 `2b19f490` 524288 B | `mame`: `src/mame/sega/y2.cpp:141` |
| `bios/taikodp.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `taikodepopira.bin` crc32 `037a8472` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2933` |
| `bios/taitotz.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `e68-04-1.ic5` crc32 `c7c2dc6b` 524288 B, `e68-05-1.ic6` crc32 `6ad9b006` 524288 B | `mame`: `src/mame/taito/taitotz.cpp:3016`<br>`mame2010`: `src/mame/drivers/taitotz.c`<br>`mame2016`: `src/mame/drivers/taitotz.cpp:2918` |
| `bios/tak_daig.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `robottransformer_unit.bin` crc32 `02deddaa` 2097152 B | `mame`: `src/mame/tvgames/xavix.cpp:2944` |
| `bios/tourvis.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `v4-40.ic29` crc32 `ba6290cc` 32768 B, `v4-42.ic29` crc32 `0372606d` 32768 B, `v4-43.ic29` crc32 `88da23f3` 32768 B, `v4-51.ic29` crc32 `2de0b946` 32768 B, `v4-52.ic29` crc32 `ffd7b0fe` 32768 B, `v4-53.ic29` crc32 `bccb53c9` 32768 B, `v4-54.ic29` crc32 `125e7a6e` 32768 B, `v4-55.ic29` crc32 `87cf66c1` 32768 B, `v4-60.ic29` crc32 `1fd27e22` 32768 B, `vt_1.1.bin` crc32 `27abbc36` 32768 B, `vt_2.0.bin` crc32 `36012f88` 32768 B | `mame`: `src/mame/pce/tourvis.cpp:448`<br>`mame2016`: `src/mame/drivers/tourvis.cpp:550` |
| `bios/triforce.zip` | yes | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/mame`: `triforce_bootrom.bin` crc32 `d1883221` 2097152 B | `mame`: `src/mame/sega/triforce.cpp:1310`<br>`mame2010`: `src/mame/drivers/triforce.c`<br>`mame2016`: `src/mame/drivers/triforce.cpp:1027` |
| `bios/v4bios.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `vid.p1` crc32 `e996bc18` 65536 B | `mame`: `src/mame/barcrest/mpu4vid.cpp:8698`<br>`mame2016`: `src/mame/drivers/mpu4vid.cpp:3654` |
| `bios/ym2413.zip` | no | `libretro/mame` | zip, for `libretro/mame`: `ym2413_instruments.bin` crc32 `6f582d01` 144 B | `mame`: `src/devices/sound/ymopl.cpp:218-223` |
| `bios/ym2608.zip` | no | `libretro/mame` | zip, for `libretro/fbneo`, `libretro/mame`: `ym2608_adpcm_rom.bin` crc32 `23c9e0d8` 8192 B | `mame`: `src/devices/sound/ymopn.cpp:76-91` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `airlbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `aleck64.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `alg3do.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `alg_bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `allied.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ar_bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `aristmk5.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `aristmk6.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `aristmk7.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `atarisy1.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `awbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `bios/acpsx.zip` | `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/atluspsx.zip` | `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/atpsx.zip` | `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/bctvidbs.zip` | `libretro/mame2010` | no identity: no source states a hash for it |
| `bios/cd32.zip` | `libretro/mame2010` | no identity: no source states a hash for it |
| `bios/cpzn1.zip` | `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/cpzn2.zip` | `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/galgbios.zip` | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/gts1s.zip` | `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/mac2bios.zip` | `libretro/mame`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/psarc95.zip` | `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/pyson.zip` | `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/sncwgltd.zip` | `libretro/mame2003`, `libretro/mame2003_plus` | no identity: no source states a hash for it |
| `bios/taitofx1.zip` | `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/taitogn.zip` | `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/tps.zip` | `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bios/vspsx.zip` | `libretro/mame2010`, `libretro/mame2016` | no identity: no source states a hash for it |
| `bubsys.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `cedmag.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `chihiro.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1000a.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1000c.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1000t.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1000w.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1001l.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1002e.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1002m.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1002t.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh1002v.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh3002c.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `coh3002t.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `crysbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `cubo.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ddrfammt.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `decocass.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekara.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekaraa.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekarag.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekaraj.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekaramix.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekaraphs.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ekaras.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `epitch.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `evio.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `f355bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `f355dlx.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `galgames.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `galgbios.zip` | `mame64` | mame64 has no cited firmware directory |
| `gcslottv.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `gp_110.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `gq863.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `gts1.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `hikara.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `hikaru.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `hng64.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `hod2bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `isgsm.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `isinger.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `iteagle.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `jpopira.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `konamigv.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `konamigx.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `konendev.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `kpython.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `kpython2.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `kviper.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `lindbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `mac2bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `macsbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `maxaflex.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `megaplay.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `megatech.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `miuchiz.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `naomi.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `naomi2.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `naomigd.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `neogeo.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `nichidvd.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `nss.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `pgm.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `playch10.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `popira.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `popira2.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `popirak.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `pumpitup.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `qsound.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `recel.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `sammymdl.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `segasp.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `sfcbox.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `shtzone.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `skns.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `slvrball.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `stvbios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `su2000.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `sys246.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `sys256.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `sys573.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `systemy2.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `taikodp.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `taitotz.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `tak_daig.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `tourvis.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `triforce.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `v4bios.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ym2413.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |
| `ym2608.zip` | `groovymame`, `mame64` | groovymame has no cited firmware directory; mame64 has no cited firmware directory |

## mastersystem

Master System: 7 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/[BIOS] Sega Master System (Japan) (v2.1).sms` | yes | `bizhawk/SMSHawk`, RetroBat's list | `24a519c53f67b00640d0048ef7089105` japan; BizHawk's FirmwareDatabase.cs, a dump catalog's name |  |
| `bios/[BIOS] Sega Master System (USA, Europe) (v1.3).sms` | yes | `bizhawk/SMSHawk`, RetroBat's list | `840481177270d5642a14ca71ee72844c` north-america, europe; BizHawk's FirmwareDatabase.cs, a dump catalog's name |  |
| `bios/bios.sms` | no | `libretro/gearsystem` | `840481177270d5642a14ca71ee72844c` north-america, europe; RomM's known_bios_files | `gearsystem`: `platforms/libretro/libretro.cpp:736, platforms/libretro/libretro.cpp:731-743 (load_bootroms)` |
| `bios/bios_E.sms` | no | `libretro/genesis_plus_gx` | `840481177270d5642a14ca71ee72844c` europe; RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:514` |
| `bios/bios_J.sms` | no | `libretro/genesis_plus_gx` | `24a519c53f67b00640d0048ef7089105` japan; RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:517` |
| `bios/bios_U.sms` | no | `libretro/genesis_plus_gx` | `840481177270d5642a14ca71ee72844c` north-america; RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:511` |
| `bios/SMS_kr.sms` | yes | `bizhawk/SMSHawk` | `e49e63328c78fe3d24ae32bf903583e0` korea; BizHawk's FirmwareDatabase.cs |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios.sms` | `jgenesis` | jgenesis has no cited firmware directory |
| `bios_E.sms` | `ares/MasterSystem` | ares has no cited firmware directory |
| `bios_J.sms` | `ares/MasterSystem` | ares has no cited firmware directory |
| `bios_U.sms` | `ares/MasterSystem` | ares has no cited firmware directory |
| `emulators/mesen/Firmware/bios.sms` | `mesen` | outside bios/, where RomMBat never writes |

## megacd

Sega CD: 3 slots, 15 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bios_CD_E.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide`, `libretro/picodrive`, RetroBat's list | `e66fa1dc5820d254611fdcdba0662372` europe; RetroBat's list, RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:412`<br>`genesis_plus_gx_wide`: `core/loadrom.c:411`<br>`picodrive`: `platform/libretro/libretro.c:1276` |
| `bios/bios_CD_J.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide`, `libretro/picodrive`, RetroBat's list | `278a9397d192149e84e820ac621a8edd` japan; RetroBat's list, RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:415`<br>`genesis_plus_gx_wide`: `core/loadrom.c:414`<br>`picodrive`: `platform/libretro/libretro.c:1279` |
| `bios/bios_CD_U.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide`, `libretro/picodrive`, RetroBat's list | `854b9150240a198070150e4566ae1290` north-america; RetroBat's list<br>`2efd74e3232ff260e371b99f84024f7f` north-america; RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:409`<br>`genesis_plus_gx_wide`: `core/loadrom.c:408`<br>`picodrive`: `platform/libretro/libretro.c:1273` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/eu_mcd1_9210.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/eu_mcd2_9303.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/eu_mcd2_9306.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/jp_mcd1_9111.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/jp_mcd1_9112.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/jp_mcd2_921222.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/SegaCDBIOS9303.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/us_scd1_9210.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios/us_scd2_9306.bin` | `libretro/picodrive` | no identity: no source states a hash for it |
| `bios_CD_E.bin` | `ares/MegaCD`, `jgenesis` | ares has no cited firmware directory; jgenesis has no cited firmware directory |
| `bios_CD_J.bin` | `ares/MegaCD`, `jgenesis` | ares has no cited firmware directory; jgenesis has no cited firmware directory |
| `bios_CD_U.bin` | `ares/MegaCD`, `jgenesis` | ares has no cited firmware directory; jgenesis has no cited firmware directory |

## megadrive

megadrive: 8 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/areplay.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide` | `a0028b3043f9d59ceeb03da5b073b30d` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/areplay.c:64, libretro/libretro.c:3517`<br>`genesis_plus_gx_wide`: `core/cart_hw/areplay.c:64, libretro/libretro.c:3804` |
| `bios/bios_MD.bin` | yes | `bizhawk/Genplus-gx`, `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide` | `d3293ebaaa7f4eb2a6766b68a0fb4609` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `genesis_plus_gx`: `libretro/libretro.c:3780`<br>`genesis_plus_gx_wide`: `libretro/libretro.c:3939` |
| `bios/ggenie.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide` | `e8af7fe115a75c849f6aab3701e7799b` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/ggenie.c:64, libretro/libretro.c:3516`<br>`genesis_plus_gx_wide`: `core/cart_hw/ggenie.c:64, libretro/libretro.c:3803` |
| `bios/MCD_eu_100.bin` | yes | `bizhawk/Genplus-gx` | `e66fa1dc5820d254611fdcdba0662372` europe; BizHawk's FirmwareDatabase.cs |  |
| `bios/MCD_jp_100p.bin` | yes | `bizhawk/Genplus-gx` | `278a9397d192149e84e820ac621a8edd` japan; BizHawk's FirmwareDatabase.cs |  |
| `bios/SCD_m1_us_110.bin` | yes | `bizhawk/Genplus-gx` | `2efd74e3232ff260e371b99f84024f7f` north-america; BizHawk's FirmwareDatabase.cs |  |
| `bios/sk.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide` | `4ea493ea4e9f6c9ebfccbdb15110367e` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/md_cart.c:867`<br>`genesis_plus_gx_wide`: `core/cart_hw/md_cart.c:750` |
| `bios/sk2chip.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_wide` | `b4e76e416b887f4e7413ba76fa735f16` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/md_cart.c:873`<br>`genesis_plus_gx_wide`: `core/cart_hw/md_cart.c:756` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `rom.db` | `blastem` | blastem has no cited firmware directory |
| `tmss.md` | `blastem` | blastem has no cited firmware directory |

## megadrive-msu

megadrive-msu: 8 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/areplay.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `a0028b3043f9d59ceeb03da5b073b30d` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/areplay.c:64, libretro/libretro.c:3517`<br>`genesis_plus_gx_wide`: `core/cart_hw/areplay.c:64, libretro/libretro.c:3804` |
| `bios/bios_CD_E.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `e66fa1dc5820d254611fdcdba0662372` europe; RetroBat's list, RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:412`<br>`genesis_plus_gx_wide`: `core/loadrom.c:411` |
| `bios/bios_CD_J.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `278a9397d192149e84e820ac621a8edd` japan; RetroBat's list, RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:415`<br>`genesis_plus_gx_wide`: `core/loadrom.c:414` |
| `bios/bios_CD_U.bin` | yes | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `854b9150240a198070150e4566ae1290` north-america; RetroBat's list<br>`2efd74e3232ff260e371b99f84024f7f` north-america; RomM's known_bios_files | `genesis_plus_gx`: `core/loadrom.c:409`<br>`genesis_plus_gx_wide`: `core/loadrom.c:408` |
| `bios/bios_MD.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `d3293ebaaa7f4eb2a6766b68a0fb4609` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `genesis_plus_gx`: `libretro/libretro.c:3780`<br>`genesis_plus_gx_wide`: `libretro/libretro.c:3939` |
| `bios/ggenie.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `e8af7fe115a75c849f6aab3701e7799b` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/ggenie.c:64, libretro/libretro.c:3516`<br>`genesis_plus_gx_wide`: `core/cart_hw/ggenie.c:64, libretro/libretro.c:3803` |
| `bios/sk.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `4ea493ea4e9f6c9ebfccbdb15110367e` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/md_cart.c:867`<br>`genesis_plus_gx_wide`: `core/cart_hw/md_cart.c:750` |
| `bios/sk2chip.bin` | no | `libretro/genesis_plus_gx`, `libretro/genesis_plus_gx_expanded`, `libretro/genesis_plus_gx_wide` | `b4e76e416b887f4e7413ba76fa735f16` no region; RomM's known_bios_files | `genesis_plus_gx`: `core/cart_hw/md_cart.c:873`<br>`genesis_plus_gx_wide`: `core/cart_hw/md_cart.c:756` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `rom.db` | `blastem` | blastem has no cited firmware directory |
| `tmss.md` | `blastem` | blastem has no cited firmware directory |

## msx1

MSX: 18 slots, 46 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/DISK.ROM` | yes | RetroBat's list | `80dcd1ad1a4cf65d64b7ba10504e8190` no region; RetroBat's list |  |
| `bios/FMPAC.ROM` | yes | RetroBat's list | `6f69cc8b5ed761b03afd78000dfb0e19` no region; RetroBat's list |  |
| `bios/KANJI.ROM` | yes | RetroBat's list | `febe8782b466d7c3b16de6d104826b34` no region; RetroBat's list |  |
| `bios/Machines/Shared Roms/FMPAC.rom` | no | `libretro/bluemsx` | `398856474188363d68d5744d2a867e90` no region; retrobios `bluemsx` | `bluemsx`: `Src/Memory/MegaromCartridge.c:290-306` |
| `bios/Machines/Shared Roms/KANJI.rom` | no | `libretro/bluemsx` | `febe8782b466d7c3b16de6d104826b34` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX.rom` | yes | `libretro/bluemsx` | `aa95aea2563cd5ec0a0919b44cc17d47` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX/config.ini` |
| `bios/Machines/Shared Roms/RS232.ROM` | no | `libretro/bluemsx` | `279efd1eae0d358eecd4edc7d9adedf3` no region; RomM's known_bios_files |  |
| `bios/MSX.ROM` | yes | RetroBat's list | `364a1a579fe5cb8dba54519bcfcdac0d` no region; RetroBat's list |  |
| `bios/msx.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `msx.rom` crc32 `a317e6b4` 32768 B | `fbneo`: `src/burn/drv/msx/d_msx.cpp:1773`; `src/burn/drv/msx/d_msx.cpp:1774`; `src/burn/drv/msx/d_msx.cpp:1775` |
| `bios/MSXDOS2.ROM` | yes | RetroBat's list | `6418d091cd6907bbcf940324339e43bb` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/cf-3300_basic-bios1.rom` | yes | RetroBat's list | `f873f1260b14f1468fa118778ae1c3d2` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/cf-3300_disk.rom` | yes | RetroBat's list | `318b6aa76da71c54ccad7734356e1902` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fmpac.rom` | yes | RetroBat's list | `6f69cc8b5ed761b03afd78000dfb0e19` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/px-7_basic-bios1.rom` | yes | RetroBat's list | `9218f6dd89f7ba01e3090d44721b4260` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/px-7_pbasic.rom` | yes | RetroBat's list | `281809cafe63437fc4bffb3033fbd0db` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/vg8020_basic-bios1.rom` | yes | RetroBat's list | `a0452dbf5ace7d2e49d0a8029efed09a` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/yrw801.rom` | yes | RetroBat's list | `42af93619160ef2116416f74a6cb12f2` no region; RetroBat's list |  |
| `bios/PAINTER.ROM` | yes | RetroBat's list | `403cdea1cbd2bb24fae506941f8f655e` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Machines/Shared Roms/ARAB1.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/ARABIC.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/BEERIDE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/GCVMX80.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/HANGUL.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MICROSOLDISK.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MOONSOUND.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXAR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXBR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXFR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXG.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXHAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXJ.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKANJI.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NATIONALDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NOVAXIS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos1.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PAINT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PANASONICDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PHILIPSDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SUNRISEIDE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SWP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/XBASIC2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `COLECO.ROM` | `openmsx` | openmsx has no cited firmware directory |
| `fmpac.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1gt_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_basic-bios2p.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_disk.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_fmbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjibasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjifont.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_msx2psub.rom` | `openmsx` | openmsx has no cited firmware directory |
| `ide250.dat` | `openmsx` | openmsx has no cited firmware directory |
| `NMS8245SystemROM1.08.bin` | `openmsx` | openmsx has no cited firmware directory |
| `phc-70fd2_basickun.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_basic-bios1.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_pbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `svi-3x8_v111.rom` | `openmsx` | openmsx has no cited firmware directory |
| `yrw801.rom` | `openmsx` | openmsx has no cited firmware directory |

## msx2

MSX2: 21 slots, 65 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/Machines/Shared Roms/FMPAC.rom` | no | `libretro/bluemsx` | `398856474188363d68d5744d2a867e90` no region; retrobios `bluemsx` | `bluemsx`: `Src/Memory/MegaromCartridge.c:290-306` |
| `bios/Machines/Shared Roms/KANJI.rom` | no | `libretro/bluemsx` | `febe8782b466d7c3b16de6d104826b34` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX.rom` | yes | `libretro/bluemsx` | `aa95aea2563cd5ec0a0919b44cc17d47` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX/config.ini` |
| `bios/Machines/Shared Roms/MSX2.rom` | yes | `libretro/bluemsx` | `ec3a01c91f24fbddcbcab0ad301bc9ef` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/MSX2EXT.rom` | yes | `libretro/bluemsx` | `2183c2aff17cf4297bdb496de78c2e8a` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/RS232.ROM` | no | `libretro/bluemsx` | `279efd1eae0d358eecd4edc7d9adedf3` no region; RomM's known_bios_files |  |
| `bios/msx.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `msx.rom` crc32 `a317e6b4` 32768 B | `fbneo`: `src/burn/drv/msx/d_msx.cpp:1773`; `src/burn/drv/msx/d_msx.cpp:1774`; `src/burn/drv/msx/d_msx.cpp:1775` |
| `bios/MSX2.ROM` | yes | RetroBat's list | `ec3a01c91f24fbddcbcab0ad301bc9ef` no region; RetroBat's list |  |
| `bios/MSX2EXT.ROM` | yes | RetroBat's list | `2183c2aff17cf4297bdb496de78c2e8a` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fmpac.rom` | yes | RetroBat's list | `6f69cc8b5ed761b03afd78000dfb0e19` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_basic-bios2.rom` | yes | RetroBat's list | `5bf4fdfd3c3ffea3e573b386550cb3fa` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_disk.rom` | yes | RetroBat's list | `86269da485e852d9f581ac27f4ba32ff` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_kanjibasic.rom` | yes | RetroBat's list | `afbe6ba903453902540ae988cc89dc7b` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_kanjifont.rom` | yes | RetroBat's list | `090539674630c1338a90a1df943a93e2` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_msx2sub.rom` | yes | RetroBat's list | `dcd5e2388115172f2fb48875b2089dbf` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-5500_superimp.rom` | yes | RetroBat's list | `7eb6ae0fbe9736b20dda8dcbd5064f67` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/nms8245_basic-bios2.rom` | yes | RetroBat's list | `ec3a01c91f24fbddcbcab0ad301bc9ef` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/nms8245_disk.rom` | yes | RetroBat's list | `3525ea046ddedc66991790eeae32acfd` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/nms8245_disk_1.06.rom` | yes | RetroBat's list | `57509815f93e2817d3eb57e20286c7fb` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/nms8245_msx2sub.rom` | yes | RetroBat's list | `248514aba82a0ec7fe2a9106862b05cd` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/yrw801.rom` | yes | RetroBat's list | `42af93619160ef2116416f74a6cb12f2` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Machines/Shared Roms/ARAB1.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/ARABIC.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/BEERIDE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/GCVMX80.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/HANGUL.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MICROSOLDISK.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MOONSOUND.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AREXT.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2G.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2GEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2HAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2J.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2JEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2REXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SPEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXAR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXBR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXFR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXG.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXHAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXJ.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKANJI.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NATIONALDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NOVAXIS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos1.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PAINT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PANASONICDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PHILIPSDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SUNRISEIDE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SWP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/XBASIC2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `COLECO.ROM` | `openmsx` | openmsx has no cited firmware directory |
| `fmpac.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1gt_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_basic-bios2p.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_disk.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_fmbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjibasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjifont.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_msx2psub.rom` | `openmsx` | openmsx has no cited firmware directory |
| `ide250.dat` | `openmsx` | openmsx has no cited firmware directory |
| `NMS8245SystemROM1.08.bin` | `openmsx` | openmsx has no cited firmware directory |
| `phc-70fd2_basickun.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_basic-bios1.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_pbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `svi-3x8_v111.rom` | `openmsx` | openmsx has no cited firmware directory |
| `yrw801.rom` | `openmsx` | openmsx has no cited firmware directory |

## msx2+

MSX2+: 20 slots, 66 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/Machines/Shared Roms/FMPAC.rom` | no | `libretro/bluemsx` | `398856474188363d68d5744d2a867e90` no region; retrobios `bluemsx` | `bluemsx`: `Src/Memory/MegaromCartridge.c:290-306` |
| `bios/Machines/Shared Roms/KANJI.rom` | no | `libretro/bluemsx` | `febe8782b466d7c3b16de6d104826b34` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX.rom` | yes | `libretro/bluemsx` | `aa95aea2563cd5ec0a0919b44cc17d47` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX/config.ini` |
| `bios/Machines/Shared Roms/MSX2.rom` | yes | `libretro/bluemsx` | `ec3a01c91f24fbddcbcab0ad301bc9ef` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/MSX2EXT.rom` | yes | `libretro/bluemsx` | `2183c2aff17cf4297bdb496de78c2e8a` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/MSX2P.rom` | yes | `libretro/bluemsx` | `6d8c0ca64e726c82a4b726e9b01cdf1e` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX2PEXT.rom` | yes | `libretro/bluemsx` | `7c8243c71d8f143b2531f01afa6a05dc` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/RS232.ROM` | no | `libretro/bluemsx` | `279efd1eae0d358eecd4edc7d9adedf3` no region; RomM's known_bios_files |  |
| `bios/msx.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `msx.rom` crc32 `a317e6b4` 32768 B | `fbneo`: `src/burn/drv/msx/d_msx.cpp:1773`; `src/burn/drv/msx/d_msx.cpp:1774`; `src/burn/drv/msx/d_msx.cpp:1775` |
| `bios/MSX2P.ROM` | yes | RetroBat's list | `847cc025ffae665487940ff2639540e5` no region; RetroBat's list |  |
| `bios/MSX2PEXT.ROM` | yes | RetroBat's list | `7c8243c71d8f143b2531f01afa6a05dc` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fmpac.rom` | yes | RetroBat's list | `6f69cc8b5ed761b03afd78000dfb0e19` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_basic-bios2p.rom` | yes | RetroBat's list | `c5c26c3e8bc6c485424818057f0507b9` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_disk.rom` | yes | RetroBat's list | `00aa02b6077de40a0b51d71a3c3e1d5f` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_firmware.rom` | yes | RetroBat's list | `fa8e7d4b999af058fe2864a5f2e014ec` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_fmbasic.rom` | yes | RetroBat's list | `0c40e7db1c1fcc2405e4d0cdd215adb4` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_kanjibasic.rom` | yes | RetroBat's list | `9dfdebfaa6b547222a40aab8bb2e29f8` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_kanjifont.rom` | yes | RetroBat's list | `acf53887c2d2783dc059a9b442c86b90` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1wsx_msx2psub.rom` | yes | RetroBat's list | `7c8243c71d8f143b2531f01afa6a05dc` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/yrw801.rom` | yes | RetroBat's list | `42af93619160ef2116416f74a6cb12f2` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Machines/Shared Roms/ARAB1.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/ARABIC.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/BEERIDE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/GCVMX80.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/HANGUL.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MICROSOLDISK.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MOONSOUND.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AREXT.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2G.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2GEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2HAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2J.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2JEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2PMUS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2REXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SPEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXAR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXBR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXFR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXG.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXHAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXJ.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKANJI.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NATIONALDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NOVAXIS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos1.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PAINT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PANASONICDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PHILIPSDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SUNRISEIDE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SWP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/XBASIC2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `COLECO.ROM` | `openmsx` | openmsx has no cited firmware directory |
| `fmpac.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1gt_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_basic-bios2p.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_disk.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_fmbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjibasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjifont.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_msx2psub.rom` | `openmsx` | openmsx has no cited firmware directory |
| `ide250.dat` | `openmsx` | openmsx has no cited firmware directory |
| `NMS8245SystemROM1.08.bin` | `openmsx` | openmsx has no cited firmware directory |
| `phc-70fd2_basickun.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_basic-bios1.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_pbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `svi-3x8_v111.rom` | `openmsx` | openmsx has no cited firmware directory |
| `yrw801.rom` | `openmsx` | openmsx has no cited firmware directory |

## msxturbor

MSX Turbo R: 13 slots, 71 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/Machines/Shared Roms/FMPAC.rom` | no | `libretro/bluemsx` | `398856474188363d68d5744d2a867e90` no region; retrobios `bluemsx` | `bluemsx`: `Src/Memory/MegaromCartridge.c:290-306` |
| `bios/Machines/Shared Roms/KANJI.rom` | no | `libretro/bluemsx` | `febe8782b466d7c3b16de6d104826b34` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX.rom` | yes | `libretro/bluemsx` | `aa95aea2563cd5ec0a0919b44cc17d47` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX/config.ini` |
| `bios/Machines/Shared Roms/MSX2.rom` | yes | `libretro/bluemsx` | `ec3a01c91f24fbddcbcab0ad301bc9ef` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/MSX2EXT.rom` | yes | `libretro/bluemsx` | `2183c2aff17cf4297bdb496de78c2e8a` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2/config.ini` |
| `bios/Machines/Shared Roms/MSX2P.rom` | yes | `libretro/bluemsx` | `6d8c0ca64e726c82a4b726e9b01cdf1e` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/MSX2PEXT.rom` | yes | `libretro/bluemsx` | `7c8243c71d8f143b2531f01afa6a05dc` no region; RomM's known_bios_files | `bluemsx`: `system/bluemsx/Machines/MSX2+/config.ini` |
| `bios/Machines/Shared Roms/RS232.ROM` | no | `libretro/bluemsx` | `279efd1eae0d358eecd4edc7d9adedf3` no region; RomM's known_bios_files |  |
| `bios/msx.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `msx.rom` crc32 `a317e6b4` 32768 B | `fbneo`: `src/burn/drv/msx/d_msx.cpp:1773`; `src/burn/drv/msx/d_msx.cpp:1774`; `src/burn/drv/msx/d_msx.cpp:1775` |
| `bios/openMSX/share/systemroms/fmpac.rom` | yes | RetroBat's list | `6f69cc8b5ed761b03afd78000dfb0e19` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1gt_firmware.rom` | yes | RetroBat's list | `af17a344bcc177b97a4888c4c559f3ae` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/fs-a1gt_kanjifont.rom` | yes | RetroBat's list | `acf53887c2d2783dc059a9b442c86b90` no region; RetroBat's list |  |
| `bios/openMSX/share/systemroms/yrw801.rom` | yes | RetroBat's list | `42af93619160ef2116416f74a6cb12f2` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Machines/Shared Roms/ARAB1.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/ARABIC.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/BEERIDE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/GCVMX80.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/HANGUL.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MICROSOLDISK.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MOONSOUND.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2AREXT.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2BREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2FREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2G.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2GEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2HAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2J.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2JEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2KREXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2PMUS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2R2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2REXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSX2SPEXT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXAR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXBR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXDOS23.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXFR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXG.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXHAN.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXJ.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKANJI.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXKR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXR2.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSE.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXSP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXTR.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXTREXT.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXTRMUS.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/MSXTROPT.ROM` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NATIONALDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/NOVAXIS.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos1.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/nowindDos2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PAINT.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PANASONICDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/PHILIPSDISK.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SUNRISEIDE.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/SWP.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/Shared Roms/XBASIC2.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `COLECO.ROM` | `openmsx` | openmsx has no cited firmware directory |
| `fmpac.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1gt_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_basic-bios2p.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_disk.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_firmware.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_fmbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjibasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_kanjifont.rom` | `openmsx` | openmsx has no cited firmware directory |
| `fs-a1wsx_msx2psub.rom` | `openmsx` | openmsx has no cited firmware directory |
| `ide250.dat` | `openmsx` | openmsx has no cited firmware directory |
| `NMS8245SystemROM1.08.bin` | `openmsx` | openmsx has no cited firmware directory |
| `phc-70fd2_basickun.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_basic-bios1.rom` | `openmsx` | openmsx has no cited firmware directory |
| `px-7_pbasic.rom` | `openmsx` | openmsx has no cited firmware directory |
| `svi-3x8_v111.rom` | `openmsx` | openmsx has no cited firmware directory |
| `yrw801.rom` | `openmsx` | openmsx has no cited firmware directory |

## n64

n64: 1 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/64DD_IPL.bin` | no | `libretro/parallel_n64` | `8d3d9f294b6e174bc7b1d2fd1c727530` japan; RomM's known_bios_files | `parallel_n64`: `libretro/libretro.c:848` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Mupen64plus/font.ttf` | `libretro/mupen64plus_next` | no identity: no source states a hash for it |
| `bios/Mupen64plus/IPL.n64` | `libretro/mupen64plus_next` | no identity: no source states a hash for it |
| `pif.ntsc.rom` | `simple64` | simple64 has no cited firmware directory |
| `pif.pal.rom` | `simple64` | simple64 has no cited firmware directory |

## n64dd

Nintendo 64 DD: 4 slots, 11 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/64DD_IPL.bin` | no | `libretro/parallel_n64` | `8d3d9f294b6e174bc7b1d2fd1c727530` japan; RomM's known_bios_files | `parallel_n64`: `libretro/libretro.c:848` |
| `bios/Mupen64plus/IPL_DEV.n64` | yes | RetroBat's list | `aad37b1492886b892f1821f37fd3ae34` japan; RetroBat's list |  |
| `bios/Mupen64plus/IPL_JAP.n64` | yes | RetroBat's list | `8d3d9f294b6e174bc7b1d2fd1c727530` japan; RetroBat's list |  |
| `bios/Mupen64plus/IPL_USA.n64` | yes | RetroBat's list | `37c36e4286d36892a9fc70eafe4104be` north-america; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `64dd_ipl_DEV.bin` | `ares/Nintendo64DD`, `project64` | ares has no cited firmware directory; project64 has no cited firmware directory |
| `64DD_IPL_DEV.n64` | `mupen64` | mupen64 has no cited firmware directory |
| `64dd_ipl_J.bin` | `ares/Nintendo64DD` | ares has no cited firmware directory |
| `64DD_IPL_JP.n64` | `mupen64` | mupen64 has no cited firmware directory |
| `64dd_ipl_U.bin` | `ares/Nintendo64DD` | ares has no cited firmware directory |
| `64DD_IPL_US.n64` | `mupen64` | mupen64 has no cited firmware directory |
| `64DD_IPL_USA.bin` | `project64` | project64 has no cited firmware directory |
| `64DD_IPL_v12_JPN.bin` | `project64` | project64 has no cited firmware directory |
| `bios/Mupen64plus/font.ttf` | `libretro/mupen64plus_next` | no identity: no source states a hash for it |
| `bios/Mupen64plus/IPL.n64` | `libretro/mupen64plus_next` | no identity: no source states a hash for it |

## namco2x6

Namco2x6: 2 slots, 11 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/pcsx2/bios/r27v1602f.7d` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/pcsx2/bios/r27v1602f.8g` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bin/resources/game_controller_db.txt` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `bin/resources/GameIndex.yaml` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `bin/resources/patches.zip` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `bin/resources/RedumpDatabase.yaml` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `COH-H31100.mec` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `COH-H31100.nvm` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `COH-H31100.rom0` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `COH-H31100.rom1` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `COH-H31100.rom2` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `eeprom.dat` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |
| `flash.dat` | `pcsx2x6/namco2x6` | pcsx2x6 has no cited firmware directory |

## naomi

Naomi: 5 slots, 13 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dc/f355bios.zip` | no | `libretro/flycast` | `547f3d12aed389058ca06148f1cca0ed` no region; RomM's known_bios_files | `flycast`: `core/hw/naomi/naomi_roms.h:96-103` |
| `bios/dc/f355dlx.zip` | no | `libretro/flycast` | `1028615bcac4c31634a3364ce5c04044` no region; RomM's known_bios_files | `flycast`: `core/hw/naomi/naomi_roms.h:105-119` |
| `bios/dc/hod2bios.zip` | no | `libretro/flycast` | `f4011d3116500354edf7302a90402711` no region; RomM's known_bios_files | `flycast`: `core/hw/naomi/naomi_roms.h:121-131` |
| `bios/dc/naomi.zip` | yes | `libretro/flycast`, RetroBat's list | `eb4099aeb42ef089cfe94f8fe95e51f6` no region; RetroBat's list | `flycast`: `core/hw/naomi/naomi_roms.h:133-188` |
| `bios/dc/naomi_boot.bin`<br>also `bios/dc/epr-21576d.bin`, `bios/dc/naomi_bios.bin`, `bios/dc/naomi_bios.bin.bin`, `bios/dc/naomi_boot.bin.bin` | no | `libretro/flycast` | `3bffafac42a7767d8dcecf771f5552ba` no region; RomM's known_bios_files | `flycast`: `core/hw/holly/sb_mem.cpp:219,58-59` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `airlbios.zip` | `demul/naomi` | demul has no cited firmware directory |
| `dc/f355bios.zip` | `flycast/naomi` | flycast has no cited firmware directory |
| `dc/f355dlx.zip` | `flycast/naomi` | flycast has no cited firmware directory |
| `dc/hod2bios.zip` | `flycast/naomi` | flycast has no cited firmware directory |
| `dc/naomi.zip` | `flycast/naomi` | flycast has no cited firmware directory |
| `dc/naomi_boot.bin` | `flycast/naomi` | flycast has no cited firmware directory |
| `f355bios.zip` | `demul/naomi` | demul has no cited firmware directory |
| `f355dlx.zip` | `demul/naomi` | demul has no cited firmware directory |
| `hod2bios.zip` | `demul/naomi` | demul has no cited firmware directory |
| `jvs13551.zip` | `demul/naomi` | demul has no cited firmware directory |
| `mie.zip` | `demul/naomi` | demul has no cited firmware directory |
| `naomi.zip` | `demul/naomi` | demul has no cited firmware directory |
| `naomigd.zip` | `demul/naomi` | demul has no cited firmware directory |

## naomi2

Naomi2: 1 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/dc/naomi2.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `jvs13551.zip` | `demul/naomi` | demul has no cited firmware directory |
| `mie.zip` | `demul/naomi` | demul has no cited firmware directory |
| `naomi2.zip` | `demul/naomi` | demul has no cited firmware directory |

## nds

Nintendo DS: 8 slots, 21 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bios7.bin` | yes | `bizhawk/melonDS`, `libretro/desmume`, `libretro/melonds`, `libretro/melondsds`, `libretro/noods`, RetroBat's list | `df692a80a5b1bc90728bc3dfc76cd948` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `desmume`: `desmume/src/NDSSystem.cpp:2156-2166, desmume/src/frontend/libretro/libretro.cpp:763`<br>`melonds`: `src/NDS.h:197, src/NDS.cpp:482-492, src/frontend/Util_ROM.cpp:117-131`<br>`melonds_ds`: `src/libretro/config/config.hpp:326, src/libretro/config/console.cpp:379, src/libretro/config/console.cpp:526-529, src/libretro/config/console.cpp:1098-1103, src/MemConstants.h:31, src/MemConstants.h:36`<br>`noods`: `src/memory.cpp:144-155` |
| `bios/bios9.bin` | yes | `bizhawk/melonDS`, `libretro/desmume`, `libretro/melonds`, `libretro/melondsds`, `libretro/noods`, RetroBat's list | `a392174eb3e572fed6447e956bde4b25` world; BizHawk's FirmwareDatabase.cs, RetroBat's list | `desmume`: `desmume/src/NDSSystem.cpp:2214-2224, desmume/src/frontend/libretro/libretro.cpp:764`<br>`melonds`: `src/NDS.h:196, src/NDS.cpp:467-477, src/frontend/Util_ROM.cpp:99-111`<br>`melonds_ds`: `src/libretro/config/config.hpp:325, src/libretro/config/console.cpp:380, src/libretro/config/console.cpp:531-534, src/libretro/config/console.cpp:1098-1103, src/MemConstants.h:30, src/MemConstants.h:37`<br>`noods`: `src/memory.cpp:130-141` |
| `bios/dsi_bios7.bin` | yes | `libretro/melonds`, `libretro/melondsds`, RetroBat's list | no identity: RetroBat names it without a hash and no source gives one | `melonds`: `src/DSi.h:34, src/DSi.cpp:604-620, src/frontend/Util_ROM.cpp:158-170`<br>`melonds_ds`: `src/libretro/config/config.hpp:328, src/libretro/config/console.cpp:516-519, src/libretro/config/console.cpp:1098-1103, src/MemConstants.h:32, src/MemConstants.h:44` |
| `bios/dsi_bios9.bin` | yes | `libretro/melonds`, `libretro/melondsds`, RetroBat's list | no identity: RetroBat names it without a hash and no source gives one | `melonds`: `src/DSi.h:33, src/DSi.cpp:587-599, src/frontend/Util_ROM.cpp:145-156`<br>`melonds_ds`: `src/libretro/config/config.hpp:327, src/libretro/config/console.cpp:521-524, src/libretro/config/console.cpp:1098-1103, src/MemConstants.h:32, src/MemConstants.h:45` |
| `bios/dsi_firmware.bin` | yes | `libretro/melonds`, `libretro/melondsds`, RetroBat's list | no identity: RetroBat names it without a hash and no source gives one | `melonds`: `src/SPI.cpp:197-211, src/frontend/Util_ROM.cpp:201-217`<br>`melonds_ds`: `src/libretro/config/console.cpp:497-499, src/libretro/config/console.cpp:536-551, src/libretro/config/constants.cpp:102-176` |
| `bios/firmware.bin` | yes | `libretro/desmume`, `libretro/melonds`, `libretro/melondsds`, `libretro/noods`, RetroBat's list | `93dbb42c5b3d7a2109b4d27105c9e3aa` no region; a dump catalog's name | `desmume`: `desmume/src/firmware.cpp:249-280, desmume/src/frontend/libretro/libretro.cpp:765`<br>`melonds`: `src/SPI.cpp:192-211, src/frontend/Util_ROM.cpp:174-195`<br>`melonds_ds`: `src/libretro/config/console.cpp:337-364, src/libretro/config/console.cpp:1136-1182, src/libretro/config/constants.cpp:102-176, src/libretro/config/constants.hpp:288`<br>`noods`: `src/spi.cpp:62-160` |
| `bios/NDS_Bios7i.bin` | yes | `bizhawk/melonDS` | `322ceeafb77b0cc96a1fcc6271e0e0ae` world; BizHawk's FirmwareDatabase.cs |  |
| `bios/NDS_Bios9i.bin` | yes | `bizhawk/melonDS` | `23031f25602a52c2df32c39554b0c55e` world; BizHawk's FirmwareDatabase.cs |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/DSi_Firmware.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/dsi_nand.bin` | `libretro/melonds`, `libretro/melondsds` | no identity: no source states a hash for it |
| `bios/DSi_Nand_AUS.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/DSi_Nand_CHN.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/DSi_Nand_EUR.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/DSi_Nand_JPN.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/DSi_Nand_KOR.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/DSi_Nand_USA.bin` | `bizhawk/melonDS` | no identity: no source states a hash for it |
| `bios/dsi_sd_card.bin` | `libretro/melonds` | no identity: no source states a hash for it |
| `bios/nds_sd_card.bin` | `libretro/noods` | no identity: no source states a hash for it |
| `bios7.bin` | `desmume`, `melonds` | desmume has no cited firmware directory; melonds has no cited firmware directory |
| `bios9.bin` | `desmume`, `melonds` | desmume has no cited firmware directory; melonds has no cited firmware directory |
| `dsi_bios7.bin` | `melonds` | melonds has no cited firmware directory |
| `dsi_bios9.bin` | `melonds` | melonds has no cited firmware directory |
| `dsi_firmware.bin` | `melonds` | melonds has no cited firmware directory |
| `dsi_nand.bin` | `melonds` | melonds has no cited firmware directory |
| `dsi_sd_card.bin` | `melonds` | melonds has no cited firmware directory |
| `firmware.bin` | `desmume`, `melonds` | desmume has no cited firmware directory; melonds has no cited firmware directory |

## neogeo

NeoGeo: 3 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/aes.zip` | yes | `libretro/geolith` | `ad9585c72130c56f04ae26aae87c289d` no region; retrobios `geolith`<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `neo-epo.bin` crc32 `d27a71f1` 131072 B | `geolith`: `libretro/libretro.c:1222-1225, jg.c:221-222`; `src/geo.c:178-181`; `src/geo.c:234-243` |
| `bios/irrmaze.zip` | no | `libretro/geolith` | `0e369df30a70e45547cef9e4078dc511` no region; retrobios `geolith` | `geolith`: `libretro/libretro.c:1281-1292, jg.c:229-230`; `src/geo.c:286-299, libretro/libretro.c:1281-1292` |
| `bios/neogeo.zip` | yes | `libretro/fbneo`, `libretro/geolith`, RetroBat's list | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `fbneo`: `src/burn/drv/neogeo/d_neogeo.cpp:1624`; `src/burn/drv/neogeo/d_neogeo.cpp:1625`; `src/burn/drv/neogeo/d_neogeo.cpp:1626`; `src/burn/drv/neogeo/d_neogeo.cpp:1627`; `src/burn/drv/neogeo/d_neogeo.cpp:1628`; `src/burn/drv/neogeo/d_neogeo.cpp:1629`; `src/burn/drv/neogeo/d_neogeo.cpp:1630`; `src/burn/drv/neogeo/d_neogeo.cpp:1631`; `src/burn/drv/neogeo/d_neogeo.cpp:1632`; `src/burn/drv/neogeo/d_neogeo.cpp:1633`; `src/burn/drv/neogeo/d_neogeo.cpp:1634`; `src/burn/drv/neogeo/d_neogeo.cpp:1635`; `src/burn/drv/neogeo/d_neogeo.cpp:1636`; `src/burn/drv/neogeo/d_neogeo.cpp:1637`; `src/burn/drv/neogeo/d_neogeo.cpp:1638`; `src/burn/drv/neogeo/d_neogeo.cpp:1640`; `src/burn/drv/neogeo/d_neogeo.cpp:1641`; `src/burn/drv/neogeo/d_neogeo.cpp:1642`; `src/burn/drv/neogeo/d_neogeo.cpp:1648`; `src/burn/drv/neogeo/d_neogeo.cpp:1649`; `src/burn/drv/neogeo/d_neogeo.cpp:1650`; `src/burn/drv/neogeo/d_neogeo.cpp:1651`; `src/burn/drv/neogeo/d_neogeo.cpp:1652`; `src/burn/drv/neogeo/d_neogeo.cpp:1653`; `src/burn/drv/neogeo/d_neogeo.cpp:1654`; `src/burn/drv/neogeo/d_neogeo.cpp:1655`; `src/burn/drv/neogeo/d_neogeo.cpp:1656`; `src/burn/drv/neogeo/d_neogeo.cpp:1657`; `src/burn/drv/neogeo/d_neogeo.cpp:1658`; `src/burn/drv/neogeo/d_neogeo.cpp:1659`; `src/burn/drv/neogeo/d_neogeo.cpp:1660`; `src/burn/drv/neogeo/d_neogeo.cpp:1661`; `src/burn/drv/neogeo/d_neogeo.cpp:1662`; `src/burn/drv/neogeo/d_neogeo.cpp:1663`; `src/burn/drv/neogeo/d_neogeo.cpp:1665`; `src/burn/drv/neogeo/d_neogeo.cpp:1672`; `src/burn/drv/neogeo/d_neogeo.cpp:1673`; `src/burn/drv/neogeo/d_neogeo.cpp:1674`; `src/burn/drv/neogeo/d_neogeo.cpp:1764`; `src/burn/drv/neogeo/d_neogeo.cpp:1765`; `src/burn/drv/neogeo/d_neogeo.cpp:1811`; `src/burn/drv/neogeo/d_neogeo.cpp:1899`<br>`geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/hiscore.dat` | `libretro/fbalpha` | no identity: no source states a hash for it |
| `neogeo.zip` | `mame64/neogeo` | mame64 has no cited firmware directory |

## neogeo64

Hyper NeoGeo 64: 2 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/hng64.zip` | yes | `libretro/mame`, `libretro/mame2016` | zip, for `libretro/mame`: `bios_export.bin` crc32 `bbf07ec6` 524288 B, `bios_korea.bin` crc32 `ac953e2e` 524288 B, `bios_us.bin` crc32 `ab5948d6` 524288 B, `brom1.bin` crc32 `a30dd3de` 524288 B, `from1.bin` crc32 `6b933005` 524288 B, `lvs-igx-br9020f.u3` crc32 `af9f4287` 256 B, `lvs-ioj-br9020f.u2` crc32 `78b7020d` 256 B, `lvs-jam-br9020f.u3` crc32 `dabec5d2` 256 B, `rom1.bin` crc32 `4a6832dc` 130866 B, `tmp87ph40an.bin` crc32 `b70df21f` 32768 B | `mame`: `src/mame/snk/hng64.cpp:3238`<br>`mame2016`: `src/mame/drivers/hng64.cpp:1994` |
| `bios/neogeo.zip` | yes | RetroBat's list | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `hng64.zip` | `mame64` | mame64 has no cited firmware directory |

## neogeocd

NeoGeo CD: 12 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/neocd/front-sp1.bin` | yes | `libretro/neocd`, RetroBat's list | `5c2366f25ff92d71788468ca492ebeca` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:59 FRONT_LOADER_SEARCH_PATTERN, src/bios.cpp:148-160 autoByteSwap` |
| `bios/neocd/neocd.bin` | yes | `libretro/neocd`, RetroBat's list | `f39572af7584cb5b3f70ae8cc848aba2` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:61 CDZ_SEARCH_PATTERN, src/bios.cpp:148-160 autoByteSwap` |
| `bios/neocd/neocd_f.rom` | yes | `libretro/neocd`, RetroBat's list | `8834880c33164ccbe6476b559f3e37de` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:59 FRONT_LOADER_SEARCH_PATTERN at 0xC0006C` |
| `bios/neocd/neocd_sf.rom` | yes | `libretro/neocd`, RetroBat's list | `043d76d5f0ef836500700c34faef774d` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:62 SMKDANBETA_FRONT_SEARCH_PATTERN at 0xC00004` |
| `bios/neocd/neocd_st.rom` | yes | `libretro/neocd`, RetroBat's list | `f6325a33c6d63ea4b9162a3fa8c32727` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:63 SMKDANBETA_TOP_SEARCH_PATTERN at 0xC00004` |
| `bios/neocd/neocd_sz.rom` | yes | `libretro/neocd`, RetroBat's list | `971ee8a36fb72da57aed01758f0a37f5` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:64 SMKDANBETA_CDZ_SEARCH_PATTERN at 0xC00004` |
| `bios/neocd/neocd_t.rom` | yes | `libretro/neocd`, RetroBat's list | `de3cf45d227ad44645b22aa83b49f450` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:60 TOP_LOADER_SEARCH_PATTERN at 0xC0006C` |
| `bios/neocd/neocd_z.rom` | yes | `libretro/neocd`, RetroBat's list | `11526d58d4c524daef7d5d677dc6b004` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:61 CDZ_SEARCH_PATTERN at 0xC0006C` |
| `bios/neocd/top-sp1.bin` | yes | `libretro/neocd`, RetroBat's list | `122aee210324c72e8a11116e6ef9c0d0` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:60 TOP_LOADER_SEARCH_PATTERN, src/bios.cpp:148-160 autoByteSwap` |
| `bios/neocd/uni-bioscd.rom` | yes | `libretro/neocd`, RetroBat's list | `08ca8b2dba6662e8024f9e789711c6fc` no region; RomM's known_bios_files | `neocd`: `src/bios.cpp:68 UNIVERSE33_SEARCH_PATTERN at 0xC00150` |
| `bios/neocdz.zip` | yes | `libretro/fbneo`, RetroBat's list | `c733b4b7bd30fa849874d96c591c8639` no region; RetroBat's list<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `neocd.bin` crc32 `df9de490` 524288 B | `fbneo`: `src/burn/drv/neogeo/d_neogeo.cpp:1983`; `src/burn/drv/neogeo/d_neogeo.cpp:1984`; `src/burn/drv/neogeo/d_neogeo.cpp:1985`; `src/burn/drv/neogeo/d_neogeo.cpp:1986`; `src/burn/drv/neogeo/d_neogeo.cpp:1988` |
| `bios/neogeo.zip` | yes | RetroBat's list | `dffb72f116d36d025068b23970a4f6df` no region; RetroBat's list<br>`00dad01abdbf8ea9e79ad2fe11bdb182` no region; retrobios `geolith`<br>zip, for `libretro/fbneo`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B<br>zip, for `libretro/geolith`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B<br>zip, for `libretro/mame`: `000-lo.lo` crc32 `5a86cff2` 131072 B, `japan-j3.bin` crc32 `dff6d41f` 131072 B, `sfix.sfix` crc32 `c2ea0cfd` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sm1.sm1` crc32 `94416d67` 131072 B, `sp-1v1_3db8c.bin` crc32 `162f0ebe` 131072 B, `sp-45.sp1` crc32 `03cc9f6a` 524288 B, `sp-e.sp1` crc32 `2723a5b5` 131072 B, `sp-j2.sp1` crc32 `acede59c` 131072 B, `sp-j3.sp1` crc32 `486cb450` 524288 B, `sp-s.sp1` crc32 `c7f2fa45` 131072 B, `sp-s2.sp1` crc32 `9036d879` 131072 B, `sp-s3.sp1` crc32 `91b64be3` 131072 B, `sp-u2.sp1` crc32 `e72943de` 131072 B, `sp1-j3.bin` crc32 `fbc6d469` 131072 B, `sp1-u2` crc32 `62f021f4` 131072 B, `sp1-u3.bin` crc32 `2025b7a2` 131072 B, `sp1-u4.bin` crc32 `1179a30f` 131072 B, `sp1.jipan.1024` crc32 `9fb0abe4` 131072 B, `uni-bios_1_0.rom` crc32 `0ce453a0` 131072 B, `uni-bios_1_1.rom` crc32 `5dda0d84` 131072 B, `uni-bios_1_2.rom` crc32 `4fa698e9` 131072 B, `uni-bios_1_2o.rom` crc32 `e19d3ce9` 131072 B, `uni-bios_1_3.rom` crc32 `b24b44a0` 131072 B, `uni-bios_2_0.rom` crc32 `0c12c2ad` 131072 B, `uni-bios_2_1.rom` crc32 `8dabf76b` 131072 B, `uni-bios_2_2.rom` crc32 `2d50996a` 131072 B, `uni-bios_2_3.rom` crc32 `27664eb5` 131072 B, `uni-bios_2_3o.rom` crc32 `601720ae` 131072 B, `uni-bios_3_0.rom` crc32 `a97c89a9` 131072 B, `uni-bios_3_1.rom` crc32 `0c58093f` 131072 B, `uni-bios_3_2.rom` crc32 `a4e8b9b3` 131072 B, `uni-bios_3_3.rom` crc32 `24858466` 131072 B, `uni-bios_4_0.rom` crc32 `a7aab458` 131072 B, `vs-bios.rom` crc32 `f0e8f27d` 131072 B<br>zip, for `libretro/mame2000`: `neo-geo.rom` crc32 `9036d879` 131072 B, `ng-sfix.rom` crc32 `354029fc` 131072 B, `ng-sm1.rom` crc32 `97cf998b` 131072 B<br>zip, for `libretro/mame2003`: `000-lo.lo` crc32 `e09e253c` 65536 B, `asia-s3.rom` crc32 `91b64be3`, `sfix.sfx` crc32 `354029fc` 131072 B, `sm1.sm1` crc32 `97cf998b` 131072 B, `sp-e.sp1` crc32 `2723a5b5`, `sp-j2.rom` crc32 `acede59c`, `sp-s.sp1` crc32 `c7f2fa45`, `sp-s2.sp1` crc32 `9036d879`, `usa_2slt.bin` crc32 `e72943de`, `vs-bios.rom` crc32 `f0e8f27d` | `geolith`: `libretro/libretro.c:1222-1225, jg.c:223-224`; `src/geo.c:183-198`; `src/geo.c:200-203`; `src/geo.c:234-243`; `src/geo.c:245-255`; `src/geo.c:257-267` |

## nes

nes: 2 slots, 10 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/FDS_disksys-nintendo.rom` | yes | `bizhawk/NesHawk`, `bizhawk/quickerNES` | `ca30b50f880eb660a320674ed365ef7a` japan; BizHawk's FirmwareDatabase.cs |  |
| `bios/NstDatabase.xml` | no | `libretro/nestopia` | `7bfe8c0540ed4bd6a0f1e2a0f0118ced` no region; RomM's known_bios_files | `nestopia`: `libretro/libretro.cpp:2121-2151 (database load), libretro/nstdatabase.hpp (baked-in fallback)` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/custom.pal` | `libretro/nestopia` | no identity: no source states a hash for it |
| `bios/gamegenie.nes` | `libretro/fceumm` | no identity: no source states a hash for it |
| `bios/MesenPalette.pal` | `libretro/mesen` | no identity: no source states a hash for it |
| `bios/nes.pal` | `libretro/fceumm` | no identity: no source states a hash for it |
| `bios/StudyBox.bin` | `libretro/mesen` | no identity: no source states a hash for it |
| `emulators/mesen/Firmware/lh5323m1.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/StudyBox.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/ymf288_adpcm_rom.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `gg.rom` | `mednafen/nes` | mednafen has no cited firmware directory |
| `nes.pal` | `jgenesis` | jgenesis has no cited firmware directory |

## ngage

Nokia N-gage: 1 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/eka2l1/data/roms/NEM-4/SYM.ROM` | yes | RetroBat's list | `64a95a4a884cf4cc15a566b856603193` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `data/roms/nem-4/SYM.ROM` | `eka2l1` | eka2l1 has no cited firmware directory |

## ngp

Neo Geo Pocket: 1 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/[BIOS] SNK Neo Geo Pocket (Japan, Europe) (En,Ja).ngp` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `ngp_bios.rom` | `ares/NeoGeoPocket` | ares has no cited firmware directory |

## ngpc

Neo Geo Pocket Color: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/[BIOS] SNK Neo Geo Pocket (Japan, Europe) (En,Ja).ngp` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `ngp_bios.rom` | `ares/NeoGeoPocketColor` | ares has no cited firmware directory |
| `ngpc_bios.rom` | `ares/NeoGeoPocketColor` | ares has no cited firmware directory |

## odyssey2

Odyssey 2: 4 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/c52.bin` | yes | `bizhawk/O2Hawk`, `libretro/o2em`, RetroBat's list | `f1071cdb0b6b10dde94d3bc8a6146387` no region; BizHawk's FirmwareDatabase.cs, retrobios `o2em`, RetroBat's list | `o2em`: `libretro.c:1149-1155` |
| `bios/g7400.bin` | yes | `bizhawk/O2Hawk`, `libretro/o2em`, RetroBat's list | `c500ff71236068e0dc0d0603d265ae76` europe; BizHawk's FirmwareDatabase.cs, retrobios `o2em`, RetroBat's list | `o2em`: `libretro.c:1156-1161` |
| `bios/jopac.bin` | yes | `libretro/o2em`, RetroBat's list | `279008e4a0db2dc5f1c048853b033828` no region; retrobios `o2em`, RetroBat's list | `o2em`: `libretro.c:1162-1167` |
| `bios/o2rom.bin` | yes | `bizhawk/O2Hawk`, `libretro/o2em`, RetroBat's list | `562d5ebf9e030a40d6fabfc2f33139fd` europe, north-america; BizHawk's FirmwareDatabase.cs, retrobios `o2em`, RetroBat's list | `o2em`: `libretro.c:1143-1147` |

## oricatmos

oricatmos: 0 slots, 12 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `8dos2.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `basic10.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `basic11b.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `bd500.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `hyperbas.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `jasmin.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `kernel.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `microdis.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `pravetzt.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `shell.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `teleass.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |
| `telmon24.rom` | `oricutron/oricutron` | oricutron has no cited firmware directory |

## pc88

NEC PC-8800: 8 slots, 9 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/quasi88/font.rom`<br>also `bios/quasi88/FONT.ROM` | no | `libretro/quasi88` | `38d32748ae49d1815b0614970849fd40` no region; RomM's known_bios_files | `quasi88`: `src/LIBRETRO/libretro.c:566` |
| `bios/quasi88/N88.ROM`<br>also `bios/quasi88/N88.ROM` | yes | `libretro/quasi88`, RetroBat's list | `4f984e04a99d56c4cfe36115415d6eb8` no region; RetroBat's list | `quasi88`: `src/LIBRETRO/libretro.c:550-551` |
| `bios/quasi88/N88EXT0.ROM` | yes | RetroBat's list | `d675a2ca186c6efcd6277b835de4c7e5` no region; RetroBat's list |  |
| `bios/quasi88/N88EXT1.ROM` | yes | RetroBat's list | `e844534dfe5744b381444dbe61ef1b66` no region; RetroBat's list |  |
| `bios/quasi88/N88EXT2.ROM` | yes | RetroBat's list | `6548fa45061274dee1ea8ae1e9e93910` no region; RetroBat's list |  |
| `bios/quasi88/N88EXT3.ROM` | yes | RetroBat's list | `fc4b76a402ba501e6ba6de4b3e8b4273` no region; RetroBat's list |  |
| `bios/quasi88/N88N.ROM`<br>also `bios/quasi88/N80.ROM`, `bios/quasi88/N88N.ROM`, `bios/quasi88/n80.rom` | yes | `libretro/quasi88`, RetroBat's list | `2ff07b8769367321128e03924af668a0` no region; RetroBat's list | `quasi88`: `src/LIBRETRO/libretro.c:559` |
| `bios/quasi88/N88SUB.ROM`<br>also `bios/quasi88/DISK.ROM`, `bios/quasi88/N88SUB.ROM`, `bios/quasi88/disk.rom` | yes | `libretro/quasi88`, RetroBat's list | `793f86784e5608352a5d7f03f03e0858` no region; RetroBat's list | `quasi88`: `src/LIBRETRO/libretro.c:114, src/LIBRETRO/libretro.c:552-553` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/quasi88/font2.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/font3.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88_0.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88_1.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88_2.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88_3.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88jisho.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88knj1.rom` | `libretro/quasi88` | no identity: no source states a hash for it |
| `bios/quasi88/n88knj2.rom` | `libretro/quasi88` | no identity: no source states a hash for it |

## pc98

NEC PC-9800: 20 slots, 7 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/np2/2608_bd.wav` | no | `libretro/nekop2` | `d94546e70f17fd899be8df3544ab6cbb` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:11,60-71` |
| `bios/np2/2608_hh.wav` | no | `libretro/nekop2` | `08c54a0c1f774a5538a848a6665a34b4` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:14` |
| `bios/np2/2608_rim.wav` | no | `libretro/nekop2` | `465ea0768b27da404aec45dfc501404b` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:16` |
| `bios/np2/2608_sd.wav` | no | `libretro/nekop2` | `d71004351c8bbfdad53b18222c061d49` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:12` |
| `bios/np2/2608_tom.wav` | no | `libretro/nekop2` | `96a4ead13f364734f79b0c58af2f0e1f` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:15` |
| `bios/np2/2608_top.wav` | no | `libretro/nekop2` | `593cff6597ab9380d822b8f824fd2c28` no region; RomM's known_bios_files | `nekop2`: `sound/rhythmc.c:13` |
| `bios/np2/bios.rom` | no | `libretro/nekop2` | `cd237e16e7e77c06bb58540e9e9fca68` no region; RomM's known_bios_files | `nekop2`: `bios/bios.c:232-237, common/strres.c:53` |
| `bios/np2/font.bmp`<br>also `bios/np2/FONT.ROM` | no | `libretro/nekop2` | `7da1e5b7c482d4108d22a5b09631d967` no region; RomM's known_bios_files | `nekop2`: `libretro/libretro.c:872, font/font.c:113, font/fontdata.c:12` |
| `bios/np2/sound.rom` | no | `libretro/nekop2` | `524473c1a5a03b17e21d86a0408ff827` no region; RomM's known_bios_files | `nekop2`: `sound/soundrom.c:15-16,21-55,65-78` |
| `bios/np2kai/2608_bd.wav`<br>also `bios/np2kai/2608_BD.WAV` | no | `libretro/np2kai` | `d94546e70f17fd899be8df3544ab6cbb` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:11, 60-71, sound/fmgen/fmgen_opna.cpp:1435` |
| `bios/np2kai/2608_hh.wav`<br>also `bios/np2kai/2608_HH.WAV` | no | `libretro/np2kai` | `08c54a0c1f774a5538a848a6665a34b4` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:14, sound/fmgen/fmgen_opna.cpp:1435-1439` |
| `bios/np2kai/2608_rim.wav`<br>also `bios/np2kai/2608_RIM.WAV`, `bios/np2kai/2608_RYM.WAV` | no | `libretro/np2kai` | `465ea0768b27da404aec45dfc501404b` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:16, sound/fmgen/fmgen_opna.cpp:1435-1445` |
| `bios/np2kai/2608_sd.wav`<br>also `bios/np2kai/2608_SD.WAV` | no | `libretro/np2kai` | `d71004351c8bbfdad53b18222c061d49` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:12, sound/fmgen/fmgen_opna.cpp:1435-1439` |
| `bios/np2kai/2608_tom.wav`<br>also `bios/np2kai/2608_TOM.WAV` | no | `libretro/np2kai` | `96a4ead13f364734f79b0c58af2f0e1f` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:15, sound/fmgen/fmgen_opna.cpp:1435-1439` |
| `bios/np2kai/2608_top.wav`<br>also `bios/np2kai/2608_TOP.WAV` | no | `libretro/np2kai` | `593cff6597ab9380d822b8f824fd2c28` no region; RomM's known_bios_files | `np2kai`: `sound/rhythmc.c:13, sound/fmgen/fmgen_opna.cpp:1435-1439` |
| `bios/np2kai/BIOS.ROM` | yes | `libretro/np2kai`, RetroBat's list | `e246140dec5124c5e404869a84caefce` no region; RetroBat's list | `np2kai`: `bios/bios.c:440-447, pccore.c:129, common/strres.c:67` |
| `bios/np2kai/font.bmp`<br>also `bios/np2kai/FONT.BMP`, `bios/np2kai/FONT.ROM`, `bios/np2kai/font.rom` | yes | `libretro/np2kai`, RetroBat's list | `7da1e5b7c482d4108d22a5b09631d967` no region; RetroBat's list | `np2kai`: `sdl/libretro/libretro.c:1830, font/font.c:86-125, font/fontdata.c:11` |
| `bios/np2kai/FONT.ROM` | yes | RetroBat's list | `2af6179d7de4893ea0b705c00e9a98d6` no region; RetroBat's list |  |
| `bios/np2kai/ITF.ROM` | yes | RetroBat's list | `e9fc3890963b12cf15d0a2eea5815b72` no region; RetroBat's list |  |
| `bios/np2kai/SOUND.ROM` | yes | `libretro/np2kai`, RetroBat's list | `caf90f22197aed6f14c471c21e64658d` no region; RetroBat's list | `np2kai`: `sound/soundrom.c:15-16,21-55,93-106` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/np2/bios9821.rom` | `libretro/nekop2` | no identity: no source states a hash for it |
| `bios/np2/sasi.rom` | `libretro/nekop2` | no identity: no source states a hash for it |
| `bios/np2/scsi.rom` | `libretro/nekop2` | no identity: no source states a hash for it |
| `bios/np2kai/ide.rom` | `libretro/np2kai` | no identity: no source states a hash for it |
| `bios/np2kai/pci.rom` | `libretro/np2kai` | no identity: no source states a hash for it |
| `bios/np2kai/sasi.rom` | `libretro/np2kai` | no identity: no source states a hash for it |
| `bios/np2kai/scsi.rom` | `libretro/np2kai` | no identity: no source states a hash for it |

## pcengine

PC Engine: 6 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gexpress.pce`<br>also `bios/gecard.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `6d2cb14fc3e1f65ceb135633d1694122` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1057-1058, mednafen/settings.cpp:41, mednafen/pce/pce.cpp:374-411`<br>`mednafen_pce`: `libretro.cpp:1057-1058, mednafen/settings.cpp:41, mednafen/pce/pce.cpp:373-411`<br>`mednafen_pce_fast`: `libretro.c:1906-1907` |
| `bios/syscard1.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `2b7ccb3d86baa18f6402c176f3065082` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1055-1056`<br>`mednafen_pce`: `libretro.cpp:1055-1056`<br>`mednafen_pce_fast`: `libretro.c:1905` |
| `bios/syscard2.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `3cdd6614a918616bfc41c862e889dd79` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1053-1054`<br>`mednafen_pce`: `libretro.cpp:1053-1054`<br>`mednafen_pce_fast`: `libretro.c:1903` |
| `bios/syscard2u.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `94279f315e8b52904f65ab3108542afe` north-america; retrobios `beetle_pce` | `beetle_pce`: `libretro.cpp:1061-1062`<br>`mednafen_pce`: `libretro.cpp:1061-1062`<br>`mednafen_pce_fast`: `libretro.c:1910-1911` |
| `bios/syscard3.pce` | yes | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast`, RetroBat's list | `38179df8f4ac870017db21ebcbf53114` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RetroBat's list | `beetle_pce`: `libretro.cpp:1051-1052, mednafen/settings.cpp:40`<br>`mednafen_pce`: `libretro.cpp:1051-1052, mednafen/settings.cpp:40`<br>`mednafen_pce_fast`: `libretro.c:65, libretro.c:1896-1901` |
| `bios/syscard3u.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `0754f903b52e3b3342202bdafb13efa5` north-america; retrobios `beetle_pce` | `beetle_pce`: `libretro.cpp:1059-1060`<br>`mednafen_pce`: `libretro.cpp:1059-1060`<br>`mednafen_pce_fast`: `libretro.c:1908-1909` |

## pcenginecd

PC Engine CD: 6 slots, 8 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gexpress.pce`<br>also `bios/gecard.pce` | yes | `bizhawk/HyperNyma`, `bizhawk/PCEHawk`, `bizhawk/TurboNyma`, `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `6d2cb14fc3e1f65ceb135633d1694122` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1057-1058, mednafen/settings.cpp:41, mednafen/pce/pce.cpp:374-411`<br>`mednafen_pce`: `libretro.cpp:1057-1058, mednafen/settings.cpp:41, mednafen/pce/pce.cpp:373-411`<br>`mednafen_pce_fast`: `libretro.c:1906-1907` |
| `bios/syscard1.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `2b7ccb3d86baa18f6402c176f3065082` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1055-1056`<br>`mednafen_pce`: `libretro.cpp:1055-1056`<br>`mednafen_pce_fast`: `libretro.c:1905` |
| `bios/syscard2.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `3cdd6614a918616bfc41c862e889dd79` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1053-1054`<br>`mednafen_pce`: `libretro.cpp:1053-1054`<br>`mednafen_pce_fast`: `libretro.c:1903` |
| `bios/syscard2u.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `94279f315e8b52904f65ab3108542afe` north-america; retrobios `beetle_pce` | `beetle_pce`: `libretro.cpp:1061-1062`<br>`mednafen_pce`: `libretro.cpp:1061-1062`<br>`mednafen_pce_fast`: `libretro.c:1910-1911` |
| `bios/syscard3.pce` | yes | `bizhawk/HyperNyma`, `bizhawk/PCEHawk`, `bizhawk/TurboNyma`, `libretro/mednafen_pce`, `libretro/mednafen_pce_fast`, RetroBat's list | `38179df8f4ac870017db21ebcbf53114` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RetroBat's list | `beetle_pce`: `libretro.cpp:1051-1052, mednafen/settings.cpp:40`<br>`mednafen_pce`: `libretro.cpp:1051-1052, mednafen/settings.cpp:40`<br>`mednafen_pce_fast`: `libretro.c:65, libretro.c:1896-1901` |
| `bios/syscard3u.pce` | no | `libretro/mednafen_pce`, `libretro/mednafen_pce_fast` | `0754f903b52e3b3342202bdafb13efa5` north-america; retrobios `beetle_pce` | `beetle_pce`: `libretro.cpp:1059-1060`<br>`mednafen_pce`: `libretro.cpp:1059-1060`<br>`mednafen_pce_fast`: `libretro.c:1908-1909` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `emulators/mesen/Firmware/[BIOS] Games Express CD Card (Japan).pce` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/[BIOS] Super CD-ROM System (Japan) (v3.0).pce` | `mesen` | outside bios/, where RomMBat never writes |
| `games_express.pce` | `ares/PCEngineCD` | ares has no cited firmware directory |
| `gecard.pce` | `mednafen/pce` | mednafen has no cited firmware directory |
| `syscard1.pce` | `ares/PCEngineCD` | ares has no cited firmware directory |
| `syscard3.pce` | `ares/PCEngineCD`, `mednafen/pce` | ares has no cited firmware directory; mednafen has no cited firmware directory |
| `syscard3u.pce` | `ares/PCEngineCD` | ares has no cited firmware directory |

## pcfx

PC-FX: 2 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/fx-scsi.rom` | yes | `bizhawk/PCFX`, RetroBat's list | `430e9745f9235c515bc8e652d6ca3004` no region; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files |  |
| `bios/pcfx.rom` | yes | `bizhawk/PCFX`, `libretro/mednafen_pcfx`, RetroBat's list | `08e36edbea28a017f79f8d4f7ff9b6d7` no region; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pcfx`, RetroBat's list | `beetle_pcfx`: `libretro.cpp:367-394, mednafen/settings.c:92-93` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `fx-scsi.rom` | `mednafen/pcfx` | mednafen has no cited firmware directory |
| `pcfx.rom` | `mednafen/pcfx` | mednafen has no cited firmware directory |

## pegasus

Aamber Pegasus: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/pegasus.zip` | yes | `libretro/mame`, RetroBat's list | `fee37d8140e4fab48aac4fdab2847f7d` no region; RetroBat's list<br>zip, for `libretro/mame`: `6571.bin` crc32 `5a25144b` 2048 B, `mon10_2569.bin` crc32 `910fc930` 4096 B, `mon11_2569.bin` crc32 `07b92002` 4096 B, `mon11_2669.bin` crc32 `f3ee23c8` 4096 B, `mon11_2674.bin` crc32 `1640ff7e` 4096 B, `mon22_2856.bin` crc32 `5f5f688a` 4096 B, `mon22b_2856.bin` crc32 `a47b0308` 4096 B, `mon23_2601.bin` crc32 `0e024222` 4096 B, `mon23a_2569.bin` crc32 `248e62c9` 4096 B | `mame`: `src/mame/ausnz/pegasus.cpp:575` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/pegasus_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `pegasus.zip` | `mame64/pegasus` | mame64 has no cited firmware directory |

## pokemini

Nintendo Pokemon Mini: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/bios.min` | yes | `libretro/pokemini`, RetroBat's list | `1e4fb124a3a886865acb574f388c803d` world; RetroBat's list | `pokemini`: `source/PokeMini.c:189-206 (PokeMini_LoadBIOSFile), libretro/libretro.c:565 (path)` |

## prboom

prboom: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/prboom.wad` | no | `libretro/prboom` | `72ae1b47820fcc93cc0df9c428d0face` no region; retrobios `prboom` | `prboom`: `src/d_main.c:1449-1465 (PACKAGE .wad load), libretro/libretro.c:4171-4252 (I_FindFile search), src/config.h:39 (PACKAGE=prboom)` |

## ps2

PS2: 3 slots, 24 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/pcsx2/bios/ps2-0230a-20080220.bin` | yes | RetroBat's list | `21038400dc633070a78ad53090c53017` no region; RetroBat's list |  |
| `bios/pcsx2/resources/GameIndex.yaml` | no | `libretro/pcsx2` | `fc3c14208a929c386445635b19baa3f2` no region; retrobios `lrps2` | `lrps2`: `pcsx2/GameDatabase.cpp:49, pcsx2/GameDatabase.cpp:979`<br>`pcsx2`: `pcsx2/GameDatabase.cpp:39, pcsx2/GameDatabase.cpp:1026-1035` |
| `bios/ps2-0230a-20080220.bin` | yes | `libretro/pcsx2` | `21038400dc633070a78ad53090c53017` no region; RomM's known_bios_files | `pcsx2`: `pcsx2/ps2/BiosTools.cpp:16-17,80-197,258-283,317-370` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/eeprom.dat` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/bios/<bios>.bin` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/bios/<bios>.mec` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/bios/<bios>.nvm` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/bios/<bios>.rom1` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/bios/<bios>.rom2` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/resources/cheats_ni.zip` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/pcsx2/resources/cheats_ws.zip` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/ps2-0230a-20080220.mec` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/ps2-0230a-20080220.nvm` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/ps2-0230a-20080220.rom1` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `bios/ps2-0230a-20080220.rom2` | `libretro/pcsx2` | no identity: no source states a hash for it |
| `eeprom.dat` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |
| `ps2-0230a-20080220.bin` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |
| `ps2-0230a-20080220.mec` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |
| `ps2-0230a-20080220.nvm` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |
| `ps2-0230a-20080220.rom1` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |
| `ps2-0230a-20080220.rom2` | `pcsx2-16`, `pcsx2/pcsx2` | pcsx2 has no cited firmware directory; pcsx2-16 has no cited firmware directory |

## ps3

ps3: 0 slots, 1 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `PS3UPDAT.PUP` | `rpcs3` | installed by the emulator: RPCS3 installs the PS3 system software from this package through its own menu, into dev_flash/ |

## ps4

ps4: 0 slots, 26 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `shadps4/sys_modules/libSceAt9Enc.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodec.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodecCpu.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodecCpuDdp.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodecCpuDtsHdLbr.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodecCpuHevag.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceAudiodecCpuM4aac.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceCesCs.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceFont.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceFontFt.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceFreeTypeOl.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceFreeTypeOptOl.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceFreeTypeOt.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceJpegDec.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceJpegEnc.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceJson.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceJson2.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceLibcInternal.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceNgs2.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libScePngEnc.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceRtc.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceRudp.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceSystemGesture.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceUlt.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceWkFontConfig.sprx` | `shadps4` | shadps4 has no cited firmware directory |
| `shadps4/sys_modules/libSceXml.sprx` | `shadps4` | shadps4 has no cited firmware directory |

## psp

psp: 1 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/PPSSPP/ppge_atlas.zim` | no | `libretro/ppsspp` | `866855cc330b9b95cc69135fb7b41d38` no region; RomM's known_bios_files | `ppsspp`: `Core/Util/PPGeDraw.cpp:227-228` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/PPSSPP/ppge_atlas.meta` | `libretro/ppsspp` | no identity: no source states a hash for it |
| `PPSSPP/ppge_atlas.meta` | `ppsspp` | ppsspp has no cited firmware directory |
| `PPSSPP/ppge_atlas.zim` | `ppsspp` | ppsspp has no cited firmware directory |

## psvita

psvita: 0 slots, 2 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `psvita/PSP2UPDAT.PUP` | `vita3k` | vita3k has no cited firmware directory |
| `psvita/PSVUPDAT.PUP` | `vita3k` | vita3k has no cited firmware directory |

## psx

PSX: 10 slots, 109 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/ps1_rom.bin`<br>also `bios/PS1_ROM.bin` | no | `libretro/mednafen_psx_hw`, `libretro/swanstation` | `81bbe60ba7a3d1cea1d48c14cbcc647b` world; retrobios `beetle_psx`, retrobios `swanstation` | `beetle_psx`: `libretro.c:531-537`<br>`swanstation`: `src/core/bios.h:9, src/core/bios.cpp:69-70, src/core/bios.cpp:81-90` |
| `bios/PSX_DTLH2000.bin` | yes | `bizhawk/Nymashock`, `bizhawk/Octoshock` | `2118230527a9f51bd9216e32fa912842` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/psxonpsp660.bin`<br>also `bios/PSXONPSP660.bin` | yes | `libretro/mednafen_psx_hw`, `libretro/pcsx_rearmed`, `libretro/swanstation`, RetroBat's list | `c53ca5908936d412331790f4426c6c33` world; retrobios `beetle_psx`, retrobios `swanstation`, RetroBat's list | `beetle_psx`: `libretro.c:523-529`<br>`pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[3]), frontend/libretro.c:3654-3669 (try_use_bios region guess)`<br>`swanstation`: `src/core/bios.cpp:67-68` |
| `bios/scph1001.bin` | no | `libretro/pcsx_rearmed` | `924e392ed05558ffdb115408c263dccf` north-america; retrobios `pcsx_rearmed` | `pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[6])` |
| `bios/scph101.bin` | no | `libretro/pcsx_rearmed` | `6e3735ff4c7dc899ee98981385f6f3d0` north-america; RomM's known_bios_files | `pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[4])` |
| `bios/scph5500.bin`<br>also `bios/SCPH-5500.bin`, `bios/SCPH5500.bin` | yes | `libretro/mednafen_psx_hw`, `libretro/pcsx_rearmed`, `libretro/swanstation` | `8dd7d5296a650fac7319bce665a6a53c` japan; retrobios `beetle_psx`, retrobios `swanstation` | `beetle_psx`: `libretro.c:594-602`<br>`pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[0])`<br>`swanstation`: `src/core/bios.cpp:49` |
| `bios/scph5501.bin`<br>also `bios/SCPH-5501.bin`, `bios/SCPH-5503.bin`, `bios/SCPH-7003.bin`, `bios/SCPH5501.bin`, `bios/SCPH5503.bin`, `bios/SCPH7003.bin`, `bios/scph5503.bin`, `bios/scph7003.bin` | yes | `libretro/mednafen_psx_hw`, `libretro/pcsx_rearmed`, `libretro/swanstation` | `490f666e1afb15b7362b406ed1cea246` north-america; retrobios `beetle_psx`, retrobios `swanstation` | `beetle_psx`: `libretro.c:613-631`<br>`pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[1])`<br>`swanstation`: `src/core/bios.cpp:50-51` |
| `bios/scph5502.bin`<br>also `bios/SCPH-5502.bin`, `bios/SCPH-5552.bin`, `bios/SCPH5502.bin`, `bios/SCPH5552.bin`, `bios/scph5552.bin` | yes | `libretro/mednafen_psx_hw`, `libretro/pcsx_rearmed`, `libretro/swanstation` | `32736f17079d0b2b7024407c39bd3050` europe, oceania; retrobios `beetle_psx`, retrobios `swanstation` | `beetle_psx`: `libretro.c:633-647`<br>`pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[2])`<br>`swanstation`: `src/core/bios.cpp:52-53` |
| `bios/scph5903.bin` | no | `libretro/mednafen_psx_hw` | `81328b966e6dcf7ea1e32e55e1c104bb` japan; retrobios `beetle_psx` | `beetle_psx`: `libretro.c:604-611, libretro.c:3203-3217, libretro.c:3236-3238, libretro.c:6159` |
| `bios/scph7001.bin` | no | `libretro/pcsx_rearmed` | `1e68c231d0896b7eadcad1d7d8e76129` north-america; retrobios `pcsx_rearmed` | `pcsx_rearmed`: `frontend/libretro.c:3864-3867 (listed_bios[5])` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/openbios.bin` | `libretro/mednafen_psx_hw` | no identity: no source states a hash for it |
| `dtlh1100.bin` | `duckstation` | duckstation has no cited firmware directory |
| `openbios.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_desr5000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_desr5500.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh10000_0117.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh10000_0217.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh10000_0224.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30101.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30102_0902.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh30102_1228.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh50000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh50001_0325.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh50001_0623.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh50002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh50009.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh70002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh75000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh75000a.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh90000_a.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_dtlh90000_j.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_kdl22px300.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph10000_0117.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph10000_15000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph18000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph18000_gh003.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph18000_gh008.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30000_0427.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30000_35000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001_0727.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001_0902.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001_35001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001r_0427.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001r_0704.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30001r_1004.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30002_0902.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30002_1228.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30002r_0704.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30002r_1004.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30003.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph30005r.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph37000_39000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph39001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph39002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph39005.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph39008.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50000_0822.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50000_55000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50001_0325.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50001_0623.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50002_0227.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50002_0623.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50004_0822.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50005.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50006.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50008.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50009.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph50011.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph55000_0623.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph70000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph70001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph70002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph70002_alt.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph70005.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph75000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph75001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph75002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph75006.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph77000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph77001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph77002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph77006.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph79000_90000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph79001_90001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph79002_90002.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph79006_90006.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph90000_0220.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph90001_0220.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph90002_0220.bin` | `duckstation` | duckstation has no cited firmware directory |
| `ps2_scph90005_0220.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph100.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1000r.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1001_v20.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1001_v21.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1002a.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1002b.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph1002c.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph101_v44.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph101_v45.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph102_v44.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph102_v45.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph3000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph3500.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph5000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph5500.bin` | `duckstation`, `mednafen/psx` | duckstation has no cited firmware directory; mednafen has no cited firmware directory |
| `scph5501.bin` | `duckstation`, `mednafen/psx` | duckstation has no cited firmware directory; mednafen has no cited firmware directory |
| `scph5502.bin` | `duckstation`, `mednafen/psx` | duckstation has no cited firmware directory; mednafen has no cited firmware directory |
| `scph7000.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph7000w.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph7001.bin` | `duckstation` | duckstation has no cited firmware directory |
| `scph7002.bin` | `duckstation` | duckstation has no cited firmware directory |

## pv1000

PV-1000: 0 slots, 1 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/pv1000.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## samcoupe

samcoupe: 0 slots, 4 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `atom.rom` | `simcoupe/simcoupe` | simcoupe has no cited firmware directory |
| `atomlite.rom` | `simcoupe/simcoupe` | simcoupe has no cited firmware directory |
| `samcoupe.rom` | `simcoupe/simcoupe` | simcoupe has no cited firmware directory |
| `sp0256-al2.bin` | `simcoupe/simcoupe` | simcoupe has no cited firmware directory |

## satellaview

Satellaview: 19 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/BS-X.bin`<br>also `bios/BS-X.bios` | yes | `libretro/mesen-s`, `libretro/snes9x`, RetroBat's list | `fed4d8242cfbed61343d53d48432aced` japan; RetroBat's list, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:39-47,96-113, Core/BaseCartridge.cpp:65`<br>`snes9x`: `libretro/libretro.cpp:1414, bsx.cpp:1209, bsx.cpp:1214` |
| `bios/cgb_boot.bin`<br>also `bios/gbc_bios.bin` | no | `libretro/mesen-s` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:149, Core/Gameboy.cpp:108-119, Core/GbBootRom.h:83` |
| `bios/dmg_boot.bin`<br>also `bios/gb_bios.bin` | no | `libretro/mesen-s` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:148, Core/Gameboy.cpp:108-114, Core/GbBootRom.h:7` |
| `bios/dsp1.data.rom` | no | `libretro/mesen-s` | `3d81b45fa0c2aa8b852dfb1ece7c0971` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1.program.rom` | no | `libretro/mesen-s` | `ae209fbe789fbf11a48aea5ab1197321` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1b.data.rom` | no | `libretro/mesen-s` | `1e3f568634a7d8284020dddc0ae905bc` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp1b.program.rom` | no | `libretro/mesen-s` | `d10f446888e097cbf500f3f663cf4f6d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp2.data.rom` | no | `libretro/mesen-s` | `e9417e29223b139c3c4b635a2a3b8744` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp2.program.rom` | no | `libretro/mesen-s` | `aa6e5922a3ed5ded54f24247c11143c5` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp3.data.rom` | no | `libretro/mesen-s` | `0a81210c0a940b997dd9843281008ee6` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp3.program.rom` | no | `libretro/mesen-s` | `d99ca4562818d49cee1f242705bba6f8` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp4.data.rom` | no | `libretro/mesen-s` | `ee4990879eb68e3cbca239c5bc20303d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/dsp4.program.rom` | no | `libretro/mesen-s` | `a151023b948b90ffc23a5b594bb6fef2` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/SGB1.sfc` | no | `libretro/mesen-s` | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/SGB2.sfc` | no | `libretro/mesen-s` | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/st010.data.rom` | no | `libretro/mesen-s` | `254d70762b6f59f99c27c395aba7d07d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st010.program.rom` | no | `libretro/mesen-s` | `1d70019179a59a566a0bb5d3f2845544` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st011.data.rom` | no | `libretro/mesen-s` | `10bd3f4aa949737ab9836512c35bcc29` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st011.program.rom` | no | `libretro/mesen-s` | `95222ebf1c0c2990bcf25db43743f032` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/sgb2_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |
| `bios/sgb_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |

## saturn

Sega Saturn: 9 slots, 22 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kronos/saturn_bios.bin` | yes | `libretro/kronos`, RetroBat's list | `af5828fdff51384f99b3c4926be27762` no region; RetroBat's list | `kronos`: `yabause/src/libretro/libretro.c:1619-1623` |
| `bios/mpr-17933.bin` | yes | `bizhawk/Saturnus`, `libretro/kronos`, `libretro/mednafen_saturn`, `libretro/yabasanshiro`, RetroBat's list | `3240872c70984b6cbfda1586cab68dbe` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `beetle_saturn`: `mednafen/ss/ss.c:1847-1848 (region filename), mednafen/ss/ss.c:1867-1873 (open), mednafen/ss/ss.c:1961-1984 (size check, read), mednafen/ss/ss.c:1988 (sha256 for save states), libretro.c:1103 (default area)`<br>`kronos`: `yabause/src/libretro/libretro.c:1631`<br>`yabasanshiro`: `yabause/src/libretro/libretro.c:1056` |
| `bios/mpr-18811-mx.ic1` | yes | `bizhawk/Saturnus`, `libretro/kronos`, `libretro/mednafen_saturn`, RetroBat's list | `255113ba943c92a54facd25a10fd780c` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `beetle_saturn`: `mednafen/settings.c:62-63 (filename), mednafen/ss/cart.c:197-216 (open), mednafen/ss/cart/rom.c:67-68 (read), mednafen/ss/db.c:90-91 (game table), libretro.c:527 (option)`<br>`kronos`: `yabause/src/utils/src/db.c:43-44, yabause/src/sys/memory/src/cs0.c:1526-1535` |
| `bios/mpr-19367-mx.ic1` | yes | `bizhawk/Saturnus`, `libretro/kronos`, `libretro/mednafen_saturn`, RetroBat's list | `1cd19988d1d72a3e7caa0b73234c96b4` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `beetle_saturn`: `mednafen/settings.c:64-65 (filename), mednafen/ss/cart.c:197-216 (open), mednafen/ss/cart/rom.c:67-68 (read), mednafen/ss/db.c:92 (game table), libretro.c:529 (option)`<br>`kronos`: `yabause/src/utils/src/db.c:45, yabause/src/sys/memory/src/cs0.c:1526-1535` |
| `bios/SAT_STV_17952A-(U).bin` | yes | `bizhawk/Saturnus` | `249b67334cfae53810c1d1e3b93957a3` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SAT_STV_17954A-(E).bin` | yes | `bizhawk/Saturnus` | `ba2cb634c15e59d7ea5552729306f2ef` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SAT_STV_20091-(J).bin` | yes | `bizhawk/Saturnus` | `34175c7ad31acbe44f2baccc7ea065c7` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/saturn_bios.bin` | yes | `libretro/yabasanshiro`, RetroBat's list | `af5828fdff51384f99b3c4926be27762` no region; RetroBat's list | `yabasanshiro`: `yabause/src/libretro/libretro.c:1048` |
| `bios/sega_101.bin`<br>also `bios/mpr-17933.bin`, `bios/saturn_bios.bin` | yes | `bizhawk/Saturnus`, `libretro/kronos`, `libretro/mednafen_saturn`, `libretro/yabasanshiro`, `libretro/ymir`, RetroBat's list | `85ec9ca47d8f6807718151cbcca8b964` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `beetle_saturn`: `mednafen/ss/ss.c:1845-1846 (region filename), mednafen/ss/ss.c:1867-1873 (open), mednafen/ss/ss.c:1961-1984 (size check, read), mednafen/ss/ss.c:1988 (sha256 for save states)`<br>`kronos`: `yabause/src/libretro/libretro.c:1627`<br>`yabasanshiro`: `yabause/src/libretro/libretro.c:1052`<br>`ymir`: `standalone: apps/ymir-sdl3/src/util/rom_loader.cpp:9-22, apps/ymir-sdl3/src/app/rom_manager.cpp:14-56, libs/ymir-core/include/ymir/sys/memory_defs.hpp:9; libretro: apps/ymir-libretro/src/libretro.cpp:260-263,534-558` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/cdb/cdb_rom.bin` | `libretro/ymir` | no identity: no source states a hash for it |
| `bios/mpeg_card.bin` | `libretro/mednafen_saturn` | no identity: no source states a hash for it |
| `bios/satar4mp.bin` | `libretro/mednafen_saturn` | no identity: no source states a hash for it |
| `cdb/cdb_rom.bin` | `ymir` | ymir has no cited firmware directory |
| `kronos/saturn_bios.bin` | `kronos` | kronos has no cited firmware directory |
| `mpr-17933.bin` | `kronos`, `mednafen/saturn`, `yabasanshiro` | kronos has no cited firmware directory; mednafen has no cited firmware directory; yabasanshiro has no cited firmware directory |
| `mpr-18811-mx.ic1` | `kronos`, `mednafen/saturn` | kronos has no cited firmware directory; mednafen has no cited firmware directory |
| `mpr-19367-mx.ic1` | `kronos`, `mednafen/saturn` | kronos has no cited firmware directory; mednafen has no cited firmware directory |
| `satar4mp.bin` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `saturn_bios.bin` | `yabasanshiro` | yabasanshiro has no cited firmware directory |
| `sega1003.bin` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `sega_100.bin` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `sega_100a.bin` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `sega_101.bin` | `kronos`, `mednafen/saturn`, `ssf`, `yabasanshiro`, `ymir` | kronos has no cited firmware directory; mednafen has no cited firmware directory; ssf has no cited firmware directory; yabasanshiro has no cited firmware directory; ymir has no cited firmware directory |

## scummvm

ScummVM: 2 slots, 106 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/scummvm/extra/MT32_CONTROL.ROM` | yes | `libretro/scummvm`, RetroBat's list | `5626206284b22c2734f3e9efefcd2675` no region; RetroBat's list | `scummvm`: `audio/softsynth/mt32.cpp:298,581` |
| `bios/scummvm/extra/MT32_PCM.ROM` | yes | `libretro/scummvm`, RetroBat's list | `89e42e386e82e0cacb4a2704a03706ca` no region; RetroBat's list | `scummvm`: `audio/softsynth/mt32.cpp:298,581` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/scummvm/extra/access.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/achievements.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/bagel.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/classicmacfonts.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/CM32L_CONTROL.ROM` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/CM32L_PCM.ROM` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/cryo.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/cryomni3d.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/darkseed.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/drascula.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/encoding.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/fonts-cjk.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/fonts-imgui.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/fonts.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/freescape.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/got.aud` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/got.gfx` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/grim-patch.lab` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/hadesch_translations.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/helpdialog.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/hugo.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/kyra.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/lure.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/macgui.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/macventure.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/mm.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/monkey4-patch.m4b` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/mort.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/myst3.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/nancy.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/neverhood.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/pred.dic` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/prince_translation.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/queen.tbl` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/Roland_SC-55.sf2` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/sky.cpt` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/supernova.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/teenagent.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/titanic.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/tony.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/toon.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/ultima.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/ultima8.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/vkeybd_default.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/extra/wintermute.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/gui-icons.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/residualvm.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/scummclassic.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/scummmodern.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/scummremastered.zip` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/shaders.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `bios/scummvm/theme/translations.dat` | `libretro/scummvm` | no identity: no source states a hash for it |
| `scummvm/extra/access.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/achievements.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/bagel.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/classicmacfonts.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/CM32L_CONTROL.ROM` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/CM32L_PCM.ROM` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/cryo.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/cryomni3d.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/darkseed.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/drascula.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/encoding.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/fonts-cjk.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/fonts-imgui.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/fonts.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/freescape.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/got.aud` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/got.gfx` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/grim-patch.lab` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/hadesch_translations.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/helpdialog.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/hugo.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/kyra.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/lure.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/macgui.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/macventure.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/mm.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/monkey4-patch.m4b` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/mort.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/MT32_CONTROL.ROM` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/MT32_PCM.ROM` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/myst3.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/nancy.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/neverhood.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/pred.dic` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/prince_translation.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/queen.tbl` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/Roland_SC-55.sf2` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/sky.cpt` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/supernova.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/teenagent.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/titanic.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/tony.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/toon.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/ultima.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/ultima8.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/vkeybd_default.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/extra/wintermute.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/gui-icons.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/residualvm.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/scummclassic.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/scummmodern.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/scummremastered.zip` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/shaders.dat` | `scummvm` | scummvm has no cited firmware directory |
| `scummvm/theme/translations.dat` | `scummvm` | scummvm has no cited firmware directory |

## scv

Super Cassette Vision: 2 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/scv.zip` | yes | RetroBat's list | `33df021613fb12abe2ad14b9eb17c7fe` no region; RetroBat's list |  |
| `bios/upd7801g.s01`<br>also `bios/bios.bin`, `bios/bios.rom`, `bios/upd7801g.bin`, `bios/upd7801g.bios` | yes | `libretro/emuscv`, RetroBat's list | `635a978fd40db9a18ee44eff449fc126` no region; retrobios `emuscv`, RetroBat's list | `emuscv`: `src/vm/scv/memory.cpp:74-243` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/scv.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## sega32x

Sega 32X: 3 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/32X_G_BIOS.BIN` | yes | `bizhawk/PicoDrive`, `libretro/picodrive`, RetroBat's list | `6a5433f6a132a2b683635819a6dcf085` north-america; BizHawk's FirmwareDatabase.cs, another source's hash for this name | `picodrive`: `pico/32x/memory.c:2208-2245` |
| `bios/32X_M_BIOS.BIN` | yes | `bizhawk/PicoDrive`, `libretro/picodrive`, RetroBat's list | `f88354ec482be09aeccd76a97bb75868` north-america; BizHawk's FirmwareDatabase.cs, another source's hash for this name | `picodrive`: `pico/32x/memory.c:2251-2278` |
| `bios/32X_S_BIOS.BIN` | yes | `bizhawk/PicoDrive`, `libretro/picodrive`, RetroBat's list | `7f041b6a55cd7423a6c08a219335269e` north-america; BizHawk's FirmwareDatabase.cs, another source's hash for this name | `picodrive`: `pico/32x/memory.c:2281-2299` |

## segastv

Sega ST-V: 2 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/kronos/stvbios.zip` | yes | RetroBat's list | zip, for RetroBat's list: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B, `stvbios.nv` crc32 `15432ae2` 128 B |  |
| `bios/stvbios.zip` | yes | `libretro/kronos`, `libretro/mame`, `libretro/mame2003`, `libretro/mame2003_plus`, `libretro/mame2010`, `libretro/mame2016` | zip, for `libretro/kronos`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B<br>zip, for `libretro/mame`: `epr-17740.ic8` crc32 `5c5aa63d` 524288 B, `epr-17740a.ic8` crc32 `3e23c81f` 524288 B, `epr-17741a.ic8` crc32 `4166c663` 524288 B, `epr-17742a.ic8` crc32 `02daf123` 524288 B, `epr-17951a.ic8` crc32 `2672f9d8` 524288 B, `epr-17952a.ic8` crc32 `d1be2adf` 524288 B, `epr-17953a.ic8` crc32 `a4c47570` 524288 B, `epr-17954a.ic8` crc32 `f7722da3` 524288 B, `epr-19730.ic8` crc32 `d0e0889d` 524288 B, `epr-19854.ic8` crc32 `e09d1f60` 524288 B, `epr-20091.ic8` crc32 `59ed40f4` 524288 B, `epr-23603.ic8` crc32 `f688ae60` 524288 B, `stv1061.bin` crc32 `728dbca3` 524288 B, `stv110.bin` crc32 `3dfeda92` 524288 B, `stvbios.nv` crc32 `15432ae2` 128 B | `kronos`: `yabause/src/libretro/libretro.c:1608-1617, yabause/src/ctrl/src/stv.c:112-136`; `yabause/src/ctrl/src/stv.c:120`; `yabause/src/ctrl/src/stv.c:121`; `yabause/src/ctrl/src/stv.c:122`; `yabause/src/ctrl/src/stv.c:123`; `yabause/src/ctrl/src/stv.c:124`; `yabause/src/ctrl/src/stv.c:125`; `yabause/src/ctrl/src/stv.c:126`; `yabause/src/ctrl/src/stv.c:127`; `yabause/src/ctrl/src/stv.c:128`; `yabause/src/ctrl/src/stv.c:129`; `yabause/src/ctrl/src/stv.c:130`; `yabause/src/ctrl/src/stv.c:131`; `yabause/src/ctrl/src/stv.c:132`; `yabause/src/ctrl/src/stv.c:133`<br>`mame`: `src/mame/sega/stv.cpp:3948`<br>`mame2003`: `src/drivers/stv.c:4597`<br>`mame2003_plus`: `src/drivers/stv.c:3741-3767`<br>`mame2010`: `src/mame/drivers/stv.c`<br>`mame2016`: `src/mame/drivers/stv.cpp:3404` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `epr-17952a.ic8` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `epr-17954a.ic8` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `epr-20091.ic8` | `mednafen/saturn` | mednafen has no cited firmware directory |
| `stvbios.zip` | `mame64` | mame64 has no cited firmware directory |

## sgb

Super Game Boy: 22 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/BS-X.bin` | no | `libretro/mesen-s` | `fed4d8242cfbed61343d53d48432aced` japan; RetroBat's list, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:39-47,96-113, Core/BaseCartridge.cpp:65` |
| `bios/cgb_boot.bin`<br>also `bios/gbc_bios.bin` | no | `libretro/mesen-s` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:149, Core/Gameboy.cpp:108-119, Core/GbBootRom.h:83` |
| `bios/dmg_boot.bin`<br>also `bios/gb_bios.bin` | yes | `bizhawk/BSNES`, `bizhawk/Gambatte`, `libretro/mesen-s` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:148, Core/Gameboy.cpp:108-114, Core/GbBootRom.h:7` |
| `bios/dsp1.data.rom` | no | `libretro/mesen-s` | `3d81b45fa0c2aa8b852dfb1ece7c0971` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1.program.rom` | no | `libretro/mesen-s` | `ae209fbe789fbf11a48aea5ab1197321` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1b.data.rom` | no | `libretro/mesen-s` | `1e3f568634a7d8284020dddc0ae905bc` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp1b.program.rom` | no | `libretro/mesen-s` | `d10f446888e097cbf500f3f663cf4f6d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp2.data.rom` | no | `libretro/mesen-s` | `e9417e29223b139c3c4b635a2a3b8744` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp2.program.rom` | no | `libretro/mesen-s` | `aa6e5922a3ed5ded54f24247c11143c5` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp3.data.rom` | no | `libretro/mesen-s` | `0a81210c0a940b997dd9843281008ee6` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp3.program.rom` | no | `libretro/mesen-s` | `d99ca4562818d49cee1f242705bba6f8` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp4.data.rom` | no | `libretro/mesen-s` | `ee4990879eb68e3cbca239c5bc20303d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/dsp4.program.rom` | no | `libretro/mesen-s` | `a151023b948b90ffc23a5b594bb6fef2` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/SGB1.sfc` | yes | `libretro/mesen-s`, RetroBat's list | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/SGB2.sfc` | yes | `libretro/mesen-s`, RetroBat's list | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/sgb2_boot.bin`<br>also `bios/sgb_bios.bin` | yes | `bizhawk/BSNES`, `bizhawk/Gambatte`, `libretro/mesen-s`, RetroBat's list | `e0430bca9925fb9882148fd2dc2418c1` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `mesen-s`: `Core/FirmwareHelper.h:151, Core/Gameboy.cpp:108-127, Core/GbBootRom.h:57` |
| `bios/sgb_bios.bin` | no | `libretro/mgba` | `d574d4f9c12f305074798f54c091a8b4` no region; RomM's known_bios_files | `mgba`: `src/platform/libretro/libretro.c:2100` |
| `bios/sgb_boot.bin`<br>also `bios/sgb_bios.bin` | yes | `bizhawk/BSNES`, `bizhawk/Gambatte`, `libretro/mesen-s`, RetroBat's list | `d574d4f9c12f305074798f54c091a8b4` no region; BizHawk's FirmwareDatabase.cs, RetroBat's list | `mesen-s`: `Core/FirmwareHelper.h:150, Core/Gameboy.cpp:108-127, Core/GbBootRom.h:32` |
| `bios/st010.data.rom` | no | `libretro/mesen-s` | `254d70762b6f59f99c27c395aba7d07d` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st010.program.rom` | no | `libretro/mesen-s` | `1d70019179a59a566a0bb5d3f2845544` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st011.data.rom` | no | `libretro/mesen-s` | `10bd3f4aa949737ab9836512c35bcc29` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st011.program.rom` | no | `libretro/mesen-s` | `95222ebf1c0c2990bcf25db43743f032` no region; RomM's known_bios_files | `mesen-s`: `Core/NecDsp.cpp:101` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `emulators/mesen/Firmware/SGB1.sfc` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/SGB2.sfc` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/sgb2_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/sgb_boot.bin` | `mesen` | outside bios/, where RomMBat never writes |
| `sgb_bios.bin` | `mgba/mgba` | mgba has no cited firmware directory |

## sgb-msu1

sgb-msu1: 2 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/SGB1.sfc` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta` | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040` |
| `bios/SGB2.sfc` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta` | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040` |

## singe

singe: 0 slots, 71 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `ace.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace91_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `ace_a2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `aceeuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astron.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `astronp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlandp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `badlands.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `bega.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `begar1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `blazer.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliff_ox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cliffalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobra.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraab.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobraconv.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cobram3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `cputest.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle11.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle20.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `dle21.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `esh.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshalt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `eshfix7alt2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefox.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `firefoxa.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxy.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `galaxyp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gpworld.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `gtg.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `interstellar.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_211.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_300.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_314.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_315.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_316_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_318.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_euro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair2_319_span.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_a.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_b.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_c.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_d2.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_e.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_f.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_ita.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_n1.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lair_x.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `laireuro.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `lgp.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `mach3.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `roadblaster.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sae.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshort.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `sdqshortalt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `timetrav.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_alt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `tq_swear.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |
| `uvt.zip` | `hypseus/hypseus` | hypseus has no cited firmware directory |

## snes

snes: 31 slots, 31 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/BS-X.bin` | no | `libretro/mesen-s` | `fed4d8242cfbed61343d53d48432aced` japan; RetroBat's list, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:39-47,96-113, Core/BaseCartridge.cpp:65` |
| `bios/cgb_boot.bin`<br>also `bios/gbc_bios.bin` | no | `libretro/mesen-s` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:149, Core/Gameboy.cpp:108-119, Core/GbBootRom.h:83` |
| `bios/cx4.data.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x`, `libretro/bsnes`, `libretro/bsnes_hd_beta` | `037ac4296b6b6a5c47c440188d3c72e3` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes` | `bsnes`: `bsnes/sfc/cartridge/load.cpp:444-456, bsnes/sfc/coprocessor/hitachidsp/hitachidsp.hpp:49, bsnes/heuristics/super-famicom.cpp:597, bsnes/sfc/coprocessor/hitachidsp/data-rom.cpp:3 (staticDataROM[3072])`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:431-454` |
| `bios/dmg_boot.bin`<br>also `bios/gb_bios.bin` | no | `libretro/mesen-s` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:148, Core/Gameboy.cpp:108-114, Core/GbBootRom.h:7` |
| `bios/dsp1.data.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `3d81b45fa0c2aa8b852dfb1ece7c0971` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes-jg`: `libretro/libretro.cpp:432, src/cartridge.cpp:793, src/cartridge.cpp:805-811`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1.program.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `ae209fbe789fbf11a48aea5ab1197321` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/heuristics/super-famicom.cpp:601`<br>`bsnes-jg`: `libretro/libretro.cpp:429, src/cartridge.cpp:781, src/cartridge.cpp:805-811`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1b.data.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `1e3f568634a7d8284020dddc0ae905bc` no region; RomM's known_bios_files | `bsnes`: `bsnes/heuristics/super-famicom.cpp:606`<br>`bsnes-jg`: `libretro/libretro.cpp:438, src/cartridge.cpp:793, src/cartridge.cpp:805-811`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp1b.program.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `d10f446888e097cbf500f3f663cf4f6d` no region; RomM's known_bios_files | `bsnes`: `bsnes/heuristics/super-famicom.cpp:606`<br>`bsnes-jg`: `libretro/libretro.cpp:435, src/cartridge.cpp:781, src/cartridge.cpp:805-811`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp2.data.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `e9417e29223b139c3c4b635a2a3b8744` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes-jg`: `libretro/libretro.cpp:444, src/cartridge.cpp:793, src/cartridge.cpp:813-818`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:515-520`<br>`mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp2.program.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `aa6e5922a3ed5ded54f24247c11143c5` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:515-520, bsnes/heuristics/super-famicom.cpp:602`<br>`bsnes-jg`: `libretro/libretro.cpp:441, src/cartridge.cpp:781, src/cartridge.cpp:813-818`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:515-520`<br>`mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp3.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `0a81210c0a940b997dd9843281008ee6` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:531-534`<br>`bsnes-jg`: `libretro/libretro.cpp:451-452, src/cartridge.cpp:793, src/cartridge.cpp:829-835`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:531-534`<br>`mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp3.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `d99ca4562818d49cee1f242705bba6f8` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:531-534`<br>`bsnes-jg`: `libretro/libretro.cpp:447-448, src/cartridge.cpp:781, src/cartridge.cpp:829-835`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:531-534`<br>`mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp4.data.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `ee4990879eb68e3cbca239c5bc20303d` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes-jg`: `libretro/libretro.cpp:458, src/cartridge.cpp:793, src/cartridge.cpp:820-825`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:522-528`<br>`mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/dsp4.program.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `a151023b948b90ffc23a5b594bb6fef2` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:522-528, bsnes/heuristics/super-famicom.cpp:604-605`<br>`bsnes-jg`: `libretro/libretro.cpp:455, src/cartridge.cpp:781, src/cartridge.cpp:820-825`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:522-528`<br>`mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/SGB1.sfc` | no | `libretro/mesen-s` | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/SGB2.sfc` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x`, `libretro/mesen-s` | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/SNES_dsp1.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `428fdb968d54353d9c9eced1b0586671` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_dsp1b.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `332273cc0df5775d3803f2fd88e95d18` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_dsp2.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `9ebbdcec67c0c01d5a0593d3cf167c9e` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_dsp3.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `484908d68405d44e2be757b4e1e75d15` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_dsp4.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `f229fda2d7b33c5a218cba28147ab9b8` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_SGB_(JU).sfc` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `6a702a9c6c110863ace946321b871632` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_st010.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `636093910fbaff60c8b327be00aefbd9` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_st011.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `5c209ce0283632b6574ad835bf862bee` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/SNES_st018.rom` | yes | `bizhawk/BSNES`, `bizhawk/Faust`, `bizhawk/Snes9x` | `dafae0e0c71c924075811c595c61a30e` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/st010.data.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `254d70762b6f59f99c27c395aba7d07d` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:578-583`<br>`bsnes-jg`: `libretro/libretro.cpp:464, src/cartridge.cpp:870, src/cartridge.cpp:879-889`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:578-583, bsnes/sfc/cartridge/load.cpp:586-596`<br>`mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st010.program.rom` | no | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `1d70019179a59a566a0bb5d3f2845544` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/heuristics/super-famicom.cpp:583-587`<br>`bsnes-jg`: `libretro/libretro.cpp:461, src/cartridge.cpp:858, src/cartridge.cpp:879-889`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/sfc/cartridge/load.cpp:586-596`<br>`mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st011.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `10bd3f4aa949737ab9836512c35bcc29` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:599-601`<br>`bsnes-jg`: `libretro/libretro.cpp:471-472, src/cartridge.cpp:870, src/cartridge.cpp:891-908`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:578-583, bsnes/sfc/cartridge/load.cpp:599-602`<br>`mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st011.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `95222ebf1c0c2990bcf25db43743f032` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:599-601, bsnes/heuristics/super-famicom.cpp:586`<br>`bsnes-jg`: `libretro/libretro.cpp:467-468, src/cartridge.cpp:858, src/cartridge.cpp:891-908`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/sfc/cartridge/load.cpp:599-602`<br>`mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st018.data.rom`<br>also `bios/arm6.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta` | `49c898b60d0f15e90d0ba780dd12f366` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:387-393, bsnes/sfc/coprocessor/armdsp/armdsp.hpp:32`<br>`bsnes-jg`: `libretro/libretro.cpp:479-480, src/cartridge.cpp:657`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:387-393` |
| `bios/st018.program.rom`<br>also `bios/arm6.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes-jg`, `libretro/bsnes_hd_beta` | `dda40ccd57390c96e49d30a041f9a9e7` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:379-385, bsnes/sfc/coprocessor/armdsp/armdsp.hpp:31, bsnes/heuristics/super-famicom.cpp:580`<br>`bsnes-jg`: `libretro/libretro.cpp:475-476, src/cartridge.cpp:647`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:379-384` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/sgb2_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |
| `bios/sgb_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |
| `dsp1.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `dsp2.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `dsp3.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `dsp4.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `emulators/mesen/Firmware/dsp1.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st018.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `st010.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `st011.rom` | `jgenesis` | jgenesis has no cited firmware directory |
| `st018.rom` | `jgenesis` | jgenesis has no cited firmware directory |

## snes-msu1

snes-msu1: 22 slots, 24 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/BS-X.bin` | no | `libretro/mesen-s` | `fed4d8242cfbed61343d53d48432aced` japan; RetroBat's list, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:39-47,96-113, Core/BaseCartridge.cpp:65` |
| `bios/cgb_boot.bin`<br>also `bios/gbc_bios.bin` | no | `libretro/mesen-s` | `dbfce9db9deaa2567f6a84fde55f9680` world; RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:149, Core/Gameboy.cpp:108-119, Core/GbBootRom.h:83` |
| `bios/cx4.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta` | `037ac4296b6b6a5c47c440188d3c72e3` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes` | `bsnes`: `bsnes/sfc/cartridge/load.cpp:444-456, bsnes/sfc/coprocessor/hitachidsp/hitachidsp.hpp:49, bsnes/heuristics/super-famicom.cpp:597, bsnes/sfc/coprocessor/hitachidsp/data-rom.cpp:3 (staticDataROM[3072])`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:431-454` |
| `bios/dmg_boot.bin`<br>also `bios/gb_bios.bin` | no | `libretro/mesen-s` | `32fbbd84168d3482956eb3c5051637f5` world; BizHawk's FirmwareDatabase.cs, RomM's known_bios_files | `mesen-s`: `Core/FirmwareHelper.h:148, Core/Gameboy.cpp:108-114, Core/GbBootRom.h:7` |
| `bios/dsp1.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `3d81b45fa0c2aa8b852dfb1ece7c0971` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1.program.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `ae209fbe789fbf11a48aea5ab1197321` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/heuristics/super-famicom.cpp:601`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:95, Core/FirmwareHelper.h:17-35` |
| `bios/dsp1b.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `1e3f568634a7d8284020dddc0ae905bc` no region; RomM's known_bios_files | `bsnes`: `bsnes/heuristics/super-famicom.cpp:606`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp1b.program.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `d10f446888e097cbf500f3f663cf4f6d` no region; RomM's known_bios_files | `bsnes`: `bsnes/heuristics/super-famicom.cpp:606`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:506-513`<br>`mesen-s`: `Core/NecDsp.cpp:96` |
| `bios/dsp2.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `e9417e29223b139c3c4b635a2a3b8744` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:515-520`<br>`mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp2.program.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `aa6e5922a3ed5ded54f24247c11143c5` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:515-520, bsnes/heuristics/super-famicom.cpp:602`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:515-520`<br>`mesen-s`: `Core/NecDsp.cpp:97` |
| `bios/dsp3.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `0a81210c0a940b997dd9843281008ee6` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:531-534`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:531-534`<br>`mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp3.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `d99ca4562818d49cee1f242705bba6f8` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:531-534`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:531-534`<br>`mesen-s`: `Core/NecDsp.cpp:98` |
| `bios/dsp4.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `ee4990879eb68e3cbca239c5bc20303d` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:498-503`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:498-503, bsnes/sfc/cartridge/load.cpp:522-528`<br>`mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/dsp4.program.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `a151023b948b90ffc23a5b594bb6fef2` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:522-528, bsnes/heuristics/super-famicom.cpp:604-605`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:490-494, bsnes/sfc/cartridge/load.cpp:522-528`<br>`mesen-s`: `Core/NecDsp.cpp:99` |
| `bios/SGB1.sfc` | no | `libretro/mesen-s` | `b15ddb15721c657d82c5bab6db982ee9` no region; retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/SGB2.sfc` | no | `libretro/mesen-s` | `8ecd73eb4edf7ed7e81aef1be80031d5` no region; BizHawk's FirmwareDatabase.cs, retrobios `bsnes`, retrobios `bsnes_hd_beta`, RetroBat's list, RomM's known_bios_files | `bsnes`: `bsnes/target-libretro/libretro.cpp:937-946 (system_dir + sgb_bios), bsnes/target-libretro/libretro.cpp:393 (core option bsnes_sgb_bios)`<br>`bsnes_hd_beta`: `bsnes/target-libretro/libretro.cpp:1022, bsnes/target-libretro/libretro.cpp:1040`<br>`mesen-s`: `Core/FirmwareHelper.h:118-133, Core/BaseCartridge.cpp:630-638` |
| `bios/st010.data.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `254d70762b6f59f99c27c395aba7d07d` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:578-583`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:578-583, bsnes/sfc/cartridge/load.cpp:586-596`<br>`mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st010.program.rom` | no | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `1d70019179a59a566a0bb5d3f2845544` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/heuristics/super-famicom.cpp:583-587`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/sfc/cartridge/load.cpp:586-596`<br>`mesen-s`: `Core/NecDsp.cpp:100` |
| `bios/st011.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `10bd3f4aa949737ab9836512c35bcc29` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:599-601`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:578-583, bsnes/sfc/cartridge/load.cpp:599-602`<br>`mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st011.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta`, `libretro/mesen-s` | `95222ebf1c0c2990bcf25db43743f032` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:599-601, bsnes/heuristics/super-famicom.cpp:586`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:570-574, bsnes/sfc/cartridge/load.cpp:599-602`<br>`mesen-s`: `Core/NecDsp.cpp:101` |
| `bios/st018.data.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta` | `49c898b60d0f15e90d0ba780dd12f366` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:387-393, bsnes/sfc/coprocessor/armdsp/armdsp.hpp:32`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:387-393` |
| `bios/st018.program.rom` | yes | `libretro/bsnes`, `libretro/bsnes_hd_beta` | `dda40ccd57390c96e49d30a041f9a9e7` no region; RomM's known_bios_files | `bsnes`: `bsnes/sfc/cartridge/load.cpp:379-385, bsnes/sfc/coprocessor/armdsp/armdsp.hpp:31, bsnes/heuristics/super-famicom.cpp:580`<br>`bsnes_hd_beta`: `bsnes/sfc/cartridge/load.cpp:379-384` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/sgb2_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |
| `bios/sgb_boot.bin` | `libretro/mesen-s` | no identity: no source states a hash for it |
| `emulators/mesen/Firmware/dsp1.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp1b.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp2.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp3.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/dsp4.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st010.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.data.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.program.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st011.rom` | `mesen` | outside bios/, where RomMBat never writes |
| `emulators/mesen/Firmware/st018.rom` | `mesen` | outside bios/, where RomMBat never writes |

## spectravideo

spectravideo: 0 slots, 2 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/Machines/SVI - Spectravideo SVI-318/svi318.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |
| `bios/Machines/SVI - Spectravideo SVI-328/svi328.rom` | `libretro/bluemsx` | no identity: no source states a hash for it |

## sufami

Sufami: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/STBIOS.bin` | yes | `libretro/snes9x`, RetroBat's list | `d3a44ba7d42a74d3ac58cb9c14c6a5ca` no region; RetroBat's list | `snes9x`: `libretro/libretro.cpp:1506, libretro/libretro.cpp:1546, memmap.cpp:1720` |

## supergrafx

Supergrafx: 4 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/gexpress.pce` | no | `libretro/geargrafx`, `libretro/mednafen_supergrafx` | `6d2cb14fc3e1f65ceb135633d1694122` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1057-1058, mednafen/settings.cpp:41, mednafen/pce/pce.cpp:374-411`<br>`geargrafx`: `platforms/libretro/libretro.cpp:715, platforms/libretro/libretro.cpp:748-749, src/media.cpp:662-705, src/media.cpp:821-823, src/media.cpp:835-839, src/media.cpp:993-997, src/defines.h:54, src/game_db.h:55-56`<br>`mednafen_supergrafx`: `libretro.cpp:978-979, mednafen/pce_fast/pce.cpp:330-367,453-454` |
| `bios/syscard1.pce` | no | `libretro/geargrafx`, `libretro/mednafen_supergrafx` | `2b7ccb3d86baa18f6402c176f3065082` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1055-1056`<br>`geargrafx`: `platforms/libretro/libretro.cpp:712, platforms/libretro/libretro.cpp:720-721, src/media.cpp:662-705, src/defines.h:53, src/game_db.h:48`<br>`mednafen_supergrafx`: `libretro.cpp:976-977` |
| `bios/syscard2.pce` | no | `libretro/geargrafx`, `libretro/mednafen_supergrafx` | `3cdd6614a918616bfc41c862e889dd79` japan; retrobios `beetle_pce`, RomM's known_bios_files | `beetle_pce`: `libretro.cpp:1053-1054`<br>`geargrafx`: `platforms/libretro/libretro.cpp:713, platforms/libretro/libretro.cpp:723-724, src/media.cpp:662-705, src/defines.h:53, src/game_db.h:49-50, src/game_db.h:53`<br>`mednafen_supergrafx`: `libretro.cpp:974-975` |
| `bios/syscard3.pce` | yes | `libretro/geargrafx`, `libretro/mednafen_supergrafx`, RetroBat's list | `38179df8f4ac870017db21ebcbf53114` japan; BizHawk's FirmwareDatabase.cs, retrobios `beetle_pce`, RetroBat's list | `beetle_pce`: `libretro.cpp:1051-1052, mednafen/settings.cpp:40`<br>`geargrafx`: `platforms/libretro/libretro.cpp:714, platforms/libretro/libretro.cpp:726-731, platforms/libretro/libretro.cpp:736-746, src/media.cpp:662-705, src/defines.h:53, src/game_db.h:51-52, src/game_db.h:54`<br>`mednafen_supergrafx`: `libretro.cpp:972-973, mednafen/settings.cpp:37` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/pac-n1.bin` | `libretro/geargrafx` | no identity: no source states a hash for it |
| `bios/pac-n10.bin` | `libretro/geargrafx` | no identity: no source states a hash for it |
| `bios/pce-lp1.bin` | `libretro/geargrafx` | no identity: no source states a hash for it |

## supracan

Super A Can: 2 slots, 1 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/supracan.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |
| `bios/umc6650.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/supracan.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## switch

switch: 0 slots, 17 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `firmware NCAs` | every emulator | installed by the emulator: each Switch emulator installs the system firmware's NCA files through its own menu |
| `switch/console.keys` | `citron`, `eden`, `ryujinx`, `yuzu` | citron has no cited firmware directory; eden has no cited firmware directory; ryujinx has no cited firmware directory; yuzu has no cited firmware directory |
| `switch/dev.keys` | `eden` | eden has no cited firmware directory |
| `switch/key_retail.bin` | `citron`, `eden`, `yuzu` | citron has no cited firmware directory; eden has no cited firmware directory; yuzu has no cited firmware directory |
| `switch/prod.keys` | `citron`, `eden`, `ryujinx`, `yuzu` | citron has no cited firmware directory; eden has no cited firmware directory; ryujinx has no cited firmware directory; yuzu has no cited firmware directory |
| `switch/title.keys` | `citron`, `eden`, `ryujinx`, `yuzu` | citron has no cited firmware directory; eden has no cited firmware directory; ryujinx has no cited firmware directory; yuzu has no cited firmware directory |

## ti99

TI/99: 2 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/ti99_4a.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `994a_grom0.u500` crc32 `2445a5e8` 6144 B, `994a_grom1.u501` crc32 `b8f367ab` 6144 B, `994a_grom2.u502` crc32 `e0bb5341` 6144 B, `994a_rom_hb.u610` crc32 `ee859c5f` 4096 B, `994a_rom_lb.u611` crc32 `37859301` 4096 B | `mame`: `src/mame/ti/ti99_4x.cpp:1153-1166` |
| `bios/ti99_speech.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `cd2325a.u2a` crc32 `1f58b571` 16384 B, `cd2326a.u2b` crc32 `65d00401` 16384 B | `mame`: `src/devices/bus/ti99/sidecar/speechsyn.cpp:242-246` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/ti99_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `ti99_4a.zip` | `mame64/mame64` | mame64 has no cited firmware directory |
| `ti99_speech.zip` | `mame64/mame64` | mame64 has no cited firmware directory |

## triforce

Triforce: 2 slots, 72 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/GBA/gba_bios.bin` | no | `libretro/dolphin` | `a860e8c0b6d573d191e4ec7db1b1e4f6` world; RomM's known_bios_files | `dolphin`: `Source/Core/Common/CommonPaths.h:145, Source/Core/Core/HW/GBACore.cpp:346-363` |
| `bios/triforce/segaboot.gcm` | yes | `libretro/dolphin`, RetroBat's list | `e475c7dc6c3b236a3e4bd8e104e97fb0` no region; another source's hash for this name | `dolphin`: `Source/Core/Core/HW/DVD/AMMediaboard.cpp:163, Source/Core/Core/HW/DVD/AMMediaboard.cpp:521, Source/Core/Core/HW/DVD/AMMediaboard.cpp:551-571, Source/Core/Core/HW/DVD/DVDInterface.cpp:292-294, Source/Core/Core/System.h:147` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/codehandler.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_coef.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_rom.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/EUR/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_japanese.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_western.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/JAP/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/USA/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/keys.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Load/WiiSD.raw` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/nand.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientcakey.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/rootca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/shared2/sys/SYSCONF` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/title/00000001/00000002/data/setting.txt` | `libretro/dolphin` | no identity: no source states a hash for it |
| `codehandler.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GBA/gba_bios.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/dsp_coef.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/dsp_rom.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/EUR/IPL.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/EUR/IPL_TRI.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `GC/font_japanese.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/font_western.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/JAP/IPL.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/JAP/IPL_TRI.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `GC/USA/IPL.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `GC/USA/IPL_TRI.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `keys.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723d_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821c_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8851bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852btu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8922au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/WiiSD.raw` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `nand.bin` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Sys/codehandler.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `Sys/GC/font_japanese.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `Sys/GC/font_western.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `Sys/Triforce/segaboot.gcm` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `Triforce/segaboot.gcm` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii System Menu` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Wii/clientca.pem` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Wii/clientcakey.pem` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Wii/keys.bin` | `dolphin-triforce` | dolphin-triforce has no cited firmware directory |
| `Wii/rootca.pem` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Wii/shared2/sys/SYSCONF` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |
| `Wii/title/00000001/00000002/data/setting.txt` | `dolphin-triforce`, `dolphin/dolphin` | dolphin has no cited firmware directory; dolphin-triforce has no cited firmware directory |

## tutor

APF M-1000: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/tutor.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `tutor1.bin` crc32 `702c38ba` 32768 B, `tutor2.bin` crc32 `05f228f5` 16384 B | `mame`: `src/mame/tomy/tutor.cpp:796-799` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/tutor.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `tutor.zip` | `mame64/tutor` | mame64 has no cited firmware directory |

## vc4000

VC4000: 0 slots, 1 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/vc4000.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |

## vectrex

Vectrex: 3 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/VEC_Bios.bin` | yes | `bizhawk/VectrexHawk` | `ba9260bb961d60e718378cc16a8051d7` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/VEC_Minestorm.vec` | yes | `bizhawk/VectrexHawk` | `ab082fa8c8e632dd68589a8c7741388f` world; BizHawk's FirmwareDatabase.cs |  |
| `bios/Vectrex_Bios.bin` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

## vg5k

VG5000: 1 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/vg5k.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `charset.rom` crc32 `b2f49eb3` 8192 B, `vg5k10.bin` crc32 `57983260` 16384 B, `vg5k11.bin` crc32 `a6f4a0ea` 16384 B | `mame`: `src/mame/philips/vg5k.cpp:463-472` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/vg5k.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `vg5k.zip` | `mame64/vg5k` | mame64 has no cited firmware directory |

## vircon32

vircon32: 1 slots, 0 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/Vircon32Bios.v32` | no | `libretro/vircon32` | `73f04397cbb2dafcf2047b19a856153a` no region; retrobios `vircon32` | `vircon32`: `libretro.cpp:422-425` |

## vpinball

vpinball: 0 slots, 10 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `assets/fabian-hrir.sofa` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/allied.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gp_110.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts1.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts1s.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts80.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts80a.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts80as.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/gts80s.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |
| `pinmame/roms/recel.zip` | `vpinball/vpinball` | vpinball has no cited firmware directory |

## vsmile

V. Smile: 1 slots, 5 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/vsmile.zip` | yes | `libretro/mame`, RetroBat's list | zip, for `libretro/mame`, RetroBat's list: `vsmile_v100.bin` crc32 `205c5296` 2097152 B, `vsmile_v102.bin` crc32 `0cd0bdf5` 2097152 B, `vsmile_v103.bin` crc32 `387fbc24` 2097152 B | `mame`: `src/mame/vtech/vsmile.cpp:364-394` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/mame/hash/vsmile_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/vsmile_cd.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/vsmileb_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bios/mame/hash/vsmilem_cart.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `vsmile.zip` | `mame64/vsmile` | mame64 has no cited firmware directory |

## wii

wii: 1 slots, 49 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/GBA/gba_bios.bin` | no | `libretro/dolphin` | `a860e8c0b6d573d191e4ec7db1b1e4f6` world; RomM's known_bios_files | `dolphin`: `Source/Core/Common/CommonPaths.h:145, Source/Core/Core/HW/GBACore.cpp:346-363` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/codehandler.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_coef.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/dsp_rom.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/EUR/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_japanese.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/font_western.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/JAP/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/GC/USA/IPL.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/keys.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Load/WiiSD.raw` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/nand.bin` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/clientcakey.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/rootca.pem` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/shared2/sys/SYSCONF` | `libretro/dolphin` | no identity: no source states a hash for it |
| `bios/Wii/title/00000001/00000002/data/setting.txt` | `libretro/dolphin` | no identity: no source states a hash for it |
| `codehandler.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GBA/gba_bios.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/dsp_coef.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/dsp_rom.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/EUR/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/font_japanese.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/font_western.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/JAP/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `GC/USA/IPL.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `keys.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8723d_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8761bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821a_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8821c_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822b_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8822cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8851bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852btu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852bu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8852cu_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/Firmware/rtl_bt/rtl8922au_fw.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Load/WiiSD.raw` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `nand.bin` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii System Menu` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/clientca.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/clientcakey.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/rootca.pem` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/shared2/sys/SYSCONF` | `dolphin/dolphin` | dolphin has no cited firmware directory |
| `Wii/title/00000001/00000002/data/setting.txt` | `dolphin/dolphin` | dolphin has no cited firmware directory |

## wiiu

wiiu: 0 slots, 63 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `cafeLibs/erreula.rpl` | `cemu` | cemu has no cited firmware directory |
| `cafeLibs/snd_user.rpl` | `cemu` | cemu has no cited firmware directory |
| `cafeLibs/snduser2.rpl` | `cemu` | cemu has no cited firmware directory |
| `cafeLibs/swkbd.rpl` | `cemu` | cemu has no cited firmware directory |
| `keys.txt` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10042400/content/CafeCn.ttf` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10042400/content/CafeKr.ttf` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10042400/content/CafeStd.ttf` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10042400/content/CafeTw.ttf` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/1004f000/content/00/swkbd/swkbd.pack` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10051000/content/00/erreula/erreula.pack` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_ACCOUNT_1_CERT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_ACCOUNT_1_RSA_KEY.aes` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_COMMON_1_CERT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_COMMON_1_RSA_KEY.aes` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_OLIVE_1_CERT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_OLIVE_1_RSA_KEY.aes` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_VINO_1_CERT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_VINO_1_RSA_KEY.aes` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_WOOD_1_CERT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/ccerts/WIIU_WOOD_1_RSA_KEY.aes` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/ADDTRUST_EXT_CA_ROOT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/BALTIMORE_CYBERTRUST_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CA_G3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CLASS2_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CLASS2_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CACERT_NINTENDO_CLASS2_CA_G3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/COMODO_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/CYBERTRUST_GLOBAL_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/DIGICERT_ASSURED_ID_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/DIGICERT_ASSURED_ID_ROOT_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/DIGICERT_GLOBAL_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/DIGICERT_GLOBAL_ROOT_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/DIGICERT_HIGH_ASSURANCE_EV_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/ENTRUST_CA_2048.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/ENTRUST_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/ENTRUST_ROOT_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/ENTRUST_SECURE_SERVER_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/EQUIFAX_SECURE_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GEOTRUST_GLOBAL_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GEOTRUST_GLOBAL_CA2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GEOTRUST_PRIMARY_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GEOTRUST_PRIMARY_CA_G3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GLOBALSIGN_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GLOBALSIGN_ROOT_CA_R2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GLOBALSIGN_ROOT_CA_R3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/GTE_CYBERTRUST_GLOBAL_ROOT.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/THAWTE_PREMIUM_SERVER_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/THAWTE_PRIMARY_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/THAWTE_PRIMARY_ROOT_CA_G3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/UTN_DATACORP_SGC_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/UTN_USERFIRST_HARDWARE_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERISIGN_CLASS3_PUBLIC_PRIMARY_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERISIGN_CLASS3_PUBLIC_PRIMARY_CA_G2.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERISIGN_CLASS3_PUBLIC_PRIMARY_CA_G3.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERISIGN_CLASS3_PUBLIC_PRIMARY_CA_G5.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERISIGN_UNIVERSAL_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `mlc01/sys/title/0005001b/10054000/content/scerts/VERIZON_GLOBAL_ROOT_CA.der` | `cemu` | cemu has no cited firmware directory |
| `otp.bin` | `cemu` | cemu has no cited firmware directory |
| `seeprom.bin` | `cemu` | cemu has no cited firmware directory |
| `shareddata.bin` | `cemu` | cemu has no cited firmware directory |

## x1

Sharp x1: 2 slots, 3 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/xmil/IPLROM.X1` | yes | `libretro/x1`, RetroBat's list | `eeeea1cd29c6e0e8b094790ae969bfa7` no region; RetroBat's list | `x1`: `io/memio.c:82,96-108` |
| `bios/xmil/iplrom.x1t` | yes | `libretro/x1`, RetroBat's list | `56c28adcf1f3a2f87cf3d57c378013f5` no region; RetroBat's list | `x1`: `io/memio.c:83,97-108` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/xmil/FNT0808.X1` | `libretro/x1` | no identity: no source states a hash for it |
| `bios/xmil/FNT0816.X1` | `libretro/x1` | no identity: no source states a hash for it |
| `bios/xmil/FNT1616.X1` | `libretro/x1` | no identity: no source states a hash for it |

## x68000

Sharp x68000: 5 slots, 8 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/keropi/cgrom.dat` | yes | `libretro/px68k`, RetroBat's list | `cb0a5cfcf7247a7eab74bb2716260269` no region; RetroBat's list | `px68k`: `libretro.c:877, libretro.c:912-922` |
| `bios/keropi/iplrom.dat` | yes | `libretro/px68k`, RetroBat's list | `7fd4caabac1d9169e289f0f7bbf71d8e` no region; RetroBat's list | `px68k`: `libretro.c:874-891` |
| `bios/keropi/iplrom30.dat` | no | `libretro/px68k` | `f373003710ab4322642f527f567e020a` no region, priority 2; RomM's known_bios_files | `px68k`: `libretro.c:875` |
| `bios/keropi/iplromco.dat` | no | `libretro/px68k` | `cc78d4f4900f622bd6de1aed7f52592f` no region, priority 3; RomM's known_bios_files | `px68k`: `libretro.c:875` |
| `bios/keropi/iplromxv.dat` | no | `libretro/px68k` | `0617321daa182c3f3d6f41fd02fb3275` no region, priority 4; RomM's known_bios_files | `px68k`: `libretro.c:875` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/keropi/cgrom.tmp` | `libretro/px68k` | no identity: no source states a hash for it |
| `CGROM.TMP` | `xm6pro` | xm6pro has no cited firmware directory |
| `IPLROM.DAT` | `xm6pro` | xm6pro has no cited firmware directory |
| `IPLROM30.DAT` | `xm6pro` | xm6pro has no cited firmware directory |
| `IPLROMCO.DAT` | `xm6pro` | xm6pro has no cited firmware directory |
| `IPLROMXV.DAT` | `xm6pro` | xm6pro has no cited firmware directory |
| `ROM30.DAT` | `xm6pro` | xm6pro has no cited firmware directory |
| `SRAM.DAT` | `xm6pro` | xm6pro has no cited firmware directory |

## xbox

XBox: 2 slots, 8 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/Complex_4627.bin` | yes | RetroBat's list | `39cee882148a87f93cb440b99dde3ceb` no region; RetroBat's list |  |
| `bios/mcpx_1.0.bin` | yes | RetroBat's list | `d49c52a4102f6df7bcf8d0617ac475ed` no region; RetroBat's list |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `Complex_4627.bin` | `xemu` | xemu has no cited firmware directory |
| `EEPROM.bin` | `cxbx`, `xemu` | cxbx has no cited firmware directory; xemu has no cited firmware directory |
| `EmuDisk/Partition2/xboxdash.xbe` | `cxbx` | cxbx has no cited firmware directory |
| `keys.bin` | `cxbx` | cxbx has no cited firmware directory |
| `mcpx_1.0.bin` | `xemu` | xemu has no cited firmware directory |
| `wpcap.dll` | `xemu` | xemu has no cited firmware directory |
| `xbox_hdd.qcow2` | `xemu` | xemu has no cited firmware directory |

## xbox360

xbox360: 0 slots, 5 files reported.

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `D3D12/D3D12Core.dll` | `xenia-edge` | xenia-edge has no cited firmware directory |
| `D3D12/D3D12SDKLayers.dll` | `xenia-edge` | xenia-edge has no cited firmware directory |
| `D3D12/dxil.dll` | `xenia-edge` | xenia-edge has no cited firmware directory |
| `vc_redist.x64.exe` | `xenia-edge` | xenia-edge has no cited firmware directory |
| `Vulkan/vulkan-1.dll` | `xenia-edge` | xenia-edge has no cited firmware directory |

## xegs

Atari XEGS: 1 slots, 106 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/xegs.zip` | yes | RetroBat's list | no identity: RetroBat names it without a hash and no source gives one |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `1050-duplicator.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo-ii-3.4.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo-ii-3.5.rom` | `altirra` | altirra has no cited firmware directory |
| `1050-turbo.rom` | `altirra` | altirra has no cited firmware directory |
| `1400xl-handler-v1.rom` | `altirra` | altirra has no cited firmware directory |
| `1400xl-handler-v2.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-controller.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-post-rev-f.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-disk-handler-rev-f.rom` | `altirra` | altirra has no cited firmware directory |
| `1450xld-tong-disk-controller.rom` | `altirra` | altirra has no cited firmware directory |
| `810-archiver.rom` | `altirra` | altirra has no cited firmware directory |
| `810-turbo-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `amdek-amdc.rom` | `altirra` | altirra has no cited firmware directory |
| `ape-warp-os.rom` | `altirra` | altirra has no cited firmware directory |
| `astra-1001-1620.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1025.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1029-english.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-download.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-external.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1030-internal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-h.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-j.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-k.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1050-rev-l.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1090-charset.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1090-firmware-rev-9.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1200xl-os-rev-10.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-1200xl-os-rev-11.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-a-ntsc.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-a-pal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-ntsc-xformer.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-ntsc.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-800-os-b-pal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-b.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-c.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-810-rev-e.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-815.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-820.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-835-internal.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-850-early.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-850.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-a.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-b.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-basic-rev-c.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xegs-missile-command.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xegs-os-ver-4.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-1.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-2.rom` | `altirra` | altirra has no cited firmware directory |
| `atari-xl-os-ver-3.rom` | `altirra` | altirra has no cited firmware directory |
| `atr8000-3.02.rom` | `altirra` | altirra has no cited firmware directory |
| `bios/mame/hash/xegs.xml` | RetroBat's list | a MAME software list RetroBat ships, not firmware |
| `bit3-full-view-80-charset-4k.rom` | `altirra` | altirra has no cited firmware directory |
| `bit3-full-view-80-charset.rom` | `altirra` | altirra has no cited firmware directory |
| `bit3-full-view-80-firmware.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-1.34.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-1.41.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-2.16.rom` | `altirra` | altirra has no cited firmware directory |
| `blackbox-floppy-board-1.5.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-1050-rev-1.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-1050-rev-2.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-3k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-4k-nonoriginal.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-prev7-4k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-6k-modified.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-6k.rom` | `altirra` | altirra has no cited firmware directory |
| `happy-810-v7-8k.rom` | `altirra` | altirra has no cited firmware directory |
| `indus-gt-1.1.rom` | `altirra` | altirra has no cited firmware directory |
| `indus-gt-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `is-plate.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide2-main.rom` | `altirra` | altirra has no cited firmware directory |
| `kmkjz-ide2-sdx.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.1k-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.1m-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-128kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-256kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `mio-1.41-64kbit.rom` | `altirra` | altirra has no cited firmware directory |
| `myide2.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at-88-1.2-damaged.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at-88-1.2.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.01.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.11.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-at88-spd-1.21.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.00.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.10.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-1.20.rom` | `altirra` | altirra has no cited firmware directory |
| `percom-rfd-2.10.rom` | `altirra` | altirra has no cited firmware directory |
| `rapidus-core-pbi.rom` | `altirra` | altirra has no cited firmware directory |
| `rapidus-flash.rom` | `altirra` | altirra has no cited firmware directory |
| `side.rom` | `altirra` | altirra has no cited firmware directory |
| `side2.rom` | `altirra` | altirra has no cited firmware directory |
| `side3.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-xf-1.7.rom` | `altirra` | altirra has no cited firmware directory |
| `speedy-xf-1.8.rom` | `altirra` | altirra has no cited firmware directory |
| `super-archiver.rom` | `altirra` | altirra has no cited firmware directory |
| `toms-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `tygrys-1050.rom` | `altirra` | altirra has no cited firmware directory |
| `ultimate1mb.rom` | `altirra` | altirra has no cited firmware directory |
| `us-doubler-rev-l.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.4.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7-720k.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7-density-hack.rom` | `altirra` | altirra has no cited firmware directory |
| `xf551-7.7.rom` | `altirra` | altirra has no cited firmware directory |

## zinc

zinc: 2 slots, 2 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/coh1000c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp01.ic652` crc32 `bb7ba033` 8 B, `m534002c-13.ic353` crc32 `50033af6` 524288 B, `msm27c402zb.ic353` crc32 `f20f7fe5` 524288 B | `mame`: `src/mame/sony/zn.cpp:5969` |
| `bios/coh3002c.zip` | yes | `libretro/mame` | zip, for `libretro/mame`: `cp10.ic652` crc32 `7808fcef` 8 B, `m534002c-59.ic353` crc32 `e860ea8b` 524288 B | `mame`: `src/mame/sony/zn.cpp:6055` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `coh1000c.zip` | `mame64` | mame64 has no cited firmware directory |
| `coh3002c.zip` | `mame64` | mame64 has no cited firmware directory |

## zx81

zx81: 31 slots, 6 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/fuse/128-0.rom` | no | `libretro/fuse` | `b4d2692115a9f2924df92a3cbfb358fb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:214, fuse/machines/spec128.c:70` |
| `bios/fuse/128-1.rom` | no | `libretro/fuse` | `6e09e5d3c4aef166601669feaaadc01c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:215, fuse/machines/spec128.c:73` |
| `bios/fuse/128p-0.rom` | yes | `libretro/fuse` | `a249565f03b98d004ee7f019570069cd` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:226, fuse/settings.c:230, fuse/settings.c:234, fuse/machines/pentagon.c:131` |
| `bios/fuse/128p-1.rom` | yes | `libretro/fuse` | `6e09e5d3c4aef166601669feaaadc01c` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:227, fuse/settings.c:231, fuse/settings.c:235, fuse/machines/pentagon.c:134` |
| `bios/fuse/256s-0.rom` | yes | `libretro/fuse` | `b9fda5b6a747ff037365b0e2d8c4379a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:252, fuse/machines/scorpion.c:76` |
| `bios/fuse/256s-1.rom` | yes | `libretro/fuse` | `643861ad34831b255bf2eb64e8b6ecb8` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:253, fuse/machines/scorpion.c:79` |
| `bios/fuse/256s-2.rom` | yes | `libretro/fuse` | `d8ad507b1c915a9acfe0d73957082926` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:254, fuse/machines/scorpion.c:82` |
| `bios/fuse/256s-3.rom` | yes | `libretro/fuse` | `ce0723f9bc02f4948c15d3b3230ae831` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:255, fuse/machines/scorpion.c:85` |
| `bios/fuse/48.rom` | no | `libretro/fuse` | `4c42a2f075212361c3117015b107ff68` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:216-217, fuse/machines/spec48.c:76` |
| `bios/fuse/disciple.rom` | no | `libretro/fuse` | `78e61a2a02121873c1756b21fd1398b1` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:220, fuse/peripherals/disk/disciple.c:279` |
| `bios/fuse/gluck.rom` | yes | `libretro/fuse` | `d5869034604dbfd2c1d54170e874fd0a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:229, fuse/settings.c:233, fuse/machines/pentagon512.c:82` |
| `bios/fuse/if1-2.rom` | no | `libretro/fuse` | `31b704ae925305e74f50699271fddd9a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:221, fuse/peripherals/if1.c:404` |
| `bios/fuse/plus2-0.rom` | no | `libretro/fuse` | `4ed7af4636308b8a48d7a35e6c5b546b` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:237, fuse/machines/specplus2.c:70` |
| `bios/fuse/plus2-1.rom` | no | `libretro/fuse` | `b3db95931cc844efaeb82db9c171b9f3` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:238, fuse/machines/specplus2.c:73` |
| `bios/fuse/plus3-0.rom` | no | `libretro/fuse` | `9833b8b73384dd5fa3678377ff00a2bb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:239, fuse/settings.c:243, fuse/machines/specplus3.c:193` |
| `bios/fuse/plus3-1.rom` | no | `libretro/fuse` | `0f711ceb5ab801b4701989982e0f334c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:240, fuse/settings.c:244, fuse/machines/specplus3.c:196` |
| `bios/fuse/plus3-2.rom` | no | `libretro/fuse` | `3b6dd659d5e4ec97f0e2f7878152c987` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:241, fuse/settings.c:245, fuse/machines/specplus3.c:199` |
| `bios/fuse/plus3-3.rom` | no | `libretro/fuse` | `a148bcc575e51389e84fdf5d555c3196` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:242, fuse/settings.c:246, fuse/machines/specplus3.c:202` |
| `bios/fuse/plus3e-0.rom` | no | `libretro/fuse` | `bc123f625e245c225f92ef05933ed134` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:247, fuse/machines/specplus3e.c:66` |
| `bios/fuse/plus3e-1.rom` | no | `libretro/fuse` | `617364264c587d20c9fc4746c29679f2` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:248, fuse/machines/specplus3e.c:69` |
| `bios/fuse/plus3e-2.rom` | no | `libretro/fuse` | `c363e95dcd0a90e6e7f847e6e47e0179` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:249, fuse/machines/specplus3e.c:72` |
| `bios/fuse/plus3e-3.rom` | no | `libretro/fuse` | `a148bcc575e51389e84fdf5d555c3196` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:250, fuse/machines/specplus3e.c:75` |
| `bios/fuse/plusd.rom` | no | `libretro/fuse` | `42e5de16fb5e50082bb954ec7ce45851` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:251, fuse/peripherals/disk/plusd.c:239` |
| `bios/fuse/se-0.rom` | no | `libretro/fuse` | `fb3f86eb1e5a695d9c50c124e7cfb875` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:257, fuse/machines/spec_se.c:89` |
| `bios/fuse/se-1.rom` | no | `libretro/fuse` | `88de4a3129086f34bb9ca559acc51e6c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:258, fuse/machines/spec_se.c:92` |
| `bios/fuse/speccyboot-1.4.rom` | no | `libretro/fuse` | `c54aa8f374b0971f51546c29d5d1eba1` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:259, fuse/peripherals/speccyboot.c:148` |
| `bios/fuse/tc2048.rom` | no | `libretro/fuse` | `9dd7ecf784a6c04265c073c236f5fadb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:260, fuse/machines/tc2048.c:80` |
| `bios/fuse/tc2068-0.rom` | no | `libretro/fuse` | `55d462fccc6c536037404ef4ced08bec` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:261, fuse/settings.c:263, fuse/machines/tc2068.c:138` |
| `bios/fuse/tc2068-1.rom` | no | `libretro/fuse` | `575d203c6e15e679fba0b73f854ec7a2` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:262, fuse/settings.c:264, fuse/machines/tc2068.c:141` |
| `bios/fuse/trdos.rom` | yes | `libretro/fuse` | `0c42e3b9ab8dd91ea096f1d0c07c55e5` no region; retrobios `fuse`<br>`0da70a5d2a0e733398e005b96b7e4ba6` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:228, fuse/settings.c:232, fuse/settings.c:236, fuse/machines/pentagon.c:137` |
| `bios/fuse/usource.rom` | no | `libretro/fuse` | `ebe1490920cbb62bde7282ae24928d95` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:266, fuse/peripherals/usource.c:129` |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/fuse/didaktik80.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/mf1.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/mf128.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/mf3.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/opus.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/ttx2000s.rom` | `libretro/fuse` | no identity: no source states a hash for it |

## zxspectrum

zxspectrum: 38 slots, 4 files reported.

| Slot | Required | Readers | Accepted | Evidence |
| --- | --- | --- | --- | --- |
| `bios/fuse/128-0.rom` | no | `libretro/fuse` | `b4d2692115a9f2924df92a3cbfb358fb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:214, fuse/machines/spec128.c:70` |
| `bios/fuse/128-1.rom` | no | `libretro/fuse` | `6e09e5d3c4aef166601669feaaadc01c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:215, fuse/machines/spec128.c:73` |
| `bios/fuse/128p-0.rom` | yes | `libretro/fuse` | `a249565f03b98d004ee7f019570069cd` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:226, fuse/settings.c:230, fuse/settings.c:234, fuse/machines/pentagon.c:131` |
| `bios/fuse/128p-1.rom` | yes | `libretro/fuse` | `6e09e5d3c4aef166601669feaaadc01c` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:227, fuse/settings.c:231, fuse/settings.c:235, fuse/machines/pentagon.c:134` |
| `bios/fuse/256s-0.rom` | yes | `libretro/fuse` | `b9fda5b6a747ff037365b0e2d8c4379a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:252, fuse/machines/scorpion.c:76` |
| `bios/fuse/256s-1.rom` | yes | `libretro/fuse` | `643861ad34831b255bf2eb64e8b6ecb8` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:253, fuse/machines/scorpion.c:79` |
| `bios/fuse/256s-2.rom` | yes | `libretro/fuse` | `d8ad507b1c915a9acfe0d73957082926` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:254, fuse/machines/scorpion.c:82` |
| `bios/fuse/256s-3.rom` | yes | `libretro/fuse` | `ce0723f9bc02f4948c15d3b3230ae831` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:255, fuse/machines/scorpion.c:85` |
| `bios/fuse/48.rom` | no | `libretro/fuse` | `4c42a2f075212361c3117015b107ff68` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:216-217, fuse/machines/spec48.c:76` |
| `bios/fuse/disciple.rom` | no | `libretro/fuse` | `78e61a2a02121873c1756b21fd1398b1` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:220, fuse/peripherals/disk/disciple.c:279` |
| `bios/fuse/gluck.rom` | yes | `libretro/fuse` | `d5869034604dbfd2c1d54170e874fd0a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:229, fuse/settings.c:233, fuse/machines/pentagon512.c:82` |
| `bios/fuse/if1-2.rom` | no | `libretro/fuse` | `31b704ae925305e74f50699271fddd9a` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:221, fuse/peripherals/if1.c:404` |
| `bios/fuse/mf1.rom` | no | `libretro/fuse` | `ad539b4d026dd53a8442505f51441dfc` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:222, fuse/peripherals/multiface.c:224` |
| `bios/fuse/mf128.rom` | no | `libretro/fuse` | `c420ef03f6e0c477d9474efe3441a556` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:223, fuse/peripherals/multiface.c:225` |
| `bios/fuse/mf3.rom` | no | `libretro/fuse` | `afe2218c3a6a43f1854fe824424d4167` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:224, fuse/peripherals/multiface.c:226` |
| `bios/fuse/plus2-0.rom` | no | `libretro/fuse` | `4ed7af4636308b8a48d7a35e6c5b546b` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:237, fuse/machines/specplus2.c:70` |
| `bios/fuse/plus2-1.rom` | no | `libretro/fuse` | `b3db95931cc844efaeb82db9c171b9f3` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:238, fuse/machines/specplus2.c:73` |
| `bios/fuse/plus3-0.rom` | no | `libretro/fuse` | `9833b8b73384dd5fa3678377ff00a2bb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:239, fuse/settings.c:243, fuse/machines/specplus3.c:193` |
| `bios/fuse/plus3-1.rom` | no | `libretro/fuse` | `0f711ceb5ab801b4701989982e0f334c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:240, fuse/settings.c:244, fuse/machines/specplus3.c:196` |
| `bios/fuse/plus3-2.rom` | no | `libretro/fuse` | `3b6dd659d5e4ec97f0e2f7878152c987` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:241, fuse/settings.c:245, fuse/machines/specplus3.c:199` |
| `bios/fuse/plus3-3.rom` | no | `libretro/fuse` | `a148bcc575e51389e84fdf5d555c3196` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:242, fuse/settings.c:246, fuse/machines/specplus3.c:202` |
| `bios/fuse/plus3e-0.rom` | no | `libretro/fuse` | `bc123f625e245c225f92ef05933ed134` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:247, fuse/machines/specplus3e.c:66` |
| `bios/fuse/plus3e-1.rom` | no | `libretro/fuse` | `617364264c587d20c9fc4746c29679f2` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:248, fuse/machines/specplus3e.c:69` |
| `bios/fuse/plus3e-2.rom` | no | `libretro/fuse` | `c363e95dcd0a90e6e7f847e6e47e0179` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:249, fuse/machines/specplus3e.c:72` |
| `bios/fuse/plus3e-3.rom` | no | `libretro/fuse` | `a148bcc575e51389e84fdf5d555c3196` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:250, fuse/machines/specplus3e.c:75` |
| `bios/fuse/plusd.rom` | no | `libretro/fuse` | `42e5de16fb5e50082bb954ec7ce45851` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:251, fuse/peripherals/disk/plusd.c:239` |
| `bios/fuse/se-0.rom` | no | `libretro/fuse` | `fb3f86eb1e5a695d9c50c124e7cfb875` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:257, fuse/machines/spec_se.c:89` |
| `bios/fuse/se-1.rom` | no | `libretro/fuse` | `88de4a3129086f34bb9ca559acc51e6c` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:258, fuse/machines/spec_se.c:92` |
| `bios/fuse/speccyboot-1.4.rom` | no | `libretro/fuse` | `c54aa8f374b0971f51546c29d5d1eba1` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:259, fuse/peripherals/speccyboot.c:148` |
| `bios/fuse/tc2048.rom` | no | `libretro/fuse` | `9dd7ecf784a6c04265c073c236f5fadb` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:260, fuse/machines/tc2048.c:80` |
| `bios/fuse/tc2068-0.rom` | no | `libretro/fuse` | `55d462fccc6c536037404ef4ced08bec` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:261, fuse/settings.c:263, fuse/machines/tc2068.c:138` |
| `bios/fuse/tc2068-1.rom` | no | `libretro/fuse` | `575d203c6e15e679fba0b73f854ec7a2` no region; retrobios `fuse` | `fuse`: `fuse/settings.c:262, fuse/settings.c:264, fuse/machines/tc2068.c:141` |
| `bios/fuse/trdos.rom` | yes | `libretro/fuse` | `0c42e3b9ab8dd91ea096f1d0c07c55e5` no region; retrobios `fuse`<br>`0da70a5d2a0e733398e005b96b7e4ba6` no region; RomM's known_bios_files | `fuse`: `fuse/settings.c:228, fuse/settings.c:232, fuse/settings.c:236, fuse/machines/pentagon.c:137` |
| `bios/spec128.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `zx128_0.rom` crc32 `e76799d2` 16384 B, `zx128_1.rom` crc32 `b96a36be` 16384 B | `fbneo`: `src/burn/drv/spectrum/d_spectrum.cpp:188`; `src/burn/drv/spectrum/d_spectrum.cpp:189` |
| `bios/spec1282a.zip` | no | `libretro/fbneo` | zip, for `libretro/fbneo`: `zx128_2a_0.rom` crc32 `30c9f490` 16384 B, `zx128_2a_1.rom` crc32 `a7916b3f` 16384 B, `zx128_2a_2.rom` crc32 `c9a0b748` 16384 B, `zx128_2a_3.rom` crc32 `b88fd6e3` 16384 B | `fbneo`: `src/burn/drv/spectrum/d_spectrum.cpp:196`; `src/burn/drv/spectrum/d_spectrum.cpp:197`; `src/burn/drv/spectrum/d_spectrum.cpp:198`; `src/burn/drv/spectrum/d_spectrum.cpp:199` |
| `bios/spectrum.zip` | yes | `libretro/fbneo` | zip, for `libretro/fbneo`: `spectrum.rom` crc32 `ddee531f` 16384 B | `fbneo`: `src/burn/drv/spectrum/d_spectrum.cpp:181` |
| `bios/ZX_pentagon.rom` | yes | `bizhawk/ZXHawk` | `178d780c1347cf6634471805bd10e52c` no region; BizHawk's FirmwareDatabase.cs |  |
| `bios/ZX_trdos.rom` | yes | `bizhawk/ZXHawk` | `53e2f417c6996df9af170e147df8e369` no region; BizHawk's FirmwareDatabase.cs |  |

Read, never fetched:

| File | Readers | Why |
| --- | --- | --- |
| `bios/fuse/didaktik80.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/opus.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/ttx2000s.rom` | `libretro/fuse` | no identity: no source states a hash for it |
| `bios/fuse/usource.rom` | `libretro/fuse` | no identity: no source states a hash for it |
