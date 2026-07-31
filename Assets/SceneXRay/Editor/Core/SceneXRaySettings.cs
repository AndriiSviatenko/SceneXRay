using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// Project-wide SceneXRay settings persisted in ProjectSettings (not in Assets, not in VCS-visible Resources).
    /// Edited through Project Settings &gt; SceneXRay (see <see cref="SceneXRaySettingsProvider"/>).
    /// </summary>
    [FilePath("ProjectSettings/SceneXRaySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class SceneXRaySettings : ScriptableSingleton<SceneXRaySettings>
    {
        [Header("Overlay")]
        public bool EnableXRayOverlay = true;
        public bool AnimateOverlay = false;

        [Header("Colors (Scene overlay + graph edges)")]
        public Color DirectColor = new Color(0.24f, 0.58f, 0.88f);
        public Color EventColor = new Color(0.95f, 0.67f, 0.22f);
        public Color MissingColor = new Color(0.9f, 0.32f, 0.32f);
        public Color AssetColor = new Color(0.65f, 0.49f, 0.98f);
        [Range(0.5f, 5f)] public float LineWidth = 2f;

        [Header("Inspector")]
        public bool EnableInspectorIntegration = true;

        [Header("Graph")]
        public int MaxNodesInGraph = 500;

        [Header("Scanning")]
        public List<string> IgnoredComponents = new List<string> { "Transform", "RectTransform" };
        /// <summary>Record references to project assets: materials, meshes, clips, ScriptableObjects…</summary>
        public bool ScanAssetReferences = true;
        /// <summary>Also record Unity's built-in resources (Default-Material, built-in meshes/fonts).</summary>
        public bool IncludeBuiltInAssets = false;

        [Header("Build")]
        public bool EnableBuildGuard = true;
        public bool FailBuildOnMissingReferences = false;

        /// <summary>Raised after the settings asset is persisted (SettingsProvider calls Save on edit).</summary>
        public static event System.Action SettingsChanged;

        public void Save()
        {
            Save(true);
            SettingsChanged?.Invoke();
        }
    }
}
