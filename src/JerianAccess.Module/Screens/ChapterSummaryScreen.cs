using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.ChapterSummary;
using _Scripts.AMVCC.Views.Windows.ChapterSummary.ChapterParametersViews;
using _Scripts.AMVCC.Views.Windows.ChapterSummary.ChapterSummaryViews;
using _Scripts.AMVCC.Views.Windows.Destiny;
using _Scripts.Localization;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The chapter book (Window_ChapterSummary): the recap the game opens and closes a chapter
    /// with - left page a summary, right page that page's subject, one page per
    /// ChapterSummaryType the chapter was configured with (objectives, personality, skills,
    /// creed, the empire, the timeline...). The objectives page is the one the player plans a
    /// chapter from: which events can still happen and what each of them is waiting on.
    ///
    /// It is BUILT AS ONE FLAT LIST (the pattern the interlude's conversion book uses): every
    /// page's rows behind a per-page region, so a page is a Ctrl+Up/Ctrl+Down away and its rows
    /// are ordinary Up/Down stops. The game's visible page follows focus - the mod calls the
    /// controller's own ShowPage, so the animation, the tooltips and the page help all run. The
    /// game's own pager keys stand down while this screen owns input (GameInputPatches), and the
    /// Continue at the end of the list runs the controller's own ContinueToNextScene.
    ///
    /// An objective row is a stop of its own: its name, whether this playthrough has reached it,
    /// and on Space the game's description plus each requirement with a live met marker - the
    /// page shows that marker only by dimming the row, which no text sweep can hear.
    /// </summary>
    public sealed class ChapterSummaryScreen : Screen
    {
        public override string Key => "chaptersummary";
        public override Message ScreenName => Message.Localized("ui", "screen.chaptersummary");
        public override int Layer => 0;

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo PageIndexField =
            typeof(ChapterSummaryWindowController).GetField("_currentPageIndex", NP);
        private static readonly FieldInfo ReadyField =
            typeof(ChapterSummaryWindowController).GetField("_isReadyToShowPage", NP);
        private static readonly FieldInfo SummaryViewsField =
            typeof(ChapterSummaryWindowController).GetField("_chapterSummaryViews", NP);
        private static readonly FieldInfo ParametersViewsField =
            typeof(ChapterSummaryWindowController).GetField("_chapterParametersViews", NP);
        private static readonly FieldInfo CategoryTitleField =
            typeof(ObjectiveContainerController).GetField("titleLocalization", NP);
        private static readonly FieldInfo CategoryContainerField =
            typeof(ObjectiveContainerController).GetField("container", NP);

        public override bool IsActive() => Controller() != null;

        private static ChapterSummaryWindowController Controller()
            => Object.FindObjectOfType<ChapterSummaryWindowController>();

        private static int PageCount(ChapterSummaryWindowController c)
        {
            var views = ParametersViewsField.GetValue(c) as ICollection;
            return views == null ? 0 : views.Count;
        }

        private static GameObject Page(FieldInfo field, ChapterSummaryWindowController c, int index)
        {
            var views = field.GetValue(c) as IList;
            if (views == null || index < 0 || index >= views.Count) return null;
            var summary = views[index] as IChapterSummaryView;
            if (summary != null) return summary.GameObject;
            var parameters = views[index] as IChapterParametersView;
            return parameters == null ? null : parameters.GameObject;
        }

        private const string RegionPrefix = "chaptersummary:page:";

        public override void OnUpdate()
        {
            var c = Controller();
            if (c == null) return;
            var region = Navigation.FocusedRegionKey as string;
            if (region == null || !region.StartsWith(RegionPrefix)) return;
            int target = int.Parse(region.Substring(RegionPrefix.Length));
            if (target == (int)PageIndexField.GetValue(c)) return;
            // The controller refuses a swap while the last one is still fading; the next frame's
            // poll picks the move up again.
            if (!(bool)ReadyField.GetValue(c)) return;
            c.ShowPage(target);
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;
            int pages = PageCount(c);
            if (pages == 0) return;

            b.PushContext("", role: null, positions: false);
            for (int i = 0; i < pages; i++)
            {
                b.SetRegion(RegionPrefix + i);
                int page = i;
                b.AddItem(ControlId.Structural("chaptersummary:pos:" + i),
                    TextRow(() => Loc.T("page.position", new { index = page + 1, count = pages })));
                BuildPage(b, Page(SummaryViewsField, c, i), i, "summary");
                BuildPage(b, Page(ParametersViewsField, c, i), i, "parameters");
            }
            b.SetRegion(null);
            b.PopContext();

            // The book's own way out. The game reveals its Continue on the last page only, at the
            // end of the pager's reveal animation; as the last stop of the list it is reached the
            // same way - by walking to the end - and runs the controller's own handler.
            b.AddItem(ControlId.Structural("chaptersummary:continue"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("chaptersummary.continue"),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () => { var live = Controller(); if (live != null) live.ContinueToNextScene(); },
            });
        }

        // One side of one page. The objectives page hangs its rows under the game's own category
        // containers (personal, family, political...), which is the structure the page is read
        // with; every other page is game-composed rows with no model behind them, so its visible
        // labels are the rows.
        private static void BuildPage(GraphBuilder b, GameObject page, int index, string side)
        {
            if (page == null) return;
            var folded = new HashSet<GameObject>();
            foreach (var category in page.GetComponentsInChildren<ObjectiveContainerController>(true))
            {
                var container = (RectTransform)CategoryContainerField.GetValue(category);
                if (container == null || container.childCount == 0) continue;
                var title = (TextMeshProLocalization)CategoryTitleField.GetValue(category);
                if (title != null) folded.Add(title.gameObject);
                b.PushContext(title == null ? "" : UiWidgets.LocalizedLabel(title.gameObject),
                    role: null);
                foreach (var view in category.GetComponentsInChildren<DestinyObjectiveView>(true))
                {
                    foreach (var tmp in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        folded.Add(tmp.gameObject);
                    AddObjective(b, view, index, side);
                }
                b.PopContext();
            }

            foreach (var tmp in page.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (folded.Contains(tmp.gameObject)) continue;
                if (!WouldShow(page, tmp.transform)) continue;
                if (string.IsNullOrEmpty(tmp.text) || tmp.text.Trim().Length == 0) continue;
                var t = tmp;
                b.AddItem(ControlId.Referenced(t,
                        "chaptersummary:" + index + ":" + side + ":" + t.GetInstanceID()),
                    TextRow(() => t.text.Trim()));
            }
        }

        private static void AddObjective(GraphBuilder b, DestinyObjectiveView view,
            int index, string side)
        {
            var v = view;
            b.AddItem(ControlId.Referenced(v,
                    "chaptersummary:" + index + ":" + side + ":obj:" + v.GetInstanceID()),
                new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => ObjectiveLabel(v), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => ObjectiveState(v), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => ObjectiveLabel(v),
                    OnTooltip = () => Mod.Speech.Output(ObjectiveDetails(v)),
                });
        }

        // The row's own words, with the figures the game draws beside a stepped objective folded
        // in. A row on a page the book is not showing renders nothing, so the model's name stands
        // in - that is the whole point of building every page at once.
        private static string ObjectiveLabel(DestinyObjectiveView view)
        {
            var rendered = PanelText.Read(view.gameObject);
            if (!string.IsNullOrEmpty(rendered)) return rendered;
            if (view.Objective != null) return view.Objective.GetNameLocalised();
            // An ENDING row shares this prefab and carries no objective model, so its name comes
            // from the text the game already wrote into the row - read past the inactive page.
            var sb = new System.Text.StringBuilder();
            foreach (var tmp in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
                PanelText.Append(sb, tmp.text == null ? null : tmp.text.Trim());
            return sb.Length == 0 ? null : sb.ToString();
        }

        // Reached or not - drawn as black against gray, which speech cannot carry. An ENDING row
        // (the chapter's finales share the objective prefab) carries no objective model and no
        // reached state of its own.
        private static string ObjectiveState(DestinyObjectiveView view)
        {
            var objective = view.Objective;
            if (objective == null) return null;
            return Loc.T(objective.Unlocked ? "destiny.achieved" : "destiny.notachieved");
        }

        private static string ObjectiveDetails(DestinyObjectiveView view)
        {
            var objective = view.Objective;
            if (objective == null) return Loc.T("nav.no_details");
            var sb = new System.Text.StringBuilder();
            PanelText.Append(sb, objective.GetDescriptionLocalised());
            PanelText.Append(sb, ChoiceConditions.ObjectiveDetails(objective, view.gameObject));
            return sb.Length == 0 ? Loc.T("nav.no_details") : sb.ToString();
        }

        // Shown-when-its-page-shows: every ancestor below the page root is individually active
        // (the page root itself toggles as the book turns, so it is exempt).
        private static bool WouldShow(GameObject page, Transform t)
        {
            for (; t != null && t.gameObject != page; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return t != null;
        }

        private static NodeVtable TextRow(System.Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new[]
            {
                new NodeAnnouncement(text, kind: AnnouncementKinds.Label),
            },
        };
    }
}
