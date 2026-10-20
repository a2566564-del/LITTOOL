using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Littool.MaterialBatch
{
    /// <summary>
    /// lilToon helpers. LITTOOL never references lilToon's assemblies at compile time,
    /// so it keeps compiling with any lilToon version (or none installed).
    /// </summary>
    internal static class LilToonUtil
    {
        /// <summary>Every material-facing lilToon shader (normal, Lite, Multi, Fur, Gem, Outline, Tessellation...) declares this.</summary>
        public const string VersionProperty = "_lilToonVersion";

        const string ErrorShaderName = "Hidden/InternalErrorShader";

        static MethodInfo s_SetupMultiMaterial;
        static bool s_SetupSearched;
        static bool s_SetupFailedLogged;

        public static bool IsInstalled()
        {
            return Shader.Find("lilToon") != null;
        }

        /// <summary>True when the shader asset is missing or failed to compile (the pink error shader).</summary>
        public static bool IsShaderMissing(Material material)
        {
            if (material == null) return true;
            var shader = material.shader;
            return shader == null || shader.name == ErrorShaderName;
        }

        /// <summary>
        /// Same rule as lilToon itself, plus the version property:
        /// a lilToon material has _lilToonVersion, "lilToon" in its shader name, or a .lilcontainer shader.
        /// </summary>
        public static bool IsLilToon(Material material)
        {
            if (IsShaderMissing(material)) return false;
            var shader = material.shader;
            if (shader.FindPropertyIndex(VersionProperty) >= 0) return true;
            if (shader.name.IndexOf("lilToon", StringComparison.Ordinal) >= 0) return true;
            string path = AssetDatabase.GetAssetPath(shader);
            return !string.IsNullOrEmpty(path) && path.IndexOf(".lilcontainer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Same rule as lilShaderUtils.IsMultiShaderName: "Multi" after the last '/' of the shader name.</summary>
        public static bool IsMulti(Material material)
        {
            if (IsShaderMissing(material)) return false;
            string name = material.shader.name;
            int separator = name.LastIndexOf('/');
            if (separator < 0 || separator + 1 >= name.Length) return false;
            return name.IndexOf("Multi", separator + 1, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// lilToon Multi drives its features with shader keywords, so after values change
        /// lilToon.lilMaterialUtils.SetupMultiMaterial(Material) must run. lilToon also does this itself
        /// when the .mat file is re-imported; calling it here keeps the result correct even before saving.
        /// </summary>
        public static bool TrySetupMultiMaterial(Material material)
        {
            if (material == null) return false;
            var method = FindSetupMultiMaterial();
            if (method == null) return false;
            try
            {
                method.Invoke(null, new object[] { material });
                return true;
            }
            catch (Exception e)
            {
                if (!s_SetupFailedLogged)
                {
                    s_SetupFailedLogged = true;
                    Debug.LogWarning("[LITTOOL] lilToon SetupMultiMaterial failed. lilToon will update the material when it is re-imported.\n" + e);
                }
                return false;
            }
        }

        static MethodInfo FindSetupMultiMaterial()
        {
            if (s_SetupSearched) return s_SetupMultiMaterial;
            s_SetupSearched = true;
            try
            {
                var type = Type.GetType("lilToon.lilMaterialUtils, lilToon.Editor", false);
                if (type == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType("lilToon.lilMaterialUtils", false);
                        if (type != null) break;
                    }
                }
                if (type != null)
                {
                    s_SetupMultiMaterial = type.GetMethod(
                        "SetupMultiMaterial",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                        null,
                        new[] { typeof(Material) },
                        null);
                }
            }
            catch (Exception)
            {
                s_SetupMultiMaterial = null;
            }
            return s_SetupMultiMaterial;
        }
    }
}
