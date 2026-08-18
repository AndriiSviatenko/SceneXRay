using SceneXRay.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class SceneXRaySettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/SceneXRay", SettingsScope.Project)
            {
                label = "SceneXRay",
                keywords = new[] { "xray", "dependency", "scene", "overlay", "graph", "missing", "reference" },
                guiHandler = _ =>
                {
                    var settings = SceneXRaySettings.instance;
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.LabelField("Overlay", EditorStyles.boldLabel);
                    settings.EnableXRayOverlay = EditorGUILayout.Toggle("Enable Scene Overlay", settings.EnableXRayOverlay);
                    settings.AnimateOverlay = EditorGUILayout.Toggle("Animate Overlay", settings.AnimateOverlay);

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Applies to both the Scene View overlay and the graph edges.", MessageType.None);
                    settings.DirectColor = EditorGUILayout.ColorField("Direct", settings.DirectColor);
                    settings.EventColor = EditorGUILayout.ColorField("UnityEvent", settings.EventColor);
                    settings.MissingColor = EditorGUILayout.ColorField("Missing", settings.MissingColor);
                    settings.AssetColor = EditorGUILayout.ColorField("Asset", settings.AssetColor);
                    settings.ImplicitColor = EditorGUILayout.ColorField("In Code", settings.ImplicitColor);
                    settings.LineWidth = EditorGUILayout.Slider("Line Width", settings.LineWidth, 0.5f, 5f);

                    if (GUILayout.Button("Reset Colors To Defaults", GUILayout.Width(200)))
                    {
                        settings.DirectColor = new Color(0.24f, 0.58f, 0.88f);
                        settings.EventColor = new Color(0.95f, 0.67f, 0.22f);
                        settings.MissingColor = new Color(0.9f, 0.32f, 0.32f);
                        settings.AssetColor = new Color(0.65f, 0.49f, 0.98f);
                        settings.ImplicitColor = new Color(0.30f, 0.78f, 0.62f);
                        settings.LineWidth = 2f;
                        GUI.changed = true;
                    }

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
                    settings.EnableInspectorIntegration = EditorGUILayout.Toggle("Inspector Integration", settings.EnableInspectorIntegration);

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Graph", EditorStyles.boldLabel);
                    settings.MaxNodesInGraph = EditorGUILayout.IntField("Max Nodes In Graph", settings.MaxNodesInGraph);

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Ignored Components (comma-separated):");
                    string joined = string.Join(",", settings.IgnoredComponents);
                    string edited = EditorGUILayout.TextField(joined);
                    if (edited != joined)
                    {
                        settings.IgnoredComponents.Clear();
                        foreach (var part in edited.Split(','))
                        {
                            var trimmed = part.Trim();
                            if (trimmed.Length > 0) settings.IgnoredComponents.Add(trimmed);
                        }
                    }

                    settings.ScanAssetReferences = EditorGUILayout.Toggle(
                        new GUIContent("Scan Asset References",
                            "Record links to project assets: materials, meshes, clips, ScriptableObjects, textures…"),
                        settings.ScanAssetReferences);
                    using (new EditorGUI.DisabledScope(!settings.ScanAssetReferences))
                    {
                        settings.IncludeBuiltInAssets = EditorGUILayout.Toggle(
                            new GUIContent("Include Built-in Assets",
                                "Also record Unity's built-in resources (Default-Material, built-in meshes/fonts). Noisy."),
                            settings.IncludeBuiltInAssets);
                    }

                    settings.ScanImplicitDependencies = EditorGUILayout.Toggle(
                        new GUIContent("Scan Code Dependencies",
                            "Find dependencies that exist only in code: FindAnyObjectByType<T>(), " +
                            "GetComponent<T>(), GameObject.Find(\"…\"). Reads the source of every script " +
                            "present in the scene, so it costs more than a scene-data scan."),
                        settings.ScanImplicitDependencies);
                    using (new EditorGUI.DisabledScope(!settings.ScanImplicitDependencies))
                    {
                        settings.MaxImplicitTargetsPerLookup = EditorGUILayout.IntSlider(
                            new GUIContent("Max Targets Per Lookup",
                                "A code lookup matching more objects than this draws nothing. " +
                                "GetComponent<Transform>() matches everything, and an edge to " +
                                "everything is not information."),
                            settings.MaxImplicitTargetsPerLookup, 1, 32);

                        settings.ShowScriptNodes = EditorGUILayout.Toggle(
                            new GUIContent("Show Script Nodes",
                                "Draw the class a code lookup asks for as its own graph node, not " +
                                "just edges to the objects that carry it."),
                            settings.ShowScriptNodes);
                    }

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
                    settings.EnableBuildGuard = EditorGUILayout.Toggle("Enable Build Guard", settings.EnableBuildGuard);
                    settings.FailBuildOnMissingReferences = EditorGUILayout.Toggle("Fail Build On Missing Refs", settings.FailBuildOnMissingReferences);

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Language", EditorStyles.boldLabel);
                    var lang = (UI.XRayLocalization.Language)EditorGUILayout.EnumPopup("Editor Language", UI.XRayLocalization.CurrentLanguage);
                    if (lang != UI.XRayLocalization.CurrentLanguage)
                        UI.XRayLocalization.SetLanguage(lang);

                    if (EditorGUI.EndChangeCheck())
                        settings.Save();
                }
            };
        }
    }
}
