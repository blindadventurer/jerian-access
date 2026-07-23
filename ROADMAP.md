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
- [x] verified - Git repo initialized, scaffold committed (f82ed56)
- [x] verified - Decompiled reference at jerian-decompiled/ (ilspycmd 8.2, net6 tool; 949 types
      in Assembly-CSharp - Brante had 507 - plus 89 in firstpass, which now hosts I2.Loc)

## Phase 1 - Bring-up (dev server before features)

- [x] verified - Vendored BepInEx 5.4.23.5 installed into F:\games\...; boots clean
      (LogOutput.log: "Chainloader startup complete", runs under Unity v2020.3.43)
- [x] verified - Host + Core + Module compile and deploy. 37 Brante-facing files excluded via
      the csproj BrokenOnJerian list (each re-adds as its surface is ported); GameUi/UiWidgets
      re-pointed (TitleMainText → DescriptionKey; TextMeshProLocalization keys → reflection);
      I2.Loc referenced from Assembly-CSharp-firstpass
- [x] verified - Game boots with the plugin, log prefixed "Jerian Access". GOTCHA fixed live:
      the game destroys BepInEx's manager GameObject ~9s into boot (mid-run, no error) - host
      teardown now keys off Application.quitting, pump survives on the HideAndDontSave object,
      HostPump uses ReferenceEquals ("infrastructure stays up" warning observed, server lives)
- [x] verified - /health answers (module=generation 1, frame counter advances); /eval "1+1"
      → 2 (Mono.CSharp REPL fine on Unity 2020 Mono)
- [x] verified - INPUT DECISION: legacy Input WORKS at runtime - /eval
      UnityEngine.Input.GetKey(KeyCode.A) → False, no throw (game runs Both input backends).
      No InputSystem rewrite needed; OS key repeat read OK (delay=0.5s, interval=0.033s)
- [ ] todo - Speech pipeline up: Prism acquires a backend (or SAPI fallback), /speech captures.
      Dev tap already proven (reload announcement landed at speechCursor=1 under
      JERIAN_NO_SPEECH=1); backends need a run WITHOUT the env var + immediate /mute
- [x] verified - Hot reload proven: module-only build with game running + POST /reload →
      "reloaded: generation 2", /health confirms generation 2, no restart
- [x] verified - steam_appid.txt (2936290) beside the exe: without it SteamManager's
      RestartAppIfNecessary quits the game and respawns it without the doorstop (first-launch
      mystery); with it the game boots in-process. File is part of the install now

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
