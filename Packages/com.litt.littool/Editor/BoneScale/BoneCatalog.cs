using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// The standard bone groups of the Bone Scale tab: which humanoid bones they are, where their dots sit on the
    /// silhouette, and the list keys stored in the project settings. Other bones are stored as "path:" + the path
    /// from the avatar root (AnimationUtility.CalculateTransformPath).
    /// </summary>
    internal static class BoneCatalog
    {
        public const string CustomPrefix = "path:";

        /// <summary>Size of the silhouette drawing area; dot positions are in this space.</summary>
        public static readonly Vector2 SilhouetteSize = new Vector2(166f, 298f);

        public sealed class GroupDef
        {
            public readonly string Id;
            public readonly string Name;
            public readonly HumanBodyBones Left;
            public readonly HumanBodyBones Right;
            public readonly HumanBodyBones TipLeft;
            public readonly HumanBodyBones TipRight;
            public readonly Vector2 DotLeft;
            public readonly Vector2 DotRight;

            /// <summary>Breast is found by name: the humanoid mapping has no breast bones.</summary>
            public readonly bool ByName;

            public bool Paired => Right != HumanBodyBones.LastBone || ByName;

            public GroupDef(string id, string name, HumanBodyBones left, HumanBodyBones right, HumanBodyBones tipLeft,
                HumanBodyBones tipRight, Vector2 dotLeft, Vector2 dotRight, bool byName = false)
            {
                Id = id;
                Name = name;
                Left = left;
                Right = right;
                TipLeft = tipLeft;
                TipRight = tipRight;
                DotLeft = dotLeft;
                DotRight = dotRight;
                ByName = byName;
            }
        }

        const HumanBodyBones None = HumanBodyBones.LastBone;

        /// <summary>Catalogue order is also the order of the + menu. The left bone is drawn on the viewer's right (front view).</summary>
        public static readonly GroupDef[] Groups =
        {
            new GroupDef("head", "Head", HumanBodyBones.Head, None, None, None, new Vector2(83f, 35f), Vector2.zero),
            new GroupDef("neck", "Neck", HumanBodyBones.Neck, None, HumanBodyBones.Head, None, new Vector2(83f, 66f), Vector2.zero),
            new GroupDef("breast", "Breast", None, None, None, None, new Vector2(95f, 101f), new Vector2(71f, 101f), true),
            new GroupDef("shoulder", "Shoulder", HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, new Vector2(104f, 83f), new Vector2(62f, 83f)),
            new GroupDef("upperArm", "UpperArm", HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, new Vector2(120f, 107f), new Vector2(46f, 107f)),
            new GroupDef("lowerArm", "LowerArm", HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand, new Vector2(138f, 149f), new Vector2(28f, 149f)),
            new GroupDef("hand", "Hand", HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal, new Vector2(145f, 171f), new Vector2(21f, 171f)),
            new GroupDef("upperLeg", "UpperLeg", HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, new Vector2(99f, 203f), new Vector2(67f, 203f)),
            new GroupDef("lowerLeg", "LowerLeg", HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, new Vector2(102f, 244f), new Vector2(64f, 244f)),
            new GroupDef("foot", "Foot", HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                HumanBodyBones.LeftToes, HumanBodyBones.RightToes, new Vector2(103f, 266f), new Vector2(63f, 266f)),
        };

        public static readonly string[] DefaultEntries = { "head", "neck", "breast", "upperArm", "lowerArm", "upperLeg", "lowerLeg" };

        static Dictionary<string, GroupDef> s_ById;

        public static bool TryGet(string key, out GroupDef def)
        {
            if (s_ById == null)
            {
                s_ById = new Dictionary<string, GroupDef>(StringComparer.Ordinal);
                foreach (var group in Groups) s_ById[group.Id] = group;
            }
            if (key == null)
            {
                def = null;
                return false;
            }
            return s_ById.TryGetValue(key, out def);
        }

        public static bool IsCustom(string key) => key != null && key.StartsWith(CustomPrefix, StringComparison.Ordinal) && key.Length > CustomPrefix.Length;

        public static bool IsValidKey(string key) => TryGet(key, out _) || IsCustom(key);

        public static string CustomKey(string path) => CustomPrefix + path;

        public static string PathOf(string customKey) => IsCustom(customKey) ? customKey.Substring(CustomPrefix.Length) : string.Empty;

        /// <summary>Key of one side of a group inside a body preset: "head", "breast.L", "path:Armature/.../Ear_R".</summary>
        public static string PresetKey(string groupId, string side) => string.IsNullOrEmpty(side) ? groupId : groupId + "." + side;

        // ---- Breast detection ------------------------------------------------------------------------------

        static readonly Regex s_BreastWord = new Regex("(breast|bust|oppai|mune|胸|おっぱい)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Whether a bone name looks like a breast bone, and on which side ('L', 'R' or '\0' when it has no side).</summary>
        public static bool LooksLikeBreast(string name, out char side)
        {
            side = '\0';
            if (string.IsNullOrEmpty(name) || !s_BreastWord.IsMatch(name)) return false;
            side = SideOf(name);
            return true;
        }

        // ---- Left / right names ------------------------------------------------------------------------------

        // A lone L or R between separators (Ear_L, L_Ear, Ear.L, "Ear L"), or right after a lower-case letter at the end (EarL).
        static readonly Regex s_LoneSide = new Regex("(?<![A-Za-z])([LRlr])(?![A-Za-z])", RegexOptions.CultureInvariant);
        static readonly Regex s_TailSide = new Regex("(?<=[a-z])([LR])$", RegexOptions.CultureInvariant);
        static readonly Regex s_WordSide = new Regex("(Left|Right|left|right|LEFT|RIGHT)", RegexOptions.CultureInvariant);
        static readonly Regex s_JaSide = new Regex("([左右])", RegexOptions.CultureInvariant);

        /// <summary>'L', 'R' or '\0'.</summary>
        public static char SideOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return '\0';
            var match = LastMatch(s_LoneSide, name) ?? LastMatch(s_WordSide, name) ?? LastMatch(s_TailSide, name) ?? LastMatch(s_JaSide, name);
            if (match == null) return '\0';
            string token = match.Groups[1].Value;
            char first = char.ToUpperInvariant(token[0]);
            if (token == "左") return 'L';
            if (token == "右") return 'R';
            return first == 'L' ? 'L' : (first == 'R' ? 'R' : '\0');
        }

        /// <summary>The name with its side swapped (Ear_L → Ear_R), or null when the name has no side.</summary>
        public static string MirrorName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var match = LastMatch(s_LoneSide, name) ?? LastMatch(s_WordSide, name) ?? LastMatch(s_TailSide, name) ?? LastMatch(s_JaSide, name);
            if (match == null) return null;
            var group = match.Groups[1];
            string swapped = Swap(group.Value);
            if (swapped == null) return null;
            return name.Substring(0, group.Index) + swapped + name.Substring(group.Index + group.Length);
        }

        /// <summary>The name without its side token and the separator next to it ("Ear_L" → "Ear").</summary>
        public static string BaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var match = LastMatch(s_LoneSide, name) ?? LastMatch(s_WordSide, name) ?? LastMatch(s_TailSide, name) ?? LastMatch(s_JaSide, name);
            if (match == null) return name;
            var group = match.Groups[1];
            int start = group.Index;
            int end = group.Index + group.Length;
            // Also drop one separator on the side that has one ("Ear_L" → "Ear", "L_Ear" → "Ear").
            if (start > 0 && IsSeparator(name[start - 1])) start--;
            else if (end < name.Length && IsSeparator(name[end])) end++;
            string result = (name.Substring(0, start) + name.Substring(end)).Trim();
            return result.Length > 0 ? result : name;
        }

        static bool IsSeparator(char c) => c == '_' || c == '.' || c == '-' || c == ' ';

        static Match LastMatch(Regex regex, string name)
        {
            Match last = null;
            for (var m = regex.Match(name); m.Success; m = m.NextMatch()) last = m;
            return last;
        }

        static string Swap(string token)
        {
            switch (token)
            {
                case "L": return "R";
                case "R": return "L";
                case "l": return "r";
                case "r": return "l";
                case "Left": return "Right";
                case "Right": return "Left";
                case "left": return "right";
                case "right": return "left";
                case "LEFT": return "RIGHT";
                case "RIGHT": return "LEFT";
                case "左": return "右";
                case "右": return "左";
                default: return null;
            }
        }
    }
}
