using System.Linq;
using SceneXRay.Editor.Windows;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using SceneXRay.Editor.UI;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// SceneXRay menus — core items at the top, niche tools under Advanced/.
    /// Hotkeys via Shortcut Manager (rebindable): Ctrl+Shift+Alt+J / K.
    /// </summary>
    public static class XRayMenuItems
    {
        // ── Hierarchy ──────────────────────────────────────────────

        [MenuItem("GameObject/SceneXRay/Show In Graph", false, 49)]
        private static void ShowInGraph()
        {
            var go = Selection.activeGameObject;
            if (go != null)
                XRayWindow.ShowWindowFocused(go);
            else
                XRayWindow.ShowWindow();
        }

        [MenuItem("GameObject/SceneXRay/Browse References", false, 50)]
        private static void BrowseReferences()
        {
            var go = Selection.activeGameObject;
            if (go != null)
                XRayReferencesWindow.ShowFor(go);
        }

        [MenuItem("GameObject/SceneXRay/Toggle Bookmark", false, 51)]
        private static void ToggleBookmarkHierarchy() => ToggleBookmark();

        [MenuItem("GameObject/SceneXRay/Show In Graph", true)]
        [MenuItem("GameObject/SceneXRay/Browse References", true)]
        [MenuItem("GameObject/SceneXRay/Toggle Bookmark", true)]
        private static bool ValidateHierarchySelection() => Selection.activeGameObject != null;

        // ── Tools: core ────────────────────────────────────────────

        // Open Graph View lives on XRayWindow (Ctrl+Shift+X).

        [MenuItem("Tools/SceneXRay/Global Search", false, 1)]
        private static void OpenGlobalSearch() => XRayGlobalSearchWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Fix Missing", false, 2)]
        private static void OpenFixMissing() => XRayFixMissingWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Toggle Bookmark", false, 3)]
        private static void ToggleBookmarkMenu() => ToggleBookmark();

        // Ctrl+Shift+Alt+J — unlikely to collide with Unity/Android defaults.
        [Shortcut("SceneXRay/Toggle Bookmark", KeyCode.J,
            ShortcutModifiers.Action | ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
        private static void ToggleBookmarkShortcut() => ToggleBookmark();

        private static void ToggleBookmark()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("SceneXRay", "Select a GameObject first.", "OK");
                return;
            }
            XRayBookmarks.Toggle(Selection.activeGameObject, out var msg);
            if (!string.IsNullOrEmpty(msg))
                Debug.Log($"SceneXRay: {msg}");
        }

        [MenuItem("Tools/SceneXRay/Bookmarks", false, 4)]
        private static void OpenBookmarksMenu() => XRayBookmarkWindow.ShowWindow();

        // Ctrl+Shift+Alt+K
        [Shortcut("SceneXRay/Open Bookmarks", KeyCode.K,
            ShortcutModifiers.Action | ShortcutModifiers.Shift | ShortcutModifiers.Alt)]
        private static void OpenBookmarksShortcut() => XRayBookmarkWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Settings", false, 5)]
        private static void OpenSettings() => SettingsService.OpenProjectSettings("Project/SceneXRay");

        // ── Tools: Advanced ────────────────────────────────────────

        [MenuItem("Tools/SceneXRay/Advanced/Clear Cache", false, 100)]
        private static void ClearCache() => XRayCacheManager.ClearCache();

        [MenuItem("Tools/SceneXRay/Advanced/Scene Diff", false, 103)]
        private static void OpenSceneDiff() => XRaySceneDiffWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Advanced/Save Snapshot", false, 104)]
        private static void SaveSnapshot()
        {
            var links = SceneScanner.ScanAllGameObjects();
            SnapshotManager.SaveSnapshot(links, $"Manual snapshot — {System.DateTime.Now:HH:mm}");
        }

        [MenuItem("Tools/SceneXRay/Advanced/Compare Snapshots…", false, 105)]
        private static void CompareSnapshots()
        {
            var snapshots = SnapshotManager.GetSnapshots();
            if (snapshots.Count < 2)
            {
                EditorUtility.DisplayDialog("SceneXRay",
                    "Need at least two snapshots to compare.\nUse Advanced > Save Snapshot first.", "OK");
                return;
            }

            // Newest two — the common case is "what changed since my last checkpoint".
            string a = snapshots[snapshots.Count - 2];
            string b = snapshots[snapshots.Count - 1];

            var rows = SnapshotManager.CompareSnapshots(a, b)
                .Split('\n')
                .Select(line => line.TrimEnd())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => new XRayReportWindow.Row { Label = line })
                .ToList();

            XRayReportWindow.Show(
                UI.XRayLocalization.GetText("compare_snapshots"),
                $"{System.IO.Path.GetFileNameWithoutExtension(a)}  →  {System.IO.Path.GetFileNameWithoutExtension(b)}",
                UI.XRayLocalization.GetText("scene_diff"),
                rows);
        }

        [MenuItem("Tools/SceneXRay/Advanced/References Window", false, 101)]
        private static void OpenReferencesWindow()
        {
            if (Selection.activeGameObject != null)
                XRayReferencesWindow.ShowFor(Selection.activeGameObject);
            else
                XRayReferencesWindow.ShowEmpty();
        }

        [MenuItem("Tools/SceneXRay/Advanced/Tutorial", false, 102)]
        private static void OpenTutorial() => XRayTutorial.ShowWindow();
    }
}
