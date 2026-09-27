---
summary: Eviction previews by default, which of the three policies it can honour, and the order it removes in.
read-when: Changing EvictionPlanner, its policies or its ordering.
---

# Eviction order and policies

- Eviction is a first-class operation and a dry run by default: it shows what would be
  removed before anything is, and refuses to evict anything with unflushed local saves.
  **From M4 it takes a ROM's media and its gamelist entry with it**, and still never touches
  a file RomMBat did not download, which is what keeps a user's own scraped art safe.
  **Two of the plan's three eviction policies cannot be honoured yet, and the code says so
  rather than ignoring them.** "Keep favourites" needs a fact RomM does not carry on a ROM
  (favourites are collection membership) and "keep the last N played" needs the play sessions
  M6 owns. What M3 can order by is real: departures first, then games no set claims, then the
  lowest-ranked members of a set, with the ROM id breaking every tie so a dry run and the run
  that follows it agree.
