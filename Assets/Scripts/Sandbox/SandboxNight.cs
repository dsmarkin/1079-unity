using UnityEngine;
using Height1079.Core;
using Height1079.Night;
using Height1079.Snow;

namespace Height1079.Sandbox
{
    /// <summary>Night over the range, and the weather in it. The sandbox comes up in daylight so a leg spring can be
    /// read; a run of the yard (<see cref="SandboxHunt"/>) asks for the night and this brings it down — the light
    /// goes, the fog closes to what the brief allows (twelve metres without a torch), and then the blizzard walls
    /// it to three — and takes it away again when the run is over.
    ///
    /// The weather is the game's own (<see cref="Weather"/>, driving snow, ground drift, gusts), driven off the
    /// run's storm clock instead of the session's; the wind in the ears is the game's two loops
    /// (<see cref="WindSound"/>); the vignette and grain are the game's film (<see cref="NightFilm"/>). Only the
    /// light and fog numbers are the sandbox's, because its sky is a solid colour and the mountain's is a dome.</summary>
    public sealed class SandboxNight : MonoBehaviour
    {
        /// <summary>0 day … 1 night, what is asked for; the change takes a few seconds.</summary>
        public float Want;
        /// <summary>0 … 1 the blizzard as the run clock says it; the weather and the fog follow it.</summary>
        public float Storm;
        public float Night { get; private set; }

        public Weather Weather { get; private set; }
        public WindSound Wind { get; private set; }
        NightFilm film;
        Light sun;
        // the day the range was built with, read back rather than repeated
        Color daySky, dayEq, dayGround, dayFog, dayCam; float dayFogDensity, daySun;
        static readonly Color NightSky = new Color(.045f, .055f, .085f), NightEq = new Color(.028f, .034f, .05f), NightGround = new Color(.02f, .024f, .034f);
        static readonly Color NightFog = new Color(.03f, .036f, .052f), StormFog = new Color(.16f, .17f, .19f);

        public static SandboxNight Create(Transform parent, Camera cam)
        {
            var go = new GameObject("SandboxNight", typeof(SandboxNight));
            go.transform.SetParent(parent, false);
            var n = go.GetComponent<SandboxNight>();
            n.Setup(cam);
            return n;
        }

        void Setup(Camera cam)
        {
            daySky = RenderSettings.ambientSkyColor; dayEq = RenderSettings.ambientEquatorColor; dayGround = RenderSettings.ambientGroundColor;
            dayFog = RenderSettings.fogColor; dayFogDensity = RenderSettings.fogDensity;
            dayCam = cam != null ? cam.backgroundColor : dayFog;
            var sunGo = GameObject.Find("Sun");
            sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
            daySun = sun != null ? sun.intensity : 1.25f;
            Weather = Weather.Create(transform);
            Weather.Live = false;
            Wind = WindSound.Create(transform);
            Wind.Level = 0f;
            // its own root: an overlay canvas is happiest there; taken down with this object below
            film = NightFilm.Create();
        }

        void OnDestroy()
        {
            if (film != null) Destroy(film.gameObject);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Night = Mathf.MoveTowards(Night, Mathf.Clamp01(Want), dt / 4f);
            float n = Night, storm = Mathf.Clamp01(Storm) * n;

            Weather.Want = Storm; Weather.Live = n > .05f;
            Weather.RiseSeconds = 8f; Weather.FallSeconds = 12f;
            var boot = SandboxBoot.Instance;
            Weather.GroundY = boot != null && boot.Body != null ? boot.Body.Torso.position.y - boot.Tuning.HoverHeight : float.NaN;
            Wind.Level = n; Wind.Dark = n;

            RenderSettings.ambientSkyColor = Color.Lerp(daySky, NightSky, n);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayEq, NightEq, n);
            RenderSettings.ambientGroundColor = Color.Lerp(dayGround, NightGround, n);
            // the night fog: what a torch can still reach through; the wall: the brief's three metres
            var fog = Color.Lerp(dayFog, Color.Lerp(NightFog, StormFog, storm * Mathf.Lerp(.6f, 1f, Weather.Gust)), n);
            float density = Mathf.Lerp(dayFogDensity, Mathf.Lerp(.035f, .38f, storm * storm), n);
            RenderSettings.fog = true;
            RenderSettings.fogColor = fog; RenderSettings.fogDensity = density;
            var cam = boot != null ? boot.Cam : Camera.main;
            if (cam != null) cam.backgroundColor = Color.Lerp(dayCam, fog, n);
            if (sun != null)
            {
                // overcast, no moon: a whisper of blue from above so the snow keeps its shape, no shadows
                sun.intensity = Mathf.Lerp(daySun, .05f, n) * (1f - .5f * storm);
                sun.color = Color.Lerp(new Color(1f, .96f, .89f), new Color(.6f, .68f, .9f), n);
                sun.shadows = n > .5f ? LightShadows.None : LightShadows.Soft;
            }
            film?.Set(n, storm);
            var prints = SnowPrints.Instance;
            if (prints != null) { prints.Snowing = n > .5f; prints.Storm = storm > .5f; }
        }

        /// <summary>Metres one sees right now, as the rules say it — for the panel.</summary>
        public float Visibility => Night < .5f ? 1000f : HuntRules.Visibility(Storm);
    }
}
