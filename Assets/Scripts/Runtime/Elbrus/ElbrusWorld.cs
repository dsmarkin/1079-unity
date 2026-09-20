using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Builds the Elbrus location at runtime from <see cref="Elbrus"/> and the DEM: the six ropeways with their
    /// towers, ropes and cars, the terminals, the barrel camp and the shelters, the snow-cats and the wands of the
    /// summit route. <see cref="ElbrusDressing"/> then fills the same ground with everything smaller.
    /// Prefabs come from Assets/Generated/World/Prefabs/Elbrus (ElbrusFactory, ElbrusProps) through WorldAssets.
    /// Nothing here is placed by a single height sample: buildings are sat on their whole footprint and, where the
    /// ground falls away under them, stood on a plinth (see <see cref="Seat"/>).</summary>
    public static class ElbrusWorld
    {
        public static readonly List<RopewayRig> Lines = new List<RopewayRig>();
        public static readonly List<RatrakRide> Ratraks = new List<RatrakRide>();
        /// <summary>Every station hall the ropeway pass put on the ground — the dressing hangs antennas, wind socks and
        /// banners on them, and keeps its own props off them.</summary>
        internal static readonly List<Hall> Halls = new List<Hall>();
        static HeightField dem;
        static readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>();
        /// <summary>Ground already used, as turned rectangles, so nothing is dropped inside a wall or on top of another
        /// prop — and so a long thin building does not forbid the whole circle around it.</summary>
        static readonly List<(float x, float z, float hx, float hz, float cos, float sin)> taken =
            new List<(float x, float z, float hx, float hz, float cos, float sin)>();

        /// <summary>A station building standing on the slope: where its floor is and which way it looks.</summary>
        internal readonly struct Hall
        {
            public readonly string Prefab;
            public readonly Vector3 Base;
            public readonly float Yaw;
            public Hall(string prefab, Vector3 basePoint, float yaw) { Prefab = prefab; Base = basePoint; Yaw = yaw; }
        }

        /// <summary>How the object meets the ground.</summary>
        internal enum Sit
        {
            /// <summary>A building: stands part of the way up the drop under its footprint (cut into the hill uphill,
            /// filled downhill) and gets a plinth when the drop is worth closing.</summary>
            Pad,
            /// <summary>A ropeway terminal: keeps the height of the centre sample, because the cars' platform level is
            /// measured from that same point, and closes the gap under it with a plinth.</summary>
            Anchor,
            /// <summary>A vehicle or a loose prop: sits at the middle of its footprint, never on a plinth.</summary>
            Flat,
            /// <summary>Anything on wheels or runners, and the duckboards: laid on the slope itself, tilted to it, so a
            /// car parked across a hillside leans the way a car does.</summary>
            Lie,
        }

        /// <summary>Drop across a footprint that is worth a plinth (metres).</summary>
        const float PlinthStep = .4f;

        static GameObject Load(string name)
        {
            if (loaded.TryGetValue(name, out var cached)) return cached;
            var p = WorldAssets.Load<GameObject>("Prefabs/Elbrus/" + name);
            if (p == null) Debug.LogWarning("1079 Эльбрус: нет префаба " + name);
            loaded[name] = p;
            return p;
        }

        internal static float Ground(float x, float z) => dem.Sample(x, z);

        /// <summary>Compass bearing (degrees) from a to b in the game's frame (z = north).</summary>
        static float Yaw(float ax, float az, float bx, float bz) => Mathf.Atan2(bx - ax, bz - az) * Mathf.Rad2Deg;

        public static void Build(HeightField heights)
        {
            dem = heights;
            Lines.Clear(); Ratraks.Clear(); Halls.Clear(); taken.Clear(); loaded.Clear();
            var root = new GameObject("Elbrus").transform;

            Ropeways(root);
            Azau(root);
            Stations(root);
            Camp(root);
            RouteFurniture(root);
            Valley(root);
            ElbrusDressing.Build(root);
            // the buildings never move, so their meshes are merged by material: two hundred and fifty prefabs of
            // thirty-odd renderers each were fifteen hundred draws with a state change on most of them, and facing
            // the village cost seventy per cent more than facing away from it (Scenery). The ropeways, the
            // snow-cats and the parties are deliberately left out — they move, and a batched mesh cannot.
            // «Camp» is not in this list: the snow-cats park in it and they drive away.
            foreach (var name in new[] { "Azau", "Stations" })
            {
                var group = root.Find(name);
                if (group != null) Scenery.Batch(group);
            }
            root.gameObject.AddComponent<ElbrusRides>();
            CafeService.Create(root);
            RentalService.Create(root);
            // the three counters the markers inside the buildings were put there for, the snow-cat's own little
            // counter, and the other parties on the slope (docs/ELBRUS.md, «Как это вызывать из рантайма»)
            RescueDesk.Create(root);
            WeatherBoards.Create(root);
            LodgeService.Create(root);
            RatrakService.Create(root);
            Parties.Create(root);
        }

        // ── putting things on a slope ─────────────────────────────────────────────────────────────────────
        /// <summary>Ground under a footprint of <paramref name="size"/> metres (x across, z along) turned by
        /// <paramref name="yaw"/>: the lowest and the highest of the nine samples (corners, edge middles, centre).
        /// This is what tells a building whether it would hang in the air on one corner.</summary>
        internal static (float lo, float hi) Pad(float x, float z, float yaw, Vector2 size)
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

        /// <summary>Sits a prefab on the ground by its whole footprint instead of by one point, and slips a concrete
        /// plinth under it when the ground falls away by more than <see cref="PlinthStep"/> — the way a building is
        /// actually put on a slope. <paramref name="sink"/> is how far the prefab's own slab reaches below its pivot
        /// (the terminals bury a 0.9 m apron), so the plinth meets the underside of the building and not the pivot.</summary>
        internal static GameObject Seat(GameObject prefab, Transform parent, float x, float z, float yaw, Vector2 size,
            float dy = 0f, Sit sit = Sit.Pad, float sink = 0f)
        {
            if (prefab == null) return null;
            var (lo, hi) = Pad(x, z, yaw, size);
            float drop = hi - lo;
            float baseY = sit == Sit.Anchor || sit == Sit.Lie ? Ground(x, z)
                : sit == Sit.Flat ? (lo + hi) * .5f - .04f
                // a building stands on the HIGHEST corner of its footprint, with a plinth filling the step below it:
                // seated any lower, the hillside pokes up through the floor and you stand on grass indoors
                : hi + .05f;
            var rot = sit == Sit.Lie ? Lay(x, z, yaw) : Quaternion.Euler(0, yaw, 0);
            var go = Object.Instantiate(prefab, new Vector3(x, baseY + dy, z), rot, parent);
            if (sit == Sit.Pad || sit == Sit.Anchor)
                if (drop > PlinthStep) PlinthUnder(parent, x, z, yaw, size, baseY + dy - sink, lo);
            Reserve(x, z, size, yaw);
            return go;
        }

        internal static GameObject Seat(string prefab, Transform parent, float x, float z, float yaw,
            float dy = 0f, Sit sit = Sit.Pad, float sink = 0f)
            => Seat(Load(prefab), parent, x, z, yaw, Footprint(prefab),
                dy, sit == Sit.Flat && Lies(prefab) ? Sit.Lie : sit, sink);

        /// <summary>What lies on the slope instead of standing level on it: anything on wheels or on runners, the
        /// duckboards, the rubble, the wands. A post, a bin or a crate stays upright, the way it does in life.</summary>
        static bool Lies(string prefab)
        {
            switch (prefab)
            {
                case "Elb_Car_White":
                case "Elb_Car_Silver":
                case "Elb_Car_Red":
                case "Elb_Car_Suv":
                case "Elb_Van":
                case "Elb_Bus":
                case "Elb_Trailer":
                case "Elb_Ratrak":
                case "Elb_Snowmobile":
                case "Elb_Sled":
                case "Elb_Deck":
                case "Elb_Rubble":
                case "Elb_Wand": return true;
                default: return false;
            }
        }

        /// <summary>The rotation of something lying on the slope: <paramref name="yaw"/> kept as the heading, but the
        /// object turned onto the ground plane so it leans downhill instead of standing on two wheels.</summary>
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

        /// <summary>The pad a building stands on where the hill falls away under it: scaled to the footprint and to the
        /// gap it has to close, buried 0.3 m into the ground so no seam shows on the uphill side.</summary>
        static void PlinthUnder(Transform parent, float x, float z, float yaw, Vector2 size, float under, float lo)
        {
            float depth = under - (lo - .3f);
            if (depth < .35f) return;
            var prefab = Load("Elb_Plinth");
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, new Vector3(x, under + .03f, z), Quaternion.Euler(0, yaw, 0), parent);
            go.name = "Elb_Plinth";
            go.transform.localScale = new Vector3(size.x + .8f, Mathf.Min(depth, 7f), size.y + .8f);
        }

        static void Reserve(float x, float z, Vector2 size, float yaw)
        {
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            taken.Add((x, z, Mathf.Max(.2f, size.x * .5f), Mathf.Max(.2f, size.y * .5f), c, s));
        }

        internal static void Reserve(float x, float z, float radius) => Reserve(x, z, new Vector2(radius * 2f, radius * 2f), 0f);

        /// <summary>Puts a prefab at an exact point instead of on the ground — for what stands on something else: a
        /// snow-cat on its trailer, an antenna on a station roof.</summary>
        internal static GameObject At(string prefab, Transform parent, Vector3 pos, float yaw)
            => At(prefab, parent, pos, Quaternion.Euler(0, yaw, 0));

        internal static GameObject At(string prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            var p = Load(prefab);
            if (p == null) return null;
            return Object.Instantiate(p, pos, rot, parent);
        }

        /// <summary>How much room a car passing on a ropeway leaves over this point, or a large number where no line
        /// runs near it. Nothing tall goes under a rope: the cabins come down to three and a half metres.</summary>
        internal static float Headroom(float x, float z, float margin)
        {
            float best = float.MaxValue;
            float ground = Ground(x, z);
            for (int n = 0; n < Lines.Count; n++)
            {
                var rig = Lines[n];
                if (rig == null || rig.Line == null) continue;
                var rope = rig.Line.Rope;
                float hang = Ropeway.Hang(rig.Spec.Kind);
                for (int i = 1; i < rope.Length; i++)
                {
                    float t;
                    float d = SegmentDistance(x, z, rope[i - 1].x, rope[i - 1].z, rope[i].x, rope[i].z, out t);
                    if (d > margin + Ropeway.Gauge) continue;
                    float y = Mathf.Lerp(rope[i - 1].y, rope[i].y, t);
                    best = Mathf.Min(best, y - hang - ground);
                }
            }
            return best;
        }

        static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz, out float t)
        {
            float dx = bx - ax, dz = bz - az;
            float len = dx * dx + dz * dz;
            t = len < 1e-4f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len);
            float cx = ax + dx * t - px, cz = az + dz * t - pz;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        /// <summary>Is this a spot a prop can stand on: on the map, clear of everything already placed, and with enough
        /// room under the ropeways for something <paramref name="height"/> metres tall.</summary>
        internal static bool Free(float x, float z, float radius, float height = 2f)
        {
            if (!Elbrus.Inside(x, z, 40f)) return false;
            // a mast needs more room beside a line than a bin does: the cabins swing, and a nine-metre floodlight
            // beside a station door is in the way even when the rope misses it
            if (height + 1.2f > Headroom(x, z, radius + 3f + height * .35f)) return false;
            for (int i = 0; i < taken.Count; i++)
            {
                var t = taken[i];
                // distance from the point to the turned rectangle: measure in the rectangle's own frame
                float dx = x - t.x, dz = z - t.z;
                float lx = dx * t.cos - dz * t.sin, lz = dx * t.sin + dz * t.cos;
                float qx = Mathf.Max(0f, Mathf.Abs(lx) - t.hx), qz = Mathf.Max(0f, Mathf.Abs(lz) - t.hz);
                if (qx * qx + qz * qz < radius * radius) return false;
            }
            return true;
        }

        /// <summary>Drops a prop where it fits and says whether it went down: the dressing calls this a few hundred
        /// times and lets the ground decide what survives.</summary>
        internal static GameObject Prop(string prefab, Transform parent, float x, float z, float yaw,
            float height = 2f, float dy = 0f, Sit sit = Sit.Flat, float radius = -1f)
        {
            var size = Footprint(prefab);
            if (radius < 0) radius = Mathf.Max(size.x, size.y) * .5f;
            if (!Free(x, z, radius, height)) return null;
            return Seat(Load(prefab), parent, x, z, yaw, size, dy, sit == Sit.Flat && Lies(prefab) ? Sit.Lie : sit);
        }

        /// <summary>Puts a prop down whether or not the ground is spoken for — for rows laid out by hand (a fence, a
        /// queue barrier, the kiosks of the market), where the spacing is the point.</summary>
        internal static GameObject Fixture(string prefab, Transform parent, float x, float z, float yaw,
            float dy = 0f, Sit sit = Sit.Pad, float radius = -1f)
        {
            var size = Footprint(prefab);
            var go = Seat(Load(prefab), parent, x, z, yaw, size, dy, sit == Sit.Flat && Lies(prefab) ? Sit.Lie : sit);
            if (go != null && radius >= 0)
            {
                taken.RemoveAt(taken.Count - 1);
                Reserve(x, z, radius);
            }
            return go;
        }

        /// <summary>A run of sections end to end along the contour through a point — railings, chains and snow fences
        /// stand like this, across the fall line, each section turned to the ground it lands on.</summary>
        internal static void ContourRun(string prefab, Transform parent, float x, float z, int count, float pitch, Sit sit = Sit.Flat)
        {
            float d = FaceDownhill(x, z);
            float ux = Mathf.Cos(d * Mathf.Deg2Rad), uz = -Mathf.Sin(d * Mathf.Deg2Rad);
            float x0 = x - ux * pitch * (count - 1) * .5f, z0 = z - uz * pitch * (count - 1) * .5f;
            for (int i = 0; i < count; i++)
            {
                float px = x0 + ux * pitch * i, pz = z0 + uz * pitch * i;
                Fixture(prefab, parent, px, pz, AlongContour(px, pz), 0f, sit, pitch * .45f);
            }
        }

        /// <summary>Yaw that turns a prefab's +Z straight down the fall line.</summary>
        internal static float FaceDownhill(float x, float z, float span = 14f)
        {
            var (dx, dz, _) = dem.Fall(x, z, span);
            if (dx * dx + dz * dz < 1e-6f) return 0f;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>Yaw that turns a prefab's +Z along the contour — how a car is parked or a hut set on a slope.</summary>
        internal static float FaceContour(float x, float z, float span = 14f) => FaceDownhill(x, z, span) + 90f;

        /// <summary>How steep the ground is here, in degrees.</summary>
        internal static float Slope(float x, float z, float span = 12f) => dem.Fall(x, z, span).slopeDeg;

        /// <summary>Yaw that lays a prefab's +X along the contour and its face down the slope — how a railing, a fence
        /// or a snow fence stands. (A prefab turned by yaw θ has +Z on bearing θ and +X on bearing θ + 90.)</summary>
        internal static float AlongContour(float x, float z, float span = 14f) => FaceDownhill(x, z, span);

        /// <summary>Ground footprint of every prefab the runtime puts down (x across, z along), the concrete apron
        /// included, because the apron is what has to lie flat. Kept here rather than in the factories so one table
        /// answers for both of them.</summary>
        internal static Vector2 Footprint(string prefab)
        {
            switch (prefab)
            {
                // ── ropeway terminals (hall + apron) ──
                case "Elb_Terminal_Azau": return new Vector2(18f, 28f);
                case "Elb_Terminal_Krugozor": return new Vector2(15f, 23f);
                case "Elb_Terminal_Mir": return new Vector2(16f, 26f);
                case "Elb_Terminal_Garabashi": return new Vector2(15f, 24f);
                case "Elb_Terminal_Small": return new Vector2(12f, 18f);
                case "Elb_Terminal_Old": return new Vector2(19f, 31f);
                // ── buildings ──
                case "Elb_Hotel": return new Vector2(11.8f, 18.8f);
                case "Elb_Chalet": return new Vector2(8.8f, 12.8f);
                // the café is a building you can walk into now (ElbrusFactory.Cafe): W × L plus a 3 m terrace at +Z
                case "Elb_Cafe": return new Vector2(10f, 14f);
                case "Elb_Cafe_Hall": return new Vector2(13f, 16f);
                case "Elb_Booth": return new Vector2(6.5f, 4.2f);
                case "Elb_Kiosk": return new Vector2(3.3f, 2.7f);
                case "Elb_Toilet": return new Vector2(4.8f, 2.8f);
                case "Elb_Monument": return new Vector2(4.4f, 4.4f);
                case "Elb_Exhibit_Wagon": return new Vector2(3.4f, 6.4f);
                case "Elb_Foundation": return new Vector2(9f, 6f);
                case "Elb_Hut_Small": return new Vector2(3.9f, 5f);
                case "Elb_Hut_Diesel": return new Vector2(8.2f, 11.6f);
                case "Elb_Hut_Capsule": return new Vector2(4.4f, 12f);
                case "Elb_Barrel": return new Vector2(2.7f, 6.4f);
                // the two buildings of ElbrusLodge you walk into: the base on the meadow (hall plus its porch) and
                // the приют of the national park
                case "Elb_Base_Azau": return new Vector2(13.4f, 13.4f);
                case "Elb_Hut_Natspark": return new Vector2(9f, 14.6f);
                case "Elb_Genset": return new Vector2(2.6f, 3.4f);
                // ── street furniture ──
                case "Elb_Rail": return new Vector2(8f, .3f);
                case "Elb_Bench": return new Vector2(2.1f, .6f);
                case "Elb_Lamp": return new Vector2(.5f, 1.3f);
                case "Elb_Postbox": return new Vector2(.5f, .4f);
                case "Elb_Sign": return new Vector2(2.4f, .3f);
                case "Elb_Wand": return new Vector2(.2f, .2f);
                // ── vehicles ──
                case "Elb_Ratrak": return new Vector2(4.6f, 6.6f);
                case "Elb_Snowmobile": return new Vector2(1f, 3.2f);
                case "Elb_Car_White":
                case "Elb_Car_Silver":
                case "Elb_Car_Red": return new Vector2(1.8f, 4.4f);
                case "Elb_Car_Suv": return new Vector2(2f, 4.7f);
                case "Elb_Van": return new Vector2(2.1f, 5.7f);
                case "Elb_Bus": return new Vector2(2.6f, 9.8f);
                case "Elb_Trailer": return new Vector2(2.8f, 7.4f);
                case "Elb_Sled": return new Vector2(1f, 2.2f);
                // ── the small stuff ──
                case "Elb_Plinth": return new Vector2(1f, 1f);
                case "Elb_Fence": return new Vector2(4f, .3f);
                case "Elb_QueueRail": return new Vector2(2.5f, .5f);
                case "Elb_SnowFence": return new Vector2(4f, 1.3f);
                case "Elb_Chain": return new Vector2(3.1f, .3f);
                case "Elb_Banner": return new Vector2(7f, .4f);
                case "Elb_Barrier": return new Vector2(5.2f, .8f);
                case "Elb_Bin": return new Vector2(.55f, .55f);
                case "Elb_Bin_Barrel": return new Vector2(.7f, .7f);
                case "Elb_Drum": return new Vector2(.65f, .65f);
                case "Elb_InfoBoard": return new Vector2(3f, .6f);
                case "Elb_FlagPole": return new Vector2(.7f, .7f);
                case "Elb_Turnstile": return new Vector2(1.7f, 1f);
                case "Elb_RentStand": return new Vector2(3f, 1.3f);
                case "Elb_SkiRack": return new Vector2(2.4f, .9f);
                case "Elb_Woodpile": return new Vector2(3.5f, 1.4f);
                case "Elb_SignPost": return new Vector2(1.8f, 1.8f);
                case "Elb_Transformer": return new Vector2(3.4f, 2.9f);
                case "Elb_Container": return new Vector2(2.5f, 6.1f);
                case "Elb_Floodlight": return new Vector2(.8f, .8f);
                case "Elb_CrateStack": return new Vector2(2.4f, 1.6f);
                case "Elb_GasCage": return new Vector2(1.9f, 1.1f);
                case "Elb_Deck": return new Vector2(3f, 4f);
                case "Elb_Skip": return new Vector2(1.4f, 1.3f);
                case "Elb_Windsock": return new Vector2(.6f, .6f);
                case "Elb_Antenna": return new Vector2(1.5f, 1.5f);
                case "Elb_Pylon": return new Vector2(2.2f, .6f);
                case "Elb_Rubble": return new Vector2(3f, 2.4f);
                default: return new Vector2(1.5f, 1.5f);
            }
        }

        // ── ropeways ──────────────────────────────────────────────────────────────────────────────────────
        static void Ropeways(Transform root)
        {
            var terminals = new List<(float x, float z)>();
            var ropeRoot = new GameObject("Ropeways").transform; ropeRoot.SetParent(root, false);
            foreach (var spec in Elbrus.Ropeways)
            {
                var line = new Ropeway(spec, Ground);
                var rig = RopewayRig.Create(spec, line, ropeRoot);
                Lines.Add(rig);

                // one station building per distinct terminal (a jig-back and the gondola beside it have their own halls)
                for (int end = 0; end < 2; end++)
                {
                    var t = end == 0 ? spec.Towers[0] : spec.Towers[spec.Towers.Length - 1];
                    bool seen = false;
                    foreach (var q in terminals) if (Elbrus.Distance(q.x, q.z, t.x, t.z) < 55f) seen = true;
                    if (seen) continue;
                    terminals.Add(t);
                    var other = end == 0 ? spec.Towers[1] : spec.Towers[spec.Towers.Length - 2];
                    float yaw = end == 0 ? Yaw(t.x, t.z, other.x, other.z) : Yaw(other.x, other.z, t.x, t.z);
                    string prefab = TerminalPrefab(spec, end == 0);
                    // the terminal keeps the height of its own centre — the cars' platform level is measured from it —
                    // and the plinth closes whatever the slope leaves open under the apron
                    Seat(prefab, ropeRoot, t.x, t.z, yaw, -.9f, Sit.Anchor, .9f);
                    Halls.Add(new Hall(prefab, new Vector3(t.x, Ground(t.x, t.z) - .9f, t.z), yaw));
                }
            }
        }

        static string TerminalPrefab(RopewaySpec spec, bool bottom)
        {
            if (spec.Kind == RopewayKind.Chair) return "Elb_Terminal_Small";
            // the 1969 jig-back still works out of its own concrete halls, beside the modern gondola terminals
            if (spec.Kind == RopewayKind.Pendulum) return "Elb_Terminal_Old";
            string id = bottom ? spec.BottomId : spec.TopId;
            switch (id)
            {
                case "azau": return "Elb_Terminal_Azau";
                case "krugozor": return "Elb_Terminal_Krugozor";
                case "mir": return "Elb_Terminal_Mir";
                case "garabashi": return "Elb_Terminal_Garabashi";
                default: return "Elb_Terminal_Small";
            }
        }

        /// <summary>The name board of a station, where a visitor actually meets it: on the flat beside the hall, its
        /// face turned down the slope so it is read by whoever is coming up to it.</summary>
        static void StationSign(Transform parent, Elbrus.Poi p, float x, float z)
        {
            var sign = Seat("Elb_Sign", parent, x, z, FaceDownhill(x, z) + 180f, 0f, Sit.Flat);
            var labels = sign != null ? sign.GetComponentsInChildren<TextMesh>() : null;
            if (labels != null) foreach (var l in labels) l.text = p.Label.Replace(" · ", "\n");
        }

        // ── Azau: the village at the bottom ───────────────────────────────────────────────────────────────
        /// <summary>The local frame of the Azau meadow. The gondola terminal is the origin; <b>+v runs east-south-east
        /// down the glade towards Terskol</b> — the old pendulum hall of 1969 stands 128 m along it and the road comes up
        /// it — and +u crosses the glade to the south-south-west. A building turned by 0 faces +v, by +90 faces +u.
        /// (The ground: flat at u −40…+20 for the first 140 m, falling away east and south past that.)</summary>
        internal const float AzauYaw = 114f;

        internal static (float x, float z) AzauAt(float u, float v)
        {
            float ca = Mathf.Cos(AzauYaw * Mathf.Deg2Rad), sa = Mathf.Sin(AzauYaw * Mathf.Deg2Rad);
            var a = Elbrus.Azau;
            return (a.X + u * ca + v * sa, a.Z - u * sa + v * ca);
        }

        /// <summary>Поляна Азау, 2 350 м: the square between the two bottom stations — the gondola terminal at the top of
        /// the glade, the concrete hall of the 1969 jig-back 128 m down it — with the ticket offices in front of them, two
        /// rows of souvenir stalls along the path between them, cafés and shashlyk places on both sides, hotels and
        /// chalets around the edge of the meadow and the car park at the bottom, where the road from Terskol ends.
        /// Laid out from photographs of the real place against the DEM, see docs/ELBRUS.md.</summary>
        static void Azau(Transform root)
        {
            var village = new GameObject("Azau").transform; village.SetParent(root, false);

            void Put(string prefab, float u, float v, float turn)
            {
                var (x, z) = AzauAt(u, v);
                Fixture(prefab, village, x, z, AzauYaw + turn);
            }

            // the ticket offices, both facing down the glade at the people walking up from the car park.
            // Elbrus.Start puts the visitor at about (u 18, v 34), so the second one stands clear of that spot.
            Put("Elb_Booth", -22f, 28f, 0f);
            Put("Elb_Booth", 25f, 36f, 0f);
            // the souvenir market: two rows of stalls along the path between the stations, counters towards the path
            for (int k = 0; k < 9; k++) Put("Elb_Kiosk", -20f, 40f + k * 6.2f, -90f);
            for (int k = 0; k < 7; k++) Put("Elb_Kiosk", 16f, 44f + k * 6.2f, 90f);
            // cafés and shashlyk places: two on the upper side of the square, one on the lower, propped up on its plinth
            Put("Elb_Cafe", -36f, 52f, 90f);
            Put("Elb_Cafe", -36f, 86f, 90f);
            Put("Elb_Cafe", 34f, 64f, -90f);
            Put("Elb_Toilet", 22f, 24f, -90f);
            // the base of the rescue service and the hire shop, on the upper corner of the square where the visitor
            // lands (Elbrus.Start is about u 18, v 34): the one building the ascent is actually prepared in, its
            // porch turned towards the path (ElbrusLodge.BaseAzau)
            Put("Elb_Base_Azau", -34f, 26f, -90f);
            // hotels along the meadow, chalets under the pines
            Put("Elb_Hotel", -56f, 40f, 90f);
            Put("Elb_Hotel", -58f, 112f, 90f);
            Put("Elb_Hotel", -64f, 176f, 84f);
            Put("Elb_Chalet", -72f, 70f, 90f);
            Put("Elb_Chalet", -70f, 132f, 80f);
            Put("Elb_Chalet", 30f, 40f, -90f);
            var (signX, signZ) = AzauAt(-6f, 34f);
            StationSign(village, Elbrus.Azau, signX, signZ);
        }

        // ── the three stations above ──────────────────────────────────────────────────────────────────────
        /// <summary>What stands at Старый Кругозор, Мир and Гара-Баши, as a visitor finds it — and, this time, where the
        /// ground can actually hold it: every building here sits on the bench the station itself stands on, with the
        /// viewing railings along the edge where the slope falls away. Cafés and toilets at every station, the old cable
        /// car set up as a monument at Krugozor, the museum of the defence of Prielbrusye and the monument to its
        /// defenders at Mir with the unfinished concrete beside them, the highest café and post box in Russia at
        /// Gara-Bashi.</summary>
        static void Stations(Transform root)
        {
            var group = new GameObject("Stations").transform; group.SetParent(root, false);

            void Put(Elbrus.Poi p, string prefab, float east, float north, float turn)
                => Fixture(prefab, group, p.X + east, p.Z + north, turn);

            // Старый Кругозор, 3 000 м — the bench is west of the old hall; east of it the slope drops thirty degrees
            // into the Baksan, which is what everyone comes out to look at
            var k = Elbrus.Krugozor;
            Put(k, "Elb_Cafe_Hall", -34f, 18f, 96f);
            Put(k, "Elb_Cafe", -46f, -14f, 118f);
            Put(k, "Elb_Toilet", -74f, 28f, 70f);
            Put(k, "Elb_Exhibit_Wagon", -10f, 34f, 22f);         // the 1969 wagon on its plinth
            Put(k, "Elb_Monument", -54f, 46f, 150f);             // the wall of remembrance above the station
            Put(k, "Elb_Bench", -20f, -2f, 116f);
            Put(k, "Elb_Bench", -20f, 8f, 116f);
            ContourRun("Elb_Rail", group, k.X + 4f, k.Z - 26f, 4, 8f, Sit.Pad);   // the viewing edge, south of the hall
            StationSign(group, k, k.X - 16f, k.Z + 14f);

            // Мир, 3 500 м — the shelf runs east and north-east from the halls, the drop is behind them to the west
            var m = Elbrus.Mir;
            Put(m, "Elb_Cafe_Hall", 46f, 22f, 250f);
            Put(m, "Elb_Cafe", 50f, -28f, 268f);
            Put(m, "Elb_Booth", 46f, 56f, 220f);                 // the museum of the defence of Prielbrusye
            Put(m, "Elb_Monument", 28f, 76f, 210f);
            Put(m, "Elb_Toilet", 62f, 40f, 250f);
            Put(m, "Elb_Foundation", 86f, 16f, 14f);             // concrete started and abandoned
            Put(m, "Elb_Foundation", 90f, 58f, 24f);
            Put(m, "Elb_Kiosk", 34f, -2f, 250f);
            Put(m, "Elb_Kiosk", 37.5f, -4f, 250f);
            Put(m, "Elb_Bench", 26f, 12f, 250f);
            ContourRun("Elb_Rail", group, m.X + 2f, m.Z - 32f, 4, 8f, Sit.Pad);    // the edge over the valley
            StationSign(group, m, m.X + 22f, m.Z + 18f);

            // Гара-Баши, 3 847 м — the top station, on its own shelf; the ground falls to the west and to the south
            var g = Elbrus.Garabashi;
            Put(g, "Elb_Cafe_Hall", 32f, -8f, 244f);             // the highest café in Russia
            Put(g, "Elb_Postbox", 26f, -2f, 250f);
            Put(g, "Elb_Toilet", 46f, 26f, 236f);
            Put(g, "Elb_Bench", 16f, -20f, 244f);
            ContourRun("Elb_Rail", group, g.X - 24f, g.Z - 4f, 4, 8f, Sit.Pad);
            StationSign(group, g, g.X + 20f, g.Z - 14f);
        }

        // ── Garabashi camp and the shelters ───────────────────────────────────────────────────────────────
        static void Camp(Transform root)
        {
            var camp = new GameObject("Camp").transform; camp.SetParent(root, false);
            var b = Elbrus.Barrels;
            // two rows of barrels lying along the contour, doors downhill, as they stand on the Garabashi bench
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 5; k++)
                {
                    float x = b.X - 16f + k * 8f + row * 3.5f;
                    float z = b.Z - 8f + row * 11f;
                    Seat("Elb_Barrel", camp, x, z, FaceDownhill(x, z, 12f), 0f, Sit.Pad);
                }
            // the snow-cats wait on their own road just above the barrels, side by side
            var ratrak = Load("Elb_Ratrak");
            for (int k = 0; k < 3; k++)
            {
                if (ratrak == null) break;
                var go = Object.Instantiate(ratrak, Vector3.zero, Quaternion.identity, camp);
                var ride = go.AddComponent<RatrakRide>();
                ride.Setup(dem, 74f + k * 16f, -11f + k * 11f);
                Ratraks.Add(ride);
                Reserve(go.transform.position.x, go.transform.position.z, 5f);
            }

            // the diesel that lights the camp, between the two rows where it always stands
            {
                float gx = b.X - 2f, gz = b.Z - 2.5f;
                Seat("Elb_Genset", camp, gx, gz, FaceDownhill(gx, gz, 12f), 0f, Sit.Pad);
            }

            Hut("redfox", "Elb_Hut_Small", camp);
            Hut("garabashiHut", "Elb_Hut_Natspark", camp);       // приют «Нацпарк»: rooms of four, heating, sockets
            Hut("priut88", "Elb_Hut_Small", camp);
            Hut("priut11", "Elb_Hut_Diesel", camp, 90f);      // eleven metres long: it lies along the contour
            // LeapRus: three capsules side by side across the slope, each looking down the valley through its
            // glazed end — so they are spaced along the contour, not along the map's east axis
            var l = Elbrus.Get("leaprus");
            float fall = FaceDownhill(l.X, l.Z, 15f);
            float lx = Mathf.Cos(fall * Mathf.Deg2Rad), lz = -Mathf.Sin(fall * Mathf.Deg2Rad);
            for (int k = 0; k < 3; k++)
            {
                float x = l.X + lx * (k - 1) * 7.5f, z = l.Z + lz * (k - 1) * 7.5f;
                Seat("Elb_Hut_Capsule", camp, x, z, FaceDownhill(x, z, 15f), 0f, Sit.Pad);
            }
            // the emergency box on the saddle (RedFox 5300) is no longer a placeholder hut: the real one, with its
            // bunks, its tambour and its six guys, stands in the same spot inside Elb_Ascent (see RouteFurniture)
        }

        static void Hut(string id, string prefab, Transform parent, float turn = 0f)
        {
            var p = Elbrus.Get(id);
            Seat(prefab, parent, p.X, p.Z, FaceDownhill(p.X, p.Z, 15f) + turn, 0f, Sit.Pad);
        }

        // ── route ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Everything the rules of <see cref="Ascent"/> and <see cref="AscentRoute"/> hang on, as objects on
        /// the mountain: the wands every 40 m along the whole route, the МЧС cable strung from 4 901 to 5 251 m, the
        /// fixed ropes of the summit rise, Pastukhov rocks as a corridor the wands run down the middle of, the
        /// snow-cat lane with its spoil banks lying on the very line the crevasse rule checks, the hollows and the two
        /// open crevasses right of that lane and the brooks left of it, the huts of the moraine, and the Red Fox box
        /// on the saddle with its fumarole beside it.
        ///
        /// All of it is one prefab, baked by <c>ElbrusAscent</c> in the editor: everything inside is already in world
        /// coordinates and already sat on the height field, so the runtime has exactly one thing to do. The old
        /// runtime wands every 55 m and the placeholder hut on the saddle are gone — the prefab carries both, in the
        /// right places and to the right spacing (see docs/ELBRUS.md).</summary>
        static void RouteFurniture(Transform root)
        {
            var prefab = Load("Elb_Ascent");
            if (prefab == null) return;
            Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
        }

        /// <summary>The floor of the valley: the village of Terskol, the Devichyi Kosy waterfall, the Pik Terskol
        /// observatory and the four ways between them — one prefab baked by <c>ElbrusValley</c> in world coordinates
        /// and already sat on the height field (docs/ELBRUS.md). The acclimatisation days of a real ascent happen
        /// down here, and until now the whole lower half of the map stood empty.</summary>
        static void Valley(Transform root)
        {
            var prefab = Load("Elb_Valley");
            if (prefab == null) return;
            Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root);
        }

        // ── daylight ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>A clear July morning on the southern slope: hard sun, thin air, the valley haze far below.
        /// Replaces the 1959 night sky, which belongs to the other map.</summary>
        public static void Daylight(Light sun)
        {
            if (sun != null)
            {
                sun.color = new Color(1f, .97f, .9f);
                sun.intensity = 1.35f;                                   // snow blows out at 1.75: the slope turned into white paper
                sun.shadows = LightShadows.Soft;
                sun.enabled = true;
                sun.transform.rotation = Quaternion.LookRotation(-DaySky.SunDirection, Vector3.up);   // mid-morning, sun in the south-east
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // blue shadows on snow, but not a white-out: the ground bounce stays moderate so the relief keeps its shape
            RenderSettings.ambientSkyColor = new Color(.42f, .54f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.54f, .6f, .7f);
            RenderSettings.ambientGroundColor = new Color(.5f, .53f, .58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .00012f;                         // aerial perspective, but the slope must stay readable to the far ridges
            RenderSettings.fogColor = new Color(.66f, .76f, .88f);
            if (Object.FindFirstObjectByType<DaySky>() == null) DaySky.Create();
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.6f, .72f, .86f);
                cam.farClipPlane = 20000f;
            }
        }
    }
}
