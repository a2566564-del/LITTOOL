using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Littool
{
    /// <summary>
    /// The package's own images (white shapes tinted in code), found by GUID and, if that fails (e.g. the folder was
    /// re-imported with new GUIDs), by file name.
    /// </summary>
    internal static class LittoolImages
    {
        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> s_Missing = new HashSet<string>();

        /// <summary>Front view of a body for the Bone Scale tab (166×298 points, 2x resolution).</summary>
        public static Texture2D Silhouette => Load(LittoolInfo.SilhouetteGuid, LittoolInfo.SilhouetteName);

        /// <summary>Globe icon next to the Language selector (16×16 points, 2x resolution).</summary>
        public static Texture2D Globe => Load(LittoolInfo.GlobeGuid, LittoolInfo.GlobeName);

        public static Texture2D Load(string guid, string name)
        {
            if (s_Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            if (s_Missing.Contains(name)) return null;

            Texture2D texture = null;
            string path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path)) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                foreach (string found in AssetDatabase.FindAssets(name + " t:Texture2D"))
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(found);
                    if (Path.GetFileNameWithoutExtension(foundPath) != name) continue;
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(foundPath);
                    if (texture != null) break;
                }
            }

            if (texture != null) s_Cache[name] = texture;
            else s_Missing.Add(name);
            return texture;
        }

        /// <summary>Forget what was found or missing (the project changed).</summary>
        public static void ClearCache()
        {
            s_Cache.Clear();
            s_Missing.Clear();
        }
    }
}
