# Contributing

Bug reports from players are the most valuable thing this project can receive - see
[Reporting a problem](#reporting-a-problem) below. Code contributions are welcome too; this
page describes how the repository is put together and what "done" means here.

## Reporting a problem

Open an [issue](https://github.com/blindadventurer/jerian-access/issues) with:

- which screen or scene you were on,
- what you pressed,
- what you heard, or where it went silent,
- `BepInEx\LogOutput.log` from the game folder, attached whole.

Quit the game before you collect the log - it is written up to the moment you leave, and it is
overwritten the next time the game starts. The mod's own lines in it begin with "Jerian Access".
If the game crashed or froze, attach
`%USERPROFILE%\AppData\LocalLow\Schisma\The Life and Suffering of Prince Jerian\Player.log` too.

Silence counts as a bug. A screen that says nothing is indistinguishable, to a blind player,
from a screen that has stopped responding.

## What you need to build it

- A Windows install of the game. The build reads reference assemblies out of
  `<Game>\The Life and Suffering of Prince Jerian_Data\Managed`, so there is no way to compile
  without one - which is also why this repository has no build CI.
- The .NET SDK, 6 or newer. Note that SDK 6 and 7 cannot open the `.slnx` solution file; build
  the projects directly (`dotnet build src/JerianAccess.Module/JerianAccess.Module.csproj`) or
  use `build.ps1`, which does that for you.
- BepInEx and Prism are vendored under `third_party/`, so no build ever needs the network.

`setup-bepinex.ps1` installs the vendored BepInEx into the game folder once. The game is found
automatically (Steam libraries, GOG entries, and a `games` folder on any fixed drive); set the
`JERIAN_GAME` environment variable if your copy lives somewhere else.

## Layout

Three projects, a split ported from the mod's predecessors so that feature work does not need a
game restart:

- `src/JerianAccess.Core` - contracts only (`IModModule`, `IModHost`, `IDevDriver`, `TextUtil`).
  Pure BCL: no Unity or game references. Loaded once, giving the module a stable type identity
  across reloads.
- `src/JerianAccess` - the permanent host plugin: the BepInEx entry point, the speech backends
  (Prism, SAPI, clipboard), the module loader, the frame pump, and the Debug-only dev server.
  Changing this or Core requires a game restart; their DLLs are locked while the game runs.
- `src/JerianAccess.Module` - every feature: screens, the graph navigator, the input manager,
  the game adapters, localization. Loaded from bytes, so the file is never locked and the whole
  module can be swapped into a running game.

Around them: `lang/` (mod-authored strings in the 14 languages the game ships), `scripts/`
(packaging, the player-facing docs that go into the zip, and the dev driver shell helpers),
`third_party/` (vendored dependencies with their licenses).

## The development loop

With the game running, build the module and reload it:

```bash
dotnet build src/JerianAccess.Module/JerianAccess.Module.csproj
curl -X POST http://127.0.0.1:8772/reload
```

New behavior is live in about two seconds; F6 in-game does the same thing. A reload that fails
to load keeps the previous module running and says so, out loud and in the log.

Debug builds run a loopback HTTP server on port 8772 (`JERIAN_DEV_PORT` overrides it,
`JERIAN_NO_DEV=1` disables it). It exposes `/eval` (a C# REPL inside the game), `/input`
(navigation verbs through the mod's own navigator), `/wait`, `/speech`, `/log`, `/gui`,
`/focus`, `/typeinfo`, `/screenshot` and `/health`. Release builds compile it out entirely.

When driving the game while a screen reader is running, set `JERIAN_NO_SPEECH=1` or POST `on`
to `/mute` first, so the mod's dev speech does not land in the user's screen reader. Captured
speech still works, so verification is unaffected.

## What "done" means

A feature is finished when it has been exercised against the live game and the speech it
produced was read back - not when it compiles and looks right. Read the decompiled game code to
discover structure; use the dev server to confirm runtime values. `ROADMAP.md` records one line
of evidence per verified item, and that convention is worth keeping: it is what makes it
possible to tell, months later, which screens were actually heard and which were only written.

Other conventions worth knowing before you change anything:

- Prefer subscribing to the game's own event bus and reading live UI state over Harmony patches.
  Patch only where no event or state exists.
- Activate through the game's own handlers, so its logic, sounds and analytics run exactly as
  they do for a sighted player.
- Every announced string is localized. Mod-authored strings live in `lang/`; anything the game
  already words is taken from the game.
- Judgment calls go into `DECISIONS.md`, with the alternative that was rejected and why.
- `CLAUDE.md` is the working brief for the AI sessions that write this mod. If you change how
  something works, change that file with it.

## Pull requests

Keep a pull request to one subject, and say in it which screens you exercised and what you
heard. If you could not test something - no screen reader, no save far enough in - say so
plainly; that is far more useful than an untested claim.
