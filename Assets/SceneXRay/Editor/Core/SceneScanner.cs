using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace SceneXRay.Editor.Core
{
    /// <summary>Scans GameObjects for outgoing dependencies (direct refs, UnityEvents, missing refs, SO assets).</summary>
    public static class SceneScanner
    {
        private static readonly Dictionary<Type, FieldInfo[]> _unityEventFieldsCache = new();

        public static List<DependencyLink> ScanGameObject(GameObject go)
        {
            var links = new List<DependencyLink>();
            if (go == null) return links;

            var ignored = SceneXRaySettings.instance.IgnoredComponents;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                if (ignored != null && ignored.Contains(compType.Name)) continue;

                // Track UnityEvent targets found via reflection so the SerializedObject
                // iterator does not duplicate them through m_Target properties.
                var eventTargets = new HashSet<UnityEngine.Object>();
                ScanUnityEvents(go, comp, compType, links, eventTargets);
                ScanSerializedProperties(go, comp, compType, links, eventTargets);
            }
            return links;
        }

        private static void ScanSerializedProperties(GameObject go, Component comp, System.Type compType,
            List<DependencyLink> links, HashSet<UnityEngine.Object> eventTargets)
        {
            using (var so = new SerializedObject(comp))
            {
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                    // Skip UnityEvent internals — already covered by reflection pass.
                    if (prop.propertyPath.Contains("m_PersistentCalls")) continue;

                    // Every MonoBehaviour points at its own MonoScript — pure noise here,
                    // the component name is already on the link.
                    if (prop.propertyPath == "m_Script") continue;

                    var value = prop.objectReferenceValue;
                    if (value == null && prop.objectReferenceInstanceIDValue != 0)
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
                    // Components and GameObjects are object links, never asset links — even inside
                    // a prefab asset, where every object would otherwise look "persistent".
                    else if (value is Component targetComp)
                    {
                        if (targetComp.gameObject == go) continue; // self-reference
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
                        if (targetGo == go) continue; // self-reference
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
                    // Any other referenced asset: material, mesh, texture, clip, ScriptableObject…
                    // (GameObject/Component references were handled above, so prefab links keep
                    // pointing at the prefab's own node instead of turning into asset cards.)
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
        }

        /// <summary>Settings gate for asset links + the built-in resource filter.</summary>
        private static bool IncludeAsset(UnityEngine.Object asset)
        {
            var settings = SceneXRaySettings.instance;
            if (!settings.ScanAssetReferences) return false;
            if (settings.IncludeBuiltInAssets) return true;

            // Built-ins live outside Assets/ and Packages/ (Resources/unity_builtin_extra,
            // Library/unity default resources). In-memory objects have no path — keep those.
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

        /// <summary>Scans every GameObject in the currently loaded scenes.</summary>
        public static List<DependencyLink> ScanAllGameObjects()
        {
            var allLinks = new List<DependencyLink>();
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                allLinks.AddRange(ScanGameObject(go));
            return allLinks;
        }

        public static List<DependencyLink> ScanSelectedGameObjects()
        {
            var links = new List<DependencyLink>();
            foreach (var go in Selection.gameObjects)
                links.AddRange(ScanGameObject(go));
            return links;
        }

        /// <summary>
        /// Scans every scene from the build settings. Opens scenes additively — heavyweight,
        /// intended only for explicit user-triggered full-project analysis (e.g. global search, CLI).
        /// </summary>
        public static List<DependencyLink> ScanAllScenes()
        {
            var allLinks = new List<DependencyLink>();
            var scenePaths = EditorBuildSettings.scenes.Select(s => s.path).Where(s => !string.IsNullOrEmpty(s)).ToList();
            for (int i = 0; i < scenePaths.Count; i++)
            {
                var path = scenePaths[i];
                EditorUtility.DisplayProgressBar("SceneXRay", $"Scanning scene {path}", (float)i / scenePaths.Count);
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
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
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
            EditorUtility.ClearProgressBar();
            return allLinks;
        }
    }
}
