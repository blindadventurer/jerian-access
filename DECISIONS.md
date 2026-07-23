# Jerian Access - Decisions

Judgment calls made during the port, newest last. Brante-era decisions live in the original
project (`..\brante-access-1.2.0\DECISIONS.md`) and remain binding unless overridden here.

## 2026-07-23 - Port scaffolded by copy + rename, not re-derived

The entire brante-access 1.2.0 tree was copied and mass-renamed (Brante→Jerian). Rationale:
the infrastructure (speech, input, graph navigator, screen stack, loc, dev server, hot reload)
is game-agnostic and battle-tested; only the game-facing adapter layer needs rework. Screens
code is knowingly carried over BROKEN against Jerian's assembly - it gets re-pointed type by
type in ROADMAP Phase 2/3/4 rather than deleted, so the Brante implementation stays visible as
the reference while porting.

## 2026-07-23 - Hostile-destroy resilience over root-cause hunt

The game destroys the BepInEx manager GameObject seconds into boot (no error, game keeps
running). Rather than hunting the destroyer through opaque Bolt graphs, the host was made
resilient: teardown on Application.quitting only, pump on a hidden GameObject, ReferenceEquals
plugin checks. This is strictly more robust than finding and patching the purge, and it
matches how the reference mods treat engine-side surprises: survive them, log them, move on.

## 2026-07-23 - Compile-driven exclusion list instead of blanket screen rewrite

The Module builds with a csproj `BrokenOnJerian` item list naming exactly the 37 files that
fail against Jerian's assemblies; everything else (including 4 screens: Cutscene, GameOver,
YearPopup, DayStagePopup) carried over compiling and stays registered. Ported surfaces remove
their entry from the list. Chosen over stubbing call sites inside files: the Brante code stays
pristine on disk as the porting reference, and the list doubles as the port's todo inventory.

## 2026-07-23 - Non-Steam install, direct exe launch

The game copy on this machine lives at `F:\games\The Life and Suffering of Prince Jerian`
(no Steam appmanifest; a `.rne` file sits beside the exe). All launch tooling uses the exe
directly; the Steam/GOG locator paths remain in JerianGameLocator.ps1 for other machines, with
the F:\games path added as a known-local candidate checked first.
