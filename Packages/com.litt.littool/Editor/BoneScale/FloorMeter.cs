using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// Measures how far the lowest visible point of the avatar (the shoe soles of the outfits that are on) sits below
    /// the avatar root, and places an MA Floor Adjuster at that height.
    /// </summary>
    internal static class FloorMeter
    {
        public const string FloorObjectName = "LITTOOL Floor";
        public const string UndoName = "LITTOOL Floor Adjuster";

        /// <summary>Renderers whose bounds come this close (metres) to the lowest bounds are measured vertex by vertex.</summary>
        const float CandidateRange = 0.2f;

        public sealed class Result
        {
            public bool Valid;
            public float LowestHeight; // metres along the avatar root's up axis; negative = below the root
            public Renderer Lowest;
        }

        static Mesh s_Baked;
        static readonly List<Vector3> s_Vertices = new List<Vector3>();

        public static void Dispose()
        {
            if (s_Baked != null) UnityEngine.Object.DestroyImmediate(s_Baked);
            s_Baked = null;
        }

        public static float HeightOf(Transform root, Vector3 world)
        {
            return Vector3.Dot(world - root.position, root.up);
        }

        public static Result Measure(GameObject avatar)
        {
            var result = new Result();
            if (avatar == null) return result;
            var root = avatar.transform;

            // Active, enabled mesh renderers only; EditorOnly objects never reach the uploaded avatar.
            var candidates = new List<(Renderer renderer, float low)>();
            float lowestBounds = float.MaxValue;
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (MeshOf(renderer) == null || IsEditorOnly(renderer.transform, root)) continue;
                float low = BoundsLow(renderer.bounds, root);
                candidates.Add((renderer, low));
                lowestBounds = Mathf.Min(lowestBounds, low);
            }
            if (candidates.Count == 0) return result;

            float lowest = float.MaxValue;
            foreach (var (renderer, low) in candidates)
            {
                if (low > lowestBounds + CandidateRange) continue;
                float height;
                try
                {
                    height = LowestVertex(renderer, root);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LITTOOL] Could not measure " + renderer.name + ": " + e.Message);
                    continue;
                }
                if (height < lowest)
                {
                    lowest = height;
                    result.Lowest = renderer;
                }
            }
            if (result.Lowest == null) return result;
            result.Valid = true;
            result.LowestHeight = lowest;
            return result;
        }

        static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        static bool IsEditorOnly(Transform t, Transform root)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (current.CompareTag("EditorOnly")) return true;
                if (current == root) break;
            }
            return false;
        }

        static float BoundsLow(Bounds bounds, Transform root)
        {
            var min = bounds.min;
            var max = bounds.max;
            float low = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                low = Mathf.Min(low, HeightOf(root, corner));
            }
            return low;
        }

        static float LowestVertex(Renderer renderer, Transform root)
        {
            var t = renderer.transform;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                if (s_Baked == null) s_Baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                skinned.BakeMesh(s_Baked);
                s_Baked.GetVertices(s_Vertices);

                // BakeMesh(mesh) leaves out the renderer's own scale. When that scale is not 1, check which reading
                // matches the renderer's bounds, so an unusual setup still measures correctly.
                var matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                if (!NearlyOne(t.lossyScale))
                {
                    var full = t.localToWorldMatrix;
                    if (BoundsError(s_Vertices, full, renderer.bounds) < BoundsError(s_Vertices, matrix, renderer.bounds)) matrix = full;
                }
                return LowestOf(s_Vertices, matrix, root);
            }

            var mesh = MeshOf(renderer);
            mesh.GetVertices(s_Vertices);
            return LowestOf(s_Vertices, t.localToWorldMatrix, root);
        }

        static bool NearlyOne(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) < 0.001f && Mathf.Abs(scale.y - 1f) < 0.001f && Mathf.Abs(scale.z - 1f) < 0.001f;
        }

        static float BoundsError(List<Vector3> vertices, Matrix4x4 matrix, Bounds expected)
        {
            if (vertices.Count == 0) return float.MaxValue;
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            foreach (var v in vertices)
            {
                var w = matrix.MultiplyPoint3x4(v);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            return ((max - min) - expected.size).sqrMagnitude;
        }

        static float LowestOf(List<Vector3> vertices, Matrix4x4 matrix, Transform root)
        {
            float lowest = float.MaxValue;
            foreach (var v in vertices) lowest = Mathf.Min(lowest, HeightOf(root, matrix.MultiplyPoint3x4(v)));
            return lowest;
        }

        // ---- MA Floor Adjuster -------------------------------------------------------------------------------

        /// <summary>Floor Adjusters under the avatar (never on the avatar root itself), active ones first.</summary>
        public static List<Component> FindAdjusters(GameObject avatar, out int activeCount)
        {
            activeCount = 0;
            var list = new List<Component>();
            var type = MaBridge.FloorAdjusterType;
            if (avatar == null || type == null) return list;
            foreach (var component in avatar.GetComponentsInChildren(type, true))
            {
                if (component == null || component.gameObject == avatar) continue;
                list.Add(component);
            }
            list.Sort((a, b) => IsActive(b).CompareTo(IsActive(a)));
            foreach (var component in list)
            {
                if (IsActive(component)) activeCount++;
            }
            return list;
        }

        static bool IsActive(Component component)
        {
            return component is Behaviour behaviour ? behaviour.isActiveAndEnabled : component.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Puts the floor at <paramref name="height"/>: moves <paramref name="existing"/> along the avatar's up axis,
        /// or creates "LITTOOL Floor" under the avatar root with an MA Floor Adjuster. One Undo step.
        /// </summary>
        public static Component Place(GameObject avatar, Component existing, float height)
        {
            var root = avatar.transform;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            try
            {
                if (existing != null)
                {
                    var t = existing.transform;
                    Undo.RecordObject(t, UndoName);
                    t.position += root.up * (height - HeightOf(root, t.position));
                    return existing;
                }

                var go = new GameObject(FloorObjectName);
                go.transform.SetParent(root, false);
                go.transform.localPosition = root.InverseTransformPoint(root.position + root.up * height);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                return Undo.AddComponent(go, MaBridge.FloorAdjusterType);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }
    }
}
