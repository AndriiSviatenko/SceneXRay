using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
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
        public Color ImplicitColor = new Color(0.30f, 0.78f, 0.62f);
        [Range(0.5f, 5f)] public float LineWidth = 2f;

        [Header("Inspector")]
        public bool EnableInspectorIntegration = true;

        [Header("Graph")]
        public int MaxNodesInGraph = 500;

        [Header("Scanning")]
        public List<string> IgnoredComponents = new List<string> { "Transform", "RectTransform" };

        public bool ScanAssetReferences = true;

        public bool IncludeBuiltInAssets = false;

        public bool ScanImplicitDependencies = false;

        [Range(1, 32)] public int MaxImplicitTargetsPerLookup = 4;

        public bool ShowScriptNodes = true;

        [Header("Build")]

        public bool EnableBuildGuard = false;

        public bool FailBuildOnMissingReferences = false;

        public static event System.Action SettingsChanged;

        public void Save()
        {
            Save(true);
            SettingsChanged?.Invoke();
        }
    }
}
