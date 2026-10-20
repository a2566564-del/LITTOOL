using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

namespace Littool.MaterialBatch
{
    internal enum IssueLevel
    {
        None = 0,
        Warning = 1,
        Error = 2,
    }

    internal enum IssueKind
    {
        ShaderMissing,
        ReadOnlyImported,
        ReadOnlyPackage,
        ReadOnlyFile,
        NotLilToon,
        Partial,
        NoneApplicable,
        Locked,
        Checkout,
        SharedChange,
    }

    internal enum ReferenceState
    {
        None,
        ShaderMissing,
        NotLilToon,
        Ok,
    }

    /// <summary>One renderer under the target ("part"), e.g. Jacket, Shirt, Skirt.</summary>
    internal sealed class PartInfo
    {
        public Renderer Renderer;
        public int InstanceId;
        public string Path;
        public string Name;
        public readonly List<Material> Slots = new List<Material>();
        public IssueLevel Worst;
    }

    /// <summary>One unique material found under the target.</summary>
    internal sealed class MaterialInfo
    {
        public Material Material;
        public PartInfo FirstPart;
        public int UseCount;
        public bool IsReference;
        public bool IsPersistent;
        public string ShaderName;

        /// <summary>A problem that prevents applying (missing shader, read-only, not lilToon).</summary>
        public IssueKind? Blocking;
        public bool NeedsCheckout;

        // Filled by Evaluate().
        public int Order;
        /// <summary>At least one checked row (part + material) uses this material.</summary>
        public bool Selected;
        public PartInfo FirstSelectedPart;
        public int MissingCount;
        public int LockedCount;
        public bool WillApply;
        public IssueKind? Tag;
    }

    internal readonly struct ResolvedProperty
    {
        public readonly string Name;
        public readonly int Id;
        public readonly ShaderPropertyType Type;

        public ResolvedProperty(string name, ShaderPropertyType type)
        {
            Name = name;
            Id = Shader.PropertyToID(name);
            Type = type;
        }
    }

    internal sealed class Issue
    {
        public IssueLevel Level;
        public IssueKind Kind;
        public MaterialInfo Info;
        /// <summary>The part the issue is shown at (its path is the location in the list).</summary>
        public PartInfo Part;
        public int Count;
        public int Order;
    }

    internal sealed class ScanResult
    {
        public bool HasRoot;
        public string RootName;
        public readonly List<PartInfo> Parts = new List<PartInfo>();
        public readonly List<MaterialInfo> Materials = new List<MaterialInfo>();
        public readonly Dictionary<Material, MaterialInfo> Lookup = new Dictionary<Material, MaterialInfo>();
    }

    internal sealed class EvalResult
    {
        public ReferenceMode Mode;
        public ReferenceState ReferenceState;
        public string ReferenceShaderName;
        public PresetInfo Preset;
        /// <summary>Listed properties the reference does not have (material) or does not store (preset).</summary>
        public int ReferenceMissing;
        public readonly List<ResolvedProperty> Properties = new List<ResolvedProperty>();
        /// <summary>Preset mode: per list entry, x = values the preset stores, y = values in the entry.</summary>
        public readonly Dictionary<string, Vector2Int> Coverage = new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
        public bool IncludesMainColor;
        public int PartCount;
        public int SelectedPartCount;
        public int ApplyCount;
        public int SkipCount;
        public readonly List<Issue> Issues = new List<Issue>();
        public int ErrorCount;
        public int WarningCount;
    }

    /// <summary>Finds materials under a target and decides what can be applied. Pure read-only: changes nothing.</summary>
    internal static class MaterialScanner
    {
        struct ReadOnlyState
        {
            public IssueKind? Kind;
            public bool NeedsCheckout;
        }

        // AssetImporter / package / VCS lookups are relatively slow; cache them until the project changes.
        static readonly Dictionary<int, ReadOnlyState> s_ReadOnlyCache = new Dictionary<int, ReadOnlyState>();

        public static void ClearCache()
        {
            s_ReadOnlyCache.Clear();
        }

