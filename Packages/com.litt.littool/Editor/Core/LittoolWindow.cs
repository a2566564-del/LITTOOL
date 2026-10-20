using System;
using System.Collections.Generic;
using Littool.BoneScale;
using Littool.ErrorFinder;
using Littool.MaterialBatch;
using UnityEditor;
using UnityEngine;

namespace Littool
{
    /// <summary>
    /// The LITTOOL window: title image, a row of feature tabs (wrapping onto more rows when they do not fit),
    /// the selected tab, and a footer with the Language selector and the version.
    /// Open it from Tools > LITTOOL.
    /// </summary>
    internal sealed class LittoolWindow : EditorWindow
    {
        const float BannerHeight = 64f;
        const float SideMargin = 6f;
        const float FooterHeight = 26f;
        const float LanguagePopupWidth = 96f;
        static readonly Color32 BannerBack = new Color32(0x2A, 0x2A, 0x2A, 0xFF);
        static readonly Color32 BannerEdge = new Color32(0x14, 0x14, 0x14, 0xFF);

        [SerializeField] int m_TabIndex;
        [SerializeField] List<Vector2> m_Scrolls = new List<Vector2>();
        [SerializeField] MaterialBatchTab m_MaterialBatch = new MaterialBatchTab();
        [SerializeField] BoneScaleTab m_BoneScale = new BoneScaleTab();
        [SerializeField] ErrorFinderTab m_ErrorFinder = new ErrorFinderTab();

        /// <summary>Index of the Error Finder tab in <see cref="m_Tabs"/>.</summary>
        const int ErrorFinderIndex = 2;

        [NonSerialized] ILittoolTab[] m_Tabs;
        [NonSerialized] Texture2D m_Banner;
        [NonSerialized] bool m_BannerSearched;
        [NonSerialized] GUIStyle m_FallbackTitleStyle;
        [NonSerialized] GUIStyle m_FooterStyle;
        [NonSerialized] GUIStyle m_FooterLabelStyle;
        [NonSerialized] bool m_FooterProSkin;

        [MenuItem(LittoolInfo.MenuPath, false, 2000)]
        static void Open()
        {
            var window = GetWindow<LittoolWindow>();
            window.titleContent = new GUIContent(LittoolInfo.Name);
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent(LittoolInfo.Name);
            wantsMouseMove = true;
            if (m_MaterialBatch == null) m_MaterialBatch = new MaterialBatchTab();
            if (m_BoneScale == null) m_BoneScale = new BoneScaleTab();
            if (m_ErrorFinder == null) m_ErrorFinder = new ErrorFinderTab();
            if (m_Scrolls == null) m_Scrolls = new List<Vector2>();
            m_Tabs = new ILittoolTab[] { m_MaterialBatch, m_BoneScale, m_ErrorFinder };
            while (m_Scrolls.Count < m_Tabs.Length) m_Scrolls.Add(Vector2.zero);
            foreach (var tab in m_Tabs) tab.OnEnable(this);
            // errors were found while the window was closed (the scene view said so): open on the Error Finder tab
            if (ErrorStore.instance.OpenTabRequest)
            {
                ErrorStore.instance.OpenTabRequest = false;
                m_TabIndex = ErrorFinderIndex;
            }
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        /// <summary>
        /// A unit ended with errors: every open LITTOOL window switches to the Error Finder tab (no notification).
        /// Returns false when no LITTOOL window is open.
        /// </summary>
        internal static bool FocusErrorFinder()
        {
            bool any = false;
            foreach (var window in Resources.FindObjectsOfTypeAll<LittoolWindow>())
            {
                if (window == null || window.m_Tabs == null) continue;
                window.m_TabIndex = ErrorFinderIndex;
                window.Repaint();
                any = true;
            }
            return any;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            if (m_Tabs == null) return;
            foreach (var tab in m_Tabs) tab.OnDisable();
        }

        void HandleUndoRedo()
        {
            if (m_Tabs != null) foreach (var tab in m_Tabs) tab.OnUndoRedo();
            Repaint();
        }

        void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (m_Tabs != null) foreach (var tab in m_Tabs) tab.OnPlayModeChanged();
            Repaint();
        }

        void OnHierarchyChange()
        {
            if (m_Tabs != null) foreach (var tab in m_Tabs) tab.OnHierarchyChange();
        }

        void OnProjectChange()
        {
            m_Banner = null;
            m_BannerSearched = false;
            if (m_Tabs != null) foreach (var tab in m_Tabs) tab.OnProjectChange();
        }

