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

- [x] verified - Choice component mapped: ConsequenceButtonController, its ConditionGroup model,
      resource locks and the hold-to-choose path (CLAUDE.md game facts)
- [x] verified - TextController paging mapped: page indices (-10 choices, -15 continue, -20
      trigger page), condition-skipped blocks, TextPanelClick as the turn entry, and the
      PageNavigationController the scene controllers hand it
- [x] verified - Windows enumerated: exactly the 7 WindowTypes, all through
      WindowControllersManager. Soul / Articles / Bind / CharacterStatus / BilateralParameters
      do not exist in this build; ChapterSummary is a SCENE type, not a window
- [x] verified - The game's own keyboard handling surveyed: GlobalConfig left/right/next/skip
      keycodes read by TextController, PageNavigationController and SimplePageTurner (all three
      suppressed while the mod owns input), plus InputSystem actions for Escape/submit in the
      credits and chapter-picture controllers (left alone - the mod never claims those)
- [x] dropped - Survey Messenger event names / SceneStateMachine phases. Both were discovery for
      an event-driven delivery design; every surface the port ships delivers off POLLED model
      state instead (the pager's page index, the controller's ready flag, the rendered panel),
      which is what the reference mods do and what survives the game hiding and re-showing a
      scene. Nothing left in the tree needs either survey - reopen if a surface turns up that
      polling cannot see

## Phase 3 - Core scene reading (the event loop of the game)

- [x] verified - Scene screen v1: transcript rows off the pager model, delivery differ on
      _pageIndex (the scripted prologue auto-paging spoken faithfully page by page), Enter
      pages via the game's NextPage, scene title announced ("Взгляд назад"). Verified live
      through the whole prologue including the consequence pager (12 blocks, Enter-paged)
- [x] verified - Disclaimer scene: pages spoken on swap, Enter = game's ClickOnScreen
- [x] verified - Cutscene: subtitles spoken by POLLING the game's TMP line (no Harmony
      needed on Jerian - SubtitleSpeechPatches route obsolete), skip hint announced;
      CutsceneShowing gate on _hasExecuted (controller shell stays active after handoff)
- [x] verified - Choices: list/focus/activate via game handlers wired (ConsequenceButtonController,
      -10 seats first choice). Live 2026-07-28: every choice panel of the chapter-1 run was walked
      and resolved by keyboard alone (announcement, availability, Enter) - six scenes' worth
