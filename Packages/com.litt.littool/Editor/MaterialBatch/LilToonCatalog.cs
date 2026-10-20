using System;
using System.Collections.Generic;

namespace Littool.MaterialBatch
{
    internal sealed class CatalogSection
    {
        public readonly string Id;
        readonly string[] m_Names;
        public readonly IReadOnlyList<CatalogProperty> Properties;

        public CatalogSection(string id, string[] names, CatalogProperty[] properties)
        {
            Id = id;
            m_Names = names;
            Properties = properties;
            foreach (var property in properties) property.Section = this;
        }

        /// <summary>Section name in the current language (lilToon's own translation).</summary>
        public string Name
        {
            get
            {
                int index = (int)L.Current;
                return index >= 0 && index < m_Names.Length && !string.IsNullOrEmpty(m_Names[index]) ? m_Names[index] : m_Names[0];
            }
        }
    }

    internal sealed class CatalogProperty
    {
        public readonly string Name;
        public readonly string TypeLabel;
        public CatalogSection Section { get; internal set; }

        public CatalogProperty(string name, string typeLabel)
        {
            Name = name;
            TypeLabel = typeLabel;
        }

        public string Key => LilToonCatalog.PropertyPrefix + Name;
    }

    /// <summary>
    /// The lilToon properties LITTOOL can copy, grouped by section.
    /// Textures, normal maps, UV/mesh-dependent values and rendering settings are intentionally absent.
    /// List entries are either a section id ("shadow") or "p:" + a property name ("p:_ShadowBorder").
    /// </summary>
    internal static partial class LilToonCatalog
    {
        public const string PropertyPrefix = "p:";

        public static readonly string[] DefaultEntries = { "shadow", "rimshade", "backlight", "rim" };

        static CatalogSection[] s_Sections;
        static Dictionary<string, CatalogSection> s_SectionById;
        static Dictionary<string, CatalogProperty> s_PropertyByName;

        public static IReadOnlyList<CatalogSection> Sections
        {
            get
            {
                EnsureBuilt();
                return s_Sections;
            }
        }

        static void EnsureBuilt()
        {
            if (s_Sections != null) return;
            var sections = CreateSections();
            var byId = new Dictionary<string, CatalogSection>(StringComparer.Ordinal);
            var byName = new Dictionary<string, CatalogProperty>(StringComparer.Ordinal);
            foreach (var section in sections)
            {
                byId[section.Id] = section;
                foreach (var property in section.Properties)
                {
                    if (!byName.ContainsKey(property.Name)) byName.Add(property.Name, property);
                }
            }
            s_SectionById = byId;
            s_PropertyByName = byName;
            s_Sections = sections;
        }

        static CatalogSection S(string id, string ko, string en, string ja, params CatalogProperty[] properties)
        {
            return new CatalogSection(id, new[] { ko, en, ja }, properties);
        }

        static CatalogProperty P(string name, string typeLabel)
        {
            return new CatalogProperty(name, typeLabel);
        }

        public static string PropertyKey(string propertyName) => PropertyPrefix + propertyName;

        public static bool IsPropertyKey(string key) => key != null && key.StartsWith(PropertyPrefix, StringComparison.Ordinal);

        public static bool TryGetSection(string key, out CatalogSection section)
        {
            EnsureBuilt();
            section = null;
            return key != null && s_SectionById.TryGetValue(key, out section);
        }

        public static bool TryGetPropertyByKey(string key, out CatalogProperty property)
        {
            property = null;
            if (!IsPropertyKey(key)) return false;
            return TryGetProperty(key.Substring(PropertyPrefix.Length), out property);
        }

        public static bool TryGetProperty(string propertyName, out CatalogProperty property)
        {
            EnsureBuilt();
            property = null;
            return propertyName != null && s_PropertyByName.TryGetValue(propertyName, out property);
        }

        public static bool IsValidKey(string key)
        {
            return TryGetSection(key, out _) || TryGetPropertyByKey(key, out _);
        }

        /// <summary>Display name of a list entry in the current language.</summary>
        public static string GetDisplayName(string key)
        {
            if (TryGetSection(key, out var section)) return section.Name;
            if (TryGetPropertyByKey(key, out var property)) return property.Name;
            return key;
        }

        /// <summary>Expands list entries to property names, keeping list order and removing duplicates.</summary>
        public static List<string> ExpandToPropertyNames(IEnumerable<string> entries)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null) return result;
            foreach (var key in entries)
            {
                if (TryGetSection(key, out var section))
                {
                    foreach (var property in section.Properties)
                    {
                        if (seen.Add(property.Name)) result.Add(property.Name);
                    }
                }
                else if (TryGetPropertyByKey(key, out var single))
                {
                    if (seen.Add(single.Name)) result.Add(single.Name);
                }
            }
            return result;
        }
    }
}
