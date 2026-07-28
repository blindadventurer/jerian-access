using JerianAccess.Module.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using _Scripts.Tooltips;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// Reads the game's OWN hover tooltip for a control - the detail a sighted player gets by
    /// resting the cursor on it (a parameter's scale, a character's status, an article, a lock's
    /// conditions, a map region). Jerian routes all of that through TooltipZoneView: the zone
    /// broadcasts, a per-type controller builds a view prefab into the tooltip container one
    /// frame later, and the rendered view IS the game's localized output for that type. So rather
    /// than modelling ten tooltip types, the mod asks the zone to open (the same entry the zone's
    /// keyboard selection uses), waits for the view to settle, speaks it, and closes it again.
    ///
    /// One request at a time, ticked from the module pump: a tooltip cannot be read synchronously
    /// because the game builds it after a WaitForEndOfFrame.
    /// </summary>
    public static class TooltipReader
    {
        private const float SettleSeconds = 0.1f;
        private const float GiveUpSeconds = 1.5f;

        // Live references for one in-flight request; never content (the text is re-read per tick).
        private static TooltipZoneView _open;
        private static float _asked;
        private static string _pending;
        private static float _pendingSince;

        /// <summary>Does this control have a game tooltip to read?</summary>
        public static bool Available(GameObject go) => Zone(go) != null;

        /// <summary>Ask for this control's tooltip. The answer is spoken when the game has built
        /// it (or the no-details line when it builds nothing). False when the control has no
        /// tooltip zone at all - the caller speaks its own fallback.</summary>
        public static bool Request(GameObject go)
        {
            var zone = Zone(go);
            if (zone == null) return false;
            Close();
            _open = zone;
            _asked = Time.unscaledTime;
            _pending = null;
            // The zone's own keyboard entry: OnSelect with a non-pointer event is what the game
            // itself calls when a selectable takes focus without a mouse.
            zone.OnSelect(new BaseEventData(EventSystem.current));
            return true;
        }

        /// <summary>Per-frame: speak the tooltip once its text stops changing.</summary>
        public static void Tick()
        {
            if (_open == null) return;
            var text = Text();
            if (!string.IsNullOrEmpty(text) && text == _pending
                && Time.unscaledTime - _pendingSince >= SettleSeconds)
            {
                Mod.Speech.Speak(text);
                Close();
                return;
            }
            if (text != _pending)
            {
                _pending = text;
                _pendingSince = Time.unscaledTime;
                return;
            }
            // The game builds nothing for this zone (a type with no configuration, or tooltips
            // suppressed during an animation): answer rather than leaving the key silent.
            if (Time.unscaledTime - _asked >= GiveUpSeconds)
            {
                Mod.Speech.Speak(Loc.T("nav.no_details"));
                Close();
            }
        }

        /// <summary>Drop an in-flight request (screen change): the game's tooltip is closed the
        /// way a pointer leaving the zone closes it.</summary>
        public static void Close()
        {
            if (_open == null) return;
            var zone = _open;
            _open = null;
            if (zone.gameObject.activeInHierarchy)
                zone.OnDeselect(new BaseEventData(EventSystem.current));
        }

        // The rendered tooltip. Every controller instantiates its view into the manager's own
        // container, so one sweep covers all ten types.
        private static string Text()
        {
            var manager = Object.FindObjectOfType<TooltipControllersManager>();
            if (manager == null || manager.TooltipViewsContainer == null) return null;
            return PanelText.Read(manager.TooltipViewsContainer.gameObject);
        }

        // The zone belonging to this control: on it, inside it, above it, or - the common case in
        // the windows - BESIDE it, because the game lays the hover area over a row half while the
        // label is its own object. The one a mouse pointing at the label would be inside wins;
        // failing that, the nearest one on the row.
        private static TooltipZoneView Zone(GameObject go)
        {
            if (go == null) return null;
            var own = Live(go.GetComponent<TooltipZoneView>())
                ?? Live(go.GetComponentInChildren<TooltipZoneView>(false))
                ?? Live(go.GetComponentInParent<TooltipZoneView>());
            if (own != null) return own;

            // The zone a mouse pointing AT this label would be inside - nothing looser: a zone
            // that merely sits nearby belongs to another row, and answering with its tooltip
            // would state something false about the focused item.
            var point = (Vector2)go.transform.position;
            for (var row = go.transform.parent; row != null; row = row.parent)
                foreach (var zone in row.GetComponentsInChildren<TooltipZoneView>(false))
                {
                    if (Live(zone) == null) continue;
                    var rect = zone.transform as RectTransform;
                    if (rect != null && Contains(rect, point)) return zone;
                }
            return null;
        }

        private static TooltipZoneView Live(TooltipZoneView zone)
            => zone != null && zone.enabled && zone.gameObject.activeInHierarchy ? zone : null;

        private static bool Contains(RectTransform rect, Vector2 point)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return point.x >= corners[0].x && point.x <= corners[2].x
                && point.y >= corners[0].y && point.y <= corners[2].y;
        }
    }
}