        public static ScanResult Scan(GameObject root, Material reference)
        {
            var result = new ScanResult();
            if (root == null) return result;

            result.HasRoot = true;
            result.RootName = root.name;

            // includeInactive: parts toggled off on the avatar still count.
            // sharedMaterials (never .materials) so nothing is instantiated or leaked in edit mode.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                PartInfo part = null;
                foreach (var material in materials)
                {
                    if (material == null) continue; // empty slot
                    if (part == null)
                    {
                        part = new PartInfo
                        {
                            Renderer = renderer,
                            InstanceId = renderer.GetInstanceID(),
                            Name = renderer.gameObject.name,
                            Path = BuildPath(root.transform, renderer.transform),
                        };
                        result.Parts.Add(part);
                    }
                    part.Slots.Add(material);

                    if (!result.Lookup.TryGetValue(material, out var info))
                    {
                        info = Inspect(material, reference);
                        info.FirstPart = part;
                        result.Lookup.Add(material, info);
                        result.Materials.Add(info);
                    }
                    info.UseCount++;
                }
            }
            return result;
        }

        /// <param name="isRowSelected">Whether a material is checked under a part; null = everything is checked.</param>
        public static EvalResult Evaluate(ScanResult scan, ReferenceInput reference, IList<string> entries, Func<PartInfo, Material, bool> isRowSelected)
        {
            var eval = new EvalResult { Mode = reference.Mode };
            var names = LilToonCatalog.ExpandToPropertyNames(entries);
            eval.IncludesMainColor = names.Contains("_Color");
            eval.ReferenceShaderName = string.Empty;

            if (reference.Mode == ReferenceMode.Preset)
            {
                ResolveFromPreset(eval, reference.Preset, names, entries);
                // A preset only changes the main color if it stores one (lilToon's "Illust" presets do not).
                if (reference.Preset != null && !reference.Preset.Values.ContainsKey("_Color")) eval.IncludesMainColor = false;
            }
            else
            {
                ResolveFromMaterial(eval, reference.Material, names);
            }

            bool canCheckProperties = eval.ReferenceState == ReferenceState.Ok && eval.Properties.Count > 0;
            bool IsSelected(PartInfo part, Material material) => isRowSelected == null || isRowSelected(part, material);

            // Which materials are checked somewhere, and where they are first checked.
            for (int i = 0; i < scan.Materials.Count; i++)
            {
                var info = scan.Materials[i];
                info.Order = i;
                info.Selected = false;
                info.FirstSelectedPart = null;
                info.MissingCount = 0;
                info.LockedCount = 0;
                info.WillApply = false;
                info.Tag = null;
            }
            eval.PartCount = scan.Parts.Count;
            foreach (var part in scan.Parts)
            {
                bool any = false;
                foreach (var material in part.Slots)
                {
                    if (material == null || !IsSelected(part, material)) continue;
                    any = true;
                    if (!scan.Lookup.TryGetValue(material, out var info)) continue;
                    info.Selected = true;
                    if (info.FirstSelectedPart == null) info.FirstSelectedPart = part;
                }
                if (any) eval.SelectedPartCount++;
            }

            foreach (var info in scan.Materials)
            {
                if (info.Material == null || info.IsReference || !info.Selected) continue;
                var location = info.FirstSelectedPart ?? info.FirstPart;

                if (info.Blocking.HasValue)
                {
                    var kind = info.Blocking.Value;
                    AddIssue(eval, info, location, kind, LevelOf(kind), 0);
                    info.Tag = kind;
                    eval.SkipCount++;
                    continue;
                }

                if (canCheckProperties)
                {
                    var material = info.Material;
                    var shader = material.shader;
                    bool isVariant = material.isVariant;
                    foreach (var property in eval.Properties)
                    {
                        if (!IsCompatible(shader, property))
                        {
                            info.MissingCount++;
                        }
                        else if (isVariant && material.IsPropertyLockedByAncestor(property.Name))
                        {
                            info.LockedCount++;
                        }
                    }

                    int usable = eval.Properties.Count - info.MissingCount - info.LockedCount;
                    info.WillApply = usable > 0;

                    if (usable <= 0)
                    {
                        AddIssue(eval, info, location, IssueKind.NoneApplicable, IssueLevel.Warning, 0);
                        info.Tag = IssueKind.NoneApplicable;
                    }
                    else
                    {
                        if (info.MissingCount > 0)
                        {
                            AddIssue(eval, info, location, IssueKind.Partial, IssueLevel.Warning, info.MissingCount);
                            info.Tag = IssueKind.Partial;
                        }
                        if (info.LockedCount > 0)
                        {
                            AddIssue(eval, info, location, IssueKind.Locked, IssueLevel.Warning, info.LockedCount);
                            if (!info.Tag.HasValue) info.Tag = IssueKind.Locked;
                        }
                    }
                }
                else
                {
                    // No valid reference/list yet: show what would be applied once they are set.
                    info.WillApply = true;
                }

                if (info.NeedsCheckout && info.WillApply)
                {
                    AddIssue(eval, info, location, IssueKind.Checkout, IssueLevel.Warning, 0);
                    if (!info.Tag.HasValue) info.Tag = IssueKind.Checkout;
                }

                if (info.WillApply) eval.ApplyCount++;
                else eval.SkipCount++;
            }

            // A material file is shared by every part that uses it: if one checked row changes it,
            // the unchecked rows of the same material change too. Warn at each of those rows.
            var warned = new HashSet<long>();
            foreach (var part in scan.Parts)
            {
                foreach (var material in part.Slots)
                {
                    if (material == null || IsSelected(part, material)) continue;
                    if (!scan.Lookup.TryGetValue(material, out var info) || !info.WillApply) continue;
                    long key = ((long)part.InstanceId << 32) ^ (uint)material.GetInstanceID();
                    if (!warned.Add(key)) continue;
                    AddIssue(eval, info, part, IssueKind.SharedChange, IssueLevel.Warning, 0);
                }
            }

            // Errors first, then warnings; within a level keep hierarchy order (OrderBy is stable).
            var sorted = eval.Issues.OrderByDescending(i => (int)i.Level).ThenBy(i => i.Order).ToList();
            eval.Issues.Clear();
            eval.Issues.AddRange(sorted);

            // Worst level per part, for the icon on the part row.
            foreach (var part in scan.Parts)
            {
                part.Worst = IssueLevel.None;
                foreach (var material in part.Slots)
                {
                    if (material == null || !scan.Lookup.TryGetValue(material, out var info) || info.IsReference) continue;
                    var level = IssueLevel.None;
                    if (IsSelected(part, material))
                    {
                        if (info.Tag.HasValue) level = LevelOf(info.Tag.Value);
                    }
                    else if (info.WillApply)
                    {
                        level = IssueLevel.Warning; // changes through another part
                    }
                    if (level > part.Worst) part.Worst = level;
                }
            }
            return eval;
        }

