using UnityEngine;
using Height1079.Core;
using Height1079.Night;
using Height1079.Snow;

namespace Height1079.Sandbox
{
    /// <summary>The sky over the small map, and the weather under it. Time runs: the map opens in the afternoon, the
    /// sun goes down, dusk turns to night, the stars come round, the moon rises and the morning comes back — a whole
    /// day of 1–2 February 1959 in a few minutes of real time. Nothing here is a game mode: the errand
    /// (<see cref="SandboxHunt"/>) may ask the clock for night at its start, and that is all it ever says to the sky.
    ///
    /// None of this is the small map's own: the dome, the stars, the moon, the clouds and the palette are the game's
    /// (<see cref="SkyDome"/>, shared assembly <c>Height1079.Night</c>), the sun and moon are counted by
    /// <see cref="Sky"/> for a Julian day, the blizzard is the game's (<see cref="Weather"/> — driving snow, ground
    /// drift, gusts), the wind in the ears is its two loops (<see cref="WindSound"/>) and the vignette and grain are
    /// its film (<see cref="NightFilm"/>). What is the map's own is only how much of the sky's light reaches this
    /// flat yard: the two lights, the ambient triple and the fog, because the yard's night has to hold the brief's
    /// twelve metres without a torch and three in the wall.
    ///
    /// The pace is data: <c>sky.hour</c> and <c>sky.dayMinutes</c> in <c>StreamingAssets/sandbox/yard.json</c>
    /// (docs/SANDBOX.md §11) — no Unity, no rebuild.</summary>
    public sealed class SandboxSky : MonoBehaviour
    {
        public static SandboxSky Instance { get; private set; }

        /// <summary>The local clock, minutes after midnight of 1 February 1959. It runs past 1440 into the next day;
        /// the sky reads it modulo nothing, because a Julian day is a number and 2 February is the next one.</summary>
        public double Minutes { get; private set; } = 14 * 60;

        /// <summary>Real seconds a whole twenty-four hours takes. Eight minutes by default: about a minute of
        /// afternoon light, a minute of sunset and dusk, five of night, and the dawn at the end.</summary>
        public float CycleSeconds = 8f * 60f;

        /// <summary>Fronts come and go by themselves. Off, only a run of the errand can raise one.</summary>
        public bool WeatherRuns = true;

        /// <summary>What a run of the errand is doing to the weather on top of the day's own (0 … 1). The run owns
        /// its storm clock because its rules are written round it; it does not own the sky, and it does not own the
        /// weather either — it can only add to it.</summary>
        public float RunStorm;

        public Weather Weather { get; private set; }
        public WindSound Wind { get; private set; }
        public SkyDome Dome { get; private set; }

        /// <summary>0 day … 1 night, off the sun's altitude — the same reading the game's own lighting takes, so
        /// "night" means one thing in both.</summary>
        public float Night => SkyDome.Darkness;
        /// <summary>0 … 1 the blizzard as it is right now.</summary>
        public float Storm => Weather.Storm;

        /// <summary>The hour on the clock, as the debug panel prints it.</summary>
        public string Clock
        {
            get { int m = (int)(Minutes % 1440.0); return $"{m / 60:00}:{m % 60:00}"; }
        }

        NightFilm film;
        Light sun, moon;
        Camera cam;
        float weatherSeed, age;

        // the two ends of the ambient triple: a winter day on snow, and a closed overcast night
        static readonly Color DayAmbSky = new Color(.66f, .76f, .92f), DayAmbEq = new Color(.74f, .78f, .83f), DayAmbGround = new Color(.82f, .84f, .88f);
        static readonly Color NightAmbSky = new Color(.045f, .055f, .085f), NightAmbEq = new Color(.028f, .034f, .05f), NightAmbGround = new Color(.02f, .024f, .034f);
        static readonly Color StormFog = new Color(.16f, .17f, .19f);

        public static SandboxSky Create(Transform parent, Camera camera)
        {
            var go = new GameObject("SandboxSky", typeof(SandboxSky));
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<SandboxSky>();
            s.Setup(camera);
            return s;
        }

