using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// Full-project reference search: walks every build scene and every prefab.
    /// Heavyweight by design — intended ONLY for the Global Search window (user-triggered),
    /// never from inspector or overlay code paths. Shows a progress bar while working.
    /// </summary>
    public static class GlobalReferenceSearch
    {
        public static List<DependencyLink> FindReferences(GameObject target)
        {
            return CollectAllLinks().Where(l => l.Target == target).ToList();
        }

        /// <summary>Scans loaded scenes, other build scenes (opened additively) and all prefabs.</summary>
        public static List<DependencyLink> CollectAllLinks()
        {
            var allLinks = new List<DependencyLink>();
            try
            {
                // 1. Currently loaded scenes — reuse the background index when it is fresh.
                if (XRayReferenceIndex.IsReady)
                {
                    allLinks.AddRange(XRayReferenceIndex.AllLinks);
                }
                else
                {
                    EditorUtility.DisplayProgressBar("SceneXRay Global Search", "Scanning loaded scenes...", 0f);
                    allLinks.AddRange(SceneScanner.ScanAllGameObjects());
                }

                var loadedPaths = new HashSet<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    loadedPaths.Add(SceneManager.GetSceneAt(i).path);

                // 2. Remaining build scenes, opened additively one by one.
                var pending = EditorBuildSettings.scenes
                    .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && !loadedPaths.Contains(s.path))
                    .ToList();
                for (int i = 0; i < pending.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("SceneXRay Global Search",
                        $"Scanning scene {pending[i].path}", 0.1f + 0.6f * i / Mathf.Max(1, pending.Count));
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        pending[i].path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    try
                    {
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                                allLinks.AddRange(SceneScanner.ScanGameObject(child.gameObject));
                        }
                    }
                    finally
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                    }
                }

                // 3. Prefabs.
                EditorUtility.DisplayProgressBar("SceneXRay Global Search", "Scanning prefabs...", 0.8f);
                foreach (var pair in PrefabScanner.ScanAllPrefabs())
                    allLinks.AddRange(pair.Value);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return allLinks;
        }
    }
}
