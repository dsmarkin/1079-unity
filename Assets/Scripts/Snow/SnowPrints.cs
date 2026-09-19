using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Snow
{
    /// <summary>What walking leaves in the snow, for every body and every ground: boot prints where the feet land,
    /// a trodden patch every other step, a puff of snow at each footfall, snowfall slowly filling the lot, and the
    /// trail map — how many times this spot has been walked — that says how deep the next boot goes and how fast it
    /// moves (virgin powder is slow and swallows the boot; a path trodden three times is a path).
    ///
    /// This used to live inside the night's snow effects, and the sandbox — which cannot see the game — walked on
    /// snow that took no print. It is one thing now, in an assembly both can reach: the hiker's <c>SnowTrail</c>
    /// and the sandbox's camera rig both call <see cref="Step"/> and read <see cref="Sink"/>, and what they get is
    /// the same print, the same puff and the same rule for the depth. The caller owns the question of <em>where</em>
    /// the foot is and what the snow is like there — a terrain, a mesh, a plate under a skin of snow — and hands in
    /// a point on the visible surface, its normal and the depth a boot sinks there.</summary>
    public sealed class SnowPrints : MonoBehaviour
    {
        public static SnowPrints Instance { get; private set; }

        public const int FadeLevels = 6;
        const int MaxPrints = 2400, MaxTrodden = 900;
        /// <summary>The trail map's cell. A stride is 0.7 m, so a boot in the same cell is a boot on the same path.</summary>
        const float Cell = .8f;
        /// <summary>Where the editor's factory bakes the materials (under <c>Resources</c>).</summary>
        public const string MaterialDir = "World/Materials/SnowFx/";

        /// <summary>Snow is falling and filling the prints: about ten minutes of calm snowfall erases a print, a
        /// blizzard (<see cref="Storm"/>) does it in a minute and a half. The sandbox's day does not snow, and its
        /// prints stay until the range is rebuilt.</summary>
        public bool Snowing;
        public bool Storm;

        Material[] printMats, trodMats;
        Mesh printMesh, trodMesh;
        ParticleSystem puffs;

        sealed class Decal { public Transform T; public MeshRenderer R; public float Fill; public int Level = -1; public long Cell; public bool Trod; }
        readonly Queue<Decal> prints = new Queue<Decal>(), trodden = new Queue<Decal>();
        readonly Dictionary<long, int> passes = new Dictionary<long, int>();
        Transform decalRoot;
        float fadeTick;

        /// <summary>Under <paramref name="parent"/> when given, so the prints die with whatever owns them (the
        /// sandbox); otherwise kept across scene loads the way the game keeps its weather.</summary>
        public static SnowPrints Create(Transform parent = null)
        {
            var go = new GameObject("SnowPrints", typeof(SnowPrints));
            if (parent != null) go.transform.SetParent(parent, false);
            else DontDestroyOnLoad(go);
            return go.GetComponent<SnowPrints>();
        }

        static Material Mat(string name) => Resources.Load<Material>(MaterialDir + name);

        void Awake()
        {
            Instance = this;
            decalRoot = new GameObject("SnowDecals").transform;
            decalRoot.SetParent(transform, false);
            printMats = new Material[FadeLevels]; trodMats = new Material[FadeLevels];
            for (int i = 0; i < FadeLevels; i++) { printMats[i] = Mat("Footprint_" + i); trodMats[i] = Mat("Trodden_" + i); }
            // no baked materials (the world has not been generated): paint them now, the same pictures
            if (printMats[0] == null)
            {
                var print = SnowPrintArt.Footprint();
                var trod = SnowPrintArt.Trodden();
                for (int i = 0; i < FadeLevels; i++)
                {
                    float a = 1f - i / (float)FadeLevels;
                    printMats[i] = SnowPrintArt.LitFade("Footprint_" + i, print, new Color(1, 1, 1, a));
                    trodMats[i] = SnowPrintArt.LitFade("Trodden_" + i, trod, new Color(1, 1, 1, a * .75f));
                }
            }
            printMesh = Quad(.13f, .30f); trodMesh = Quad(.95f, 1.15f);
            var puffMat = Mat("SnowPuff");
            if (puffMat == null) puffMat = Mat("Snowflakes");
            if (puffMat != null) puffs = Puffs(puffMat);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static Mesh Quad(float w, float l)
        {
            var m = new Mesh { name = "decal" };
            m.vertices = new[] { new Vector3(-w / 2, 0, -l / 2), new Vector3(-w / 2, 0, l / 2), new Vector3(w / 2, 0, l / 2), new Vector3(w / 2, 0, -l / 2) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        ParticleSystem Puffs(Material mat)
        {
            var ps = new GameObject("SnowPuffs", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .09f);
            main.gravityModifier = .35f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1, 1, 1, .8f);
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .12f; shape.rotation = new Vector3(-90f, 0f, 0f);
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2; sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (!Snowing) return;
            fadeTick += Time.deltaTime;
            if (fadeTick >= .5f) { FillDecals(fadeTick, Storm); fadeTick = 0f; }
        }

        void FillDecals(float dt, bool storm)
        {
            float rate = dt / (storm ? 90f : 600f);
            Fill(prints, printMats, rate);
            Fill(trodden, trodMats, rate * .6f);
        }

        void Fill(Queue<Decal> queue, Material[] mats, float rate)
        {
            int n = queue.Count;
            for (int i = 0; i < n; i++)
            {
                var d = queue.Dequeue();
                d.Fill += rate;
                if (d.Fill >= 1f) { Release(d); continue; }
                SetLevel(d, mats);
                queue.Enqueue(d);
            }
        }

        void SetLevel(Decal d, Material[] mats)
        {
            int level = Mathf.Clamp(Mathf.FloorToInt(d.Fill * FadeLevels), 0, FadeLevels - 1);
            if (level == d.Level) return;
            d.Level = level;
            if (mats[level] != null) d.R.sharedMaterial = mats[level];
        }

        void Release(Decal d)
        {
            if (!d.Trod && passes.TryGetValue(d.Cell, out int c)) { if (c <= 1) passes.Remove(d.Cell); else passes[d.Cell] = c - 1; }
            if (d.T != null) Destroy(d.T.gameObject);
        }

        /// <summary>Everything gone at once: the ground under the prints was rebuilt.</summary>
        public void Clear()
        {
            while (prints.Count > 0) Release(prints.Dequeue());
            while (trodden.Count > 0) Release(trodden.Dequeue());
            passes.Clear();
        }

        static long Key(float x, float z) => ((long)Mathf.FloorToInt(x / Cell) << 32) ^ (uint)Mathf.FloorToInt(z / Cell);

        /// <summary>How many prints lie in this spot (0 = virgin snow).</summary>
        public int Passes(float x, float z) => passes.TryGetValue(Key(x, z), out int c) ? c : 0;

        /// <summary>How much of the snow's depth a boot still sinks after this many passes: all of it in virgin snow,
        /// a bit over half on a path walked once or twice, a third on a trodden path.</summary>
        public static float SinkFactor(int passes) => passes >= 3 ? .3f : passes > 0 ? .6f : 1f;

        /// <summary>How deep a boot goes here, given how deep the snow is: the depth, less what has been trodden.</summary>
        public float Sink(float x, float z, float depth) => depth * SinkFactor(Passes(x, z));

        /// <summary>Movement factor for trail-breaking: virgin powder is slow, a path trodden by others is fast.</summary>
        public float SpeedFactor(float x, float z, float depth)
        {
            float fresh = Mathf.Lerp(1f, .62f, Mathf.InverseLerp(.05f, .32f, depth));
            int p = Passes(x, z);
            return p >= 3 ? 1f : Mathf.Lerp(fresh, 1f, p / 3f);
        }

        /// <summary>A foot has landed. <paramref name="foot"/> is on the snow you can see — the print is the picture
        /// of the hole, drawn on the surface, whatever the boot is really standing on underneath — and
        /// <paramref name="depth"/> is how deep the snow is there, which sizes the puff.</summary>
        public void Step(Vector3 foot, Vector3 forward, Vector3 normal, float depth, bool trodPatch)
        {
            if (printMesh == null) return;
            var along = Vector3.ProjectOnPlane(forward, normal);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.ProjectOnPlane(Vector3.forward, normal);
            var rot = Quaternion.LookRotation(along.normalized, normal);
            prints.Enqueue(Spawn(foot + normal * .025f, rot, printMesh, printMats, false, Random.Range(.9f, 1.1f)));
            if (prints.Count > MaxPrints) Release(prints.Dequeue());
            if (trodPatch)
            {
                var twist = rot * Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
                trodden.Enqueue(Spawn(foot + normal * .018f, twist, trodMesh, trodMats, true, Random.Range(.85f, 1.2f)));
                if (trodden.Count > MaxTrodden) Release(trodden.Dequeue());
            }
            if (puffs != null)
            {
                var emit = new ParticleSystem.EmitParams { position = foot + Vector3.up * .05f, applyShapeToPosition = true };
                puffs.Emit(emit, Mathf.RoundToInt(Mathf.Lerp(3, 12, depth / .32f)));
            }
        }

        Decal Spawn(Vector3 pos, Quaternion rot, Mesh mesh, Material[] mats, bool trod, float scale)
        {
            var go = new GameObject(trod ? "Trodden" : "Print", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(decalRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var d = new Decal { T = go.transform, R = r, Trod = trod, Cell = Key(pos.x, pos.z) };
            if (!trod) passes[d.Cell] = Passes(pos.x, pos.z) + 1;
            SetLevel(d, mats);
            return d;
        }
    }
}
