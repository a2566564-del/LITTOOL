using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    /// <summary>One error of the NDMF Console with the objects it points at.</summary>
    internal struct NdmfError
    {
        public string Message;
        public string Severity;
        public List<Object> Objects;
        public string AvatarName;
        /// <summary>The display name of the plugin that reported it (ErrorContext.Plugin), null when not known.</summary>
        public string Plugin;
        /// <summary>For an exception NDMF caught: its first console line ("InvalidOperationException: …"), null otherwise.</summary>
        public string ExceptionHead;
    }

    /// <summary>
    /// Reads NDMF's error reports (the NDMF Console) by reflection: nadena.dev.ndmf.ErrorReport.Reports,
    /// each report's Errors → ErrorContext.TheError (ToMessage(), Severity, References → ObjectReference),
    /// ErrorContext.Plugin.DisplayName and, for exceptions NDMF caught, StackTraceError.Exception.
    /// Checked against NDMF 1.15.0-beta.1. NDMF also writes every error to the console as a warning
    /// ("[NDMF] Error Reported: …"), so the text is caught even when this cannot be read; this adds the objects.
    /// </summary>
    internal static class NdmfBridge
    {
        const string ReportTypeName = "nadena.dev.ndmf.ErrorReport";

        static bool s_Resolved;
        static Type s_ReportType;
        static FieldInfo s_Reports;
        static PropertyInfo s_Errors, s_AvatarName;
        static SourceState s_State = SourceState.Missing;

        public static SourceState State
        {
            get
            {
                Resolve();
                return s_State;
            }
        }

        public static string Version
        {
            get
            {
                Resolve();
                return FinderReflection.PackageVersion(s_ReportType);
            }
        }

        static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;
            s_ReportType = FinderReflection.FindType(ReportTypeName, "nadena.dev.ndmf");
            if (s_ReportType == null)
            {
                s_State = SourceState.Missing;
                return;
            }
            s_Reports = s_ReportType.GetField("Reports", FinderReflection.AnyStatic);
            s_Errors = s_ReportType.GetProperty("Errors", FinderReflection.AnyInstance);
            s_AvatarName = s_ReportType.GetProperty("AvatarName", FinderReflection.AnyInstance);
            s_State = s_Reports != null && s_Errors != null ? SourceState.Ready : SourceState.Unreadable;
        }

        /// <summary>Errors of every report NDMF keeps now (empty when NDMF is missing or cannot be read).</summary>
        public static List<NdmfError> ReadErrors()
        {
            var list = new List<NdmfError>();
            Resolve();
            if (s_State != SourceState.Ready) return list;
            try
            {
                if (!(s_Reports.GetValue(null) is IEnumerable reports)) return list;
                foreach (var report in reports)
                {
                    if (report == null) continue;
                    string avatar = s_AvatarName != null ? s_AvatarName.GetValue(report) as string : null;
                    if (!(s_Errors.GetValue(report) is IEnumerable errors)) continue;
                    foreach (var context in errors)
                    {
                        var error = context?.GetType().GetField("TheError", FinderReflection.AnyInstance)?.GetValue(context);
                        if (error == null) continue;
                        var e = new NdmfError { Objects = new List<Object>(), AvatarName = avatar };
                        var toMessage = error.GetType().GetMethod("ToMessage", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                        e.Message = toMessage != null ? toMessage.Invoke(error, null) as string : null;
                        e.Severity = error.GetType().GetProperty("Severity", FinderReflection.AnyInstance)?.GetValue(error)?.ToString();
                        if (string.IsNullOrEmpty(e.Message)) continue;
                        e.Plugin = PluginOf(context);
                        e.ExceptionHead = ExceptionHeadOf(error);
                        CollectObjects(error, report, e.Objects);
                        list.Add(e);
                    }
                }
            }
            catch (Exception)
            {
                s_State = SourceState.Unreadable;
            }
            return list;
        }

        /// <summary>ErrorContext.Plugin.DisplayName (NDMF groups the NDMF Console by it).</summary>
        static string PluginOf(object context)
        {
            try
            {
                var plugin = context.GetType().GetField("Plugin", FinderReflection.AnyInstance)?.GetValue(context);
                return plugin?.GetType().GetProperty("DisplayName", FinderReflection.AnyInstance)?.GetValue(plugin) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>StackTraceError.Exception as the console shows it: "TypeName: first line of the message".</summary>
        static string ExceptionHeadOf(object error)
        {
            try
            {
                if (!(error.GetType().GetProperty("Exception", FinderReflection.AnyInstance)?.GetValue(error) is Exception ex)) return null;
                string message = ex.Message ?? string.Empty;
                int nl = message.IndexOf('\n');
                if (nl >= 0) message = message.Substring(0, nl);
                return (ex.GetType().Name + ": " + message).Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void CollectObjects(object error, object report, List<Object> into)
        {
            var refsProp = error.GetType().GetProperty("References", FinderReflection.AnyInstance);
            if (refsProp == null || !(refsProp.GetValue(error) is IEnumerable refs)) return;
            foreach (var r in refs)
            {
                if (r == null) continue;
                Object obj = null;
                var tryResolve = r.GetType().GetMethod("TryResolve", FinderReflection.AnyInstance);
                if (tryResolve != null)
                {
                    var args = new object[] { report, null };
                    try
                    {
                        if ((bool)tryResolve.Invoke(r, args)) obj = args[1] as Object;
                    }
                    catch (Exception)
                    {
                        obj = null;
                    }
                }
                if (obj == null) obj = r.GetType().GetProperty("Object", FinderReflection.AnyInstance)?.GetValue(r) as Object;
                if (obj != null && !into.Contains(obj)) into.Add(obj);
            }
        }

        /// <summary>Opens the NDMF Console (menu Tools › NDM Framework › Show NDMF Console of NDMF 1.15), when NDMF is installed.</summary>
        public static bool OpenConsole()
        {
            if (State == SourceState.Missing) return false;
            return UnityEditor.EditorApplication.ExecuteMenuItem("Tools/NDM Framework/Show NDMF Console");
        }
    }
}