        static void ResolveFromMaterial(EvalResult eval, Material reference, List<string> names)
        {
            if (reference == null) eval.ReferenceState = ReferenceState.None;
            else if (LilToonUtil.IsShaderMissing(reference)) eval.ReferenceState = ReferenceState.ShaderMissing;
            else if (!LilToonUtil.IsLilToon(reference)) eval.ReferenceState = ReferenceState.NotLilToon;
            else eval.ReferenceState = ReferenceState.Ok;
            eval.ReferenceShaderName = reference != null && reference.shader != null ? reference.shader.name : string.Empty;

            if (eval.ReferenceState != ReferenceState.Ok) return;
            var shader = reference.shader;
            foreach (var name in names)
            {
                int index = shader.FindPropertyIndex(name);
                if (index < 0)
                {
                    eval.ReferenceMissing++;
                    continue;
                }
                var type = shader.GetPropertyType(index);
                if (type == ShaderPropertyType.Texture)
                {
                    eval.ReferenceMissing++;
                    continue;
                }
                eval.Properties.Add(new ResolvedProperty(name, type));
            }
        }

        static void ResolveFromPreset(EvalResult eval, PresetInfo preset, List<string> names, IList<string> entries)
        {
            eval.Preset = preset;
            if (preset == null)
            {
                eval.ReferenceState = ReferenceState.None;
                return;
            }
            eval.ReferenceState = ReferenceState.Ok;

            foreach (var name in names)
            {
                if (preset.Values.TryGetValue(name, out var stored)) eval.Properties.Add(new ResolvedProperty(name, ReferenceValues.TypeOf(stored)));
                else eval.ReferenceMissing++;
            }

            if (entries == null) return;
            foreach (var key in entries)
            {
                if (key == null || eval.Coverage.ContainsKey(key)) continue;
                var keyNames = LilToonCatalog.ExpandToPropertyNames(new[] { key });
                int stored = 0;
                foreach (var name in keyNames)
                {
                    if (preset.Values.ContainsKey(name)) stored++;
                }
                eval.Coverage[key] = new Vector2Int(stored, keyNames.Count);
            }
        }

