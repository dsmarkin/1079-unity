using UnityEngine;

namespace Height1079.Night
{
    /// <summary>Wind and blizzard. Whoever owns the night decides when a storm blows and says so through
    /// <see cref="Want"/> (the game: <c>WeatherDriver</c> off the session; the sandbox: its run clock); here it builds
    /// up and dies down smoothly, gusts come and go, the forest bends (global values for the Height1079/TreeWind
    /// shader), driving snow streaks past the camera and ground drift snakes over the snow. In a full gust of the
    /// blizzard you see a few metres.
    ///
    /// In its own assembly, with nothing of the session in it, so the sandbox blows the same snow the mountain does
    /// instead of a copy of it.</summary>
    public sealed class Weather : MonoBehaviour
    {
        public static Weather Instance { get; private set; }

        // ── what the owner tells it, every frame ──
        /// <summary>0 … 1, the blizzard the owner wants. The front takes <see cref="RiseSeconds"/> to arrive at 1 and
        /// <see cref="FallSeconds"/> to die back to 0.</summary>
        public float Want;
        public float RiseSeconds = 25f, FallSeconds = 40f;
        /// <summary>There is a night to blow through. Off, the air is still and nothing is emitted (the menu).</summary>
        public bool Live;
        /// <summary>The day's own wind, 0 … 1, where a day has one (the southern slope draws it from its seed); below
        /// zero the blizzard cycle sets the wind by itself.</summary>
        public float DayWind = -1f;
        /// <summary>The listener is under canvas: no snow drives past the eye.</summary>
        public bool Inside;
        /// <summary>Where the ground is under the listener, for the drift; NaN puts it a man's height under the camera.</summary>
        public float GroundY = float.NaN;

        /// <summary>0 calm night … 1 full blizzard (smoothed).</summary>
        public static float Storm { get; private set; }
        /// <summary>0 … 1, the current gust on top of the base wind.</summary>
        public static float Gust { get; private set; }
        /// <summary>0 … 1 overall wind strength the trees and sounds follow.</summary>
        public static float Wind { get; private set; }
        /// <summary>Where the wind blows to, world x/z (it comes from the north-west down the slope and wanders a little).</summary>
        public static Vector2 Direction { get; private set; } = new Vector2(.7f, -.7f);

        static readonly int WindParams = Shader.PropertyToID("_WindParams"), WindDir = Shader.PropertyToID("_WindDir");

        ParticleSystem driving, drift;
        Material tracks;
        float trackFill;
        float windTime, gustPhase, seed;

        /// <summary>Under <paramref name="parent"/> when given, so it dies with whatever owns it (the sandbox);
        /// otherwise kept across scene loads the way the game keeps the rest of its night.</summary>
        public static Weather Create(Transform parent = null)
        {
            var go = new GameObject("Weather", typeof(Weather));
            if (parent != null) go.transform.SetParent(parent, false);
            else DontDestroyOnLoad(go);
            return go.GetComponent<Weather>();
        }

        void Awake()
        {
            Instance = this;
            seed = Random.value * 50f;
            var flakes = Resources.Load<Material>("World/Materials/SnowFx/Snowflakes");
            var puff = Resources.Load<Material>("World/Materials/SnowFx/Smoke"); // soft round puff, lit
            tracks = Resources.Load<Material>("World/Materials/AnimalTracks");
            driving = Driving(flakes);
            drift = Drift(puff != null ? puff : flakes);
        }

