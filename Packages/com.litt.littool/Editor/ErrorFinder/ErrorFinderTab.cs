using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// "오류 찾기" tab (v1.3): when Gesture Manager play, a VRChat SDK upload or a script compilation ends with errors,
    /// shows what the problem is and the order to fix it, one card per cause. It never changes the avatar or a setting:
    /// its buttons only select an object, show a folder, or open a window.
    ///
    /// IMGUI note: everything that decides how many controls are drawn is built once per event during
    /// EventType.Layout (the Snapshot), like the Bone Scale tab. Parts: ErrorFinderTab.*.cs.
    /// </summary>
    [Serializable]
    internal sealed partial class ErrorFinderTab : ILittoolTab
    {
        const int MaxCards = 30;
        const float CardGap = 6f;

        [SerializeField] int m_Filter;
        [SerializeField] bool m_ShowAll;
        [SerializeField] bool m_ResultsOpen;
        [SerializeField] int m_StateUnit;

        [NonSerialized] EditorWindow m_Window;
        [NonSerialized] Styles m_Styles;
        [NonSerialized] Snapshot m_Snap;
        [NonSerialized] FinderAnalysis m_Analysis;
        [NonSerialized] int m_AnalysisRevision = -1;
        [NonSerialized] int m_AnalysisUnit = -1;
        [NonSerialized] bool m_TargetsDirty = true;
        [NonSerialized] Dictionary<string, List<TargetView>> m_Targets;
        [NonSerialized] Dictionary<string, bool> m_Open;
        [NonSerialized] HashSet<string> m_RawOpen;
        [NonSerialized] HashSet<string> m_WhyOpen;
        [NonSerialized] Dictionary<string, int> m_Choice;
        [NonSerialized] int m_LooseErrors;
        [NonSerialized] WatchRow[] m_Watch;

        public string Title => L.T("tab.errorFinder");

        // ------------------------------------------------------------------------------------------------
        // ILittoolTab

        public void OnEnable(EditorWindow window)
        {
            m_Window = window;
            m_Snap = null;
            m_Analysis = null;
            m_AnalysisRevision = -1;
            m_TargetsDirty = true;
            m_Watch = null;
            ErrorCollector.Changed += OnCollectorChanged;
        }

        public void OnDisable()
        {
            ErrorCollector.Changed -= OnCollectorChanged;
            m_Styles?.Dispose();
            m_Styles = null;
            m_Window = null;
        }

        public void OnHierarchyChange()
        {
            m_TargetsDirty = true;
        }

        public void OnProjectChange()
        {
            m_TargetsDirty = true;
            m_Watch = null;
            Repaint();
        }

        public void OnUndoRedo()
        {
            m_TargetsDirty = true;
            Repaint();
        }

        public void OnPlayModeChanged()
        {
            m_TargetsDirty = true;
            Repaint();
        }

        public void OnLanguageChanged()
        {
            m_Analysis = null;
            m_AnalysisRevision = -1;
            m_Watch = null;
            Repaint();
        }

        void OnCollectorChanged()
        {
            m_TargetsDirty = true;
            Repaint();
        }

        void Repaint()
        {
            if (m_Window != null) m_Window.Repaint();
        }

        // ------------------------------------------------------------------------------------------------
        // GUI

        public void OnGUI()
        {
            if (m_Styles == null || !m_Styles.IsValid)
            {
                m_Styles?.Dispose();
                m_Styles = new Styles();
            }
            if (Event.current.type == EventType.Layout || m_Snap == null) BuildSnapshot();
            var snap = m_Snap;

            DrawStatusLine(snap);
            if (snap.LoadError != null) EditorGUILayout.HelpBox(L.F("ef.catalogError", snap.LoadError), MessageType.Error);
            GUILayout.Space(6f);
            if (snap.Idle) DrawIdle(snap);
            else DrawResults(snap);
            if (!string.IsNullOrEmpty(snap.Problem))
            {
                GUILayout.Space(4f);
                GUILayout.Label(L.F("ef.problem", snap.Problem), m_Styles.SmallDim, GUILayout.MinWidth(0f));
            }
            GUILayout.Space(8f);
        }

        void DrawStatusLine(Snapshot snap)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("ef.title"), m_Styles.Heading, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                bool auto = GUILayout.Toggle(snap.Auto, new GUIContent(L.T("ef.autoCollect"), L.T("ef.autoCollectTip")), GUILayout.ExpandWidth(false));
                if (EditorGUI.EndChangeCheck())
                {
                    ErrorCollector.SetAutoCollect(auto);
                    m_Watch = null;
                    Repaint();
                }
                GUILayout.Space(6f);
                if (GUILayout.Button(new GUIContent(L.T("ef.btn.rescan"), L.T("ef.rescanTip")), GUILayout.ExpandWidth(false)))
                    RescanNow();
            }
        }

        void RescanNow()
        {
            int before = ErrorStore.instance.Revision;
            ErrorCollector.Rescan();
            var store = ErrorStore.instance;
            if (m_Window != null && (!store.Shown.Exists || store.Shown.kind != FinderUnitKind.Rescan || store.Revision == before))
                m_Window.ShowNotification(new GUIContent(L.T("ef.toast.rescanNone")), 1.5);
            GUI.FocusControl(null);
            Repaint();
        }

        // ---- results ----------------------------------------------------------------------------------------

        void DrawResults(Snapshot snap)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(snap.SummaryText, m_Styles.Summary, GUILayout.MinWidth(0f));
                GUILayout.Label(snap.TimeText, m_Styles.SummaryTime, GUILayout.ExpandWidth(false));
            }
            if (!string.IsNullOrEmpty(snap.NoticeText)) GUILayout.Label(snap.NoticeText, m_Styles.Note, GUILayout.MinWidth(0f));
            if (snap.Filters.Count > 0)
            {
                GUILayout.Space(4f);
                DrawFilterChips(snap);
            }
            GUILayout.Space(2f);
            GUILayout.Label(snap.ReadLine, m_Styles.SmallDim, GUILayout.MinWidth(0f));
            GUILayout.Space(6f);

            if (snap.NoCause)
            {
                GUILayout.Label(L.T("ef.noCauseHint"), m_Styles.Note, GUILayout.MinWidth(0f));
            }
            else if (snap.FilterEmpty)
            {
                GUILayout.Label(L.T("ef.filterEmpty"), m_Styles.SmallDim, GUILayout.MinWidth(0f));
            }
            foreach (var card in snap.Cards)
            {
                DrawCard(snap, card);
                GUILayout.Space(CardGap);
            }
            if (snap.Hidden > 0)
            {
                if (GUILayout.Button(L.F("ef.btn.more", snap.Hidden)))
                {
                    m_ShowAll = true;
                    Repaint();
                }
            }
            if (snap.Results.Count > 0) DrawResultBox(snap);

            GUILayout.Space(10f);
            DrawSeparator();
            GUILayout.Space(4f);
            GUILayout.Label(L.T("ef.footer"), m_Styles.SmallDim, GUILayout.MinWidth(0f));
        }

        void DrawFilterChips(Snapshot snap)
        {
            float avail = AvailableWidth(0f);
            var widths = new float[snap.Filters.Count];
            for (int i = 0; i < widths.Length; i++) widths[i] = Mathf.Ceil(m_Styles.FilterChip.CalcSize(new GUIContent(snap.Filters[i].Label)).x) + 2f;
            int rows = FlowRows(widths, avail, 6f);
            const float h = 22f, gap = 4f;
            var area = GUILayoutUtility.GetRect(avail, rows * h + (rows - 1) * gap, GUILayout.ExpandWidth(true));
            float x = area.x, y = area.y;
            for (int i = 0; i < widths.Length; i++)
            {
                var f = snap.Filters[i];
                if (x > area.x && x + widths[i] > area.x + avail)
                {
                    x = area.x;
                    y += h + gap;
                }
                var r = new Rect(x, y, widths[i], h);
                bool on = snap.Filter == (int)f.Bucket;
                if (Event.current.type == EventType.Repaint)
                {
                    Rounded(r, on ? m_Styles.FilterOn : m_Styles.FilterOff, h * 0.5f);
                    var style = m_Styles.FilterChip;
                    var old = style.normal.textColor;
                    style.normal.textColor = on ? m_Styles.FilterOnText : (f.Count == 0 ? m_Styles.ChipText[4] : m_Styles.ChipText[1]);
                    style.Draw(r, new GUIContent(f.Label), false, false, false, false);
                    style.normal.textColor = old;
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    m_Filter = (int)f.Bucket;
                    Repaint();
                }
                x += widths[i] + 6f;
            }
        }

        void DrawResultBox(Snapshot snap)
        {
            GUILayout.Space(2f);
            using (var scope = new EditorGUILayout.VerticalScope(m_Styles.Card))
            {
                var head = EditorGUILayout.BeginHorizontal();
                var arrow = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f), GUILayout.Height(18f));
                GUILayout.Label(L.F("ef.results", snap.Results.Count), m_Styles.BodyBold, GUILayout.ExpandWidth(false));
                GUILayout.Label(L.T("ef.resultsHint"), m_Styles.SmallDim, GUILayout.MinWidth(0f));
                EditorGUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint) EditorStyles.foldout.Draw(arrow, false, false, snap.ResultsOpen, false);
                if (Clicked(head))
                {
                    m_ResultsOpen = !m_ResultsOpen;
                    Repaint();
                }
                if (snap.ResultsOpen)
                {
                    foreach (var r in snap.Results)
                    {
                        GUILayout.Space(4f);
                        string count = r.Count > 1 ? "  " + L.F("ef.card.count", r.Count) : string.Empty;
                        GUILayout.Label(r.Text + count, m_Styles.Code, GUILayout.MinWidth(0f));
                        GUILayout.Label(r.Explain, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                    }
                }
                if (Event.current.type == EventType.Repaint) Border(scope.rect, m_Styles.CardEdge, m_Styles.GrayBar);
            }
        }

        // ---- snapshot ---------------------------------------------------------------------------------------

        sealed class Snapshot
        {
            public bool Auto;
            public bool Playing;
            /// <summary>No network: the translate buttons are disabled (read once per layout, not on every repaint).</summary>
            public bool Offline;
            public string LoadError;
            public string Problem;
            public bool Idle;

            public FinderUnit Unit;
            public FinderAnalysis Analysis;
            public string SummaryText;
            public string TimeText;
            public string NoticeText;
            public string ReadLine;
            public int Filter;
            public readonly List<FilterChip> Filters = new List<FilterChip>();
            public readonly List<CardView> Cards = new List<CardView>();
            public int Hidden;
            public bool FilterEmpty;
            public bool NoCause;
            public bool ResultsOpen;
            public List<ResultLine> Results = new List<ResultLine>();

            public string LastScanText;
            public int LooseErrors;
            public WatchRow[] Watch;
            public string CatalogLine;
        }

        sealed class FilterChip
        {
            public SeverityBucket Bucket;
            public string Label;
            public int Count;
        }

        sealed class Chip
        {
            public string Text;
            public ChipKind Kind;
            public bool Dashed;
        }

        sealed class WatchRow
        {
            public string Name;
            public string Desc;
            public string Status;
            public bool On;
        }

        void BuildSnapshot()
        {
            var store = ErrorStore.instance;
            var snap = new Snapshot
            {
                Auto = ErrorCollector.AutoCollect,
                Playing = EditorApplication.isPlayingOrWillChangePlaymode,
                Offline = Application.internetReachability == NetworkReachability.NotReachable,
                LoadError = ErrorCatalog.LoadError,
                Problem = ErrorCollector.LastProblem,
            };
            var unit = store.Shown;
            snap.Idle = !unit.Exists;
            if (snap.Idle)
            {
                BuildIdle(snap, store);
                m_Snap = snap;
                return;
            }

            if (m_StateUnit != unit.id)
            {
                // a new result: start from the first card opened, every filter off
                m_StateUnit = unit.id;
                m_Filter = 0;
                m_ShowAll = false;
                m_ResultsOpen = false;
                m_Open = null;
                m_RawOpen = null;
                m_WhyOpen = null;
                m_Choice = null;
                m_TargetsDirty = true;
            }
            if (m_Open == null) m_Open = new Dictionary<string, bool>();
            if (m_RawOpen == null) m_RawOpen = new HashSet<string>();
            if (m_WhyOpen == null) m_WhyOpen = new HashSet<string>();
            if (m_Choice == null) m_Choice = new Dictionary<string, int>();

            var matcher = ErrorCatalog.Matcher;
            if (m_Analysis == null || m_AnalysisRevision != store.Revision || m_AnalysisUnit != unit.id)
            {
                m_Analysis = matcher != null ? ErrorAnalysis.Run(unit, matcher) : null;
                m_AnalysisRevision = store.Revision;
                m_AnalysisUnit = unit.id;
                m_TargetsDirty = true;
            }
            var a = m_Analysis;
            snap.Unit = unit;
            snap.Analysis = a;
            int causes = a != null ? a.Cards.Count : 0;
            snap.SummaryText = SummaryLine(unit, a);
            snap.TimeText = new DateTime(unit.endTicks > 0 ? unit.endTicks : unit.startTicks).ToString("HH:mm", CultureInfo.InvariantCulture);
            snap.NoticeText = store.Notice == "rescanNothingNew" ? L.T("ef.notice.rescanNothingNew") : null;
            snap.ReadLine = ReadLine(unit, a);
            snap.NoCause = a != null && causes == 0;  // (no list: the load error above says why)
            snap.ResultsOpen = m_ResultsOpen;
            if (a != null) snap.Results = a.Results;

            if (a != null && causes > 0)
            {
                snap.Filters.Add(new FilterChip { Bucket = SeverityBucket.None, Label = L.F("ef.filter.all", causes), Count = causes });
                AddFilter(snap, a, SeverityBucket.Upload, "ef.filter.upload");
                AddFilter(snap, a, SeverityBucket.Play, "ef.filter.play");
                AddFilter(snap, a, SeverityBucket.Broken, "ef.filter.broken");
                AddFilter(snap, a, SeverityBucket.Unknown, "ef.filter.unknown");
                if (a.CountOf(SeverityBucket.Project) > 0) AddFilter(snap, a, SeverityBucket.Project, "ef.filter.project");
            }
            snap.Filter = m_Filter;

            if (m_TargetsDirty || m_Targets == null) BuildTargets(a);
            if (a != null)
            {
                var visible = new List<FinderCard>();
                foreach (var c in a.Cards)
                {
                    if (m_Filter == 0 || ((int)c.Buckets & m_Filter) != 0) visible.Add(c);
                }
                snap.FilterEmpty = causes > 0 && visible.Count == 0;
                int limit = m_ShowAll ? visible.Count : Math.Min(visible.Count, MaxCards);
                snap.Hidden = visible.Count - limit;
                for (int i = 0; i < limit; i++) snap.Cards.Add(MakeView(visible[i], i == 0, unit, snap.Playing));
            }
            m_Snap = snap;
        }

        static void AddFilter(Snapshot snap, FinderAnalysis a, SeverityBucket bucket, string key)
        {
            int n = a.CountOf(bucket);
            snap.Filters.Add(new FilterChip { Bucket = bucket, Label = L.F(key, n), Count = n });
        }

        // ---- texts shared with the collector ----------------------------------------------------------------

        /// <summary>"업로드 실패 · 원인 3개를 찾았어요".</summary>
        public static string SummaryLine(FinderUnit unit, FinderAnalysis a)
        {
            int n = a != null ? a.Cards.Count : 0;
            if (n == 0) return L.F("ef.sum.noCause", KindLabel(unit, true));
            return L.F("ef.sum.found", KindLabel(unit, true), n);
        }

        /// <summary>The scene view notification: "LITTOOL · 업로드 실패, 원인 3개를 찾았어요 / Tools › LITTOOL에서 확인하세요".</summary>
        public static string NotificationText(FinderUnit unit, FinderAnalysis a)
        {
            int n = a != null ? a.Cards.Count : 0;
            return n > 0 ? L.F("ef.notify", KindLabel(unit, true), n) : L.F("ef.notifyNoCause", KindLabel(unit, true));
        }

        /// <summary>플레이 오류 · 업로드 실패 · Build &amp; Test 실패 · 스크립트 오류 · 다시 검사.</summary>
        public static string KindLabel(FinderUnit unit, bool summary)
        {
            switch (unit?.kind)
            {
                case FinderUnitKind.Play: return L.T(summary ? "ef.kind.playErrors" : "ef.when.play");
                case FinderUnitKind.Upload: return L.T(summary ? (unit.Failed ? "ef.kind.uploadFailed" : "ef.kind.uploadErrors") : "ef.when.upload");
                case FinderUnitKind.Test: return L.T(summary ? (unit.Failed ? "ef.kind.testFailed" : "ef.kind.testErrors") : "ef.when.test");
                case FinderUnitKind.Compile: return L.T(summary ? "ef.kind.compileErrors" : "ef.when.compile");
                default: return L.T(summary ? "ef.kind.rescan" : "ef.when.rescan");
            }
        }

        static string ChannelLabel(string channel)
        {
            switch (channel)
            {
                case FinderChannel.Sdk: return L.T("ef.ch.sdk");
                case FinderChannel.Validation: return L.T("ef.ch.validation");
                case FinderChannel.Ndmf: return L.T("ef.ch.ndmf");
                case FinderChannel.Compile: return L.T("ef.ch.compile");
                default: return L.T("ef.ch.console");
            }
        }

        static string ReadLine(FinderUnit unit, FinderAnalysis a)
        {
            var names = new List<string>();
            // the order of the requirements' "지켜보는 곳"
            foreach (var ch in new[] { FinderChannel.Sdk, FinderChannel.Validation, FinderChannel.Console, FinderChannel.Ndmf, FinderChannel.Compile })
            {
                if (unit.channels.Contains(ch)) names.Add(ChannelLabel(ch));
            }
            if (names.Count == 0) names.Add(ChannelLabel(FinderChannel.Console));
            string line = L.F("ef.read", string.Join(" · ", names));
            if (a != null && a.Results.Count > 0) line += " · " + L.T("ef.readFold");
            if (a != null && a.Ignored > 0) line += " · " + L.F("ef.ignored", a.Ignored);
            return line;
        }

        // ---- drawing helpers --------------------------------------------------------------------------------

        static float AvailableWidth(float inset)
        {
            // window side margins (2 × 6) + vertical scroll bar + this element's own inset
            return Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 12f - 18f - inset);
        }

        static int FlowRows(float[] widths, float avail, float gap)
        {
            int rows = 1;
            float x = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                if (x > 0f && x + widths[i] > avail)
                {
                    rows++;
                    x = 0f;
                }
                x += widths[i] + gap;
            }
            return rows;
        }

        static bool Clicked(Rect rect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        static void Rounded(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        static void RoundedBorder(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 1f, radius);
        }

        static void Dashed(Rect r, Color color)
        {
            const float dash = 3f, gap = 2f;
            for (float x = r.x + 2f; x < r.xMax - 2f; x += dash + gap)
            {
                float w = Mathf.Min(dash, r.xMax - 2f - x);
                EditorGUI.DrawRect(new Rect(x, r.y, w, 1f), color);
                EditorGUI.DrawRect(new Rect(x, r.yMax - 1f, w, 1f), color);
            }
            for (float y = r.y + 2f; y < r.yMax - 2f; y += dash + gap)
            {
                float h = Mathf.Min(dash, r.yMax - 2f - y);
                EditorGUI.DrawRect(new Rect(r.x, y, 1f, h), color);
                EditorGUI.DrawRect(new Rect(r.xMax - 1f, y, 1f, h), color);
            }
        }

        static void Border(Rect r, Color edge, Color bar)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), edge);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), bar);
        }

        void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, m_Styles.Edge);
        }

        /// <summary>Rich text for a raw line: '&lt;' and '&gt;' become look-alikes so the error text is never read as a tag.</summary>
        static string Plain(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace('<', '‹').Replace('>', '›');
        }
    }
}
