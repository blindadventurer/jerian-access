Jerian Access - screen reader support for blind players
=======================================================

An accessibility mod for "The Life and Suffering of Prince Jerian". The whole
game is playable with a screen reader and the keyboard alone: story text,
choices (including locked ones, with the reason they are locked), parameters,
menus, saves, popups and endings. The mouse is not needed anywhere.

Project page: https://github.com/blindadventurer/jerian-access
A Russian version of this file is next to it, as JerianAccess-README-ru.txt.


What you need
-------------

- The Windows version of "The Life and Suffering of Prince Jerian".
- A screen reader. NVDA and JAWS are spoken through directly, using the
  bundled Prism library; with neither one running, speech falls back to the
  standard SAPI voice.


Installing
----------

1. Open the game folder - the one holding
   "The Life and Suffering of Prince Jerian.exe".
   On Steam: right-click the game in your library, "Manage", "Browse local
   files".
2. Extract the whole archive into it, keeping the folder structure. When you
   are done, winhttp.dll, prism.dll, steam_appid.txt and a BepInEx folder sit
   next to the game's exe.
3. Start the game as usual. The first launch with the mod takes a few seconds
   longer - the loader writes its config. The mod speaks a short greeting line
   once it is ready.

The mod speaks the language the game is set to: a Russian interface gets
Russian speech.


Keys
----

The complete key reference is in JerianAccess-KEYS.txt, next to this file.
The short version:

- Up and Down arrows: previous and next item.
- Enter: activate (a choice, a button, the next page).
- Escape: back, close a window; in a scene it opens the pause menu.
- Tab and Shift plus Tab: next and previous group of items - for example from
  the scene text to the HUD bar and back.
- Space or F1: details about the focused item - a choice's conditions and its
  consequences, a parameter's scale, a character or objective description.
- Backspace: the secondary action. On a choice the game offers to unlock for
  Sanity or Tempest Legacy, it pays the price (press it again to take the
  payment back). On a save slot, it deletes.

Some pages the game turns itself: interludes and newspapers between events,
tutorial cards, the chapter book, the outcome page of a project for the year.
There the game's own keys work: Right arrow, D or Space go forward; Left arrow
or A goes back; Enter is "Continue"; hold Shift to skip.


If something goes wrong
-----------------------

The mod writes its log straight into the game folder:

    <game folder>\BepInEx\LogOutput.log

For example:
    C:\Program Files (x86)\Steam\steamapps\common\The Life and Suffering of
    Prince Jerian\BepInEx\LogOutput.log

What to do:

1. Note WHERE it happened: which scene or window, what you pressed, what you
   heard (or did not hear).
2. Quit the game - the file is written up to the moment you leave.
3. Open an issue at https://github.com/blindadventurer/jerian-access/issues with
   the whole LogOutput.log attached and a couple of sentences about what
   happened.

The log is overwritten every time the game starts, so save a copy before you
launch it again. The mod's own lines in it start with "Jerian Access".

There is a second, game-side log at
    %USERPROFILE%\AppData\LocalLow\Schisma\The Life and Suffering of Prince Jerian\Player.log
Attach that one too if the game crashed or froze.

Your saves live in the Saves folder next to it, and the mod never touches them.


Turning it off
--------------

Delete winhttp.dll from the game folder and the mod stops loading. To remove
it completely, delete prism.dll, doorstop_config.ini, .doorstop_version and
the BepInEx folder as well. Your saves are left alone.


Licenses
--------

The mod is distributed under the MIT license. It bundles BepInEx (LGPL-2.1)
and Prism (MPL-2.0); the license texts are in
BepInEx\plugins\JerianAccess\licenses.

Jerian Access is a port of Rashad Naqeeb's brante-access, the screen reader
mod for the first game in the series, "The Life and Suffering of Sir Brante":
https://github.com/rashadnaqeeb/brante-access
