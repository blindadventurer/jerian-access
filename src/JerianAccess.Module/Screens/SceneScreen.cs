using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.Controllers;
using _Scripts.AMVCC.Views.ConsequenceButton;
using _Scripts.AMVCC.Views.Windows.ChapterSummary.PageNavigation;
using _Scripts.AMVCC.Views.Windows.Cutscenes;
using _Scripts.AMVCC.Models.Static;
using _Scripts.Localization;
using _Scripts.Managers;
using _Scripts.SpecialPages;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The story scene, in both flavours the game ships: one with choices (BasicSceneController)
    /// and a straight-through one (InconsequentialSceneController) - the same surface to read, so
    /// the same screen reads both and only the choices differ. The passage pager is a TRANSCRIPT
    /// (house pattern): every delivered
    /// page is a text row in one silent-positions stop, arrows re-read earlier pages without
    /// touching game state, Enter on the newest row advances through the game's own click entry
    /// (sounds, death triggers, skip service all run). A new page re-homes focus SILENTLY; the
    /// queued delivery announcement is the speech, spoken once per page off model state (the
    /// pager's own page index), no matter what advanced it. Special pager index -10 = the
    /// choice panel is up (focus moves to the first choice, announced); the Continue is offered
    /// when the game enables it. Page text resolves through the game's own I2 keys
    /// ({TextKey}_Text_{n} / {TextKey}_ConsequenceText_{c}_{n}) plus the game's default
    /// replace map (hero name), with the block's character name prefixed where the game shows
    /// a portrait. Page -20 is the TRIGGER page the scene opens on when the player's earlier
    /// choices are what summoned it. Choices are ConsequenceButtonController nodes: labels from
    /// their I2 key, activation through the game's own click path (its guards decide),
    /// unavailability and unlock prices off the game's own condition model, Space reading the
    /// consequence sheet the game paints for a sighted player on hover.
    /// </summary>
    public sealed class SceneScreen : Screen
    {
        public override string Key => "scene";
        public override int Layer => 0;

        // Live reference for entry bookkeeping only, never content: the controller of the scene
        // this screen entered on. The game DEACTIVATES the scene prefab while a window or a
        // between-events popup covers it, which hides the controller from a type search - and a
        // screen that dropped out there would pop and push again, re-announcing the scene every
        // time one closed. The scene is still the scene; only a destroyed controller (the scene
        // really unloaded) ends it, which Unity's own null reports for us.
        private MonoBehaviour _entered;

        public override bool IsActive()
        {
            if (GameUi.State != GameState.RUNNING || CutsceneScreen.CutsceneShowing) return false;
            var controller = SceneController();
            if (controller != null) _entered = controller;
            return _entered != null;
        }

        public override Message ScreenName => Message.MaybeRaw(TitleText());

        /// <summary>The scene controller the loaded scene runs on - a choice scene's, else a
        /// straight-through scene's.</summary>
        private static MonoBehaviour SceneController()
        {
            var basic = Object.FindObjectOfType<BasicSceneController>();
            if (basic != null) return basic;
            return Object.FindObjectOfType<InconsequentialSceneController>();
        }

        private static readonly FieldInfo BasicTitleField = typeof(BasicSceneController)
            .GetField("titleText", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InconsequentialTitleField =
            typeof(InconsequentialSceneController)
                .GetField("titleText", BindingFlags.NonPublic | BindingFlags.Instance);

        // The title as the page shows it, not as the scene was configured: a straight-through
        // scene swaps its title mid-run on a special-scenery block (ChangeSceneTitleEvent, whose
        // handler writes the label and leaves TitleKey pointing at the scene's own title).
        private static string TitleText()
        {
            var ctrl = SceneController();
            if (ctrl == null) return null;
            var field = ctrl is BasicSceneController ? BasicTitleField : InconsequentialTitleField;
            var label = (TextMeshProLocalization)field.GetValue(ctrl);
            return label == null ? null : UiWidgets.LocalizedLabel(label.gameObject);
        }

        private static readonly FieldInfo PageIndexField = typeof(TextController)
            .GetField("_pageIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TriggerSceneField = typeof(TextController)
            .GetField("_triggerScene", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ItsConsequenceField = typeof(TextController)
            .GetField("ItsConsequence", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ContinueButtonField = typeof(TextController)
            .GetField("ContinueButton", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PageNavField = typeof(TextController)
            .GetField("_pageNavigationController", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PageNavContinueField = typeof(PageNavigationController)
            .GetField("continueButton", BindingFlags.NonPublic | BindingFlags.Instance);

        // Live component references for delivery bookkeeping only; all text re-reads at
        // speech time.
        private TextController _watched;
        private int _spokenPage;
        private bool _entryPending;
        private string _spokenTitle;
        private bool _continueOffered;

        // The consequence pager only activates after a choice resolves - while active it IS
        // the story surface (the game hides the setup pager), so it wins.
        private static TextController Pager()
        {
            TextController main = null;
            foreach (var tc in Object.FindObjectsOfType<TextController>())
            {
                if ((bool)ItsConsequenceField.GetValue(tc)) return tc;
                main = main ?? tc;
            }
            return main;
        }

        private static int PageIndex(TextController tc) => (int)PageIndexField.GetValue(tc);

        // The game's I2 translation plus its own default replace map (hero name etc.).
        private static string Translate(string key)
        {
            var text = I2.Loc.LocalizationManager.GetTranslation(key);
            if (string.IsNullOrEmpty(text)) return text;
            var map = LocalizationExtensions.GetDefaultReplaceMap();
            if (map == null || map.Count == 0) return text;
            var sb = new StringBuilder(text);
            foreach (var pair in map) sb.Replace(pair.Key, pair.Value);
            return sb.ToString();
        }

        // Structural, not Referenced: every row would share the pager component as its
        // reference, and reference-tier focus reconciliation would snap focus back to the
        // first row sharing it on every rebuild.
        private static ControlId PageId(int page) => ControlId.Structural("scene:page:" + page);

        private static string PageText(TextController tc, int page)
        {
            var block = tc.TextBlocks[page];
            var text = Translate(block.SpeechByKey);
            if (block.Character != null)
                text = Translate(block.Character.GetNameTerm()) + ": " + text;
            return text;
        }

        public override void OnPush() { _entryPending = true; }

        // The screen-name announcement IS the scene title, and a scene's title is announced ONCE -
        // like a page, keyed to content rather than to focus. Every overlay the game raises over a
        // live scene (the year flash, a window, a popup) hands focus back on the way out, and
        // re-announcing there repeated the title the player had just heard. What the player needs
        // on the way back is the row they are on, which the navigator's own re-seat speaks.
        public override void OnFocus()
        {
            var title = TitleText(); // empty when the scene hasn't set up yet - delivered below
            if (!string.IsNullOrEmpty(title) && title != _spokenTitle) Mod.Speech.Output(title);
            _spokenTitle = title;
        }

        public override void OnPop()
        {
            _entered = null;
            _watched = null;
            _spokenTitle = null;
            _continueOffered = false;
        }

        public override void OnUpdate()
        {
            DeliverTitle();
            // The stat changes a resolved choice brought, from the scene's own consequence page.
            RewardPage.Deliver();
            var tc = Pager();
            if (tc == null) return;
            DeliverContinue(tc);
            int page = PageIndex(tc);

            // A pager swap. On screen entry the navigator's own focus-seat announcement reads
            // the current row - delivery must not repeat it. Mid-screen (choice resolved into
            // the consequence pager, or a new scene loaded) the swap IS new content.
            if (tc != _watched)
            {
                bool entry = _entryPending;
                _entryPending = false;
                _watched = tc;
                _spokenPage = page;
                _continueOffered = false;
                if (!entry && page >= 0)
                {
                    Navigation.FocusNode(PageId(page), announce: false);
                    Mod.Speech.Output(PageText(tc, page));
                }
                return;
            }
            if (page == _spokenPage) return;
            _spokenPage = page;

            if (page == -10)
            {
                // The choice panel replaced the text: seat focus on the first choice and let
                // the navigator's announcement present it.
                var first = FirstChoice();
                if (first != null)
                    Navigation.FocusNode(
                        ControlId.Referenced(first, "scene:choice:" + first.ButtonIndex),
                        announce: true);
                return;
            }
            if (page == -20)
            {
                // The trigger page opens the scene, ahead of its first passage.
                Navigation.FocusNode(TriggerPageId, announce: true);
                return;
            }
            if (page < 0) return;

            // New page delivered: silent re-home, the queued delivery announcement is the
            // speech.
            Navigation.FocusNode(PageId(page), announce: false);
            Mod.Speech.Output(PageText(tc, page));
        }

        // The consequence Continue, seated when the GAME makes it usable. Its page index is not
        // the signal: on a page owned by a PageNavigationController the button is revealed and
        // enabled at the end of that controller's own run, several pages after the text pager
        // calls itself finished.
        private void DeliverContinue(TextController tc)
        {
            var cont = ReadyContinue(tc);
            if ((cont != null) == _continueOffered) return;
            _continueOffered = cont != null;
            if (cont != null)
                Navigation.FocusNode(ControlId.Referenced(cont, "scene:continue"), announce: true);
        }

        // The next scene loads IN PLACE - the game swaps the whole scene inside one frame, so the
        // screen never leaves the stack and OnFocus (which speaks the screen name) never runs
        // again. The title is therefore delivered off the page's own title label, once per new
        // title, ahead of the page it belongs to.
        private void DeliverTitle()
        {
            var title = TitleText();
            if (string.IsNullOrEmpty(title) || title == _spokenTitle) return;
            _spokenTitle = title;
            Mod.Speech.Output(title);
        }

        public override void Build(GraphBuilder b)
        {
            var tc = Pager();
            if (tc == null) return;
            int current = PageIndex(tc);

            // The trigger page (-20) precedes the passages: the scene announcing itself as a
            // consequence of what the player did earlier. It is the only page on screen while
            // it shows, so the transcript below stays empty until it is paged past.
            if (current == -20)
            {
                b.AddItem(TriggerPageId, new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    SilentRecovery = true,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => TriggerPageText(tc), kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => tc.TextPanelClick(),
                });
                b.SetStart(TriggerPageId);
                HudReadout.Build(b);
                return;
            }

            // Transcript rows: every DELIVERED text page - the ones the pager can reach (a block
            // whose conditions fail is skipped by the game and never shown, so reading it would
            // be reading a passage the player's playthrough does not contain) up to the current
            // one (all of them once the pager has moved past the text into choices/continue).
            var rows = new List<int>();
            for (int i = 0; i < tc.TextBlocks.Count; i++)
                if ((current < 0 || i <= current) && Unlocked(tc, i)) rows.Add(i);

            b.PushContext("", role: null, positions: false);
            foreach (var row in rows)
            {
                int page = row;
                bool newest = current >= 0 && page == current;
                b.AddItem(PageId(page), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    SilentRecovery = true,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => PageText(tc, page),
                            kind: AnnouncementKinds.Label),
                    },
                    // Enter on the newest page turns to the next through TextPanelClick - the
                    // entry the game's own click zone uses, which hands the turn to whichever
                    // pager owns the page. Calling TextController.NextPage directly moves the
                    // text but leaves a PageNavigationController's page model behind, and that
                    // model is what reveals the Continue button - a consequence paged the direct
                    // way is never offered one and the scene cannot be finished. Both routes
                    // carry their own navigation locks, so no gate of ours is needed.
                    OnActivate = !newest ? (System.Action)null : () => tc.TextPanelClick(),
                });
            }
            if (current >= 0 && rows.Count > 0) b.SetStart(PageId(current));
            b.PopContext();

            // The consequence Continue - present exactly while the game offers it; the
            // serialized button object can sit active in the hierarchy earlier, disabled and
            // faded out, while the choices or the text are the surface.
            var contBtn = ReadyContinue(tc);
            if (contBtn != null)
                b.AddItem(ControlId.Referenced(contBtn, "scene:continue"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(contBtn.gameObject),
                            kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => UiWidgets.Click(contBtn.gameObject),
                });

            // Choices follow the transcript, positioned ("1 of 4"). Only the buttons the game
            // is showing exist here (the panel activates on the -10 page).
            b.PushContext("", role: null);
            foreach (var cbc in Choices())
            {
                var choice = cbc;
                b.AddItem(ControlId.Referenced(choice, "scene:choice:" + choice.ButtonIndex),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => Translate(choice.ButtonTextKey),
                                kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(
                                () => ChoiceConditions.UnavailableReason(choice),
                                kind: AnnouncementKinds.Enabled),
                        },
                        SearchText = () => Translate(choice.ButtonTextKey),
                        // Space reads what the sighted player sees on the choice's sheet - the
                        // consequences it promises - then the full requirement breakdown (met
                        // and not-met) from the choice's own typed Condition model.
                        OnTooltip = () => Mod.Speech.Output(Detail(choice) ?? Loc.T("nav.no_details")),
                        // Backspace pays a resource lock open (and pays it back): the game's own
                        // lock click, the only way to reach a choice bought with Sanity or
                        // Tempest Legacy - clicking the choice itself does nothing until then.
                        OnSecondary = () => PayLocks(choice),
                        OnActivate = () => Choose(choice),
                    });
            }
            b.PopContext();

            // Where focus lands when the screen has no remembered position - re-entering the
            // scene after a window closes, most of all. The live surface is what the game is
            // showing: the choice panel if it is up, else the Continue it is offering, else the
            // newest page. Without this the graph's first node wins and the scene re-reads its
            // opening passage while the choices sit unheard below it (seen live).
            if (current == -10)
            {
                var first = FirstChoice();
                if (first != null)
                    b.SetStart(ControlId.Referenced(first, "scene:choice:" + first.ButtonIndex));
            }
            else if (current < 0 && contBtn != null)
            {
                b.SetStart(ControlId.Referenced(contBtn, "scene:continue"));
            }

            // The story HUD (resource counters + window buttons) as its own Tab-stop.
            HudReadout.Build(b);
        }

        /// <summary>The Continue button while the game is offering it - shown AND enabled by
        /// whichever pager owns the page; null otherwise. A page owned by a
        /// PageNavigationController has its OWN Continue (the one that controller reveals at the
        /// end of its run and whose click runs its Continue): the text pager's serialized button
        /// only ever appears on a scene that pages itself.</summary>
        private static UnityEngine.UI.Button ReadyContinue(TextController tc)
        {
            var owner = (PageNavigationController)PageNavField.GetValue(tc);
            var cont = owner != null
                ? (UnityEngine.UI.Button)PageNavContinueField.GetValue(owner)
                : GetComponent<UnityEngine.UI.Button>((GameObject)ContinueButtonField.GetValue(tc));
            return cont != null && cont.gameObject.activeInHierarchy && cont.interactable
                ? cont : null;
        }

        private static T GetComponent<T>(GameObject go) where T : UnityEngine.Component
            => go == null ? null : go.GetComponent<T>();

        // A text block the pager will actually stop on: the game skips blocks whose conditions
        // this playthrough does not meet (TextControllerExtensions.GetNextUnlockedTextIndex).
        private static bool Unlocked(TextController tc, int page)
            => tc.TextBlocks[page].AreAllConditionsSatisfied(tc.GlobalConditionGroups);

        private static readonly ControlId TriggerPageId = ControlId.Structural("scene:trigger");

        /// <summary>The trigger page: the scene the player's earlier choices opened, and the
        /// conditions that opened it. The page's own header row (the game's explanation) leads,
        /// then the scene name, then each condition with its met marker - the page shows those
        /// markers as dimming, which no text sweep can hear.</summary>
        private static string TriggerPageText(TextController tc)
        {
            var trigger = (TriggerScene)TriggerSceneField.GetValue(tc);
            if (trigger == null) return null;
            var parts = new List<string>();
            var header = TriggerPageHeader(tc);
            if (!string.IsNullOrEmpty(header)) parts.Add(header);
            var title = Translate(trigger.SceneTitleKey);
            if (!string.IsNullOrEmpty(title)) parts.Add(title);
            var rows = ChoiceConditions.GroupDetails(trigger.ConditionGroup, tc.gameObject);
            if (rows != null) parts.Add(rows);
            return string.Join(". ", parts.ToArray());
        }

        // The game builds the page's static explanation row from a prefab, first into the
        // conditions container - its label is the game's own word for what this page is.
        private static readonly FieldInfo TriggerPageField = typeof(TextController)
            .GetField("triggerScenePage", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TriggerConditionsField = typeof(TriggerScenePageView)
            .GetField("triggerConditionsContainer", BindingFlags.NonPublic | BindingFlags.Instance);

        private static string TriggerPageHeader(TextController tc)
        {
            var page = (TriggerScenePageView)TriggerPageField.GetValue(tc);
            if (page == null) return null;
            var container = (RectTransform)TriggerConditionsField.GetValue(page);
            if (container == null || container.childCount == 0) return null;
            return UiWidgets.LocalizedLabel(container.GetChild(0).gameObject);
        }

        /// <summary>Space on a choice: its promised consequences (the sheet the game paints on
        /// hover) followed by its requirements.</summary>
        private static string Detail(ConsequenceButtonController choice)
        {
            var sb = new StringBuilder();
            PanelText.Append(sb, ChoiceRewards.Summary(choice));
            PanelText.Append(sb, ChoiceConditions.Details(choice));
            return sb.Length == 0 ? null : sb.ToString();
        }

        // The game's own click gate, in the game's own order: an uninteractable choice states
        // why, an unpaid lock states its price (Backspace pays it), and a choice already
        // resolving is left alone (SelectedButton is set until the consequence flow takes over).
        private static void Choose(ConsequenceButtonController choice)
        {
            if (!choice.GetIsButtonInteractable())
            {
                Mod.Speech.Output(ChoiceConditions.UnavailableReason(choice)
                    ?? Loc.T("state.unavailable"), interrupt: true);
                return;
            }
            if (!ChoiceConditions.LocksPaid(choice))
            {
                Mod.Speech.Output(ChoiceConditions.UnavailableReason(choice)
                    ?? Loc.T("state.unavailable"), interrupt: true);
                return;
            }
            var bsc = Object.FindObjectOfType<BasicSceneController>();
            if (bsc != null && bsc.SelectedButton != null)
            {
                // A choice is already resolving - the scene is mid-handoff and a second click
                // would fight it. Silent by design, but logged: silence is the one answer a
                // player cannot interpret, so a run of these is a bug report.
                Mod.Log("choice " + choice.ButtonIndex + " not clicked: scene already resolving '"
                        + bsc.SelectedButton.ButtonIndex + "'");
                return;
            }

            if (HoldChoice.Holding) HoldChoice.Resolve(choice);
            else UiWidgets.Click(choice.Selectable.gameObject);
        }

        // Pay (or take back) every resource lock on the choice, through the game's own lock
        // handler - the click a sighted player aims at the lock icon. The spoken answer is the
        // state each lock ends in.
        private static void PayLocks(ConsequenceButtonController choice)
        {
            var locks = ChoiceConditions.Locks(choice);
            if (locks.Count == 0)
            {
                Mod.Speech.Output(Loc.T("nav.no_details"), interrupt: true);
                return;
            }
            var said = new List<string>();
            foreach (var view in locks)
            {
                // A lock the player cannot afford (or one the game holds shut behind another
                // lock) answers with the requirement, not with a click that does nothing.
                if (!view.IsUnlocked && !view.IsUnlockable)
                {
                    said.Add(ChoiceConditions.UnavailableReason(choice) ?? Loc.T("state.unavailable"));
                    continue;
                }
                view.OnClick();
                said.Add(ChoiceConditions.LockState(view));
            }
            Mod.Speech.Output(string.Join(", ", said.ToArray()), interrupt: true);
        }

        private static ConsequenceButtonController FirstChoice()
        {
            var all = Choices();
            return all.Count == 0 ? null : all[0];
        }

        private static List<ConsequenceButtonController> Choices()
        {
            var list = new List<ConsequenceButtonController>(
                Object.FindObjectsOfType<ConsequenceButtonController>());
            list.Sort((a, c) => a.ButtonIndex.CompareTo(c.ButtonIndex));
            return list;
        }
    }
}
