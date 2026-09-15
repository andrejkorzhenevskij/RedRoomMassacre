using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RRM.Editor
{
    public static class DesktopBuild
    {
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows", "RRM.exe");
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Linux", "RRM.x86_64");

        private static void Build(BuildTarget target, string platform, string executable)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException($"Install the {platform} build support module for Unity {Application.unityVersion}.");
            // Target switching needs a domain reload, so CLI must select it before this method runs.
            if (EditorUserBuildSettings.activeBuildTarget != target
                || EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new BuildFailedException($"Start Unity with -buildTarget {target} -standaloneBuildSubtarget Player.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatPrototypeBuilder.ScenePath))
                throw new BuildFailedException("CombatPrototype scene is missing.");

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", platform));
            string playerPath = Path.Combine(output, executable);
            var originalBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                if (originalBackend != ScriptingImplementation.Mono2x)
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                // ponytail: these two fixed folders contain generated builds only; no arbitrary output paths.
                if (Directory.Exists(output)) Directory.Delete(output, true);
                Directory.CreateDirectory(output);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CombatPrototypeBuilder.ScenePath },
                    locationPathName = playerPath,
                    target = target,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.CompressWithLz4HC
                });
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new BuildFailedException($"{platform} build {report.summary.result}: {report.summary.totalErrors} errors. See the build log.");
                if (!File.Exists(playerPath) || !Directory.Exists(Path.Combine(output, "RRM_Data"))
                    || !File.Exists(Path.Combine(output, platform == "Windows" ? "UnityPlayer.dll" : "UnityPlayer.so")))
                    throw new BuildFailedException("Unity reported success but the expected Player files are missing.");
                Debug.Log($"RRM DESKTOP BUILD PASSED: {target}, Player, Mono, scene={CombatPrototypeBuilder.ScenePath}, "
                    + $"output={playerPath}, bytes={report.summary.totalSize}, warnings={report.summary.totalWarnings}, errors=0");
            }
            finally
            {
                if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != originalBackend)
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, originalBackend);
            }
        }
    }
}