- [x] verified - Unavailable choices announce the failed requirement (Game/ChoiceConditions.cs:
      composes failed non-hidden conditions from the typed Condition model - Parameter,
      Relation, Status, Objective, Ending, Article, Estate, Bilateral - reusing the game's own
      GetNameLocalised/GetTranslation; Space reads the full met/not-met breakdown). Wired into
      SceneScreen choice nodes. Live 2026-07-28: a met condition read correctly on the prologue's
      third choice ("Разум не менее 6, выполнено"); FAILING ones read through the chapter-1 run
      ("Заступиться за семью Марты, кнопка, недоступно, требуется Страсть не менее 6")
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
- [x] verified - Scene title on a scene CHANGE (SceneScreen.DeliverTitle): the next scene loads in
      place inside one frame, so IsActive never dips, the screen never re-focuses and OnFocus's
      screen-name announcement never ran again - only the first page was spoken (observed live:
      speech 245 "Утро. Такое же..." with no title, title only after a hot reload). Now polled
      off BasicSceneController.TitleKey and delivered once per scene, ahead of its first page;
      OnFocus records what it spoke so an entry never doubles. Verified live by swapping the
      controller's key (spoke "Наставление Корнелия" on the change, once). Confirmed on real
      transitions through chapter 1 - and the title no longer DOUBLES when the year flash (or any
      overlay) hands focus back: the title is announced once per scene, keyed to content like a
      page, never on re-focus (the navigator's re-seat speaks the row the player is on)
- [x] built - Chapter transitions announced: SceneType.ChapterStart and ChapterFinish are
      ChapterSummaryWindowController scenes on Jerian, which ChapterSummaryScreen already reads
      (Brante's own chapter-transition panels are gone - their screens stay excluded)
- [x] built - Inconsequential scenes (SceneType.Inconsequential / InconsequentialSceneController,
      "Window_Inconsequential" - text pages, no choices) were read by NO screen: SceneScreen gated
      on BasicSceneController, so such a scene was silent. SceneScreen now accepts either
      controller, and the title is delivered off the RENDERED title label, not TitleKey, because
      such a scene swaps its title mid-run on a special-scenery block ("ChangeSceneTitleEvent"
      writes the label only)
- [x] verified - The two option-picking scene types (Screens/OptionSceneScreen.cs): "Дело на год"
      (CaseForAYear) and the trip selection are whole scenes with no BasicSceneController, so
      nothing saw them. Each option is a node (game label, availability, Space for the tooltip /
      the typed conditions, Enter picks through the hold-aware resolve), with the rest of the page
      as text rows. Live 2026-07-28 on chapter 2's "Имперские Проекты" and "Сила Темпестов": nine
      options walked and one picked by keyboard. Three fixes the pass found - the page's own words
      now come FIRST and no longer recite every option card; and after the pick the scene turns
      into a CONSEQUENCE page the game pages itself, where the screen used to keep claiming UI and
      left the page unturnable by any key (now delivery-only, like the interlude popups)
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
- [x] verified - Windows: the generic OpenedWindowScreen covers every WindowType the game has
      (Personality, Destiny, Relations, Empire, Map, Creed, Council) - they all open through
      WindowControllersManager, so there is no per-window porting left to make them readable.
      Live 2026-07-28 on Personality, Destiny, Empire and Map (the four a chapter-2 run can
      reach). Four fixes the pass found: the MAP was read by nothing at all (a picture with
      fourteen hover zones and no text - the screen's content test wanted text, so the window
      never opened as a screen and the player was left on a dead scene); figures the game leaves
      to its hover tooltip read as stops that said a number and nothing else ("22", "15", "0"),
      and now carry the name from the data container the tooltip reads; a row that renders only
      its reading says whose it is ("Великие дома, Лояльность – 12"); a scale's end marker (every
      personality axis ends in "20") is the ruler, not a reading, so it is no longer a stop; and a
      "*Name" label pairs with a "*Value" only inside its own row - the hero's name had claimed a
      value from the far side of the page, leaving that row with none
- [x] built - The UIManager popup slot's family (Screens/GenericPopupScreen.cs, re-pointed at
      Jerian): objective / info / condition-help / case-of-year / finals-remind popups read as
      body text plus their buttons
- [x] verified - The PopupControllersManager family (Screens/ScenePopupScreen.cs): interlude and
      creed-outcome popups deliver each settled page; the game pages them with its own keys.
      Live 2026-07-28 on three chapter-1 interludes (a nine-page newspaper among them). Two fixes
      the pass found: the popup's TITLE is chrome that stays while the pages turn, so it is read
      with the first page only; and a page that GREW opened on the composer's separator, which the
      reader voiced ("точка Вестник Аркнии")
- [x] verified - BLOCKER: a delivery-only screen was unpageable by ANY key. Those screens claim no
      input on purpose - the game pages them with its own keys - but the covered scene UNDERNEATH
      kept claiming UI, and GameInputPatches suppresses the game's pagers for exactly as long as
      the mod claims UI. So every interlude, tutorial and chapter book opened over a scene was a
      dead end for a keyboard player. A screen that declares no categories now stops the category
      walk (ScreenManager.ProvideCategories): the keyboard goes to the GAME, not to a covered
      screen. Live: with an interlude up, CategoryActive(UI) went True -> False and its pages turn
- [x] verified - Tutorial popups (Screens/TutorialPopupScreen.cs). These LOCK scene navigation
      while open, so an unread one was not just silence - the scene had stopped answering.
      Verified live 2026-07-28: the Destiny-lock tutorial spoke on the prologue's first choice
      panel, and chapter 1's Sanity-lock tutorial read and paged in the chapter-1 run
