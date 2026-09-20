using GLTFast;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>A skinned model from a file on the sandbox's body — the test of <see cref="PuppetSkeleton"/>: the
    /// puppet can wear any skinned model with a skeleton, and this wears the one the repository has, the game's own
    /// <c>hiker-v1.glb</c> (shipped as <c>Resources/hiker.bytes</c>), loaded through glTFast the way the game's
    /// HikerAnimator loads it. The file's five animation clips are not wanted: the puppet poses the bones itself, so
    /// nothing that could animate them is left on the instance.
    ///
    /// The sculpted figure (<see cref="PuppetFigure"/>) is the sandbox's look and its default. The model is put on
    /// with B — or from the start with <c>-glb</c> on the command line, which is how the shot script photographs
    /// it — and taken off with B again, so the two can be compared on the same walk. The file is read the first
    /// time it is asked for and never before; when it is missing or will not bind, the log says why and B says so.</summary>
    public sealed class SandboxHiker : MonoBehaviour
    {
        public const string Resource = "hiker";
        /// <summary>The player's choice: the model on the body, or the sculpted figure (the default).</summary>
        public static bool WantModel;

        GltfImport gltf;
        bool loading, ready, failed;
        /// <summary>The body whose figure the model has been bound to.</summary>
        Puppet.Puppet dressed;
        /// <summary>A press of B is waiting for the model to be loaded or worn: say so when it is.</summary>
        bool announce;

        /// <summary>The file is loaded; a body asking for the model gets a copy.</summary>
        public bool Ready => ready;
        /// <summary>The file could not be loaded or worn: the sculpted figure is all there is.</summary>
        public bool Failed => failed;

        /// <summary><c>-glb</c> on the command line: the model from the start.</summary>
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-glb") return true;
                return false;
            }
        }

        const string OnMessage = "фигура: модель hiker-v1 на скелете (B — снять)";
        const string OffMessage = "фигура: процедурная, эталон песочницы (B — надеть модель hiker-v1)";

        /// <summary>One line for the panel's button.</summary>
        public string Status
        {
            get
            {
                if (failed) return "модель не загрузилась — фигура процедурная";
                if (WantModel && !ready) return "модель hiker-v1 грузится…";
                return WantModel ? "фигура: модель hiker-v1 (B — снять)" : "фигура: процедурная (B — надеть модель hiker-v1)";
            }
        }

        void Awake()
        {
            if (Requested) WantModel = true;
        }

        async void Load()
        {
            loading = true;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
            {
                Fail($"Resources/{Resource}.bytes is missing or empty — the sculpted figure stays");
                return;
            }
            var import = new GltfImport();
            bool ok = false;
            try { ok = await import.Load(asset.bytes, null, new ImportSettings { AnimationMethod = AnimationMethod.None }); }
            catch (System.Exception e) { Debug.LogWarning("sandbox: hiker GLB threw while loading — " + e.Message); }
            if (this == null) { import.Dispose(); return; }      // the sandbox was left while the file was being read
            if (!ok) { import.Dispose(); Fail("hiker GLB failed to load — the sculpted figure stays"); return; }
            gltf = import;
            ready = true;
        }

        void Fail(string why)
        {
            failed = true; loading = false;
            Debug.LogWarning("sandbox: " + why);
            if (announce) { announce = false; SandboxHud.Say("модели нет — фигура процедурная (причина в логе)"); }
        }

        void OnDestroy()
        {
            gltf?.Dispose();
            gltf = null;
        }

        /// <summary>The body is thrown away and rebuilt whenever a number changes (SandboxBoot.Spawn): a new one
        /// that is meant to wear the model is dressed the frame it appears. LateUpdate, so the figure has been bound
        /// to it first. Nothing is loaded until the model is first asked for.</summary>
        void LateUpdate()
        {
            var boot = SandboxBoot.Instance;
            var body = boot != null ? boot.Body : null;
            if (body == null || !WantModel || failed) return;
            if (!ready) { if (!loading) Load(); return; }
            if (body == dressed) return;
            dressed = body;
            Dress(body);
        }

        async void Dress(Puppet.Puppet body)
        {
            var figure = body.GetComponent<PuppetFigure>();
            if (figure == null) return;
            var wrapper = new GameObject("Model");
            bool ok = false;
            try { ok = await gltf.InstantiateMainSceneAsync(wrapper.transform); }
            catch (System.Exception e) { Debug.LogWarning("sandbox: hiker GLB threw while instantiating — " + e.Message); }
            // the body may have gone while the instance was being built: then so does the instance
            if (this == null || body == null || figure == null) { if (wrapper != null) Destroy(wrapper); return; }
            if (!ok) { Destroy(wrapper); Fail("hiker GLB could not be instantiated — the sculpted figure stays"); return; }
            // the file's clips must never move these bones — the figure does
            foreach (var a in wrapper.GetComponentsInChildren<Animation>(true)) { a.Stop(); Destroy(a); }
            foreach (var a in wrapper.GetComponentsInChildren<Animator>(true)) Destroy(a);
            // glTFast's material as it is — vertex colours and all, the same one the game draws this model with
            foreach (var r in wrapper.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (!figure.Wear(wrapper.transform, WantModel))
            {
                Destroy(wrapper);
                Fail("hiker GLB has no skeleton the puppet can wear — the sculpted figure stays (see the warning above)");
                return;
            }
            if (announce && WantModel) { announce = false; SandboxHud.Say(OnMessage); }
        }

        /// <summary>B: the other figure. Says what it did, or that it is doing it.</summary>
        public void Toggle() => Set(!WantModel);

        /// <summary>Put the model on the current body (true) or show the sculpted figure (false), and remember the
        /// choice for the bodies to come. The first time the model is asked for it is loaded first, and worn — and
        /// announced — the moment it is.</summary>
        public void Set(bool model)
        {
            WantModel = model;
            var boot = SandboxBoot.Instance;
            var figure = boot != null && boot.Body != null ? boot.Body.GetComponent<PuppetFigure>() : null;
            if (!model)
            {
                announce = false;
                figure?.WearModel(false);
                SandboxHud.Say(OffMessage);
                return;
            }
            if (failed) { SandboxHud.Say("модели нет — фигура процедурная (причина в логе)"); return; }
            if (figure != null && figure.HasModel) { figure.WearModel(true); SandboxHud.Say(OnMessage); return; }
            // not loaded, or this body not dressed yet: LateUpdate does both, and the message follows
            announce = true;
            SandboxHud.Say(ready ? "надеваем модель…" : "модель hiker-v1 грузится…");
        }
    }
}
