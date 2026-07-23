using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.Models.Static;
using _Scripts.AMVCC.Views.HUD;
using _Scripts.Managers;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// The story HUD as its own Tab-stop: the two resource counters (Sanity, Tempest Legacy -
    /// read from the model, not the beads) then the window buttons (Destiny, Character,
    /// Relations, Council, Empire, Creed, Map, ...). Button labels are the game's own serialized
    /// unlockedTextKey and locked reasons its tooltip key (lockedTextKey + the unlocking chapter);
    /// availability is the controller's own Unlocked flag; activation goes through the button's
    /// click wiring so the game opens the window. Everything re-reads live (CLAUDE.md).
    /// </summary>
    public static class HudReadout
    {
        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo UnlockedKeyField =
            typeof(HudButtonController).GetField("unlockedTextKey", NP);
        private static readonly FieldInfo LockedKeyField =
            typeof(HudButtonController).GetField("lockedTextKey", NP);
        private static readonly FieldInfo ButtonField =
            typeof(HudButtonController).GetField("button", NP);
        private static readonly FieldInfo IsBackField =
            typeof(HudButtonController).GetField("isBackButton", NP);
        private static readonly FieldInfo SanityParamField =
            typeof(HudSanityCounterView).GetField("sanityParameter", NP);
        private static readonly FieldInfo TempestParamField =
            typeof(HudTempestLegacyCounterView).GetField("tempestLegacyParameter", NP);

        public static void Build(GraphBuilder b)
        {
            var sanity = Object.FindObjectOfType<HudSanityCounterView>();
            var tempest = Object.FindObjectOfType<HudTempestLegacyCounterView>();
            var buttons = Buttons();
            if (sanity == null && tempest == null && buttons.Count == 0) return;

            b.BeginStop("hud");
            b.PushContext(Loc.T("hud.region"), role: null);

            if (sanity != null && sanity.gameObject.activeInHierarchy)
                AddCounter(b, "hud:sanity", (Parameter)SanityParamField.GetValue(sanity));
            if (tempest != null && tempest.gameObject.activeInHierarchy)
                AddCounter(b, "hud:tempest", (Parameter)TempestParamField.GetValue(tempest));

            foreach (var btn in buttons) AddButton(b, btn);
            b.PopContext();
        }

        private static void AddCounter(GraphBuilder b, string id, Parameter param)
        {
            if (param == null) return;
            b.AddItem(ControlId.Referenced(param, id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("hud.pair", new
                    {
                        label = param.GetNameLocalised(),
                        value = ParametersManager.Instance.GetParameterValue(param),
                    }), kind: AnnouncementKinds.Label),
                },
            });
        }

        private static void AddButton(GraphBuilder b, HudButtonController ctrl)
        {
            var button = (Button)ButtonField.GetValue(ctrl) ?? ctrl.GetComponentInChildren<Button>();
            if (button == null) return;
            var unlockedKey = (string)UnlockedKeyField.GetValue(ctrl);
            b.AddItem(ControlId.Referenced(ctrl, "hud:btn:" + ctrl.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Translate(unlockedKey), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => LockedReason(ctrl), kind: AnnouncementKinds.Enabled),
                },
                OnActivate = () =>
                {
                    // The game also blocks HUD buttons transiently without locking them (page-turn
                    // animations, IsButtonsBlocked) - Enter reports that rather than clicking into
                    // a button the game is ignoring.
                    var reason = LockedReason(ctrl)
                        ?? (button.interactable ? null : Loc.T("state.unavailable"));
                    if (reason != null)
                    {
                        Mod.Speech.Speak(reason, interrupt: true);
                        return;
                    }
                    UiWidgets.Click(button.gameObject);
                },
            });
        }

        // The game's own reason for a locked button, as its tooltip states it: the serialized
        // lockedTextKey is a PREFIX completed by the unlocking chapter (HUD.WillOpen + 2 =
        // "В главе II"). A button the game locks with no tooltip (scene-locked, or locked past
        // its own chapter) gives no reason, and the bare unavailable word carries it.
        private static string LockedReason(HudButtonController ctrl)
        {
            if (ctrl.Unlocked) return null;
            var reason = ctrl.DontShowTooltip ? null
                : Translate((string)LockedKeyField.GetValue(ctrl) + ctrl.ChapterToUnlock);
            return string.IsNullOrEmpty(reason)
                ? Loc.T("state.unavailable")
                : Loc.T("state.unavailable_reason", new { reason });
        }

        // Visible HUD buttons top-to-bottom (the column runs down the right rail), then left to
        // right. Re-queried per render - never cached.
        private static List<HudButtonController> Buttons()
        {
            var list = new List<HudButtonController>();
            var seen = new HashSet<string>();
            foreach (var ctrl in Object.FindObjectsOfType<HudButtonController>())
            {
                if (!ctrl.gameObject.activeInHierarchy || !UiWidgets.Visible(ctrl.gameObject)) continue;
                if ((bool)IsBackField.GetValue(ctrl)) continue; // the back button is its own node
                // The HUD prefab carries duplicate window buttons (a template + the live one share
                // a name); keep the first so a window opens from one stop, not two.
                if (!seen.Add(ctrl.gameObject.name)) continue;
                list.Add(ctrl);
            }
            list.Sort((a, c) =>
            {
                int byY = c.transform.position.y.CompareTo(a.transform.position.y);
                return byY != 0 ? byY : a.transform.position.x.CompareTo(c.transform.position.x);
            });
            return list;
        }

        private static string Translate(string key)
            => string.IsNullOrEmpty(key) ? null : I2.Loc.LocalizationManager.GetTranslation(key);

        /// <summary>The HUD's back-to-scene button while it is shown (a window is open), or null.
        /// Clicking it closes the open window through the game's own wiring.</summary>
        public static Button BackButton()
        {
            var ctrl = BackController();
            if (ctrl == null) return null;
            return (Button)ButtonField.GetValue(ctrl) ?? ctrl.GetComponentInChildren<Button>();
        }

        /// <summary>The game's own localized label for the back button ("Назад к сцене").</summary>
        public static string BackLabel()
        {
            var ctrl = BackController();
            return ctrl == null ? null : Translate((string)UnlockedKeyField.GetValue(ctrl));
        }

        private static HudButtonController BackController()
        {
            foreach (var ctrl in Object.FindObjectsOfType<HudButtonController>())
                if ((bool)IsBackField.GetValue(ctrl)
                    && ctrl.gameObject.activeInHierarchy && UiWidgets.Visible(ctrl.gameObject))
                    return ctrl;
            return null;
        }
    }
}
