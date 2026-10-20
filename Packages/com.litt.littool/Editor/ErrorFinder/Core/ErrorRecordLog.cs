using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// 문서\LITTOOL\오류 기록\YYYY-MM.txt: the errors LITTOOL found, kept on this PC only, to grow the error list later
    /// (decided 2026-10-15). An error that matched the list exactly only raises a monthly count; the others are written
    /// in full with the closest row of the list. User and PC names, avatar names, VRChat IDs, e-mail addresses and the
    /// project folder are masked. Nothing is sent anywhere. Only automatic units are written ([다시 검사] is not).
    /// </summary>
    internal static class ErrorRecordLog
    {
        public const string FolderName = "오류 기록";
        const string CountHeader = "== 목록과 맞은 오류 (이번 달 횟수 · 검사 한 번에 1) ==";
        const string DetailHeader = "== 목록과 똑같지 않은 오류 ==";
        const long MaxBytes = 5L * 1024 * 1024;
        const int MaxLinesPerMessage = 40;
        const int MaxCharsPerMessage = 4000;
        const int MaxMessagesPerCard = 3;

        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex s_CountLine = new Regex(@"^#(\d+)\t(\d+)\t(.*)$", Opt);
        static readonly Regex s_WinUser = new Regex(@"([A-Za-z]:[\\/](?:Users|Documents and Settings)[\\/])[^\\/\r\n'""<>|]+", Opt | RegexOptions.IgnoreCase);
        static readonly Regex s_UnixUser = new Regex(@"(/(?:Users|home)/)[^/\r\n'""<>|\s]+", Opt);
        static readonly Regex s_VrcId = new Regex(@"\b(avtr|usr|wrld|file|grp|prnt|inst)_[0-9A-Za-z]{2,}(?:-[0-9A-Za-z]+)*", Opt);
        static readonly Regex s_Email = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", Opt);

        /// <summary>The folder: Documents\LITTOOL\오류 기록 (문서\LITTOOL\오류 기록 on a Korean Windows).</summary>
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LITTOOL", FolderName);

        public static string FileFor(DateTime when, string folder = null) =>
            Path.Combine(folder ?? Folder, when.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".txt");

        public static void Write(FinderAnalysis a, ErrorCatalogData data, IList<string> secrets, string projectRoot, DateTime now,
                                 string littoolVersion, string folder = null)
        {
            if (a == null || a.Unit == null || a.Empty) return;
            folder = folder ?? Folder;
            string path = FileFor(now, folder);
            Directory.CreateDirectory(folder);

            var counts = new Dictionary<int, KeyValuePair<int, string>>();
            string details = string.Empty;
            bool full = false;
            if (File.Exists(path))
            {
                full = new FileInfo(path).Length > MaxBytes;
                string old = File.ReadAllText(path, Encoding.UTF8);
                int ci = old.IndexOf(CountHeader, StringComparison.Ordinal);
                int di = old.IndexOf(DetailHeader, StringComparison.Ordinal);
                if (ci >= 0)
                {
                    int end = di > ci ? di : old.Length;
                    foreach (var raw in old.Substring(ci, end - ci).Split('\n'))
                    {
                        var m = s_CountLine.Match(raw.TrimEnd('\r'));
                        if (m.Success) counts[int.Parse(m.Groups[1].Value)] = new KeyValuePair<int, string>(int.Parse(m.Groups[2].Value), m.Groups[3].Value);
                    }
                }
                if (di >= 0) details = old.Substring(di + DetailHeader.Length).Trim('\r', '\n');
            }

            var records = new StringBuilder();
            foreach (var c in a.Cards)
            {
                if (c.Match == CardMatch.Confirmed && c.Row != null)
                {
                    counts.TryGetValue(c.Row.no, out var prev);
                    counts[c.Row.no] = new KeyValuePair<int, string>(prev.Key + 1, c.Row.what);
                    continue;
                }
                if (full) continue;
                AppendRecord(records, a.Unit, c, data, secrets, projectRoot, now);
            }

            var sb = new StringBuilder();
            string nl = Environment.NewLine;
            sb.Append("LITTOOL 오류 기록 · ").Append(now.ToString("yyyy-MM", CultureInfo.InvariantCulture)).Append(nl);
            sb.Append("LITTOOL ").Append(littoolVersion).Append(" · 오류 목록 ").Append(data?.version).Append(" (")
              .Append(data?.rows?.Length ?? 0).Append("개)").Append(nl);
            sb.Append("LITTOOL이 찾은 오류를 이 PC에만 적어 두는 파일이에요. 어디로도 보내지 않아요.").Append(nl);
            sb.Append("사용자 이름·PC 이름·아바타 이름·VRChat ID·이메일·프로젝트 폴더는 가려서 적어요.").Append(nl).Append(nl);
            sb.Append(CountHeader).Append(nl);
            var nos = new List<int>(counts.Keys);
            nos.Sort((x, y) =>
            {
                int k = counts[y].Key.CompareTo(counts[x].Key);
                return k != 0 ? k : x.CompareTo(y);
            });
            foreach (int no in nos) sb.Append('#').Append(no).Append('\t').Append(counts[no].Key).Append('\t').Append(counts[no].Value).Append(nl);
            if (nos.Count == 0) sb.Append("(아직 없음)").Append(nl);
            sb.Append(nl).Append(DetailHeader).Append(nl).Append(nl);
            if (details.Length > 0) sb.Append(details).Append(nl).Append(nl);
            sb.Append(records);
            if (full) sb.Append("(이 달 파일이 5MB를 넘어 새 기록은 횟수만 셉니다)").Append(nl);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static void AppendRecord(StringBuilder sb, FinderUnit unit, FinderCard c, ErrorCatalogData data, IList<string> secrets,
                                 string projectRoot, DateTime now)
        {
            string nl = Environment.NewLine;
            sb.Append("---- ").Append(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
              .Append(" · ").Append(UnitLabel(unit))
              .Append(" · ").Append(Mask(ErrorMatcher.Describe(c.First), secrets, projectRoot))
              .Append(c.ToolLabel != null ? " · " + Mask(c.ToolLabel, secrets, projectRoot) : string.Empty)
              .Append(" · ").Append(c.Count).Append("번 · 목록 ").Append(data?.version).Append(nl);
            if (c.NearestNo > 0 && c.NearestPercent >= 0)
            {
                string what = string.Empty;
                if (data?.rows != null && c.NearestNo <= data.rows.Length) what = data.rows[c.NearestNo - 1].what;
                sb.Append("가장 가까운 목록 오류: #").Append(c.NearestNo).Append(' ').Append(c.NearestPercent).Append("% — ").Append(what).Append(nl);
            }
            else if (c.Match == CardMatch.Similar && c.Row != null)
            {
                sb.Append("비슷하다고 안내한 목록 오류: #").Append(c.Row.no).Append(' ').Append(c.Percent).Append("% — ").Append(c.Row.what).Append(nl);
            }
            int n = 0;
            foreach (var it in c.Items)
            {
                if (n++ >= MaxMessagesPerCard) break;
                if (n > 1) sb.Append("·").Append(nl);
                sb.Append(Limit(Mask(it.text, secrets, projectRoot))).Append(nl);
            }
            sb.Append(nl);
        }

        static string UnitLabel(FinderUnit unit)
        {
            switch (unit.kind)
            {
                case FinderUnitKind.Play: return "플레이";
                case FinderUnitKind.Upload: return unit.Failed ? "업로드 실패" : "업로드";
                case FinderUnitKind.Test: return unit.Failed ? "Build & Test 실패" : "Build & Test";
                case FinderUnitKind.Compile: return "스크립트 컴파일";
                default: return unit.kind;
            }
        }

        static string Limit(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length && i < MaxLinesPerMessage; i++)
            {
                if (i > 0) sb.Append(Environment.NewLine);
                sb.Append(lines[i]);
            }
            if (lines.Length > MaxLinesPerMessage) sb.Append(Environment.NewLine).Append("(… ").Append(lines.Length - MaxLinesPerMessage).Append("줄 더)");
            string s = sb.ToString();
            return s.Length > MaxCharsPerMessage ? s.Substring(0, MaxCharsPerMessage) + "…" : s;
        }

        /// <summary>Masks names and IDs: project folder → &lt;프로젝트&gt;, user folder → &lt;사용자&gt;, VRChat IDs, e-mails, names.</summary>
        public static string Mask(string text, IList<string> secrets, string projectRoot)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string s = text;
            if (!string.IsNullOrEmpty(projectRoot) && projectRoot.Length > 3)
            {
                string fwd = projectRoot.Replace('\\', '/').TrimEnd('/');
                string back = fwd.Replace('/', '\\');
                s = ReplaceIgnoreCase(s, fwd, "<프로젝트>");
                s = ReplaceIgnoreCase(s, back, "<프로젝트>");
            }
            s = s_WinUser.Replace(s, "$1<사용자>");
            s = s_UnixUser.Replace(s, "$1<사용자>");
            s = s_VrcId.Replace(s, "$1_<ID>");
            s = s_Email.Replace(s, "<이메일>");
            if (secrets != null)
            {
                var names = new List<string>();
                foreach (var name in secrets)
                {
                    string n = name?.Trim();
                    if (!string.IsNullOrEmpty(n) && n.Length >= 3 && !names.Contains(n)) names.Add(n);
                }
                names.Sort((x, y) => y.Length.CompareTo(x.Length));
                foreach (var n in names) s = s.Replace(n, "<이름>");
            }
            return s;
        }

        static string ReplaceIgnoreCase(string s, string find, string with)
        {
            if (string.IsNullOrEmpty(find)) return s;
            var sb = new StringBuilder();
            int pos = 0;
            while (true)
            {
                int k = s.IndexOf(find, pos, StringComparison.OrdinalIgnoreCase);
                if (k < 0) break;
                sb.Append(s, pos, k - pos).Append(with);
                pos = k + find.Length;
            }
            sb.Append(s, pos, s.Length - pos);
            return sb.ToString();
        }
    }
}
