using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>What the sandbox's body looks like. Three looks, on B in turn, all riding the same physics and
    /// the same solved pose (a model is worn over it by <see cref="PuppetSkeleton"/>, its bones aimed along the
    /// solved segments):
    /// <list type="bullet">
    /// <item>the Quaternius <b>Adventurer</b> — a skinned model from a file, dressed in the team's palette by the
    /// editor (FigureFactory) and shipped as <c>Resources/Sandbox/Adventurer.prefab</c>. The default: this is the
    /// climber now (docs/ART.md §7);</item>
    /// <item>the game's own <b>hiker-v1.glb</b>, through glTFast the way HikerAnimator loads it, its five clips
    /// dropped — kept for comparison;</item>
    /// <item>the figure <b>sculpted in code</b> (<see cref="PuppetFigure"/>) — the fallback when the prefab is
    /// missing, and on its way out.</item>
    /// </list>
    /// Whichever is on, the first person is the same: the body hidden, the eye's own arms in front of it.
    /// <c>-glb</c> or <c>-sculpted</c> on the command line starts with that look instead.</summary>
    public sealed class SandboxFigure : MonoBehaviour
    {
        public enum Look { Adventurer, HikerGlb, Sculpted }

        public const string AdventurerResource = "Sandbox/Adventurer", GlbResource = "hiker";
        /// <summary>The player's choice, remembered across bodies.</summary>
        public static Look Want = Look.Adventurer;
        /// <summary>What the current body actually wears.</summary>
        public Look Current { get; private set; } = Look.Sculpted;
        /// <summary>The hiker GLB could not be loaded or worn; asking for it gives the sculpted figure.</summary>
        public bool GlbFailed { get; private set; }
        /// <summary>The Adventurer prefab is not in Resources; asking for it gives the sculpted figure.</summary>
        public bool AdventurerMissing { get; private set; }

        GltfImport gltf;
        Task<bool> glbLoad;
        Puppet.Puppet dressed;
        Look dressedLook;
        /// <summary>Which look the figure's bound model is, per body, so that going Adventurer → sculpted →
        /// Adventurer shows the same instance instead of building another.</summary>
        Look boundLook = Look.Sculpted;
        /// <summary>Bumped on every change of body or wish, so a model still being built for an older one is
        /// thrown away when it arrives.</summary>
        int ticket;

        public static bool Flag(string flag)
        {
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == flag) return true;
            return false;
        }

        void Awake()
        {
            if (Flag("-glb")) Want = Look.HikerGlb;
            else if (Flag("-sculpted")) Want = Look.Sculpted;
        }

        public static string Name(Look l) => l switch
        {
            Look.Adventurer => "Adventurer (Quaternius, в палитре)",
            Look.HikerGlb => "hiker-v1.glb, модель игры",
            _ => "процедурная, из кода",
        };

        static Look After(Look l) => (Look)(((int)l + 1) % 3);

        /// <summary>One line for the panel's button.</summary>
        public string Status => Current == Want
            ? $"фигура: {Name(Current)} · B — {Name(After(Want))}"
            : $"фигура: {Name(Current)} (просили {Name(Want)}) · B — {Name(After(Want))}";

        string Said(Look l) => $"фигура: {Name(l)} · B — {Name(After(l))}";

        void OnDestroy()
        {
            gltf?.Dispose();
            gltf = null;
        }

        /// <summary>The body is thrown away and rebuilt whenever a number changes (SandboxBoot.Spawn): a new one
        /// is dressed the frame it appears, in the look wanted. LateUpdate, so the figure has been bound to it first.</summary>
        void LateUpdate()
        {
            var boot = SandboxBoot.Instance;
            var body = boot != null ? boot.Body : null;
            if (body == null || (body == dressed && dressedLook == Want)) return;
            var figure = body.GetComponent<PuppetFigure>();
            if (figure == null) return;
            if (body != dressed) boundLook = Look.Sculpted;      // a fresh body wears nothing yet
            dressed = body; dressedLook = Want;
            ticket++;
            switch (Want)
            {
                case Look.Sculpted: Sculpt(figure, Said(Look.Sculpted)); break;
                case Look.Adventurer: WearAdventurer(figure); break;
                default: WearGlb(figure, ticket); break;
            }
        }

        /// <summary>B: the next look. What actually happens is said once it has.</summary>
        public void Next() => Set(After(Want));

        /// <summary>Ask for a look on the current body, and remember it for the bodies to come.</summary>
        public void Set(Look look)
        {
            Want = look;
            if (look == Look.HikerGlb && !GlbFailed && gltf == null) SandboxHud.Say("модель hiker-v1 грузится…");
        }

        void Sculpt(PuppetFigure figure, string say)
        {
            figure.WearModel(false);
            Current = Look.Sculpted;
            SandboxHud.Say(say);
        }

        void Fallback(PuppetFigure figure, string why)
        {
            Debug.LogWarning("sandbox: " + why);
            Sculpt(figure, "модели нет — фигура процедурная (причина в логе) · B — " + Name(After(Want)));
        }

        void WearAdventurer(PuppetFigure figure)
        {
            if (boundLook == Look.Adventurer && figure.HasModel)
            {
                figure.WearModel(true);
                Current = Look.Adventurer;
                SandboxHud.Say(Said(Look.Adventurer));
                return;
            }
            var prefab = Resources.Load<GameObject>(AdventurerResource);
            if (prefab == null)
            {
                AdventurerMissing = true;
                Fallback(figure, $"Resources/{AdventurerResource}.prefab is missing — open the project in the editor once (SandboxSetup builds it) — the sculpted figure stays");
                return;
            }
            var root = Instantiate(prefab);
            root.name = "Adventurer";
            Strip(root.transform);
            if (!figure.Wear(root.transform))
            {
                Destroy(root);
                Fallback(figure, "the Adventurer's skeleton could not be worn — the sculpted figure stays (see the warning above)");
                return;
            }
            boundLook = Look.Adventurer;
            Current = Look.Adventurer;
            SandboxHud.Say(Said(Look.Adventurer));
        }

        async void WearGlb(PuppetFigure figure, int my)
        {
            if (boundLook == Look.HikerGlb && figure.HasModel)
            {
                figure.WearModel(true);
                Current = Look.HikerGlb;
                SandboxHud.Say(Said(Look.HikerGlb));
                return;
            }
            if (!GlbFailed && gltf == null)
            {
                await (glbLoad ??= LoadGlb());
                if (this == null || my != ticket || figure == null) return;
            }
            if (GlbFailed) { Fallback(figure, "hiker GLB is not available — the sculpted figure stays"); return; }
            var wrapper = new GameObject("Model");
            bool ok = false;
            try { ok = await gltf.InstantiateMainSceneAsync(wrapper.transform); }
            catch (System.Exception e) { Debug.LogWarning("sandbox: hiker GLB threw while instantiating — " + e.Message); }
            // the body or the wish may have changed while the instance was being built: then it is not wanted
            if (this == null || my != ticket || figure == null) { if (wrapper != null) Destroy(wrapper); return; }
            if (!ok) { Destroy(wrapper); GlbFailed = true; Fallback(figure, "hiker GLB could not be instantiated — the sculpted figure stays"); return; }
            Strip(wrapper.transform);
            if (!figure.Wear(wrapper.transform))
            {
                Destroy(wrapper);
                GlbFailed = true;
                Fallback(figure, "hiker GLB has no skeleton the puppet can wear — the sculpted figure stays (see the warning above)");
                return;
            }
            boundLook = Look.HikerGlb;
            Current = Look.HikerGlb;
            SandboxHud.Say(Said(Look.HikerGlb));
        }

        /// <summary>The game's hiker-v1.glb out of Resources, once, the way HikerAnimator reads it — but with the
        /// file's clips left out: the puppet poses the bones.</summary>
        async Task<bool> LoadGlb()
        {
            var asset = Resources.Load<TextAsset>(GlbResource);
            if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
            {
                GlbFailed = true;
                Debug.LogWarning($"sandbox: Resources/{GlbResource}.bytes is missing or empty");
                return false;
            }
            var import = new GltfImport();
            bool ok = false;
            try { ok = await import.Load(asset.bytes, null, new ImportSettings { AnimationMethod = AnimationMethod.None }); }
            catch (System.Exception e) { Debug.LogWarning("sandbox: hiker GLB threw while loading — " + e.Message); }
            if (this == null) { import.Dispose(); return false; }      // the sandbox was left meanwhile
            if (!ok) { import.Dispose(); GlbFailed = true; Debug.LogWarning("sandbox: hiker GLB failed to load"); return false; }
            gltf = import;
            return true;
        }

        /// <summary>Nothing on the instance may move its bones but the figure; and it throws a shadow like the
        /// rest of the body.</summary>
        static void Strip(Transform root)
        {
            foreach (var a in root.GetComponentsInChildren<Animation>(true)) { a.Stop(); Destroy(a); }
            foreach (var a in root.GetComponentsInChildren<Animator>(true)) Destroy(a);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