        public static IssueLevel LevelOf(IssueKind kind)
        {
            switch (kind)
            {
                case IssueKind.ShaderMissing:
                case IssueKind.ReadOnlyImported:
                case IssueKind.ReadOnlyPackage:
                case IssueKind.ReadOnlyFile:
                    return IssueLevel.Error;
                default:
                    return IssueLevel.Warning;
            }
        }

        /// <summary>Float and Range are both float-backed (lilToon's "Int" properties are float-backed too).</summary>
        public static int CategoryOf(ShaderPropertyType type)
        {
            switch (type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return 0;
                case ShaderPropertyType.Color:
                    return 1;
                case ShaderPropertyType.Vector:
                    return 2;
                case ShaderPropertyType.Int:
                    return 3;
                default:
                    return 4; // Texture: never copied
            }
        }

        public static bool IsCompatible(Shader shader, ResolvedProperty property)
        {
            if (shader == null) return false;
            int index = shader.FindPropertyIndex(property.Name);
            if (index < 0) return false;
            int category = CategoryOf(shader.GetPropertyType(index));
            return category != 4 && category == CategoryOf(property.Type);
        }

        static void AddIssue(EvalResult eval, MaterialInfo info, PartInfo part, IssueKind kind, IssueLevel level, int count)
        {
            eval.Issues.Add(new Issue { Level = level, Kind = kind, Info = info, Part = part, Count = count, Order = info.Order });
            if (level == IssueLevel.Error) eval.ErrorCount++;
            else eval.WarningCount++;
        }

        static MaterialInfo Inspect(Material material, Material reference)
        {
            var info = new MaterialInfo
            {
                Material = material,
                IsReference = reference != null && material == reference,
                IsPersistent = EditorUtility.IsPersistent(material),
                ShaderName = material.shader != null ? material.shader.name : "(none)",
            };

            if (LilToonUtil.IsShaderMissing(material))
            {
                info.Blocking = IssueKind.ShaderMissing;
            }
            else if (!LilToonUtil.IsLilToon(material))
            {
                info.Blocking = IssueKind.NotLilToon;
            }
            else
            {
                int id = material.GetInstanceID();
                if (!s_ReadOnlyCache.TryGetValue(id, out var state))
                {
                    state.Kind = GetReadOnlyKind(material, out state.NeedsCheckout);
                    s_ReadOnlyCache[id] = state;
                }
                if (state.Kind.HasValue) info.Blocking = state.Kind;
                info.NeedsCheckout = state.NeedsCheckout;
            }
            return info;
        }

        /// <summary>
        /// Why a material asset cannot be modified, or null if it can.
        /// Scene-embedded (non-persistent) materials are editable; saving the scene keeps them.
        /// </summary>
        static IssueKind? GetReadOnlyKind(Material material, out bool needsCheckout)
        {
            needsCheckout = false;
            if (!EditorUtility.IsPersistent(material)) return null;

            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path)) return null;

            // Materials generated by a model/scripted importer (FBX, .blend, VRM, ...) are read-only sub-assets.
            var importer = AssetImporter.GetAtPath(path);
            if (importer is ModelImporter || importer is ScriptedImporter) return IssueKind.ReadOnlyImported;
            if ((material.hideFlags & HideFlags.NotEditable) != 0) return IssueKind.ReadOnlyImported;

            // Registry / Git / built-in / tarball packages are immutable. Embedded and local ones (VCC packages) are fine.
            if (IsImmutablePackagePath(path)) return IssueKind.ReadOnlyPackage;

            bool vcsActive = UnityEditor.VersionControl.Provider.isActive;
            if (!AssetDatabase.IsOpenForEdit(material))
            {
                if (vcsActive)
                {
                    needsCheckout = true;
                    return null;
                }
                return IssueKind.ReadOnlyFile;
            }

            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
                {
                    if (vcsActive)
                    {
                        needsCheckout = true;
                        return null;
                    }
                    return IssueKind.ReadOnlyFile;
                }
            }
            catch (Exception)
            {
                // Path or permission problems: let the save step report them instead.
            }
            return null;
        }

        /// <summary>True for paths inside a registry, git, built-in or tarball package (embedded and local ones are writable).</summary>
        public static bool IsImmutablePackagePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Packages/", StringComparison.Ordinal)) return false;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local;
        }

        static string BuildPath(Transform root, Transform target)
        {
            if (target == root) return root.name;
            var names = new List<string>();
            for (var t = target; t != null && t != root; t = t.parent) names.Add(t.name);
            names.Add(root.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
