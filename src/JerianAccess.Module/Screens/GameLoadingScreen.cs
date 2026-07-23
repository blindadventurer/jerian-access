using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.Views.Windows.LoadingWindow;
using _Scripts.Managers;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The chapter-timeline / loading screen (additive "GameLoadingScreen" scene owned by
    /// GameLoadingScreenBehaviour), shown after picking a save: a Continue button that resumes,
    /// up to five chapter items that jump to an unlocked chapter's start (via the game's own
    /// confirm popup), a back-to-menu button, plus the current age and the on-screen hint read
    /// as context. Chapter availability is the game's own model (LoadChapterItemBehavior
    /// interactable / IronMan mode); labels are the game's localized chapter names. Activation
    /// runs the buttons' own click wiring. All read live (CLAUDE.md).
    /// </summary>
    public sealed class GameLoadingScreen : Screen
    {
        public override string Key => "gameloading";
        public override Message ScreenName => Message.Localized("ui", "screen.gameloading");
        public override int Layer => 10;

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo AgeTextField =
            typeof(GameLoadingScreenBehaviour).GetField("currentAgeText", NP);
        private static readonly FieldInfo HelpBoxField =
            typeof(GameLoadingScreenBehaviour).GetField("_restartHelpTextBox", NP);
        private static readonly FieldInfo ContinueButtonField =
            typeof(GameLoadingScreenBehaviour).GetField("_continueButton", NP);
        private static readonly string[] ChapterItemFields =
            { "firstChapterItem", "secondChapterItem", "thirdChapterItem",
              "fourthChapterItem", "fifthChapterItem" };

        private static readonly FieldInfo LciInteractable =
            typeof(LoadChapterItemBehavior).GetField("interactable", NP);
        private static readonly FieldInfo LciButton =
            typeof(LoadChapterItemBehavior).GetField("button", NP);
        private static readonly FieldInfo LciName =
            typeof(LoadChapterItemBehavior).GetField("chapterName", NP);

        public override bool IsActive() => Behaviour() != null;

        private static GameLoadingScreenBehaviour Behaviour()
            => Object.FindObjectOfType<GameLoadingScreenBehaviour>();

        public override void Build(GraphBuilder b)
        {
            var glb = Behaviour();
            if (glb == null) return;

            // Continue - the primary resume action; focus homes here.
            var continueGo = (GameObject)ContinueButtonField.GetValue(glb);
            var continueBtn = continueGo == null ? null : continueGo.GetComponentInChildren<Button>(true);
            if (continueBtn != null && continueGo.activeInHierarchy)
            {
                var id = ControlId.Referenced(continueBtn, "gameloading:continue");
                b.AddItem(id, Button(continueBtn));
                b.SetStart(id);
            }

            // Chapter items in chapter order; only those the game is showing.
            foreach (var field in ChapterItemFields)
            {
                var item = (LoadChapterItemBehavior)typeof(GameLoadingScreenBehaviour)
                    .GetField(field, NP).GetValue(glb);
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                if (!UiWidgets.Visible(item.gameObject)) continue;
                AddChapter(b, item);
            }

            // Back to the main menu (button object matched by name in the scene).
            var back = FindBackButton(glb);
            if (back != null)
                b.AddItem(ControlId.Referenced(back, "gameloading:back"), Button(back));

            // Context, re-readable at the end: current age, then the on-screen hint.
            var ageText = (TMPro.TextMeshProUGUI)AgeTextField.GetValue(glb);
            if (ageText != null && !string.IsNullOrEmpty(ageText.text))
                b.AddItem(ControlId.Referenced(ageText, "gameloading:age"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Loc.T("gameloading.age", new { age = ageText.text }),
                            kind: AnnouncementKinds.Label),
                    },
                });

            var help = (GameObject)HelpBoxField.GetValue(glb);
            if (help != null && help.activeInHierarchy)
            {
                var t = UiWidgets.LabelText(help);
                if (!string.IsNullOrEmpty(t))
                    b.AddItem(ControlId.Referenced(help, "gameloading:hint"), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => UiWidgets.LabelText(help),
                                kind: AnnouncementKinds.Label),
                        },
                    });
            }
        }

        private static void AddChapter(GraphBuilder b, LoadChapterItemBehavior item)
        {
            var nameText = (TMPro.TextMeshProUGUI)LciName.GetValue(item);
            var button = (Button)LciButton.GetValue(item);
            b.AddItem(ControlId.Referenced(item, "gameloading:chapter:" + item.gameObject.name),
                new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => nameText == null ? null : nameText.text,
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Reason(item), kind: AnnouncementKinds.Enabled),
                    },
                    SearchText = () => nameText == null ? null : nameText.text,
                    OnActivate = () =>
                    {
                        var reason = Reason(item);
                        if (reason != null) { Mod.Speech.Speak(reason, interrupt: true); return; }
                        if (button != null) UiWidgets.Click(button.gameObject);
                    },
                });
        }

        // Null when the chapter can be jumped to; else the game's own rule as a spoken reason.
        private static string Reason(LoadChapterItemBehavior item)
        {
            if (GameManager.Instance.IronManMode) return Loc.T("chapterselect.ironman");
            return (bool)LciInteractable.GetValue(item) ? null : Loc.T("chapterselect.locked");
        }

        private static NodeVtable Button(Button button)
            => new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(button.gameObject),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () => UiWidgets.Click(button.gameObject),
            };

        private static Button FindBackButton(GameLoadingScreenBehaviour glb)
        {
            foreach (var btn in glb.GetComponentsInChildren<Button>(true))
            {
                var n = btn.gameObject.name;
                if (n.Contains("BackToMainMenu") || n.Contains("MainMenu"))
                    return btn;
            }
            return null;
        }
    }
}
