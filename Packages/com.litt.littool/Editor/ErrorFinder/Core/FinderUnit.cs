using System;
using System.Collections.Generic;

namespace Littool.ErrorFinder
{
    // What one inspection unit collected. Plain serializable classes (no Unity API) so the analysis can be tested
    // outside Unity; ErrorStore keeps them across domain reloads.

    /// <summary>Where a message was read.</summary>
    internal static class FinderChannel
    {
        /// <summary>The Unity console (Application.logMessageReceivedThreaded, or the console entries on [다시 검사]).</summary>
        public const string Console = "console";
        /// <summary>The VRChat SDK builder's build / upload result (OnSdkBuildError, OnSdkUploadError).</summary>
        public const string Sdk = "sdk";
        /// <summary>The red items of the SDK window's Validations.</summary>
        public const string Validation = "validation";
        /// <summary>NDMF's error report (NDMF Console).</summary>
        public const string Ndmf = "ndmf";
        /// <summary>Script compilation (CompilationPipeline.assemblyCompilationFinished).</summary>
        public const string Compile = "compile";
    }

    /// <summary>Kinds of inspection units.</summary>
    internal static class FinderUnitKind
    {
        /// <summary>Enter Play Mode → back in Edit Mode (Gesture Manager, Av3Emulator, NDMF Apply on Play).</summary>
        public const string Play = "play";
        /// <summary>Build &amp; Publish: build start → upload result.</summary>
        public const string Upload = "upload";
        /// <summary>Build &amp; Test: build start → build result.</summary>
        public const string Test = "test";
        /// <summary>One script compilation.</summary>
        public const string Compile = "compile";
        /// <summary>[다시 검사].</summary>
        public const string Rescan = "rescan";
    }

    /// <summary>One message collected in an inspection unit (the same text again = the same item, count + 1).</summary>
    [Serializable]
    internal sealed class FinderItem
    {
        /// <summary>The message as the console shows it: the message lines, then the stack trace.</summary>
        public string text;
        /// <summary>0 = error / exception / assert, 1 = warning.</summary>
        public int level;
        /// <summary>A <see cref="FinderChannel"/> value: where it was read first.</summary>
        public string channel;
        public int count = 1;
        public long ticks;
        public int order;
        /// <summary>Objects the message points at (console context object, NDMF references, SDK validation subject).</summary>
        public List<int> objectIds = new List<int>();
        /// <summary>The NDMF plugin that reported the message (NDMF keeps it with each error), "" when not known.</summary>
        public string plugin = string.Empty;

        public string FirstLine
        {
            get
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;
                foreach (var line in text.Split('\n'))
                {
                    string s = line.Trim();
                    if (s.Length > 0) return s;
                }
                return string.Empty;
            }
        }

        public FinderItem Copy()
        {
            return new FinderItem
            {
                text = text,
                level = level,
                channel = channel,
                count = count,
                ticks = ticks,
                order = order,
                objectIds = new List<int>(objectIds ?? new List<int>()),
                plugin = plugin,
            };
        }
    }

    /// <summary>
    /// An inspection unit: one play (enter → exit Play Mode), one upload (Build &amp; Publish / Build &amp; Test → result),
    /// one script compilation, or one [다시 검사].
    /// </summary>
    [Serializable]
    internal sealed class FinderUnit
    {
        public const int MaxItems = 2000;
        public const int MaxTextLength = 12000;

        public int id;
        /// <summary>A <see cref="FinderUnitKind"/> value.</summary>
        public string kind;
        public long startTicks;
        public long endTicks;
        public bool open;
        /// <summary>failed · succeeded · "" (not known)</summary>
        public string outcome = string.Empty;
        public List<FinderItem> items = new List<FinderItem>();
        public List<string> channels = new List<string>();
        public int nextOrder;

        /// <summary>Upload units: "" while building, "built" after a successful build, "uploading" after the upload started.</summary>
        public string stage = string.Empty;
        /// <summary>EditorApplication.timeSinceStartup after which an upload unit that heard nothing more is closed (0 = none).</summary>
        public double deadline;
        /// <summary>Compile units: errors heard just before the compilation (they are kept only when it fails).</summary>
        public List<FinderItem> before = new List<FinderItem>();

        [NonSerialized] Dictionary<string, FinderItem> m_Index;

        public bool Exists => id != 0;
        public bool Failed => outcome == "failed";
        public bool Succeeded => outcome == "succeeded";

        public void AddChannel(string channel)
        {
            if (!string.IsNullOrEmpty(channel) && !channels.Contains(channel)) channels.Add(channel);
        }

        public bool HasErrors
        {
            get
            {
                foreach (var it in items)
                {
                    if (it.level == 0) return true;
                }
                return false;
            }
        }

        /// <summary>Adds a message: the same text again only raises its count.</summary>
        public FinderItem Add(string text, int level, string channel, long ticks, int objectId = 0)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = text.TrimEnd();
            if (text.Length == 0) return null;
            if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
            if (m_Index == null)
            {
                m_Index = new Dictionary<string, FinderItem>(StringComparer.Ordinal);
                foreach (var it in items) m_Index[it.text] = it;
            }
            AddChannel(channel);
            if (m_Index.TryGetValue(text, out var item))
            {
                item.count++;
                if (level < item.level) item.level = level;
                if (objectId != 0 && !item.objectIds.Contains(objectId)) item.objectIds.Add(objectId);
                return item;
            }
            item = new FinderItem
            {
                text = text,
                level = level,
                channel = channel,
                ticks = ticks,
                order = nextOrder++,
            };
            if (objectId != 0) item.objectIds.Add(objectId);
            items.Add(item);
            m_Index[text] = item;
            if (items.Count > MaxItems) Evict();
            return item;
        }

        /// <summary>
        /// Adds a message unless an item with the same first line is already there (the SDK's build error is also in the
        /// console with a stack trace). Returns the item either way.
        /// </summary>
        public FinderItem AddOnce(string text, int level, string channel, long ticks, int objectId = 0)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string first = new FinderItem { text = text }.FirstLine;
            foreach (var it in items)
            {
                if (it.FirstLine == first)
                {
                    AddChannel(channel);
                    if (level < it.level) it.level = level;
                    if (objectId != 0 && !it.objectIds.Contains(objectId)) it.objectIds.Add(objectId);
                    return it;
                }
            }
            return Add(text, level, channel, ticks, objectId);
        }

        /// <summary>Over the limit: the oldest warning goes first, then the oldest error (the first errors are usually the cause).</summary>
        void Evict()
        {
            int victim = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].level > 0)
                {
                    victim = i;
                    break;
                }
            }
            if (victim < 0) victim = items.Count - 1;  // all errors: drop the newest, keep the first (cause) ones
            m_Index?.Remove(items[victim].text);
            items.RemoveAt(victim);
        }

        public void ForgetIndex() => m_Index = null;
    }
}
