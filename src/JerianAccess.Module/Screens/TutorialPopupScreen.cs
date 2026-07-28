using System.Collections.Generic;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using UnityEngine;
using _Scripts.Tutorials;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The tutorial popup the game raises over a scene (TutorialPopupController: a title, an
    /// optional description above and below, and a step illustration). While one is open the game
    /// LOCKS scene navigation (OnTutorialOpened), so an unread tutorial is not just silence - it
    /// is a scene that has stopped answering. The popup pages itself through its own
    /// PageNavigationController, which reads the game's own arrow / continue keys, so this screen
    /// claims NO input and only DELIVERS: each settled step is spoken once off the rendered panel.
    /// The player pages with the same keys a sighted player uses.
    /// </summary>
    public sealed class TutorialPopupScreen : Screen
    {
        public override string Key => "tutorial";
        public override int Layer => 26;
        public override Message ScreenName => Message.Localized("ui", "screen.tutorial");

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        // A step's content prefab is instantiated a frame after the step's texts swap, so the
        // panel is read once it holds still.
        private readonly SettledDelivery _delivery = new SettledDelivery(0.2f);

        private static TutorialPopupController Popup()
        {
            var p = Object.FindObjectOfType<TutorialPopupController>();
            return p != null && UiWidgets.Visible(p.gameObject) ? p : null;
        }

        public override bool IsActive() => Popup() != null;

        public override void OnPop() => _delivery.Reset();

        public override void OnUpdate()
        {
            var popup = Popup();
            if (popup == null) return;
            bool grew;
            var text = _delivery.Poll(PanelText.Read(popup.gameObject), out grew);
            if (!string.IsNullOrEmpty(text)) Mod.Speech.Speak(text);
        }
    }
}
