using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Height1079.Core;
using Debug = UnityEngine.Debug;

namespace Height1079.EditorTools.World
{
    /// <summary>The tourist map of the southern slope — the sheet the player unfolds in the HUD, and the picture the
    /// mini-map window looks at. Hypsometric tints with a hillshade, contours every 100 m, the glaciers, the six
    /// ropeways, the summit and snow-cat routes, every station, hut and landmark, a kilometre grid and a scale bar.
    ///
    /// The PNG covers exactly the Elbrus frame: 12 288 m centred on the origin, x east, z north. Column 0 is
    /// x = −Half and row 0 is z = −Half, so the HUD can map world position to uv linearly (row 0 of a Unity texture
    /// is the bottom row, which is the southern edge — north is up on the sheet).
    ///
    /// Everything is drawn here from numbers: no fonts, no meshes, no source image. Letters come from the little
    /// stroke alphabet at the bottom of the file, so the whole map stays deterministic and the repository text-only.</summary>
    public static class ElbrusMapFactory
    {
        public const int Res = 2048;
        public const string Name = "elbrus_map";
        /// <summary>Metres of ground in one map pixel (6 m — the same step as the Elbrus DEM).</summary>
        public static readonly float Cell = Elbrus.Size / Res;

        static float Xw(int col) => -Elbrus.Half + (col + .5f) * Cell;
        static float Zw(int row) => -Elbrus.Half + (row + .5f) * Cell;
        /// <summary>World metres (x or z) → pixel coordinate on the sheet.</summary>
        static float Pix(float world) => (world + Elbrus.Half) / Cell - .5f;
        static Vector2 P(float x, float z) => new Vector2(Pix(x), Pix(z));

        static readonly Color Ink = new Color(.08f, .09f, .12f);
        static readonly Color Paper = new Color(.97f, .96f, .90f);
        static readonly Color Sepia = new Color(.42f, .31f, .20f);
        static readonly Color IceLine = new Color(.44f, .54f, .68f);
        static readonly Color RouteRed = new Color(.74f, .13f, .11f);
        static readonly Color RatrakGrey = new Color(.32f, .33f, .37f);

        // corners kept clear of place labels: the printed furniture of the sheet
        static readonly Rect TitleBox = new Rect(58, Res - 58 - 104, 636, 104);
        static readonly Rect ScaleBox = new Rect(58, 58, 430, 112);
        static readonly Rect LegendBox = new Rect(Res - 58 - 486, 58, 486, 274);
        static readonly Rect RoseBox = new Rect(Res - 58 - 116, Res - 58 - 138, 116, 138);

        /// <summary>Builds the sheet and writes it to Resources/World/Textures/elbrus_map.png.</summary>
        public static Texture2D Build(HeightField dem)
        {
            if (dem == null) { Debug.LogWarning("1079 Эльбрус: карта не собрана — нет сетки высот"); return null; }
            var clock = Stopwatch.StartNew();

            var elev = new float[Res * Res];
            for (int r = 0; r < Res; r++)
            {
                float z = Zw(r);
                int row = r * Res;
                for (int c = 0; c < Res; c++) elev[row + c] = dem.Sample(Xw(c), z);
            }

            var s = new Sheet(Res, Res);
            Relief(s, elev);
            Grid(s);
            Lines(s);

            var taken = new List<Rect>();
            foreach (var box in new[] { TitleBox, ScaleBox, LegendBox, RoseBox })
                taken.Add(new Rect(box.xMin - 12, box.yMin - 12, box.width + 24, box.height + 24));
            Places(s, taken);
            Furniture(s);

            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false) { name = Name };
            tex.SetPixels(s.Px);
            tex.Apply();
            var saved = TextureFactory.Save(Name, tex, false, TextureWrapMode.Clamp);
            Debug.Log($"1079 Эльбрус: карта южного склона {Res}×{Res} ({Cell:0.#} м в пикселе) собрана за {clock.Elapsed.TotalSeconds:0.0} с");
            return saved;
        }

        // ── relief ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Hypsometric ramp of the southern slope: Baksan pine forest, alpine meadow, the yellow-brown moraines
        /// of Krugozor and Mir, grey lava, then the rock and ice of the cone.</summary>
        static readonly (float ele, Color c)[] Bands =
        {
            (2050f, new Color(.36f, .47f, .29f)),
            (2450f, new Color(.49f, .59f, .35f)),
            (2800f, new Color(.68f, .71f, .44f)),
            (3100f, new Color(.82f, .76f, .53f)),
            (3450f, new Color(.80f, .70f, .55f)),
            (3800f, new Color(.74f, .68f, .63f)),
            (4300f, new Color(.78f, .76f, .75f)),
            (4900f, new Color(.86f, .86f, .87f)),
            (5700f, new Color(.94f, .95f, .96f)),
        };

