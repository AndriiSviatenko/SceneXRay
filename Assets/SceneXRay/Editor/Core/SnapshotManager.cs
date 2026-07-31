using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// Saves/loads dependency graph snapshots as JSON in Library/SceneXRay/Snapshots (outside VCS).
    /// Node/edge IDs are GlobalObjectId strings, stable across editor sessions.
    /// </summary>
    public static class SnapshotManager
    {
        private static string SnapshotFolder => Path.Combine("Library", "SceneXRay", "Snapshots");

        public static void SaveSnapshot(List<DependencyLink> links, string comment)
        {
            var report = XRayAnalyzer.Analyze(links);
            var snapshot = new SceneXRaySnapshot
            {
                Id = Guid.NewGuid().ToString(),
                ScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                Author = Environment.UserName,
                Timestamp = DateTime.Now,
                Comment = comment,
                HealthScore = report.Score,
                MissingCount = report.Missing,
                CycleCount = report.CycleCount
            };

            var allObjects = new HashSet<GameObject>();
            foreach (var l in links)
            {
                if (l.Source != null) allObjects.Add(l.Source);
                if (l.Target != null) allObjects.Add(l.Target);
            }
            foreach (var go in allObjects)
            {
                snapshot.Nodes.Add(new SnapshotNode
                {
                    Id = DependencyLink.GetGlobalId(go),
                    Name = go.name,
                    Type = "GameObject",
                    Position = SnapshotVec3.From(go.transform.position),
                    Components = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToList()
                });
            }
            int missingIdx = 0;
            foreach (var l in links.Where(l => l.IsMissing && l.Source != null))
            {
                string mid = $"missing_{missingIdx++}";
                snapshot.Nodes.Add(new SnapshotNode
                {
                    Id = mid,
                    Name = "Missing",
                    Type = "Missing",
                    Position = new SnapshotVec3(0, 0, 0),
                    Components = new List<string>()
                });
                snapshot.Edges.Add(new SnapshotEdge
                {
                    SourceId = l.SourceGlobalId,
                    TargetId = mid,
                    Label = l.SourcePropertyName,
                    Type = l.LinkType.ToString()
                });
            }
            foreach (var l in links.Where(l => l.Source != null && l.Target != null && !l.IsMissing))
            {
                snapshot.Edges.Add(new SnapshotEdge
                {
                    SourceId = l.SourceGlobalId,
                    TargetId = l.TargetGlobalId,
                    Label = l.SourcePropertyName + (l.IsUnityEvent ? " (evt)" : ""),
                    Type = l.LinkType.ToString()
                });
            }

            Directory.CreateDirectory(SnapshotFolder);
            string json = JsonUtility.ToJson(snapshot, true);
            string path = Path.Combine(SnapshotFolder, $"{snapshot.Timestamp:yyyy-MM-dd_HH-mm-ss}.json");
            File.WriteAllText(path, json);
            Debug.Log($"SceneXRay snapshot saved: {path}");
        }

        public static List<string> GetSnapshots()
        {
            return Directory.Exists(SnapshotFolder)
                ? Directory.GetFiles(SnapshotFolder, "*.json").OrderBy(p => p).ToList()
                : new List<string>();
        }

        public static SceneXRaySnapshot LoadSnapshot(string path)
        {
            return JsonUtility.FromJson<SceneXRaySnapshot>(File.ReadAllText(path));
        }

        public static string CompareSnapshots(string path1, string path2)
        {
            var s1 = LoadSnapshot(path1);
            var s2 = LoadSnapshot(path2);
            var diff = new System.Text.StringBuilder();
            diff.AppendLine($"Comparison: {s1.Timestamp} vs {s2.Timestamp}");
            diff.AppendLine($"Health Score: {s1.HealthScore:F2} -> {s2.HealthScore:F2}");
            diff.AppendLine($"Missing: {s1.MissingCount} -> {s2.MissingCount}");
            diff.AppendLine($"Cycles: {s1.CycleCount} -> {s2.CycleCount}");

            var names1 = s1.Nodes.ToDictionary(n => n.Id, n => n.Name);
            var names2 = s2.Nodes.ToDictionary(n => n.Id, n => n.Name);
            string Describe(string edgeKey, Dictionary<string, string> names)
            {
                var parts = edgeKey.Split(new[] { "->" }, StringSplitOptions.None);
                string From(string id) => names.TryGetValue(id, out var n) ? n : id;
                return parts.Length == 2 ? $"{From(parts[0])} -> {From(parts[1])}" : edgeKey;
            }

            var edges1 = s1.Edges.Select(e => $"{e.SourceId}->{e.TargetId}").ToHashSet();
            var edges2 = s2.Edges.Select(e => $"{e.SourceId}->{e.TargetId}").ToHashSet();
            var added = edges2.Except(edges1).Select(e => Describe(e, names2)).ToList();
            var removed = edges1.Except(edges2).Select(e => Describe(e, names1)).ToList();
            if (added.Any()) diff.AppendLine($"Added edges: {string.Join(", ", added)}");
            if (removed.Any()) diff.AppendLine($"Removed edges: {string.Join(", ", removed)}");

            return diff.ToString();
        }
    }
}
