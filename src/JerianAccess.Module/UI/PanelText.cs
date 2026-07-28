using System.Collections.Generic;
using System.Text;
using Assets._Scripts.AMVCC.Views;
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
            => Read(root, null, skip);

        /// <summary>The same read with a subtree filter: a label the filter excludes belongs to
        /// something else on the page (an option card that is its own stop), not to this panel.</summary>
        public static string Read(GameObject root, System.Func<GameObject, bool> exclude,
            params string[] skip)
        {
            if (root == null) return "";
            var rows = new List<Transform>();
            var texts = new List<string>();
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                Take(tmp.gameObject, tmp.text, skip, exclude, rows, texts);
            foreach (var legacy in root.GetComponentsInChildren<UnityEngine.UI.Text>(false))
                Take(legacy.gameObject, legacy.text, skip, exclude, rows, texts);
            // A panel that shows an estate's strength as a bare figure (the outcome page an
            // option scene puts beside its choices) reads as a run of loose numbers otherwise.
            // The game hangs the word on the same data container its own tooltip reads.
            for (int i = 0; i < texts.Count; i++)
            {
                if (!IsFigure(texts[i])) continue;
                var name = Game.ContainerLabel.NameOf(rows[i].gameObject);
                if (!string.IsNullOrEmpty(name)) texts[i] = name + " " + texts[i];
            }

            var order = new List<int>();
            for (int i = 0; i < rows.Count; i++) order.Add(i);
            order.Sort((a, c) =>
            {
                // A bare figure follows the words it belongs to: the game lays a row's value
                // ahead of its name as often as after it, and "0 Прогресс" states it backwards.
                if (rows[a].parent == rows[c].parent)
                {
                    bool figureA = IsFigure(texts[a]), figureC = IsFigure(texts[c]);
                    if (figureA != figureC) return figureA ? 1 : -1;
                }
                int byY = rows[c].position.y.CompareTo(rows[a].position.y);
                return byY != 0 ? byY : rows[a].position.x.CompareTo(rows[c].position.x);
            });

            var sb = new StringBuilder();
            foreach (var i in order) Append(sb, texts[i]);
            return SpokenOperators(sb.ToString());
        }

        /// <summary>Add one more fragment to a spoken line, separated as a sentence - and never
        /// doubly punctuated: a fragment that ends in its own punctuation (a chapter card's
        /// "1132г.") keeps it and gets a plain space.</summary>
        public static void Append(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (sb.Length > 0)
            {
                char last = sb[sb.Length - 1];
                bool punctuated = last == '.' || last == '!' || last == '?' || last == ':'
                                  || last == ',' || last == ';' || last == '…';
                // A bare figure is the value of the words just read ("Прогресс 0"), not a
                // sentence of its own.
                sb.Append(punctuated || IsFigure(text) ? " " : ". ");
            }
            sb.Append(text);
        }

        /// <summary>The comparison operators as WORDS. The game renders a condition row's
        /// operator as a bare symbol ("≥"), which readers voice unreliably or not at all; the
        /// symbols are the game's own translations of its Operations terms, so each is swapped
        /// for the mod's spoken word for that same operation.</summary>
        private static string SpokenOperators(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (Operations op in System.Enum.GetValues(typeof(Operations)))
            {
                var symbol = I2.Loc.LocalizationManager.GetTranslation("Operations." + op);
                if (string.IsNullOrEmpty(symbol) || symbol.Length > 2) continue;
                var word = Localization.LocalizationManager.GetOrDefault(
                    "ui", "choice.op." + op.ToString().ToLowerInvariant(), null);
                if (word != null) text = text.Replace(symbol, word);
            }
            return text;
        }

        private static void Take(GameObject go, string text, string[] skip,
            System.Func<GameObject, bool> exclude, List<Transform> rows, List<string> texts)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;
            if (!UiWidgets.Visible(go)) return;
            foreach (var name in skip)
                if (go.name.Contains(name)) return;
            if (exclude != null && exclude(go)) return;
            rows.Add(go.transform);
            texts.Add(text.Trim());
        }

        private static bool IsFigure(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            foreach (var c in s)
                if (!char.IsDigit(c) && c != '+' && c != '-') return false;
            return true;
        }
    }
}
