using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Height1079.EditorTools
{
    /// <summary>The physics sandbox lives in its own scene and its own build, so work on the body does not wait for the
    /// world to generate and does not disturb it. The scene is empty — <c>SandboxBoot</c> builds the range, the body
    /// and the panel at run time — and, like every generated scene here, it is not in git.</summary>
    public static class SandboxSetup
    {
        public const string ScenePath = "Assets/Scenes/Sandbox.unity";

        [MenuItem("1079/Physics sandbox/Open scene")]
        public static void Open()
        {
            Ensure();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("1079/Physics sandbox/Build and run (Mac)")]
        public static void BuildAndRunMac()
        {
            SandboxBuilds.Mac();
            EditorUtility.RevealInFinder("Builds/sandbox/mac");
        }

        /// <summary>Makes the scene if it is missing. Called from <c>ProjectSetup.EnsureAll</c>, so a fresh clone has
        /// the sandbox without anybody having to know it exists.</summary>
        public static void Ensure()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 3f, -14f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }

    /// <summary>Batch entry points: <c>-executeMethod Height1079.EditorTools.SandboxBuilds.Mac</c>. The world is not
    /// generated and not shipped — this player is the body, the range and nothing else, which is why it builds in
    /// seconds and can be rebuilt twenty times in an evening.</summary>
    public static class SandboxBuilds
    {
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/sandbox/mac/1079-sandbox.app");
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/sandbox/windows/1079-sandbox.exe");

        static void Build(BuildTarget target, string location)
        {
            SandboxSetup.Ensure();
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SandboxSetup.ScenePath },
                target = target,
                locationPathName = location,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            });
            Debug.Log($"Sandbox build {target}: {report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
