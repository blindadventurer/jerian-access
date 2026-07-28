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

## 2026-07-28 - A screen that claims no keys hands the keyboard to the GAME

`ScreenManager.ProvideCategories` walks the stack focus-first and unions each screen's input
categories. A delivery-only screen (interlude popup, tutorial, chapter book) declares none
because the GAME pages it - but the walk continued into the scene underneath, which claims UI,
and `GameInputPatches` suppresses the game's own pagers for exactly as long as the mod claims
UI. The result was a popup no key could turn. The walk now stops at a screen that declares no
categories, the same way it stops at an Exclusive one. Rejected: marking each such screen
Exclusive by hand (the invariant then lives in five files and the sixth one forgets it), and
giving those screens their own paging nodes (DECISIONS 2026-07-28 already rejected that - it
re-implements page state the game owns).

## 2026-07-28 - The scene title is content, not a focus announcement

A scene's title was the screen-name announcement, so every overlay the game raises over a live
scene (the year flash, a window, a popup) spoke it again on the way out - the player heard the
title, a two-second flash, then the title again. The title is now delivered once per scene,
keyed to the scene like a page is keyed to its index. What a player needs on the way back from
an overlay is the row they are on, which the navigator's own re-seat already speaks.

## 2026-07-28 - A figure the game leaves to a tooltip is named from its data container

Jerian's windows draw a lot of their content as numbers with the WORD living in the hover
tooltip: an estate's power, the summary counters, a province on the map. Read literally those
are stops that say "22" and nothing else. The mod names them from the same data container the
game's own tooltip controller reads (NumericalParameterContainer / TextParameterContainer /
GenericTranslationKeyContainer / MapElementView) and keeps the RENDERED figure - a container's
own CurrentValue can be a different reading of the same item (an estate answers with its
loyalty, not the power drawn beside it). Rejected: reading the tooltip on focus (it is
asynchronous - the mod's own rule is to read detail from the model, not from a tooltip that
lags focus), and grouping every text under an item's root into one stop (the shields row would
merge three separate counters into "0 15 0").

## 2026-07-28 - The port versions itself from 0.9.0, and ships steam_appid.txt

The plugin carried 1.2.0, the version of the Brante mod this port was copied from - a number
that claims a maturity this port has not earned and would confuse a bug report. The first
tester build is 0.9.0.

The archive also drops `steam_appid.txt` (2936290) beside the exe. Without it the game's own
SteamAPI restart check quits after a full boot and respawns the process WITHOUT the mod loader
on any copy Steam is not launching - to a tester the mod would simply look absent. Launched
through Steam the check passes either way, so the file costs nothing on a legitimate install.

## 2026-07-23 - Non-Steam install, direct exe launch

The game copy on this machine lives at `F:\games\The Life and Suffering of Prince Jerian`
(no Steam appmanifest; a `.rne` file sits beside the exe). All launch tooling uses the exe
directly; the Steam/GOG locator paths remain in JerianGameLocator.ps1 for other machines, with
the F:\games path added as a known-local candidate checked first.

## 2026-07-29 - The consequence page is composed from the reward model, not read off its rows

The page a scene turns into after a choice (`SceneConsequenceController`) shows one reward group
at a time as animated sliders, counters and crests. Its rendered text does NOT carry the change:
a skill row renders "Закон – 3" (name and the NEW value) and the delta lives only in the slider
movement, a parameter row hides its name and delta labels entirely behind `ParameterSliderView`,
an estate row is a crest with a number. So the page is composed from
`SceneConsequenceController._rewardGroups` at its own page index, through the same
`ChoiceRewards.Describe` the pre-choice sheet and the interlude reward pages use. The previous
implementation swept two of the NINE row component types the game builds and was silent for
every group a normal choice grants.

Two sub-calls that follow from that:

* The AUTHORED reward is announced, not the clamped one the rows animate from
  (`_invertedRewardGroups`). The inverted copies read live parameter state at page-setup time and
  invert character statuses and article values against whatever is current, so re-reading them at
  speech time can compose a change that never happened. The authored value is also exactly what
  the choice's own sheet promises, so a pick reads the same before and after.
* No "стало N" resulting value. Whether a group's rewards are applied before or after the page
  that shows them differs by path (`NextPage` receives the inverted group, `AdjustRewards` the
  real one), so a resulting value read at speech time would be wrong on one of them - and a wrong
  number is worse to a player who cannot see the slider than no number. The delta is unambiguous;
  the current value is a Tab away on the HUD.

Delivery is a single static in `Game/RewardPage.cs` rather than per-screen state: a story scene
and an option scene (case-for-a-year, trip selection) raise the SAME page, and both call it, so
sharing the "already spoken" key is what keeps it from being announced twice.
