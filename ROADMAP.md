# Jerian Access - Work Queue

Port of brante-access 1.2.0 to The Life and Suffering of Prince Jerian. The single source of
truth for progress. Statuses: `todo` → `built` (code complete, compiles, deployed) → `verified`
(exercised against the LIVE game via the dev server, speech output checked; one-line evidence
note appended). Work top to bottom within a phase; phases mostly in order, but a blocked item
may be skipped with a note rather than stalling the run.

Rules of engagement: read CLAUDE.md first. After each verified item: update this file, commit.
After every ~4 verified items, run the standing regression sweep (Phase 8, R1). Judgment calls
go to DECISIONS.md, not to the user.

The brante-access source this port started from is at
`..\brante-access-1.2.0` - when a ported file misbehaves, diff against the original before
debugging from scratch.

## Phase 0 - Scaffold

- [x] built - Copied brante-access-1.2.0 → jerian-access; mass-renamed (Brante→Jerian,
      144 files); CLAUDE.md game facts rewritten for Jerian (Unity 2020.3.43f1, non-Steam
      install at F:\games, known type renames); csprojs point GameDir at F:\games and
      reference UnityEngine.InputLegacyModule (Unity 2020 home of legacy Input)
- [ ] todo - Git repo initialized, initial commit of the scaffold
- [ ] todo - Decompiled reference at jerian-decompiled/ (ilspycmd; record type counts here)

## Phase 1 - Bring-up (dev server before features)

- [ ] todo - Install vendored BepInEx (third_party/bepinex, 5.4.23.5 win x64 Mono) into
      F:\games\The Life and Suffering of Prince Jerian; game still boots clean with no plugin
      (LogOutput.log: "Chainloader startup complete")
- [ ] todo - Host + Core + Module compile against Jerian assemblies (expect Module compile
      errors where Brante game types are gone - stub those call sites with TODO(J) markers and
      keep the build green; they get re-pointed in Phase 2)
- [ ] todo - Deploy lands; game boots with the plugin; log lines prefixed "Jerian Access"
- [ ] todo - Dev server answers /health; /eval works (Mono.CSharp REPL on Unity 2020 Mono)
- [ ] todo - INPUT DECISION: verify legacy Input.GetKey works at runtime (game ships
      InputSystem alongside InputLegacyModule). If it throws, switch KeyboardBinding/
      GraphNavigator/HostPump polling to InputSystem Keyboard.current and record in DECISIONS
- [ ] todo - Speech pipeline up: Prism acquires a backend (or SAPI fallback), /speech captures
- [ ] todo - Hot reload proven: module-only build with game running + POST /reload → new
      behavior live, no restart

## Phase 2 - Discovery (decompiled source + live probing; update CLAUDE.md game facts)

- [ ] todo - Map the choice component: ChoiseButton / DecisionButton / AnswerButton /
      ConsequenceButton roles, text keys, availability checks, click handlers
- [ ] todo - Map TextController paging + how passage text is keyed (scene-per-event is gone;
      ~24 scenes only) and how consequence text is delivered
- [ ] todo - Survey Messenger event names actually fired (WriteAnswerInLog and
      ChangeSceneTitleEvent survive; find the GoToConsequenceEvent successor)
- [ ] todo - Enumerate windows + their controllers (WindowControllersManager) incl. NEW:
      Council, Soul, Articles, Bind, CharacterStatus, BilateralParameters, ChapterSummary
- [ ] todo - Survey the game's own keyboard handling (Update bodies reading Input) so our keys
      never fight the game's
- [ ] todo - SceneStateMachine phases on Jerian: confirm names/order used for delivery gating

## Phase 3 - Core scene reading (the event loop of the game)

- [ ] todo - Scene screen: passage text spoken on delivery, paging keys work
- [ ] todo - Choices: list, focus, activate via game handlers; unavailable choices announce
      the failed requirement
- [ ] todo - Consequence sheet reading (stat changes, rewards)
- [ ] todo - HUD readout (stats, date/chapter context) on demand
- [ ] todo - Scene title / chapter transitions announced off Messenger events

## Phase 4 - Windows

- [ ] todo - Window shell + ScreenManager reconciliation on Jerian's UIManager slots
- [ ] todo - Port surviving windows: Character, Chapter select, Save/Load, Settings, Pause,
      popups (Confirm/Generic), Death/GameOver, Credits, Disclaimer, Main menu
- [ ] todo - NEW windows: Council
- [ ] todo - NEW windows: Soul
- [ ] todo - NEW windows: Articles
- [ ] todo - NEW windows: Bind
- [ ] todo - NEW windows: CharacterStatus + BilateralParameters + ChapterSummary
- [ ] todo - Tooltip controller set: fold tooltip detail onto items (never a separate stop)

## Phase 5 - Cutscenes & interludes

- [ ] todo - Cutscene subtitles spoken (Addressables video bundles; port
      SubtitleSpeechPatches approach); skip key announced state
- [ ] todo - Interlude / chapter picture screens

## Phase 6 - Input & navigation polish

- [ ] todo - Full key map reviewed against Jerian's own keys; KEYS.md regenerated
- [ ] todo - Typeahead search on lists; verbosity settings; UI sounds

## Phase 7 - Localization

- [ ] todo - lang/ tables reviewed: drop Brante-only keys, add Jerian-specific keys, en first,
      then propagate the 13 other languages

## Phase 8 - Regression & release

- [ ] R1 (standing) - Regression sweep: boot → menu → new game → first scene → choice →
      consequence → save/load → quit, all spoken correctly
- [ ] todo - Full-game blind playthrough pass
- [ ] todo - package.ps1 produces a working release zip against a clean game copy
