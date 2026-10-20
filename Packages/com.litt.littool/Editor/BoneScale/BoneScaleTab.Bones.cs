using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Littool.BoneScale
{
    // "본" section: the silhouette, the selected bone's panel (slider, value, X·Y·Z) and the bone list.
    internal sealed partial class BoneScaleTab
    {
        const float SilhouetteBoxWidth = 168f;
        const float SilhouetteBoxHeight = 300f;
        const float SliderMin = 0.5f;
        const float SliderMax = 2f;
        const float DotHit = 22f;

        [NonSerialized] ReorderableList m_BoneList;
        [NonSerialized] AdvancedDropdownState m_PickerState;

        sealed class BoneRow
        {
            public BoneGroup Group;
            public int Side; // -1 = both sides of a pair
            public string Label;
            public string Value;
            public bool Changed;
            public bool Missing;
        }

        sealed class DotInfo
        {
            public BoneSlot Slot;
            public bool Selected;
            public bool Linked;
            public bool Changed;
            public bool Missing;
            public string Tooltip;
        }

        sealed class OutfitLine
        {
            public string Text;
            public Tone Tone;
        }

        sealed class BoneView
        {
            public bool Symmetry;
            public bool MaInstalled;
            public readonly List<BoneRow> Rows = new List<BoneRow>();
            public readonly List<DotInfo> Dots = new List<DotInfo>();
            public int SelectedRow = -1;
            public BoneGroup Group;
            public int Side;
            public readonly List<BoneSlot> Targets = new List<BoneSlot>();
            public bool Split;
            public bool Missing;
            public bool BreastFields;
            public string Title;
            public string Kind;
            public string Path;
            public string SymNote;
            public readonly List<OutfitLine> OutfitLines = new List<OutfitLine>();
            public bool CanRemove;
        }

        // ---- Snapshot ------------------------------------------------------------------------------------

        void BuildBoneSnapshot(Snapshot snap)
        {
            var view = snap.Bones;
            view.Symmetry = m_Symmetry;
            view.MaInstalled = m_Outfits != null && m_Outfits.MaInstalled;
            var groups = m_Model.Groups;

            var group = m_Model.FindGroup(m_SelectedKey) ?? (groups.Count > 0 ? groups[0] : null);
            int side = group != null && group.Paired ? Mathf.Clamp(m_SelectedSide, 0, 1) : 0;
            if (group != null && group.Key != m_SelectedKey)
            {
                m_SelectedKey = group.Key;
                m_SelectedSide = side;
            }
            view.Group = group;
            view.Side = side;
            if (group != null) view.Targets.AddRange(TargetsOf(group, side));

            foreach (var g in groups)
            {
                if (g.Paired && !m_Symmetry)
                {
                    AddRow(view, g, 0);
                    AddRow(view, g, 1);
                }
                else
                {
                    AddRow(view, g, -1);
                }
            }
            for (int i = 0; i < view.Rows.Count; i++)
            {
                var row = view.Rows[i];
                if (row.Group == group && (row.Side < 0 || row.Side == side)) view.SelectedRow = i;
            }

            foreach (var g in groups)
            {
                if (!g.HasDots) continue;
                foreach (var slot in g.Slots)
                {
                    bool missing = slot.Transform == null;
                    bool selected = g == group && slot.Index == side;
                    view.Dots.Add(new DotInfo
                    {
                        Slot = slot,
                        Selected = selected,
                        Linked = !selected && view.Targets.Contains(slot),
                        Missing = missing,
                        Changed = !missing && !ScaleEditor.IsDefault(slot.Transform.localScale),
                        Tooltip = slot.Label + "  " + (missing ? L.T("bs.missing") : ScaleEditor.Format(slot.Transform.localScale)),
                    });
                }
            }

            view.CanRemove = group != null && Entries.Count > 1;
            if (group == null) return;

            view.Split = m_SplitKeys.Contains(group.Key);
            view.Missing = view.Targets.Exists(s => s.Transform == null);
            view.BreastFields = group.Key == "breast" && (group.Slots.Exists(s => s.Transform == null) || group.Slots.Exists(s => s.Kind == SlotKind.Manual));
            view.Title = view.Targets.Count == 2 ? group.Name + " L/R" : view.Targets[0].Label;
            view.Kind = KindText(view.Targets[0].Kind);
            view.Path = PathText(group, view.Targets);
            if (view.Targets.Count == 2 && !view.Missing)
                view.SymNote = L.F("bs.panel.symNote", view.Targets[0].Transform.name, view.Targets[1].Transform.name);
            BuildOutfitLines(view);
        }

        List<BoneSlot> TargetsOf(BoneGroup group, int side)
        {
            var list = new List<BoneSlot>();
            if (group.Paired && m_Symmetry) list.AddRange(group.Slots);
            else list.Add(group.Slots[Mathf.Clamp(side, 0, group.Slots.Count - 1)]);
            return list;
        }

        static void AddRow(BoneView view, BoneGroup group, int side)
        {
            var slots = side < 0 ? group.Slots : new List<BoneSlot> { group.Slots[side] };
            var row = new BoneRow
            {
                Group = group,
                Side = side,
                Label = side < 0 ? (group.Paired ? group.Name + " L/R" : group.Name) : group.Slots[side].Label,
            };
            bool allMissing = true;
            foreach (var slot in slots)
            {
                if (slot.Transform == null) continue;
                allMissing = false;
                if (!ScaleEditor.IsDefault(slot.Transform.localScale)) row.Changed = true;
            }
            row.Missing = allMissing;
            if (slots.Count == 2)
            {
                string left = ValueText(slots[0]);
                string right = ValueText(slots[1]);
                row.Value = left == right ? left : "L " + left + " · R " + right;
            }
            else
            {
                row.Value = ValueText(slots[0]);
            }
            view.Rows.Add(row);
        }

        static string ValueText(BoneSlot slot)
        {
            return slot.Transform == null ? L.T("bs.missing") : ScaleEditor.Format(slot.Transform.localScale);
        }

        static string KindText(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.ByName: return L.T("bs.kind.byName");
                case SlotKind.Manual: return L.T("bs.kind.manual");
                case SlotKind.Armature: return L.T("bs.kind.armature");
                default: return L.T("bs.kind.humanoid");
            }
        }

        string PathText(BoneGroup group, List<BoneSlot> targets)
        {
            var first = targets[0].Transform;
            if (first == null)
            {
                return group.IsCustom ? BoneCatalog.PathOf(group.Key).Replace("/", " › ") : L.T("bs.missing");
            }
            string path = AnimationUtility.CalculateTransformPath(first, m_Avatar.transform);
            var segments = path.Split('/');
            string text = segments.Length > 5
                ? "… › " + string.Join(" › ", segments, segments.Length - 4, 4)
                : string.Join(" › ", segments);
            if (targets.Count == 2 && targets[1].Transform != null) text += " / " + targets[1].Transform.name;
            return text;
        }

        void BuildOutfitLines(BoneView view)
        {
            if (m_Outfits == null || !m_Outfits.MaInstalled) return;
            var bones = new HashSet<Transform>();
            foreach (var slot in view.Targets)
            {
                if (slot.Transform != null) bones.Add(slot.Transform);
            }
            if (bones.Count == 0) return;
            var primary = view.Targets[0].Transform;

            foreach (var outfit in m_Outfits.Outfits)
            {
                if (outfit.Root == null) continue;
                BonePair firstPair = null;
                bool mismatched = false;
                foreach (var pair in outfit.Pairs)
                {
                    if (!bones.Contains(pair.AvatarBone)) continue;
                    if (firstPair == null) firstPair = pair;
                    if (ScaleEditor.Mismatched(pair)) mismatched = true;
                }
                if (firstPair == null) continue;

                var partNames = new List<string>();
                foreach (var part in outfit.Parts)
                {
                    if (part.Renderer == null) continue;
                    foreach (var bone in bones)
                    {
                        if (!part.AvatarBones.Contains(bone)) continue;
                        partNames.Add(part.Renderer.gameObject.name);
                        break;
                    }
                }
                string label = outfit.Root.name;
                if (partNames.Count > 0)
                {
                    const int shown = 3;
                    string parts = partNames.Count <= shown
                        ? string.Join(", ", partNames)
                        : string.Join(", ", partNames.GetRange(0, shown)) + L.F("common.more", partNames.Count - shown);
                    label += " (" + parts + ")";
                }

                bool on = outfit.Root.activeSelf;
                string status;
                Tone tone;
                if (mismatched)
                {
                    status = L.T("bs.line.mismatch");
                    tone = Tone.Warn;
                }
                else if (firstPair.Locked)
                {
                    status = L.T(on ? "bs.line.locked" : "bs.line.lockedOff");
                    tone = on ? Tone.Ok : Tone.Dim;
                }
                else
                {
                    status = L.F(on ? "bs.line.ratio" : "bs.line.ratioOff", primary != null ? ScaleEditor.Format(primary.localScale) : "?");
                    tone = on ? Tone.Normal : Tone.Dim;
                }
                view.OutfitLines.Add(new OutfitLine { Text = label + " — " + status, Tone = tone });
            }
        }

        // ---- Drawing --------------------------------------------------------------------------------------

        void DrawBones(Snapshot snap)
        {
            var view = snap.Bones;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("bs.bones.header"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var symContent = new GUIContent(L.T("bs.bones.symmetry"), L.T("bs.bones.symmetryTooltip"));
                EditorGUI.BeginChangeCheck();
                bool sym = EditorGUILayout.ToggleLeft(symContent, view.Symmetry, GUILayout.Width(EditorStyles.label.CalcSize(symContent).x + 20f));
                if (EditorGUI.EndChangeCheck())
                {
                    m_Symmetry = sym;
                    GUI.FocusControl(null);
                    Repaint();
                }
                var resetContent = new GUIContent(L.T("bs.bones.resetAll"), L.T("bs.bones.resetAllTooltip"));
                if (GUILayout.Button(resetContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false))) ResetAll();
            }
            GUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                var box = GUILayoutUtility.GetRect(SilhouetteBoxWidth, SilhouetteBoxHeight,
                    GUILayout.Width(SilhouetteBoxWidth), GUILayout.Height(SilhouetteBoxHeight));
                DrawSilhouette(box, view);
                GUILayout.Space(10f);
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(120f)))
                {
                    DrawPanel(view);
                }
            }
            GUILayout.Space(6f);
            DrawBoneList(view);
            GUILayout.Label(L.T("bs.bones.hint"), m_Styles.MiniDimWrap);
        }

        void DrawSilhouette(Rect box, BoneView view)
        {
            var evt = Event.current;
            var inner = Inflate(box, -1f);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(box, m_Styles.Edge);
                EditorGUI.DrawRect(inner, m_Styles.BoxBack);
                var figure = LittoolImages.Silhouette;
                if (figure != null)
                {
                    var figureRect = new Rect(inner.x, inner.y, BoneCatalog.SilhouetteSize.x, BoneCatalog.SilhouetteSize.y);
                    GUI.DrawTexture(figureRect, figure, ScaleMode.StretchToFill, true, 0f, m_Styles.Figure, 0f, 0f);
                }
                GUI.Label(new Rect(inner.x + 6f, inner.y + 2f, 14f, 14f), "R", MiniTone(Tone.Dim));
                GUI.Label(new Rect(inner.xMax - 14f, inner.y + 2f, 14f, 14f), "L", MiniTone(Tone.Dim));
                GUI.Label(new Rect(inner.x, inner.yMax - 18f, inner.width, 16f), L.T("bs.bones.front"), m_Styles.MiniCenter);
            }

            foreach (var dot in view.Dots)
            {
                var center = new Vector2(inner.x + dot.Slot.Dot.x, inner.y + dot.Slot.Dot.y);
                var hit = new Rect(center.x - DotHit * 0.5f, center.y - DotHit * 0.5f, DotHit, DotHit);
                if (evt.type == EventType.Repaint)
                {
                    DrawDot(center, dot);
                    GUI.Label(hit, new GUIContent(string.Empty, dot.Tooltip));
                }
                EditorGUIUtility.AddCursorRect(hit, MouseCursor.Link);
                if (evt.type == EventType.MouseDown && evt.button == 0 && hit.Contains(evt.mousePosition))
                {
                    Select(dot.Slot.Group, dot.Slot.Index, true);
                    evt.Use();
                }
            }
        }

        void DrawDot(Vector2 center, DotInfo dot)
        {
            float size = dot.Selected ? 13f : 10f;
            var rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            float alpha = dot.Missing ? 0.35f : 1f;
            if (dot.Selected || dot.Linked)
            {
                var halo = m_Styles.Halo;
                halo.a *= dot.Selected ? 1f : 0.5f;
                Circle(Inflate(rect, 3f), halo);
            }
            var fill = dot.Changed ? m_Styles.Accent : m_Styles.BoxBack;
            var ring = dot.Changed ? m_Styles.Accent : m_Styles.DotRing;
            fill.a *= alpha;
            ring.a *= alpha;
            Circle(rect, fill);
            Circle(rect, ring, 2f);
        }

        void DrawPanel(BoneView view)
        {
            if (view.Group == null)
            {
                GUILayout.Label(L.T("bs.panel.empty"), m_Styles.MiniDimWrap);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var title = new GUIContent(view.Title);
                GUILayout.Label(title, m_Styles.PanelTitle, GUILayout.MaxWidth(m_Styles.PanelTitle.CalcSize(title).x));
                var kind = new GUIContent(view.Kind);
                var kindSize = m_Styles.Badge.CalcSize(kind);
                var kindRect = GUILayoutUtility.GetRect(kindSize.x, 18f, GUILayout.Width(kindSize.x), GUILayout.Height(18f));
                if (Event.current.type == EventType.Repaint)
                {
                    var pill = new Rect(kindRect.x, kindRect.y + 2f, kindRect.width, 14f);
                    GUI.DrawTexture(pill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, m_Styles.BadgeBack, 0f, 7f);
                    m_Styles.Badge.Draw(pill, kind, false, false, false, false);
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.Label(view.Path, m_Styles.MiniDimWrap);

            if (view.BreastFields) DrawBreastFields(view);

            if (view.Missing)
            {
                GUILayout.Label(L.T(view.Group.Key == "breast" ? "bs.panel.breastMissing" : "bs.panel.missing"), m_Styles.MiniDimWrap);
            }
            else
            {
                if (view.Split)
                {
                    ScaleRow("X", 0, view.Targets);
                    ScaleRow("Y", 1, view.Targets);
                    ScaleRow("Z", 2, view.Targets);
                }
                else
                {
                    ScaleRow("Scale", -1, view.Targets);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    var splitContent = new GUIContent(L.T("bs.panel.split"), L.T("bs.panel.splitTooltip"));
                    EditorGUI.BeginChangeCheck();
                    bool split = EditorGUILayout.ToggleLeft(splitContent, view.Split, GUILayout.Width(EditorStyles.label.CalcSize(splitContent).x + 20f));
                    if (EditorGUI.EndChangeCheck()) SetSplit(view.Group, split);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(L.T("bs.panel.reset"), L.T("bs.panel.resetTooltip")), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        ApplyValue(view.Targets, -1, 1f, false);
                }
                if (view.SymNote != null) GUILayout.Label(view.SymNote, m_Styles.MiniDimWrap);
            }

            GUILayout.Space(3f);
            DrawSeparator();
            GUILayout.Space(1f);
            GUILayout.Label(L.T("bs.panel.outfits"), MiniTone(Tone.Dim));
            if (view.OutfitLines.Count == 0)
            {
                GUILayout.Label(L.T(view.MaInstalled ? "bs.panel.noOutfit" : "bs.panel.noMa"), MiniTone(Tone.Dim));
            }
            foreach (var line in view.OutfitLines)
            {
                GUILayout.Label(new GUIContent(line.Text, line.Text), MiniTone(line.Tone), GUILayout.MinWidth(0f));
            }
        }

        void DrawBreastFields(BoneView view)
        {
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 64f;
            for (int i = 0; i < view.Group.Slots.Count; i++)
            {
                var slot = view.Group.Slots[i];
                EditorGUI.BeginChangeCheck();
                var picked = (Transform)EditorGUILayout.ObjectField(new GUIContent("Breast " + slot.Side, L.T("bs.panel.breastTooltip")),
                    slot.Transform, typeof(Transform), true);
                if (EditorGUI.EndChangeCheck()) SetBreast(i, picked);
            }
            EditorGUIUtility.labelWidth = oldWidth;
        }

        /// <summary>One "Scale" (or X / Y / Z) row: a 0.5–2 slider and a number field that also takes values outside that range.</summary>
        void ScaleRow(string label, int axis, List<BoneSlot> targets)
        {
            var primary = targets[0].Transform;
            if (primary == null) return;
            var scale = primary.localScale;
            float value = axis < 0 ? scale.x : scale[axis];
            bool mixed = axis < 0 && !ScaleEditor.IsUniform(scale);
            for (int i = 1; i < targets.Count; i++)
            {
                var other = targets[i].Transform;
                if (other == null) continue;
                float otherValue = axis < 0 ? other.localScale.x : other.localScale[axis];
                if (Mathf.Abs(otherValue - value) > 0.0005f || (axis < 0 && !ScaleEditor.IsUniform(other.localScale))) mixed = true;
            }

            var row = EditorGUILayout.GetControlRect();
            const float labelWidth = 38f;
            const float fieldWidth = 54f;
            var labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            var fieldRect = new Rect(row.xMax - fieldWidth, row.y, fieldWidth, row.height);
            var sliderRect = new Rect(labelRect.xMax, row.y, Mathf.Max(10f, fieldRect.x - 6f - labelRect.xMax), row.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.BeginChangeCheck();
            float slid = GUI.HorizontalSlider(sliderRect, Mathf.Clamp(value, SliderMin, SliderMax), SliderMin, SliderMax);
            if (EditorGUI.EndChangeCheck())
            {
                BeginDrag();
                ApplyValue(targets, axis, Mathf.Round(slid * 100f) / 100f, true);
            }

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            float typed = EditorGUI.DelayedFloatField(fieldRect, value);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) ApplyValue(targets, axis, typed, false);
        }

        void DrawBoneList(BoneView view)
        {
            EnsureBoneList();
            m_BoneList.list = view.Rows;
            m_BoneList.index = view.SelectedRow;
            m_BoneList.DoLayoutList();
        }

        void EnsureBoneList()
        {
            if (m_BoneList != null) return;
            m_BoneList = new ReorderableList(new List<BoneRow>(), typeof(BoneRow), false, false, true, true)
            {
                elementHeight = 20f,
            };
            m_BoneList.drawElementCallback = DrawBoneRow;
            m_BoneList.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, L.T("bs.bones.none"));
            m_BoneList.onSelectCallback = list => OnBoneRowSelected(list.index);
            m_BoneList.onAddDropdownCallback = (buttonRect, list) => ShowAddMenu(buttonRect);
            m_BoneList.onRemoveCallback = list => RemoveSelected();
            m_BoneList.onCanRemoveCallback = list => m_Snap != null && m_Snap.Bones.CanRemove && !m_Snap.Playing;
            m_BoneList.onCanAddCallback = list => m_Snap != null && !m_Snap.Playing;
        }

        void DrawBoneRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            var rows = m_BoneList.list as List<BoneRow>;
            if (rows == null || index < 0 || index >= rows.Count) return;
            var row = rows[index];

            float middle = rect.y + rect.height * 0.5f;
            var dotRect = new Rect(rect.x + 2f, middle - 4f, 8f, 8f);
            if (Event.current.type == EventType.Repaint)
            {
                if (row.Changed) Circle(dotRect, m_Styles.Accent);
                else Circle(dotRect, m_Styles.DotRing, 1.5f);
            }

            var valueContent = new GUIContent(row.Value, row.Value);
            var valueStyle = MiniToneRight(row.Changed ? Tone.Accent : Tone.Dim);
            float valueWidth = Mathf.Min(valueStyle.CalcSize(valueContent).x, rect.width * 0.55f);
            var valueRect = new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            var labelRect = new Rect(dotRect.xMax + 8f, rect.y, Mathf.Max(0f, valueRect.x - 6f - (dotRect.xMax + 8f)), rect.height);
            EditorGUI.LabelField(labelRect, row.Label, row.Missing ? m_Styles.LabelDim : EditorStyles.label);
            GUI.Label(valueRect, valueContent, valueStyle);
        }

        // ---- Actions --------------------------------------------------------------------------------------

        void OnBoneRowSelected(int index)
        {
            if (m_Snap == null || index < 0 || index >= m_Snap.Bones.Rows.Count) return;
            var row = m_Snap.Bones.Rows[index];
            Select(row.Group, row.Side < 0 ? 0 : row.Side, true);
        }

        void Select(BoneGroup group, int side, bool flash)
        {
            if (group == null) return;
            m_SelectedKey = group.Key;
            m_SelectedSide = side;
            GUI.FocusControl(null); // a number field still being edited belongs to the previous bone
            if (flash)
            {
                var bones = new List<(Transform, Transform)>();
                foreach (var slot in TargetsOf(group, side))
                {
                    if (slot.Transform != null) bones.Add((slot.Transform, slot.Tip));
                }
                SceneFlash.Show(bones);
            }
            Repaint();
        }

        void SetSplit(BoneGroup group, bool split)
        {
            if (split)
            {
                if (!m_SplitKeys.Contains(group.Key)) m_SplitKeys.Add(group.Key);
            }
            else
            {
                m_SplitKeys.Remove(group.Key);
            }
            GUI.FocusControl(null);
            Repaint();
        }

        void SetBreast(int index, Transform picked)
        {
            if (picked != null && (m_Avatar == null || !picked.IsChildOf(m_Avatar.transform) || picked == m_Avatar.transform))
            {
                SetMessage(L.T("bs.panel.breastOutside"), MessageType.Warning);
                return;
            }
            if (index == 0) m_BreastL = picked;
            else m_BreastR = picked;
            m_ModelDirty = true;
            Repaint();
        }

        /// <summary>
        /// Writes one axis (0–2) or all three (-1) of the target bones. A drag joins the open drag Undo step;
        /// anything else is its own step.
        /// </summary>
        void ApplyValue(List<BoneSlot> targets, int axis, float value, bool drag)
        {
            value = ScaleEditor.ClampValue(value);
            var edits = new List<ScaleEditor.Edit>();
            bool any = false;
            foreach (var slot in targets)
            {
                var t = slot.Transform;
                if (t == null) continue;
                var next = t.localScale;
                if (axis < 0) next = new Vector3(value, value, value);
                else next[axis] = value;
                if (ScaleEditor.Differs(next, t.localScale)) any = true;
                edits.Add(new ScaleEditor.Edit(t, next));
            }
            if (!any) return;

            SceneFlash.Stop();
            int group = drag ? m_DragGroup : BeginUndo(ScaleEditor.UndoName);
            ScaleEditor.Apply(edits, m_Outfits);
            Undo.CollapseUndoOperations(group);
            OnValuesChanged(drag);
        }

        void OnValuesChanged(bool drag)
        {
            JustMatched.Clear();
            m_Message = null;
            if (!drag) MarkFloorDirty();
            Repaint();
        }

        void ResetAll()
        {
            var edits = new List<ScaleEditor.Edit>();
            foreach (var group in m_Model.Groups)
            {
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform != null && !ScaleEditor.IsDefault(slot.Transform.localScale))
                        edits.Add(new ScaleEditor.Edit(slot.Transform, Vector3.one));
                }
            }
            if (edits.Count == 0)
            {
                SetMessage(L.T("bs.bones.resetAllNothing"), MessageType.Info);
                return;
            }
            SceneFlash.Stop();
            int undoGroup = BeginUndo(ScaleEditor.UndoName);
            ScaleEditor.Apply(edits, m_Outfits);
            Undo.CollapseUndoOperations(undoGroup);
            OnValuesChanged(false);
            SetMessage(L.F("bs.bones.resetAllDone", edits.Count) + L.T("bs.undoHint"), MessageType.Info);
        }

        // ---- Adding and removing bones ------------------------------------------------------------------

        /// <summary>[+]: the standard groups not listed yet, and every bone of the Armature.</summary>
        void ShowAddMenu(Rect buttonRect)
        {
            if (m_Model == null || !m_Model.IsOk) return;
            var entries = Entries;
            var standard = new List<(string, string)>();
            foreach (var def in BoneCatalog.Groups)
            {
                if (!entries.Contains(def.Id)) standard.Add((def.Id, def.Paired ? def.Name + " L/R" : def.Name));
            }
            if (m_PickerState == null) m_PickerState = new AdvancedDropdownState();
            var dropdown = new BonePickerDropdown(m_PickerState, standard, m_Model.Armature,
                t => m_Model != null && m_Model.SlotOf.ContainsKey(t), key => AddEntry(key), OnBonePicked);
            dropdown.Show(buttonRect);
        }

        void OnBonePicked(Transform picked)
        {
            if (picked == null || m_Model == null || !m_Model.IsOk) return;

            // A bone of a standard group picked from the tree adds that group instead.
            foreach (var def in BoneCatalog.Groups)
            {
                if (def.ByName)
                {
                    if (picked == m_Model.BreastLByName || picked == m_Model.BreastRByName)
                    {
                        AddEntry(def.Id, picked == m_Model.BreastRByName ? 1 : 0);
                        return;
                    }
                    continue;
                }
                if (picked == m_Model.HumanBone(def.Left))
                {
                    AddEntry(def.Id, 0);
                    return;
                }
                if (def.Right != HumanBodyBones.LastBone && picked == m_Model.HumanBone(def.Right))
                {
                    AddEntry(def.Id, 1);
                    return;
                }
            }
            AddEntry(BoneCatalog.CustomKey(AnimationUtility.CalculateTransformPath(picked, m_Avatar.transform)));
        }

        void AddEntry(string key, int side = 0)
        {
            var entries = Entries;
            if (!entries.Contains(key))
            {
                entries.Add(key);
                LittoolSettings.instance.SaveSettings();
            }
            m_SelectedKey = key;
            m_SelectedSide = side;
            m_ModelDirty = true;
            Repaint();
        }

        /// <summary>[−]: removes the selected bone from the list only; its values stay as they are.</summary>
        void RemoveSelected()
        {
            var entries = Entries;
            int index = entries.IndexOf(m_SelectedKey);
            if (index < 0 || entries.Count <= 1) return;
            entries.RemoveAt(index);
            m_SplitKeys.Remove(m_SelectedKey);
            LittoolSettings.instance.SaveSettings();
            m_SelectedKey = entries[Mathf.Min(index, entries.Count - 1)];
            m_SelectedSide = 0;
            m_ModelDirty = true;
            SceneFlash.Stop();
            Repaint();
        }
    }
}
