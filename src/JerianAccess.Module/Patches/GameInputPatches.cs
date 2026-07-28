using System;
using HarmonyLib;
using JerianAccess.Module.Input;

namespace JerianAccess.Module.Patches
{
    /// <summary>
    /// Suppresses the game's own arrow-key SCENE paging while a mod screen owns UI navigation.
    /// On Jerian the story pager (TextController.Update) polls the arrow keys every frame and
    /// pages the scene - so with a mod window open OVER a scene (e.g. Pause), a Left/Right meant
    /// for a pause slider ALSO paged the scene underneath (reported live). The mod drives paging
    /// through the pager's own NextPage() method, not this Update, so skipping the input poll
    /// removes nothing but the stray reads.
    ///
    /// Gated on the mod OWNING input (the UI category is active), NOT merely on a screen being
    /// present: delivery-only screens that intentionally leave paging to the game (the chapter
    /// summary book, cutscenes) declare no input category, so their game paging keeps working.
    /// UIManager.Update is deliberately NOT patched - the game's own Escape read there is what
    /// opens/closes the pause menu now that the mod no longer touches Escape.
    /// </summary>
    internal static class GameInputPatches
    {
        private static readonly Type[] InputOnlyUpdates =
        {
            typeof(_Scripts.AMVCC.Controllers.TextController),
            // The pager the story scenes actually run on: BasicSceneController and its
            // inconsequential twin hand their TextController a PageNavigationController, and
            // from then on TextController.Update returns early and THIS Update reads the arrow
            // and continue keys. Unpatched, one physical Right both moved mod focus along the
            // transcript and turned the game's page underneath it.
            typeof(_Scripts.AMVCC.Views.Windows.ChapterSummary.PageNavigation.PageNavigationController),
            // The death window's pager reads the arrows itself (A/D and Left/Right).
            typeof(_Scripts.AMVCC.Views.Windows.Death.SimplePageTurner),
        };

        private static Harmony _harmony;

        public static void Apply()
        {
            _harmony = new Harmony("JerianAccess.Module." + Guid.NewGuid().ToString("N"));
            var prefix = new HarmonyMethod(typeof(GameInputPatches), nameof(SkipWhileModOwnsInput));
            foreach (var type in InputOnlyUpdates)
            {
                var update = AccessTools.Method(type, "Update");
                if (update == null)
                    throw new InvalidOperationException(
                        "no Update method on " + type.FullName + " - game update changed the input surface");
                _harmony.Patch(update, prefix: prefix);
            }
            Mod.Log("game-input patches on " + InputOnlyUpdates.Length
                + " game Update methods (harmony id " + _harmony.Id + ")");
        }

        public static void Remove()
        {
            if (_harmony == null) return;
            _harmony.UnpatchSelf();
            Mod.Log("game-input patches removed (harmony id " + _harmony.Id + ")");
            _harmony = null;
        }

        // Prefix: return false to SKIP the original Update. Skip only while the mod's UI
        // navigation is active (a screen that declares the UI category); otherwise let the game
        // read its keys as normal.
        private static bool SkipWhileModOwnsInput()
            => !InputManager.CategoryActive(InputCategory.UI);
    }
}
