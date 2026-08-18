using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SceneXRay.Editor.Core
{
    public static class CodeDependencyScanner
    {
        public class ScriptDependencies
        {
            public readonly HashSet<string> ComponentTypes = new();
            public readonly HashSet<string> ObjectNames = new();
            public readonly HashSet<string> ObjectTags = new();

            public bool IsEmpty => ComponentTypes.Count == 0 && ObjectNames.Count == 0 && ObjectTags.Count == 0;
        }

        private class CacheEntry
        {
            public DateTime WriteTimeUtc;
            public ScriptDependencies Dependencies;
        }

        private static readonly Dictionary<string, CacheEntry> _cache = new();

        private static readonly Regex TypeLookup = new(
            @"\b(?:FindObjectOfType|FindObjectsOfType|FindAnyObjectByType|FindFirstObjectByType" +
            @"|FindObjectsByType|GetComponent|GetComponents|GetComponentInChildren" +
            @"|GetComponentsInChildren|GetComponentInParent|GetComponentsInParent" +
            @"|TryGetComponent|AddComponent|RequireComponent)\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NameLookup = new(
            @"\b(?:GameObject\.Find|transform\.Find)\s*\(\s*""([^""]+)""\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TagLookup = new(
            @"\b(?:GameObject\.FindWithTag|GameObject\.FindGameObjectWithTag)" +
            @"\s*\(\s*""([^""]+)""\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex Comments = new(
            @"//[^\n]*|/\*.*?\*/",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        public static ScriptDependencies GetDependencies(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return null;

            DateTime writeTime;
            try
            {
                if (!File.Exists(assetPath)) return null;
                writeTime = File.GetLastWriteTimeUtc(assetPath);
            }
            catch (IOException)
            {
                return null;
            }

            if (_cache.TryGetValue(assetPath, out var cached) && cached.WriteTimeUtc == writeTime)
                return cached.Dependencies;

            var parsed = Parse(assetPath);
            _cache[assetPath] = new CacheEntry { WriteTimeUtc = writeTime, Dependencies = parsed };
            return parsed;
        }

        private static ScriptDependencies Parse(string assetPath)
        {
            string source;
            try
            {
                source = File.ReadAllText(assetPath);
            }
            catch (IOException)
            {
                return null;
            }

            source = Comments.Replace(source, " ");

            var result = new ScriptDependencies();

            foreach (Match match in TypeLookup.Matches(source))
            {
                string name = match.Groups[1].Value;

                if (name.Length <= 2 && char.IsUpper(name[0])) continue;
                result.ComponentTypes.Add(name);
            }

            foreach (Match match in NameLookup.Matches(source))
                result.ObjectNames.Add(match.Groups[1].Value);

            foreach (Match match in TagLookup.Matches(source))
                result.ObjectTags.Add(match.Groups[1].Value);

            return result;
        }

        public static void ClearCache() => _cache.Clear();
    }
}
