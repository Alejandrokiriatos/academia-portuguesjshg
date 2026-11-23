using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RustSurvival.EditorTools
{
    /// <summary>
    /// GameCI entry point. Builds always regenerate the complete prototype scene first,
    /// so the GitHub workflow needs no local Unity scene-generation step.
    /// </summary>
    public static class CIUnityBuild
    {
        public static void Build()
        {
            bool success = false;

            try
            {
                ProjectSetupWizard.GeneratePrototype();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                string outputPath = GetOutputPath(target);
                string outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                if (target == BuildTarget.Android)
                    EditorUserBuildSettings.buildAppBundle = false;

                BuildPlayerOptions options = new()
                {
                    scenes = GetEnabledScenes(),
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.None
                };

                Debug.Log("[Rust Survival CI] Building " + target + " to " + outputPath);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                Debug.Log("[Rust Survival CI] Result: " + summary.result +
                    " | Errors: " + summary.totalErrors +
                    " | Warnings: " + summary.totalWarnings +
                    " | Size: " + summary.totalSize + " bytes");

                success = summary.result == BuildResult.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                success = false;
            }

            // GameCI uses manualExit=true so the method controls the process exit code.
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && File.Exists(scene.path))
                    scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
                throw new InvalidOperationException(
                    "No hay escenas habilitadas. ProjectSetupWizard no generó PrototypeWorld.unity.");

            return scenes.ToArray();
        }

        private static string GetOutputPath(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.WebGL:
                    return "Builds/WebGL";
                case BuildTarget.Android:
                    return "Builds/Android/RustSurvival.apk";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Builds/Windows/RustSurvival.exe";
                default:
                    throw new NotSupportedException("Build target no configurado: " + target);
            }
        }
    }
}
