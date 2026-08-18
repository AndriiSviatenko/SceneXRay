using SceneXRay.Editor.UI;
using SceneXRay.Editor.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Exporters
{
    public static class XRayExporter
    {
        public static void ShowExportMenu(List<DependencyLink> links)
        {
            if (links == null) links = new List<DependencyLink>();
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("JSON"), false, () => ExportToJson(links));
            menu.AddItem(new GUIContent("CSV"), false, () => ExportToCsv(links));
            menu.AddItem(new GUIContent("HTML (interactive)"), false, () => ExportToHtml(links));
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
            public int Target;
            public string TargetName;
            public string Component;
            public string Property;
            public string Method;
            public string Type;
        }

        [System.Serializable]
        private class JsonPayload
        {
            public string generated;
            public string scene;
            public float healthScore;
            public List<JsonNode> nodes;
            public List<JsonLink> links;
        }

        [System.Serializable]
        private class HtmlNode
        {
            public int id;
            public string name;
            public string group;
            public string path;
        }

        [System.Serializable]
        private class HtmlLink
        {
            public int source;
            public int target;
            public string type;
            public string label;
        }

        [System.Serializable]
        private class HtmlPayload
        {
            public List<HtmlNode> nodes;
            public List<HtmlLink> links;
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

        public static void ExportToHtml(List<DependencyLink> links)
        {
            string path = EditorUtility.SaveFilePanel("Export HTML", "", "scenexray_graph", "html");
            if (string.IsNullOrEmpty(path)) return;

            var nodeIds = new Dictionary<UnityEngine.Object, int>();
            var htmlNodes = new List<HtmlNode>();
            var htmlLinks = new List<HtmlLink>();

            int GetId(UnityEngine.Object obj, string name, string group)
            {
                if (obj != null && nodeIds.TryGetValue(obj, out int existing)) return existing;
                int id = htmlNodes.Count;
                if (obj != null) nodeIds[obj] = id;
                htmlNodes.Add(new HtmlNode
                {
                    id = id,
                    name = name,
                    group = group,
                    path = obj is GameObject go ? GetPath(go)
                        : obj != null ? AssetDatabase.GetAssetPath(obj) : null
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
                    tgt = GetId(l.TargetAsset,
                        l.IsImplicit ? $"Script: {l.TargetAsset.name}" : $"Asset: {l.TargetAsset.name}",
                        l.IsImplicit ? "script" : "asset");
                else
                    continue;

                htmlLinks.Add(new HtmlLink
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
            string dataJson = JsonUtility.ToJson(new HtmlPayload { nodes = htmlNodes, links = htmlLinks });
            string dataBase64 = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(dataJson));

            string html = HtmlTemplate
                .Replace("__TITLE__", System.Net.WebUtility.HtmlEncode($"SceneXRay — {sceneName}"))
                .Replace("__SUBTITLE__", System.Net.WebUtility.HtmlEncode(
                    $"{sceneName} · {links.Count} links · Health {score:F1}% · {System.DateTime.Now:yyyy-MM-dd HH:mm}"))
                .Replace("__DATA_BASE64__", dataBase64);

            System.IO.File.WriteAllText(path, html, Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"SceneXRay: HTML exported to {path}");
        }

        private const string HtmlTemplate = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<title>__TITLE__</title>
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
  <span class=""lg""><span class=""dot"" style=""background:#4dc79e""></span>In code</span>
</div>
<div id=""tooltip""></div>
<svg></svg>
<script>
const data = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob('__DATA_BASE64__'), c => c.charCodeAt(0))));
const linkColor = { Direct: '#46a0ff', UnityEvent: '#ffd23c', Missing: '#ff5050', AssetReference: '#a078ff', Implicit: '#4dc79e' };
const nodeColor = { object: '#3a6ea5', missing: '#8a3030', asset: '#5a3f8a', script: '#287d69' };
const ns = 'http://www.w3.org/2000/svg';
const svg = document.querySelector('svg');
const viewport = document.createElementNS(ns, 'g');
svg.appendChild(viewport);
const tooltip = document.querySelector('#tooltip');
const byId = new Map(data.nodes.map(n => [n.id, n]));
const columns = Math.max(1, Math.ceil(Math.sqrt(data.nodes.length)));
data.nodes.forEach((n, i) => { n.x = 100 + (i % columns) * 170; n.y = 100 + Math.floor(i / columns) * 90; });

const lineLayer = document.createElementNS(ns, 'g');
const nodeLayer = document.createElementNS(ns, 'g');
viewport.append(lineLayer, nodeLayer);

data.links.forEach(link => {
  link.sourceNode = byId.get(link.source);
  link.targetNode = byId.get(link.target);
  const line = document.createElementNS(ns, 'line');
  line.setAttribute('class', 'link');
  line.setAttribute('stroke', linkColor[link.type] || '#888');
  line.setAttribute('stroke-width', link.type === 'Missing' ? '2.5' : '1.5');
  if (link.type === 'Missing') line.setAttribute('stroke-dasharray', '6 4');
  const title = document.createElementNS(ns, 'title');
  title.textContent = link.label || '';
  line.appendChild(title);
  lineLayer.appendChild(line);
  link.element = line;
});

data.nodes.forEach(node => {
  const group = document.createElementNS(ns, 'g');
  group.setAttribute('class', 'node');
  const circle = document.createElementNS(ns, 'circle');
  circle.setAttribute('r', '10');
  circle.setAttribute('fill', nodeColor[node.group] || '#666');
  circle.setAttribute('stroke', '#0d0d10');
  circle.setAttribute('stroke-width', '1.5');
  const label = document.createElementNS(ns, 'text');
  label.setAttribute('x', '14');
  label.setAttribute('y', '4');
  label.textContent = node.name || '';
  group.append(circle, label);
  group.addEventListener('pointerenter', e => { tooltip.textContent = node.path || node.name || ''; tooltip.style.opacity = '1'; });
  group.addEventListener('pointermove', e => { tooltip.style.left = (e.pageX + 12) + 'px'; tooltip.style.top = (e.pageY - 10) + 'px'; });
  group.addEventListener('pointerleave', () => { tooltip.style.opacity = '0'; });
  group.addEventListener('pointerdown', e => { e.stopPropagation(); dragNode = node; svg.setPointerCapture(e.pointerId); });
  nodeLayer.appendChild(group);
  node.element = group;
});

let panX = 0, panY = 0, zoom = 1, panning = false, lastX = 0, lastY = 0, dragNode = null;
function render() {
  viewport.setAttribute('transform', `translate(${panX} ${panY}) scale(${zoom})`);
  data.nodes.forEach(n => n.element.setAttribute('transform', `translate(${n.x} ${n.y})`));
  data.links.forEach(l => {
    if (!l.sourceNode || !l.targetNode) return;
    l.element.setAttribute('x1', l.sourceNode.x); l.element.setAttribute('y1', l.sourceNode.y);
    l.element.setAttribute('x2', l.targetNode.x); l.element.setAttribute('y2', l.targetNode.y);
  });
}
svg.addEventListener('wheel', e => {
  e.preventDefault();
  const old = zoom;
  zoom = Math.min(4, Math.max(0.1, zoom * (e.deltaY < 0 ? 1.1 : 0.9)));
  panX = e.clientX - (e.clientX - panX) * (zoom / old);
  panY = e.clientY - (e.clientY - panY) * (zoom / old);
  render();
}, { passive: false });
svg.addEventListener('pointerdown', e => { panning = true; lastX = e.clientX; lastY = e.clientY; svg.setPointerCapture(e.pointerId); });
svg.addEventListener('pointermove', e => {
  if (dragNode) {
    dragNode.x = (e.clientX - panX) / zoom; dragNode.y = (e.clientY - panY) / zoom;
  } else if (panning) {
    panX += e.clientX - lastX; panY += e.clientY - lastY; lastX = e.clientX; lastY = e.clientY;
  } else return;
  render();
});
svg.addEventListener('pointerup', e => { panning = false; dragNode = null; if (svg.hasPointerCapture(e.pointerId)) svg.releasePointerCapture(e.pointerId); });
render();
</script>
</body>
</html>";
    }
}
