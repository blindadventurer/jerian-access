using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The standalone GameOver scene: a "Game over" title over an endless credits scroll
    /// (GameOverWindow moves its Text object upward forever). No code path loads this scene -
    /// the shipping game-over ending is Credits_GameOver via WindowCreditsController - but the
    /// death flow's Bolt graphs are opaque, and if a player ever lands here the scene has no
    /// exit and would otherwise be perfectly silent. Two text rows, read live; the scene
    /// offers no interaction to mirror.
    /// </summary>
    public sealed class GameOverScreen : Screen
    {
        public override string Key => "gameover";
        public override int Layer => 10;

        private static GameOverWindow Window() => Object.FindObjectOfType<GameOverWindow>();

        public override bool IsActive()
        {
            var w = Window();
            return w != null && w.gameObject.activeInHierarchy;
        }

        // No screen-name announcement: the title rides on the scrolling Text object, which is
        // also the first stop - naming it here said "Game over" twice in a row.
        public override Message ScreenName => null;

        public override void Build(GraphBuilder b)
        {
            var w = Window();
            if (w == null) return;
            int i = 0;
            foreach (var t in w.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                var tmp = t;
                b.AddItem(ControlId.Referenced(tmp, "gameover:text:" + i++), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => tmp.text, kind: AnnouncementKinds.Label),
                    },
                });
            }
        }
    }
}
