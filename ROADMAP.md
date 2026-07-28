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
- [x] verified - Speech pipeline up end to end: "PrismHandler backend acquired: NVDA
      (features=0xB5)", test line spoken through the user's live NVDA and captured at
      /speech cursor 0 ("Джериан Аксесс работает..."); dev tap also proven earlier under
      JERIAN_NO_SPEECH=1 (reload announcement)
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

- [x] verified - Scene screen v1: transcript rows off the pager model, delivery differ on
      _pageIndex (the scripted prologue auto-paging spoken faithfully page by page), Enter
      pages via the game's NextPage, scene title announced ("Взгляд назад"). Verified live
      through the whole prologue including the consequence pager (12 blocks, Enter-paged)
- [x] verified - Disclaimer scene: pages spoken on swap, Enter = game's ClickOnScreen
- [x] verified - Cutscene: subtitles spoken by POLLING the game's TMP line (no Harmony
      needed on Jerian - SubtitleSpeechPatches route obsolete), skip hint announced;
      CutsceneShowing gate on _hasExecuted (controller shell stays active after handoff)
- [x] built - Choices: list/focus/activate via game handlers wired (ConsequenceButtonController,
      -10 seats first choice); prologue resolved its choice scripted, so interactive choice
      activation still needs a live pass in chapter 1
- [ ] built - Unavailable choices announce the failed requirement (Game/ChoiceConditions.cs:
      composes failed non-hidden conditions from the typed Condition model - Parameter,
      Relation, Status, Objective, Ending, Article, Estate, Bilateral - reusing the game's own
      GetNameLocalised/GetTranslation; Space reads the full met/not-met breakdown). Wired into
      SceneScreen choice nodes. Live 2026-07-28: a met condition read correctly on the prologue's
      third choice ("Разум не менее 6, выполнено"). A FAILING one still needs a live pass
- [x] built - Choice RESOURCE LOCKS (Jerian mechanic Brante had no equivalent of): a choice whose
      conditions fail can be bought open with Sanity / Tempest Legacy, and until its
      ConditionLockView is clicked the choice's own click does nothing - so without this the
      choice was unreachable by keyboard. The choice now announces "открыть за N <ресурс>",
      Backspace pays every lock (and pays it back) through the game's own OnClick, and Enter
      states the price instead of clicking into nothing. Availability is computed LIVE from the
      condition groups, not from the button's interactable flag (that flag is set once in Start,
      so on the frame the panel appears every choice read as unavailable)
- [x] built - Hold-to-choose setting: with it on the game leaves the choice button's onClick
      unwired and resolves from the hold sequence, so keyboard activation reached nothing at all.
      Game/HoldChoice.cs runs the view's own ResolveClickEvent instead (scene choices, trip
      options, case-for-a-year options share the shape)
- [x] built - Consequence sheet as choice detail (Game/ChoiceRewards.cs): Space on a choice reads
      what the game paints on the right page for a sighted player - the choice's own description
      key plus its reward rows, composed from the model (name + signed value, character rows
      following the game's own status/relation rules). Hidden-consequences mode is honored - the
      game hides the rows there, so the mod says nothing either. Live 2026-07-28: composition
      checked on the prologue's three choices ("Эмиль, отношение +1, Закон +1")
- [x] verified - HUD readout as a Tab-stop (Game/HudReadout.cs): Sanity + Tempest Legacy counters
      from the model, HUD window buttons with game labels + locked reasons + click activation.
      Live: Tab from the transcript speaks "HUD, Рассудок 5, 1 из 10", "Наследие Темпестов 0,
      2 из 10", then the eight window buttons; Enter on Личность opens the window (stack
      window(20)), Escape returns to the scene. FIX found by the pass: the locked reason read
      the bare lockedTextKey, which translates to nothing - the game completes that key with the
      unlocking chapter (HUD.WillOpen + 2), so a locked button announced no reason and Enter on
      it was SILENT. Now "Карта Империи, кнопка, недоступно, В главе II, 10 из 10" and Enter
      speaks "недоступно, В главе II"; a button the game blocks without locking (IsButtonsBlocked)
      answers with the bare unavailable word instead of clicking into nothing
- [x] built - Trigger-scene page (-20) reading: the page a scene opens on when the player's own
      earlier choices summoned it. Was SILENT and Enter was dead there (the transcript exposed the
      whole unread scene instead), so the page could only be left with the game's own keys. Now one
      node: the page's own header row, the trigger scene's title, and each condition with a met
      marker (the page shows those as dimming); Enter pages on through TextPanelClick
