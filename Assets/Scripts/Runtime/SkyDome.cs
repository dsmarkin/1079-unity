using UnityEngine;
using UnityEngine.Rendering;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The sky of 1–2 February 1959 around the camera: gradient and twilight glow, Milky Way, the real star field turning with
    /// sidereal time, the waning moon rising before dawn, a drifting cloud deck (overcast in blizzards, broken in the lulls) and, in two of
    /// the lulls, a faint aurora (an artistic assumption). Also publishes the sun/moon state the lighting uses (Bootstrap.Atmosphere).</summary>
    public sealed class SkyDome : MonoBehaviour
    {
        public static double Minutes { get; private set; }
        public static float SunAlt { get; private set; }
        public static Vector3 SunDir { get; private set; }
        public static float MoonAlt { get; private set; }
        public static Vector3 MoonDir { get; private set; }
        public static float MoonLit { get; private set; }
        public static float CloudCover { get; private set; }
        public static float Aurora { get; private set; }
        public static Color Horizon { get; private set; } = new Color(.6f, .64f, .7f);
        public static Color Zenith { get; private set; }
        public static Color Glow { get; private set; }

        static readonly int ZenithId = Shader.PropertyToID("_SkyZenith"), HorizonId = Shader.PropertyToID("_SkyHorizon"), GlowId = Shader.PropertyToID("_SkyGlow"),
            SunId = Shader.PropertyToID("_SunDirW"), MoonId = Shader.PropertyToID("_MoonDirW"), PoleId = Shader.PropertyToID("_GalPoleW"), CentreId = Shader.PropertyToID("_GalCentreW"),
            StormId = Shader.PropertyToID("_SkyStorm"), MilkyId = Shader.PropertyToID("_MilkyWay"), StarVisId = Shader.PropertyToID("_StarVis"),
            CoverId = Shader.PropertyToID("_CloudCover"), CloudTimeId = Shader.PropertyToID("_CloudTime"), WindId = Shader.PropertyToID("_CloudWind"),
            AuroraId = Shader.PropertyToID("_Aurora"), AuroraTimeId = Shader.PropertyToID("_AuroraTime"), MoonLitId = Shader.PropertyToID("_MoonLit");

        Transform dome, clouds, aurora, stars, moon;
        float cloudTime;

        public static SkyDome Create()
        {
            var go = new GameObject("SkyDome", typeof(SkyDome));
            DontDestroyOnLoad(go);
            return go.GetComponent<SkyDome>();
        }

        Transform Part(string name, string mesh, string mat, float scale)
        {
            var m = Resources.Load<Mesh>("World/Meshes/Sky/" + mesh);
            var mt = Resources.Load<Material>("World/Materials/Sky/" + mat);
            if (m == null || mt == null) { Debug.LogWarning($"1079 sky: missing {mesh}/{mat}"); return null; }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mt;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        void Awake()
        {
            dome = Part("Sky", "Dome", "Sky", 3000f);
            stars = Part("Stars", "Stars", "Stars", 1f);
            aurora = Part("Aurora", "Dome", "Aurora", 2900f);
            moon = Part("Moon", "MoonQuad", "Moon", 1f);
            clouds = Part("Clouds", "Dome", "Clouds", 2800f);
        }

        // twilight palette by the sun's altitude (degrees): zenith, horizon, glow
        static readonly float[] Alts = { 4f, 0f, -4f, -8f, -12f, -18f };
        static readonly Color[] Zeniths = { new Color(.36f, .45f, .58f), new Color(.27f, .35f, .48f), new Color(.14f, .19f, .31f), new Color(.055f, .08f, .15f), new Color(.032f, .042f, .072f), new Color(.022f, .028f, .046f) };
        static readonly Color[] Horizons = { new Color(.78f, .76f, .74f), new Color(.66f, .6f, .6f), new Color(.42f, .38f, .44f), new Color(.15f, .15f, .21f), new Color(.062f, .068f, .092f), new Color(.036f, .042f, .058f) };
        static readonly Color[] Glows = { new Color(.9f, .55f, .3f), new Color(1.1f, .55f, .3f), new Color(.85f, .38f, .27f), new Color(.42f, .2f, .18f), new Color(.1f, .06f, .07f), Color.black };

        static Color Palette(Color[] table, float alt)
        {
            if (alt >= Alts[0]) return table[0];
            for (int i = 1; i < Alts.Length; i++)
                if (alt >= Alts[i]) return Color.Lerp(table[i], table[i - 1], (alt - Alts[i]) / (Alts[i - 1] - Alts[i]));
            return table[table.Length - 1];
        }

        static Vector3 V(double alt, double az) { var d = Sky.Direction(alt, az); return new Vector3((float)d.x, (float)d.y, (float)d.z); }
        static Vector3 V((double x, double y, double z) d) => new Vector3((float)d.x, (float)d.y, (float)d.z);

        /// <summary>Cloud cover over the night: evening overcast with breaks, blizzards closed, lulls broken and clearing.</summary>
        static float CoverAt(float elapsed)
        {
            float n = Mathf.PerlinNoise(elapsed * .012f, 3.7f);
            return elapsed < 110f ? .55f + .25f * n : .22f + .38f * n;
        }

        /// <summary>Aurora in the second and third lulls, a weak glow in the last one (0..1).</summary>
        static float AuroraAt(float elapsed)
        {
            float Window(float a, float b, float peak) => elapsed < a || elapsed > b ? 0f : peak * Mathf.Sin(Mathf.PI * (elapsed - a) / (b - a));
            return Mathf.Max(Window(560f, 650f, 1f), Mathf.Max(Window(825f, 915f, .55f), Window(1095f, 1200f, .25f)));
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var s = NightSession.Instance;
            float elapsed = s != null ? s.Elapsed.Value : 0f;
            bool running = s != null;
            // the demo reel walks the night itself, with no session behind it
            if (DemoReel.NightSeconds >= 0f) { elapsed = DemoReel.NightSeconds; running = true; }
            // the menu shows the valley at 16:40, the sun on the ridge
            Minutes = running ? Sky.ClockMinutes(elapsed) : 16 * 60 + 40;
            double jd = Sky.JulianDay(Minutes);
            var (sa, sz) = Sky.Sun(jd);
            var (ma, mz, lit) = Sky.Moon(jd);
            SunAlt = (float)sa; SunDir = V(sa, sz);
            MoonAlt = (float)ma; MoonDir = V(ma, mz); MoonLit = (float)lit;

            float storm = Weather.Storm;
            CloudCover = Mathf.Lerp(running ? CoverAt(elapsed) : .45f, 1f, storm);
            Aurora = running ? AuroraAt(elapsed) * (1f - storm) : 0f;

            var z = Palette(Zeniths, SunAlt); var h = Palette(Horizons, SunAlt); var g = Palette(Glows, SunAlt);
            // an aurora tints the northern sky and the snow a little green
            z += new Color(.0f, .02f, .01f) * Aurora;
            h += new Color(.0f, .03f, .015f) * Aurora;
            float light = Mathf.Max(h.grayscale, .03f);
            var stormCol = new Color(.075f, .085f, .1f) * Mathf.Clamp(light * 3f, .25f, 3f);
            Zenith = Color.Lerp(z, stormCol, storm); Horizon = Color.Lerp(h, stormCol, storm); Glow = g * (1f - storm * .85f);

            Shader.SetGlobalVector(ZenithId, (Vector4)Zenith);
            Shader.SetGlobalVector(HorizonId, (Vector4)Horizon);
            Shader.SetGlobalVector(GlowId, (Vector4)Glow);
            Shader.SetGlobalVector(SunId, SunDir);
            Shader.SetGlobalVector(MoonId, MoonDir);
            Shader.SetGlobalFloat(StormId, storm);
            Shader.SetGlobalFloat(StarVisId, Mathf.InverseLerp(-5f, -13f, SunAlt));
            Shader.SetGlobalFloat(MilkyId, Mathf.InverseLerp(-13f, -18f, SunAlt) * (1f - storm));
            Shader.SetGlobalFloat(CoverId, CloudCover);
            cloudTime += Time.deltaTime * (.004f + .012f * Weather.Wind);
            Shader.SetGlobalFloat(CloudTimeId, cloudTime);
            Shader.SetGlobalVector(WindId, new Vector4(-Weather.Direction.x, -Weather.Direction.y, 0, 0));
            Shader.SetGlobalFloat(AuroraId, Aurora);
            Shader.SetGlobalFloat(AuroraTimeId, Time.time);
            Shader.SetGlobalFloat(MoonLitId, MoonLit);

            // celestial sphere: equatorial → game frame (the star mesh stores y negated, so the matrix is a proper rotation)
            var rot = Sky.CelestialRotation(jd);
            Vector3 c1 = V(rot.c1), c2 = V(rot.c2);
            var celestial = Quaternion.LookRotation(c2, -c1);
            Shader.SetGlobalVector(PoleId, V(Sky.Rotate(rot, Sky.GalacticPole)));
            Shader.SetGlobalVector(CentreId, V(Sky.Rotate(rot, Sky.GalacticCentre)));

            var p = cam.transform.position;
            transform.position = p;
            if (stars != null) stars.rotation = celestial;
            if (moon != null)
            {
                const float R = 2500f;
                moon.position = p + MoonDir * R;
                moon.rotation = Quaternion.LookRotation(MoonDir, Vector3.up);
                moon.localScale = Vector3.one * (R * Mathf.Tan(.6f * Mathf.Deg2Rad) * 8f);   // the disc is 1/8 of the quad; drawn ×2.3 of its true size, as the eye sees a low moon
                moon.gameObject.SetActive(MoonAlt > -3f);
            }
            if (aurora != null) aurora.gameObject.SetActive(Aurora > .001f);
            if (cam.farClipPlane < 3100f) cam.farClipPlane = 3100f;
        }
    }
}
