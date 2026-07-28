using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.Helpers;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The chapter-picture scene (SceneType.ChapterPicture, ChapterPictureController): a
    /// full-screen illustration under two title lines - the chapter's name on the left, its
    /// subtitle on the right - and nothing else. The whole screen is the dismiss control (the
    /// game clicks the picture, or takes its InputSystem submit), which is unusual enough to earn
    /// the hint. Enter runs the controller's own click handler, the entry its submit action uses,
    /// so the hide animation, sfx and the Bolt flow advance exactly as a mouse click would.
    /// </summary>
    public sealed class ChapterPictureScreen : Screen
    {
        public override string Key => "chapterpicture";
        public override int Layer => 0;

        private static ChapterPictureController Controller()
        {
            var c = Object.FindObjectOfType<ChapterPictureController>();
            return c != null && UiWidgets.Visible(c.gameObject) ? c : null;
        }

        public override bool IsActive() => Controller() != null;

        public override Message ScreenName => null; // the title row below is the announcement

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;
            b.AddItem(ControlId.Referenced(c, "chapterpicture:title"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => PanelText.Read(c.gameObject),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => Loc.T("chapterpicture.hint")),
                },
                OnActivate = () => c.OnPointerClick(),
            });
        }
    }
}
