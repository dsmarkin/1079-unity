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
    /// move the night clock (<see cref="SkyDome"/>) and the weather (<see cref="Weather"/>) itself.</summary>
    public sealed class DemoReel : MonoBehaviour
    {
        public const int Width = 1280, Height = 720, Fps = 30;

        /// <summary>Seconds into the night the reel wants the sky to show, or −1 to leave the sky alone.</summary>
        public static float NightSeconds = -1f;
        /// <summary>The blizzard the reel wants, 0…1, or −1 to leave the weather alone.</summary>
        public static float StormWanted = -1f;
        public static bool Running { get; private set; }

        /// <summary>One shot: where the camera goes, what it looks at, and what the world does while it films.</summary>
        sealed class Shot
        {
            public string Caption = "";
            public float Seconds = 3.5f;
            public Place Place;
            /// <summary>Called once the place is standing, before the first frame.</summary>
            public Action Prepare;
            /// <summary>Eye and target for a moment of the shot, t going 0 → 1.</summary>
            public Func<float, (Vector3 eye, Vector3 look)> Fly;
            /// <summary>Called every frame with the same t: weather, clocks, machinery.</summary>
            public Action<float> Each;
        }

        RenderTexture rt;
        Texture2D frame;
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
            rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "DemoReel" };
            frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            string folder = Folder();
            file = new FileStream(Path.Combine(folder, "demo.mjpeg"), FileMode.Create, FileAccess.Write);
            sheet = new StreamWriter(Path.Combine(folder, "demo.txt"), false);
            Debug.Log($"1079 демо: пишу {Path.Combine(folder, "demo.mjpeg")}, {Width}×{Height} @ {Fps}");
            Build();
            StartCoroutine(Film());
        }

        // ── the shot list ─────────────────────────────────────────────────────────────────────────────────
        static float Ground(float x, float z) => Bootstrap.Dem != null ? TerrainBuilder.Height(Bootstrap.Dem, x, z) : 0f;

        /// <summary>A point that is always at least <paramml name="over"/> metres above the snow, whatever the ground does.</summary>
        static Vector3 Over(float x, float z, float over) => new Vector3(x, Ground(x, z) + over, z);

        /// <summary>Smooth both ends of a shot, so nothing starts or stops with a jerk.</summary>
        static float Ease(float t) => t * t * (3f - 2f * t);

        static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, Ease(t));

        void Build()
        {
            var azau = Elbrus.Azau; var mir = Elbrus.Mir; var top = Elbrus.WestSummit;
            var gara = Elbrus.Garabashi; var barrels = Elbrus.Barrels;

            // 1 — the meadow at Azau: down onto the village, then up to the summit
            shots.Add(new Shot
            {
                Caption = "Поляна Азау, 2350 м · июльский день",
                Place = Place.Elbrus, Seconds = 3.6f,
                Fly = t =>
                {
                    var eye = Lerp(Over(azau.X + 150f, azau.Z - 172f, 12f), Over(azau.X + 52f, azau.Z - 66f, 4.5f), t);
                    var look = Lerp(Over(azau.X + 4f, azau.Z - 6f, 6f), new Vector3(top.X, Ground(top.X, top.Z) + 60f, top.Z), Ease(t) * .8f);
                    return (eye, look);
                },
            });

            // 2 — a gondola cabin on the move, filmed from alongside: the rope, the towers, the valley under it
            RopewayRig gondola = null; int car = -1;
            shots.Add(new Shot
            {
                Caption = "Канатная дорога работает · шесть очередей",
                Place = Place.Elbrus, Seconds = 3.6f,
                Prepare = () =>
                {
                    gondola = null; car = -1;
                    foreach (var r in ElbrusWorld.Lines)
                        if (r != null && r.Spec.Id == "gondola1") gondola = r;
                    if (gondola == null && ElbrusWorld.Lines.Count > 0) gondola = ElbrusWorld.Lines[0];
                    if (gondola == null) return;
                    float best = float.MaxValue;
                    for (int i = 0; i < gondola.Line.Cars; i++)
                    {
                        var c = gondola.Line.CarAt(i, RopewayRig.Clock);
                        if (!c.Up) continue;
                        float d = Mathf.Abs(c.S / gondola.Line.Length - .42f);
                        if (d < best) { best = d; car = i; }
                    }
                },
                Each = _ => RopewayRig.Clock += Time.deltaTime * 5.0,      // on top of the line's own pace
                Fly = t =>
                {
                    var cabin = gondola != null && car >= 0 ? gondola.Car(car) : null;
                    if (cabin == null) return (Over(azau.X, azau.Z - 60f, 30f), Over(azau.X, azau.Z, 4f));
                    var p = cabin.position;
                    // alongside and a little behind, swinging in towards the cabin as it climbs
                    var side = Vector3.Cross(Vector3.up, cabin.forward).normalized;
                    var eye = p + side * Mathf.Lerp(16f, 9f, Ease(t)) - cabin.forward * Mathf.Lerp(14f, 4f, Ease(t)) + Vector3.up * Mathf.Lerp(7f, 2.5f, Ease(t));
                    return (eye, p);
                },
            });

            // 3 — over the Mir station: the hall, the cafes, the stalls, the monument
            shots.Add(new Shot
            {
                Caption = "Станция «Мир», 3500 м · кафе, музей, ратраки",
                Place = Place.Elbrus, Seconds = 3.2f,
                Fly = t =>
                {
                    var eye = Lerp(Over(mir.X + 150f, mir.Z - 170f, 52f), Over(mir.X + 28f, mir.Z - 34f, 16f), t);
                    return (eye, Over(mir.X, mir.Z + 6f, 6f));
                },
            });

            // 4 — Gara-Bashi, 3847: the barrels, the huts and the snow-cats under the summit
            shots.Add(new Shot
            {
                Caption = "Гара-Баши, 3847 м · бочки, приюты, ратраки",
                Place = Place.Elbrus, Seconds = 3.4f,
                Fly = t =>
                {
                    float a = Mathf.Lerp(150f, 196f, Ease(t)) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(150f, 105f, Ease(t));
                    var eye = Over(barrels.X + Mathf.Sin(a) * r, barrels.Z + Mathf.Cos(a) * r, Mathf.Lerp(46f, 24f, Ease(t)));
                    var look = Vector3.Lerp(Over(barrels.X, barrels.Z, 3f), new Vector3(top.X, Ground(top.X, top.Z) + 20f, top.Z), Ease(t) * .45f);
                    return (eye, look);
                },
            });

            // ── the pass, the evening and the night ───────────────────────────────────────────────────────
            var camp = WorldData.Camp; var tent = WorldData.Tent; var cedar = WorldData.Cedar;

            // 5 — the camp of 31 January: the fire on its log raft, the skis in the snow, the tent behind
            shots.Add(new Shot
            {
                Caption = "Холатчахль, 1 февраля 1959 · ночёвка в лесу",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () => { NightSeconds = 0f; StormWanted = 0f; },
                Fly = t =>
                {
                    float a = Mathf.Lerp(28f, 74f, Ease(t)) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(13f, 8f, Ease(t));
                    var eye = Over(camp.x + Mathf.Sin(a) * r, camp.z - Mathf.Cos(a) * r, Mathf.Lerp(3.4f, 2f, Ease(t)));
                    return (eye, Over(camp.x, camp.z, 1.1f));
                },
            });

            // 6 — a ski track laid through the forest, camera low along it
            Vector3 from = Vector3.zero, to = Vector3.zero;
            shots.Add(new Shot
            {
                Caption = "Лыжня: топчешь — идущему следом легче, ветер заметает",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () =>
                {
                    NightSeconds = 90f; StormWanted = 0f;
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
                    var on = Vector3.Lerp(from, to, .12f + .5f * Ease(t));
                    var eye = Over(on.x, on.z, 1.55f);
                    var ahead = on + dir * 16f;
                    return (eye, Over(ahead.x, ahead.z, .4f));
                },
            });

            // 7 — the tent on the open slope and the front that comes in over it
            shots.Add(new Shot
            {
                Caption = "Пурга приходит · видимость падает до метров",
                Place = Place.Kholat, Seconds = 3.8f,
                Prepare = () => { NightSeconds = 120f; StormWanted = 1f; },
                Fly = t =>
                {
                    var eye = Lerp(Over(tent.X + 30f, tent.Z - 34f, 3.2f), Over(tent.X + 11f, tent.Z - 13f, 2.1f), t);
                    return (eye, Over(tent.X, tent.Z, 1.3f));
                },
            });

            // 8 — deep night over the pass: the tent, the stars, the moon and the aurora above it
            shots.Add(new Shot
            {
                Caption = "Ночь считает хост: холод, руки, ясность, исходы",
                Place = Place.Kholat, Seconds = 3.6f,
                Prepare = () => { NightSeconds = 700f; StormWanted = 0f; },
                Fly = t =>
                {
                    var eye = Lerp(Over(tent.X + 13f, tent.Z - 15f, 2.4f), Over(tent.X + 34f, tent.Z - 40f, 15f), t);
                    var look = Lerp(Over(tent.X, tent.Z, 2f), Over(tent.X, tent.Z, 34f), Ease(t) * .8f);
                    return (eye, look);
                },
            });
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
                shot.Prepare?.Invoke();
                yield return null;

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
            cam.fieldOfView = 62f;
            if (cam.farClipPlane < 20000f) cam.farClipPlane = 20000f;
        }

        void Capture(float fade)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var was = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = was;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
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

            var jpg = frame.EncodeToJPG(88);
            file.Write(jpg, 0, jpg.Length);
            written++;
        }

        void Finish()
        {
            Time.captureFramerate = 0;
            NightSeconds = -1f; StormWanted = -1f;
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
            NightSeconds = -1f; StormWanted = -1f;
            Running = false;
            if (file != null) { file.Flush(); file.Dispose(); file = null; }
            if (sheet != null) { sheet.Flush(); sheet.Dispose(); sheet = null; }
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (frame != null) Destroy(frame);
        }
    }
}
