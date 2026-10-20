using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Littool.MaterialBatch
{
    /// <summary>
    /// The reference preset picker: one group per lilToon preset category, then the presets
    /// ("Illust  ·  Built-in"). Typing in the search box finds presets in every category.
    /// </summary>
    internal sealed class PresetDropdown : AdvancedDropdown
    {
        sealed class PresetItem : AdvancedDropdownItem
        {
            public readonly string Guid;

            public PresetItem(string name, string guid) : base(name)
            {
                Guid = guid;
                id = StableId("preset:" + guid);
            }
        }

        readonly IReadOnlyList<PresetInfo> m_Presets;
        readonly string m_SelectedGuid;
        readonly Action<string> m_OnPicked;

        public PresetDropdown(AdvancedDropdownState state, IReadOnlyList<PresetInfo> presets, string selectedGuid, Action<string> onPicked) : base(state)
        {
            m_Presets = presets ?? new List<PresetInfo>();
            m_SelectedGuid = selectedGuid;
            m_OnPicked = onPicked;
            minimumSize = new Vector2(260f, 300f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(L.T("preset.dropdownTitle"));
            var groups = new AdvancedDropdownItem[LilToonPresets.CategoryCount];

            foreach (var preset in m_Presets)
            {
                int category = Mathf.Clamp(preset.Category, 0, groups.Length - 1);
                if (groups[category] == null)
                {
                    groups[category] = new AdvancedDropdownItem(LilToonPresets.CategoryName(category)) { id = StableId("category:" + category) };
                }
                string text = preset.DisplayName + "  ·  " + L.T(preset.IsBuiltIn ? "preset.builtIn" : "preset.user");
                if (preset.Guid == m_SelectedGuid) text += "  ·  " + L.T("preset.current");
                groups[category].AddChild(new PresetItem(text, preset.Guid));
            }

            int added = 0;
            foreach (var group in groups)
            {
                if (group == null) continue;
                root.AddChild(group);
                added++;
            }
            if (added == 0) root.AddChild(new AdvancedDropdownItem(L.T("preset.none")) { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is PresetItem presetItem) m_OnPicked?.Invoke(presetItem.Guid);
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
