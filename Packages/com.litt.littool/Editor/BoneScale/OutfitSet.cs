using System.Collections.Generic;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>An outfit bone and the bone it follows.</summary>
    internal sealed class BonePair
    {
        public Transform Base;       // the bone this outfit bone merges into
        public Transform Merge;      // the outfit's own bone
        public Transform AvatarBone; // Base, followed through nested Merge Armatures down to the avatar's own bone
        public bool Locked;          // MA keeps Merge following Base in the editor
        public int Depth;            // 0 = Base is the avatar's own bone
        public Outfit Outfit;
    }

    /// <summary>A renderer of an outfit and the avatar bones it is bound to.</summary>
    internal sealed class OutfitPart
    {
        public Renderer Renderer;
        public readonly HashSet<Transform> AvatarBones = new HashSet<Transform>();
    }

    /// <summary>One outfit: every MA Merge Armature under one outfit root object.</summary>
    internal sealed class Outfit
    {
        public GameObject Root;
        public int Id;
        public readonly List<Component> Merges = new List<Component>();
        public readonly List<BonePair> Pairs = new List<BonePair>();
        public readonly List<OutfitPart> Parts = new List<OutfitPart>();
        public bool AnyLocked;
        public bool AnyUnlocked;
    }

    /// <summary>The outfits dressed on an avatar with Modular Avatar, and which outfit bones follow which avatar bones.</summary>
    internal sealed class OutfitSet
    {
        public readonly List<Outfit> Outfits = new List<Outfit>();
        public readonly HashSet<int> MergeIds = new HashSet<int>();
        readonly Dictionary<Transform, List<BonePair>> m_ByAvatarBone = new Dictionary<Transform, List<BonePair>>();
        static readonly List<BonePair> s_NoPairs = new List<BonePair>();

        public bool MaInstalled;

        /// <summary>Every outfit bone that follows <paramref name="avatarBone"/>, directly or through nested merges.</summary>
        public List<BonePair> PairsFor(Transform avatarBone)
        {
            if (avatarBone != null && m_ByAvatarBone.TryGetValue(avatarBone, out var list)) return list;
            return s_NoPairs;
        }

        public Outfit Find(int id)
        {
            foreach (var outfit in Outfits)
            {
                if (outfit.Id == id) return outfit;
            }
            return null;
        }

        public static OutfitSet Scan(AvatarModel model)
        {
            var set = new OutfitSet { MaInstalled = MaBridge.Installed };
            if (!set.MaInstalled || model == null || !model.IsOk) return set;

            var root = model.Root.transform;
            var components = model.Root.GetComponentsInChildren(MaBridge.MergeArmatureType, true);
            if (components.Length == 0) return set;

            // 1. Map every outfit bone to the bone it merges into.
            var mergeToBase = new Dictionary<Transform, Transform>();
            var mergeOf = new Dictionary<Transform, Component>();
            var mappings = new List<(Component component, List<(Transform baseBone, Transform mergeBone)> pairs)>();
            foreach (var component in components)
            {
                if (component == null) continue;
                var pairs = MaBridge.GetMapping(component);
                mappings.Add((component, pairs));
                foreach (var (baseBone, mergeBone) in pairs)
                {
                    if (mergeToBase.ContainsKey(mergeBone)) continue;
                    mergeToBase[mergeBone] = baseBone;
                    mergeOf[mergeBone] = component;
                }
            }

            // 2. Group the merges by outfit root: the parent of the Merge Armature object (the object itself when it sits
            //    right under the avatar), and a root inside another outfit joins that outfit.
            var candidate = new Dictionary<Component, Transform>();
            var roots = new HashSet<Transform>();
            foreach (var (component, _) in mappings)
            {
                var t = component.transform;
                var outfitRoot = t.parent != null && t.parent != root ? t.parent : t;
                candidate[component] = outfitRoot;
                roots.Add(outfitRoot);
            }
            var outfitsByRoot = new Dictionary<Transform, Outfit>();
            foreach (var (component, pairs) in mappings)
            {
                var outfitRoot = Outermost(candidate[component], roots, root);
                if (!outfitsByRoot.TryGetValue(outfitRoot, out var outfit))
                {
                    outfit = new Outfit { Root = outfitRoot.gameObject, Id = outfitRoot.gameObject.GetInstanceID() };
                    outfitsByRoot[outfitRoot] = outfit;
                    set.Outfits.Add(outfit);
                }
                outfit.Merges.Add(component);
                set.MergeIds.Add(component.GetInstanceID());
                bool locked = MaBridge.IsLocked(component);
                if (locked) outfit.AnyLocked = true;
                else outfit.AnyUnlocked = true;

                foreach (var (baseBone, mergeBone) in pairs)
                {
                    if (!mergeOf.TryGetValue(mergeBone, out var owner) || owner != component) continue;
                    var avatarBone = Resolve(baseBone, mergeToBase, out int depth);
                    if (avatarBone == null || model.Armature == null || !avatarBone.IsChildOf(model.Armature)) continue;
                    var pair = new BonePair
                    {
                        Base = baseBone,
                        Merge = mergeBone,
                        AvatarBone = avatarBone,
                        Locked = locked,
                        Depth = depth,
                        Outfit = outfit,
                    };
                    outfit.Pairs.Add(pair);
                    if (!set.m_ByAvatarBone.TryGetValue(avatarBone, out var list))
                    {
                        list = new List<BonePair>();
                        set.m_ByAvatarBone[avatarBone] = list;
                    }
                    list.Add(pair);
                }
            }

            // 3. Parts: the outfit's mesh renderers and the avatar bones they are bound to.
            foreach (var outfit in set.Outfits)
            {
                foreach (var renderer in outfit.Root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                    var part = new OutfitPart { Renderer = renderer };
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        foreach (var bone in skinned.bones)
                        {
                            var avatarBone = AvatarBoneOf(bone, mergeToBase, model.Armature);
                            if (avatarBone != null) part.AvatarBones.Add(avatarBone);
                        }
                    }
                    else
                    {
                        for (var t = renderer.transform; t != null && t != outfit.Root.transform.parent; t = t.parent)
                        {
                            var avatarBone = AvatarBoneOf(t, mergeToBase, model.Armature);
                            if (avatarBone == null) continue;
                            part.AvatarBones.Add(avatarBone);
                            break;
                        }
                    }
                    outfit.Parts.Add(part);
                }
            }
            return set;
        }

        static Transform Outermost(Transform outfitRoot, HashSet<Transform> roots, Transform avatarRoot)
        {
            var result = outfitRoot;
            for (var t = outfitRoot.parent; t != null && t != avatarRoot; t = t.parent)
            {
                if (roots.Contains(t)) result = t;
            }
            return result;
        }

        /// <summary>Follows a bone through nested Merge Armatures to the bone it finally merges into.</summary>
        static Transform Resolve(Transform bone, Dictionary<Transform, Transform> mergeToBase, out int depth)
        {
            depth = 0;
            var current = bone;
            while (current != null && mergeToBase.TryGetValue(current, out var next) && depth < 16)
            {
                current = next;
                depth++;
            }
            return current;
        }

        static Transform AvatarBoneOf(Transform bone, Dictionary<Transform, Transform> mergeToBase, Transform armature)
        {
            if (bone == null || armature == null) return null;
            if (mergeToBase.ContainsKey(bone))
            {
                var resolved = Resolve(bone, mergeToBase, out _);
                return resolved != null && resolved.IsChildOf(armature) ? resolved : null;
            }
            return bone.IsChildOf(armature) ? bone : null;
        }
    }
}
