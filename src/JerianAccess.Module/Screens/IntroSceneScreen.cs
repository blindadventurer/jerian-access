using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.Views.Windows.Intro;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The intro spread a new game opens on (IntroSceneController): two facing pages of setup
    /// text and a button on to the story. It is not a story scene - no controller the scene
    /// reader knows - so nothing spoke it, and it is the FIRST thing a new player meets. Both
    /// pages read as one stop in reading order; Enter runs the game's own next-button click, so
    /// the fade-out and the Bolt handoff happen exactly as they would for a mouse.
    /// </summary>
    public sealed class IntroSceneScreen : Screen
    {
        public override string Key => "intro";
        public override int Layer => 0;

        private static IntroSceneController Controller()
        {
            var c = Object.FindObjectOfType<IntroSceneController>();
            return c != null && UiWidgets.Visible(c.gameObject) ? c : null;
        }

        public override bool IsActive() => Controller() != null;

        public override Message ScreenName => null; // the pages below are the announcement

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;

            b.PushContext("", role: null, positions: false);
            b.AddItem(ControlId.Referenced(c, "intro:pages"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => PanelText.Read(c.gameObject),
                        kind: AnnouncementKinds.Label),
                },
            });
            b.PopContext();

            // The way on. The controller disables the button as it starts the fade, so the
            // game's own interactable state is the gate.
            var next = NextButton(c);
            if (next != null)
                b.AddItem(ControlId.Referenced(next, "intro:next"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(next.gameObject),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(
                            () => UiWidgets.Interactable(next.gameObject)
                                ? null : Loc.T("state.unavailable"),
                            kind: AnnouncementKinds.Enabled),
                    },
                    OnActivate = () => UiWidgets.Click(next.gameObject),
                });
        }

        private static Button NextButton(IntroSceneController c)
        {
            foreach (var button in c.GetComponentsInChildren<Button>(false))
                if (UiWidgets.Visible(button.gameObject)) return button;
            return null;
        }
    }
}
