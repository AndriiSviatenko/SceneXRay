using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    [InitializeOnLoad]
    public static class CompatibilityChecker
    {
        private const string CheckedKey = "SceneXRay_InstallChecked";

        static CompatibilityChecker()
        {
            if (SessionState.GetBool(CheckedKey, false)) return;
            SessionState.SetBool(CheckedKey, true);

            EditorApplication.delayCall += () =>
            {
                if (SceneXRayCompat.LoadStyleSheet() == null)
                    Debug.LogWarning(
                        $"SceneXRay: the package style sheet could not be resolved (last path: " +
                        $"'{SceneXRayCompat.StyleSheetPath}'). " +
                        "The windows will render unstyled — re-import the whole SceneXRay folder.");
            };
        }
    }
}
