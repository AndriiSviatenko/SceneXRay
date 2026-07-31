using System.Collections.Generic;
using SceneXRay.Editor.UI;
using System.Text;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.Core;
using System.Linq;

namespace SceneXRay.Editor.Exporters
{
    public static class XRayMarkdownExporter
    {
        public static void ExportToMarkdown(List<DependencyLink> links)
        {
            string path = EditorUtility.SaveFilePanel("Export Markdown", "", "report", "md");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("# SceneXRay Report");
            sb.AppendLine($"Generated: {System.DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- **Total Dependencies:** {links.Count}");
            sb.AppendLine($"- **Missing References:** {links.Count(l => l.IsMissing)}");
            sb.AppendLine($"- **UnityEvents:** {links.Count(l => l.IsUnityEvent)}");
            sb.AppendLine($"- **Health Score:** {XRayAnalyzer.CalculateHealthScore(links):F1}%");

            var errors = QualityGateManager.Validate(links);
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## ⚠️ Quality Issues");
                foreach (var err in errors)
                    sb.AppendLine($"- {err}");
            }

            var cycles = XRayAnalyzer.FindCycles(links);
            if (cycles.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 🔄 Cyclic Dependencies");
                foreach (var cycle in cycles)
                {
                    sb.AppendLine("```");
                    sb.AppendLine(string.Join(" -> ", cycle.Select(l => l.Source?.name ?? "null")));
                    sb.AppendLine("```");
                }
            }

            var godObjects = XRayAnalyzer.FindGodObjects(links);
            if (godObjects.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 🏛️ God Objects (10+ dependencies)");
                foreach (var go in godObjects)
                    sb.AppendLine($"- {go.name}");
            }

            sb.AppendLine();
            sb.AppendLine("## 📋 Dependency List");
            sb.AppendLine("| Source | Target | Property | Type | Missing |");
            sb.AppendLine("|--------|--------|----------|------|---------|");
            foreach (var l in links)
            {
                string src = l.Source?.name ?? "null";
                string tgt = l.Target?.name ?? "null";
                string missing = l.IsMissing ? "❌" : "✅";
                sb.AppendLine($"| {src} | {tgt} | {l.SourcePropertyName} | {l.LinkType} | {missing} |");
            }

            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"Markdown report saved to {path}");
        }
    }
}