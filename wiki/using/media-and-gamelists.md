# Artwork and game lists

Every game RomMBat syncs arrives with its name, description and other details from RomM, and
with artwork, so EmulationStation shows it the way it shows a game you scraped yourself.

## What artwork comes down

RomMBat fetches a cover, a thumbnail, a marquee, a video and a manual for each game, when RomM has
them. Video and manuals follow the switches in RetroBat's own scraper settings, so turn those
off in EmulationStation if you do not want them. Both are on in a new RetroBat.

Turning one of those switches off also takes back what was already fetched for it, the next time
that platform syncs. Artwork counts against your [disk limit](disk-budget.md): videos and manuals are
most of it.

## EmulationStation's game lists

EmulationStation reads each system's games from a `gamelist.xml` file in its folder, such as
`roms\snes\gamelist.xml`. RomMBat adds its games to that file and leaves everything else in it
alone, so entries you scraped yourself, your favourites and your play counts stay.

After a sync, RomMBat asks EmulationStation to reload its lists, so new games appear without a
restart. EmulationStation holds that reload until you leave RomMBat, and then the games are
there. If EmulationStation was not running at all, they appear the next time you open it.

## From a terminal

`rommbat-agent gamelist` rewrites the game lists from what RomMBat already knows, with no server
needed. `--media` chooses the kinds of artwork yourself, and RomMBat remembers the choice: every
later sync uses it instead of RetroBat's video and manual switches. See
[Command line](../reference/cli.md).
