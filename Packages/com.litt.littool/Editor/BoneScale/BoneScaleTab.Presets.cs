using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    // "체형" section: body shapes saved in the project settings (save with a name, rename, apply, delete).
    internal sealed partial class BoneScaleTab
    {
        const string NameControl = "LITTOOL.BodyPresetName";

        enum NamingMode
        {
            None,
            Save,
            Rename,
        }

        [NonSerialized] NamingMode m_Naming;
        [NonSerialized] string m_NameText = string.Empty;
        [NonSerialized] bool m_FocusName;

        sealed class PresetView
        {
            public string[] Names = new string[0];
            public int Index = -1;
            public NamingMode Naming;
            public string NameNote;
            public bool NameNoteWarn;
            public bool CanConfirm;
            public string ConfirmLabel;
        }

        static List<BodyPreset> Presets => LittoolSettings.instance.BodyPresets;

        BodyPreset SelectedPreset
        {
            get
            {
                var presets = Presets;
                return m_PresetIndex >= 0 && m_PresetIndex < presets.Count ? presets[m_PresetIndex] : null;
            }
        }

        void BuildPresetSnapshot(Snapshot snap)
        {
            var view = snap.Presets;
            var presets = Presets;
            view.Names = new string[presets.Count];
            for (int i = 0; i < presets.Count; i++) view.Names[i] = string.IsNullOrEmpty(presets[i].Name) ? "-" : presets[i].Name;
            m_PresetIndex = presets.Count == 0 ? -1 : Mathf.Clamp(m_PresetIndex, 0, presets.Count - 1);
            view.Index = m_PresetIndex;

            if (m_Naming == NamingMode.Rename && SelectedPreset == null) m_Naming = NamingMode.None;
            view.Naming = m_Naming;
            if (m_Naming == NamingMode.None) return;

            string name = (m_NameText ?? string.Empty).Trim();
            bool renaming = m_Naming == NamingMode.Rename;
            var current = SelectedPreset;
            var same = presets.Find(p => p.Name == name && !(renaming && p == current));

            view.ConfirmLabel = L.T(renaming ? "bs.preset.renameOk" : (same != null ? "bs.preset.overwrite" : "bs.preset.saveOk"));
            if (name.Length == 0)
            {
                view.NameNote = L.T("bs.preset.nameEmpty");
                view.NameNoteWarn = true;
            }
            else if (renaming && same != null)
            {
                view.NameNote = L.T("bs.preset.nameTaken");
                view.NameNoteWarn = true;
            }
            else if (renaming && current != null && name == current.Name)
            {
                view.NameNote = L.F("bs.preset.renameNote", current.Name);
            }
            else if (same != null)
            {
                view.NameNote = L.T("bs.preset.overwriteNote");
                view.NameNoteWarn = true;
                view.CanConfirm = true;
            }
            else
            {
                view.NameNote = renaming ? L.F("bs.preset.renameNote", current != null ? current.Name : "-") : L.F("bs.preset.saveNote", m_Model.Groups.Count);
                view.CanConfirm = true;
            }
        }

        void DrawPresets(Snapshot snap)
        {
            var view = snap.Presets;
            bool any = view.Names.Length > 0;
            GUILayout.Label(L.T("bs.preset.header"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!any))
                {
                    EditorGUI.BeginChangeCheck();
                    int index = EditorGUILayout.Popup(any ? view.Index : 0, any ? view.Names : new[] { L.T("bs.preset.empty") }, GUILayout.MinWidth(60f));
                    if (EditorGUI.EndChangeCheck() && any && index != view.Index)
                    {
                        m_PresetIndex = index;
                        if (m_Naming == NamingMode.Rename) CloseNaming();
                        Repaint();
                    }
                    if (GUILayout.Button(new GUIContent(L.T("bs.preset.apply"), L.T("bs.preset.applyTooltip")), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        ApplyPreset(view.Index);
                }

                bool saving = view.Naming == NamingMode.Save;
                if (GUILayout.Toggle(saving, new GUIContent(L.T("bs.preset.save"), L.T("bs.preset.saveTooltip")), EditorStyles.miniButton, GUILayout.ExpandWidth(false)) != saving)
                    ToggleNaming(NamingMode.Save);

                using (new EditorGUI.DisabledScope(!any))
                {
                    bool renaming = view.Naming == NamingMode.Rename;
                    if (GUILayout.Toggle(renaming, L.T("bs.preset.rename"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)) != renaming)
                        ToggleNaming(NamingMode.Rename);
                    if (GUILayout.Button(L.T("bs.preset.delete"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        DeletePreset(view.Index);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (view.Naming != NamingMode.None) DrawNamePanel(view);
            else if (!any) GUILayout.Label(L.T("bs.preset.emptyHint"), m_Styles.MiniDimWrap);
        }

        void DrawNamePanel(PresetView view)
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == NameControl)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.Use();
                    ConfirmName();
                    GUIUtility.ExitGUI();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    evt.Use();
                    CloseNaming();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var nameLabel = new GUIContent(L.T("bs.preset.name"));
                    GUILayout.Label(nameLabel, GUILayout.Width(EditorStyles.label.CalcSize(nameLabel).x + 4f));
                    GUI.SetNextControlName(NameControl);
                    m_NameText = EditorGUILayout.TextField(m_NameText ?? string.Empty, GUILayout.MinWidth(60f));
                    using (new EditorGUI.DisabledScope(!view.CanConfirm))
                    {
                        if (GUILayout.Button(view.ConfirmLabel, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        {
                            ConfirmName();
                            GUIUtility.ExitGUI();
                        }
                    }
                    if (GUILayout.Button(L.T("common.cancel"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        CloseNaming();
                        GUIUtility.ExitGUI();
                    }
                }
                GUILayout.Label(view.NameNote, view.NameNoteWarn ? m_Styles.MiniWarnWrap : m_Styles.MiniDimWrap);
            }

            if (m_FocusName && evt.type == EventType.Repaint)
            {
                m_FocusName = false;
                EditorGUI.FocusTextInControl(NameControl);
                Repaint();
            }
        }

        void ToggleNaming(NamingMode mode)
        {
            if (m_Naming == mode)
            {
                CloseNaming();
                return;
            }
            if (mode == NamingMode.Rename && SelectedPreset == null) return;
            m_Naming = mode;
            m_NameText = mode == NamingMode.Rename ? SelectedPreset.Name : NextPresetName();
            m_FocusName = true;
            m_Message = null;
            GUI.FocusControl(null);
            Repaint();
        }

        void CloseNaming()
        {
            m_Naming = NamingMode.None;
            m_NameText = string.Empty;
            m_FocusName = false;
            GUI.FocusControl(null);
            Repaint();
        }

        static string NextPresetName()
        {
            var presets = Presets;
            for (int n = 1; n < 10000; n++)
            {
                string name = L.F("bs.preset.defaultName", n);
                if (!presets.Exists(p => p.Name == name)) return name;
            }
            return L.F("bs.preset.defaultName", presets.Count + 1);
        }

        void ConfirmName()
        {
            string name = (m_NameText ?? string.Empty).Trim();
            if (name.Length == 0 || m_Model == null || !m_Model.IsOk) return;
            var presets = Presets;

            if (m_Naming == NamingMode.Rename)
            {
                var current = SelectedPreset;
                if (current == null)
                {
                    CloseNaming();
                    return;
                }
                if (name == current.Name || presets.Exists(p => p != current && p.Name == name)) return;
                string old = current.Name;
                current.Name = name;
                LittoolSettings.instance.SaveSettings();
                CloseNaming();
                SetMessage(L.F("bs.preset.renamed", old, name), MessageType.Info);
                return;
            }

            if (m_Naming != NamingMode.Save) return;
            var preset = presets.Find(p => p.Name == name);
            bool overwrite = preset != null;
            if (preset == null)
            {
                preset = new BodyPreset { Name = name };
                presets.Add(preset);
            }
            preset.Keys = new List<string>();
            preset.Values = new List<Vector3>();
            foreach (var group in m_Model.Groups)
            {
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform != null) preset.Set(slot.PresetKey, slot.Transform.localScale);
                }
            }
            LittoolSettings.instance.SaveSettings();
            m_PresetIndex = presets.IndexOf(preset);
            CloseNaming();
            SetMessage(L.F(overwrite ? "bs.preset.overwritten" : "bs.preset.saved", name, preset.Count), MessageType.Info);
        }

        void DeletePreset(int index)
        {
            var presets = Presets;
            if (index < 0 || index >= presets.Count) return;
            var preset = presets[index];
            if (!EditorUtility.DisplayDialog(L.T("bs.preset.deleteTitle"), L.F("bs.preset.deleteBody", preset.Name),
                    L.T("bs.preset.deleteOk"), L.T("common.cancel"))) return;
            presets.RemoveAt(index);
            LittoolSettings.instance.SaveSettings();
            m_PresetIndex = presets.Count == 0 ? -1 : Mathf.Min(index, presets.Count - 1);
            if (m_Naming == NamingMode.Rename) CloseNaming();
            SetMessage(L.F("bs.preset.deleted", preset.Name), MessageType.Info);
        }

        /// <summary>Writes a saved body shape to the avatar (outfits follow as usual). One Undo step.</summary>
        void ApplyPreset(int index)
        {
            var presets = Presets;
            if (index < 0 || index >= presets.Count || m_Model == null || !m_Model.IsOk) return;
            var preset = presets[index];

            // 1. Every saved bone gets a row (a pair added by one side also finds the other side).
            bool added = false;
            for (int i = 0; i < preset.Count; i++)
            {
                string key = preset.Keys[i];
                if (FindSlot(key) != null) continue;
                string entry = EntryFor(key);
                if (entry == null || Entries.Contains(entry)) continue;
                Entries.Add(entry);
                added = true;
                m_Model = AvatarModel.Build(m_Avatar, Entries, m_BreastL, m_BreastR);
                m_ModelDirty = false;
            }
            if (added) LittoolSettings.instance.SaveSettings();

            // 2. Write the values.
            var edits = new List<ScaleEditor.Edit>();
            var bones = new HashSet<Transform>();
            int skipped = 0;
            for (int i = 0; i < preset.Count; i++)
            {
                var slot = FindSlot(preset.Keys[i]);
                if (slot == null || slot.Transform == null || !bones.Add(slot.Transform))
                {
                    if (slot == null || slot.Transform == null) skipped++;
                    continue;
                }
                edits.Add(new ScaleEditor.Edit(slot.Transform, ScaleEditor.Clamp(preset.Values[i])));
            }
            if (edits.Count == 0)
            {
                SetMessage(L.F("bs.preset.nothing", preset.Name), MessageType.Warning);
                return;
            }

            SceneFlash.Stop();
            int group = BeginUndo(ScaleEditor.UndoName);
            ScaleEditor.Apply(edits, m_Outfits);
            Undo.CollapseUndoOperations(group);
            OnValuesChanged(false);

            var written = new List<string>();
            var follow = new List<string>();
            if (m_Outfits != null)
            {
                foreach (var outfit in m_Outfits.Outfits)
                {
                    if (outfit.Root == null) continue;
                    bool locked = false, unlocked = false;
                    foreach (var pair in outfit.Pairs)
                    {
                        if (!bones.Contains(pair.AvatarBone)) continue;
                        if (pair.Locked) locked = true;
                        else unlocked = true;
                    }
                    if (unlocked) written.Add(outfit.Root.name);
                    else if (locked) follow.Add(outfit.Root.name);
                }
            }
            string message = L.F("bs.preset.applied", preset.Name, edits.Count);
            if (written.Count > 0) message += L.F("bs.preset.appliedWritten", string.Join(", ", written));
            if (follow.Count > 0) message += L.F("bs.preset.appliedFollow", string.Join(", ", follow));
            if (skipped > 0) message += L.F("bs.preset.appliedSkipped", skipped);
            SetMessage(message + L.T("bs.undoHint"), MessageType.Info);
        }

        BoneSlot FindSlot(string presetKey)
        {
            if (m_Model == null) return null;
            foreach (var group in m_Model.Groups)
            {
                foreach (var slot in group.Slots)
                {
                    if (slot.PresetKey == presetKey) return slot;
                }
            }
            return null;
        }

        /// <summary>The list entry that shows a preset key: "breast.L" → "breast", a bone path → itself.</summary>
        static string EntryFor(string presetKey)
        {
            if (string.IsNullOrEmpty(presetKey)) return null;
            if (BoneCatalog.IsCustom(presetKey)) return presetKey;
            int dot = presetKey.IndexOf('.');
            string id = dot >= 0 ? presetKey.Substring(0, dot) : presetKey;
            return BoneCatalog.TryGet(id, out _) ? id : null;
        }
    }
}
