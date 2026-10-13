#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OblivionsEdge.BuildTools
{
    /// <summary>Creates a playable WebGL build from the scenes enabled in Build Settings.</summary>
    public static class WebGLBuilder
    {
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("OBLIVION'S EDGE/Build WebGL Preview")]
        public static void BuildWebGLPreview()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("WebGL build cancelled: enable at least one scene in File > Build Settings.");
                return;
            }

            Directory.CreateDirectory(OutputPath);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"WebGL build complete: {OutputPath} ({report.summary.totalSize} bytes).");
            }
            else
            {
                Debug.LogError($"WebGL build failed: {report.summary.result}.");
            }
        }
    }
}
#endif
