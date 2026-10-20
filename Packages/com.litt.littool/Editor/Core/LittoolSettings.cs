using System;
using System.Collections.Generic;
using Littool.BoneScale;
using Littool.MaterialBatch;
using UnityEditor;
using UnityEngine;

namespace Littool
{
    /// <summary>
    /// Project-level settings, stored in ProjectSettings/LITTOOLSettings.asset (text).
    /// The file lives outside Assets, so updating the LITTOOL package never overwrites it,
    /// and it travels with the project when the project is copied or put under version control.
    /// </summary>
    [FilePath("ProjectSettings/LITTOOLSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LittoolSettings : ScriptableSingleton<LittoolSettings>
    {
        const int CurrentVersion = 3;

        [SerializeField] int m_Version = CurrentVersion;
        [SerializeField] bool m_Initialized;
        [SerializeField] List<string> m_MaterialBatchEntries = new List<string>();

        // Bone Scale (v1.2). Fields missing from an older file keep these defaults.
        [SerializeField] bool m_BoneScaleInitialized;
        [SerializeField] List<string> m_BoneScaleEntries = new List<string>();
        [SerializeField] List<BodyPreset> m_BodyPresets = new List<BodyPreset>();
        [SerializeField] bool m_AutoMatchNewOutfits = true;

        // Error Finder (v1.3). A field missing from an older file keeps this default.
        [SerializeField] bool m_ErrorAutoCollect = true;

        /// <summary>Settings format version (for future migrations).</summary>
        public int Version => m_Version;

        /// <summary>
        /// Entries of the Material Batch list. A section id (e.g. "shadow") or "p:" + property name.
        /// The same List instance is kept for the lifetime of the settings object.
        /// </summary>
        public List<string> MaterialBatchEntries
        {
            get
            {
                if (m_MaterialBatchEntries == null) m_MaterialBatchEntries = new List<string>();
                if (!m_Initialized)
                {
                    m_MaterialBatchEntries.Clear();
                    m_MaterialBatchEntries.AddRange(LilToonCatalog.DefaultEntries);
                    m_Initialized = true;
                    SaveSettings();
                }
                return m_MaterialBatchEntries;
            }
        }

        /// <summary>Restores the default list (the four default sections).</summary>
        public void ResetMaterialBatchEntries()
        {
            var entries = MaterialBatchEntries;
            entries.Clear();
            entries.AddRange(LilToonCatalog.DefaultEntries);
            SaveSettings();
        }

        /// <summary>Removes entries that are not in the catalogue (e.g. after a catalogue update).</summary>
        public void SanitizeMaterialBatchEntries()
        {
            var entries = MaterialBatchEntries;
            var seen = new HashSet<string>();
            int removed = entries.RemoveAll(key => !LilToonCatalog.IsValidKey(key) || !seen.Add(key));
            if (removed > 0) SaveSettings();
        }

        /// <summary>
        /// Bones listed in the Bone Scale tab: a standard group id ("head", "upperArm") or "path:" + a path from the
        /// avatar root. The same List instance is kept for the lifetime of the settings object.
        /// </summary>
        public List<string> BoneScaleEntries
        {
            get
            {
                if (m_BoneScaleEntries == null) m_BoneScaleEntries = new List<string>();
                if (!m_BoneScaleInitialized)
                {
                    m_BoneScaleEntries.Clear();
                    m_BoneScaleEntries.AddRange(BoneCatalog.DefaultEntries);
                    m_BoneScaleInitialized = true;
                    SaveSettings();
                }
                return m_BoneScaleEntries;
            }
        }

        public void SanitizeBoneScaleEntries()
        {
            var entries = BoneScaleEntries;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int removed = entries.RemoveAll(key => !BoneCatalog.IsValidKey(key) || !seen.Add(key));
            if (removed > 0) SaveSettings();
        }

        /// <summary>Saved body shapes ("체형"), in the order they were saved.</summary>
        public List<BodyPreset> BodyPresets
        {
            get
            {
                if (m_BodyPresets == null) m_BodyPresets = new List<BodyPreset>();
                m_BodyPresets.RemoveAll(p => p == null);
                return m_BodyPresets;
            }
        }

        /// <summary>"새 의상 자동 맞추기": match newly dressed outfits to the current body shape.</summary>
        public bool AutoMatchNewOutfits
        {
            get => m_AutoMatchNewOutfits;
            set
            {
                if (m_AutoMatchNewOutfits == value) return;
                m_AutoMatchNewOutfits = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// "자동으로 모으기" of the Error Finder: listen to the console, the SDK, NDMF and script compilation from the
        /// moment the editor starts (default). Off = nothing is heard; [다시 검사] reads everything at once.
        /// </summary>
        public bool ErrorAutoCollect
        {
            get => m_ErrorAutoCollect;
            set
            {
                if (m_ErrorAutoCollect == value) return;
                m_ErrorAutoCollect = value;
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            m_Version = CurrentVersion;
            Save(true);
        }
    }
}
