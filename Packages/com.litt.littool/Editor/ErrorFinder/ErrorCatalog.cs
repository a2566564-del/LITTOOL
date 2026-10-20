using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// Loads Editor/ErrorFinder/ErrorCatalog.json (shipped with LITTOOL; never downloaded) and builds the matcher once.
    /// </summary>
    internal static class ErrorCatalog
    {
        const string FileName = "ErrorCatalog";

        static ErrorMatcher s_Matcher;
        static bool s_Tried;
        static string s_Error;

        public static ErrorMatcher Matcher
        {
            get
            {
                Load();
                return s_Matcher;
            }
        }

        public static ErrorCatalogData Data => Matcher?.Data;

        /// <summary>Why the list could not be loaded (null when it was).</summary>
        public static string LoadError
        {
            get
            {
                Load();
                return s_Error;
            }
        }

        public static int RowCount => Data?.rows?.Length ?? 0;
        public static string Version => Data?.version ?? string.Empty;

        static void Load()
        {
            if (s_Tried) return;
            s_Tried = true;
            try
            {
                var asset = FindAsset();
                if (asset == null)
                {
                    s_Error = "ErrorCatalog.json";
                    return;
                }
                var data = JsonUtility.FromJson<ErrorCatalogData>(asset.text);
                if (data?.rows == null || data.rows.Length == 0)
                {
                    s_Error = "ErrorCatalog.json (empty)";
                    return;
                }
                s_Matcher = new ErrorMatcher(data);
                s_Error = null;
            }
            catch (Exception e)
            {
                s_Error = e.GetType().Name + ": " + e.Message;
                s_Matcher = null;
            }
        }

        static TextAsset FindAsset()
        {
            string path = AssetDatabase.GUIDToAssetPath(LittoolInfo.ErrorCatalogGuid);
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) return asset;
            foreach (var guid in AssetDatabase.FindAssets(FileName + " t:TextAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                // ErrorCatalog.cs is a TextAsset too (MonoScript): only the .json is the list
                if (Path.GetFileNameWithoutExtension(p) == FileName &&
                    string.Equals(Path.GetExtension(p), ".json", StringComparison.OrdinalIgnoreCase) &&
                    p.Replace('\\', '/').Contains("/ErrorFinder/"))
                {
                    asset = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
                    if (asset != null) return asset;
                }
            }
            return null;
        }

        /// <summary>Forget the loaded list (after LITTOOL was updated).</summary>
        public static void Reload()
        {
            s_Tried = false;
            s_Matcher = null;
            s_Error = null;
        }
    }
}
