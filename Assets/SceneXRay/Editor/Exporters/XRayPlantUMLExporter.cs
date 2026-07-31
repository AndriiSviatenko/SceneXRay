using SceneXRay.Editor.UI;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.Core;
using System.Linq;

namespace SceneXRay.Editor.Exporters
{
    public static class XRayPlantUMLExporter
    {
        public static void ExportToPlantUML(List<DependencyLink> links, string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.SaveFilePanel("Export PlantUML", "", "scene", "puml");
                if (string.IsNullOrEmpty(path)) return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("@startuml");
            sb.AppendLine("!define MASTER_MARKUP_CSS");
            sb.AppendLine("skinparam componentStyle uml2");
            sb.AppendLine("skinparam backgroundColor #FEFEFE");
            sb.AppendLine();

            var objects = new HashSet<GameObject>();
            foreach (var l in links)
            {
                if (l.Source != null) objects.Add(l.Source);
                if (l.Target != null) objects.Add(l.Target);
            }

            foreach (var go in objects)
            {
                string name = go.name.Replace(" ", "_");
                string comps = string.Join("\\n", go.GetComponents<Component>().Select(c => c?.GetType().Name ?? "null"));
                sb.AppendLine($"object \"{go.name}\" as {name} {{");
                sb.AppendLine($"  {comps}");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            int noteCounter = 0;
            foreach (var l in links)
            {
                if (l.Source == null) continue;

                if (l.IsMissing)
                {
                    string noteId = $"N{noteCounter++}";
                    sb.AppendLine($"note \"Missing: {l.SourcePropertyName}\" as {noteId}");
                    sb.AppendLine($"{l.Source.name.Replace(" ", "_")} --> {noteId} : missing");
                }
                else if (l.Target != null)
                {
                    string src = l.Source.name.Replace(" ", "_");
                    string tgt = l.Target.name.Replace(" ", "_");
                    string label = l.IsUnityEvent ? $"{l.SourcePropertyName} (event)" : l.SourcePropertyName;
                    sb.AppendLine($"{src} --> {tgt} : {label}");
                }
            }

            sb.AppendLine("@enduml");
            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"PlantUML exported to {path}");
        }
    }
}