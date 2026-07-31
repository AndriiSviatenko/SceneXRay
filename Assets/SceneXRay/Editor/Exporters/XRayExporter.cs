using SceneXRay.Editor.UI;
using SceneXRay.Editor.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Exporters
{
    /// <summary>Export menu + JSON/CSV/HTML exporters. All exports go through SaveFilePanel.</summary>
    public static class XRayExporter
    {
        public static void ShowExportMenu(List<DependencyLink> links)
        {
            if (links == null) links = new List<DependencyLink>();
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("JSON"), false, () => ExportToJson(links));
            menu.AddItem(new GUIContent("CSV"), false, () => ExportToCsv(links));
            menu.AddItem(new GUIContent("HTML (D3.js)"), false, () => ExportToHtml(links));
            menu.AddItem(new GUIContent("PlantUML"), false, () => XRayPlantUMLExporter.ExportToPlantUML(links));
            menu.AddItem(new GUIContent("Mermaid"), false, () => XRayMermaidExporter.ExportToMermaid(links));
            menu.AddItem(new GUIContent("Markdown"), false, () => XRayMarkdownExporter.ExportToMarkdown(links));
            menu.ShowAsContext();
        }

        internal static string GetPath(GameObject go)
        {
            if (go == null) return null;
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }

        // ---------------- JSON ----------------

        [System.Serializable]
        private class JsonNode
        {
            public int Id;
            public string Name;
            public string Path;
        }

        [System.Serializable]
        private class JsonLink
        {
            public int Source;
            public int Target; // -1 when missing / asset
            public string TargetName;
            public string Component;
            public string Property;
            public string Method;
            public string Type;
        }

        // JsonUtility needs concrete serializable types — no anonymous objects, no bare lists.
        [System.Serializable]
        private class JsonPayload
        {
            public string generated;
            public string scene;
            public float healthScore;
            public List<JsonNode> nodes;
            public List<JsonLink> links;
        }

        /// <summary>D3 force-graph node — lowercase names are what the HTML template reads.</summary>
        [System.Serializable]
        private class D3Node
        {
            public int id;
            public string name;
            public string group;
            public string path;
        }

        [System.Serializable]
        private class D3Link
        {
            public int source;
            public int target;
            public string type;
            public string label;
        }

        [System.Serializable]
        private class D3Payload
        {
            public List<D3Node> nodes;
            public List<D3Link> links;
        }

        public static void ExportToJson(List<DependencyLink> links)
        {
            string path = EditorUtility.SaveFilePanel("Export JSON", "", "scenexray_graph", "json");
            if (string.IsNullOrEmpty(path)) return;

            var (nodes, jsonLinks) = BuildGraphModel(links);
            var payload = new JsonPayload
            {
                generated = System.DateTime.Now.ToString("s"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                healthScore = XRayAnalyzer.CalculateHealthScore(links),
                nodes = nodes,
                links = jsonLinks
            };

            System.IO.File.WriteAllText(path, JsonUtility.ToJson(payload, true), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"SceneXRay: JSON exported to {path}");
        }

        private static (List<JsonNode> nodes, List<JsonLink> links) BuildGraphModel(List<DependencyLink> links)
        {
            var nodeIds = new Dictionary<GameObject, int>();
            var nodes = new List<JsonNode>();

            int GetId(GameObject go)
            {
                if (!nodeIds.TryGetValue(go, out int id))
                {
                    id = nodes.Count;
                    nodeIds[go] = id;
                    nodes.Add(new JsonNode { Id = id, Name = go.name, Path = GetPath(go) });
                }
                return id;
            }

            var jsonLinks = new List<JsonLink>();
            foreach (var l in links)
            {
                if (l.Source == null) continue;
                jsonLinks.Add(new JsonLink
                {
                    Source = GetId(l.Source),
                    Target = l.Target != null ? GetId(l.Target) : -1,
                    TargetName = l.Target != null ? l.Target.name : (l.TargetAsset != null ? l.TargetAsset.name : "Missing"),
                    Component = l.SourceComponentName,
                    Property = l.SourcePropertyName,
                    Method = l.TargetMethodName,
                    Type = l.LinkType.ToString()
                });
            }
            return (nodes, jsonLinks);
        }

        // ---------------- CSV ----------------

        public static void ExportToCsv(List<DependencyLink> links)
        {
            string path = EditorUtility.SaveFilePanel("Export CSV", "", "scenexray_links", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("Source,SourcePath,Component,Property,Target,TargetPath,Method,Type");
            foreach (var l in links)
            {
                sb.Append(Csv(l.Source != null ? l.Source.name : "")).Append(',');
                sb.Append(Csv(GetPath(l.Source) ?? "")).Append(',');
                sb.Append(Csv(l.SourceComponentName)).Append(',');
                sb.Append(Csv(l.SourcePropertyName)).Append(',');
                sb.Append(Csv(l.Target != null ? l.Target.name : (l.TargetAsset != null ? l.TargetAsset.name : ""))).Append(',');
                sb.Append(Csv(GetPath(l.Target) ?? "")).Append(',');
                sb.Append(Csv(l.TargetMethodName)).Append(',');
                sb.Append(l.LinkType).AppendLine();
            }

            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"SceneXRay: CSV exported to {path}");
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // ---------------- HTML (interactive D3.js force graph) ----------------

        public static void ExportToHtml(List<DependencyLink> links)
        {
            string path = EditorUtility.SaveFilePanel("Export HTML", "", "scenexray_graph", "html");
            if (string.IsNullOrEmpty(path)) return;

            // D3 wants links to reference node ids; add synthetic nodes for missing/asset targets.
            var nodeIds = new Dictionary<GameObject, int>();
            var d3Nodes = new List<D3Node>();
            var d3Links = new List<D3Link>();

            int GetId(GameObject go, string name, string group)
            {
                if (go != null && nodeIds.TryGetValue(go, out int existing)) return existing;
                int id = d3Nodes.Count;
                if (go != null) nodeIds[go] = id;
                d3Nodes.Add(new D3Node
                {
                    id = id,
                    name = name,
                    group = group,
                    path = go != null ? GetPath(go) : null
                });
                return id;
            }

            foreach (var l in links)
            {
                if (l.Source == null) continue;
                int src = GetId(l.Source, l.Source.name, "object");
                int tgt;
                if (l.IsMissing)
                    tgt = GetId(null, $"Missing: {l.SourcePropertyName}", "missing");
                else if (l.Target != null)
                    tgt = GetId(l.Target, l.Target.name, "object");
                else if (l.TargetAsset != null)
                    tgt = GetId(null, $"Asset: {l.TargetAsset.name}", "asset");
                else
                    continue;

                d3Links.Add(new D3Link
                {
                    source = src,
                    target = tgt,
                    type = l.LinkType.ToString(),
                    label = $"{l.SourceComponentName}.{l.SourcePropertyName}" +
                            (string.IsNullOrEmpty(l.TargetMethodName) ? "" : $" → {l.TargetMethodName}")
                });
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            float score = XRayAnalyzer.CalculateHealthScore(links);
            string dataJson = JsonUtility.ToJson(new D3Payload { nodes = d3Nodes, links = d3Links });

            string html = HtmlTemplate
                .Replace("__TITLE__", $"SceneXRay — {sceneName}")
                .Replace("__SUBTITLE__", $"{sceneName} · {links.Count} links · Health {score:F1}% · {System.DateTime.Now:yyyy-MM-dd HH:mm}")
                .Replace("__DATA__", dataJson);

            System.IO.File.WriteAllText(path, html, Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"SceneXRay: HTML exported to {path}");
        }

        private const string HtmlTemplate = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>__TITLE__</title>
<script src=""https://cdn.jsdelivr.net/npm/d3@7""></script>
<style>
  html, body { margin: 0; height: 100%; background: #16161a; color: #dcdceb; font-family: 'Segoe UI', sans-serif; }
  #header { position: absolute; top: 12px; left: 16px; z-index: 10; pointer-events: none; }
  #header h1 { margin: 0; font-size: 18px; }
  #header p { margin: 4px 0 0; font-size: 12px; color: #9a9ab0; }
  #legend { position: absolute; bottom: 12px; left: 16px; font-size: 12px; z-index: 10; }
  .lg { display: inline-block; margin-right: 14px; }
  .dot { display: inline-block; width: 10px; height: 10px; border-radius: 5px; margin-right: 4px; vertical-align: middle; }
  svg { width: 100vw; height: 100vh; }
  .link { stroke-opacity: 0.6; }
  .node text { font-size: 11px; fill: #dcdceb; pointer-events: none; }
  #tooltip { position: absolute; padding: 6px 10px; background: rgba(30,30,38,0.95); border: 1px solid #444;
             border-radius: 6px; font-size: 12px; pointer-events: none; opacity: 0; z-index: 20; }
</style>
</head>
<body>
<div id=""header""><h1>__TITLE__</h1><p>__SUBTITLE__</p></div>
<div id=""legend"">
  <span class=""lg""><span class=""dot"" style=""background:#46a0ff""></span>Direct</span>
  <span class=""lg""><span class=""dot"" style=""background:#ffd23c""></span>UnityEvent</span>
  <span class=""lg""><span class=""dot"" style=""background:#ff5050""></span>Missing</span>
  <span class=""lg""><span class=""dot"" style=""background:#a078ff""></span>Asset</span>
</div>
<div id=""tooltip""></div>
<svg></svg>
<script>
const data = __DATA__;
const linkColor = { Direct: '#46a0ff', UnityEvent: '#ffd23c', Missing: '#ff5050', AssetReference: '#a078ff' };
const nodeColor = { object: '#3a6ea5', missing: '#8a3030', asset: '#5a3f8a' };

const svg = d3.select('svg');
const width = window.innerWidth, height = window.innerHeight;
const g = svg.append('g');
svg.call(d3.zoom().scaleExtent([0.1, 4]).on('zoom', e => g.attr('transform', e.transform)));

const sim = d3.forceSimulation(data.nodes)
  .force('link', d3.forceLink(data.links).id(d => d.id).distance(90))
  .force('charge', d3.forceManyBody().strength(-250))
  .force('center', d3.forceCenter(width / 2, height / 2))
  .force('collide', d3.forceCollide(28));

const link = g.append('g').selectAll('line').data(data.links).join('line')
  .attr('class', 'link')
  .attr('stroke', d => linkColor[d.type] || '#888')
  .attr('stroke-width', d => d.type === 'Missing' ? 2.5 : 1.5)
  .attr('stroke-dasharray', d => d.type === 'Missing' ? '6 4' : null);

const tooltip = d3.select('#tooltip');
const node = g.append('g').selectAll('g').data(data.nodes).join('g')
  .attr('class', 'node')
  .call(d3.drag()
    .on('start', (e, d) => { if (!e.active) sim.alphaTarget(0.3).restart(); d.fx = d.x; d.fy = d.y; })
    .on('drag', (e, d) => { d.fx = e.x; d.fy = e.y; })
    .on('end', (e, d) => { if (!e.active) sim.alphaTarget(0); d.fx = null; d.fy = null; }));

node.append('circle')
  .attr('r', 10)
  .attr('fill', d => nodeColor[d.group] || '#666')
  .attr('stroke', '#0d0d10').attr('stroke-width', 1.5)
  .on('mouseover', (e, d) => tooltip.style('opacity', 1).html((d.path || d.name)))
  .on('mousemove', e => tooltip.style('left', (e.pageX + 12) + 'px').style('top', (e.pageY - 10) + 'px'))
  .on('mouseout', () => tooltip.style('opacity', 0));

node.append('text').attr('dx', 14).attr('dy', 4).text(d => d.name);

link.append('title').text(d => d.label);

sim.on('tick', () => {
  link.attr('x1', d => d.source.x).attr('y1', d => d.source.y)
      .attr('x2', d => d.target.x).attr('y2', d => d.target.y);
  node.attr('transform', d => `translate(${d.x},${d.y})`);
});
</script>
</body>
</html>";
    }
}
