using System;
using System.Reflection;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// Status of one place the Error Finder reads (console, VRChat SDK, NDMF, script compilation).
    /// The SDK and NDMF are optional: LITTOOL has no reference to them and reads them by reflection.
    /// </summary>
    internal enum SourceState
    {
        /// <summary>The tool is installed and could be read.</summary>
        Ready,
        /// <summary>The tool is not in the project.</summary>
        Missing,
        /// <summary>The tool is there but its members changed (another version): read the console text instead.</summary>
        Unreadable,
    }

    internal static class FinderReflection
    {
        public const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>A type by its full name in any loaded assembly (preferring <paramref name="assemblyName"/>).</summary>
        public static Type FindType(string fullName, string assemblyName = null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName != null && asm.GetName().Name != assemblyName) continue;
                Type t = null;
                try
                {
                    t = asm.GetType(fullName, false);
                }
                catch (Exception)
                {
                    // a broken assembly: skip it
                }
                if (t != null) return t;
            }
            return assemblyName != null ? FindType(fullName) : null;
        }

        /// <summary>A type by its short name (when the namespace is not known), in assemblies whose name starts with <paramref name="assemblyPrefix"/>.</summary>
        public static Type FindTypeByName(string name, string assemblyPrefix)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith(assemblyPrefix, StringComparison.Ordinal)) continue;
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
                    if (t != null && t.Name == name) return t;
                }
            }
            return null;
        }

        /// <summary>Version of the package that contains <paramref name="type"/> ("" when unknown).</summary>
        public static string PackageVersion(Type type)
        {
            if (type == null) return string.Empty;
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
                return info != null ? info.version : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
