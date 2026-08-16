using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.Models.Static;
using _Scripts.AMVCC.Views.Windows;
using _Scripts.AMVCC.Views.Windows.UnlockSceneConsequencePopup;
using _Scripts.Helpers;
using _Scripts.Managers;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The UIManager popup slot's generic reader (the PopupsEnum family: InfoPopup, TriggerPopup,
    /// ObjectivePopup, CaseOfYear, ConditionHelp, GameFinalsRemind): these popups are
    /// game-composed panels - title, body text, one or two buttons - with no shared model to
    /// adapt, so the swept live components are the readout. The body is the screen announcement;
    /// each visible button is a node that activates through the game's own wiring.
    ///
    /// One of them does have a model: the TRIGGER popup, which announces that an event fired
    /// because of what the player did earlier. Its rows are the scene's own trigger conditions,
    /// and whether each is met is drawn as dimming - so those rows are composed from the model
    /// with met markers rather than swept off the screen.
    /// </summary>
    public sealed class GenericPopupScreen : Screen
    {
        public override string Key => "popup";
        public override int Layer => 20;

        // The popup opens as a screen once it has WORDS: the game assigns the slot a frame or
        // more before its texts localize, and a screen that opened on the empty prefab announced
        // an empty body - the player heard a position and nothing else (heard live on the
        // objective popup: "1 of 2").
        public override bool IsActive()
        {
            var p = GameUi.OpenedPopup;
            return p != null && p.activeInHierarchy && PanelText.Read(p).Length > 0;
        }

        // No screen-name announcement: the body node below is the first stop, and its own
        // announcement IS the popup - naming it here would say the whole body twice.
        public override Message ScreenName => null;

        // These popups animate in, and reading order is the order things are ON SCREEN: read
        // mid-animation the rows come out shuffled (the title landed last, after the button).
        // The graph therefore stays EMPTY until the panel has held still - focus seats when the
        // nodes appear, and its announcement is the settled popup.
        private readonly SettledDelivery _settle = new SettledDelivery(0.25f);
        private bool _settled;

        public override void OnPop()
        {
            _settle.Reset();
            _settled = false;
        }

        public override void OnUpdate()
        {
            var p = GameUi.OpenedPopup;
            if (p == null) return;
            bool grew;
            if (_settle.Poll(PanelText.Read(p), out grew) != null) _settled = true;
        }

        public override void Build(GraphBuilder b)
        {
            var p = GameUi.OpenedPopup;
            if (p == null || !_settled) return;

            b.AddItem(ControlId.Referenced(p.transform, "popup:body"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Body(p), kind: AnnouncementKinds.Label),
                },
            });

            foreach (var button in p.GetComponentsInChildren<Button>(false))
            {
                var btn = button;
                if (!UiWidgets.Visible(btn.gameObject)) continue;
                // The full-screen click-away backdrop behind the popup carries no caption. It is
                // a real button, but as a stop it is an unlabeled one that answers nothing.
                if (string.IsNullOrEmpty(UiWidgets.LocalizedLabel(btn.gameObject))) continue;
                b.AddItem(ControlId.Referenced(btn, "popup:btn:" + btn.GetInstanceID()),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => UiWidgets.LocalizedLabel(btn.gameObject),
                                kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(
                                () => UiWidgets.Interactable(btn.gameObject)
                                    ? null : Loc.T("state.unavailable"),
                                kind: AnnouncementKinds.Enabled),
                        },
                        OnActivate = () => UiWidgets.Click(btn.gameObject),
                    });
            }
        }

        /// <summary>The popup's words: the panel's own title and text, and - on the trigger popup -
        /// the requirements that opened this event, each with whether it is met.</summary>
        private static string Body(GameObject popup)
        {
            var trigger = TriggerConditions(popup);
            if (trigger == null) return PanelText.Read(popup);
            // The rendered rows say the same names and figures the model rows do, minus the met
            // marker - reading both said every requirement twice.
            return PanelText.Read(popup, InsideConditionRow) + ". " + trigger;
        }

        private static bool InsideConditionRow(GameObject go)
            => go.GetComponentInParent<ConditionRowController>() != null;

        /// <summary>The trigger popup's condition rows from the game's own model. The popup builds
        /// them from the opened scene's TriggerScene entry (TriggerScenePopupController.
        /// SetConditionText); the rendered rows carry the names and figures but not the one thing
        /// the player is being told - which of them their playthrough actually met.</summary>
        private static string TriggerConditions(GameObject popup)
        {
            if (popup.GetComponentInChildren<TriggerScenePopupController>(true) == null) return null;
            var collector = Singleton<SceneConditionsCollector>.Instance;
            if (collector == null) return null;
            TriggerScene scene;
            // TryGet, not GetTriggerSceneConditionGroup: the getter logs an error of its own for a
            // scene with no entry, every time the popup's announcement is composed.
            if (!collector.TryGetTriggerScene(GameManager.Instance.OpenedSceneName, out scene)
                || scene == null) return null;
            return ChoiceConditions.GroupDetails(scene.ConditionGroup, popup);
        }
    }
}
