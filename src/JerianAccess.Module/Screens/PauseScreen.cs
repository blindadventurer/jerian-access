using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using WindowPause = _Scripts.AMVCC.Views.Windows.WindowPause;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The in-game pause menu (WindowPause, toggled by the game's Escape read - our Back action
    /// runs the same UIManager.ShowPauseMenu). All of WindowPause's control references are
    /// private on Jerian, so they are read by reflection: Music/Sound/Cutscene volume sliders
    /// (immediate volume), the hidden-consequences / illustration-animation / hold-the-choice /
    /// tutorials toggles, then the remaining visible buttons (exit to menu, resume). Each row's
    /// caption is the game's own localized label beside the control. Not a WindowControllersManager
    /// window, so the generic window reader does not see it - this screen owns it.
    /// </summary>
    public sealed class PauseScreen : Screen
    {
        public override string Key => "pause";
        public override int Layer => 24;

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo[] SliderFields =
            { F("Music"), F("Sound"), F("Cutscene") };
        private static readonly FieldInfo[] ToggleFields =
            { F("HiddenConsequencesModeToggle"), F("PictureAnimation"),
              F("HoldingButtonProgress"), F("enableTutorialsToggle") };
        private static readonly FieldInfo ExitButtonField = F("exitButton");

        private static FieldInfo F(string name) => typeof(WindowPause).GetField(name, NP);

        private static WindowPause Window()
        {
            var w = Object.FindObjectOfType<WindowPause>();
            return w != null && w.gameObject.activeInHierarchy && UiWidgets.Visible(w.gameObject)
                ? w : null;
        }

        public override bool IsActive() => Window() != null;

        public override Message ScreenName => Message.Localized("ui", "screen.pause");

        // No mod Back action: the game's own Escape read (UIManager.Update) closes the pause
        // menu. A mod-side ShowPauseMenu on the same Escape would re-toggle it open.

        public override void Build(GraphBuilder b)
        {
            var w = Window();
            if (w == null) return;
            var exitButton = (Button)ExitButtonField.GetValue(w);

            foreach (var f in SliderFields)
            {
                var slider = (Slider)f.GetValue(w);
                if (slider != null && UiWidgets.Visible(slider.gameObject)) AddSlider(b, slider);
            }
            foreach (var f in ToggleFields)
            {
                var toggle = (Toggle)f.GetValue(w);
                if (toggle != null && UiWidgets.Visible(toggle.gameObject)) AddToggle(b, toggle);
            }

            // The remaining buttons (exit to menu, resume/close) - read by their own game labels.
            foreach (var btn in w.GetComponentsInChildren<Button>(false))
            {
                if (!UiWidgets.Visible(btn.gameObject)) continue;
                // The arrow buttons of an (inactive) language cycler carry no text.
                if (btn.gameObject.name == "LeftAr" || btn.gameObject.name == "RightAr") continue;
                var go = btn.gameObject;
                // The full-screen click-away backdrop ("Bg") is image-only; dismissal stays on
                // the labeled Resume, so the unlabeled backdrop would only add a bare stop.
                if (btn != exitButton && string.IsNullOrEmpty(UiWidgets.LocalizedLabel(go))) continue;
                bool isExit = btn == exitButton;
                b.AddItem(ControlId.Referenced(btn, "pause:btn:" + btn.GetInstanceID()), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(
                            () => !isExit || UiWidgets.Interactable(go) ? null : Loc.T("state.unavailable"),
                            kind: AnnouncementKinds.Enabled),
                    },
                    OnActivate = () =>
                    {
                        if (isExit && !UiWidgets.Interactable(go))
                        {
                            Mod.Speech.Output(Loc.T("state.unavailable"), interrupt: true);
                            return;
                        }
                        UiWidgets.Click(go);
                    },
                });
            }
        }

        private static void AddSlider(GraphBuilder b, Slider slider)
        {
            b.AddItem(ControlId.Referenced(slider, "pause:slider:" + slider.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Slider,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Caption(slider), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Percent(slider), live: true, kind: AnnouncementKinds.Value),
                    },
                    OnAdjust = (sign, large) =>
                    {
                        float step = (slider.maxValue - slider.minValue) * (large ? 0.25f : 0.1f);
                        slider.value = Mathf.Clamp(slider.value + sign * step, slider.minValue, slider.maxValue);
                    },
                    StateText = () => Percent(slider),
                });
        }

        private static void AddToggle(GraphBuilder b, Toggle toggle)
        {
            b.AddItem(ControlId.Referenced(toggle, "pause:toggle:" + toggle.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Toggle,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Caption(toggle), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Loc.T(toggle.isOn ? "state.on" : "state.off"),
                            live: true, kind: AnnouncementKinds.Value),
                    },
                    OnActivate = () => UiWidgets.Click(toggle.gameObject),
                    StateText = () => Loc.T(toggle.isOn ? "state.on" : "state.off"),
                });
        }

        private static string Percent(Slider slider)
        {
            int pct = Mathf.RoundToInt((slider.value - slider.minValue)
                / (slider.maxValue - slider.minValue) * 100f);
            return Loc.T("settings.percent", new { value = pct });
        }

        // A control's row caption: the nearest label text under the control's row (its parent,
        // then grandparent) that is not part of the control itself. Climbs at most three levels
        // so it never wanders into a neighbouring row.
        private static string Caption(Component ctrl)
        {
            var row = ctrl.transform.parent;
            for (int depth = 0; depth < 3 && row != null; depth++, row = row.parent)
            {
                foreach (var t in row.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (t.transform.IsChildOf(ctrl.transform)) continue;
                    var s = UiWidgets.LocalizedLabel(t.gameObject);
                    if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0) return s;
                }
            }
            return null;
        }
    }
}
