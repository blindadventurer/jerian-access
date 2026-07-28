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
- [x] verified - Choice RESOURCE LOCKS (Jerian mechanic Brante had no equivalent of): a choice whose
      conditions fail can be bought open with Sanity / Tempest Legacy, and until its
      ConditionLockView is clicked the choice's own click does nothing - so without this the
      choice was unreachable by keyboard. The choice now announces "открыть за N <ресурс>",
      Backspace pays every lock (and pays it back) through the game's own OnClick, and Enter
      states the price instead of clicking into nothing. Availability is computed LIVE from the
      condition groups, not from the button's interactable flag (that flag is set once in Start,
      so on the frame the panel appears every choice read as unavailable). Live 2026-07-28 across chapters 1-2: the offer is announced on focus ("открыть за 30
      Наследие Темпестов"), Backspace pays it ("открыто, потрачено 30 Наследие Темпестов") and
      pays it back, and a bought choice reads as AVAILABLE - a fix this pass found, since
      availability was read off the conditions the game itself stops consulting once a choice
      can be bought open
- [x] verified - Hold-to-choose setting: with it on the game leaves the choice button's onClick
      unwired and resolves from the hold sequence, so keyboard activation reached nothing at all.
      Game/HoldChoice.cs runs the view's own ResolveClickEvent instead (scene choices, trip
      options, case-for-a-year options share the shape). The setting is off in the test save, so the direct-click path is what the chapter 1-3 run
      exercised; the hold path shares its resolve call with the option scenes
