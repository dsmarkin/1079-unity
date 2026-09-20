using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>The bottom of the map: the Baksan valley between Azau and Terskol, and the two places an
    /// acclimatisation day actually goes to — the Девичьи Косы waterfall and the Terskol observatory on пик Терскол.
    ///
    /// Why it exists. A real Elbrus trip is a week, and days two and three are not on the mountain at all: they are
    /// walks out of the village to the waterfall and up to the observatory and back down to sleep low. Until now the
    /// whole lower half of our 12,3 km square was empty — forest, meadow and nothing in it. This file fills it with
    /// three real places and the paths between them.
    ///
    /// Everything is baked in world coordinates against the Elbrus height field and saved as ONE prefab,
    /// Assets/Generated/World/Prefabs/Elbrus/Elb_Valley, exactly the way <see cref="ElbrusAscent"/> bakes the mountain above
    /// Гара-Баши. The runtime glue is a single line: instantiate it at the origin with no rotation. Fifty-odd
    /// buildings, four routes, several kilometres of fence, wire and gas pipe cost a few dozen renderers, not a few
    /// thousand transforms.
    ///
    /// Seating follows <see cref="Height1079.Runtime.ElbrusWorld"/> and <see cref="ElbrusAscent"/>: a building takes
    /// the nine samples of its footprint and stands on the highest of them (Sit.Pad), a loose prop sits at the middle
    /// of its footprint (Sit.Flat), anything on wheels lies on the slope plane (Sit.Lie). Those helpers are internal
    /// to the runtime assembly, so the same three rules are written out again below — <see cref="Pad"/>,
    /// <see cref="Stand"/>, <see cref="Flat"/>, <see cref="Lay"/>.
    ///
    /// Heights come off our own height field and never off a signpost, and our height field is built from ~30 m radar
    /// (docs/ELBRUS.md): in a gorge this narrow it fills the bottom in, so the village reads 2 260 m where the real
    /// one is at 2 100, and the waterfall reads 3 060 m where the real one is at 2 800. The relative order — village
    /// low, waterfall high above it, observatory highest — is what the day is built on, and that survives.
    ///
    /// Snow. The village (2 200–2 270 m) and the waterfall (3 060 m) are BELOW the summer firn line the terrain
    /// splat draws (patches from 3 250 m, nothing at all under 3 050 m — <see cref="ElbrusImporter"/>), so not one
    /// snow material is used anywhere in this file. There is no snow in Terskol in July and there must be none here.
    ///
    /// Night. This is the only place on the map with electricity and people in it, so it is the only place with lit
    /// windows and street lamps. Window glass is emissive Standard, which is dark under a torch and bright of its
    /// own accord — never unlit, which would burn white at two in the morning (CLAUDE.md, «Ночь»).
    ///
    /// Sources are listed in docs/ELBRUS.md, section «Низ карты».</summary>
    public static class ElbrusValley
    {
        const string PrefabDir = WorldPaths.Kit + "/Prefabs/Elbrus";
        const string MeshDir = WorldPaths.Kit + "/Meshes/Valley";

        // ── the three places, as the real map puts them ───────────────────────────────────────────────────
        /// <summary>Посёлок Терскол, 43.26562 / 42.51519 — the centre of the village, by the junction where the road
        /// from the Baksan gorge turns up towards Azau. Real height ≈2 100 m; our DEM reads 2 261 m here.</summary>
        public static readonly Vector2 Terskol = new Vector2(4597f, -4831f);
        /// <summary>Обсерватория «Пик Терскол», 43.27477 / 42.49994 — the international astronomical station on пик
        /// Терскол. Signposted 3 127–3 150 m; our DEM reads 3 088 m on the shelf it stands on.</summary>
        public static readonly Vector2 Observatory = new Vector2(3358f, -3814f);
        /// <summary>Водопад «Девичьи Косы», 43.27837 / 42.49826 — 25 m of water fanning down a rock slab on the
        /// Чыранбаши-Су, the stream that drains the Гара-Баши glacier. Signposted 2 800 m; our DEM reads 3 060 m,
        /// because a 30 m raster cannot hold the gorge the fall is cut into.</summary>
        public static readonly Vector2 Falls = new Vector2(3222f, -3414f);
        /// <summary>Where the valley road ends: the square in front of the Azau terminals (<see cref="Elbrus.Start"/>).</summary>
        static Vector2 Azau => new Vector2(Elbrus.Azau.X + 24f, Elbrus.Azau.Z - 30f);

        // ── the numbers the places are built to ───────────────────────────────────────────────────────────
        /// <summary>Length of the village ribbon along the river, in metres of z. Real Terskol is about 900 m from the
        /// bridge to the last shed; ours is the 610 m of gorge floor our height field actually holds.</summary>
        const float VillageFromZ = -4790f, VillageToZ = -5400f;
        /// <summary>Half-width of the band the river is looked for in.</summary>
        const float RiverXLo = 4440f, RiverXHi = 4920f;
        /// <summary>The cross-section of the village, in metres from the middle of the river — negative is the left
        /// bank going downstream (east, where the street and the private sector are), positive the right bank (west,
        /// where the base is). The whole village is 130 m wide because that is how much floor the gorge has: at 90 m
        /// east of the water the hillside is already standing at nearly thirty degrees.
        /// Lавки −20, улица −34, дома −54, верхний проулок −68, частный сектор −86; правый берег +32 и +48.</summary>
        const float StreetOff = -34f, LaneOff = -68f, WestOff = 32f;
        /// <summary>The fall itself: 25 m high, about 15 m wide where it fans out at the bottom (resort-elbrus.ru,
        /// vpoxod.ru). Ours is built as a rock band standing proud of the slope, because a prefab cannot cut a hole in
        /// a height field — the same trick <see cref="ElbrusAscent"/> uses for the crevasses.</summary>
        const float FallHeightM = 25f, FallWidthM = 15f;
        /// <summary>Диаметр полноповоротного купола «Цейсс-2000» — 20 м, масса 250 т (ИНАСАН). Ours is that dome.</summary>
        const float BigDomeR = 10f;

        static HeightField dem;
        static int meshCounter;
        static int houses, sheds, shops, publicBuildings, obsBuildings, lamps, poles, guardPosts, cairns, signs, bridges, cars;
        static float roadMetres, trailMetres, fenceMetres, wireMetres, gasMetres, fallDrop, plinthMax;

        // ── materials (own ElbV* names, so this file never fights the other factories over an asset) ───────
        static Material Asphalt => Materials.Get("ElbVAsphalt", new Color(.34f, .34f, .35f),
            TextureFactory.Ground("elbv_asphalt", new Color(.17f, .17f, .18f), new Color(.38f, .38f, .39f), 6f, 311, .1f),
            null, .12f, tiling: new Vector2(3f, 3f));
        static Material Gravel => Materials.Get("ElbVGravel", new Color(.62f, .6f, .56f),
            TextureFactory.Ground("elbv_gravel", new Color(.3f, .29f, .26f), new Color(.63f, .61f, .55f), 9f, 313, .3f),
            null, .07f, tiling: new Vector2(3f, 3f));
        /// <summary>A path is not a road: it is bare trodden earth with the stones of the slope showing through it,
        /// a shade darker and browner than the turf either side.</summary>
        static Material Trodden => Materials.Get("ElbVTrodden", new Color(.58f, .53f, .45f),
            TextureFactory.Ground("elbv_trodden", new Color(.27f, .24f, .19f), new Color(.6f, .55f, .45f), 7f, 317, .26f),
            null, .05f, tiling: new Vector2(2f, 2f));
        static Material Verge => Materials.Get("ElbVVerge", new Color(.4f, .42f, .3f),
            TextureFactory.Ground("elbv_verge", new Color(.19f, .21f, .13f), new Color(.45f, .44f, .3f), 5f, 319, .22f),
            null, .05f, tiling: new Vector2(2f, 2f));
        static Material Rock => Materials.PH("ElbVRock", "lichen_rock", new Color(.78f, .78f, .76f), 2f, .1f);
        static Material RockWet => Materials.PH("ElbVRockWet", "lichen_rock", new Color(.42f, .44f, .45f), 2f, .62f);
        static Material Water => Materials.Get("ElbVWater", new Color(.26f, .38f, .44f), smoothness: .93f);
        /// <summary>Broken white water: the fall itself, the rapids under the bridges, the foam of the pool. Light,
        /// rough, and lit — an unlit white would be a lamp at night.</summary>
        static Material Foam => Materials.Get("ElbVFoam", new Color(.88f, .91f, .93f), smoothness: .35f);
        static Material Concrete => Materials.Get("ElbVConcrete", new Color(.63f, .62f, .59f), smoothness: .08f);
        static Material ConcreteDark => Materials.Get("ElbVConcreteDark", new Color(.44f, .44f, .42f), smoothness: .07f);
        static Material Stone => Materials.PH("ElbVStone", "rock_face_03", new Color(.62f, .59f, .54f), 1.4f, .08f);
        static Material Plaster => Materials.Get("ElbVPlaster", new Color(.84f, .79f, .68f), smoothness: .06f);
        static Material PlasterCold => Materials.Get("ElbVPlasterCold", new Color(.72f, .74f, .73f), smoothness: .06f);
        static Material PlasterBlue => Materials.Get("ElbVPlasterBlue", new Color(.58f, .66f, .7f), smoothness: .06f);
        static Material Brick => Materials.Get("ElbVBrick", new Color(.55f, .4f, .33f), smoothness: .05f);
        static Material Plank => Materials.PH("ElbVPlank", "raw_plank_wall", new Color(.72f, .58f, .42f), 1.6f);
        static Material PlankGrey => Materials.PH("ElbVPlankGrey", "raw_plank_wall", new Color(.46f, .44f, .4f), 1.6f);
        static Material Log => Materials.Get("ElbVLog", new Color(.6f, .48f, .34f), smoothness: .06f);
        /// <summary>Профлист: the corrugated sheet that roofs and fences half of Prielbrusye.</summary>
        static Material Sheet => Materials.Get("ElbVSheet", new Color(.6f, .63f, .66f), smoothness: .5f);
        static Material SheetGreen => Materials.Get("ElbVSheetGreen", new Color(.24f, .38f, .31f), smoothness: .46f);
        static Material SheetRed => Materials.Get("ElbVSheetRed", new Color(.5f, .21f, .16f), smoothness: .46f);
        static Material SheetBlue => Materials.Get("ElbVSheetBlue", new Color(.19f, .32f, .5f), smoothness: .46f);
        /// <summary>Шифер: grey asbestos-cement sheet, the other half of every roof here.</summary>
        static Material Slate => Materials.Get("ElbVSlate", new Color(.55f, .56f, .55f), smoothness: .12f);
        static Material Rust => Materials.Get("ElbVRust", new Color(.44f, .29f, .2f), smoothness: .16f);
        static Material Steel => Materials.Get("ElbVSteel", new Color(.45f, .47f, .5f), smoothness: .55f);
        static Material Alu => Materials.Get("ElbVAlu", new Color(.74f, .76f, .79f), smoothness: .62f);
        static Material White => Materials.Get("ElbVWhite", new Color(.9f, .91f, .9f), smoothness: .45f);
        static Material Red => Materials.Get("ElbVRed", new Color(.73f, .18f, .14f), smoothness: .4f);
        static Material Green => Materials.Get("ElbVGreen", new Color(.22f, .38f, .28f), smoothness: .35f);
        static Material Khaki => Materials.Get("ElbVKhaki", new Color(.36f, .38f, .28f), smoothness: .35f);
        /// <summary>The yellow gas main on its little stilts, the one thing that follows every street in the Baksan
        /// valley from Terskol to Azau.</summary>
        static Material GasYellow => Materials.Get("ElbVGas", new Color(.85f, .69f, .12f), smoothness: .4f);
        static Material Cloth1 => Materials.Cloth("elbv_wash_a", new Color(.78f, .8f, .82f));
        static Material Cloth2 => Materials.Cloth("elbv_wash_b", new Color(.5f, .24f, .22f));
        static Material Tarp => Materials.Get("ElbVTarp", new Color(.76f, .74f, .66f), smoothness: .08f);
        static Material Turf => Materials.Get("ElbVTurf", new Color(.37f, .4f, .27f),
            TextureFactory.Ground("elbv_turf", new Color(.19f, .21f, .13f), new Color(.44f, .43f, .29f), 5f, 91, .24f),
            null, .05f, tiling: new Vector2(2f, 2f));

        /// <summary>A surface that shows its own light — a lit window, a bulb over a door, the red lamp on the mast.
        /// No GI: what it shows is all a lit map needs, and it stays dark under a torch beam the way an unlit material
        /// never would.</summary>
        static Material Emissive(string name, Color baseColor, Color emission, float smoothness = .3f)
        {
            var m = Materials.Get(name, baseColor, smoothness: smoothness);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A window with the light on behind it. Warm, not bright: from the road it is a yellow rectangle,
        /// and up close it is a curtain with a lamp behind it.</summary>
        static Material WindowLit => Emissive("ElbVWindowLit", new Color(.5f, .42f, .28f), new Color(1f, .78f, .42f) * 1.5f, .35f);
        /// <summary>A window with nobody behind it. Dark glass, and it keeps its highlight.</summary>
        static Material WindowDark => Materials.Get("ElbVWindowDark", new Color(.11f, .13f, .16f), smoothness: .9f);
        static Material LampGlow => Emissive("ElbVLampGlow", new Color(.97f, .92f, .8f), new Color(1f, .84f, .56f) * 2.6f, .4f);
        /// <summary>The obstruction light on the observatory mast and on the top of the five-storey block: the two
        /// red points that say, from the bottom of the valley at night, that there is something up there.</summary>
        static Material RedLamp => Emissive("ElbVRedLamp", new Color(.4f, .08f, .06f), new Color(1f, .12f, .08f) * 2.2f, .5f);

        // ── little helpers, the same ones every factory here has ──────────────────────────────────────────
        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            Directory.CreateDirectory(MeshDir);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string meshName = $"Val_{name}_{meshCounter++}";
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

        /// <summary>Text painted on a board. A TextMesh faces its own +Z, so <paramref name="yaw"/> is the direction
        /// the reader looks <em>from</em>; <see cref="Height1079.Runtime.SignText"/> keeps it behind whatever stands
        /// in front of it instead of shining through the board.</summary>
        static GameObject Label(Transform t, string text, Vector3 at, float yaw, float size, Color color)
        {
            var go = new GameObject("Text"); go.transform.SetParent(t, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var tm = go.AddComponent<TextMesh>();
            go.AddComponent<Height1079.Runtime.SignText>();
            tm.text = text; tm.characterSize = size; tm.fontSize = 70;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            signs++;
            return go;
        }

        /// <summary>A point light with a bulb on it. Range is short and the shadows are off: there are a dozen of
        /// these in the village and the built-in pipeline pays for every pixel light.</summary>
        static void Lamp(Transform t, Vector3 at, float range, float intensity, Color color, bool pixel)
        {
            var go = new GameObject("Lamp", typeof(Light)); go.transform.SetParent(t, false);
            go.transform.localPosition = at;
            var l = go.GetComponent<Light>();
            l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = pixel ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
            lamps++;
        }

        static void B(MeshBuilder mb, int s, Vector3 c, Vector3 size, float uv = .7f) => mb.Box(s, c, size, Quaternion.identity, uv);
        static void BR(MeshBuilder mb, int s, Vector3 c, Vector3 size, Quaternion rot, float uv = .7f) => mb.Box(s, c, size, rot, uv);
        static void T(MeshBuilder mb, int s, Vector3 a, Vector3 b, float r, int sides = 7) => mb.Tube(s, a, b, r, r, sides, 1f, 0, true);

        /// <summary>A flat card standing in the world: a pane, a sign face, a sheet of washing.</summary>
        static void Card(MeshBuilder mb, int s, Vector3 centre, Quaternion rot, float w, float h, bool doubleSided = true)
        {
            Vector3 P(float u, float v) => centre + rot * new Vector3(u * w * .5f, v * h * .5f, 0);
            mb.Quad(s, P(-1, -1), P(1, -1), P(1, 1), P(-1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, doubleSided);
        }

        // ── the ground, and how things meet it ────────────────────────────────────────────────────────────
        /// <summary>Where the three crossings of the Baksan are, as a fraction of the river's length through the
        /// village: the road bridge at the top, two footbridges below it. One array, so the deck and the piece of road
        /// that runs onto it can never end up in different places.</summary>
        static readonly float[] Crossings = { .16f, .46f, .78f };

        static float Ground(float x, float z) => dem.Sample(x, z);
        static float Ground(Vector2 p) => dem.Sample(p.x, p.y);

        /// <summary>Lowest and highest of the nine samples under a footprint (corners, edge middles, centre) turned by
        /// <paramref name="yaw"/> — the same nine <see cref="Height1079.Runtime.ElbrusWorld"/> takes, so a house here
        /// and a hut on the mountain sit on the slope the same way. Copied, because the original is internal to the
        /// runtime assembly.</summary>
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

        /// <summary>Sit.Pad: a building stands on the HIGHEST corner of its footprint, or the hillside pokes up
        /// through the floor.</summary>
        static float Stand(float x, float z, float yaw, Vector2 size) => Pad(x, z, yaw, size).hi + .05f;

        /// <summary>Where the floor of a building goes. A small step in the ground is taken the way
        /// <see cref="Height1079.Runtime.ElbrusWorld"/> takes it — stand on the highest corner and put a plinth under
        /// the rest, or the hillside pokes up through the floor. But a thirty-four-metre barrack across a gorge side
        /// steps eight or twelve metres end to end, and standing THAT on its highest corner leaves the far end nine
        /// metres in the air on a plinth nobody would ever build. So anything with a real step under it is terraced
        /// instead: the floor goes two thirds of the way up the drop, the hill buries the uphill end the way a cutting
        /// does, and the downhill end stands on the fill of its own terrace.</summary>
        static float Seat(float x, float z, float yaw, Vector2 size)
        {
            var (lo, hi) = Pad(x, z, yaw, size);
            float drop = hi - lo;
            return drop <= 2.5f ? Stand(x, z, yaw, size) : lo + drop * .62f + .05f;
        }

        /// <summary>Sit.Flat: a loose prop sits at the middle of its footprint.</summary>
        static float Flat(float x, float z, float yaw, Vector2 size)
        {
            var (lo, hi) = Pad(x, z, yaw, size);
            return (lo + hi) * .5f - .04f;
        }

        /// <summary>Sit.Lie: laid on the slope plane and tilted to it, the way a car parked across a hillside leans.</summary>
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

        /// <summary>Compass bearing (degrees, z = north) of the fall line — a house on a slope faces downhill.</summary>
        static float FaceDownhill(float x, float z, float span = 14f)
        {
            var (dx, dz, slope) = dem.Fall(x, z, span);
            return slope < .2f ? 0f : Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>World-space geometry collected into chunk meshes by a square of ground, so a village of fifty
        /// buildings costs a dozen renderers instead of fifty transforms. Sorted, so two builds of the same world give
        /// the same asset names.</summary>
        sealed class Batch
        {
            readonly SortedDictionary<long, MeshBuilder> parts = new SortedDictionary<long, MeshBuilder>();
            readonly string name;
            readonly float span;
            readonly Material[] mats;

            public Batch(string name, float span, params Material[] mats)
            {
                this.name = name; this.span = span; this.mats = mats;
            }

            public MeshBuilder At(float x, float z)
            {
                long cx = Mathf.FloorToInt(x / span) + 4096, cz = Mathf.FloorToInt(z / span) + 4096;
                long k = cz * 8192 + cx;
                if (!parts.TryGetValue(k, out var mb)) parts[k] = mb = new MeshBuilder(mats.Length);
                return mb;
            }

            public MeshBuilder At(Vector2 p) => At(p.x, p.y);
            public MeshBuilder At(Vector3 p) => At(p.x, p.z);

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

        // ── laying a route on the relief ──────────────────────────────────────────────────────────────────
        /// <summary>A road or a path found on the height field itself, never drawn by hand: Dijkstra over a lattice
        /// of <paramref name="step"/>-metre nodes between the two ends, with the cost of a hop being its length
        /// multiplied by a penalty that grows as the fourth power of the gradient. Anything steeper than twice
        /// <paramref name="maxGrade"/> is simply impassable.
        ///
        /// That single rule is what makes a serpentine: a line straight up a 30° hillside is cheap in metres and
        /// ruinous in penalty, so the search folds it into switchbacks until every leg is at a walkable angle — the
        /// way a road is actually surveyed, and the way a herd path wears in. The lattice step sets the scale of the
        /// bends (a road turns in fifty metres, a footpath in thirty), and the neighbourhood is the sixteen coprime
        /// moves inside a 5×5 window, so a leg can leave at any of sixteen bearings instead of eight.
        ///
        /// Deterministic: same height field, same route, every build. <paramref name="pad"/> is how far outside the
        /// bounding box of the two ends the search is allowed to wander, which is what gives it room to zigzag.</summary>
        static (float x, float z)[] Survey(Vector2 from, Vector2 to, float maxGrade, float step, float pad, float penalty)
        {
            float x0 = Mathf.Min(from.x, to.x) - pad, x1 = Mathf.Max(from.x, to.x) + pad;
            float z0 = Mathf.Min(from.y, to.y) - pad, z1 = Mathf.Max(from.y, to.y) + pad;
            // never search outside the map: Sample() clamps at the edge and a clamped plateau would look passable
            x0 = Mathf.Max(x0, -Elbrus.Half + step); x1 = Mathf.Min(x1, Elbrus.Half - step);
            z0 = Mathf.Max(z0, -Elbrus.Half + step); z1 = Mathf.Min(z1, Elbrus.Half - step);
            int nx = Mathf.FloorToInt((x1 - x0) / step) + 1, nz = Mathf.FloorToInt((z1 - z0) / step) + 1;
            int n = nx * nz;
            Vector2 P(int i) => new Vector2(x0 + i % nx * step, z0 + i / nx * step);
            int Index(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y - z0) / step), 0, nz - 1) * nx
                                  + Mathf.Clamp(Mathf.RoundToInt((p.x - x0) / step), 0, nx - 1);

            var h = new float[n];
            for (int i = 0; i < n; i++) { var p = P(i); h[i] = Ground(p.x, p.y); }

            // the sixteen coprime moves of a 5×5 window: eight compass points plus the eight knight's moves
            var dirs = new[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
                (1, 2), (2, 1), (-1, 2), (-2, 1), (1, -2), (2, -1), (-1, -2), (-2, -1),
            };

            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            int src = Index(from), dst = Index(to);
            dist[src] = 0f;

            var heap = new Heap(n);
            heap.Push(src, 0f);
            while (heap.Count > 0)
            {
                int u = heap.Pop();
                if (done[u]) continue;
                done[u] = true;
                if (u == dst) break;
                int ui = u % nx, uk = u / nx;
                foreach (var (di, dk) in dirs)
                {
                    int vi = ui + di, vk = uk + dk;
                    if (vi < 0 || vi >= nx || vk < 0 || vk >= nz) continue;
                    int v = vk * nx + vi;
                    if (done[v]) continue;
                    float len = Mathf.Sqrt(di * di + dk * dk) * step;
                    float grade = Mathf.Abs(h[v] - h[u]) / len;
                    if (grade > maxGrade * 2f) continue;
                    float k = grade / maxGrade;
                    float d = dist[u] + len * (1f + penalty * k * k * k * k);
                    if (d >= dist[v]) continue;
                    dist[v] = d; prev[v] = u;
                    heap.Push(v, d);
                }
            }
            if (prev[dst] < 0 && dst != src) return new[] { (from.x, from.y), (to.x, to.y) };

            var back = new List<(float x, float z)>();
            for (int at = dst; at >= 0; at = prev[at]) { var p = P(at); back.Add((p.x, p.y)); if (at == src) break; }
            back.Reverse();
            // the tolerance is small on purpose. Simplify at a fifth of the lattice step only merges the collinear
            // runs the search leaves behind; anything larger starts swallowing whole switchbacks — the apex of a
            // hairpin deviates less from the chord between its two arms than the tolerance does, and the chord then
            // runs straight up the hillside at thirty-seven degrees where the survey said eleven.
            return Round(Simplify(back.ToArray(), step * .05f), step * .1f, maxGrade);
        }

        /// <summary>A binary heap of (node, cost). Unity's C# has no PriorityQueue, and a sorted set of tuples would
        /// cost more than the search.</summary>
        sealed class Heap
        {
            readonly int[] item;
            readonly float[] key;
            int count;
            public Heap(int capacity) { item = new int[capacity * 8 + 16]; key = new float[capacity * 8 + 16]; }
            public int Count => count;

            public void Push(int value, float cost)
            {
                if (count >= item.Length) return;            // the lattice is small; overflow means a bug, not a stall
                int i = count++;
                item[i] = value; key[i] = cost;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (key[p] <= key[i]) break;
                    (item[p], item[i]) = (item[i], item[p]);
                    (key[p], key[i]) = (key[i], key[p]);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = item[0];
                count--;
                item[0] = item[count]; key[0] = key[count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < count && key[l] < key[m]) m = l;
                    if (r < count && key[r] < key[m]) m = r;
                    if (m == i) break;
                    (item[m], item[i]) = (item[i], item[m]);
                    (key[m], key[i]) = (key[i], key[m]);
                    i = m;
                }
                return top;
            }
        }

        /// <summary>Douglas–Peucker: drops the lattice's staircase and keeps the bends.</summary>
        static (float x, float z)[] Simplify((float x, float z)[] pts, float tol)
        {
            if (pts.Length < 3) return pts;
            var keep = new bool[pts.Length];
            keep[0] = keep[pts.Length - 1] = true;
            var stack = new Stack<(int lo, int hi)>();
            stack.Push((0, pts.Length - 1));
            while (stack.Count > 0)
            {
                var (lo, hi) = stack.Pop();
                if (hi - lo < 2) continue;
                float ax = pts[lo].x, az = pts[lo].z, dx = pts[hi].x - ax, dz = pts[hi].z - az;
                float len2 = dx * dx + dz * dz;
                float best = -1f; int bi = -1;
                for (int i = lo + 1; i < hi; i++)
                {
                    float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(((pts[i].x - ax) * dx + (pts[i].z - az) * dz) / len2);
                    float ex = pts[i].x - (ax + dx * t), ez = pts[i].z - (az + dz * t);
                    float d = ex * ex + ez * ez;
                    if (d > best) { best = d; bi = i; }
                }
                if (bi < 0 || best <= tol * tol) continue;
                keep[bi] = true;
                stack.Push((lo, bi)); stack.Push((bi, hi));
            }
            var outp = new List<(float x, float z)>();
            for (int i = 0; i < pts.Length; i++) if (keep[i]) outp.Add(pts[i]);
            return outp.ToArray();
        }

        /// <summary>Rounds the corners a lattice search leaves behind, and rounds them by a fixed radius rather than
        /// by a fraction of the leg. Plain Chaikin cutting would take a quarter off both legs of every hairpin, which
        /// on a road with fifty-five hairpins in it both shortens the route by a quarter and drives a shortcut
        /// straight up the hillside between the two arms — the surveyed 11° turns into 34° at the apex. A fillet of
        /// <paramref name="radius"/> metres, capped at a third of the shorter leg, gives the bend a turning circle and
        /// leaves the grade where the survey put it.</summary>
        static (float x, float z)[] Round((float x, float z)[] pts, float radius, float maxGrade)
        {
            if (pts.Length < 3) return pts;
            var o = new List<(float x, float z)> { pts[0] };
            for (int i = 1; i < pts.Length - 1; i++)
            {
                var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
                float la = Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                float lc = Mathf.Sqrt((c.x - b.x) * (c.x - b.x) + (c.z - b.z) * (c.z - b.z));
                float r = Mathf.Min(radius, Mathf.Min(la, lc) / 3f);
                if (r < .5f || la < 1e-3f || lc < 1e-3f) { o.Add(b); continue; }
                // a hairpin is not a corner to be rounded off: the two arms are nearly antiparallel, so a fillet
                // there folds the line back over itself and the strip baked along it crosses. Anything sharper than a
                // right angle keeps its vertex — which is what a switchback looks like anyway.
                float cos = ((b.x - a.x) * (c.x - b.x) + (b.z - a.z) * (c.z - b.z)) / (la * lc);
                if (cos <= .5f) { o.Add(b); continue; }
                float p1x = b.x + (a.x - b.x) / la * r, p1z = b.z + (a.z - b.z) / la * r;
                float p2x = b.x + (c.x - b.x) / lc * r, p2z = b.z + (c.z - b.z) / lc * r;
                // and it must not cut across the contour: on a forty-degree traverse the chord over the inside of a
                // bend picks up in five metres what the surveyed line spreads over fifty
                float chord = Mathf.Sqrt((p2x - p1x) * (p2x - p1x) + (p2z - p1z) * (p2z - p1z));
                if (chord > .5f && Mathf.Abs(Ground(p2x, p2z) - Ground(p1x, p1z)) / chord > maxGrade * 1.5f) { o.Add(b); continue; }
                o.Add((p1x, p1z));
                o.Add(((p1x + p2x) * .25f + b.x * .5f, (p1z + p2z) * .25f + b.z * .5f));
                o.Add((p2x, p2z));
            }
            o.Add(pts[pts.Length - 1]);
            return o.ToArray();
        }

        /// <summary>A polyline pushed sideways by <paramref name="off"/> metres, positive to the RIGHT of the way it
        /// runs — the same sign convention as <see cref="Elbrus.Nearest"/>. This is how the street, the upper lane and
        /// the far bank are got out of one line down the middle of the river.</summary>
        static (float x, float z)[] Shift((float x, float z)[] line, float off)
        {
            var o = new (float x, float z)[line.Length];
            for (int i = 0; i < line.Length; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(line.Length - 1, i + 1);
                var d = new Vector2(line[b].x - line[a].x, line[b].z - line[a].z);
                if (d.sqrMagnitude < 1e-6f) d = Vector2.up;
                d.Normalize();
                o[i] = (line[i].x + d.y * off, line[i].z - d.x * off);
            }
            return o;
        }

        /// <summary>The bottom of a gorge, read straight off the height field: for every step of z between two banks,
        /// the x that measures lowest. This is where the Baksan runs through Terskol, and everything in the village —
        /// the street, the lane, the bridges — is hung off it.</summary>
        static (float x, float z)[] Thalweg(float zFrom, float zTo, float xLo, float xHi, float step)
        {
            var pts = new List<(float x, float z)>();
            for (float z = zFrom; z >= zTo; z -= step)
            {
                float bestX = xLo, bestH = float.MaxValue;
                for (float x = xLo; x <= xHi; x += 3f)
                {
                    float h = Ground(x, z);
                    if (h < bestH) { bestH = h; bestX = x; }
                }
                pts.Add((bestX, z));
            }
            // the raw minimum jumps a cell at a time; three passes of the box filter turn it into a watercourse
            var a = pts.ToArray();
            for (int p = 0; p < 3; p++)
            {
                var b = new (float x, float z)[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    int lo = Mathf.Max(0, i - 1), hi = Mathf.Min(a.Length - 1, i + 1);
                    b[i] = ((a[lo].x + a[i].x + a[hi].x) / 3f, a[i].z);
                }
                a = b;
            }
            return a;
        }

        // ── baking a route into the ground ───────────────────────────────────────────────────────────────
        /// <summary>A strip laid along a route and sat on the height field at every station: the running surface of a
        /// road, the trodden earth of a path, the water of the river. Split across its width, because on a 6 m height
        /// grid one wide quad would bridge a swell and hang in the air over it — the same reason
        /// <c>ElbrusAscent.CatLane</c> uses four strips across the snow-cat lane.
        ///
        /// It drapes and does not grade. A graded shelf would be the honest thing for a road, but a prefab cannot cut
        /// a hillside away, so a levelled cross-section on a forty-degree traverse either buries its uphill half in
        /// the ground or stands its downhill half on two metres of air. Draped, the road is always exactly on the
        /// ground, and the only steepness in it is the mountain's own — which is the same bargain
        /// <c>ElbrusAscent</c> made with the snow-cat lane. The crown is raised very slightly in the middle
        /// (<paramref name="sink"/>), which is what makes it read as a made surface rather than as paint.</summary>
        static float Ribbon(Batch batch, int sub, (float x, float z)[] line, float halfWidth, float lift, float tileM,
            int across = 3, float sink = 0f)
        {
            if (line.Length < 2) return 0f;
            float len = Len(line);
            float stepM = Mathf.Clamp(halfWidth * 1.6f, 2.5f, 7f);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / stepM) + 1);
            var prev = new Vector3[across + 1];
            var row = new Vector3[across + 1];
            void Row(float s, Vector3[] into)
            {
                var (cx, cz) = Elbrus.PointAt(line, s);
                var f = Heading(line, s);
                var r = new Vector2(f.y, -f.x);
                for (int i = 0; i <= across; i++)
                {
                    float o = -halfWidth + i * (halfWidth * 2f / across);
                    float x = cx + r.x * o, z = cz + r.y * o;
                    into[i] = new Vector3(x, Ground(x, z) + lift - sink * Mathf.Abs(o) / Mathf.Max(.01f, halfWidth), z);
                }
            }
            Row(0f, prev);
            float run = 0f, v = 0f;
            for (int k = 1; k < n; k++)
            {
                Row(len * k / (n - 1), row);
                var mb = batch.At(row[across / 2]);
                float dv = Vector3.Distance(row[across / 2], prev[across / 2]) / Mathf.Max(.5f, tileM);
                float v0 = v - Mathf.Floor(v);
                for (int i = 0; i < across; i++)
                {
                    float u0 = i / (float)across, u1 = (i + 1) / (float)across;
                    mb.Quad(sub, prev[i], row[i], row[i + 1], prev[i + 1],
                        new Vector2(u0, v0), new Vector2(u0, v0 + dv), new Vector2(u1, v0 + dv), new Vector2(u1, v0), false, Vector3.up);
                }
                v += dv;
                run += Vector3.Distance(row[across / 2], prev[across / 2]);
                var swap = prev; prev = row; row = swap;
            }
            return run;
        }

        static (float x, float z)[] lenLine;
        static float lenValue;
        /// <summary>Arc length of a polyline, remembered for the line asked about last. Everything below walks its
        /// route by arc length, so this is asked hundreds of thousands of times per build.</summary>
        static float Len((float x, float z)[] line)
        {
            if (!ReferenceEquals(line, lenLine)) { lenLine = line; lenValue = Elbrus.Length(line); }
            return lenValue;
        }

        /// <summary>Unit vector along a route at arc length <paramref name="s"/>.</summary>
        static Vector2 Heading((float x, float z)[] line, float s)
        {
            float len = Len(line);
            var a = Elbrus.PointAt(line, Mathf.Max(0f, s - 3f));
            var b = Elbrus.PointAt(line, Mathf.Min(len, s + 3f));
            var d = new Vector2(b.x - a.x, b.z - a.z);
            return d.sqrMagnitude < 1e-6f ? Vector2.up : d.normalized;
        }

        /// <summary>A point on a route: arc length, signed offset (positive to the right), height over the ground.</summary>
        static Vector3 On((float x, float z)[] line, float s, float off = 0f, float up = 0f)
        {
            var (x, z) = Elbrus.PointAt(line, s);
            var f = Heading(line, s);
            x += f.y * off; z -= f.x * off;
            return new Vector3(x, Ground(x, z) + up, z);
        }

        /// <summary>Compass bearing (degrees) of a route at arc length <paramref name="s"/>.</summary>
        static float Bearing((float x, float z)[] line, float s)
        {
            var f = Heading(line, s);
            return Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg;
        }

        // ── build ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Everything below Гара-Баши, baked against <paramref name="heights"/> into one prefab.</summary>
        public static void Build(HeightField heights)
        {
            dem = heights;
            meshCounter = 0;
            houses = sheds = shops = publicBuildings = obsBuildings = lamps = poles = guardPosts = cairns = signs = bridges = cars = 0;
            roadMetres = trailMetres = fenceMetres = wireMetres = gasMetres = fallDrop = plinthMax = 0f;
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(PrefabDir);

            var root = new GameObject("Elb_Valley");
            var t = root.transform;

            // the four lines of the third day, all of them surveyed on our own height field
            // …and the first hundred metres of each is dropped, because inside the village the street is the road and
            // two ribbons on the same ground would fight for the same pixels
            var toAzau = Cut(Survey(Terskol, Azau, .12f, 50f, 600f, 3f), 95f, 1e6f);
            var toObs = Cut(Survey(Terskol, Observatory, .13f, 50f, 500f, 9f), 95f, 1e6f);
            var toFalls = Cut(Survey(Terskol, Falls, .28f, 30f, 400f, 3f), 28f, 1e6f);
            var fallsToObs = Survey(Falls, Observatory, .26f, 25f, 300f, 3f);
            var river = Thalweg(VillageFromZ + 120f, VillageToZ - 160f, RiverXLo, RiverXHi, 12f);

            Roads(t, toAzau, toObs);
            Trails(t, toFalls, fallsToObs);
            Terskol1079(t, river, toAzau, toObs, toFalls);
            ObservatoryStation(t, toObs);
            DevichiKosy(t, toFalls, fallsToObs);

            AssetDatabase.DeleteAsset($"{PrefabDir}/Elb_Valley.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Elb_Valley.prefab");
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"1079 Эльбрус, низ карты: Терскол — {houses} жилых домов, {shops} лавок и кафе, "
                    + $"{publicBuildings} общественных зданий, {sheds} сараев; обсерватория — {obsBuildings} построек; "
                    + $"дорог {roadMetres / 1000f:0.0} км, троп {trailMetres / 1000f:0.0} км, {bridges} моста, "
                    + $"{cairns} туриков и меток, {signs} надписей, {lamps} фонарей, {poles} опор ЛЭП, "
                    + $"{guardPosts} сигнальных столбиков, {fenceMetres:0} м забора, {wireMetres:0} м проводов, "
                    + $"{gasMetres:0} м газовой трубы, {cars} машин, самый высокий цоколь {plinthMax:0.0} м. "
                    + $"Водопад «Девичьи Косы»: {fallDrop:0.0} м на отметке {Ground(Falls):0} м (DEM), "
                    + $"обсерватория {Ground(Observatory):0} м, посёлок {Ground(Terskol):0} м");
        }

        // ── 1. roads ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Two roads and nothing else is paved down here.
        ///
        /// <b>Терскол — Азау</b> is the last four kilometres of the А-158, the road that comes up the whole Baksan
        /// gorge and dies on the square in front of the ropeway. It is two narrow lanes of patched asphalt with a
        /// gravel shoulder, a line of concrete guard posts on the river side of every bend, and the yellow gas main
        /// running beside it on its little stilts all the way — the one thing you cannot photograph Prielbrusye
        /// without. Surveyed at 12 % maximum grade, which lets it keep to the valley floor.
        ///
        /// <b>Терскол — обсерватория</b> is the service road of the observatory: a single-lane dirt shelf that climbs
        /// eight hundred metres in something over twelve kilometres of switchbacks. It is the road the observatory's
        /// own truck uses and the thing walkers cut the bends of all the way up. Surveyed at 13 %, which is what puts
        /// the hairpins in. Above 2 900 m it is cut into scree and has a stone lip on the outside of the bends; at
        /// the top there is a barrier across it.</summary>
        static void Roads(Transform root, (float x, float z)[] toAzau, (float x, float z)[] toObs)
        {
            var group = new GameObject("Roads").transform; group.SetParent(root, false);
            var surface = new Batch("Road", 220f, Asphalt, Gravel, Verge);
            var kit = new Batch("RoadKit", 220f, Concrete, Steel, White, Red, GasYellow);

            // the valley road: 6 m of asphalt with a metre of gravel either side
            roadMetres += Ribbon(surface, 0, toAzau, 3.0f, .10f, 4f, 3, .07f);
            Ribbon(surface, 1, Shift(toAzau, 3.9f), 1.0f, .06f, 3f, 1);
            Ribbon(surface, 1, Shift(toAzau, -3.9f), 1.0f, .06f, 3f, 1);
            GuardPosts(kit, toAzau, 3.0f);
            GasMain(kit, toAzau, -5.6f);

            // the observatory road: 3.6 m of dirt, a scree lip on the downhill side above the tree line
            roadMetres += Ribbon(surface, 1, toObs, 1.8f, .08f, 3f, 3, .05f);
            Ribbon(surface, 2, Shift(toObs, 2.5f), .7f, .05f, 3f, 1);
            Ribbon(surface, 2, Shift(toObs, -2.5f), .7f, .05f, 3f, 1);
            RoadLip(kit, toObs);

            surface.Emit(group, shadows: false);
            kit.Emit(group);
        }

        /// <summary>Сигнальные столбики: the concrete posts with a red reflector that stand on the outside of every
        /// bend of a mountain road, where the drop is. They are put where the ground falls away, not every so many
        /// metres — which is why the road reads as a road even from above.</summary>
        static void GuardPosts(Batch kit, (float x, float z)[] line, float halfWidth)
        {
            float len = Elbrus.Length(line);
            for (float s = 14f; s < len; s += 22f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var foot = On(line, s, side * (halfWidth + 1.5f));
                    var inner = On(line, s, side * (halfWidth + 5.5f));
                    if (foot.y - inner.y < 1.2f) continue;              // no drop on this side: no post
                    var mb = kit.At(foot);
                    var rot = Quaternion.Euler(0, Bearing(line, s), 0);
                    mb.Box(0, foot + Vector3.up * .37f, new Vector3(.16f, .9f, .12f), rot, 1f);
                    mb.Box(2, foot + Vector3.up * .74f, new Vector3(.165f, .18f, .125f), rot, 1f);
                    mb.Box(3, foot + Vector3.up * .66f + rot * new Vector3(0, 0, -.065f), new Vector3(.08f, .1f, .01f), rot, 1f);
                    guardPosts++;
                }
            }
        }

        /// <summary>The gas main. In the Baksan valley it does not go underground: it runs on knee-high stilts beside
        /// the road, painted yellow, stepping up and over every gateway, for kilometres. Batched as one long line of
        /// tubes, which is the only way a thing like this can exist at all.</summary>
        static void GasMain(Batch kit, (float x, float z)[] line, float off)
        {
            float len = Elbrus.Length(line);
            const float Pitch = 9f, PipeY = .85f;
            var prev = Vector3.zero; bool first = true;
            for (float s = 0f; s <= len; s += Pitch)
            {
                var foot = On(line, s, off);
                var top = foot + Vector3.up * PipeY;
                var mb = kit.At(foot);
                // the stand: a bent steel leg on a little concrete pad
                mb.Box(0, foot + Vector3.up * .06f, new Vector3(.3f, .12f, .3f), Quaternion.identity, 1f);
                mb.Tube(1, foot + Vector3.up * .05f, top, .035f, .035f, 5, 1f, 0, false);
                if (!first)
                {
                    mb.Tube(4, prev, top, .09f, .09f, 7, 1f, 0, false);
                    gasMetres += Vector3.Distance(prev, top);
                }
                prev = top; first = false;
            }
        }

        /// <summary>Above the tree line the dirt road is not laid on the hill, it is cut into it: a shelf with the
        /// spoil pushed over the outside edge and a kerb of blocks on the bends, so the truck has something to see at
        /// night. Only on the downhill side, and only where the drop is real.</summary>
        static void RoadLip(Batch kit, (float x, float z)[] line)
        {
            float len = Elbrus.Length(line);
            var rng = new System.Random(3122);
            for (float s = 20f; s < len; s += 3.4f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var edge = On(line, s, side * 2.9f);
                    var below = On(line, s, side * 7f);
                    if (edge.y - below.y < 1.6f) continue;
                    var mb = kit.At(edge);
                    float r = .2f + (float)rng.NextDouble() * .22f;
                    var c = edge + Vector3.up * (r * .55f);
                    mb.Box(0, c, new Vector3(r * 2.1f, r * 1.3f, r * 1.7f),
                        Quaternion.Euler((float)rng.NextDouble() * 14f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 14f), 1f);
                }
            }
        }

        // ── 2. trails ─────────────────────────────────────────────────────────────────────────────────────
        /// <summary>The walking half of the third day: up from the village to the waterfall, and on up to the
        /// observatory. Both are surveyed at a walker's grade (28 % and 26 %), which is why they zigzag where the road
        /// sweeps, and both are built so that they can be FOLLOWED and not guessed at:
        /// <list type="bullet">
        /// <item>a trodden strip a metre and a bit wide — bare earth with the stones showing, a shade darker than the
        /// turf, laid on the ground at every station so it never bridges a swell;</item>
        /// <item>турики: cairns of three to five stones, every hundred metres or so, on the uphill side of the line
        /// where you look for them, and doubled at every bend sharper than a right angle;</item>
        /// <item>paint blazes on the boulders beside it, the red-and-white bar that means «this is the way»;</item>
        /// <item>wooden signposts with arrow boards at the two junctions and at the two destinations;</item>
        /// <item>stone steps beaten into the steepest pitches, because that is what a track does when a thousand
        /// people a summer walk it.</item>
        /// </list>
        /// Not one flake of snow anywhere on either of them: the top of this is 3 090 m and the map's firn line starts
        /// at 3 250 m.</summary>
        static void Trails(Transform root, (float x, float z)[] toFalls, (float x, float z)[] fallsToObs)
        {
            var group = new GameObject("Trails").transform; group.SetParent(root, false);
            var tread = new Batch("Trail", 200f, Trodden, Verge);
            var kit = new Batch("TrailKit", 200f, Rock, White, Red, Plank, Steel);

            trailMetres += Ribbon(tread, 0, toFalls, .58f, .05f, 1.6f, 2);
            trailMetres += Ribbon(tread, 0, fallsToObs, .52f, .05f, 1.6f, 2);
            // the edge of a path is not a line: the turf is worn thin for a stride either side of it
            Ribbon(tread, 1, Shift(toFalls, 1.1f), .45f, .03f, 2f, 1);
            Ribbon(tread, 1, Shift(toFalls, -1.1f), .45f, .03f, 2f, 1);
            Ribbon(tread, 1, Shift(fallsToObs, 1.0f), .4f, .03f, 2f, 1);
            Ribbon(tread, 1, Shift(fallsToObs, -1.0f), .4f, .03f, 2f, 1);

            Waymarks(kit, toFalls, 4101, 105f);
            Waymarks(kit, fallsToObs, 4102, 70f);
            Steps(kit, toFalls, 4111);
            Steps(kit, fallsToObs, 4112);

            // the four boards that say where this goes. Real signs in Prielbrusye are a plank on a post with the name
            // burnt into it and the walking time under it.
            float lenF = Elbrus.Length(toFalls), lenO = Elbrus.Length(fallsToObs);
            Signpost(kit, group, On(toFalls, 22f, 2.6f), Bearing(toFalls, 22f) + 90f,
                "ВОДОПАД ДЕВИЧЬИ КОСЫ 5,2 км · 2 ч\nОБСЕРВАТОРИЯ ПИК ТЕРСКОЛ 5,6 км · 2 ч 30\nвниз: ТЕРСКОЛ");
            Signpost(kit, group, On(toFalls, lenF * .5f, 2.2f), Bearing(toFalls, lenF * .5f) + 90f,
                "ТРОПА НА ВОДОПАД\nне сходить с тропы\nобратно затемно не идти");
            Signpost(kit, group, On(toFalls, lenF - 26f, 2.4f), Bearing(toFalls, lenF - 26f) + 90f,
                "ДЕВИЧЬИ КОСЫ 2 800 м\nвысота падения 25 м\nдальше вверх: ОБСЕРВАТОРИЯ 40 мин");
            Signpost(kit, group, On(fallsToObs, lenO - 30f, 2.4f), Bearing(fallsToObs, lenO - 30f) + 90f,
                "ОБСЕРВАТОРИЯ «ПИК ТЕРСКОЛ»\n3 127 м · режимный объект\nбез сопровождения не входить");

            tread.Emit(group, shadows: false);
            kit.Emit(group);
        }

        /// <summary>Cairns and paint blazes along one line. A cairn goes on the uphill side, because that is where the
        /// eye goes when the track fades; a bend gets one on both sides; and every third cairn has a painted stone
        /// beside it instead, which is what the marking of this route actually looks like.</summary>
        static void Waymarks(Batch kit, (float x, float z)[] line, int seed, float pitch)
        {
            float len = Elbrus.Length(line);
            var rng = new System.Random(seed);
            int i = 0;
            for (float s = 30f; s < len - 10f; s += pitch * (.75f + (float)rng.NextDouble() * .5f), i++)
            {
                // which side is uphill: put the mark where a walker looks for it
                var left = On(line, s, -3.5f);
                var right = On(line, s, 3.5f);
                float side = left.y > right.y ? -1f : 1f;
                var at = On(line, s, side * 1.9f, -.05f);
                if (i % 3 == 2) Blaze(kit, at, Bearing(line, s), rng);
                else Cairn(kit, 0, at, .34f + (float)rng.NextDouble() * .26f, rng);
                // a bend sharper than a right angle gets a second one, on the other side, so it cannot be walked past
                float turn = Mathf.DeltaAngle(Bearing(line, Mathf.Max(0f, s - 14f)), Bearing(line, Mathf.Min(len, s + 14f)));
                if (Mathf.Abs(turn) > 80f) Cairn(kit, 0, On(line, s, -side * 1.9f, -.05f), .4f, rng);
            }
        }

        /// <summary>Турик: three to five stones stacked, biggest at the bottom, each one turned any old way, because
        /// somebody put it there with one hand while holding a pole in the other.</summary>
        static void Cairn(Batch kit, int sub, Vector3 foot, float r, System.Random rng)
        {
            var mb = kit.At(foot);
            int n = 3 + rng.Next(3);
            float y = foot.y - .04f;
            for (int i = 0; i < n; i++)
            {
                float k = r * Mathf.Lerp(1f, .42f, i / (float)Mathf.Max(1, n - 1));
                float th = k * (.5f + (float)rng.NextDouble() * .3f);
                var c = new Vector3(foot.x + ((float)rng.NextDouble() - .5f) * r * .5f, y + th * .5f,
                                    foot.z + ((float)rng.NextDouble() - .5f) * r * .5f);
                mb.Box(sub, c, new Vector3(k * 2f, th, k * 1.6f),
                    Quaternion.Euler((float)rng.NextDouble() * 12f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 12f), 1.4f);
                y += th * .88f;
            }
            cairns++;
        }

        /// <summary>A red-and-white bar painted on a boulder: the mark this route is actually waymarked with. The
        /// stone is real geometry, the bar is two quads a finger's breadth above its face.</summary>
        static void Blaze(Batch kit, Vector3 foot, float bearing, System.Random rng)
        {
            var mb = kit.At(foot);
            float r = .45f + (float)rng.NextDouble() * .35f;
            var rot = Quaternion.Euler(0, bearing + (float)rng.NextDouble() * 30f - 15f, 0);
            mb.Box(0, foot + Vector3.up * (r * .42f), new Vector3(r * 2f, r * .95f, r * 1.5f), rot, 1.4f);
            var face = foot + Vector3.up * (r * .55f) + rot * new Vector3(0, 0, r * .76f);
            var look = rot * Quaternion.Euler(0, 0, 0);
            Card(mb, 1, face + rot * new Vector3(0, .09f, .01f), look, r * .9f, .1f);
            Card(mb, 2, face + rot * new Vector3(0, -.01f, .01f), look, r * .9f, .1f);
            Card(mb, 1, face + rot * new Vector3(0, -.11f, .01f), look, r * .9f, .1f);
            cairns++;
        }

        /// <summary>Stone steps on the steep pitches: flat slabs beaten in across the line wherever the track is
        /// steeper than about eighteen degrees. It is what stops the whole pitch from turning into a gully, and it is
        /// the thing that tells a walker in the dark that he is still on the track.</summary>
        static void Steps(Batch kit, (float x, float z)[] line, int seed)
        {
            float len = Elbrus.Length(line);
            var rng = new System.Random(seed);
            for (float s = 6f; s < len - 6f; s += 1.4f)
            {
                float rise = On(line, s + 3f).y - On(line, s - 3f).y;
                if (Mathf.Abs(rise) / 6f < .33f) continue;              // 18°
                var at = On(line, s, ((float)rng.NextDouble() - .5f) * .5f, -.03f);
                var mb = kit.At(at);
                float w = .75f + (float)rng.NextDouble() * .5f;
                mb.Box(0, at + Vector3.up * .05f, new Vector3(w, .13f, .42f),
                    Quaternion.Euler(0, Bearing(line, s) + (float)rng.NextDouble() * 16f - 8f, 0), 1.6f);
            }
        }

        /// <summary>A signpost: a squared post, one or two arrow boards nailed to it, and the text burnt into them.
        /// The board faces the reader; <see cref="Height1079.Runtime.SignText"/> keeps the letters from shining
        /// through the plank.</summary>
        static void Signpost(Batch kit, Transform group, Vector3 foot, float yaw, string text)
        {
            var mb = kit.At(foot);
            var rot = Quaternion.Euler(0, yaw, 0);
            float h = 1.95f;
            mb.Box(3, foot + Vector3.up * (h * .5f - .25f), new Vector3(.11f, h, .11f), rot, 1f);
            var board = foot + Vector3.up * (h - .5f);
            mb.Box(3, board, new Vector3(1.5f, .58f, .05f), rot, 1f);
            mb.Box(4, board + rot * new Vector3(0, .33f, 0), new Vector3(1.54f, .04f, .07f), rot, 1f);
            Label(group, text, board + rot * new Vector3(0, 0, -.045f), yaw + 180f, .052f, new Color(.12f, .1f, .08f));
        }

        // ── 3. the village: what a building here is made of ───────────────────────────────────────────────
        /// <summary>The batches a village is built into. Seven of them, split by material family rather than by
        /// building, so fifty-eight buildings come out as a couple of dozen renderers.</summary>
        sealed class Kit
        {
            public Batch Shell, Roof, Wood, Glass, Metal, Soft, Flow;
            public Kit(float span)
            {
                Shell = new Batch("Wall", span, Plaster, PlasterCold, PlasterBlue, Brick, Stone, Concrete);
                Roof = new Batch("Roof", span, Sheet, SheetGreen, SheetRed, SheetBlue, Slate, Rust);
                Wood = new Batch("Wood", span, Plank, PlankGrey, Log, Tarp);
                Glass = new Batch("Glass", span, WindowDark, WindowLit);
                Metal = new Batch("Kit", span, Steel, Alu, White, Red, GasYellow, ConcreteDark, RedLamp);
                Soft = new Batch("Soft", span, Cloth1, Cloth2, Turf, Green);
                Flow = new Batch("River", span, Water, Foam, Rock, RockWet);
            }
            public void Emit(Transform t)
            {
                Shell.Emit(t, collide: true);
                Roof.Emit(t, collide: true);
                Wood.Emit(t);
                Glass.Emit(t);
                Metal.Emit(t);
                Soft.Emit(t, shadows: false);
                Flow.Emit(t, shadows: false);
            }
        }

        /// <summary>Roof shapes the valley builds in: a pitched roof of corrugated sheet or шифер, a flat concrete
        /// slab over a shop, and the lean-to of a shed.</summary>
        enum Roofs { Gable, Flat, Lean }

        /// <summary>One building, put together the way they are actually put together here: a rubble-and-concrete
        /// plinth that takes up the fall of the ground, plastered or block walls, a window grid with real openings
        /// in it, a pitched sheet roof with eaves that overhang, a chimney, and — because nothing in this village is
        /// finished — a balcony of welded angle on the valley side and a satellite dish on the gable end.
        ///
        /// <paramref name="lit"/> is the share of windows with the light on. It is the only reason to come down here
        /// at night: from the top of the gorge Terskol is a handful of yellow rectangles and a dozen street lamps, and
        /// there is nothing else lit on this whole map.</summary>
        static void Building(Kit kit, Vector3 foot, float yaw, float w, float d, int floors, float floorH,
            int wallSub, int roofSub, Roofs roof, int seed, bool balcony, bool dish, float lit, bool porch = false)
        {
            var rng = new System.Random(seed);
            var rot = Quaternion.Euler(0, yaw, 0);
            var shell = kit.Shell.At(foot); var rf = kit.Roof.At(foot);
            var wd = kit.Wood.At(foot); var gl = kit.Glass.At(foot); var mt = kit.Metal.At(foot);

            var (lo, hi) = Pad(foot.x, foot.z, yaw, new Vector2(w, d));
            float y0 = foot.y;                                   // floor level, already decided by Seat()
            Vector3 L(float u, float v, float y) => new Vector3(foot.x, 0, foot.z) + rot * new Vector3(u, 0, v) + Vector3.up * y;

            // the plinth: rubble stone, reaching from the floor down to the lowest ground under the footprint — which
            // for a terraced building (see Seat) is a good deal less than the whole step. Under a thirty-metre block
            // on the side of a gorge that is five or six metres of retaining wall, and that is exactly what is under
            // the real ones.
            float plinth = Mathf.Clamp(y0 - lo + .35f, .35f, 9f);
            shell.Box(4, L(0, 0, y0 - plinth * .5f + .1f), new Vector3(w + .35f, plinth, d + .35f), rot, .9f);

            float wallTop = y0 + floors * floorH;
            shell.Box(wallSub, L(0, 0, (y0 + wallTop) * .5f), new Vector3(w, floors * floorH, d), rot, .55f);

            // windows: a grid of real openings, two thirds of a metre in from the corners
            int nw = Mathf.Max(2, Mathf.FloorToInt(w / 2.5f));
            int nd = Mathf.Max(1, Mathf.FloorToInt(d / 2.9f));
            for (int f = 0; f < floors; f++)
            {
                float sill = y0 + f * floorH + .95f;
                for (int i = 0; i < nw; i++)
                {
                    float u = Mathf.Lerp(-w * .5f + 1.1f, w * .5f - 1.1f, nw == 1 ? .5f : i / (float)(nw - 1));
                    Window(shell, gl, rot, L(u, d * .5f, sill + .62f), 1.15f, 1.35f, 0f, wallSub, rng, lit);
                    Window(shell, gl, rot, L(u, -d * .5f, sill + .62f), 1.15f, 1.35f, 180f, wallSub, rng, lit * .7f);
                }
                for (int i = 0; i < nd; i++)
                {
                    float v = Mathf.Lerp(-d * .5f + 1.2f, d * .5f - 1.2f, nd == 1 ? .5f : i / (float)(nd - 1));
                    Window(shell, gl, rot, L(w * .5f, v, sill + .62f), 1.0f, 1.3f, 90f, wallSub, rng, lit * .6f);
                    Window(shell, gl, rot, L(-w * .5f, v, sill + .62f), 1.0f, 1.3f, 270f, wallSub, rng, lit * .6f);
                }
            }

            // the door, in the middle of the front wall, with a step up to it
            wd.Box(0, L(0, d * .5f + .04f, y0 + 1.05f), new Vector3(1.0f, 2.1f, .1f), rot, 1f);
            shell.Box(5, L(0, d * .5f + .5f, y0 - .12f), new Vector3(1.5f, .26f, 1.1f), rot, 1f);
            if (porch)
            {
                for (int k = -1; k <= 1; k += 2)
                    mt.Tube(0, L(k * .85f, d * .5f + 1.05f, y0), L(k * .85f, d * .5f + 1.05f, y0 + 2.5f), .04f, .04f, 5, 1f, 0, false);
                rf.Box(roofSub, L(0, d * .5f + .7f, y0 + 2.55f), new Vector3(2.4f, .07f, 1.6f), rot * Quaternion.Euler(-8f, 0, 0), 1f);
            }

            switch (roof)
            {
                case Roofs.Gable:
                {
                    float rise = Mathf.Clamp(d * .28f, .9f, 2.4f);
                    float over = .55f;
                    Gable(rf, roofSub, rot, foot, w, d, wallTop, rise, over);
                    // the gable ends, plastered like the walls
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var a = L(-w * .5f, k * d * .5f, wallTop);
                        var b = L(w * .5f, k * d * .5f, wallTop);
                        var c = L(0, k * d * .5f, wallTop + rise);
                        shell.Quad(wallSub, a, b, c, c, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                    }
                    // chimney: a brick stack on the leeward slope, and a tin cap on it
                    var ch = L(w * .5f - 1.2f, -d * .18f, wallTop + rise * .55f + .7f);
                    shell.Box(3, ch, new Vector3(.55f, 1.6f, .55f), rot, 1f);
                    rf.Box(0, ch + Vector3.up * .88f, new Vector3(.75f, .06f, .75f), rot, 1f);
                    break;
                }
                case Roofs.Flat:
                    rf.Box(roofSub, L(0, 0, wallTop + .12f), new Vector3(w + .5f, .22f, d + .5f), rot, 1f);
                    // the parapet, which every flat roof here has and which is always a bit crooked
                    for (int k = -1; k <= 1; k += 2)
                    {
                        rf.Box(roofSub, L(0, k * (d * .5f + .2f), wallTop + .5f), new Vector3(w + .5f, .5f, .1f), rot, 1f);
                        rf.Box(roofSub, L(k * (w * .5f + .2f), 0, wallTop + .5f), new Vector3(.1f, .5f, d + .5f), rot, 1f);
                    }
                    break;
                case Roofs.Lean:
                {
                    float rise = d * .22f;
                    var slope = rot * Quaternion.Euler(-Mathf.Atan2(rise, d) * Mathf.Rad2Deg, 0, 0);
                    rf.Box(roofSub, L(0, 0, wallTop + rise * .5f + .08f), new Vector3(w + .5f, .06f, Mathf.Sqrt(d * d + rise * rise) + .5f), slope, 1f);
                    shell.Quad(wallSub, L(-w * .5f, -d * .5f, wallTop), L(w * .5f, -d * .5f, wallTop),
                        L(w * .5f, -d * .5f, wallTop + rise), L(-w * .5f, -d * .5f, wallTop + rise),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                    break;
                }
            }

            // the balcony, always on the side with the view, always welded out of angle and never quite level
            if (balcony && floors > 1)
            {
                for (int f = 1; f < floors; f++)
                {
                    float by = y0 + f * floorH + .1f;
                    mt.Box(0, L(0, d * .5f + .8f, by), new Vector3(w * .62f, .09f, 1.6f), rot, 1f);
                    for (int k = -1; k <= 1; k += 2)
                        mt.Tube(0, L(k * w * .3f, d * .5f + 1.55f, by), L(k * w * .3f, d * .5f + 1.55f, by + 1.0f), .03f, .03f, 4, 1f, 0, false);
                    mt.Box(0, L(0, d * .5f + 1.55f, by + 1.0f), new Vector3(w * .62f, .05f, .05f), rot, 1f);
                    // the sheet the balcony is boarded up with, which is what they all look like
                    if (rng.NextDouble() < .6)
                        rf.Box(1, L(0, d * .5f + 1.55f, by + .5f), new Vector3(w * .62f, 1.0f, .03f), rot, 1f);
                }
            }

            // спутниковая тарелка on the gable end, pointed south at a satellite over the equator
            if (dish)
            {
                var at = L(w * .5f + .35f, d * .22f, wallTop - .8f);
                mt.Tube(0, at, at + rot * new Vector3(-.35f, 0, 0), .025f, .025f, 4, 1f, 0, false);
                DishBowl(mt, 2, at + Vector3.up * .1f, 30f, .32f);
            }
        }

        /// <summary>A window: a rebate cut into the plaster, a frame, and the pane. Lit or dark, decided once and for
        /// good, so a village seen from the top of the gorge has the same windows burning every time.</summary>
        static void Window(MeshBuilder shell, MeshBuilder gl, Quaternion rot, Vector3 centre, float w, float h,
            float turn, int wallSub, System.Random rng, float lit)
        {
            var r = rot * Quaternion.Euler(0, turn, 0);
            Card(gl, rng.NextDouble() < lit ? 1 : 0, centre + r * new Vector3(0, 0, .012f), r, w, h);
            // the sill and the lintel, standing proud of the plaster. Four boxes per window would be truer and there
            // are well over a thousand windows down here, so it is these two and the two frame bars — which between
            // them are what stops a window being a sticker on a wall.
            shell.Box(wallSub, centre + r * new Vector3(0, -h * .5f - .07f, .05f), new Vector3(w + .26f, .1f, .16f), r, 1f);
            shell.Box(wallSub, centre + r * new Vector3(0, h * .5f + .07f, .04f), new Vector3(w + .22f, .1f, .1f), r, 1f);
            shell.Box(wallSub, centre + r * new Vector3(0, 0, .03f), new Vector3(.05f, h, .04f), r, 1f);
            shell.Box(wallSub, centre + r * new Vector3(0, 0, .03f), new Vector3(w, .05f, .04f), r, 1f);
        }

        /// <summary>A pitched roof: two slopes with an overhang, and the fascia board under the eaves.</summary>
        static void Gable(MeshBuilder rf, int sub, Quaternion rot, Vector3 foot, float w, float d, float wallTop,
            float rise, float over)
        {
            Vector3 L(float u, float v, float y) => new Vector3(foot.x, 0, foot.z) + rot * new Vector3(u, 0, v) + Vector3.up * y;
            float hw = w * .5f + over, hd = d * .5f + over;
            var ridgeA = L(0, -hd, wallTop + rise);
            var ridgeB = L(0, hd, wallTop + rise);
            for (int k = -1; k <= 1; k += 2)
            {
                var a = L(k * hw, -hd, wallTop);
                var b = L(k * hw, hd, wallTop);
                rf.Quad(sub, a, b, ridgeB, ridgeA, Vector2.zero, new Vector2(0, d * .5f), new Vector2(w * .3f, d * .5f), new Vector2(w * .3f, 0), true);
                // the fascia, the one board that makes an eave read as an eave
                rf.Quad(sub, a, b, b + Vector3.down * .14f, a + Vector3.down * .14f,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            }
        }

        /// <summary>A parabolic dish, pointed at a bearing of <paramref name="bearing"/> and about 25° up.</summary>
        static void DishBowl(MeshBuilder mb, int sub, Vector3 centre, float bearing, float r)
        {
            var rot = Quaternion.Euler(-25f, bearing, 0);
            const int Rings = 3, Sect = 10;
            Vector3 P(int i, int j)
            {
                float t = i / (float)Rings, ang = j / (float)Sect * Mathf.PI * 2;
                float rr = r * t;
                return centre + rot * new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, -rr * rr / (r * 2.4f));
            }
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Sect; j++)
                    mb.Quad(sub, P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            mb.Tube(sub, centre, centre + rot * new Vector3(0, 0, -r * .75f), .02f, .02f, 4, 1f, 0, false);
        }

        // ── 4. посёлок Терскол ────────────────────────────────────────────────────────────────────────────
        /// <summary>Терскол: about a thousand people living in six hundred metres of gorge at 2 200–2 270 m, and the
        /// only place on this map with mains electricity, a shop and a bus.
        ///
        /// It is laid out the way the real one is laid out, which is the way the gorge allows and no other: the river
        /// down the bottom, one street on the terrace above its left bank, a second lane above that where the частный
        /// сектор is, and on the right bank the base — half the village, in the real place, belongs to the ЦСКА
        /// mountaineering school, founded here in 1935, and to the Ministry of Defence sanatorium. Everything in this
        /// method is hung off <paramref name="river"/>, which is not drawn by hand either: it is the line of lowest
        /// ground read straight out of the height field (<see cref="Thalweg"/>).
        ///
        /// What makes it Terskol and not a village in general, from the photographs and the accounts in
        /// docs/ELBRUS.md: two-and three-storey houses of block and plaster under corrugated sheet and шифер, balconies
        /// of welded angle boarded up with sheet, a satellite dish on every second gable; long two-storey Soviet
        /// barracks of the base with the twin-headed Elbrus cut into the end wall; the highest five-storey block in
        /// Russia at the entrance; the yellow gas main on its stilts along every street; the mosque, which is where
        /// the path up to the waterfall starts; a white obelisk to the men who died here in 1943; benches made out of
        /// BelAZ tyres on the square; half a kilometre of cow sheds at the bottom end, left over from when this was an
        /// aul; a market row, a bus stop, and a helipad on the flattest ground anybody could find.</summary>
        static void Terskol1079(Transform root, (float x, float z)[] river, (float x, float z)[] toAzau,
            (float x, float z)[] toObs, (float x, float z)[] toFalls)
        {
            var group = new GameObject("Terskol").transform; group.SetParent(root, false);
            var kit = new Kit(180f);
            float len = Elbrus.Length(river);
            float s0 = 120f, s1 = Mathf.Min(len - 60f, 730f);

            var street = Shift(river, StreetOff);
            var lane = Shift(river, LaneOff);
            var west = Shift(river, WestOff);

            Baksan(kit, river);
            // the three ways through the village: the street is the through road and is metalled, the upper lane and
            // the road on the right bank are gravel
            Streets(group, river, street, lane, west, s0, s1);

            // ── row A: the shops, between the street and the water ───────────────────────────────────────
            string[] shopNames = { "ПРОДУКТЫ", "КАФЕ «КУПОЛ»", "ПРОКАТ · РЕМОНТ", "ХЫЧИНЫ · ЧАЙ", "АПТЕКА", "СУВЕНИРЫ · ШЕРСТЬ" };
            int shopI = 0;
            for (int i = 0; i < 16; i++)
            {
                float s = 150f + i * 36f;
                if (s > s1) break;
                float yaw = Bearing(river, s) - 90f;
                if (i == 0) { BusStop(kit, group, river, s, yaw); continue; }
                if (i == 5) { Market(kit, group, river, s, yaw); continue; }
                if ((i % 3 == 1 || i == 11) && shopI < shopNames.Length)
                {
                    Shop(kit, group, river, s, yaw, shopNames[shopI], shopI == 1 || shopI == 3, 6100 + i);
                    shopI++;
                    continue;
                }
                Plot(kit, river, s, -20f, yaw, 10f, 6.5f, 2, 3.1f, i % 2, 1 + i % 3, Roofs.Gable, 6200 + i, true, i % 2 == 0, .45f, true, 3f);
                houses++;
            }

            // ── row B: the street's own houses ───────────────────────────────────────────────────────────
            for (int i = 0; i < 19; i++)
            {
                float s = 140f + i * 31f;
                if (s > s1) break;
                float yaw = Bearing(river, s) + 90f;
                bool guest = i % 4 == 2;
                Plot(kit, river, s, -54f, yaw, guest ? 12f : 9.5f, guest ? 9f : 7.5f, guest ? 3 : 2, 3.05f,
                    i % 3, i % 5, Roofs.Gable, 6300 + i, true, i % 2 == 1, guest ? .55f : .4f, guest, 4f);
                houses++;
                if (i % 3 != 0) Yard(kit, river, s, -42f, yaw, 6400 + i);
            }

            // ── row C: the частный сектор on the lane above ──────────────────────────────────────────────
            for (int i = 0; i < 15; i++)
            {
                float s = 185f + i * 33f;
                if (s > s1) break;
                float yaw = Bearing(river, s) + 90f;
                if (i % 5 == 4) { Shed(kit, river, s, -86f, yaw, 6500 + i); sheds++; continue; }
                Plot(kit, river, s, -86f, yaw, 9f, 7f, 2, 3.0f, (i + 1) % 3, (i + 2) % 5, Roofs.Gable, 6500 + i,
                    i % 2 == 0, i % 3 == 0, .3f, false, 4f);
                houses++;
                Yard(kit, river, s, -76f, yaw, 6600 + i);
            }

            // the cow sheds at the bottom end of the village, on the right bank: half a kilometre of them in the real
            // place, left over from when this was an aul and the houses were down here too
            for (int i = 0; i < 9; i++)
            {
                float s = s1 - 115f + i * 13f;
                float yaw = Bearing(river, s) - 90f;
                Shed(kit, river, s, 44f + (i % 2) * 12f, yaw, 6700 + i);
                sheds++;
            }

            // ── the right bank: the base, the block and the mosque ───────────────────────────────────────
            Base(kit, group, river, west, s0, s1);

            // the junction board at the top of the village, where the through road, the observatory road and the path
            // up the gorge all leave: the one sign that tells a visitor the whole of the third day
            {
                var at = On(river, 158f, -27f);
                var rot = Quaternion.Euler(0, Bearing(river, 158f) + 90f, 0);
                var mb = kit.Metal.At(at);
                for (int k = -1; k <= 1; k += 2)
                    mb.Tube(0, at + rot * new Vector3(k * 1.5f, 0, 0), at + rot * new Vector3(k * 1.5f, 3.1f, 0), .06f, .055f, 6, 1f, 0, true);
                var board = at + Vector3.up * 2.4f;
                mb.Box(2, board, new Vector3(3.4f, 1.3f, .06f), rot, 1f);
                Label(group, $"АЗАУ {Elbrus.Length(toAzau) / 1000f + .1f:0.0} км · канатная дорога\n"
                           + $"ОБСЕРВАТОРИЯ {Elbrus.Length(toObs) / 1000f + .1f:0.0} км · серпантин\n"
                           + $"ВОДОПАД ДЕВИЧЬИ КОСЫ {Elbrus.Length(toFalls) / 1000f:0.0} км · тропа\n"
                           + "НАЛЬЧИК 133 км",
                    board + rot * new Vector3(0, 0, -.04f), Bearing(river, 158f) + 270f, .095f, new Color(.14f, .13f, .12f));
                publicBuildings++;
            }

            Power(kit, river, s0, s1);
            GasBranch(kit, street, s0, s1);
            StreetLamps(kit, group, street, s0, s1);
            Vehicles(kit, river, street, s0, s1);
            Helipad(kit, group, river, s1 + 55f);

            kit.Emit(group);
        }

        /// <summary>Puts one building on its plot. The gorge floor is thirteen to thirty-seven degrees depending on
        /// where you stand, and a nine-metre footprint on thirty-five degrees is a six-metre step — so the plot is
        /// hunted for: the site is nudged up to <paramref name="reach"/> metres across the street and seven metres
        /// along it, and the flattest of the fifty-odd footprints wins. Which is exactly what a man with a tape does
        /// before he digs.</summary>
        static void Plot(Kit kit, (float x, float z)[] river, float s, float off, float yaw, float w, float d,
            int floors, float floorH, int wallSub, int roofSub, Roofs roof, int seed, bool balcony, bool dish, float lit,
            bool porch, float reach = 5f)
        {
            var size = new Vector2(w, d);
            float bestOff = off, bestS = s, bestDrop = float.MaxValue;
            for (float a = -7f; a <= 7.01f; a += 3.5f)
                for (float k = -reach; k <= reach + .01f; k += 1.5f)
                {
                    var q = On(river, s + a, off + k);
                    var (lo, hi) = Pad(q.x, q.z, yaw, size);
                    if (hi - lo >= bestDrop) continue;
                    bestDrop = hi - lo; bestOff = off + k; bestS = s + a;
                }
            var p = On(river, bestS, bestOff);
            float y = Seat(p.x, p.z, yaw, size);
            if (y - Pad(p.x, p.z, yaw, size).lo > plinthMax) plinthMax = y - Pad(p.x, p.z, yaw, size).lo;
            Building(kit, new Vector3(p.x, y, p.z), yaw, w, d, floors, floorH, wallSub, roofSub, roof, seed, balcony, dish, lit, porch);
        }

        /// <summary>The Baksan through the village. Our height field cannot hold a channel — it fills a gorge this
        /// narrow in — so the river is built the way the crevasses on the glacier are built, upward: the water lies on
        /// the line of lowest ground with a bank of boulders heaped along each side of it, and from the bridge the eye
        /// reads a river bed. It is white water almost the whole way: it falls a hundred and twenty metres in the six
        /// hundred the village is long.</summary>
        static void Baksan(Kit kit, (float x, float z)[] river)
        {
            float len = Elbrus.Length(river);
            Ribbon(kit.Flow, 0, river, 3.4f, .12f, 6f, 3);
            var rng = new System.Random(5001);
            for (float s = 0f; s < len; s += 2.6f)
            {
                // the banks: boulders heaped along both edges, biggest where the water runs fastest
                float grade = Mathf.Abs(On(river, s + 6f).y - On(river, s - 6f).y) / 12f;
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = On(river, s + (float)rng.NextDouble() * 2f, side * (3.4f + (float)rng.NextDouble() * 2.2f), -.1f);
                    var mb = kit.Flow.At(at);
                    float r = .3f + (float)rng.NextDouble() * (.5f + grade * 3f);
                    mb.Box(2, at + Vector3.up * (r * .45f), new Vector3(r * 2f, r * 1.1f, r * 1.7f),
                        Quaternion.Euler((float)rng.NextDouble() * 16f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 16f), 1.3f);
                }
                // the wet stones in the water itself, and the foam breaking over them
                if (grade > .07f)
                {
                    var at = On(river, s, ((float)rng.NextDouble() - .5f) * 4f, .05f);
                    var mb = kit.Flow.At(at);
                    float r = .22f + (float)rng.NextDouble() * .3f;
                    mb.Box(3, at + Vector3.up * (r * .4f), new Vector3(r * 1.8f, r, r * 1.5f),
                        Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), 1.3f);
                    mb.Quad(1, at + new Vector3(-1.1f, .16f, -.5f), at + new Vector3(1.1f, .16f, -.5f),
                        at + new Vector3(1.1f, .2f, 1.4f), at + new Vector3(-1.1f, .2f, 1.4f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                }
            }
            // three crossings: the road bridge at the top of the village, and two footbridges
            Bridge(kit, river, len * Crossings[0], 14f, 8.5f, true, 5101);
            Bridge(kit, river, len * Crossings[1], 12f, 2.4f, false, 5102);
            Bridge(kit, river, len * Crossings[2], 12f, 2.2f, false, 5103);
        }

        /// <summary>A bridge: two abutments of blockwork standing where the banks are, a deck between them, and a
        /// railing. The road bridge is concrete with a steel parapet and takes the А-158 over the water; the two
        /// footbridges are a pair of larch stringers with planks across them and a pipe handrail, which is what they
        /// are here.</summary>
        static void Bridge(Kit kit, (float x, float z)[] river, float s, float halfSpan, float width, bool road, int seed)
        {
            var mid = On(river, s);
            var f = Heading(river, s);
            var across = new Vector3(f.y, 0, -f.x);
            var a = mid + across * halfSpan; a.y = Ground(a.x, a.z);
            var b = mid - across * halfSpan; b.y = Ground(b.x, b.z);
            float deckY = Mathf.Max(a.y, b.y) + (road ? 1.1f : .9f);
            var shell = kit.Shell.At(mid); var wd = kit.Wood.At(mid); var mt = kit.Metal.At(mid);
            var rot = Quaternion.LookRotation(across, Vector3.up);

            // the abutments, each one buried well into its bank
            foreach (var end in new[] { a, b })
                shell.Box(road ? 5 : 4, new Vector3(end.x, (end.y + deckY) * .5f - .4f, end.z),
                    new Vector3(width + 1.2f, deckY - end.y + 1.2f, 3.4f), rot, 1f);

            var deckMid = new Vector3(mid.x, deckY, mid.z);
            if (road)
            {
                shell.Box(5, deckMid, new Vector3(width, .5f, halfSpan * 2f + 1.5f), rot, 1f);
                for (int k = -1; k <= 1; k += 2)
                {
                    var rail = deckMid + rot * new Vector3(k * width * .5f, .75f, 0);
                    mt.Box(0, rail, new Vector3(.12f, .12f, halfSpan * 2f), rot, 1f);
                    for (float t = -halfSpan; t <= halfSpan; t += 2.2f)
                        mt.Tube(0, deckMid + rot * new Vector3(k * width * .5f, .25f, t),
                            deckMid + rot * new Vector3(k * width * .5f, .82f, t), .045f, .045f, 5, 1f, 0, false);
                }
            }
            else
            {
                for (int k = -1; k <= 1; k += 2)
                    wd.Box(2, deckMid + rot * new Vector3(k * width * .4f, -.16f, 0), new Vector3(.22f, .3f, halfSpan * 2f), rot, 1f);
                var rng = new System.Random(seed);
                for (float t = -halfSpan; t <= halfSpan; t += .34f)
                    wd.Box(0, deckMid + rot * new Vector3(0, .01f + (float)rng.NextDouble() * .015f, t),
                        new Vector3(width, .05f, .3f), rot, 1f);
                for (int k = -1; k <= 1; k += 2)
                {
                    var side = deckMid + rot * new Vector3(k * width * .5f, 0, 0);
                    mt.Tube(0, side + Vector3.up * .95f + rot * new Vector3(0, 0, -halfSpan),
                        side + Vector3.up * .95f + rot * new Vector3(0, 0, halfSpan), .03f, .03f, 5, 1f, 0, false);
                    for (float t = -halfSpan; t <= halfSpan; t += 1.8f)
                        mt.Tube(0, side + rot * new Vector3(0, 0, t), side + rot * new Vector3(0, .95f, t), .028f, .028f, 5, 1f, 0, false);
                }
            }
            bridges++;
        }

        /// <summary>The three ways through the village, on their own batch because a road is not made of anything a
        /// house is made of: the street is patched asphalt, the upper lane and the road on the right bank are gravel,
        /// and all three are laid on the ground at every station so none of them floats over a swell.</summary>
        static void Streets(Transform group, (float x, float z)[] river, (float x, float z)[] street,
            (float x, float z)[] lane, (float x, float z)[] west, float s0, float s1)
        {
            var surf = new Batch("Street", 180f, Asphalt, Gravel, Verge);
            // the three crossings: a piece of road from the street over the water to the road on the right bank, so a
            // bridge is a thing you walk onto and not a deck standing in the river by itself
            float rlen = Len(river);
            for (int i = 0; i < Crossings.Length; i++)
            {
                float cs = rlen * Crossings[i];
                var link = new[] { (On(river, cs, StreetOff - 5f).x, On(river, cs, StreetOff - 5f).z),
                                   (On(river, cs, WestOff + 5f).x, On(river, cs, WestOff + 5f).z) };
                roadMetres += Ribbon(surf, i == 0 ? 0 : 1, link, i == 0 ? 3.6f : 1.0f, .09f, 4f, 3, .05f);
            }
            roadMetres += Ribbon(surf, 0, Cut(street, s0 - 40f, s1 + 40f), 3.2f, .09f, 4f, 3, .06f);
            roadMetres += Ribbon(surf, 1, Cut(lane, s0 + 40f, s1 - 20f), 2.3f, .07f, 3f, 3, .04f);
            roadMetres += Ribbon(surf, 1, Cut(west, s0, s1 - 60f), 2.4f, .07f, 3f, 3, .04f);
            Ribbon(surf, 2, Shift(Cut(street, s0 - 40f, s1 + 40f), 4.1f), 1.0f, .05f, 3f, 1);
            Ribbon(surf, 2, Shift(Cut(street, s0 - 40f, s1 + 40f), -4.1f), 1.0f, .05f, 3f, 1);
            surf.Emit(group, shadows: false);
        }

        /// <summary>The piece of a polyline between two arc lengths. It keeps the line's own vertices rather than
        /// resampling: twelve kilometres of road cut at four-metre intervals would be three thousand points, and every
        /// arc-length lookup below walks the whole array.</summary>
        static (float x, float z)[] Cut((float x, float z)[] line, float from, float to)
        {
            float len = Len(line);
            from = Mathf.Clamp(from, 0f, Mathf.Max(0f, len - 8f));
            to = Mathf.Clamp(to, from + 8f, len);
            var o = new List<(float x, float z)> { Elbrus.PointAt(line, from) };
            float run = 0f;
            for (int i = 1; i < line.Length; i++)
            {
                float d = Elbrus.Distance(line[i - 1].x, line[i - 1].z, line[i].x, line[i].z);
                run += d;
                if (run > from && run < to) o.Add(line[i]);
            }
            o.Add(Elbrus.PointAt(line, to));
            return o.ToArray();
        }

        /// <summary>A shop or a café: one storey, flat roof with a crooked parapet, a wide window that is lit long
        /// after the houses are dark, a painted board over the door, and the crates and the gas bottle that live
        /// outside every one of them. The two cafés get a red sign that shows its own light — from the street at night
        /// that is the whole of Terskol's neon.</summary>
        static void Shop(Kit kit, Transform group, (float x, float z)[] river, float s, float yaw, string name, bool neon, int seed)
        {
            var size = new Vector2(9f, 6f);
            var p = On(river, s, -20f);
            float y = Seat(p.x, p.z, yaw, size);
            var foot = new Vector3(p.x, y, p.z);
            Building(kit, foot, yaw, 9f, 6f, 1, 3.4f, 1, 5, Roofs.Flat, seed, false, false, .85f);
            var rot = Quaternion.Euler(0, yaw, 0);
            var mt = kit.Metal.At(foot); var wd = kit.Wood.At(foot); var gl = kit.Glass.At(foot);
            // the shop window, wider than a house window and lit
            Card(gl, 1, foot + rot * new Vector3(-2.4f, 1.6f, 3.05f), rot, 3.0f, 1.8f);
            mt.Box(0, foot + rot * new Vector3(-2.4f, 1.6f, 3.08f), new Vector3(3.1f, .07f, .06f), rot, 1f);
            // the awning over the door
            wd.Box(3, foot + rot * new Vector3(1.4f, 2.7f, 3.6f), new Vector3(3.2f, .06f, 1.5f), rot * Quaternion.Euler(-11f, 0, 0), 1f);
            // the board with the name on it
            var board = foot + rot * new Vector3(0, 3.55f, 3.1f);
            // a café gets a sign that shows its own light; a shop gets a painted board
            (neon ? kit.Glass : kit.Wood).At(foot).Box(1, board, new Vector3(6.2f, .75f, .08f), rot, 1f);
            Label(group, name, board + rot * new Vector3(0, 0, .07f), yaw, .14f, neon ? new Color(1f, .93f, .8f) : new Color(.94f, .92f, .86f));
            if (neon) Lamp(group, board + rot * new Vector3(0, -.4f, .5f), 9f, 1.1f, new Color(1f, .5f, .32f), false);
            // crates and a gas bottle by the door, which is where they always are
            var rng = new System.Random(seed + 7);
            for (int i = 0; i < 4; i++)
            {
                var at = foot + rot * new Vector3(3.6f + (i % 2) * .6f, .2f + (i / 2) * .42f, 2.6f + (float)rng.NextDouble() * .8f);
                wd.Box(1, at, new Vector3(.55f, .4f, .4f), rot * Quaternion.Euler(0, (float)rng.NextDouble() * 20f - 10f, 0), 1f);
            }
            mt.Tube(3, foot + rot * new Vector3(-4.2f, 0, 2.4f), foot + rot * new Vector3(-4.2f, .85f, 2.4f), .16f, .16f, 8, 1f, 0, true);
            shops++;
        }

        /// <summary>The bus stop: three posts, a sheet roof, a bench, and the board with the two departures a day on
        /// it. The bus to Нальчик leaves from here, and it is the only way out of this valley that is not a car.</summary>
        static void BusStop(Kit kit, Transform group, (float x, float z)[] river, float s, float yaw)
        {
            var p = On(river, s, -22f);
            float y = Flat(p.x, p.z, yaw, new Vector2(5f, 2.6f));
            var foot = new Vector3(p.x, y, p.z);
            var rot = Quaternion.Euler(0, yaw, 0);
            var mt = kit.Metal.At(foot); var wd = kit.Wood.At(foot); var rf = kit.Roof.At(foot);
            for (int k = -1; k <= 1; k++)
                mt.Tube(0, foot + rot * new Vector3(k * 2.2f, 0, -1.1f), foot + rot * new Vector3(k * 2.2f, 2.5f, -1.1f), .045f, .045f, 5, 1f, 0, false);
            rf.Box(3, foot + rot * new Vector3(0, 2.58f, -.2f), new Vector3(5.2f, .06f, 2.4f), rot * Quaternion.Euler(-7f, 0, 0), 1f);
            rf.Box(3, foot + rot * new Vector3(0, 1.3f, -1.25f), new Vector3(5.0f, 2.6f, .06f), rot, 1f);
            wd.Box(0, foot + rot * new Vector3(0, .48f, -.72f), new Vector3(4.4f, .07f, .38f), rot, 1f);
            for (int k = -1; k <= 1; k += 2)
                wd.Box(0, foot + rot * new Vector3(k * 1.9f, .24f, -.72f), new Vector3(.1f, .48f, .34f), rot, 1f);
            var board = foot + rot * new Vector3(1.6f, 1.75f, -1.18f);
            wd.Box(1, board, new Vector3(1.3f, .9f, .05f), rot, 1f);
            Label(group, "ТЕРСКОЛ\nНальчик 07:10 · 14:40\nАзау — маршрутка по мере\nнаполнения", board + rot * new Vector3(0, 0, .04f), yaw, .056f, new Color(.15f, .14f, .12f));
            publicBuildings++;
        }

        /// <summary>The market row: six stalls under one lean-to of corrugated sheet, trestle tables, crates, and the
        /// knitted shawls hung up on a line across the front of it, which is what is actually sold here.</summary>
        static void Market(Kit kit, Transform group, (float x, float z)[] river, float s, float yaw)
        {
            var p = On(river, s, -21f);
            float y = Seat(p.x, p.z, yaw, new Vector2(14f, 4.2f));
            var foot = new Vector3(p.x, y, p.z);
            var rot = Quaternion.Euler(0, yaw, 0);
            var mt = kit.Metal.At(foot); var wd = kit.Wood.At(foot); var rf = kit.Roof.At(foot); var soft = kit.Soft.At(foot);
            for (float u = -7f; u <= 7.01f; u += 2.8f)
            {
                mt.Tube(0, foot + rot * new Vector3(u, 0, 1.9f), foot + rot * new Vector3(u, 2.35f, 1.9f), .04f, .04f, 5, 1f, 0, false);
                mt.Tube(0, foot + rot * new Vector3(u, 0, -1.9f), foot + rot * new Vector3(u, 2.75f, -1.9f), .04f, .04f, 5, 1f, 0, false);
            }
            rf.Box(0, foot + rot * new Vector3(0, 2.6f, 0), new Vector3(14.6f, .06f, 4.4f), rot * Quaternion.Euler(-6f, 0, 0), 1f);
            var rng = new System.Random(6901);
            for (int i = 0; i < 6; i++)
            {
                float u = -7f + i * 2.8f + 1.4f;
                wd.Box(0, foot + rot * new Vector3(u, .82f, .6f), new Vector3(2.4f, .06f, 1.1f), rot, 1f);
                for (int k = -1; k <= 1; k += 2)
                    wd.Box(0, foot + rot * new Vector3(u + k * 1.0f, .41f, .6f), new Vector3(.08f, .82f, .08f), rot, 1f);
                for (int c = 0; c < 2; c++)
                    wd.Box(1, foot + rot * new Vector3(u - .5f + c * .9f, .95f + (float)rng.NextDouble() * .1f, .6f),
                        new Vector3(.5f, .22f, .4f), rot * Quaternion.Euler(0, (float)rng.NextDouble() * 18f, 0), 1f);
                // the shawls, hung from the front rail
                Card(soft, i % 2, foot + rot * new Vector3(u, 1.65f, 1.85f), rot, 1.1f, 1.3f);
            }
            Label(group, "РЫНОК · ШЕРСТЬ · МЁД", foot + rot * new Vector3(0, 2.95f, -1.95f), yaw + 180f, .13f, new Color(.93f, .91f, .84f));
            publicBuildings++;
        }

        /// <summary>A shed — сарай, коровник, гараж. Planks and offcuts of sheet on a frame of poles, a lean-to roof
        /// weighted down with a stone or two, and no window. There is half a kilometre of these at the bottom of the
        /// real village, left from when it was an aul, and the houses that go with them are new and further up.</summary>
        static void Shed(Kit kit, (float x, float z)[] river, float s, float off, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            float w = 4.6f + (float)rng.NextDouble() * 2.4f, d = 3.2f + (float)rng.NextDouble() * 1.4f;
            var size = new Vector2(w, d);
            var p = On(river, s, off);
            float y = Seat(p.x, p.z, yaw, size);
            var foot = new Vector3(p.x, y, p.z);
            var rot = Quaternion.Euler(0, yaw, 0);
            var wd = kit.Wood.At(foot); var rf = kit.Roof.At(foot); var mt = kit.Metal.At(foot);
            int wood = rng.Next(2);                         // planks or weathered grey board
            wd.Box(wood, foot + rot * new Vector3(0, 1.1f, 0), new Vector3(w, 2.2f, d), rot, .8f);
            float rise = d * .24f;
            rf.Box(rng.Next(2) == 0 ? 4 : 5, foot + rot * new Vector3(0, 2.3f + rise * .5f, 0),
                new Vector3(w + .4f, .05f, Mathf.Sqrt(d * d + rise * rise) + .4f),
                rot * Quaternion.Euler(-Mathf.Atan2(rise, d) * Mathf.Rad2Deg, 0, 0), 1f);
            // the stones on the roof, because the sheet is not nailed down
            for (int i = 0; i < 2; i++)
                rf.Box(5, foot + rot * new Vector3(((float)rng.NextDouble() - .5f) * w, 2.45f + rise * (float)rng.NextDouble(), ((float)rng.NextDouble() - .5f) * d),
                    new Vector3(.4f, .22f, .32f), rot * Quaternion.Euler(0, (float)rng.NextDouble() * 60f, 0), 1f);
            // the door, a sheet of ply on one hinge
            wd.Box(1, foot + rot * new Vector3(0, 1.0f, d * .5f + .04f), new Vector3(1.3f, 2.0f, .06f),
                rot * Quaternion.Euler(0, 0, (float)rng.NextDouble() * 4f - 2f), 1f);
            if (rng.NextDouble() < .5) Woodpile(kit, foot + rot * new Vector3(w * .5f + 1.1f, 0, 0), yaw, seed + 3);
            mt.Tube(0, foot + rot * new Vector3(-w * .5f - .4f, 0, d * .5f), foot + rot * new Vector3(-w * .5f - .4f, 1.6f, d * .5f), .03f, .03f, 4, 1f, 0, false);
        }

        /// <summary>A yard: the corrugated sheet fence half of them have and the picket the other half have, a gate,
        /// firewood stacked against the fence, a line of washing, and a water barrel. This is the half of a village
        /// that is not buildings, and without it a street of houses is a model railway.</summary>
        static void Yard(Kit kit, (float x, float z)[] river, float s, float off, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            var p = On(river, s, off);
            var rot = Quaternion.Euler(0, yaw, 0);
            var centre = new Vector3(p.x, 0, p.z);
            bool sheetFence = rng.Next(2) == 0;
            float hw = 6f, hd = 4.2f;
            Vector3 C(float u, float v) { var q = centre + rot * new Vector3(u, 0, v); q.y = Ground(q.x, q.z); return q; }
            var corners = new[] { C(-hw, -hd), C(hw, -hd), C(hw, hd), C(-hw, hd) };
            for (int i = 0; i < 4; i++)
            {
                if (i == 3) continue;                        // the side towards the street is the gate side
                Fence(kit, corners[i], corners[(i + 1) % 4], sheetFence, yaw, seed + i);
            }
            Gate(kit, C(-hw + 1.6f, hd), C(hw - 1.6f, hd), yaw, sheetFence);
            if (rng.NextDouble() < .7) Woodpile(kit, C(-hw + 1.2f, -hd + 1.2f), yaw, seed + 11);
            if (rng.NextDouble() < .6) Washing(kit, C(-hw + 1.4f, 1.2f), C(hw - 1.4f, 2.2f), seed + 13);
            var barrel = C(hw - 1.2f, -hd + 1.2f);
            kit.Metal.At(barrel).Tube(0, barrel, barrel + Vector3.up * .9f, .32f, .32f, 9, 1f, 0, true);
        }

        /// <summary>A run of fence: posts every two metres and either sheets of профлист or a picket of offcuts
        /// between them, following the ground the whole way. Batched — there are hundreds of metres of it.</summary>
        static void Fence(Kit kit, Vector3 a, Vector3 b, bool sheet, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
            if (len < .5f) return;
            var dir = (new Vector3(b.x, 0, b.z) - new Vector3(a.x, 0, a.z)).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float h = sheet ? 1.85f : 1.35f;
            const float Pitch = 2f;
            for (float t = 0; t < len; t += Pitch)
            {
                float t1 = Mathf.Min(len, t + Pitch);
                var p0 = a + dir * t; p0.y = Ground(p0.x, p0.z);
                var p1 = a + dir * t1; p1.y = Ground(p1.x, p1.z);
                var mid = (p0 + p1) * .5f;
                var mb = sheet ? kit.Roof.At(mid) : kit.Wood.At(mid);
                var post = kit.Wood.At(mid);
                post.Tube(2, p0 + Vector3.down * .3f, p0 + Vector3.up * (h + .12f), .06f, .055f, 5, 1f, 0, true);
                if (sheet) mb.Box(rng.Next(3), mid + Vector3.up * (h * .5f + .1f), new Vector3(.03f, h, t1 - t), rot, 1f);
                else
                    for (float u = t; u < t1 - .05f; u += .16f)
                    {
                        var q = a + dir * u; q.y = Ground(q.x, q.z);
                        mb.Box(rng.Next(2), q + Vector3.up * (h * .5f + .05f + (float)rng.NextDouble() * .08f),
                            new Vector3(.025f, h, .1f), rot, 1f);
                    }
                fenceMetres += t1 - t;
            }
        }

        /// <summary>The gate: two leaves that never both shut, and a wicket beside them.</summary>
        static void Gate(Kit kit, Vector3 a, Vector3 b, float yaw, bool sheet)
        {
            var mid = (a + b) * .5f;
            var dir = (new Vector3(b.x, 0, b.z) - new Vector3(a.x, 0, a.z)).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var wd = kit.Wood.At(mid); var rf = kit.Roof.At(mid);
            float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
            wd.Tube(2, a + Vector3.down * .3f, a + Vector3.up * 2.1f, .08f, .075f, 6, 1f, 0, true);
            wd.Tube(2, b + Vector3.down * .3f, b + Vector3.up * 2.1f, .08f, .075f, 6, 1f, 0, true);
            for (int k = -1; k <= 1; k += 2)
            {
                float swing = k > 0 ? 0f : 14f;              // one leaf half open
                var hinge = mid + dir * (k * len * .5f);
                var leaf = hinge - dir * (k * len * .25f);
                (sheet ? rf : wd).Box(sheet ? 1 : 0, leaf + Vector3.up * 1.0f, new Vector3(.05f, 1.8f, len * .5f),
                    rot * Quaternion.Euler(0, swing * k, 0), 1f);
            }
            fenceMetres += len;
        }

        /// <summary>Firewood, stacked the way it is stacked everywhere: split lengths end-on in a wall as tall as a
        /// man's chest, with the bark side out and a sheet of something over the top.</summary>
        static void Woodpile(Kit kit, Vector3 foot, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            var rot = Quaternion.Euler(0, yaw, 0);
            var wd = kit.Wood.At(foot);
            int rows = 5 + rng.Next(3);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 9; c++)
                {
                    if (r == rows - 1 && rng.NextDouble() < .35) continue;
                    var at = foot + rot * new Vector3(-.9f + c * .225f, .12f + r * .21f, 0);
                    wd.Tube(2, at, at + rot * new Vector3(0, 0, .42f), .1f, .095f, 6, 1f, 0, true);
                }
            kit.Roof.At(foot).Box(5, foot + rot * new Vector3(0, .14f + rows * .21f, 0), new Vector3(2.2f, .03f, .7f), rot, 1f);
        }

        /// <summary>A line of washing between two posts. Sheets and a shirt: the only moving-looking thing in the
        /// village, and the thing that says somebody lives here.</summary>
        static void Washing(Kit kit, Vector3 a, Vector3 b, int seed)
        {
            var rng = new System.Random(seed);
            var wd = kit.Wood.At(a); var soft = kit.Soft.At(a);
            var ha = a + Vector3.up * 1.8f; var hb = b + Vector3.up * 1.75f;
            wd.Tube(2, a, ha, .05f, .045f, 5, 1f, 0, true);
            wd.Tube(2, b, hb, .05f, .045f, 5, 1f, 0, true);
            wd.Tube(2, ha, hb, .008f, .008f, 4, 1f, 0, false);
            int n = 2 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float t = (i + .6f) / (n + .2f);
                var at = Vector3.Lerp(ha, hb, t) + Vector3.down * .45f;
                var rot = Quaternion.LookRotation((hb - ha).normalized, Vector3.up) * Quaternion.Euler(0, 90f, 0);
                Card(soft, i % 2, at, rot, .8f, .9f);
            }
        }

        /// <summary>The right bank: the base, which is half the village. In the real Terskol that half belongs to the
        /// ЦСКА school of mountain training — founded here in 1935 as the Red Army's mountaineering school — and to
        /// the Ministry of Defence sanatorium; it has its own wooden entry arch of Soviet vintage, a white obelisk of
        /// 2016 to the men who fought here in 1943, two long two-storey barracks with the twin-headed Elbrus cut into
        /// the end wall, and, at the entrance to the village, what is honestly advertised as the highest five-storey
        /// block of flats in Russia. The mosque stands by the bridge; the path to the waterfall starts beside it.</summary>
        static void Base(Kit kit, Transform group, (float x, float z)[] river, (float x, float z)[] west, float s0, float s1)
        {
            float BearW(float s) => Bearing(river, s) - 90f;

            // the mosque, by the bridge, where the path up the gorge starts
            {
                float s = 185f;
                float yaw = BearW(s);
                var p = On(river, s, 42f);
                float y = Seat(p.x, p.z, yaw, new Vector2(12f, 12f));
                var foot = new Vector3(p.x, y, p.z);
                Building(kit, foot, yaw, 12f, 12f, 1, 4.6f, 1, 1, Roofs.Flat, 7001, false, false, .25f);
                var rot = Quaternion.Euler(0, yaw, 0);
                Dome(kit.Roof.At(foot), 1, foot + Vector3.up * 5.0f, 3.4f, 10, 6);
                // the minaret: seventeen metres, a balcony two thirds up and a little cone on top
                var mn = foot + rot * new Vector3(6.6f, 0, 5.2f);
                mn.y = Seat(mn.x, mn.z, yaw, new Vector2(1.6f, 1.6f));
                var sh = kit.Shell.At(mn);
                sh.Tube(1, mn, mn + Vector3.up * 13.5f, .85f, .7f, 10, 1f, 0, false);
                sh.Tube(1, mn + Vector3.up * 9.5f, mn + Vector3.up * 10.1f, 1.15f, 1.15f, 10, 1f, 0, true);
                kit.Roof.At(mn).Cone(1, mn + Vector3.up * 14.4f, 1.0f, 2.2f, 10, 1f);
                for (int k = 0; k < 4; k++)
                {
                    float a = k * 90f * Mathf.Deg2Rad;
                    var q = mn + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .72f + Vector3.up * 11.6f;
                    Card(kit.Glass.At(mn), 0, q, Quaternion.Euler(0, k * 90f + 90f, 0), .5f, 1.5f);
                }
                Lamp(group, foot + rot * new Vector3(0, 3.2f, 6.6f), 11f, .9f, new Color(1f, .9f, .72f), true);
                Label(group, "МЕЧЕТЬ · отсюда тропа\nна водопад и обсерваторию", foot + rot * new Vector3(0, 2.2f, 6.2f), yaw, .1f, new Color(.9f, .9f, .86f));
                publicBuildings++;
            }

            // the obelisk, white, with a star on top of it
            {
                float s = 150f, yaw = BearW(s);
                var p = On(river, s, 41f);
                float y = Seat(p.x, p.z, yaw, new Vector2(2.2f, 2.2f));
                var foot = new Vector3(p.x, y, p.z);
                var sh = kit.Shell.At(foot); var mt = kit.Metal.At(foot);
                var rot = Quaternion.Euler(0, yaw, 0);
                sh.Box(5, foot + Vector3.up * .3f, new Vector3(2.4f, .6f, 2.4f), rot, 1f);
                sh.Box(5, foot + Vector3.up * .85f, new Vector3(1.5f, .5f, 1.5f), rot, 1f);
                for (int i = 0; i < 5; i++)
                    sh.Box(5, foot + Vector3.up * (1.2f + i * .72f), new Vector3(Mathf.Lerp(1.0f, .5f, i / 4f), .72f, Mathf.Lerp(1.0f, .5f, i / 4f)), rot, 1f);
                mt.Box(2, foot + Vector3.up * 5.1f, new Vector3(.5f, .5f, .07f), rot * Quaternion.Euler(0, 0, 45f), 1f);
                Label(group, "1942—1943\nзащитникам Приэльбрусья", foot + rot * new Vector3(0, 1.5f, .78f), yaw, .075f, new Color(.2f, .2f, .22f));
                publicBuildings++;
            }

            // the five-storey block at the entrance to the village
            {
                float s = 250f, yaw = BearW(s);
                Plot(kit, river, s, 56f, yaw, 26f, 12f, 5, 3.0f, 1, 0, Roofs.Flat, 7010, true, true, .35f, false);
                publicBuildings++;
                var p = On(river, s, 56f);
                var rot = Quaternion.Euler(0, yaw, 0);
                var top = new Vector3(p.x, Seat(p.x, p.z, yaw, new Vector2(26f, 12f)) + 15.9f, p.z);
                kit.Metal.At(top).Tube(0, top, top + Vector3.up * 2.4f, .05f, .04f, 5, 1f, 0, false);
                kit.Metal.At(top).Tube(6, top + Vector3.up * 2.4f, top + Vector3.up * 2.62f, .1f, .1f, 7, 1f, 0, true);
            }

            // the two barracks of the base, with the mountain cut into the end wall
            for (int i = 0; i < 2; i++)
            {
                float s = 330f + i * 72f, yaw = BearW(s);
                Plot(kit, river, s, 52f, yaw, 34f, 9f, 2, 3.2f, 1, i == 0 ? 0 : 4, Roofs.Gable, 7020 + i, true, false, .4f, true);
                publicBuildings++;
                var p = On(river, s, 52f);
                float y = Seat(p.x, p.z, yaw, new Vector2(34f, 9f));
                var rot = Quaternion.Euler(0, yaw, 0);
                var wall = new Vector3(p.x, y, p.z) + rot * new Vector3(-17.1f, 3.9f, 0);
                var gl = kit.Glass.At(wall);
                // the twin-headed silhouette: two triangles, which is all it ever is on a gable end
                for (int k = -1; k <= 1; k += 2)
                {
                    var a = wall + rot * new Vector3(0, -1.5f, k * .2f - 1.6f);
                    var b = wall + rot * new Vector3(0, -1.5f, k * .2f + 1.6f);
                    var c = wall + rot * new Vector3(0, 1.5f + (k > 0 ? .35f : 0f), k * .2f);
                    gl.Quad(0, a, b, c, c, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
            }

            // the sanatorium block
            {
                float s = 490f, yaw = BearW(s);
                Plot(kit, river, s, 58f, yaw, 30f, 13f, 4, 3.1f, 2, 3, Roofs.Flat, 7030, true, false, .45f, true);
                publicBuildings++;
            }

            // the wooden entry arch over the road on the right bank, and the flagpole beside it
            {
                float s = 300f, yaw = Bearing(west, ArcOf(west, On(river, s, WestOff)));
                var mid = On(river, s, WestOff);
                var rot = Quaternion.Euler(0, yaw, 0);
                var wd = kit.Wood.At(mid); var mt = kit.Metal.At(mid);
                for (int k = -1; k <= 1; k += 2)
                {
                    var leg = mid + rot * new Vector3(k * 3.6f, 0, 0);
                    leg.y = Ground(leg.x, leg.z);
                    wd.Tube(2, leg, leg + Vector3.up * 4.4f, .16f, .14f, 6, 1f, 0, true);
                }
                var beam = mid + Vector3.up * 4.3f;
                wd.Box(0, beam, new Vector3(7.8f, .5f, .22f), rot, 1f);
                wd.Box(0, beam + Vector3.up * .45f, new Vector3(8.4f, .12f, .4f), rot, 1f);
                Label(group, "ЦСКА · ТЕРСКОЛ · 1935", beam + rot * new Vector3(0, 0, -.13f), yaw + 180f, .17f, new Color(.93f, .9f, .8f));
                var pole = mid + rot * new Vector3(5.4f, 0, 1.8f); pole.y = Ground(pole.x, pole.z);
                mt.Tube(1, pole, pole + Vector3.up * 7.5f, .07f, .05f, 6, 1f, 0, true);
                Card(kit.Soft.At(pole), 0, pole + rot * new Vector3(.9f, 6.6f, 0), rot, 1.8f, 1.1f);
            }
        }

        /// <summary>Arc length along a line of the point nearest a place — how far down the street you are level with
        /// a thing that was placed off the river.</summary>
        static float ArcOf((float x, float z)[] line, Vector3 p)
        {
            var (s, _) = Elbrus.Nearest(line, p.x, p.z);
            return s;
        }

        /// <summary>A hemispherical dome, built as rings — the mosque's, and the observatory's.</summary>
        static void Dome(MeshBuilder mb, int sub, Vector3 centre, float r, int sect, int rings, float squash = 1f)
        {
            Vector3 P(int i, int j)
            {
                float lat = i / (float)rings * Mathf.PI * .5f, lon = j / (float)sect * Mathf.PI * 2f;
                return centre + new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon) * r, Mathf.Sin(lat) * r * squash, Mathf.Cos(lat) * Mathf.Sin(lon) * r);
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sect; j++)
                    mb.Quad(sub, P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>The line that makes this the only lit place on the map: wooden poles down the street every
        /// thirty-eight metres, a crossarm with four insulators on it, four wires between them hanging on an honest
        /// catenary, and a service drop into every third house. A line drawn straight between two poles is the one
        /// thing that would give the whole village away.</summary>
        static void Power(Kit kit, (float x, float z)[] river, float s0, float s1)
        {
            var prev = new Vector3[4];
            bool first = true;
            var rng = new System.Random(8001);
            for (float s = s0 - 20f; s <= s1 + 20f; s += 38f)
            {
                var foot = On(river, s, -44f);
                var mb = kit.Wood.At(foot); var mt = kit.Metal.At(foot);
                float h = 8.2f;
                var rot = Quaternion.Euler(0, Bearing(river, s), 0);
                mb.Tube(2, foot + Vector3.down * .5f, foot + Vector3.up * h, .12f, .095f, 7, 1f, 0, true);
                var arm = foot + Vector3.up * (h - .6f);
                mb.Box(2, arm, new Vector3(2.0f, .09f, .09f), rot, 1f);
                var eyes = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var at = arm + rot * new Vector3(-.75f + i * .5f, .18f, 0);
                    mt.Tube(1, arm + rot * new Vector3(-.75f + i * .5f, 0, 0), at, .035f, .035f, 4, 1f, 0, true);
                    eyes[i] = at;
                }
                if (!first)
                    for (int i = 0; i < 4; i++)
                    {
                        Sag(kit.Metal.At((prev[i] + eyes[i]) * .5f), 0, prev[i], eyes[i], .55f, .015f, 7);
                        wireMetres += Vector3.Distance(prev[i], eyes[i]);
                    }
                // the drop into the nearest house, and the meter box on the pole
                var drop = On(river, s + 6f, -56f, 4.4f);
                Sag(mt, 0, eyes[0], drop, .3f, .012f, 5);
                wireMetres += Vector3.Distance(eyes[0], drop);
                mt.Box(5, foot + rot * new Vector3(.2f, 2.2f, 0), new Vector3(.3f, .4f, .2f), rot, 1f);
                for (int i = 0; i < 4; i++) prev[i] = eyes[i];
                first = false;
                poles++;
                if (rng.NextDouble() < .35) Washing(kit, foot + rot * new Vector3(1f, 0, 0), foot + rot * new Vector3(5f, 0, 2f), 8100 + (int)s);
            }
        }

        /// <summary>A line hung between two points the way a cable really hangs: y = a·cosh(u/a), fitted to the span
        /// and the sag. Same solver as the МЧС cable up on the mountain (<see cref="ElbrusAscent"/>).</summary>
        static void Sag(MeshBuilder mb, int sub, Vector3 a, Vector3 b, float sag, float radius, int segments)
        {
            float span = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            if (span < .05f) { mb.Tube(sub, a, b, radius, radius, 4, 1f, 0, false); return; }
            double guess = span * span / (8 * Mathf.Max(.01f, sag));
            double lo = guess * .2, hi = guess * 5 + span;
            for (int i = 0; i < 50; i++)
            {
                double m = (lo + hi) * .5;
                if (m * (Math.Cosh(span / (2 * m)) - 1) > sag) lo = m; else hi = m;
            }
            float pmm = (float)((lo + hi) * .5);
            double top = Math.Cosh(span / (2 * pmm));
            var prev = a;
            for (int k = 1; k <= segments; k++)
            {
                float t = k / (float)segments;
                var q = Vector3.Lerp(a, b, t);
                double u = (t - .5) * span;
                q.y -= (float)(pmm * (top - Math.Cosh(u / pmm)));
                mb.Tube(sub, prev, q, radius, radius, 4, 1f, 0, false);
                prev = q;
            }
        }

        /// <summary>The gas main again, this time through the village: the same yellow pipe on the same stilts, but
        /// here it steps up and over every gateway and has a riser and a meter at every second house.</summary>
        static void GasBranch(Kit kit, (float x, float z)[] street, float s0, float s1)
        {
            var prev = Vector3.zero; bool first = true;
            int i = 0;
            for (float s = s0 - 30f; s <= s1 + 30f; s += 7f, i++)
            {
                var foot = On(street, s, -5.6f);
                var mb = kit.Metal.At(foot);
                bool gate = i % 5 == 0;
                var top = foot + Vector3.up * (gate ? 2.5f : .9f);
                mb.Tube(1, foot, top, .03f, .03f, 5, 1f, 0, false);
                if (!first)
                {
                    mb.Tube(4, prev, top, .075f, .075f, 6, 1f, 0, false);
                    gasMetres += Vector3.Distance(prev, top);
                }
                prev = top; first = false;
                if (i % 4 == 2)
                {
                    var riser = On(street, s, -14f, 1.6f);
                    mb.Tube(4, top, riser, .05f, .05f, 5, 1f, 0, false);
                    mb.Box(5, riser + Vector3.up * .2f, new Vector3(.34f, .42f, .24f), Quaternion.identity, 1f);
                }
            }
        }

        /// <summary>Street lamps: a bracket off every third pole with a shade and a sodium bulb in it. Twelve of them
        /// down six hundred metres, which is about what the real street has, and it is the only artificial light on
        /// this map outside a hut.</summary>
        static void StreetLamps(Kit kit, Transform group, (float x, float z)[] street, float s0, float s1)
        {
            for (float s = s0; s <= s1; s += 52f)
            {
                var foot = On(street, s, 5.2f);
                var mb = kit.Metal.At(foot);
                var rot = Quaternion.Euler(0, Bearing(street, s), 0);
                mb.Tube(0, foot + Vector3.down * .4f, foot + Vector3.up * 7f, .075f, .055f, 6, 1f, 0, true);
                var head = foot + Vector3.up * 7f + rot * new Vector3(-1.5f, .5f, 0);
                mb.Tube(0, foot + Vector3.up * 6.6f, head, .05f, .04f, 5, 1f, 0, false);
                mb.Cone(2, head + Vector3.down * .04f, .28f, .26f, 8, 1f);
                Card(kit.Glass.At(foot), 1, head + Vector3.down * .1f, Quaternion.Euler(90f, 0, 0), .42f, .42f);
                Lamp(group, head + Vector3.down * .3f, 17f, 1.25f, new Color(1f, .82f, .58f), true);
            }
        }

        /// <summary>What is parked in the street: a УАЗ буханка with a roof rack, a couple of old saloons, and a
        /// minibus by the bus stop. Laid on the slope plane and tilted to it, the way a car parked across a hillside
        /// leans (Sit.Lie).</summary>
        static void Vehicles(Kit kit, (float x, float z)[] river, (float x, float z)[] street, float s0, float s1)
        {
            var rng = new System.Random(8200);
            for (int i = 0; i < 9; i++)
            {
                float s = s0 + 25f + i * 62f;
                if (s > s1) break;
                float off = -31.5f - (i % 2) * 4.5f;
                var p = On(river, s, off);
                float yaw = Bearing(river, s) + (i % 2 == 0 ? 0f : 180f) + (float)rng.NextDouble() * 8f - 4f;
                var rot = Lay(p.x, p.z, yaw);
                var at = new Vector3(p.x, Flat(p.x, p.z, yaw, new Vector2(2f, 4.6f)), p.z);
                Car(kit, at, rot, i % 3, 8300 + i);
            }
        }

        /// <summary>One vehicle. Three kinds, and every one of them is a box on wheels with the glass let into it —
        /// at the distance a village street is seen from, that is what a car is.</summary>
        static void Car(Kit kit, Vector3 at, Quaternion rot, int kind, int seed)
        {
            var mb = kit.Metal.At(at); var gl = kit.Glass.At(at); var rb = kit.Roof.At(at);
            float l = kind == 1 ? 4.5f : kind == 2 ? 5.4f : 4.3f;
            float w = kind == 0 ? 1.7f : 2.0f;
            float bodyY = kind == 0 ? .62f : .85f;
            int paint = kind == 1 ? 2 : kind == 2 ? 3 : 1;
            mb.Box(paint, at + rot * new Vector3(0, bodyY, 0), new Vector3(w, kind == 0 ? .7f : 1.5f, l), rot, 1f);
            if (kind == 0)
            {
                mb.Box(paint, at + rot * new Vector3(0, 1.22f, -.25f), new Vector3(w * .92f, .55f, l * .5f), rot, 1f);
                Card(gl, 0, at + rot * new Vector3(0, 1.24f, l * .25f - .28f), rot, w * .82f, .5f);
                Card(gl, 0, at + rot * new Vector3(0, 1.24f, -l * .25f - .1f), rot, w * .82f, .5f);
            }
            else
            {
                for (int k = -1; k <= 1; k += 2)
                    Card(gl, 0, at + rot * new Vector3(k * w * .5f, 1.35f, 0), rot * Quaternion.Euler(0, 90f, 0), l * .7f, .6f);
                Card(gl, 0, at + rot * new Vector3(0, 1.35f, l * .5f), rot, w * .86f, .6f);
                // the roof rack, which every УАЗ here has and which always has something tied to it
                rb.Box(0, at + rot * new Vector3(0, 1.68f, 0), new Vector3(w * .9f, .06f, l * .8f), rot, 1f);
            }
            for (int fz = -1; fz <= 1; fz += 2)
                for (int fx = -1; fx <= 1; fx += 2)
                {
                    var c = at + rot * new Vector3(fx * w * .5f, .34f, fz * l * .33f);
                    mb.Tube(5, c - rot * new Vector3(fx * .09f, 0, 0), c + rot * new Vector3(fx * .09f, 0, 0), .34f, .34f, 9, 1f, 0, true);
                }
            cars++;
        }

        /// <summary>The helipad: a concrete disc on the flattest ground in the gorge, a white H, a ring of painted
        /// stones and a wind sock. It is a real thing in Terskol — it is how a man comes off the mountain when it goes
        /// wrong, and it is the reason the МЧС are here at all.</summary>
        static void Helipad(Kit kit, Transform group, (float x, float z)[] river, float s)
        {
            var p = On(river, s, 40f);
            float y = Seat(p.x, p.z, 0f, new Vector2(18f, 18f));
            var at = new Vector3(p.x, y + .12f, p.z);
            var mb = kit.Shell.At(at); var mt = kit.Metal.At(at); var soft = kit.Soft.At(at);
            const int Sect = 20;
            for (int i = 0; i < Sect; i++)
            {
                float a0 = i / (float)Sect * Mathf.PI * 2, a1 = (i + 1) / (float)Sect * Mathf.PI * 2;
                var c = at;
                var q0 = at + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 9f;
                var q1 = at + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 9f;
                mb.Quad(5, c, q0, q1, q1, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                // the skirt, so the disc is a slab and not a decal
                mb.Quad(5, q0, q1, q1 + Vector3.down * .5f, q0 + Vector3.down * .5f, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            }
            // the H
            for (int k = -1; k <= 1; k += 2)
                mt.Box(2, at + new Vector3(k * 1.5f, .03f, 0), new Vector3(.45f, .02f, 4f), Quaternion.identity, 1f);
            mt.Box(2, at + Vector3.up * .03f, new Vector3(3f, .02f, .45f), Quaternion.identity, 1f);
            // the wind sock
            var mast = at + new Vector3(11f, 0, 3f); mast.y = Ground(mast.x, mast.z);
            mt.Tube(0, mast, mast + Vector3.up * 6f, .06f, .045f, 6, 1f, 0, true);
            var head = mast + Vector3.up * 5.8f;
            for (int i = 0; i < 5; i++)
            {
                var a = head + new Vector3(i * .5f, -i * .1f, 0);
                var b = head + new Vector3((i + 1) * .5f, -(i + 1) * .12f, 0);
                soft.Tube(i % 2 == 0 ? 0 : 1, a, b, .26f - i * .03f, .24f - i * .03f, 8, 1f, 0, false);
            }
            Label(group, "ВЕРТОЛЁТНАЯ ПЛОЩАДКА\nЭВПСО МЧС · не заходить", at + new Vector3(0, 1.4f, -10.5f), 0f, .1f, new Color(.9f, .9f, .86f));
            publicBuildings++;
        }

        // ── 5. обсерватория «Пик Терскол» ─────────────────────────────────────────────────────────────────
        /// <summary>The Terskol observatory, on the shelf of пик Терскол — the highest astronomical station in Russia
        /// and in Europe, working since 1980 and run today as the Terskol branch of ИНАСАН with the МЦАЭД.
        ///
        /// What is actually up there, and therefore what is here:
        /// <list type="bullet">
        /// <item><b>«Цейсс-2000»</b>, the two-metre, Carl Zeiss Jena, under a fully rotating dome 20 m across and
        /// 250 t heavy — the thing you see from the whole of the Baksan valley, a white drum with a white dome and a
        /// shutter slit down it;</item>
        /// <item><b>«Цейсс-600»</b>, the 60 cm Cassegrain, in its own small tower;</item>
        /// <item><b>АЦУ-26</b>, the 65 cm horizontal solar telescope — a long low hall with a coelostat tower at the
        /// end of it, which is what makes the silhouette up here unmistakable;</item>
        /// <item>two robotic domes, the Meade LX200 (356 mm) and the Celestron 11 (280 mm);</item>
        /// <item>the hostel built in 2001, where the ten people who work here live a month at a time — nobody stays
        /// longer, the ozone at this height is bad for you;</item>
        /// <item>the diesel house, the fuel and water tanks, the mast with the «Волна-К» aerial on it;</item>
        /// <item>a barrier across the road, because it is a closed site and you go in with a guide or not at all.</item>
        /// </list>
        /// Lit at night, of course — but blind-side only: a lamp over the door, a red light on the mast, and the
        /// windows of the hostel. Nobody shines a light at a dome.</summary>
        static void ObservatoryStation(Transform root, (float x, float z)[] toObs)
        {
            var group = new GameObject("Observatory").transform; group.SetParent(root, false);
            var kit = new Kit(140f);
            var white = new Batch("Dome", 140f, White, PlasterCold, Steel, WindowDark, Alu);

            float along = FaceDownhill(Observatory.x, Observatory.y, 60f);      // the shelf falls to the west
            float yaw = along + 90f;                                            // buildings stand along the contour
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 Site(float u, float v)
            {
                var q = new Vector3(Observatory.x, 0, Observatory.y) + rot * new Vector3(u, 0, v);
                q.y = Ground(q.x, q.z);
                return q;
            }

            // ── 1. the two-metre and its dome ────────────────────────────────────────────────────────────
            {
                var at = Site(0f, 0f);
                float y = Seat(at.x, at.z, yaw, new Vector2(BigDomeR * 2f, BigDomeR * 2f));
                var foot = new Vector3(at.x, y, at.z);
                var mb = white.At(foot);
                // the drum: three storeys of concrete with a ring of small windows in the middle one
                mb.Tube(1, foot - Vector3.up * 1.2f, foot + Vector3.up * 11.5f, BigDomeR, BigDomeR, 20, .5f, 0, false);
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Card(white.At(foot), 3, foot + n * (BigDomeR + .05f) + Vector3.up * 5.6f,
                        Quaternion.LookRotation(n, Vector3.up), 1.1f, 1.4f);
                }
                // the dome, twenty metres across, and the shutter down the front of it
                Dome(mb, 0, foot + Vector3.up * 11.5f, BigDomeR, 20, 7, .92f);
                mb.Tube(2, foot + Vector3.up * 11.3f, foot + Vector3.up * 11.9f, BigDomeR + .22f, BigDomeR + .22f, 20, .5f, 0, false);
                var shutterDir = Quaternion.Euler(0, along, 0);
                for (int i = 0; i < 7; i++)
                {
                    float t0 = i / 7f, t1 = (i + 1) / 7f;
                    float l0 = t0 * Mathf.PI * .5f, l1 = t1 * Mathf.PI * .5f;
                    Vector3 P(float l, float w) => foot + Vector3.up * (11.5f + Mathf.Sin(l) * BigDomeR * .92f)
                        + shutterDir * new Vector3(w, 0, Mathf.Cos(l) * BigDomeR);
                    mb.Quad(2, P(l0, -1.6f), P(l0, 1.6f), P(l1, 1.6f), P(l1, -1.6f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
                Solid(group, "DomeTower", foot + Vector3.up * 6f, new Vector3(BigDomeR * 1.9f, 14f, BigDomeR * 1.9f));
                obsBuildings++;
            }

            // ── 2. «Цейсс-600» in its own tower ─────────────────────────────────────────────────────────
            {
                var at = Site(-26f, 12f);
                float y = Seat(at.x, at.z, yaw, new Vector2(8f, 8f));
                var foot = new Vector3(at.x, y, at.z);
                var mb = white.At(foot);
                mb.Tube(1, foot - Vector3.up * 1f, foot + Vector3.up * 6.5f, 3.6f, 3.6f, 14, .5f, 0, false);
                Dome(mb, 0, foot + Vector3.up * 6.5f, 3.6f, 14, 5, .95f);
                Card(white.At(foot), 2, foot + Quaternion.Euler(0, along, 0) * new Vector3(0, 8.4f, 3.3f),
                    Quaternion.Euler(0, along, 0), 1.3f, 3.6f);
                obsBuildings++;
            }

            // ── 3. АЦУ-26, the horizontal solar telescope ────────────────────────────────────────────────
            {
                var at = Site(24f, -16f);
                float y = Seat(at.x, at.z, yaw, new Vector2(20f, 8f));
                var foot = new Vector3(at.x, y, at.z);
                Building(kit, foot, yaw, 20f, 7.5f, 1, 4.2f, 1, 5, Roofs.Flat, 9101, false, false, .2f);
                // the coelostat tower at the north end: the mirror that feeds the horizontal tube
                var tw = foot + rot * new Vector3(-10.5f, 0, 0);
                tw.y = Seat(tw.x, tw.z, yaw, new Vector2(5f, 5f));
                var mb = white.At(tw);
                mb.Tube(1, tw - Vector3.up * .8f, tw + Vector3.up * 9f, 2.3f, 2.1f, 12, .5f, 0, false);
                Dome(mb, 0, tw + Vector3.up * 9f, 2.1f, 12, 4, .8f);
                mb.Tube(2, tw + Vector3.up * 9.1f, tw + Vector3.up * 9.5f, 2.3f, 2.3f, 12, .5f, 0, false);
                obsBuildings++;
            }

            // ── 4. the two robotic domes on their pads ───────────────────────────────────────────────────
            for (int i = 0; i < 2; i++)
            {
                var at = Site(-6f + i * 13f, 26f);
                float y = Seat(at.x, at.z, yaw, new Vector2(4f, 4f));
                var foot = new Vector3(at.x, y, at.z);
                var mb = white.At(foot);
                kit.Shell.At(foot).Box(5, foot + Vector3.down * .25f, new Vector3(4.4f, .5f, 4.4f), rot, 1f);
                mb.Tube(1, foot, foot + Vector3.up * 1.7f, 1.6f, 1.6f, 10, .5f, 0, false);
                Dome(mb, 0, foot + Vector3.up * 1.7f, 1.6f, 10, 4, .9f);
                obsBuildings++;
            }

            // ── 5. the hostel, the diesel house and the tanks ────────────────────────────────────────────
            {
                var at = Site(34f, 22f);
                float y = Seat(at.x, at.z, yaw, new Vector2(22f, 10f));
                var foot = new Vector3(at.x, y, at.z);
                Building(kit, foot, yaw, 22f, 10f, 2, 3.1f, 1, 0, Roofs.Gable, 9102, false, false, .6f, true);
                Lamp(group, foot + rot * new Vector3(0, 2.6f, 5.6f), 12f, 1.1f, new Color(1f, .88f, .68f), true);
                Card(kit.Glass.At(foot), 1, foot + rot * new Vector3(0, 2.55f, 5.35f), rot, .3f, .3f);
                Label(group, "ОБСЕРВАТОРИЯ «ПИК ТЕРСКОЛ» · 3 127 м\nТерскольский филиал ИНАСАН\nвахта — месяц", foot + rot * new Vector3(0, 4.6f, 5.2f), yaw, .1f, new Color(.92f, .92f, .88f));
                obsBuildings++;
            }
            {
                var at = Site(50f, -4f);
                float y = Seat(at.x, at.z, yaw, new Vector2(9f, 6f));
                var foot = new Vector3(at.x, y, at.z);
                Building(kit, foot, yaw, 9f, 6f, 1, 3.2f, 5, 5, Roofs.Lean, 9103, false, false, .15f);
                obsBuildings++;
                // fuel and water: three tanks on a concrete cradle, which is all the plumbing there is at 3 100 m
                for (int i = 0; i < 3; i++)
                {
                    var tk = Site(58f + i * 4.2f, 8f);
                    var mb = kit.Metal.At(tk);
                    kit.Shell.At(tk).Box(5, tk + Vector3.up * .25f, new Vector3(3.4f, .5f, 2.2f), rot, 1f);
                    mb.Tube(i == 2 ? 1 : 0, tk + Vector3.up * .5f + rot * new Vector3(-1.4f, 1f, 0),
                        tk + Vector3.up * .5f + rot * new Vector3(1.4f, 1f, 0), 1.0f, 1.0f, 12, 1f, 0, true);
                }
            }

            // ── 6. the mast, and the barrier on the road ─────────────────────────────────────────────────
            {
                var at = Site(-40f, -8f);
                var mb = kit.Metal.At(at);
                for (int i = 0; i < 4; i++)
                    mb.Tube(0, at + Vector3.up * (i * 3.5f), at + Vector3.up * ((i + 1) * 3.5f), .09f, .08f, 5, 1f, 0, false);
                for (int i = 0; i < 3; i++)
                {
                    float y0 = 3.5f + i * 3.5f;
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 120f * Mathf.Deg2Rad;
                        var guy = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7f;
                        guy.y = Ground(guy.x, guy.z);
                        if (i == 0) mb.Tube(0, at + Vector3.up * 12f, guy, .012f, .012f, 4, 1f, 0, false);
                    }
                    DishBowl(mb, 1, at + Vector3.up * y0 + rot * new Vector3(.6f, 0, 0), along, .55f);
                }
                mb.Tube(6, at + Vector3.up * 14.1f, at + Vector3.up * 14.35f, .11f, .11f, 7, 1f, 0, true);
                Lamp(group, at + Vector3.up * 14.2f, 8f, .5f, new Color(1f, .2f, .12f), false);
            }
            {
                float len = Elbrus.Length(toObs);
                var at = On(toObs, len - 70f, 0f);
                var bar = kit.Metal.At(at);
                var b = Quaternion.Euler(0, Bearing(toObs, len - 70f) + 90f, 0);
                for (int k = -1; k <= 1; k += 2)
                {
                    var post = at + b * new Vector3(k * 2.6f, 0, 0); post.y = Ground(post.x, post.z);
                    bar.Tube(0, post, post + Vector3.up * 1.3f, .08f, .08f, 6, 1f, 0, true);
                }
                var beam = at + Vector3.up * 1.05f;
                bar.Box(2, beam, new Vector3(5.2f, .12f, .12f), b, 1f);
                for (int i = -2; i <= 2; i++)
                    bar.Box(3, beam + b * new Vector3(i * 1.04f, 0, 0), new Vector3(.52f, .125f, .125f), b, 1f);
                Label(group, "РЕЖИМНЫЙ ОБЪЕКТ\nпроезд и проход\nтолько по согласованию", beam + Vector3.up * .9f, Bearing(toObs, len - 70f) + 180f, .085f, new Color(.9f, .3f, .24f));
                Car(kit, new Vector3(at.x, Flat(at.x, at.z, 0f, new Vector2(2f, 4.6f)), at.z) + b * new Vector3(6f, 0, 3f),
                    Lay(at.x, at.z, Bearing(toObs, len - 70f)), 1, 9200);
            }

            kit.Emit(group);
            white.Emit(group, collide: true);
        }

        // ── 6. водопад «Девичьи Косы» ─────────────────────────────────────────────────────────────────────
        /// <summary>The Девичьи Косы, «the maiden's braids»: twenty-five metres of the Чыранбаши-Су — the stream that
        /// drains the Гара-Баши glacier — coming down a rock slab in a hundred separate threads, narrow at the top
        /// and spread into a fan fifteen metres wide at the bottom, with a shallow grotto behind the sheet. Signposted
        /// at 2 800 m; on our height field the point is at 3 060 m, because a 30 m raster fills in the gorge it is
        /// cut into.
        ///
        /// Which is also why it is built the way it is. A prefab cannot cut a hole in a height field
        /// (<see cref="ElbrusAscent"/> says the same about its crevasses), so the fall is built UPWARD: a band of
        /// resistant rock standing proud of the hillside, with a bench behind it for the stream to arrive along, a
        /// slab in front of it steep enough to be a waterfall, and the pool at the bottom sitting on the real ground.
        /// The drop is measured, not assumed: the foot is found by walking down the fall line until the ground is
        /// twenty-five metres below the crest.
        ///
        /// No snow anywhere near it. It is at 3 060 m and the map's firn line starts at 3 250 m — in July this place
        /// is wet rock, moss and alpine grass, and it must look like it.</summary>
        static void DevichiKosy(Transform root, (float x, float z)[] toFalls, (float x, float z)[] fallsToObs)
        {
            var group = new GameObject("DevichiKosy").transform; group.SetParent(root, false);
            var water = new Batch("Falls", 120f, Water, Foam, RockWet, Rock);
            var kit = new Kit(120f);

            var (fx, fz, _) = dem.Fall(Falls.x, Falls.y, 25f);
            var down = new Vector3(fx, 0, fz).normalized;
            var across = new Vector3(down.z, 0, -down.x);
            var lip = new Vector3(Falls.x, Ground(Falls.x, Falls.y), Falls.y);

            // the crest: a rock bar six metres proud of the slope, which is what makes the slab steep enough to be a
            // fall and not a rapid
            const float CrestRise = 6f;
            float crestY = lip.y + CrestRise;

            // the foot: walk down the fall line until the ground is FallHeightM below the crest
            float run = 12f;
            for (float d = 12f; d <= 140f; d += 2f)
            {
                var q = lip + down * d;
                run = d;
                if (Ground(q.x, q.z) <= crestY - FallHeightM) break;
            }
            var foot = lip + down * run;
            float poolY = Ground(foot.x, foot.z) + .35f;
            fallDrop = crestY - poolY;

            // ── the slab the water comes down ────────────────────────────────────────────────────────────
            // v = 0 at the crest, 1 at the pool; the run stretches out towards the bottom, so the top of the slab is
            // the steep part, which is where the braids are thin and white
            // The slab is written as the hillside PLUS a lift, never as a line drawn between two heights: a straight
            // line from the crest to the pool dives under the ground in the middle of a slope this steep and has to be
            // clamped back out of it, and the clamp is visible as a flat spot. Written this way the band is above the
            // ground everywhere by construction. The lift dies off as √v, so almost all of the extra drop happens in
            // the first few metres — the fall is steep and narrow at the top and spreads into the fan below, which is
            // what the braids are named for.
            Vector3 Slab(float u, float v, float lift)
            {
                float l = run * (v * .55f + v * v * .45f);
                float half = Mathf.Lerp(4.5f, FallWidthM * .72f, v) * (1f + .35f * Mathf.Abs(u) * Mathf.Abs(u));
                var c = lip + down * l + across * (u * half);
                float rise = CrestRise * (1f - Mathf.Sqrt(Mathf.Clamp01(v)));
                // the flanks of the band fall away to the hillside, so it is a rib of rock and not a wall
                rise *= 1f - Mathf.Clamp01(Mathf.Abs(u) * Mathf.Abs(u) * 1.1f);
                float y = Ground(c.x, c.z) + rise + .14f;
                return new Vector3(c.x, y + lift, c.z);
            }
            const int Uu = 12, Vv = 14;
            for (int i = 0; i < Uu; i++)
                for (int j = 0; j < Vv; j++)
                {
                    float u0 = -1f + 2f * i / Uu, u1 = -1f + 2f * (i + 1) / Uu;
                    float v0 = j / (float)Vv, v1 = (j + 1) / (float)Vv;
                    var a = Slab(u0, v0, 0f); var b = Slab(u1, v0, 0f);
                    var c = Slab(u1, v1, 0f); var d = Slab(u0, v1, 0f);
                    var mb = water.At(a);
                    // wet rock in the middle of the band where the spray reaches, dry lichen rock at the edges
                    int sub = Mathf.Abs(u0) < .55f ? 2 : 3;
                    mb.Quad(sub, a, b, c, d, new Vector2(0, v0 * 4f), new Vector2(1, v0 * 4f), new Vector2(1, v1 * 4f), new Vector2(0, v1 * 4f), true);
                }

            // ── the braids ───────────────────────────────────────────────────────────────────────────────
            // one sheet of green water behind, and eleven white threads over it that splay as they fall — which is
            // the whole reason the thing is called what it is called
            for (int j = 0; j < Vv; j++)
            {
                float v0 = j / (float)Vv, v1 = (j + 1) / (float)Vv;
                var a = Slab(-.30f, v0, .06f); var b = Slab(.30f, v0, .06f);
                var c = Slab(.30f, v1, .06f); var d = Slab(-.30f, v1, .06f);
                water.At(a).Quad(0, a, b, c, d, new Vector2(0, v0 * 6f), new Vector2(1, v0 * 6f), new Vector2(1, v1 * 6f), new Vector2(0, v1 * 6f), true);
            }
            var rng = new System.Random(9401);
            for (int k = 0; k < 11; k++)
            {
                float u0 = -.26f + k * .052f;
                float spread = .5f + (float)rng.NextDouble() * 1.5f;
                float wob = (float)rng.NextDouble() * 6.28f;
                for (int j = 0; j < Vv; j++)
                {
                    float v0 = j / (float)Vv, v1 = (j + 1) / (float)Vv;
                    float w0 = .022f + v0 * .03f, w1 = .022f + v1 * .03f;
                    float c0 = u0 * (1f + v0 * spread) + Mathf.Sin(v0 * 9f + wob) * .02f;
                    float c1 = u0 * (1f + v1 * spread) + Mathf.Sin(v1 * 9f + wob) * .02f;
                    var a = Slab(c0 - w0, v0, .13f); var b = Slab(c0 + w0, v0, .13f);
                    var c = Slab(c1 + w1, v1, .13f); var d = Slab(c1 - w1, v1, .13f);
                    water.At(a).Quad(1, a, b, c, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
            }

            // ── the grotto behind the sheet, the pool and the spray ──────────────────────────────────────
            {
                var alcove = Slab(0f, .96f, 0f) - down * 2.4f;
                var mb = water.At(alcove);
                mb.Box(2, alcove + Vector3.up * 1.4f, new Vector3(5.5f, 2.8f, 2.6f),
                    Quaternion.LookRotation(down, Vector3.up), 1.4f);
            }
            {
                var pool = new Vector3(foot.x, poolY, foot.z) + down * 3.5f;
                var mb = water.At(pool);
                const int Sect = 16;
                for (int i = 0; i < Sect; i++)
                {
                    float a0 = i / (float)Sect * Mathf.PI * 2, a1 = (i + 1) / (float)Sect * Mathf.PI * 2;
                    var q0 = pool + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 5.5f;
                    var q1 = pool + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 5.5f;
                    q0.y = Mathf.Max(poolY, Ground(q0.x, q0.z) + .1f);
                    q1.y = Mathf.Max(poolY, Ground(q1.x, q1.z) + .1f);
                    mb.Quad(0, pool, q0, q1, q1, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                }
                // the foam ring where the fall lands, and the boulders the water has rolled out of the way
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f;
                    var q = pool + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (2f + (float)rng.NextDouble() * 1.4f);
                    q.y = poolY + .1f;
                    mb.Quad(1, q + new Vector3(-1.1f, 0, -1.1f), q + new Vector3(1.1f, 0, -1.1f),
                        q + new Vector3(1.1f, .04f, 1.1f), q + new Vector3(-1.1f, .04f, 1.1f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                }
                for (int i = 0; i < 14; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = 5.5f + (float)rng.NextDouble() * 7f;
                    var q = pool + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                    q.y = Ground(q.x, q.z);
                    float sz = .35f + (float)rng.NextDouble() * .9f;
                    water.At(q).Box(i % 3 == 0 ? 2 : 3, q + Vector3.up * (sz * .4f), new Vector3(sz * 2f, sz * 1.1f, sz * 1.7f),
                        Quaternion.Euler((float)rng.NextDouble() * 18f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 18f), 1.3f);
                }
                // the spray: four low cards standing in the plunge, lit like everything else here
                for (int i = 0; i < 4; i++)
                {
                    var q = pool - down * (1.2f + i * .5f) + across * ((float)rng.NextDouble() * 4f - 2f);
                    q.y = poolY + .1f;
                    Card(water.At(q), 1, q + Vector3.up * (1.2f + i * .35f), Quaternion.LookRotation(-down, Vector3.up), 5f + i, 2.4f + i * .7f);
                }
            }

            // ── the stream, above the fall and below it ──────────────────────────────────────────────────
            Brook(water, lip - down * 4f, -1f, 90f, 9501);
            Brook(water, new Vector3(foot.x, 0, foot.z) + down * 9f, 1f, 150f, 9502);

            // ── what people leave at a waterfall ─────────────────────────────────────────────────────────
            {
                // the memorial plate on the rock, which is really there, and the flat stone everybody sits on
                var seat = new Vector3(foot.x, 0, foot.z) - down * 4f + across * 11f;
                seat.y = Ground(seat.x, seat.z);
                var mb = kit.Shell.At(seat);
                mb.Box(4, seat + Vector3.up * 1.3f, new Vector3(3.2f, 2.6f, 2.2f),
                    Quaternion.Euler(6f, Mathf.Atan2(-down.x, -down.z) * Mathf.Rad2Deg + 20f, 4f), 1.3f);
                var plate = seat + Vector3.up * 1.7f - down * 1.15f;
                kit.Metal.At(seat).Box(1, plate, new Vector3(.9f, .6f, .03f), Quaternion.LookRotation(-down, Vector3.up), 1f);
                Label(group, "здесь погиб\nтоварищ\n1979", plate - down * .03f, Mathf.Atan2(-down.x, -down.z) * Mathf.Rad2Deg, .055f, new Color(.2f, .2f, .22f));
                var bench = seat + across * 4f; bench.y = Ground(bench.x, bench.z);
                mb.Box(4, bench + Vector3.up * .28f, new Vector3(2.6f, .55f, 1.5f), Quaternion.Euler(3f, 40f, 2f), 1.3f);
                Cairn(kit.Shell, 4, bench + across * 2.4f + Vector3.up * (Ground(bench.x + across.x * 2.4f, bench.z + across.z * 2.4f) - bench.y), .5f, rng);
            }

            water.Emit(group, shadows: false);
            kit.Emit(group);
        }

        /// <summary>A mountain brook following the fall line: a thread of water two thirds of a metre wide with wet
        /// stones along it, laid on the ground the whole way. <paramref name="dir"/> is +1 downhill and −1 up, so the
        /// same routine draws the stream that feeds the fall and the one that leaves it.</summary>
        static void Brook(Batch water, Vector3 start, float dir, float length, int seed)
        {
            var rng = new System.Random(seed);
            var p = new Vector3(start.x, Ground(start.x, start.z), start.z);
            float run = 0f, phase = (float)rng.NextDouble() * 6.28f;
            Vector3 prevL = p, prevR = p;
            bool first = true;
            while (run < length)
            {
                var (dx, dz, slope) = dem.Fall(p.x, p.z, 12f);
                if (slope < .15f) break;
                var step = new Vector3(dx, 0, dz).normalized * dir;
                var side = new Vector3(step.z, 0, -step.x);
                var next = p + step * 3.5f + side * (Mathf.Sin(run * .08f + phase) * .8f);
                next.y = Ground(next.x, next.z);
                float w = .35f + .15f * Mathf.Sin(run * .19f + phase);
                var l = next - side * w; l.y = Ground(l.x, l.z) + .04f;
                var r = next + side * w; r.y = Ground(r.x, r.z) + .04f;
                if (!first)
                {
                    var mb = water.At(next);
                    mb.Quad(0, prevL, l, r, prevR, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, false, Vector3.up);
                    if (rng.NextDouble() < .3)
                    {
                        var q = next + side * (w + .5f) * (rng.Next(2) * 2 - 1); q.y = Ground(q.x, q.z);
                        float sz = .16f + (float)rng.NextDouble() * .3f;
                        mb.Box(2, q + Vector3.up * (sz * .4f), new Vector3(sz * 2f, sz, sz * 1.6f),
                            Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), 1.4f);
                    }
                }
                prevL = l; prevR = r; p = next; run += 3.5f; first = false;
            }
        }
    }
}
