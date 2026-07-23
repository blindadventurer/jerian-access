using System.Reflection;
using _Scripts.Managers;
using _Scripts.Windows;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// The one adapter for the game's UI surface (CLAUDE.md: centralize shared reads; never cache
    /// game state - every property re-reads the live singleton at call time). UIManager.Initiate
    /// is null between scene loads; that is the only legitimate null here.
    /// </summary>
    public static class GameUi
    {
        public static UIManager Manager => UIManager.Initiate;

        public static string SceneName => SceneManager.GetActiveScene().name;

        /// <summary>Whether a named scene is loaded, active or ADDITIVE - the main menu opens
        /// Settings/LoadWindow as additive scene loads, not UIManager window slots.</summary>
        public static bool IsSceneLoaded(string name) => SceneManager.GetSceneByName(name).isLoaded;

        // Jerian moved HUD windows off UIManager's slot onto WindowControllersManager, whose
        // private _activeWindow is the live open window (null when none, cleared on close).
        // UIManager.OpenedWindow stays null on Jerian, so read the manager directly.
        private static readonly FieldInfo ActiveWindowField =
            typeof(WindowControllersManager).GetField("_activeWindow",
                BindingFlags.NonPublic | BindingFlags.Instance);

        public static GameObject OpenedWindow
        {
            get
            {
                var mgr = Object.FindObjectOfType<WindowControllersManager>();
                if (mgr == null) return null;
                var w = (GameObject)ActiveWindowField.GetValue(mgr);
                return w != null && w.activeInHierarchy ? w : null;
            }
        }
        public static GameObject OpenedPopup => Manager == null ? null : Manager.OpenedPopup;
        public static GameObject OpenedTooltip => Manager == null ? null : Manager.OpenedTooltip;

        public static bool PauseOpen =>
            Manager != null && Manager.PauseWindow != null && Manager.PauseWindow.activeInHierarchy;

        /// <summary>The open window's what-this-is help: the description behind the game's
        /// TitleRow help icon (Window_X.Description), read fresh per call. Null where the
        /// window carries none. The first Title-parented HelpIcon is the window-level one
        /// (deeper icons - Destiny's timeline - belong to their own panes).</summary>
        public static string WindowHelp()
        {
            var w = OpenedWindow;
            if (w == null) return null;
            foreach (var tip in w.GetComponentsInChildren<_Scripts.AMVCC.Views.TooltipWithTitleBehavior>(true))
                if (tip.name == "HelpIcon" && tip.transform.parent.name.Contains("Title")
                    && !string.IsNullOrEmpty(tip.DescriptionKey))
                    return I2.Loc.LocalizationManager.GetTranslation(tip.DescriptionKey);
            return null;
        }

        /// <summary>The game's own global Escape read (UIManager.Update, suppressed while our
        /// screens are active), replayed for screens without a Back action of their own: the
        /// pause menu toggles exactly when the game itself would allow it - IsEscButtonBlocked
        /// tracks GameState and is false only while RUNNING.</summary>
        public static void GlobalEscape()
        {
            var m = Manager;
            if (m != null && !m.IsEscButtonBlocked) m.ShowPauseMenu();
        }

        /// <summary>PREGAME at the menu and pre-story scenes; RUNNING flips the instant a game
        /// begins (SetCharacterName / save load), BEFORE the menu scene unloads - screens gate on
        /// it to drop out early instead of speaking stale refocus lines mid-transition.
        /// GameManager.Instance is null for the frames of a save-load scene swap; that
        /// transition reads as PREGAME so per-frame screen polls drop out instead of throwing.</summary>
        public static GameState State => GameManager.Instance == null
            ? GameState.PREGAME : GameManager.Instance.CurrentGameState;
    }
}
