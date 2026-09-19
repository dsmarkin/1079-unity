using UnityEngine;
using Height1079.Core;
using Height1079.Snow;

namespace Height1079.Runtime
{
    /// <summary>Snow around the players: layered snowfall that drifts and swirls, and the storm switch for it. What the
    /// snow does underfoot — boot prints, trodden paths, puffs, the trail map for trail-breaking — is
    /// <see cref="SnowPrints"/>, shared with the sandbox; this creates it, keeps it under the same object and tells it
    /// whether it is snowing and how hard.</summary>
    public sealed class SnowFx : MonoBehaviour
    {
        public static SnowFx Instance { get; private set; }

        ParticleSystem nearFlakes, farFlakes;
        SnowPrints prints;

        public static SnowFx Create()
        {
            var go = new GameObject("SnowFx", typeof(SnowFx));
            DontDestroyOnLoad(go);
            return go.GetComponent<SnowFx>();
        }

        static Material Mat(string name) => Resources.Load<Material>(SnowPrints.MaterialDir + name);

        void Awake()
        {
            Instance = this;
            prints = SnowPrints.Create(transform);
            prints.Snowing = true;

            var flakeMat = Mat("Snowflakes");
            // Near layer: large soft flakes within ~12 m, swirling. Far layer: fine dense snow up to ~45 m that reads as a veil.
            nearFlakes = Flakes("SnowNear", flakeMat, 26f, 14f, .018f, .045f, 700, 3000);
            farFlakes = Flakes("SnowFar", flakeMat, 90f, 30f, .03f, .07f, 1400, 6000);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        ParticleSystem Flakes(string name, Material mat, float box, float height, float minSize, float maxSize, float rate, int max)
        {
            var ps = new GameObject(name, typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, .55f), new Color(1, 1, 1, .95f));
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(box, height, box);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(.4f, 1.4f); vel.y = new ParticleSystem.MinMaxCurve(-1.3f, -.7f); vel.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .55f; noise.frequency = .35f; noise.scrollSpeed = .25f; noise.octaveCount = 2; noise.quality = ParticleSystemNoiseQuality.Medium;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(1, .9f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Low; col.lifetimeLoss = 1f; col.bounce = 0f; col.radiusScale = .1f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Particles/Standard Unlit"));
            r.minParticleSize = 0f; r.maxParticleSize = .012f; // keeps a flake passing the lens from filling the screen
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingFudge = 10;
            ps.Play();
            return ps;
        }

        void Update()
        {
            var s = NightSession.Instance;
            bool storm = s != null && s.Storm.Value;
            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                // Emit slightly upwind so the drift carries flakes through the view.
                float wind = storm ? 9f : 1f;
                nearFlakes.transform.position = p + new Vector3(-wind * .6f, 5f, 0);
                farFlakes.transform.position = p + new Vector3(-wind * 1.5f, 11f, 0);
            }
            SetWeather(nearFlakes, storm, 700f, 2600f);
            SetWeather(farFlakes, storm, 1400f, 5200f);
            prints.Storm = storm;
        }

        static void SetWeather(ParticleSystem ps, bool storm, float calm, float blizzard)
        {
            var emission = ps.emission; emission.rateOverTime = storm ? blizzard : calm;
            var vel = ps.velocityOverLifetime;
            vel.x = storm ? new ParticleSystem.MinMaxCurve(8f, 13f) : new ParticleSystem.MinMaxCurve(.4f, 1.4f);
            vel.y = storm ? new ParticleSystem.MinMaxCurve(-3.5f, -1.5f) : new ParticleSystem.MinMaxCurve(-1.3f, -.7f);
            var noise = ps.noise; noise.strength = storm ? 1.6f : .55f; noise.frequency = storm ? .6f : .35f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = storm ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = storm ? .015f : 0f; r.lengthScale = 1f;
        }

        /// <summary>Snow depth a boot sinks into at this point: deep powder in the forest and valley, wind crust on the open slope.</summary>
        public static float Depth(float groundHeight) => groundHeight < Sites.TreeLine ? .32f : groundHeight < 820f ? .16f : .07f;

        /// <summary>Movement factor for trail-breaking: virgin powder is slow, a path trodden by others is fast.</summary>
        public float SpeedFactor(float x, float z, float groundHeight) => prints.SpeedFactor(x, z, Depth(groundHeight));
    }
}