        ParticleSystem Driving(Material mat)
        {
            var go = new GameObject("BlizzardDriving", typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, .5f), new Color(1, 1, 1, .95f));
            main.maxParticles = 16000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(46f, 16f, 46f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(-2f, 1f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.6f; noise.frequency = .25f; noise.scrollSpeed = 1.2f; noise.octaveCount = 2; noise.quality = ParticleSystemNoiseQuality.Medium;
            // the flake texture is a 2×2 atlas: one flake per particle
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .018f; r.lengthScale = 1f;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem Drift(Material mat)
        {
            var go = new GameObject("BlizzardGroundDrift", typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.3f, 1.35f, 1.45f, .1f), new Color(1.4f, 1.45f, 1.55f, .22f));
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(40f, 1.2f, 40f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(-.2f, .6f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .6f, 1, 1.6f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // a sandbox that has been left takes its weather with it; the numbers must not stay blowing
            Storm = Gust = 0f; Wind = .15f;
            Shader.SetGlobalVector(WindParams, new Vector4(.15f, 0f, windTime, 0));
        }

        void Update()
        {
            // the front takes RiseSeconds to arrive and FallSeconds to die down
            float want = Mathf.Clamp01(Want);
            float rate = Time.deltaTime / Mathf.Max(want > Storm ? RiseSeconds : FallSeconds, .05f);
            Storm = Mathf.MoveTowards(Storm, want, rate);

            // gusts: Perlin swells, sharper and more often in a storm
            gustPhase += Time.deltaTime * Mathf.Lerp(.08f, .22f, Storm);
            float g = Mathf.PerlinNoise(seed, gustPhase);
            g = Mathf.Clamp01((g - Mathf.Lerp(.35f, .2f, Storm)) * Mathf.Lerp(1.6f, 2.2f, Storm));
            Gust = g * g * (3f - 2f * g);
            bool live = Live;
            Wind = !live ? .15f : Mathf.Clamp01(Mathf.Lerp(.22f, .75f, Storm) + Gust * Mathf.Lerp(.25f, .3f, Storm));
            // where the strength of the wind is a property of the day (the southern slope draws it once from the
            // save's seed), the gusts ride on top of it instead of on the blizzard cycle
            if (live && DayWind >= 0f) Wind = Mathf.Clamp01(DayWind + Gust * Mathf.Lerp(.15f, .3f, Storm));

            float wander = (Mathf.PerlinNoise(seed + 7f, Time.time * .02f) - .5f) * 50f;
            var dir = Quaternion.Euler(0, wander, 0) * new Vector3(.7071f, 0, -.7071f);
            Direction = new Vector2(dir.x, dir.z);

            // tree shader time runs faster in strong wind so the rocking speeds up without jumping
            windTime += Time.deltaTime * (.6f + .9f * Wind);
            Shader.SetGlobalVector(WindParams, new Vector4(Wind, Gust, windTime, 0));
            Shader.SetGlobalVector(WindDir, new Vector4(Direction.x, Direction.y, 0, 0));

            // a blizzard fills the animal tracks; they stay faint afterwards
            trackFill = Mathf.MoveTowards(trackFill, Storm > .5f ? 1f : trackFill, Time.deltaTime / 120f);
            if (tracks != null) tracks.color = new Color(1, 1, 1, Mathf.Lerp(1f, .3f, trackFill));

            var cam = Camera.main;
            if (cam == null) return;
            bool inside = Inside;
            float speed = Mathf.Lerp(3f, 16f, Wind) * (1f + .35f * Gust);
            var p = cam.transform.position;

            float drive = !live || inside ? 0f : Storm * (.55f + .45f * Gust);
            var em = driving.emission; em.rateOverTime = 9000f * drive;
            driving.transform.position = p - new Vector3(Direction.x, 0, Direction.y) * speed * 1.1f + Vector3.up * 2f;
            var v = driving.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(Direction.x * speed * .8f, Direction.x * speed * 1.2f);
            v.z = new ParticleSystem.MinMaxCurve(Direction.y * speed * .8f, Direction.y * speed * 1.2f);

            float drifting = !live || inside ? 0f : Mathf.Clamp01(Wind * 1.4f - .3f) * (.5f + .5f * Gust);
            var de = drift.emission; de.rateOverTime = 420f * drifting;
            float ground = float.IsNaN(GroundY) ? p.y - 1.6f : GroundY;
            drift.transform.position = new Vector3(p.x, ground + .4f, p.z) - new Vector3(Direction.x, 0, Direction.y) * speed;
            var dv = drift.velocityOverLifetime;
            dv.x = new ParticleSystem.MinMaxCurve(Direction.x * speed * .7f, Direction.x * speed * 1.1f);
            dv.z = new ParticleSystem.MinMaxCurve(Direction.y * speed * .7f, Direction.y * speed * 1.1f);
        }
    }
}
