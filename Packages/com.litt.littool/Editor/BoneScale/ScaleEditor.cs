using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// Writes bone scales. An avatar bone gets the new value; every outfit bone that follows it without an MA lock gets
    /// the same ratio, axis by axis (locked outfit bones are left to MA). Undo grouping is done by the caller.
    /// </summary>
    internal static class ScaleEditor
    {
        public const string UndoName = "LITTOOL Bone Scale";
        public const string MatchUndoName = "LITTOOL Match Outfit";
        public const float MinScale = 0.01f;
        public const float MaxScale = 100f;
        const float Epsilon = 0.0005f;

        public struct Edit
        {
            public Transform Bone;
            public Vector3 Value;

            public Edit(Transform bone, Vector3 value)
            {
                Bone = bone;
                Value = value;
            }
        }

        // Values at the start of a slider drag, so the outfit ratio is taken from the start of the gesture
        // instead of being multiplied frame after frame.
        static Dictionary<Transform, Vector3> s_DragStart;

        public static bool Dragging => s_DragStart != null;

        public static void BeginDrag()
        {
            if (s_DragStart == null) s_DragStart = new Dictionary<Transform, Vector3>();
        }

        public static void EndDrag()
        {
            s_DragStart = null;
        }

        static Vector3 StartValue(Transform t)
        {
            if (s_DragStart == null) return t.localScale;
            if (!s_DragStart.TryGetValue(t, out var value))
            {
                value = t.localScale;
                s_DragStart[t] = value;
            }
            return value;
        }

        public static Vector3 Clamp(Vector3 value)
        {
            return new Vector3(ClampValue(value.x), ClampValue(value.y), ClampValue(value.z));
        }

        public static float ClampValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Mathf.Clamp(value, MinScale, MaxScale);
        }

        /// <summary>Applies the edits; returns how many outfit bones were written.</summary>
        public static int Apply(IList<Edit> edits, OutfitSet outfits)
        {
            var targets = new List<Object>();
            var seen = new HashSet<Transform>();
            foreach (var edit in edits)
            {
                if (edit.Bone == null || !seen.Add(edit.Bone)) continue;
                targets.Add(edit.Bone);
                if (outfits == null) continue;
                foreach (var pair in outfits.PairsFor(edit.Bone))
                {
                    if (!pair.Locked && pair.Merge != null && seen.Add(pair.Merge)) targets.Add(pair.Merge);
                }
            }
            if (targets.Count == 0) return 0;
            Undo.RecordObjects(targets.ToArray(), UndoName);

            int outfitBones = 0;
            var done = new HashSet<Transform>();
            foreach (var edit in edits)
            {
                if (edit.Bone == null || !done.Add(edit.Bone)) continue;
                var start = StartValue(edit.Bone);
                var value = Clamp(edit.Value);
                edit.Bone.localScale = value;
                if (outfits == null) continue;

                var ratio = new Vector3(Ratio(value.x, start.x), Ratio(value.y, start.y), Ratio(value.z, start.z));
                foreach (var pair in outfits.PairsFor(edit.Bone))
                {
                    if (pair.Locked || pair.Merge == null || !done.Add(pair.Merge)) continue;
                    pair.Merge.localScale = Vector3.Scale(StartValue(pair.Merge), ratio);
                    outfitBones++;
                }
            }
            return outfitBones;
        }

        static float Ratio(float value, float start)
        {
            return Mathf.Abs(start) < 1e-6f ? 1f : value / start;
        }

        public static bool Differs(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) > Epsilon || Mathf.Abs(a.y - b.y) > Epsilon || Mathf.Abs(a.z - b.z) > Epsilon;
        }

        /// <summary>Whether an outfit bone's scale differs from the bone it follows.</summary>
        public static bool Mismatched(BonePair pair)
        {
            return pair.Base != null && pair.Merge != null && Differs(pair.Merge.localScale, pair.Base.localScale);
        }

        /// <summary>
        /// Copies the followed bone's scale to each outfit bone that differs, outer bones first so nested outfit
        /// bones compare against already matched ones. Locked bones are written too: MA then keeps the new offset.
        /// Returns how many avatar bones were matched.
        /// </summary>
        public static int Match(IEnumerable<BonePair> pairs)
        {
            var list = new List<BonePair>(pairs);
            list.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            var matchedBones = new HashSet<Transform>();
            foreach (var pair in list)
            {
                if (!Mismatched(pair)) continue;
                Undo.RecordObject(pair.Merge, MatchUndoName);
                pair.Merge.localScale = pair.Base.localScale;
                matchedBones.Add(pair.AvatarBone);
            }
            return matchedBones.Count;
        }

        /// <summary>"0.83", "1", "1.1 / 1 / 1".</summary>
        public static string Format(Vector3 value)
        {
            if (IsUniform(value)) return Format(value.x);
            return Format(value.x) + " / " + Format(value.y) + " / " + Format(value.z);
        }

        public static string Format(float value)
        {
            return (Mathf.Round(value * 1000f) / 1000f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool IsUniform(Vector3 value)
        {
            return Mathf.Abs(value.x - value.y) <= Epsilon && Mathf.Abs(value.y - value.z) <= Epsilon;
        }

        public static bool IsDefault(Vector3 value)
        {
            return !Differs(value, Vector3.one);
        }
    }
}
