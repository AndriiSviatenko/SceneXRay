using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneXRay.Editor.Core
{
    public static class XRayLayoutStore
    {
        private const string Folder = "UserSettings/SceneXRay/Layouts";

        [Serializable]
        private class Entry
        {
            public string Key;
            public float X;
            public float Y;
        }

        [Serializable]
        private class LayoutData
        {
            public string Scene;
            public string SavedAt;
            public List<Entry> Nodes = new();
        }

        public static string CurrentSceneKey
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(scene.path))
                {
                    string guid = AssetDatabase.AssetPathToGUID(scene.path);
                    if (!string.IsNullOrEmpty(guid)) return guid;
                }
                return string.IsNullOrEmpty(scene.name) ? "Untitled" : "name_" + Sanitize(scene.name);
            }
        }

        public static string CurrentSceneName
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                return string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            }
        }

        private static string PathFor(string sceneKey) => $"{Folder}/{Sanitize(sceneKey)}.json";

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        public static bool HasLayout(string sceneKey) =>
            !string.IsNullOrEmpty(sceneKey) && File.Exists(PathFor(sceneKey));

        public static void Save(string sceneKey, IReadOnlyDictionary<string, Vector2> positions)
        {
            if (string.IsNullOrEmpty(sceneKey) || positions == null) return;

            var data = new LayoutData
            {
                Scene = CurrentSceneName,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            foreach (var kv in positions)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                data.Nodes.Add(new Entry { Key = kv.Key, X = kv.Value.x, Y = kv.Value.y });
            }

            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(sceneKey), JsonUtility.ToJson(data, true));
        }

        public static Dictionary<string, Vector2> Load(string sceneKey)
        {
            var result = new Dictionary<string, Vector2>();
            if (!HasLayout(sceneKey)) return result;

            try
            {
                var data = JsonUtility.FromJson<LayoutData>(File.ReadAllText(PathFor(sceneKey)));
                if (data?.Nodes == null) return result;
                foreach (var e in data.Nodes)
                    if (!string.IsNullOrEmpty(e.Key))
                        result[e.Key] = new Vector2(e.X, e.Y);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SceneXRay: could not read the saved graph layout — {ex.Message}");
            }
            return result;
        }

        public static void Delete(string sceneKey)
        {
            if (!HasLayout(sceneKey)) return;
            try { File.Delete(PathFor(sceneKey)); }
            catch (Exception ex) { Debug.LogWarning($"SceneXRay: could not delete the saved layout — {ex.Message}"); }
        }
    }
}
