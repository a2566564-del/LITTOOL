using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Littool.MaterialBatch
{
    internal sealed class ApplyReport
    {
        public int Applied;
        public int Failed;
        public bool Exception;
    }

    /// <summary>
    /// Writes the reference values (from a material or a preset) to the target materials.
    /// Everything goes into one Undo group, and modified .mat files are written to disk right away
    /// (and again after Undo/Redo) so the files never disagree with what the editor shows.
    /// </summary>
    internal static class MaterialApplier
    {
        const string UndoName = "LITTOOL Material Batch";
        const int ProgressThreshold = 20;

        // Instance ids of every material LITTOOL changed in this editor session.
        // Kept in SessionState so the list survives script reloads (the Undo history survives them too).
        const string AppliedKey = "LITTOOL.MaterialBatch.Applied";
        const int MaxRemembered = 4096;

        [InitializeOnLoadMethod]
        static void RegisterUndoHook()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        /// <summary>After Undo/Redo, write the reverted materials to disk so the files match the editor.</summary>
        static void OnUndoRedo()
        {
            int[] ids = SessionState.GetIntArray(AppliedKey, new int[0]);
            if (ids == null || ids.Length == 0) return;
            var dirty = new List<Material>();
            foreach (int id in ids)
            {
                var material = EditorUtility.InstanceIDToObject(id) as Material;
                if (material != null && EditorUtility.IsPersistent(material) && EditorUtility.IsDirty(material)) dirty.Add(material);
            }
            if (dirty.Count > 0) SaveMaterials(dirty);
        }

        static void RememberApplied(List<Material> materials)
        {
            var ids = new List<int>(SessionState.GetIntArray(AppliedKey, new int[0]) ?? new int[0]);
            foreach (var material in materials)
            {
                if (material == null) continue;
                int id = material.GetInstanceID();
                ids.Remove(id);
                ids.Add(id);
            }
            if (ids.Count > MaxRemembered) ids.RemoveRange(0, ids.Count - MaxRemembered);
            SessionState.SetIntArray(AppliedKey, ids.ToArray());
        }

        /// <param name="values">Values read from the reference right before applying.</param>
        /// <param name="exclude">The reference material (never modified), or null for a preset.</param>
        public static ApplyReport Apply(IList<SourceValue> values, IList<MaterialInfo> targets, Material exclude)
        {
            var report = new ApplyReport();
            if (values == null || values.Count == 0 || targets == null) return report;

            var materials = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (var info in targets)
            {
                var material = info != null ? info.Material : null;
                if (material == null || (exclude != null && material == exclude) || !seen.Add(material)) continue;
                materials.Add(material);
            }
            if (materials.Count == 0) return report;

            // Version control: try to check out files that are not open for edit.
            var checkoutPaths = materials
                .Where(m => EditorUtility.IsPersistent(m) && !AssetDatabase.IsOpenForEdit(m))
                .Select(m => AssetDatabase.GetAssetPath(m))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToArray();
            if (checkoutPaths.Length > 0)
            {
                var notEditable = new List<string>();
                AssetDatabase.MakeEditable(checkoutPaths, null, notEditable);
                if (notEditable.Count > 0)
                {
                    var blocked = new HashSet<string>(notEditable);
                    int before = materials.Count;
                    materials.RemoveAll(m => blocked.Contains(AssetDatabase.GetAssetPath(m)));
                    report.Failed += before - materials.Count;
                    Debug.LogWarning("[LITTOOL] Could not make these files editable, they were skipped:\n" + string.Join("\n", notEditable));
                }
            }
            if (materials.Count == 0) return report;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            Undo.RecordObjects(materials.ToArray(), UndoName);

            bool showProgress = materials.Count >= ProgressThreshold;
            bool sceneMaterialChanged = false;
            try
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    var material = materials[i];
                    if (showProgress)
                    {
                        EditorUtility.DisplayProgressBar(
                            L.T("apply.progressTitle"),
                            L.F("apply.progress", i + 1, materials.Count),
                            (float)(i + 1) / materials.Count);
                    }

                    int copied = CopyValues(values, material);
                    if (copied <= 0) continue;

                    if (LilToonUtil.IsMulti(material)) LilToonUtil.TrySetupMultiMaterial(material);

                    EditorUtility.SetDirty(material);
                    if (!EditorUtility.IsPersistent(material)) sceneMaterialChanged = true;
                    report.Applied++;
                }
            }
            catch (Exception e)
            {
                report.Exception = true;
                Debug.LogException(e);
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(group);
            }

            // Save every touched file, including one that was half-written when an exception happened,
            // so the files on disk always match the editor (and Undo can restore them).
            SaveMaterials(materials.Where(m => m != null && EditorUtility.IsDirty(m)).ToList());
            if (sceneMaterialChanged && !EditorApplication.isPlayingOrWillChangePlaymode) EditorSceneManager.MarkAllScenesDirty();

            RememberApplied(materials);
            return report;
        }

        /// <summary>Writes the values the target supports. Textures are never touched. Returns how many values were written.</summary>
        static int CopyValues(IList<SourceValue> values, Material target)
        {
            var targetShader = target.shader;
            if (targetShader == null) return 0;
            bool isVariant = target.isVariant;
            int copied = 0;

            foreach (var value in values)
            {
                var property = value.Property;
                if (!MaterialScanner.IsCompatible(targetShader, property)) continue;
                if (isVariant && target.IsPropertyLockedByAncestor(property.Name)) continue;

                switch (MaterialScanner.CategoryOf(property.Type))
                {
                    case 0:
                        target.SetFloat(property.Id, value.Float);
                        break;
                    case 1:
                        target.SetColor(property.Id, value.Color);
                        break;
                    case 2:
                        target.SetVector(property.Id, value.Vector);
                        break;
                    case 3:
                        target.SetInteger(property.Id, value.Int);
                        break;
                    default:
                        continue;
                }
                copied++;
            }
            return copied;
        }

        static void SaveMaterials(IList<Material> materials)
        {
            if (materials == null || materials.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var material in materials)
                {
                    if (material != null && EditorUtility.IsPersistent(material)) AssetDatabase.SaveAssetIfDirty(material);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
