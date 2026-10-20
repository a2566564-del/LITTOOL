using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Littool.MaterialBatch
{
    /// <summary>
    /// "Copy without textures": makes "&lt;reference name&gt;_Ref.mat" next to the reference material,
    /// with every texture slot emptied and every value kept. The original material is never changed.
    /// </summary>
    internal static class ReferenceCopier
    {
        public const string Suffix = "_Ref";

        /// <summary>The reference must be a material asset with a working shader.</summary>
        public static bool CanCopy(Material source)
        {
            return source != null && EditorUtility.IsPersistent(source) && !LilToonUtil.IsShaderMissing(source);
        }

        /// <summary>Texture slots of the material that hold a texture.</summary>
        public static int CountTextures(Material material)
        {
            if (material == null || material.shader == null) return 0;
            var shader = material.shader;
            int count = 0;
            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                if (material.GetTexture(shader.GetPropertyNameId(i)) != null) count++;
            }
            return count;
        }

        /// <summary>"Body" → "Body_Ref" (characters that cannot be in a file name become "_").</summary>
        public static string FileNameFor(Material source)
        {
            string name = source != null ? source.name : string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\') chars[i] = '_';
            }
            string clean = new string(chars).Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(clean)) clean = "Material";
            return clean + Suffix;
        }

        /// <summary>
        /// "&lt;folder of the reference&gt;/&lt;name&gt;_Ref.mat", numbered ("Body_Ref 1.mat") when the name is taken.
        /// Null when that folder cannot be written to (a read-only package); the caller then asks where to save.
        /// </summary>
        public static string SuggestPath(Material source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;
            string folder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(folder)) return null;
            folder = folder.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder) || MaterialScanner.IsImmutablePackagePath(folder)) return null;
            return AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileNameFor(source) + ".mat");
        }

        /// <summary>Creates the copy at <paramref name="path"/> (a project path ending in .mat) and returns the new asset.</summary>
        public static Material CreateCopy(Material source, string path, out int cleared)
        {
            cleared = 0;
            if (!CanCopy(source)) throw new ArgumentException("The reference material cannot be copied.", nameof(source));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("No path for the copy.", nameof(path));
            // CreateAsset replaces whatever is at the path: never let that be the original.
            if (string.Equals(path, AssetDatabase.GetAssetPath(source), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The copy cannot replace the original material.", nameof(path));

            // A fresh material (never a Material Variant), then every value, keyword and render queue of the source.
            var copy = new Material(source.shader);
            copy.CopyPropertiesFromMaterial(source);
            copy.enableInstancing = source.enableInstancing;
            copy.doubleSidedGI = source.doubleSidedGI;
            copy.name = Path.GetFileNameWithoutExtension(path);

            var shader = copy.shader;
            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                int id = shader.GetPropertyNameId(i);
                if (copy.GetTexture(id) != null) cleared++;
                copy.SetTexture(id, null);
            }

            if (LilToonUtil.IsMulti(copy)) LilToonUtil.TrySetupMultiMaterial(copy);

            try
            {
                AssetDatabase.CreateAsset(copy, path);
            }
            catch
            {
                // Not saved: do not leave an orphan material in memory.
                UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
            if (!EditorUtility.IsPersistent(copy))
            {
                // CreateAsset can fail with only a Console error (e.g. a read-only folder).
                UnityEngine.Object.DestroyImmediate(copy);
                throw new InvalidOperationException("The copy could not be saved at " + path + ".");
            }
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }
    }
}
