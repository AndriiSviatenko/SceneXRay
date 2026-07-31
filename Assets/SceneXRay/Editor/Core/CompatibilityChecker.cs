using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// One-time install sanity check. The version guard alone was useless — on an
    /// unsupported Unity the assembly fails to compile before any warning can print —
    /// so this also verifies the assets the plugin needs at runtime are actually there.
    /// </summary>
    [InitializeOnLoad]
    public static class CompatibilityChecker
    {
        private const string StyleSheetPath = "Assets/SceneXRay/Editor/Styles/XRayStyles.uss";
        private const string CheckedKey = "SceneXRay_InstallChecked";

        static CompatibilityChecker()
        {
#if !UNITY_2022_3_OR_NEWER
            Debug.LogWarning($"SceneXRay: Unity {Application.unityVersion} is below the supported minimum. Please use Unity 2022.3 LTS or newer.");
#endif
            // Runs once per project: an incomplete copy (scripts without Styles/) renders
            // every window unstyled, which looks like a bug rather than a missing file.
            if (SessionState.GetBool(CheckedKey, false)) return;
            SessionState.SetBool(CheckedKey, true);

            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(StyleSheetPath) == null)
                    Debug.LogWarning(
                        $"SceneXRay: style sheet missing at {StyleSheetPath}. " +
                        "The windows will render unstyled — re-import the whole SceneXRay folder.");
            };
        }
    }
}