        void Awake() => Instance = this;

        void Setup(Camera camera)
        {
            cam = camera;
            var layout = SandboxYardLayout.Current;
            Minutes = layout.sky.hour * 60.0;
            CycleSeconds = Mathf.Max(layout.sky.dayMinutes, .5f) * 60f;
            WeatherRuns = layout.sky.weather;
            weatherSeed = layout.seed % 977 * .137f;

            // the sky asks this every frame instead of reaching for a session it cannot see
            SkyDome.Clock = () => new SkyMoment { Minutes = Minutes, NightSeconds = 0f, Running = false };
            Dome = SkyDome.Create();
            Dome.transform.SetParent(transform, false);
            Dome.ShowSun = true;

            sun = Lamp("Sun", new Color(1f, .96f, .89f), .7f);
            moon = Lamp("MoonLight", new Color(.62f, .7f, .92f), .6f);

            Weather = Weather.Create(transform);
            Weather.Live = true;
            Weather.RiseSeconds = 18f; Weather.FallSeconds = 30f;
            Wind = WindSound.Create(transform);
            // its own root: an overlay canvas is happiest there; taken down with this object below
            film = NightFilm.Create();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            var prints = SnowPrints.Instance;
            if (prints != null) prints.Snowing = true;
            Debug.Log($"1079 sandbox: небо идёт с {Clock}, сутки за {CycleSeconds / 60f:0.#} мин, погода {(WeatherRuns ? "сама" : "только от забега")}");
        }

        Light Lamp(string name, Color colour, float shadowStrength)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(transform, false);
            var l = go.GetComponent<Light>();
            l.type = LightType.Directional; l.color = colour; l.intensity = 0f;
            l.shadows = LightShadows.Soft; l.shadowStrength = shadowStrength;
            l.enabled = false;
            return l;
        }

        void OnDestroy()
        {
            if (film != null) Destroy(film.gameObject);
            if (Dome != null) Destroy(Dome.gameObject);
            if (Instance == this) Instance = null;
            SkyDome.Clock = null;
        }

        // ── the clock ──

        /// <summary>Put the clock at this hour of 1 February (13.5 is half past one in the afternoon). Hours past 24
        /// are the morning of the 2nd, which is where the night of the game's story ends.</summary>
        public void SetHour(double hour) => Minutes = hour * 60.0;

        /// <summary>The dead of night, once. The errand asks for this at its start and never touches the sky again;
        /// time goes on running from there, so a long enough run sees the sky grey at the edges.</summary>
        public void ToNight()
        {
            double m = Minutes % 1440.0;
            // 23:00 of the day the clock is already on, or of the next one if it is past midnight and before dusk
            Minutes = Minutes - m + (m < 23 * 60.0 ? 23 * 60.0 : 1440.0 + 23 * 60.0);
        }

        // ── every frame ──

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            Minutes += dt * (1440.0 / Mathf.Max(CycleSeconds, 30f));

