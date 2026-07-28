using System.Collections.Generic;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.CaseForAYear;
using _Scripts.AMVCC.Views.Windows.TripSelection;
using _Scripts.PageScene;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The two option-picking scene types - "Дело на год" (CaseForAYear) and the trip selection -
    /// which are whole scenes rather than story pages: a left page listing the options the
    /// chapter offers and a right page showing what they would do to the estates. Both build
    /// their options from prefabs, both gate the pick behind the hold-the-choice setting, and
    /// neither is a BasicSceneController, so nothing else in the mod sees them. Each option is a
    /// node - the game's own label, its completed/locked state, Space for the tooltip the game
    /// shows on hover - and the rest of the page follows as text rows in reading order.
    /// </summary>
    public sealed class OptionSceneScreen : Screen
    {
        public override string Key => "scene:options";
        public override int Layer => 0;

        public override bool IsActive() => Root() != null;

        public override Message ScreenName => Message.MaybeRaw(Title());

        private static readonly InputCategory[] None = new InputCategory[0];

        /// <summary>Once the pick is made the scene turns into its CONSEQUENCE - a story page the
        /// game pages with its own keys - so the screen claims nothing there, the way every
        /// delivery-only surface does, and the game's pager stays live. Claiming input there left
        /// the page unturnable: the mod had no node to page it with and the game's own keys were
        /// suppressed for as long as the mod claimed UI.</summary>
        public override IReadOnlyList<InputCategory> InputCategories
            => Consequence() != null ? None : base.InputCategories;

        private readonly SettledDelivery _consequence = new SettledDelivery(0.25f);

        /// <summary>The consequence page this scene turned into, or null while the options are up.</summary>
        private static GameObject Consequence()
        {
            var root = Root();
            if (root == null) return null;
            var page = root.GetComponentInChildren<PageConsequenceController>(false);
            return page != null && UiWidgets.Visible(page.gameObject) ? page.gameObject : null;
        }

        public override void OnPop() => _consequence.Reset();

        public override void OnUpdate()
        {
            var page = Consequence();
            if (page == null) return;
            bool grew;
            var text = _consequence.Poll(PanelText.Read(page), out grew);
            if (!string.IsNullOrEmpty(text)) Mod.Speech.Speak(text);
        }

        // The scene root: whichever of the two controllers is loaded.
        private static GameObject Root()
        {
            var trip = Object.FindObjectOfType<TripSelectionWindowController>();
            if (trip != null && UiWidgets.Visible(trip.gameObject)) return trip.gameObject;
            var year = Object.FindObjectOfType<CaseForAYearWindowController>();
            return year != null && UiWidgets.Visible(year.gameObject) ? year.gameObject : null;
        }

        // The page's own heading: the game titles both scenes with a text row at the top, which
        // the reading-order sweep returns first.
        private static string Title()
        {
            var root = Root();
            if (root == null) return null;
            var text = PanelText.Read(root, InsideOption);
            int stop = text.IndexOf(". ");
            return stop <= 0 ? text : text.Substring(0, stop);
        }

        /// <summary>This label belongs to an option card, which is a stop of its own.</summary>
        private static bool InsideOption(GameObject go)
            => go.GetComponentInParent<CaseForAYearOptionView>() != null
            || go.GetComponentInParent<TripOptionView>() != null;

        public override void Build(GraphBuilder b)
        {
            var root = Root();
            if (root == null || Consequence() != null) return; // the game owns the consequence page

            // The page's own words first (the section headings, the year, what the picks would do
            // to the empire), then the options - the transcript shape: context, then the choices.
            // The option cards' own words are excluded: each card is its own stop, and reading
            // them here made the context stop a recital of every option on the page.
            b.AddItem(ControlId.Structural("options:page"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => PanelText.Read(root, InsideOption),
                        kind: AnnouncementKinds.Label),
                },
            });

            b.PushContext("", role: null);
            foreach (var view in root.GetComponentsInChildren<TripOptionView>(false))
                AddTrip(b, view);
            foreach (var view in root.GetComponentsInChildren<CaseForAYearOptionView>(false))
                AddCase(b, view);
            b.PopContext();
        }

        private static void AddTrip(GraphBuilder b, TripOptionView view)
        {
            var option = view;
            b.AddItem(ControlId.Referenced(option, "options:trip:" + option.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => PanelText.Read(option.gameObject),
                            kind: AnnouncementKinds.Label),
                    },
                    OnTooltip = () => Tooltip(option.gameObject),
                    OnActivate = () => Choose(option),
                });
        }

        private static void AddCase(GraphBuilder b, CaseForAYearOptionView view)
        {
            var option = view;
            b.AddItem(ControlId.Referenced(option, "options:case:" + option.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => PanelText.Read(option.gameObject),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(
                            () => option.IsUnlocked ? null : Loc.T("state.unavailable"),
                            kind: AnnouncementKinds.Enabled),
                    },
                    OnTooltip = () => Mod.Speech.Speak(Conditions(option)),
                    OnActivate = () =>
                    {
                        if (!option.IsUnlocked)
                        {
                            Mod.Speech.Speak(Conditions(option), interrupt: true);
                            return;
                        }
                        Choose(option);
                    },
                });
        }

        // Both option views resolve their pick the same way the scene's choices do - through the
        // view's own click, or its hold-resolution when the hold setting leaves the click unwired.
        private static void Choose(MonoBehaviour option)
        {
            if (HoldChoice.Holding) HoldChoice.Resolve(option);
            else UiWidgets.Click(option.gameObject);
        }

        // A case-for-a-year option carries the same typed condition groups a scene choice does.
        private static string Conditions(CaseForAYearOptionView option)
        {
            var rows = new List<string>();
            foreach (var group in option.ConditionGroups)
            {
                var text = ChoiceConditions.GroupDetails(group, option.gameObject);
                if (text != null) rows.Add(text);
            }
            return rows.Count == 0 ? Loc.T("nav.no_details") : string.Join(". ", rows.ToArray());
        }

        // The option's own hover tooltip, read from the game's tooltip system.
        private static void Tooltip(GameObject go)
        {
            if (!TooltipReader.Request(go)) Mod.Speech.Speak(Loc.T("nav.no_details"));
        }
    }
}
