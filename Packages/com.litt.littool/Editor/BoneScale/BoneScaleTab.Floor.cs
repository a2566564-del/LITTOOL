using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    // "바닥 맞추기" section: how far the soles sink below the floor, and the MA Floor Adjuster button.
    internal sealed partial class BoneScaleTab
    {
        const float FloorTolerance = 0.005f; // 0.5 cm
        const double FloorSettleSeconds = 0.3; // MA updates locked outfit bones a moment after a change

        [NonSerialized] FloorMeter.Result m_Floor;
        [NonSerialized] bool m_FloorDirty = true;
        [NonSerialized] double m_FloorDirtyTime;

        sealed class FloorView
        {
            public bool MaInstalled;
            public bool Supported;
            public string Status = string.Empty;
            public bool StatusOk;
            public string Extra;
            public string Button = string.Empty;
            public bool ButtonEnabled;
            public string Note = string.Empty;
        }

        void MarkFloorDirty()
        {
            m_FloorDirty = true;
            m_FloorDirtyTime = EditorApplication.timeSinceStartup;
        }

        void OnEditorUpdate()
        {
            if (!m_FloorDirty || m_Dragging || m_Window == null) return;
            if (EditorApplication.timeSinceStartup - m_FloorDirtyTime < FloorSettleSeconds) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                m_FloorDirty = false;
                return;
            }
            // Wait until the next GUI pass has rebuilt the avatar after a change (or the tab was first drawn).
            if (m_Model == null || m_ModelDirty) return;
            m_FloorDirty = false;
            MeasureFloor();
            Repaint();
        }

        void MeasureFloor()
        {
            if (m_Avatar == null || m_Model == null || !m_Model.IsOk)
            {
                m_Floor = null;
                return;
            }
            try
            {
                m_Floor = FloorMeter.Measure(m_Avatar);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                m_Floor = null;
            }
        }

        void BuildFloorSnapshot(Snapshot snap)
        {
            var view = snap.Floor;
            view.MaInstalled = MaBridge.Installed;
            view.Supported = MaBridge.FloorAdjusterType != null;

            int active = 0;
            var adjusters = view.Supported ? FloorMeter.FindAdjusters(m_Avatar, out active) : new List<Component>();
            var adjuster = adjusters.Count > 0 ? adjusters[0] : null;
            if (active > 1) view.Extra = L.F("bs.floor.multiple", active);

            var result = m_Floor;
            if (result == null || !result.Valid)
            {
                view.Status = L.T(result == null ? "bs.floor.measuring" : "bs.floor.nothing");
                view.StatusOk = false;
                view.Button = L.T(adjuster != null ? "bs.floor.move" : "bs.floor.place");
                view.ButtonEnabled = false;
            }
            else
            {
                float sink = -result.LowestHeight; // metres below the avatar root (negative = floating)
                string where = WhereText(result.Lowest);
                if (adjuster == null)
                {
                    bool ok = Mathf.Abs(sink) < FloorTolerance;
                    view.StatusOk = ok;
                    view.Status = ok ? L.T("bs.floor.okNoAdjuster") : L.F(sink > 0f ? "bs.floor.below" : "bs.floor.above", where, Cm(sink));
                    view.Button = L.T("bs.floor.place");
                    view.ButtonEnabled = !ok && view.Supported;
                }
                else
                {
                    float adjusterHeight = FloorMeter.HeightOf(m_Avatar.transform, adjuster.transform.position);
                    float off = result.LowestHeight - adjusterHeight;
                    bool ok = Mathf.Abs(off) < FloorTolerance;
                    view.StatusOk = ok;
                    view.Status = ok
                        ? L.F(sink >= 0f ? "bs.floor.okAdjusterBelow" : "bs.floor.okAdjusterAbove", Cm(sink))
                        : L.F("bs.floor.off", where, Cm(off));
                    view.Button = L.T("bs.floor.move");
                    view.ButtonEnabled = !ok && view.Supported;
                }
            }

            if (!view.MaInstalled) view.Note = L.T("bs.floor.noMa");
            else if (!view.Supported) view.Note = L.F("bs.floor.oldMa", string.IsNullOrEmpty(MaBridge.Version) ? "?" : MaBridge.Version);
            else if (adjuster != null) view.Note = L.F("bs.floor.noteMove", adjuster.gameObject.name);
            else view.Note = L.T("bs.floor.notePlace");
        }

        string WhereText(Renderer renderer)
        {
            if (renderer == null) return "?";
            if (m_Outfits != null)
            {
                foreach (var outfit in m_Outfits.Outfits)
                {
                    if (outfit.Root != null && renderer.transform.IsChildOf(outfit.Root.transform))
                        return outfit.Root.name + " › " + renderer.gameObject.name;
                }
            }
            return renderer.gameObject.name;
        }

        static string Cm(float metres)
        {
            return (Mathf.Abs(metres) * 100f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        void DrawFloor(Snapshot snap)
        {
            var view = snap.Floor;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("bs.floor.header"), EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                GUILayout.Label(new GUIContent(L.T("bs.floor.basis"), L.T("bs.floor.basis")), MiniToneRight(Tone.Dim), GUILayout.MinWidth(0f));
                if (GUILayout.Button(m_Styles.RefreshContent, EditorStyles.miniButton, GUILayout.Width(26f)))
                {
                    m_FloorDirty = false;
                    MeasureFloor();
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var iconRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
                if (Event.current.type == EventType.Repaint) DrawStatusIcon(iconRect, view.StatusOk);
                GUILayout.Label(view.Status, m_Styles.StatusText, GUILayout.MinWidth(0f));
            }
            if (!string.IsNullOrEmpty(view.Extra)) EditorGUILayout.HelpBox(view.Extra, MessageType.Warning);

            using (new EditorGUI.DisabledScope(!view.ButtonEnabled))
            {
                if (GUILayout.Button(view.Button, GUILayout.Height(24f)))
                {
                    PlaceFloor();
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.Label(view.Note, m_Styles.MiniDimWrap);
        }

        /// <summary>A green check in a circle when the floor is right, the console warning icon when it is not.</summary>
        void DrawStatusIcon(Rect rect, bool ok)
        {
            if (!ok)
            {
                if (m_Styles.WarnIcon != null) GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, 16f, 16f), m_Styles.WarnIcon, ScaleMode.ScaleToFit);
                return;
            }
            var circle = new Rect(rect.x + 2f, rect.y + 2f, 14f, 14f);
            Circle(circle, m_Styles.OkColor, 1.4f);
            var corner = new Vector2(circle.x + 6.1f, circle.y + 9.8f);
            Stroke(new Vector2(circle.x + 3.5f, circle.y + 7.2f), corner, 1.8f, m_Styles.OkColor);
            Stroke(corner, new Vector2(circle.x + 10.7f, circle.y + 4.4f), 1.8f, m_Styles.OkColor);
        }

        /// <summary>A rounded line from <paramref name="a"/> to <paramref name="b"/>, drawn with the GUI matrix (so it follows scrolling).</summary>
        static void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            var bar = new Rect(a.x - width * 0.5f, a.y - width * 0.5f, length + width, width);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, width * 0.5f);
            GUI.matrix = matrix;
        }

        void PlaceFloor()
        {
            if (m_Avatar == null || MaBridge.FloorAdjusterType == null) return;
            MeasureFloor(); // measure again so the floor goes exactly where the soles are now
            m_FloorDirty = false;
            if (m_Floor == null || !m_Floor.Valid) return;

            var adjusters = FloorMeter.FindAdjusters(m_Avatar, out _);
            var existing = adjusters.Count > 0 ? adjusters[0] : null;
            Component placed;
            try
            {
                placed = FloorMeter.Place(m_Avatar, existing, m_Floor.LowestHeight);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetMessage(L.T("bs.floor.failed"), MessageType.Error);
                return;
            }
            if (placed == null)
            {
                SetMessage(L.T("bs.floor.failed"), MessageType.Error);
                return;
            }
            string depth = Cm(m_Floor.LowestHeight);
            string key = existing != null ? "bs.floor.moved" : "bs.floor.placed";
            if (m_Floor.LowestHeight > 0f) key += "Above";
            SetMessage(L.F(key, placed.gameObject.name, depth) + L.T("bs.undoHint"), MessageType.Info);
            EditorGUIUtility.PingObject(placed.gameObject);
            Repaint();
        }
    }
}
