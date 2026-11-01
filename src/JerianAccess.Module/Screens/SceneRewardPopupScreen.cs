using System.Collections.Generic;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.SceneConsequence;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The reward popup a scene raises over its own consequence pages
    /// (SceneConsequenceRewardPopupController): the ending a run has just earned, the estates it
    /// moved, the outcome of a war - each a page the popup turns with its own
    /// PageNavigationController. It lives INSIDE the scene, not in the popup manager's container,
    /// so no other screen sees it: the scene went quiet with an unread page on screen, and the
    /// scene's own UI claim suppressed the game's pager, leaving nothing that could turn it.
    /// Delivery-only, like every surface the game pages itself: each settled page is spoken and
    /// the player pages with the game's own keys.
    /// </summary>
    public sealed class SceneRewardPopupScreen : Screen
    {
        public override string Key => "popup:reward";
        public override int Layer => 22;

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        public override Message ScreenName => null; // the delivered page is the announcement

        private readonly SettledDelivery _delivery = new SettledDelivery(0.25f);

        private static GameObject Popup()
        {
            var p = Object.FindObjectOfType<SceneConsequenceRewardPopupController>();
            return p != null && UiWidgets.Visible(p.gameObject) ? p.gameObject : null;
        }

        public override bool IsActive() => Popup() != null;

        public override void OnPop() => _delivery.Reset();

        public override void OnUpdate()
        {
            var popup = Popup();
            if (popup == null) return;
            bool grew;
            var text = _delivery.Poll(PanelText.Read(popup), out grew);
            if (!string.IsNullOrEmpty(text)) Mod.Speech.Output(text);
        }
    }
}
