using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneXRay.Editor.Core
{
    [InitializeOnLoad]
    public static class XRayReferenceIndex
    {
        private const double TimeBudgetMs = 8.0;
        private const double RebuildDebounceSeconds = 0.35;

        private static readonly Dictionary<ulong, List<DependencyLink>> _incoming = new();
        private static readonly Dictionary<ulong, List<DependencyLink>> _outgoing = new();

        private static readonly Dictionary<ulong, List<DependencyLink>> _incomingAsset = new();
        private static readonly List<DependencyLink> _allLinks = new();
        private static readonly Queue<GameObject> _scanQueue = new();

        private static bool _rebuildRequested;
        private static double _rebuildRequestTime;
        private static bool _building;

        public static event Action IndexUpdated;

        public static bool IsReady { get; private set; }

        static XRayReferenceIndex()
        {
            EditorApplication.update += OnEditorUpdate;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.playModeStateChanged += _ => RequestRebuild();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, _) => RequestRebuild();
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += _ => RequestRebuild();

            UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += _ => RequestRebuild();
            UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += _ => RequestRebuild();
            RequestRebuild();
        }

        public static IReadOnlyList<DependencyLink> GetIncoming(GameObject go)
            => go != null ? Live(_incoming, SceneXRayCompat.IdOf(go)) : Array.Empty<DependencyLink>();

        public static IReadOnlyList<DependencyLink> GetOutgoing(GameObject go)
            => go != null ? Live(_outgoing, SceneXRayCompat.IdOf(go)) : Array.Empty<DependencyLink>();

        public static IReadOnlyList<DependencyLink> GetIncomingForAsset(UnityEngine.Object asset)
            => asset != null ? Live(_incomingAsset, SceneXRayCompat.IdOf(asset)) : Array.Empty<DependencyLink>();

        public static IReadOnlyList<DependencyLink> AllLinks
        {
            get
            {
                PurgeDead(_allLinks);
                return _allLinks;
            }
        }

        private static IReadOnlyList<DependencyLink> Live(Dictionary<ulong, List<DependencyLink>> map, ulong key)
        {
            if (!map.TryGetValue(key, out var list))
                return Array.Empty<DependencyLink>();

            if (PurgeDead(list) && list.Count == 0)
            {
                map.Remove(key);
                return Array.Empty<DependencyLink>();
            }
            return list;
        }

        private static bool PurgeDead(List<DependencyLink> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!IsDead(list[i])) continue;
                list.RemoveAll(IsDead);
                return true;
            }
            return false;
        }

        private static bool IsDead(DependencyLink link)
        {
            if (link.Source == null) return true;

            if (link.IsMissing) return false;
            return link.Target == null && link.TargetAsset == null;
        }

        public static void RequestRebuild()
        {
            _rebuildRequested = true;
            _rebuildRequestTime = EditorApplication.timeSinceStartup;
        }

        public static void RebuildImmediate()
        {
            _rebuildRequested = false;
            BeginBuild();
            while (_building)
                ProcessSlice();
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (!AffectsReferences(ref stream, i)) continue;
                RequestRebuild();
                return;
            }
        }

        private static bool AffectsReferences(ref ObjectChangeEventStream stream, int index)
        {
            if (stream.GetEventType(index) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                return true;

            stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var args);
            var changed = SceneXRayCompat.ObjectFromChange(args);

            if (changed == null) return true;
            return changed is not Transform;
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

            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
                    _scanQueue.Enqueue(t.gameObject);
            }

            _pendingIncoming.Clear();
            _pendingOutgoing.Clear();
            _pendingIncomingAsset.Clear();
            _pendingLinks.Clear();
            _pendingObjects.Clear();
            _building = true;
            IsReady = false;
        }

        private static readonly Dictionary<ulong, List<DependencyLink>> _pendingIncoming = new();
        private static readonly Dictionary<ulong, List<DependencyLink>> _pendingOutgoing = new();
        private static readonly Dictionary<ulong, List<DependencyLink>> _pendingIncomingAsset = new();
        private static readonly List<DependencyLink> _pendingLinks = new();

        private static readonly List<GameObject> _pendingObjects = new();

        private static void ProcessSlice()
        {
            double start = EditorApplication.timeSinceStartup;
            while (_scanQueue.Count > 0)
            {
                var go = _scanQueue.Dequeue();
                if (go != null)
                {
                    _pendingObjects.Add(go);
                    foreach (var link in SceneScanner.ScanGameObject(go))
                        Record(link, go);
                }

                if ((EditorApplication.timeSinceStartup - start) * 1000.0 > TimeBudgetMs)
                    return;
            }

            if (SceneXRaySettings.instance.ScanImplicitDependencies)
            {
                foreach (var link in ImplicitDependencyScanner.Scan(_pendingObjects))
                    Record(link, link.Source);
            }

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
            _pendingObjects.Clear();

            _building = false;
            IsReady = true;
            IndexUpdated?.Invoke();
        }

        private static void Record(DependencyLink link, GameObject source)
        {
            if (link == null || source == null) return;

            _pendingLinks.Add(link);
            AddTo(_pendingOutgoing, SceneXRayCompat.IdOf(source), link);
            if (link.Target != null)
                AddTo(_pendingIncoming, SceneXRayCompat.IdOf(link.Target), link);
            if (link.TargetAsset != null)
                AddTo(_pendingIncomingAsset, SceneXRayCompat.IdOf(link.TargetAsset), link);
        }

        private static void AddTo(Dictionary<ulong, List<DependencyLink>> map, ulong key, DependencyLink link)
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
