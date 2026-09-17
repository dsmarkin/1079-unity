using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Builds the Elbrus location at runtime from <see cref="Elbrus"/> and the DEM: the six ropeways with their
    /// towers, ropes and cars, the terminals, the barrel camp and the shelters, the snow-cats and the wands of the
    /// summit route. Prefabs come from Resources/World/Prefabs/Elbrus (ElbrusFactory).</summary>
    public static class ElbrusWorld
    {
        public static readonly List<RopewayRig> Lines = new List<RopewayRig>();
        public static readonly List<RatrakRide> Ratraks = new List<RatrakRide>();
        static HeightField dem;

        static GameObject Load(string name)
        {
            var p = Resources.Load<GameObject>("World/Prefabs/Elbrus/" + name);
            if (p == null) Debug.LogWarning("1079 Эльбрус: нет префаба " + name);
            return p;
        }

        static float Ground(float x, float z) => dem.Sample(x, z);

        static GameObject Place(GameObject prefab, Transform parent, float x, float z, float yaw, float dy = 0f)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, new Vector3(x, Ground(x, z) + dy, z), Quaternion.Euler(0, yaw, 0), parent);
            return go;
        }

        /// <summary>Compass bearing (degrees) from a to b in the game's frame (z = north).</summary>
        static float Yaw(float ax, float az, float bx, float bz) => Mathf.Atan2(bx - ax, bz - az) * Mathf.Rad2Deg;

        public static void Build(HeightField heights)
        {
            dem = heights;
            Lines.Clear(); Ratraks.Clear();
            var root = new GameObject("Elbrus").transform;

            Ropeways(root);
            Azau(root);
            Stations(root);
            Camp(root);
            Route(root);
            root.gameObject.AddComponent<ElbrusRides>();
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
                    Place(Load(TerminalPrefab(spec, end == 0)), ropeRoot, t.x, t.z, yaw, -.9f);
                }
            }

            foreach (var id in new[] { "azau", "krugozor", "mir", "garabashi" })
            {
                var p = Elbrus.Get(id);
                var sign = Place(Load("Elb_Sign"), root, p.X + 9f, p.Z - 9f, 180f);
                var label = sign != null ? sign.GetComponentInChildren<TextMesh>() : null;
                if (label != null) label.text = p.Label.Replace(" · ", "\n");
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


        // ── Azau: the village at the bottom ───────────────────────────────────────────────────────────────
        /// <summary>Поляна Азау, 2 350 м: the square between the two bottom stations — the old pendulum hall of 1969 to
        /// the east, the gondola terminal to the west — with the ticket offices in front of them, a row of souvenir
        /// stalls along the path, cafés and hotels around the meadow, the car park and the bus turn-round below.
        /// Laid out from photographs of the real place, see docs/ELBRUS.md.</summary>
        static void Azau(Transform root)
        {
            var village = new GameObject("Azau").transform; village.SetParent(root, false);
            var a = Elbrus.Azau;
            // local frame: +u runs down the meadow to the east (towards Terskol), +v uphill to the north-west
            float yaw = 118f;                                   // the square faces the stations
            float ca = Mathf.Cos(yaw * Mathf.Deg2Rad), sa = Mathf.Sin(yaw * Mathf.Deg2Rad);
            (float x, float z) At(float u, float v) => (a.X + u * ca + v * sa, a.Z - u * sa + v * ca);

            void Put(string prefab, float u, float v, float turn, float dy = 0f)
            {
                var (x, z) = At(u, v);
                Place(Load(prefab), village, x, z, yaw + turn, dy);
            }

            // ticket offices in front of the terminals
            Put("Elb_Booth", -26f, -34f, 0f);
            Put("Elb_Booth", 16f, -36f, 6f);
            // the souvenir market: two rows of stalls along the path from the car park to the stations
            for (int k = 0; k < 9; k++)
            {
                Put("Elb_Kiosk", -38f + k * 7.5f, -58f, 0f);
                if (k < 7) Put("Elb_Kiosk", -30f + k * 7.5f, -74f, 180f);
            }
            // cafés and shashlyk places along the east side of the square
            Put("Elb_Cafe", 44f, -52f, -28f);
            Put("Elb_Cafe", 52f, -78f, -14f);
            Put("Elb_Cafe", -62f, -66f, 34f);
            // hotels around the meadow: the big ones east of the square, chalets under the pines to the west
            Put("Elb_Hotel", 78f, -34f, -22f);
            Put("Elb_Hotel", 96f, -74f, -8f);
            Put("Elb_Hotel", 60f, -108f, 12f);
            Put("Elb_Chalet", -86f, -96f, 26f);
            Put("Elb_Chalet", -66f, -122f, 8f);
            Put("Elb_Chalet", 30f, -128f, -6f);
            Put("Elb_Toilet", -52f, -46f, 90f);
            // lamps along the path, benches on the square
            for (int k = 0; k < 7; k++) Put("Elb_Lamp", -40f + k * 13f, -48f, 0f);
            for (int k = 0; k < 4; k++) Put("Elb_Bench", -24f + k * 16f, -44f, 0f);
            // the car park and the road below the village
            for (int k = 0; k < 5; k++) Put("Elb_Rail", -34f + k * 17f, -92f, 0f);
        }

        // ── the three stations above ──────────────────────────────────────────────────────────────────────
        /// <summary>What stands at Старый Кругозор, Мир and Гара-Баши, as a visitor finds it: cafés and toilets at every
        /// station, the old cable-car wagon set up as a monument at Krugozor, the museum of the defence of Prielbrusye and
        /// the monument to its defenders at Mir with the unfinished concrete beside them, the highest café and post box in
        /// Russia at Gara-Bashi, and viewing rails wherever the ground falls away.</summary>
        static void Stations(Transform root)
        {
            var group = new GameObject("Stations").transform; group.SetParent(root, false);

            void Put(Elbrus.Poi p, string prefab, float east, float north, float turn, float dy = 0f)
                => Place(Load(prefab), group, p.X + east, p.Z + north, turn, dy);

            // Старый Кругозор, 3 000 м
            var k = Elbrus.Krugozor;
            Put(k, "Elb_Cafe", 34f, -16f, 120f);
            Put(k, "Elb_Cafe", 44f, -34f, 150f);
            Put(k, "Elb_Toilet", -34f, -26f, 70f);
            Put(k, "Elb_Exhibit_Wagon", 20f, 22f, 28f);          // the 1969 wagon on its plinth
            Put(k, "Elb_Monument", -26f, 16f, 160f);             // the wall of remembrance above the station
            Put(k, "Elb_Bench", 12f, -40f, 180f);
            Put(k, "Elb_Bench", 26f, -44f, 180f);
            for (int i = 0; i < 4; i++) Put(k, "Elb_Rail", 6f + i * 8f, -50f, 96f);

            // Мир, 3 500 м
            var m = Elbrus.Mir;
            Put(m, "Elb_Cafe", 30f, -20f, 128f);
            Put(m, "Elb_Cafe", 40f, -40f, 150f);
            Put(m, "Elb_Booth", -24f, -30f, 80f);                // the museum of the defence of Prielbrusye
            Put(m, "Elb_Monument", -30f, 24f, 170f);
            Put(m, "Elb_Toilet", 46f, 12f, 200f);
            Put(m, "Elb_Foundation", -52f, -8f, 24f);            // concrete started and abandoned
            Put(m, "Elb_Foundation", -64f, -36f, 10f);
            Put(m, "Elb_Kiosk", 16f, -46f, 180f);
            Put(m, "Elb_Kiosk", 23f, -47f, 180f);
            Put(m, "Elb_Bench", -8f, -44f, 180f);
            for (int i = 0; i < 5; i++) Put(m, "Elb_Rail", -14f + i * 8f, -52f, 92f);

            // Гара-Баши, 3 847 м — the top station
            var g = Elbrus.Garabashi;
            Put(g, "Elb_Cafe", 26f, -18f, 134f);                 // the highest café in Russia
            Put(g, "Elb_Postbox", 20f, -12f, 140f);
            Put(g, "Elb_Toilet", -28f, -20f, 60f);
            Put(g, "Elb_Bench", 8f, -30f, 180f);
            for (int i = 0; i < 4; i++) Put(g, "Elb_Rail", -4f + i * 8f, -36f, 90f);
            var sled = Load("Elb_Snowmobile");
            for (int i = 0; i < 4; i++)
                Place(sled, group, g.X - 14f + i * 5.5f, g.Z - 42f, 150f + i * 9f);
        }

        // ── Garabashi camp and the shelters ───────────────────────────────────────────────────────────────
        static void Camp(Transform root)
        {
            var camp = new GameObject("Camp").transform; camp.SetParent(root, false);
            var barrel = Load("Elb_Barrel");
            var b = Elbrus.Barrels;
            // two rows of barrels facing downhill, as they stand on the Garabashi bench
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 5; k++)
                {
                    float x = b.X - 16f + k * 8f + row * 3.5f;
                    float z = b.Z - 8f + row * 11f;
                    var (dx, dz, _) = dem.Fall(x, z, 12f);
                    Place(barrel, camp, x, z, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg);
                }
            // the snow-cats wait on their own road just above the barrels, side by side
            var ratrak = Load("Elb_Ratrak");
            for (int k = 0; k < 3; k++)
            {
                if (ratrak == null) break;
                var go = Object.Instantiate(ratrak, Vector3.zero, Quaternion.identity, camp);
                var ride = go.AddComponent<RatrakRide>();
                ride.Setup(dem, 70f + k * 14f, -9f + k * 9f);
                Ratraks.Add(ride);
            }

            Hut("redfox", "Elb_Hut_Small", camp);
            Hut("garabashiHut", "Elb_Hut_Small", camp);
            Hut("priut88", "Elb_Hut_Small", camp);
            Hut("priut11", "Elb_Hut_Diesel", camp);
            var leap = Load("Elb_Hut_Capsule");
            var l = Elbrus.Get("leaprus");
            for (int k = 0; k < 3; k++)
            {
                float x = l.X + k * 7f - 7f, z = l.Z;
                var (dx, dz, _) = dem.Fall(x, z, 15f);
                Place(leap, camp, x, z, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg + 90f);
            }
            // the emergency box on the saddle (RedFox 5300)
            var saddle = Elbrus.Saddle;
            Place(Load("Elb_Hut_Small"), camp, saddle.X + 25f, saddle.Z + 12f, 200f);
        }

        static void Hut(string id, string prefab, Transform parent)
        {
            var p = Elbrus.Get(id);
            var (dx, dz, _) = dem.Fall(p.X, p.Z, 15f);
            Place(Load(prefab), parent, p.X, p.Z, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg);
        }

        // ── route ─────────────────────────────────────────────────────────────────────────────────────────
        static void Route(Transform root)
        {
            var wands = new GameObject("Wands").transform; wands.SetParent(root, false);
            var wand = Load("Elb_Wand");
            if (wand == null) return;
            float total = Elbrus.Length(Elbrus.SummitRoute);
            var rng = new System.Random(5642);
            for (float s = 0; s < total; s += 55f)
            {
                var (x, z) = Elbrus.PointAt(Elbrus.SummitRoute, s);
                x += (float)(rng.NextDouble() - .5) * 3f;
                z += (float)(rng.NextDouble() - .5) * 3f;
                var go = Place(wand, wands, x, z, (float)rng.NextDouble() * 360f, -.15f);
                if (go != null) go.transform.Rotate(Vector3.forward, (float)(rng.NextDouble() - .5) * 18f, Space.Self);
            }
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