- [x] built - Death window (Brante's DeathScreen re-pointed: Jerian keeps the same
      DeathWindowBehaviour / SimplePageTurner / ResolveBehaviour shapes; the page-text helper now
      reflects on the type that declares it and the pager's own arrow reads are suppressed while
      the mod owns input)
- [x] built - Credits roll (skipping stays the game's own InputSystem Escape)
- [x] verified - Tooltip controller set (Game/TooltipReader.cs): Jerian hangs nearly all detail on
      TooltipZoneView, and Space now asks the zone to open, waits for the game to build its view,
      speaks it and closes it - one path for all ten tooltip types, no per-type modelling. A
      pending read is cancelled by the next keypress (a tooltip that arrives after focus moved
      would describe the control the player just left). Live 2026-07-28: the HUD Sanity counter
      reads its whole scale; a Personality row reads "Разум. Склонность к анализу... 3..7 Баланс
      Разума и Страсти..."; a Destiny objective reads its description plus every condition
- [x] verified - Windows were hiding most of their content: Jerian's hover zones ARE Selectables,
      and the sweep skipped every label owned by one. Only buttons and toggles count as controls
      now. Live: the Destiny window went from "title + 3 years + 6 locked tabs" to the whole
      chronicle and objective list
- [x] verified - Per-window polish: labels the game drew under one parent read as ONE stop with a
      bare figure last ("Отношения 1"); picture items (a hover zone with no words - an objective,
      a chronicle entry, a seal) are named from the game's own data containers; the item a list is
      showing announces "выбрано", not "недоступно"; comparison operators speak as words
- [x] verified - The confirm popups' click-away backdrop is no longer a stop (it took the opening
      focus, so "exit to menu?" opened on an unlabeled button)
- [x] built - The intro spread a new game opens on (IntroSceneController): two pages of setup text
      and the button on to the story, previously read by nothing. Needs a new-game pass

## Phase 5 - Cutscenes & interludes

- [x] verified - Cutscene subtitles spoken. No Harmony patch on Jerian: the game's own localized
      TMP line is polled and spoken as it toggles per phrase (SubtitleSpeechPatches stays
      excluded). Live 2026-07-28 on the chapter-2 opening cutscene - the skip hint, then all
      twenty-six subtitle lines in order
- [x] verified - Interlude / chapter picture screens: the interludes read and page (see the
      PopupControllersManager item), and the chapter picture announces its two title lines with
      the Enter hint ("1132г. Глава II. Наследник, Enter продолжить")

## Phase 6 - Input & navigation polish

- [x] built - The game's own pagers no longer act under the mod's arrows: GameInputPatches now
      also suppresses PageNavigationController.Update (the pager the story scenes actually run on
      once BasicSceneController hands it the TextController - so one physical Right both moved mod
      focus and turned the game's page) and SimplePageTurner.Update (the death window's). Still
      gated on the mod OWNING input, so delivery-only screens keep the game's keys live
- [x] built - Popups that PopupControllersManager instantiates are found by TYPE, not through
      UIManager's popup slot: Jerian never writes that slot for them, so the year-increment and
      day-stage screens could never wake up
- [x] verified - Full key map reviewed against Jerian's own keys (GlobalConfig: left = A /
      Left, right = D / Right / Space, continue = Enter / keypad Enter, skip = either Shift):
      the mod's own bindings match the habits those keys teach, so a surface the game pages
      itself needs no new key from the player. KEYS.md regenerated - it now documents the
      game-paged surfaces and Backspace paying (and un-paying) a choice's resource lock
- [ ] todo - Typeahead search on lists; verbosity settings; UI sounds

## Phase 7 - Localization

- [ ] todo - lang/ tables reviewed: drop Brante-only keys, add Jerian-specific keys, en first,
      then propagate the 13 other languages

## Phase 8 - Regression & release

- [ ] R1 (standing) - Regression sweep: boot → menu → new game → first scene → choice →
      consequence → save/load → quit, all spoken correctly
- [ ] todo - Full-game blind playthrough pass
- [ ] todo - package.ps1 produces a working release zip against a clean game copy
