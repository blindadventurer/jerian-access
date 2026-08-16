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
    ///
    /// An interlude is the letters, newspaper cuttings and pamphlets of the game, and it is a
    /// DOCUMENT: it opens (when the event was triggered by what the player did) on a page of
    /// conditions, runs several text pages, and ends on one reward page per change. So every page
    /// announces where it stands in the document and the last one says so - without that the
    /// player cannot tell a page they have read from one they have not, and paging back reads as
    /// new text.
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

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo ContainerField = typeof(PopupControllersManager)
            .GetField("popupViewsContainer", NP);

        private static readonly FieldInfo RewardIndexField =
            typeof(InterludePopupController).GetField("_rewardIndex", NP);
        private static readonly FieldInfo InterludeField =
            typeof(InterludePopupController).GetField("_interlude", NP);
        private static readonly FieldInfo PageIndexField =
            typeof(InterludePopupController).GetField("_pageIndex", NP);
        private static readonly FieldInfo RewardsPanelField =
            typeof(InterludePopupController).GetField("rewardsPanel", NP);
        private static readonly FieldInfo ConditionsPageField =
            typeof(InterludePopupController).GetField("_isTriggerConditionsPageActive", NP);
        private static readonly FieldInfo ConditionsPanelField =
            typeof(InterludePopupController).GetField("InterludeConditionsPanel", NP);
        private static readonly FieldInfo ConditionsContainerField =
            typeof(InterludeConditionsPanel).GetField("triggerConditionsContainer", NP);

        private int _spokenReward = -1;
        private bool _spokenConditions;

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
            _spokenConditions = false;
        }

        public override void OnUpdate()
        {
            var popup = Popup();
            if (popup == null) return;
            var interlude = popup.GetComponentInChildren<InterludePopupController>(true);
            if (DeliverConditions(interlude)) return;
            if (DeliverReward(interlude)) return;
            bool grew;
            var text = _delivery.Poll(PanelText.Read(popup, TitleRow), out grew);
            if (string.IsNullOrEmpty(text)) return;
            var title = Title(popup);
            if (!string.IsNullOrEmpty(title) && title != _spokenTitle)
            {
                _spokenTitle = title;
                text = title + ". " + text;
            }
            var position = Position(interlude);
            if (position != null) text = position + ". " + text;
            Mod.Speech.Speak(text);
        }

        /// <summary>The page of conditions an interlude opens on when the player's own history is
        /// what summoned it - the game's answer to "why did this happen". It draws the rows as
        /// dimmed or lit, which no text sweep can hear, so they are composed from the interlude's
        /// own condition group with met markers. True while that page owns the popup.</summary>
        private bool DeliverConditions(InterludePopupController controller)
        {
            if (controller == null || !(bool)ConditionsPageField.GetValue(controller))
            {
                _spokenConditions = false;
                return false;
            }
            if (_spokenConditions) return true;
            var interlude = (Interlude)InterludeField.GetValue(controller);
            if (interlude == null) return true;
            var rows = ChoiceConditions.GroupDetails(interlude.ConditionGroup, controller.gameObject);
            // Nothing the player may see: leave the page to the rendered sweep rather than
            // holding the popup silent for as long as it is up.
            if (rows == null) return false;
            _spokenConditions = true;
            var header = ConditionsHeader(controller);
            Mod.Speech.Speak(string.IsNullOrEmpty(header) ? rows : header + ". " + rows);
            return true;
        }

        // The panel builds its own static explanation row from a prefab, first into the conditions
        // container - its label is the game's own word for what this page is.
        private static string ConditionsHeader(InterludePopupController controller)
        {
            var panel = (InterludeConditionsPanel)ConditionsPanelField.GetValue(controller);
            if (panel == null) return null;
            var container = (RectTransform)ConditionsContainerField.GetValue(panel);
            if (container == null || container.childCount == 0) return null;
            return UiWidgets.LocalizedLabel(container.GetChild(0).gameObject);
        }

        // An interlude ends on its REWARD pages - one reward to a page, drawn as a row whose
        // figure counts from the old value to the new one. Read mid-count that row says
        // "Догма 4 0" and a beat later "Догма 3 0": the animation, not the change. The reward is
        // read from the same model the row is built from instead, the way a choice's sheet is,
        // and followed by where the figure now stands.
        private bool DeliverReward(InterludePopupController controller)
        {
            // The reward INDEX is never reset when the player pages back out of the rewards
            // (InterludePopupController.ShowPage leaves it where it was), so the panel's own
            // shown state is the only honest signal that a reward page is up. Keying off the
            // index alone made every text page after a visit to the rewards silent.
            if (controller == null || !RewardsShowing(controller)) { _spokenReward = -1; return false; }
            int index = (int)RewardIndexField.GetValue(controller);
            if (index < 0) return false;
            var rewards = ((Interlude)InterludeField.GetValue(controller)).VisibleRewards;
            // A reward the sheet has no words for (a creed ending is painted as a seal) falls
            // through to the rendered read rather than leaving the page silent.
            var text = index < rewards.Count
                ? ChoiceRewards.Describe(rewards[index], withCurrent: true) : null;
            if (string.IsNullOrEmpty(text)) return false;
            if (index == _spokenReward) return true;
            _spokenReward = index;
            // The reward pages ARE the end of the document - the game offers Continue on the
            // last of them and nothing else follows.
            if (index == rewards.Count - 1) text += ", " + Loc.T("page.last");
            Mod.Speech.Speak(text);
            return true;
        }

        private static bool RewardsShowing(InterludePopupController controller)
        {
            var panel = (GameObject)RewardsPanelField.GetValue(controller);
            return panel != null && panel.activeInHierarchy;
        }

        /// <summary>Where the text page stands in the document, and whether it is the last one -
        /// the game marks both only by dimming its pager arrows. Null for a popup that is not a
        /// paged interlude.</summary>
        private static string Position(InterludePopupController controller)
        {
            if (controller == null || RewardsShowing(controller)) return null;
            var blocks = controller.TextBlocks;
            if (blocks == null || blocks.Count == 0) return null;
            // The pager walks only the blocks this playthrough unlocked (GetPageIndices), so a
            // count over all of them would promise pages the player will never be shown.
            int page = (int)PageIndexField.GetValue(controller);
            int count = 0, index = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].AreAllConditionsSatisfied(controller.GlobalConditionGroups)) continue;
                count++;
                if (i == page) index = count;
            }
            if (count <= 1 || index == 0) return null;
            var position = Loc.T("page.position", new { index, count });
            // A document that ends on reward pages is not over at its last text page.
            bool ends = index == count
                        && ((Interlude)InterludeField.GetValue(controller)).VisibleRewards.Count == 0;
            return ends ? position + ", " + Loc.T("page.last") : position;
        }
    }
}
