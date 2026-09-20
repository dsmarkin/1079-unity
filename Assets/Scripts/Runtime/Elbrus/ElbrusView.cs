using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>The engine half of the southern slope: what the shell asks a map for once the terrain is standing —
    /// the dressing and the light, the menu camera, the frame of a day, and what «Продолжить» has to offer.
    /// The rules half is <see cref="ElbrusLocation"/>, in <c>Height1079.Elbrus.Core</c>, which knows no engine.
    ///
    /// This class is also where the map puts itself on the game's map: nothing in the shell names Elbrus, so if
    /// nobody calls <see cref="Register"/> the location does not exist — the menu will not list it and
    /// <see cref="World.Current"/> will refuse it. The hooks below are the same two the Steam layer uses
    /// (Assets/Scripts/Steam/SteamLobby.cs), and both are safe to run twice.</summary>
    public sealed class ElbrusView : ILocationView
    {
        static readonly ElbrusView Instance = new ElbrusView();
        ElbrusView() { }

        /// <summary>In a player: before the first scene, so the menu already has the map when it is built.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ElbrusLocation.Register();
            LocationViews.Register(Instance);
            DemoReel.Extras -= ElbrusShots.Add;
            DemoReel.Extras += ElbrusShots.Add;
            UiWindows.Watch(WindowOpen);
        }

        /// <summary>The counters of the southern slope, for the keys they take over while their window is up
        /// (<see cref="UiWindows"/>). A named method and not a lambda, so registering twice still leaves one.</summary>
        static bool WindowOpen() =>
            RentalService.Ordering || CafeService.Ordering || LodgeService.Booking
            || RescueDesk.Filing || Ratraks.Hailing || WeatherBoards.Reading;

#if UNITY_EDITOR
        /// <summary>And in the editor, where no player ever starts: the factories, the tests run from the menu and
        /// the scene that is generated all ask <see cref="Locations"/> what places there are. Fully qualified on
        /// purpose — a <c>using UnityEditor</c> in a runtime assembly is a trap waiting for the next reader.</summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterInEditor() => Register();
#endif

        public Place Place => Place.Elbrus;

        /// <summary>Must match <c>ElbrusImporter.BaseHeight</c> in the editor.</summary>
        public float GroundMargin => 6f;

        public bool RunsNight => false;

        /// <summary>The mountain's own blizzard, as a share of the full wind (<see cref="MountainDay"/>).</summary>
        public float DayWind => MountainDay.WindShare;

        /// <summary>What the terrain thinks is under the visitor's feet at Azau. The southern slope has no snow at
        /// 2 350 m, so if the first weight comes back as 1 the splat map did not survive the import and the ground
        /// renders as snow.</summary>
        public void Inspect(Terrain terrain)
        {
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null) return;
            var (x, z) = Elbrus.Start;
            int n = data.alphamapResolution;
            int c = Mathf.Clamp(Mathf.RoundToInt((x + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            int r = Mathf.Clamp(Mathf.RoundToInt((z + Elbrus.Half) / Elbrus.Size * (n - 1)), 0, n - 1);
            var a = data.GetAlphamaps(c, r, 1, 1);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < data.alphamapLayers; k++)
            {
                var layer = k < data.terrainLayers.Length ? data.terrainLayers[k] : null;
                string tex = layer != null && layer.diffuseTexture != null ? layer.diffuseTexture.name : "нет текстуры";
                sb.Append($" {(layer != null ? layer.name : "null")}({tex})={a[0, 0, k]:0.00}");
            }
            Debug.Log($"1079 Эльбрус: грунт у Азау, слоёв {data.alphamapLayers}, alphamap {n}:{sb}");
        }

        /// <summary>Ropeways, the Gara-Bashi camp and the summit route, in daylight. None of the 1959 dressing applies
        /// here — no camp fire, no archive layer, no night.</summary>
        public Light Build(HeightField dem)
        {
            var sunGo = new GameObject("Sun", typeof(Light));
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            ElbrusWorld.Daylight(sun);
            ElbrusWorld.Build(dem);
            CampView.Create();
            return sun;
        }

        /// <summary>The menu looks up from above Azau towards the Western summit.</summary>
        public bool MenuCamera(Camera cam, HeightField dem)
        {
            if (cam == null || dem == null) return false;
            var a = Elbrus.Azau; var top = Elbrus.WestSummit;
            float ay = dem.Sample(a.X, a.Z) + 60f;
            cam.transform.position = new Vector3(a.X + 120f, ay, a.Z - 260f);
            cam.transform.LookAt(new Vector3(top.X, dem.Sample(top.X, top.Z), top.Z));
            cam.nearClipPlane = .1f; cam.farClipPlane = 20000f;
            return true;
        }

        /// <summary>A clear day on the southern slope: aerial perspective and nothing else.</summary>
        const float ClearFog = .00012f;
        static readonly Color ClearHaze = new Color(.66f, .76f, .88f), CloudHaze = new Color(.84f, .87f, .9f);

        /// <summary>Daylight: nothing of the night machinery runs and the film stays clean — but the sky of the day is
        /// a <em>rule</em> before it is a picture. When <see cref="AscentRoute.VisibilityM"/> says a party can see
        /// three hundred metres, the player has to be unable to see further either, or the mountain is lying to him in
        /// the one place it must not (docs/ELBRUS.md, «Как это вызывается из рантайма»).</summary>
        public bool Frame(out float darkness, out float storm)
        {
            darkness = 0f; storm = 0f;
            var cam = Camera.main;
            if (cam != null && cam.farClipPlane < 15000f) cam.farClipPlane = 20000f;

            float vis = AscentRoute.VisibilityM(Climb.Sky(Weather.Storm));
            float want = vis >= 2000f ? ClearFog : Mathf.Clamp(1.2f / Mathf.Max(60f, vis), ClearFog, .025f);
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, want, Time.deltaTime * .006f);
            RenderSettings.fogColor = Color.Lerp(ClearHaze, CloudHaze, Mathf.InverseLerp(2000f, 120f, vis));
            return true;
        }

        /// <summary>The bearing the guide's programme is on right now, for the brass index of the compass.</summary>
        public bool Course(HikerController hiker, out float headingDeg)
        {
            var aim = Programmes.Aim(hiker);
            headingDeg = aim.HeadingDeg;
            return aim.Has;
        }

        // ── «Продолжить» ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The newest save of the ascent, worded for the button: «ПРОДОЛЖИТЬ · косая полка, 5290 м · 08:40 ·
        /// вчера», or null when there is nothing to go back to.</summary>
        public string Saved
        {
            get
            {
                var slot = Saves.Newest(Place.Elbrus);
                if (slot.IsEmpty) return null;
                string where = string.IsNullOrEmpty(slot.Where) ? "склон" : slot.Where;
                return $"ПРОДОЛЖИТЬ · {where} · {AscentRoute.Clock(slot.Hour)} · " +
                       SaveStore.When(slot.SavedUtc.ToLocalTime(), System.DateTime.Now);
            }
        }

        /// <summary>Read the newest save and park it in <see cref="Saves.Pending"/>, where the host's
        /// <see cref="NightSession"/> takes it as it spawns and puts the clock, the weather and the tent back before
        /// anybody is placed.</summary>
        public string Resume()
        {
            var slot = Saves.Newest(Place.Elbrus);
            if (slot.IsEmpty) return null;
            var save = Saves.Read(slot.Path);
            if (save == null) return null;
            Saves.Hold(save);
            return $"Продолжаем: {save.Where}, {AscentRoute.Clock(save.Hour)}.";
        }

        public void Drop() => Saves.Forget();
    }
}
