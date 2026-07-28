using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JerianAccess.Module.Game;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.ChapterSummary;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The chapter-summary book (Window_ChapterSummary): a paged recap - left page a summary
    /// (name, title, description), right page the chapter's parameters. The game pages it
    /// NATIVELY (PageNavigationController.Update reads the arrow / continue keys), so this
    /// screen claims NO input and only DELIVERS: each settled page is spoken once, off the
    /// controller's own page index and the rendered visible text. Speaking off the rendered
    /// content means whatever the game shows is what is read, in reading order (left page then
    /// right). The player pages with the same keys a sighted player uses.
    /// </summary>
    public sealed class ChapterSummaryScreen : Screen
    {
        public override string Key => "chaptersummary";
        public override Message ScreenName => Message.Localized("ui", "screen.chaptersummary");
        public override int Layer => 0;

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo LeftPageField =
            typeof(ChapterSummaryWindowController).GetField("leftPage", NP);
        private static readonly FieldInfo RightPageField =
            typeof(ChapterSummaryWindowController).GetField("rightPage", NP);
        private static readonly FieldInfo PageIndexField =
            typeof(ChapterSummaryWindowController).GetField("_currentPageIndex", NP);
        private static readonly FieldInfo ReadyField =
            typeof(ChapterSummaryWindowController).GetField("_isReadyToShowPage", NP);
        private static readonly FieldInfo SummaryViewsField =
            typeof(ChapterSummaryWindowController).GetField("_chapterSummaryViews", NP);

        private int _spokenPage = -1;
        private string _spokenBody;

        public override bool IsActive() => Controller() != null;

        private static ChapterSummaryWindowController Controller()
            => Object.FindObjectOfType<ChapterSummaryWindowController>();

        public override void OnPop()
        {
            _spokenPage = -1;
            _spokenBody = null;
        }

        // Deliver each page exactly ONCE, and only after the controller reports the page has
        // finished its fade (_isReadyToShowPage). Keying off the stable page index - not the
        // rendered text - stops the fade animation from re-speaking partial content every frame
        // (the persistent year label made every partial read say the year, hence the "year
        // keeps mixing in").
        public override void OnUpdate()
        {
            var c = Controller();
            if (c == null) return;
            if (!(bool)ReadyField.GetValue(c)) return; // page still animating in
            int page = (int)PageIndexField.GetValue(c);
            if (page == _spokenPage) return;
            var body = PageText(c);
            if (string.IsNullOrEmpty(body)) return; // nothing rendered yet
            // The controller reports itself ready a beat before the new page's text lands, so a
            // page turned quickly read as the PREVIOUS page's content under the new page's
            // number. Content that has not changed yet is not this page's content: wait for it.
            if (body == _spokenBody) return;
            _spokenPage = page;
            _spokenBody = body;
            Mod.Speech.Speak(Position(c, page) + body);
        }

        private static string Position(ChapterSummaryWindowController c, int page)
        {
            var views = SummaryViewsField.GetValue(c) as System.Collections.ICollection;
            int count = views == null ? 0 : views.Count;
            if (count <= 1) return "";
            return Loc.T("nav.position", new { index = page + 1, count }) + ". ";
        }

        // Visible text of the current page: left page (summary) then right page (parameters),
        // each top-to-bottom. Reads rendered content, so it always matches the screen.
        private static string PageText(ChapterSummaryWindowController c)
        {
            var sb = new StringBuilder();
            Collect(sb, (GameObject)LeftPageField.GetValue(c));
            Collect(sb, (GameObject)RightPageField.GetValue(c));
            return sb.ToString().Trim();
        }

        private static void Collect(StringBuilder sb, GameObject page)
        {
            if (page == null) return;
            var texts = new List<TMPro.TMP_Text>();
            foreach (var t in page.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (!UiWidgets.Visible(t.gameObject)) continue;
                // The year label is persistent context, not page content - skip it so it is
                // not re-read on every page (the position prefix already orients the reader).
                if (t.gameObject.name.Contains("Year")) continue;
                var s = t.text;
                if (string.IsNullOrEmpty(s) || s.Trim().Length == 0) continue;
                texts.Add(t);
            }
            texts.Sort((a, b) =>
            {
                int byY = b.transform.position.y.CompareTo(a.transform.position.y);
                return byY != 0 ? byY : a.transform.position.x.CompareTo(b.transform.position.x);
            });
            foreach (var t in texts) UI.PanelText.Append(sb, t.text.Trim());
        }
    }
}
