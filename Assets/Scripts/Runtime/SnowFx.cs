using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Snow around the players: layered snowfall that drifts and swirls, boot prints and trodden paths that snowfall slowly fills,
    /// puffs of snow at every step, and the trail map used for trail-breaking (fresh snow is slower than a trodden path).</summary>
    public sealed class SnowFx : MonoBehaviour
    {
        public static SnowFx Instance { get; private set; }

        const int FadeLevels = 6;
        const int MaxPrints = 2400, MaxTrodden = 900;
        const float Cell = .8f;

        ParticleSystem nearFlakes, farFlakes, puffs;
        Material[] printMats, trodMats;
        Mesh printMesh, trodMesh;

        sealed class Decal { public Transform T; public MeshRenderer R; public float Fill; public int Level = -1; public long Cell; public bool Trod; }
        readonly Queue<Decal> prints = new Queue<Decal>(), trodden = new Queue<Decal>();
        readonly Dictionary<long, int> passes = new Dictionary<long, int>();
        Transform decalRoot;
        float fadeTick;

        public static SnowFx Create()
        {
            var go = new GameObject("SnowFx", typeof(SnowFx));
            DontDestroyOnLoad(go);
            return go.GetComponent<SnowFx>();
        }

        static Material Mat(string name) => Resources.Load<Material>("World/Materials/SnowFx/" + name);

        void Awake()
        {
            Instance = this;
            decalRoot = new GameObject("SnowDecals").transform;
            DontDestroyOnLoad(decalRoot.gameObject);
            printMats = new Material[FadeLevels]; trodMats = new Material[FadeLevels];
            for (int i = 0; i < FadeLevels; i++) { printMats[i] = Mat("Footprint_" + i); trodMats[i] = Mat("Trodden_" + i); }
            printMesh = Quad(.13f, .30f); trodMesh = Quad(.95f, 1.15f);

            var flakeMat = Mat("Snowflakes");
            // Near layer: large soft flakes within ~12 m, swirling. Far layer: fine dense snow up to ~45 m that reads as a veil.
            nearFlakes = Flakes("SnowNear", flakeMat, 26f, 14f, .018f, .045f, 700, 3000);
            farFlakes = Flakes("SnowFar", flakeMat, 90f, 30f, .03f, .07f, 1400, 6000);
            var puffMat = Mat("SnowPuff");
            puffs = Puffs(puffMat != null ? puffMat : flakeMat);
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

        ParticleSystem Flakes(string name, Material mat, float box, float height, float minSize, float maxSize, float rate, int max)
        {
            var ps = new GameObject(name, typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, .55f), new Color(1, 1, 1, .95f));
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(box, height, box);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(.4f, 1.4f); vel.y = new ParticleSystem.MinMaxCurve(-1.3f, -.7f); vel.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .55f; noise.frequency = .35f; noise.scrollSpeed = .25f; noise.octaveCount = 2; noise.quality = ParticleSystemNoiseQuality.Medium;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(1, .9f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Low; col.lifetimeLoss = 1f; col.bounce = 0f; col.radiusScale = .1f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Particles/Standard Unlit"));
            r.minParticleSize = 0f; r.maxParticleSize = .012f; // keeps a flake passing the lens from filling the screen
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingFudge = 10;
            ps.Play();
            return ps;
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
            var s = NightSession.Instance;
            bool storm = s != null && s.Storm.Value;
            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                // Emit slightly upwind so the drift carries flakes through the view.
                float wind = storm ? 9f : 1f;
                nearFlakes.transform.position = p + new Vector3(-wind * .6f, 5f, 0);
                farFlakes.transform.position = p + new Vector3(-wind * 1.5f, 11f, 0);
            }
            SetWeather(nearFlakes, storm, 700f, 2600f);
            SetWeather(farFlakes, storm, 1400f, 5200f);

            fadeTick += Time.deltaTime;
            if (fadeTick >= .5f) { FillDecals(fadeTick, storm); fadeTick = 0f; }
        }

        static void SetWeather(ParticleSystem ps, bool storm, float calm, float blizzard)
        {
            var emission = ps.emission; emission.rateOverTime = storm ? blizzard : calm;
            var vel = ps.velocityOverLifetime;
            vel.x = storm ? new ParticleSystem.MinMaxCurve(8f, 13f) : new ParticleSystem.MinMaxCurve(.4f, 1.4f);
            vel.y = storm ? new ParticleSystem.MinMaxCurve(-3.5f, -1.5f) : new ParticleSystem.MinMaxCurve(-1.3f, -.7f);
            var noise = ps.noise; noise.strength = storm ? 1.6f : .55f; noise.frequency = storm ? .6f : .35f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = storm ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = storm ? .015f : 0f; r.lengthScale = 1f;
        }

        /// <summary>Falling snow fills prints: ~10 minutes of calm snowfall erases a print, a blizzard does it in ~1.5 minutes.</summary>
        void FillDecals(float dt, bool storm)
        {
            float rate = dt / (storm ? 90f : 600f);
            Fill(prints, printMats, rate, false);
            Fill(trodden, trodMats, rate * .6f, true);
        }

        void Fill(Queue<Decal> queue, Material[] mats, float rate, bool trod)
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
            if (passes.TryGetValue(d.Cell, out int c)) { if (c <= 1) passes.Remove(d.Cell); else passes[d.Cell] = c - 1; }
            Destroy(d.T.gameObject);
        }

        static long Key(float x, float z) => ((long)Mathf.FloorToInt(x / Cell) << 32) ^ (uint)Mathf.FloorToInt(z / Cell);

        /// <summary>How many prints lie in this spot (0 = virgin snow).</summary>
        public int Passes(float x, float z) => passes.TryGetValue(Key(x, z), out int c) ? c : 0;

        /// <summary>Snow depth a boot sinks into at this point: deep powder in the forest and valley, wind crust on the open slope.</summary>
        public static float Depth(float groundHeight) => groundHeight < Sites.TreeLine ? .32f : groundHeight < 820f ? .16f : .07f;

        /// <summary>Movement factor for trail-breaking: virgin powder is slow, a path trodden by others is fast.</summary>
        public float SpeedFactor(float x, float z, float groundHeight)
        {
            float depth = Depth(groundHeight);
            float fresh = Mathf.Lerp(1f, .62f, Mathf.InverseLerp(.05f, .32f, depth));
            int p = Passes(x, z);
            return p >= 3 ? 1f : Mathf.Lerp(fresh, 1f, p / 3f);
        }

        public void Step(Vector3 foot, Vector3 forward, Vector3 normal, float depth, bool trodPatch)
        {
            if (printMesh == null) return;
            var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, normal).normalized, normal);
            prints.Enqueue(Spawn(foot + normal * .025f, rot, printMesh, printMats, false, Random.Range(.9f, 1.1f)));
            if (prints.Count > MaxPrints) Release(prints.Dequeue());
            if (trodPatch)
            {
                var twist = rot * Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
                trodden.Enqueue(Spawn(foot + normal * .018f, twist, trodMesh, trodMats, true, Random.Range(.85f, 1.2f)));
                if (trodden.Count > MaxTrodden) Release(trodden.Dequeue());
            }
            var emit = new ParticleSystem.EmitParams { position = foot + Vector3.up * .05f, applyShapeToPosition = true };
            puffs.Emit(emit, Mathf.RoundToInt(Mathf.Lerp(3, 12, depth / .32f)));
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
