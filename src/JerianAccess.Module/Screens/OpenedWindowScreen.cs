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
    /// A generic reader for whatever window the HUD opened (Destiny, Personality/Character,
    /// Relations, Council, Empire, Creed, Map, Soul...) while it has no dedicated screen yet.
    /// One stop in reading order (top-to-bottom, then left-to-right): every visible label
    /// becomes a text node, every visible button/toggle an activatable node, with any hover
    /// tooltip folded onto the item (so Space reads it). A closing node ("back to scene",
    /// the HUD back button) ends the stop. The rendered text IS the game's localized output, so
    /// reading the live components is the model read. Dedicated per-window screens can replace
    /// this later; for now it makes every HUD window navigable and spoken.
    /// </summary>
    public sealed class OpenedWindowScreen : Screen
    {
        public override string Key => "window";
        public override int Layer => 20;

        public override bool IsActive()
        {
            var w = GameUi.OpenedWindow;
            return w != null && UiWidgets.Visible(w);
        }

        public override Message ScreenName
        {
            get
            {
                var w = GameUi.OpenedWindow;
                if (w == null) return null;
                var title = FindTitle(w);
                return title != null ? Message.MaybeRaw(title) : Message.Localized("ui", "screen.window");
            }
        }

        public override void Build(GraphBuilder b)
        {
            var w = GameUi.OpenedWindow;
            if (w == null) return;

            var items = Collect(w);
            b.PushContext("", role: null, positions: false);
            foreach (var it in items)
            {
                if (it.Control is Button btn) AddButton(b, btn);
                else if (it.Control is Toggle tg) AddToggle(b, tg);
                else if (it.Text != null) AddText(b, it.Text);
            }
            b.PopContext();

            // A labeled way out: the HUD back button closes the window through the game's wiring.
            var back = HudReadout.BackButton();
            if (back != null)
                b.AddItem(ControlId.Referenced(back, "window:back"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(back.gameObject),
                            kind: AnnouncementKinds.Label),
                    },
                    OnActivate = () => UiWidgets.Click(back.gameObject),
                });
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, null, _ =>
            {
                var back = HudReadout.BackButton();
                if (back != null) UiWidgets.Click(back.gameObject);
            });
        }

        private struct Item
        {
            public Transform Tf;
            public Selectable Control;
            public TMPro.TMP_Text Text;
        }

        // Controls (buttons/toggles) and standalone labels, in reading order. A label that is
        // part of a control is skipped - the control node speaks it.
        private static List<Item> Collect(GameObject w)
        {
            var items = new List<Item>();
            foreach (var sel in w.GetComponentsInChildren<Selectable>(false))
            {
                if (!(sel is Button) && !(sel is Toggle)) continue;
                if (!UiWidgets.Visible(sel.gameObject)) continue;
                items.Add(new Item { Tf = sel.transform, Control = sel });
            }
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!UiWidgets.Visible(t.gameObject)) continue;
                if (t.GetComponentInParent<Selectable>() != null) continue;
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                items.Add(new Item { Tf = t.transform, Text = t });
            }
            items.Sort((a, c) =>
            {
                int byY = c.Tf.position.y.CompareTo(a.Tf.position.y);
                return byY != 0 ? byY : a.Tf.position.x.CompareTo(c.Tf.position.x);
            });
            return items;
        }

        private static void AddButton(GraphBuilder b, Button btn)
        {
            var go = btn.gameObject;
            b.AddItem(ControlId.Referenced(btn, "window:btn:" + btn.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(
                        () => UiWidgets.Interactable(go) ? null : Loc.T("state.unavailable"),
                        kind: AnnouncementKinds.Enabled),
                },
                OnTooltip = TooltipAction(go),
                OnActivate = () =>
                {
                    if (!UiWidgets.Interactable(go))
                    {
                        Mod.Speech.Speak(Loc.T("state.unavailable"), interrupt: true);
                        return;
                    }
                    UiWidgets.Click(go);
                },
            });
        }

        private static void AddToggle(GraphBuilder b, Toggle tg)
        {
            var go = tg.gameObject;
            b.AddItem(ControlId.Referenced(tg, "window:toggle:" + tg.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T(tg.isOn ? "state.on" : "state.off"),
                        live: true, kind: AnnouncementKinds.Value),
                },
                OnTooltip = TooltipAction(go),
                OnActivate = () => UiWidgets.Click(go),
                StateText = () => Loc.T(tg.isOn ? "state.on" : "state.off"),
            });
        }

        private static void AddText(GraphBuilder b, TMPro.TMP_Text text)
        {
            var go = text.gameObject;
            b.AddItem(ControlId.Referenced(text, "window:text:" + text.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => go == null ? null : text.text, kind: AnnouncementKinds.Label),
                },
                OnTooltip = TooltipAction(go),
            });
        }

        // The game shows most detail in a hover tooltip keyed off a serialized localization key
        // (TooltipWithTitleBehavior / TooltipKeyHolder). Fold it onto Space so the detail is
        // reachable - this is why some tooltips "did not read": a node with no tooltip fold has
        // nothing to speak on Space.
        private static System.Action TooltipAction(GameObject go)
        {
            if (go == null) return null;
            var withTitle = go.GetComponentInParent<_Scripts.AMVCC.Views.TooltipWithTitleBehavior>();
            if (withTitle != null && !string.IsNullOrEmpty(withTitle.DescriptionKey))
                return () => Speak(I2.Loc.LocalizationManager.GetTranslation(withTitle.DescriptionKey)
                    ?? I2.Loc.LocalizationManager.GetTranslation(withTitle.TitleKey));
            var holder = go.GetComponentInParent<TooltipKeyHolder>();
            if (holder != null && !string.IsNullOrEmpty(holder.LocalizationKey))
                return () => Speak(I2.Loc.LocalizationManager.GetTranslation(holder.LocalizationKey));
            return null;
        }

        private static void Speak(string s)
            => Mod.Speech.Speak(string.IsNullOrEmpty(s) ? Loc.T("nav.no_details") : s);

        private static string FindTitle(GameObject w)
        {
            var t = w.transform.Find("Title") ?? w.transform.Find("Header/Title");
            if (t == null) return null;
            var tmp = t.GetComponentInChildren<TMPro.TMP_Text>(true);
            return tmp == null ? null : tmp.text;
        }
    }
}
