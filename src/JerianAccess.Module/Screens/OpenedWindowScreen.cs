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

        // The window's CONTENT is what makes it a screen: the game instantiates the prefab and
        // then animates its page in, so a screen that opened on the bare prefab would announce
        // the window before it says anything. Once open it STAYS open until the game destroys
        // the window - the page-in animation dips the CanvasGroup to zero on the way, and a
        // screen that dropped out there would push again and announce the window twice (heard
        // live). Live reference for that bookkeeping only, never content.
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

        // Content is not only words: the Empire map is a picture with fourteen hover zones and no
        // text at all, and a text-only test left that whole window unread and unnavigable.
        private static bool HasContent(GameObject w)
        {
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                if (UiWidgets.Visible(t.gameObject)) return true;
            }
            foreach (var zone in w.GetComponentsInChildren<_Scripts.Tooltips.TooltipZoneView>(false))
                if (zone.enabled && UiWidgets.Visible(zone.gameObject)
                    && !string.IsNullOrEmpty(ContainerLabel.Of(zone.gameObject))) return true;
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
                if (it.Control is Button btn) AddButton(b, btn, it.Selected);
                else if (it.Control is Toggle tg) AddToggle(b, tg);
                else if (it.Zone != null) AddPicture(b, it.Zone, it.Label);
                else if (it.Text != null) AddText(b, it.Text, it.ValueText, it.Row);
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
            public TMPro.TMP_Text ValueText;      // folded value for a "*Name" + "*Value" pair
            public List<TMPro.TMP_Text> Row;      // labels sharing a parent, read as one stop
            public GameObject Zone;               // a picture item, named from its containers
            public string Label;
            public bool Selected;                 // the one item of a list the window is showing
        }

        /// <summary>The one item in a list that the window is CURRENTLY showing. The game marks
        /// that by switching its button off (RelationCharacterController.SelectCharacter), so a
        /// list where exactly one twin is off and the others are live is a selection, not a lock -
        /// and calling it unavailable would be a lie about the item the player is reading.</summary>
        private static bool IsTheSelectedOne(Selectable control)
        {
            if (control.IsInteractable() || control.transform.parent == null) return false;
            int twins = 0, off = 0;
            foreach (Transform sibling in control.transform.parent)
            {
                if (sibling.gameObject.name != control.gameObject.name) continue;
                var twin = sibling.GetComponent<Selectable>();
                if (twin == null || !UiWidgets.Visible(twin.gameObject)) continue;
                twins++;
                if (!twin.IsInteractable()) off++;
            }
            return twins > 1 && off == 1;
        }

        /// <summary>The figure a scale draws at its own end (every personality axis ends in "20"),
        /// which is the ruler, not a reading: the slider owns it, the row states its own value
        /// beside its name, and as a stop it says a number and nothing else. What the scale means
        /// is what Space reads on the row.</summary>
        private static bool IsScaleEndMarker(TMPro.TMP_Text t)
            => IsNumber(t.text) && t.GetComponentInParent<CustomSlider>() != null;

        private static bool IsNumber(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            foreach (var c in s)
                if (!char.IsDigit(c) && c != '+' && c != '-') return false;
            return true;
        }

        private static bool HasText(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                var s = t.text;
                if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0
                    && UiWidgets.Visible(t.gameObject)) return true;
            }
            return false;
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
                items.Add(new Item
                {
                    Tf = sel.transform,
                    Control = sel,
                    Selected = IsTheSelectedOne(sel),
                });
            }

            var texts = new List<TMPro.TMP_Text>();
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!UiWidgets.Visible(t.gameObject)) continue;
                // A label belonging to a real CONTROL is spoken by the control's node. Only
                // buttons and toggles count: Jerian's hover zones are Selectables too, and
                // treating those as controls dropped every label they cover - which is most of
                // the content in the destiny and creed windows.
                var owner = t.GetComponentInParent<Selectable>();
                if (owner is Button || owner is Toggle) continue;
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                if (IsScaleEndMarker(t)) continue;
                texts.Add(t);
            }

            // Pair every "*Name" label with its "*Value" FIRST, then emit: a value that appears
            // ahead of its name in the sweep would otherwise be emitted as a bare stop of its own
            // ("6") and then again folded onto the name ("Разум 6").
            var consumed = new HashSet<TMPro.TMP_Text>();
            var pairs = new Dictionary<TMPro.TMP_Text, TMPro.TMP_Text>();
            foreach (var t in texts)
            {
                var name = t.gameObject.name;
                if (!name.EndsWith("Name")) continue;
                var value = NearestValue(texts, consumed, t,
                    name.Substring(0, name.Length - 4) + "Value");
                if (value == null) continue;
                consumed.Add(value);
                pairs[t] = value;
            }
            // Labels that share a parent are one thing the game drew in pieces (a relation row's
            // word and its number): they read as ONE stop, in reading order, so the number is
            // never a stop of its own saying "1".
            foreach (var t in texts)
            {
                if (consumed.Contains(t)) continue;
                TMPro.TMP_Text value;
                if (pairs.TryGetValue(t, out value))
                {
                    items.Add(new Item { Tf = t.transform, Text = t, ValueText = value });
                    continue;
                }
                var row = new List<TMPro.TMP_Text>();
                foreach (var sibling in texts)
                    if (sibling.transform.parent == t.transform.parent
                        && !consumed.Contains(sibling)) row.Add(sibling);
                foreach (var member in row) consumed.Add(member);
                // Reading order, except that a bare figure follows the words it belongs to
                // ("Отношения 1"): the game stacks the number above its caption as often as
                // beside it, and a stop that opens with a number states nothing.
                row.Sort((a, c) =>
                {
                    bool numA = IsNumber(a.text), numC = IsNumber(c.text);
                    if (numA != numC) return numA ? 1 : -1;
                    int byY = c.transform.position.y.CompareTo(a.transform.position.y);
                    return byY != 0 ? byY : a.transform.position.x.CompareTo(c.transform.position.x);
                });
                items.Add(new Item { Tf = t.transform, Text = t, Row = row });
            }

            // Picture items: a hover zone with no words of its own (a destiny objective, a
            // chronicle entry, an article seal). They are most of what some windows SHOW, and a
            // text sweep sees nothing of them - their name comes from the data containers the
            // game's own tooltip reads, and Space reads that tooltip in full.
            foreach (var zone in w.GetComponentsInChildren<_Scripts.Tooltips.TooltipZoneView>(false))
            {
                if (!zone.enabled || !UiWidgets.Visible(zone.gameObject)) continue;
                if (HasText(zone.gameObject)) continue;
                var label = ContainerLabel.Of(zone.gameObject);
                if (string.IsNullOrEmpty(label)) continue;
                // The window may already name this item in words next to the picture (the
                // relation slider beside its own row) - then the picture adds nothing.
                bool named = false;
                foreach (var t in texts)
                    if (label.StartsWith(t.text.Trim())) named = true;
                if (named) continue;
                items.Add(new Item { Tf = zone.transform, Zone = zone.gameObject, Label = label });
            }

            items.Sort((a, c) =>
            {
                int byY = c.Tf.position.y.CompareTo(a.Tf.position.y);
                return byY != 0 ? byY : a.Tf.position.x.CompareTo(c.Tf.position.x);
            });
            return items;
        }

        // The value for a "*Name" label is the like-named "*Value" text physically closest to it
        // (Jerian lays a row's name and value in sibling columns, not always the same parent) -
        // but always WITHIN THE SAME ROW. Without that bound the hero's "Name" claimed a "Value"
        // from the far side of the page ("Джериан II Темпест 5"), and the row that value really
        // belonged to lost it ("Рассудок" with no number).
        private static TMPro.TMP_Text NearestValue(List<TMPro.TMP_Text> texts,
            HashSet<TMPro.TMP_Text> consumed, TMPro.TMP_Text name, string valueName)
        {
            TMPro.TMP_Text best = null;
            float bestSqr = float.MaxValue;
            foreach (var t in texts)
            {
                if (t == name || consumed.Contains(t) || t.gameObject.name != valueName) continue;
                if (!SameRow(name.transform, t.transform)) continue;
                float d = (t.transform.position - name.transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            return best;
        }

        // One row: the same parent, or two columns of the same one (a shared grandparent).
        private static bool SameRow(Transform a, Transform b)
            => a.parent == b.parent
            || (a.parent != null && b.parent != null && a.parent.parent == b.parent.parent);

        private static void AddButton(GraphBuilder b, Button btn, bool selected)
        {
            var go = btn.gameObject;
            b.AddItem(ControlId.Referenced(btn, "window:btn:" + btn.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(
                        () => selected ? Loc.T("state.selected")
                            : UiWidgets.Interactable(go) ? null : Loc.T("state.unavailable"),
                        kind: selected ? AnnouncementKinds.Selected : AnnouncementKinds.Enabled),
                },
                OnTooltip = TooltipAction(go),
                OnActivate = () =>
                {
                    if (!UiWidgets.Interactable(go))
                    {
                        Mod.Speech.Speak(
                            Loc.T(selected ? "state.selected" : "state.unavailable"), interrupt: true);
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

        // The stop's words, re-read live: the label, its paired value, or the whole row of
        // labels the game drew under one parent.
        private static string RowText(TMPro.TMP_Text text, TMPro.TMP_Text value,
            List<TMPro.TMP_Text> row)
        {
            if (value != null) return text.text + " " + value.text;
            if (row == null || row.Count <= 1) return Named(text, text.text);
            var sb = new System.Text.StringBuilder();
            bool figuresOnly = true;
            foreach (var member in row)
            {
                var s = member.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                if (!IsNumber(s)) figuresOnly = false;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s.Trim());
            }
            return figuresOnly ? Named(text, sb.ToString()) : sb.ToString();
        }

        // A stop the game drew as a bare FIGURE (an estate's power, a summary counter) states
        // nothing on its own: the game leaves the word to the hover tooltip, and hangs it on the
        // data container that tooltip reads. The word comes from there, the figure stays the
        // rendered one - it is what the player is looking at.
        private static string Named(TMPro.TMP_Text text, string words)
        {
            var name = ContainerLabel.NameOf(text.gameObject);
            if (string.IsNullOrEmpty(name)) return words;
            if (IsNumber(words)) return name + " " + words.Trim();
            // A row that renders its own reading ("Лояльность – 12") still has to say WHOSE. The
            // game composes the container's name as "<item>: <parameter>", so the item is exactly
            // the half the rendered row leaves out.
            int colon = name.IndexOf(':');
            if (colon <= 0) return words;
            var item = name.Substring(0, colon).Trim();
            return words.StartsWith(item) ? words : item + ", " + words;
        }

        private static void AddPicture(GraphBuilder b, GameObject zone, string label)
        {
            b.AddItem(ControlId.Referenced(zone.transform, "window:item:" + zone.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => ContainerLabel.Of(zone) ?? label,
                            kind: AnnouncementKinds.Label),
                    },
                    OnTooltip = TooltipAction(zone),
                });
        }

        private static void AddText(GraphBuilder b, TMPro.TMP_Text text, TMPro.TMP_Text value,
            List<TMPro.TMP_Text> row)
        {
            var go = text.gameObject;
            b.AddItem(ControlId.Referenced(text, "window:text:" + text.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => RowText(text, value, row),
                        kind: AnnouncementKinds.Label),
                },
                OnTooltip = TooltipAction(go),
            });
        }

        // The game shows most detail in a hover tooltip. Jerian routes nearly all of it through
        // TooltipZoneView (a per-type view the game builds on hover), so Space asks the game for
        // that view and reads it; the older key-holder components are the fallback for the few
        // places that still carry one. A node with neither has nothing to say on Space.
        private static System.Action TooltipAction(GameObject go)
        {
            if (go == null) return null;
            if (TooltipReader.Available(go)) return () => TooltipReader.Request(go);
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
