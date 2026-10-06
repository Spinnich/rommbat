# RomMBat.UI

`RomMBat.exe`: the full-screen, gamepad-navigable front end, launched from the EmulationStation
menu. Avalonia over Skia. It holds views and view models; set resolution, mapping, conflicts
and the outbox are Core's.

| Where      | What                                                                                             |
| ---------- | ------------------------------------------------------------------------------------------------ |
| `Screens/` | Screens and view models. No Avalonia types, so tests walk them by pad                            |
| `Shell/`   | `ShellWindow`, the one place that knows both the framework and the pad                           |
| `Input/`   | Navigation actions and key repeat. The pad map itself is Core's `EsInputMap` and `GamepadReader` |

## Traps

- **Never reference `EsSettingsFile` or `TreeLock`.** The UI always runs under a live ES, which
  discards a write to `es_settings.cfg`; queue it in `pending_config` for `background quit`.
  Taking the lock even briefly makes a concurrent flush skip its work. Both are asserted
  against the built assembly.
- **If it cannot be tested without a window, it is in the wrong project.**
- **Input is read, never detected.** No vendor-id table; the map comes from `es_input.cfg`
  (`retrobat-layout`, "Controller input").
- **No primary flow needs a mouse.**
- A physical keyboard drives it at a desk, and is not a supported user flow: arrows move, Enter
  is A, Escape is B, Backspace or PageUp is L1, PageDown is R1, Tab is the left face button, Q
  the top one, F5 is Start.
- **Start is the menu, never a verb.** A screen lists its verbs as `ScreenAction`s and the
  navigator owns Start and the shortcuts; drive one in a test through `Navigator.Press` or
  `Navigator.Run`, because the screen's own `Handle` no longer sees them. Accept confirms or
  moves on, and Back never commits: an editor saves from its last row, and anything Back would
  lose is asked through `ConfirmScreen`. A connected controller is read
  through the same `es_input.cfg` a real install uses.
- **Reference `Avalonia.Win32`, `Avalonia.Skia` and `Avalonia.HarfBuzz`, never
  `Avalonia.Desktop`.** The last pulls in a package that fails `-warnaserror`. Without
  `UseHarfBuzz` the app builds clean and throws at startup; `TextShapingTests` guards it.
- **A finished screen says so**: past-tense title, an outcome word, and a footer reading Done on
  Accept instead of offering a stop. `ListScreen` moves Done to Accept whenever its back label
  reads `ListScreen.DoneLabel`.
- The publish is five files and must stay out of self-extraction, which unpacks natives outside
  the tree (`docs/architecture/projects.md`, "src/RomMBat.UI").
