using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>What is actually standing on the mountain above Гара-Баши — the things the rules in
    /// <see cref="Ascent"/>, <see cref="AscentCold"/> and <see cref="AscentRoute"/> reach for.
    ///
    /// Everything here is baked in world coordinates against the Elbrus height field and saved as ONE prefab,
    /// Resources/World/Prefabs/Elbrus/Elb_Ascent, the way <see cref="AnimalTracksFactory"/> bakes the animal trails.
    /// The runtime glue is a single line: instantiate it at the origin with no rotation. That keeps the placement
    /// deterministic (it is computed once, from the DEM, in the editor), keeps the object count down (a hundred and
    /// sixty-eight wands are fourteen meshes, not a hundred and sixty-eight transforms) and leaves the runtime free.
    ///
    /// The numbers are the ones the Core rules are written with — <see cref="AscentRoute.WandSpacingM"/>,
    /// <see cref="AscentRoute.WandHeightM"/>, <see cref="AscentRoute.RopeFromEle"/>/<see cref="AscentRoute.RopeToEle"/>,
    /// <see cref="AscentRoute.LaneHalfWidthM"/>, <see cref="AscentRoute.FumaroleM"/> — so the world and the rules can
    /// never drift apart. Heights are read off our own height field, never off a signpost.
    ///
    /// Seating follows <see cref="Height1079.Runtime.ElbrusWorld"/>: a building takes the nine samples of its footprint
    /// and stands on the highest of them (Sit.Pad), a loose prop sits at the middle of its footprint (Sit.Flat), and
    /// anything on runners lies on the slope plane (Sit.Lie). Those helpers are internal to the runtime assembly, so
    /// the same three rules are written out again below — <see cref="Pad"/>, <see cref="Flat"/>, <see cref="Lay"/>.
    ///
    /// One thing the terrain cannot do: a prefab cannot cut a hole in a height field. So every hollow on the glacier —
    /// the settled saucers over the bridges, the open crevasses — is built UPWARD, as a wind-built rim with a low
    /// middle. The eye reads the dish; the ground underneath stays what it was.
    ///
    /// Nothing is unlit. At two in the morning the only light on this mountain is a head torch, and an unlit material
    /// would burn white (CLAUDE.md, «Ночь»). The reflective tape on the wands is a very smooth light material, so a
    /// torch picks it out the way it picks out the real thing, and it is dark when nothing shines on it.</summary>
    public static partial class ElbrusAscent
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Elbrus";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Ascent";

        // ── the switches a season turns ───────────────────────────────────────────────────────────────────
        /// <summary>Whether the rope handrails are rigged this season. The steel cable of the МЧС stays up all year —
        /// it was strung in 2013 and nobody takes it down — but the rope on the summit rise and the two lines above
        /// Pastukhov rocks are put up by the guides and are simply not there early in the season. One flag: false and
        /// the whole «Ropes» group leaves the mountain. (The group keeps its name, so the runtime can switch it too.)</summary>
        public const bool RopesRigged = true;

        // ── wands ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>How far from the line a wand can end up. Nobody surveys these in: a man walks up with a bundle
        /// under his arm and pushes them in where he is standing.</summary>
        const float WandScatterM = 1.5f;
        /// <summary>Share of the wands the summer snowfalls have taken down to a stub. Half-metre wands were the old
        /// standard and they vanished, which is why the bamboo is a metre and a half now — but a metre and a half is
        /// not enough either after a bad week.</summary>
        const float WandDriftedShare = .22f;
        const float WandStubMinM = .4f, WandStubMaxM = .6f;

        // ── the МЧС cable ─────────────────────────────────────────────────────────────────────────────────
        /// <summary>Metres between the stakes. The Эльбрусский ВПСО drove them in on foot in 2013, so the spacing is
        /// eyeballed, not measured.</summary>
        const float CablePostMinM = 15f, CablePostMaxM = 25f;
        /// <summary>Height of the cable at a stake. It is a handrail at waist height, not a belay.</summary>
        const float CableEyeM = 1.0f;
        const float CablePostM = 1.3f;
        /// <summary>Sag at mid-span — an honest catenary, solved for, not a straight line drawn between two posts.</summary>
        const float CableSagM = .17f;
        /// <summary>The cable runs a stride to the left of the trodden line, inside <see cref="AscentRoute.RopeReachM"/>,
        /// so a hand finds it without leaving the route.</summary>
        const float CableOffsetM = -1.5f;
        /// <summary>Ten millimetres of steel would be one pixel at fifty metres. Sixteen reads as the thread it has to
        /// read as and still looks like wire in the hand.</summary>
        const float CableR = .016f;

        // ── the seasonal rope ─────────────────────────────────────────────────────────────────────────────
        /// <summary>The summit rise: loose snow over ice, and the one place on the southern route people actually
        /// climb clipped in.</summary>
        const float SummitRopeFromEle = 5400f, SummitRopeToEle = 5550f;
        /// <summary>«Две линии» above Pastukhov rocks — one up, one down — about 330 m of rope between them.</summary>
        const float PastukhovRopeM = 330f;
        const float RopeAnchorM = 15f;
        /// <summary>A fixed rope is not strung in the air: it lies on the snow between its anchors and lifts to the
        /// hanger at each of them, which is where the carabiner is re-clipped.</summary>
        const float RopeLieM = .07f, RopeEyeM = .26f, RopeR = .014f;

        // ── the snow-cat lane ─────────────────────────────────────────────────────────────────────────────
        /// <summary>Metres of lane in one tile of the tread texture.</summary>
        const float TreadTileM = 2.4f;
        /// <summary>Step along the lane between two rows of quads — short enough that the bends read.</summary>
        const float LaneStepM = 8f;

        // ── the glacier ───────────────────────────────────────────────────────────────────────────────────
        /// <summary>Where the wind-built rim of a saucer or a crevasse lip stands over the snow around it.</summary>
        const float RimM = .34f, CrevasseLipM = 1.15f;

        static HeightField dem;
        static int meshCounter;
        static (float x, float z)[] Up => Elbrus.SummitRoute;
        static (float x, float z)[] Lane => Elbrus.RatrakRoute;
        static float upLength, laneLength;
        static int wandCount, wandDrifted, cablePosts, ropeAnchors, ropeMetres, crevasseCount, sagCount, streamCount;

        // ── materials (own ElbA* names, so this file never fights the other two over a material asset) ─────
        static Material Bamboo => Materials.Get("ElbABamboo", new Color(.79f, .72f, .46f), smoothness: .3f);
        /// <summary>The flag on a wand: whatever bright rag was in the bundle. It is the thing a head torch finds.</summary>
        static Material FlagCloth => Materials.Get("ElbAFlag", new Color(.88f, .26f, .12f), smoothness: .08f);
        /// <summary>Retro-reflective tape. Not emissive — it is dark until a torch is on it, and then it is the
        /// brightest thing on the mountain. A very smooth, very light surface does exactly that.</summary>
        static Material Tape => Materials.Get("ElbATape", new Color(.93f, .94f, .92f), smoothness: .95f);
        static Material RedTape => Materials.Get("ElbATapeRed", new Color(.82f, .14f, .12f), smoothness: .5f);
        static Material CableSteel => Materials.Get("ElbACable", new Color(.42f, .44f, .47f), smoothness: .62f);
        static Material PostPaint => Materials.Get("ElbAPostPaint", new Color(.85f, .35f, .08f), smoothness: .35f);
        /// <summary>Dynamic rope: red or orange, fuzzy, never shiny.</summary>
        static Material RopeCord => Materials.Get("ElbARope", new Color(.84f, .21f, .13f), smoothness: .06f);
        static Material AnchorAlu => Materials.Get("ElbAAnchorAlu", new Color(.74f, .76f, .78f), smoothness: .6f);
        static Material AnchorSteel => Materials.Get("ElbAAnchorSteel", new Color(.5f, .52f, .55f), smoothness: .55f);
        static Material Snow => Materials.PH("ElbASnow", "snow_02", new Color(.97f, .98f, 1f), 3f, .3f, .7f);
        /// <summary>Snow that lies in its own shadow: the inside of a settled saucer, the lee of a lip. Same stuff,
        /// less light — which is exactly how a dish reads on a white slope.</summary>
        static Material ShadeSnow => Materials.PH("ElbAShade", "snow_02", new Color(.69f, .75f, .87f), 3f, .28f, .7f);
        /// <summary>Glacier ice in a fresh break. Blue because the break is deep and the ice is old.</summary>
        static Material BlueIce => Materials.Get("ElbABlueIce", new Color(.29f, .55f, .74f), smoothness: .78f);
        static Material DarkDepth => Materials.Get("ElbADepth", new Color(.05f, .09f, .15f), smoothness: .2f);
        static Material Water => Materials.Get("ElbAWater", new Color(.24f, .35f, .42f), smoothness: .92f);
        static Material WetSnow => Materials.PH("ElbAWetSnow", "snow_03", new Color(.72f, .77f, .82f), 2.5f, .45f, .6f);

        static Texture2D treadTex;
        static Material tread;
        /// <summary>The rolled lane: packed snow with the tiller's transverse combing and the two broad bands the
        /// crawler tracks leave. Opaque, because a groomed lane really does end in a hard edge.</summary>
        static Material Tread
        {
            get
            {
                if (tread != null) return tread;
                if (treadTex == null) treadTex = TreadTexture();
                return tread = Materials.Get("ElbACatTrack", new Color(.93f, .95f, .99f), treadTex, null, .2f);
            }
        }

        // ── little helpers ────────────────────────────────────────────────────────────────────────────────
        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            Directory.CreateDirectory(MeshDir);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string meshName = $"Asc_{name}_{meshCounter++}";
            var mesh = mb.ToMesh(meshName);
            string path = $"{MeshDir}/{meshName}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        static void Solid(Transform t, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>World-space geometry collected into chunk meshes by arc length along a route, so a line of a
        /// hundred and sixty-eight wands costs a dozen renderers instead of a hundred and sixty-eight transforms.
        /// Sorted, so two builds of the same world give the same asset names.</summary>
        sealed class Chunks
        {
            readonly SortedDictionary<int, MeshBuilder> parts = new SortedDictionary<int, MeshBuilder>();
            readonly string name;
            readonly float span;
            readonly Material[] mats;

            public Chunks(string name, float span, params Material[] mats)
            {
                this.name = name; this.span = span; this.mats = mats;
            }

            public MeshBuilder At(float s)
            {
                int k = Mathf.FloorToInt(s / span);
                if (!parts.TryGetValue(k, out var mb)) parts[k] = mb = new MeshBuilder(mats.Length);
                return mb;
            }

            public int Emit(Transform parent, bool shadows = true, bool collide = false)
            {
                int n = 0;
                foreach (var kv in parts)
                {
                    if (kv.Value.TriangleCount == 0) continue;
                    var go = Part(parent, $"{name}_{kv.Key}", kv.Value, mats);
                    if (!shadows) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (collide) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    n++;
                }
                return n;
            }
        }

        // ── the ground, and how things meet it ────────────────────────────────────────────────────────────
        static float Ground(float x, float z) => dem.Sample(x, z);

        /// <summary>Lowest and highest of the nine samples under a footprint (corners, edge middles, centre) turned by
        /// <paramref name="yaw"/> — the same nine <see cref="Height1079.Runtime.ElbrusWorld"/> takes, so a hut here and
        /// a hut there sit on the slope the same way.</summary>
        static (float lo, float hi) Pad(float x, float z, float yaw, Vector2 size)
        {
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            float hx = Mathf.Max(.4f, size.x * .5f), hz = Mathf.Max(.4f, size.y * .5f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int k = -1; k <= 1; k++)
                {
                    float u = i * hx, v = k * hz;
                    float h = Ground(x + u * c + v * s, z - u * s + v * c);
                    if (h < lo) lo = h;
                    if (h > hi) hi = h;
                }
            return (lo, hi);
        }

        /// <summary>Sit.Pad: a building stands on the HIGHEST corner of its footprint, or the hillside pokes up through
        /// the floor.</summary>
        static float Stand(float x, float z, float yaw, Vector2 size) => Pad(x, z, yaw, size).hi + .05f;

        /// <summary>Sit.Flat: a loose prop sits at the middle of its footprint.</summary>
        static float Flat(float x, float z, float yaw, Vector2 size)
        {
            var (lo, hi) = Pad(x, z, yaw, size);
            return (lo + hi) * .5f - .04f;
        }

        /// <summary>Sit.Lie: laid on the slope plane and tilted to it, the way a sled or a board lies.</summary>
        static Quaternion Lay(float x, float z, float yaw)
        {
            var (dx, dz, slope) = dem.Fall(x, z, 8f);
            if (slope < .6f) return Quaternion.Euler(0, yaw, 0);
            var down = new Vector3(dx, 0, dz).normalized;
            float a = slope * Mathf.Deg2Rad;
            var normal = (Vector3.up * Mathf.Cos(a) - down * Mathf.Sin(a)).normalized;
            var fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad));
            var proj = fwd - normal * Vector3.Dot(fwd, normal);
            if (proj.sqrMagnitude < 1e-4f) return Quaternion.Euler(0, yaw, 0);
            return Quaternion.LookRotation(proj.normalized, normal);
        }

        /// <summary>Compass bearing (degrees, z = north) of the fall line — a building on a slope faces downhill.</summary>
        static float FaceDownhill(float x, float z, float span = 12f)
        {
            var (dx, dz, slope) = dem.Fall(x, z, span);
            return slope < .2f ? 0f : Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        // ── the route, by arc length ──────────────────────────────────────────────────────────────────────
        static float Len((float x, float z)[] path) => path == Lane ? laneLength : upLength;

        /// <summary>Unit vector along a route at arc length <paramref name="s"/>, pointing the way the climb goes.</summary>
        static Vector2 Along((float x, float z)[] path, float s)
        {
            float len = Len(path);
            var a = Elbrus.PointAt(path, Mathf.Max(0f, s - 4f));
            var b = Elbrus.PointAt(path, Mathf.Min(len, s + 4f));
            var d = new Vector2(b.x - a.x, b.z - a.z);
            return d.sqrMagnitude < 1e-6f ? Vector2.up : d.normalized;
        }

        /// <summary>Unit vector to the RIGHT of the climb — Cross(up, forward) in the game's frame. This is the sign
        /// the crevasse rule is written in: <see cref="AscentRoute.CrevasseChance"/> is zero for anything at or left of
        /// the lane and climbs with distance to the right of it. Everything this file puts on the right-hand side is
        /// there to teach that.</summary>
        static Vector2 Across((float x, float z)[] path, float s)
        {
            var f = Along(path, s);
            return new Vector2(f.y, -f.x);
        }

        /// <summary>A point on a route: arc length, signed offset (positive to the right of the climb), height over
        /// the snow.</summary>
        static Vector3 On((float x, float z)[] path, float s, float off = 0f, float up = 0f)
        {
            var (x, z) = Elbrus.PointAt(path, s);
            var r = Across(path, s);
            x += r.x * off; z += r.y * off;
            return new Vector3(x, Ground(x, z) + up, z);
        }

        static float EleAt((float x, float z)[] path, float s)
        {
            var (x, z) = Elbrus.PointAt(path, s);
            return Ground(x, z);
        }

        /// <summary>The first arc length at or above a height, measured on our own height field. Every belt in this
        /// file is placed this way and never by eye: «from 4 900 m» means what the radar says, not what a sign says.</summary>
        static float ArcAt((float x, float z)[] path, float ele, float from = 0f)
        {
            float len = Len(path);
            for (float s = from; s <= len; s += 2f) if (EleAt(path, s) >= ele) return s;
            return len;
        }

        /// <summary>Arc length of the point on a route nearest a place on the mountain — how far up the line you are
        /// level with the hut, the rocks or the huts on the moraine.</summary>
        static float NearestOn((float x, float z)[] path, float x, float z)
        {
            float len = Len(path), best = float.MaxValue, at = 0f;
            for (float s = 0; s <= len; s += 4f)
            {
                var (px, pz) = Elbrus.PointAt(path, s);
                float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                if (d < best) { best = d; at = s; }
            }
            return at;
        }

        /// <summary>A small closed loop of wire — a carabiner, a bolt hanger, the eye of a cable stake.</summary>
        static void Ring(MeshBuilder mb, int sub, Vector3 centre, Vector3 normal, float radius, float wire)
        {
            var n = normal.normalized;
            var a = Vector3.Cross(n, Mathf.Abs(n.y) > .9f ? Vector3.right : Vector3.up).normalized;
            var b = Vector3.Cross(a, n);
            const int seg = 6;
            var prev = centre + a * radius;
            for (int i = 1; i <= seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2;
                var p = centre + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radius;
                mb.Tube(sub, prev, p, wire, wire, 4, 1f, 0, false);
                prev = p;
            }
        }

        // ── build ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Everything above Гара-Баши, baked against <paramref name="heights"/> into one prefab.</summary>
        public static void Build(HeightField heights)
        {
            dem = heights;
            meshCounter = 0;
            treadTex = null; tread = null; steam = null;
            wandCount = wandDrifted = cablePosts = ropeAnchors = ropeMetres = crevasseCount = sagCount = streamCount = 0;
            rockCount = moraineProps = ruinPieces = 0;
            upLength = Elbrus.Length(Up);
            laneLength = Elbrus.Length(Lane);
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(PrefabDir);

            var root = new GameObject("Elb_Ascent");
            var t = root.transform;

            CatLane(t);
            Garabashi(t);
            Moraine(t);
            PastukhovRocks(t);
            Wands(t);
            RescueCable(t);
            FixedRopes(t);
            SaddleCamp(t);

            AssetDatabase.DeleteAsset($"{PrefabDir}/Elb_Ascent.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Elb_Ascent.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"1079 Эльбрус, подъём: {wandCount} вешек ({wandDrifted} занесено), трос МЧС на {cablePosts} кольях, "
                    + $"{ropeMetres} м верёвочных перил на {ropeAnchors} точках, колея {laneLength:0} м, "
                    + $"{crevasseCount} трещин, {sagCount} мульд, {streamCount} ручьёв, "
                    + $"{rockCount} глыб на скалах Пастухова, {moraineProps} предметов на морене, {ruinPieces} обломков");
        }

        // ── 1. wands ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Wands every <see cref="AscentRoute.WandSpacingM"/> along the whole line, Гара-Баши to the West
        /// summit. Bamboo a metre and a half tall, screwed into the ice and watered in — the half-metre ones the old
        /// hands used went under in the first summer snowfall, which is why they are this long now. A bright rag or a
        /// band of reflective tape at the top: in the dark a head torch finds the tape, and that is the whole point of
        /// the thing. Each one is pushed in by hand, so it leans a little and stands up to a metre and a half off the
        /// line; about a fifth of them are down to a stub with the flag already under the snow, which is what
        /// <see cref="AscentRoute.WandsReadable"/> is counting.</summary>
        static void Wands(Transform root)
        {
            var group = new GameObject("Wands").transform; group.SetParent(root, false);
            var ch = new Chunks("Wands", 1000f, Bamboo, FlagCloth, Tape);
            var rng = new System.Random(1550);
            for (float s = 0; s <= upLength; s += AscentRoute.WandSpacingM)
            {
                float off = (float)(rng.NextDouble() * 2 - 1) * WandScatterM;
                var foot = On(Up, s, off, -.03f);
                bool drifted = rng.NextDouble() < WandDriftedShare;
                float show = drifted ? WandStubMinM + (float)rng.NextDouble() * (WandStubMaxM - WandStubMinM)
                                     : AscentRoute.WandHeightM;
                Wand(ch.At(s), foot, show,
                    3f + (float)rng.NextDouble() * 9f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f,
                    !drifted, 1, 2);
                wandCount++; if (drifted) wandDrifted++;
            }
            ch.Emit(group);
        }

        /// <summary>One wand, straight into a chunk mesh at its place on the mountain. Submesh 0 is the cane;
        /// <paramref name="flagSub"/> and <paramref name="tapeSub"/> say which submeshes the rag and the tape go in,
        /// so the same cane serves the route line and the red-taped markers by the crevasses.</summary>
        static void Wand(MeshBuilder mb, Vector3 foot, float show, float tiltDeg, float tiltDir, float flagDir,
            bool topGear, int flagSub, int tapeSub)
        {
            float tr = tiltDeg * Mathf.Deg2Rad, dr = tiltDir * Mathf.Deg2Rad;
            var axis = new Vector3(Mathf.Sin(tr) * Mathf.Sin(dr), Mathf.Cos(tr), Mathf.Sin(tr) * Mathf.Cos(dr));
            var top = foot + axis * show;
            // screwed a good third of a metre into the ice and watered in, so it starts below the surface
            mb.Tube(0, foot - axis * .32f, top, .023f, .017f, 6, 1.4f, 0, true);
            for (float h = .34f; h < show - .06f; h += .43f)         // bamboo nodes
            {
                var c = foot + axis * h;
                mb.Tube(0, c - axis * .013f, c + axis * .013f, .028f, .028f, 6, 1f, 0, false);
            }
            if (!topGear) return;
            var band = top - axis * .34f;
            mb.Tube(tapeSub, band, band + axis * .13f, .027f, .027f, 6, 1f, 0, false);
            var side = new Vector3(Mathf.Sin(flagDir * Mathf.Deg2Rad), 0, Mathf.Cos(flagDir * Mathf.Deg2Rad));
            side = (side - axis * Vector3.Dot(side, axis)).normalized;
            var a = top - axis * .27f;
            var b = top - axis * .02f;
            mb.Quad(flagSub, a, b, b + side * .27f - axis * .06f, a + side * .24f - axis * .03f,
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
        }

        // ── 2. the МЧС cable ──────────────────────────────────────────────────────────────────────────────
        /// <summary>The steel cable the Эльбрусский ВПСО strung in 2013, from <see cref="AscentRoute.RopeFromEle"/> to
        /// <see cref="AscentRoute.RopeToEle"/> and not a metre further either way. It is not protection — without a
        /// harness it stops nothing (<see cref="AscentRoute.RopeProtects"/>) — it is navigation: in a пурга you put a
        /// hand on it and you cannot lose the route (<see cref="AscentRoute.RopeInReach"/>). So it is hung at waist
        /// height on steel stakes driven into the ice fifteen to twenty-five metres apart, with a guy either side of
        /// every stake, and it sags between them along an honest catenary. From below it reads as one thread going up
        /// the slope, which is what it is for.</summary>
        static void RescueCable(Transform root)
        {
            var group = new GameObject("RescueCable").transform; group.SetParent(root, false);
            float s0 = ArcAt(Up, AscentRoute.RopeFromEle);
            float s1 = ArcAt(Up, AscentRoute.RopeToEle, s0);
            var ch = new Chunks("Cable", 400f, CableSteel, PostPaint);
            var rng = new System.Random(2013);

            var at = new List<float>();
            for (float s = s0; s < s1 - CablePostMinM * .5f; )
            {
                at.Add(s);
                s += CablePostMinM + (float)rng.NextDouble() * (CablePostMaxM - CablePostMinM);
            }
            at.Add(s1);

            var eyes = new Vector3[at.Count];
            for (int i = 0; i < at.Count; i++)
            {
                float s = at[i];
                var mb = ch.At(s);
                var foot = On(Up, s, CableOffsetM, -.05f);
                var f = Along(Up, s);
                // the stake leans uphill, into the pull of the cable
                var axis = (Vector3.up + new Vector3(f.x, 0, f.y) * Mathf.Tan(7f * Mathf.Deg2Rad)).normalized;
                var rot = Quaternion.LookRotation(new Vector3(f.x, 0, f.y), axis);
                var mid = foot + axis * (CablePostM * .5f - .14f);
                // a steel angle, the cheap thing you can carry a bundle of and beat into ice with a hammer
                mb.Box(0, mid + rot * new Vector3(.019f, 0, 0), new Vector3(.007f, CablePostM + .28f, .05f), rot, .6f);
                mb.Box(0, mid + rot * new Vector3(0, 0, .019f), new Vector3(.05f, CablePostM + .28f, .007f), rot, .6f);
                // the painted cap: the thing that makes a 50 mm angle findable from fifty metres
                mb.Box(1, foot + axis * (CablePostM - .1f), new Vector3(.075f, .2f, .075f), rot, 1f);
                var eye = foot + axis * CableEyeM;
                Ring(mb, 0, eye, new Vector3(f.x, 0, f.y), .045f, .009f);
                eyes[i] = eye;
                // two guys across the line to short pegs, the way a stake in ice is actually held
                for (int k = -1; k <= 1; k += 2)
                {
                    var side = new Vector3(f.y, 0, -f.x) * k;
                    var pegTop = foot + side * 1.05f;
                    pegTop.y = Ground(pegTop.x, pegTop.z) + .12f;
                    mb.Tube(0, foot + axis * (CablePostM - .16f), pegTop, .006f, .006f, 4, 1f, 0, false);
                    mb.Tube(0, pegTop + Vector3.up * .06f, pegTop - Vector3.up * .3f, .012f, .012f, 5, 1f, 0, true);
                }
                cablePosts++;
            }
            for (int i = 1; i < eyes.Length; i++)
                Catenary(ch.At(at[i - 1]), 0, eyes[i - 1], eyes[i], CableSagM, CableR, 9);
            ch.Emit(group);
        }

        /// <summary>A line hung between two points as a cable really hangs: y = a·cosh(u/a), fitted to the span and the
        /// sag. A straight segment between two posts is the one thing that would give the whole slope away.</summary>
        static void Catenary(MeshBuilder mb, int sub, Vector3 a, Vector3 b, float sag, float radius, int segments)
        {
            float span = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            if (span < .05f) { mb.Tube(sub, a, b, radius, radius, 4, 1f, 0, false); return; }
            float p = CatenaryParam(span, Mathf.Max(.01f, sag));
            double top = Math.Cosh(span / (2 * p));
            var prev = a;
            for (int k = 1; k <= segments; k++)
            {
                float t = k / (float)segments;
                var q = Vector3.Lerp(a, b, t);
                double u = (t - .5) * span;
                q.y -= (float)(p * (top - Math.Cosh(u / p)));
                mb.Tube(sub, prev, q, radius, radius, 4, 1f, 0, false);
                prev = q;
            }
        }

        /// <summary>Solves d = a·(cosh(L/2a) − 1) for a, by bisection: d falls as a grows, so the bracket never
        /// escapes.</summary>
        static float CatenaryParam(float span, float sag)
        {
            double guess = span * span / (8 * sag);
            double lo = guess * .2, hi = guess * 5 + span;
            for (int i = 0; i < 60; i++)
            {
                double m = (lo + hi) * .5;
                if (m * (Math.Cosh(span / (2 * m)) - 1) > sag) lo = m; else hi = m;
            }
            return (float)((lo + hi) * .5);
        }

        // ── 3. the seasonal rope ──────────────────────────────────────────────────────────────────────────
        /// <summary>Rope handrails, which are a different animal from the cable: bright dynamic rope on ice screws and
        /// snow anchors, walked with a cow's tail clipped in and re-clipped at every intermediate point. One line on
        /// the summit rise (5 400–5 550 m, loose snow lying on ice — the worst surface on the route), and «две линии»
        /// above Pastukhov rocks, up and down, about 330 m of rope between them. Rigging is seasonal: see
        /// <see cref="RopesRigged"/>. Between its anchors the rope lies on the snow, as it does in every photograph
        /// ever taken of the place — only at a hanger does it lift off the ground.</summary>
        static void FixedRopes(Transform root)
        {
            var group = new GameObject("Ropes").transform; group.SetParent(root, false);
            var ch = new Chunks("Rope", 400f, RopeCord, AnchorAlu, AnchorSteel);

            float a0 = ArcAt(Up, SummitRopeFromEle), a1 = ArcAt(Up, SummitRopeToEle, a0);
            RopeLine(ch, a0, a1, -1.2f, 5400);

            float b0 = ArcAt(Up, PastukhovToEle) + 6f;
            float b1 = b0 + PastukhovRopeM * .5f;
            RopeLine(ch, b0, b1, -4f, 4701);          // the line people go up
            RopeLine(ch, b0, b1, +4f, 4702);          // and the line they come down

            ch.Emit(group);
            group.gameObject.SetActive(RopesRigged);
        }

        static void RopeLine(Chunks ch, float s0, float s1, float off, int seed)
        {
            var rng = new System.Random(seed);
            float phase = (float)rng.NextDouble() * 6.28f;    // so the two lines above the rocks do not wander in step
            var at = new List<float>();
            for (float s = s0; s < s1 - RopeAnchorM * .5f; s += RopeAnchorM) at.Add(s);
            at.Add(s1);

            for (int i = 0; i < at.Count; i++)
            {
                float s = at[i];
                var mb = ch.At(s);
                var f = Along(Up, s);
                var head = new Vector3(f.x, 0, f.y);
                // ice screws bite where the firn has gone to ice; higher up, in loose snow lying over ice, it is a
                // buried plate instead — which is exactly the belt Surface.LooseOverIce names
                if (EleAt(Up, s) < SummitRopeFromEle || (i & 1) == 0) IceScrew(mb, On(Up, s, off), head);
                else SnowAnchor(mb, On(Up, s, off), head);
                ropeAnchors++;
            }

            float Lift(float s)
            {
                float best = 1e9f;
                foreach (float a in at) best = Mathf.Min(best, Mathf.Abs(a - s));
                return Mathf.Lerp(RopeEyeM, RopeLieM, Mathf.Clamp01(best / 2.5f));
            }

            var prev = On(Up, s0, off, RopeEyeM);
            for (float s = s0 + 2f; s <= s1; s += 2f)
            {
                var p = On(Up, s, off + Mathf.Sin(s * .17f + phase) * .22f, Lift(s));
                ch.At(s).Tube(0, prev, p, RopeR, RopeR, 5, 1f, 0, false);
                prev = p;
            }
            ropeMetres += Mathf.RoundToInt(s1 - s0);
        }

        /// <summary>Ледобур: a tube screwed into the ice at an angle, its hanger and the carabiner left on it.</summary>
        static void IceScrew(MeshBuilder mb, Vector3 foot, Vector3 head)
        {
            var axis = (Vector3.up * .72f - head * .69f).normalized;       // screwed in leaning into the pull
            var top = foot + axis * .13f;
            mb.Tube(2, foot - axis * .2f, top, .011f, .011f, 6, 1f, 0, true);
            mb.Box(1, top + axis * .015f, new Vector3(.055f, .04f, .01f), Quaternion.LookRotation(head, axis), 1f);
            Ring(mb, 1, top + axis * .06f, head, .036f, .006f);
        }

        /// <summary>Снежный якорь: an aluminium plate buried edge-on with its wire out of the snow.</summary>
        static void SnowAnchor(MeshBuilder mb, Vector3 foot, Vector3 head)
        {
            var rot = Quaternion.LookRotation(head, Vector3.up);
            mb.Box(1, foot + Vector3.up * .02f - head * .04f, new Vector3(.26f, .3f, .012f),
                rot * Quaternion.Euler(50f, 0, 0), 1f);
            var eye = foot + Vector3.up * .2f + head * .12f;
            mb.Tube(2, foot + Vector3.up * .06f, eye, .004f, .004f, 4, 1f, 0, false);
            Ring(mb, 1, eye, head, .034f, .006f);
        }

        // ── 9. the snow-cat lane ──────────────────────────────────────────────────────────────────────────
        /// <summary>The rolled lane from the barrels to the top of the groomed ground at 5 080 m — the one landmark on
        /// the lower half of the mountain, and the thing that keeps a party on the route when nothing else does
        /// (<see cref="AscentRoute.CanReadTheRoute"/> takes catTrackUnderfoot). Packed snow with the tiller's
        /// transverse combing and the two broad bands the crawler tracks leave, ending in a hard edge with a berm
        /// pushed up on either side — and the berms stand at exactly <see cref="AscentRoute.LaneHalfWidthM"/>, so the
        /// line the crevasse rule is written against is a thing the eye can see. It is also the trap: on the Garabashi
        /// glacier the closed crevasses run under this lane.</summary>
        static void CatLane(Transform root)
        {
            var group = new GameObject("CatTrack").transform; group.SetParent(root, false);
            var ch = new Chunks("CatTrack", 600f, Tread, Snow);
            float half = AscentRoute.LaneHalfWidthM;
            const int Across = 4;                   // four strips across twelve metres: on a 6 m height grid one wide
                                                    // quad would bridge a swell and hang in the air over it
            var prev = new Vector3[Across + 1];
            var prevBerm = new Vector3[4];
            for (int i = 0; i <= Across; i++) prev[i] = On(Lane, 0, -half + i * (half * 2f / Across), .05f);
            prevBerm[0] = On(Lane, 0, -half - .95f, .01f); prevBerm[1] = On(Lane, 0, -half - .45f, .22f);
            prevBerm[2] = On(Lane, 0, half + .45f, .22f); prevBerm[3] = On(Lane, 0, half + .95f, .01f);
            float v = 0f;
            for (float s = LaneStepM; s <= laneLength; s += LaneStepM)
            {
                var mb = ch.At(s);
                var row = new Vector3[Across + 1];
                for (int i = 0; i <= Across; i++) row[i] = On(Lane, s, -half + i * (half * 2f / Across), .05f);
                float dv = new Vector2(row[Across / 2].x - prev[Across / 2].x, row[Across / 2].z - prev[Across / 2].z).magnitude / TreadTileM;
                // the texture repeats, so the running distance is folded back into 0..1 before it goes into a UV:
                // four and a half kilometres of lane would otherwise put v up near two thousand and the combing
                // would start to shimmer at the top of the slope
                float v0 = v - Mathf.Floor(v);
                for (int i = 0; i < Across; i++)
                {
                    float u0 = i / (float)Across, u1 = (i + 1) / (float)Across;
                    // a → b along the lane, a → d across it: MeshBuilder.Quad faces Cross(b - a, d - a), and
                    // across-then-along would point the whole lane at the ground
                    mb.Quad(0, prev[i], row[i], row[i + 1], prev[i + 1],
                        new Vector2(u0, v0), new Vector2(u0, v0 + dv), new Vector2(u1, v0 + dv), new Vector2(u1, v0), false, Vector3.up);
                }
                // the berm the blade throws off each side: the visible edge of the lane, and it stands at exactly
                // AscentRoute.LaneHalfWidthM, so the line the crevasse rule is written against is a thing you can see
                var berm = new[]
                {
                    On(Lane, s, -half - .95f, .01f), On(Lane, s, -half - .45f, .22f),
                    On(Lane, s, half + .45f, .22f), On(Lane, s, half + .95f, .01f),
                };
                mb.Quad(1, prevBerm[0], berm[0], berm[1], prevBerm[1], Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                mb.Quad(1, prevBerm[1], berm[1], row[0], prev[0], Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                mb.Quad(1, prev[Across], row[Across], berm[2], prevBerm[2], Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                mb.Quad(1, prevBerm[2], berm[2], berm[3], prevBerm[3], Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                prev = row; prevBerm = berm;
                v += dv;
            }
            ch.Emit(group, shadows: false);
        }

        /// <summary>The tread of the lane, drawn rather than modelled: packed snow, the transverse combing of the
        /// tiller every fifteen centimetres, and the two broad darker bands the crawler tracks leave. u runs across the
        /// lane, v along it.</summary>
        static Texture2D TreadTexture()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "elb_cat_track" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N, v = (y + .5f) / N;
                    float k = .88f + .1f * Mathf.PerlinNoise(u * 22f, v * 30f);
                    // the tiller: sixteen combs over 2.4 m of lane
                    k *= .93f + .07f * Mathf.Cos(v * Mathf.PI * 32f);
                    // the two crawler bands, a shade darker and smoother than the combed snow between them
                    float band = Mathf.Min(Mathf.Abs(u - .26f), Mathf.Abs(u - .74f));
                    if (band < .1f) k *= Mathf.Lerp(.9f, 1f, band / .1f);
                    // the shear edge where the blade cut
                    float edge = Mathf.Min(u, 1 - u);
                    if (edge < .05f) k *= Mathf.Lerp(.8f, 1f, edge / .05f);
                    px[y * N + x] = new Color(k, k * 1.005f, Mathf.Min(1f, k * 1.02f), 1f);
                }
            t.SetPixels(px); t.Apply();
            return TextureFactory.Save("elb_cat_track", t, false, TextureWrapMode.Repeat);
        }

        // ── 10. what the glacier says about its crevasses ─────────────────────────────────────────────────
        /// <summary>The Garabashi glacier between the barrels and the rise to Приют 11: about eight crevasses, four of
        /// the upper ones dangerous, all of them under the snow and all of them to the RIGHT of the lane going up
        /// (<see cref="AscentRoute.CrevasseChance"/>). The rule is never written down anywhere in the game; it is left
        /// lying about on the glacier for the player to pick up:
        /// <list type="bullet">
        /// <item>saucers of settled snow, always on the right — the shape a bridged crevasse makes as it sags;</item>
        /// <item>two crevasses actually open, blue ice in the break, in the critical band 50–100 m below the rise;</item>
        /// <item>a bamboo with a red streamer beside the worst of them, left there by somebody;</item>
        /// <item>and, on the LEFT, a meltwater stream running on the surface. Where a stream runs there is nothing
        /// under it — that is the second half of the rule, and it is the only reason the left side is ever safe.</item>
        /// </list></summary>
        static void Garabashi(Transform root)
        {
            var group = new GameObject("Garabashi").transform; group.SetParent(root, false);
            var ice = new Chunks("Crevasse", 500f, Snow, ShadeSnow, BlueIce, DarkDepth);
            var wet = new Chunks("Stream", 500f, Water, WetSnow);
            var marks = new Chunks("Warning", 500f, Bamboo, RedTape);

            float top = ArcAt(Lane, AscentRoute.CrevasseToEle);            // the rise to the hut, ≈1 890 m of lane
            float crit0 = ArcAt(Lane, AscentRoute.CriticalFromEle);
            float crit1 = ArcAt(Lane, AscentRoute.CriticalToEle, crit0);

            // the four sagging bridges: right of the lane, spread over the band the rule spreads them over
            var rng = new System.Random(3700);
            for (int i = 0; i < 4; i++)
            {
                float s = top * (.18f + i * .2f);
                float off = AscentRoute.LaneHalfWidthM + 7f + (float)rng.NextDouble() * 26f;
                Sag(ice.At(s), On(Lane, s, off), 7f + (float)rng.NextDouble() * 5f, RimM, 100 + i);
                sagCount++;
            }
            // and one just left of the lane that is nothing at all — a wind hollow, so the shape alone is not the rule
            {
                float s = top * .34f;
                Sag(ice.At(s), On(Lane, s, -19f), 6f, RimM * .7f, 117);
                sagCount++;
            }

            // the two open ones, in the critical band, lying across the fall line the way they do
            for (int i = 0; i < 2; i++)
            {
                float s = Mathf.Lerp(crit0, crit1, .3f + i * .45f);
                float off = AscentRoute.LaneHalfWidthM + 10f + i * 16f;
                var c = On(Lane, s, off);
                var a = Across(Lane, s);
                var dir = new Vector3(a.x, 0, a.y);                        // across the slope, which is along the break
                float half = (7f + i * 4f) * .5f;
                Crevasse(ice.At(s), c - dir * half, c + dir * half, 1.1f + i * .5f, 202 + i);
                crevasseCount++;
                // somebody's bamboo with a red streamer on the lane side of it
                var mark = On(Lane, s - 9f, off - 5f, -.03f);
                Wand(marks.At(s), mark, 1.45f, 8f, 40f + i * 90f, 20f + i * 60f, true, 1, 1);
                Streamer(marks.At(s), mark + Vector3.up * 1.2f, 1, 300 + i);
            }

            // the streams, low down where the ice is bare in August — and both of them on the left
            for (int i = 0; i < 2; i++)
            {
                float s = 280f + i * 420f;
                Stream(wet.At(s), On(Lane, s, -(24f + i * 13f)), 70f, 500 + i);
                streamCount++;
            }

            ice.Emit(group, collide: true);
            wet.Emit(group, shadows: false);
            marks.Emit(group);
        }

        /// <summary>A мульда: the saucer of settled snow over a bridge that has begun to go. Our terrain is a height
        /// field a prefab cannot cut, so the dish is built upward — a wind-built rim standing
        /// <paramref name="rim"/> over the snow with a low, shaded middle. Walking into it you climb a hand's breadth
        /// and then drop into shadow, which is what the real thing does to the eye at four in the morning.</summary>
        static void Sag(MeshBuilder mb, Vector3 centre, float radius, float rim, int seed)
        {
            const int Rings = 7, Sect = 20;
            var rnd = new System.Random(seed);
            float wob = (float)rnd.NextDouble() * 6.28f;
            Vector3 P(int i, int j)
            {
                float ang = j / (float)Sect * Mathf.PI * 2;
                float t = i / (float)Rings;
                float r = radius * t * (.84f + .16f * Mathf.Sin(ang * 3f + wob));
                float x = centre.x + Mathf.Cos(ang) * r, z = centre.z + Mathf.Sin(ang) * r;
                // zero in the middle and zero again where it meets the snow, with the wind-built crest about two
                // thirds of the way out: a saucer, not a step
                float lift = rim * 1.6f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * Mathf.SmoothStep(0f, 1f, t);
                return new Vector3(x, Ground(x, z) + .03f + lift, z);
            }
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Sect; j++)
                {
                    int sub = i < 3 ? 1 : 0;                                // the floor lies in its own shadow
                    mb.Quad(sub, P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
        }

        /// <summary>A crevasse that is open: the wind-built lips, the blue ice of the walls, the dark below. Built
        /// upward for the same reason the saucers are — the hole is a 1.15 m swell of snow split along its length, and
        /// from two paces away it reads as a gash you would not step over.</summary>
        static void Crevasse(MeshBuilder mb, Vector3 a, Vector3 b, float width, int seed)
        {
            const int N = 18;
            var rnd = new System.Random(seed);
            float wob = (float)rnd.NextDouble() * 6.28f;
            var dir = new Vector3(b.x - a.x, 0, b.z - a.z);
            float len = dir.magnitude;
            if (len < .5f) return;
            dir /= len;
            var side = new Vector3(dir.z, 0, -dir.x);

            Vector3 At(float t, float across, float up)
            {
                var p = a + dir * (len * t) + side * across;
                return new Vector3(p.x, Ground(p.x, p.z) + up, p.z);
            }
            float W(float t) => width * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI), .45f);
            float Lip(float t) => CrevasseLipM * (.72f + .28f * Mathf.Sin(t * 9f + wob)) * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

            for (int k = 0; k < N; k++)
            {
                float t0 = k / (float)N, t1 = (k + 1) / (float)N;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float w0 = W(t0) * .5f * sgn, w1 = W(t1) * .5f * sgn;
                    float o0 = (Mathf.Abs(w0) + 3.2f) * sgn, o1 = (Mathf.Abs(w1) + 3.2f) * sgn;
                    float c0 = (Mathf.Abs(w0) + .4f) * sgn, c1 = (Mathf.Abs(w1) + .4f) * sgn;
                    // outer snow, tapering away to nothing
                    mb.Quad(0, At(t0, o0, 0f), At(t1, o1, 0f), At(t1, c1, Lip(t1)), At(t0, c0, Lip(t0)),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                    // the crest of the lip and its overhang into the break
                    mb.Quad(0, At(t0, c0, Lip(t0)), At(t1, c1, Lip(t1)), At(t1, w1, Lip(t1) * .92f), At(t0, w0, Lip(t0) * .92f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                    // the wall, blue because the break is deep and the ice is old
                    mb.Quad(2, At(t0, w0, Lip(t0) * .92f), At(t1, w1, Lip(t1) * .92f), At(t1, w1 * .5f, .05f), At(t0, w0 * .5f, .05f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
                // and the dark that has no bottom
                mb.Quad(3, At(t0, -W(t0) * .25f, .04f), At(t1, -W(t1) * .25f, .04f), At(t1, W(t1) * .25f, .04f), At(t0, W(t0) * .25f, .04f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            }
        }

        /// <summary>A meltwater stream on the glacier, following the fall line. Where one runs on the surface there is
        /// no crevasse under it — and that is the one hint the world gives the player for free
        /// (<see cref="AscentRoute.CrevasseChance"/> takes streamNearby). Both of ours run down the LEFT side, which is
        /// the side the rule says is free.</summary>
        static void Stream(MeshBuilder mb, Vector3 start, float length, int seed)
        {
            var rnd = new System.Random(seed);
            var p = start;
            float run = 0f, phase = (float)rnd.NextDouble() * 6.28f;
            var prevL = p; var prevR = p; var prevA = p; var prevB = p;
            bool first = true;
            while (run < length)
            {
                var (dx, dz, slope) = dem.Fall(p.x, p.z, 10f);
                if (slope < .2f) break;
                var down = new Vector3(dx, 0, dz).normalized;
                var across = new Vector3(down.z, 0, -down.x);
                float meander = Mathf.Sin(run * .09f + phase) * .5f;
                var next = p + down * 3f + across * meander;
                next.y = Ground(next.x, next.z);
                float w = .3f + .12f * Mathf.Sin(run * .21f + phase);
                var l = next - across * w; l.y = Ground(l.x, l.z) + .02f;
                var r = next + across * w; r.y = Ground(r.x, r.z) + .02f;
                var aa = next - across * (w + .9f); aa.y = Ground(aa.x, aa.z) + .015f;
                var bb = next + across * (w + .9f); bb.y = Ground(bb.x, bb.z) + .015f;
                if (!first)
                {
                    // downstream first, across second — same reason as the lane
                    mb.Quad(0, prevL, l, r, prevR, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                    mb.Quad(1, prevA, aa, l, prevL, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                    mb.Quad(1, prevR, r, bb, prevB, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                }
                prevL = l; prevR = r; prevA = aa; prevB = bb;
                p = next; run += 3f; first = false;
            }
        }

        /// <summary>A strip of red tape left tied to a wand: the mark somebody put beside a hole.</summary>
        static void Streamer(MeshBuilder mb, Vector3 knot, int sub, int seed)
        {
            var rnd = new System.Random(seed);
            float dir = (float)rnd.NextDouble() * 6.28f;
            var side = new Vector3(Mathf.Sin(dir), 0, Mathf.Cos(dir));
            var prev = knot;
            for (int k = 1; k <= 5; k++)
            {
                var next = knot + side * (k * .11f) + Vector3.down * (k * k * .022f);
                var n = Vector3.Cross(next - prev, Vector3.up).normalized;
                mb.Quad(sub, prev - n * .015f, prev + n * .015f, next + n * .015f, next - n * .015f,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                prev = next;
            }
        }
    }
}
