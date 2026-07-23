using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.MainMenu;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The Settings scene (loaded ADDITIVELY from the main menu, never a UIManager window
    /// slot). One form stop, rows in the game's visual order (MainPanel/Container children),
    /// each row keyed off SettingsWindowController's serialized control references (private on
    /// Jerian - read via reflection) and captioned by the row's game-localized label: Language
    /// spinner, three toggles (illustration animations, hold-the-choice-button, tips),
    /// Music/Sound/Cutscene volume sliders, Screen mode spinner, Resolution dropdown
    /// (interactable only when the mode allows - the game's rule), VSync toggle, Back button.
    /// Left/Right adjust through the game's own handlers (volume applies immediately).
    /// No Back action here: Jerian's controller binds its OWN InputSystem escapeAction that
    /// closes the window on the physical Escape press - a mod-side back would double-close.
    /// </summary>
    public sealed class SettingsScreen : Screen
    {
        public override string Key => "settings";
        public override int Layer => 10;
        public override bool IsActive() => GameUi.IsSceneLoaded("Settings");

        private static readonly FieldInfo[] Fields = Resolve(
            "Sound", "Music", "Cutscene", "PictureAnimation", "VSync", "HoldingButtonProgress",
            "enableTutorialsToggle", "Resolutions", "LanguageText", "WindowModeText");

        private static FieldInfo[] Resolve(params string[] names)
        {
            var fields = new FieldInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
                fields[i] = typeof(SettingsWindowController).GetField(
                    names[i], BindingFlags.NonPublic | BindingFlags.Instance);
            return fields;
        }

        private static T Field<T>(SettingsWindowController w, int index) where T : Object
            => (T)Fields[index].GetValue(w);

        public override Message ScreenName
        {
            get
            {
                var w = Window();
                if (w == null) return null;
                // The game's own localized window title; null while the additive scene is
                // still activating - the focus announcement then waits for content.
                var title = w.transform.Find("Title");
                return title == null ? null
                    : Message.MaybeRaw(UiWidgets.LocalizedLabel(title.gameObject));
            }
        }

        private static SettingsWindowController Window()
            => Object.FindObjectOfType<SettingsWindowController>();

        public override void Build(GraphBuilder b)
        {
            var w = Window();
            if (w == null) return;
            var sound = Field<Slider>(w, 0);
            var music = Field<Slider>(w, 1);
            var cutscene = Field<Slider>(w, 2);
            var pictureAnimation = Field<Toggle>(w, 3);
            var vsync = Field<Toggle>(w, 4);
            var holding = Field<Toggle>(w, 5);
            var tutorials = Field<Toggle>(w, 6);
            var resolutions = Field<TMPro.TMP_Dropdown>(w, 7);
            var languageText = Field<TMPro.TextMeshProUGUI>(w, 8);
            var windowModeText = Field<TMPro.TextMeshProUGUI>(w, 9);

            // Container = the ancestor the rows are children of; climbed from a known control
            // rather than a serialized path so a prefab reshuffle fails loudly, not silently.
            var container = sound.transform;
            while (container != null && container.name != "Container") container = container.parent;
            if (container == null) { Mod.Warn("settings: no Container ancestor above Sound"); return; }

            for (int i = 0; i < container.childCount; i++)
            {
                var row = container.GetChild(i);
                if (!row.gameObject.activeSelf) continue;
                var caption = row.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (caption == null) continue; // divider

                if (sound.transform.IsChildOf(row)) AddSlider(b, caption, sound, "sound");
                else if (music.transform.IsChildOf(row)) AddSlider(b, caption, music, "music");
                else if (cutscene.transform.IsChildOf(row)) AddSlider(b, caption, cutscene, "cutscene");
                else if (pictureAnimation.transform.IsChildOf(row)) AddToggle(b, caption, pictureAnimation, "animations");
                else if (vsync.transform.IsChildOf(row)) AddToggle(b, caption, vsync, "vsync");
                else if (holding.transform.IsChildOf(row)) AddToggle(b, caption, holding, "holding");
                else if (tutorials.transform.IsChildOf(row)) AddToggle(b, caption, tutorials, "tutorials");
                else if (resolutions.transform.IsChildOf(row)) AddDropdown(b, caption, resolutions);
                else if (languageText.transform.IsChildOf(row)) AddSpinner(b, caption, row, languageText, "language");
                else if (windowModeText.transform.IsChildOf(row)) AddSpinner(b, caption, row, windowModeText, "windowmode");
            }

            var back = w.transform.Find("BackButton");
            if (back == null) { Mod.Warn("settings: no BackButton under controller"); return; }
            b.AddItem(ControlId.Referenced(back.GetComponent<Button>(), "settings:back"), new NodeVtable
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

        // A volume slider (0..1): value spoken as percent, Left/Right steps through the
        // slider's own onValueChanged so the game applies volume and plays feedback live.
        private static void AddSlider(GraphBuilder b, TMPro.TMP_Text caption, Slider slider, string id)
        {
            b.AddItem(ControlId.Referenced(slider, "settings:" + id), new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(caption.gameObject),
                        kind: AnnouncementKinds.Label),
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

        private static string Percent(Slider slider)
        {
            int pct = Mathf.RoundToInt((slider.value - slider.minValue) / (slider.maxValue - slider.minValue) * 100f);
            return Loc.T("settings.percent", new { value = pct });
        }

        private static void AddToggle(GraphBuilder b, TMPro.TMP_Text caption, Toggle toggle, string id)
        {
            b.AddItem(ControlId.Referenced(toggle, "settings:" + id), new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(caption.gameObject),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T(toggle.isOn ? "state.on" : "state.off"),
                        live: true, kind: AnnouncementKinds.Value),
                },
                OnActivate = () => UiWidgets.Click(toggle.gameObject),
                StateText = () => Loc.T(toggle.isOn ? "state.on" : "state.off"),
            });
        }

        // A left/right cycler row (Language, Screen mode): the value is the game's own text
        // between the arrow buttons; Left/Right run the game's arrow-button handlers (which
        // set the language / screen mode and persist it). Arrow names confirmed live: LeftAr,
        // RightAr under the row's Parameter node.
        private static void AddSpinner(GraphBuilder b, TMPro.TMP_Text caption, Transform row,
            TMPro.TextMeshProUGUI valueText, string id)
        {
            var left = FindButton(row, "LeftAr");
            var right = FindButton(row, "RightAr");
            b.AddItem(ControlId.Referenced(valueText, "settings:" + id), new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(caption.gameObject),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => valueText.text, live: true, kind: AnnouncementKinds.Value),
                },
                OnAdjust = (sign, large) => UiWidgets.Click((sign > 0 ? right : left).gameObject),
                StateText = () => valueText.text,
            });
        }

        private static Button FindButton(Transform row, string name)
        {
            foreach (var btn in row.GetComponentsInChildren<Button>(true))
                if (btn.gameObject.name == name) return btn;
            throw new System.InvalidOperationException(
                "settings spinner row " + row.name + " has no button named " + name);
        }

        // The resolution dropdown: value from the model (the selected option), adjustable only
        // when the screen mode allows - the game's own rule, announced with its reason since
        // the game only dims the control.
        private static void AddDropdown(GraphBuilder b, TMPro.TMP_Text caption, TMPro.TMP_Dropdown dropdown)
        {
            b.AddItem(ControlId.Referenced(dropdown, "settings:resolution"), new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(caption.gameObject),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => dropdown.options[dropdown.value].text,
                        live: true, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(
                        () => dropdown.interactable ? null : Loc.T("settings.resolution_unavailable"),
                        live: true, kind: AnnouncementKinds.Enabled),
                },
                OnAdjust = (sign, large) =>
                {
                    if (!dropdown.interactable)
                    {
                        Mod.Speech.Speak(Loc.T("settings.resolution_unavailable"), interrupt: true);
                        return;
                    }
                    dropdown.value = Mathf.Clamp(dropdown.value + sign, 0, dropdown.options.Count - 1);
                },
                StateText = () => dropdown.options[dropdown.value].text,
            });
        }
    }
}
