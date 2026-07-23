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

## 2026-07-23 - Non-Steam install, direct exe launch

The game copy on this machine lives at `F:\games\The Life and Suffering of Prince Jerian`
(no Steam appmanifest; a `.rne` file sits beside the exe). All launch tooling uses the exe
directly; the Steam/GOG locator paths remain in JerianGameLocator.ps1 for other machines, with
the F:\games path added as a known-local candidate checked first.
