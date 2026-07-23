using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.Disclaimer;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The disclaimer scene shown on the way into a new game: DisclaimerController pages
    /// through its localized texts with fades. One text node; each newly shown page is spoken
    /// off the label's own text change (model state - fades don't matter, the text swaps once
    /// per page). Enter advances through the controller's own ClickOnScreen, which honors the
    /// grace period and the fade-skip logic; the last page hands off to the Bolt flow.
    /// </summary>
    public sealed class DisclaimerScreen : Screen
    {
        public override string Key => "disclaimer";
        public override Message ScreenName => Message.Localized("ui", "screen.disclaimer");
        public override int Layer => 0;

        private string _spokenText;

        private static readonly System.Reflection.FieldInfo DisabledField =
            typeof(DisclaimerController).GetField("_isDisabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // The Disclaimer scene stays LOADED while the cutscene that follows plays (seen live);
        // _isDisabled flips when the controller hands off - disabled = no longer the surface.
        public override bool IsActive()
        {
            var c = Controller();
            return c != null && !(bool)DisabledField.GetValue(c);
        }

        private static DisclaimerController Controller()
            => Object.FindObjectOfType<DisclaimerController>();

        public override void OnPop()
        {
            _spokenText = null;
        }

        // A page swap is new content: spoken once per text change. The screen-entry seat
        // announcement reads the first page as the node label - prime the differ on entry so
        // delivery does not repeat it.
        public override void OnUpdate()
        {
            var c = Controller();
            if (c == null) return;
            var text = CurrentText(c);
            if (string.IsNullOrEmpty(text)) return;
            if (_spokenText == null) { _spokenText = text; return; }
            if (text == _spokenText) return;
            _spokenText = text;
            Mod.Speech.Speak(text);
        }

        private static string CurrentText(DisclaimerController c)
        {
            var tmp = c.GetComponentInChildren<TMPro.TMP_Text>(true);
            return tmp == null ? null : tmp.text;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;
            b.AddItem(ControlId.Referenced(c, "disclaimer:text"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => CurrentText(Controller()),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () =>
                {
                    var live = Controller();
                    if (live != null) live.ClickOnScreen();
                },
            });
        }
    }
}
