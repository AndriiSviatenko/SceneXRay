using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class ImplicitDependencyScanner
    {
        private static Dictionary<string, Type> _typesByName;
        private static HashSet<string> _ambiguousNames;

        public static List<DependencyLink> Scan(IReadOnlyList<GameObject> objects)
        {
            var links = new List<DependencyLink>();
            if (objects == null || objects.Count == 0) return links;
            if (!SceneXRaySettings.instance.ScanImplicitDependencies) return links;

            int cap = Mathf.Max(1, SceneXRaySettings.instance.MaxImplicitTargetsPerLookup);

            var byType = new Dictionary<Type, List<GameObject>>();
            var byName = new Dictionary<string, List<GameObject>>();
            var byTag = new Dictionary<string, List<GameObject>>();
            var scriptOfType = new Dictionary<Type, MonoScript>();
            IndexScene(objects, byType, byName, byTag, scriptOfType);

            EnsureTypeTable();

            var depsByType = new Dictionary<Type, CodeDependencyScanner.ScriptDependencies>();

            var behaviourBuffer = new List<MonoBehaviour>();
            var processedTypes = new HashSet<Type>();
            foreach (var source in objects)
            {
                if (source == null) continue;
                source.GetComponents(behaviourBuffer);
                processedTypes.Clear();

                foreach (var behaviour in behaviourBuffer)
                {
                    if (behaviour == null) continue;

                    var type = behaviour.GetType();

                    if (!processedTypes.Add(type)) continue;
                    if (!depsByType.TryGetValue(type, out var deps))
                    {
                        var script = MonoScript.FromMonoBehaviour(behaviour);
                        deps = script != null
                            ? CodeDependencyScanner.GetDependencies(AssetDatabase.GetAssetPath(script))
                            : null;
                        depsByType[type] = deps;
                    }

                    if (deps == null || deps.IsEmpty) continue;

                    AddTypeLinks(source, type.Name, deps, byType, scriptOfType, cap, links);
                    AddNameLinks(source, type.Name, deps, byName, cap, links);
                    AddTagLinks(source, type.Name, deps, byTag, cap, links);
                }
            }

            return links;
        }

        private static void AddTypeLinks(GameObject source, string componentName,
            CodeDependencyScanner.ScriptDependencies deps, Dictionary<Type, List<GameObject>> byType,
            Dictionary<Type, MonoScript> scriptOfType, int cap, List<DependencyLink> links)
        {
            bool wantScriptNodes = SceneXRaySettings.instance.ShowScriptNodes;

            foreach (var typeName in deps.ComponentTypes)
            {
                var type = ResolveType(typeName);
                if (type == null) continue;

                if (wantScriptNodes && scriptOfType.TryGetValue(type, out var script) && script != null)
                {
                    links.Add(new DependencyLink
                    {
                        Source = source,
                        TargetAsset = script,
                        SourceComponentName = componentName,
                        SourcePropertyName = $"<{type.Name}> in code",
                        LinkType = LinkType.Implicit
                    });
                }

                if (!byType.TryGetValue(type, out var targets)) continue;
                if (targets.Count > cap) continue;

                foreach (var target in targets)
                {
                    if (target == source) continue;
                    links.Add(new DependencyLink
                    {
                        Source = source,
                        Target = target,
                        SourceComponentName = componentName,
                        SourcePropertyName = $"<{type.Name}> in code",
                        LinkType = LinkType.Implicit
                    });
                }
            }
        }

        private static void AddNameLinks(GameObject source, string componentName,
            CodeDependencyScanner.ScriptDependencies deps, Dictionary<string, List<GameObject>> byName,
            int cap, List<DependencyLink> links)
        {
            foreach (var name in deps.ObjectNames)
            {
                if (!byName.TryGetValue(name, out var targets)) continue;
                if (targets.Count > cap) continue;

                foreach (var target in targets)
                {
                    if (target == source) continue;
                    links.Add(new DependencyLink
                    {
                        Source = source,
                        Target = target,
                        SourceComponentName = componentName,
                        SourcePropertyName = $"Find(\"{name}\")",
                        LinkType = LinkType.Implicit
                    });
                }
            }
        }

        private static void AddTagLinks(GameObject source, string componentName,
            CodeDependencyScanner.ScriptDependencies deps, Dictionary<string, List<GameObject>> byTag,
            int cap, List<DependencyLink> links)
        {
            foreach (var tag in deps.ObjectTags)
            {
                if (!byTag.TryGetValue(tag, out var targets)) continue;
                if (targets.Count > cap) continue;

                foreach (var target in targets)
                {
                    if (target == source) continue;
                    links.Add(new DependencyLink
                    {
                        Source = source,
                        Target = target,
                        SourceComponentName = componentName,
                        SourcePropertyName = $"FindWithTag(\"{tag}\")",
                        LinkType = LinkType.Implicit
                    });
                }
            }
        }

        private static void IndexScene(IReadOnlyList<GameObject> objects,
            Dictionary<Type, List<GameObject>> byType, Dictionary<string, List<GameObject>> byName,
            Dictionary<string, List<GameObject>> byTag,
            Dictionary<Type, MonoScript> scriptOfType)
        {
            var componentBuffer = new List<Component>();
            foreach (var go in objects)
            {
                if (go == null) continue;

                if (!byName.TryGetValue(go.name, out var named))
                    byName[go.name] = named = new List<GameObject>();
                named.Add(go);

                string tag = go.tag;
                if (!byTag.TryGetValue(tag, out var tagged))
                    byTag[tag] = tagged = new List<GameObject>();
                tagged.Add(go);

                go.GetComponents(componentBuffer);
                foreach (var component in componentBuffer)
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (!byType.TryGetValue(type, out var list))
                        byType[type] = list = new List<GameObject>();
                    list.Add(go);

                    if (component is MonoBehaviour behaviour && !scriptOfType.ContainsKey(type))
                        scriptOfType[type] = MonoScript.FromMonoBehaviour(behaviour);
                }
            }
        }

        private static void EnsureTypeTable()
        {
            if (_typesByName != null) return;

            _typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
            _ambiguousNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (type.FullName != null && type.FullName != type.Name)
                    _typesByName[type.FullName] = type;

                if (_typesByName.TryGetValue(type.Name, out var existing) && existing != type)
                    _ambiguousNames.Add(type.Name);
                else if (existing == null)
                    _typesByName[type.Name] = type;
            }
        }

        private static Type ResolveType(string name)
        {
            if (_ambiguousNames.Contains(name)) return null;
            return _typesByName.TryGetValue(name, out var type) ? type : null;
        }

        public static void InvalidateTypeTable()
        {
            _typesByName = null;
            _ambiguousNames = null;
        }
    }
}
