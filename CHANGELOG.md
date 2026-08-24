# Changelog

All notable changes to Jerian Access. Dates are the day the build was cut.

The mod is a port of [brante-access](https://github.com/rashadnaqeeb/brante-access) 1.2.0, so
it starts its own numbering at 0.9.0: the infrastructure arrived finished, the game-facing
half was written from scratch.

## 0.9.4 - 2026-08-24

- The plugin id is the port author's own: `com.blindadventurer.jerianaccess`, where it used to
  be the one inherited from brante-access. Nothing about the mod behaves differently, but BepInEx
  names its settings file after the id, so `BepInExnfigm.rashadnaqeeb.jerianaccess.cfg`
  is replaced by a fresh `com.blindadventurer.jerianaccess.cfg` with the default values. If you
  had changed the speech backend or the SAPI rate by hand, set them again; the verbosity toggle
  (Ctrl plus V) is kept elsewhere and survives. The old file can be deleted.

## 0.9.3 - 2026-08-24

First public release. No change to what the mod does - the code is the 0.9.2 build - but the
documentation that ships inside the zip is now English, with the Russian version beside it:
`JerianAccess-README.txt` and `JerianAccess-KEYS.txt`, plus `JerianAccess-README-ru.txt` and
`JerianAccess-KEYS-ru.txt`.

## 0.9.2 - 2026-08-16

The seven items a blind tester filed after playing the game through. All seven fixed, six of
them exercised live against a chapter-3 replay (the seventh fires once per save and could not
be reproduced on a played save).

- A choice that was only saying "unavailable" now names the requirement. `OR` condition lists
  and scene-group references are expanded, conditions are evaluated against the scene's own
  container, and a choice with no words left names the resource it falls short of. This also
  silenced three "GroupConditionsContainer is not found!!" errors per choice read in the game's
  own log.
- The unlock price of a choice no longer depends on when the game armed its lock view - it
  falls back to the condition groups the views are built from, so a tutorial choice states its
  price too.
- An interlude's conditions page reads why the event fired, with met markers, from the model.
- Letters, newspaper cuttings and pamphlets say where they stand ("page 3 of 14") and mark the
  last page.
- Paging back out of a document's reward pages no longer leaves the rest of the document
  silent.
- Projects for the year are grouped into imperial and personal, say why a closed one is closed,
  and read their description and what they grant on Space.
- The chapter book is navigated as a list instead of being recited a page at a time: each page's
  rows behind a region, an objective its own stop with its requirements on Space, and Continue
  as the last item.
- A change now says where the figure ends up ("Dogma +1, now 3", "Sciences +1, now 8 of 20"),
  on the consequence page and on the interlude reward pages - which also makes a reward the
  game clamped read as what it is.

## 0.9.1 - 2026-07-29

- The consequence page says WHICH parameter changed, instead of reading the change as a run of
  loose figures.

## 0.9.0 - 2026-07-28

First build handed to a tester. The whole game covered end to end:

- 24 screens: scenes and choices, the HUD and its seven windows, the chapter book, chapter
  starts and finishes, projects for the year, trip selection, interludes and documents,
  tutorials, popups and confirmations, saves and loads, settings, endings, game over, credits.
- Choices announce their conditions, what they will change, and the price of an unlock -
  and refuse with a reason instead of going quiet.
- One navigation set across every screen, plus type-ahead search, region jumps and a verbosity
  toggle. The game's own paging keys are left live on the surfaces the game turns itself.
- Speech through Prism (NVDA, JAWS) with SAPI and clipboard fallbacks; all mod-authored strings
  localized to the 14 languages the game ships.
- Packaged as a single zip that extracts into the game folder, BepInEx included.
