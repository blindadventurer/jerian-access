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

        // The window's CONTENT is what makes it a screen. The game instantiates the window
        // prefab and then animates its page in, and for those frames the only node in the graph
        // is the back button - the screen would open announcing "back to scene" and the content
        // would need an arrow press to reach. Waiting for the first rendered label seats the
        // opening focus on the window itself.
        // Live reference for entry bookkeeping only, never content: the window this screen has
        // already opened on. Once open it stays open until the game destroys the window - the
        // page-in animation dips the CanvasGroup to zero on the way, and a screen that dropped
        // out there would push again and announce the window twice (heard live).
        private GameObject _entered;

        public override bool IsActive()
        {
            var w = GameUi.OpenedWindow;
            if (w == null) { _entered = null; return false; }
            if (ReferenceEquals(w, _entered)) return true;
            if (!UiWidgets.Visible(w) || !HasContent(w)) return false;
            _entered = w;
            return true;
        }

        private static bool HasContent(GameObject w)
        {
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                if (UiWidgets.Visible(t.gameObject)) return true;
            }
            return false;
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

            // Nothing rendered yet (the page is still animating in): declare an EMPTY graph
            // rather than a back button on its own. A graph whose only node is the way out seats
            // focus there, and focus stays where it was seated once the content arrives - the
            // window would open on "back to scene" every time (seen live).
            var items = Collect(w);
            if (items.Count == 0) return;

            b.PushContext("", role: null, positions: false);
            foreach (var it in items)
            {
                if (it.Control is Button btn) AddButton(b, btn);
                else if (it.Control is Toggle tg) AddToggle(b, tg);
                else if (it.Text != null) AddText(b, it.Text, it.ValueText);
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
                        new NodeAnnouncement(() => HudReadout.BackLabel(), kind: AnnouncementKinds.Label),
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
            public TMPro.TMP_Text ValueText; // folded value for a "*Name" + "*Value" pair
        }

        // Controls (buttons/toggles) and standalone labels, in reading order. A label that is
        // part of a control is skipped - the control node speaks it. A "...Name" label folds its
        // sibling "...Value" onto one item ("Разум 5") so a stat reads as a single stop.
        private static List<Item> Collect(GameObject w)
        {
            var items = new List<Item>();
            foreach (var sel in w.GetComponentsInChildren<Selectable>(false))
            {
                if (!(sel is Button) && !(sel is Toggle)) continue;
                if (!UiWidgets.Visible(sel.gameObject)) continue;
                items.Add(new Item { Tf = sel.transform, Control = sel });
            }

            var texts = new List<TMPro.TMP_Text>();
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!UiWidgets.Visible(t.gameObject)) continue;
                if (t.GetComponentInParent<Selectable>() != null) continue;
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                texts.Add(t);
            }

            var consumed = new HashSet<TMPro.TMP_Text>();
            foreach (var t in texts)
            {
                if (consumed.Contains(t)) continue;
                var name = t.gameObject.name;
                if (name.EndsWith("Name"))
                {
                    var value = NearestValue(texts, consumed, t,
                        name.Substring(0, name.Length - 4) + "Value");
                    if (value != null)
                    {
                        consumed.Add(value);
                        items.Add(new Item { Tf = t.transform, Text = t, ValueText = value });
                        continue;
                    }
                }
                items.Add(new Item { Tf = t.transform, Text = t });
            }

            items.Sort((a, c) =>
            {
                int byY = c.Tf.position.y.CompareTo(a.Tf.position.y);
                return byY != 0 ? byY : a.Tf.position.x.CompareTo(c.Tf.position.x);
            });
            return items;
        }

        // The value for a "*Name" label is the like-named "*Value" text physically closest to it
        // (Jerian lays a row's name and value in sibling columns, not always the same parent).
        private static TMPro.TMP_Text NearestValue(List<TMPro.TMP_Text> texts,
            HashSet<TMPro.TMP_Text> consumed, TMPro.TMP_Text name, string valueName)
        {
            TMPro.TMP_Text best = null;
            float bestSqr = float.MaxValue;
            foreach (var t in texts)
            {
                if (t == name || consumed.Contains(t) || t.gameObject.name != valueName) continue;
                float d = (t.transform.position - name.transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            return best;
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

        private static void AddText(GraphBuilder b, TMPro.TMP_Text text, TMPro.TMP_Text value)
        {
            var go = text.gameObject;
            b.AddItem(ControlId.Referenced(text, "window:text:" + text.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(
                        () => value == null ? text.text : (text.text + " " + value.text),
                        kind: AnnouncementKinds.Label),
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

        // The window's name is the game's own name for the HUD button that opened it: every
        // window controller keeps that button (WindowControllersManager hands it over on
        // OnSelect) and the button carries the localization key the HUD speaks. A prefab title
        // row is the fallback for a window opened some other way.
        private static string FindTitle(GameObject w)
        {
            foreach (var comp in w.GetComponents<MonoBehaviour>())
                foreach (var f in comp.GetType().GetFields(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (f.FieldType != typeof(GameObject) || f.Name != "_hudButton") continue;
                    var label = HudReadout.ButtonLabel((GameObject)f.GetValue(comp));
                    if (!string.IsNullOrEmpty(label)) return label;
                }
            var t = w.transform.Find("Title") ?? w.transform.Find("Header/Title");
            if (t == null) return null;
            var tmp = t.GetComponentInChildren<TMPro.TMP_Text>(true);
            return tmp == null ? null : tmp.text;
        }
    }
}
