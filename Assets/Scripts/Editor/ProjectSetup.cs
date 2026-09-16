using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Height1079.Runtime;

namespace Height1079.EditorTools
{
    /// <summary>Keeps the repository text-only: prefabs, the scene and the DEM resource are generated on first open and can be regenerated from the 1079 menu.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ResourcesDir = "Assets/Resources", ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureAll(false); };
        }

        [MenuItem("1079/Generate prefabs and scene")]
        public static void Regenerate() => EnsureAll(true);

        public static void EnsureAll(bool force)
        {
            Directory.CreateDirectory(ResourcesDir);
            EnsureDem(force);
            EnsureHikerPrefab(force);
            EnsureSessionPrefab(force);
            EnsureScene(force);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void EnsureDem(bool force)
        {
            string target = Path.Combine(ResourcesDir, "terrain.bytes");
            if (File.Exists(target) && !force) return;
            File.Copy("Assets/Data/terrain.png", target, true);
            AssetDatabase.ImportAsset(target);
        }

        static void EnsureHikerPrefab(bool force)
        {
            string path = Path.Combine(ResourcesDir, "Hiker.prefab");
            if (File.Exists(path) && !force) return;
            var go = new GameObject("Hiker", typeof(Rigidbody), typeof(CapsuleCollider), typeof(NetworkObject), typeof(OwnerNetworkTransform), typeof(HikerController));
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = new Vector3(0, .95f, 0); body.transform.localScale = new Vector3(.62f, .78f, .62f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere); hood.name = "Hood"; hood.transform.SetParent(visual.transform, false);
            hood.transform.localPosition = new Vector3(0, 1.62f, .02f); hood.transform.localScale = new Vector3(.46f, .5f, .46f);
            Object.DestroyImmediate(hood.GetComponent<Collider>());
            var pack = GameObject.CreatePrimitive(PrimitiveType.Cube); pack.name = "Pack"; pack.transform.SetParent(visual.transform, false);
            pack.transform.localPosition = new Vector3(0, 1.2f, -.33f); pack.transform.localScale = new Vector3(.46f, .5f, .26f);
            Object.DestroyImmediate(pack.GetComponent<Collider>());
            var coat = MakeMaterial("HikerCoat", new Color(.35f, .43f, .5f)); var packMat = MakeMaterial("HikerPack", new Color(.36f, .4f, .32f));
            body.GetComponent<Renderer>().sharedMaterial = coat; hood.GetComponent<Renderer>().sharedMaterial = coat; pack.GetComponent<Renderer>().sharedMaterial = packMat;
            var nt = go.GetComponent<OwnerNetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.Interpolate = true;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void EnsureSessionPrefab(bool force)
        {
            string path = Path.Combine(ResourcesDir, "NightSession.prefab");
            if (File.Exists(path) && !force) return;
            var go = new GameObject("NightSession", typeof(NetworkObject), typeof(NightSession));
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static Material MakeMaterial(string name, Color color)
        {
            string path = Path.Combine(ResourcesDir, name + ".mat");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.color = color; return existing; }
            var m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Glossiness", .05f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureScene(bool force)
        {
            if (File.Exists(ScenePath) && !force) { EnsureBuildScene(); return; }
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Everything else is created by Bootstrap at runtime; the camera exists so the empty scene is not black in the editor.
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 800, -200);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene();
        }

        static void EnsureBuildScene()
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == ScenePath)) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// <summary>Batch-mode builds: `Unity -batchmode -quit -projectPath unity -executeMethod Height1079.EditorTools.Builds.Mac` (or Windows).</summary>
    public static class Builds
    {
        static string[] Scenes => new[] { "Assets/Scenes/Main.unity" };

        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/mac/1079.app");
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/windows/1079.exe");

        static void Build(BuildTarget target, string location)
        {
            ProjectSetup.EnsureAll(false);
            PlayerSettings.productName = "1079 · Высота";
            PlayerSettings.companyName = "1079";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, target = target, locationPathName = location, options = BuildOptions.None });
            Debug.Log($"Build {target}: {report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
