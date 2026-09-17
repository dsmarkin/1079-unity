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
            RenderSettings.fogDensity = .00032f;                         // aerial perspective: the far ridges recede
            RenderSettings.fogColor = new Color(.66f, .76f, .88f);
            DaySky.Create();
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