- [x] built - Transcript rows are the pages the game will actually SHOW: a text block whose
      conditions this playthrough fails is skipped by the pager, and reading it meant reading
      passages that are not in the player's story (TextControllerExtensions.GetNextUnlockedTextIndex
      is the game's own rule)
- [x] built - Focus seating on scene re-entry: closing a window re-pushes the scene screen, and
      with the choice panel up the graph's first node (the opening passage) won the seat - the
      scene re-read its first page while the choices sat unheard. The start node is now the live
      surface (choices, else the offered Continue, else the newest page). Verified live 2026-07-28
- [x] built - Consequence sheet reading - see the choice-detail item above (ChoiceRewards)
- [ ] built - Scene title on a scene CHANGE (SceneScreen.DeliverTitle): the next scene loads in
      place inside one frame, so IsActive never dips, the screen never re-focuses and OnFocus's
      screen-name announcement never ran again - only the first page was spoken (observed live:
      speech 245 "Утро. Такое же..." with no title, title only after a hot reload). Now polled
      off BasicSceneController.TitleKey and delivered once per scene, ahead of its first page;
      OnFocus records what it spoke so an entry never doubles. Verified live by swapping the
      controller's key (spoke "Наставление Корнелия" on the change, once). Awaiting confirmation
      on a real transition
- [x] built - Chapter transitions announced: SceneType.ChapterStart and ChapterFinish are
      ChapterSummaryWindowController scenes on Jerian, which ChapterSummaryScreen already reads
      (Brante's own chapter-transition panels are gone - their screens stay excluded)
- [x] built - Inconsequential scenes (SceneType.Inconsequential / InconsequentialSceneController,
      "Window_Inconsequential" - text pages, no choices) were read by NO screen: SceneScreen gated
      on BasicSceneController, so such a scene was silent. SceneScreen now accepts either
      controller, and the title is delivered off the RENDERED title label, not TitleKey, because
      such a scene swaps its title mid-run on a special-scenery block ("ChangeSceneTitleEvent"
      writes the label only)
- [x] built - The two option-picking scene types (Screens/OptionSceneScreen.cs): "Дело на год"
      (CaseForAYear) and the trip selection are whole scenes with no BasicSceneController, so
      nothing saw them. Each option is a node (game label, availability, Space for the tooltip /
      the typed conditions, Enter picks through the hold-aware resolve), with the rest of the page
      as text rows
- [x] built - Chapter-picture scene (SceneType.ChapterPicture): the two title lines as one node
      plus the game's own click handler on Enter (rewritten off ChapterPictureController; Brante's
      StartPictureHelper is gone)
