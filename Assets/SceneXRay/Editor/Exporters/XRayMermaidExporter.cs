using SceneXRay.Editor.UI;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.Core;

namespace SceneXRay.Editor.Exporters
{
    public static class XRayMermaidExporter
    {
        public static void ExportToMermaid(List<DependencyLink> links, string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.SaveFilePanel("Export Mermaid", "", "graph", "mmd");
                if (string.IsNullOrEmpty(path)) return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("graph TD");
            var nodeIds = new Dictionary<GameObject, string>();
            int idCounter = 0;

            var allObjects = new HashSet<GameObject>();
            foreach (var l in links)
            {
                if (l.Source != null) allObjects.Add(l.Source);
                if (l.Target != null) allObjects.Add(l.Target);
            }

            foreach (var go in allObjects)
            {
                string id = $"N{idCounter++}";
                nodeIds[go] = id;
                string label = go.name.Replace("\"", "\\\"");
                sb.AppendLine($"    {id}[\"{label}\"]");
            }

            foreach (var l in links)
            {
                if (l.Source == null || !nodeIds.ContainsKey(l.Source)) continue;

                if (l.IsMissing)
                {
                    string missingId = $"M{idCounter++}";
                    sb.AppendLine($"    {missingId}[\"Missing: {l.SourcePropertyName}\"]:::missing");
                    sb.AppendLine($"    {nodeIds[l.Source]} --> {missingId}");
                }
                else if (l.Target != null && nodeIds.ContainsKey(l.Target))
                {
                    // Mermaid edge labels use the A -->|label| B form.
                    string arrow = l.IsUnityEvent ? "-.->" : "-->";
                    string label = l.SourcePropertyName?.Replace("|", "/") ?? "";
                    sb.AppendLine(string.IsNullOrEmpty(label)
                        ? $"    {nodeIds[l.Source]} {arrow} {nodeIds[l.Target]}"
                        : $"    {nodeIds[l.Source]} {arrow}|{label}| {nodeIds[l.Target]}");
                }
            }

            sb.AppendLine("    classDef missing fill:#5a2323,stroke:#ff5050,color:#fff;");

            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"Mermaid exported to {path}");
        }
    }
}