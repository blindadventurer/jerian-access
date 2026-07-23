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
using _Scripts.AMVCC.Views.Windows.Cutscenes;
using _Scripts.Localization;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The event scene - the passage pager as a TRANSCRIPT (house pattern): every delivered
    /// page is a text row in one silent-positions stop, arrows re-read earlier pages without
    /// touching game state, Enter on the newest row advances through the game's own NextPage
    /// (sounds, death triggers, skip service all run). A new page re-homes focus SILENTLY; the
    /// queued delivery announcement is the speech, spoken once per page off model state (the
    /// pager's own page index), no matter what advanced it. Special pager indices: -10 = the
    /// choice panel is up (focus moves to the first choice, announced), -15 = the consequence
    /// Continue page. Page text resolves through the game's own I2 keys
    /// ({TextKey}_Text_{n} / {TextKey}_ConsequenceText_{c}_{n}) plus the game's default
    /// replace map (hero name), with the block's character name prefixed where the game shows
    /// a portrait. Choices are ConsequenceButtonController nodes: labels from their I2 key,
    /// activation through the game's own click path (its guards decide), unavailability off
    /// the game's own interactable model. TODO(J): spoken unavailable REASONS (the typed
    /// Condition model), the trigger-scene page (-20), and the HUD Tab-stop follow.
    /// </summary>
    public sealed class SceneScreen : Screen
    {
        public override string Key => "scene";
        public override int Layer => 0;

        public override bool IsActive()
            => GameUi.State == GameState.RUNNING
            && Object.FindObjectOfType<BasicSceneController>() != null
            && !CutsceneScreen.CutsceneShowing;

        public override Message ScreenName
        {
            get
            {
                var key = TitleKey();
                return string.IsNullOrEmpty(key) ? null : Message.MaybeRaw(Translate(key));
            }
        }

        // The scene the game currently has loaded, by its own title key.
        private static string TitleKey()
        {
            var bsc = Object.FindObjectOfType<BasicSceneController>();
            return bsc == null ? null : bsc.TitleKey;
        }

        private static readonly FieldInfo PageIndexField = typeof(TextController)
            .GetField("_pageIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ItsConsequenceField = typeof(TextController)
            .GetField("ItsConsequence", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo NextButtonField = typeof(TextController)
            .GetField("NextButton", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ContinueButtonField = typeof(TextController)
            .GetField("ContinueButton", BindingFlags.NonPublic | BindingFlags.Instance);

        // Live component references for delivery bookkeeping only; all text re-reads at
        // speech time.
        private TextController _watched;
        private int _spokenPage;
        private bool _entryPending;
        private string _spokenTitleKey;

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

        // Reward-sheet delivery state (the stat changes after a choice): read once, when the
        // slider/counter animations have settled, off the rendered content.
        private string _spokenRewards;
        private string _pendingRewards;
        private int _pendingRewardsFrame;

        public override void OnPush() { _entryPending = true; }

        public override void OnFocus()
        {
            base.OnFocus();               // the screen-name announcement IS the title
            _spokenTitleKey = TitleKey(); // null when the scene hasn't set up yet - delivered below
        }

        public override void OnPop()
        {
            _watched = null;
            _spokenRewards = null;
            _pendingRewards = null;
            _spokenTitleKey = null;
        }

        public override void OnUpdate()
        {
            DeliverTitle();
            DeliverRewards();
            var tc = Pager();
            if (tc == null) return;
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
                if (!entry && page >= 0)
                {
                    Navigation.FocusNode(PageId(page), announce: false);
                    Mod.Speech.Speak(PageText(tc, page));
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
            if (page == -15)
            {
                var cont = ContinueButton(tc);
                if (cont != null)
                    Navigation.FocusNode(ControlId.Referenced(cont, "scene:continue"),
                        announce: true);
                return;
            }
            if (page < 0) return;

            // New page delivered: silent re-home, the queued delivery announcement is the
            // speech.
            Navigation.FocusNode(PageId(page), announce: false);
            Mod.Speech.Speak(PageText(tc, page));
        }

        // The next scene loads IN PLACE - the game swaps the whole scene inside one frame, so the
        // screen never leaves the stack and OnFocus (which speaks the screen name) never runs
        // again. The title is therefore delivered off the controller's own key, once per scene,
        // ahead of that scene's first page.
        private void DeliverTitle()
        {
            var key = TitleKey();
            if (string.IsNullOrEmpty(key) || key == _spokenTitleKey) return;
            _spokenTitleKey = key;
            var title = Translate(key);
            if (!string.IsNullOrEmpty(title)) Mod.Speech.Speak(title);
        }

        // The post-choice reward sheet (parameter/relation/status/... changes). The rows render
        // the game's own localized "name, delta, become N" text; sliders animate the value up,
        // so this speaks ONCE the rendered text stops changing (settled), off the final content.
        private void DeliverRewards()
        {
            var sig = RewardSheetText();
            if (string.IsNullOrEmpty(sig)) { _pendingRewards = null; return; }
            if (sig != _pendingRewards)
            {
                _pendingRewards = sig;
                _pendingRewardsFrame = UnityEngine.Time.frameCount;
                return;
            }
            if (UnityEngine.Time.frameCount - _pendingRewardsFrame < 20) return; // still animating
            if (sig == _spokenRewards) return;
            _spokenRewards = sig;
            Mod.Speech.Speak(Loc.T("scene.rewards") + sig);
        }

        private static string RewardSheetText()
        {
            var rows = new List<UnityEngine.Component>();
            foreach (var r in Object.FindObjectsOfType<
                _Scripts.AMVCC.Views.Windows.SceneConsequence.SceneConsequenceRewardRow>())
                if (UiWidgets.Visible(r.gameObject)) rows.Add(r);
            foreach (var r in Object.FindObjectsOfType<
                _Scripts.AMVCC.Views.Windows.SceneConsequence.ConsequenceRewardRow.ParameterConsequenceRewardRow>())
                if (UiWidgets.Visible(r.gameObject)) rows.Add(r);
            if (rows.Count == 0) return null;
            rows.Sort((a, c) => c.transform.position.y.CompareTo(a.transform.position.y));

            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                foreach (var t in row.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (!UiWidgets.Visible(t.gameObject)) continue;
                    var s = t.text;
                    if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(s.Trim());
                }
            }
            return sb.ToString();
        }

        public override void Build(GraphBuilder b)
        {
            var tc = Pager();
            if (tc == null) return;
            int current = PageIndex(tc);

            // Transcript rows: every text page up to the current one (all of them once the
            // pager has moved past the text into choices/continue).
            var rows = new List<int>();
            for (int i = 0; i < tc.TextBlocks.Count; i++)
                if (current < 0 || i <= current) rows.Add(i);

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
                    // Enter on the newest page turns to the next through the game's own pager,
                    // gated on the game's own next-button state (page-turn animations and
                    // death windows disable it).
                    OnActivate = !newest ? (System.Action)null : () =>
                    {
                        var next = (GameObject)NextButtonField.GetValue(tc);
                        if (next != null && next.GetComponent<UnityEngine.UI.Button>().interactable)
                            tc.NextPage();
                    },
                });
            }
            if (current >= 0 && rows.Count > 0) b.SetStart(PageId(current));
            b.PopContext();

            // The consequence Continue - only on its own page (-15); the serialized button
            // object can sit active in the hierarchy earlier while the choices are the
            // surface.
            var contBtn = current == -15 ? ContinueButton(tc) : null;
            if (contBtn != null && contBtn.gameObject.activeInHierarchy)
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
                        // Space reads the full requirement breakdown (met and not-met),
                        // composed from the choice's own typed Condition model.
                        OnTooltip = () => Mod.Speech.Speak(
                            ChoiceConditions.Details(choice) ?? Loc.T("nav.no_details")),
                        OnActivate = () =>
                        {
                            var reason = ChoiceConditions.UnavailableReason(choice);
                            if (reason != null)
                            {
                                Mod.Speech.Speak(reason, interrupt: true);
                                return;
                            }
                            // The game's own resolving gate: a clicked choice sets
                            // SelectedButton until the consequence flow takes over - Enter
                            // must not choose twice.
                            var bsc = Object.FindObjectOfType<BasicSceneController>();
                            if (bsc != null && bsc.SelectedButton != null) return;
                            UiWidgets.Click(choice.Selectable.gameObject);
                        },
                    });
            }
            b.PopContext();

            // The story HUD (resource counters + window buttons) as its own Tab-stop.
            HudReadout.Build(b);
        }

        private static UnityEngine.UI.Button ContinueButton(TextController tc)
        {
            var go = (GameObject)ContinueButtonField.GetValue(tc);
            return go == null ? null : go.GetComponent<UnityEngine.UI.Button>();
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
