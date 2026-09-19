using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Low mist between the trunks around the camera: big soft lit puffs a metre or two above the snow, thicker under the canopy,
    /// in hollows and along the brooks, drifting slowly with the wind. A blizzard tears it apart; at dusk it is faint, at night the torch
    /// beam catches it.</summary>
    public sealed class ForestMist : MonoBehaviour
    {
        const float Radius = 45f;
        ParticleSystem ps;
        TerrainData terrainData;
        float[,,] alphas;
        int alphaRes;
        float budget;

        public static ForestMist Create()
        {
            var go = new GameObject("ForestMist", typeof(ForestMist));
            DontDestroyOnLoad(go);
            return go.GetComponent<ForestMist>();
        }

        void Awake()
        {
            var mat = Resources.Load<Material>("World/Materials/SnowFx/Mist");
            if (mat == null) { Debug.LogWarning("1079: Mist material missing"); enabled = false; return; }
            QualitySettings.softParticles = true;
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 24f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.05f, .05f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .8f, 1, 1.3f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 3f;
            ps.Play();
        }

        /// <summary>0..1 how much canopy stands at x/z: the needle-litter terrain layer (3) marks the forest floor.</summary>
        float Forest(float x, float z)
        {
            if (terrainData == null)
            {
                var t = Terrain.activeTerrain;
                if (t == null) return 0f;
                terrainData = t.terrainData;
                alphaRes = terrainData.alphamapResolution;
                alphas = terrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);
                if (alphas.GetLength(2) < 4) { alphas = null; return 0f; }
            }
            if (alphas == null) return 0f;
            int c = Mathf.Clamp(Mathf.RoundToInt((x + HeightField.Half) / (HeightField.Half * 2) * (alphaRes - 1)), 0, alphaRes - 1);
            int r = Mathf.Clamp(Mathf.RoundToInt((z + HeightField.Half) / (HeightField.Half * 2) * (alphaRes - 1)), 0, alphaRes - 1);
            return alphas[r, c, 3];
        }

        void Update()
        {
            var cam = Camera.main;
            var dem = Bootstrap.Dem;
            if (cam == null || dem == null) return;
            if ((cam.depthTextureMode & DepthTextureMode.Depth) == 0) cam.depthTextureMode |= DepthTextureMode.Depth;
            var me = Bootstrap.LocalHiker;
            bool inside = me != null && me.Crawling;
            var s = NightSession.Instance;
            // calm air makes mist; wind and blizzard sweep it away
            float calm = Mathf.Clamp01(1f - Weather.Storm * 1.3f) * Mathf.Lerp(1f, .5f, Weather.Gust);
            float amount = s == null ? .35f : Mathf.Lerp(.45f, 1f, Bootstrap.Darkness) * calm;
            if (inside) amount = 0f;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, .05f * amount), new Color(1, 1, 1, .13f * amount));

            budget += Time.deltaTime * 60f * amount;
            var p = cam.transform.position;
            var wind = new Vector3(Weather.Direction.x, 0, Weather.Direction.y) * Mathf.Lerp(.15f, .6f, Weather.Wind);
            int tries = 0;
            while (budget >= 1f && tries++ < 40)
            {
                var o = Random.insideUnitCircle * Radius;
                float x = p.x + o.x, z = p.z + o.y;
                float gy = TerrainBuilder.Height(dem, x, z);
                // hollows keep the mist: compare with the ground 12 m around
                float around = (TerrainBuilder.Height(dem, x + 12, z) + TerrainBuilder.Height(dem, x - 12, z) + TerrainBuilder.Height(dem, x, z + 12) + TerrainBuilder.Height(dem, x, z - 12)) * .25f;
                float hollow = Mathf.Clamp01((around - gy) * .6f + .3f);
                float w = Mathf.Clamp01(Forest(x, z) * .9f + .1f) * (.5f + hollow);
                if (Random.value > w) continue;
                budget -= 1f;
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(x, gy + Random.Range(.4f, 2.6f), z),
                    velocity = wind + Random.insideUnitSphere * .08f,
                    applyShapeToPosition = false,
                }, 1);
            }
            if (budget > 5f) budget = 5f;
        }
    }
}
