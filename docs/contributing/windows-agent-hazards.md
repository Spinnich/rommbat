---
summary: Three ways the Windows shell tooling corrupts an edit in transit, and how to edit around each.
read-when: Before a scripted edit from a Windows shell, or before editing a PR body or comment through gh.
---

# Windows agent hazards

Each of these corrupts content silently. The command reports success, and the damage shows up
later as a build error, a mangled line, or a file that breaks only on Linux. They apply to an
agent (or a person) editing this repo from Windows through Git Bash, PowerShell or `gh`. A cloud
session on Linux is unaffected.

## Backslashes do not survive a Bash tool argument

Claude Code's Bash tool on Windows processes backslash escapes before the command runs, even
inside a quoted heredoc. `\\` collapses to `\`, and `\r`, `\t` and `\n` become real control
characters before Python parses the script. Observed forms:

- a `<<'PY'` heredoc turned `cores\\nestopia` into `cores`, a newline, and `estopia`;
- `grep '\\' file` died with "Trailing backslash";
- `sed -i 's|R:\\RetroBat|...|g'` reported success and changed nothing.

A script written to a file with the Write tool avoids the shell, but not Python itself: a
non-raw `"""..."""` block holding C#'s `string.Join('\n', discs)` became a real newline and the
C# failed with CS1010.

- Use the Edit tool for a change containing a path or an escape, and the Grep tool for a
  pattern containing a backslash.
- For a scripted edit, write the script to a file and use raw strings (`r"""..."""`) for any
  block of C#, or build the backslash from `chr(92)`.
- Best of all, avoid the backslash: name `nestopia_libretro.dll` rather than its full path.
- Check afterwards with `sed -n` plus `cat -A`, because the Read tool shows a stray CR as nothing.

## Python text mode writes CRLF

`pathlib.write_text`, and any text-mode `open`, translates `\n` to `\r\n` on Windows. Read and
write bytes (`read_bytes`, `write_bytes`) when patching repo files.

Most paths are protected: `.gitattributes` sets `* text=auto eol=lf`, so a CRLF working copy of
a `.cs` or `.md` file normalizes in the index. `reference/**` and `tests/**/fixtures/**` are
`-text`, stored byte for byte, so a CRLF there reaches a Linux checkout intact. A CRLF
`reference/refresh.sh` fails with `env: 'bash\r': No such file or directory`, or as
`bash refresh.sh` with `set: pipefail: invalid option name`. `trunk check` passes with it in
place.

After a scripted edit of a `-text` path, count carriage returns in the committed blob:
`git cat-file blob HEAD:<path> | tr -cd '\r' | wc -c` must print 0.

## gh output can hide mojibake

Non-ASCII in a PR body or comment can be stored on GitHub as the UTF-8 of mojibake while the
terminal decodes it back into the right glyph, so `print()` and `repr()` both look correct. A
robot emoji stored as `C3 B0 C5 B8 C2 A4 E2 80 93` displays the same as the real `F0 9F A4 96`.
Any edit that retypes those characters can make it worse, and looking cannot tell you.

- Fetch with `subprocess.run([...], capture_output=True)` and decode the bytes yourself, rather
  than through a shell redirect.
- Anchor the edit on an ASCII substring (`Generated with [Claude Code]`, not the emoji before it).
- Before posting, assert `bytes(c for c in before if c > 127)` equals the same for the new body.
  After posting, fetch it again and compare it with what was sent.
