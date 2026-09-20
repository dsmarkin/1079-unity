using System;
using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Every knob that trades looks for frames, in one place, remembered between runs.
    ///
    /// Before this existed the game had no way to draw itself cheaper. <see cref="Bootstrap"/> only ever raised the
    /// machine's own quality level with <c>Mathf.Max</c>, so a slow computer was stuck with whatever Unity's default
    /// profile decided; there was no vsync switch, and the player rendered at the display's native resolution — on a
    /// Retina screen that is 3420×2146, seven and a third million pixels a frame, most of them spent on grass.
    ///
    /// Three knobs, because three is what a player will actually use:
    /// <list type="bullet">
    /// <item><b>Картинка</b> (<see cref="Scale"/>) — the render resolution as a fraction of the display. 0.8 costs 36 %
    /// fewer pixels and 0.7 costs half, which on a scene that is fill-bound (grass is two passes of alpha test over the
    /// whole lower screen) buys more than everything else here put together.</item>
    /// <item><b>Качество</b> (<see cref="Level"/>) — shadows, grass and trees together, because they are the three
    /// distances that cost, and because a player does not want to tune them separately.</item>
    /// <item><b>Вертикальная синхронизация</b> — off is what lets anybody, including us, see how much headroom there
    /// actually is. With it on, «60 fps» only ever means «60 or better».</item>
    /// </list>
    ///
    /// Nothing here is a Core rule: it changes how the world is drawn, never what it is. <see cref="Changed"/> is
    /// raised on every change so the terrain can pick up the new distances without a restart.</summary>
    public static class Quality
    {
        public enum Level { Low = 0, Medium = 1, High = 2 }

        const string LevelKey = "1079.quality.level", ScaleKey = "1079.quality.scale", VSyncKey = "1079.quality.vsync";

        /// <summary>The render scales offered, in the order the buttons stand.</summary>
        public static readonly float[] Scales = { .7f, .8f, .9f, 1f };

        static bool loaded;
        static Level level = Level.High;
        static float scale = 1f;
        static bool vsync = true;
        static int nativeW, nativeH;

        /// <summary>Raised after anything here changes, on the main thread. The terrain listens.</summary>
        public static event Action Changed;

        public static Level Current { get { Load(); return level; } }
        public static float Scale { get { Load(); return scale; } }
        public static bool VSync { get { Load(); return vsync; } }

        /// <summary>The display as the player actually has it, before we scale anything — kept up to date by
        /// <see cref="NoticeScreen"/>, because once we have scaled, the current size is our own answer and not the
        /// display's.</summary>
        public static Vector2Int Native => new Vector2Int(Mathf.Max(640, nativeW), Mathf.Max(400, nativeH));

        /// <summary>What the game is drawing right now, for the line under the buttons.</summary>
        public static Vector2Int Rendered => new Vector2Int(Screen.width, Screen.height);

        // ── the three distances ───────────────────────────────────────────────────────────────────────────

        /// <summary>Metres of grass around the player. The Azau meadow is the only place in the game with any, and at
        /// 95 m it was forty to a hundred thousand billboards on a two-pass waving shader — the dip on the meadow.</summary>
        public static float DetailDistance => Current switch { Level.Low => 30f, Level.Medium => 45f, _ => 55f };

        /// <summary>Fraction of the grass the terrain actually plants (the editor's density is the ceiling).</summary>
        public static float DetailDensity => Current switch { Level.Low => .45f, Level.Medium => .7f, _ => 1f };

        /// <summary>Metres of trees. A narrow map is a forest at arm's length; a wide one looks down a valley with
        /// fifty thousand pines in it, and those pines are the one thing left that separates a high frame from a
        /// medium one there — at half a kilometre they are already specks, so high stops at 520 rather than 700.
        /// <paramref name="wide"/> is the map twelve kilometres across, not a map by name (ARCHITECTURE.md).</summary>
        public static float TreeDistance(bool wide) => Current switch
        {
            Level.Low => wide ? 350f : 600f,
            Level.Medium => wide ? 450f : 900f,
            _ => wide ? 520f : 1400f,
        };

        public static float ShadowDistance(bool wide) => Current switch
        {
            Level.Low => 45f,
            Level.Medium => wide ? 90f : 70f,
            _ => wide ? 140f : 110f,
        };

        /// <summary>Every cascade re-renders every shadow caster, and a day lit by one sun over 140 m does not need
        /// four of them: two cost three or four milliseconds less on the view down the Baksan valley and the join is
        /// not visible at this distance.</summary>
        static int Cascades => Current switch { Level.Low => 1, _ => 2 };

        /// <summary>The night is lit by torches, a stove and a fire; a day by one sun. Six per-pixel lights are a
        /// night number, and on a daylight map they were being paid for indoor lamps burning in broad daylight.</summary>
        static int PixelLights(bool day) => Current switch
        {
            Level.Low => day ? 1 : 3,
            Level.Medium => day ? 2 : 4,
            _ => day ? 2 : 6,
        };

        // ── reading and writing the player's answer ───────────────────────────────────────────────────────

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                level = (Level)Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, (int)Level.High), 0, 2);
                scale = Nearest(PlayerPrefs.GetFloat(ScaleKey, 1f));
                vsync = PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            }
            catch { /* a player with no writable prefs simply starts on high every time */ }
        }

        static float Nearest(float want)
        {
            float best = Scales[Scales.Length - 1], gap = float.MaxValue;
            foreach (float s in Scales) { float d = Mathf.Abs(s - want); if (d < gap) { gap = d; best = s; } }
            return best;
        }

        static void Remember()
        {
            try
            {
                PlayerPrefs.SetInt(LevelKey, (int)level);
                PlayerPrefs.SetFloat(ScaleKey, scale);
                PlayerPrefs.SetInt(VSyncKey, vsync ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch { /* not worth a word to the player */ }
        }

        public static void Set(Level next)
        {
            Load();
            if (next == level) return;
            level = next;
            Remember();
            Apply();
        }

        public static void SetScale(float next)
        {
            Load();
            next = Nearest(next);
            if (Mathf.Approximately(next, scale)) return;
            scale = next;
            Remember();
            Apply();
        }

        public static void SetVSync(bool on)
        {
            Load();
            if (on == vsync) return;
            vsync = on;
            Remember();
            Apply();
        }

        // ── putting it into the engine ────────────────────────────────────────────────────────────────────

        /// <summary>Called from <see cref="Bootstrap"/> before the world is built, on every place change and on every
        /// change of a knob.</summary>
        public static void Apply()
        {
            Load();
            NoticeScreen();

            // two questions about the map we are in, and neither of them is its name: how far across it is, and
            // whether it is walked at night (ILocationView)
            bool wide = Height1079.Core.World.Size > 8000f;
            bool day = !LocationViews.RunsNight;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            // without vsync the frame rate is worth capping somewhere sane: a menu at 900 fps only heats the machine
            Application.targetFrameRate = vsync ? -1 : 144;
            QualitySettings.shadowDistance = ShadowDistance(wide);
            QualitySettings.shadowCascades = Cascades;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.pixelLightCount = PixelLights(day);
            // soft vegetation draws the grass alpha-blended instead of alpha-tested, and grass fills the bottom half
            // of the screen on the meadow: it was most of the difference between the high preset and the medium one
            // there, for a softness nobody looks at while walking
            QualitySettings.softVegetation = false;
            QualitySettings.lodBias = level switch { Level.Low => .7f, Level.Medium => 1f, _ => 1.15f };
            QualitySettings.shadows = level == Level.Low ? ShadowQuality.HardOnly : ShadowQuality.All;

            int w = Mathf.Max(640, Mathf.RoundToInt(nativeW * scale));
            int h = Mathf.Max(400, Mathf.RoundToInt(nativeH * scale));
            askedW = w; askedH = h;
            if (w != Screen.width || h != Screen.height) Screen.SetResolution(w, h, Screen.fullScreenMode);

            try { Changed?.Invoke(); } catch (Exception e) { Debug.LogWarning("1079: настройки качества — " + e.Message); }
        }

        /// <summary>The display can change under us: the player starts in a window and presses the green button, or
        /// moves the game to another monitor. While we are not scaling anything, whatever the screen reports is the
        /// native size by definition; while we are, it can still only have grown. Called every frame from the HUD —
        /// two comparisons — and before every <see cref="Apply"/>, because getting this wrong once would pin the game
        /// to the size of the window it happened to boot in.</summary>
        public static void NoticeScreen()
        {
            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;
            if (nativeW == 0) { nativeW = w; nativeH = h; return; }
            // Only a size we did not ask for, and only a bigger one, is news about the display. Anything we asked for
            // is our own scaling coming back at us, and adopting that as the display would ratchet the game down a
            // step every time the player went back to 100 %.
            if ((w != askedW || h != askedH) && w > nativeW) { nativeW = w; nativeH = h; }
        }
        static int askedW, askedH;

        public static string LevelTitle(Level l) => l switch
        {
            Level.Low => "НИЗКОЕ", Level.Medium => "СРЕДНЕЕ", _ => "ВЫСОКОЕ",
        };
    }
}
