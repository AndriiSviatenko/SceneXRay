using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class XRayCacheManager
    {
        private static string CacheFolder => Path.Combine("Library", "SceneXRay", "Cache");

        private static readonly Dictionary<string, List<DependencyLink>> _memoryCache = new();

        [Serializable]
        private class CachedLink
        {
            public string SourceId;
            public string TargetId;
            public string SourcePropertyName;
            public string TargetMethodName;
            public string SourceComponentName;
            public LinkType LinkType;
        }

        [Serializable]
        private class CachedLinkList
        {
            public List<CachedLink> links = new();
        }

        public static void Save(string scenePath, List<DependencyLink> links)
        {
            if (string.IsNullOrEmpty(scenePath) || links == null) return;
            _memoryCache[scenePath] = new List<DependencyLink>(links);

            var cached = new List<CachedLink>(links.Count);
            foreach (var l in links)
            {
                cached.Add(new CachedLink
                {
                    SourceId = l.SourceGlobalId,
                    TargetId = l.TargetGlobalId,
                    SourcePropertyName = l.SourcePropertyName,
                    TargetMethodName = l.TargetMethodName,
                    SourceComponentName = l.SourceComponentName,
                    LinkType = l.LinkType
                });
            }

            Directory.CreateDirectory(CacheFolder);
            File.WriteAllText(GetCachePath(scenePath), JsonUtility.ToJson(new CachedLinkList { links = cached }));
        }

        public static bool TryLoad(string scenePath, out List<DependencyLink> links)
        {
            if (_memoryCache.TryGetValue(scenePath, out links))
                return true;

            string path = GetCachePath(scenePath);
            if (!File.Exists(path))
            {
                links = null;
                return false;
            }

            try
            {
                var cached = JsonUtility.FromJson<CachedLinkList>(File.ReadAllText(path))?.links ?? new List<CachedLink>();
                links = new List<DependencyLink>(cached.Count);
                foreach (var c in cached)
                {
                    links.Add(new DependencyLink
                    {
                        Source = Resolve(c.SourceId) as GameObject,
                        Target = Resolve(c.TargetId) as GameObject,
                        TargetAsset = c.LinkType == LinkType.AssetReference ? Resolve(c.TargetId) : null,
                        SourcePropertyName = c.SourcePropertyName,
                        TargetMethodName = c.TargetMethodName,
                        SourceComponentName = c.SourceComponentName,
                        LinkType = c.LinkType
                    });
                }
                _memoryCache[scenePath] = links;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneXRay: failed to read cache for '{scenePath}': {e.Message}");
                links = null;
                return false;
            }
        }

        public static void ClearCache()
        {
            _memoryCache.Clear();
            if (Directory.Exists(CacheFolder))
                Directory.Delete(CacheFolder, true);
            Debug.Log("SceneXRay cache cleared.");
        }

        private static string GetCachePath(string scenePath)
        {
            string safeName = scenePath.Replace("/", "_").Replace("\\", "_").Replace(".unity", "");
            return Path.Combine(CacheFolder, safeName + ".json");
        }

        private static UnityEngine.Object Resolve(string globalId)
        {
            if (string.IsNullOrEmpty(globalId)) return null;
            return GlobalObjectId.TryParse(globalId, out var id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id)
                : null;
        }
    }
}
