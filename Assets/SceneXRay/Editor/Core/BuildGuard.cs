using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;

namespace SceneXRay.Editor.Core
{
    /// <summary>
    /// Pre-build validation. In batchmode (CI) never shows dialogs: logs the report and
    /// fails the build only when FailBuildOnMissingReferences is enabled in settings.
    /// </summary>
    public class BuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = SceneXRaySettings.instance;
            if (!settings.EnableBuildGuard) return;

            var links = SceneScanner.ScanAllGameObjects();
            int missing = links.Count(l => l.IsMissing);
            int cycles = XRayAnalyzer.FindCycles(links).Count;
            if (missing == 0 && cycles == 0) return;

            string msg = $"SceneXRay Build Guard: missing references: {missing}, cyclic dependencies: {cycles}.";

            if (Application.isBatchMode)
            {
                if (settings.FailBuildOnMissingReferences)
                {
                    Debug.LogError(msg + " Build failed (FailBuildOnMissingReferences is enabled).");
                    throw new BuildFailedException("Build cancelled by SceneXRay Build Guard.");
                }
                Debug.LogWarning(msg);
                return;
            }

            if (!EditorUtility.DisplayDialog("SceneXRay Build Guard",
                    msg + "\n\nDo you want to continue building?", "Continue Build", "Cancel Build"))
            {
                throw new BuildFailedException("Build cancelled by SceneXRay Build Guard.");
            }
        }
    }
}
