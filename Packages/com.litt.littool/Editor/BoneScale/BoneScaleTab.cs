using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Littool.BoneScale
{
    /// <summary>
    /// "Bone Scale" tab: change the avatar's bone scales (Head, Neck, Breast, arms, legs...) from a silhouette and a
    /// list, keep the outfits dressed with Modular Avatar matching, save body shapes, and set the floor with an
    /// MA Floor Adjuster. Every change is applied at once and can be undone.
    ///
    /// IMGUI note: everything that decides how many controls are drawn is built once per event during
    /// EventType.Layout (the Snapshot), so state changed in the middle of an event never desynchronises GUILayout.
    /// The parts of this class are split by section (BoneScaleTab.*.cs).
    /// </summary>
    [Serializable]
    internal sealed partial class BoneScaleTab : ILittoolTab
    {
        const float SectionGap = 8f;

        [SerializeField] GameObject m_Avatar;
        [SerializeField] string m_SelectedKey;
        [SerializeField] int m_SelectedSide;
        [SerializeField] bool m_Symmetry = true;
        [SerializeField] List<string> m_SplitKeys = new List<string>();
        [SerializeField] List<int> m_ExpandedOutfits = new List<int>();
        [SerializeField] Transform m_BreastL;
        [SerializeField] Transform m_BreastR;
        [SerializeField] int m_PresetIndex;

        [NonSerialized] EditorWindow m_Window;
        [NonSerialized] Styles m_Styles;
        [NonSerialized] Snapshot m_Snap;
        [NonSerialized] AvatarModel m_Model;
        [NonSerialized] OutfitSet m_Outfits;
        [NonSerialized] bool m_ModelDirty = true;
        [NonSerialized] bool m_OutfitsDirty = true;
        [NonSerialized] bool m_AutoFillTried;
        [NonSerialized] string m_Message;
        [NonSerialized] MessageType m_MessageType;
        [NonSerialized] bool m_StructureQueued;

        // Outfits seen since the avatar was set or the window opened; anything else is a newly dressed outfit.
        [NonSerialized] HashSet<int> m_KnownMerges;
        [NonSerialized] int m_KnownAvatarId;
        [NonSerialized] Dictionary<int, int> m_JustMatched;

        // One slider drag = one Undo step.
        [NonSerialized] bool m_Dragging;
        [NonSerialized] int m_DragGroup;

        public string Title => L.T("tab.boneScale");

        static List<string> Entries => LittoolSettings.instance.BoneScaleEntries;

        Dictionary<int, int> JustMatched => m_JustMatched ?? (m_JustMatched = new Dictionary<int, int>());

        // ------------------------------------------------------------------------------------------------
        // ILittoolTab

        public void OnEnable(EditorWindow window)
        {
            m_Window = window;
            if (m_SplitKeys == null) m_SplitKeys = new List<string>();
            if (m_ExpandedOutfits == null) m_ExpandedOutfits = new List<int>();
            m_Model = null;
            m_Outfits = null;
            m_Snap = null;
            m_BoneList = null;
            m_ModelDirty = true;
            m_OutfitsDirty = true;
            m_KnownMerges = null;
            LittoolSettings.instance.SanitizeBoneScaleEntries();
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
            EditorApplication.update += OnEditorUpdate;
            // Remember the outfits that are there when the window opens, even if this tab is not shown yet,
            // so only outfits dressed afterwards count as new.
            EditorApplication.delayCall += InitialScan;
            MarkFloorDirty();
        }

        void InitialScan()
        {
            if (m_Window == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureScans();
        }

        public void OnDisable()
        {
            ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
            EditorApplication.update -= OnEditorUpdate;
            if (m_Dragging) EndDrag();
            SceneFlash.Stop();
            FloorMeter.Dispose();
            m_Window = null;
        }

        public void OnHierarchyChange()
        {
            OnStructureChanged();
        }

        public void OnProjectChange()
        {
            LittoolImages.ClearCache();
            m_ModelDirty = true;
            Repaint();
        }

        public void OnUndoRedo()
        {
            // Outfit on/off and bone values may have changed; the list itself is not part of Undo.
            m_ModelDirty = true;
            m_OutfitsDirty = true;
            JustMatched.Clear();
            MarkFloorDirty();
            Repaint();
        }

        public void OnPlayModeChanged()
        {
            if (m_Dragging) EndDrag();
            SceneFlash.Stop();
            m_ModelDirty = true;
            m_OutfitsDirty = true;
            m_KnownMerges = null;
            m_Floor = null;
            MarkFloorDirty();
            Repaint();
        }

        public void OnLanguageChanged()
        {
            m_Styles = null;
            m_Message = null;
            m_PickerState = null;
            Repaint();
        }

        // ------------------------------------------------------------------------------------------------
        // GUI

        public void OnGUI()
        {
            if (m_Styles == null || !m_Styles.IsValid) m_Styles = new Styles();
            if (Event.current.type == EventType.Layout || m_Snap == null) BuildSnapshot();
            var snap = m_Snap;

            if (snap.Playing) EditorGUILayout.HelpBox(L.T("bs.playMode"), MessageType.Warning);

            using (new EditorGUI.DisabledScope(snap.Playing))
            {
                DrawAvatar(snap);
                if (snap.AvatarState == AvatarState.Ok)
                {
                    GUILayout.Space(10f);
                    DrawOutfits(snap);
                    GUILayout.Space(SectionGap);
                    DrawSeparator();
                    GUILayout.Space(6f);
                    DrawBones(snap);
                    GUILayout.Space(SectionGap);
                    DrawSeparator();
                    GUILayout.Space(6f);
                    DrawPresets(snap);
                    if (!string.IsNullOrEmpty(snap.Message))
                    {
                        GUILayout.Space(4f);
                        EditorGUILayout.HelpBox(snap.Message, snap.MessageType);
                    }
                    GUILayout.Space(SectionGap);
                    DrawSeparator();
                    GUILayout.Space(6f);
                    DrawFloor(snap);
                }
            }
            GUILayout.Space(6f);

            // The slider releases the mouse (or a key press changed it): close this drag's Undo step.
            if (m_Dragging && GUIUtility.hotControl == 0) EndDrag();
        }

        void BuildSnapshot()
        {
            var snap = new Snapshot { Playing = EditorApplication.isPlayingOrWillChangePlaymode };
            if (!snap.Playing && m_Avatar == null && !m_AutoFillTried)
            {
                m_AutoFillTried = true;
                TryAutoFillAvatar();
            }
            EnsureScans();
            snap.AvatarState = m_Model.IsOk ? AvatarState.Ok : (m_Model.State == AvatarState.Ok ? AvatarState.None : m_Model.State);
            snap.AvatarInfo = AvatarInfo();
            snap.Message = m_Message;
            snap.MessageType = m_MessageType;
            if (m_Model.IsOk)
            {
                BuildOutfitSnapshot(snap);
                BuildBoneSnapshot(snap);
                BuildPresetSnapshot(snap);
                BuildFloorSnapshot(snap);
            }
            m_Snap = snap;
        }

        void EnsureScans()
        {
            // (The avatar object may also have been deleted: then m_Avatar compares equal to null.)
            bool avatarGone = m_Model != null && m_Model.State != AvatarState.None && m_Avatar == null;
            if (m_Model == null || m_ModelDirty || avatarGone || !ReferenceEquals(m_Model.Root, m_Avatar))
            {
                m_Model = AvatarModel.Build(m_Avatar, Entries, m_BreastL, m_BreastR);
                m_ModelDirty = false;
                m_OutfitsDirty = true;
            }
            // Keep the outfit pairs stable during a slider drag; rescan when it ends.
            if (m_Outfits == null || (m_OutfitsDirty && !m_Dragging))
            {
                m_Outfits = OutfitSet.Scan(m_Model);
                m_OutfitsDirty = false;
                InitKnownMerges();
            }
        }

        // ---- Avatar -----------------------------------------------------------------------------------------

        void DrawAvatar(Snapshot snap)
        {
            GUILayout.Label(L.T("bs.avatar.header"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var avatar = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Avatar", L.T("bs.avatar.tooltip")), m_Avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) SetAvatar(avatar);

            switch (snap.AvatarState)
            {
                case AvatarState.None:
                    EditorGUILayout.HelpBox(L.T("bs.avatar.none"), MessageType.Info);
                    break;
                case AvatarState.PrefabAsset:
                    EditorGUILayout.HelpBox(L.T("bs.avatar.prefabAsset"), MessageType.Error);
                    break;
                case AvatarState.NoAnimator:
                    EditorGUILayout.HelpBox(L.T("bs.avatar.noAnimator"), MessageType.Error);
                    break;
                case AvatarState.NotHumanoid:
                    EditorGUILayout.HelpBox(L.T("bs.avatar.notHumanoid"), MessageType.Error);
                    break;
                default:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 2f);
                        GUILayout.Label(snap.AvatarInfo, m_Styles.MiniDimWrap, GUILayout.MinWidth(0f));
                    }
                    break;
            }
        }

        string AvatarInfo()
        {
            if (m_Model == null || !m_Model.IsOk) return string.Empty;
            var breast = m_Model.FindGroup("breast");
            Transform left, right;
            bool manual = false;
            if (breast != null)
            {
                left = breast.Slots[0].Transform;
                right = breast.Slots[1].Transform;
                manual = breast.Slots[0].Kind == SlotKind.Manual || breast.Slots[1].Kind == SlotKind.Manual;
            }
            else
            {
                left = m_Model.BreastLByName;
                right = m_Model.BreastRByName;
            }
            if (left == null && right == null) return L.T("bs.avatar.infoNoBreast");
            string names = (left != null ? left.name : "—") + ", " + (right != null ? right.name : "—");
            return L.F(manual ? "bs.avatar.infoManual" : "bs.avatar.info", names);
        }

        void SetAvatar(GameObject avatar)
        {
            if (avatar == m_Avatar) return;
            m_Avatar = avatar;
            m_BreastL = null;
            m_BreastR = null;
            m_Message = null;
            m_KnownMerges = null;
            m_Floor = null;
            JustMatched.Clear();
            m_ModelDirty = true;
            m_OutfitsDirty = true;
            SceneFlash.Stop();
            MarkFloorDirty();
            Repaint();
        }

        /// <summary>When nothing is assigned and the open scenes hold exactly one avatar (VRC Avatar Descriptor), use it.</summary>
        void TryAutoFillAvatar()
        {
            Type descriptor = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    descriptor = assembly.GetType("VRC.SDKBase.VRC_AvatarDescriptor", false)
                                 ?? assembly.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor", false);
                }
                catch (Exception)
                {
                    descriptor = null;
                }
                if (descriptor != null) break;
            }
            if (descriptor == null) return;

            var found = UnityEngine.Object.FindObjectsByType(descriptor, FindObjectsSortMode.None);
            GameObject only = null;
            foreach (var obj in found)
            {
                if (!(obj is Component component) || EditorUtility.IsPersistent(component)) continue;
                if (only != null) return; // more than one avatar: let the user choose
                only = component.gameObject;
            }
            if (only != null) m_Avatar = only;
        }

        // ---- Structure changes and newly dressed outfits ----------------------------------------------------

        void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeScene:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        OnStructureChanged();
                        return;
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        if (!m_Dragging) MarkFloorDirty(); // e.g. an outfit switched on in the Hierarchy
                        break;
                }
            }
        }

        void OnStructureChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // avatar emulators change the hierarchy constantly
            m_ModelDirty = true;
            m_OutfitsDirty = true;
            MarkFloorDirty();
            if (!m_StructureQueued)
            {
                m_StructureQueued = true;
                EditorApplication.delayCall += ProcessStructureChange;
            }
            Repaint();
        }

        void ProcessStructureChange()
        {
            m_StructureQueued = false;
            if (m_Window == null || EditorApplication.isPlayingOrWillChangePlaymode || m_Dragging) return;
            EnsureScans();
            if (m_Model == null || !m_Model.IsOk || m_Outfits == null || m_KnownMerges == null) return;

            var fresh = new List<Outfit>();
            foreach (var outfit in m_Outfits.Outfits)
            {
                foreach (var merge in outfit.Merges)
                {
                    if (merge == null || m_KnownMerges.Contains(merge.GetInstanceID())) continue;
                    fresh.Add(outfit);
                    break;
                }
            }
            m_KnownMerges.UnionWith(m_Outfits.MergeIds);
            if (fresh.Count > 0 && LittoolSettings.instance.AutoMatchNewOutfits) AutoMatch(fresh);
            Repaint();
        }

        void InitKnownMerges()
        {
            if (m_Model == null || !m_Model.IsOk || m_Outfits == null)
            {
                m_KnownMerges = null;
                return;
            }
            int avatarId = m_Avatar.GetInstanceID();
            if (m_KnownMerges != null && m_KnownAvatarId == avatarId) return;
            m_KnownMerges = new HashSet<int>(m_Outfits.MergeIds);
            m_KnownAvatarId = avatarId;
        }

        HashSet<Transform> ListedAvatarBones()
        {
            var set = new HashSet<Transform>();
            if (m_Model == null) return set;
            foreach (var group in m_Model.Groups)
            {
                foreach (var slot in group.Slots)
                {
                    if (slot.Transform != null) set.Add(slot.Transform);
                }
            }
            return set;
        }

        // ---- Undo grouping ----------------------------------------------------------------------------------

        static int BeginUndo(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }

        void BeginDrag()
        {
            if (m_Dragging) return;
            m_Dragging = true;
            m_DragGroup = BeginUndo(ScaleEditor.UndoName);
            ScaleEditor.BeginDrag();
        }

        void EndDrag()
        {
            if (!m_Dragging) return;
            m_Dragging = false;
            ScaleEditor.EndDrag();
            Undo.CollapseUndoOperations(m_DragGroup);
            Undo.IncrementCurrentGroup();
            MarkFloorDirty();
            Repaint();
        }

        // ---- Shared helpers ---------------------------------------------------------------------------------

        void SetMessage(string message, MessageType type)
        {
            m_Message = message;
            m_MessageType = type;
            Repaint();
        }

        void Repaint()
        {
            if (m_Window != null) m_Window.Repaint();
        }

        void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, m_Styles.Edge);
        }

        static void Circle(Rect rect, Color color, float border = 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, border, rect.width * 0.5f);
        }

        static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        GUIStyle MiniTone(Tone tone) => m_Styles.Mini[(int)tone];

        GUIStyle MiniToneRight(Tone tone) => m_Styles.MiniRight[(int)tone];

        // ---- Snapshot -------------------------------------------------------------------------------------

        internal enum Tone
        {
            Normal = 0,
            Dim = 1,
            Accent = 2,
            Ok = 3,
            Warn = 4,
        }

        sealed class Snapshot
        {
            public bool Playing;
            public AvatarState AvatarState;
            public string AvatarInfo;
            public string Message;
            public MessageType MessageType;

            public readonly OutfitView Outfits = new OutfitView();
            public readonly BoneView Bones = new BoneView();
            public readonly PresetView Presets = new PresetView();
            public readonly FloorView Floor = new FloorView();
        }

        // ---- Styles -----------------------------------------------------------------------------------------

        sealed class Styles
        {
            readonly bool m_ProSkin;

            public readonly GUIStyle[] Mini = new GUIStyle[5];
            public readonly GUIStyle[] MiniRight = new GUIStyle[5];
            public readonly GUIStyle MiniDimWrap;
            public readonly GUIStyle MiniWarnWrap;
            public readonly GUIStyle StatusText;
            public readonly GUIStyle MiniCenter;
            public readonly GUIStyle LabelDim;
            public readonly GUIStyle PanelTitle;
            public readonly GUIStyle Badge;
            public readonly GUIStyle NameField;
            public readonly Color Edge;
            public readonly Color BoxBack;
            public readonly Color Figure;
            public readonly Color DotRing;
            public readonly Color Accent;
            public readonly Color Halo;
            public readonly Color BadgeBack;
            public readonly Color Hover;
            public readonly Texture ObjectIcon;
            public readonly Texture WarnIcon;
            public readonly Color OkColor;
            public readonly GUIContent RefreshContent;

            public bool IsValid => m_ProSkin == EditorGUIUtility.isProSkin;

            public Styles()
            {
                m_ProSkin = EditorGUIUtility.isProSkin;
                var normal = m_ProSkin ? new Color32(0xD2, 0xD2, 0xD2, 0xFF) : new Color32(0x20, 0x20, 0x20, 0xFF);
                var dim = m_ProSkin ? new Color32(0xA8, 0xA8, 0xA8, 0xFF) : new Color32(0x58, 0x58, 0x58, 0xFF);
                var accent = m_ProSkin ? new Color32(0x7B, 0xAE, 0xFA, 0xFF) : new Color32(0x1F, 0x5F, 0xBF, 0xFF);
                var ok = m_ProSkin ? new Color32(0x8F, 0xC9, 0x8F, 0xFF) : new Color32(0x2E, 0x7D, 0x32, 0xFF);
                var warn = m_ProSkin ? new Color32(0xE5, 0xB9, 0x4E, 0xFF) : new Color32(0x8A, 0x5A, 0x00, 0xFF);
                var tones = new Color[] { normal, dim, accent, ok, warn };
                for (int i = 0; i < tones.Length; i++)
                {
                    Mini[i] = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
                    Mini[i].normal.textColor = tones[i];
                    MiniRight[i] = new GUIStyle(Mini[i]) { alignment = TextAnchor.MiddleRight };
                }
                MiniDimWrap = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                MiniDimWrap.normal.textColor = dim;
                MiniWarnWrap = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                MiniWarnWrap.normal.textColor = warn;
                StatusText = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11 };
                MiniCenter = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                MiniCenter.normal.textColor = dim;
                LabelDim = new GUIStyle(EditorStyles.label);
                LabelDim.normal.textColor = dim;
                PanelTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                Badge = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 0, 0) };
                Badge.normal.textColor = m_ProSkin ? new Color32(0xC9, 0xC9, 0xC9, 0xFF) : new Color32(0x30, 0x30, 0x30, 0xFF);
                NameField = new GUIStyle(EditorStyles.textField);

                Edge = m_ProSkin ? new Color32(0x23, 0x23, 0x23, 0xFF) : new Color32(0x99, 0x99, 0x99, 0xFF);
                BoxBack = m_ProSkin ? new Color32(0x30, 0x30, 0x30, 0xFF) : new Color32(0xD8, 0xD8, 0xD8, 0xFF);
                Figure = m_ProSkin ? new Color32(0x47, 0x47, 0x47, 0xFF) : new Color32(0xB4, 0xB4, 0xB4, 0xFF);
                DotRing = m_ProSkin ? new Color32(0xB4, 0xB4, 0xB4, 0xFF) : new Color32(0x6A, 0x6A, 0x6A, 0xFF);
                Accent = m_ProSkin ? new Color32(0x5B, 0x9B, 0xF0, 0xFF) : new Color32(0x2F, 0x6F, 0xD0, 0xFF);
                Halo = m_ProSkin ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.1f, 0.1f, 0.1f, 0.75f);
                BadgeBack = m_ProSkin ? new Color32(0x47, 0x47, 0x47, 0xFF) : new Color32(0xC4, 0xC4, 0xC4, 0xFF);
                Hover = m_ProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f);

                ObjectIcon = EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
                var warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
                WarnIcon = warnIcon != null ? warnIcon.image : null;
                OkColor = ok;
                var refresh = EditorGUIUtility.IconContent("Refresh");
                RefreshContent = new GUIContent(refresh != null ? refresh.image : null, L.T("bs.floor.refresh"));
                if (RefreshContent.image == null) RefreshContent.text = "↻";
            }
        }
    }
}
