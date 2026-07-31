using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// Background reverse-reference index for all currently loaded scenes.
    /// Maps target GameObject -> incoming DependencyLinks without ever opening other scenes.
    /// Built incrementally in ~2 ms time slices on EditorApplication.update and invalidated
    /// (with debounce) through ObjectChangeEvents, so reading it from inspector GUI is free.
    /// </summary>
    [InitializeOnLoad]
    public static class XRayReferenceIndex
    {
        private const double TimeBudgetMs = 8.0;
        private const double RebuildDebounceSeconds = 0.35;

        private static readonly Dictionary<int, List<DependencyLink>> _incoming = new();
        private static readonly Dictionary<int, List<DependencyLink>> _outgoing = new();
        /// <summary>Asset instance id -> scene links pointing at it (ScriptableObjects, materials, …).</summary>
        private static readonly Dictionary<int, List<DependencyLink>> _incomingAsset = new();
        private static readonly List<DependencyLink> _allLinks = new();
        private static readonly Queue<GameObject> _scanQueue = new();

        private static bool _rebuildRequested;
        private static double _rebuildRequestTime;
        private static bool _building;

        /// <summary>Raised when the index finishes a full rebuild.</summary>
        public static event Action IndexUpdated;

        /// <summary>True when the index reflects the current state of loaded scenes.</summary>
        public static bool IsReady { get; private set; }

        static XRayReferenceIndex()
        {
            EditorApplication.update += OnEditorUpdate;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.playModeStateChanged += _ => RequestRebuild();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, _) => RequestRebuild();
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += _ => RequestRebuild();
            RequestRebuild();
        }

        /// <summary>Incoming links for a GameObject (who references it). Never scans — returns cached data.</summary>
        public static IReadOnlyList<DependencyLink> GetIncoming(GameObject go)
        {
            if (go != null && _incoming.TryGetValue(go.GetInstanceID(), out var list))
                return list;
            return Array.Empty<DependencyLink>();
        }

        /// <summary>Outgoing links for a GameObject (what it references). Never scans — returns cached data.</summary>
        public static IReadOnlyList<DependencyLink> GetOutgoing(GameObject go)
        {
            if (go != null && _outgoing.TryGetValue(go.GetInstanceID(), out var list))
                return list;
            return Array.Empty<DependencyLink>();
        }

        /// <summary>
        /// Scene links that point at an asset (ScriptableObject, material, …).
        /// Never scans — returns cached data, empty until the index is ready.
        /// </summary>
        public static IReadOnlyList<DependencyLink> GetIncomingForAsset(UnityEngine.Object asset)
        {
            if (asset != null && _incomingAsset.TryGetValue(asset.GetInstanceID(), out var list))
                return list;
            return Array.Empty<DependencyLink>();
        }

        /// <summary>All links found in loaded scenes at the last completed build.</summary>
        public static IReadOnlyList<DependencyLink> AllLinks => _allLinks;

        /// <summary>Schedules a debounced full rebuild of the index.</summary>
        public static void RequestRebuild()
        {
            _rebuildRequested = true;
            _rebuildRequestTime = EditorApplication.timeSinceStartup;
        }

        /// <summary>
        /// Synchronous full rebuild. Used by tests and CLI paths; interactive editor code
        /// should rely on the time-sliced background build instead.
        /// </summary>
        public static void RebuildImmediate()
        {
            _rebuildRequested = false;
            BeginBuild();
            while (_building)
                ProcessSlice();
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            // Any structural or property change may alter references — rebuild lazily.
            if (stream.length > 0)
                RequestRebuild();
        }

        private static void OnEditorUpdate()
        {
            if (_rebuildRequested &&
                EditorApplication.timeSinceStartup - _rebuildRequestTime >= RebuildDebounceSeconds)
            {
                _rebuildRequested = false;
                BeginBuild();
            }

            if (_building)
                ProcessSlice();
        }

        private static void BeginBuild()
        {
            _scanQueue.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        _scanQueue.Enqueue(t.gameObject);
                }
            }

            _pendingIncoming.Clear();
            _pendingOutgoing.Clear();
            _pendingIncomingAsset.Clear();
            _pendingLinks.Clear();
            _building = true;
            IsReady = false;
        }

        private static readonly Dictionary<int, List<DependencyLink>> _pendingIncoming = new();
        private static readonly Dictionary<int, List<DependencyLink>> _pendingOutgoing = new();
        private static readonly Dictionary<int, List<DependencyLink>> _pendingIncomingAsset = new();
        private static readonly List<DependencyLink> _pendingLinks = new();

        private static void ProcessSlice()
        {
            double start = EditorApplication.timeSinceStartup;
            while (_scanQueue.Count > 0)
            {
                var go = _scanQueue.Dequeue();
                if (go != null)
                {
                    foreach (var link in SceneScanner.ScanGameObject(go))
                    {
                        _pendingLinks.Add(link);
                        AddTo(_pendingOutgoing, go.GetInstanceID(), link);
                        if (link.Target != null)
                            AddTo(_pendingIncoming, link.Target.GetInstanceID(), link);
                        if (link.TargetAsset != null)
                            AddTo(_pendingIncomingAsset, link.TargetAsset.GetInstanceID(), link);
                    }
                }

                if ((EditorApplication.timeSinceStartup - start) * 1000.0 > TimeBudgetMs)
                    return; // Budget exhausted — continue next tick.
            }

            // Queue drained: atomically publish the new index.
            _incoming.Clear();
            _outgoing.Clear();
            _incomingAsset.Clear();
            _allLinks.Clear();
            foreach (var kv in _pendingIncoming) _incoming[kv.Key] = kv.Value;
            foreach (var kv in _pendingOutgoing) _outgoing[kv.Key] = kv.Value;
            foreach (var kv in _pendingIncomingAsset) _incomingAsset[kv.Key] = kv.Value;
            _allLinks.AddRange(_pendingLinks);
            _pendingIncoming.Clear();
            _pendingOutgoing.Clear();
            _pendingIncomingAsset.Clear();
            _pendingLinks.Clear();

            _building = false;
            IsReady = true;
            IndexUpdated?.Invoke();
        }

        private static void AddTo(Dictionary<int, List<DependencyLink>> map, int key, DependencyLink link)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<DependencyLink>();
                map[key] = list;
            }
            list.Add(link);
        }
    }
}