        static Color Hypso(float e)
        {
            if (e <= Bands[0].ele) return Bands[0].c;
            for (int i = 1; i < Bands.Length; i++)
                if (e <= Bands[i].ele)
                    return Color.Lerp(Bands[i - 1].c, Bands[i].c, Mathf.InverseLerp(Bands[i - 1].ele, Bands[i].ele, e));
            return Bands[Bands.Length - 1].c;
        }

        /// <summary>Ground tint, hillshade, firn and contours in one pass over the sheet. The snow line is the one
        /// <see cref="ElbrusImporter"/> paints the terrain with: patches from 3 250 m, continuous firn above 3 700 m,
        /// nothing below 3 050 m, and the steep south-facing ribs lose it first.</summary>
        static void Relief(Sheet s, float[] elev)
        {
            // the printed convention: sun from the north-west, 45° above the horizon
            var sun = new Vector3(-.5f, .5f, .7071f).normalized;
            for (int r = 0; r < Res; r++)
            {
                int rm = Mathf.Max(0, r - 1), rp = Mathf.Min(Res - 1, r + 1);
                float z = Zw(r);
                for (int c = 0; c < Res; c++)
                {
                    int cm = Mathf.Max(0, c - 1), cp = Mathf.Min(Res - 1, c + 1);
                    float x = Xw(c);
                    float e = elev[r * Res + c];
                    float gx = (elev[r * Res + cp] - elev[r * Res + cm]) / ((cp - cm) * Cell);
                    float gz = (elev[rp * Res + c] - elev[rm * Res + c]) / ((rp - rm) * Cell);
                    float g = Mathf.Sqrt(gx * gx + gz * gz);
                    float slope = Mathf.Atan(g) * Mathf.Rad2Deg;
                    var n = new Vector3(-gx, -gz, 1f).normalized;
                    float shade = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, sun)), .85f);
                    float grain = .96f + .07f * Mathf.PerlinNoise(c * .35f, r * .35f);

                    // the same patch noise the terrain is splatted with, drawn a little coarser: a printed map
                    // generalises the edge of the firn into fields instead of speckling it pixel by pixel
                    float patch = Mathf.PerlinNoise(x * .006f + 21f, z * .006f + 33f);
                    float snowy = Mathf.Clamp01(Mathf.InverseLerp(3250f, 3700f, e) + (patch - .55f) * .6f);
                    snowy *= Mathf.InverseLerp(3050f, 3260f, e);
                    snowy *= Mathf.Lerp(1f, .55f, Mathf.InverseLerp(26f, 42f, slope));

                    var band = Hypso(e);
                    float k = Mathf.Lerp(.56f, 1.16f, shade) * grain;
                    var col = new Color(Mathf.Clamp01(band.r * k), Mathf.Clamp01(band.g * k), Mathf.Clamp01(band.b * k), 1f);
                    if (snowy > .001f)
                    {
                        // firn and glacier ice: white where the sun stands on it, blue in every hollow
                        float w = Mathf.Clamp01(shade * grain);
                        var firn = new Color(Mathf.Lerp(.62f, 1f, w), Mathf.Lerp(.71f, 1f, w), Mathf.Lerp(.86f, 1f, w), 1f);
                        col = Color.Lerp(col, firn, snowy);
                    }

                    // contours: the distance to the nearest 100 m line, measured in pixels — dp changes by exactly one
                    // per pixel step whatever the slope, so a line keeps its width from the meadow to the summit cone
                    float perPx = g * Cell;
                    int lvl = Mathf.RoundToInt(e / 100f);
                    float dp = Mathf.Abs(e - lvl * 100f) / Mathf.Max(perPx, 1e-4f);
                    bool index = lvl % 5 == 0;
                    float cov = Mathf.Clamp01((index ? 1.05f : .5f) - dp + .5f);
                    if (!index) cov *= 1f - Mathf.InverseLerp(5f, 9f, perPx); // thin lines crowd on the steepest ribs
                    if (cov > .002f)
                        col = Color.Lerp(col, Color.Lerp(Sepia, IceLine, snowy), cov * (index ? .78f : .52f));

