using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// Modular Avatar access through reflection only, so LITTOOL compiles and runs without MA installed
    /// (the Bone Scale tab then changes the avatar's own bones only).
    /// </summary>
    internal static class MaBridge
    {
        const string MergeArmatureName = "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature";
        const string FloorAdjusterName = "nadena.dev.modular_avatar.core.ModularAvatarFloorAdjuster";

        // ArmatureLockMode: Legacy = 0, NotLocked = 1, BaseToMerge = 2, BidirectionalExact = 3.
        const int LockLegacy = 0;
        const int LockNone = 1;

        static bool s_Searched;
        static Type s_MergeArmature;
        static Type s_FloorAdjuster;
        static FieldInfo s_LockMode;
        static FieldInfo s_LegacyLocked;
        static FieldInfo s_Prefix;
        static FieldInfo s_Suffix;
        static PropertyInfo s_MergeTargetObject;
        static MethodInfo s_GetBonesMapping;
        static string s_Version;
        static bool s_MappingFailed;

        public static bool Installed
        {
            get
            {
                Search();
                return s_MergeArmature != null;
            }
        }

        public static Type MergeArmatureType
        {
            get
            {
                Search();
                return s_MergeArmature;
            }
        }

        /// <summary>MA Floor Adjuster, added in MA 1.17.0. Null when MA is missing or older.</summary>
        public static Type FloorAdjusterType
        {
            get
            {
                Search();
                return s_FloorAdjuster;
            }
        }

        /// <summary>Installed MA version ("1.16.2"), or null when unknown.</summary>
        public static string Version
        {
            get
            {
                Search();
                return s_Version;
            }
        }

        /// <summary>Look the types up again (packages may have been added or removed).</summary>
        public static void Reset()
        {
            s_Searched = false;
            s_MergeArmature = null;
            s_FloorAdjuster = null;
            s_LockMode = null;
            s_LegacyLocked = null;
            s_Prefix = null;
            s_Suffix = null;
            s_MergeTargetObject = null;
            s_GetBonesMapping = null;
            s_Version = null;
        }

        static void Search()
        {
            if (s_Searched) return;
            s_Searched = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (s_MergeArmature == null) s_MergeArmature = assembly.GetType(MergeArmatureName, false);
                    if (s_FloorAdjuster == null) s_FloorAdjuster = assembly.GetType(FloorAdjusterName, false);
                }
                catch (Exception)
                {
                    // Dynamic or broken assemblies: skip.
                }
                if (s_MergeArmature != null && s_FloorAdjuster != null) break;
            }
            if (s_MergeArmature == null) return;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            s_LockMode = s_MergeArmature.GetField("LockMode", flags);
            s_LegacyLocked = s_MergeArmature.GetField("legacyLocked", flags) ?? s_MergeArmature.GetField("locked", flags);
            s_Prefix = s_MergeArmature.GetField("prefix", flags);
            s_Suffix = s_MergeArmature.GetField("suffix", flags);
            s_MergeTargetObject = s_MergeArmature.GetProperty("mergeTargetObject", flags);
            s_GetBonesMapping = s_MergeArmature.GetMethod("GetBonesMapping", flags, null, Type.EmptyTypes, null);

            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(s_MergeArmature.Assembly);
                if (package != null) s_Version = package.version;
            }
            catch (Exception)
            {
                s_Version = null;
            }
        }

        /// <summary>The object the outfit armature merges into (usually the avatar's Armature).</summary>
        public static GameObject GetMergeTarget(Component mergeArmature)
        {
            if (mergeArmature == null || s_MergeTargetObject == null) return null;
            try
            {
                return s_MergeTargetObject.GetValue(mergeArmature, null) as GameObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether MA keeps the outfit bones following the avatar bones in the editor (BaseToMerge or BidirectionalExact).
        /// MA turns the old "Legacy" mode into one of those two when the component is enabled.
        /// </summary>
        public static bool IsLocked(Component mergeArmature)
        {
            if (mergeArmature == null) return false;
            try
            {
                if (s_LockMode != null)
                {
                    int mode = Convert.ToInt32(s_LockMode.GetValue(mergeArmature));
                    if (mode == LockLegacy) return true;
                    return mode != LockNone;
                }
                if (s_LegacyLocked != null) return (bool)s_LegacyLocked.GetValue(mergeArmature);
            }
            catch (Exception)
            {
                // Unknown layout: treat as not locked, so values are written to the outfit like a plain armature.
            }
            return false;
        }

        /// <summary>
        /// (avatar side bone, outfit bone) pairs, as MA itself matches them (ModularAvatarMergeArmature.GetBonesMapping).
        /// Falls back to MA's name rule (prefix/suffix) on versions without that method.
        /// </summary>
        public static List<(Transform baseBone, Transform mergeBone)> GetMapping(Component mergeArmature)
        {
            var result = new List<(Transform, Transform)>();
            if (mergeArmature == null) return result;

            if (s_GetBonesMapping != null && !s_MappingFailed)
            {
                try
                {
                    if (s_GetBonesMapping.Invoke(mergeArmature, null) is IEnumerable pairs)
                    {
                        foreach (object item in pairs)
                        {
                            if (item is ValueTuple<Transform, Transform> pair && pair.Item1 != null && pair.Item2 != null)
                                result.Add((pair.Item1, pair.Item2));
                        }
                    }
                    return result;
                }
                catch (Exception e)
                {
                    s_MappingFailed = true;
                    Debug.LogWarning("[LITTOOL] Modular Avatar bone mapping failed; using name matching instead.\n" + e);
                    result.Clear();
                }
            }

            var target = GetMergeTarget(mergeArmature);
            if (target == null) return result;
            string prefix = GetString(s_Prefix, mergeArmature);
            string suffix = GetString(s_Suffix, mergeArmature);
            ScanByName(mergeArmature, mergeArmature.transform, target.transform, prefix, suffix, result, 0);
            return result;
        }

        static string GetString(FieldInfo field, object target)
        {
            if (field == null) return string.Empty;
            try
            {
                return field.GetValue(target) as string ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        static void ScanByName(Component self, Transform merge, Transform baseBone, string prefix, string suffix,
            List<(Transform, Transform)> result, int depth)
        {
            if (depth > 64) return;
            foreach (Transform child in merge)
            {
                // A nested Merge Armature maps its own bones.
                var nested = child.GetComponent(s_MergeArmature);
                if (nested != null && nested != self) continue;

                string name = child.gameObject.name;
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)
                                                                         || name.Length <= prefix.Length + suffix.Length) continue;
                string baseName = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
                var baseChild = baseBone.Find(baseName);
                if (baseChild == null) continue;
                result.Add((baseChild, child));
                ScanByName(self, child, baseChild, prefix, suffix, result, depth + 1);
            }
        }
    }
}
