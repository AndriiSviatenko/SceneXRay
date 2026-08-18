using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class SceneDiffUtility
    {
        public class DiffEntry
        {
            public string SourcePath;
            public string TargetPath;
            public string SourceName;
            public string TargetName;
            public string ComponentName;
            public string PropertyName;
            public string MethodName;
            public LinkType LinkType;

            public string Key => $"{SourcePath}|{ComponentName}|{PropertyName}|{MethodName}|{LinkType}|{TargetPath}";

            public string Describe()
            {
                string member = string.IsNullOrEmpty(MethodName)
                    ? $"{ComponentName}.{PropertyName}"
                    : $"{ComponentName}.{PropertyName}() → {MethodName}";
                return $"{SourcePath} → {TargetPath}   ({member})";
            }
        }

        public class SceneDiffResult
        {
            public List<DiffEntry> OnlyInSceneA = new List<DiffEntry>();
            public List<DiffEntry> OnlyInSceneB = new List<DiffEntry>();
            public List<DiffEntry> Common = new List<DiffEntry>();
        }

        public static SceneDiffResult CompareScenes(string pathA, string pathB)
        {
            var entriesA = ScanScene(pathA);
            var entriesB = ScanScene(pathB);

            var setA = new HashSet<string>(entriesA.Select(e => e.Key));
            var setB = new HashSet<string>(entriesB.Select(e => e.Key));

            return new SceneDiffResult
            {
                OnlyInSceneA = entriesA.Where(e => !setB.Contains(e.Key)).ToList(),
                OnlyInSceneB = entriesB.Where(e => !setA.Contains(e.Key)).ToList(),
                Common = entriesA.Where(e => setB.Contains(e.Key)).ToList()
            };
        }

        private static List<DiffEntry> ScanScene(string path)
        {
            var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            var scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            var entries = new Dictionary<string, DiffEntry>();
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        foreach (var link in SceneScanner.ScanGameObject(t.gameObject))
                        {
                            var entry = ToEntry(link);
                            if (!entries.ContainsKey(entry.Key))
                                entries.Add(entry.Key, entry);
                        }
                    }
                }
            }
            finally
            {
                if (!alreadyOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return entries.Values.ToList();
        }

        private static DiffEntry ToEntry(DependencyLink link)
        {
            return new DiffEntry
            {
                SourcePath = HierarchyPath(link.Source),
                SourceName = link.Source != null ? link.Source.name : "<none>",
                TargetPath = TargetPathOf(link),
                TargetName = link.Target != null ? link.Target.name
                    : link.TargetAsset != null ? link.TargetAsset.name : "<missing>",
                ComponentName = link.SourceComponentName,
                PropertyName = link.SourcePropertyName,
                MethodName = link.TargetMethodName,
                LinkType = link.LinkType
            };
        }

        private static string TargetPathOf(DependencyLink link)
        {
            if (link.Target != null) return HierarchyPath(link.Target);
            if (link.TargetAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(link.TargetAsset);
                return string.IsNullOrEmpty(assetPath)
                    ? $"{link.TargetAsset.GetType().Name}:{link.TargetAsset.name}"
                    : assetPath;
            }
            return "<missing>";
        }

        private static string HierarchyPath(GameObject go)
        {
            if (go == null) return "<none>";
            var t = go.transform;
            var stack = new List<string>();
            while (t != null)
            {
                stack.Add(t.name);
                t = t.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }
    }
}
