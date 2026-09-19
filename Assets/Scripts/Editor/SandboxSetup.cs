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

        /// <summary>The sandbox runs in its own assembly and cannot see the editor's world library, so the ground it
        /// walks on used to be flat grey paint while the game walks on PolyHaven scans. These materials are built from
        /// the same scans and written into Resources, where the sandbox can load them by name — the snow underfoot is
        /// then literally the game's snow, not a lookalike.</summary>
        const string MatDir = "Assets/Resources/Sandbox";

        static void EnsureMaterials()
        {
            Directory.CreateDirectory(MatDir);
            // the prints and puffs the body leaves in the snow are the game's own (Height1079.Snow.SnowPrints), and
            // their materials are baked by the world pipeline; a sandbox built before the world exists bakes just them
            if (AssetDatabase.LoadAssetAtPath<Material>(World.WorldPaths.Generated + "/Materials/SnowFx/Footprint_0.mat") == null)
                World.SnowFxFactory.Build();
            Mat("Snow", "snow_02", 6f, .22f, new Color(.97f, .98f, 1f));
            Mat("Crust", "snow_03", 9f, .34f, new Color(.93f, .95f, 1f));
            Mat("Rock", "lichen_rock", 4f, .12f, new Color(.72f, .70f, .66f));
            Cloth();
        }

        /// <summary>The climber's material is made at run time (its atlas is painted in code), but the shader variant
        /// it needs is not: Standard's detail-map path — the cloth weave, tiled through the second UV set — is a
        /// keyword variant, and a build only carries the variants some material in it uses. This asset is that
        /// material: no textures, just the keyword and the UV-set switch. <c>PuppetSkinTexture</c> starts its
        /// material from it, so the player and the editor draw the same thing.</summary>
        static void Cloth()
        {
            string path = MatDir + "/Cloth.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var m = existing ?? new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_UVSec", 1f);
            m.EnableKeyword("_DETAIL_MULX2");
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        static void Mat(string name, string id, float tile, float smoothness, Color tint)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            string dir = "Assets/Art/ThirdParty/PolyHaven/Textures/" + id;
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_diff_1k.jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_nor_gl_1k.jpg");
            if (diff == null) { if (existing == null) Debug.LogWarning("1079 sandbox: нет текстуры " + id); return; }
            var m = existing ?? new Material(Shader.Find("Standard"));
            m.mainTexture = diff;
            m.mainTextureScale = Vector2.one * tile;
            m.color = tint;
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (nor != null) { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", nor); m.SetTextureScale("_BumpMap", Vector2.one * tile); }
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        /// <summary>Makes the scene if it is missing. Called from <c>ProjectSetup.EnsureAll</c>, so a fresh clone has
        /// the sandbox without anybody having to know it exists.</summary>
        public static void Ensure()
        {
            EnsureMaterials();
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