        void OnGUI()
        {
            if (m_Tabs == null || m_Tabs.Length == 0) return;
            if (Event.current.type == EventType.MouseMove) Repaint();
            m_TabIndex = Mathf.Clamp(m_TabIndex, 0, m_Tabs.Length - 1);
            while (m_Scrolls.Count < m_Tabs.Length) m_Scrolls.Add(Vector2.zero);

            // A tab click changes m_TabIndex; this event keeps drawing the tab it was laid out with.
            int tabIndex = m_TabIndex;

            GUILayout.Space(SideMargin);
            DrawBanner();
            GUILayout.Space(4f);
            DrawTabBar();
            GUILayout.Space(6f);

            m_Scrolls[tabIndex] = EditorGUILayout.BeginScrollView(m_Scrolls[tabIndex]);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SideMargin);
                using (new EditorGUILayout.VerticalScope())
                {
                    m_Tabs[tabIndex].OnGUI();
                }
                GUILayout.Space(SideMargin);
            }
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        void DrawBanner()
        {
            var rect = GUILayoutUtility.GetRect(0f, BannerHeight, GUILayout.ExpandWidth(true));
            rect.xMin += SideMargin;
            rect.xMax -= SideMargin;
            if (Event.current.type != EventType.Repaint) return;

            // The image is transparent; the background is drawn here so it always spans the window width.
            EditorGUI.DrawRect(rect, BannerEdge);
            var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            EditorGUI.DrawRect(inner, BannerBack);

            var banner = GetBanner();
            if (banner != null)
            {
                GUI.DrawTexture(inner, banner, ScaleMode.ScaleToFit, true);
            }
            else
            {
                if (m_FallbackTitleStyle == null)
                {
                    m_FallbackTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 22,
                    };
                    m_FallbackTitleStyle.normal.textColor = new Color32(0xF4, 0xF4, 0xF4, 0xFF);
                }
                GUI.Label(inner, LittoolInfo.Name, m_FallbackTitleStyle);
            }
        }

        Texture2D GetBanner()
        {
            if (m_Banner != null) return m_Banner;
            if (m_BannerSearched) return null;
            m_BannerSearched = true;
            m_Banner = LittoolImages.Load(LittoolInfo.BannerGuid, LittoolInfo.BannerName);
            return m_Banner;
        }

        /// <summary>One button per feature. Rows wrap when the buttons are wider than the window.</summary>
        void DrawTabBar()
        {
            var titles = new string[m_Tabs.Length];
            for (int i = 0; i < titles.Length; i++) titles[i] = m_Tabs[i].Title;

            var style = GUI.skin.button;
            float available = Mathf.Max(80f, position.width - SideMargin * 2f);
            var rowStarts = new List<int> { 0 };
            float x = 0f;
            for (int i = 0; i < titles.Length; i++)
            {
                float width = style.CalcSize(new GUIContent(titles[i])).x;
                if (i > rowStarts[rowStarts.Count - 1] && x + width > available)
                {
                    rowStarts.Add(i);
                    x = 0f;
                }
                x += width;
            }

            for (int r = 0; r < rowStarts.Count; r++)
            {
                int start = rowStarts[r];
                int end = r + 1 < rowStarts.Count ? rowStarts[r + 1] : titles.Length;
                var rowTitles = new string[end - start];
                Array.Copy(titles, start, rowTitles, 0, rowTitles.Length);
                int local = m_TabIndex >= start && m_TabIndex < end ? m_TabIndex - start : -1;

                if (r > 0) GUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(SideMargin);
                    int picked = GUILayout.Toolbar(local, rowTitles, style, GUI.ToolbarButtonSize.FitToContents);
                    if (picked != local && picked >= 0)
                    {
                        m_TabIndex = start + picked;
                        GUI.FocusControl(null);
                        Repaint();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>Footer: globe + Language on the left, the version on the right. It stays outside the scroll area.</summary>
        void DrawFooter()
        {
            if (m_FooterStyle == null || m_FooterProSkin != EditorGUIUtility.isProSkin)
            {
                m_FooterProSkin = EditorGUIUtility.isProSkin;
                m_FooterStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                m_FooterLabelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
            }

            var rect = GUILayoutUtility.GetRect(0f, FooterHeight, GUILayout.ExpandWidth(true));
            var edge = EditorGUIUtility.isProSkin ? new Color32(0x23, 0x23, 0x23, 0xFF) : new Color32(0x99, 0x99, 0x99, 0xFF);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), edge);

            float middle = rect.y + 1f + (rect.height - 1f) * 0.5f;
            float x = rect.x + SideMargin + 2f;
            var globe = LittoolImages.Globe;
            if (globe != null)
            {
                var iconRect = new Rect(x, middle - 7f, 14f, 14f);
                if (Event.current.type == EventType.Repaint)
                {
                    var tint = EditorGUIUtility.isProSkin ? new Color32(0xB4, 0xB4, 0xB4, 0xFF) : new Color32(0x55, 0x55, 0x55, 0xFF);
                    GUI.DrawTexture(iconRect, globe, ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
                }
                x = iconRect.xMax + 4f;
            }

            var label = new GUIContent("Language");
            float labelWidth = m_FooterLabelStyle.CalcSize(label).x;
            GUI.Label(new Rect(x, middle - 9f, labelWidth, 18f), label, m_FooterLabelStyle);
            x += labelWidth + 6f;

            EditorGUI.BeginChangeCheck();
            int language = EditorGUI.Popup(new Rect(x, middle - 9f, LanguagePopupWidth, 18f), (int)L.Current, L.LanguageNames);
            if (EditorGUI.EndChangeCheck())
            {
                L.Current = (Lang)language;
                foreach (var tab in m_Tabs) tab.OnLanguageChanged();
                Repaint();
            }
            x += LanguagePopupWidth + 8f;

            var version = new GUIContent(LittoolInfo.Name + " v" + LittoolInfo.Version);
            float versionWidth = m_FooterStyle.CalcSize(version).x;
            float right = rect.xMax - SideMargin - 2f;
            if (right - versionWidth >= x) GUI.Label(new Rect(right - versionWidth, middle - 9f, versionWidth, 18f), version, m_FooterStyle);
        }
    }
}
