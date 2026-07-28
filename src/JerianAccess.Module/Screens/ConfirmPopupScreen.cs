using System.Collections.Generic;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The confirm popups (chapter-restart, save-delete, case-of-year, exit...). They share one
    /// shape - a title, a body line, and Confirm / Cancel buttons - so this handles them
    /// generically: the body text (every visible label that is NOT a button caption) becomes the
    /// screen announcement, and each visible button in the popup becomes a node that activates
    /// through the game's own click wiring. Type-agnostic: it keys off the popup roots, not their
    /// fields, so a new confirm popup works without a code change. High layer so it wins over the
    /// surface beneath it.
    /// </summary>
    public sealed class ConfirmPopupScreen : Screen
    {
        public override string Key => "confirm";
        public override int Layer => 30;

        // The confirm popup controller types (each sits on the popup root). FindObjectOfType per
        // type is cheaper than a full MonoBehaviour scan and avoids grabbing unrelated popups.
        private static readonly System.Type[] ConfirmTypes =
        {
            typeof(_Scripts.AMVCC.Views.Windows.Popup.LoadPreviousChapterConfirmPopup.LoadPreviousChapterConfirmPopup),
            typeof(_Scripts.AMVCC.Views.Windows.DeletePopupConfirmation.DeletePopupConfirmation),
            typeof(_Scripts.AMVCC.Views.Windows.Popup.ConfirmationPopupController),
            typeof(_Scripts.AMVCC.Views.Windows.Popup.ExitConfirmationPopup.ExitConfirmationPopupController),
            typeof(_Scripts.AMVCC.Views.Windows.NewGameConfirmController),
            typeof(_Scripts.AMVCC.Views.ConfirmPopupController),
        };

        private static GameObject Root()
        {
            foreach (var type in ConfirmTypes)
            {
                var comp = Object.FindObjectOfType(type) as MonoBehaviour;
                if (comp != null && comp.gameObject.activeInHierarchy) return comp.gameObject;
            }
            return null;
        }

        public override bool IsActive() => Root() != null;

        public override Message ScreenName
        {
            get
            {
                var root = Root();
                return root == null ? null : Message.MaybeRaw(BodyText(root));
            }
        }

        public override void Build(GraphBuilder b)
        {
            var root = Root();
            if (root == null) return;

            ControlId first = null;
            foreach (var button in Buttons(root))
            {
                var go = button.gameObject;
                var id = ControlId.Referenced(button, "confirm:" + go.name);
                if (first == null) first = id;
                b.AddItem(id, new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go),
                            kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => UiWidgets.Click(go),
                });
            }
            if (first != null) b.SetStart(first);
        }

        // Every visible label under the popup that is NOT a button caption, top-to-bottom - the
        // title and body of the confirm.
        private static string BodyText(GameObject root)
        {
            var texts = new List<TMPro.TMP_Text>();
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!UiWidgets.Visible(t.gameObject)) continue;
                if (t.GetComponentInParent<Button>() != null) continue; // a button caption
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                texts.Add(t);
            }
            texts.Sort((a, c) => c.transform.position.y.CompareTo(a.transform.position.y));
            var sb = new System.Text.StringBuilder();
            foreach (var t in texts)
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(t.text.Trim());
            }
            return sb.ToString();
        }

        // Visible buttons in the popup, left to right (Cancel / Confirm as the game lays them out).
        private static List<Button> Buttons(GameObject root)
        {
            var list = new List<Button>();
            foreach (var btn in root.GetComponentsInChildren<Button>(false))
                if (UiWidgets.Visible(btn.gameObject))
                    list.Add(btn);
            list.Sort((a, c) => a.transform.position.x.CompareTo(c.transform.position.x));
            return list;
        }
    }
}
