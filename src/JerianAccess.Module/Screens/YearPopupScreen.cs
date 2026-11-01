using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using UnityEngine;
using GameManager = _Scripts.Managers.GameManager;
using YearPopup = _Scripts.AMVCC.Views.Windows.Popup.YearIncrementPopup.YearIncrementPopupController;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The year-increment flash shown between events when the year advances: the year as animated
    /// digit sprites, the chapter it belongs to, and the hero's age on a scale. It carries NO
    /// control - it plays and hides itself - so this screen claims no input and only DELIVERS the
    /// line once. The year is read from the serializer because the digits are sprites with no
    /// text behind them; the chapter and age are the popup's own localized labels.
    /// </summary>
    public sealed class YearPopupScreen : Screen
    {
        public override string Key => "popup:year";
        public override int Layer => 22;

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo ChapterNumberField = typeof(YearPopup)
            .GetField("chapterCountText", NP);
        private static readonly FieldInfo ChapterNameField = typeof(YearPopup)
            .GetField("chapterNameText", NP);
        private static readonly FieldInfo AgeField = typeof(YearPopup)
            .GetField("protagonistAgeText", NP);

        private bool _spoken;

        // Found by TYPE, not through UIManager's popup slot: Jerian's PopupControllersManager
        // instantiates this popup into its own container and never touches that slot, so a
        // slot-gated screen would never wake up.
        private static YearPopup Popup()
        {
            var p = Object.FindObjectOfType<YearPopup>();
            return p != null && UiWidgets.Visible(p.gameObject) ? p : null;
        }

        public override bool IsActive() => Popup() != null;

        public override Message ScreenName => null; // the line below is the announcement

        public override void OnPop() => _spoken = false;

        // The popup fills its labels in SetUp, a beat after it is instantiated: deliver once the
        // line is complete, and only once.
        public override void OnUpdate()
        {
            if (_spoken) return;
            var popup = Popup();
            if (popup == null) return;
            var line = Summary(popup);
            if (string.IsNullOrEmpty(line)) return;
            _spoken = true;
            Mod.Speech.Output(line);
        }

        private static string Summary(YearPopup popup)
        {
            var chapter = Label(ChapterNumberField, popup);
            var name = Label(ChapterNameField, popup);
            var age = Label(AgeField, popup);
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(age)) return null;
            return Loc.T("yearpopup.summary", new
            {
                year = GameManager.Instance.GameManagerSerializer.CurrentYear,
                chapter = string.IsNullOrEmpty(chapter) ? name : chapter + " " + name,
                age,
            });
        }

        // The labels resolve through the game's own localization - a freshly instantiated prefab
        // shows the authoring language until its Start runs.
        private static string Label(FieldInfo field, YearPopup popup)
        {
            var component = (Component)field.GetValue(popup);
            return component == null ? null : UiWidgets.LocalizedLabel(component.gameObject);
        }
    }
}
