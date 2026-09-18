using UnityEngine;
using UnityEngine.Rendering;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Daylight sky of the Elbrus map: the same dome and cloud shaders the 1959 night uses, with a day palette —
    /// deep blue zenith, pale haze on the horizon, the sun as a disc with its aureole, and a thin cloud deck drifting east.
    /// No stars, no moon, no aurora. The night sky of Kholat is <see cref="SkyDome"/>.</summary>
    public sealed class DaySky : MonoBehaviour
    {
        /// <summary>Mid-morning sun over the southern slope: altitude and azimuth in degrees (matches ElbrusWorld.Daylight).</summary>
        public const float SunAltitude = 44f, SunAzimuth = 128f;

        static readonly int ZenithId = Shader.PropertyToID("_SkyZenith"), HorizonId = Shader.PropertyToID("_SkyHorizon"), GlowId = Shader.PropertyToID("_SkyGlow"),
            SunId = Shader.PropertyToID("_SunDirW"), MoonId = Shader.PropertyToID("_MoonDirW"), StormId = Shader.PropertyToID("_SkyStorm"),
            MilkyId = Shader.PropertyToID("_MilkyWay"), StarVisId = Shader.PropertyToID("_StarVis"), CoverId = Shader.PropertyToID("_CloudCover"),
            CloudTimeId = Shader.PropertyToID("_CloudTime"), WindId = Shader.PropertyToID("_CloudWind"), AuroraId = Shader.PropertyToID("_Aurora"),
            MoonLitId = Shader.PropertyToID("_MoonLit"), SunDiscId = Shader.PropertyToID("_SunDisc");

        Transform dome, clouds;
        float cloudTime;

        /// <summary>Direction to the sun in the game frame.</summary>
        public static Vector3 SunDirection
        {
            get
            {
                float alt = SunAltitude * Mathf.Deg2Rad, az = SunAzimuth * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(alt) * Mathf.Sin(az), Mathf.Sin(alt), Mathf.Cos(alt) * Mathf.Cos(az));
            }
        }

        public static DaySky Create()
        {
            var go = new GameObject("DaySky", typeof(DaySky));
            return go.GetComponent<DaySky>();
        }

        Transform Part(string name, string mesh, string mat, float scale)
        {
            var m = Resources.Load<Mesh>("World/Meshes/Sky/" + mesh);
            var mt = Resources.Load<Material>("World/Materials/Sky/" + mat);
            if (m == null || mt == null) { Debug.LogWarning($"1079 day sky: missing {mesh}/{mat}"); return null; }
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
            dome = Part("Sky", "Dome", "Sky", 14000f);
            clouds = Part("Clouds", "Dome", "Clouds", 13000f);
            Debug.Log($"1079 day sky: dome {(dome != null ? "ok" : "MISSING")}, clouds {(clouds != null ? "ok" : "MISSING")}");
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // above 3 000 m the sky darkens and the haze thins out; the player's altitude drives both a little
            float high = Mathf.InverseLerp(2300f, 5300f, cam.transform.position.y);
            var zenith = Color.Lerp(new Color(.18f, .35f, .66f), new Color(.07f, .19f, .48f), high);
            var horizon = Color.Lerp(new Color(.74f, .82f, .9f), new Color(.5f, .64f, .82f), high);

            Shader.SetGlobalVector(ZenithId, (Vector4)zenith);
            Shader.SetGlobalVector(HorizonId, (Vector4)horizon);
            Shader.SetGlobalVector(GlowId, (Vector4)new Color(.16f, .14f, .1f));
            Shader.SetGlobalVector(SunId, SunDirection);
            Shader.SetGlobalVector(MoonId, Vector3.down);
            Shader.SetGlobalFloat(SunDiscId, 1f);
            Shader.SetGlobalFloat(StormId, 0f);
            Shader.SetGlobalFloat(MilkyId, 0f);
            Shader.SetGlobalFloat(StarVisId, 0f);
            Shader.SetGlobalFloat(AuroraId, 0f);
            Shader.SetGlobalFloat(MoonLitId, 0f);
            Shader.SetGlobalFloat(CoverId, .2f);
            cloudTime += Time.deltaTime * .01f;
            Shader.SetGlobalFloat(CloudTimeId, cloudTime);
            Shader.SetGlobalVector(WindId, new Vector4(.9f, .35f, 0, 0));

            transform.position = cam.transform.position;
            if (cam.farClipPlane < 15000f) cam.farClipPlane = 20000f;
        }

        void OnDestroy() => Shader.SetGlobalFloat(SunDiscId, 0f);
    }
}
