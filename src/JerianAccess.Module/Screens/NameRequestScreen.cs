using System.Collections.Generic;
using System.Reflection;
using JerianAccess.Module.Game;
using JerianAccess.Module.Speech;
using JerianAccess.Module.UI;
using JerianAccess.Module.UI.Graph;
using UnityEngine;
using UnityEngine.UI;
using _Scripts.AMVCC.MainMenu;

namespace JerianAccess.Module.Screens
{
    /// <summary>
    /// The new-game window (Jerian: additive "NewGameWindow" scene owned by
    /// NewGameWindowController; also instantiable as a UIManager popup prefab - presence is the
    /// live controller either way). Content: title, the name field with the game's live
    /// validation subtitle (wrong/blacklisted names swap its text - spoken as it changes during
    /// the edit), three radio pairs (chapter returns, consequences, tutorials - real
    /// ToggleGroups, labels and headers from game text), Back, and Start gated on the game's
    /// own rule (min length + not blacklisted). Enter on the name field starts a MODAL EDIT:
    /// the game's TMP_InputField takes keyboard focus, this screen captures raw input, and
    /// typed characters echo from the field's own text changes. The game's unpatched Update
    /// closes the window on physical Escape - no mod-side Back action (double-close).
    /// </summary>
    public sealed class NameRequestScreen : Screen
    {
        public override string Key => "namerequest";
        public override int Layer => 20;
        public override bool IsActive() => Window() != null;

        private static readonly FieldInfo NameInputField = F("nameInput");
        private static readonly FieldInfo NameSubtitleField = F("nameSubtitle");
        private static readonly FieldInfo NextButtonField = F("_nextButton");
        private static readonly FieldInfo MinCharsField = F("_minCharactersCountForName");
        private static readonly FieldInfo StartLockedField = F("_isStartButtonLocked");
        private static readonly FieldInfo[] PairFields =
        {
            F("IronManToggleOn"), F("IronManToggleOff"),
            F("StoryModeToggleOn"), F("StoryModeToggleOff"),
            F("tutorialsToggleOn"), F("tutorialsToggleOff"),
        };

        private static FieldInfo F(string name) => typeof(NewGameWindowController)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        public override Message ScreenName
        {
            get
            {
                var w = Window();
                if (w == null) return null;
                var title = w.transform.Find("Body/Name");
                return title == null ? null
                    : Message.MaybeRaw(UiWidgets.LocalizedLabel(title.gameObject));
            }
        }

        // Live reference to the field being edited (read at speech time), plus the edit
        // differ's spoken baselines - the last text already announced, never game state kept
        // for later.
        private TMPro.TMP_InputField _editField;
        private TMPro.TextMeshProUGUI _editSubtitle;
        private bool _wasEditing;
        private string _lastText;
        private string _lastSubtitle;

        public override bool CapturesRawInput => _editField != null && _editField.isFocused;

        private static NewGameWindowController Window()
            => Object.FindObjectOfType<NewGameWindowController>();

