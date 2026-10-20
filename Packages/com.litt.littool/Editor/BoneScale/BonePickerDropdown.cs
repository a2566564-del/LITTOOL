using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// The bone list's + menu: the standard groups that are not listed yet, then "Armature에서 다른 본 고르기…", the
    /// avatar's Armature as a searchable tree. A bone with children opens as a group whose first item is the bone itself.
    /// </summary>
    internal sealed class BonePickerDropdown : AdvancedDropdown
    {
        const int MaxDepth = 48;
        const int StandardIdBase = 1000000;
        const int GroupIdBase = 2000000;

        readonly IList<(string key, string label)> m_Standard;
        readonly Transform m_Root;
        readonly Func<Transform, bool> m_IsListed;
        readonly Action<string> m_OnStandard;
        readonly Action<Transform> m_OnBone;
        readonly List<Transform> m_Bones = new List<Transform>();
        int m_GroupCount;

        public BonePickerDropdown(AdvancedDropdownState state, IList<(string key, string label)> standard, Transform root,
            Func<Transform, bool> isListed, Action<string> onStandard, Action<Transform> onBone)
            : base(state)
        {
            m_Standard = standard;
            m_Root = root;
            m_IsListed = isListed;
            m_OnStandard = onStandard;
            m_OnBone = onBone;
            minimumSize = new Vector2(280f, 340f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            m_Bones.Clear();
            m_GroupCount = 0;
            var root = new AdvancedDropdownItem(L.T("bs.add.title")) { id = NextGroupId() };

            if (m_Standard.Count == 0)
            {
                root.AddChild(new AdvancedDropdownItem(L.T("bs.add.allListed")) { id = NextGroupId(), enabled = false });
            }
            for (int i = 0; i < m_Standard.Count; i++)
            {
                root.AddChild(new AdvancedDropdownItem(m_Standard[i].label) { id = StandardIdBase + i });
            }
            root.AddSeparator();

            var pick = new AdvancedDropdownItem(L.T("bs.add.pick")) { id = NextGroupId() };
            if (m_Root != null)
            {
                pick.AddChild(MakeBone(m_Root, L.F("bs.pick.this", m_Root.name)));
                if (m_Root.childCount > 0) pick.AddSeparator();
                AddChildren(pick, m_Root, 0);
            }
            root.AddChild(pick);
            return root;
        }

        int NextGroupId() => GroupIdBase + m_GroupCount++;

        void AddChildren(AdvancedDropdownItem parent, Transform t, int depth)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child.childCount > 0 && depth < MaxDepth)
                {
                    var group = new AdvancedDropdownItem(child.name) { id = NextGroupId() };
                    group.AddChild(MakeBone(child, L.F("bs.pick.this", child.name)));
                    group.AddSeparator();
                    AddChildren(group, child, depth + 1);
                    parent.AddChild(group);
                }
                else
                {
                    parent.AddChild(MakeBone(child, child.name));
                }
            }
        }

        AdvancedDropdownItem MakeBone(Transform t, string label)
        {
            var item = new AdvancedDropdownItem(label)
            {
                id = m_Bones.Count,
                enabled = m_IsListed == null || !m_IsListed(t),
            };
            m_Bones.Add(t);
            return item;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item == null) return;
            int id = item.id;
            if (id >= 0 && id < m_Bones.Count)
            {
                var bone = m_Bones[id];
                if (bone != null && m_OnBone != null) m_OnBone(bone);
                return;
            }
            int standard = id - StandardIdBase;
            if (standard >= 0 && standard < m_Standard.Count && m_OnStandard != null) m_OnStandard(m_Standard[standard].key);
        }
    }
}
