using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneXRay.Editor.UI
{
    internal static class XRayStructuralRank
    {
        public const int TierLogic = 0;

        public const int TierObject = 1;

        public const int TierScript = 2;

        public const int TierAsset = 3;

        public struct Rank
        {
            public int Tier;
            public float Score;
            public int OutObject;
            public int OutAsset;
            public int InDegree;
        }

        public static List<Node> Sort(List<Node> nodes, List<Edge> edges, out Dictionary<Node, Rank> ranks)
        {
            ranks = Compute(nodes, edges);
            var table = ranks;
            return nodes
                .OrderBy(n => table[n].Tier)
                .ThenByDescending(n => table[n].Score)
                .ThenBy(n => n.title, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static Dictionary<Node, Rank> Compute(List<Node> nodes, List<Edge> edges)
        {
            var ranks = new Dictionary<Node, Rank>(nodes.Count);
            foreach (var node in nodes)
                ranks[node] = new Rank { Tier = TierOf(node) };

            foreach (var edge in edges)
            {
                var source = edge.output?.node;
                var target = edge.input?.node;
                if (source == null || target == null || source == target) continue;
                if (!ranks.TryGetValue(source, out var sourceRank)) continue;
                if (!ranks.TryGetValue(target, out var targetRank)) continue;

                if (targetRank.Tier == TierAsset) sourceRank.OutAsset++;
                else sourceRank.OutObject++;
                targetRank.InDegree++;

                ranks[source] = sourceRank;
                ranks[target] = targetRank;
            }

            foreach (var node in nodes)
            {
                var rank = ranks[node];

                rank.Score = rank.OutObject * 2f + rank.InDegree + rank.OutAsset * 0.25f;
                ranks[node] = rank;
            }

            return ranks;
        }

        private static int TierOf(Node node)
        {
            if (node is not XRayNode xNode) return TierObject;

            var go = xNode.GameObject;
            if (go == null)
                return xNode.Asset is UnityEditor.MonoScript ? TierScript : TierAsset;

            foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                if (behaviour != null) return TierLogic;

            return TierObject;
        }
    }
}
