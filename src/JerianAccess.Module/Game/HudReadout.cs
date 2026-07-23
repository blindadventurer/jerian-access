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
    /// Relations, Council, Empire, Creed, Map, ...). Button labels and their locked reasons are
    /// the game's own serialized keys (unlockedTextKey / lockedTextKey); availability is the
    /// controller's own Unlocked flag; activation goes through the button's click wiring so the
    /// game opens the window. Everything re-reads live (CLAUDE.md).
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
            var lockedKey = (string)LockedKeyField.GetValue(ctrl);
            b.AddItem(ControlId.Referenced(ctrl, "hud:btn:" + ctrl.gameObject.name), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Translate(unlockedKey), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(
                        () => ctrl.Unlocked ? null : Translate(lockedKey),
                        kind: AnnouncementKinds.Enabled),
                },
                OnActivate = () =>
                {
                    if (!ctrl.Unlocked)
                    {
                        Mod.Speech.Speak(Translate(lockedKey), interrupt: true);
                        return;
                    }
                    UiWidgets.Click(button.gameObject);
                },
            });
        }

        // Visible HUD buttons top-to-bottom (the column runs down the right rail), then left to
        // right. Re-queried per render - never cached.
        private static List<HudButtonController> Buttons()
        {
            var list = new List<HudButtonController>();
            foreach (var ctrl in Object.FindObjectsOfType<HudButtonController>())
                if (ctrl.gameObject.activeInHierarchy && UiWidgets.Visible(ctrl.gameObject))
                    list.Add(ctrl);
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
            foreach (var ctrl in Object.FindObjectsOfType<HudButtonController>())
                if ((bool)IsBackField.GetValue(ctrl)
                    && ctrl.gameObject.activeInHierarchy && UiWidgets.Visible(ctrl.gameObject))
                    return (Button)ButtonField.GetValue(ctrl) ?? ctrl.GetComponentInChildren<Button>();
            return null;
        }
    }
}
