using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class PrefabScanner
    {
        public static bool IsPrefabStageOpen => PrefabStageUtility.GetCurrentPrefabStage() != null;

        public static string CurrentPrefabStagePath => PrefabStageUtility.GetCurrentPrefabStage()?.assetPath;

        public static List<DependencyLink> ScanOpenPrefabStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return null;

            var links = new List<DependencyLink>();
            var root = stage.prefabContentsRoot;
            if (root == null) return links;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                links.AddRange(SceneScanner.ScanGameObject(t.gameObject));
            return links;
        }

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

        public static List<DependencyLink> ScanPrefabAssetsUsedInLoadedScenes()
            => ScanPrefabAssetsUsedInLoadedScenes(new HashSet<ulong>());

        public static List<DependencyLink> ScanPrefabAssetsUsedInLoadedScenes(HashSet<ulong> scannedAssetIds)
        {
            var links = new List<DependencyLink>();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                    var nested = PrefabUtility.GetCorrespondingObjectFromSource(
                        PrefabUtility.GetNearestPrefabInstanceRoot(go));
                    if (nested == null) continue;
                    if (!scannedAssetIds.Add(SceneXRayCompat.IdOf(nested))) continue;
                    links.AddRange(ScanPrefab(nested));
                }
                return links;
            }

            foreach (var go in SceneXRayCompat.FindAll<GameObject>())
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (root == null) continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (source == null) continue;

                ulong id = SceneXRayCompat.IdOf(source);
                if (!scannedAssetIds.Add(id)) continue;

                links.AddRange(ScanPrefab(source));
            }

            return links;
        }

        public static List<DependencyLink> ScanReferencedPrefabAssets(
            IEnumerable<DependencyLink> links, HashSet<ulong> scannedAssetIds)
        {
            var result = new List<DependencyLink>();
            if (links == null) return result;

            foreach (var link in links)
            {
                var candidate = link.Target != null ? link.Target : link.TargetAsset as GameObject;
                if (candidate == null) continue;
                if (!EditorUtility.IsPersistent(candidate)) continue;
                if (!PrefabUtility.IsPartOfPrefabAsset(candidate)) continue;

                var root = candidate.transform.root.gameObject;
                if (!scannedAssetIds.Add(SceneXRayCompat.IdOf(root))) continue;

                result.AddRange(ScanPrefab(root));
            }

            return result;
        }

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
