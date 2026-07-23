using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.MainMenu;
using _Scripts.SaveGame;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The main menu. Jerian's menu is TMP text buttons driven by MainMenuController (Brante's
    /// sprite CustomMainMenuButtons survive in the assembly but are unused here), so labels are
    /// the game's own localized text read live per render. One stop: every visible button in
    /// the MainMenu scene in visual order. Continue's availability keys off the MODEL
    /// (SaveLoadManagerV2.SavesExist), never Button.interactable - the game also drops
    /// interactable as a post-click guard. Activation goes through the game's pointer-click
    /// path so handlers, sounds, and the nav-group toggle run.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "mainmenu";
        public override Message ScreenName => Message.Localized("ui", "screen.mainmenu");
        public override int Layer => 0;

        // MainMenuController turns its navigationGroups off while an additive window scene
        // (NewGame/Settings/LoadWindow) owns the input - the game's own "menu handed off" flag,
        // restored by the BackToMainMenuEvent handler. Both fields are private serialized.
        private static readonly FieldInfo NavigationGroupsField = typeof(MainMenuController)
            .GetField("navigationGroups", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ContinueButtonField = typeof(MainMenuController)
            .GetField("_continueButton", BindingFlags.NonPublic | BindingFlags.Instance);

        public override bool IsActive()
        {
            if (GameUi.SceneName != "MainMenu" || GameUi.State != GameState.PREGAME) return false;
            var controller = Object.FindObjectOfType<MainMenuController>();
            if (controller == null) return false;
            var groups = (List<GameObject>)NavigationGroupsField.GetValue(controller);
            foreach (var g in groups)
                if (g != null && !g.activeSelf) return false; // handed off to an additive window
            // The menu buttons fade in after the logo (CanvasGroup alpha); activating before
            // they are visible builds a graph of just the always-on link buttons and homes
            // focus there (seen live: first focus landed on Discord). Wait for the primary
            // block so the first graph is complete and home lands on the first menu button.
            var continueButton = (Button)ContinueButtonField.GetValue(controller);
            return continueButton != null && UiWidgets.Visible(continueButton.gameObject);
        }

        public override void Build(GraphBuilder b)
        {
            var controller = Object.FindObjectOfType<MainMenuController>();
            if (controller == null) return;
            var continueButton = (Button)ContinueButtonField.GetValue(controller);
            foreach (var btn in MenuButtons())
            {
                var go = btn.gameObject;
                bool isContinue = btn == continueButton;
                b.AddItem(ControlId.Referenced(btn, "mainmenu:" + go.name), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => UiWidgets.LocalizedLabel(go),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(
                            () => !isContinue || SavesExist() ? null : Loc.T("state.unavailable"),
                            kind: AnnouncementKinds.Enabled),
                    },
                    OnActivate = () =>
                    {
                        if (isContinue && !SavesExist())
                        {
                            Mod.Speech.Speak(Loc.T("state.unavailable"), interrupt: true);
                            return;
                        }
                        UiWidgets.Click(go);
                    },
                });
            }
        }

        private static bool SavesExist() => SaveLoadManagerV2.Instance.SavesExist();

        // Visible MainMenu-scene buttons in visual order (top to bottom, then left to right).
        // Scene-scoped: FindObjectsOfType sees additively loaded scenes too, and their buttons
        // belong to their own (future) screens. Re-queried per render - never cached (CLAUDE.md).
        private static List<Button> MenuButtons()
        {
            var list = new List<Button>();
            foreach (var btn in Object.FindObjectsOfType<Button>())
                if (btn.gameObject.scene.name == "MainMenu" && UiWidgets.Visible(btn.gameObject))
                    list.Add(btn);
            list.Sort((a, c) =>
            {
                int byY = c.transform.position.y.CompareTo(a.transform.position.y);
                return byY != 0 ? byY : a.transform.position.x.CompareTo(c.transform.position.x);
            });
            return list;
        }
    }
}
