using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Littool.ErrorFinder
{
    internal sealed partial class ErrorFinderTab
    {
        [NonSerialized] int m_LooseCountKey = -1;

        void BuildIdle(Snapshot snap, ErrorStore store)
        {
            snap.LastScanText = store.LastScanTicks > 0
                ? L.F("ef.idle.last", new DateTime(store.LastScanTicks).ToString("HH:mm", CultureInfo.InvariantCulture), ShortKindLabel(store.LastScanKind))
                : L.T("ef.idle.never");
            if (snap.Auto)
            {
                // errors heard outside play / upload / compile: counted once per new message, not every frame
                if (m_LooseCountKey != store.Loose.Count)
                {
                    m_LooseCountKey = store.Loose.Count;
                    m_LooseErrors = 0;
                    var matcher = ErrorCatalog.Matcher;
                    foreach (var it in store.Loose)
                    {
                        if (it.level > 0) continue;
                        if (matcher != null && matcher.Analyze(it.text).Kind == MatchKind.Ignore) continue;
                        m_LooseErrors++;
                    }
                }
                snap.LooseErrors = m_LooseErrors;
            }
            if (m_Watch == null) m_Watch = BuildWatch(snap.Auto);
            snap.Watch = m_Watch;
            var data = ErrorCatalog.Data;
            if (data != null) snap.CatalogLine = L.F("ef.know.list", data.rows.Length, data.version, MonthLabel(data.date));
        }

        static string ShortKindLabel(string kind)
        {
            switch (kind)
            {
                case FinderUnitKind.Play: return L.T("ef.short.play");
                case FinderUnitKind.Upload: return L.T("ef.short.upload");
                case FinderUnitKind.Test: return L.T("ef.short.test");
                case FinderUnitKind.Compile: return L.T("ef.short.compile");
                default: return L.T("ef.short.rescan");
            }
        }

        static string MonthLabel(string date)
        {
            if (!DateTime.TryParseExact(date ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return date ?? string.Empty;
            switch (L.Current)
            {
                case Lang.Ko: return d.Year + "년 " + d.Month + "월";
                case Lang.Ja: return d.Year + "年" + d.Month + "月";
                default: return d.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            }
        }

        static WatchRow[] BuildWatch(bool auto)
        {
            string on = L.T("ef.st.on"), scan = L.T("ef.st.scan");
            return new[]
            {
                new WatchRow
                {
                    Name = L.T("ef.watch.console"), Desc = L.T("ef.watch.consoleDesc"), On = auto,
                    Status = auto ? on : (ConsoleBridge.Available ? scan : L.T("ef.st.unreadable")),
                },
                new WatchRow
                {
                    Name = L.T("ef.watch.sdk"), Desc = L.T("ef.watch.sdkDesc"), On = auto && SdkBridge.State == SourceState.Ready,
                    Status = StateText(SdkBridge.State, SdkBridge.Version, auto),
                },
                new WatchRow
                {
                    Name = L.T("ef.watch.ndmf"), Desc = L.T("ef.watch.ndmfDesc"), On = auto && NdmfBridge.State == SourceState.Ready,
                    Status = StateText(NdmfBridge.State, NdmfBridge.Version, auto),
                },
                new WatchRow { Name = L.T("ef.watch.compile"), Desc = L.T("ef.watch.compileDesc"), On = auto, Status = auto ? on : scan },
            };
        }

        static string StateText(SourceState state, string version, bool auto)
        {
            switch (state)
            {
                case SourceState.Missing: return L.T("ef.st.missing");
                case SourceState.Unreadable: return string.IsNullOrEmpty(version) ? L.T("ef.st.unreadable") : L.F("ef.st.unreadableVer", version);
                default: return L.T(auto ? "ef.st.on" : "ef.st.scan");
            }
        }

        void DrawIdle(Snapshot snap)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f), GUILayout.Height(24f));
                if (Event.current.type == EventType.Repaint) DrawCheck(icon);
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(L.T("ef.idle.title"), m_Styles.IdleTitle, GUILayout.MinWidth(0f));
                    GUILayout.Label(L.T(snap.Auto ? "ef.idle.desc" : "ef.idle.descOff"), m_Styles.Body, GUILayout.MinWidth(0f));
                    GUILayout.Label(snap.LastScanText, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                }
            }
            if (snap.LooseErrors > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label(L.F("ef.idle.loose", snap.LooseErrors), m_Styles.Note, GUILayout.MinWidth(0f));
            }

            GUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("ef.watch"), m_Styles.Section, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                GUILayout.Label(L.T(snap.Auto ? "ef.watch.capOn" : "ef.watch.capOff"), m_Styles.SmallDimRight, GUILayout.ExpandWidth(false));
            }
            using (var scope = new EditorGUILayout.VerticalScope(m_Styles.Card))
            {
                for (int i = 0; i < snap.Watch.Length; i++)
                {
                    var w = snap.Watch[i];
                    if (i > 0) GUILayout.Space(6f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            GUILayout.Label(w.Name, m_Styles.BodyBold, GUILayout.MinWidth(0f));
                            GUILayout.Label(w.Desc, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                        }
                        var content = new GUIContent(w.Status);
                        var size = m_Styles.Chip.CalcSize(content);
                        var r = GUILayoutUtility.GetRect(size.x + 2f, 18f, GUILayout.ExpandWidth(false), GUILayout.Height(18f));
                        if (Event.current.type == EventType.Repaint)
                        {
                            int k = w.On ? (int)ChipKind.Confirmed : (int)ChipKind.Gray;
                            Rounded(r, m_Styles.ChipBack[k], 4f);
                            var style = m_Styles.Chip;
                            var old = style.normal.textColor;
                            style.normal.textColor = m_Styles.ChipText[k];
                            style.Draw(r, content, false, false, false, false);
                            style.normal.textColor = old;
                        }
                    }
                }
                if (Event.current.type == EventType.Repaint) Border(scope.rect, m_Styles.CardEdge, m_Styles.CardEdge);
            }

            GUILayout.Space(12f);
            GUILayout.Label(L.T("ef.know"), m_Styles.Section);
            Bullet(L.T("ef.know.noChange"));
            Bullet(L.T("ef.know.safeMode"));
            if (!string.IsNullOrEmpty(snap.CatalogLine)) Bullet(snap.CatalogLine);
            Bullet(L.T("ef.know.log"));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                if (GUILayout.Button(L.T("ef.btn.logFolder"), m_Styles.MiniButton, GUILayout.ExpandWidth(false))) OpenLogFolder();
                GUILayout.FlexibleSpace();
            }
        }

        void Bullet(string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("·", m_Styles.Small, GUILayout.Width(10f));
                GUILayout.Label(text, m_Styles.Small, GUILayout.MinWidth(0f));
            }
        }

        void DrawCheck(Rect r)
        {
            Rounded(r, m_Styles.OkColor, r.width * 0.5f);
            var old = Handles.color;
            Handles.color = Color.white;
            var a = new Vector3(r.x + r.width * 0.27f, r.y + r.height * 0.52f);
            var b = new Vector3(r.x + r.width * 0.44f, r.y + r.height * 0.68f);
            var c = new Vector3(r.x + r.width * 0.74f, r.y + r.height * 0.34f);
            Handles.DrawAAPolyLine(3f, a, b, c);
            Handles.color = old;
        }

        /// <summary>[기록 폴더 열기]: Explorer with this month's file selected (the folder when there is no file yet).</summary>
        void OpenLogFolder()
        {
            try
            {
                string folder = ErrorRecordLog.Folder;
                string file = ErrorRecordLog.FileFor(DateTime.Now);
                Directory.CreateDirectory(folder);
                bool hasFile = File.Exists(file);
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // explorer /select,"file" opens the folder with the file selected
                    string args = hasFile ? "/select,\"" + file.Replace('/', '\\') + "\"" : "\"" + folder.Replace('/', '\\') + "\"";
                    Process.Start("explorer.exe", args);
                }
                else
                {
                    EditorUtility.RevealInFinder(hasFile ? file : folder);
                }
            }
            catch (Exception e)
            {
                if (m_Window != null) m_Window.ShowNotification(new GUIContent(L.F("ef.toast.logFail", e.Message)), 3.0);
            }
        }
    }
}
