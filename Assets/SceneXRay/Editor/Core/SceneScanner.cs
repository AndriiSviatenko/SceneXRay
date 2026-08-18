using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace SceneXRay.Editor.Core
{
    public static class SceneScanner
    {
        private static readonly Dictionary<Type, FieldInfo[]> _unityEventFieldsCache = new();

        private static readonly Dictionary<Type, bool> _hasObjectRefProperties = new();

        private static readonly List<Component> _componentBuffer = new();

        public static List<DependencyLink> ScanGameObject(GameObject go)
        {
            var links = new List<DependencyLink>();
            if (go == null) return links;

            var ignored = SceneXRaySettings.instance.IgnoredComponents;

            go.GetComponents(_componentBuffer);
            foreach (var comp in _componentBuffer)
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                if (ignored != null && ignored.Contains(compType.Name)) continue;

                var eventTargets = new HashSet<UnityEngine.Object>();
                ScanUnityEvents(go, comp, compType, links, eventTargets);
                ScanSerializedProperties(go, comp, compType, links, eventTargets);
            }
            return links;
        }

        private static void ScanSerializedProperties(GameObject go, Component comp, System.Type compType,
            List<DependencyLink> links, HashSet<UnityEngine.Object> eventTargets)
        {
            if (_hasObjectRefProperties.TryGetValue(compType, out bool known) && !known) return;

            bool sawObjectRef = false;
            bool sawManagedRef = false;
            bool sawVariableShape = false;

            using (var so = new SerializedObject(comp))
            {
                var prop = so.GetIterator();

                while (prop.Next(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ManagedReference)
                        sawManagedRef = true;
                    if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                        sawVariableShape = true;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                    if (prop.propertyPath.Contains("m_PersistentCalls")) continue;

                    if (prop.propertyPath == "m_Script") continue;

                    sawObjectRef = true;

                    var value = prop.objectReferenceValue;
                    if (SceneXRayCompat.IsBrokenReference(prop))
                    {
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = null,
                            SourcePropertyName = prop.displayName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.Missing
                        });
                    }

                    else if (value is Component targetComp)
                    {
                        if (targetComp.gameObject == go) continue;
                        if (eventTargets.Contains(value)) continue;
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = targetComp.gameObject,
                            SourcePropertyName = prop.displayName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.Direct
                        });
                    }
                    else if (value is GameObject targetGo)
                    {
                        if (targetGo == go) continue;
                        if (eventTargets.Contains(value)) continue;
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = targetGo,
                            SourcePropertyName = prop.displayName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.Direct
                        });
                    }

                    else if (value is ScriptableObject || (value != null && EditorUtility.IsPersistent(value)))
                    {
                        if (!IncludeAsset(value)) continue;
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = null,
                            TargetAsset = value,
                            SourcePropertyName = prop.displayName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.AssetReference
                        });
                    }
                }
            }

            if (!sawManagedRef && !sawVariableShape)
                _hasObjectRefProperties[compType] = sawObjectRef;
        }

        public static void ClearTypeCaches()
        {
            _hasObjectRefProperties.Clear();
            _unityEventFieldsCache.Clear();
        }

        private static bool IncludeAsset(UnityEngine.Object asset)
        {
            var settings = SceneXRaySettings.instance;
            if (!settings.ScanAssetReferences) return false;
            if (settings.IncludeBuiltInAssets) return true;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return true;
            return path.StartsWith("Assets/") || path.StartsWith("Packages/");
        }

        private static FieldInfo[] GetUnityEventFields(Type compType)
        {
            if (_unityEventFieldsCache.TryGetValue(compType, out var cached))
                return cached;

            var all = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var eventFields = new List<FieldInfo>();
            foreach (var field in all)
            {
                if (field.FieldType == typeof(UnityEvent) || field.FieldType.IsSubclassOf(typeof(UnityEventBase)))
                    eventFields.Add(field);
            }
            cached = eventFields.ToArray();
            _unityEventFieldsCache[compType] = cached;
            return cached;
        }

        private static void ScanUnityEvents(GameObject go, Component comp, System.Type compType,
            List<DependencyLink> links, HashSet<UnityEngine.Object> eventTargets)
        {
            foreach (var field in GetUnityEventFields(compType))
            {
                if (field.GetValue(comp) is not UnityEventBase unityEvent) continue;

                int persistentCount = unityEvent.GetPersistentEventCount();
                for (int i = 0; i < persistentCount; i++)
                {
                    var targetObj = unityEvent.GetPersistentTarget(i);
                    var methodName = unityEvent.GetPersistentMethodName(i);
                    if (targetObj is Component targetComp)
                    {
                        eventTargets.Add(targetObj);
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = targetComp.gameObject,
                            SourcePropertyName = field.Name,
                            TargetMethodName = methodName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.UnityEvent
                        });
                    }
                    else if (targetObj is GameObject targetGo)
                    {
                        eventTargets.Add(targetObj);
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = targetGo,
                            SourcePropertyName = field.Name,
                            TargetMethodName = methodName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.UnityEvent
                        });
                    }
                    else if (targetObj == null && !string.IsNullOrEmpty(methodName))
                    {
                        links.Add(new DependencyLink
                        {
                            Source = go,
                            Target = null,
                            SourcePropertyName = field.Name,
                            TargetMethodName = methodName,
                            SourceComponentName = compType.Name,
                            LinkType = LinkType.Missing
                        });
                    }
                }
            }
        }

        public static List<DependencyLink> ScanAllGameObjects()
        {
            var stageLinks = PrefabScanner.ScanOpenPrefabStage();
            if (stageLinks != null) return stageLinks;

            var allLinks = new List<DependencyLink>();
            foreach (var go in SceneXRayCompat.FindAll<GameObject>())
                allLinks.AddRange(ScanGameObject(go));
            return allLinks;
        }

        public static List<GameObject> CollectScannableObjects()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var result = new List<GameObject>();
                if (stage.prefabContentsRoot != null)
                    foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
                        result.Add(t.gameObject);
                return result;
            }

            return new List<GameObject>(SceneXRayCompat.FindAll<GameObject>());
        }

        public static List<DependencyLink> ScanSelectedGameObjects()
        {
            var links = new List<DependencyLink>();
            foreach (var go in Selection.gameObjects)
                links.AddRange(ScanGameObject(go));
            return links;
        }

        public static List<DependencyLink> ScanAllScenes(bool enabledOnly = false)
        {
            var allLinks = new List<DependencyLink>();
            var scenePaths = EditorBuildSettings.scenes
                .Where(s => !string.IsNullOrEmpty(s.path) && (!enabledOnly || s.enabled))
                .Select(s => s.path)
                .ToList();
            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    var path = scenePaths[i];
                    EditorUtility.DisplayProgressBar("SceneXRay", $"Scanning scene {path}", (float)i / scenePaths.Count);

                    var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                    bool alreadyOpen = existing.IsValid() && existing.isLoaded;
                    var scene = alreadyOpen
                        ? existing
                        : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    try
                    {
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                                allLinks.AddRange(ScanGameObject(child.gameObject));
                        }
                    }
                    finally
                    {
                        if (!alreadyOpen)
                            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return allLinks;
        }
    }
}
