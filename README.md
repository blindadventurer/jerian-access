# Jerian Access

A screen reader accessibility mod for **The Life and Suffering of Prince Jerian**. The goal is that the entire game is playable by a blind player with speech and the keyboard alone - no mouse, no sighted assistance.

Current version: **0.9.3**. A blind tester has played the game through with it; the seven items that report produced are fixed. See [CHANGELOG.md](CHANGELOG.md) for what changed per release and [ROADMAP.md](ROADMAP.md) for the per-feature status.

## Where this came from

This mod is a port of [brante-access](https://github.com/rashadnaqeeb/brante-access) by Rashad Naqeeb - the screen reader mod for the first game in the series, *The Life and Suffering of Sir Brante*. That mod is the reason this one exists: it showed that a story game of this kind can be made fully playable without sight, and its infrastructure (speech backends, input manager, screen stack, graph navigator, localization layer) carried over to the sequel almost unchanged. Everything game-facing had to be rewritten against Prince Jerian's own code.

Like the original, the port was written by Claude (Anthropic's AI, through Claude Code): the reverse engineering, the implementation, and the verification - driving the live game over a local dev server and reading back its own speech output to confirm each feature worked, rather than assuming it did.

## What it does

- Speaks every screen: story passages, choices (including unavailable ones, with the reason they are locked), parameters, character and family windows, chapter transitions, the chapter book, letters and newspapers, popups, tooltips, project pages, endings and credits.
- Full keyboard navigation everywhere the game only supported the mouse. See [KEYS.md](KEYS.md) for the complete key reference.
- Announces what a choice will cost and what it will change, and where a changed figure now stands ("Reason +2, now 20 of 20").
- Speech through Prism (NVDA and JAWS), with SAPI and clipboard fallbacks.
- Uses the game's own localized text and its own click handlers, so game logic, sounds, and localization stay intact. The mod speaks whatever language the game is set to.

## Installing

Download the latest `JerianAccess-<version>.zip` from the [Releases page](https://github.com/blindadventurer/jerian-access/releases) and extract it into the game folder - the one holding `The Life and Suffering of Prince Jerian.exe`. That is the whole install: the zip bundles BepInEx, the plugin, and the speech library.

Full steps, troubleshooting and the key reference ship inside the zip as `JerianAccess-README.txt` and `JerianAccess-KEYS.txt` (Russian versions beside them, with the `-ru` suffix).

To uninstall, delete `winhttp.dll` from the game folder. Saves are never touched.

## Reporting a problem

Open an [issue](https://github.com/blindadventurer/jerian-access/issues) and attach `BepInEx\LogOutput.log` from the game folder, written up to the moment you quit the game. Say which screen you were on, what you pressed, and what you heard or did not hear. Silence is a bug: a screen that says nothing is a screen a blind player cannot use.

## How it is built

- The game runs on Unity 2020.3.43f1 with the Mono backend, which makes it patchable with [BepInEx 5](https://github.com/BepInEx/BepInEx) and Harmony.
- The mod is a BepInEx plugin split into a permanent host (speech backends, hot-reload loader, debug dev server) and a reloadable module holding all feature code, so new behavior can be loaded into the running game in about two seconds.
- Features prefer subscribing to the game's own event bus and reading live UI state over patching; announcements are keyed off the same signals the game's own views use.
- In debug builds a loopback HTTP server exposes eval, input, speech and log endpoints. Every feature was verified by driving the real game through that server and checking the captured speech, not by inspection.

## Building

Requires a Windows install of the game and the .NET SDK (6 or newer).

1. Clone the repo.
2. Run `setup-bepinex.ps1` once. It finds the game install (Steam and GOG are auto-detected, and a `games` folder on any fixed drive is checked; set the `JERIAN_GAME` environment variable if yours is somewhere unusual) and installs the vendored BepInEx into it.
3. Close the game if it is running, then run `build.ps1`. It builds the mod, deploys the plugin and the speech library into the game folder, and tells you if anything is missing.
4. Launch the game.

If Windows blocks the scripts, run them as `powershell -ExecutionPolicy Bypass -File .\setup-bepinex.ps1` (and the same for `build.ps1`).

To build the distributable zip instead, run `package.ps1`: it makes a Release build (dev server compiled out) and writes `artifacts\JerianAccess-<version>.zip`, laid out to extract straight into the game folder. It only reads the game install for reference assemblies and does not need BepInEx installed there.

[CONTRIBUTING.md](CONTRIBUTING.md) has the details: repository layout, the hot-reload loop, the dev server, and how a change is expected to be verified before it is called done.

## Documentation

- [KEYS.md](KEYS.md) - the complete key reference.
- [CHANGELOG.md](CHANGELOG.md) - what changed in each release.
- [ROADMAP.md](ROADMAP.md) - every feature, its status, and the evidence that it was verified against the live game.
- [DECISIONS.md](DECISIONS.md) - the judgment calls made during the port and why.
- [CLAUDE.md](CLAUDE.md) - the working brief for the AI sessions that write this mod: game facts, architecture, and the gotchas found the hard way.

## License

MIT - see [LICENSE](LICENSE). The port keeps the original brante-access copyright alongside its own, as MIT requires.

## Third-party components

Vendored under `third_party/` with their licenses: BepInEx (LGPL-2.1), [Prism](https://github.com/ethindp/prism) (MPL-2.0), and Mono.CSharp (MIT). Decompiled game code is used only as a local reference and is not part of this repository.
