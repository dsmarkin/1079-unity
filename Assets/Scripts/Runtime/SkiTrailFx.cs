using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The лыжня: the line of pressed snow a party leaves behind, both as a fact of the world and as something to look at.
    ///
    /// The fact is <see cref="SkiTrack"/> from the core rules — a one-metre grid of prints that hardens on a second pass and is
    /// filled in by the wind. Every machine keeps one. The host's copy is the authority (it is what
    /// <see cref="NightSession.TickSkisServer"/> charges strength against), a client's copy is its own view of the same skiing
    /// and is close enough to steer by. Both are fed the same way: from the hikers everyone can see.
    ///
    /// The look is one ribbon mesh per stretch of track, grown a rib at a time as the skier moves, exactly the way
    /// <see cref="SnowTrail"/> drops boot prints: a few hundred objects for a whole night, not tens of thousands. Across the
    /// ribbon a generated texture draws what a wooden ski leaves — two grooves 21 cm apart, the lip of snow pushed up beside
    /// each of them, the flattened strip between, and a glint where the crust broke. As the wind eats it the ribbon fades and
    /// narrows in two steps, so a filling лыжня thins to a line before it goes.</summary>
    public sealed class SkiTrailFx : MonoBehaviour
    {
        public static SkiTrailFx Instance { get; private set; }

        /// <summary>The track itself. On the host this is the authoritative one.</summary>
        public readonly SkiTrack Track = new SkiTrack();

        /// <summary>A rib every half metre, the spacing <see cref="SkiTrack"/> is written for.</summary>
        public const float Step = .5f;
        /// <summary>Ribs in one ribbon mesh, and how many ribbons are kept at once: 24 × 0.5 m = 12 m a piece, 260 pieces = 3 km.</summary>
        const int RibsPerChunk = 24, MaxChunks = 260;
        /// <summary>Half the width of the drawn ribbon, metres. <see cref="Skiing.TrackWidth"/> is the strip that carries weight;
        /// the drawing runs a little wider, because snow is pushed aside as well as pressed down.</summary>
        const float Half = .45f;
        /// <summary>Metres of track one turn of the texture covers.</summary>
        const float TextureMetres = 1.4f;
        /// <summary>Clear of the snow, so the ribbon draws over the boot prints instead of fighting them.</summary>
        const float Lift = .035f;
        /// <summary>The ribbon narrows in two steps as it fills in.</summary>
        static readonly float[] Narrow = { 1f, .72f, .45f };

        const float WeatherSeconds = 1.5f, ForgetSeconds = 4f, FadeSeconds = .75f;

        sealed class Chunk
        {
            public Transform T;
            public MeshRenderer R;
            public Mesh M;
            public int Ribs;
            /// <summary>Value of the track's drift counter when this stretch was laid.</summary>
            public float Born;
            public int Step = -1;
            public float Shown = -1f;
            public readonly Vector3[] C = new Vector3[RibsPerChunk], S = new Vector3[RibsPerChunk], N = new Vector3[RibsPerChunk];
            public readonly float[] V = new float[RibsPerChunk];
        }

        sealed class Lane
        {
            public Vector3 Last;
            public bool Started;
            public float Run;        // metres of track already drawn, for the texture to run along
            public Chunk Open;
            public Travel Was;
        }

        readonly Dictionary<ulong, Lane> lanes = new Dictionary<ulong, Lane>();
        /// <summary>Every stretch of ribbon on the ground, oldest first.</summary>
        readonly List<Chunk> live = new List<Chunk>();

        Transform root;
        Material material;
        MaterialPropertyBlock props;
        float weatherTick, forgetTick, fadeTick;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // scratch, reused for every mesh rebuild
        static readonly List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();
        static readonly List<Vector2> uvs = new List<Vector2>();
        static readonly List<int> tris = new List<int>();

        public static SkiTrailFx Create()
        {
            var go = new GameObject("SkiTrailFx", typeof(SkiTrailFx));
            DontDestroyOnLoad(go);
            return go.GetComponent<SkiTrailFx>();
        }

        /// <summary>How trodden this spot is, 0..1 — the <c>packed</c> every <see cref="Skiing"/> rule asks for.</summary>
        public float Packed(float x, float z) => Track.Packed(x, z, Time.timeAsDouble);

        void Awake()
        {
            Instance = this;
            root = new GameObject("SkiTracks").transform;
            DontDestroyOnLoad(root.gameObject);
            props = new MaterialPropertyBlock();
            material = BuildMaterial();
            ApplyWeather();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (root != null) Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- the material

        /// <summary>A copy of the trodden-snow decal material — Standard in Fade mode, lit, already in the build with its
        /// transparent variants — carrying a лыжня texture instead. Shaders only ship when a material in Resources points at
        /// them, so nothing here is conjured with Shader.Find if it can be helped.</summary>
        static Material BuildMaterial()
        {
            var template = Resources.Load<Material>("World/Materials/SnowFx/Trodden_0");
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                m = new Material(Shader.Find("Standard"));
                m.SetFloat("_Mode", 2); m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
            }
            m.name = "SkiTrack";
            m.mainTexture = TrackTexture();
            m.mainTextureScale = Vector2.one;
            m.color = Color.white;
            m.SetFloat("_Glossiness", .26f);
            m.SetFloat("_Metallic", 0f);
            m.renderQueue = 2991;    // just over the boot prints (2990), well under the particles
            m.enableInstancing = false;
            return m;
        }

        /// <summary>What a pair of wooden skis leaves in the snow, drawn across the ribbon. u runs across (clamped),
        /// v along it (repeating, so the noise has to close on itself — hence the circle of Perlin samples).</summary>
        static Texture2D TrackTexture()
        {
            const int W = 96, H = 96;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true)
            {
                name = "ski_track",
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Repeat,
                anisoLevel = 4,
            };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + .5f) / H, a = v * Mathf.PI * 2f;
                float cs = Mathf.Cos(a) * 2.4f, sn = Mathf.Sin(a) * 2.4f;
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W;
                    float xm = (u - .5f) * (Half * 2f);                       // metres from the centre line
                    float grain = Mathf.PerlinNoise(x * .21f + cs + 5.1f, 7.3f + sn);
                    float streak = Mathf.PerlinNoise(x * .06f + cs * .5f + 19f, 3.7f + sn * .5f);

                    float gd = Mathf.Min(Mathf.Abs(xm + .105f), Mathf.Abs(xm - .105f));   // to the nearer groove centre
                    float groove = Mathf.Clamp01(1f - gd / .041f);                        // the board's own width
                    float lip = Mathf.Clamp01(1f - Mathf.Abs(gd - .062f) / .026f);        // snow pushed up beside it
                    float between = Mathf.Clamp01(1f - Mathf.Abs(xm) / .066f);            // the flattened strip between the skis
                    float body = Mathf.Clamp01(1f - Mathf.Abs(xm) / .30f);

                    // a groove is a shadow, a lip is the brightest thing on a night slope, the strip between is merely smoothed
                    float shade;
                    if (groove >= lip && groove >= between) shade = Mathf.Lerp(.56f, .70f, grain * .6f + streak * .4f);
                    else if (lip >= between) shade = Mathf.Lerp(.93f, 1f, grain);
                    else shade = Mathf.Lerp(.72f, .84f, streak);
                    // the crust breaks in plates and catches whatever light there is
                    float glint = Mathf.Clamp01((grain - .86f) * 7f) * Mathf.Clamp01(body * 1.5f);
                    shade = Mathf.Min(1f, shade + glint * .25f);

                    float alpha = Mathf.Max(groove * .95f, Mathf.Max(lip * .78f, between * .58f));
                    alpha *= .62f + .38f * (grain * .5f + streak * .5f);
                    alpha = Mathf.Clamp01(alpha * Mathf.Clamp01(body * 2.1f));
                    px[y * W + x] = new Color(shade * .93f, shade * .96f, shade, alpha);
                }
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ---------------------------------------------------------------- the night

        void Update()
        {
            var session = NightSession.Instance;
            // the host charges strength and keeps the authoritative packing; it has no Update of its own for this
            if (session != null && session.IsServer) session.TickSkisServer(Time.deltaTime);

            double now = Time.timeAsDouble;
            weatherTick += Time.deltaTime;
            if (weatherTick >= WeatherSeconds) { weatherTick = 0f; ApplyWeather(); }

            Follow(now);

            forgetTick += Time.deltaTime;
            if (forgetTick >= ForgetSeconds) { forgetTick = 0f; Track.Forget(now); }

            fadeTick += Time.deltaTime;
            if (fadeTick >= FadeSeconds) { fadeTick = 0f; Fade(); }
        }

        /// <summary>How long a fresh print lives in the weather as it stands. <see cref="Weather"/> smooths the host's storm flag
        /// into a 0..1 wind, which maps onto the 2–12 m/s the drift model is built around, and a blizzard also brings snow.</summary>
        void ApplyWeather()
        {
            float wind = Mathf.Lerp(1.5f, 15f, Mathf.Clamp01(Weather.Wind));
            float fall = Mathf.Clamp01(.1f + .75f * Weather.Storm);
            Track.Weather(wind, fall);
        }

        void Follow(double now)
        {
            foreach (var h in HikerController.All)
            {
                if (h == null || !h.isActiveAndEnabled) continue;
                var gear = h.Skis;
                var mode = gear != null ? gear.Mode : Travel.Foot;
                var pos = h.transform.position;
                if (!lanes.TryGetValue(h.OwnerClientId, out var lane)) lanes[h.OwnerClientId] = lane = new Lane { Was = mode };
                if (!lane.Started) { lane.Started = true; lane.Last = pos; lane.Was = mode; continue; }
                var d = pos - lane.Last; d.y = 0f;
                float dist = d.magnitude;
                if (dist > 6f) { lane.Last = pos; lane.Open = null; lane.Was = mode; continue; }   // teleport or a lift
                if (mode != lane.Was) { lane.Open = null; lane.Was = mode; }
                if (dist < Step) continue;
                var dir = d / dist;
                lane.Last = pos;
                Track.Stamp(pos.x, pos.z, now, mode);                      // boots pack the snow too, just narrowly
                if (mode == Travel.Skis || mode == Travel.Hauling) Draw(lane, pos, dir, dist);
                else lane.Open = null;
            }
        }

        void Draw(Lane lane, Vector3 pos, Vector3 dir, float dist)
        {
            float ground = Ground(pos.x, pos.z);
            // in a cabin, on a chair or in mid-air nothing is pressed into the snow. The hiker's own ground probe only runs for
            // whoever owns the body, so the height above the snow is what decides it here, for everyone alike.
            if (pos.y - ground > .6f) { lane.Open = null; return; }
            var n = Normal(pos.x, pos.z);
            var f = Vector3.ProjectOnPlane(dir, n);
            if (f.sqrMagnitude < 1e-5f) return;
            f.Normalize();
            var side = Vector3.Cross(n, f).normalized;
            var centre = new Vector3(pos.x, ground, pos.z) + n * Lift;
            lane.Run += dist;

            var c = lane.Open;
            if (c != null && c.T == null) c = null;      // the cap threw this stretch away while we were still on it
            if (c == null || c.Ribs >= RibsPerChunk)
            {
                var fresh = NewChunk();
                // the new stretch starts on the last rib of the old one, so the ribbon has no gap at the joint
                if (c != null && c.Ribs > 0) Add(fresh, c.C[c.Ribs - 1], c.S[c.Ribs - 1], c.N[c.Ribs - 1], c.V[c.Ribs - 1]);
                lane.Open = c = fresh;
            }
            Add(c, centre, side, n, lane.Run / TextureMetres);
            Rebuild(c);
        }

        Chunk NewChunk()
        {
            var go = new GameObject("Лыжня", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            var c = new Chunk
            {
                T = go.transform,
                R = go.GetComponent<MeshRenderer>(),
                M = new Mesh { name = "ski_track" },
                Born = Track.Drifted,
                Step = 0,
            };
            c.M.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = c.M;
            c.R.sharedMaterial = material;
            c.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.R.receiveShadows = true;
            live.Add(c);
            while (live.Count > MaxChunks) Release(live[0]);
            return c;
        }

        static void Add(Chunk c, Vector3 centre, Vector3 side, Vector3 n, float v)
        {
            int i = c.Ribs++;
            c.C[i] = centre; c.S[i] = side; c.N[i] = n; c.V[i] = v;
        }

        /// <summary>Rebuilds one ribbon: three vertices to a rib (both shoulders and the middle), so it hugs a cross slope, and
        /// both windings of every triangle, so backface culling picks the right one whichever way the ground faces.</summary>
        static void Rebuild(Chunk c)
        {
            verts.Clear(); norms.Clear(); uvs.Clear(); tris.Clear();
            float half = Half * Narrow[Mathf.Clamp(c.Step, 0, Narrow.Length - 1)];
            for (int i = 0; i < c.Ribs; i++)
            {
                var p = c.C[i]; var s = c.S[i] * half; var n = c.N[i]; float v = c.V[i];
                verts.Add(p - s); verts.Add(p); verts.Add(p + s);
                norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(0f, v)); uvs.Add(new Vector2(.5f, v)); uvs.Add(new Vector2(1f, v));
            }
            for (int i = 1; i < c.Ribs; i++)
            {
                int a = (i - 1) * 3, b = i * 3;
                Quad(a, b); Quad(a + 1, b + 1);
            }
            c.M.Clear();
            c.M.SetVertices(verts);
            c.M.SetNormals(norms);
            c.M.SetUVs(0, uvs);
            c.M.SetTriangles(tris, 0);
            c.M.RecalculateBounds();
        }

        static void Quad(int a, int b)
        {
            tris.Add(a); tris.Add(b); tris.Add(b + 1);
            tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            tris.Add(a); tris.Add(b + 1); tris.Add(b);
            tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
        }

        /// <summary>The wind fills the track in: it pales and narrows, and at the end it is gone. The erosion is the track's own
        /// shared drift counter, so a blizzard in the middle of a calm night eats exactly its own share.</summary>
        void Fade()
        {
            float drifted = Track.Drifted;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var c = live[i];
                if (c.T == null) { live.RemoveAt(i); continue; }
                float erosion = drifted - c.Born;
                if (erosion >= 1f) { live.RemoveAt(i); Release(c); continue; }
                int step = erosion < .45f ? 0 : erosion < .75f ? 1 : 2;
                if (step != c.Step && c.Ribs > 1) { c.Step = step; Rebuild(c); }
                float alpha = Mathf.Pow(1f - erosion, 1.2f);
                if (Mathf.Abs(alpha - c.Shown) < .02f) continue;
                c.Shown = alpha;
                props.Clear();
                props.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
                c.R.SetPropertyBlock(props);
            }
        }

        void Release(Chunk c)
        {
            if (c == null) return;
            live.Remove(c);
            if (c.M != null) Destroy(c.M);
            if (c.T != null) Destroy(c.T.gameObject);
            c.T = null;
        }

        static Vector3 Normal(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return Vector3.up;
            var tp = terrain.transform.position; var size = terrain.terrainData.size;
            return terrain.terrainData.GetInterpolatedNormal((x - tp.x) / size.x, (z - tp.z) / size.z);
        }

        static float Ground(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            return Bootstrap.Dem != null ? TerrainBuilder.Height(Bootstrap.Dem, x, z) : 0f;
        }
    }
}
