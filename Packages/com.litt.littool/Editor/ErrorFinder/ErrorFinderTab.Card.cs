using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    internal sealed partial class ErrorFinderTab
    {
        const float CardInset = 22f;   // card padding left + right
        const float StepInset = 24f;   // number column

        sealed class TargetView
        {
            public Object Obj;
            public string Folder;
            public string Label;
            public string Detail;
            public bool Missing;
            public bool IsAsset;
            /// <summary>A component found from the stack trace's types (the tool's component, maybe not the broken one).</summary>
            public bool FromType;
            /// <summary>A line of text only ("같은 종류가 N개 더 있어요").</summary>
            public bool Info;
        }

        sealed class CardView
        {
            public FinderCard Card;
            public string Title;
            public string CountText;
            public string Found;
            public bool Open;
            public bool RawOpen;
            public bool WhyOpen;
            public int Choice = -1;
            public readonly List<Chip> Chips = new List<Chip>();
            public List<TargetView> Targets = new List<TargetView>();
            public TargetView Pipeline;
            public readonly List<CardStep> Steps = new List<CardStep>();
            public string TargetStepText;
            public string ChoiceNote;
        }

        // ---- building ---------------------------------------------------------------------------------------

        CardView MakeView(FinderCard c, bool first, FinderUnit unit, bool playing)
        {
            var v = new CardView
            {
                Card = c,
                Title = c.Title ?? L.T("ef.card.notListed"),
                CountText = c.Count > 1 ? L.F("ef.card.count", c.Count) : null,
                Open = m_Open.TryGetValue(c.Key, out bool open) ? open : first,
                RawOpen = m_RawOpen.Contains(c.Key),
                WhyOpen = m_WhyOpen.Contains(c.Key),
            };

            // chips: severity · tool · how sure
            if (c.Severity.Length > 0)
                v.Chips.Add(new Chip { Text = SeverityLabel(c.Severity), Kind = c.Severity == "기능 깨짐" ? ChipKind.Gray : ChipKind.Severe });
            else
                v.Chips.Add(new Chip { Text = L.T("ef.sev.unknown"), Kind = ChipKind.Gray });
            string toolName = c.ToolLabel ?? c.Tool;
            if (!string.IsNullOrEmpty(toolName))
            {
                string tool = c.ToolGuessed
                    ? (L.Current == Lang.Ko ? ErrorGuide.Ro(toolName) + " 추정" : L.F("ef.tool.guess", toolName))
                    : toolName;
                v.Chips.Add(new Chip { Text = tool, Kind = ChipKind.Gray, Dashed = c.ToolGuessed });
            }
            switch (c.Match)
            {
                case CardMatch.Confirmed:
                    v.Chips.Add(new Chip { Text = c.MaCode != null ? L.F("ef.match.code", c.MaCode) : L.F("ef.match.row", c.Row.no), Kind = ChipKind.Confirmed });
                    break;
                case CardMatch.Similar:
                    v.Chips.Add(new Chip { Text = L.F("ef.match.similar", c.Percent), Kind = ChipKind.Similar });
                    break;
                default:
                    v.Chips.Add(new Chip { Text = L.T("ef.match.none"), Kind = ChipKind.Gray });
                    break;
            }

            var where = new List<string>();
            foreach (var ch in c.Channels)
            {
                string label = ChannelLabel(ch);
                if (!where.Contains(label)) where.Add(label);
            }
            v.Found = L.F("ef.found", KindLabel(unit, false), string.Join(" · ", where));

            if (m_Targets != null && m_Targets.TryGetValue(c.Key, out var targets)) v.Targets = targets;
            if (c.PipelineTarget)
            {
                v.Pipeline = CachedPipelineTarget();
                // blueprint ID cards without a "how" choice show it in the first step like any other target
                if (c.Choice == null) v.Targets = new List<TargetView>(v.Targets) { v.Pipeline };
            }

            if (c.Choice != null && c.Choice.options != null && c.Choice.options.Length > 0)
            {
                v.Choice = m_Choice.TryGetValue(c.Key, out int ci) ? Mathf.Clamp(ci, 0, c.Choice.options.Length - 1) : 0;
                var option = c.Choice.options[v.Choice];
                foreach (var raw in option.steps ?? new string[0])
                {
                    var action = StepAction.None;
                    string s = raw;
                    if (s.Contains("{select}"))
                    {
                        action = StepAction.SelectPipeline;
                        s = s.Replace("{select}", string.Empty);
                    }
                    if (s.Contains("{sdk}"))
                    {
                        if (action == StepAction.None) action = StepAction.OpenSdk;
                        s = s.Replace("{sdk}", string.Empty);
                    }
                    v.Steps.Add(new CardStep(s.Trim(), action));
                }
                v.ChoiceNote = option.note;
            }
            else
            {
                v.Steps.AddRange(c.Steps);
                if (v.Targets.Count > 0)
                {
                    bool objects = false, folders = false;
                    foreach (var t in v.Targets)
                    {
                        if (t.Info || t.Missing) continue;
                        if (t.Folder != null || t.IsAsset) folders = true;
                        else objects = true;
                    }
                    bool fromType = objects;
                    foreach (var t in v.Targets)
                    {
                        if (t.Obj != null && !t.IsAsset && !t.FromType) fromType = false;
                    }
                    v.TargetStepText = objects && folders ? ErrorGuide.TargetStep
                        : fromType ? ErrorGuide.ComponentTargetStep
                        : objects ? ErrorGuide.TargetObjectsStep : ErrorGuide.TargetFoldersStep;
                }
            }
            return v;
        }

        static string SeverityLabel(string sev)
        {
            switch (sev)
            {
                case "업로드 차단": return L.T("ef.sev.upload");
                case "플레이 차단": return L.T("ef.sev.play");
                case "플레이·업로드 차단": return L.T("ef.sev.both");
                case "기능 깨짐": return L.T("ef.sev.broken");
                case "프로젝트 차단": return L.T("ef.sev.project");
                default: return L.T("ef.sev.unknown");
            }
        }

        /// <summary>The objects and folders each card points at, checked against the scene and the project now.</summary>
        void BuildTargets(FinderAnalysis a)
        {
            m_Targets = new Dictionary<string, List<TargetView>>();
            m_TargetsDirty = false;
            if (a == null) return;
            foreach (var c in a.Cards)
            {
                var list = new List<TargetView>();
                bool missing = false;
                var assetFolders = new List<string>();
                foreach (int id in c.ObjectIds)
                {
                    if (list.Count >= 8) break;
                    var obj = EditorUtility.InstanceIDToObject(id);
                    if (obj == null)
                    {
                        missing = true;
                        continue;
                    }
                    if (EditorUtility.IsPersistent(obj))
                    {
                        string path = AssetDatabase.GetAssetPath(obj);
                        if (string.IsNullOrEmpty(path)) continue;
                        list.Add(new TargetView { Obj = obj, IsAsset = true, Label = Arrowed(path) });
                        int slash = path.LastIndexOf('/');
                        if (slash > 0) assetFolders.Add(path.Substring(0, slash));
                    }
                    else
                    {
                        list.Add(new TargetView { Obj = obj, Label = HierarchyLabel(obj) });
                    }
                }
                foreach (var folder in c.Folders)
                {
                    if (list.Count >= 8) break;
                    if (assetFolders.Contains(folder) || !AssetDatabase.IsValidFolder(folder)) continue;
                    list.Add(new TargetView { Folder = folder, Label = Arrowed(folder) });
                }
                if (missing) list.Add(new TargetView { Missing = true, Label = L.T("ef.notFound") });
                AddComponentTargets(c, list);
                m_Targets[c.Key] = list;
            }
        }

        /// <summary>
        /// Tools the list does not know, with no object from NDMF or the console: the tool's components in the scene, found
        /// from the type names of its code in the stack trace (the [name: subject] object of the message first).
        /// </summary>
        static void AddComponentTargets(FinderCard c, List<TargetView> list)
        {
            if (c.TypeNames.Count == 0 && c.TypeRoots.Count == 0) return;
            foreach (var t in list)
            {
                if (t.Obj != null && !t.IsAsset) return;
            }
            List<Component> found;
            int more;
            try
            {
                found = ComponentFinder.Find(c.TypeNames, c.TypeRoots, c.Reading?.Subject, 4, out more);
            }
            catch (Exception)
            {
                return;
            }
            foreach (var comp in found) list.Add(new TargetView { Obj = comp, Label = HierarchyLabel(comp), FromType = true });
            if (more > 0) list.Add(new TargetView { Info = true, Label = L.F("ef.moreTargets", more) });
        }

        [NonSerialized] TargetView m_Pipeline;
        [NonSerialized] double m_PipelineTime = -10.0;

        /// <summary>The window repaints on every mouse move: look the SDK window up at most once a second.</summary>
        TargetView CachedPipelineTarget()
        {
            double now = EditorApplication.timeSinceStartup;
            if (m_Pipeline == null || m_TargetsDirty || now - m_PipelineTime > 1.0 || (m_Pipeline.Obj == null && !m_Pipeline.Missing))
            {
                m_Pipeline = PipelineTarget();
                m_PipelineTime = now;
            }
            return m_Pipeline;
        }

        /// <summary>Blueprint ID cards: the Pipeline Manager of the avatar chosen in the SDK window (or the only avatar in the scene).</summary>
        static TargetView PipelineTarget()
        {
            var avatar = SdkBridge.SelectedAvatar();
            if (avatar == null) avatar = OnlyAvatarInScene();
            if (avatar == null) return new TargetView { Missing = true, Label = L.T("ef.noPipeline") };
            Component pm = null;
            foreach (var comp in avatar.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().FullName == "VRC.Core.PipelineManager")
                {
                    pm = comp;
                    break;
                }
            }
            if (pm == null) return new TargetView { Missing = true, Label = L.T("ef.noPipeline") };
            string id = null;
            try
            {
                id = pm.GetType().GetField("blueprintId")?.GetValue(pm) as string;
            }
            catch (Exception)
            {
                id = null;
            }
            return new TargetView { Obj = pm, Label = avatar.name + " (Pipeline Manager)", Detail = ShortId(id) };
        }

        static GameObject OnlyAvatarInScene()
        {
            var type = FinderReflection.FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor", "VRC.SDK3A")
                       ?? FinderReflection.FindType("VRC.SDKBase.VRC_AvatarDescriptor", "VRC.SDKBase");
            if (type == null) return null;
            GameObject only = null;
            foreach (var o in Object.FindObjectsByType(type, FindObjectsSortMode.None))
            {
                if (!(o is Component c) || EditorUtility.IsPersistent(c)) continue;
                if (only != null) return null;
                only = c.gameObject;
            }
            return only;
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return id.Length > 16 ? id.Substring(0, 9) + "…" + id.Substring(id.Length - 4) : id;
        }

        static string HierarchyLabel(Object obj)
        {
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go == null) return obj.name;
            var names = new List<string>();
            for (var t = go.transform; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            string path = string.Join(" › ", names);
            if (obj is Component comp && !(obj is Transform)) path += " (" + ObjectNames.NicifyVariableName(comp.GetType().Name) + ")";
            return path;
        }

        static string Arrowed(string path) => path.Replace("/", " › ");

        // ---- drawing ----------------------------------------------------------------------------------------

        void DrawCard(Snapshot snap, CardView v)
        {
            var c = v.Card;
            using (var scope = new EditorGUILayout.VerticalScope(m_Styles.Card))
            {
                var head = EditorGUILayout.BeginHorizontal();
                var arrow = GUILayoutUtility.GetRect(14f, 18f, GUILayout.Width(14f), GUILayout.Height(18f));
                GUILayout.Label(v.Title, m_Styles.CardTitle, GUILayout.MinWidth(0f));
                if (v.CountText != null) GUILayout.Label(v.CountText, m_Styles.Count, GUILayout.ExpandWidth(false));
                EditorGUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint) EditorStyles.foldout.Draw(arrow, false, false, v.Open, false);
                GUILayout.Space(2f);
                DrawChips(v.Chips, AvailableWidth(CardInset));
                if (Clicked(head))
                {
                    m_Open[c.Key] = !v.Open;
                    Repaint();
                }
                if (v.Open) DrawCardBody(snap, v);
                if (Event.current.type == EventType.Repaint) Border(scope.rect, m_Styles.CardEdge, BarColor(c));
            }
        }

        Color BarColor(FinderCard c)
        {
            if (c.Match == CardMatch.Similar) return m_Styles.SimilarBar;
            return c.Severity.Length > 0 && c.Severity != "기능 깨짐" ? m_Styles.SevereBar : m_Styles.GrayBar;
        }

        void DrawChips(List<Chip> chips, float avail)
        {
            const float h = 18f, gap = 4f;
            var widths = new float[chips.Count];
            for (int i = 0; i < widths.Length; i++) widths[i] = Mathf.Ceil(m_Styles.Chip.CalcSize(new GUIContent(chips[i].Text)).x) + 2f;
            int rows = FlowRows(widths, avail, gap);
            var area = GUILayoutUtility.GetRect(avail, rows * h + (rows - 1) * gap, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            float x = area.x, y = area.y;
            for (int i = 0; i < chips.Count; i++)
            {
                if (x > area.x && x + widths[i] > area.x + avail)
                {
                    x = area.x;
                    y += h + gap;
                }
                var r = new Rect(x, y, Mathf.Min(widths[i], avail), h);
                var chip = chips[i];
                int k = (int)chip.Kind;
                if (chip.Dashed) Dashed(r, m_Styles.ChipText[1]);
                else Rounded(r, m_Styles.ChipBack[k], 4f);
                var style = m_Styles.Chip;
                var old = style.normal.textColor;
                style.normal.textColor = m_Styles.ChipText[k];
                style.Draw(r, new GUIContent(chip.Text), false, false, false, false);
                style.normal.textColor = old;
                x += widths[i] + gap;
            }
        }

        void DrawCardBody(Snapshot snap, CardView v)
        {
            var c = v.Card;
            GUILayout.Space(6f);
            GUILayout.Label(v.Found, m_Styles.SmallDim, GUILayout.MinWidth(0f));

            if (c.Match == CardMatch.Similar)
            {
                GUILayout.Space(4f);
                if (c.Notes.Count > 0) GUILayout.Label(c.Notes[0], m_Styles.Note, GUILayout.MinWidth(0f));
                if (c.Similar != null && FoldHeader(L.T("ef.sec.why"), v.WhyOpen))
                {
                    if (v.WhyOpen) m_WhyOpen.Remove(c.Key);
                    else m_WhyOpen.Add(c.Key);
                    Repaint();
                }
                if (v.WhyOpen && c.Similar != null)
                {
                    GUILayout.Label(L.T("ef.sec.received"), m_Styles.SmallDim);
                    GUILayout.Label(Highlight(c.Similar.Received, c.Similar.Shared), m_Styles.CodeRich, GUILayout.MinWidth(0f));
                    GUILayout.Label(L.T("ef.sec.listed"), m_Styles.SmallDim);
                    GUILayout.Label(Highlight(c.Similar.Listed, c.Similar.Shared), m_Styles.CodeRich, GUILayout.MinWidth(0f));
                }
            }

            if (c.Location.Count > 0)
            {
                Section(L.T("ef.sec.where"));
                foreach (var s in c.Location) GUILayout.Label(s, m_Styles.Body, GUILayout.MinWidth(0f));
                if (c.LocationLines.Count > 0)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < c.LocationLines.Count; i++)
                    {
                        if (i > 0) sb.Append('\n');
                        sb.Append(Mark(c.LocationLines[i], c.LocationMark));
                    }
                    GUILayout.Label(sb.ToString(), m_Styles.CodeRich, GUILayout.MinWidth(0f));
                }
            }

            if (c.Reading != null) DrawMessage(snap, c);

            if (!string.IsNullOrEmpty(c.Cause))
            {
                Section(L.T("ef.sec.cause"));
                GUILayout.Label(c.Cause, m_Styles.Body, GUILayout.MinWidth(0f));
            }

            if (c.Choice != null) DrawChoice(v);

            Section(L.T(c.FirstSteps ? "ef.sec.first" : "ef.sec.steps"));
            int n = 1;
            if (v.TargetStepText != null) DrawStep(snap, v, n++, new CardStep(v.TargetStepText, StepAction.Targets));
            foreach (var s in v.Steps) DrawStep(snap, v, n++, s);

            foreach (var s in c.Cautions) GUILayout.Label(s, m_Styles.Caution, GUILayout.MinWidth(0f));
            if (snap.Playing && (v.Targets.Count > 0 || v.Pipeline != null)) GUILayout.Label(ErrorGuide.PlayModeCaution, m_Styles.Caution, GUILayout.MinWidth(0f));
            for (int i = c.Match == CardMatch.Similar ? 1 : 0; i < c.Notes.Count; i++) GUILayout.Label(c.Notes[i], m_Styles.Note, GUILayout.MinWidth(0f));
            if (!string.IsNullOrEmpty(v.ChoiceNote)) GUILayout.Label(v.ChoiceNote, m_Styles.Note, GUILayout.MinWidth(0f));

            if (!string.IsNullOrEmpty(c.IfNot))
            {
                Section(L.T("ef.sec.ifNot"));
                GUILayout.Label(c.IfNot, m_Styles.Body, GUILayout.MinWidth(0f));
            }
            if (!string.IsNullOrEmpty(c.NearNote))
            {
                GUILayout.Space(4f);
                GUILayout.Label(c.NearNote, m_Styles.SmallDim, GUILayout.MinWidth(0f));
            }

            GUILayout.Space(6f);
            if (FoldHeader(L.F("ef.sec.raw", c.Items.Count), v.RawOpen))
            {
                if (v.RawOpen) m_RawOpen.Remove(c.Key);
                else m_RawOpen.Add(c.Key);
                Repaint();
            }
            if (v.RawOpen)
            {
                float width = AvailableWidth(CardInset);
                int shown = 0;
                foreach (var it in c.Items)
                {
                    if (shown++ >= 5) break;
                    string text = it.text.Length > 4000 ? it.text.Substring(0, 4000) + "…" : it.text;
                    float h = m_Styles.Raw.CalcHeight(new GUIContent(text), width);
                    EditorGUILayout.SelectableLabel(text, m_Styles.Raw, GUILayout.Height(Mathf.Min(h, 400f)), GUILayout.MinWidth(0f));
                    if (it.count > 1) GUILayout.Label(L.F("ef.card.count", it.count), m_Styles.SmallDim);
                }
            }
            if (c.Sources.Count > 0) DrawSources(c);
        }

        /// <summary>"메시지 내용": what the tool itself wrote, its instruction sentences in color, and the translate buttons.</summary>
        void DrawMessage(Snapshot snap, FinderCard c)
        {
            var r = c.Reading;
            Section(L.T("ef.sec.message"));
            GUILayout.Label(ErrorGuide.MessageCaption(c.ToolLabel ?? c.Tool, r.FromNdmf), m_Styles.Body, GUILayout.MinWidth(0f));
            GUILayout.Label(MarkAll(r.Body, r.Instructions), m_Styles.CodeRich, GUILayout.MinWidth(0f));
            if (r.Lang == TargetLanguage()) return;
            string tip = snap.Offline ? L.T("ef.translate.offline") : L.T("ef.translate.tip");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(snap.Offline))
                {
                    if (GUILayout.Button(new GUIContent(L.T("ef.btn.papago"), tip), m_Styles.MiniButton, GUILayout.ExpandWidth(false))) OpenTranslation(r, false);
                    if (GUILayout.Button(new GUIContent(L.T("ef.btn.google"), tip), m_Styles.MiniButton, GUILayout.ExpandWidth(false))) OpenTranslation(r, true);
                }
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>The tab's language as a translation target.</summary>
        static string TargetLanguage() => L.Current == Lang.Ja ? "ja" : L.Current == Lang.En ? "en" : "ko";

        /// <summary>
        /// Opens the message on Papago or Google Translate in the browser, only when the button is pressed. Names, paths and
        /// IDs are masked first (the same masking as the error log); nothing else of the project is sent.
        /// </summary>
        static void OpenTranslation(MessageReading r, bool google)
        {
            string text = ErrorCollector.MaskForSharing(r.Body);
            if (text.Length > 1000) text = text.Substring(0, 1000);
            string q = Uri.EscapeDataString(text);
            string target = TargetLanguage();
            string url = google
                ? "https://translate.google.com/?sl=auto&tl=" + target + "&text=" + q + "&op=translate"
                : "https://papago.naver.com/?sk=" + (r.Lang == "other" ? "auto" : r.Lang) + "&tk=" + target + "&hn=0&st=" + q;
            Application.OpenURL(url);
        }

        /// <summary>The text with each of <paramref name="parts"/> in color (the message's instruction sentences).</summary>
        string MarkAll(string text, List<string> parts)
        {
            string t = Plain(text);
            foreach (var p in parts)
            {
                string m = Plain(p);
                int k = m.Length > 0 ? t.IndexOf(m, StringComparison.Ordinal) : -1;
                if (k < 0) continue;
                t = t.Substring(0, k) + "<color=" + m_Styles.MarkHex + "><b>" + m + "</b></color>" + t.Substring(k + m.Length);
            }
            return t;
        }

        void DrawChoice(CardView v)
        {
            var ch = v.Card.Choice;
            GUILayout.Space(8f);
            GUILayout.Label(ch.question, m_Styles.BodyBold, GUILayout.MinWidth(0f));
            var labels = new string[ch.options.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = ch.options[i].label;
            int picked = GUILayout.Toolbar(v.Choice, labels, GUILayout.MinWidth(0f));
            if (picked != v.Choice)
            {
                m_Choice[v.Card.Key] = picked;
                Repaint();
            }
            if (!string.IsNullOrEmpty(ch.hint)) GUILayout.Label(ch.hint, m_Styles.SmallDim, GUILayout.MinWidth(0f));
        }

        void DrawStep(Snapshot snap, CardView v, int number, CardStep step)
        {
            GUILayout.Space(3f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var num = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
                if (Event.current.type == EventType.Repaint)
                {
                    Rounded(num, m_Styles.StepBack, 9f);
                    GUI.Label(num, number.ToString(), m_Styles.StepNumber);
                }
                GUILayout.Space(6f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(step.Text, m_Styles.Body, GUILayout.MinWidth(0f));
                    switch (step.Action)
                    {
                        case StepAction.Targets:
                            foreach (var t in v.Targets) DrawTarget(t);
                            break;
                        case StepAction.SelectPipeline:
                            if (v.Pipeline != null) DrawTarget(v.Pipeline);
                            break;
                        case StepAction.OpenSdk:
                            using (new EditorGUI.DisabledScope(SdkBridge.State == SourceState.Missing))
                            {
                                if (ActionButton(L.T("ef.btn.sdk"))) SdkBridge.OpenPanel();
                            }
                            break;
                        case StepAction.OpenNdmf:
                            using (new EditorGUI.DisabledScope(NdmfBridge.State == SourceState.Missing))
                            {
                                if (ActionButton(L.T("ef.btn.ndmf"))) NdmfBridge.OpenConsole();
                            }
                            break;
                        case StepAction.Recheck:
                            if (ActionButton(L.T("ef.btn.rescan"))) RescanNow();
                            break;
                    }
                }
            }
        }

        bool ActionButton(string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool clicked = GUILayout.Button(label, m_Styles.MiniButton, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                return clicked;
            }
        }

        void DrawTarget(TargetView t)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (t.Info)
                {
                    GUILayout.Label(t.Label, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                    return;
                }
                if (t.Missing)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUILayout.Button(L.T("ef.btn.select"), m_Styles.MiniButton, GUILayout.ExpandWidth(false));
                    }
                    GUILayout.Label(t.Label, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                    return;
                }
                bool folder = t.Folder != null || t.IsAsset;
                if (GUILayout.Button(L.T(folder ? "ef.btn.folder" : "ef.btn.select"), m_Styles.MiniButton, GUILayout.ExpandWidth(false)))
                {
                    if (folder) ShowInProject(t);
                    else SelectInScene(t.Obj);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(t.Label, m_Styles.Path, GUILayout.MinWidth(0f));
                    if (!string.IsNullOrEmpty(t.Detail)) GUILayout.Label(t.Detail, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                }
            }
        }

        /// <summary>[선택]: selects the object in the Hierarchy and pings it. Only the selection changes.</summary>
        static void SelectInScene(Object obj)
        {
            if (obj == null) return;
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            Selection.activeObject = go != null ? (Object)go : obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>[폴더 보기]: pings the file (or folder) in the Project window.</summary>
        static void ShowInProject(TargetView t)
        {
            var obj = t.Obj != null ? t.Obj : AssetDatabase.LoadAssetAtPath<Object>(t.Folder);
            if (obj != null) EditorGUIUtility.PingObject(obj);
        }

        void DrawSources(FinderCard c)
        {
            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L.T("ef.sec.source"), m_Styles.SmallDim, GUILayout.ExpandWidth(false));
                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (var s in c.Sources)
                    {
                        if (s.Value == null)
                        {
                            GUILayout.Label(s.Key, m_Styles.SmallDim, GUILayout.MinWidth(0f));
                            continue;
                        }
                        var content = new GUIContent(s.Key, s.Value);
                        var r = GUILayoutUtility.GetRect(content, m_Styles.Link, GUILayout.MinWidth(0f));
                        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                        if (GUI.Button(r, content, m_Styles.Link)) Application.OpenURL(s.Value);
                    }
                }
            }
        }

        bool FoldHeader(string label, bool open)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent(label), m_Styles.Foldout, GUILayout.MinWidth(0f));
            if (Event.current.type == EventType.Repaint) m_Styles.Foldout.Draw(r, new GUIContent(label), false, false, open, false);
            return Clicked(r);
        }

        void Section(string label)
        {
            GUILayout.Space(8f);
            GUILayout.Label(label, m_Styles.Section);
        }

        /// <summary>The line with the part that told where the error happened in color.</summary>
        string Mark(string line, string mark)
        {
            string text = Plain(line);
            if (string.IsNullOrEmpty(mark)) return text;
            string m = Plain(mark);
            int k = text.IndexOf(m, StringComparison.Ordinal);
            if (k < 0) return text;
            return text.Substring(0, k) + "<color=" + m_Styles.MarkHex + "><b>" + m + "</b></color>" + text.Substring(k + m.Length);
        }

        /// <summary>The words both sentences share, in color ("왜 비슷한가요").</summary>
        string Highlight(string sentence, List<string> shared)
        {
            string text = Plain(sentence);
            if (shared == null || shared.Count == 0) return text;
            var set = new HashSet<string>(shared, StringComparer.Ordinal);
            var sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsLetterOrDigit(text[i]) || text[i] == '_')
                {
                    int j = i;
                    while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;
                    string word = text.Substring(i, j - i);
                    if (set.Contains(word.ToLowerInvariant())) sb.Append("<color=").Append(m_Styles.MarkHex).Append("><b>").Append(word).Append("</b></color>");
                    else sb.Append(word);
                    i = j;
                }
                else
                {
                    sb.Append(text[i]);
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