        public override void Build(GraphBuilder b)
        {
            var w = Window();
            if (w == null) return;
            var field = (TMPro.TMP_InputField)NameInputField.GetValue(w);
            var subtitle = (TMPro.TextMeshProUGUI)NameSubtitleField.GetValue(w);
            int minChars = (int)MinCharsField.GetValue(w);

            var titleId = ControlId.Referenced(w, "namerequest:title");
            b.AddItem(titleId, new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() =>
                    {
                        var t = w.transform.Find("Body/Name");
                        return t == null ? null : UiWidgets.LocalizedLabel(t.gameObject);
                    }, kind: AnnouncementKinds.Label),
                },
            });
            // Explicit start: without it the selected-member landing would seat initial focus
            // on a checked radio in this mixed form-stop instead of the top.
            b.SetStart(titleId);

            b.AddItem(ControlId.Referenced(field, "namerequest:name"), new NodeVtable
            {
                ControlType = ControlTypes.Edit,
                Announcements = new[]
                {
                    // NameRow sits under NameBlock, whose first label is the game's
                    // "enter the hero's name" caption.
                    new NodeAnnouncement(
                        () => UiWidgets.LocalizedLabel(field.transform.parent.parent.gameObject),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(
                        () => field.text.Length > 0 ? field.text : Loc.T("edit.blank"),
                        live: true, kind: AnnouncementKinds.Value),
                },
                OnActivate = () => BeginEdit(field, subtitle),
            });

            // The validation subtitle ("the hero is a man", wrong-name, blacklisted-name): its
            // own stop so it is arrow-reachable; changes DURING the edit are spoken by
            // OnUpdate's subtitle differ.
            b.AddItem(ControlId.Referenced(subtitle, "namerequest:subtitle"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => subtitle.text, kind: AnnouncementKinds.Label),
                },
            });

            AddTogglePair(b, w, 0, "restarts");
            AddTogglePair(b, w, 2, "consequences");
            AddTogglePair(b, w, 4, "tutorials");

            var back = w.transform.Find("Body/BackButton");
            if (back != null)
                b.AddItem(ControlId.Referenced(back.GetComponent<Button>(), "namerequest:back"),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => UiWidgets.LocalizedLabel(back.gameObject),
                                kind: AnnouncementKinds.Label),
                        },
                        OnActivate = () => UiWidgets.Click(back.gameObject),
                    });

            var start = (Button)NextButtonField.GetValue(w);
            b.AddItem(ControlId.Referenced(start, "namerequest:start"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => UiWidgets.LocalizedLabel(start.gameObject),
                        kind: AnnouncementKinds.Label),
                    // Gate on the MODEL (the game's min-length + blacklist rule mirrored into
                    // _isStartButtonLocked), not the button: the game also disables the button
                    // as its double-click guard after Start fires, and the live watch must not
                    // read that as "unavailable" mid-transition.
                    new NodeAnnouncement(
                        () => StartAllowed(w, field, minChars) ? null
                            : Loc.T("namerequest.start_unavailable"),
                        live: true, kind: AnnouncementKinds.Enabled),
                },
                OnActivate = () =>
                {
                    if (!StartAllowed(w, field, minChars))
                    {
                        // The game's own subtitle carries the reason (too short / blacklisted).
                        Mod.Speech.Speak(subtitle.text.Length > 0
                            ? subtitle.text : Loc.T("namerequest.start_unavailable"),
                            interrupt: true);
                        return;
                    }
                    UiWidgets.Click(start.gameObject); // honors the button's own interactable state
                },
            });
        }

        private static bool StartAllowed(NewGameWindowController w, TMPro.TMP_InputField field,
            int minChars)
            => field.text.Length >= minChars && !(bool)StartLockedField.GetValue(w);

        // One radio pair (a real ToggleGroup on Jerian): the two toggles as a left/right row
        // under the game's group header (the first label of the toggles' parent node),
        // region-jumpable. Clicking the selected one is skipped (the group would force it
        // back); the game's click path plays the toggle sound and sets the mode.
        private static void AddTogglePair(GraphBuilder b, NewGameWindowController w,
            int firstFieldIndex, string regionKey)
        {
            var pair = new List<Toggle>
            {
                (Toggle)PairFields[firstFieldIndex].GetValue(w),
                (Toggle)PairFields[firstFieldIndex + 1].GetValue(w),
            };
            pair.RemoveAll(t => t == null);
            pair.Sort((a, c) => a.transform.position.x.CompareTo(c.transform.position.x));
            if (pair.Count == 0) return;

            string title = UiWidgets.LocalizedLabel(pair[0].transform.parent.gameObject);
            b.PushContext(title);
            b.SetRegion(regionKey);
            b.StartRow();
            foreach (var t in pair)
            {
                var toggle = t;
                b.AddItem(
                    ControlId.Referenced(toggle,
                        "namerequest:" + regionKey + ":" + toggle.gameObject.name),
                    new NodeVtable
                    {
                        ControlType = ControlTypes.RadioButton,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => UiWidgets.LabelText(toggle.gameObject),
                                kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(
                                () => toggle.isOn ? Loc.T("state.selected") : null,
                                live: true, kind: AnnouncementKinds.Selected),
                        },
                        OnActivate = () =>
                        {
                            if (!toggle.isOn) UiWidgets.Click(toggle.gameObject);
                        },
                        StateText = () => toggle.isOn ? Loc.T("state.selected") : null,
                    });
            }
            b.EndRow();
            b.PopContext();
            b.SetRegion(null);
        }

        private void BeginEdit(TMPro.TMP_InputField field, TMPro.TextMeshProUGUI subtitle)
        {
            _editField = field;
            _editSubtitle = subtitle;
            _lastText = field.text;
            _lastSubtitle = subtitle == null ? null : subtitle.text;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            Mod.Speech.Speak(Loc.T("edit.start"), interrupt: true);
        }

        // The edit differ: echo is driven by the FIELD's text changing (model state), not our
        // keystrokes - paste, the game's auto-capitalization, and backspace all speak
        // correctly. The subtitle differ speaks the game's validation line the moment the game
        // swaps it. Interrupts are the house exception for feedback under key repeat.
        public override void OnUpdate()
        {
            bool editing = CapturesRawInput;
            if (editing)
            {
                var text = _editField.text;
                if (_wasEditing && text != _lastText)
                {
                    if (text.Length == _lastText.Length + 1 && text.StartsWith(_lastText))
                    {
                        var added = text[text.Length - 1];
                        Mod.Speech.Speak(added == ' ' ? Loc.T("edit.space") : added.ToString(),
                            interrupt: true);
                    }
                    else
                    {
                        Mod.Speech.Speak(text.Length > 0 ? text : Loc.T("edit.blank"),
                            interrupt: true);
                    }
                }
                _lastText = text;

                if (_editSubtitle != null)
                {
                    var sub = _editSubtitle.text;
                    if (_wasEditing && sub != _lastSubtitle && !string.IsNullOrEmpty(sub))
                        Mod.Speech.Speak(sub);
                    _lastSubtitle = sub;
                }
            }
            else if (_wasEditing)
            {
                // Edit ended (field commit/cancel, or the window closed): speak the resulting
                // name. A destroyed field means the window is gone - stay silent, the screen
                // pop announces the return.
                if (_editField != null)
                    Mod.Speech.Speak(_editField.text.Length > 0
                        ? _editField.text : Loc.T("edit.blank"), interrupt: true);
            }
            _wasEditing = editing;
        }
    }
}
