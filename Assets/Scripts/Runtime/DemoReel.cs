using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>A scripted fly-through that films itself: eight shots over both places — the green Azau meadow, a ride in
    /// a gondola cabin, the Mir station, the firn under the summit, then the camp at dusk, a ski track, the blizzard
    /// coming in and the cedar fire under the night sky.
    ///
    /// Frames are written as one stream of JPEGs (an .mjpeg file, which ffmpeg reads with <c>-f mjpeg</c>), so a
    /// half-minute of video is one file instead of nine hundred. <see cref="Time.captureFramerate"/> pins the clock to
    /// 1/30 s a frame, so the result is smooth however slowly the thing renders.
    ///
    /// It needs no network session: the world is already standing behind the menu, and the two hooks below let the reel
    /// move the night clock (read by <c>Bootstrap.Atmosphere.GameSky</c>, which feeds <c>Night/SkyDome</c>) and the
    /// weather (<c>Night/Weather</c>) itself.</summary>
    public sealed class DemoReel : MonoBehaviour
    {
        /// <summary>The film is rendered large and with full multisampling, then shrunk to this before it is written:
        /// a Retina screen supersamples the game for free, and a bare 720p grab next to it looks like a different game —
        /// every rope, branch and grass blade crawls. Rendering 2560×1440 with 8× MSAA and resolving down to 1080p gives
        /// a cleaner frame than the screen itself.</summary>
        public const int Width = 1920, Height = 1080, Fps = 30;
        public const int RenderWidth = 2560, RenderHeight = 1440, Samples = 8;

        /// <summary>Seconds into the night the reel wants the sky to show, or −1 to leave the sky alone.</summary>
        public static float NightSeconds = -1f;
        /// <summary>The hour the reel wants on the sky clock, in minutes from midnight, or −1 to take the hour from
        /// <see cref="NightSeconds"/>. The night of 1 February is the game; a blizzard filmed in it is a black rectangle,
        /// and the same front at one in the afternoon is the white wall it actually is.</summary>
        public static float ClockMinutes = -1f;
        /// <summary>The blizzard the reel wants, 0…1, or −1 to leave the weather alone.</summary>
        public static float StormWanted = -1f;
        public static bool Running { get; private set; }

        /// <summary>Shots a map contributes out of its own assembly, added to the list before the reel's own. A map
        /// that is not compiled into this build subscribes nothing and is simply not filmed (docs/ELBRUS.md).</summary>
        public static event Action<List<Shot>> Extras;

        /// <summary>One shot: where the camera goes, what it looks at, and what the world does while it films.</summary>
        public sealed class Shot
        {
            public string Caption = "";
            public float Seconds = 3.5f;
            /// <summary>Field of view for the shot; 60° is what the hiker sees.</summary>
            public float Fov = 60f;
            /// <summary>Seconds the world runs unfilmed after <see cref="Prepare"/>: the weather of the shot before has to
            /// clear, the snow has to fall out of the air and the sky has to move to the hour the shot wants.</summary>
            public float Settle = 1.9f;
            public Place Place;
            /// <summary>Called once the place is standing, before the first frame.</summary>
            public Action Prepare;
            /// <summary>Eye and target for a moment of the shot, t going 0 → 1.</summary>
            public Func<float, (Vector3 eye, Vector3 look)> Fly;
            /// <summary>Called every frame with the same t: weather, clocks, machinery.</summary>
            public Action<float> Each;
        }

        RenderTexture rt, shrunk;
        Light lamp;
        Texture2D frame;
        int wasAa, wasCascades; float wasShadow, wasLod; AnisotropicFiltering wasAniso;
        FileStream file;
        StreamWriter sheet;
        int written;
        readonly List<Shot> shots = new List<Shot>();

        public static DemoReel Shoot()
        {
            var go = new GameObject("DemoReel", typeof(DemoReel));
            DontDestroyOnLoad(go);
            return go.GetComponent<DemoReel>();
        }

        /// <summary>Where the two files go: beside the player (Builds/mac) or, in the editor, beside the project.</summary>
        static string Folder()
        {
            string data = Application.dataPath;
            if (Application.isEditor) return Directory.GetCurrentDirectory();
            // …/1079.app/Contents/Resources/Data → …/Builds/mac
            var dir = new DirectoryInfo(data);
            for (int i = 0; i < 4 && dir.Parent != null; i++) dir = dir.Parent;
            return dir.FullName;
        }

        void Start()
        {
            Running = true;
            rt = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "DemoReel", antiAliasing = Samples, filterMode = FilterMode.Bilinear,
            };
            shrunk = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) { name = "DemoReelOut", filterMode = FilterMode.Bilinear };
            frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Lavish();
            string folder = Folder();
            file = new FileStream(Path.Combine(folder, "demo.mjpeg"), FileMode.Create, FileAccess.Write);
            sheet = new StreamWriter(Path.Combine(folder, "demo.txt"), false);
            Debug.Log($"1079 демо: пишу {Path.Combine(folder, "demo.mjpeg")}, {RenderWidth}×{RenderHeight} → {Width}×{Height} @ {Fps}, MSAA {Samples}");
            Build();
            StartCoroutine(Film());
        }

        // ── the shot list ─────────────────────────────────────────────────────────────────────────────────
        public static float Ground(float x, float z) => Bootstrap.Dem != null ? TerrainBuilder.Height(Bootstrap.Dem, x, z) : 0f;

        /// <summary>A point that is always at least <paramml name="over"/> metres above the snow, whatever the ground does.</summary>
        public static Vector3 Over(float x, float z, float over) => new Vector3(x, Ground(x, z) + over, z);

        /// <summary>Smooth both ends of a shot, so nothing starts or stops with a jerk.</summary>
        public static float Ease(float t) => t * t * (3f - 2f * t);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, Ease(t));

        void Build()
        {
            // the maps that live in their own assembly put their shots in first; one that is not in this build
            // puts none in and the reel simply starts with the pass (docs/ELBRUS.md)
            Extras?.Invoke(shots);

            // ── the pass: the same day, then the evening and the night ────────────────────────────────────
            var camp = WorldData.Camp; var tent = WorldData.Tent;

            // 5 — a ski track through the forest, camera down on it at a skier's pace
            Vector3 from = Vector3.zero, to = Vector3.zero;
            shots.Add(new Shot
            {
                Caption = "Лыжня: топчешь — идущему следом легче, ветер заметает",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () =>
                {
                    NightSeconds = -1f; StormWanted = 0f;      // late afternoon, before the night starts counting
                    // lay the track by hand: nobody is on skis in the world behind the menu
                    from = new Vector3(camp.x + 34f, 0f, camp.z - 46f);
                    to = new Vector3(camp.x - 18f, 0f, camp.z + 30f);
                    var fx = SkiTrailFx.Instance;
                    if (fx == null) return;
                    var dir = (to - from); dir.y = 0f; float len = dir.magnitude; dir /= len;
                    for (float s = 0f; s <= len; s += SkiTrailFx.Step)
                    {
                        var p = from + dir * s;
                        p.y = Ground(p.x, p.z);
                        fx.Lay(p, dir, SkiTrailFx.Step, Time.timeAsDouble);
                    }
                },
                Fly = t =>
                {
                    var dir = (to - from); dir.y = 0f; dir.Normalize();
                    var side = Vector3.Cross(Vector3.up, dir).normalized;
                    // just off the track and low, the way you see it under your own skis
                    var on = Vector3.Lerp(from, to, .14f + .13f * Ease(t)) + side * .9f;
                    var eye = Over(on.x, on.z, 1.45f);
                    var ahead = on + dir * 14f - side * 1.2f;
                    return (eye, Over(ahead.x, ahead.z, .3f));
                },
            });

            // 6 — the front comes over the pass in daylight: driving snow, the trees going out one by one
            shots.Add(new Shot
            {
                Caption = "Пурга приходит · видимость падает до метров",
                Place = Place.Kholat, Seconds = 3.8f,
                Prepare = () => { NightSeconds = -1f; ClockMinutes = 13 * 60 + 10; StormWanted = 0f; },
                Each = t => { ClockMinutes = 13 * 60 + 10; StormWanted = t < .12f ? 0f : 1f; },
                Fly = t =>
                {
                    var eye = Lerp(Over(tent.X + 16f, tent.Z - 18f, 1.7f), Over(tent.X + 7f, tent.Z - 8f, 1.55f), t);
                    return (eye, Over(tent.X, tent.Z, 1.2f));
                },
            });

            // 7 — the camp of 31 January at dusk: the tent, the fire, the skis standing in the snow
            shots.Add(new Shot
            {
                Caption = "Холатчахль, 1 февраля 1959 · ночёвка в лесу",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () => { NightSeconds = 0f; StormWanted = 0f; Lamp(new Vector3(camp.x, Ground(camp.x, camp.z) + 1.1f, camp.z), 2.4f, 16f); },
                Fly = t =>
                {
                    float a = Mathf.Lerp(34f, 76f, Ease(t)) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(8.5f, 6f, Ease(t));
                    var eye = Over(camp.x + Mathf.Sin(a) * r, camp.z - Mathf.Cos(a) * r, 1.6f);
                    return (eye, Over(camp.x, camp.z, 1.1f));
                },
            });

            // 8 — deep night: the lit tent close by, the stars and the aurora coming up over it
            shots.Add(new Shot
            {
                Caption = "Ночь считает хост: холод, руки, ясность, исходы",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () => { NightSeconds = 605f; StormWanted = 0f; Lamp(new Vector3(tent.X, Ground(tent.X, tent.Z) + .9f, tent.Z), 4.2f, 15f); },
                Fly = t =>
                {
                    var eye = Lerp(Over(tent.X + 6.5f, tent.Z - 7.5f, 1.5f), Over(tent.X + 8.5f, tent.Z - 10f, 1.9f), t);
                    var look = Lerp(Over(tent.X, tent.Z, 1.2f), Over(tent.X, tent.Z, 15f), Ease(t) * .8f);
                    return (eye, look);
                },
            });
        }

        /// <summary>Eye height of the hiker, so a shot stands where the player stands rather than hovering over the map.</summary>
        public const float Eye = 1.72f;

        /// <summary>Horizontal unit vector pointing away from the summit: the side of a building a visitor arrives on, and
        /// the side that has the mountain behind it.</summary>
        public static Vector3 Downhill(Vector3 at, Vector3 summit)
        {
            var d = new Vector3(at.x - summit.x, 0f, at.z - summit.z);
            return d.sqrMagnitude < 1e-3f ? Vector3.forward : d.normalized;
        }

        /// <summary>The nearest thing actually standing in the world whose name starts with <paramref name="prefix"/>, so a
        /// shot is framed on what got built rather than on a guess at where it went.</summary>
        public static Transform Nearest(string prefix, Vector3 near, float within)
        {
            Transform best = null; float bd = within * within;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var p = t.position; p.y = 0f;
                float d = (p - new Vector3(near.x, 0f, near.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        /// <summary>A warm light for the night shots — a stove inside the tent, a fire on its raft. Without one the pass at
        /// two in the morning films as a black rectangle, which is true and shows nothing.</summary>
        void Lamp(Vector3 at, float power, float range)
        {
            if (lamp == null)
            {
                var go = new GameObject("DemoLamp");
                go.transform.SetParent(transform, false);
                lamp = go.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(1f, .78f, .5f);
                lamp.shadows = LightShadows.None;
            }
            lamp.transform.position = at;
            lamp.intensity = power; lamp.range = range;
            lamp.enabled = true;
        }

        // ── filming ───────────────────────────────────────────────────────────────────────────────────────
        IEnumerator Film()
        {
            Time.captureFramerate = Fps;
            var hud = Bootstrap.Hud;
            if (hud != null) hud.gameObject.SetActive(false);        // the reel is about the world, not the interface
            yield return null;

            float at = 0f;
            foreach (var shot in shots)
            {
                if (World.Current != shot.Place)
                {
                    Bootstrap.SetPlace(shot.Place);
                    for (int i = 0; i < 3; i++) yield return null;   // let the world stand up before filming it
                }
                if (lamp != null) lamp.enabled = false;
                ClockMinutes = -1f;
                shot.Prepare?.Invoke();
                for (int i = Mathf.RoundToInt(shot.Settle * Fps); i > 0; i--) yield return null;

                int frames = Mathf.Max(2, Mathf.RoundToInt(shot.Seconds * Fps));
                sheet.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.00}|{1:0.00}|{2}", at, shot.Seconds, shot.Caption));
                at += shot.Seconds;

                for (int i = 0; i < frames; i++)
                {
                    float t = i / (float)(frames - 1);
                    shot.Each?.Invoke(t);
                    yield return new WaitForEndOfFrame();
                    Aim(shot, t);
                    // a short fade at both ends of every shot, so the cuts are not straight in the face
                    float fade = Mathf.Min(1f, Mathf.Min(i, frames - 1 - i) / 4f);
                    Capture(fade);
                }
            }

            Finish();
        }

        void Aim(Shot shot, float t)
        {
            var cam = Camera.main;
            if (cam == null || shot.Fly == null) return;
            var (eye, look) = shot.Fly(t);
            var dir = look - eye;
            if (dir.sqrMagnitude < 1e-4f) dir = cam.transform.forward;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(dir.normalized, Vector3.up));
            cam.fieldOfView = shot.Fov;
            if (cam.farClipPlane < 20000f) cam.farClipPlane = 20000f;
        }

        /// <summary>Everything turned up for the half minute the film takes: multisampling, anisotropy, shadows to the
        /// horizon, full LODs, trees and grass drawn far out. None of it has to run at sixty frames a second here — the
        /// clock is pinned to the frame, not to the wall.</summary>
        void Lavish()
        {
            wasAa = QualitySettings.antiAliasing; wasAniso = QualitySettings.anisotropicFiltering;
            wasShadow = QualitySettings.shadowDistance; wasCascades = QualitySettings.shadowCascades; wasLod = QualitySettings.lodBias;
            QualitySettings.antiAliasing = Samples;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.shadowDistance = Mathf.Max(wasShadow, 420f);
            QualitySettings.shadowCascades = 4;
            QualitySettings.lodBias = Mathf.Max(wasLod, 3.5f);
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.softParticles = true;
            foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                t.heightmapPixelError = 1.5f;
                t.basemapDistance = Mathf.Max(t.basemapDistance, 4000f);
                t.detailObjectDistance = 250f;
                t.detailObjectDensity = 1f;
                t.treeDistance = Mathf.Max(t.treeDistance, 4000f);
                t.treeBillboardDistance = 400f;
                t.treeMaximumFullLODCount = 1000;
                t.treeCrossFadeLength = 25f;
            }
        }

        void Plain()
        {
            QualitySettings.antiAliasing = wasAa; QualitySettings.anisotropicFiltering = wasAniso;
            QualitySettings.shadowDistance = wasShadow; QualitySettings.shadowCascades = wasCascades;
            QualitySettings.lodBias = wasLod;
        }

        void Capture(float fade)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var was = cam.targetTexture;
            bool wasMsaa = cam.allowMSAA;
            cam.allowMSAA = true;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = was;
            cam.allowMSAA = wasMsaa;

            // resolve the multisampled frame down to the film size: this is the supersampling that makes it clean
            Graphics.Blit(rt, shrunk);
            var prev = RenderTexture.active;
            RenderTexture.active = shrunk;
            frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = prev;

            if (fade < .999f)
            {
                var px = frame.GetPixels32();
                byte k = (byte)Mathf.RoundToInt(Mathf.Clamp01(fade) * 255f);
                for (int i = 0; i < px.Length; i++)
                {
                    px[i].r = (byte)(px[i].r * k / 255);
                    px[i].g = (byte)(px[i].g * k / 255);
                    px[i].b = (byte)(px[i].b * k / 255);
                }
                frame.SetPixels32(px);
            }
            frame.Apply(false);

            var jpg = frame.EncodeToJPG(93);
            file.Write(jpg, 0, jpg.Length);
            written++;
        }

        void Finish()
        {
            Time.captureFramerate = 0;
            NightSeconds = -1f; StormWanted = -1f; ClockMinutes = -1f;
            Plain();
            file.Flush(); file.Dispose(); file = null;
            sheet.Flush(); sheet.Dispose(); sheet = null;
            Running = false;
            Debug.Log($"1079 демо: готово, кадров {written}, {written / (float)Fps:0.0} с, файл demo.mjpeg в {Folder()}");
            var hud = Bootstrap.Hud;
            if (hud != null) hud.gameObject.SetActive(true);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            Time.captureFramerate = 0;
            NightSeconds = -1f; StormWanted = -1f; ClockMinutes = -1f;
            Running = false;
            if (file != null) { file.Flush(); file.Dispose(); file = null; }
            if (sheet != null) { sheet.Flush(); sheet.Dispose(); sheet = null; }
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (shrunk != null) { shrunk.Release(); Destroy(shrunk); }
            if (frame != null) Destroy(frame);
        }
    }
}
