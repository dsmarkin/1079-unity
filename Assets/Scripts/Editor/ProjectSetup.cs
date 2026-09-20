using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Height1079.Runtime;

namespace Height1079.EditorTools
{
    /// <summary>Keeps generated content out of git: prefabs, the scene and the whole world (terrain, art library, sites) are generated on first open
    /// from Assets/Data and Assets/Art, and can be regenerated from the 1079 menu.</summary>
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
            EnsureInputHandling();
            EnsureFogVariants();
            EnsureTerrainShaders();
            EnsureHikerModel(force);
            World.WorldImporter.Build(force);
            MakeMaterial("Flat", new Color(.886f, .91f, .93f));
            EnsureHikerPrefab(force);
            EnsureSessionPrefab(force);
            EnsureScene(force);
            SandboxSetup.Ensure();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>Batch entry point for the build scripts: generate the world and the prefabs, then quit. The player is built in a second
        /// Unity session, because one built in the same session as the generation shipped an empty terrain TextAsset.</summary>
        public static void Prepare()
        {
            EnsureAll(false);
            Builds.EnsureData();
            AssetDatabase.SaveAssets();
            RunErrand();
        }

        /// <summary>Runs <c>push-1079.command</c> next to the project when a file named <c>.push-now</c> sits beside it.
        /// An agent working through the desktop can write files but cannot make one executable, and a .command file
        /// without the executable bit cannot be launched from Finder — so the build script, which can be launched,
        /// carries the errand. The errand script removes the marker itself, so a build without it does nothing at all.
        /// Harmless to leave in: no marker, no errand.</summary>
        static void RunErrand()
        {
            string root = Directory.GetCurrentDirectory();
            string marker = Path.Combine(root, ".push-now"), script = Path.Combine(root, "push-1079.command");
            if (!File.Exists(marker) || !File.Exists(script)) return;
            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/bash", $"\"{script}\"")
                {
                    UseShellExecute = false, WorkingDirectory = root,
                });
                if (p == null) { Debug.LogWarning("1079: не удалось запустить push-1079.command"); return; }
                if (!p.WaitForExit(240000)) Debug.LogWarning("1079: push-1079.command не закончил за 4 минуты");
                else Debug.Log($"1079: push-1079.command отработал, код {p.ExitCode}");
            }
            catch (System.Exception e) { Debug.LogWarning("1079: push-1079.command — " + e.Message); }
        }

        /// <summary>Active input handling = Both: the Input System reads physical keys (WASD work with a Russian layout on macOS),
        /// the legacy manager keeps mouse axes. Takes effect after the editor restarts (the next check/build run).</summary>
        static void EnsureInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 2) return;
            prop.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("1079: active input handling set to Both (restart the editor to apply).");
        }

        /// <summary>Fog is switched on from code, while the scene itself has fog off, so "Automatic" fog stripping removed every fog
        /// shader variant from builds and the night fog never rendered. Keep linear, exp and exp² variants explicitly.</summary>
        static void EnsureFogVariants()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            bool changed = false;
            void Set(string name, int v)
            {
                var p = so.FindProperty(name);
                if (p != null && p.intValue != v) { p.intValue = v; changed = true; }
            }
            Set("m_FogStripping", 1);
            Set("m_FogKeepLinear", 1); Set("m_FogKeepExp", 1); Set("m_FogKeepExp2", 1);
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("1079: fog shader variants kept in builds (custom fog stripping).");
        }

        /// <summary>Terrain and sign shaders the player build would otherwise strip. Nothing in Resources references them by name:
        /// the terrain material points at "Nature/Terrain/Standard", and Unity picks the base-map, add-pass and grass
        /// shaders at run time. Without them the ground renders blank white and the grass does not draw at all.</summary>
        static void EnsureTerrainShaders()
        {
            string[] want =
            {
                "Nature/Terrain/Standard",
                "Hidden/TerrainEngine/Splatmap/Standard-Base",
                "Hidden/TerrainEngine/Splatmap/Standard-AddPass",
                "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
                "Hidden/TerrainEngine/Details/WavingDoublePass",
                "Hidden/TerrainEngine/Details/Vertexlit",
                "1079/Text3D",
                "Height1079/Ghost",
                "UI/Default",
            };
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            foreach (var name in want)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogWarning("1079: нет шейдера " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                changed = true;
            }
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("1079: шейдеры рельефа и травы добавлены в Always Included Shaders.");
        }

        static void EnsureHikerModel(bool force)
        {
            string hiker = Path.Combine(ResourcesDir, "hiker.bytes");
            if (File.Exists(hiker) && !force) return;
            if (File.Exists("Assets/Data/hiker-v1.glb")) { File.Copy("Assets/Data/hiker-v1.glb", hiker, true); AssetDatabase.ImportAsset(hiker); }
        }

        static void EnsureHikerPrefab(bool force)
        {
            string path = Path.Combine(ResourcesDir, "Hiker.prefab");
            if (File.Exists(path) && !force)
            {
                // Regenerate when a script moved files (Unity then reports the component as missing).
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null && existing.GetComponent<OwnerNetworkTransform>() != null && existing.GetComponent<HikerController>() != null && existing.GetComponent<HikerAnimator>() != null) return;
                AssetDatabase.DeleteAsset(path);
            }
            var go = new GameObject("Hiker", typeof(Rigidbody), typeof(CapsuleCollider), typeof(NetworkObject), typeof(OwnerNetworkTransform), typeof(HikerController), typeof(HikerAnimator));
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

        static Material MakeMaterial(string name, Color color, string shader = "Standard")
        {
            string path = Path.Combine(ResourcesDir, name + ".mat");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.color = color; return existing; }
            var m = new Material(Shader.Find(shader)) { color = color };
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

        /// <summary>Both scenes go into the build list: the game, and the physics sandbox the menu button loads.
        /// Main must stay first — it is the one that opens.</summary>
        static void EnsureBuildScene()
        {
            var want = new[] { ScenePath, SandboxSetup.ScenePath };
            if (EditorBuildSettings.scenes.Length == want.Length
                && EditorBuildSettings.scenes.Select(s => s.path).SequenceEqual(want)) return;
            EditorBuildSettings.scenes = want.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }
    }

    /// <summary>Batch-mode builds: `Unity -batchmode -quit -projectPath . -executeMethod Height1079.EditorTools.Builds.Mac` (or Windows).</summary>
    public static class Builds
    {
        static string[] Scenes => new[] { "Assets/Scenes/Main.unity", SandboxSetup.ScenePath };

        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/mac/1079.app");
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/windows/1079.exe");

        /// <summary>The first batch build after a script change has produced players whose big data files were empty
        /// ("Mismatched serialization in the builtin class 'TextAsset'" in player.log, then the game starts with no world):
        /// the TextAsset in the library is there but holds nothing. Re-import the data files and check them before building.</summary>
        public static void EnsureData()
        {
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles("Assets/Resources", "*.bytes", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    long onDisk = new FileInfo(file).Length;
                    if (asset != null && asset.dataSize == onDisk) break;
                    Debug.Log($"1079 build: re-importing {path} (library holds {(asset == null ? -1 : asset.dataSize)} of {onDisk} bytes)");
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void Build(BuildTarget target, string location)
        {
            ProjectSetup.EnsureAll(false);
            PlayerSettings.productName = "1079 · Высота";
            PlayerSettings.companyName = "1079";
            EnsureData();
            // always a clean player build: the incremental data cache goes stale whenever the generated world changes and the game then
            // starts with the default skybox and no terrain ("Mismatched serialization in the builtin class 'TextAsset'" in player.log).
            // A full build of this project takes about half a minute, so there is nothing to save.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, target = target, locationPathName = location, options = BuildOptions.CleanBuildCache });
            if (File.Exists("clean.build")) File.Delete("clean.build");
            Debug.Log($"Build {target}: {report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
