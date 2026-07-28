using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using _Scripts.Managers;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// The "hold the choice" setting, as it affects keyboard activation. With holding ON the game
    /// leaves a choice button's onClick UNWIRED and resolves the choice from the mouse-hold
    /// sequence instead - a click reaches nothing and the choice cannot be taken. Both routes end
    /// in the view's own ResolveClickEvent, so a keyboard press runs that directly. Every choice
    /// surface in the game shares this shape (scene choices, trip options, case-for-a-year
    /// options), so they share this helper.
    /// </summary>
    public static class HoldChoice
    {
        public static bool Holding => GameManager.Instance.GameSettings.UseAnswerButtonHolding;

        /// <summary>Run the view's own resolve - what completing the hold would have called.</summary>
        public static void Resolve(MonoBehaviour view) => ResolveMethod(view.GetType()).Invoke(view, null);

        private static readonly Dictionary<System.Type, MethodInfo> Resolvers =
            new Dictionary<System.Type, MethodInfo>();

        private static MethodInfo ResolveMethod(System.Type type)
        {
            MethodInfo method;
            if (Resolvers.TryGetValue(type, out method)) return method;
            method = type.GetMethod("ResolveClickEvent",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                Mod.Log("hold-to-choose: " + type.Name + " has no ResolveClickEvent - the game's "
                    + "choice-resolution shape changed; keyboard activation cannot reach it");
            Resolvers[type] = method;
            return method;
        }
    }
}
