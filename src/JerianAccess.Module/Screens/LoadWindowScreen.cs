using System.Collections.Generic;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using _Scripts.AMVCC.MainMenu;
using SaveSlot = _Scripts.AMVCC.Views.Windows.SaveLoad.SaveSlot;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The load window (the main menu's Continue opens it as an ADDITIVE "LoadWindow" scene,
    /// owned by LoadGameWindowController). One stop: the save slots newest-first (the game's own
    /// order), each folding the game's localized chapter, hero name, date and time onto the item.
    /// Enter loads through the slot's own LoadSaveData (sound + load run); Backspace deletes
    /// through DeleteSaveSlot (which opens the game's confirm popup). No mod Back action: the
    /// controller binds its own InputSystem escape that closes the window on the physical Escape.
    /// </summary>
    public sealed class LoadWindowScreen : Screen
    {
        public override string Key => "loadwindow";
        public override Message ScreenName => Message.Localized("ui", "screen.loadwindow");
        public override int Layer => 10;

        public override bool IsActive()
            => Object.FindObjectOfType<LoadGameWindowController>() != null;

        public override void Build(GraphBuilder b)
        {
            var slots = Slots();
            if (slots.Count == 0)
            {
                b.AddItem(ControlId.Structural("loadwindow:empty"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Loc.T("loadwindow.empty"),
                            kind: AnnouncementKinds.Label),
                    },
                });
                return;
            }

            foreach (var slot in slots)
            {
                var s = slot;
                b.AddItem(ControlId.Referenced(s, "loadwindow:slot:" + s.gameObject.name),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => Label(s), kind: AnnouncementKinds.Label),
                        },
                        SearchText = () => Label(s),
                        OnActivate = () => s.LoadSaveData(),
                        // Delete asks for confirmation through the game's own popup.
                        OnSecondary = () => s.DeleteSaveSlot(),
                    });
            }
        }

        // The slot's own localized texts, folded onto the item: "Chapter N. Title, Hero,
        // date time". Read live at speech time - never cached.
        private static string Label(SaveSlot s)
            => Loc.T("loadwindow.slot", new
            {
                chapter = Text(s.ChapterName),
                name = Text(s.CharacterName),
                date = Text(s.Date),
                time = Text(s.Time),
            });

        private static string Text(TMPro.TextMeshProUGUI t)
            => t == null ? "" : t.text;

        // Visible save slots top-to-bottom (the game builds them newest-first down the list).
        private static List<SaveSlot> Slots()
        {
            var list = new List<SaveSlot>();
            foreach (var s in Object.FindObjectsOfType<SaveSlot>())
                if (UiWidgets.Visible(s.gameObject))
                    list.Add(s);
            list.Sort((a, c) => c.transform.position.y.CompareTo(a.transform.position.y));
            return list;
        }
    }
}