- [x] verified - Consequence sheet as choice detail (Game/ChoiceRewards.cs): Space on a choice reads
      what the game paints on the right page for a sighted player - the choice's own description
      key plus its reward rows, composed from the model (name + signed value, character rows
      following the game's own status/relation rules). Hidden-consequences mode is honored - the
      game hides the rows there, so the mod says nothing either. Live 2026-07-28: composition
      checked on the prologue's three choices ("Эмиль, отношение +1, Закон +1"). Live 2026-07-28 in chapter 2 with hidden consequences turned off: "Ты откажешься от
      перстня... изменения: Уттер Темпест, Разочарование, отношение -1, Сервилия Темпест,
      отношение -1, Корнелий Темпест, отношение +1, Воля +1"
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
- [x] verified - Trigger-scene page (-20) reading: the page a scene opens on when the player's own
      earlier choices summoned it. Was SILENT and Enter was dead there (the transcript exposed the
      whole unread scene instead), so the page could only be left with the game's own keys. Now one
      node: the page's own header row, the trigger scene's title, and each condition with a met
      marker (the page shows those as dimming); Enter pages on through TextPanelClick. Live 2026-07-28: "Условие. За горизонт. Фердинанд эль Бер не Разбитые надежды, выполнено" -
      and the pass moved the negation to where the game draws it, between the name and the value
- [x] verified - Transcript rows are the pages the game will actually SHOW: a text block whose
      conditions this playthrough fails is skipped by the pager, and reading it meant reading
      passages that are not in the player's story (TextControllerExtensions.GetNextUnlockedTextIndex
      is the game's own rule). Exercised by every scene of the chapter 1-3 run
- [x] verified - Focus seating on scene re-entry: closing a window re-pushes the scene screen, and
      with the choice panel up the graph's first node (the opening passage) won the seat - the
      scene re-read its first page while the choices sat unheard. The start node is now the live
      surface (choices, else the offered Continue, else the newest page). Verified live 2026-07-28. Re-verified through the window pass: closing a window returns focus to the story row
- [x] built - Consequence sheet reading - see the choice-detail item above (ChoiceRewards)
- [x] built - The post-choice CONSEQUENCE PAGE (Game/RewardPage.cs) - what a resolved choice
      actually changed. USER BUG 2026-07-29, hidden-consequences mode on, the prologue wardrobe
      scene: a parameter changed, the game played its parameter-change sound and the mod said
      NOTHING. The page was read by a sweep of two row component types, and the game builds NINE:
      the skills, personality, relations, resources, estates and empire groups - everything a
      prologue choice grants - use their own row prefabs and were all missed. With the sheet
      hidden before the pick, that page is the only word the player gets, so the change was
      unknowable. Now composed from the model the rows are built from
      (SceneConsequenceController._rewardGroups at its own page index, through
      ChoiceRewards.Describe), which is also the only correct source: the rendered rows carry no
      delta at all (a skill slider renders "Закон – 3" and animates, an estate is a crest). Both
      screens a consequence page can appear under call it - the story scene and the option scene
      (case-for-a-year / trip) - sharing one delivery so neither doubles, and the option scene's
      text sweep now leaves the rows to it. NOT yet verified live
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
- [x] verified - Chapter transitions announced: SceneType.ChapterStart and ChapterFinish are
      ChapterSummaryWindowController scenes on Jerian, which ChapterSummaryScreen already reads
      (Brante's own chapter-transition panels are gone - their screens stay excluded). Live 2026-07-28 on both the chapter 1 to 2 and the 2 to 3 transition - closing book, chapter
      picture, opening book, each page spoken once with its position
- [x] verified - Inconsequential scenes (SceneType.Inconsequential / InconsequentialSceneController,
      "Window_Inconsequential" - text pages, no choices) were read by NO screen: SceneScreen gated
      on BasicSceneController, so such a scene was silent. SceneScreen now accepts either
      controller, and the title is delivered off the RENDERED title label, not TitleKey, because
      such a scene swaps its title mid-run on a special-scenery block ("ChangeSceneTitleEvent"
      writes the label only). Exercised repeatedly through chapters 1-3
- [x] verified - The two option-picking scene types (Screens/OptionSceneScreen.cs): "Дело на год"
      (CaseForAYear) and the trip selection are whole scenes with no BasicSceneController, so
      nothing saw them. Each option is a node (game label, availability, Space for the tooltip /
      the typed conditions, Enter picks through the hold-aware resolve), with the rest of the page
      as text rows. Live 2026-07-28 on chapter 2's "Имперские Проекты" and "Сила Темпестов": nine
      options walked and one picked by keyboard. Three fixes the pass found - the page's own words
      now come FIRST and no longer recite every option card; and after the pick the scene turns
      into a CONSEQUENCE page the game pages itself, where the screen used to keep claiming UI and
      left the page unturnable by any key (now delivery-only, like the interlude popups)
- [x] verified - Chapter-picture scene (SceneType.ChapterPicture): the two title lines as one node
      plus the game's own click handler on Enter (rewritten off ChapterPictureController; Brante's
      StartPictureHelper is gone). Live 2026-07-28: "1134г. Глава III. Великий Цензор, Enter продолжить", Enter continues
- [x] verified - LoadWindow (save slots) ported: slots read chapter+hero+date+time, Enter
      loads, Backspace deletes, Escape (game's own) closes. Written from decompiled source
- [x] verified - GameLoadingScreen / chapter-timeline: Continue (focus home), five chapter
      items with availability (all "глава не достигнута" on an early save - correct), back to
      menu, age readout, and the on-screen hint. Graph confirmed live via /nav after reload
- [x] verified - Chapter summary book (Window_ChapterSummary - the "event list" the user hit):
      delivery-only screen speaks each settled page (position + left summary + right params) off
      the controller page index; game pages it natively. Live: "1 из 4. 1120. Глава I.
      Небытие. Сюжеты главы..."
- [x] verified - Confirm popups (ConfirmPopupScreen, generic): detects the confirm controller
      roots (chapter-restart, save-delete, case-of-year, exit), speaks the title+body as the
      screen announcement, exposes each visible button as a node with game label + click. Live
      2026-07-28 on the pause menu's exit confirm: "ВЫЙТИ ИЗ ИГРЫ. Вы уверены, что хотите выйти
      в главное меню?..." then "Выйти, кнопка, 1 из 2" / "Отменить, кнопка, 2 из 2"; Cancel
      returned to the scene

## Phase 4 - Windows

- [x] verified - Pause menu (Screens/PauseScreen.cs, WindowPause read by reflection). Live
      2026-07-28: "пауза" then all nine rows - three volume sliders with their percentages, the
      four game-setting toggles with their states, exit to menu and back to game. The game's own
      Escape opens and closes it; the mod adds no key of its own there

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
      Live 2026-07-28 on all seven: Personality, Destiny, Relations, Empire and Map in
      chapters 2-3, then Creed and Council once chapter 4 unlocked them (the creed articles with
      each council seat's position, the council seats with their holders and loyalty). Four fixes the pass found: the MAP was read by nothing at all (a picture with
      fourteen hover zones and no text - the screen's content test wanted text, so the window
      never opened as a screen and the player was left on a dead scene); figures the game leaves
      to its hover tooltip read as stops that said a number and nothing else ("22", "15", "0"),
      and now carry the name from the data container the tooltip reads; a row that renders only
      its reading says whose it is ("Великие дома, Лояльность – 12"); a scale's end marker (every
      personality axis ends in "20") is the ruler, not a reading, so it is no longer a stop; and a
      "*Name" label pairs with a "*Value" only inside its own row - the hero's name had claimed a
      value from the far side of the page, leaving that row with none
- [x] verified - The UIManager popup slot's family (Screens/GenericPopupScreen.cs, re-pointed at
      Jerian): objective / info / condition-help / case-of-year / finals-remind popups read as
      body text plus their buttons. Live 2026-07-28 on the objective popup (raised through the
      game's own UIManager.ShowPopup): it announces title, conditions and description in reading
      order, "Продолжить" activates. Two fixes the pass found - the screen opened the frame the
      game assigned the slot, before the texts localized and while the panel animated in, so the
      body was read empty (a bare "1 из 2") or with its rows shuffled; and the click-away
      backdrop was an unlabeled stop that answered nothing
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
- [x] verified - Credits roll (skipping stays the game's own InputSystem Escape). Live
      2026-07-28 from the main menu: announces "титры", the credit block reads as a text row,
      and the game's own Escape returns to the menu. Known limit: the roll exposes ONE block at a
      time and the mod does not re-announce as the scroll replaces it - a player reads the block
      on the stop and skips when done
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
- [ ] built - The intro spread (IntroSceneController): two pages of setup text and the button on
      to the story. A full new-game pass on 2026-07-28 (menu -> new game -> name -> disclaimer ->
      cutscene -> prologue -> chapter 1) never reached it, and nothing in the decompiled tree
      references the controller outside its own prefab - it may be demo-only. The screen keys off
      the type, so it reads if the game ever raises it

## Phase 5 - Cutscenes & interludes

- [x] verified - Cutscene subtitles spoken. No Harmony patch on Jerian: the game's own localized
      TMP line is polled and spoken as it toggles per phrase (SubtitleSpeechPatches stays
      excluded). Live 2026-07-28 on the chapter-2 opening cutscene - the skip hint, then all
      twenty-six subtitle lines in order
- [x] verified - Interlude / chapter picture screens: the interludes read and page (see the
      PopupControllersManager item), and the chapter picture announces its two title lines with
      the Enter hint ("1132г. Глава II. Наследник, Enter продолжить")

## Phase 6 - Input & navigation polish

- [x] verified - The game's own pagers no longer act under the mod's arrows: GameInputPatches now
      also suppresses PageNavigationController.Update (the pager the story scenes actually run on
      once BasicSceneController hands it the TextController - so one physical Right both moved mod
      focus and turned the game's page) and SimplePageTurner.Update (the death window's). Still
      gated on the mod OWNING input, so delivery-only screens keep the game's keys live. And the invariant behind it is structural now - see the delivery-only blocker item above
- [x] verified - Popups that PopupControllersManager instantiates are found by TYPE, not through
      UIManager's popup slot: Jerian never writes that slot for them, so the year-increment and
      day-stage screens could never wake up. Live: the year flash reads on every year change of the chapter 1-3 run
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

- [x] verified - R1 sweep, 2026-07-28: boot -> main menu -> NEW GAME (name entry with the game's
      own validation reason on the Start button, the three setting radio pairs) -> disclaimer ->
      opening cutscene (all subtitles) -> prologue scene -> choice panel -> chapter picture ->
      chapter book -> chapter 1 scene; then exit to menu -> load window -> the older save ->
      chapter 3 resumed. Two blockers the sweep found are fixed: a SECOND save made the load
      window unusable (and silenced the whole module), and the disclaimer announced a raw
      localization key


- [ ] R1 (standing) - Regression sweep: boot → menu → new game → first scene → choice →
      consequence → save/load → quit, all spoken correctly (last run 2026-07-28, above)
- [x] verified - Driven playthrough pass, 2026-07-28: the chapter-1 test save driven forward
      through chapters 1, 2, 3 and into 4 (~2000 driver steps) with every action's speech read
      back. Every surface the run met is covered and listed above; the run met no silent screen
      and no dead end after the fixes it found. NOT met by this run, so still unverified live:
      the DEATH window, the TRIP SELECTION scene, the insurrection day-stage flash and the intro
      spread. Chapter 4 was then replayed from its own save for another 800 driver steps (a second
      set of choices), which turned up nothing unread. Those four were then chased down directly -
      see the item below
- [x] verified - Full playthrough to an ENDING, 2026-07-28: the driven run reached the "Крах"
      finale in chapter 4 - the scene's own reward popup announcing the ending, the epilogue
      scenes, and the credits roll - all spoken, with no silent surface left on the path. One
      blocker the finale found: that reward popup (the ending, the estates it moved, a war's
      outcome) was read by nothing and could not be paged by any key, since it lives inside the
      scene rather than in the popup manager's container
- [x] verified - The four surfaces the driven run never met, chased down deliberately:
      * TRIP SELECTION - raised by loading its own scene configuration through the game's
        LoadScene ("FirstTripSelection"). Reads: the page's own words, then six provinces, each
        naming the two ESTATES its trip courts (the game draws those as crests - not a word of
        text), Space reads the game's tooltip, Enter resolves the pick. Two fixes came out of it:
        the estate crests are named at all, and the outcome panel's bare figures now carry their
        names ("Сила Темпестов 15") instead of reading as a run of loose numbers
      * GAME OVER scene - loaded directly; reads "Game over" and the credit block, once (it used
        to say the title twice, screen name and first stop)
      * DEATH window, INTRO spread, day-stage flash - THE GAME NEVER RAISES THEM. No scene in the
        build settings, no prefab in the addressables catalog (which does ship every other popup),
        and no code reference outside their own classes: they are inherited from the shared
        Brante-lineage codebase and cut from this game. Their screens stay registered and keyed by
        type, so they read if a patch ever ships that content
- [x] verified - Chapter 5 ("Тысячелетний Сенат") was NOT reached by driving: chapter 4 was
      played twice end to end (rotating picks, then first-pick, ~1500 driver steps inside it) and
      both branches finished the game there - the driver does not play well enough to survive
      into the last chapter. Nothing chapter-5-specific is known to be uncovered: its only
      distinct surface, the day-stage flash, ships in no form (see the item above), and its
      scenes, option pages and interludes are shapes this port has read hundreds of times
- [ ] todo - Full-game blind playthrough pass by the USER (every chapter, every branch)
- [x] verified - package.ps1 produces a working release zip against a clean game copy. Built
      artifacts\JerianAccess-0.9.0.zip (1.1 MB: BepInEx 5.4.23.5, the Release plugin + module,
      the locale tables, prism.dll, steam_appid.txt, Russian README and key reference, licenses),
      then INSTALLED IT AS A TESTER WOULD: the dev deploy was moved aside, the archive extracted
      into the game folder, the game started from its own exe. Log: "Loading [Jerian Access
      0.9.0]", 24 screens registered, "reflection audit: 56 game member lookups resolve",
      "PrismHandler backend acquired: NVDA", "handler loaded: prism" - and the main menu spoke
      through the live screen reader. Note for anyone repeating this: without the dev server the
      game is a normal Unity app again and PAUSES while its window is not focused, so a boot
      driven from a terminal stalls until the window is brought forward
