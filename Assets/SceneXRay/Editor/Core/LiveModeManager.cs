using UnityEditor;
using System;

namespace SceneXRay.Editor.Core
{
    [InitializeOnLoad]
    public static class LiveModeManager
    {
        private const double DebounceSeconds = 0.5;

        public static event Action OnSceneChanged;
        private static bool _enabled;
        private static bool _changePending;
        private static double _changeTime;

        static LiveModeManager()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.update += OnUpdate;
            _enabled = EditorPrefs.GetBool("SceneXRay_LiveMode", false);
        }

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                EditorPrefs.SetBool("SceneXRay_LiveMode", value);
                if (value) OnSceneChanged?.Invoke();
            }
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (!_enabled || stream.length == 0) return;
            _changePending = true;
            _changeTime = EditorApplication.timeSinceStartup;
        }

        private static void OnUpdate()
        {
            if (!_changePending) return;
            if (EditorApplication.timeSinceStartup - _changeTime < DebounceSeconds) return;
            _changePending = false;
            OnSceneChanged?.Invoke();
        }
    }
}
