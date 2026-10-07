# Find a game

Find a game lets you look up one game and put it on this device, or take it off, without making
a sync set for it. It also shows what is already here.

## Look something up

1. Choose Find a game on the main menu.
2. Pick one platform, or Every platform.
3. Press the left face button to search, type part of the game's name, and press Start. To clear
   a search, search again with nothing typed.
4. Accept opens a game.

## Move through a long list

A large library is not something to scroll through, so the game list moves the way
EmulationStation's does:

- **L1 and R1** move one screen up or down.
- **L2 and R2** switch to the previous or next platform without leaving the list. Your search,
  sort and filters stay as they are.
- **Select** opens the view options:
  - **Search for** types a search, as the left face button does.
  - **Jump to letter**: press Left and Right to choose a letter, then Accept to go to the first
    game under it. Numbers and symbols are under #. It works only when the list is sorted by name.
  - **Sort by**: by name either way, by release date, or by rating.
  - **Filters**: RomM's own filters, the same ones a saved-search sync set offers, such as genre
    or region. Choose Show these games at the bottom to apply them; Back leaves them as they were.

Sort and filters need RomM. When it cannot be reached, the games on this device are listed by
name, and you can still search them and jump to a letter.

When RomM can be reached, you are looking through its whole library. When it cannot, RomMBat says
so and shows the games already on this device instead, so you can still find those anywhere.

## Put one game on this device

On a game's screen, press Accept for Put this game on the device. RomMBat downloads it with its
artwork and the BIOS files RetroBat lists for its system, and it appears in EmulationStation.

The game joins a set of its own, named Picked on followed by this device's name. That set
behaves like any other: it is in your list of sync sets, and deleting it offers to take its
games off.

A game that cannot come down says why when you press Accept, for example when its platform has
no RetroBat folder or it is too large for this drive. On a new install every game is refused
this way until the platform list has been filled once; see
[Your first sync](../getting-started/first-sync.md#before-you-start).

## Take one game off

On a game that is on this device, press Start for the menu, and choose Take it off this device.
RomMBat shows what would go first. It removes the game, its artwork and its entry in
EmulationStation's list.

Taking a game off never removes its saves or save states. It is refused while another sync set
still wants the game, and the game's screen says which sets those are under Wanted by.

## What the game's screen shows

- Its platform and size in RomM.
- Which folder it is in, and how much room it and its artwork take up.
- Which sync sets want it. A game nothing wants may be removed the next time you make room.
- For a PlayStation 2 game on a shared memory card, the Start menu offers to give it its own
  card; see [Memory cards](../saves/memory-cards.md).

## From a terminal

`rommbat-agent game` does the same three things by a game's RomM id, which is the number in the
first column of `rommbat-agent browse`:

```text
rommbat-agent game show 1234            what the game's screen shows; add --offline to skip RomM
rommbat-agent game install 1234         put it on this device, into the same Picked on set
rommbat-agent game remove 1234          show what would go
rommbat-agent game remove 1234 --apply  take it off
```

The per-game memory card is `rommbat-agent saves convert 1234`. Every option is in the
[command line](../reference/cli.md) reference.
