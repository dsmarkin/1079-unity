using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Everything on the Elbrus map smaller than a building: the cars and the buses on the Azau meadow, the
    /// queues and the turnstiles in front of the stations, the service yards above (drums, crates, gas bottles, sledges,
    /// skips, floodlights, antennas, wind socks) and the line furniture between them — the power poles along the
    /// ropeway, the snow fences across the wind, the rubble the builders left on the pastures.
    /// Everything goes down through <see cref="ElbrusWorld.Prop"/>, so nothing lands inside a wall, on top of another
    /// prop or under a ropeway with a cabin coming through it. Density falls with height, the way it does in life:
    /// Azau is crowded, Мир is a working yard, Гара-Баши is a few drums and a wind sock.</summary>
    internal static class ElbrusDressing
    {
        static System.Random rng;
        static Transform props;

        internal static void Build(Transform root)
        {
            rng = new System.Random(3847);
            props = new GameObject("Props").transform;
            props.SetParent(root, false);

            Azau();
            Krugozor();
            Mir();
            Garabashi();
            Camp();
            Roofs();
            PowerLine();
            Pastures();
        }

        static float Jit(float m) => (float)(rng.NextDouble() - .5) * 2f * m;
        static bool Chance(double p) => rng.NextDouble() < p;

        /// <summary>Scatters a prop over a patch of ground, turned to the slope, skipping whatever does not fit.</summary>
        static void Scatter(string prefab, float cx, float cz, float spread, int tries, float height, float turnJitter = 25f)
        {
            for (int i = 0; i < tries; i++)
            {
                float x = cx + Jit(spread), z = cz + Jit(spread);
                ElbrusWorld.Prop(prefab, props, x, z, ElbrusWorld.FaceContour(x, z) + Jit(turnJitter), height);
            }
        }

        /// <summary>A run of sections end to end between two points (a fence, a line of crowd barriers, snow fences):
        /// each section is turned so its +X lies along the run.</summary>
        static void Run(string prefab, float x0, float z0, float x1, float z1, float pitch)
        {
            float dx = x1 - x0, dz = z1 - z0;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / pitch));
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg - 90f;      // +X along the run
            for (int i = 0; i < n; i++)
            {
                float t = (i + .5f) / n;
                float x = x0 + dx * t, z = z0 + dz * t;
                ElbrusWorld.Fixture(prefab, props, x, z, yaw, 0f, ElbrusWorld.Sit.Flat, pitch * .45f);
            }
        }

        // ── Азау: the meadow, the square and the car park ─────────────────────────────────────────────────
        /// <summary>The bottom of the mountain on a July morning: minibuses unloading, the bus from Terskol turning
        /// round, the queue barriers and the turnstiles at both stations, flags over the square, the hire stand, the
        /// woodpiles behind the cafés and the snow-cat on its trailer at the edge of the glade.</summary>
        static void Azau()
        {
            // ── in front of the gondola terminal (the square runs down-glade from it, +v) ──
            for (int k = 0; k < 3; k++) P("Elb_Turnstile", -5.5f + k * 4f, 19f, 0f, 1.2f);
            Run(U(-9f, 16f), U(-9f, 28f), "Elb_QueueRail", 2.5f);
            Run(U(6f, 16f), U(6f, 28f), "Elb_QueueRail", 2.5f);
            // ── and in front of the 1969 hall, 128 m down the glade ──
            for (int k = 0; k < 2; k++) P("Elb_Turnstile", -3f + k * 4f, 108f, 180f, 1.2f);
            Run(U(-8f, 100f), U(-8f, 112f), "Elb_QueueRail", 2.5f);
            Run(U(5f, 100f), U(5f, 112f), "Elb_QueueRail", 2.5f);

            // the board with the map of the lifts, the fingerpost and the flags
            P("Elb_InfoBoard", 11f, 30f, 0f, 2.6f);
            P("Elb_SignPost", 5f, 62f, 8f, 3f);
            for (int k = 0; k < 3; k++) P("Elb_FlagPole", 17f, 4f + k * 7f, 0f, 7.4f);
            P("Elb_RentStand", -13f, 106f, -90f, 2.8f);
            P("Elb_SkiRack", 12f, 98f, 90f, 1.8f);
            P("Elb_SkiRack", -12f, 24f, -90f, 1.8f);

            // lamps down both sides of the path, arms over it
            for (int k = 0; k < 6; k++) P("Elb_Lamp", 13.5f, 18f + k * 19f, -90f, 5.6f);
            for (int k = 0; k < 5; k++) P("Elb_Lamp", -14f, 28f + k * 25f, 90f, 5.6f);
            // benches and bins along it
            for (int k = 0; k < 4; k++) P("Elb_Bench", 8.5f, 42f + k * 17f, 90f, 1f);
            for (int k = 0; k < 4; k++) P("Elb_Bin", 7f, 36f + k * 21f, 0f, 1.2f);
            P("Elb_Bin", -15f, 52f, 0f, 1.2f);
            P("Elb_Bin", -15f, 88f, 0f, 1.2f);
            P("Elb_Bin", 17f, 30f, 0f, 1.2f);
            // the chain that keeps the crowd off the grass, both sides of the path
            for (int k = 0; k < 15; k++) P("Elb_Chain", -11.5f, 40f + k * 3.2f, 90f, 1.1f);
            for (int k = 0; k < 13; k++) P("Elb_Chain", 11f, 46f + k * 3.2f, 90f, 1.1f);

            // behind the cafés: wood for the stove, gas for the kitchen, a drum for the rubbish
            P("Elb_Woodpile", -47f, 53f, 0f, 1.5f);
            P("Elb_Woodpile", -47f, 92f, 4f, 1.5f);
            P("Elb_GasCage", -42f, 58f, 90f, 1.9f);
            P("Elb_Bin_Barrel", -29f, 44f, 0f, 1f);
            P("Elb_Bin_Barrel", 28f, 70f, 0f, 1f);
            P("Elb_Skip", -30f, 96f, 90f, 1.5f);
            P("Elb_Transformer", -30f, 14f, 90f, 3.2f);

            // ── the car park at the bottom of the glade, where the road from Terskol ends ──
            string[] cars = { "Elb_Car_White", "Elb_Car_Silver", "Elb_Car_Red", "Elb_Car_Suv" };
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 7; k++)
                {
                    if (Chance(.18)) continue;                       // the park is never full and never tidy
                    var (x, z) = ElbrusWorld.AzauAt(-40f + row * 10f, 150f + k * 5.6f);
                    ElbrusWorld.Prop(cars[rng.Next(cars.Length)], props, x, z,
                        ElbrusWorld.FaceContour(x, z) + (Chance(.5) ? 0f : 180f) + Jit(7f), 1.8f);
                }
            for (int k = 0; k < 3; k++)
            {
                var (x, z) = ElbrusWorld.AzauAt(-10f, 152f + k * 8f);
                ElbrusWorld.Prop("Elb_Van", props, x, z, ElbrusWorld.FaceContour(x, z) + Jit(6f), 2.7f);
            }
            {
                var (x, z) = ElbrusWorld.AzauAt(-2f, 190f);
                ElbrusWorld.Prop("Elb_Bus", props, x, z, ElbrusWorld.FaceContour(x, z) + Jit(4f), 3.2f);
            }
            // the fence round it, the boom at the way in, a lamp over the rows
            Run(U(-50f, 208f), U(6f, 208f), "Elb_Fence", 4f);
            Run(U(-50f, 202f), U(-50f, 146f), "Elb_Fence", 4f);
            P("Elb_Barrier", -24f, 199f, 0f, 1.3f);
            P("Elb_Lamp", -46f, 168f, -90f, 5.6f);
            P("Elb_Lamp", -46f, 196f, -90f, 5.6f);
            P("Elb_Bin", -14f, 206f, 0f, 1.2f);

            // ── the machinery at the north-west edge of the glade ──
            {
                var (x, z) = ElbrusWorld.AzauAt(-72f, 98f);
                float yaw = ElbrusWorld.FaceContour(x, z);
                var trailer = ElbrusWorld.Prop("Elb_Trailer", props, x, z, yaw, 2.4f);
                if (trailer != null)
                    ElbrusWorld.At("Elb_Ratrak", props, trailer.transform.position + trailer.transform.up * .98f,
                        trailer.transform.rotation);
                var (x2, z2) = ElbrusWorld.AzauAt(-79f, 114f);
                ElbrusWorld.Prop("Elb_Ratrak", props, x2, z2, ElbrusWorld.FaceContour(x2, z2) + Jit(8f), 3f);
            }
            Scatter("Elb_Drum", U(-74f, 88f).x, U(-74f, 88f).z, 3f, 5, 1f, 360f);
            P("Elb_CrateStack", -78f, 92f, 20f, 1.5f);
            P("Elb_Container", -84f, 102f, 90f, 2.9f);
        }

        /// <summary>A point of the Azau frame in world metres.</summary>
        static (float x, float z) U(float u, float v) => ElbrusWorld.AzauAt(u, v);

        /// <summary>A prop in the Azau frame: u across the glade, v down it, turn relative to the square.</summary>
        static void P(string prefab, float u, float v, float turn, float height)
        {
            var (x, z) = ElbrusWorld.AzauAt(u, v);
            ElbrusWorld.Prop(prefab, props, x, z, ElbrusWorld.AzauYaw + turn, height);
        }

        static void Run((float x, float z) a, (float x, float z) b, string prefab, float pitch)
            => Run(prefab, a.x, a.z, b.x, b.z, pitch);

        // ── Старый Кругозор, 3 000 м ──────────────────────────────────────────────────────────────────────
        /// <summary>The first change: a shabby yard on a shelf with the whole Baksan valley falling away from its eastern
        /// edge. Everything that is delivered here comes up by cable, so it stands about in crates and drums.</summary>
        static void Krugozor()
        {
            var k = Elbrus.Krugozor;
            P(k, "Elb_Floodlight", -30f, 36f, 0f, 9.4f);
            P(k, "Elb_Floodlight", -58f, 4f, 0f, 9.4f);
            P(k, "Elb_Container", -68f, 46f, 20f, 2.9f);
            P(k, "Elb_CrateStack", -42f, 2f, 0f, 1.5f);
            P(k, "Elb_CrateStack", -50f, 30f, 0f, 1.5f);
            P(k, "Elb_GasCage", -30f, 8f, 0f, 1.9f);
            P(k, "Elb_Skip", -58f, 20f, 0f, 1.5f);
            P(k, "Elb_Bin_Barrel", -24f, 6f, 0f, 1f);
            P(k, "Elb_Bin_Barrel", -44f, -6f, 0f, 1f);
            P(k, "Elb_Deck", -24f, 28f, 0f, .6f);
            P(k, "Elb_Deck", -40f, 40f, 0f, .6f);
            P(k, "Elb_SkiRack", -26f, 6f, 0f, 1.8f);
            P(k, "Elb_Windsock", -8f, 52f, 0f, 5.4f);
            P(k, "Elb_Banner", -22f, 14f, 0f, 4.2f);
            Scatter("Elb_Drum", k.X - 64f, k.Z + 12f, 3.5f, 5, 1f, 360f);
            Scatter("Elb_Chain", k.X - 46f, k.Z + 26f, 9f, 4, 1.1f);
            // snow fences on the open slope above the station, across the wind
            ElbrusWorld.ContourRun("Elb_SnowFence", props, k.X - 34f, k.Z + 74f, 6, 4.2f);
            ElbrusWorld.ContourRun("Elb_SnowFence", props, k.X - 66f, k.Z + 62f, 5, 4.2f);
        }

        // ── Мир, 3 500 м ──────────────────────────────────────────────────────────────────────────────────
        /// <summary>The working middle of the mountain: three stations on one shelf, the museum, the monument, the
        /// abandoned concrete, and the yard where the snowmobiles and the sledges are loaded for the huts above.</summary>
        static void Mir()
        {
            var m = Elbrus.Mir;
            P(m, "Elb_Floodlight", 42f, 44f, 0f, 9.4f);
            P(m, "Elb_Floodlight", 64f, 4f, 0f, 9.4f);
            P(m, "Elb_Container", 72f, 64f, 16f, 2.9f);
            P(m, "Elb_Container", 78f, 70f, 16f, 2.9f);
            P(m, "Elb_CrateStack", 40f, 8f, 0f, 1.5f);
            P(m, "Elb_CrateStack", 56f, 50f, 0f, 1.5f);
            P(m, "Elb_GasCage", 52f, 12f, 0f, 1.9f);
            P(m, "Elb_Skip", 58f, 2f, 0f, 1.5f);
            P(m, "Elb_Bin_Barrel", 40f, 30f, 0f, 1f);
            P(m, "Elb_Bin_Barrel", 60f, -18f, 0f, 1f);
            P(m, "Elb_Deck", 30f, 10f, 0f, .6f);
            P(m, "Elb_Deck", 56f, -14f, 0f, .6f);
            P(m, "Elb_SkiRack", 34f, 18f, 0f, 1.8f);
            P(m, "Elb_SkiRack", 52f, -20f, 0f, 1.8f);
            P(m, "Elb_Windsock", 10f, 44f, 0f, 5.4f);
            P(m, "Elb_Banner", 30f, 28f, 0f, 4.2f);
            P(m, "Elb_Transformer", 74f, 26f, 0f, 3.2f);
            Scatter("Elb_Drum", m.X + 68f, m.Z + 46f, 4f, 6, 1f, 360f);
            Scatter("Elb_Drum", m.X + 44f, m.Z - 34f, 3f, 3, 1f, 360f);
            // the snowmobiles that carry loads up to the huts, with their sledges behind them
            for (int i = 0; i < 3; i++)
            {
                float x = m.X + 30f + i * 4.5f, z = m.Z + 60f;
                float yaw = ElbrusWorld.FaceDownhill(x, z) + 180f + Jit(10f);
                if (ElbrusWorld.Prop("Elb_Snowmobile", props, x, z, yaw, 1.5f) == null) continue;
                float back = yaw * Mathf.Deg2Rad;
                ElbrusWorld.Prop("Elb_Sled", props, x - Mathf.Sin(back) * 3.4f, z - Mathf.Cos(back) * 3.4f, yaw, 1f);
            }
            ElbrusWorld.ContourRun("Elb_SnowFence", props, m.X + 26f, m.Z + 96f, 6, 4.2f);
            ElbrusWorld.ContourRun("Elb_SnowFence", props, m.X + 74f, m.Z + 92f, 5, 4.2f);
        }

        // ── Гара-Баши, 3 847 м ────────────────────────────────────────────────────────────────────────────
        /// <summary>The top of the cable and the edge of the snow: almost nothing stands here that does not have to.
        /// Drums, a container, the sledges of the carriers, a wind sock, and the wands where the path to the barrels
        /// leaves the platform.</summary>
        static void Garabashi()
        {
            var g = Elbrus.Garabashi;
            P(g, "Elb_Floodlight", 26f, 18f, 0f, 9.4f);
            P(g, "Elb_Container", 52f, 34f, 14f, 2.9f);
            P(g, "Elb_CrateStack", 36f, 12f, 0f, 1.5f);
            P(g, "Elb_GasCage", 44f, 2f, 0f, 1.9f);
            P(g, "Elb_Bin_Barrel", 28f, 12f, 0f, 1f);
            P(g, "Elb_Deck", 20f, 6f, 0f, .6f);
            P(g, "Elb_SkiRack", 24f, 14f, 0f, 1.8f);
            P(g, "Elb_Windsock", -10f, 34f, 0f, 5.4f);
            Scatter("Elb_Drum", g.X + 46f, g.Z + 22f, 4f, 5, 1f, 360f);
            // the snowmobiles of the carriers, drawn up below the platform with their sledges
            for (int i = 0; i < 4; i++)
            {
                float x = g.X + 6f + i * 5.5f, z = g.Z + 34f;
                float yaw = ElbrusWorld.FaceDownhill(x, z) + 180f + Jit(12f);
                if (ElbrusWorld.Prop("Elb_Snowmobile", props, x, z, yaw, 1.5f) == null) continue;
                float back = yaw * Mathf.Deg2Rad;
                ElbrusWorld.Prop("Elb_Sled", props, x - Mathf.Sin(back) * 3.4f, z - Mathf.Cos(back) * 3.4f, yaw, 1f);
            }
            ElbrusWorld.ContourRun("Elb_SnowFence", props, g.X + 18f, g.Z + 62f, 5, 4.2f);
            // the way down to the barrels, wanded like the route above
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float x = Mathf.Lerp(g.X + 10f, Elbrus.Barrels.X - 10f, t);
                float z = Mathf.Lerp(g.Z - 6f, Elbrus.Barrels.Z + 14f, t);
                ElbrusWorld.Prop("Elb_Wand", props, x + Jit(2f), z + Jit(2f), (float)rng.NextDouble() * 360f, .5f);
            }
        }

        /// <summary>A prop placed from a station, in metres east and north of it.</summary>
        static void P(Elbrus.Poi p, string prefab, float east, float north, float extra, float height)
        {
            float x = p.X + east, z = p.Z + north;
            ElbrusWorld.Prop(prefab, props, x, z, ElbrusWorld.FaceContour(x, z) + extra, height);
        }

        // ── the barrel camp ───────────────────────────────────────────────────────────────────────────────
        /// <summary>Гара-Баши camp: the fuel for the generators, the crates the food comes up in, the sledges behind the
        /// snowmobiles and the boards laid between the barrels so the path does not turn to slush.</summary>
        static void Camp()
        {
            var b = Elbrus.Barrels;
            Scatter("Elb_Drum", b.X - 22f, b.Z + 16f, 4f, 7, 1f, 360f);
            Scatter("Elb_Drum", b.X + 20f, b.Z + 12f, 3f, 4, 1f, 360f);
            P(b, "Elb_CrateStack", -24f, 12f, 0f, 1.5f);
            P(b, "Elb_CrateStack", 22f, 13f, 0f, 1.5f);
            P(b, "Elb_GasCage", -20f, 13f, 0f, 1.9f);
            P(b, "Elb_Bin_Barrel", -6f, 12f, 0f, 1f);
            P(b, "Elb_Bin_Barrel", 14f, 13f, 0f, 1f);
            P(b, "Elb_Deck", 2f, 13f, 0f, .6f);
            P(b, "Elb_Deck", 10f, 13f, 0f, .6f);
            P(b, "Elb_Container", -28f, 18f, 12f, 2.9f);
            P(b, "Elb_Floodlight", -14f, 20f, 0f, 9.4f);
            P(b, "Elb_Windsock", 26f, 22f, 0f, 5.4f);
            for (int i = 0; i < 3; i++)
            {
                float x = b.X - 12f + i * 6f, z = b.Z + 22f;
                float yaw = ElbrusWorld.FaceDownhill(x, z) + 180f + Jit(14f);
                if (ElbrusWorld.Prop("Elb_Snowmobile", props, x, z, yaw, 1.5f) == null) continue;
                float back = yaw * Mathf.Deg2Rad;
                ElbrusWorld.Prop("Elb_Sled", props, x - Mathf.Sin(back) * 3.4f, z - Mathf.Cos(back) * 3.4f, yaw, 1f);
            }
            ElbrusWorld.ContourRun("Elb_SnowFence", props, b.X - 6f, b.Z + 46f, 6, 4.2f);
            // the huts above: fuel drums and a sledge at each of them
            foreach (var id in new[] { "redfox", "priut11", "priut88", "leaprus" })
            {
                var p = Elbrus.Get(id);
                Scatter("Elb_Drum", p.X - 7f, p.Z + 9f, 3f, 3, 1f, 360f);
                float yaw = ElbrusWorld.FaceContour(p.X + 8f, p.Z + 7f);
                ElbrusWorld.Prop("Elb_Sled", props, p.X + 8f, p.Z + 7f, yaw, 1f);
            }
        }

        /// <summary>What sits on the station roofs: the dish and the whips of the radio link, and a wind sock over the
        /// top two, where the wind is the thing that closes the line.</summary>
        static void Roofs()
        {
            foreach (var hall in ElbrusWorld.Halls)
            {
                float top = RoofTop(hall.Prefab);
                if (top <= 0f) continue;
                var size = ElbrusWorld.Footprint(hall.Prefab);
                float hw = Mathf.Max(1.5f, size.x * .5f - 2.8f), hl = Mathf.Max(2f, size.y * .5f - 4f);
                float c = Mathf.Cos(hall.Yaw * Mathf.Deg2Rad), s = Mathf.Sin(hall.Yaw * Mathf.Deg2Rad);
                Vector3 World(float lx, float lz) => hall.Base + new Vector3(lx * c + lz * s, top, -lx * s + lz * c);
                ElbrusWorld.At("Elb_Antenna", props, World(hw - .8f, -(hl - 1.5f)), hall.Yaw + 24f);
                if (hall.Prefab == "Elb_Terminal_Garabashi" || hall.Prefab == "Elb_Terminal_Mir")
                    ElbrusWorld.At("Elb_Windsock", props, World(-(hw - .8f), hl - 2f), hall.Yaw);
            }
        }

        /// <summary>Top of the roof slab of every terminal, above the prefab's own pivot.</summary>
        static float RoofTop(string prefab)
        {
            switch (prefab)
            {
                case "Elb_Terminal_Azau": return 8.9f;
                case "Elb_Terminal_Krugozor": return 7.9f;
                case "Elb_Terminal_Mir": return 8.4f;
                case "Elb_Terminal_Garabashi": return 7.9f;
                case "Elb_Terminal_Small": return 6.4f;
                case "Elb_Terminal_Old": return 10.1f;
                default: return 0f;
            }
        }

        // ── between the stations ──────────────────────────────────────────────────────────────────────────
        /// <summary>The power line that feeds the stations: concrete poles up the slope beside the ropeway, far enough
        /// from it to be out of the way of the cabins, with the wires strung between them.</summary>
        static void PowerLine()
        {
            foreach (var spec in Elbrus.Ropeways)
            {
                if (spec.Id != "gondola1" && spec.Id != "gondola2" && spec.Id != "gondola3") continue;
                var line = spec.Towers;
                float total = Elbrus.Length(line);
                var tops = new List<Vector3>();
                for (float s = 40f; s < total - 40f; s += 78f)
                {
                    var (px, pz) = Elbrus.PointAt(line, s);
                    var (ax, az) = Elbrus.PointAt(line, Mathf.Max(0f, s - 6f));
                    var (bx, bz) = Elbrus.PointAt(line, Mathf.Min(total, s + 6f));
                    float dx = bx - ax, dz = bz - az;
                    float len = Mathf.Sqrt(dx * dx + dz * dz);
                    if (len < 1e-3f) continue;
                    // thirty metres to the side of the line, on the flank the cabins never swing over
                    float ox = dz / len * 32f, oz = -dx / len * 32f;
                    float x = px + ox, z = pz + oz;
                    var pole = ElbrusWorld.Prop("Elb_Pylon", props, x, z, ElbrusWorld.FaceDownhill(x, z) + 90f, 9.2f);
                    if (pole == null) continue;
                    tops.Add(pole.transform.position + Vector3.up * 8.45f);
                }
                Wires(tops);
            }
        }

        /// <summary>The wires themselves: two sagging strands from pole top to pole top.</summary>
        static void Wires(List<Vector3> tops)
        {
            if (tops.Count < 2) return;
            // ?? would ignore Unity's fake null, so ask twice the plain way
            var mat = Resources.Load<Material>("World/Materials/ElbPSteel");
            if (mat == null) mat = Resources.Load<Material>("World/Materials/ElbSteel");
            if (mat == null) return;                     // no material yet: better no wires than magenta ones
            for (int strand = 0; strand < 2; strand++)
            {
                var go = new GameObject("Wire");
                go.transform.SetParent(props, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = .06f;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.sharedMaterial = mat;
                var pts = new List<Vector3>();
                float side = strand == 0 ? -.95f : .95f;
                for (int i = 0; i < tops.Count - 1; i++)
                {
                    var a = tops[i]; var b = tops[i + 1];
                    var dir = b - a; dir.y = 0f;
                    float span = dir.magnitude;
                    var right = Vector3.Cross(Vector3.up, dir.normalized) * side;
                    float sag = Mathf.Min(span * .018f, 2.2f);
                    for (int k = 0; k < 5; k++)
                    {
                        float t = k / 5f;
                        var p = Vector3.Lerp(a, b, t) + right;
                        p.y -= 4f * sag * t * (1f - t);
                        pts.Add(p);
                    }
                }
                pts.Add(tops[tops.Count - 1] + Vector3.Cross(Vector3.up, (tops[tops.Count - 1] - tops[tops.Count - 2]).normalized) * side);
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
            }
        }

        /// <summary>The pastures between Azau and Кругозор, and the moraines above them: what forty years of building a
        /// resort left lying about — broken blocks, rebar, grey boards — and the snow fences along the old road.</summary>
        static void Pastures()
        {
            var a = Elbrus.Azau; var k = Elbrus.Krugozor;
            for (int i = 0; i < 14; i++)
            {
                float t = .18f + .64f * (float)rng.NextDouble();
                float x = Mathf.Lerp(a.X, k.X, t) + Jit(220f);
                float z = Mathf.Lerp(a.Z, k.Z, t) + Jit(220f);
                if (ElbrusWorld.Ground(x, z) > 3050f) continue;
                if (ElbrusWorld.Slope(x, z) > 22f) continue;
                ElbrusWorld.Prop("Elb_Rubble", props, x, z, (float)rng.NextDouble() * 360f, 1.2f);
            }
            for (int i = 0; i < 8; i++)
            {
                float t = .25f + .6f * (float)rng.NextDouble();
                float x = Mathf.Lerp(k.X, Elbrus.Mir.X, t) + Jit(180f);
                float z = Mathf.Lerp(k.Z, Elbrus.Mir.Z, t) + Jit(180f);
                if (ElbrusWorld.Slope(x, z) > 24f) continue;
                ElbrusWorld.Prop("Elb_Rubble", props, x, z, (float)rng.NextDouble() * 360f, 1.2f);
            }
            // snow fences along the exposed shoulders above the tree line
            ElbrusWorld.ContourRun("Elb_SnowFence", props, (a.X + k.X) * .5f + 90f, (a.Z + k.Z) * .5f + 40f, 7, 4.2f);
            ElbrusWorld.ContourRun("Elb_SnowFence", props, (k.X + Elbrus.Mir.X) * .5f - 70f, (k.Z + Elbrus.Mir.Z) * .5f, 6, 4.2f);
        }
    }
}