- [x] verified - LoadWindow (save slots) ported: slots read chapter+hero+date+time, Enter
      loads, Backspace deletes, Escape (game's own) closes. Written from decompiled source
- [x] verified - GameLoadingScreen / chapter-timeline: Continue (focus home), five chapter
      items with availability (all "глава не достигнута" on an early save - correct), back to
      menu, age readout, and the on-screen hint. Graph confirmed live via /nav after reload
- [x] verified - Chapter summary book (Window_ChapterSummary - the "event list" the user hit):
      delivery-only screen speaks each settled page (position + left summary + right params) off
      the controller page index; game pages it natively. Live: "1 из 4. 1120. Глава I.
      Небытие. Сюжеты главы..."
- [x] built - Confirm popups (ConfirmPopupScreen, generic): detects the confirm controller
      roots (chapter-restart, save-delete, case-of-year, exit), speaks the title+body as the
      screen announcement, exposes each visible button as a node with game label + click. Not
      yet triggered live (needs an unlocked chapter or a Backspace-delete on a save slot)

## Phase 4 - Windows

- [x] verified - Generic OpenedWindowScreen: reads the open window's content (labels as text
      nodes, buttons/toggles as activatable nodes, reading order) + back-to-scene close node +
      Escape back; folds "*Name"+"*Value" into one stat stop and hover tooltips
      (TooltipWithTitleBehavior / TooltipKeyHolder) onto Space (addresses "tooltips did not
      always read"). KEY FIX: Jerian moved windows off UIManager.OpenedWindow (always null) onto
      WindowControllersManager._activeWindow - GameUi.OpenedWindow now reads that. Verified live
      on the Personality window: "1120, 4 года, Тест, Страсть 5, Разум 5, Кровь 5, Воля 5,
      Закон 5, ...". Minor: a few duplicate bare-value TMP still surface as stray stops - per
      -window polish TODO
- [x] verified - Fixed: chapter-summary re-spoke every frame with the year mixed in (delivery
      now keyed on the settled page index, year label excluded from the body sweep) - 7f41191

- [x] verified - Main menu ported (rewritten for Jerian: TMP text buttons under
      MainMenuController, labels from live game text, Continue availability off
      SaveLoadManagerV2.SavesExist, handoff gate on navigationGroups + visible-Continue gate
      so the fade-in never homes focus on the Discord link). Driven live: full ui.down walk
      announces all 6 buttons with position, ui.home returns, ui.activate on unavailable
      Continue speaks "unavailable". Physical arrows = same dispatcher, need game window focus
- [x] verified - Settings ported (SettingsWindowController via reflection; new Jerian rows:
      cutscene volume, hold-choice toggle, tips toggle; game's own InputSystem escape closes
      the window, so no mod-side Back). Driven live: 11-row walk announces label+state,
      slider adjust speaks percent and applies volume, Menu button exits and the main menu
      re-homes. Language row = Left/Right on "Language" combo (user switches themselves)
- [x] verified - New-game window ported (NewGameWindowController, additive NewGameWindow
      scene): modal name edit with per-letter echo and the game's auto-capitalization, the
      validation subtitle spoken live as the game swaps it (min length / blacklist), three
      ToggleGroup radio pairs under game headers, Start gated on the game model with the
      subtitle as the spoken reason. Verified live in Russian; Menu returns to the main menu.
      NOT yet exercised: actually starting a game (needs SceneScreen first)
- [x] verified - Opening a window seats focus on its CONTENT, and the window announces itself by
      the game's own name for the HUD button that opened it ("Личность"), not the generic word.
      Two bugs behind the old behavior: the screen declared a graph whose only node was the way out
      while the page animated in (focus seated there and stayed), and it re-pushed when the
      page-in animation dipped the CanvasGroup to zero, announcing the window twice. Live
      2026-07-28: "Личность" then "1124" - one announcement, focus on the first content row
- [x] built - Windows: the generic OpenedWindowScreen covers every WindowType the game has
      (Personality, Destiny, Relations, Empire, Map, Creed, Council) - they all open through
      WindowControllersManager, so there is no per-window porting left to make them readable;
      per-window polish (folding rows the sweep splits) stays open
- [x] built - The UIManager popup slot's family (Screens/GenericPopupScreen.cs, re-pointed at
      Jerian): objective / info / condition-help / case-of-year / finals-remind popups read as
      body text plus their buttons
- [x] built - The PopupControllersManager family (Screens/ScenePopupScreen.cs): interlude and
      creed-outcome popups deliver each settled page; the game pages them with its own keys
- [x] built - Tutorial popups (Screens/TutorialPopupScreen.cs). These LOCK scene navigation while
      open, so an unread one was not just silence - the scene had stopped answering. Verified live
      2026-07-28: the Destiny-lock tutorial spoke on the prologue's first choice panel
- [x] built - Death window (Brante's DeathScreen re-pointed: Jerian keeps the same
      DeathWindowBehaviour / SimplePageTurner / ResolveBehaviour shapes; the page-text helper now
      reflects on the type that declares it and the pager's own arrow reads are suppressed while
      the mod owns input)
- [x] built - Credits roll (skipping stays the game's own InputSystem Escape)
- [ ] todo - Tooltip controller set: fold tooltip detail onto items (never a separate stop)
- [ ] todo - Per-window polish: the generic sweep still emits a few bare value stops where a row's
      name and value do not share the "*Name"/"*Value" naming

## Phase 5 - Cutscenes & interludes

- [ ] todo - Cutscene subtitles spoken (Addressables video bundles; port
      SubtitleSpeechPatches approach); skip key announced state
- [ ] todo - Interlude / chapter picture screens

## Phase 6 - Input & navigation polish

- [x] built - The game's own pagers no longer act under the mod's arrows: GameInputPatches now
      also suppresses PageNavigationController.Update (the pager the story scenes actually run on
      once BasicSceneController hands it the TextController - so one physical Right both moved mod
      focus and turned the game's page) and SimplePageTurner.Update (the death window's). Still
      gated on the mod OWNING input, so delivery-only screens keep the game's keys live
- [x] built - Popups that PopupControllersManager instantiates are found by TYPE, not through
      UIManager's popup slot: Jerian never writes that slot for them, so the year-increment and
      day-stage screens could never wake up
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
