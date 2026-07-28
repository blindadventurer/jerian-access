using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.Popup.Interlude;
using _Scripts.AMVCC.Views.Windows.SceneInterlude;
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

        private static readonly FieldInfo RewardIndexField = typeof(InterludePopupController)
            .GetField("_rewardIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InterludeField = typeof(InterludePopupController)
            .GetField("_interlude", BindingFlags.NonPublic | BindingFlags.Instance);

        private int _spokenReward = -1;

        private readonly SettledDelivery _delivery = new SettledDelivery(0.25f);

        // The popup's title row is chrome: it stays put while the pages turn under it, so it is
        // read with the first page and never again (a nine-page interlude opened every page with
        // its own name otherwise). Rows are matched by the game's own object naming.
        private const string TitleRow = "Title";
        private string _spokenTitle;

        private static string Title(GameObject popup)
        {
            foreach (var tmp in popup.GetComponentsInChildren<TMPro.TMP_Text>(false))
                if (tmp.gameObject.name.Contains(TitleRow) && UiWidgets.Visible(tmp.gameObject))
                    return tmp.text.Trim();
            return null;
        }

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

        public override void OnPop()
        {
            _delivery.Reset();
            _spokenTitle = null;
            _spokenReward = -1;
        }

        public override void OnUpdate()
        {
            var popup = Popup();
            if (popup == null) return;
            if (DeliverReward(popup)) return;
            bool grew;
            var text = _delivery.Poll(PanelText.Read(popup, TitleRow), out grew);
            if (string.IsNullOrEmpty(text)) return;
            var title = Title(popup);
            if (!string.IsNullOrEmpty(title) && title != _spokenTitle)
            {
                _spokenTitle = title;
                text = title + ". " + text;
            }
            Mod.Speech.Speak(text);
        }

        // An interlude ends on its REWARD pages - one reward to a page, drawn as a row whose
        // figure counts from the old value to the new one. Read mid-count that row says
        // "Догма 4 0" and a beat later "Догма 3 0": the animation, not the change. The reward is
        // read from the same model the row is built from instead, the way a choice's sheet is.
        private bool DeliverReward(GameObject popup)
        {
            var controller = popup.GetComponentInChildren<InterludePopupController>(true);
            if (controller == null) return false;
            int index = (int)RewardIndexField.GetValue(controller);
            if (index < 0) return false;
            var rewards = ((Interlude)InterludeField.GetValue(controller)).VisibleRewards;
            // A reward the sheet has no words for (a creed ending is painted as a seal) falls
            // through to the rendered read rather than leaving the page silent.
            var text = index < rewards.Count ? ChoiceRewards.Describe(rewards[index]) : null;
            if (string.IsNullOrEmpty(text)) return false;
            if (index == _spokenReward) return true;
            _spokenReward = index;
            Mod.Speech.Speak(text);
            return true;
        }
    }
}
