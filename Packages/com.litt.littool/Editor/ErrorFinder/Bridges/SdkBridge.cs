using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// The VRChat Avatars SDK, read by reflection (LITTOOL works without the SDK). Checked against SDK 3.10.5:
    /// VRCSdkControlPanel.OnSdkPanelEnable / OnSdkPanelDisable, VRCSdkControlPanel.window,
    /// VRCSdkControlPanel.TryGetBuilder&lt;IVRCSdkAvatarBuilderApi&gt;(out builder) and the builder's events
    /// (OnSdkBuildStart / Error / Success, OnSdkUploadStart / Error / Success), the panel's Validations
    /// (private GUIErrors: Object → List&lt;Issue&gt; with issueText) and VRC_SdkBuilder.ActiveBuildType.
    /// </summary>
    internal static class SdkBridge
    {
        const string PanelTypeName = "VRCSdkControlPanel";
        const string BuilderApiTypeName = "VRC.SDKBase.Editor.IVRCSdkBuilderApi";
        const string AvatarBuilderApiTypeName = "VRC.SDK3A.Editor.IVRCSdkAvatarBuilderApi";
        const string ShowPanelMenu = "VRChat SDK/Show Control Panel";

        public enum SdkEvent
        {
            BuildStart,
            BuildError,
            BuildSuccess,
            UploadStart,
            UploadError,
            UploadSuccess,
        }

        /// <summary>Raised from <see cref="Pump"/> (main thread) for each builder event; the string is the SDK's message (may be empty).</summary>
        public static event Action<SdkEvent, string> Changed;

        static bool s_Resolved;
        static SourceState s_State = SourceState.Missing;
        static Type s_Panel, s_BuilderApi, s_AvatarBuilderApi;
        static FieldInfo s_Window, s_GuiErrors;
        static MethodInfo s_TryGetBuilder;
        static EventInfo s_PanelEnable, s_PanelDisable;
        static PropertyInfo s_SelectedAvatar, s_ActiveBuildType;
        static bool s_ActiveBuildTypeSearched;

        static bool s_Installed;
        static Delegate s_EnableHandler, s_DisableHandler;
        static object s_Builder;
        static readonly List<KeyValuePair<EventInfo, Delegate>> s_BuilderHooks = new List<KeyValuePair<EventInfo, Delegate>>();

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
                return FinderReflection.PackageVersion(s_AvatarBuilderApi ?? s_Panel);
            }
        }

        static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;
            s_Panel = FinderReflection.FindType(PanelTypeName, "VRC.SDKBase.Editor");
            s_AvatarBuilderApi = FinderReflection.FindType(AvatarBuilderApiTypeName, "VRC.SDK3A.Editor");
            s_BuilderApi = FinderReflection.FindType(BuilderApiTypeName, "VRC.SDKBase.Editor");
            if (s_Panel == null || s_AvatarBuilderApi == null)
            {
                s_State = SourceState.Missing;
                return;
            }
            s_Window = s_Panel.GetField("window", FinderReflection.AnyStatic);
            s_GuiErrors = s_Panel.GetField("GUIErrors", FinderReflection.AnyInstance);
            s_PanelEnable = s_Panel.GetEvent("OnSdkPanelEnable", FinderReflection.AnyStatic);
            s_PanelDisable = s_Panel.GetEvent("OnSdkPanelDisable", FinderReflection.AnyStatic);
            foreach (var m in s_Panel.GetMethods(FinderReflection.AnyStatic))
            {
                if (m.Name == "TryGetBuilder" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                {
                    s_TryGetBuilder = m;
                    break;
                }
            }
            s_SelectedAvatar = s_AvatarBuilderApi.GetProperty("SelectedAvatar", FinderReflection.AnyInstance);
            bool ok = s_Window != null && s_PanelEnable != null && s_TryGetBuilder != null && s_BuilderApi != null &&
                      s_BuilderApi.GetEvent("OnSdkBuildError") != null;
            s_State = ok ? SourceState.Ready : SourceState.Unreadable;
        }

        // ---------------------------------------------------------------- events

        /// <summary>Starts listening to the SDK window (it hooks the builder whenever the window opens).</summary>
        public static void Install()
        {
            Resolve();
            if (s_State != SourceState.Ready || s_Installed) return;
            try
            {
                var flags = BindingFlags.Static | BindingFlags.NonPublic;
                s_EnableHandler = Delegate.CreateDelegate(s_PanelEnable.EventHandlerType, typeof(SdkBridge).GetMethod(nameof(OnPanelEnable), flags));
                s_PanelEnable.AddEventHandler(null, s_EnableHandler);
                if (s_PanelDisable != null)
                {
                    s_DisableHandler = Delegate.CreateDelegate(s_PanelDisable.EventHandlerType, typeof(SdkBridge).GetMethod(nameof(OnPanelDisable), flags));
                    s_PanelDisable.AddEventHandler(null, s_DisableHandler);
                }
                s_Installed = true;
                EditorApplication.delayCall += HookBuilder;  // the window may already be open
            }
            catch (Exception)
            {
                s_State = SourceState.Unreadable;
            }
        }

        public static void Uninstall()
        {
            if (!s_Installed) return;
            try
            {
                if (s_EnableHandler != null) s_PanelEnable.RemoveEventHandler(null, s_EnableHandler);
                if (s_DisableHandler != null) s_PanelDisable.RemoveEventHandler(null, s_DisableHandler);
            }
            catch (Exception)
            {
                // nothing to undo
            }
            s_EnableHandler = s_DisableHandler = null;
            s_Installed = false;
            UnhookBuilder();
        }

        static void OnPanelEnable(object sender, EventArgs e) => EditorApplication.delayCall += HookBuilder;

        static void OnPanelDisable(object sender, EventArgs e) => UnhookBuilder();

        static object Builder()
        {
            if (s_Window == null || s_Window.GetValue(null) == null) return null;  // TryGetBuilder logs an error when the window is closed
            var args = new object[] { null };
            try
            {
                bool ok = (bool)s_TryGetBuilder.MakeGenericMethod(s_AvatarBuilderApi).Invoke(null, args);
                return ok ? args[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void HookBuilder()
        {
            if (!s_Installed) return;
            var builder = Builder();
            if (builder == null || ReferenceEquals(builder, s_Builder)) return;
            UnhookBuilder();
            s_Builder = builder;
            Hook("OnSdkBuildStart", nameof(OnBuildStart));
            Hook("OnSdkBuildError", nameof(OnBuildError));
            Hook("OnSdkBuildSuccess", nameof(OnBuildSuccess));
            Hook("OnSdkUploadStart", nameof(OnUploadStart));
            Hook("OnSdkUploadError", nameof(OnUploadError));
            Hook("OnSdkUploadSuccess", nameof(OnUploadSuccess));
        }

        static void Hook(string eventName, string handlerName)
        {
            var ev = s_BuilderApi.GetEvent(eventName);
            if (ev == null) return;
            try
            {
                var handler = Delegate.CreateDelegate(ev.EventHandlerType,
                    typeof(SdkBridge).GetMethod(handlerName, BindingFlags.Static | BindingFlags.NonPublic));
                ev.AddEventHandler(s_Builder, handler);
                s_BuilderHooks.Add(new KeyValuePair<EventInfo, Delegate>(ev, handler));
            }
            catch (Exception)
            {
                // this SDK version has another signature: the console text is still read
            }
        }

        static void UnhookBuilder()
        {
            if (s_Builder != null)
            {
                foreach (var hook in s_BuilderHooks)
                {
                    try
                    {
                        hook.Key.RemoveEventHandler(s_Builder, hook.Value);
                    }
                    catch (Exception)
                    {
                        // already gone with the window
                    }
                }
            }
            s_BuilderHooks.Clear();
            s_Builder = null;
        }

        // builder events come from the SDK's async tasks: they are queued and handed over on the main thread (Pump)
        static readonly System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<SdkEvent, string>> s_Queue =
            new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<SdkEvent, string>>();

        static void Raise(SdkEvent e, string message) => s_Queue.Enqueue(new KeyValuePair<SdkEvent, string>(e, message ?? string.Empty));

        /// <summary>Raises the queued builder events (call from EditorApplication.update).</summary>
        public static void Pump()
        {
            while (s_Queue.TryDequeue(out var kv)) Changed?.Invoke(kv.Key, kv.Value);
        }

        static void OnBuildStart(object sender, object target) => Raise(SdkEvent.BuildStart, null);
        static void OnBuildError(object sender, string message) => Raise(SdkEvent.BuildError, message);
        static void OnBuildSuccess(object sender, string path) => Raise(SdkEvent.BuildSuccess, null);
        static void OnUploadStart(object sender, EventArgs e) => Raise(SdkEvent.UploadStart, null);
        static void OnUploadError(object sender, string message) => Raise(SdkEvent.UploadError, message);
        static void OnUploadSuccess(object sender, string message) => Raise(SdkEvent.UploadSuccess, null);

        // ---------------------------------------------------------------- reading

        /// <summary>True while a Build &amp; Test build is running (VRC_SdkBuilder.ActiveBuildType == Test); false when unknown.</summary>
        public static bool IsTestBuild()
        {
            if (!s_ActiveBuildTypeSearched)
            {
                s_ActiveBuildTypeSearched = true;
                // VRC.SDKBase.Editor.VRC_SdkBuilder.ActiveBuildType (a static property in VRCSDKBase-Editor.dll of SDK 3.10.5)
                var t = FinderReflection.FindType("VRC.SDKBase.Editor.VRC_SdkBuilder", "VRCSDKBase-Editor")
                        ?? FinderReflection.FindTypeByName("VRC_SdkBuilder", "VRC");
                s_ActiveBuildType = t?.GetProperty("ActiveBuildType", FinderReflection.AnyStatic);
            }
            try
            {
                return s_ActiveBuildType != null && s_ActiveBuildType.GetValue(null)?.ToString() == "Test";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The red items of the SDK window's Validations with the object each one is about.</summary>
        public static List<KeyValuePair<string, Object>> ReadValidationErrors()
        {
            var list = new List<KeyValuePair<string, Object>>();
            Resolve();
            if (s_State != SourceState.Ready || s_GuiErrors == null) return list;
            try
            {
                var window = s_Window.GetValue(null);
                if (window == null || !(s_GuiErrors.GetValue(window) is IDictionary errors)) return list;
                foreach (DictionaryEntry kv in errors)
                {
                    var subject = kv.Key as Object;
                    if (!(kv.Value is IEnumerable issues)) continue;
                    foreach (var issue in issues)
                    {
                        var text = issue?.GetType().GetField("issueText", FinderReflection.AnyInstance)?.GetValue(issue) as string;
                        if (!string.IsNullOrEmpty(text)) list.Add(new KeyValuePair<string, Object>(text, subject));
                    }
                }
            }
            catch (Exception)
            {
                // a changed SDK: the build log in the console is still read
            }
            return list;
        }

        /// <summary>The avatar selected in the SDK window (null when the window is closed or unknown).</summary>
        public static GameObject SelectedAvatar()
        {
            if (s_SelectedAvatar == null) return null;
            var builder = Builder();
            try
            {
                return builder != null ? s_SelectedAvatar.GetValue(builder) as GameObject : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool OpenPanel()
        {
            return EditorApplication.ExecuteMenuItem(ShowPanelMenu);
        }
    }
}
