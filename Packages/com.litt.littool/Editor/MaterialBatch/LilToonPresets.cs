using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.MaterialBatch
{
    /// <summary>One value stored in a lilToon preset.</summary>
    internal struct PresetValue
    {
        /// <summary>Same categories as <see cref="MaterialScanner.CategoryOf"/>: 0 Float, 1 Color, 2 Vector.</summary>
        public int Category;
        public float Float;
        public Color Color;
        public Vector4 Vector;
    }

    /// <summary>A lilToon preset asset (lilToonPreset), read without referencing lilToon's code.</summary>
    internal sealed class PresetInfo
    {
        public string Guid;
        public string Path;
        public string FileName;
        public int Category;
        public bool IsBuiltIn;
        public readonly Dictionary<string, string> NamesByLanguage = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, PresetValue> Values = new Dictionary<string, PresetValue>(StringComparer.Ordinal);

        /// <summary>Preset name in the current UI language (lilToon's own presets only have English and Japanese).</summary>
        public string DisplayName
        {
            get
            {
                foreach (string language in LilToonPresets.LanguageKeys(L.Current))
                {
                    if (NamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrEmpty(name)) return name;
                }
                foreach (string language in LilToonPresets.LanguageKeys(Lang.En))
                {
                    if (NamesByLanguage.TryGetValue(language, out var name) && !string.IsNullOrEmpty(name)) return name;
                }
                foreach (var name in NamesByLanguage.Values)
                {
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                return FileName;
            }
        }

        public string CategoryName => LilToonPresets.CategoryName(Category);

        /// <summary>"Cloth › Illust"</summary>
        public string Label => CategoryName + " › " + DisplayName;
    }

    /// <summary>Finds and reads lilToon presets. Results are cached until the project changes.</summary>
    internal static class LilToonPresets
    {
        public const string TypeName = "lilToonPreset";

        /// <summary>GUID of lilToon's "Presets" folder (lilDirectoryManager.GetPresetsFolderPath).</summary>
        const string BuiltInFolderGuid = "35817d21af2f3134182c4a7e4c07786b";

        // lilPresetCategory: Skin, Hair, Cloth, Nature, Inorganic, Effect, Other. Names from lilToon's translation files.
        static readonly string[][] s_CategoryNames =
        {
            new[] { "피부", "Skin", "肌" },
            new[] { "머리", "Hair", "髪" },
            new[] { "천", "Cloth", "布" },
            new[] { "자연물", "Nature", "自然物" },
            new[] { "무기물", "Inorganic", "無機物" },
            new[] { "효과", "Effect", "エフェクト" },
            new[] { "기타", "Other", "その他" },
        };

        public static int CategoryCount => s_CategoryNames.Length;

        static List<PresetInfo> s_Presets;

        public static string CategoryName(int category)
        {
            if (category < 0 || category >= s_CategoryNames.Length) category = s_CategoryNames.Length - 1;
            int language = (int)L.Current;
            var names = s_CategoryNames[category];
            return language >= 0 && language < names.Length ? names[language] : names[1];
        }

        /// <summary>Language identifiers a preset may use for a UI language (lilToon has used both styles).</summary>
        public static IEnumerable<string> LanguageKeys(Lang language)
        {
            switch (language)
            {
                case Lang.Ko: return new[] { "ko-KR", "Korean", "한국어" };
                case Lang.Ja: return new[] { "ja-JP", "Japanese", "日本語" };
                default: return new[] { "en-US", "English" };
            }
        }

        public static void ClearCache()
        {
            s_Presets = null;
        }

        /// <summary>All presets in the project, sorted by category, then name. Empty when lilToon is not installed.</summary>
        public static IReadOnlyList<PresetInfo> All
        {
            get
            {
                if (s_Presets == null) s_Presets = Load();
                return s_Presets;
            }
        }

        public static PresetInfo FindByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var preset in All)
            {
                if (preset.Guid == guid) return preset;
            }
            return null;
        }

        /// <summary>Reads a preset again from its asset (used right before applying, so later edits are included).</summary>
        public static PresetInfo Reload(PresetInfo preset)
        {
            if (preset == null) return null;
            var fresh = Read(preset.Path);
            return fresh ?? preset;
        }

        static List<PresetInfo> Load()
        {
            var result = new List<PresetInfo>();
            string[] guids;
            try
            {
                guids = AssetDatabase.FindAssets("t:" + TypeName);
            }
            catch (Exception)
            {
                return result;
            }

            foreach (string guid in guids)
            {
                var preset = Read(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null) result.Add(preset);
            }

            result.Sort((a, b) =>
            {
                int byCategory = a.Category.CompareTo(b.Category);
                if (byCategory != 0) return byCategory;
                int byOrigin = b.IsBuiltIn.CompareTo(a.IsBuiltIn); // lilToon's own presets first
                if (byOrigin != 0) return byOrigin;
                return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        static PresetInfo Read(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null || asset.GetType().Name != TypeName) return null;

            var preset = new PresetInfo
            {
                Guid = AssetDatabase.AssetPathToGUID(path),
                Path = path,
                FileName = System.IO.Path.GetFileNameWithoutExtension(path),
            };

            string builtInFolder = AssetDatabase.GUIDToAssetPath(BuiltInFolderGuid);
            preset.IsBuiltIn = !string.IsNullOrEmpty(builtInFolder) && path.StartsWith(builtInFolder + "/", StringComparison.Ordinal);

            using (var serialized = new SerializedObject(asset))
            {
                var category = serialized.FindProperty("category");
                preset.Category = category != null ? Mathf.Clamp(category.intValue, 0, s_CategoryNames.Length - 1) : s_CategoryNames.Length - 1;

                var bases = serialized.FindProperty("bases");
                if (bases != null && bases.isArray)
                {
                    for (int i = 0; i < bases.arraySize; i++)
                    {
                        var element = bases.GetArrayElementAtIndex(i);
                        var language = element.FindPropertyRelative("language");
                        var name = element.FindPropertyRelative("name");
                        if (name == null) continue;
                        string key = language != null ? language.stringValue ?? string.Empty : string.Empty;
                        if (!preset.NamesByLanguage.ContainsKey(key)) preset.NamesByLanguage.Add(key, name.stringValue);
                    }
                }

                ReadValues(serialized.FindProperty("floats"), 0, preset);
                ReadValues(serialized.FindProperty("colors"), 1, preset);
                ReadValues(serialized.FindProperty("vectors"), 2, preset);
                // "textures", the shader, rendering mode, render queue and the outline flag are never used.
            }
            return preset;
        }

        static void ReadValues(SerializedProperty array, int category, PresetInfo preset)
        {
            if (array == null || !array.isArray) return;
            for (int i = 0; i < array.arraySize; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                var name = element.FindPropertyRelative("name");
                var value = element.FindPropertyRelative("value");
                if (name == null || value == null || string.IsNullOrEmpty(name.stringValue)) continue;

                var entry = new PresetValue { Category = category };
                switch (category)
                {
                    case 0:
                        if (value.propertyType != SerializedPropertyType.Float) continue;
                        entry.Float = value.floatValue;
                        break;
                    case 1:
                        if (value.propertyType != SerializedPropertyType.Color) continue;
                        entry.Color = value.colorValue;
                        break;
                    default:
                        if (value.propertyType != SerializedPropertyType.Vector4) continue;
                        entry.Vector = value.vector4Value;
                        break;
                }
                preset.Values[name.stringValue] = entry; // a later entry wins, as when lilToon applies the preset
            }
        }
    }
}
