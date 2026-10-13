// Canonical build-script source. Unity imports the executable copy from
// Assets/Editor/BuildTools/WebGLBuilder.cs, because Editor menu scripts must live under Assets.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OblivionsEdge.BuildTools
{
    public static class WebGLBuilder
    {
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("OBLIVION'S EDGE/Build WebGL Preview")]
        public static void BuildWebGLPreview()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
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
            Debug.Log(report.summary.result == BuildResult.Succeeded
                ? $"WebGL build complete: {OutputPath} ({report.summary.totalSize} bytes)."
                : $"WebGL build failed: {report.summary.result}.");
        }
    }
}
#endif
