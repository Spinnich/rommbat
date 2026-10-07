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
  is A, Escape is B, Backspace or PageUp is L1, PageDown is R1, Home is L2, End is R2, Tab is
  the left face button, Q the top one, F5 is Start and F6 is Select. A connected controller is read through the same `es_input.cfg` a
  real install uses.
- **Start is the menu, never a verb.** A screen lists its verbs as `ScreenAction`s and the
  navigator owns Start and the shortcuts; drive one in a test through `Navigator.Press` or
  `Navigator.Run`, because the screen's own `Handle` no longer sees them. Accept confirms or
  moves on, and Back never commits: an editor saves from its last row.
- **Every confirmation is a `ConfirmScreen`**, with the safe answer selected first and given by
  Back. A preview goes in its `Details` and `Load`; an answer that acts in place ends in
  `Answer(outcome)`. Never build a confirmation from a `ListScreen`.
- **Reference `Avalonia.Win32`, `Avalonia.Skia` and `Avalonia.HarfBuzz`, never
  `Avalonia.Desktop`.** The last pulls in a package that fails `-warnaserror`. Without
  `UseHarfBuzz` the app builds clean and throws at startup; `TextShapingTests` guards it.
- **A line on a progress screen is a slot, never a conditional child.** Sync and Resolve lay
  out `ProgressLayout` slots, and a slot with nothing to say is drawn blank at its height. A line
  added only when it has a value moves everything under it, because the body is centered.
- **A finished screen says so**: past-tense title, an outcome word, and a footer reading Done on
  Accept instead of offering a stop. `ListScreen` moves Done to Accept whenever its back label
  reads `ListScreen.DoneLabel`.
- **A row or field never shows a raw slot or scope kind.** Show `SaveSlotLabel.Describe` and
  `SyncSetStore.ScopeLabel`; the screens say Check for changes, What it holds, Waiting to upload,
  Pending settings and Browse library, and the glossary pairs each with its term. Core's
  sentences from a flush or a conflict resolution still name the raw slot (#502).
- **Every color is a `Shell/Theme` token** copied from the carbon theme (RB-425); a screen never
  names a brush. Inside a `Window`, `Theme` is the window's own property, so `ShellWindow` aliases
  the class as `EsTheme`.
- **Fonts and help icons are read from the install at startup, never shipped.** A font from a
  file is named `fonts:RomMBatInstall#<family>`; the constructor taking the key as a URI builds a
  family that silently resolves to Segoe UI. `EsIcon` refuses an SVG it cannot draw faithfully,
  and the hint falls back to a drawn glyph.
- The publish is five files and must stay out of self-extraction, which unpacks natives outside
  the tree (`docs/architecture/projects.md`, "src/RomMBat.UI").
