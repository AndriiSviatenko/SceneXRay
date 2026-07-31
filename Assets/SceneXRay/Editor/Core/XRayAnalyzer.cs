using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>One-pass health / cycle / god-object report over a link set.</summary>
    public readonly struct HealthReport
    {
        public float Score { get; }
        public int Missing { get; }
        public int CycleCount { get; }
        public int UniqueObjects { get; }
        public float AvgDeps { get; }
        public IReadOnlyList<List<DependencyLink>> Cycles { get; }
        public IReadOnlyList<GameObject> GodObjects { get; }

        public HealthReport(
            float score,
            int missing,
            IReadOnlyList<List<DependencyLink>> cycles,
            int uniqueObjects,
            float avgDeps,
            IReadOnlyList<GameObject> godObjects)
        {
            Score = score;
            Missing = missing;
            Cycles = cycles ?? System.Array.Empty<List<DependencyLink>>();
            CycleCount = Cycles.Count;
            UniqueObjects = uniqueObjects;
            AvgDeps = avgDeps;
            GodObjects = godObjects ?? System.Array.Empty<GameObject>();
        }
    }

    public static class XRayAnalyzer
    {
        /// <summary>
        /// Finds dependency cycles via DFS. Each cycle is returned as the ordered list of links
        /// forming the loop, reconstructed from the current DFS path.
        /// </summary>
        public static List<List<DependencyLink>> FindCycles(List<DependencyLink> links)
        {
            var graph = new Dictionary<GameObject, List<DependencyLink>>();
            foreach (var l in links)
            {
                if (l.Source == null || l.Target == null || l.IsMissing) continue;
                if (!graph.TryGetValue(l.Source, out var outEdges))
                {
                    outEdges = new List<DependencyLink>();
                    graph[l.Source] = outEdges;
                }
                outEdges.Add(l);
            }

            var cycles = new List<List<DependencyLink>>();
            var visited = new HashSet<GameObject>();
            var onStack = new HashSet<GameObject>();
            var path = new List<DependencyLink>();

            void DFS(GameObject current)
            {
                visited.Add(current);
                onStack.Add(current);

                if (graph.TryGetValue(current, out var outEdges))
                {
                    foreach (var edge in outEdges)
                    {
                        var next = edge.Target;
                        if (onStack.Contains(next))
                        {
                            var cycle = new List<DependencyLink>();
                            int startIdx = path.FindIndex(p => p.Source == next);
                            if (startIdx >= 0)
                                cycle.AddRange(path.GetRange(startIdx, path.Count - startIdx));
                            cycle.Add(edge);
                            cycles.Add(cycle);
                        }
                        else if (!visited.Contains(next))
                        {
                            path.Add(edge);
                            DFS(next);
                            path.RemoveAt(path.Count - 1);
                        }
                    }
                }
                onStack.Remove(current);
            }

            foreach (var start in graph.Keys.ToList())
            {
                if (!visited.Contains(start))
                    DFS(start);
            }
            return cycles;
        }

        /// <summary>Undirected degree map (source + target counts) in a single pass.</summary>
        public static Dictionary<GameObject, int> BuildDegreeMap(List<DependencyLink> links)
        {
            var dict = new Dictionary<GameObject, int>();
            if (links == null) return dict;
            foreach (var l in links)
            {
                if (l.Source != null)
                {
                    dict.TryGetValue(l.Source, out int s);
                    dict[l.Source] = s + 1;
                }
                if (l.Target != null)
                {
                    dict.TryGetValue(l.Target, out int t);
                    dict[l.Target] = t + 1;
                }
            }
            return dict;
        }

        public static Dictionary<GameObject, int> GetMostConnected(List<DependencyLink> links)
        {
            var dict = BuildDegreeMap(links);
            return dict
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>Single pass: score, missing, cycles, god objects. Prefer this over separate calls.</summary>
        public static HealthReport Analyze(List<DependencyLink> links, int godThreshold = 10)
        {
            if (links == null || links.Count == 0)
                return new HealthReport(100f, 0, System.Array.Empty<List<DependencyLink>>(), 0, 0f,
                    System.Array.Empty<GameObject>());

            int total = links.Count;
            int missing = 0;
            var unique = new HashSet<GameObject>();
            var degrees = new Dictionary<GameObject, int>();

            foreach (var l in links)
            {
                if (l.IsMissing) missing++;
                if (l.Source != null)
                {
                    unique.Add(l.Source);
                    degrees.TryGetValue(l.Source, out int s);
                    degrees[l.Source] = s + 1;
                }
                if (l.Target != null)
                {
                    degrees.TryGetValue(l.Target, out int t);
                    degrees[l.Target] = t + 1;
                }
            }

            var cycles = FindCycles(links);
            int uniqueObjects = unique.Count;
            float avgDeps = uniqueObjects > 0 ? (float)total / uniqueObjects : 0f;

            float score = 100f;
            score -= missing * 2f;
            score -= cycles.Count * 5f;
            if (avgDeps > 10f) score -= (avgDeps - 10f) * 1f;
            score = Mathf.Clamp(score, 0f, 100f);

            var gods = new List<GameObject>();
            foreach (var kvp in degrees)
            {
                if (kvp.Value > godThreshold)
                    gods.Add(kvp.Key);
            }

            return new HealthReport(score, missing, cycles, uniqueObjects, avgDeps, gods);
        }

        public static float CalculateHealthScore(List<DependencyLink> links)
            => Analyze(links).Score;

        public static List<GameObject> FindGodObjects(List<DependencyLink> links, int threshold = 10)
        {
            var degrees = BuildDegreeMap(links);
            var result = new List<GameObject>();
            foreach (var kvp in degrees)
            {
                if (kvp.Value > threshold)
                    result.Add(kvp.Key);
            }
            return result;
        }
    }
}