                    s.Px[r * Res + c] = col;
                }
            }
        }

        // ── grid ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Kilometre grid, counted from the centre of the frame (the origin of the world), and its numbers
        /// along the southern and western edges.</summary>
        static void Grid(Sheet s)
        {
            var line = new Color(.16f, .19f, .26f);
            for (int km = -6; km <= 6; km++)
            {
                float p = Pix(km * 1000f);
                s.Seg(new Vector2(p, 0), new Vector2(p, Res - 1), .35f, line, .26f);
                s.Seg(new Vector2(0, p), new Vector2(Res - 1, p), .35f, line, .26f);
            }
            // the outermost pair of lines runs 144 m from the edge: too close to the border rule to carry a number
            var num = new Color(.14f, .16f, .2f);
            for (int km = -5; km <= 5; km++)
            {
                float p = Pix(km * 1000f);
                string t = km.ToString();
                Text(s, t, p + 6f, 20f, 15f, num, true);
                Text(s, t, 20f, p + 12f, 15f, num, true);
            }
        }

        // ── ropeways and routes ───────────────────────────────────────────────────────────────────────────

        static Vector2[] Pixels((float x, float z)[] path)
        {
            var v = new Vector2[path.Length];
            for (int i = 0; i < v.Length; i++) v[i] = P(path[i].x, path[i].z);
            return v;
        }

        /// <summary>The six ropeways (jig-backs dashed, loops solid, each tower a cross-tick), the snow-cat road and
        /// the foot route to the West summit. Every line gets a pale casing first, or the ink disappears on the lava.</summary>
        static void Lines(Sheet s)
        {
            var ratrak = Pixels(Elbrus.RatrakRoute);
            s.Dashes(ratrak, 3f, Paper, 15f, 9f, 0f, .8f);
            s.Dashes(ratrak, 1.6f, RatrakGrey, 15f, 9f, 0f, 1f);

            var summit = Pixels(Elbrus.SummitRoute);
            s.Dashes(summit, 2.7f, Paper, 10f, 7f, 5f, .8f);
            s.Dashes(summit, 1.4f, RouteRed, 10f, 7f, 5f, 1f);

            foreach (var line in Elbrus.Ropeways)
            {
                var pts = Pixels(line.Towers);
                float hw = line.Kind == RopewayKind.Chair ? 1f : 1.45f;
                s.Path(pts, hw + 1.4f, Paper, .8f);
                if (line.Kind == RopewayKind.Pendulum) s.Dashes(pts, hw, Ink, 12f, 6f, 0f, 1f);
                else s.Path(pts, hw, Ink, 1f);
                for (int i = 0; i < pts.Length; i++)
                {
                    var a = pts[Mathf.Max(0, i - 1)];
                    var b = pts[Mathf.Min(pts.Length - 1, i + 1)];
                    var d = b - a;
                    if (d.sqrMagnitude < 1e-4f) continue;
                    d.Normalize();
                    var perp = new Vector2(-d.y, d.x) * (line.Kind == RopewayKind.Chair ? 2.8f : 3.8f);
                    s.Seg(pts[i] - perp, pts[i] + perp, .85f, Ink, 1f);
                }
            }
        }

        // ── places ────────────────────────────────────────────────────────────────────────────────────────

        static int Rank(Elbrus.Kind k) => k == Elbrus.Kind.Station ? 0 : k == Elbrus.Kind.Landmark ? 1 : k == Elbrus.Kind.Hut ? 2 : 3;

        /// <summary>The name a map has room for: the label without its height and without the parenthetical.</summary>
        static string ShortName(Elbrus.Poi p)
        {
            string t = p.Label;
            int dot = t.IndexOf('·');
            if (dot > 0) t = t.Substring(0, dot);
            int brace = t.IndexOf('(');
            if (brace > 0) t = t.Substring(0, brace);
            return t.Trim();
        }

        static void Symbol(Sheet s, Elbrus.Kind kind, Vector2 q, float size)
        {
            s.Disc(q, size * .78f, Paper, .9f);
            float h = size * .5f;
            switch (kind)
            {
                case Elbrus.Kind.Station: // a terminal is a square with a solid heart
                    s.Path(new[] { new Vector2(q.x - h, q.y - h), new Vector2(q.x + h, q.y - h), new Vector2(q.x + h, q.y + h), new Vector2(q.x - h, q.y + h), new Vector2(q.x - h, q.y - h) }, size * .07f + .35f, Ink, 1f);
                    s.Fill(new Rect(q.x - h * .42f, q.y - h * .42f, h * .84f, h * .84f), Ink, 1f);
                    break;
                case Elbrus.Kind.Hut: // a hut is a little roof
                    s.Path(new[] { new Vector2(q.x - h, q.y - h * .7f), new Vector2(q.x, q.y + h), new Vector2(q.x + h, q.y - h * .7f), new Vector2(q.x - h, q.y - h * .7f) }, size * .07f + .35f, Ink, 1f);
                    break;
                case Elbrus.Kind.Landmark: // a rock or a summit is a cross
                    s.Seg(new Vector2(q.x - h, q.y - h), new Vector2(q.x + h, q.y + h), size * .075f + .35f, Ink, 1f);
                    s.Seg(new Vector2(q.x - h, q.y + h), new Vector2(q.x + h, q.y - h), size * .075f + .35f, Ink, 1f);
                    break;
                default: // a named stage of the route is an open diamond
                    s.Path(new[] { new Vector2(q.x, q.y + h), new Vector2(q.x + h, q.y), new Vector2(q.x, q.y - h), new Vector2(q.x - h, q.y), new Vector2(q.x, q.y + h) }, size * .07f + .35f, Ink, 1f);
                    break;
            }
        }

        static bool Free(List<Rect> taken, Rect r)
        {
            if (r.xMin < 34f || r.yMin < 34f || r.xMax > Res - 34f || r.yMax > Res - 34f) return false;
            foreach (var t in taken) if (t.Overlaps(r)) return false;
            return true;
        }

        /// <summary>Symbols first, then the names — every name takes the nearest free spot beside its symbol, with a
        /// hair-line leader when it has to stand off. A name that finds no room anywhere is dropped, as on a printed map.</summary>
        static void Places(Sheet s, List<Rect> taken)
        {
            var order = new List<(int rank, int i, Elbrus.Poi poi)>();
            for (int i = 0; i < Elbrus.Pois.Count; i++) order.Add((Rank(Elbrus.Pois[i].Kind), i, Elbrus.Pois[i]));
            order.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.i.CompareTo(b.i));

            var marks = new Vector2[order.Count];
            var sizes = new float[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var p = order[i].poi;
                float size = p.Kind == Elbrus.Kind.Station ? 15f : p.Kind == Elbrus.Kind.Landmark ? 15f : 13f;
                var q = P(p.X, p.Z);
                marks[i] = q; sizes[i] = size;
                Symbol(s, p.Kind, q, size);
                taken.Add(new Rect(q.x - size, q.y - size, size * 2f, size * 2f));
            }

            float[] rows = { 0f, 22f, -22f, 44f, -44f, 68f, -68f, 94f, -94f, 124f, -124f, 158f, -158f };
            for (int i = 0; i < order.Count; i++)
            {
                var p = order[i].poi;
                float cap = p.Kind == Elbrus.Kind.Station || p.Kind == Elbrus.Kind.Landmark ? 26f : 21f;
                string text = (ShortName(p) + " " + p.Ele.ToString("0")).ToUpperInvariant();
                float w = TextWidth(text, cap), gap = sizes[i] + 6f;
                var q = marks[i];
                bool placed = false;
                for (int k = 0; k < rows.Length && !placed; k++)
                    for (int side = 0; side < 2 && !placed; side++)
                    {
                        float bx = side == 0 ? q.x + gap : q.x - gap - w;
                        float by = q.y + rows[k] - cap * .38f;
                        var box = new Rect(bx - 5f, by - cap * .3f, w + 10f, cap * 1.5f);
                        if (!Free(taken, box)) continue;
                        taken.Add(box);
                        if (Mathf.Abs(rows[k]) > 24f)
                            s.Seg(q, new Vector2(side == 0 ? bx - 3f : bx + w + 3f, by + cap * .35f), .5f, Ink, .55f);
                        Text(s, text, bx, by, cap, Ink, true);
                        placed = true;
                    }
            }
        }

        // ── printed furniture ─────────────────────────────────────────────────────────────────────────────

        static void Outline(Sheet s, Rect r, float hw, Color c, float alpha)
        {
            s.Seg(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), hw, c, alpha);
            s.Seg(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), hw, c, alpha);
            s.Seg(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), hw, c, alpha);
            s.Seg(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), hw, c, alpha);
        }

        static void Card(Sheet s, Rect r)
        {
            s.Fill(r, Paper, .9f);
            Outline(s, r, .7f, Ink, .85f);
        }

        static void Furniture(Sheet s)
        {
            Title(s);
            ScaleBar(s);
            Legend(s);
            Rose(s);
            Border(s);
        }

        static void Title(Sheet s)
        {
            Card(s, TitleBox);
            Text(s, "ЭЛЬБРУС · ЮЖНЫЙ СКЛОН", TitleBox.xMin + 18f, TitleBox.yMax - 48f, 30f, Ink, false);
            Text(s, "ГОРИЗОНТАЛИ ЧЕРЕЗ 100 М · СЕТКА 1 КМ", TitleBox.xMin + 18f, TitleBox.yMin + 20f, 17f, Sepia, false);
        }

        static void ScaleBar(Sheet s)
        {
            Card(s, ScaleBox);
            float km = 1000f / Cell;
            float x0 = ScaleBox.xMin + 26f, y0 = ScaleBox.yMin + 46f, h = 12f;
            for (int i = 0; i < 4; i++)
                s.Fill(new Rect(x0 + i * km * .5f, y0, km * .5f, h), i % 2 == 0 ? Ink : Paper, 1f);
            Outline(s, new Rect(x0, y0, km * 2f, h), .6f, Ink, 1f);
            Text(s, "0", x0 - 4f, y0 + h + 9f, 15f, Ink, false);
            Text(s, "1", x0 + km - 4f, y0 + h + 9f, 15f, Ink, false);
            Text(s, "2 КМ", x0 + km * 2f - 12f, y0 + h + 9f, 15f, Ink, false);
            Text(s, "КАРТА ДЛЯ ХОДЬБЫ, НЕ ДЛЯ НАВИГАЦИИ", x0 - 4f, ScaleBox.yMin + 16f, 13f, Sepia, false);
        }

        static void Legend(Sheet s)
        {
            Card(s, LegendBox);
            float x = LegendBox.xMin + 22f, y = LegendBox.yMax - 40f, dy = 34f, cap = 16f, icon = x + 20f, text = x + 52f;
            Text(s, "УСЛОВНЫЕ ЗНАКИ", x, y, 18f, Ink, false);
            y -= dy + 4f;
            Symbol(s, Elbrus.Kind.Station, new Vector2(icon, y + cap * .4f), 14f);
            Text(s, "СТАНЦИЯ КАНАТНОЙ ДОРОГИ", text, y, cap, Ink, false); y -= dy;
            Symbol(s, Elbrus.Kind.Hut, new Vector2(icon, y + cap * .4f), 13f);
            Text(s, "ПРИЮТ, ХИЖИНА, БОЧКИ", text, y, cap, Ink, false); y -= dy;
            Symbol(s, Elbrus.Kind.Landmark, new Vector2(icon, y + cap * .4f), 14f);
            Text(s, "ВЕРШИНА, ОРИЕНТИР", text, y, cap, Ink, false); y -= dy;
            s.Seg(new Vector2(icon - 16f, y + cap * .4f), new Vector2(icon + 16f, y + cap * .4f), 1.45f, Ink, 1f);
            Text(s, "ГОНДОЛА, КРЕСЛО", text, y, cap, Ink, false); y -= dy;
            s.Dashes(new[] { new Vector2(icon - 16f, y + cap * .4f), new Vector2(icon + 16f, y + cap * .4f) }, 1.45f, Ink, 9f, 5f, 0f, 1f);
            Text(s, "МАЯТНИКОВАЯ ДОРОГА", text, y, cap, Ink, false); y -= dy;
            s.Dashes(new[] { new Vector2(icon - 16f, y + cap * .4f), new Vector2(icon + 16f, y + cap * .4f) }, 1.4f, RouteRed, 8f, 6f, 0f, 1f);
            Text(s, "МАРШРУТ НА ВЕРШИНУ", text, y, cap, Ink, false); y -= dy;
            s.Dashes(new[] { new Vector2(icon - 16f, y + cap * .4f), new Vector2(icon + 16f, y + cap * .4f) }, 1.6f, RatrakGrey, 11f, 7f, 0f, 1f);
            Text(s, "ТРАССА РАТРАКА", text, y, cap, Ink, false);
        }

        static void Rose(Sheet s)
        {
            var c = new Vector2(RoseBox.center.x, RoseBox.yMin + 52f);
            s.Disc(c, 40f, Paper, .85f);
            s.Seg(new Vector2(c.x, c.y - 34f), new Vector2(c.x, c.y + 32f), 1.6f, Ink, 1f);
            s.Seg(new Vector2(c.x - 9f, c.y + 18f), new Vector2(c.x, c.y + 34f), 1.6f, Ink, 1f);
            s.Seg(new Vector2(c.x + 9f, c.y + 18f), new Vector2(c.x, c.y + 34f), 1.6f, Ink, 1f);
            Text(s, "С", c.x - 8f, c.y + 42f, 24f, Ink, false);
        }

        static void Border(Sheet s)
        {
            s.Fill(new Rect(0, 0, Res, 8), Paper, 1f);
            s.Fill(new Rect(0, Res - 8, Res, 8), Paper, 1f);
            s.Fill(new Rect(0, 0, 8, Res), Paper, 1f);
            s.Fill(new Rect(Res - 8, 0, 8, Res), Paper, 1f);
            Outline(s, new Rect(9, 9, Res - 19, Res - 19), 1.7f, Ink, 1f);
            Outline(s, new Rect(15, 15, Res - 31, Res - 31), .6f, Ink, 1f);
        }

        // ── drawing surface ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The sheet we paint on: a plain Color buffer with anti-aliased strokes. A stroke takes the coverage
        /// of the distance from the pixel to the segment, so joints never darken twice the way a stamped brush does.</summary>
        sealed class Sheet
        {
            public readonly Color[] Px;
            public readonly int W, H;

            public Sheet(int w, int h)
            {
                W = w; H = h; Px = new Color[w * h];
                for (int i = 0; i < Px.Length; i++) Px[i] = Paper;
            }

            public void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f || x < 0 || y < 0 || x >= W || y >= H) return;
                if (a > 1f) a = 1f;
                int i = y * W + x;
                var d = Px[i];
                Px[i] = new Color(d.r + (c.r - d.r) * a, d.g + (c.g - d.g) * a, d.b + (c.b - d.b) * a, 1f);
            }

            public void Seg(Vector2 a, Vector2 b, float halfWidth, Color c, float alpha)
            {
                float pad = halfWidth + 1.5f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad));
                int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad));
                int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad));
                var ab = b - a;
                float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x, y);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        float d = (p - (a + ab * t)).magnitude;
                        Blend(x, y, c, Mathf.Clamp01(halfWidth + .5f - d) * alpha);
                    }
            }

            public void Path(IList<Vector2> pts, float halfWidth, Color c, float alpha)
            {
                for (int i = 1; i < pts.Count; i++) Seg(pts[i - 1], pts[i], halfWidth, c, alpha);
            }

            /// <summary>Dashed polyline: the pattern runs along the whole line, so dashes do not restart at every vertex.</summary>
            public void Dashes(IList<Vector2> pts, float halfWidth, Color c, float on, float off, float phase, float alpha)
            {
                float period = Mathf.Max(on + off, .01f);
                float s = phase;
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector2 a = pts[i - 1], b = pts[i];
                    float len = (b - a).magnitude;
                    if (len < 1e-4f) continue;
                    float walked = 0f;
                    while (walked < len)
                    {
                        float k = s % period;
                        float remain = k < on ? on - k : period - k;
                        float step = Mathf.Max(Mathf.Min(remain, len - walked), .01f);
                        if (k < on)
                            Seg(Vector2.Lerp(a, b, walked / len), Vector2.Lerp(a, b, Mathf.Min(1f, (walked + step) / len)), halfWidth, c, alpha);
                        walked += step; s += step;
                    }
                }
            }

            public void Disc(Vector2 p, float r, Color c, float alpha)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(p.x - r - 1f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(p.x + r + 1f));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(p.y - r - 1f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(p.y + r + 1f));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Blend(x, y, c, Mathf.Clamp01(r + .5f - (new Vector2(x, y) - p).magnitude) * alpha);
            }

            public void Fill(Rect r, Color c, float alpha)
            {
                int x0 = Mathf.Max(0, Mathf.RoundToInt(r.xMin)), x1 = Mathf.Min(W - 1, Mathf.RoundToInt(r.xMax) - 1);
                int y0 = Mathf.Max(0, Mathf.RoundToInt(r.yMin)), y1 = Mathf.Min(H - 1, Mathf.RoundToInt(r.yMax) - 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Blend(x, y, c, alpha);
            }
        }

        /// <summary>A scratch coverage buffer. Strokes are combined with max(), not added, so a letter with a halo keeps
        /// an even weight where its strokes cross. Blitted onto the sheet once.</summary>
        sealed class Stamp
        {
            readonly int x0, y0, w, h;
            readonly float[] cov;

            public Stamp(float minX, float minY, float maxX, float maxY)
            {
                x0 = Mathf.FloorToInt(minX); y0 = Mathf.FloorToInt(minY);
                w = Mathf.Max(1, Mathf.CeilToInt(maxX) - x0 + 1);
                h = Mathf.Max(1, Mathf.CeilToInt(maxY) - y0 + 1);
                cov = new float[w * h];
            }

            public void Seg(Vector2 a, Vector2 b, float halfWidth)
            {
                float pad = halfWidth + 1.5f;
                int ax = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad) - x0);
                int bx = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad) - x0);
                int ay = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad) - y0);
                int by = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad) - y0);
                var ab = b - a;
                float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                for (int y = ay; y <= by; y++)
                    for (int x = ax; x <= bx; x++)
                    {
                        var p = new Vector2(x + x0, y + y0);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        float d = (p - (a + ab * t)).magnitude;
                        float v = Mathf.Clamp01(halfWidth + .5f - d);
                        int i = y * w + x;
                        if (v > cov[i]) cov[i] = v;
                    }
            }

            public void Blit(Sheet s, Color c, float alpha)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float v = cov[y * w + x];
                        if (v > 0f) s.Blend(x0 + x, y0 + y, c, v * alpha);
                    }
            }
        }

        // ── stroke alphabet ───────────────────────────────────────────────────────────────────────────────

        /// <summary>One unit of the glyph grid: letters are 4 units wide and 7 tall (the cap height), accents reach 9.</summary>
        static float Unit(float cap) => cap / 7f;
        /// <summary>Distance from one letter to the next. The alphabet is monospaced, which is what a map legend wants.</summary>
        static float Advance(float cap) => cap * 6f / 7f;

        static float TextWidth(string text, float cap)
            => string.IsNullOrEmpty(text) ? 0f : (text.Length - 1) * Advance(cap) + 4f * Unit(cap);

        /// <summary>Draws a line of text with its baseline at <paramref name="y"/> and its left edge at <paramref name="x"/>.
        /// With <paramref name="halo"/> the letters get the pale mask a printed map puts under a name.</summary>
        static void Text(Sheet s, string text, float x, float y, float cap, Color ink, bool halo)
        {
            if (string.IsNullOrEmpty(text)) return;
            float u = Unit(cap), adv = Advance(cap);
            float inkW = Mathf.Max(.55f, cap * .055f);
            float haloW = inkW + Mathf.Max(1.1f, cap * .1f);
            float pad = haloW + 2f;
            float minX = x - pad, minY = y - cap * .4f - pad, maxX = x + TextWidth(text, cap) + pad, maxY = y + cap * 1.32f + pad;
            var body = new Stamp(minX, minY, maxX, maxY);
            var ring = halo ? new Stamp(minX, minY, maxX, maxY) : null;
            for (int i = 0; i < text.Length; i++)
            {
                var glyph = Glyph(text[i]);
                if (glyph == null) continue;
                float gx = x + i * adv;
                foreach (var line in glyph)
                {
                    if (line.Length == 1)
                    {
                        var d = new Vector2(gx + line[0].x * u, y + line[0].y * u);
                        body.Seg(d, d, inkW);
                        if (ring != null) ring.Seg(d, d, haloW);
                        continue;
                    }
                    for (int k = 1; k < line.Length; k++)
                    {
                        var a = new Vector2(gx + line[k - 1].x * u, y + line[k - 1].y * u);
                        var b = new Vector2(gx + line[k].x * u, y + line[k].y * u);
                        body.Seg(a, b, inkW);
                        if (ring != null) ring.Seg(a, b, haloW);
                    }
                }
            }
            if (ring != null) ring.Blit(s, Paper, .92f);
            body.Blit(s, ink, 1f);
        }

        static Dictionary<char, Vector2[][]> parsed;

        static Vector2[][] Glyph(char c)
        {
            if (parsed == null)
            {
                parsed = new Dictionary<char, Vector2[][]>(Strokes.Count);
                foreach (var kv in Strokes)
                {
                    var polylines = kv.Value.Split('|');
                    var lines = new Vector2[polylines.Length][];
                    for (int i = 0; i < polylines.Length; i++)
                    {
                        var pts = polylines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var line = new Vector2[pts.Length];
                        for (int k = 0; k < pts.Length; k++) line[k] = new Vector2(pts[k][0] - '0', pts[k][1] - '0');
                        lines[i] = line;
                    }
                    parsed[kv.Key] = lines;
                }
            }
            return parsed.TryGetValue(c, out var g) ? g : null;
        }

        /// <summary>The alphabet itself: every glyph is a set of polylines on a grid 4 wide and 7 high (0 is the
        /// baseline), written as pairs of digits "xy", polylines separated by "|". Upper case only — a map shouts.</summary>
        static readonly Dictionary<char, string> Strokes = new Dictionary<char, string>
        {
            ['0'] = "10 30 41 46 37 17 06 01 10",
            ['1'] = "15 27 20|10 30",
            ['2'] = "06 17 37 46 44 00 40",
            ['3'] = "07 47 24 34 43 41 30 10 01",
            ['4'] = "30 37 02 42",
            ['5'] = "47 07 04 34 43 41 30 10 01",
            ['6'] = "46 37 17 06 01 10 30 41 43 34 14 03",
            ['7'] = "07 47 10",
            ['8'] = "14 05 06 17 37 46 45 34 14 03 01 10 30 41 43 34",
            ['9'] = "01 10 30 41 46 37 17 06 04 13 33 44",

            ['А'] = "00 27 40|12 32",
            ['Б'] = "37 07 00 30 41 43 34 04",
            ['В'] = "00 07 37 46 45 34 04|34 43 41 30 00",
            ['Г'] = "00 07 47",
            ['Д'] = "00 01 11 16 27 37 31 41 40|11 31",
            ['Е'] = "47 07 00 40|04 34",
            ['Ё'] = "47 07 00 40|04 34|18 18|38 38",
            ['Ж'] = "20 27|07 23 00|47 23 40",
            ['З'] = "06 17 37 46 45 34 24|34 43 41 30 10 01",
            ['И'] = "07 00 47 40",
            ['Й'] = "07 00 47 40|18 29 38",
            ['К'] = "07 00|47 13 40",
            ['Л'] = "40 47 17 00",
            ['М'] = "00 07 23 47 40",
            ['Н'] = "07 00|47 40|04 44",
            ['О'] = "10 30 41 46 37 17 06 01 10",
            ['П'] = "00 07 47 40",
            ['Р'] = "00 07 37 46 45 34 04",
            ['С'] = "46 37 17 06 01 10 30 41",
            ['Т'] = "07 47|27 20",
            ['У'] = "07 23|47 23 00",
            ['Ф'] = "27 20|26 36 45 43 32 12 03 05 16 26",
            ['Х'] = "07 40|00 47",
            ['Ц'] = "07 01 41 47|41 40",
            ['Ч'] = "07 04 44|47 40",
            ['Ш'] = "07 00 40 47|20 27",
            ['Щ'] = "07 01 41 47|21 27|41 40",
            ['Ъ'] = "07 17|17 10 30 41 42 33 13",
            ['Ы'] = "07 00 20 31 32 23 03|47 40",
            ['Ь'] = "07 00 30 41 42 33 03",
            ['Э'] = "06 17 37 46 41 30 10 01|24 44",
            ['Ю'] = "07 00|04 14|27 37 46 41 30 20 11 16 27",
            ['Я'] = "40 47 17 06 05 14 44|24 00",

            ['A'] = "00 27 40|12 32",
            ['B'] = "00 07 37 46 45 34 04|34 43 41 30 00",
            ['C'] = "46 37 17 06 01 10 30 41",
            ['D'] = "00 07 37 46 41 30 00",
            ['E'] = "47 07 00 40|04 34",
            ['F'] = "00 07 47|04 34",
            ['G'] = "46 37 17 06 01 10 30 41 43 23",
            ['H'] = "07 00|47 40|04 44",
            ['I'] = "17 37|27 20|10 30",
            ['J'] = "47 41 30 10 01",
            ['K'] = "07 00|47 13 40",
            ['L'] = "07 00 40",
            ['M'] = "00 07 23 47 40",
            ['N'] = "00 07 40 47",
            ['O'] = "10 30 41 46 37 17 06 01 10",
            ['P'] = "00 07 37 46 45 34 04",
            ['Q'] = "10 30 41 46 37 17 06 01 10|22 40",
            ['R'] = "00 07 37 46 45 34 04|24 40",
            ['S'] = "46 37 17 06 05 14 33 42 41 30 10 01",
            ['T'] = "07 47|27 20",
            ['U'] = "07 01 10 30 41 47",
            ['V'] = "07 20 47",
            ['W'] = "07 10 24 30 47",
            ['X'] = "07 40|00 47",
            ['Y'] = "07 24 47|24 20",
            ['Z'] = "07 47 00 40",

            ['·'] = "23 23",
            ['.'] = "10 10",
            [','] = "11 00",
            ['-'] = "13 33",
            ['–'] = "13 33",
            ['«'] = "35 13 31",
            ['»'] = "15 33 11",
            ['('] = "37 15 12 30",
            [')'] = "17 35 32 10",
            ['°'] = "16 27 36 25 16",
            ['/'] = "00 47",
            [':'] = "11 11|15 15",
            ['+'] = "03 43|21 25",
            ['~'] = "04 13 33 44",
            ['!'] = "27 22|10 10",
        };
    }
}
