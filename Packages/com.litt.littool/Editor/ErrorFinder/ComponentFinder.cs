using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// The components of a tool the list does not know, found from the type names of its code in the stack trace
    /// (<see cref="FinderCard.TypeNames"/>): types loaded in the editor that are components and have objects in the open
    /// scenes. When none of the named types is a component, the components under the tool's namespaces
    /// (<see cref="FinderCard.TypeRoots"/>). Only finds and selects; nothing is changed.
    /// </summary>
    internal static class ComponentFinder
    {
        // resolved once per domain (a recompile reloads the domain and clears them)
        static readonly Dictionary<string, Type> s_ByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        static readonly Dictionary<string, List<Type>> s_ByRoot = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

        /// <summary>
        /// Scene components of the named types (or of the tools' namespaces), objects named <paramref name="preferName"/>
        /// first, at most <paramref name="max"/>; <paramref name="more"/> = how many more there are.
        /// </summary>
        public static List<Component> Find(IList<string> typeNames, IList<string> roots, string preferName, int max, out int more)
        {
            more = 0;
            var types = new List<Type>();
            foreach (var name in typeNames)
            {
                var t = Resolve(name);
                if (t != null && IsComponent(t) && !types.Contains(t)) types.Add(t);
            }
            if (types.Count == 0)
            {
                foreach (var root in roots)
                {
                    foreach (var t in ComponentsUnder(root))
                    {
                        if (!types.Contains(t)) types.Add(t);
                    }
                }
            }

            var found = new List<Component>();
            foreach (var t in types)
            {
                Object[] objs;
                try
                {
                    objs = Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (var o in objs)
                {
                    if (!(o is Component c) || c == null || EditorUtility.IsPersistent(c)) continue;
                    if ((c.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                    if (!found.Contains(c)) found.Add(c);
                }
            }

            found.Sort((a, b) =>
            {
                bool pa = !string.IsNullOrEmpty(preferName) && a.gameObject.name == preferName;
                bool pb = !string.IsNullOrEmpty(preferName) && b.gameObject.name == preferName;
                if (pa != pb) return pa ? -1 : 1;
                return string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform));
            });
            if (found.Count > max)
            {
                more = found.Count - max;
                found.RemoveRange(max, found.Count - max);
            }
            return found;
        }

        static bool IsComponent(Type t) => t != null && typeof(Component).IsAssignableFrom(t) && t != typeof(Component) && t != typeof(MonoBehaviour);

        static Type Resolve(string name)
        {
            if (s_ByName.TryGetValue(name, out var cached)) return cached;
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = asm.GetType(name, false);
                }
                catch (Exception)
                {
                    found = null;
                }
                if (found != null) break;
            }
            s_ByName[name] = found;
            return found;
        }

        /// <summary>Component types whose namespace starts with <paramref name="root"/> (assemblies of Unity, .NET and the SDK skipped).</summary>
        static List<Type> ComponentsUnder(string root)
        {
            if (s_ByRoot.TryGetValue(root, out var cached)) return cached;
            var list = new List<Type>();
            string prefix = root + ".";
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name ?? string.Empty;
                if (an.StartsWith("Unity", StringComparison.Ordinal) || an.StartsWith("System", StringComparison.Ordinal) ||
                    an == "mscorlib" || an == "netstandard" || an.StartsWith("Mono.", StringComparison.Ordinal) ||
                    an.StartsWith("VRC", StringComparison.Ordinal) || an.StartsWith("nadena", StringComparison.Ordinal) ||
                    an.StartsWith("LITTOOL", StringComparison.Ordinal))
                    continue;
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (var t in types)
                {
                    if (t == null || t.FullName == null || !t.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (IsComponent(t) && !t.IsAbstract && !list.Contains(t)) list.Add(t);
                }
            }
            s_ByRoot[root] = list;
            return list;
        }

        static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
