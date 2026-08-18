using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public class QualityGate
    {
        public string Name;
        public System.Func<List<DependencyLink>, bool> Condition;
        public string ErrorMessage;
    }

    public static class QualityGateManager
    {
        public static List<QualityGate> Gates = new List<QualityGate>();

        static QualityGateManager()
        {
            Gates.Add(new QualityGate
            {
                Name = "No Missing References",
                Condition = (links) => !links.Any(l => l.IsMissing),
                ErrorMessage = "Scene contains missing references"
            });
            Gates.Add(new QualityGate
            {
                Name = "Max Dependencies Per Object",
                Condition = (links) => !links.Any() || links.Where(l => l.Source != null).GroupBy(l => l.Source).All(g => g.Count() <= 10),
                ErrorMessage = "Some objects have more than 10 dependencies"
            });
            Gates.Add(new QualityGate
            {
                Name = "No Cyclic Dependencies",
                Condition = (links) => XRayAnalyzer.FindCycles(links).Count == 0,
                ErrorMessage = "Scene has cyclic dependencies"
            });
        }

        public static List<string> Validate(List<DependencyLink> links)
        {
            var errors = new List<string>();
            foreach (var gate in Gates)
            {
                if (!gate.Condition(links))
                    errors.Add(gate.ErrorMessage);
            }
            return errors;
        }
    }
}
