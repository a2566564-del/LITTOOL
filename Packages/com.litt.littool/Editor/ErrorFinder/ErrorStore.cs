using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// What the Error Finder has collected in this editor session. A ScriptableSingleton without a file:
    /// it survives domain reloads (entering Play Mode, recompiling) but not an editor restart
    /// (the requirements: past results are not kept).
    /// </summary>
    internal sealed class ErrorStore : ScriptableSingleton<ErrorStore>
    {
        /// <summary>Messages heard outside any unit that are kept for [다시 검사].</summary>
        public const int MaxLoose = 300;

        [SerializeField] int m_NextId = 1;
        [SerializeField] FinderUnit m_Current = new FinderUnit();
        [SerializeField] FinderUnit m_Shown = new FinderUnit();
        [SerializeField] List<FinderItem> m_Loose = new List<FinderItem>();
        [SerializeField] long m_LastScanTicks;
        [SerializeField] string m_LastScanKind = string.Empty;
        [SerializeField] string m_Notice = string.Empty;
        [SerializeField] bool m_OpenTabRequest;
        [SerializeField] int m_Revision;
        [SerializeField] int m_LooseRevision;

        /// <summary>The unit being collected (Exists = false when none).</summary>
        public FinderUnit Current => m_Current ?? (m_Current = new FinderUnit());

        /// <summary>The latest finished unit with errors (Exists = false when none).</summary>
        public FinderUnit Shown => m_Shown ?? (m_Shown = new FinderUnit());

        public List<FinderItem> Loose => m_Loose ?? (m_Loose = new List<FinderItem>());

        /// <summary>When the last unit or [다시 검사] finished (0 = not yet in this session).</summary>
        public long LastScanTicks => m_LastScanTicks;
        public string LastScanKind => m_LastScanKind ?? string.Empty;

        /// <summary>A one-line note under the summary (e.g. after [다시 검사] found nothing new). "" = none.</summary>
        public string Notice
        {
            get => m_Notice ?? string.Empty;
            set
            {
                m_Notice = value ?? string.Empty;
                m_Revision++;
            }
        }

        /// <summary>Changes whenever what the tab shows changes (cache key of the tab).</summary>
        public int Revision => m_Revision;

        /// <summary>Changes whenever the loose messages change.</summary>
        public int LooseRevision => m_LooseRevision;

        /// <summary>Set when a unit ended with errors while the LITTOOL window was closed: it opens on the Error Finder tab.</summary>
        public bool OpenTabRequest
        {
            get => m_OpenTabRequest;
            set => m_OpenTabRequest = value;
        }

        /// <summary>Starts a new unit. Messages heard before it stop being "loose" (they belong to the time before).</summary>
        public FinderUnit Begin(string kind)
        {
            m_Current = new FinderUnit
            {
                id = m_NextId++,
                kind = kind,
                startTicks = DateTime.Now.Ticks,
                open = true,
            };
            if (kind == FinderUnitKind.Compile)
            {
                foreach (var it in Loose) m_Current.before.Add(it);
            }
            ClearLoose();
            return m_Current;
        }

        /// <summary>A unit for [다시 검사] that is not the current one (the current one keeps collecting).</summary>
        public FinderUnit NewRescan()
        {
            return new FinderUnit
            {
                id = m_NextId++,
                kind = FinderUnitKind.Rescan,
                startTicks = DateTime.Now.Ticks,
                endTicks = DateTime.Now.Ticks,
                open = false,
            };
        }

        /// <summary>Closes the current unit and returns it.</summary>
        public FinderUnit End(string outcome)
        {
            var unit = Current;
            if (!unit.Exists) return unit;
            unit.open = false;
            unit.endTicks = DateTime.Now.Ticks;
            unit.deadline = 0;
            if (!string.IsNullOrEmpty(outcome)) unit.outcome = outcome;
            m_Current = new FinderUnit();
            return unit;
        }

        public void AddLoose(string text, int level, long ticks)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.TrimEnd();
            if (text.Length > FinderUnit.MaxTextLength) text = text.Substring(0, FinderUnit.MaxTextLength);
            var loose = Loose;
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                if (loose[i].text == text)
                {
                    loose[i].count++;
                    if (level < loose[i].level) loose[i].level = level;
                    m_LooseRevision++;
                    return;
                }
            }
            loose.Add(new FinderItem { text = text, level = level, channel = FinderChannel.Console, ticks = ticks, order = loose.Count });
            if (loose.Count > MaxLoose)
            {
                int victim = loose.FindIndex(x => x.level > 0);
                loose.RemoveAt(victim >= 0 ? victim : 0);
            }
            m_LooseRevision++;
        }

        public void ClearLoose()
        {
            if (Loose.Count == 0) return;
            Loose.Clear();
            m_LooseRevision++;
        }

        /// <summary>The tab shows <paramref name="unit"/> from now on.</summary>
        public void Show(FinderUnit unit)
        {
            m_Shown = unit ?? new FinderUnit();
            m_LastScanTicks = DateTime.Now.Ticks;
            m_LastScanKind = unit != null ? unit.kind : string.Empty;
            m_Notice = string.Empty;
            m_Revision++;
        }

        /// <summary>
        /// A unit ended without errors: the shown result is cleared when this unit shows it is out of date
        /// (a clean compile says nothing about an upload, a clean upload covers everything).
        /// </summary>
        public void Clean(FinderUnit unit)
        {
            m_LastScanTicks = DateTime.Now.Ticks;
            m_LastScanKind = unit != null ? unit.kind : string.Empty;
            m_Notice = string.Empty;
            if (Shown.Exists && unit != null && Covers(unit, Shown.kind)) m_Shown = new FinderUnit();
            m_Revision++;
        }

        /// <summary>Does a unit of this kind that ended without errors make a shown result of <paramref name="shownKind"/> out of date?</summary>
        public static bool Covers(FinderUnit unit, string shownKind)
        {
            bool compileLike = shownKind == FinderUnitKind.Compile || shownKind == FinderUnitKind.Rescan;
            switch (unit.kind)
            {
                case FinderUnitKind.Upload:
                    // a finished upload went through compile, build and upload; one that stopped without a result only built
                    return unit.Succeeded || compileLike || shownKind == FinderUnitKind.Test;
                case FinderUnitKind.Test:
                    // a build says nothing about the upload step (IDs, size, server) or Play Mode tools
                    return compileLike || shownKind == FinderUnitKind.Test;
                case FinderUnitKind.Play:
                    // Play Mode needs the scripts compiled
                    return compileLike || shownKind == FinderUnitKind.Play;
                case FinderUnitKind.Compile:
                case FinderUnitKind.Rescan:
                    return compileLike;
                default:
                    return false;
            }
        }

        public void Touch() => m_Revision++;

        /// <summary>Forget everything (auto collect switched off with a unit open, or a test).</summary>
        public void ResetCurrent()
        {
            m_Current = new FinderUnit();
            m_Revision++;
        }
    }
}
