# The gamepad interface

RomMBat opens full screen from EmulationStation's menu and is built for a controller. Everything
it does day to day works from the couch, and nothing needs a mouse. When you leave it, you are
back in EmulationStation. It looks like EmulationStation's own menus, using the fonts and button
icons from your RetroBat install.

## Buttons

RomMBat reads the controller layout you set up in EmulationStation, so the buttons do the same
jobs there as here. The help bar at the bottom of each screen shows what each button does on that
screen. It draws buttons by their position on the controller rather than by a letter, because
the bottom face button is A on an Xbox pad, Cross on a PlayStation pad and B on a Switch Pro.

| Button              | What it does                                                                                        |
| ------------------- | --------------------------------------------------------------------------------------------------- |
| D-pad or left stick | Move. Left and Right also step through choices, such as a disk limit                                |
| Accept              | Open or choose what the cursor is on, answer yes, save, or move on. Your confirm button in RetroBat |
| Back                | Leave this screen or cancel, or on the main menu go back to EmulationStation                        |
| Start               | Open the screen's menu, which lists everything that screen can do                                   |
| Left face button    | A shortcut to the screen's most common action, such as Sync now, or Search                          |
| Top face button     | A shortcut to the next most common, such as Check for changes                                       |
| L1 and R1           | Move up or down one screen of a list at a time, stopping at the top and the bottom                  |
| L2 and R2           | In Browse library, the previous or next platform                                                    |
| Select              | In Browse library, the view options: search, jump to a letter, sort and filters                     |

They follow EmulationStation's own: Start opens a menu there too, and Accept picks from it.

**Accept opens a list to choose from rather than stepping through one.** To step through
choices in place, use Left and Right. A filter row with a few fixed answers, such as yes, no or
either, is the one exception: Accept moves it to the next answer.

**Back never saves, deletes or stops anything without asking.** Leaving a screen with
unsaved changes asks whether to discard them, and stopping a sync or a check for changes asks first.

**Every question looks the same:** a box over the screen it is about, with its answers in a row.
Left and Right choose an answer, Accept gives it, and Back always gives the answer that changes
nothing. That answer is selected when the box opens, so pressing Accept or Back straight away is
safe. When there is something to read first, such as which games a delete would take off, it is
in the box above the answers, and Up and Down scroll it. Once you answer, the box says what
happened, and Done closes it.

When a screen has finished its work, its footer says Done, and Accept or Back leaves it. While it is still taking a game off, removing a set, settling a conflict or forgetting files, Back says Stop and asks first, with Keep going selected.

## The main menu

Each row says what it is for, and the ones that need attention carry a count.

| Row               | What it is for                                                                                                       |
| ----------------- | -------------------------------------------------------------------------------------------------------------------- |
| Sync sets         | What this device keeps. See [Sync sets](sync-sets.md)                                                                |
| Browse library    | Search your library, or look through what is on this device. See [Browse library](browse-and-install.md)             |
| Conflicts         | Saves that changed here and on another device. See [Conflicts](../saves/conflicts.md)                                |
| Platforms         | Where each RomM platform's games land in RetroBat. See [Sync sets](sync-sets.md#when-games-land-in-the-wrong-folder) |
| Pending settings  | Settings waiting for you to quit EmulationStation. See [Memory cards](../saves/memory-cards.md)                      |
| Waiting to upload | Saves, states and play sessions not yet sent to RomM, and any the server refused. Drop them here                     |
| Disk space        | How much room RomMBat may use. See [Disk space](disk-budget.md)                                                      |
| Pair with RomM    | Sign in to your server, or sign in again. See [Pairing](../getting-started/pairing.md)                               |
| This device       | Your RetroBat version, the server, what is waiting to be sent, and the controller                                    |

Every row works without the server except the ones that have to ask RomM something. Those say
so when it cannot be reached.

## Typing

Two things ask you to type: your server's address when you pair, and a name or search term. For
those, RomMBat opens an on-screen keyboard in your EmulationStation language's layout. On it, L1
deletes a letter and R1 types a space. Everything else, such as a disk limit, is chosen with
the d-pad.

## When the controller is not seen

Open This device on the main menu. Its Controller section names the controller RomMBat found and
whether it is ready. If it says Not configured, set the controller up in EmulationStation's own
controller settings, then open RomMBat again.
