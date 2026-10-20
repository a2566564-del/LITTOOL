using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    // "의상" section: the outfits dressed with MA Merge Armature, their on/off switch, parts and matching.
    internal sealed partial class BoneScaleTab
    {
        const float OutfitRowHeight = 20f;
        const float PartRowHeight = 18f;
        const int MaxRememberedOutfits = 256;
        const string OutfitUndoName = "LITTOOL Outfit On/Off";

        sealed class OutfitView
        {
            public bool MaInstalled;
            public bool AutoMatch;
            public readonly List<OutfitRow> Rows = new List<OutfitRow>();
        }

        sealed class OutfitRow
        {
            public Outfit Outfit;
            public OutfitPart Part; // null for the outfit's own row
            public string Name;
            public string Tag;
            public Tone TagTone;
            public string Tooltip;
            public bool On;
            public bool Open;
            public bool ShowFix;
            public bool Active;

            public bool IsPart => Part != null;
        }

        void BuildOutfitSnapshot(Snapshot snap)
        {
            var view = snap.Outfits;
            view.MaInstalled = m_Outfits != null && m_Outfits.MaInstalled;
            view.AutoMatch = LittoolSettings.instance.AutoMatchNewOutfits;
            if (!view.MaInstalled) return;

            var listed = ListedAvatarBones();
            var floorRef = m_Floor != null && m_Floor.Valid ? m_Floor.Lowest : null;
            foreach (var outfit in m_Outfits.Outfits)
            {
                if (outfit.Root == null) continue;
                bool on = outfit.Root.activeSelf;
                var row = new OutfitRow
                {
                    Outfit = outfit,
                    Name = outfit.Root.name,
                    On = on,
                    Open = m_ExpandedOutfits.Contains(outfit.Id),
                    Tooltip = AnimationUtility.CalculateTransformPath(outfit.Root.transform, m_Avatar.transform),
                };

                int mismatched = CountMismatch(outfit, listed, out var first);
                if (JustMatched.TryGetValue(outfit.Id, out int matched))
                {
                    row.Tag = L.F("bs.outfit.tagMatched", matched);
                    row.TagTone = Tone.Accent;
                }
                else if (mismatched > 0)
                {
                    row.Tag = MismatchText(first, mismatched);
                    row.TagTone = Tone.Warn;
                    row.ShowFix = true;
                }
                else
                {
                    bool locked = outfit.AnyLocked;
                    if (on) row.Tag = L.T(locked ? "bs.outfit.tagLocked" : "bs.outfit.tagUnlocked");
                    else row.Tag = L.T(locked ? "bs.outfit.tagOffLocked" : "bs.outfit.tagOffUnlocked");
                    row.TagTone = !on ? Tone.Dim : (locked ? Tone.Ok : Tone.Normal);
                }
                view.Rows.Add(row);
                if (!row.Open) continue;

                foreach (var part in outfit.Parts)
                {
                    if (part.Renderer == null) continue;
                    string info = PartBones(part);
                    bool isFloorRef = floorRef != null && part.Renderer == floorRef;
                    if (isFloorRef) info = string.IsNullOrEmpty(info) ? L.T("bs.part.floorRef") : info + " · " + L.T("bs.part.floorRef");
                    view.Rows.Add(new OutfitRow
                    {
                        Outfit = outfit,
                        Part = part,
                        Name = part.Renderer.gameObject.name,
                        Tag = string.IsNullOrEmpty(info) ? L.T("bs.part.noBones") : info,
                        TagTone = isFloorRef ? Tone.Accent : Tone.Dim,
                        Tooltip = AnimationUtility.CalculateTransformPath(part.Renderer.transform, m_Avatar.transform),
                        On = on,
                        Active = part.Renderer.gameObject.activeInHierarchy,
                    });
                }
            }
        }

        /// <summary>How many listed avatar bones this outfit does not match; <paramref name="first"/> = the first in list order.</summary>
        int CountMismatch(Outfit outfit, HashSet<Transform> listed, out BonePair first)
        {
            first = null;
            int firstOrder = int.MaxValue;
            var bones = new HashSet<Transform>();
            foreach (var pair in outfit.Pairs)
            {
                if (!listed.Contains(pair.AvatarBone) || !ScaleEditor.Mismatched(pair)) continue;
                bones.Add(pair.AvatarBone);
                int order = OrderOf(pair.AvatarBone);
                if (order < firstOrder)
                {
                    firstOrder = order;
                    first = pair;
                }
            }
            return bones.Count;
        }

        int OrderOf(Transform bone)
        {
            if (m_Model == null || bone == null || !m_Model.SlotOf.TryGetValue(bone, out var slot)) return int.MaxValue;
            return m_Model.Groups.IndexOf(slot.Group) * 2 + slot.Index;
        }

        string SlotLabel(Transform bone)
        {
            if (m_Model != null && bone != null && m_Model.SlotOf.TryGetValue(bone, out var slot)) return slot.Label;
            return bone != null ? bone.name : "?";
        }

        string MismatchText(BonePair pair, int count)
        {
            if (pair == null) return string.Empty;
            string text = L.F("bs.outfit.tagMismatch", SlotLabel(pair.AvatarBone),
                ScaleEditor.Format(pair.Merge.localScale), ScaleEditor.Format(pair.Base.localScale));
            if (count > 1) text += L.F("common.more", count - 1);
            return text;
        }

        /// <summary>The listed bones a part is bound to, in list order ("Head, Neck, Breast").</summary>
        string PartBones(OutfitPart part)
        {
            var names = new List<string>();
            foreach (var group in m_Model.Groups)
            {
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform == null || !part.AvatarBones.Contains(slot.Transform)) continue;
                    names.Add(group.Name);
                    break;
                }
            }
            return string.Join(", ", names);
        }

        void DrawOutfits(Snapshot snap)
        {
            var view = snap.Outfits;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("bs.outfit.header"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (view.MaInstalled)
                {
                    var content = new GUIContent(L.T("bs.outfit.autoMatch"), L.T("bs.outfit.autoMatchTooltip"));
                    EditorGUI.BeginChangeCheck();
                    bool auto = EditorGUILayout.ToggleLeft(content, view.AutoMatch, GUILayout.Width(EditorStyles.label.CalcSize(content).x + 20f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        LittoolSettings.instance.AutoMatchNewOutfits = auto;
                        Repaint();
                    }
                }
            }

            if (!view.MaInstalled)
            {
                EditorGUILayout.HelpBox(L.T("bs.outfit.noMa"), MessageType.Info);
                return;
            }
            if (view.Rows.Count == 0)
            {
                EditorGUILayout.HelpBox(L.T("bs.outfit.none"), MessageType.Info);
                return;
            }

            DrawOutfitList(view);
            GUILayout.Label(L.T("bs.outfit.checkNote"), m_Styles.MiniDimWrap);
            GUILayout.Label(L.T(view.AutoMatch ? "bs.outfit.autoOn" : "bs.outfit.autoOff"), m_Styles.MiniDimWrap);
        }

        void DrawOutfitList(OutfitView view)
        {
            float height = 4f;
            foreach (var row in view.Rows) height += row.IsPart ? PartRowHeight : OutfitRowHeight;
            var outer = GUILayoutUtility.GetRect(0f, height + 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(outer, m_Styles.Edge);
                EditorGUI.DrawRect(Inflate(outer, -1f), m_Styles.BoxBack);
            }

            EditorGUIUtility.SetIconSize(new Vector2(16f, 16f));
            float y = outer.y + 3f;
            foreach (var row in view.Rows)
            {
                float rowHeight = row.IsPart ? PartRowHeight : OutfitRowHeight;
                var rect = new Rect(outer.x + 1f, y, outer.width - 2f, rowHeight);
                if (row.IsPart) DrawPartRow(rect, row);
                else DrawOutfitRow(rect, row);
                y += rowHeight;
            }
            EditorGUIUtility.SetIconSize(Vector2.zero);
        }

        void DrawOutfitRow(Rect rect, OutfitRow row)
        {
            var evt = Event.current;
            var outfit = row.Outfit;
            if (outfit.Root == null) return;

            var foldRect = new Rect(rect.x + 4f, rect.y + 1f, 14f, rect.height - 2f);
            bool open = EditorGUI.Foldout(foldRect, row.Open, GUIContent.none, true);
            if (open != row.Open) SetOutfitExpanded(outfit.Id, open);

            var toggleRect = new Rect(rect.x + 20f, rect.y + 2f, 16f, 16f);
            bool on = EditorGUI.Toggle(toggleRect, row.On);
            if (evt.type == EventType.Repaint) GUI.Label(toggleRect, new GUIContent(string.Empty, L.T("bs.outfit.toggleTooltip")));
            if (on != row.On) SetOutfitActive(outfit, on);

            float right = rect.xMax - 6f;
            if (row.ShowFix)
            {
                var fixContent = new GUIContent(L.T("bs.outfit.fix"), L.T("bs.outfit.fixTooltip"));
                float width = EditorStyles.miniButton.CalcSize(fixContent).x + 4f;
                var fixRect = new Rect(right - width, rect.y + 2f, width, rect.height - 4f);
                if (GUI.Button(fixRect, fixContent, EditorStyles.miniButton)) MatchOutfit(outfit);
                right = fixRect.x - 6f;
            }

            var tagStyle = MiniToneRight(row.TagTone);
            var tagContent = new GUIContent(row.Tag, row.Tag);
            float tagWidth = Mathf.Min(tagStyle.CalcSize(tagContent).x, Mathf.Max(0f, (right - rect.x - 40f) * 0.62f));
            var tagRect = new Rect(right - tagWidth, rect.y, tagWidth, rect.height);
            var nameRect = new Rect(rect.x + 40f, rect.y, Mathf.Max(0f, tagRect.x - 6f - (rect.x + 40f)), rect.height);

            GUI.Label(nameRect, new GUIContent(row.Name, m_Styles.ObjectIcon, row.Tooltip), row.On ? EditorStyles.label : m_Styles.LabelDim);
            GUI.Label(tagRect, tagContent, tagStyle);

            if (evt.type == EventType.MouseDown && evt.button == 0 && nameRect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.PingObject(outfit.Root);
                if (evt.clickCount >= 2) Selection.activeGameObject = outfit.Root;
                evt.Use();
            }
        }

        void DrawPartRow(Rect rect, OutfitRow row)
        {
            var evt = Event.current;
            var renderer = row.Part.Renderer;
            if (renderer == null) return;

            if (evt.type == EventType.Repaint && rect.Contains(evt.mousePosition)) EditorGUI.DrawRect(rect, m_Styles.Hover);
            var icon = EditorGUIUtility.ObjectContent(null, renderer.GetType()).image;
            var nameContent = new GUIContent(row.Name, icon, row.Tooltip);
            float x = rect.x + 58f;
            var nameStyle = row.Active ? EditorStyles.label : m_Styles.LabelDim;
            float nameWidth = Mathf.Min(nameStyle.CalcSize(nameContent).x, Mathf.Max(0f, (rect.xMax - x) * 0.5f));
            var nameRect = new Rect(x, rect.y, nameWidth, rect.height);
            var infoRect = new Rect(nameRect.xMax + 6f, rect.y, Mathf.Max(0f, rect.xMax - 6f - (nameRect.xMax + 6f)), rect.height);

            GUI.Label(nameRect, nameContent, nameStyle);
            GUI.Label(infoRect, new GUIContent(row.Tag, row.Tag), MiniToneRight(row.Active ? row.TagTone : Tone.Dim));

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.PingObject(renderer.gameObject);
                if (evt.clickCount >= 2) Selection.activeGameObject = renderer.gameObject;
                evt.Use();
            }
        }

        void SetOutfitExpanded(int id, bool open)
        {
            if (open)
            {
                if (!m_ExpandedOutfits.Contains(id)) m_ExpandedOutfits.Add(id);
                if (m_ExpandedOutfits.Count > MaxRememberedOutfits) m_ExpandedOutfits.RemoveRange(0, m_ExpandedOutfits.Count - MaxRememberedOutfits);
            }
            else
            {
                m_ExpandedOutfits.Remove(id);
            }
            Repaint();
        }

        /// <summary>The checkbox switches the outfit object itself on or off, like the Inspector's checkbox.</summary>
        void SetOutfitActive(Outfit outfit, bool on)
        {
            var go = outfit.Root;
            if (go == null || go.activeSelf == on) return;
            Undo.RecordObject(go, OutfitUndoName);
            go.SetActive(on);
            MarkFloorDirty();
            Repaint();
        }

        static IEnumerable<BonePair> ListedPairs(Outfit outfit, HashSet<Transform> listed)
        {
            foreach (var pair in outfit.Pairs)
            {
                if (listed.Contains(pair.AvatarBone)) yield return pair;
            }
        }

        /// <summary>[맞추기]: copy the avatar's current values to this outfit's matching bones.</summary>
        void MatchOutfit(Outfit outfit)
        {
            if (outfit.Root == null) return;
            var listed = ListedAvatarBones();
            int group = BeginUndo(ScaleEditor.MatchUndoName);
            int count = ScaleEditor.Match(ListedPairs(outfit, listed));
            Undo.CollapseUndoOperations(group);
            if (count > 0)
            {
                JustMatched[outfit.Id] = count;
                SetMessage(L.F("bs.fix.done", outfit.Root.name, count) + L.T("bs.undoHint"), MessageType.Info);
                MarkFloorDirty();
            }
            Repaint();
        }

        /// <summary>New outfits found while the window is open get the avatar's current values on the listed bones.</summary>
        void AutoMatch(List<Outfit> outfits)
        {
            var listed = ListedAvatarBones();
            var names = new List<string>();
            int total = 0;
            int group = BeginUndo(ScaleEditor.MatchUndoName);
            foreach (var outfit in outfits)
            {
                if (outfit.Root == null) continue;
                int count = ScaleEditor.Match(ListedPairs(outfit, listed));
                if (count == 0) continue;
                JustMatched[outfit.Id] = count;
                names.Add(outfit.Root.name);
                total += count;
            }
            Undo.CollapseUndoOperations(group);
            if (names.Count == 0) return;
            SetMessage(L.F("bs.auto.done", string.Join(", ", names), total) + L.T("bs.undoHint"), MessageType.Info);
            MarkFloorDirty();
        }
    }
}
