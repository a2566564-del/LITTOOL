using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Littool.MaterialBatch
{
    /// <summary>Where the values come from.</summary>
    internal enum ReferenceMode
    {
        Material = 0,
        Preset = 1,
    }

    /// <summary>The reference chosen in the window: a material, or a lilToon preset.</summary>
    internal readonly struct ReferenceInput
    {
        public readonly ReferenceMode Mode;
        public readonly Material Material;
        public readonly PresetInfo Preset;

        public ReferenceInput(ReferenceMode mode, Material material, PresetInfo preset)
        {
            Mode = mode;
            Material = material;
            Preset = preset;
        }
    }

    /// <summary>One value read from the reference, ready to be written to the targets.</summary>
    internal struct SourceValue
    {
        public ResolvedProperty Property;
        public float Float;
        public Color Color;
        public Vector4 Vector;
        public int Int;
    }

    /// <summary>Reads the values to copy, right before applying (so later edits of the reference are included).</summary>
    internal static class ReferenceValues
    {
        public static List<SourceValue> FromMaterial(Material material, IList<ResolvedProperty> properties)
        {
            var values = new List<SourceValue>();
            if (material == null || properties == null) return values;
            var shader = material.shader;
            foreach (var property in properties)
            {
                if (!MaterialScanner.IsCompatible(shader, property)) continue; // the reference changed since the scan
                var value = new SourceValue { Property = property };
                switch (MaterialScanner.CategoryOf(property.Type))
                {
                    case 0:
                        value.Float = material.GetFloat(property.Id);
                        break;
                    case 1:
                        value.Color = material.GetColor(property.Id);
                        break;
                    case 2:
                        value.Vector = material.GetVector(property.Id);
                        break;
                    case 3:
                        value.Int = material.GetInteger(property.Id);
                        break;
                    default:
                        continue;
                }
                values.Add(value);
            }
            return values;
        }

        /// <summary>Values the preset stores. Properties it does not store are left out (the targets keep theirs).</summary>
        public static List<SourceValue> FromPreset(PresetInfo preset, IList<ResolvedProperty> properties)
        {
            var values = new List<SourceValue>();
            if (preset == null || properties == null) return values;
            foreach (var property in properties)
            {
                if (!preset.Values.TryGetValue(property.Name, out var stored)) continue;
                if (stored.Category != MaterialScanner.CategoryOf(property.Type)) continue;
                var value = new SourceValue { Property = property };
                switch (stored.Category)
                {
                    case 0:
                        value.Float = stored.Float;
                        break;
                    case 1:
                        value.Color = stored.Color;
                        break;
                    case 2:
                        value.Vector = stored.Vector;
                        break;
                    default:
                        continue;
                }
                values.Add(value);
            }
            return values;
        }

        /// <summary>Shader property type matching a preset value (lilToon presets store floats, colors and vectors).</summary>
        public static ShaderPropertyType TypeOf(PresetValue value)
        {
            switch (value.Category)
            {
                case 1:
                    return ShaderPropertyType.Color;
                case 2:
                    return ShaderPropertyType.Vector;
                default:
                    return ShaderPropertyType.Float;
            }
        }
    }
}
