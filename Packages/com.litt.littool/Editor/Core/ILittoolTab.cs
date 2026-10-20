using UnityEditor;

namespace Littool
{
    /// <summary>
    /// One feature of LITTOOL. Each feature is a tab of <see cref="LittoolWindow"/>,
    /// so new tools can be added later without touching the existing ones.
    /// Implementations should be [Serializable] so their state survives script reloads.
    /// </summary>
    internal interface ILittoolTab
    {
        /// <summary>Localized tab title.</summary>
        string Title { get; }

        void OnEnable(EditorWindow window);
        void OnDisable();
        void OnGUI();

        void OnHierarchyChange();
        void OnProjectChange();
        void OnUndoRedo();
        void OnPlayModeChanged();
        void OnLanguageChanged();
    }
}
