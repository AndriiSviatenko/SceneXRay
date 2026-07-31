using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class SceneDiffUtility
    {
        public class SceneDiffResult
        {
            public List<DependencyLink> OnlyInSceneA = new List<DependencyLink>();
            public List<DependencyLink> OnlyInSceneB = new List<DependencyLink>();
            public List<DependencyLink> Common = new List<DependencyLink>();
        }

        public static SceneDiffResult CompareScenes(string pathA, string pathB)
        {
            var linksA = ScanScene(pathA);
            var linksB = ScanScene(pathB);

            var result = new SceneDiffResult();
            // GlobalObjectId-based keys — stable across sessions and between scene loads.
            string Key(DependencyLink l) => $"{l.SourceGlobalId}:{l.TargetGlobalId}:{l.SourcePropertyName}";
            var setA = new HashSet<string>(linksA.Select(Key));
            var setB = new HashSet<string>(linksB.Select(Key));

            result.OnlyInSceneA = linksA.Where(l => !setB.Contains(Key(l))).ToList();
            result.OnlyInSceneB = linksB.Where(l => !setA.Contains(Key(l))).ToList();
            result.Common = linksA.Where(l => setB.Contains(Key(l))).ToList();

            return result;
        }

        private static List<DependencyLink> ScanScene(string path)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var links = new List<DependencyLink>();
            foreach (var go in scene.GetRootGameObjects())
            {
                links.AddRange(SceneScanner.ScanGameObject(go));
                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                    links.AddRange(SceneScanner.ScanGameObject(child.gameObject));
            }
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            return links;
        }
    }
}