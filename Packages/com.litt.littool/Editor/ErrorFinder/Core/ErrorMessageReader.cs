using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>What the message of an error that is not in the list says by itself.</summary>
    internal sealed class MessageReading
    {
        /// <summary>"InvalidOperationException" when the message is an exception, null otherwise.</summary>
        public string ExceptionType;
        /// <summary>The name in the message's leading [brackets] ("RealSweatSystem" of "[RealSweatSystem: RealSweat] …"), null when none.</summary>
        public string Speaker;
        /// <summary>The part after ':' in the brackets ("RealSweat" of "[RealSweatSystem: RealSweat]"), often the object it is about.</summary>
        public string Subject;
        /// <summary>The message without the exception type and the [brackets]: the lines before the stack trace.</summary>
        public string Body = string.Empty;
        /// <summary>The message came through NDMF ("[NDMF] Error Reported: …"): a plugin reported it, NDMF only shows it.</summary>
        public bool FromNdmf;
        /// <summary>A runtime's standard sentence ("Object reference not set …"): it says nothing about what to do.</summary>
        public bool Generic;
        /// <summary>"ko", "ja", "en" or "other" (the language to translate from).</summary>
        public string Lang = "en";
        /// <summary>Sentences of the message that tell the user what to do ("Run RealSweatSystem Setup again"), at most two.</summary>
        public readonly List<string> Instructions = new List<string>();

        /// <summary>Worth showing on the card: the tool wrote it (not a runtime sentence) and it has some words.</summary>
        public bool Useful => !Generic && Body.Length >= 8;
    }

    /// <summary>
    /// Reads the message of an error the list does not know (the requirements, 2026-10-19: show what the tool itself said;
    /// put its instruction sentences first). Measured before it was written (scratchpad kw/): instruction sentences of the
    /// list's English messages pointed the same way as the list's fix in 41 of 45; sentences that only state a problem
    /// were not turned into guidance because they also match tools' internal sentences ("fakeSlot not found").
    /// No Unity API, so it can be tested outside Unity.
    /// </summary>
    internal static class ErrorMessageReader
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;
        const int MaxBodyLines = 8;
        const int MaxBodyChars = 600;
        const int MaxInstructions = 2;

        /// <summary>The .NET / Unity runtime's own sentences: thrown by the runtime, not written for the user.</summary>
        static readonly Regex s_Generic = new Regex(
            @"Object reference not set to an instance|Index was outside the bounds|Specified cast is not valid|Sequence contains (?:no|more than one)|" +
            @"given key (?:'[^']*' )?was not present|Value cannot be null|Collection was modified|has been destroyed but you are still trying|" +
            @"out of the range of valid values|same key has already been added|Destination array was not long enough|type initializer for|" +
            @"Operation is not valid due to the current state|Exception of type '[^']+' was thrown|Input string was not in a correct format|" +
            @"Arithmetic operation resulted in an overflow|Attempted to divide by zero|Exception has been thrown by the target of an invocation|" +
            @"Index was out of range|Unable to cast object of type|The method or operation is not implemented|Specified method is not supported",
            Opt | RegexOptions.IgnoreCase);

        static readonly Regex s_Stack = new Regex(@"^\s*(?:at\s+)?[A-Za-z_][\w`]*(?:[.+:][\w`<>\[\],]+)+\s*\(", Opt);
        static readonly Regex s_Exception = new Regex(@"^\s*(?:[A-Za-z_]\w*\.)*([A-Za-z_]\w*Exception)\s*:\s*", Opt);
        static readonly Regex s_Bracket = new Regex(@"^\s*\[([^\[\]]{2,60})\]\s*", Opt);
        static readonly Regex s_BracketWord = new Regex(@"^(?:error|warning|info|log|debug|ok|fail(?:ed)?|fatal|assert|exception|오류|경고|エラー|警告|\d[\d:. -]*|MA-\d{4}|NDMF|AAO)$", Opt | RegexOptions.IgnoreCase);
        static readonly Regex s_SentenceEnd = new Regex(@"(?<=[.!?])\s+|(?<=[。！？])\s*", Opt);

        static readonly Regex s_Imperative = new Regex(
            @"^(?:please\b|(?:make sure|ensure|try|consider|run|re-?run|install|reinstall|re-?import|import|update|upgrade|downgrade|remove|delete|" +
            @"add|assign|set|select|check|use|enable|disable|re-?enable|restart|reopen|open|close|exit|save|reload|refresh|move|rename|create|" +
            @"attach|apply|unlock|lock|bake|reset|clear|rebuild|change|fix|place|put|drag|click|press|reduce|increase|decrease|lower|raise|" +
            @"split|specify|configure|log in|sign in|turn on|turn off|wait|switch|replace|uncheck|tick)\b)",
            Opt | RegexOptions.IgnoreCase);
        static readonly Regex s_You = new Regex(@"\byou\s+(?:may\s+)?(?:need|have|must|should)\s+to\b|\byou\s+(?:must|should)\s+\w+", Opt | RegexOptions.IgnoreCase);
        static readonly Regex s_JaInstruction = new Regex(@"してください|して下さい|ください|下さい|しましょう|してから|やり直して|再実行|再インポート|確認して", Opt);
        static readonly Regex s_KoInstruction = new Regex(@"하세요|해 주세요|해주세요|하십시오|확인해|다시 실행|해야 합니다", Opt);
        static readonly Regex s_Condition = new Regex(@"^(?:if|when|unless|while|once)\b", Opt | RegexOptions.IgnoreCase);
        static readonly Regex s_Statement = new Regex(@"^(?!please\b|make sure\b|ensure\b|try\b).*\b(?:cannot|can't|can not)\b", Opt | RegexOptions.IgnoreCase);
        /// <summary>"Check the logs for details": LITTOOL is that log reader already.</summary>
        static readonly Regex s_LogPointer = new Regex(@"^(?:please\s+)?check\s+(?:the\s+)?(?:unity\s+|console\s+)?(?:log|logs|console)\s+for\s+(?:more\s+)?details", Opt | RegexOptions.IgnoreCase);
        /// <summary>Sentences written for the tool's own author: a member call ("PlayerLoopHelper.Initialize"), an async API, "()".</summary>
        static readonly Regex s_Internal = new Regex(@"\b[A-Za-z_]\w*\.[A-Za-z_]\w*\b(?<!\.(?:cs|dll|exe|json|asset|prefab|unity|fbx|png|txt))|\w+Async\b|\(\)|==|!=", Opt);

        /// <summary>Web addresses and "e.g." are not code: "Check status.vrchat.com" stays an instruction.</summary>
        static readonly Regex s_Address = new Regex(@"\b(?:e\.g|i\.e)\.?|https?://\S+|\b[\w-]+(?:\.[\w-]+)*\.(?:com|net|org|io|jp|dev|app|me|gg)\b(?:/\S*)?", Opt | RegexOptions.IgnoreCase);

        static readonly Regex s_RunTogether = new Regex(@"(?<=[a-z][.!?])(?=(?:Please|Try|Make sure|Use|Check|Run|Set|Remove|Click|You)\b)", Opt);

        static readonly Regex s_Hangul = new Regex(@"[가-힣]", Opt);
        static readonly Regex s_Kana = new Regex(@"[ぁ-んァ-ン]", Opt);
        static readonly Regex s_Han = new Regex(@"[一-鿿]", Opt);

        public static MessageReading Read(string text)
        {
            var r = new MessageReading();
            if (string.IsNullOrEmpty(text)) return r;
            string msg = MessagePart(text).Trim();
            if (msg.StartsWith(ErrorAnalysis.NdmfWarningPrefix, StringComparison.Ordinal))
            {
                msg = msg.Substring(ErrorAnalysis.NdmfWarningPrefix.Length).Trim();
                r.FromNdmf = true;
            }
            var exc = s_Exception.Match(msg);
            if (exc.Success)
            {
                r.ExceptionType = exc.Groups[1].Value;
                msg = msg.Substring(exc.Length);
            }
            var bracket = s_Bracket.Match(msg);
            if (bracket.Success)
            {
                string inside = bracket.Groups[1].Value.Trim();
                string name = inside.Split(':')[0].Trim();
                if (name.Length >= 2 && !s_BracketWord.IsMatch(name))
                {
                    r.Speaker = name;
                    int colon = inside.IndexOf(':');
                    if (colon > 0 && colon < inside.Length - 1) r.Subject = inside.Substring(colon + 1).Trim();
                    msg = msg.Substring(bracket.Length);
                }
            }
            r.Body = Limit(msg.Trim());
            r.Generic = r.Body.Length == 0 || s_Generic.IsMatch(r.Body);
            r.Lang = s_Hangul.IsMatch(r.Body) ? "ko" : s_Kana.IsMatch(r.Body) ? "ja" : s_Han.IsMatch(r.Body) ? "other" : "en";
            if (r.Generic) return r;

            foreach (var s in Sentences(r.Body))
            {
                if (r.Instructions.Count >= MaxInstructions) break;
                if (IsInstruction(s, r.Lang)) r.Instructions.Add(s);
            }
            return r;
        }

        /// <summary>Is this sentence the message telling the user what to do?</summary>
        public static bool IsInstruction(string sentence, string lang)
        {
            if (string.IsNullOrWhiteSpace(sentence)) return false;
            string s = sentence.Trim();
            if (lang == "ja") return s_JaInstruction.IsMatch(s);
            if (lang == "ko") return s_KoInstruction.IsMatch(s);
            if (s_Condition.IsMatch(s) || s_LogPointer.IsMatch(s) || s_Internal.IsMatch(s_Address.Replace(s, " "))) return false;
            if (s_You.IsMatch(s)) return true;
            // "Set an FX Float cannot set …": a component name that starts with a verb, not an instruction
            return s_Imperative.IsMatch(s) && !s_Statement.IsMatch(s);
        }

        /// <summary>The message part of a console text: the lines before the stack trace.</summary>
        static string MessagePart(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (s_Stack.IsMatch(lines[i])) break;
                sb.Append('\n').Append(lines[i]);
            }
            return sb.ToString();
        }

        /// <summary>Sentences of the body, one line or one sentence each (a run-together "supported.Please" is split too).</summary>
        public static List<string> Sentences(string body)
        {
            var list = new List<string>();
            foreach (var raw in body.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                // "is not supported.Please change …": only before a word that starts an instruction ("Helper.Initialize" is code)
                line = s_RunTogether.Replace(line, " ");
                foreach (var part in s_SentenceEnd.Split(line))
                {
                    string s = part.Trim().TrimEnd('.', '!', '。', '！');
                    if (s.Length >= 4) list.Add(s);
                }
            }
            return list;
        }

        static string Limit(string body)
        {
            var lines = body.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            int n = 0;
            foreach (var l in lines)
            {
                string t = l.TrimEnd();
                if (t.Length == 0) continue;
                if (n++ >= MaxBodyLines) break;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(t);
            }
            string s = sb.ToString();
            return s.Length > MaxBodyChars ? s.Substring(0, MaxBodyChars - 1) + "…" : s;
        }
    }
}
