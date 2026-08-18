using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    [Serializable]
    public sealed class BookmarkEntry
    {
        public string Id;
        public string Name;
        public string ScenePath;
    }

    public static class XRayBookmarks
    {
        private const string FilePath = "Library/SceneXRay/bookmarks.json";

        private static List<BookmarkEntry> _entries;
        private static bool _loaded;

        public static event Action Changed;

        public static IReadOnlyList<BookmarkEntry> All
        {
            get
            {
                EnsureLoaded();
                return _entries;
            }
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _entries.Count;
            }
        }

        public static bool Contains(GameObject go)
        {
            if (go == null) return false;
            string id = DependencyLink.GetGlobalId(go);
            return !string.IsNullOrEmpty(id) && ContainsId(id);
        }

        public static bool ContainsId(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id) return true;
            }
            return false;
        }

        public static bool TryAdd(GameObject go, out string message)
        {
            message = null;
            if (go == null)
            {
                message = "Select a GameObject first.";
                return false;
            }

            string id = DependencyLink.GetGlobalId(go);
            if (string.IsNullOrEmpty(id))
            {
                message = "Cannot bookmark this object (unsaved scene?).";
                return false;
            }

            EnsureLoaded();
            if (ContainsId(id))
            {
                message = $"Already bookmarked: {go.name}";
                return false;
            }

            _entries.Add(new BookmarkEntry
            {
                Id = id,
                Name = go.name,
                ScenePath = go.scene.path ?? ""
            });
            Persist();
            message = $"Bookmarked: {go.name}";
            return true;
        }

        public static bool Remove(string id)
        {
            EnsureLoaded();
            int removed = _entries.RemoveAll(e => e.Id == id);
            if (removed == 0) return false;
            Persist();
            return true;
        }

        public static bool Remove(GameObject go)
        {
            if (go == null) return false;
            string id = DependencyLink.GetGlobalId(go);
            return !string.IsNullOrEmpty(id) && Remove(id);
        }

        public static bool Toggle(GameObject go, out string message)
        {
            if (go == null)
            {
                message = "Select a GameObject first.";
                return false;
            }

            if (Contains(go))
            {
                Remove(go);
                message = $"Removed bookmark: {go.name}";
                return false;
            }

            TryAdd(go, out message);
            return Contains(go);
        }

        public static void Clear()
        {
            EnsureLoaded();
            if (_entries.Count == 0) return;
            _entries.Clear();
            Persist();
        }

        public static GameObject Resolve(BookmarkEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return null;
            if (!GlobalObjectId.TryParse(entry.Id, out var gid)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        }

        public static void NotifyWindows() => Changed?.Invoke();

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _entries = new List<BookmarkEntry>();

            try
            {
                if (!File.Exists(FilePath)) return;
                string json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                if (json.TrimStart().StartsWith("{") && !json.Contains("\"entries\""))
                {
                    MigrateLegacyObject(json);
                    return;
                }

                if (json.TrimStart().StartsWith("["))
                    json = "{\"entries\":" + json + "}";

                _entries = JsonUtility.FromJson<BookmarkList>(json)?.entries ?? new List<BookmarkEntry>();

                var seen = new HashSet<string>();
                _entries = _entries
                    .Where(e => e != null && !string.IsNullOrEmpty(e.Id) && seen.Add(e.Id))
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneXRay: failed to load bookmarks — {e.Message}");
                _entries = new List<BookmarkEntry>();
            }
        }

        [Serializable]
        private class BookmarkList
        {
            public List<BookmarkEntry> entries = new();
        }

        private static void MigrateLegacyObject(string json)
        {
            foreach (Match m in Regex.Matches(json, @"""(?<k>[^""]+)""\s*:\s*""(?<v>[^""]*)"""))
            {
                string id = m.Groups["v"].Value;
                if (string.IsNullOrEmpty(id) || _entries.Any(e => e.Id == id)) continue;
                _entries.Add(new BookmarkEntry { Id = id, Name = m.Groups["k"].Value, ScenePath = "" });
            }
            Persist();
        }

        private static void Persist()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? "Library/SceneXRay");
                File.WriteAllText(FilePath, JsonUtility.ToJson(new BookmarkList { entries = _entries }, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneXRay: failed to save bookmarks — {e.Message}");
            }

            Changed?.Invoke();
        }
    }
}
