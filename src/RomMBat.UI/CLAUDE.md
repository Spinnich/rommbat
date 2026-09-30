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
  is A, Escape is B, Backspace is L1, Tab is X, F5 is Start. A connected controller is read
  through the same `es_input.cfg` a real install uses.
- **Reference `Avalonia.Win32`, `Avalonia.Skia` and `Avalonia.HarfBuzz`, never
  `Avalonia.Desktop`.** The last pulls in a package that fails `-warnaserror`. Without
  `UseHarfBuzz` the app builds clean and throws at startup; `TextShapingTests` guards it.
- **A finished screen says so**: past-tense title, an outcome word, and a footer reading Done
  instead of offering a stop.
- The publish is five files and must stay out of self-extraction, which unpacks natives outside
  the tree (`docs/architecture/projects.md`, "src/RomMBat.UI").
