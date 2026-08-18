using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class CommandLine
    {
        [Serializable]
        private class LinkDto
        {
            public string Source;
            public string Target;
            public string Property;
            public string Method;
            public string Component;
            public string LinkType;
        }

        [Serializable]
        private class LinkDtoList
        {
            public System.Collections.Generic.List<LinkDto> links = new();
        }

        public static void ScanScene()
        {
            string scenePath = GetArg("-scene");
            if (string.IsNullOrEmpty(scenePath))
                scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            if (!File.Exists(scenePath))
            {
                Debug.LogError($"SceneXRay: scene not found: {scenePath}");
                EditorApplication.Exit(1);
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var links = SceneScanner.ScanAllGameObjects();
            var errors = QualityGateManager.Validate(links);

            var dtos = links.Select(l => new LinkDto
            {
                Source = l.Source != null ? GetPath(l.Source) : null,
                Target = l.Target != null ? GetPath(l.Target) : (l.TargetAsset != null ? l.TargetAsset.name : null),
                Property = l.SourcePropertyName,
                Method = l.TargetMethodName,
                Component = l.SourceComponentName,
                LinkType = l.LinkType.ToString()
            }).ToList();

            string outputPath = GetArg("-output") ?? "dependencies.json";
            File.WriteAllText(outputPath, JsonUtility.ToJson(new LinkDtoList { links = dtos }, true));

            Debug.Log($"SceneXRay: scanned {links.Count} dependencies. Errors: {errors.Count}");
            foreach (var err in errors) Debug.LogWarning(err);

            EditorApplication.Exit(errors.Any() ? 1 : 0);
        }

        private static string GetPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name && i + 1 < args.Length)
                    return args[i + 1];
            return null;
        }
    }
}
