using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Littool.MaterialBatch
{
    /// <summary>
    /// "Material Batch" tab: copy the listed lilToon values from a reference (a material or a lilToon preset)
    /// to the checked materials under a target object (outfit, hair, accessory...).
    ///
    /// IMGUI note: anything that changes how many controls are drawn is decided once per event,
    /// during EventType.Layout (scan/evaluation results and the m_Snap* fields), so data that changes
    /// in the middle of an event never desynchronises GUILayout.
    /// </summary>
    [Serializable]
    internal sealed class MaterialBatchTab : ILittoolTab
    {
        const float RowHeight = 20f;
        const float IssueRowHeight = 34f;
        const float MaxListHeight = 160f;
        const float MaxIssueHeight = IssueRowHeight * 6f + 4f;
        const int MaxExcludedRows = 4096;

        [SerializeField] int m_Mode; // ReferenceMode
        [SerializeField] Material m_Reference;
        [SerializeField] string m_PresetGuid;
        [SerializeField] GameObject m_Target;
        [SerializeField] List<string> m_ExcludedRows = new List<string>(); // unchecked "renderer:material" rows
        [SerializeField] Vector2 m_ListScroll;
        [SerializeField] Vector2 m_IssueScroll;
        [SerializeField] bool m_IssuesExpanded = true;
        [SerializeField] List<int> m_ExpandedParts = new List<int>();

        [NonSerialized] EditorWindow m_Window;
        [NonSerialized] ReorderableList m_List;
        [NonSerialized] AdvancedDropdownState m_DropdownState;
        [NonSerialized] AdvancedDropdownState m_PresetDropdownState;
        [NonSerialized] HashSet<string> m_ExcludedSet;
        [NonSerialized] ScanResult m_Scan;
        [NonSerialized] EvalResult m_Eval;
        [NonSerialized] bool m_ScanDirty;
        [NonSerialized] bool m_EvalDirty;
        [NonSerialized] bool m_LilInstalled;
        [NonSerialized] bool m_LilCheckDirty;
        [NonSerialized] Material m_Highlight;
        [NonSerialized] string m_Result;
        [NonSerialized] MessageType m_ResultType;
        [NonSerialized] string m_CopyResult;
        [NonSerialized] MessageType m_CopyResultType;
        [NonSerialized] Styles m_Styles;
        [NonSerialized] List<Row> m_Rows;

        // Snapshot taken at EventType.Layout (see class comment).
        [NonSerialized] ReferenceMode m_SnapMode;
        [NonSerialized] int m_SnapEntryCount;
        [NonSerialized] bool m_SnapIssuesExpanded;
        [NonSerialized] string m_SnapResult;
        [NonSerialized] MessageType m_SnapResultType;
        [NonSerialized] string m_SnapCopyResult;
        [NonSerialized] MessageType m_SnapCopyResultType;
        [NonSerialized] int m_SnapPresetCount;
        [NonSerialized] bool m_SnapPlaying;

        struct Row
        {
            public PartInfo Part;
            public bool IsHeader;
            public Material Material; // only for material rows
        }

        public string Title => L.T("tab.materialBatch");

        static List<string> Entries => LittoolSettings.instance.MaterialBatchEntries;

        ReferenceMode Mode => m_Mode == (int)ReferenceMode.Preset ? ReferenceMode.Preset : ReferenceMode.Material;

        // ------------------------------------------------------------------------------------------------
        // ILittoolTab

        public void OnEnable(EditorWindow window)
        {
            m_Window = window;
            if (m_ExpandedParts == null) m_ExpandedParts = new List<int>();
            if (m_ExcludedRows == null) m_ExcludedRows = new List<string>();
            m_ExcludedSet = null;
            m_List = null;
            m_Scan = null;
            m_Eval = null;
            m_LilCheckDirty = true;
            LittoolSettings.instance.SanitizeMaterialBatchEntries();
        }

        public void OnDisable()
        {
            m_Window = null;
        }

        public void OnHierarchyChange()
        {
            // Play Mode tools (Av3Emulator, Gesture Manager...) change the hierarchy constantly;
            // applying is disabled there anyway, so rescan once when back in Edit Mode instead.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (m_Target != null || (m_Scan != null && m_Scan.HasRoot)) MarkScanDirty();
        }

        public void OnPlayModeChanged()
        {
            MarkScanDirty();
        }

        public void OnProjectChange()
        {
            m_LilCheckDirty = true;
            MaterialScanner.ClearCache();
            LilToonPresets.ClearCache();
            MarkScanDirty();
        }

        public void OnUndoRedo()
        {
            MarkScanDirty();
        }

        public void OnLanguageChanged()
        {
            m_Result = null;
            m_CopyResult = null;
            m_DropdownState = null;
            m_PresetDropdownState = null;
            m_Styles = null; // labels and tooltips are localized
            Repaint();
        }

        // ------------------------------------------------------------------------------------------------
        // GUI

        public void OnGUI()
        {
            if (m_Styles == null || !m_Styles.IsValid) m_Styles = new Styles();
            var entries = Entries;

            if (Event.current.type == EventType.Layout || m_Scan == null || m_Eval == null)
            {
                var mode = Mode;
                if (m_LilCheckDirty)
                {
                    m_LilInstalled = LilToonUtil.IsInstalled();
                    m_LilCheckDirty = false;
                }
                if (m_Scan == null || m_ScanDirty)
                {
                    m_Scan = MaterialScanner.Scan(m_Target, mode == ReferenceMode.Material ? m_Reference : null);
                    m_ScanDirty = false;
                    m_EvalDirty = true;
                }
                if (m_Eval == null || m_EvalDirty || m_Eval.Mode != mode)
                {
                    m_Eval = MaterialScanner.Evaluate(m_Scan, CurrentReference(), entries, IsRowSelected);
                    m_EvalDirty = false;
                }
                m_SnapMode = mode;
                m_SnapEntryCount = entries.Count;
                m_SnapIssuesExpanded = m_IssuesExpanded;
                m_SnapResult = m_Result;
                m_SnapResultType = m_ResultType;
                m_SnapCopyResult = m_CopyResult;
                m_SnapCopyResultType = m_CopyResultType;
                m_SnapPresetCount = mode == ReferenceMode.Preset ? LilToonPresets.All.Count : 0;
                m_SnapPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            }

            if (!m_LilInstalled) EditorGUILayout.HelpBox(L.T("lil.notInstalled"), MessageType.Warning);

            DrawReference();
            GUILayout.Space(10f);
            DrawEntries(entries);
            GUILayout.Space(10f);
            DrawSeparator();
            GUILayout.Space(6f);
            DrawTarget();
            GUILayout.Space(10f);
            DrawApply();
            GUILayout.Space(6f);
        }

        ReferenceInput CurrentReference()
        {
            var mode = Mode;
            if (mode == ReferenceMode.Preset) return new ReferenceInput(mode, null, LilToonPresets.FindByGuid(m_PresetGuid));
            return new ReferenceInput(mode, m_Reference, null);
        }

        // ---- Reference (material or preset) -----------------------------------------------------------

        void DrawReference()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("ref.header"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                int mode = GUILayout.Toolbar((int)Mode, m_Styles.ModeLabels, EditorStyles.miniButton, GUI.ToolbarButtonSize.FitToContents);
                if (EditorGUI.EndChangeCheck() && mode != (int)Mode)
                {
                    m_Mode = mode;
                    m_Result = null;
                    m_CopyResult = null;
                    m_Highlight = null;
                    MarkScanDirty(); // the reference material is marked in the scan
                }
            }

            if (m_SnapMode == ReferenceMode.Preset) DrawPresetReference();
            else DrawMaterialReference();
        }

        void DrawMaterialReference()
        {
            EditorGUI.BeginChangeCheck();
            var material = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Reference Material"), m_Reference, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                m_Reference = material;
                m_Result = null;
                m_CopyResult = null;
                m_Highlight = null;
                MarkScanDirty(); // the reference may also be one of the targets
            }

            switch (m_Eval.ReferenceState)
            {
                case ReferenceState.None:
                    EditorGUILayout.HelpBox(L.T("ref.none"), MessageType.Info);
                    break;
                case ReferenceState.ShaderMissing:
                    EditorGUILayout.HelpBox(L.T("ref.shaderMissing"), MessageType.Error);
                    break;
                case ReferenceState.NotLilToon:
                    EditorGUILayout.HelpBox(L.F("ref.notLil", m_Eval.ReferenceShaderName), MessageType.Error);
                    break;
                default:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 2f);
                        GUILayout.Label(L.F("ref.shader", m_Eval.ReferenceShaderName), m_Styles.MiniDimLine, GUILayout.MinWidth(0f));
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(m_SnapPlaying || !ReferenceCopier.CanCopy(m_Reference)))
                        {
                            if (GUILayout.Button(m_Styles.CopyContent, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                            {
                                DoCopyReference();
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                    if (m_Eval.ReferenceMissing > 0)
                        EditorGUILayout.HelpBox(L.F("ref.missingProps", m_Eval.ReferenceMissing), MessageType.Warning);
                    break;
            }

            if (!string.IsNullOrEmpty(m_SnapCopyResult)) EditorGUILayout.HelpBox(m_SnapCopyResult, m_SnapCopyResultType);
        }

        /// <summary>"Copy without textures": a "&lt;name&gt;_Ref" copy next to the reference, which then becomes the reference.</summary>
        void DoCopyReference()
        {
            var source = m_Reference;
            if (!ReferenceCopier.CanCopy(source)) return;

            int textures = ReferenceCopier.CountTextures(source);
            string path = ReferenceCopier.SuggestPath(source);
            string copyName = path != null ? System.IO.Path.GetFileNameWithoutExtension(path) : ReferenceCopier.FileNameFor(source);
            if (!EditorUtility.DisplayDialog(L.T("copy.confirmTitle"), L.F("copy.confirmBody", source.name, copyName, textures), L.T("copy.ok"), L.T("common.cancel"))) return;

            if (path == null)
            {
                // The reference's folder cannot be written to (e.g. a read-only package): ask where to save.
                path = EditorUtility.SaveFilePanelInProject(L.T("copy.pickTitle"), copyName, "mat", L.T("copy.pickMessage"), "Assets");
                if (string.IsNullOrEmpty(path)) return;
            }

            try
            {
                var copy = ReferenceCopier.CreateCopy(source, path, out int cleared);
                m_Reference = copy;
                m_CopyResult = L.F("copy.done", AssetDatabase.GetAssetPath(copy), cleared, source.name);
                m_CopyResultType = MessageType.Info;
                EditorGUIUtility.PingObject(copy);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                m_CopyResult = L.T("copy.failed");
                m_CopyResultType = MessageType.Error;
            }
            m_Result = null;
            m_Highlight = null;
            MarkScanDirty();
        }

        void DrawPresetReference()
        {
            var preset = m_Eval.Preset;
            var rect = EditorGUILayout.GetControlRect(true);
            var fieldRect = EditorGUI.PrefixLabel(rect, new GUIContent("Reference Preset"));
            var content = preset != null
                ? new GUIContent(preset.Label, preset.Path)
                : new GUIContent(L.T("preset.select"));
            if (EditorGUI.DropdownButton(fieldRect, content, FocusType.Keyboard, EditorStyles.popup))
            {
                ShowPresetDropdown(fieldRect);
            }
            HandlePresetDrop(fieldRect);

            if (m_SnapPresetCount == 0)
            {
                EditorGUILayout.HelpBox(L.T("preset.noneInProject"), MessageType.Warning);
                return;
            }
            if (m_Eval.ReferenceState != ReferenceState.Ok || preset == null)
            {
                EditorGUILayout.HelpBox(L.T("preset.pick"), MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2f);
                string origin = L.T(preset.IsBuiltIn ? "preset.builtInLong" : "preset.userLong");
                var info = new GUIContent(L.F("preset.info", preset.FileName, preset.Values.Count, origin), L.T("preset.infoTooltip"));
                if (GUILayout.Button(info, m_Styles.MiniDimLine, GUILayout.MinWidth(0f)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(preset.Path);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }
                GUILayout.FlexibleSpace();
            }
            if (m_Eval.ReferenceMissing > 0)
                EditorGUILayout.HelpBox(L.F("preset.missing", m_Eval.ReferenceMissing), MessageType.Warning);
        }

        void ShowPresetDropdown(Rect rect)
        {
            if (m_PresetDropdownState == null) m_PresetDropdownState = new AdvancedDropdownState();
            var dropdown = new PresetDropdown(m_PresetDropdownState, LilToonPresets.All, m_PresetGuid, OnPresetPicked);
            dropdown.Show(rect);
        }

        /// <summary>A preset asset dragged from the Project window onto the field selects it.</summary>
        void HandlePresetDrop(Rect rect)
        {
            var evt = Event.current;
            if ((evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) || !rect.Contains(evt.mousePosition)) return;

            UnityEngine.Object dropped = null;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj != null && obj.GetType().Name == LilToonPresets.TypeName)
                {
                    dropped = obj;
                    break;
                }
            }
            if (dropped == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dropped));
                if (!string.IsNullOrEmpty(guid))
                {
                    LilToonPresets.ClearCache(); // it may have been imported a moment ago
                    OnPresetPicked(guid);
                }
            }
            evt.Use();
        }

        void OnPresetPicked(string guid)
        {
            if (guid == m_PresetGuid) return;
            m_PresetGuid = guid;
            m_Result = null;
            MarkEvalDirty();
        }

        // ---- Properties to apply ------------------------------------------------------------------------

        void DrawEntries(List<string> entries)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("items.header"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L.T("items.reset"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    string defaults = string.Join(", ", LilToonCatalog.DefaultEntries.Select(LilToonCatalog.GetDisplayName));
                    if (EditorUtility.DisplayDialog(L.T("items.resetTitle"), L.F("items.resetBody", defaults), L.T("common.ok"), L.T("common.cancel")))
                    {
                        LittoolSettings.instance.ResetMaterialBatchEntries();
                        if (m_List != null) m_List.index = -1;
                        OnEntriesChanged();
                    }
                    GUIUtility.ExitGUI();
                }
            }

            EnsureList(entries);
            m_List.DoLayoutList();

            EditorGUILayout.HelpBox(L.T(m_SnapMode == ReferenceMode.Preset ? "preset.itemsInfo" : "items.info"), MessageType.Info);
            if (m_SnapEntryCount == 0) EditorGUILayout.HelpBox(L.T("items.empty"), MessageType.Warning);
            if (m_Eval.IncludesMainColor) EditorGUILayout.HelpBox(L.T("items.colorWarn"), MessageType.Warning);
        }

        void EnsureList(List<string> entries)
        {
            if (m_List != null && ReferenceEquals(m_List.list, entries)) return;

            m_List = new ReorderableList(entries, typeof(string), true, false, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            };
            m_List.drawElementCallback = DrawEntry;
            m_List.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, L.T("items.none"));
            m_List.onAddDropdownCallback = (buttonRect, list) => ShowDropdown(buttonRect);
            m_List.onRemoveCallback = list =>
            {
                var current = Entries;
                int index = list.index;
                if (index < 0 || index >= current.Count) return;
                current.RemoveAt(index);
                list.index = Mathf.Min(index, current.Count - 1);
                OnEntriesChanged();
            };
            m_List.onReorderCallback = list => OnEntriesChanged();
        }

        void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
        {
            var entries = Entries;
            if (index < 0 || index >= entries.Count) return;
            string key = entries[index];
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            // Preset mode: how many of the entry's values the preset stores ("26/27", "Not in preset").
            bool notInPreset = false;
            if (m_Eval != null && m_Eval.Mode == ReferenceMode.Preset && m_Eval.Coverage.TryGetValue(key, out var stored))
            {
                notInPreset = stored.x == 0;
                GUIContent coverage;
                GUIStyle coverageStyle = m_Styles.MiniRight;
                if (notInPreset)
                {
                    coverage = new GUIContent(L.T("cov.none"));
                    coverageStyle = m_Styles.TagWarn;
                }
                else if (LilToonCatalog.IsPropertyKey(key))
                {
                    coverage = new GUIContent(L.T("cov.has"));
                }
                else
                {
                    coverage = new GUIContent(L.F("cov.count", stored.x, stored.y));
                }
                float width = coverageStyle.CalcSize(coverage).x;
                GUI.Label(new Rect(rect.xMax - width, rect.y, width, rect.height), coverage, coverageStyle);
                rect.width = Mathf.Max(0f, rect.width - width - 8f);
            }
            var nameStyle = notInPreset ? m_Styles.LabelDim : EditorStyles.label;

            if (LilToonCatalog.TryGetSection(key, out var section))
            {
                EditorGUI.LabelField(rect, new GUIContent(section.Name, L.F("items.sectionTooltip", section.Properties.Count)), nameStyle);
            }
            else if (LilToonCatalog.TryGetPropertyByKey(key, out var property))
            {
                const float typeWidth = 64f;
                var nameRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - typeWidth - 4f), rect.height);
                var typeRect = new Rect(rect.xMax - typeWidth, rect.y, typeWidth, rect.height);
                EditorGUI.LabelField(nameRect, new GUIContent(property.Name, property.Section != null ? property.Section.Name : string.Empty), nameStyle);
                EditorGUI.LabelField(typeRect, property.TypeLabel, m_Styles.MiniRight);
            }
            else
            {
                EditorGUI.LabelField(rect, key);
            }
        }

        void ShowDropdown(Rect buttonRect)
        {
            if (m_DropdownState == null) m_DropdownState = new AdvancedDropdownState();
            var dropdown = new PropertyDropdown(m_DropdownState, new HashSet<string>(Entries), OnDropdownPicked);
            dropdown.Show(buttonRect);
        }

        void OnDropdownPicked(string key)
        {
            var entries = Entries;
            if (LilToonCatalog.TryGetSection(key, out var section))
            {
                // A whole section replaces its individual properties already in the list.
                entries.RemoveAll(k => LilToonCatalog.TryGetPropertyByKey(k, out var p) && p.Section == section);
                if (!entries.Contains(key)) entries.Add(key);
            }
            else if (LilToonCatalog.TryGetPropertyByKey(key, out var property))
            {
                bool sectionListed = property.Section != null && entries.Contains(property.Section.Id);
                if (!sectionListed && !entries.Contains(key)) entries.Add(key);
            }
            if (m_List != null) m_List.index = entries.IndexOf(key);
            OnEntriesChanged();
        }

        void OnEntriesChanged()
        {
            LittoolSettings.instance.SaveSettings();
            m_Result = null;
            MarkEvalDirty();
        }

        // ---- Target -----------------------------------------------------------------------------------

        void DrawTarget()
        {
            GUILayout.Label(L.T("target.header"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var target = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Target Object"), m_Target, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    m_Target = target;
                    m_Result = null;
                    m_Highlight = null;
                    m_ListScroll = Vector2.zero;
                    ClearSelection(); // a new target starts with everything checked
                    MarkScanDirty();
                }
                if (GUILayout.Button(m_Styles.RescanContent, EditorStyles.miniButton,
                        GUILayout.Width(26f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    // A full rescan also forgets cached read-only results and the preset list
                    // (e.g. the user removed a file's read-only attribute outside Unity).
                    MaterialScanner.ClearCache();
                    LilToonPresets.ClearCache();
                    m_LilCheckDirty = true;
                    MarkScanDirty();
                }
            }

            if (!m_Scan.HasRoot)
            {
                EditorGUILayout.HelpBox(L.T("target.none"), MessageType.Info);
                return;
            }
            if (m_Scan.Parts.Count == 0)
            {
                EditorGUILayout.HelpBox(L.T("target.noRenderers"), MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("target.detected"));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L.T("target.selectAll"), EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false))) SelectAll(true);
                if (GUILayout.Button(L.T("target.selectNone"), EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false))) SelectAll(false);
            }
            GUILayout.Label(L.F("target.summary", m_Eval.PartCount, m_Eval.SelectedPartCount, m_Eval.ApplyCount, m_Eval.SkipCount), m_Styles.MiniDim);

            DrawPartList();
            DrawIssues();
        }

        void BuildRows()
        {
            if (m_Rows == null) m_Rows = new List<Row>();
            m_Rows.Clear();
            foreach (var part in m_Scan.Parts)
            {
                m_Rows.Add(new Row { Part = part, IsHeader = true });
                if (!IsExpanded(part)) continue;
                foreach (var material in part.Slots) m_Rows.Add(new Row { Part = part, Material = material });
            }
        }

        void DrawPartList()
        {
            BuildRows();
            float contentHeight = m_Rows.Count * RowHeight + 4f;
            bool needsScrollbar = contentHeight > MaxListHeight;
            float height = needsScrollbar ? MaxListHeight : contentHeight;
            var outer = GUILayoutUtility.GetRect(0f, height + 2f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(outer, m_Styles.ListEdge);
                EditorGUI.DrawRect(new Rect(outer.x + 1f, outer.y + 1f, outer.width - 2f, outer.height - 2f), m_Styles.ListBack);
            }

            var inner = new Rect(outer.x + 1f, outer.y + 1f, outer.width - 2f, outer.height - 2f);
            var view = ScrollViewRect(inner, contentHeight, needsScrollbar);

            m_ListScroll = GUI.BeginScrollView(inner, m_ListScroll, view);
            EditorGUIUtility.SetIconSize(new Vector2(16f, 16f)); // component icons can be larger than a row
            for (int i = 0; i < m_Rows.Count; i++)
            {
                var rowRect = new Rect(0f, 2f + i * RowHeight, view.width, RowHeight);
                if (m_Rows[i].IsHeader) DrawPartRow(rowRect, m_Rows[i].Part);
                else DrawMaterialRow(rowRect, m_Rows[i].Part, m_Rows[i].Material);
            }
            EditorGUIUtility.SetIconSize(Vector2.zero);
            GUI.EndScrollView();
        }

        void DrawPartRow(Rect rect, PartInfo part)
        {
            bool expanded = IsExpanded(part);
            string name = part.Renderer != null ? part.Renderer.gameObject.name : part.Name;
            Texture icon = part.Renderer != null ? EditorGUIUtility.ObjectContent(null, part.Renderer.GetType()).image : null;

            int selected = 0;
            foreach (var material in part.Slots)
            {
                if (IsRowSelected(part, material)) selected++;
            }
            bool allOn = selected == part.Slots.Count;
            bool none = selected == 0;

            // Part checkbox: checked = every material, "-" = some, empty = none.
            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 16f, 16f);
            EditorGUI.showMixedValue = !allOn && !none;
            bool next = EditorGUI.Toggle(toggleRect, allOn);
            EditorGUI.showMixedValue = false;
            if (next != allOn) SetPartSelected(part, next);

            // Right side, laid out right to left: material count, issue icon, "Excluded".
            float right = rect.xMax - 6f;
            var countContent = new GUIContent(L.F("target.matCount", part.Slots.Count));
            float countWidth = m_Styles.MiniRight.CalcSize(countContent).x;
            var countRect = new Rect(right - countWidth, rect.y, countWidth, rect.height);
            right = countRect.x - 4f;

            bool hasIcon = part.Worst != IssueLevel.None;
            var iconRect = new Rect(right - 16f, rect.y + 2f, 16f, 16f);
            if (hasIcon) right = iconRect.x - 4f;

            GUIContent excludedContent = null;
            var excludedRect = new Rect();
            if (none)
            {
                excludedContent = new GUIContent(L.T("tag.excluded"));
                float width = m_Styles.TagInfo.CalcSize(excludedContent).x;
                excludedRect = new Rect(right - width, rect.y + 1f, width, rect.height - 2f);
                right = excludedRect.x - 6f;
            }

            var foldRect = new Rect(rect.x + 22f, rect.y + 1f, Mathf.Max(0f, right - (rect.x + 22f)), rect.height - 2f);
            bool nextExpanded = EditorGUI.Foldout(foldRect, expanded, new GUIContent(name, icon, part.Path), true, none ? m_Styles.FoldoutDim : EditorStyles.foldout);
            if (nextExpanded != expanded) SetExpanded(part, nextExpanded);

            GUI.Label(countRect, countContent, m_Styles.MiniRight);
            if (excludedContent != null) GUI.Label(excludedRect, excludedContent, m_Styles.TagInfo);
            if (hasIcon && Event.current.type == EventType.Repaint)
            {
                var issueIcon = part.Worst == IssueLevel.Error ? m_Styles.ErrorIcon : m_Styles.WarnIcon;
                if (issueIcon != null) GUI.DrawTexture(iconRect, issueIcon, ScaleMode.ScaleToFit);
            }
        }

        void DrawMaterialRow(Rect rect, PartInfo part, Material material)
        {
            var evt = Event.current;
            if (material == null)
            {
                // Deleted after the last scan; the next scan removes the row.
                GUI.Label(new Rect(rect.x + 64f, rect.y, Mathf.Max(0f, rect.width - 70f), rect.height), "(Missing)", m_Styles.LabelDim);
                return;
            }
            m_Scan.Lookup.TryGetValue(material, out var info);
            bool rowSelected = IsRowSelected(part, material);

            if (evt.type == EventType.Repaint)
            {
                if (material == m_Highlight) EditorGUI.DrawRect(rect, m_Styles.Highlight);
                else if (rect.Contains(evt.mousePosition)) EditorGUI.DrawRect(rect, m_Styles.Hover);
            }

            // Material checkbox. It handles its own clicks, so the row click below does not fire.
            var toggleRect = new Rect(rect.x + 24f, rect.y + 2f, 16f, 16f);
            bool next = EditorGUI.Toggle(toggleRect, rowSelected);
            if (next != rowSelected) SetRowSelected(part, material, next, true);

            // A material changes when any checked row uses it (the file is shared by every part using it).
            bool changes = info != null && info.WillApply;

            // Tags on the right, drawn right to left.
            float right = rect.xMax - 6f;
            if (info != null)
            {
                if (info.IsReference)
                {
                    right = DrawTag(rect, right, L.T("tag.reference"), m_Styles.TagInfo);
                }
                else if (rowSelected)
                {
                    if (info.Tag.HasValue)
                        right = DrawTag(rect, right, TagText(info.Tag.Value), MaterialScanner.LevelOf(info.Tag.Value) == IssueLevel.Error ? m_Styles.TagError : m_Styles.TagWarn);
                }
                else if (changes)
                {
                    right = DrawTag(rect, right, TagText(IssueKind.SharedChange), m_Styles.TagWarn);
                }
                if (info.UseCount > 1) right = DrawTag(rect, right, L.T("tag.shared"), m_Styles.TagInfo);
            }

            var iconRect = new Rect(rect.x + 44f, rect.y + 2f, 16f, 16f);
            if (evt.type == EventType.Repaint)
            {
                var thumbnail = AssetPreview.GetMiniThumbnail(material);
                if (thumbnail != null) GUI.DrawTexture(iconRect, thumbnail, ScaleMode.ScaleToFit);
            }

            var nameRect = new Rect(rect.x + 64f, rect.y, Mathf.Max(0f, right - (rect.x + 64f) - 4f), rect.height);
            bool dim = info != null && !info.IsReference && !changes;
            string tooltip = (info != null ? info.ShaderName : string.Empty) + "\n" + L.T("target.rowTooltip");
            GUI.Label(nameRect, new GUIContent(material.name, tooltip), dim ? m_Styles.LabelDim : EditorStyles.label);

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.PingObject(material);
                if (evt.clickCount >= 2) Selection.activeObject = material;
                m_Highlight = material;
                evt.Use();
            }
        }

        float DrawTag(Rect row, float right, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            float width = style.CalcSize(content).x;
            var tagRect = new Rect(right - width, row.y + 1f, width, row.height - 2f);
            GUI.Label(tagRect, content, style);
            return tagRect.x - 6f;
        }

        void DrawIssues()
        {
            if (m_Eval.Issues.Count == 0) return;
            GUILayout.Space(4f);

            var header = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(header, m_Styles.ListEdge);
                EditorGUI.DrawRect(new Rect(header.x + 1f, header.y + 1f, header.width - 2f, header.height - 2f), m_Styles.IssueBack);
            }

            const float countsWidth = 150f;
            var foldRect = new Rect(header.x + 6f, header.y + 2f, Mathf.Max(0f, header.width - countsWidth - 10f), 18f);
            m_IssuesExpanded = EditorGUI.Foldout(foldRect, m_IssuesExpanded, L.F("issues.header", m_Eval.Issues.Count), true, m_Styles.BoldFoldout);

            var countsRect = new Rect(header.xMax - countsWidth - 6f, header.y + 2f, countsWidth, 18f);
            DrawIssueCounts(countsRect);

            if (!m_SnapIssuesExpanded) return;

            float contentHeight = m_Eval.Issues.Count * IssueRowHeight + 4f;
            bool needsScrollbar = contentHeight > MaxIssueHeight;
            float listHeight = needsScrollbar ? MaxIssueHeight : contentHeight;
            var outer = GUILayoutUtility.GetRect(0f, listHeight + 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(outer, m_Styles.ListEdge);
                EditorGUI.DrawRect(new Rect(outer.x + 1f, outer.y + 1f, outer.width - 2f, outer.height - 2f), m_Styles.IssueBack);
            }

            var inner = new Rect(outer.x + 1f, outer.y + 1f, outer.width - 2f, outer.height - 2f);
            var view = ScrollViewRect(inner, contentHeight, needsScrollbar);
            m_IssueScroll = GUI.BeginScrollView(inner, m_IssueScroll, view);
            for (int i = 0; i < m_Eval.Issues.Count; i++)
            {
                var rowRect = new Rect(0f, 2f + i * IssueRowHeight, view.width, IssueRowHeight);
                DrawIssueRow(rowRect, m_Eval.Issues[i]);
            }
            GUI.EndScrollView();

            GUILayout.Label(L.T("issues.hint"), m_Styles.MiniDim);
        }

        void DrawIssueCounts(Rect rect)
        {
            EditorGUIUtility.SetIconSize(new Vector2(14f, 14f));
            var warnContent = new GUIContent(L.F("issues.warnings", m_Eval.WarningCount), m_Styles.WarnIcon);
            var errorContent = new GUIContent(L.F("issues.errors", m_Eval.ErrorCount), m_Styles.ErrorIcon);
            float warnWidth = m_Styles.MiniIconLabel.CalcSize(warnContent).x;
            float errorWidth = m_Styles.MiniIconLabel.CalcSize(errorContent).x;
            var warnRect = new Rect(rect.xMax - warnWidth, rect.y, warnWidth, rect.height);
            var errorRect = new Rect(warnRect.x - errorWidth - 8f, rect.y, errorWidth, rect.height);
            GUI.Label(errorRect, errorContent, m_Styles.MiniIconLabel);
            GUI.Label(warnRect, warnContent, m_Styles.MiniIconLabel);
            EditorGUIUtility.SetIconSize(Vector2.zero);
        }

        void DrawIssueRow(Rect rect, Issue issue)
        {
            var evt = Event.current;
            if (evt.type == EventType.Repaint && rect.Contains(evt.mousePosition)) EditorGUI.DrawRect(rect, m_Styles.Hover);

            bool isError = issue.Level == IssueLevel.Error;
            var levelRect = new Rect(rect.x + 6f, rect.y + 2f, 52f, 16f);
            GUI.Label(levelRect, L.T(isError ? "level.error" : "level.warning"), isError ? m_Styles.LevelError : m_Styles.LevelWarn);

            var info = issue.Info;
            var part = issue.Part ?? info.FirstPart;
            string where = (part != null ? part.Path : m_Scan.RootName) + "  ›  " + (info.Material != null ? info.Material.name : "(missing)");
            if (info.UseCount > 1 && issue.Kind != IssueKind.SharedChange) where += L.F("common.more", info.UseCount - 1);
            var line1 = new Rect(rect.x + 60f, rect.y + 2f, Mathf.Max(0f, rect.width - 66f), 16f);
            var line2 = new Rect(rect.x + 60f, rect.y + 17f, Mathf.Max(0f, rect.width - 66f), 15f);
            GUI.Label(line1, where, EditorStyles.label);
            GUI.Label(line2, IssueText(issue), m_Styles.MiniDim);

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                Locate(issue);
                evt.Use();
            }
        }

        void Locate(Issue issue)
        {
            var info = issue.Info;
            var part = issue.Part ?? info.FirstPart;
            if (part != null && part.Renderer != null)
            {
                Selection.activeGameObject = part.Renderer.gameObject;
                EditorGUIUtility.PingObject(part.Renderer.gameObject);
            }
            if (part != null) SetExpanded(part, true);
            m_Highlight = info.Material;

            // Scroll the detected list so the material row is visible.
            BuildRows();
            int index = m_Rows.FindIndex(r => !r.IsHeader && r.Part == part && ReferenceEquals(r.Material, info.Material));
            if (index >= 0)
            {
                float y = 2f + index * RowHeight;
                m_ListScroll.y = Mathf.Max(0f, y - (MaxListHeight - RowHeight) * 0.5f);
            }
            Repaint();
        }

        // ---- Apply ------------------------------------------------------------------------------------

        void DrawApply()
        {
            if (m_SnapPlaying) EditorGUILayout.HelpBox(L.T("apply.playMode"), MessageType.Warning);

            bool canApply = !m_SnapPlaying
                            && m_Eval.ReferenceState == ReferenceState.Ok
                            && m_SnapEntryCount > 0
                            && m_Eval.Properties.Count > 0
                            && m_Scan.HasRoot
                            && m_Eval.ApplyCount > 0;

            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button(L.F("apply.button", m_Eval.ApplyCount), GUILayout.Height(28f)))
                {
                    DoApply();
                    GUIUtility.ExitGUI();
                }
            }

            if (!string.IsNullOrEmpty(m_SnapResult)) EditorGUILayout.HelpBox(m_SnapResult, m_SnapResultType);
        }

        void DoApply()
        {
            var targets = m_Scan.Materials.Where(i => i.WillApply && i.Material != null && !i.IsReference).ToList();
            if (targets.Count == 0)
            {
                SetResult(L.T("apply.nothing"), MessageType.Warning);
                return;
            }

            bool fromPreset = m_Eval.Mode == ReferenceMode.Preset;
            PresetInfo preset = null;
            if (fromPreset)
            {
                preset = LilToonPresets.FindByGuid(m_PresetGuid);
                if (preset == null)
                {
                    SetResult(L.T("preset.pick"), MessageType.Warning);
                    return;
                }
            }
            else if (m_Reference == null)
            {
                SetResult(L.T("ref.none"), MessageType.Warning);
                return;
            }

            string referenceName = fromPreset ? preset.Label : m_Reference.name;
            int skipped = m_Eval.SkipCount;
            string body = L.F("apply.confirmBody", targets.Count,
                L.F(fromPreset ? "apply.srcPreset" : "apply.srcMaterial", referenceName),
                skipped > 0 ? L.F("apply.confirmSkip", skipped) : string.Empty);
            if (!EditorUtility.DisplayDialog(L.T("apply.confirmTitle"), body, L.T("apply.ok"), L.T("common.cancel"))) return;

            // Read the values now, so edits made to the reference since the last scan are included.
            List<SourceValue> values;
            Material exclude = null;
            if (fromPreset)
            {
                values = ReferenceValues.FromPreset(LilToonPresets.Reload(preset), m_Eval.Properties);
            }
            else
            {
                values = ReferenceValues.FromMaterial(m_Reference, m_Eval.Properties);
                exclude = m_Reference;
            }
            if (values.Count == 0)
            {
                SetResult(L.T("apply.nothing"), MessageType.Warning);
                MarkScanDirty();
                return;
            }

            var report = MaterialApplier.Apply(values, targets, exclude);

            if (report.Exception)
            {
                SetResult(L.T("apply.error"), MessageType.Error);
            }
            else
            {
                string source = L.F(fromPreset ? "apply.labelPreset" : "apply.labelMaterial", referenceName);
                string message = L.F("apply.done", report.Applied, DescribeEntries(), skipped > 0 ? L.F("apply.doneSkip", skipped) : string.Empty, source);
                if (report.Failed > 0) message += "\n" + L.F("apply.failed", report.Failed);
                SetResult(message, report.Failed > 0 ? MessageType.Warning : MessageType.Info);
            }
            MarkScanDirty();
        }

        static string DescribeEntries()
        {
            var names = Entries.Select(LilToonCatalog.GetDisplayName).ToList();
            const int shown = 4;
            if (names.Count <= shown) return string.Join(" · ", names);
            return string.Join(" · ", names.Take(shown)) + L.F("common.more", names.Count - shown);
        }

        void SetResult(string message, MessageType type)
        {
            m_Result = message;
            m_ResultType = type;
            Repaint();
        }

        // ---- Text helpers ---------------------------------------------------------------------------

        static string TagText(IssueKind kind)
        {
            switch (kind)
            {
                case IssueKind.ShaderMissing: return L.T("tag.ShaderMissing");
                case IssueKind.ReadOnlyImported:
                case IssueKind.ReadOnlyPackage:
                case IssueKind.ReadOnlyFile: return L.T("tag.ReadOnly");
                case IssueKind.NotLilToon: return L.T("tag.NotLilToon");
                case IssueKind.Partial: return L.T("tag.Partial");
                case IssueKind.NoneApplicable: return L.T("tag.NoneApplicable");
                case IssueKind.Locked: return L.T("tag.Locked");
                case IssueKind.Checkout: return L.T("tag.Checkout");
                case IssueKind.SharedChange: return L.T("tag.SharedChange");
                default: return kind.ToString();
            }
        }

        static string IssueText(Issue issue)
        {
            switch (issue.Kind)
            {
                case IssueKind.NotLilToon:
                case IssueKind.NoneApplicable:
                    return L.F("issue." + issue.Kind, issue.Info.ShaderName);
                case IssueKind.Partial:
                case IssueKind.Locked:
                    return L.F("issue." + issue.Kind, issue.Count);
                default:
                    return L.T("issue." + issue.Kind);
            }
        }

        // ---- Part / material selection ------------------------------------------------------------------

        HashSet<string> ExcludedSet
        {
            get
            {
                if (m_ExcludedSet == null)
                {
                    if (m_ExcludedRows == null) m_ExcludedRows = new List<string>();
                    m_ExcludedSet = new HashSet<string>(m_ExcludedRows, StringComparer.Ordinal);
                }
                return m_ExcludedSet;
            }
        }

        static string RowKey(PartInfo part, Material material)
        {
            return part.InstanceId.ToString(CultureInfo.InvariantCulture) + ":" + material.GetInstanceID().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Whether a material is checked under a part. Everything is checked unless unchecked by the user.</summary>
        bool IsRowSelected(PartInfo part, Material material)
        {
            if (part == null || material == null) return true;
            return !ExcludedSet.Contains(RowKey(part, material));
        }

        void SetRowSelected(PartInfo part, Material material, bool selected, bool notify)
        {
            if (part == null || material == null) return;
            string key = RowKey(part, material);
            var set = ExcludedSet;
            if (selected)
            {
                if (set.Remove(key)) m_ExcludedRows.Remove(key);
            }
            else if (set.Add(key))
            {
                m_ExcludedRows.Add(key);
                if (m_ExcludedRows.Count > MaxExcludedRows)
                {
                    set.Remove(m_ExcludedRows[0]);
                    m_ExcludedRows.RemoveAt(0);
                }
            }
            if (notify) OnSelectionChanged();
        }

        void SetPartSelected(PartInfo part, bool selected)
        {
            if (part == null) return;
            foreach (var material in part.Slots) SetRowSelected(part, material, selected, false);
            OnSelectionChanged();
        }

        void SelectAll(bool selected)
        {
            if (selected)
            {
                ClearSelection();
            }
            else if (m_Scan != null)
            {
                foreach (var part in m_Scan.Parts)
                {
                    foreach (var material in part.Slots) SetRowSelected(part, material, false, false);
                }
            }
            OnSelectionChanged();
        }

        void ClearSelection()
        {
            ExcludedSet.Clear();
            m_ExcludedRows.Clear();
        }

        void OnSelectionChanged()
        {
            m_Result = null;
            MarkEvalDirty();
        }

        // ---- State helpers ----------------------------------------------------------------------------

        bool IsExpanded(PartInfo part) => part != null && m_ExpandedParts.Contains(part.InstanceId);

        void SetExpanded(PartInfo part, bool expanded)
        {
            if (part == null) return;
            if (expanded)
            {
                if (!m_ExpandedParts.Contains(part.InstanceId)) m_ExpandedParts.Add(part.InstanceId);
                const int maxRemembered = 256;
                if (m_ExpandedParts.Count > maxRemembered) m_ExpandedParts.RemoveRange(0, m_ExpandedParts.Count - maxRemembered);
            }
            else
            {
                m_ExpandedParts.Remove(part.InstanceId);
            }
            Repaint();
        }

        void MarkScanDirty()
        {
            m_ScanDirty = true;
            m_EvalDirty = true;
            Repaint();
        }

        void MarkEvalDirty()
        {
            m_EvalDirty = true;
            Repaint();
        }

        void Repaint()
        {
            if (m_Window != null) m_Window.Repaint();
        }

        void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, m_Styles.ListEdge);
        }

        /// <summary>
        /// Content rect for GUI.BeginScrollView. It is never wider than the visible area (so no horizontal
        /// scrollbar appears) and, without scrolling, exactly as tall as it (so no vertical one either).
        /// </summary>
        static Rect ScrollViewRect(Rect inner, float contentHeight, bool needsScrollbar)
        {
            float width = inner.width;
            if (needsScrollbar)
            {
                var scrollbar = GUI.skin.verticalScrollbar;
                float scrollbarWidth = scrollbar.fixedWidth + scrollbar.margin.left;
                width -= scrollbarWidth > 0f ? scrollbarWidth : 14f;
            }
            float height = needsScrollbar ? contentHeight : inner.height;
            return new Rect(0f, 0f, Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        // ---- Styles -------------------------------------------------------------------------------------

        sealed class Styles
        {
            readonly bool m_ProSkin;

            public readonly GUIStyle MiniRight;
            public readonly GUIStyle MiniDim;
            public readonly GUIStyle MiniDimLine;
            public readonly GUIStyle MiniIconLabel;
            public readonly GUIStyle LabelDim;
            public readonly GUIStyle BoldFoldout;
            public readonly GUIStyle FoldoutDim;
            public readonly GUIStyle TagError;
            public readonly GUIStyle TagWarn;
            public readonly GUIStyle TagInfo;
            public readonly GUIStyle LevelError;
            public readonly GUIStyle LevelWarn;
            public readonly GUIContent RescanContent;
            public readonly GUIContent CopyContent;
            public readonly string[] ModeLabels;
            public readonly Texture ErrorIcon;
            public readonly Texture WarnIcon;
            public readonly Color ListBack;
            public readonly Color IssueBack;
            public readonly Color ListEdge;
            public readonly Color Highlight;
            public readonly Color Hover;

            public bool IsValid => m_ProSkin == EditorGUIUtility.isProSkin;

            public Styles()
            {
                m_ProSkin = EditorGUIUtility.isProSkin;
                var error = m_ProSkin ? new Color32(0xF0, 0x70, 0x70, 0xFF) : new Color32(0xB3, 0x26, 0x1E, 0xFF);
                var warn = m_ProSkin ? new Color32(0xE5, 0xB9, 0x4E, 0xFF) : new Color32(0x8A, 0x5A, 0x00, 0xFF);
                var dim = m_ProSkin ? new Color32(0xA8, 0xA8, 0xA8, 0xFF) : new Color32(0x55, 0x55, 0x55, 0xFF);

                MiniRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                MiniDim = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                MiniDim.normal.textColor = dim;
                MiniDimLine = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
                MiniDimLine.normal.textColor = dim;
                MiniDimLine.hover.textColor = dim;
                MiniDimLine.active.textColor = dim;
                MiniIconLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, imagePosition = ImagePosition.ImageLeft };
                MiniIconLabel.fixedHeight = 0f;
                LabelDim = new GUIStyle(EditorStyles.label);
                LabelDim.normal.textColor = dim;
                BoldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                FoldoutDim = new GUIStyle(EditorStyles.foldout);
                FoldoutDim.normal.textColor = dim;
                FoldoutDim.onNormal.textColor = dim;
                FoldoutDim.hover.textColor = dim;
                FoldoutDim.onHover.textColor = dim;
                FoldoutDim.focused.textColor = dim;
                FoldoutDim.onFocused.textColor = dim;
                FoldoutDim.active.textColor = dim;
                FoldoutDim.onActive.textColor = dim;

                TagError = MakeTag(error);
                TagWarn = MakeTag(warn);
                TagInfo = MakeTag(dim);
                LevelError = new GUIStyle(EditorStyles.boldLabel);
                LevelError.normal.textColor = error;
                LevelWarn = new GUIStyle(EditorStyles.boldLabel);
                LevelWarn.normal.textColor = warn;

                var refresh = EditorGUIUtility.IconContent("Refresh");
                RescanContent = new GUIContent(refresh != null ? refresh.image : null, L.T("target.rescan"));
                if (RescanContent.image == null) RescanContent.text = "↻";
                CopyContent = new GUIContent(L.T("copy.button"), L.T("copy.tooltip"));
                ModeLabels = new[] { L.T("ref.modeMaterial"), L.T("ref.modePreset") };
                ErrorIcon = EditorGUIUtility.IconContent("console.erroricon.sml")?.image;
                WarnIcon = EditorGUIUtility.IconContent("console.warnicon.sml")?.image;

                ListBack = m_ProSkin ? new Color32(0x30, 0x30, 0x30, 0xFF) : new Color32(0xD8, 0xD8, 0xD8, 0xFF);
                IssueBack = m_ProSkin ? new Color32(0x3A, 0x3A, 0x3A, 0xFF) : new Color32(0xE2, 0xE2, 0xE2, 0xFF);
                ListEdge = m_ProSkin ? new Color32(0x23, 0x23, 0x23, 0xFF) : new Color32(0x99, 0x99, 0x99, 0xFF);
                Highlight = m_ProSkin ? new Color(0.36f, 0.29f, 0.12f, 1f) : new Color(0.95f, 0.85f, 0.54f, 1f);
                Hover = m_ProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f);
            }

            static GUIStyle MakeTag(Color color)
            {
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                style.normal.textColor = color;
                return style;
            }
        }
    }
}
