using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    internal enum MatchKind
    {
        /// <summary>Not in the list and no location rule (general guidance).</summary>
        None,
        /// <summary>A harmless message on the ignore list: no card.</summary>
        Ignore,
        /// <summary>A row of the list (confirmed, or "similar" when How starts with 비슷).</summary>
        Entry,
        /// <summary>Not in the list, but a location rule names the tool (P2 ~ P14).</summary>
        Rule,
    }

    /// <summary>What the matcher found for one message.</summary>
    internal sealed class MatchResult
    {
        public MatchKind Kind = MatchKind.None;
        /// <summary>Row number (1-based) for Entry.</summary>
        public int No;
        /// <summary>Rule id (P2 …) or ignore id (I01 …).</summary>
        public string Rule;
        public string Tool;
        /// <summary>키워드 · 틀 · 코드 · 코드·비슷 NN% · 비슷 NN% · 위치 규칙 · 무시 목록 · 코드 규칙</summary>
        public string How;
        /// <summary>Row number of the wrapping sentence when the card came from the sentence inside it (0 = none).</summary>
        public int Wrapped;
        /// <summary>Rule cards: a similar row above the threshold (0 = none) and its percentage.</summary>
        public int HintNo;
        public int HintPercent;
        /// <summary>Best similarity found in the similarity step (also when below the threshold), for "가장 가까운 오류도 NN%".</summary>
        public double Score = -1.0;
        public int ScoreNo;

        public bool IsSimilar => Kind == MatchKind.Entry && How != null && How.StartsWith("비슷", StringComparison.Ordinal);
        public bool IsConfirmed => Kind == MatchKind.Entry && !IsSimilar;
    }

    /// <summary>
    /// The error matcher of LITTOOL v1.3: the C# form of the Python simulator the error list was measured with
    /// (sim/pipe3.py, list version v7.1). The order, the tie-breaks and the numbers are kept identical so that a
    /// message gets the same card in Unity as in the measurements.
    ///
    /// ignore list → MA code (the row with that code, else P14) → keyword (longest wins) → template →
    /// wrapped message (the sentence inside first) → location rules → similarity (≥ 65 %, first line ≥ 30, never MA rows,
    /// only the tool's groups when the tool is known) → rules without a location (P4, P9, P11, log prefixes) → none.
    /// </summary>
    internal sealed class ErrorMatcher
    {
        const int LongPathRowNo = 212;
        static readonly int[] CallbackRows = { 27, 28, 29 };

        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex s_FrameLine = new Regex(@"^\s*(?:at\s+)?([A-Za-z_][\w`]*(?:[.+][\w`<>\[\],]+)+)", Opt);
        static readonly Regex s_Compile = new Regex(@"error CS\d{4}", Opt);
        static readonly Regex s_Exception = new Regex(@"^\s*([A-Za-z_][\w.]*Exception)\b", Opt);
        static readonly Regex s_PathAny = new Regex(@"(?:^|[\s(/\\.])((?:Packages|Assets)[\\/][^\s:()]+)", Opt);
        static readonly Regex s_LeadPath = new Regex(@"(?:\.[\\/])?((?:Packages|Assets)[\\/][^\s(]+?)\(\d+,\d+\)\s*:\s*error CS\d{4}", Opt);
        static readonly Regex s_MaCode = new Regex(@"\[(MA-\d{4})\]", Opt);
        static readonly Regex s_LongPath = new Regex(@"Could not find a part of the path\s*['‘]([^'’]+)['’]", Opt);
        static readonly Regex s_VrcType = new Regex(@"'VRC\w+'", Opt);
        static readonly Regex s_Callback = new Regex(@"Callback\s+'([^']+)'|'([^']+)'\s+(?:reported a failure|threw an exception)|'([^']+)' VRCSDK", Opt);
        static readonly Regex s_PackagePath = new Regex(@"^Packages/([^/]+)/", Opt);
        static readonly Regex s_AssetPath = new Regex(@"^Assets/([^/]+)/", Opt);
        static readonly Regex s_TemplateSplit = new Regex(@"<v>|<n>|<path>|<guid>|<url>|<id>", Opt);

        struct Keyword
        {
            public int Length;
            public Regex Pattern;
            public int Row;
        }

        struct TemplateDoc
        {
            public int Row;
            public int Length;
            public string[] Chunks;  // null = too short to use
        }

        readonly ErrorCatalogData m_Data;
        readonly List<Keyword> m_Keywords = new List<Keyword>();
        readonly List<Keyword> m_KeywordsByLength;
        readonly List<KeyValuePair<Regex, string>> m_Ignore = new List<KeyValuePair<Regex, string>>();
        readonly Dictionary<string, List<int>> m_CodeRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        readonly HashSet<int> m_MaRows = new HashSet<int>();
        readonly HashSet<int> m_Wrappers = new HashSet<int>();
        readonly Dictionary<string, int[]> m_ToolGroups = new Dictionary<string, int[]>(StringComparer.Ordinal);
        readonly Dictionary<string, string> m_AssetFolders = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<TemplateDoc> m_Templates = new List<TemplateDoc>();
        readonly ErrorTfidf m_Index;
        readonly int[] m_IndexRows;

        /// <summary>Tests only: rows treated as if the list did not have them (0-based indices).</summary>
        public readonly HashSet<int> Excluded = new HashSet<int>();

        public ErrorCatalogData Data => m_Data;
        public double Threshold => m_Data.threshold;

        public ErrorMatcher(ErrorCatalogData data)
        {
            m_Data = data ?? throw new ArgumentNullException(nameof(data));
            var rows = data.rows ?? new CatalogRow[0];

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                foreach (var alt in r.keywords ?? new string[0])
                    m_Keywords.Add(new Keyword { Length = alt.Length, Pattern = ErrorText.KeywordPattern(alt), Row = i });
                foreach (var code in r.maCodes ?? new string[0])
                {
                    if (!m_CodeRows.TryGetValue(code, out var list)) m_CodeRows[code] = list = new List<int>();
                    list.Add(i);
                    m_MaRows.Add(i);
                }
                if (r.result == "감쌈") m_Wrappers.Add(i);
            }
            // a stable sort, longest first (the wrapper step looks for the longest keyword of the wrapping row)
            m_KeywordsByLength = new List<Keyword>(m_Keywords);
            StableSortByLengthDescending(m_KeywordsByLength);

            foreach (var ig in data.ignore ?? new CatalogIgnore[0])
            {
                foreach (var alt in ig.patterns ?? new string[0])
                    m_Ignore.Add(new KeyValuePair<Regex, string>(ErrorText.KeywordPattern(alt), ig.id));
            }
            foreach (var g in data.toolGroups ?? new CatalogToolGroup[0]) m_ToolGroups[g.tool] = g.groups ?? new int[0];
            foreach (var f in data.assetFolders ?? new CatalogAssetFolder[0]) m_AssetFolders[f.folder] = f.tool;

            foreach (var doc in data.docs ?? new CatalogDoc[0])
            {
                string d = ErrorText.Normalize(doc.text);
                var t = new TemplateDoc { Row = doc.row - 1, Length = d.Length };
                string core = d.Replace("<v>", "").Trim();
                if (ErrorText.WeightedLength(core, data.cjkWeight) >= 12)
                {
                    var chunks = new List<string>();
                    foreach (var c in s_TemplateSplit.Split(d))
                    {
                        string s = c.Trim();
                        if (s.Length >= 4) chunks.Add(s);
                    }
                    t.Chunks = chunks.ToArray();
                }
                m_Templates.Add(t);
            }

            var index = data.index ?? new CatalogDoc[0];
            var texts = new List<string>(index.Length);
            m_IndexRows = new int[index.Length];
            for (int k = 0; k < index.Length; k++)
            {
                texts.Add(ErrorText.Normalize(index[k].text));
                m_IndexRows[k] = index[k].row - 1;
            }
            m_Index = new ErrorTfidf(texts);
        }

        static void StableSortByLengthDescending(List<Keyword> list)
        {
            // insertion sort keeps equal lengths in their original order
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].Length < item.Length)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }

        public CatalogRow Row(int no) => no >= 1 && no <= m_Data.rows.Length ? m_Data.rows[no - 1] : null;

        // ---------------------------------------------------------------- steps
        int KeywordMatch(string text)
        {
            int best = -1, bestLen = 0;
            foreach (var k in m_Keywords)
            {
                if (Excluded.Contains(k.Row)) continue;
                if (k.Length > bestLen && k.Pattern.IsMatch(text))
                {
                    best = k.Row;
                    bestLen = k.Length;
                }
            }
            return best;
        }

        int TemplateMatch(string text)
        {
            string t = ErrorText.Normalize(text);
            int best = -1, bestLen = 0;
            foreach (var d in m_Templates)
            {
                if (m_MaRows.Contains(d.Row) || Excluded.Contains(d.Row)) continue;  // MA rows are matched by their code only
                if (d.Chunks == null) continue;
                int pos = 0;
                bool ok = true;
                foreach (var c in d.Chunks)
                {
                    int k = t.IndexOf(c, pos, StringComparison.Ordinal);
                    if (k < 0)
                    {
                        ok = false;
                        break;
                    }
                    pos = k + c.Length;
                }
                if (ok && d.Chunks.Length > 0 && d.Length > bestLen)
                {
                    best = d.Row;
                    bestLen = d.Length;
                }
            }
            return best;
        }

        /// <summary>The most similar row (first in index order on a tie) and its score, within <paramref name="allowed"/> groups.</summary>
        void Similar(string text, int[] allowed, out int row, out double score)
        {
            var sims = m_Index.Similarities(ErrorText.Normalize(text));
            var best = new Dictionary<int, double>();
            var order = new List<int>();
            for (int k = 0; k < sims.Length; k++)
            {
                int i = m_IndexRows[k];
                if (m_MaRows.Contains(i) || Excluded.Contains(i)) continue;  // never by the look of another MA code's sentence
                if (allowed != null && Array.IndexOf(allowed, m_Data.rows[i].group) < 0) continue;
                if (best.TryGetValue(i, out double old))
                {
                    if (sims[k] > old) best[i] = sims[k];
                }
                else
                {
                    best[i] = Math.Max(-1.0, sims[k]);
                    order.Add(i);
                }
            }
            row = -1;
            score = 0.0;
            foreach (int i in order)
            {
                if (row < 0 || best[i] > score)
                {
                    row = i;
                    score = best[i];
                }
            }
        }

        bool SimilarityAllowed(string text) => ErrorText.FirstLineLength(text, m_Data.cjkWeight) >= m_Data.minSimLen;

        /// <summary>Is the first line long enough to be compared with the list (the 30-character rule)?</summary>
        public bool LongEnough(string text) => SimilarityAllowed(text ?? string.Empty);

        /// <summary>
        /// The most similar row and its score (0..1), with the same limits as the similarity step (no MA rows, the tool's
        /// groups). For "가장 가까운 오류도 NN%" on cards and for the error log; it never changes the match.
        /// </summary>
        public void Nearest(string text, out int no, out double score)
        {
            text = text ?? string.Empty;
            Similar(text, SimilarityGroups(text), out int i, out score);
            no = i >= 0 ? m_Data.rows[i].no : 0;
        }

        public static int ToPercent(double score) => Percent(score);

        int[] SimilarityGroups(string text)
        {
            string tool = DetectTool(text, true);
            return tool != null && m_ToolGroups.TryGetValue(tool, out var groups) ? groups : null;
        }

        static List<string> Frames(string text)
        {
            var list = new List<string>();
            var lines = text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var m = s_FrameLine.Match(lines[i]);
                if (m.Success) list.Add(m.Groups[1].Value);
            }
            return list;
        }

        public string ToolOfNamespace(string ns)
        {
            foreach (var t in m_Data.tools ?? new CatalogTool[0])
            {
                foreach (var p in t.prefixes ?? new string[0])
                {
                    if (ns.StartsWith(p, StringComparison.Ordinal)) return t.tool;
                }
            }
            return null;
        }

        public string ToolOfPath(string path)
        {
            string p = path.Replace('\\', '/');
            var m = s_PackagePath.Match(p);
            if (m.Success)
            {
                string pkg = m.Groups[1].Value;
                foreach (var t in m_Data.tools ?? new CatalogTool[0])
                {
                    if (Array.IndexOf(t.packages ?? new string[0], pkg) >= 0) return t.tool;
                }
                if (pkg.StartsWith("com.unity.", StringComparison.Ordinal)) return "Unity 패키지(" + pkg + ")";
                return pkg;
            }
            m = s_AssetPath.Match(p);
            if (m.Success)
            {
                string folder = m.Groups[1].Value;
                return m_AssetFolders.TryGetValue(folder, out var tool) ? tool : "Assets/" + folder;
            }
            return null;
        }

        string CallbackTool(string text)
        {
            var m = s_Callback.Match(text);
            if (!m.Success) return null;
            string name = null;
            for (int g = 1; g <= 3 && name == null; g++)
            {
                if (m.Groups[g].Success) name = m.Groups[g].Value;
            }
            foreach (var cb in m_Data.callbacks ?? new CatalogCallback[0])
            {
                if (Array.IndexOf(cb.names ?? new string[0], name) >= 0) return cb.tool;
            }
            return null;
        }

        /// <summary>Tool shown on the card: a path in the text, the first tool stack frame, then (optionally) a log prefix.</summary>
        public string DetectTool(string text, bool prefixes)
        {
            text = ErrorText.PackageCacheAsPackages(text);
            var m = s_LeadPath.Match(text);
            if (!m.Success) m = s_PathAny.Match(text);
            if (m.Success)
            {
                string t = ToolOfPath(m.Groups[1].Value);
                if (t != null) return t;
            }
            foreach (var ns in Frames(text))
            {
                string t = ToolOfNamespace(ns);
                if (t != null) return t;
            }
            if (prefixes)
            {
                if (text.Contains("[MA-")) return "Modular Avatar";
                if (text.Contains("[NDMF")) return "NDMF";
                if (text.Contains("[AAO")) return "Avatar Optimizer";
            }
            return null;
        }

        struct RuleHit
        {
            public string Id;
            public string Tool;
            public int Row;  // -1 = none
        }

        /// <summary>
        /// Location rules. <paramref name="locationOnly"/> = only rules decided by a real location (stack frame, file path,
        /// path length); P4 (compile error without a path), P9 (decided by the message) and P11 (no stack) are left for later.
        /// </summary>
        bool Rules(string text, bool locationOnly, out RuleHit hit)
        {
            hit = new RuleHit { Row = -1 };
            text = ErrorText.PackageCacheAsPackages(text);

            var lp = s_LongPath.Match(text);
            if (lp.Success && lp.Groups[1].Value.Length >= 200)
            {
                hit.Id = "P10";
                hit.Row = LongPathRowNo - 1;
                return true;
            }

            if (s_Compile.IsMatch(text))
            {
                var m = s_LeadPath.Match(text);
                if (m.Success)
                {
                    string path = m.Groups[1].Value.Replace('\\', '/');
                    hit.Tool = ToolOfPath(path);
                    hit.Id = path.StartsWith("Packages/", StringComparison.Ordinal) ? "P2" : "P3";
                    return true;
                }
                if (locationOnly) return false;
                hit.Id = "P4";
                return true;
            }

            var exc = s_Exception.Match(text);
            if (exc.Success)
            {
                var frames = Frames(text);
                string first = null;
                foreach (var ns in frames)
                {
                    if (ToolOfNamespace(ns) != null)
                    {
                        first = ns;
                        break;
                    }
                }
                if (first != null && first.StartsWith("nadena.dev.ndmf.preview", StringComparison.Ordinal))
                {
                    hit.Id = "P13";
                    hit.Tool = "NDMF 미리보기";
                    return true;
                }
                // paths only from the lines under the message: a path inside the message is the file the error is about
                int nl = text.IndexOf('\n');
                string where = nl >= 0 ? text.Substring(nl + 1) : string.Empty;
                foreach (Match pm in s_PathAny.Matches(where))
                {
                    string p = pm.Groups[1].Value;
                    string tool = ToolOfPath(p);
                    if (p.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                    {
                        hit.Id = "P8";
                        hit.Tool = tool;
                        return true;
                    }
                    if (tool != null)
                    {
                        hit.Id = tool == "VRChat SDK" ? "P6" : "P5";
                        hit.Tool = tool;
                        return true;
                    }
                }
                foreach (var ns in frames)
                {
                    string tool = ToolOfNamespace(ns);
                    if (tool != null)
                    {
                        hit.Id = tool == "VRChat SDK" ? "P6" : "P5";
                        hit.Tool = tool;
                        return true;
                    }
                }
                if (frames.Count > 0)
                {
                    foreach (var ns in frames)
                    {
                        foreach (var u in m_Data.unityUi ?? new string[0])
                        {
                            if (ns.StartsWith(u, StringComparison.Ordinal))
                            {
                                hit.Id = "P7";
                                hit.Tool = "Unity 화면";
                                return true;
                            }
                        }
                    }
                }
                if (locationOnly) return false;
                if (exc.Groups[1].Value.EndsWith("MissingReferenceException", StringComparison.Ordinal) && s_VrcType.IsMatch(text))
                {
                    hit.Id = "P9";
                    hit.Tool = "VRChat SDK";
                    return true;
                }
                if (frames.Count == 0)
                {
                    hit.Id = "P11";
                    return true;
                }
                return false;
            }

            string t2 = DetectTool(text, !locationOnly);
            if (t2 != null)
            {
                hit.Id = "P12";
                hit.Tool = t2;
                return true;
            }
            return false;
        }

        void ApplyRule(MatchResult res, RuleHit hit)
        {
            if (hit.Row >= 0)
            {
                res.Kind = MatchKind.Entry;
                res.No = m_Data.rows[hit.Row].no;
                res.Rule = hit.Id;
                res.How = "위치 규칙";
            }
            else if (hit.Id == "P11")
            {
                res.Kind = MatchKind.None;
                res.Rule = hit.Id;
                res.How = "위치 규칙";
            }
            else
            {
                res.Kind = MatchKind.Rule;
                res.Rule = hit.Id;
                res.Tool = hit.Tool;
                res.How = "위치 규칙";
            }
        }

        /// <summary>Where the wrapped sentence can be: after the wrapper's keyword, then the whole text without the keyword.</summary>
        List<KeyValuePair<string, bool>> InnerTexts(string text, int wrapperRow)
        {
            var list = new List<KeyValuePair<string, bool>>();
            foreach (var k in m_KeywordsByLength)
            {
                if (k.Row != wrapperRow) continue;
                var m = k.Pattern.Match(text);
                if (!m.Success) continue;
                int end = m.Index + m.Length;
                string after = ErrorText.TrimStartChars(text.Substring(end), " :.\n").TrimEnd();
                string rest = (text.Substring(0, m.Index) + " " + text.Substring(end)).Trim();
                list.Add(new KeyValuePair<string, bool>(after, true));
                list.Add(new KeyValuePair<string, bool>(rest, false));
                return list;
            }
            return list;
        }

        int WrapperIn(string text)
        {
            foreach (var k in m_Keywords)
            {
                if (m_Wrappers.Contains(k.Row) && !Excluded.Contains(k.Row) && k.Pattern.IsMatch(text)) return m_Data.rows[k.Row].no;
            }
            return 0;
        }

        MatchResult Finish(MatchResult res, string text)
        {
            if (res.Kind == MatchKind.Entry && Array.IndexOf(CallbackRows, res.No) >= 0)
                res.Tool = CallbackTool(text) ?? res.Tool;
            if (res.Tool == null) res.Tool = DetectTool(text, true);
            return res;
        }

        static int Percent(double s) => (int)Math.Round(100.0 * s, MidpointRounding.ToEven);

        // ---------------------------------------------------------------- main
        public MatchResult Analyze(string text) => Analyze(text ?? string.Empty, 0);

        MatchResult Analyze(string text, int depth)
        {
            var res = new MatchResult();
            foreach (var ig in m_Ignore)
            {
                if (ig.Key.IsMatch(text))
                {
                    res.Kind = MatchKind.Ignore;
                    res.Rule = ig.Value;
                    res.How = "무시 목록";
                    return res;
                }
            }

            // an MA code decides on its own: the row with that code, or P14 (also inside a wrapped sentence)
            var codes = s_MaCode.Matches(text);
            if (codes.Count > 0)
            {
                int hitRow = -1;
                string hitHow = null;
                foreach (Match cm in codes)
                {
                    if (!m_CodeRows.TryGetValue(cm.Groups[1].Value, out var all)) continue;
                    var cands = new List<int>();
                    foreach (int i in all)
                    {
                        if (!Excluded.Contains(i)) cands.Add(i);
                    }
                    if (cands.Count == 0) continue;

                    int kwRow = -1, kwLen = -1;
                    foreach (var k in m_Keywords)
                    {
                        if (cands.Contains(k.Row) && k.Pattern.IsMatch(text) && k.Length > kwLen)
                        {
                            kwRow = k.Row;
                            kwLen = k.Length;
                        }
                    }
                    int byCode = -1;
                    foreach (int i in cands)
                    {
                        if (m_Data.rows[i].codeIsKeyword)
                        {
                            byCode = i;
                            break;
                        }
                    }
                    if (kwRow >= 0)
                    {
                        hitRow = kwRow;
                        hitHow = "코드";
                    }
                    else if (byCode >= 0)
                    {
                        hitRow = byCode;
                        hitHow = "코드";
                    }
                    else
                    {
                        // MA reuses a few codes for different components: the rows with this code are told apart by their sentence
                        var sims = m_Index.Similarities(ErrorText.Normalize(text));
                        double bestScore = double.NegativeInfinity;
                        int bestRow = -1;
                        for (int k = 0; k < sims.Length; k++)
                        {
                            int i = m_IndexRows[k];
                            if (!cands.Contains(i)) continue;
                            // Python max over (score, row): a higher score wins, on a tie the higher row
                            if (sims[k] > bestScore || (sims[k] == bestScore && i > bestRow))
                            {
                                bestScore = sims[k];
                                bestRow = i;
                            }
                        }
                        if (bestRow >= 0 && bestScore >= m_Data.threshold)
                        {
                            hitRow = bestRow;
                            hitHow = "코드·비슷 " + Percent(bestScore) + "%";
                        }
                    }
                    if (hitRow >= 0) break;
                }
                if (hitRow >= 0)
                {
                    res.Kind = MatchKind.Entry;
                    res.No = m_Data.rows[hitRow].no;
                    res.How = hitHow;
                }
                else
                {
                    res.Kind = MatchKind.Rule;
                    res.Rule = "P14";
                    res.Tool = "Modular Avatar";
                    res.How = "코드 규칙";
                }
                int w = WrapperIn(text);
                if (w > 0) res.Wrapped = w;
                return Finish(res, text);
            }

            int kw = KeywordMatch(text);
            if (kw >= 0)
            {
                res.Kind = MatchKind.Entry;
                res.No = m_Data.rows[kw].no;
                res.How = "키워드";
            }
            else
            {
                int tp = TemplateMatch(text);
                if (tp >= 0)
                {
                    res.Kind = MatchKind.Entry;
                    res.No = m_Data.rows[tp].no;
                    res.How = "틀";
                }
            }

            // a wrapping sentence: if the sentence inside it is in the list, show that card
            if (res.Kind == MatchKind.Entry && m_Wrappers.Contains(res.No - 1) && depth < 2)
            {
                foreach (var inner in InnerTexts(text, res.No - 1))
                {
                    if (string.IsNullOrEmpty(inner.Key)) continue;
                    var r2 = Analyze(inner.Key, depth + 1);
                    bool listed = r2.Kind == MatchKind.Entry && !(r2.How ?? "").StartsWith("비슷", StringComparison.Ordinal);
                    if (listed && !inner.Value && m_Wrappers.Contains(r2.No - 1)) continue;  // the text before is the outer wrapper
                    if (listed || r2.Rule == "P14")
                    {
                        r2.Wrapped = res.No;
                        return Finish(r2, text);
                    }
                }
            }

            // decided 2026-10-15: a certain location first, then similarity, then the rules without a location
            if (res.Kind == MatchKind.None)
            {
                if (Rules(text, true, out var hit)) ApplyRule(res, hit);
                if (res.Kind == MatchKind.Rule && SimilarityAllowed(text))
                {
                    Similar(text, SimilarityGroups(text), out int i, out double s);
                    if (i >= 0 && s >= m_Data.threshold)
                    {
                        res.HintNo = m_Data.rows[i].no;
                        res.HintPercent = Percent(s);
                    }
                }
            }
            if (res.Kind == MatchKind.None && res.Rule == null && s_MaCode.IsMatch(text))
            {
                res.Kind = MatchKind.Rule;
                res.Rule = "P14";
                res.Tool = "Modular Avatar";
                res.How = "코드 규칙";
            }
            if (res.Kind == MatchKind.None && res.Rule == null)
            {
                Similar(text, SimilarityGroups(text), out int i, out double s);
                res.Score = s;
                res.ScoreNo = i >= 0 ? m_Data.rows[i].no : 0;
                if (i >= 0 && s >= m_Data.threshold && SimilarityAllowed(text))
                {
                    res.Kind = MatchKind.Entry;
                    res.No = m_Data.rows[i].no;
                    res.How = "비슷 " + Percent(s) + "%";
                }
            }
            if (res.Kind == MatchKind.None && res.Rule == null)
            {
                if (Rules(text, false, out var hit)) ApplyRule(res, hit);
            }
            return Finish(res, text);
        }

        /// <summary>The same one-line description as the simulator (used for logs and the parity test).</summary>
        public static string Describe(MatchResult res)
        {
            string s;
            switch (res.Kind)
            {
                case MatchKind.Ignore:
                    return "무시 " + res.Rule;
                case MatchKind.Entry:
                    s = "#" + res.No + " " + res.How;
                    if (res.Wrapped > 0) s += " (감싼 #" + res.Wrapped + ")";
                    break;
                case MatchKind.Rule:
                    s = "규칙 " + res.Rule;
                    if (res.Wrapped > 0) s += " (감싼 #" + res.Wrapped + ")";
                    if (res.HintNo > 0) s += " (비슷 #" + res.HintNo + " " + res.HintPercent + "%)";
                    break;
                default:
                    s = "없음" + (res.Rule != null ? "(규칙 " + res.Rule + ")" : "");
                    break;
            }
            if (!string.IsNullOrEmpty(res.Tool)) s += " · " + res.Tool;
            return s;
        }
    }
}
