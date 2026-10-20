using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// The Korean guidance on the cards (the requirements: guidance in Korean only, the tab's buttons and titles in three
    /// languages). Error-specific sentences come from the list (ErrorCatalog.json); this class only cuts them into steps,
    /// fills the tool names into the rule sentences and adds the few sentences that belong to the tab itself
    /// (how to check again, where the target is, why a card is only an estimate).
    /// </summary>
    internal static class ErrorGuide
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex s_Circled = new Regex(@"[①②③④⑤⑥⑦⑧⑨⑩]", Opt);
        static readonly Regex s_SentenceEnd = new Regex(@"(?<=[.。])\s+", Opt);
        static readonly Regex s_Url = new Regex(@"https?://[^\s<>""']+", Opt);

        // ---------------------------------------------------------------- Korean particles

        /// <summary>Does the word end in a consonant when read in Korean (받침)? Latin names are read the way Korean users say them.</summary>
        public static bool EndsWithConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            string w = word.TrimEnd();
            // "Unity 패키지(com.unity.x)" → the particle follows how the part before the bracket is read
            while (w.EndsWith(")", StringComparison.Ordinal) && w.IndexOf('(') > 0) w = w.Substring(0, w.LastIndexOf('(')).TrimEnd();
            w = w.TrimEnd('.', '·', '/', '\\', ' ', '\'', '’', '"');
            if (w.Length == 0) return false;
            char c = w[w.Length - 1];
            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;
            if (c >= '0' && c <= '9') return "0136781".IndexOf(c) >= 0;  // 영 일 삼 육 칠 팔 (+ 0, 1)
            if (!char.IsLetter(c)) return false;
            // the last word: an acronym is read letter by letter (SDK → 에스디케이), a word as it sounds
            int start = w.Length - 1;
            while (start > 0 && char.IsLetter(w[start - 1])) start--;
            string last = w.Substring(start);
            bool acronym = last.Length <= 5 && last.ToUpperInvariant() == last;
            if (acronym) return "LMNR".IndexOf(char.ToUpperInvariant(c)) >= 0;  // 엘 엠 엔 알
            char lc = char.ToLowerInvariant(c);
            if (last.EndsWith("ng", StringComparison.OrdinalIgnoreCase)) return true;
            return "lnmptkcb".IndexOf(lc) >= 0;  // tool → 툴, toon → 툰, system → 시스템, setup → 셋업, chat → 챗
        }

        /// <summary>을/를.</summary>
        public static string Eul(string word) => word + (EndsWithConsonant(word) ? "을" : "를");

        /// <summary>은/는.</summary>
        public static string Eun(string word) => word + (EndsWithConsonant(word) ? "은" : "는");

        /// <summary>이/가.</summary>
        public static string Ga(string word) => word + (EndsWithConsonant(word) ? "이" : "가");

        /// <summary>과/와.</summary>
        public static string Wa(string word) => word + (EndsWithConsonant(word) ? "과" : "와");

        /// <summary>로/으로 (로 after a vowel or ㄹ: "VRCFury로", "TexTransTool로", "lilToon으로").</summary>
        public static string Ro(string word)
        {
            if (!EndsWithConsonant(word)) return word + "로";
            string w = word.TrimEnd();
            while (w.EndsWith(")", StringComparison.Ordinal) && w.IndexOf('(') > 0) w = w.Substring(0, w.LastIndexOf('(')).TrimEnd();
            char c = w.Length > 0 ? w[w.Length - 1] : ' ';
            bool rieul = (c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 8) || c == '1' || c == '7' || c == '8' ||
                         char.ToLowerInvariant(c) == 'l' || (char.IsUpper(c) && (c == 'L' || c == 'R'));
            return word + (rieul ? "로" : "으로");
        }

        /// <summary>
        /// Fills &lt;툴&gt;, &lt;폴더&gt;, &lt;패키지&gt; in a rule sentence and fixes the particle right after each
        /// (the list writes 을 / 는 after the placeholder).
        /// </summary>
        public static string Fill(string text, string tool, string folder, string package)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = FillOne(text, "<툴>", tool);
            text = FillOne(text, "<폴더>", folder);
            text = FillOne(text, "<패키지>", package);
            return text;
        }

        static string FillOne(string text, string placeholder, string value)
        {
            if (string.IsNullOrEmpty(value) || text.IndexOf(placeholder, StringComparison.Ordinal) < 0) return text;
            var sb = new StringBuilder();
            int pos = 0;
            while (true)
            {
                int k = text.IndexOf(placeholder, pos, StringComparison.Ordinal);
                if (k < 0)
                {
                    sb.Append(text, pos, text.Length - pos);
                    break;
                }
                sb.Append(text, pos, k - pos);
                int after = k + placeholder.Length;
                string next = after < text.Length ? text.Substring(after, 1) : string.Empty;
                // only a particle written right after the placeholder and followed by a space or the end
                bool particleEnd = after + 1 >= text.Length || !IsHangul(text[after + 1]);
                if (particleEnd && (next == "을" || next == "를"))
                {
                    sb.Append(Eul(value));
                    pos = after + 1;
                }
                else if (particleEnd && (next == "은" || next == "는"))
                {
                    sb.Append(Eun(value));
                    pos = after + 1;
                }
                else if (particleEnd && (next == "이" || next == "가"))
                {
                    sb.Append(Ga(value));
                    pos = after + 1;
                }
                else
                {
                    sb.Append(value);
                    pos = after;
                }
            }
            return sb.ToString();
        }

        static bool IsHangul(char c) => c >= 0xAC00 && c <= 0xD7A3;

        // ---------------------------------------------------------------- steps

        /// <summary>
        /// The list's "해결" text as steps: at ①②③ when it has them (text before ① is the first step), otherwise at " → ".
        /// One sentence with alternatives (" / ") stays one step.
        /// </summary>
        public static List<string> SplitFix(string fix)
        {
            var steps = new List<string>();
            if (string.IsNullOrWhiteSpace(fix)) return steps;
            fix = fix.Trim();
            if (s_Circled.IsMatch(fix))
            {
                foreach (var part in s_Circled.Split(fix)) AddStep(steps, part);
                return steps;
            }
            foreach (var part in fix.Split(new[] { " → " }, StringSplitOptions.None)) AddStep(steps, part);
            return steps;
        }

        static void AddStep(List<string> steps, string part)
        {
            string s = part.Trim().TrimEnd(',', '，');
            if (s.Length > 0) steps.Add(s);
        }

        /// <summary>
        /// A rule's guide sentence split into what it says about where the error happened (the leading sentences that end
        /// in 요) and what to do (the rest, cut at " → " and at sentence ends).
        /// </summary>
        public static void SplitRule(string guide, out List<string> explain, out List<string> steps)
        {
            explain = new List<string>();
            steps = new List<string>();
            if (string.IsNullOrWhiteSpace(guide)) return;
            bool leading = true;
            foreach (var raw in s_SentenceEnd.Split(guide.Trim()))
            {
                string sentence = raw.Trim();
                if (sentence.Length == 0) continue;
                string bare = sentence.TrimEnd('.', '。');
                if (leading && bare.EndsWith("요", StringComparison.Ordinal))
                {
                    explain.Add(sentence);
                    continue;
                }
                leading = false;
                foreach (var part in sentence.Split(new[] { " → " }, StringSplitOptions.None)) AddStep(steps, part);
            }
        }

        // ---------------------------------------------------------------- tool lines

        public static bool IsUnityPackage(string tool) => tool != null && tool.StartsWith("Unity 패키지(", StringComparison.Ordinal);

        public static bool IsAssetsFolder(string tool) => tool != null && tool.StartsWith("Assets/", StringComparison.Ordinal);

        /// <summary>Package id inside "Unity 패키지(com.unity.x)" ("" when it is not one).</summary>
        public static string UnityPackageId(string tool)
        {
            if (!IsUnityPackage(tool)) return string.Empty;
            int a = tool.IndexOf('(');
            int b = tool.LastIndexOf(')');
            return b > a ? tool.Substring(a + 1, b - a - 1) : string.Empty;
        }

        /// <summary>Tools that run inside NDMF (their errors also show when Play Mode starts, NDMF Apply on Play).</summary>
        public static bool IsNdmfBased(string tool)
        {
            if (string.IsNullOrEmpty(tool)) return false;
            return tool.StartsWith("Modular Avatar", StringComparison.Ordinal) || tool.StartsWith("NDMF", StringComparison.Ordinal) ||
                   tool == "Avatar Optimizer" || tool == "VRCQuestTools" || tool == "TexTransTool" || tool == "lilycalInventory" ||
                   tool == "VRCFury";
        }

        /// <summary>"안 될 때": update the related tool (the requirements: one line, no contact addresses).</summary>
        public static string IfNot(string tool)
        {
            if (string.IsNullOrEmpty(tool)) return "관련 툴을 VCC에서 최신으로 업데이트한 뒤 다시 해 보세요.";
            if (IsUnityPackage(tool))
                return "Window › Package Manager에서 " + UnityPackageId(tool) + " 패키지의 버전을 확인하고, VRChat SDK를 VCC에서 최신으로 업데이트한 뒤 다시 해 보세요.";
            if (IsAssetsFolder(tool))
                return tool + " 폴더의 툴을 받은 곳(BOOTH 등)에서 최신 버전으로 다시 받아 넣은 뒤 다시 해 보세요.";
            if (tool == "Modular Avatar" || tool == "Avatar Optimizer")
                return Wa(tool) + " NDMF를 VCC에서 최신으로 업데이트한 뒤 다시 해 보세요.";
            return "관련 툴(" + tool + ")을 VCC에서 최신으로 업데이트한 뒤 다시 해 보세요.";
        }

        // ---------------------------------------------------------------- fixed sentences of the tab

        public const string TargetStep = "문제가 된 곳을 확인해요.";
        public const string TargetObjectsStep = "문제가 된 오브젝트를 선택해서 확인해요.";
        public const string TargetFoldersStep = "문제가 된 파일이 있는 폴더를 확인해요.";
        /// <summary>Components found from the stack trace's types: the tool's components, not necessarily the broken one.</summary>
        public const string ComponentTargetStep = "오류가 난 툴의 컴포넌트예요. 선택해서 빈 칸(None)이나 설정을 확인해요.";
        public const string SdkAutoFixStep = "VRChat SDK 창의 Validations에서 이 항목 옆의 Auto Fix 버튼을 눌러요.";
        public const string NdmfConsoleStep = "NDMF Console에서 이 항목을 눌러 대상 오브젝트와 안내(Hint)를 확인해요.";
        public const string ValidationExplain = "VRChat SDK 창의 검사 목록(Validations)에 있는 빨간 항목이에요.";
        public const string ValidationLookStep ="VRChat SDK 창의 Validations에서 이 항목의 설명과 옆의 버튼(Auto Fix·Select 등)을 확인해요.";

        // ---- the tool's own message (cards not in the list)

        /// <summary>A sentence of the message that tells what to do, as the first step (kept in the message's language).</summary>
        public static string MessageStep(string sentence) => "메시지에 적힌 방법대로 먼저 해 보세요: “" + sentence + "”";

        public const string MessageCheckStep = "위 ‘메시지 내용’에 무엇이 없거나 맞지 않다고 적혀 있으면 그것부터 확인해요.";

        /// <summary>The line above the message: who wrote it.</summary>
        public static string MessageCaption(string tool, bool fromNdmf)
        {
            if (fromNdmf && (string.IsNullOrEmpty(tool) || tool == "NDMF")) return "NDMF Console에 보고된 메시지예요.";
            return string.IsNullOrEmpty(tool) ? "오류를 낸 쪽이 직접 남긴 메시지예요." : Ga(tool) + " 직접 남긴 메시지예요.";
        }

        public const string CheckCompile ="Unity가 다시 컴파일하면 이 오류가 사라졌는지 확인해요.";
        public const string CheckValidation = "SDK 창의 Validations에서 이 항목이 사라졌는지 확인해요.";
        public const string CheckPlay = "다시 플레이해서 이 오류가 사라졌는지 확인해요.";
        public const string CheckUpload = "다시 업로드해서 이 오류가 사라졌는지 확인해요.";
        public const string CheckTest = "다시 Build & Test를 눌러서 이 오류가 사라졌는지 확인해요.";
        public const string CheckPlayOrUpload = "다시 플레이하거나 업로드해서 이 오류가 사라졌는지 확인해요.";
        public const string CheckPlayFaster = " 보통 플레이로 확인하는 게 빨라요.";
        public const string CheckRestart = "Unity를 다시 열어서 이 오류가 사라졌는지 확인해요.";
        public const string CheckAgain = "같은 작업을 다시 해서 이 오류가 사라졌는지 확인해요.";

        public const string SafeModeCaution =
            "이 상태로 Unity를 껐다 켜면 Safe Mode로 열릴 수 있어요. Safe Mode에서는 LITTOOL도 열리지 않으니, 지금 고치는 게 편해요.";
        public const string PlayModeCaution = "플레이 중에 고친 내용은 플레이를 멈추면 사라져요. 플레이를 멈춘 뒤 고치세요.";
        public const string CommunityNote = "이 해결법은 커뮤니티 답변에서 나온 것이라, 상황에 따라 맞지 않을 수 있어요.";

        public static string SimilarNote(string tool)
        {
            return string.IsNullOrEmpty(tool) || tool.StartsWith("Unity", StringComparison.Ordinal)
                ? "목록에 똑같은 문장은 없어서, 가장 비슷한 오류로 안내해요. 추정이라 맞지 않을 수 있어요."
                : "목록에 똑같은 문장은 없어서, 같은 툴(" + tool + ")에서 가장 비슷한 오류로 안내해요. 추정이라 맞지 않을 수 있어요.";
        }

        public static string NearNote(int percent) => "목록에서 가장 가까운 오류도 " + percent + "%라서 따로 안내하지 않았어요.";

        public static string ShortNote(int percent) =>
            "메시지 첫 줄이 짧아서 비슷한 오류로 추정하지 않았어요(목록에서 가장 가까운 오류 " + percent + "%).";

        public static string HintNote(string what, int percent) =>
            "목록의 ‘" + what + "’" + (EndsWithConsonant(what.TrimEnd('’')) ? "과" : "와") + " " + percent +
            "% 비슷하지만, 오류가 난 위치가 분명해서 위치로 안내해요.";

        public static string NoToolExplain => "메시지에서 오류가 난 툴을 찾지 못했어요.";

        public static string ToolExplain(string tool) => tool + " 쪽 코드에서 난 오류로 보여요.";

        public static string RuleExplainFallback(string ruleId)
        {
            switch (ruleId)
            {
                case "P4": return "컴파일 오류인데 오류 줄에 파일 경로가 없어서, 어느 툴에서 났는지 정할 수 없어요.";
                case "P11": return "예외인데 어디서 났는지 알려 주는 줄(스택트레이스)이 없어요.";
                default: return string.Empty;
            }
        }

        // ---------------------------------------------------------------- sources

        /// <summary>The list's "출처": URLs become links, other lines (e.g. "SDK 3.10.5 AvatarBuilder.cs L790") stay text.</summary>
        public static List<KeyValuePair<string, string>> Sources(string source)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(source)) return list;
            foreach (var raw in source.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var m = s_Url.Match(line);
                if (m.Success)
                {
                    string url = m.Value.TrimEnd('.', ',', ')');
                    list.Add(new KeyValuePair<string, string>(ShortUrl(url), url));
                }
                else
                {
                    list.Add(new KeyValuePair<string, string>(line, null));
                }
            }
            return list;
        }

        static string ShortUrl(string url)
        {
            string s = Regex.Replace(url, @"^https?://(www\.)?", string.Empty, Opt);
            if (s.Length <= 64) return s;
            int slash = s.IndexOf('/');
            string host = slash > 0 ? s.Substring(0, slash) : s;
            string tail = s.Substring(s.Length - 28);
            return host + "/…" + tail;
        }
    }
}
