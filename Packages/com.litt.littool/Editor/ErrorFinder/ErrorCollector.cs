using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// Collects errors into inspection units (the requirements, 오류 모으기 · 검사 단위):
    /// a play (Exiting Edit Mode → Entered Edit Mode), an upload (the SDK builder's build start → upload result,
    /// Build &amp; Test → build result), a script compilation (compilationStarted → compilationFinished).
    /// "자동으로 모으기" on (default): listens from the moment the editor starts, even with the LITTOOL window closed,
    /// and analyses a unit as soon as it ends. Off: nothing is hooked; [다시 검사] reads everything at once.
    /// LITTOOL only reads: nothing here changes the project, an avatar or a setting.
    /// </summary>
    [InitializeOnLoad]
    internal static class ErrorCollector
    {
        const string SessionKey = "LITTOOL.ErrorFinder.Started";
        const double BuiltWaitSeconds = 120.0;      // build succeeded, waiting for the upload to start
        const double UploadWaitSeconds = 1800.0;    // upload started, waiting for its result
        const double BeforeCompileSeconds = 120.0;  // errors heard this long before a failed compilation belong to it
        const double GraceSeconds = 1.0;            // the SDK may log a little more right after its result event

        static readonly Regex s_CompileLine = new Regex(@"\(\d+,\d+\)\s*:\s*error CS\d{4}", RegexOptions.CultureInvariant);

        struct Heard
        {
            public string Text;
            public int Level;
            public long Ticks;
        }

        static readonly ConcurrentQueue<Heard> s_Queue = new ConcurrentQueue<Heard>();
        static bool s_Hooked;
        static bool s_Finishing;

        /// <summary>Raised on the main thread whenever what the tab shows may have changed.</summary>
        public static event Action Changed;

        /// <summary>The last internal problem of the collector (shown small in the tab), null when none.</summary>
        public static string LastProblem { get; private set; }

        static ErrorCollector()
        {
            // Hooked at once so the first frames of Play Mode (NDMF Apply on Play) are heard; the setting is read in
            // AfterLoad, because a static constructor runs while the editor is still restoring its objects.
            Hook();
            EditorApplication.delayCall += AfterLoad;
        }

        public static bool AutoCollect
        {
            get
            {
                try
                {
                    return LittoolSettings.instance.ErrorAutoCollect;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        /// <summary>"자동으로 모으기" switched in the tab.</summary>
        public static void SetAutoCollect(bool on)
        {
            LittoolSettings.instance.ErrorAutoCollect = on;
            if (on)
            {
                Hook();
                SdkBridge.Install();
            }
            else
            {
                Unhook();
                ErrorStore.instance.ResetCurrent();
                ErrorStore.instance.ClearLoose();
            }
            RaiseChanged();
        }

        static void Hook()
        {
            if (s_Hooked) return;
            s_Hooked = true;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += Drain;
            SdkBridge.Changed += OnSdk;
        }

        static void Unhook()
        {
            if (!s_Hooked) return;
            s_Hooked = false;
            Application.logMessageReceivedThreaded -= OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= Drain;
            SdkBridge.Changed -= OnSdk;
            SdkBridge.Uninstall();
            while (s_Queue.TryDequeue(out _)) { }
        }

        static void AfterLoad()
        {
            if (!AutoCollect)
            {
                // "자동으로 모으기" off: nothing is heard
                Unhook();
                ErrorStore.instance.ResetCurrent();
                ErrorStore.instance.ClearLoose();
                return;
            }
            if (!s_Hooked) return;
            SdkBridge.Install();
            var store = ErrorStore.instance;
            // a compilation that succeeded ends with a domain reload: if its end was not heard, close it now
            if (store.Current.Exists && store.Current.kind == FinderUnitKind.Compile) FinishCurrent("succeeded");
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                // the project opened with compile errors (and not in Safe Mode): show them like a compilation
                if (EditorUtility.scriptCompilationFailed) CompileErrorsAtStart();
            }
        }

        // ---------------------------------------------------------------- hearing

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            // any thread: only queue it
            if (type == LogType.Log || string.IsNullOrEmpty(condition)) return;
            string text = string.IsNullOrEmpty(stackTrace) ? condition : condition + "\n" + stackTrace;
            s_Queue.Enqueue(new Heard { Text = text, Level = type == LogType.Warning ? 1 : 0, Ticks = DateTime.Now.Ticks });
        }

        /// <summary>Moves what was heard into the open unit, or into the loose list when no unit is open.</summary>
        static void Drain()
        {
            if (s_Queue.IsEmpty) return;
            var store = ErrorStore.instance;
            var unit = store.Current;
            bool changed = false;
            while (s_Queue.TryDequeue(out var h))
            {
                bool compileLine = s_CompileLine.IsMatch(FirstLineOf(h.Text));
                if (unit.Exists && unit.open)
                {
                    // compile errors come first from the compiler (with the file), then again in the console
                    if (compileLine && HasCompileItem(unit, h.Text)) continue;
                    unit.Add(h.Text, h.Level, FinderChannel.Console, h.Ticks);
                }
                else
                {
                    // compile errors left in the console are read again by [다시 검사]; keep the loose list for the rest
                    if (compileLine) continue;
                    store.AddLoose(h.Text, h.Level, h.Ticks);
                    changed = true;
                }
            }
            if (changed) RaiseChanged();
        }

        static bool HasCompileItem(FinderUnit unit, string text)
        {
            string t = text.TrimEnd();
            foreach (var it in unit.items)
            {
                if (it.channel == FinderChannel.Compile && it.text == t) return true;
            }
            return false;
        }

        static void OnUpdate()
        {
            try
            {
                Drain();
                SdkBridge.Pump();
                var unit = ErrorStore.instance.Current;
                // the grace after a result ended, or the SDK said nothing more (the unit keeps the outcome it has)
                if (unit.Exists && unit.open && unit.deadline > 0 && EditorApplication.timeSinceStartup > unit.deadline)
                    FinishCurrent(string.Empty);
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        // ---------------------------------------------------------------- play

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            try
            {
                var store = ErrorStore.instance;
                switch (change)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        Drain();
                        if (store.Current.Exists) FinishCurrent(string.Empty);
                        store.Begin(FinderUnitKind.Play);
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                        Drain();
                        if (store.Current.Exists && store.Current.kind == FinderUnitKind.Play)
                            FinishCurrent(store.Current.HasErrors ? string.Empty : "succeeded");
                        break;
                }
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        // ---------------------------------------------------------------- compilation

        static void OnCompilationStarted(object context)
        {
            try
            {
                Drain();
                var store = ErrorStore.instance;
                if (store.Current.Exists && store.Current.open) return;  // e.g. recompiling during Play Mode: it belongs to that unit
                store.Begin(FinderUnitKind.Compile);
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            try
            {
                if (messages == null || messages.Length == 0) return;
                var store = ErrorStore.instance;
                var unit = store.Current;
                if (!unit.Exists) unit = store.Begin(FinderUnitKind.Compile);
                long now = DateTime.Now.Ticks;
                foreach (var m in messages)
                {
                    // warnings of the compiler (obsolete APIs …) never block anything
                    if (m.type == CompilerMessageType.Error) unit.Add(m.message, 0, FinderChannel.Compile, now);
                }
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        static void OnCompilationFinished(object context)
        {
            try
            {
                Drain();
                var unit = ErrorStore.instance.Current;
                if (!unit.Exists || unit.kind != FinderUnitKind.Compile) return;
                bool failed = false;
                foreach (var it in unit.items)
                {
                    if (it.channel == FinderChannel.Compile && it.level == 0) failed = true;
                }
                if (failed)
                {
                    // errors heard just before (e.g. while importing a package that brought a second copy of a DLL)
                    long from = unit.startTicks - TimeSpan.FromSeconds(BeforeCompileSeconds).Ticks;
                    foreach (var it in unit.before)
                    {
                        if (it.ticks >= from) unit.Add(it.text, it.level, it.channel, it.ticks);
                    }
                }
                unit.before.Clear();
                FinishCurrent(failed ? "failed" : "succeeded");
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        /// <summary>The project opened with compile errors: they are in the console, not heard from a compilation.</summary>
        static void CompileErrorsAtStart()
        {
            var store = ErrorStore.instance;
            if (store.Current.Exists) return;
            var unit = store.Begin(FinderUnitKind.Compile);
            long now = DateTime.Now.Ticks;
            foreach (var e in ConsoleBridge.Read())
            {
                if (e.IsCompileError) unit.Add(e.Text, 0, FinderChannel.Compile, now, e.InstanceId);
            }
            FinishCurrent(unit.items.Count > 0 ? "failed" : string.Empty);
        }

        // ---------------------------------------------------------------- VRChat SDK

        static void OnSdk(SdkBridge.SdkEvent e, string message)
        {
            try
            {
                Drain();
                var store = ErrorStore.instance;
                var unit = store.Current;
                bool uploadOpen = unit.Exists && unit.open && (unit.kind == FinderUnitKind.Upload || unit.kind == FinderUnitKind.Test);
                long now = DateTime.Now.Ticks;
                switch (e)
                {
                    case SdkBridge.SdkEvent.BuildStart:
                        if (unit.Exists) FinishCurrent(string.Empty);
                        store.Begin(SdkBridge.IsTestBuild() ? FinderUnitKind.Test : FinderUnitKind.Upload);
                        break;
                    case SdkBridge.SdkEvent.BuildError:
                        if (!uploadOpen) unit = BeginUpload(store);
                        unit.AddOnce(message, 0, FinderChannel.Sdk, now);
                        AddValidations(unit, now);
                        EndSoon(unit, "failed");
                        break;
                    case SdkBridge.SdkEvent.BuildSuccess:
                        if (!uploadOpen) break;
                        if (unit.kind == FinderUnitKind.Test || SdkBridge.IsTestBuild())
                        {
                            unit.kind = FinderUnitKind.Test;
                            EndSoon(unit, "succeeded");
                        }
                        else
                        {
                            unit.stage = "built";
                            unit.deadline = EditorApplication.timeSinceStartup + BuiltWaitSeconds;
                        }
                        break;
                    case SdkBridge.SdkEvent.UploadStart:
                        if (!uploadOpen) unit = BeginUpload(store);
                        unit.kind = FinderUnitKind.Upload;
                        unit.stage = "uploading";
                        unit.deadline = EditorApplication.timeSinceStartup + UploadWaitSeconds;
                        break;
                    case SdkBridge.SdkEvent.UploadError:
                        // also before the build (login, permissions, copyright agreement): no build start was heard
                        if (!uploadOpen) unit = BeginUpload(store);
                        unit.kind = FinderUnitKind.Upload;
                        unit.AddOnce(message, 0, FinderChannel.Sdk, now);
                        EndSoon(unit, "failed");
                        break;
                    case SdkBridge.SdkEvent.UploadSuccess:
                        if (uploadOpen) EndSoon(unit, "succeeded");
                        break;
                }
            }
            catch (Exception ex)
            {
                Problem(ex);
            }
        }

        /// <summary>The result is known: the unit closes a moment later so the messages the SDK logs right after it still belong to it.</summary>
        static void EndSoon(FinderUnit unit, string outcome)
        {
            // a failure stays a failure even if a success event follows (another platform of a multi-platform build)
            if (!unit.Failed) unit.outcome = outcome;
            unit.deadline = EditorApplication.timeSinceStartup + GraceSeconds;
        }

        static FinderUnit BeginUpload(ErrorStore store)
        {
            if (store.Current.Exists) FinishCurrent(string.Empty);
            return store.Begin(SdkBridge.IsTestBuild() ? FinderUnitKind.Test : FinderUnitKind.Upload);
        }

        static void AddValidations(FinderUnit unit, long now)
        {
            foreach (var v in SdkBridge.ReadValidationErrors())
                unit.AddOnce(v.Key, 0, FinderChannel.Validation, now, v.Value != null ? v.Value.GetInstanceID() : 0);
        }

        // ---------------------------------------------------------------- the end of a unit

        static void FinishCurrent(string outcome)
        {
            var store = ErrorStore.instance;
            if (!store.Current.Exists || s_Finishing) return;
            s_Finishing = true;
            FinderUnit unit = null;
            try
            {
                Drain();
                unit = store.End(outcome);
            }
            catch (Exception e)
            {
                Problem(e);
            }
            finally
            {
                s_Finishing = false;
            }
            if (unit == null || !unit.Exists) return;
            if ((unit.items.Count == 0 && !unit.Failed) || (unit.kind == FinderUnitKind.Compile && unit.Succeeded))
            {
                // nothing to analyse (a clean compilation is followed by a domain reload: no time for more)
                store.Clean(unit);
                RaiseChanged();
                return;
            }
            if (unit.kind == FinderUnitKind.Compile)
            {
                // a failed compilation: no reload follows; the list is loaded outside the compilation callback
                EditorApplication.delayCall += () => Complete(unit);
                return;
            }
            Complete(unit);
        }

        /// <summary>Analyses a finished unit: shows it (and tells the user) when it has causes, clears what it makes out of date otherwise.</summary>
        static void Complete(FinderUnit unit)
        {
            var store = ErrorStore.instance;
            try
            {
                Enrich(unit);
                var matcher = ErrorCatalog.Matcher;
                var analysis = matcher != null ? ErrorAnalysis.Run(unit, matcher) : null;
                bool show = analysis != null ? !analysis.Empty || unit.Failed : unit.HasErrors;
                if (show)
                {
                    store.Show(unit);
                    Notify(unit, analysis);
                    if (analysis != null && !analysis.Empty) WriteLog(analysis, matcher.Data);
                }
                else
                {
                    store.Clean(unit);
                }
            }
            catch (Exception e)
            {
                Problem(e);
            }
            RaiseChanged();
        }

        /// <summary>Adds what the console text alone does not have: the objects NDMF and the console point at.</summary>
        static void Enrich(FinderUnit unit)
        {
            if (unit == null || unit.items.Count == 0) return;
            if (NdmfBridge.State == SourceState.Ready)
            {
                var errors = NdmfBridge.ReadErrors();
                if (errors.Count > 0)
                {
                    foreach (var it in unit.items)
                    {
                        string head = it.FirstLine;
                        bool warning = it.text.StartsWith(ErrorAnalysis.NdmfWarningPrefix, StringComparison.Ordinal);
                        if (warning && head.Length <= ErrorAnalysis.NdmfWarningPrefix.Length) continue;
                        // a report by its title line (the same title for several objects is one item with every object);
                        // an exception NDMF caught by its first console line
                        string title = warning ? head.Substring(ErrorAnalysis.NdmfWarningPrefix.Length).Trim() : null;
                        foreach (var ne in errors)
                        {
                            bool same = warning
                                ? !string.IsNullOrEmpty(ne.Message) && FirstLineOf(ne.Message) == title
                                : !string.IsNullOrEmpty(ne.ExceptionHead) && ne.ExceptionHead == head;
                            if (!same) continue;
                            foreach (var o in ne.Objects)
                            {
                                int id = o != null ? o.GetInstanceID() : 0;
                                if (id != 0 && !it.objectIds.Contains(id)) it.objectIds.Add(id);
                            }
                            if (string.IsNullOrEmpty(it.plugin) && !string.IsNullOrEmpty(ne.Plugin)) it.plugin = ne.Plugin;
                            unit.AddChannel(FinderChannel.Ndmf);
                        }
                    }
                }
            }
            bool needConsole = false;
            foreach (var it in unit.items)
            {
                if (it.objectIds.Count == 0 && it.channel == FinderChannel.Console) needConsole = true;
            }
            if (!needConsole || !ConsoleBridge.Available) return;
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in ConsoleBridge.Read())
            {
                if (e.InstanceId != 0) ids[FirstLineOf(e.Text)] = e.InstanceId;
            }
            foreach (var it in unit.items)
            {
                if (it.objectIds.Count == 0 && ids.TryGetValue(it.FirstLine, out int id)) it.objectIds.Add(id);
            }
        }

        // ---------------------------------------------------------------- [다시 검사]

        /// <summary>
        /// [다시 검사]. On: what can be checked now (compile errors in the console, the SDK window's Validations) and the
        /// errors heard outside any unit since the last one. Off: everything at once (console, NDMF, SDK, compilation).
        /// </summary>
        public static void Rescan()
        {
            try
            {
                Drain();
                var store = ErrorStore.instance;
                bool auto = AutoCollect;
                var unit = store.NewRescan();
                long now = DateTime.Now.Ticks;
                foreach (var e in ConsoleBridge.Read())
                {
                    if (e.IsCompileError) unit.Add(e.Text, 0, FinderChannel.Compile, now, e.InstanceId);
                    else if (!auto && (e.IsError || e.IsWarning)) unit.Add(e.Text, e.IsError ? 0 : 1, FinderChannel.Console, now, e.InstanceId);
                }
                if (auto)
                {
                    foreach (var it in store.Loose)
                    {
                        var copy = unit.Add(it.text, it.level, it.channel, it.ticks);
                        if (copy != null) copy.count = Math.Max(copy.count, it.count);
                    }
                    store.ClearLoose();
                }
                else
                {
                    foreach (var ne in NdmfBridge.ReadErrors())
                    {
                        var item = unit.AddOnce(ErrorAnalysis.NdmfWarningPrefix + ne.Message, 1, FinderChannel.Ndmf, now);
                        if (item == null) continue;
                        if (string.IsNullOrEmpty(item.plugin) && !string.IsNullOrEmpty(ne.Plugin)) item.plugin = ne.Plugin;
                        foreach (var o in ne.Objects)
                        {
                            int id = o != null ? o.GetInstanceID() : 0;
                            if (id != 0 && !item.objectIds.Contains(id)) item.objectIds.Add(id);
                        }
                    }
                }
                AddValidations(unit, now);
                Enrich(unit);

                var matcher = ErrorCatalog.Matcher;
                var analysis = matcher != null ? ErrorAnalysis.Run(unit, matcher) : null;
                bool found = analysis != null ? !analysis.Empty : unit.HasErrors;
                if (found)
                {
                    store.Show(unit);
                }
                else
                {
                    store.Clean(unit);
                    // play / upload errors can only be checked by playing or uploading again
                    if (store.Shown.Exists) store.Notice = "rescanNothingNew";
                }
            }
            catch (Exception e)
            {
                Problem(e);
            }
            RaiseChanged();
        }

        // ---------------------------------------------------------------- telling the user

        static void Notify(FinderUnit unit, FinderAnalysis analysis)
        {
            if (unit.kind == FinderUnitKind.Rescan) return;
            if (LittoolWindow.FocusErrorFinder()) return;  // the window is open: only its tab changes
            ErrorStore.instance.OpenTabRequest = true;
            var content = new GUIContent(ErrorFinderTab.NotificationText(unit, analysis));
            var scene = SceneView.lastActiveSceneView;
            if (scene != null) scene.ShowNotification(content, 4.0);
            if (EditorApplication.isPlaying)
            {
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType))
                        (w as EditorWindow)?.ShowNotification(content, 4.0);
                }
            }
        }

        static void WriteLog(FinderAnalysis analysis, ErrorCatalogData data)
        {
            if (analysis.Unit == null || analysis.Unit.kind == FinderUnitKind.Rescan) return;
            try
            {
                ErrorRecordLog.Write(analysis, data, Secrets(), ProjectRoot(), DateTime.Now, LittoolInfo.Version);
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        /// <summary>
        /// A message as it may leave this PC (the translate buttons): the same masking as the error log — project and user
        /// folders, VRChat IDs, e-mail addresses, user / PC / avatar names.
        /// </summary>
        internal static string MaskForSharing(string text) => ErrorRecordLog.Mask(text, Secrets(), ProjectRoot());

        static string ProjectRoot()
        {
            string assets = Application.dataPath;
            return assets.EndsWith("/Assets", StringComparison.Ordinal) ? assets.Substring(0, assets.Length - 7) : assets;
        }

        /// <summary>Names that must not be written to the log: the user and PC names, avatar names of the open scenes.</summary>
        static List<string> Secrets()
        {
            var list = new List<string>();
            try
            {
                list.Add(Environment.UserName);
                list.Add(Environment.MachineName);
            }
            catch (Exception)
            {
                // not available: the paths are still masked
            }
            var type = FinderReflection.FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor", "VRC.SDK3A")
                       ?? FinderReflection.FindType("VRC.SDKBase.VRC_AvatarDescriptor", "VRC.SDKBase");
            if (type != null)
            {
                foreach (var o in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (o is Component c && c != null) list.Add(c.gameObject.name);
                }
            }
            foreach (var ne in NdmfBridge.ReadErrors())
            {
                if (!string.IsNullOrEmpty(ne.AvatarName)) list.Add(ne.AvatarName);
            }
            return list;
        }

        static void Problem(Exception e)
        {
            LastProblem = e.GetType().Name + ": " + e.Message;
        }

        static void RaiseChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Problem(e);
            }
        }

        static string FirstLineOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            foreach (var line in text.Split('\n'))
            {
                string s = line.Trim();
                if (s.Length > 0) return s;
            }
            return string.Empty;
        }
    }
}
