using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class PrefabScanner
    {
        public static List<DependencyLink> ScanPrefab(GameObject prefabInstance)
        {
            var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstance);
            if (prefabRoot == null) prefabRoot = prefabInstance;

            var allChildren = prefabRoot.GetComponentsInChildren<Transform>(true);
            var links = new List<DependencyLink>();
            foreach (var t in allChildren)
                links.AddRange(SceneScanner.ScanGameObject(t.gameObject));
            return links;
        }

        /// <summary>
        /// Scans unique prefab assets that have instances in currently loaded scenes.
        /// Prefer this over <see cref="ScanAllPrefabs"/> for interactive graph use.
        /// </summary>
        public static List<DependencyLink> ScanPrefabAssetsUsedInLoadedScenes()
        {
            var links = new List<DependencyLink>();
            var scannedAssetIds = new HashSet<int>();

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (root == null) continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (source == null) continue;

                int id = source.GetInstanceID();
                if (!scannedAssetIds.Add(id)) continue;

                links.AddRange(ScanPrefab(source));
            }

            return links;
        }

        /// <summary>Full-project prefab scan — heavyweight; use only for explicit CLI / export.</summary>
        public static Dictionary<string, List<DependencyLink>> ScanAllPrefabs()
        {
            var result = new Dictionary<string, List<DependencyLink>>();
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (guids.Length > 20)
                    EditorUtility.DisplayProgressBar("SceneXRay", $"Scanning prefab {path}", (float)i / guids.Length);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    result[path] = ScanPrefab(prefab);
            }

            if (guids.Length > 20)
                EditorUtility.ClearProgressBar();

            return result;
        }
    }
}
