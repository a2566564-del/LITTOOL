using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Littool.BoneScale
{
    /// <summary>
    /// A thin ring around the picked bone in the Scene view for about 1.5 seconds, so the user sees which bone they
    /// picked without anything staying in the way while they judge the avatar's shape.
    /// </summary>
    internal static class SceneFlash
    {
        const double HoldSeconds = 1.0;
        const double FadeSeconds = 0.6;
        static readonly Color RingColor = new Color(0.36f, 0.61f, 0.94f, 1f);

        static readonly List<(Transform bone, Transform tip)> s_Bones = new List<(Transform, Transform)>();
        static double s_Start = -1.0;
        static bool s_Hooked;

        public static void Show(IEnumerable<(Transform bone, Transform tip)> bones)
        {
            s_Bones.Clear();
            foreach (var entry in bones)
            {
                if (entry.bone != null) s_Bones.Add(entry);
            }
            if (s_Bones.Count == 0)
            {
                Stop();
                return;
            }
            s_Start = EditorApplication.timeSinceStartup;
            Hook();
            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            if (s_Start < 0.0 && !s_Hooked) return;
            s_Start = -1.0;
            s_Bones.Clear();
            Unhook();
            SceneView.RepaintAll();
        }

        static void Hook()
        {
            if (s_Hooked) return;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnUpdate;
            s_Hooked = true;
        }

        static void Unhook()
        {
            if (!s_Hooked) return;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnUpdate;
            s_Hooked = false;
        }

        static void OnUpdate()
        {
            if (s_Start < 0.0 || EditorApplication.timeSinceStartup - s_Start > HoldSeconds + FadeSeconds)
            {
                Stop();
                return;
            }
            SceneView.RepaintAll(); // keeps the fade moving
        }

        static void OnSceneGUI(SceneView view)
        {
            if (s_Start < 0.0 || Event.current.type != EventType.Repaint || view == null || view.camera == null) return;
            double elapsed = EditorApplication.timeSinceStartup - s_Start;
            float alpha = elapsed <= HoldSeconds ? 1f : Mathf.Clamp01(1f - (float)((elapsed - HoldSeconds) / FadeSeconds));
            if (alpha <= 0f) return;

            var oldColor = Handles.color;
            var oldZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
            var normal = view.camera.transform.forward;
            foreach (var (bone, tip) in s_Bones)
            {
                if (bone == null) continue;
                Ring(bone, tip, out var center, out float radius);
                Handles.DrawWireDisc(center, normal, radius, 2f);
            }
            Handles.zTest = oldZTest;
            Handles.color = oldColor;
        }

        /// <summary>Centre and size of the bone's segment: from the bone to its next bone, or to the middle of its children.</summary>
        static void Ring(Transform bone, Transform tip, out Vector3 center, out float radius)
        {
            var start = bone.position;
            Vector3 end;
            if (tip != null)
            {
                end = tip.position;
            }
            else if (bone.childCount > 0)
            {
                var sum = Vector3.zero;
                for (int i = 0; i < bone.childCount; i++) sum += bone.GetChild(i).position;
                end = sum / bone.childCount;
            }
            else
            {
                end = start;
            }
            float length = Vector3.Distance(start, end);
            center = (start + end) * 0.5f;
            radius = length < 0.001f ? 0.04f : Mathf.Clamp(length * 0.6f, 0.025f, 0.35f);
        }
    }
}
