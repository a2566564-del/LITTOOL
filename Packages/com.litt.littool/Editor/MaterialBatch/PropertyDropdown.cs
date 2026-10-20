using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Littool.MaterialBatch
{
    /// <summary>
    /// The "+" menu of the list: sections first, then (inside a section) "Add whole section"
    /// and the individual properties. Typing in the search box finds properties across all sections.
    /// Entries already in the list are not offered again.
    /// </summary>
    internal sealed class PropertyDropdown : AdvancedDropdown
    {
        sealed class KeyItem : AdvancedDropdownItem
        {
            public readonly string Key;

            public KeyItem(string name, string key) : base(name)
            {
                Key = key;
                id = StableId(key);
            }
        }

        readonly HashSet<string> m_Existing;
        readonly Action<string> m_OnPicked;

        public PropertyDropdown(AdvancedDropdownState state, HashSet<string> existing, Action<string> onPicked) : base(state)
        {
            m_Existing = existing ?? new HashSet<string>();
            m_OnPicked = onPicked;
            minimumSize = new Vector2(300f, 360f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(L.T("dropdown.title"));
            int groups = 0;

            foreach (var section in LilToonCatalog.Sections)
            {
                if (m_Existing.Contains(section.Id)) continue; // whole section already in the list

                var group = new AdvancedDropdownItem(section.Name) { id = StableId("section:" + section.Id) };
                group.AddChild(new KeyItem(L.F("dropdown.addAll", section.Name), section.Id));
                group.AddSeparator();
                foreach (var property in section.Properties)
                {
                    if (m_Existing.Contains(property.Key)) continue;
                    group.AddChild(new KeyItem(property.Name + "  ·  " + property.TypeLabel, property.Key));
                }
                root.AddChild(group);
                groups++;
            }

            if (groups == 0)
            {
                root.AddChild(new AdvancedDropdownItem(L.T("dropdown.empty")) { enabled = false });
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is KeyItem keyItem) m_OnPicked?.Invoke(keyItem.Key);
        }

        /// <summary>Deterministic id (string.GetHashCode is not stable across runs).</summary>
        static int StableId(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return hash == 0 ? 1 : hash;
            }
        }
    }
}
