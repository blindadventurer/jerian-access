using System.Collections.Generic;
using JerianAccess.Module.Input;
using JerianAccess.Module.Speech;
using UnityEngine;
using _Scripts.AMVCC.Views.Windows.Cutscenes;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// A video cutscene (Jerian: CutsceneController + VideoPlayer in the CutsceneTemplate
    /// scene). Claims NO input category so every key reaches the game untouched - the game's
    /// own Space skip is the only path that ends the cutscene early. Subtitles are the game's
    /// own localized TMP line that toggles on per phrase: spoken off its text/visibility
    /// change (no Harmony needed on Jerian - the Brante SubtitleSpeechPatches route is
    /// obsolete here). The skip hint ("[SPACE] skip") is announced once when the game shows
    /// it.
    /// </summary>
    public sealed class CutsceneScreen : Screen
    {
        public override string Key => "cutscene";
        public override int Layer => 0;

        private static readonly InputCategory[] None = new InputCategory[0];
        public override IReadOnlyList<InputCategory> InputCategories => None;

        private string _spokenSubtitle;
        private bool _skipHintSpoken;

        private static readonly System.Reflection.FieldInfo HasExecutedField =
            typeof(CutsceneController).GetField("_hasExecuted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>The cutscene is actually SHOWING - the controller shell stays active in its
        /// loaded scene after NextScene fires (_hasExecuted), while the story already owns the
        /// screen (seen live). Shared gate: SceneScreen yields exactly while this is true.</summary>
        public static bool CutsceneShowing
        {
            get
            {
                var c = Object.FindObjectOfType<CutsceneController>();
                return c != null && !(bool)HasExecutedField.GetValue(c);
            }
        }

        public override bool IsActive() => CutsceneShowing;

        public override Message ScreenName => Message.Localized("ui", "cutscene.chapter");

        public override void OnPop()
        {
            _spokenSubtitle = null;
            _skipHintSpoken = false;
        }

        public override void OnUpdate()
        {
            var controller = Object.FindObjectOfType<CutsceneController>();
            if (controller == null) return;
            var scene = controller.gameObject.scene;

            string subtitle = null;
            string skipHint = null;
            foreach (var tmp in Object.FindObjectsOfType<TMPro.TextMeshProUGUI>())
            {
                if (tmp.gameObject.scene != scene || !tmp.gameObject.activeInHierarchy) continue;
                bool isSkip = false;
                for (var t = tmp.transform; t != null; t = t.parent)
                    if (t.name == "SpaceForSkip") { isSkip = true; break; }
                if (isSkip) skipHint = tmp.text;
                else subtitle = tmp.text;
            }

            if (!string.IsNullOrEmpty(subtitle) && subtitle != _spokenSubtitle)
            {
                _spokenSubtitle = subtitle;
                Mod.Speech.Output(subtitle);
            }
            if (!string.IsNullOrEmpty(skipHint) && !_skipHintSpoken)
            {
                _skipHintSpoken = true;
                Mod.Speech.Output(skipHint);
            }
        }
    }
}
