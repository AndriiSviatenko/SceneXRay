using UnityEditor;
using UnityEngine;

namespace SceneXRay.Compat
{
    [InitializeOnLoad]
    internal static class UnsupportedVersionNotice
    {
        private const string ShownKey = "SceneXRay_UnsupportedVersionNotice";

        static UnsupportedVersionNotice()
        {
            if (SessionState.GetBool(ShownKey, false)) return;
            SessionState.SetBool(ShownKey, true);

            Debug.LogWarning(
                $"SceneXRay is disabled: Unity {Application.unityVersion} is older than the " +
                "supported minimum (Unity 6000.0 LTS). The plugin's assembly is skipped on purpose, so " +
                "nothing in your project is broken — but no SceneXRay window or menu will appear. " +
                "Upgrade to Unity 6000.0 LTS or newer to use it.");
        }
    }
}
