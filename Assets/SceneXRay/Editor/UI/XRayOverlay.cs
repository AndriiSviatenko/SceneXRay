using SceneXRay.Editor.Windows;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using SceneXRay.Editor.Core;

namespace SceneXRay.Editor.UI
{
    /// <summary>
    /// Scene view overlay drawing dependency lines for the selected GameObject.
    /// Repaints are throttled (~30 FPS) and only requested while animation is
    /// enabled and a SceneView is actually visible.
    /// </summary>
    [InitializeOnLoad]
    public static class XRayOverlay
    {
        private const double RepaintInterval = 1.0 / 30.0; // ~30 FPS cap

        private static GameObject _selectedObject;
        private static List<DependencyLink> _cachedLinks;
        private static double _lastAnimTime;
        private static double _lastRepaintTime;
        private static float _animationPhase;

        static XRayOverlay()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += UpdateAnimation;
        }

        private static void OnSelectionChanged()
        {
            _selectedObject = Selection.activeGameObject;
            var settings = SceneXRaySettings.instance;
            if (_selectedObject != null && settings.EnableXRayOverlay && !EditorUtility.IsPersistent(_selectedObject))
                _cachedLinks = SceneScanner.ScanGameObject(_selectedObject);
            else
                _cachedLinks = null;
            SceneView.RepaintAll();
        }

        private static void UpdateAnimation()
        {
            var settings = SceneXRaySettings.instance;
            if (!settings.EnableXRayOverlay || !settings.AnimateOverlay) return;
            if (_cachedLinks == null || _cachedLinks.Count == 0) return;
            if (SceneView.lastActiveSceneView == null) return;

            double now = EditorApplication.timeSinceStartup;
            _animationPhase += (float)(now - _lastAnimTime) * 1.5f;
            _lastAnimTime = now;
            if (_animationPhase > Mathf.PI * 2) _animationPhase -= Mathf.PI * 2;

            // Throttled repaint of the active SceneView only.
            if (now - _lastRepaintTime >= RepaintInterval)
            {
                _lastRepaintTime = now;
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var settings = SceneXRaySettings.instance;
            if (!settings.EnableXRayOverlay) return;
            if (_selectedObject == null || _cachedLinks == null || _cachedLinks.Count == 0) return;

            Color directColor = settings.DirectColor;
            Color eventColor = settings.EventColor;
            Color missingColor = settings.MissingColor;
            float lineWidth = settings.LineWidth;

            foreach (var link in _cachedLinks)
            {
                if (link.IsMissing)
                {
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = missingColor } };
                    Handles.Label(_selectedObject.transform.position + Vector3.up * 2f,
                        $"Missing: {link.SourcePropertyName}", style);
                    continue;
                }
                if (link.Target == null) continue;

                Vector3 src = _selectedObject.transform.position;
                Vector3 tgt = link.Target.transform.position;
                Color color = link.IsUnityEvent ? eventColor : directColor;
                if (settings.AnimateOverlay)
                    color.a = 0.6f + 0.4f * Mathf.Sin(_animationPhase + src.GetHashCode());

                Handles.color = color;
                Handles.DrawLine(src, tgt, lineWidth);

                Vector3 dir = tgt - src;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    Handles.ArrowHandleCap(0, tgt - dir * 0.5f, Quaternion.LookRotation(dir), 0.5f, EventType.Repaint);
                }

                Vector3 mid = (src + tgt) / 2f + Vector3.up * 0.5f;
                Handles.Label(mid, link.SourcePropertyName + (link.IsUnityEvent ? " (evt)" : ""));
            }
        }
    }
}
