using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using UnityEngine;
using _Scripts.Popups;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The story popups the game raises between events - the ones PopupControllersManager
    /// instantiates into its own container rather than the UIManager slot (interlude, creed
    /// outcome, and whatever the game adds next). They share a shape: a panel of game-composed
    /// text that pages itself through a PageNavigationController reading the game's own keys. So
    /// this screen claims NO input and only DELIVERS the panel as each page settles; the player
    /// pages with the same keys a sighted player uses. Popups with a dedicated screen (the year
    /// flash, the day-stage flash) stand this one down by type.
    /// </summary>
    public sealed class ScenePopupScreen : Screen
    {
        public override string Key => "popup:scene";
        public override int Layer => 22;

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        // Popup types whose own screen reads them - never this one.
        private static readonly System.Type[] Covered =
        {
            typeof(_Scripts.AMVCC.Views.Windows.Popup.YearIncrementPopup.YearIncrementPopupController),
            typeof(DayTimePopupController),
        };

        private static readonly FieldInfo ContainerField = typeof(PopupControllersManager)
            .GetField("popupViewsContainer", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly SettledDelivery _delivery = new SettledDelivery(0.25f);

        /// <summary>The live popup in the manager's container, or null. The manager destroys a
        /// popup when it closes, so a live child IS an open popup.</summary>
        private static GameObject Popup()
        {
            var manager = Object.FindObjectOfType<PopupControllersManager>();
            if (manager == null) return null;
            var container = (Transform)ContainerField.GetValue(manager);
            if (container == null) return null;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i).gameObject;
                if (!UiWidgets.Visible(child)) continue;
                bool covered = false;
                foreach (var type in Covered)
                    if (child.GetComponentInChildren(type, true) != null) covered = true;
                if (!covered) return child;
            }
            return null;
        }

        public override bool IsActive() => Popup() != null;

        public override Message ScreenName => null; // the delivered page is the announcement

        public override void OnPop() => _delivery.Reset();

        public override void OnUpdate()
        {
            var popup = Popup();
            if (popup == null) return;
            bool grew;
            var text = _delivery.Poll(PanelText.Read(popup), out grew);
            if (!string.IsNullOrEmpty(text)) Mod.Speech.Speak(text);
        }
    }
}
