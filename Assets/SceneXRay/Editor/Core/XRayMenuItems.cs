using SceneXRay.Editor.Windows;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.UI;

namespace SceneXRay.Editor.Core
{
    public static class XRayMenuItems
    {
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

        [MenuItem("Assets/SceneXRay/Show In Graph", false, 30)]
        private static void ShowPrefabInGraph()
        {
            if (SelectedPrefabAsset() is GameObject prefab)
                XRayWindow.ShowWindowFocused(prefab);
        }

        [MenuItem("Assets/SceneXRay/Browse References", false, 31)]
        private static void BrowsePrefabReferences()
        {
            if (SelectedPrefabAsset() is GameObject prefab)
                XRayReferencesWindow.ShowFor(prefab);
        }

        [MenuItem("Assets/SceneXRay/Show In Graph", true)]
        [MenuItem("Assets/SceneXRay/Browse References", true)]
        private static bool ValidatePrefabAssetSelection() => SelectedPrefabAsset() != null;

        private static GameObject SelectedPrefabAsset()
        {
            return Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)
                ? go
                : null;
        }

        [MenuItem("Tools/SceneXRay/Global Search", false, 1)]
        private static void OpenGlobalSearch() => XRayGlobalSearchWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Fix Missing", false, 2)]
        private static void OpenFixMissing() => XRayFixMissingWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Toggle Bookmark %#&j", false, 3)]
        private static void ToggleBookmarkMenu() => ToggleBookmark();

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

        [MenuItem("Tools/SceneXRay/Bookmarks %#&k", false, 4)]
        private static void OpenBookmarksMenu() => XRayBookmarkWindow.ShowWindow();

        [MenuItem("Tools/SceneXRay/Settings", false, 5)]
        private static void OpenSettings() => SettingsService.OpenProjectSettings("Project/SceneXRay");

        [MenuItem("Tools/SceneXRay/Advanced/Clear Cache", false, 100)]
        private static void ClearCache() => XRayCacheManager.ClearCache();

        [MenuItem("Tools/SceneXRay/Advanced/Scene Diff", false, 103)]
        private static void OpenSceneDiff() => XRaySceneDiffWindow.ShowWindow();

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
