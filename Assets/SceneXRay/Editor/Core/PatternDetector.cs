using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class PatternDetector
    {
        public enum ArchitecturePattern { None, MVC, MVVM, EventBus, DI, Singleton, Factory }

        public static ArchitecturePattern DetectPattern(List<DependencyLink> links)
        {
            if (HasMVCPattern(links)) return ArchitecturePattern.MVC;
            if (HasEventBusPattern(links)) return ArchitecturePattern.EventBus;
            if (HasDIPattern(links)) return ArchitecturePattern.DI;
            return ArchitecturePattern.None;
        }

        private static bool HasMVCPattern(List<DependencyLink> links)
        {
            var names = links.Select(l => l.Source?.name ?? "").Concat(links.Select(l => l.Target?.name ?? "")).Distinct();
            bool hasModel = names.Any(n => n.Contains("Model"));
            bool hasView = names.Any(n => n.Contains("View") || n.Contains("Panel") || n.Contains("Canvas"));
            bool hasController = names.Any(n => n.Contains("Controller"));
            return hasModel && hasView && hasController;
        }

        private static bool HasEventBusPattern(List<DependencyLink> links)
            => links.Any(l => l.Source?.name?.Contains("Event") == true || l.Target?.name?.Contains("Event") == true);

        private static bool HasDIPattern(List<DependencyLink> links)
            => links.Any(l => l.Source?.name?.Contains("Service") == true || l.Target?.name?.Contains("Service") == true);
    }
}