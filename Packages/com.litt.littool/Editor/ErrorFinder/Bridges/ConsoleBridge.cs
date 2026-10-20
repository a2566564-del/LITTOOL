using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Littool.ErrorFinder
{
    /// <summary>One entry of the Unity console as the Console window shows it.</summary>
    internal struct ConsoleEntry
    {
        public string Text;
        public int Mode;
        public int InstanceId;
        public string File;

        public bool IsError => (Mode & ConsoleBridge.ErrorMask) != 0;
        public bool IsWarning => !IsError && (Mode & ConsoleBridge.WarningMask) != 0;
        public bool IsCompileError => (Mode & ConsoleBridge.ScriptCompileError) != 0;
    }

    /// <summary>
    /// Reads the Unity console (UnityEditor.LogEntries, an internal API of Unity 2022.3, by reflection).
    /// Used for the object a message points at (the console entry's context object) and for [다시 검사] when
    /// "자동으로 모으기" is off. Mode bits are ConsoleWindow.Mode of Unity 2022.3.22f1.
    /// </summary>
    internal static class ConsoleBridge
    {
        public const int ErrorBit = 1 << 0;
        public const int AssertBit = 1 << 1;
        public const int FatalBit = 1 << 4;
        public const int AssetImportError = 1 << 6;
        public const int AssetImportWarning = 1 << 7;
        public const int ScriptingError = 1 << 8;
        public const int ScriptingWarning = 1 << 9;
        public const int ScriptCompileError = 1 << 11;
        public const int ScriptCompileWarning = 1 << 12;
        public const int ScriptingException = 1 << 17;
        public const int ScriptingAssertion = 1 << 21;

        public const int ErrorMask = ErrorBit | AssertBit | FatalBit | AssetImportError | ScriptingError | ScriptCompileError |
                                     ScriptingException | ScriptingAssertion;
        public const int WarningMask = AssetImportWarning | ScriptingWarning | ScriptCompileWarning;

        static bool s_Resolved;
        static Type s_EntryType;
        static MethodInfo s_Start, s_End, s_GetEntry;
        static FieldInfo s_Message, s_Mode, s_InstanceId, s_File;

        public static bool Available
        {
            get
            {
                Resolve();
                return s_GetEntry != null;
            }
        }

        static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                var entries = asm.GetType("UnityEditor.LogEntries");
                s_EntryType = asm.GetType("UnityEditor.LogEntry");
                if (entries == null || s_EntryType == null) return;
                s_Start = entries.GetMethod("StartGettingEntries", FinderReflection.AnyStatic);
                s_End = entries.GetMethod("EndGettingEntries", FinderReflection.AnyStatic);
                s_GetEntry = entries.GetMethod("GetEntryInternal", FinderReflection.AnyStatic);
                s_Message = s_EntryType.GetField("message", FinderReflection.AnyInstance);
                s_Mode = s_EntryType.GetField("mode", FinderReflection.AnyInstance);
                s_InstanceId = s_EntryType.GetField("instanceID", FinderReflection.AnyInstance);
                s_File = s_EntryType.GetField("file", FinderReflection.AnyInstance);
                if (s_Start == null || s_End == null || s_Message == null || s_Mode == null) s_GetEntry = null;
            }
            catch (Exception)
            {
                s_GetEntry = null;
            }
        }

        /// <summary>The entries now in the console (empty when the internal API cannot be read).</summary>
        public static List<ConsoleEntry> Read()
        {
            var list = new List<ConsoleEntry>();
            Resolve();
            if (s_GetEntry == null) return list;
            try
            {
                int count = (int)s_Start.Invoke(null, null);
                try
                {
                    var entry = Activator.CreateInstance(s_EntryType, true);
                    var args = new object[2];
                    for (int i = 0; i < count; i++)
                    {
                        args[0] = i;
                        args[1] = entry;
                        if (!(bool)s_GetEntry.Invoke(null, args)) continue;
                        list.Add(new ConsoleEntry
                        {
                            Text = (string)s_Message.GetValue(entry) ?? string.Empty,
                            Mode = (int)s_Mode.GetValue(entry),
                            InstanceId = s_InstanceId != null ? (int)s_InstanceId.GetValue(entry) : 0,
                            File = s_File != null ? (string)s_File.GetValue(entry) : null,
                        });
                    }
                }
                finally
                {
                    s_End.Invoke(null, null);
                }
            }
            catch (Exception)
            {
                // the internal API changed: the caller goes on without console objects
            }
            return list;
        }
    }
}
