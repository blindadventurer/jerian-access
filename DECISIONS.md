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

## 2026-07-28 - Delivery-only screens for everything the game pages itself

Jerian pages several surfaces through `PageNavigationController`, which reads the game's own
arrow / continue keys every frame: the chapter-summary book, tutorial popups, interludes and
the creed-outcome popup. Those screens declare NO input category and only DELIVER (speak each
settled page); the player pages them with the same keys a sighted player uses. Alternative
considered and rejected: claiming input and driving the pager from the mod - it needs a
suppression patch per pager and re-implements page state the game already owns. The rule that
makes this safe: `GameInputPatches` skips a game Update only while the mod's UI category is
active, so a delivery-only screen leaves the game's keys live by construction.

## 2026-07-28 - Choice unlocking is a first-class action (Backspace), not a hidden Enter step

A Jerian choice whose requirements fail can still be BOUGHT open with Sanity / Tempest Legacy
by clicking its lock (`ConditionLockView`), and until that click the choice's own click does
nothing. Enter therefore never pays: it states the price. Backspace (the existing secondary
action) pays every lock on the choice and pays it back, answering with the state each lock
ends in. Rationale: paying is spending a scarce resource - it must be a deliberate keypress,
and it must be reversible the same way the mouse can un-click a lock.

## 2026-07-28 - The mod resolves a held choice directly when hold-to-choose is on

With the "hold the choice" setting on, the game leaves choice buttons' onClick UNWIRED and
resolves the pick from the mouse-hold sequence, so a synthesized click reaches nothing and the
choice cannot be taken by keyboard at all. Every choice surface (scene choices, trip options,
case-for-a-year options) ends its hold in a private `ResolveClickEvent`; the mod calls that
directly (`Game/HoldChoice.cs`) after checking the same guards the game checks. Rejected:
simulating a timed hold (frame-rate dependent, and the progress animation would fight it) and
forcing the setting off (the game's setting is the player's, not the mod's).

## 2026-07-28 - Tooltips are READ FROM THE GAME, not re-modelled

Jerian shows a control's detail by having `TooltipZoneView` broadcast on hover and a per-type
controller build a view prefab a frame later - ten types, each with its own layout and data
containers. The mod asks the zone to open (its own keyboard-selection entry), waits for the
view to settle, speaks the rendered view and closes it, rather than composing ten readouts from
the model. Rationale: the rendered view IS the game's localized output for that type, it stays
correct when the game changes one, and one code path covers surfaces the mod has not seen yet.
The cost is asynchrony - the read is ticked, and any keypress cancels a pending one, because a
tooltip that answers after focus moved would describe the control the player just left.

Where the mod DOES model instead: a choice's requirements and consequences, because those must
be spoken as part of the choice's own announcement (on focus, not on demand) and the game shows
them on a sheet that appears a frame late and vanishes on the next hover.

## 2026-07-28 - A screen stays entered while the game hides its surface

Jerian deactivates the whole scene prefab whenever a window or a between-events popup covers
it, and dips a window's CanvasGroup to zero while its page animates in. Screens keyed purely on
"is my surface visible right now" therefore popped and pushed again, announcing themselves
twice and losing focus to whatever node the graph happened to start with. Screens whose surface
the game hides this way (the scene, the opened window) now hold a live reference to what they
entered on and only leave when the game DESTROYS it - which Unity's own null reports.

## 2026-07-23 - Non-Steam install, direct exe launch

The game copy on this machine lives at `F:\games\The Life and Suffering of Prince Jerian`
(no Steam appmanifest; a `.rne` file sits beside the exe). All launch tooling uses the exe
directly; the Steam/GOG locator paths remain in JerianGameLocator.ps1 for other machines, with
the F:\games path added as a known-local candidate checked first.