            // the day's own weather: a front every few hours of the clock, building and dying on Weather's own ramps.
            // The first minute is clear whatever the noise says — the first thing anybody sees has to be the daylight,
            // not the inside of a white-out.
            float world = 0f;
            if (WeatherRuns)
            {
                float n = Mathf.PerlinNoise(weatherSeed, (float)(Minutes / 60.0) * .18f);
                world = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, .72f, n)) * Mathf.InverseLerp(40f, 70f, age);
            }
            Weather.Want = Mathf.Max(world, Mathf.Clamp01(RunStorm));
            var boot = SandboxBoot.Instance;
            Weather.GroundY = boot != null && boot.Body != null ? boot.Body.Torso.position.y - boot.Tuning.HoverHeight : float.NaN;

            float night = Night, storm = Storm;
            float alt = SkyDome.SunAlt;

            // ambient: the sky's own light, warmed by what is left of the afterglow, greened a little by an aurora
            var glow = SkyDome.Glow * .12f * (1f - night);
            var aurora = new Color(0f, .03f, .015f) * SkyDome.Aurora * (1f - SkyDome.CloudCover * .7f);
            RenderSettings.ambientSkyColor = Color.Lerp(DayAmbSky, NightAmbSky, night) + glow + aurora;
            RenderSettings.ambientEquatorColor = Color.Lerp(DayAmbEq, NightAmbEq, night) + glow * 1.4f + aurora;
            RenderSettings.ambientGroundColor = Color.Lerp(DayAmbGround, NightAmbGround, night);

            // fog: the colour of the horizon by day, the brief's twelve metres by night and three in the wall
            var fog = Color.Lerp(SkyDome.Horizon, StormFog, storm * Mathf.Lerp(.6f, 1f, Weather.Gust) * night);
            float density = Mathf.Lerp(.0026f, Mathf.Lerp(.035f, .38f, storm * storm), night);
            density = Mathf.Lerp(density, Mathf.Lerp(.012f, .05f, Weather.Gust), storm * (1f - night));
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;
            var eye = cam != null ? cam : Camera.main;
            if (eye != null) { eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = SkyDome.Horizon; }

            if (sun != null)
            {
                // above the horizon the sun itself; below it, the afterglow as a soft light from the sun's side
                var dir = SkyDome.SunDir;
                var flat = new Vector3(dir.x, 0f, dir.z).normalized;
                float up = Mathf.Max(alt, 7f) * Mathf.Deg2Rad;
                var from = (flat * Mathf.Cos(up) + Vector3.up * Mathf.Sin(up)).normalized;
                sun.transform.rotation = Quaternion.LookRotation(-from);
                float direct = Mathf.InverseLerp(-.5f, 3f, alt);
                float after = Mathf.InverseLerp(-11f, -1f, alt) * .45f * (1f - SkyDome.CloudCover * .5f);
                sun.intensity = Mathf.Max(direct * 1.3f, after) * (1f - storm * .6f);
                sun.color = direct > .01f ? Color.Lerp(new Color(1f, .82f, .66f), new Color(1f, .96f, .89f), Mathf.InverseLerp(0f, 8f, alt))
                                          : Color.Lerp(new Color(.55f, .6f, .85f), new Color(.95f, .6f, .5f), Mathf.InverseLerp(-8f, -1f, alt));
                sun.shadows = direct > .01f ? LightShadows.Soft : LightShadows.None;
                sun.enabled = sun.intensity > .005f;
            }
            if (moon != null)
            {
                float m = Mathf.InverseLerp(0f, 8f, SkyDome.MoonAlt) * SkyDome.MoonLit * (1f - SkyDome.CloudCover * .8f) * (1f - storm);
                var md = SkyDome.MoonDir;
                var mflat = new Vector3(md.x, 0f, md.z).normalized;
                float mup = Mathf.Max(SkyDome.MoonAlt, 4f) * Mathf.Deg2Rad;
                moon.transform.rotation = Quaternion.LookRotation(-(mflat * Mathf.Cos(mup) + Vector3.up * Mathf.Sin(mup)));
                moon.intensity = m * .22f;
                moon.enabled = moon.intensity > .003f;
            }

            Wind.Level = Mathf.Clamp01(Mathf.Max(night, storm) * .85f + Weather.Wind * .3f);
            Wind.Dark = night;
            film?.Set(night, storm * night);
            var fire = SandboxHuntYard.FireLight;
            if (fire != null) fire.intensity = (3.2f + .8f * Mathf.PerlinNoise(Time.time * 6f, 0f)) * Mathf.Lerp(1f, 1.4f, night);
            var prints = SnowPrints.Instance;
            if (prints != null) { prints.Snowing = true; prints.Storm = storm > .5f; }
        }

        /// <summary>Metres one sees right now, as the rules say it — for the panel.</summary>
        public float Visibility => Night < .5f ? 1000f : HuntRules.Visibility(Storm);
    }
}
