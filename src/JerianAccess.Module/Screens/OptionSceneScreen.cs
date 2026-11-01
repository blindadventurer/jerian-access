using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.Models.Static;
using _Scripts.AMVCC.Views.Windows.CaseForAYear;
using _Scripts.AMVCC.Views.Windows.TripSelection;
using _Scripts.Managers;
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
            // The consequence's own reward rows are announced from the model (RewardPage) rather
            // than from the sliders and crests the game draws them as, so the sweep reads the
            // page's words and leaves the rows to it.
            var text = _consequence.Poll(PanelText.Read(page, RewardPage.OwnsLabel), out grew);
            if (!string.IsNullOrEmpty(text)) Mod.Speech.Output(text);
            RewardPage.Deliver();
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
            b.PopContext();

            // The projects come in two families the game separates only by which half of the page
            // they are drawn on - the empire's and the character's own - and from chapter 3 both
            // are on screen at once. Each is its own context so the family is announced when focus
            // enters it and the positions count within it.
            AddCases(b, root, CaseForAYearOptionType.Empire, "case.empire");
            AddCases(b, root, CaseForAYearOptionType.Personal, "case.personal");
        }

        private static void AddCases(GraphBuilder b, GameObject root,
            CaseForAYearOptionType type, string labelKey)
        {
            var views = new List<CaseForAYearOptionView>();
            foreach (var view in root.GetComponentsInChildren<CaseForAYearOptionView>(false))
                if (view.CaseForAYearOption.Type == type) views.Add(view);
            if (views.Count == 0) return;
            b.PushContext(Loc.T(labelKey), role: null);
            foreach (var view in views) AddCase(b, view);
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
                        new NodeAnnouncement(
                            () => TripCompleted(option) ? Loc.T("case.completed") : null,
                            kind: AnnouncementKinds.Enabled),
                        new NodeAnnouncement(() => TripEstates(option),
                            kind: AnnouncementKinds.Value),
                    },
                    OnTooltip = () => Tooltip(option.gameObject),
                    OnActivate = () =>
                    {
                        if (TripCompleted(option))
                        {
                            Mod.Speech.Output(Loc.T("case.completed"), interrupt: true);
                            return;
                        }
                        Choose(option);
                    },
                });
        }

        private static readonly FieldInfo TripOptionField = typeof(TripOptionView)
            .GetField("_tripOption", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TripCompletedField = typeof(TripOptionView)
            .GetField("_isCompleted", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool TripCompleted(TripOptionView view)
            => (bool)TripCompletedField.GetValue(view);

        /// <summary>Which two estates a province's trip courts. The game draws them as a pair of
        /// crests on the card - the whole point of the pick, and not a word of it is text.</summary>
        private static string TripEstates(TripOptionView view)
        {
            var trip = (TripOption)TripOptionField.GetValue(view);
            if (trip == null) return null;
            var left = trip.LeftEstate == null ? null : trip.LeftEstate.GetNameLocalised();
            var right = trip.RightEstate == null ? null : trip.RightEstate.GetNameLocalised();
            if (string.IsNullOrEmpty(left)) return right;
            if (string.IsNullOrEmpty(right)) return left;
            return left + ", " + right;
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
                        new NodeAnnouncement(() => CaseLabel(option), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => CaseState(option),
                            kind: AnnouncementKinds.Enabled),
                        new NodeAnnouncement(() => CaseProgress(option),
                            kind: AnnouncementKinds.Value),
                    },
                    OnTooltip = () => Mod.Speech.Output(Details(option)),
                    OnActivate = () =>
                    {
                        var state = CaseState(option);
                        if (state != null)
                        {
                            Mod.Speech.Output(
                                Completed(option) ? state : Details(option), interrupt: true);
                            return;
                        }
                        Choose(option);
                    },
                });
        }

        private static bool Completed(CaseForAYearOptionView view)
            => view.CaseForAYearOption.IsCompleted
            || view.OptionState == CaseForAYearOptionState.Achieved;

        /// <summary>The project's name. A COMPLETED project's card drops its caption (the game
        /// swaps in a seal), so the name comes from the same model the card is built from -
        /// otherwise that card is a stop that says only "button".</summary>
        private static string CaseLabel(CaseForAYearOptionView view)
        {
            var rendered = PanelText.Read(view.gameObject);
            if (!string.IsNullOrEmpty(rendered)) return rendered;
            return I2.Loc.LocalizationManager.GetTranslation(
                view.CaseForAYearOption.GetShortDescriptionTerm() ?? "");
        }

        /// <summary>What the game says about this project with an icon: done, taken for this
        /// year, or not open yet - and for the last, WHY. The game draws the reason as a padlock
        /// whose hover tooltip names the chapter, or as a dimmed card whose requirements live only
        /// in the model. Null when the project is simply available.</summary>
        private static string CaseState(CaseForAYearOptionView view)
        {
            if (Completed(view)) return Loc.T("case.completed");
            if (view.OptionState == CaseForAYearOptionState.Chosen) return Loc.T("state.selected");
            if (view.OptionState != CaseForAYearOptionState.Disabled && view.IsUnlocked) return null;
            var reason = LockReason(view);
            return reason == null
                ? Loc.T("state.unavailable")
                : Loc.T("state.unavailable_reason", new { reason });
        }

        /// <summary>Why a project is closed: the chapter it opens in (the game's own padlock
        /// string, the one CaseForAYearLockView puts in its tooltip), else the requirements its
        /// condition groups are still waiting on. Null when neither names anything.</summary>
        private static string LockReason(CaseForAYearOptionView view)
        {
            var option = view.CaseForAYearOption;
            if (!GameManager.Instance.IsChapterUnlocked(option.UnlockChapter))
            {
                var text = I2.Loc.LocalizationManager.GetTranslation(
                    "HUD.WillOpen" + option.UnlockChapter);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            return ChoiceConditions.FailedRequirementText(view.ConditionGroups, view.gameObject);
        }

        /// <summary>How far the project has come - the game draws it as a row of rhombus steps.</summary>
        private static string CaseProgress(CaseForAYearOptionView view)
        {
            var option = view.CaseForAYearOption;
            if (option.TotalSteps <= 1 || Completed(view)) return null;
            return Loc.T("case.step", new { step = option.CurrentStep, total = option.TotalSteps });
        }

        // Both option views resolve their pick the same way the scene's choices do - through the
        // view's own click, or its hold-resolution when the hold setting leaves the click unwired.
        private static void Choose(MonoBehaviour option)
        {
            if (HoldChoice.Holding) HoldChoice.Resolve(option);
            else UiWidgets.Click(option.gameObject);
        }

        /// <summary>Everything the page holds about a project and does not say out loud: the
        /// game's own long description (the card shows the short one), what taking it would
        /// change, and its requirements with live met markers. A project is a commitment for the
        /// whole year, so picking one blind is the one thing this list must not force.</summary>
        private static string Details(CaseForAYearOptionView view)
        {
            var sb = new StringBuilder();
            var option = view.CaseForAYearOption;
            PanelText.Append(sb, I2.Loc.LocalizationManager.GetTranslation(
                option.GetLongDescriptionTerm() ?? ""));
            var rewards = Rewards(option);
            if (rewards != null) PanelText.Append(sb, Loc.T("scene.rewards") + " " + rewards);
            // A case-for-a-year option carries the same typed condition groups a scene choice does.
            foreach (var group in view.ConditionGroups)
                PanelText.Append(sb, ChoiceConditions.GroupDetails(group, view.gameObject));
            return sb.Length == 0 ? Loc.T("nav.no_details") : sb.ToString();
        }

        /// <summary>What the project's step would grant, from the same reward model a choice's
        /// consequence sheet reads.</summary>
        private static string Rewards(CaseForAYearOption option)
        {
            if (option.SceneConsequence == null) return null;
            var rows = new List<string>();
            foreach (var reward in option.SceneConsequence.Rewards)
            {
                if (!reward.ShownToPlayer) continue;
                var text = ChoiceRewards.Describe(reward);
                if (!string.IsNullOrEmpty(text)) rows.Add(text);
            }
            return rows.Count == 0 ? null : string.Join(", ", rows.ToArray());
        }

        // The option's own hover tooltip, read from the game's tooltip system.
        private static void Tooltip(GameObject go)
        {
            if (!TooltipReader.Request(go)) Mod.Speech.Output(Loc.T("nav.no_details"));
        }
    }
}
