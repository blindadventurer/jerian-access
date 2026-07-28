using System.Linq;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using WindowCredits = _Scripts.AMVCC.Views.Windows.Credits.WindowCreditsController;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The credits roll (Window_Credits - the menu's Credits scene and the game's ending both
    /// carry this controller). The visual scroll keeps playing; the credit blocks are exposed as
    /// text rows in top-to-bottom order so they can be read at the player's own pace, labels read
    /// live from the scene's TMP blocks. Skipping stays the game's: the controller listens for
    /// Escape through its own InputSystem actions, which the mod does not suppress.
    /// </summary>
    public sealed class CreditsScreen : Screen
    {
        public override string Key => "credits";
        public override Message ScreenName => Message.Localized("ui", "screen.credits");
        public override int Layer => 10;
        public override bool IsActive() => Controller() != null;

        private static WindowCredits Controller() => Object.FindObjectOfType<WindowCredits>();

        public override void Build(GraphBuilder b)
        {
            var ctrl = Controller();
            if (ctrl == null) return;

            // One row per credit block, ordered top-to-bottom. Sibling order under the Credits
            // container IS the reading order; world y is not usable here because the graph builds
            // on the scene's first frame, before the layout group has positioned the blocks.
            var blocks = ctrl.GetComponentsInChildren<TMPro.TMP_Text>()
                .OrderBy(t => t.transform.parent.GetSiblingIndex());
            foreach (var tmp in blocks)
            {
                var block = tmp;
                b.AddItem(ControlId.Referenced(block, "credits:" + block.transform.parent.name),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => block.text, kind: AnnouncementKinds.Label),
                        },
                    });
            }
        }
    }
}
