using System.Collections.Generic;
using System.Text;
using JerianAccess.Module.Game;
using UnityEngine;

namespace JerianAccess.Module.UI
{
    /// <summary>
    /// What a game-composed panel says, in reading order: every visible label under a root,
    /// top-to-bottom then left-to-right, joined into one line. For surfaces the game builds from
    /// prefabs at runtime (tutorial steps, interlude pages, popup bodies) the rendered text IS the
    /// game's own localized output - there is no model behind it to read instead - so a screen
    /// delivers the panel by reading it. Sweeps TMP and legacy uGUI Text both (the game mixes
    /// them).
    /// </summary>
    internal static class PanelText
    {
        /// <summary>The panel's visible text, or an empty string when nothing is rendered yet.
        /// Names in <paramref name="skip"/> are matched against the object name and dropped -
        /// persistent chrome (a year label, a page counter) that would repeat on every page.</summary>
        public static string Read(GameObject root, params string[] skip)
        {
            if (root == null) return "";
            var rows = new List<Transform>();
            var texts = new List<string>();
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                Take(tmp.gameObject, tmp.text, skip, rows, texts);
            foreach (var legacy in root.GetComponentsInChildren<UnityEngine.UI.Text>(false))
                Take(legacy.gameObject, legacy.text, skip, rows, texts);

            var order = new List<int>();
            for (int i = 0; i < rows.Count; i++) order.Add(i);
            order.Sort((a, c) =>
            {
                int byY = rows[c].position.y.CompareTo(rows[a].position.y);
                return byY != 0 ? byY : rows[a].position.x.CompareTo(rows[c].position.x);
            });

            var sb = new StringBuilder();
            foreach (var i in order)
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(texts[i]);
            }
            return sb.ToString();
        }

        private static void Take(GameObject go, string text, string[] skip,
            List<Transform> rows, List<string> texts)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;
            if (!UiWidgets.Visible(go)) return;
            foreach (var name in skip)
                if (go.name.Contains(name)) return;
            rows.Add(go.transform);
            texts.Add(text.Trim());
        }
    }
}
