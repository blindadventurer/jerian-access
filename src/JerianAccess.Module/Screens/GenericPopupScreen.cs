using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The UIManager popup slot's generic reader (the PopupsEnum family: InfoPopup, TriggerPopup,
    /// ObjectivePopup, CaseOfYear, ConditionHelp, GameFinalsRemind): these popups are
    /// game-composed panels - title, body text, one or two buttons - with no shared model to
    /// adapt, so the swept live components are the readout. The body is the screen announcement;
    /// each visible button is a node that activates through the game's own wiring.
    /// </summary>
    public sealed class GenericPopupScreen : Screen
    {
        public override string Key => "popup";
        public override int Layer => 20;

        // Live reference for first-frame bookkeeping only, never content.
        private GameObject _seen;
        private int _seenFrame;

        public override bool IsActive()
        {
            var p = GameUi.OpenedPopup;
            if (p == null || !p.activeInHierarchy)
            {
                _seen = null;
                return false;
            }
            // Popups that localize in Start still show serialized editor text on the frame
            // UIManager assigns the slot; every popup therefore activates one frame after first
            // observation - Start has run by then, and synchronously-configured popups lose
            // nothing audible.
            if (!ReferenceEquals(p, _seen))
            {
                _seen = p;
                _seenFrame = Time.frameCount;
            }
            return Time.frameCount > _seenFrame;
        }

        public override Message ScreenName
        {
            get
            {
                var p = GameUi.OpenedPopup;
                return p == null ? null : Message.MaybeRaw(PanelText.Read(p));
            }
        }

        public override void Build(GraphBuilder b)
        {
            var p = GameUi.OpenedPopup;
            if (p == null) return;

            b.AddItem(ControlId.Referenced(p.transform, "popup:body"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => PanelText.Read(p), kind: AnnouncementKinds.Label),
                },
            });

            foreach (var button in p.GetComponentsInChildren<Button>(false))
            {
                var btn = button;
                if (!UiWidgets.Visible(btn.gameObject)) continue;
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
    }
}
