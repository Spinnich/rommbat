# The gamepad interface

RomMBat opens full screen from EmulationStation's menu and is built for a controller. Everything
it does day to day works from the couch, and nothing needs a mouse. When you leave it, you are
back in EmulationStation.

## Buttons

RomMBat reads the controller layout you set up in EmulationStation, so the buttons do the same
jobs there as here. The footer at the bottom of each screen shows what each button does on that
screen. It draws buttons by their position on the controller rather than by a letter, because
the bottom face button is A on an Xbox pad, Cross on a PlayStation pad and B on a Switch Pro.

| Button              | What it does                                                                                        |
| ------------------- | --------------------------------------------------------------------------------------------------- |
| D-pad or left stick | Move. Left and Right also step through choices, such as a disk limit                                |
| Accept              | Open or choose what the cursor is on, answer yes, save, or move on. Your confirm button in RetroBat |
| Back                | Leave this screen or cancel, or on the main menu go back to EmulationStation                        |
| Start               | Open the screen's menu, which lists everything that screen can do                                   |
| Left face button    | A shortcut to the screen's most common action, such as Sync now, or Search                          |
| Top face button     | A shortcut to the next most common, such as Query                                                   |

They follow EmulationStation's own: Start opens a menu there too, and Accept picks from it.

**Accept never changes a value.** It opens a list to choose from. To step through choices in
place, use Left and Right.

**Back never saves or deletes anything, and never stops a sync without asking.** Leaving a
screen with unsaved changes asks whether to discard them, and stopping a sync asks first, because
the game it is downloading is removed. Stopping a query does not ask, because nothing is lost: the
next query carries on from where it stopped. In every such question
the answer that changes nothing is selected, so pressing Accept or Back straight away is safe.

When a screen has finished its work, its footer says Done, and Accept or Back leaves it.

## The main menu

Each row says what it is for, and the ones that need attention carry a count.

| Row            | What it is for                                                                                                       |
| -------------- | -------------------------------------------------------------------------------------------------------------------- |
| Sync sets      | What this device keeps. See [Sync sets](sync-sets.md)                                                                |
| Find a game    | Search your library, or look through what is on this device. See [Find a game](browse-and-install.md)                |
| Conflicts      | Saves that changed here and on another device. See [Conflicts](../saves/conflicts.md)                                |
| Platforms      | Where each RomM platform's games land in RetroBat. See [Sync sets](sync-sets.md#when-games-land-in-the-wrong-folder) |
| Queued changes | Settings waiting for you to quit EmulationStation. See [Memory cards](../saves/memory-cards.md)                      |
| Outbox         | Saves, states and play sessions not yet sent to RomM, and any the server refused. Drop them here                     |
| Disk space     | How much room RomMBat may use. See [Disk space](disk-budget.md)                                                      |
| Pair with RomM | Sign in to your server, or sign in again. See [Pairing](../getting-started/pairing.md)                               |
| This device    | Your RetroBat version, the server, what is waiting to be sent, and the controller                                    |

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
