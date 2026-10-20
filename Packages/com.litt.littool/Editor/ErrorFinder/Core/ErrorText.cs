using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// Text helpers of the matcher. Each pattern is the C# form of the one in the Python simulator (sim/pipe3.py,
    /// sim/sim_test.py) that the error list was measured with, so both give the same results.
    /// </summary>
    internal static class ErrorText
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;

        // ---- normalisation (sim_test.normalize + the v7.1 compiler-code guard)
        static readonly Regex s_CsCode = new Regex(@"\b[Cc][Ss](\d{4})\b", Opt);
        static readonly Regex s_Placeholder = new Regex(@"\{[^}]*\}", Opt);
        static readonly Regex s_Url = new Regex(@"https?://\S+", Opt);
        static readonly Regex s_DrivePath = new Regex(@"[A-Za-z]:[\\/][^\s'""]+", Opt);
        static readonly Regex s_UnityPath = new Regex(@"(?:Assets|Packages|Library|Temp)/[^\s'""]+", Opt);
        static readonly Regex s_Guid = new Regex(@"\b[0-9a-f]{32}\b", Opt);
        static readonly Regex s_AvatarId = new Regex(@"avtr_[0-9a-f-]+", Opt);
        static readonly Regex s_SingleQuoted = new Regex(@"'[^']{1,80}'", Opt);
        static readonly Regex s_DoubleQuoted = new Regex(@"""[^""]{1,80}""", Opt);
        static readonly Regex s_Number = new Regex(@"(?<![A-Za-z])\d+(?:\.\d+)*(?:\s?(?:MB|KB|bits|px))?", Opt);
        static readonly Regex s_Spaces = new Regex(@"\s+", Opt);

        static readonly Regex s_Cjk = new Regex(@"[぀-ヿ㐀-鿿가-힯ｦ-ﾟ]", Opt);
        static readonly Regex s_PlaceholderTag = new Regex(@"<[a-z]+>", Opt);
        static readonly Regex s_PackageCache = new Regex(@"Library[\\/]PackageCache[\\/]([^\\/@\s]+)@[^\\/\s]+", Opt);

        /// <summary>
        /// Placeholders for the parts that change from one error to the next (quoted names, paths, numbers, GUIDs,
        /// avatar IDs, URLs), lower case. A compiler code such as CS0246 keeps its identity (its digits become letters).
        /// </summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s_CsCode.Replace(s, m => "cs" + DigitsToLetters(m.Groups[1].Value));
            s = s.Replace('’', '\'');
            s = s_Placeholder.Replace(s, " <V> ");
            s = s_Url.Replace(s, " <URL> ");
            s = s_DrivePath.Replace(s, " <PATH> ");
            s = s_UnityPath.Replace(s, " <PATH> ");
            s = s_Guid.Replace(s, " <GUID> ");
            s = s_AvatarId.Replace(s, " <ID> ");
            s = s_SingleQuoted.Replace(s, " <V> ");
            s = s_DoubleQuoted.Replace(s, " <V> ");
            s = s_Number.Replace(s, " <N> ");
            s = s_Spaces.Replace(s, " ").Trim();
            return s.ToLowerInvariant();
        }

        static string DigitsToLetters(string digits)
        {
            var sb = new StringBuilder(digits.Length);
            foreach (char c in digits) sb.Append(c >= '0' && c <= '9' ? (char)('a' + (c - '0')) : c);
            return sb.ToString();
        }

        /// <summary>Length for "is this long enough to compare": a Korean / Japanese / Chinese character counts as <paramref name="cjkWeight"/>.</summary>
        public static int WeightedLength(string s, int cjkWeight)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Length + (cjkWeight - 1) * s_Cjk.Matches(s).Count;
        }

        /// <summary>Weighted length of the first non-empty line once normalised, placeholders left out.</summary>
        public static int FirstLineLength(string text, int cjkWeight)
        {
            string first = string.Empty;
            foreach (var line in text.Split('\n'))
            {
                if (line.Trim().Length > 0)
                {
                    first = line;
                    break;
                }
            }
            string lit = s_PlaceholderTag.Replace(Normalize(first), " ");
            return WeightedLength(s_Spaces.Replace(lit, " ").Trim(), cjkWeight);
        }

        /// <summary>Library/PackageCache/&lt;id&gt;@&lt;version&gt;/… is read like Packages/&lt;id&gt;/… (where the error happened).</summary>
        public static string PackageCacheAsPackages(string text)
        {
            if (text == null || text.IndexOf("PackageCache", StringComparison.Ordinal) < 0) return text;
            return s_PackageCache.Replace(text, m => "Packages/" + m.Groups[1].Value);
        }

        /// <summary>Strips the characters in <paramref name="chars"/> from the start (Python str.lstrip(chars)).</summary>
        public static string TrimStartChars(string s, string chars)
        {
            int i = 0;
            while (i < s.Length && chars.IndexOf(s[i]) >= 0) i++;
            return s.Substring(i);
        }

        // ---- the keyword pattern: '*' = any text (across lines), everything else literal, case-insensitive
        public static Regex KeywordPattern(string alt)
        {
            var parts = alt.Split('*');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(".*");
                sb.Append(Regex.Escape(parts[i]));
            }
            return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>Keyword alternatives of a list cell: " | " separated, an alternative in brackets is a note, not a keyword.</summary>
        public static List<string> Alternatives(string kw)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(kw)) return list;
            foreach (var raw in kw.Split(new[] { " | " }, StringSplitOptions.None))
            {
                var alt = raw.Trim();
                if (alt.Length > 0 && !alt.StartsWith("(", StringComparison.Ordinal)) list.Add(alt);
            }
            return list;
        }
    }
}
