using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>How sure a card is (also its order: confirmed → similar → rule (tool found) → none).</summary>
    internal enum CardMatch
    {
        Confirmed = 0,
        Similar = 1,
        Rule = 2,
        None = 3,
    }

    internal enum StepAction
    {
        None,
        /// <summary>The [선택] / [폴더 보기] rows of the card.</summary>
        Targets,
        OpenSdk,
        OpenNdmf,
        /// <summary>[다시 검사].</summary>
        Recheck,
        /// <summary>[선택] on the Pipeline Manager of the avatar chosen in the SDK window (blueprint ID choices).</summary>
        SelectPipeline,
    }

    /// <summary>The severity filter chips. '플레이·업로드 차단' counts as both upload and play.</summary>
    [Flags]
    internal enum SeverityBucket
    {
        None = 0,
        Upload = 1,
        Play = 2,
        Broken = 4,
        Unknown = 8,
        Project = 16,
    }

    internal sealed class CardStep
    {
        public readonly string Text;
        public readonly StepAction Action;

        public CardStep(string text, StepAction action = StepAction.None)
        {
            Text = text;
            Action = action;
        }
    }

    /// <summary>A result message folded under the causes ("결과 메시지 N개 · 위 원인을 고치면 같이 사라져요").</summary>
    internal sealed class ResultLine
    {
        public string Text;
        public string Explain;
        public int Count;
        public int No;
        public int Order;
    }

    /// <summary>"왜 비슷한가요": the message, the list's sentence and the words they share.</summary>
    internal sealed class SimilarView
    {
        public string Received;
        public string Listed;
        public readonly List<string> Shared = new List<string>();
    }

    /// <summary>One card: one cause (all the messages it explains, with their count and targets).</summary>
    internal sealed class FinderCard
    {
        public string Key;
        public CardMatch Match;
        public CatalogRow Row;
        public string RuleId;
        public MatchResult First;
        public string Tool;
        /// <summary>The tool was found from where the error happened (a dashed "…로 추정" chip).</summary>
        public bool ToolGuessed;
        /// <summary>The list's sentence (Korean); null for cards that are not in the list (the tab shows its own title).</summary>
        public string Title;
        /// <summary>The list's severity (Korean value), "" = not known.</summary>
        public string Severity = string.Empty;
        public int SeverityRank;
        public SeverityBucket Buckets;
        public string MaCode;
        /// <summary>
        /// A better name for a tool the list does not know (the message's [name], the NDMF plugin, the folder under Assets:
        /// "RealSweatSystem (CookiTools)"); null = show <see cref="Tool"/>. <see cref="Tool"/> stays the matcher's value.
        /// </summary>
        public string ToolLabel;
        /// <summary>Cards not in the list: what the tool's own message says (null when it is a runtime sentence or a compile error).</summary>
        public MessageReading Reading;
        public int Percent;
        public int NearestNo;
        public int NearestPercent = -1;
        public int Count;
        public int FirstOrder = int.MaxValue;
        public bool IsResultCard;
        public bool IsCompile;

        public readonly List<FinderItem> Items = new List<FinderItem>();
        public readonly List<string> Channels = new List<string>();
        public readonly List<int> ObjectIds = new List<int>();
        /// <summary>Folders of the asset paths named in the messages (Assets/… or Packages/…), checked by the tab.</summary>
        public readonly List<string> Folders = new List<string>();
        /// <summary>Blueprint ID rows: the target is the Pipeline Manager of the avatar chosen in the SDK window.</summary>
        public bool PipelineTarget;
        /// <summary>
        /// Tools the list does not know: type names of the tool's code in the stack trace ("CookiTools.RealSweatTuning"),
        /// for the tab to look for those components in the scene. Most are not components; the tab keeps the ones that are.
        /// </summary>
        public readonly List<string> TypeNames = new List<string>();
        /// <summary>The tool's namespaces in the stack trace ("CookiTools"): the tab looks for its components when no named type is one.</summary>
        public readonly List<string> TypeRoots = new List<string>();

        public string Cause;
        public readonly List<string> Location = new List<string>();
        public readonly List<string> LocationLines = new List<string>();
        public string LocationMark;
        public SimilarView Similar;
        public readonly List<CardStep> Steps = new List<CardStep>();
        /// <summary>"먼저 해 볼 것" (not in the list) instead of "해결 순서".</summary>
        public bool FirstSteps;
        public readonly List<string> Cautions = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public string IfNot;
        public string NearNote;
        public CatalogChoice Choice;
        public List<KeyValuePair<string, string>> Sources = new List<KeyValuePair<string, string>>();

        public bool HasTargets => ObjectIds.Count > 0 || Folders.Count > 0 || PipelineTarget;
    }

    internal sealed class FinderAnalysis
    {
        public FinderUnit Unit;
        public readonly List<FinderCard> Cards = new List<FinderCard>();
        public readonly List<ResultLine> Results = new List<ResultLine>();
        /// <summary>Messages skipped because they are on the ignore list (harmless).</summary>
        public int Ignored;
        /// <summary>Warnings left out because they are not in the list.</summary>
        public int Warnings;

        public int CountOf(SeverityBucket bucket)
        {
            int n = 0;
            foreach (var c in Cards)
            {
                if ((c.Buckets & bucket) != 0) n++;
            }
            return n;
        }

        public bool Empty => Cards.Count == 0;
    }

    /// <summary>
    /// Turns the messages of a unit into cards (the requirements, 원인과 결과 나누기 · 카드): one card per cause, result
    /// messages folded under the causes when there is a cause, a wrapping sentence folded when the sentence inside it
    /// made the card, ordered by severity → how sure → first seen. No Unity API, so it can be tested outside Unity.
    /// </summary>
    internal static class ErrorAnalysis
    {
        public const string NdmfWarningPrefix = "[NDMF] Error Reported: ";

        const RegexOptions Opt = RegexOptions.CultureInvariant;
        static readonly Regex s_MaCode = new Regex(@"\[(MA-\d{4})\]", Opt);
        static readonly Regex s_LeadFile = new Regex(@"^\s*(?:\.[\\/])?((?:Assets|Packages)[\\/].+?\.\w+)\(\d+,\d+\)", Opt);
        static readonly Regex s_AnyPath = new Regex(@"(?:^|[\s(\[""'‘,])((?:Assets|Packages)[\\/][^\s:()\[\]""'’,]+)", Opt);
        static readonly Regex s_StackLine = new Regex(@"^\s*(?:at\s+[\w.`<>\[\],+]+|[A-Za-z_][\w.`<>\[\],+]*[:.][\w.`<>\[\]|]+\s*\(.*\)(?:\s*\(at\s.*\))?\s*$)", Opt);
        static readonly Regex s_Words = new Regex(@"[\p{L}\p{N}_]+", Opt);
        static readonly Regex s_VrcType = new Regex(@"'VRC\w+'", Opt);
        static readonly HashSet<string> s_Stop = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "not", "with", "this", "that", "are", "you", "your", "has", "have", "from", "was", "can",
            "cannot", "could", "will", "but", "its", "into", "any", "all", "one", "please", "error", "errors",
        };

        // ---------------------------------------------------------------- severity

        public static int SeverityRank(string sev)
        {
            switch (sev)
            {
                case "프로젝트 차단": return 0;
                case "플레이·업로드 차단": return 1;
                case "업로드 차단":
                case "플레이 차단": return 2;
                case "기능 깨짐": return 3;
                default: return 4;
            }
        }

        public static SeverityBucket BucketsOf(string sev)
        {
            switch (sev)
            {
                case "업로드 차단": return SeverityBucket.Upload;
                case "플레이 차단": return SeverityBucket.Play;
                case "플레이·업로드 차단": return SeverityBucket.Upload | SeverityBucket.Play;
                case "기능 깨짐": return SeverityBucket.Broken;
                case "프로젝트 차단": return SeverityBucket.Project;
                default: return SeverityBucket.Unknown;
            }
        }

        // ---------------------------------------------------------------- main

        public static FinderAnalysis Run(FinderUnit unit, ErrorMatcher matcher)
        {
            var a = new FinderAnalysis { Unit = unit };
            if (unit == null || matcher == null || unit.items == null) return a;

            var items = new List<FinderItem>(unit.items);
            items.Sort((x, y) => x.order.CompareTo(y.order));

            var cards = new Dictionary<string, FinderCard>(StringComparer.Ordinal);
            var all = new List<FinderCard>();
            var wrapped = new List<ResultLine>();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.text)) continue;
                var res = matcher.Analyze(item.text);
                if (res.Kind == MatchKind.Ignore)
                {
                    a.Ignored += item.count;
                    continue;
                }
                bool ndmf = item.text.StartsWith(NdmfWarningPrefix, StringComparison.Ordinal);
                if (item.level > 0 && !ndmf && !res.IsConfirmed)
                {
                    a.Warnings += item.count;  // warnings are shown only when the list knows them
                    continue;
                }
                string key = KeyOf(res, item);
                if (!cards.TryGetValue(key, out var card))
                {
                    card = NewCard(key, res, matcher);
                    cards[key] = card;
                    all.Add(card);
                }
                AddItem(card, item);
                if (res.Wrapped > 0)
                {
                    var w = matcher.Row(res.Wrapped);
                    if (w != null) wrapped.Add(new ResultLine { Text = Shorten(item.FirstLine, 200), Explain = w.what, Count = item.count, No = w.no, Order = item.order });
                }
            }

            bool hasCause = false;
            foreach (var c in all)
            {
                if (!c.IsResultCard) hasCause = true;
            }
            foreach (var c in all)
            {
                if (c.IsResultCard && hasCause)
                {
                    foreach (var it in c.Items)
                        a.Results.Add(new ResultLine { Text = Shorten(it.FirstLine, 200), Explain = c.Row.what, Count = it.count, No = c.Row.no, Order = it.order });
                    continue;
                }
                a.Cards.Add(c);
            }
            a.Results.AddRange(wrapped);
            a.Results.Sort((x, y) => x.Order.CompareTo(y.Order));

            foreach (var c in a.Cards) Build(c, unit, matcher);
            a.Cards.Sort((x, y) =>
            {
                int k = x.SeverityRank.CompareTo(y.SeverityRank);
                if (k != 0) return k;
                k = ((int)x.Match).CompareTo((int)y.Match);
                return k != 0 ? k : x.FirstOrder.CompareTo(y.FirstOrder);
            });
            return a;
        }

        static string KeyOf(MatchResult res, FinderItem item)
        {
            switch (res.Kind)
            {
                case MatchKind.Entry: return "row:" + res.No;
                case MatchKind.Rule: return "rule:" + res.Rule + "|" + (res.Tool ?? string.Empty);
                default: return "none:" + ErrorText.Normalize(item.FirstLine);
            }
        }

        static FinderCard NewCard(string key, MatchResult res, ErrorMatcher m)
        {
            var c = new FinderCard { Key = key, First = res, RuleId = res.Rule, Tool = res.Tool };
            if (res.Kind == MatchKind.Entry)
            {
                c.Row = m.Row(res.No);
                c.Match = res.IsSimilar ? CardMatch.Similar : CardMatch.Confirmed;
                if (res.IsSimilar) c.Percent = PercentOf(res.How);
                c.Severity = c.Row.sev ?? string.Empty;
                c.IsResultCard = c.Row.result == "예";
                c.Title = c.Row.what;
                // NDMF only reports the error for the tool the row is about (MA, AAO …): name that tool
                string rowTool = c.Row.maCodes != null && c.Row.maCodes.Length > 0 ? "Modular Avatar" : ToolOfRow(c.Row, m.Data);
                if (string.IsNullOrEmpty(c.Tool) || (c.Tool == "NDMF" && !string.IsNullOrEmpty(rowTool))) c.Tool = rowTool ?? c.Tool;
            }
            else
            {
                c.Match = res.Kind == MatchKind.Rule ? CardMatch.Rule : CardMatch.None;
                c.ToolGuessed = !string.IsNullOrEmpty(c.Tool) && res.Rule != "P14";
                c.Severity = res.Rule == "P2" || res.Rule == "P3" || res.Rule == "P4" ? "플레이·업로드 차단" : string.Empty;
            }
            c.SeverityRank = SeverityRank(c.Severity);
            c.Buckets = BucketsOf(c.Severity);
            return c;
        }

        static int PercentOf(string how)
        {
            if (string.IsNullOrEmpty(how)) return 0;
            var m = Regex.Match(how, @"(\d+)%", Opt);
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        static void AddItem(FinderCard c, FinderItem item)
        {
            c.Items.Add(item);
            c.Count += item.count;
            if (item.order < c.FirstOrder) c.FirstOrder = item.order;
            if (!string.IsNullOrEmpty(item.channel) && !c.Channels.Contains(item.channel)) c.Channels.Add(item.channel);
            if (item.text.StartsWith(NdmfWarningPrefix, StringComparison.Ordinal) && !c.Channels.Contains(FinderChannel.Ndmf))
                c.Channels.Add(FinderChannel.Ndmf);
            foreach (int id in item.objectIds ?? new List<int>())
            {
                if (id != 0 && !c.ObjectIds.Contains(id)) c.ObjectIds.Add(id);
            }
            foreach (var folder in FoldersIn(item.text))
            {
                if (c.Folders.Count < 4 && !c.Folders.Contains(folder)) c.Folders.Add(folder);
            }
        }

        /// <summary>The tool of a row from the tool → group table, when the group belongs to one tool (NDMF aside).</summary>
        public static string ToolOfRow(CatalogRow row, ErrorCatalogData data)
        {
            if (row == null || data?.toolGroups == null) return null;
            var tools = new List<string>();
            foreach (var g in data.toolGroups)
            {
                if (g.groups != null && Array.IndexOf(g.groups, row.group) >= 0) tools.Add(g.tool);
            }
            if (tools.Count > 1) tools.Remove("NDMF");
            return tools.Count == 1 ? tools[0] : null;
        }

        // ---------------------------------------------------------------- text of a card

        static void Build(FinderCard c, FinderUnit unit, ErrorMatcher m)
        {
            var data = m.Data;
            var first = c.Items[0];
            c.IsCompile = IsCompile(c, unit);

            if (c.Match == CardMatch.Confirmed || c.Match == CardMatch.Similar)
            {
                var row = c.Row;
                c.Cause = row.cause;
                if (c.Match == CardMatch.Similar)
                {
                    c.Notes.Add(ErrorGuide.SimilarNote(c.Tool));
                    c.Similar = SimilarOf(first.FirstLine, row.no, data);
                }
                if (row.maCodes != null && row.maCodes.Length > 0)
                {
                    foreach (Match mc in s_MaCode.Matches(first.text))
                    {
                        if (Array.IndexOf(row.maCodes, mc.Groups[1].Value) >= 0)
                        {
                            c.MaCode = mc.Groups[1].Value;
                            break;
                        }
                    }
                    if (c.MaCode == null) c.MaCode = row.maCodes[0];
                }
                c.PipelineTarget = row.group == 1;
                if (data.choices != null)
                {
                    foreach (var ch in data.choices)
                    {
                        if (ch.row == row.no) c.Choice = ch;
                    }
                }
                if (row.auto == "SDK Auto Fix") c.Steps.Add(new CardStep(ErrorGuide.SdkAutoFixStep, StepAction.OpenSdk));
                foreach (var s in ErrorGuide.SplitFix(row.fix)) c.Steps.Add(new CardStep(s, ActionOf(s)));
                if (row.basis == "커뮤니티") c.Notes.Add(ErrorGuide.CommunityNote);
                c.Sources = ErrorGuide.Sources(row.source);
            }
            else
            {
                c.FirstSteps = true;
                var lines = LinesOf(first.text);
                c.LocationLines.Add(Shorten(lines.Count > 0 ? lines[0] : first.FirstLine, 300));
                CatalogRule rule = null;
                if (c.Match == CardMatch.Rule) rule = RuleOf(data, c.RuleId);
                if (rule != null)
                {
                    string folder = null, package = null;
                    // P4 writes <폴더>/<패키지> to describe the path format: only the rules with a real path fill them
                    if (c.RuleId == "P2" || c.RuleId == "P3" || c.RuleId == "P8") FolderAndPackage(first.text, c.Tool, out folder, out package);
                    ErrorGuide.SplitRule(ErrorGuide.Fill(rule.guide, c.Tool, folder, package), out var explain, out var steps);
                    c.Location.AddRange(explain);
                    // the rule sentence has one step for tools from VCC and one for Unity packages: keep the one that fits,
                    // the Unity package step first for a Unity package
                    bool unity = ErrorGuide.IsUnityPackage(c.Tool);
                    var kept = new List<string>();
                    foreach (var s in steps)
                    {
                        bool forUnityPackage = s.StartsWith("com.unity.", StringComparison.Ordinal);
                        if (forUnityPackage && !unity) continue;
                        if (unity && !forUnityPackage && s.Contains("VCC에서 " + c.Tool)) continue;
                        if (unity && forUnityPackage) kept.Insert(0, s);
                        else kept.Add(s);
                    }
                    foreach (var s in kept) c.Steps.Add(new CardStep(s, ActionOf(s)));
                }
                else if (c.Channels.Count == 1 && c.Channels[0] == FinderChannel.Validation)
                {
                    // read from the SDK window itself: the tool is known
                    c.Tool = "VRChat SDK";
                    c.ToolGuessed = false;
                    c.Location.Add(ErrorGuide.ValidationExplain);
                    c.Steps.Add(new CardStep(ErrorGuide.ValidationLookStep, StepAction.OpenSdk));
                }
                else
                {
                    var p11 = RuleOf(data, "P11");
                    if (p11 != null)
                    {
                        ErrorGuide.SplitRule(p11.guide, out _, out var steps);
                        foreach (var s in steps) c.Steps.Add(new CardStep(s, ActionOf(s)));
                    }
                }

                bool validationOnly = c.Channels.Count == 1 && c.Channels[0] == FinderChannel.Validation;
                if (!validationOnly)
                {
                    c.ToolLabel = ToolLabelOf(c, first, data);
                    bool compile = c.RuleId == "P2" || c.RuleId == "P3" || c.RuleId == "P4" || c.Channels.Contains(FinderChannel.Compile);
                    if (!compile && !IsKnownTool(c.Tool, data)) ComponentCandidates(first.text, c.TypeNames, c.TypeRoots);
                    else if (c.Tool == "NDMF")
                    {
                        // a plugin's report: the stack is NDMF's own, the plugin's class name gives the tool's namespace
                        string root = PluginRoot(c);
                        if (root != null) c.TypeRoots.Add(root);
                    }
                    var reading = compile ? null : ErrorMessageReader.Read(first.text);
                    if (reading != null && reading.Useful)
                    {
                        // what the tool itself says comes first: its instructions replace the rule's general steps
                        // (those stay in "안 될 때"); without one, the user is pointed at the message before the rule
                        c.Reading = reading;
                        if (reading.Instructions.Count > 0)
                        {
                            c.Steps.Clear();
                            foreach (var s in reading.Instructions) c.Steps.Add(new CardStep(ErrorGuide.MessageStep(s)));
                        }
                        else
                        {
                            c.Steps.Insert(0, new CardStep(ErrorGuide.MessageCheckStep));
                        }
                    }
                }

                if (c.Location.Count == 0)
                {
                    string fallback = ErrorGuide.RuleExplainFallback(c.RuleId ?? string.Empty);
                    string tool = c.ToolLabel ?? c.Tool;
                    if (fallback.Length > 0) c.Location.Add(fallback);
                    else c.Location.Add(string.IsNullOrEmpty(tool) ? ErrorGuide.NoToolExplain : ErrorGuide.ToolExplain(tool));
                }
                DecidingLine(c, first.text, lines, data);
                // the message has its own section: "어디서 났나요" keeps only the line that told where
                if (c.Reading != null && c.LocationLines.Count > 0) c.LocationLines.RemoveAt(0);

                if (c.First.HintNo > 0)
                {
                    var hint = m.Row(c.First.HintNo);
                    if (hint != null) c.NearNote = ErrorGuide.HintNote(hint.what, c.First.HintPercent);
                    c.NearestNo = c.First.HintNo;
                    c.NearestPercent = c.First.HintPercent;
                }
                else
                {
                    m.Nearest(first.text, out int no, out double score);
                    if (no > 0 && score > 0)
                    {
                        c.NearestNo = no;
                        c.NearestPercent = ErrorMatcher.ToPercent(score);
                        c.NearNote = score >= m.Threshold && !m.LongEnough(first.text)
                            ? ErrorGuide.ShortNote(c.NearestPercent)
                            : ErrorGuide.NearNote(c.NearestPercent);
                    }
                }
            }

            if (c.Choice == null) c.Steps.Add(FinalStep(c, unit));
            if (c.IsCompile) c.Cautions.Add(ErrorGuide.SafeModeCaution);
            bool updateInSteps = false;
            foreach (var s in c.Steps)
            {
                if (s.Text.Contains("최신")) updateInSteps = true;
            }
            if (!updateInSteps) c.IfNot = ErrorGuide.IfNot(c.Tool);
        }

        static readonly Regex s_UsesSdkWindow = new Regex(@"SDK (?:창|패널)(?:에서|의|을|를| Validations)|Validations(?:의|에서)", Opt);
        static readonly Regex s_UsesNdmfConsole = new Regex(@"NDMF Console(?:에서|을|를|의)", Opt);

        /// <summary>A step that tells the user to look in the SDK window or the NDMF Console gets the button that opens it.</summary>
        static StepAction ActionOf(string step)
        {
            if (s_UsesNdmfConsole.IsMatch(step)) return StepAction.OpenNdmf;
            if (s_UsesSdkWindow.IsMatch(step)) return StepAction.OpenSdk;
            return StepAction.None;
        }

        static bool IsCompile(FinderCard c, FinderUnit unit)
        {
            if (c.Channels.Contains(FinderChannel.Compile)) return true;
            bool compileUnit = unit != null && (unit.kind == FinderUnitKind.Compile || unit.kind == FinderUnitKind.Rescan);
            if (!compileUnit) return false;
            if (c.RuleId == "P2" || c.RuleId == "P3" || c.RuleId == "P4") return true;
            return c.Row != null && c.Row.when != null && c.Row.when.StartsWith("컴파일", StringComparison.Ordinal);
        }

        static CardStep FinalStep(FinderCard c, FinderUnit unit)
        {
            if (c.IsCompile) return new CardStep(ErrorGuide.CheckCompile, StepAction.Recheck);
            bool validationOnly = c.Channels.Count == 1 && c.Channels[0] == FinderChannel.Validation;
            if (validationOnly) return new CardStep(ErrorGuide.CheckValidation, StepAction.Recheck);
            bool faster = ErrorGuide.IsNdmfBased(c.Tool) || (c.Row != null && (c.Row.group == 14 || c.Row.group == 15 || c.Row.group == 16));
            if (c.Row != null && !string.IsNullOrEmpty(c.Row.when))
            {
                string when = c.Row.when;
                if (when.StartsWith("컴파일", StringComparison.Ordinal)) return new CardStep(ErrorGuide.CheckCompile, StepAction.Recheck);
                bool play = when.Contains("플레이");
                bool test = when.Contains("Build & Test");
                bool build = when.Contains("빌드") || when.Contains("업로드");
                if (play && (build || test)) return new CardStep(ErrorGuide.CheckPlayOrUpload + (faster ? ErrorGuide.CheckPlayFaster : string.Empty));
                if (play) return new CardStep(ErrorGuide.CheckPlay);
                if (test && !build) return new CardStep(ErrorGuide.CheckTest);
                if (build || test || when.Contains("SDK"))
                    return new CardStep(unit != null && unit.kind == FinderUnitKind.Test ? ErrorGuide.CheckTest : ErrorGuide.CheckUpload);
                if (when.Contains("시작") || when.Contains("열기")) return new CardStep(ErrorGuide.CheckRestart);
                return new CardStep(ErrorGuide.CheckAgain);
            }
            switch (unit?.kind)
            {
                case FinderUnitKind.Play: return new CardStep(ErrorGuide.CheckPlay);
                case FinderUnitKind.Compile: return new CardStep(ErrorGuide.CheckCompile, StepAction.Recheck);
                default: return new CardStep(ErrorGuide.CheckPlayOrUpload + (faster ? ErrorGuide.CheckPlayFaster : string.Empty));
            }
        }

        // ---------------------------------------------------------------- the name of a tool the list does not know

        static readonly Regex s_AssetsProduct = new Regex(@"(?:^|[\s(/\\.])Assets[\\/]([^\\/\s:()]+)[\\/](?:([^\\/\s:()]+)[\\/])?", Opt);
        static readonly Regex s_FrameName = new Regex(@"^\s*(?:at\s+)?([A-Za-z_][\w`]*)(?:[.+:][\w`<>\[\],]+)+\s*\(", Opt);
        static readonly HashSet<string> s_PlainFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Editor", "Scripts", "Script", "Runtime", "Plugins", "Resources", "Shaders", "Shader", "Prefabs", "Prefab", "Materials",
            "Textures", "Animations", "Animation", "Models", "Model", "Core", "Src", "Source", "Data", "Scenes", "Components",
            "Common", "Utils", "Utility", "Utilities", "Internal", "Lib", "Libs", "Assets", "Resource", "Images", "Icons", "Fonts",
        };
        /// <summary>Namespaces of the runtime, Unity and the SDK: a stack frame there does not name the tool.</summary>
        static readonly HashSet<string> s_InfraRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            "System", "Mono", "Microsoft", "Unity", "UnityEngine", "UnityEditor", "UnityEditorInternal", "TMPro", "VRC", "VRCSDK",
            "VRCSDK2", "VRCSDK3", "nadena", "Newtonsoft", "JetBrains", "Littool", "Cysharp", "Bee", "NUnit",
        };

        /// <summary>
        /// A name for a tool the list does not know, from the message's own [name], the NDMF plugin that reported it, the folder
        /// under Assets (a product folder inside a maker's folder: "RealSweatSystem (CookiTools)") or the first namespace of the
        /// stack trace. Null when the tool is one the list knows, or nothing better than <see cref="FinderCard.Tool"/> is found.
        /// </summary>
        static string ToolLabelOf(FinderCard c, FinderItem first, ErrorCatalogData data)
        {
            // NDMF only shows what its plugins report: the plugin is the tool
            string plugin = PluginName(c);
            if (c.Tool == "NDMF" && plugin != null)
            {
                c.ToolGuessed = false;
                return plugin;
            }
            if (IsKnownTool(c.Tool, data)) return null;
            var reading = ErrorMessageReader.Read(first.text);
            string text = ErrorText.PackageCacheAsPackages(first.text ?? string.Empty);

            string vendor = null, product = null;
            if (c.Tool == null || ErrorGuide.IsAssetsFolder(c.Tool))
            {
                var m = s_AssetsProduct.Match(text);
                if (m.Success)
                {
                    vendor = m.Groups[1].Value;
                    if (m.Groups[2].Success && !s_PlainFolders.Contains(m.Groups[2].Value)) product = m.Groups[2].Value;
                    // Assets/Plugins/UniTask/…: "Plugins" is not a maker
                    if (s_PlainFolders.Contains(vendor) && product != null) vendor = null;
                }
            }

            // the tool names itself (its [name] in the message, or the NDMF plugin): sure; a folder or a namespace: a guess
            string name = reading.Speaker ?? plugin;
            bool sure = name != null;
            string label;
            if (name != null) label = vendor != null && !ContainsName(name, vendor) ? name + " (" + vendor + ")" : name;
            else if (product != null) label = vendor == null || Same(product, vendor) ? product : product + " (" + vendor + ")";
            else if (vendor != null) label = c.Tool == null ? "Assets/" + vendor : null;
            else label = c.Tool == null ? FirstToolNamespace(text) : null;

            if (label == null || label == c.Tool) return null;
            if (sure) c.ToolGuessed = false;
            else if (string.IsNullOrEmpty(c.Tool)) c.ToolGuessed = true;
            return label;
        }

        static bool Same(string a, string b) => a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        static bool ContainsName(string name, string vendor) => name.IndexOf(vendor, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The tools the list knows by name (their own rules and guidance): keep the list's name.</summary>
        static bool IsKnownTool(string tool, ErrorCatalogData data)
        {
            if (string.IsNullOrEmpty(tool)) return false;
            if (ErrorGuide.IsUnityPackage(tool)) return true;
            if (tool == "VRChat SDK" || tool == "Unity 화면" || tool == "NDMF 미리보기" || tool == "Modular Avatar" || tool == "NDMF") return true;
            foreach (var t in data?.tools ?? new CatalogTool[0])
            {
                if (t.tool == tool) return true;
            }
            foreach (var f in data?.assetFolders ?? new CatalogAssetFolder[0])
            {
                if (f.tool == tool) return true;
            }
            return false;
        }

        /// <summary>The display name of the NDMF plugin that reported the message (NDMF keeps it with each error), cleaned up.</summary>
        static string PluginName(FinderCard c)
        {
            foreach (var it in c.Items)
            {
                string p = it.plugin;
                if (string.IsNullOrWhiteSpace(p)) continue;
                p = p.Trim();
                // a plugin without its own display name shows its class name: "CookiTools.Editor.RealSweatPlugin" → "RealSweat"
                if (p.IndexOf(' ') < 0 && p.IndexOf('.') > 0)
                {
                    p = p.Substring(p.LastIndexOf('.') + 1);
                    if (p.EndsWith("Plugin", StringComparison.Ordinal) && p.Length > 8) p = p.Substring(0, p.Length - 6);
                }
                if (p.StartsWith("nadena.dev", StringComparison.Ordinal) || p.Length < 2) continue;
                return p;
            }
            return null;
        }

        static readonly Regex s_Dotted = new Regex(@"\b[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+(?:\+[A-Za-z_]\w*)*", Opt);
        const int MaxTypeNames = 16;

        /// <summary>
        /// The tool's namespaces (roots of stack frames that are not the runtime's, Unity's or the SDK's) and every type name
        /// under them in the stack lines: the frame's own class ("CookiTools.Editor.RealSweatRestBuildSyncPass") and the
        /// parameter types ("CookiTools.RealSweatTuning tuning"). A frame "Type.Method" also gives "Type".
        /// </summary>
        static void ComponentCandidates(string text, List<string> names, List<string> roots)
        {
            var lines = ErrorText.PackageCacheAsPackages(text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var m = s_FrameName.Match(lines[i]);
                if (!m.Success) continue;
                string root = m.Groups[1].Value;
                if (s_InfraRoots.Contains(root) || root.StartsWith("<", StringComparison.Ordinal) || roots.Contains(root)) continue;
                roots.Add(root);
            }
            if (roots.Count == 0) return;
            for (int i = 1; i < lines.Length && names.Count < MaxTypeNames; i++)
            {
                if (!s_FrameName.IsMatch(lines[i])) continue;
                foreach (Match t in s_Dotted.Matches(lines[i]))
                {
                    string name = t.Value;
                    int dot = name.IndexOf('.');
                    if (dot < 0 || !roots.Contains(name.Substring(0, dot))) continue;
                    AddName(names, name);
                    int last = Math.Max(name.LastIndexOf('.'), name.LastIndexOf('+'));
                    if (last > dot) AddName(names, name.Substring(0, last));
                }
            }
        }

        static void AddName(List<string> names, string name)
        {
            if (names.Count < MaxTypeNames && !names.Contains(name)) names.Add(name);
        }

        /// <summary>"CookiTools" of the plugin class name "CookiTools.Editor.RealSweatPlugin" (null for NDMF's own and Unity's).</summary>
        static string PluginRoot(FinderCard c)
        {
            foreach (var it in c.Items)
            {
                string p = it.plugin;
                if (string.IsNullOrEmpty(p) || p.IndexOf(' ') >= 0) continue;
                int dot = p.IndexOf('.');
                if (dot <= 0) continue;
                string root = p.Substring(0, dot);
                if (!s_InfraRoots.Contains(root)) return root;
            }
            return null;
        }

        /// <summary>The first namespace of the stack trace that is not the runtime's, Unity's or the SDK's.</summary>
        static string FirstToolNamespace(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var m = s_FrameName.Match(lines[i]);
                if (!m.Success) continue;
                string root = m.Groups[1].Value;
                if (s_InfraRoots.Contains(root) || root.StartsWith("<", StringComparison.Ordinal)) continue;
                return root;
            }
            return null;
        }

        static CatalogRule RuleOf(ErrorCatalogData data, string id)
        {
            if (data?.rules == null || string.IsNullOrEmpty(id)) return null;
            foreach (var r in data.rules)
            {
                if (r.id == id) return r;
            }
            return null;
        }

        /// <summary>The Assets folder ("Assets/OldTool") and the package id of the path that decided the rule.</summary>
        static void FolderAndPackage(string text, string tool, out string folder, out string package)
        {
            folder = tool;
            package = string.Empty;
            string t = ErrorText.PackageCacheAsPackages(text);
            foreach (Match mm in s_AnyPath.Matches(t))
            {
                string p = mm.Groups[1].Value.Replace('\\', '/');
                var parts = p.Split('/');
                if (parts.Length < 2) continue;
                if (parts[0] == "Packages" && package.Length == 0) package = parts[1];
                if (parts[0] == "Assets" && (folder == null || folder.StartsWith("Assets/", StringComparison.Ordinal))) folder = "Assets/" + parts[1];
            }
        }

        /// <summary>The line that tells where the error happened, and the part of it to highlight.</summary>
        static void DecidingLine(FinderCard c, string text, List<string> lines, ErrorCatalogData data)
        {
            string firstLine = lines.Count > 0 ? lines[0] : string.Empty;
            string mark = null;
            string line = null;
            switch (c.RuleId)
            {
                case "P2":
                case "P3":
                    var lead = s_LeadFile.Match(ErrorText.PackageCacheAsPackages(firstLine));
                    if (lead.Success) mark = FolderOf(lead.Groups[1].Value.Replace('\\', '/'), 2);
                    break;
                case "P9":
                    var vrc = s_VrcType.Match(firstLine);
                    if (vrc.Success) mark = vrc.Value;
                    break;
                case "P14":
                    var code = s_MaCode.Match(text);
                    if (code.Success) mark = code.Value;
                    break;
            }
            if (mark == null && !string.IsNullOrEmpty(c.Tool))
            {
                var keys = new List<string>();
                foreach (var t in data.tools ?? new CatalogTool[0])
                {
                    if (t.tool != c.Tool) continue;
                    keys.AddRange(t.prefixes ?? new string[0]);
                    foreach (var p in t.packages ?? new string[0]) keys.Add("Packages/" + p);
                }
                if (c.Tool.StartsWith("Assets/", StringComparison.Ordinal)) keys.Add(c.Tool);
                if (c.Tool == "Unity 화면") keys.AddRange(data.unityUi ?? new string[0]);
                if (c.RuleId == "P13") keys.Add("nadena.dev.ndmf.preview");
                string pkg = ErrorGuide.UnityPackageId(c.Tool);
                if (pkg.Length > 0) keys.Add("Packages/" + pkg);
                foreach (var assetFolder in data.assetFolders ?? new CatalogAssetFolder[0])
                {
                    if (assetFolder.tool == c.Tool) keys.Add("Assets/" + assetFolder.folder);
                }
                for (int i = 1; i < lines.Count && line == null; i++)
                {
                    string l = ErrorText.PackageCacheAsPackages(lines[i]).Replace('\\', '/');
                    foreach (var k in keys)
                    {
                        if (k.Length > 0 && l.Contains(k))
                        {
                            line = lines[i];
                            mark = k.StartsWith("Packages/", StringComparison.Ordinal) && !lines[i].Contains(k) ? null : k;
                            break;
                        }
                    }
                }
            }
            if (line == null && lines.Count > 1 && c.Match == CardMatch.None)
            {
                for (int i = 1; i < lines.Count; i++)
                {
                    if (lines[i].Trim().Length > 0)
                    {
                        line = lines[i];
                        break;
                    }
                }
            }
            if (line != null) c.LocationLines.Add(Shorten(line.Trim(), 300));
            c.LocationMark = mark;
        }

        static string FolderOf(string path, int depth)
        {
            var parts = path.Split('/');
            if (parts.Length <= depth) return path;
            return string.Join("/", parts, 0, depth);
        }

        // ---------------------------------------------------------------- similar

        static SimilarView SimilarOf(string received, int rowNo, ErrorCatalogData data)
        {
            var view = new SimilarView { Received = Shorten(received, 300) };
            var mine = WordsOf(received);
            string best = null;
            int bestShared = -1;
            foreach (var list in new[] { data.docs, data.index })
            {
                if (list == null) continue;
                foreach (var d in list)
                {
                    if (d.row != rowNo || string.IsNullOrEmpty(d.text)) continue;
                    int shared = 0;
                    foreach (var w in WordsOf(d.text))
                    {
                        if (mine.Contains(w)) shared++;
                    }
                    if (shared > bestShared)
                    {
                        bestShared = shared;
                        best = d.text;
                    }
                }
            }
            view.Listed = Shorten(FirstLineOf(best ?? string.Empty), 300);
            var listedWords = WordsOf(view.Listed);
            foreach (var w in mine)
            {
                if (listedWords.Contains(w)) view.Shared.Add(w);
            }
            return view;
        }

        public static HashSet<string> WordsOf(string s)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(s)) return set;
            foreach (Match m in s_Words.Matches(s))
            {
                string w = m.Value.ToLowerInvariant();
                bool cjk = w[0] >= 0x3040;
                if ((cjk ? w.Length >= 2 : w.Length >= 3) && !s_Stop.Contains(w) && !IsNumber(w)) set.Add(w);
            }
            return set;
        }

        static bool IsNumber(string w)
        {
            foreach (char ch in w)
            {
                if (!char.IsDigit(ch)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- text helpers

        static List<string> LinesOf(string text)
        {
            var list = new List<string>();
            foreach (var l in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                if (l.Trim().Length > 0) list.Add(l.TrimEnd());
            }
            return list;
        }

        static string FirstLineOf(string text)
        {
            foreach (var l in text.Split('\n'))
            {
                if (l.Trim().Length > 0) return l.Trim();
            }
            return string.Empty;
        }

        /// <summary>The message part of a console text: the lines before the stack trace.</summary>
        public static string MessagePart(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            int end = lines.Length;
            for (int i = 1; i < lines.Length; i++)
            {
                if (s_StackLine.IsMatch(lines[i]))
                {
                    end = i;
                    break;
                }
            }
            return string.Join("\n", lines, 0, end);
        }

        /// <summary>Folders of the asset paths named in the message part (not in the stack trace: that is the tool's own code).</summary>
        public static List<string> FoldersIn(string text)
        {
            var list = new List<string>();
            string msg = ErrorText.PackageCacheAsPackages(MessagePart(text));
            foreach (var line in msg.Split('\n'))
            {
                var lead = s_LeadFile.Match(line);
                if (lead.Success) AddFolder(list, lead.Groups[1].Value);
                foreach (Match m in s_AnyPath.Matches(line)) AddFolder(list, m.Groups[1].Value);
            }
            return list;
        }

        static void AddFolder(List<string> list, string path)
        {
            string p = path.Replace('\\', '/').TrimEnd('/', '.', ',', ';');
            int slash = p.LastIndexOf('/');
            string last = slash >= 0 ? p.Substring(slash + 1) : p;
            // a file (has an extension) → its folder
            if (last.IndexOf('.') > 0 && slash > 0) p = p.Substring(0, slash);
            if (p == "Assets" || p == "Packages" || p.Length == 0) return;
            if (!list.Contains(p)) list.Add(p);
        }

        public static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
