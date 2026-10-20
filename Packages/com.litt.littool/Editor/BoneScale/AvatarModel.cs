using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.BoneScale
{
    internal enum AvatarState
    {
        None,
        PrefabAsset,
        NoAnimator,
        NotHumanoid,
        Ok,
    }

    internal enum SlotKind
    {
        Humanoid,
        ByName,
        Manual,
        Armature,
    }

    /// <summary>One bone of a group: the single bone, or the left or right one of a pair.</summary>
    internal sealed class BoneSlot
    {
        public BoneGroup Group;
        public int Index;            // 0 = left (or single), 1 = right
        public string Side;          // "", "L", "R"
        public Transform Transform;  // null = not found on this avatar
        public Transform Tip;        // next bone, for the scene view ring (null = use the children)
        public SlotKind Kind;
        public string PresetKey;
        public Vector2 Dot;          // silhouette position (standard groups only)

        public string Label => Group.Paired ? Group.Name + " " + Side : Group.Name;
    }

    /// <summary>One entry of the bone list: a standard group (Head, UpperArm...) or a bone picked from the Armature.</summary>
    internal sealed class BoneGroup
    {
        public string Key;
        public string Name;
        public BoneCatalog.GroupDef Def; // null for a bone picked from the Armature
        public readonly List<BoneSlot> Slots = new List<BoneSlot>();

        public bool Paired => Slots.Count == 2;
        public bool IsCustom => Def == null;
        public bool HasDots => Def != null;
    }

    /// <summary>The avatar as the Bone Scale tab sees it: its state, its Armature and the listed bone groups.</summary>
    internal sealed class AvatarModel
    {
        public GameObject Root;
        public Animator Animator;
        public AvatarState State;
        public Transform Armature;
        public Transform BreastLByName;
        public Transform BreastRByName;
        public readonly List<BoneGroup> Groups = new List<BoneGroup>();
        public readonly Dictionary<Transform, BoneSlot> SlotOf = new Dictionary<Transform, BoneSlot>();

        public bool IsOk => State == AvatarState.Ok && Root != null;

        public BoneGroup FindGroup(string key)
        {
            foreach (var group in Groups)
            {
                if (group.Key == key) return group;
            }
            return null;
        }

        public static AvatarModel Build(GameObject root, IList<string> entries, Transform breastL, Transform breastR)
        {
            var model = new AvatarModel { Root = root };
            if (root == null)
            {
                model.State = AvatarState.None;
                return model;
            }
            if (EditorUtility.IsPersistent(root))
            {
                model.State = AvatarState.PrefabAsset;
                return model;
            }

            var animator = root.GetComponent<Animator>();
            model.Animator = animator;
            if (animator == null)
            {
                model.State = AvatarState.NoAnimator;
                return model;
            }
            Transform hips;
            try
            {
                hips = animator.isHuman && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            }
            catch (Exception)
            {
                hips = null; // GetBoneTransform throws when the avatar asset is broken
            }
            if (hips == null)
            {
                model.State = AvatarState.NotHumanoid;
                return model;
            }

            model.State = AvatarState.Ok;
            model.Armature = hips.parent != null && hips.parent != root.transform && hips.parent.IsChildOf(root.transform) ? hips.parent : hips;
            FindBreasts(model);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in entries)
            {
                if (!seen.Add(key)) continue;
                BoneGroup group = null;
                if (BoneCatalog.TryGet(key, out var def)) group = BuildStandard(model, def, breastL, breastR);
                else if (BoneCatalog.IsCustom(key)) group = BuildCustom(model, key);
                if (group == null) continue;

                // A bone listed twice (e.g. picked from the Armature and also a standard group) keeps its first entry.
                bool duplicate = false;
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform != null && model.SlotOf.ContainsKey(slot.Transform)) duplicate = true;
                }
                if (duplicate) continue;

                model.Groups.Add(group);
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform != null) model.SlotOf[slot.Transform] = slot;
                }
            }
            return model;
        }

        /// <summary>The humanoid bone, or null (also when the avatar asset is broken).</summary>
        public Transform HumanBone(HumanBodyBones bone)
        {
            if (bone == HumanBodyBones.LastBone || Animator == null) return null;
            try
            {
                return Animator.GetBoneTransform(bone);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static BoneGroup BuildStandard(AvatarModel model, BoneCatalog.GroupDef def, Transform breastL, Transform breastR)
        {
            var group = new BoneGroup { Key = def.Id, Name = def.Name, Def = def };
            if (def.ByName)
            {
                // Breast: a manual assignment wins over the name search.
                bool manualL = breastL != null && breastL.IsChildOf(model.Root.transform);
                bool manualR = breastR != null && breastR.IsChildOf(model.Root.transform);
                AddSlot(group, 0, "L", manualL ? breastL : model.BreastLByName, null, manualL ? SlotKind.Manual : SlotKind.ByName, def.DotLeft);
                AddSlot(group, 1, "R", manualR ? breastR : model.BreastRByName, null, manualR ? SlotKind.Manual : SlotKind.ByName, def.DotRight);
                return group;
            }
            if (!def.Paired)
            {
                AddSlot(group, 0, string.Empty, model.HumanBone(def.Left), model.HumanBone(def.TipLeft), SlotKind.Humanoid, def.DotLeft);
                return group;
            }
            AddSlot(group, 0, "L", model.HumanBone(def.Left), model.HumanBone(def.TipLeft), SlotKind.Humanoid, def.DotLeft);
            AddSlot(group, 1, "R", model.HumanBone(def.Right), model.HumanBone(def.TipRight), SlotKind.Humanoid, def.DotRight);
            return group;
        }

        static BoneGroup BuildCustom(AvatarModel model, string key)
        {
            string path = BoneCatalog.PathOf(key);
            var picked = model.Root.transform.Find(path);
            string pickedName = picked != null ? picked.name : LastSegment(path);
            var group = new BoneGroup { Key = key };

            // A bone whose name has a side (Ear_L) is paired with its mirror (Ear_R) when the avatar has one.
            Transform mirror = null;
            string mirrorName = BoneCatalog.MirrorName(pickedName);
            if (picked != null && mirrorName != null) mirror = FindMirror(model, picked, path);
            if (mirror != null && mirror != picked)
            {
                bool pickedIsLeft = BoneCatalog.SideOf(pickedName) != 'R';
                var left = pickedIsLeft ? picked : mirror;
                var right = pickedIsLeft ? mirror : picked;
                group.Name = BoneCatalog.BaseName(pickedName);
                AddSlot(group, 0, "L", left, null, SlotKind.Armature, Vector2.zero);
                AddSlot(group, 1, "R", right, null, SlotKind.Armature, Vector2.zero);
                group.Slots[0].PresetKey = BoneCatalog.CustomKey(AnimationUtility.CalculateTransformPath(left, model.Root.transform));
                group.Slots[1].PresetKey = BoneCatalog.CustomKey(AnimationUtility.CalculateTransformPath(right, model.Root.transform));
                return group;
            }

            group.Name = pickedName;
            AddSlot(group, 0, string.Empty, picked, null, SlotKind.Armature, Vector2.zero);
            group.Slots[0].PresetKey = key;
            return group;
        }

        static void AddSlot(BoneGroup group, int index, string side, Transform transform, Transform tip, SlotKind kind, Vector2 dot)
        {
            group.Slots.Add(new BoneSlot
            {
                Group = group,
                Index = index,
                Side = side,
                Transform = transform,
                Tip = tip,
                Kind = kind,
                Dot = dot,
                PresetKey = BoneCatalog.PresetKey(group.Key, side),
            });
        }

        static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        /// <summary>The mirrored bone: the same path with every side swapped, else a unique bone with the mirrored name.</summary>
        static Transform FindMirror(AvatarModel model, Transform picked, string path)
        {
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string swapped = BoneCatalog.MirrorName(segments[i]);
                if (swapped != null) segments[i] = swapped;
            }
            var byPath = model.Root.transform.Find(string.Join("/", segments));
            if (byPath != null && byPath != picked) return byPath;

            string mirrorName = BoneCatalog.MirrorName(picked.name);
            var searchRoot = model.Armature != null ? model.Armature : model.Root.transform;
            Transform found = null;
            foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == picked || t.name != mirrorName) continue;
                if (found != null) return null; // ambiguous
                found = t;
            }
            return found;
        }

        // ---- Breast --------------------------------------------------------------------------------------

        /// <summary>
        /// Breast bones by name (Breast_L, Breast.L, Bust_L, Left_Breast, 胸_左...) under the spine, outside the
        /// neck, arm and leg branches. The candidate closest to the spine wins on each side.
        /// </summary>
        static void FindBreasts(AvatarModel model)
        {
            var start = model.HumanBone(HumanBodyBones.Spine) ?? model.HumanBone(HumanBodyBones.Chest);
            if (start == null) return;

            var skip = new HashSet<Transform>();
            foreach (var bone in new[]
                     {
                         HumanBodyBones.Neck, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                         HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                         HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                     })
            {
                var t = model.HumanBone(bone);
                if (t != null) skip.Add(t);
            }

            int bestLeft = int.MaxValue, bestRight = int.MaxValue;
            var stack = new Stack<(Transform, int)>();
            stack.Push((start, 0));
            while (stack.Count > 0)
            {
                var (t, depth) = stack.Pop();
                if (depth > 0 && BoneCatalog.LooksLikeBreast(t.name, out char side))
                {
                    if (side == 'L' && depth < bestLeft)
                    {
                        bestLeft = depth;
                        model.BreastLByName = t;
                    }
                    else if (side == 'R' && depth < bestRight)
                    {
                        bestRight = depth;
                        model.BreastRByName = t;
                    }
                }
                if (depth > 12) continue;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var child = t.GetChild(i);
                    if (skip.Contains(child)) continue;
                    stack.Push((child, depth + 1));
                }
            }
        }
    }
}
