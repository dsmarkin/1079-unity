using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Procedural textures that have no CC0 source in the project: needle sprays, bare birch twigs, birch bark, tent canvas, snow crust cards.
    /// Written as PNG into the generated library so artists can replace them file-for-file.</summary>
    public static class TextureFactory
    {
        public const string Dir = WorldPaths.Generated + "/Textures";

        internal static Texture2D Save(string name, Texture2D tex, bool alpha, TextureWrapMode wrap)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = alpha; imp.wrapMode = wrap; imp.mipmapEnabled = true; imp.mipMapsPreserveCoverage = alpha; imp.alphaTestReferenceValue = .45f;
            imp.sRGBTexture = true; imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D New(int w, int h, Color fill)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h]; Color32 f = fill; for (int i = 0; i < px.Length; i++) px[i] = f;
            t.SetPixels32(px);
            return t;
        }

        static void Stroke(Texture2D t, Vector2 a, Vector2 b, float width, Color c)
        {
            int steps = Mathf.CeilToInt((b - a).magnitude * 1.5f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                float w = width * (1f - .6f * i / steps);
                for (int dy = -Mathf.CeilToInt(w); dy <= Mathf.CeilToInt(w); dy++)
                    for (int dx = -Mathf.CeilToInt(w); dx <= Mathf.CeilToInt(w); dx++)
                    {
                        int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                        if (x < 0 || y < 0 || x >= t.width || y >= t.height) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy); if (d > w + .5f) continue;
                        float k = Mathf.Clamp01(w + .5f - d);
                        var old = t.GetPixel(x, y);
                        t.SetPixel(x, y, new Color(Mathf.Lerp(old.r, c.r, k), Mathf.Lerp(old.g, c.g, k), Mathf.Lerp(old.b, c.b, k), Mathf.Max(old.a, k * c.a)));
                    }
            }
        }

        /// <summary>Conifer spray seen from above: central shoot along v, side shoots, dense needles. Spruce: short stiff needles; Siberian pine: long soft needles in bundles.</summary>
        public static Texture2D NeedleSpray(string name, int seed, float needleLength, Color dark, Color light, bool longSoft)
        {
            const int W = 256, H = 512;
            var rnd = new System.Random(seed);
            var t = New(W, H, new Color(dark.r, dark.g, dark.b, 0));
            float R() => (float)rnd.NextDouble();
            var stem = new Color(.33f, .24f, .17f, 1);
            Stroke(t, new Vector2(W / 2, 4), new Vector2(W / 2 + 6, H - 10), 2.2f, stem);
            int sides = longSoft ? 9 : 13;
            for (int s = 0; s < sides; s++)
            {
                float y0 = 20 + s * (H - 70f) / sides;
                float len = (W * .46f) * (1f - .55f * s / sides) * (.85f + .3f * R());
                foreach (int dir in new[] { -1, 1 })
                {
                    Vector2 a = new Vector2(W / 2 + 3, y0), b = a + new Vector2(dir * len, len * .55f);
                    Stroke(t, a, b, 1.3f, stem);
                    int needles = (int)(len / (longSoft ? 3.2f : 2.1f));
                    for (int k = 0; k < needles; k++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, k / (float)needles);
                        foreach (int side in new[] { -1, 1 })
                        {
                            float ang = (side * (longSoft ? 35 : 55) + (R() - .5f) * 30) * Mathf.Deg2Rad;
                            Vector2 along = (b - a).normalized;
                            Vector2 dirv = new Vector2(along.x * Mathf.Cos(ang) - along.y * Mathf.Sin(ang), along.x * Mathf.Sin(ang) + along.y * Mathf.Cos(ang));
                            var col = Color.Lerp(dark, light, R() * .8f + (longSoft ? .1f : 0));
                            Stroke(t, p, p + dirv * needleLength * (.7f + .5f * R()), longSoft ? .8f : 1.0f, col);
                        }
                    }
                }
            }
            // needles on the leader
            for (int k = 0; k < 90; k++)
            {
                float y = 8 + k * (H - 20f) / 90;
                Vector2 p = new Vector2(W / 2 + 6 * y / H, y);
                float ang = (R() * 2 - 1) * 70 * Mathf.Deg2Rad;
                Stroke(t, p, p + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * needleLength, 1f, Color.Lerp(dark, light, R()));
            }
            t.Apply();
            return Save(name, t, true, TextureWrapMode.Clamp);
        }

        /// <summary>Bare winter birch twigs: thin purple-brown branching strokes.</summary>
        public static Texture2D Twigs(string name, int seed) => Twigs(name, seed, new Color(.33f, .29f, .27f), 2, 0f);

        /// <summary>Bare winter twigs. <paramref name="extraKids"/> makes the spray denser (downy birch holds a fine purple-brown haze);
        /// <paramref name="spurs"/> 0..1 adds the short knobby shoots of larch.</summary>
        public static Texture2D Twigs(string name, int seed, Color tone, int extraKids, float spurs)
        {
            const int W = 256, H = 256;
            var rnd = new System.Random(seed);
            var t = New(W, H, new Color(tone.r, tone.g, tone.b, 0));
            void Branch(Vector2 a, float ang, float len, float width, int depth)
            {
                if (depth > 5 || len < 4) return;
                Vector2 b = a + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * len;
                Stroke(t, a, b, width, new Color(tone.r + .03f * depth, tone.g + .02f * depth, tone.b + .02f * depth, 1));
                if (spurs > 0 && depth >= 1)
                    for (float k = .15f; k < 1f; k += .12f)
                        if (rnd.NextDouble() < spurs)
                        {
                            Vector2 p = Vector2.Lerp(a, b, k);
                            float sa = ang + (rnd.NextDouble() < .5 ? -1.2f : 1.2f);
                            Stroke(t, p, p + new Vector2(Mathf.Sin(sa), Mathf.Cos(sa)) * 3.5f, 1.6f, new Color(tone.r * .8f, tone.g * .8f, tone.b * .8f, 1));
                        }
                int kids = 2 + rnd.Next(extraKids);
                for (int i = 0; i < kids; i++)
                {
                    float at = .35f + .6f * (float)rnd.NextDouble();
                    Branch(Vector2.Lerp(a, b, at), ang + ((float)rnd.NextDouble() - .5f) * 1.4f, len * (.45f + .2f * (float)rnd.NextDouble()), Mathf.Max(.5f, width * .65f), depth + 1);
                }
            }
            Branch(new Vector2(W / 2, 2), 0, 110, 2.2f, 0);
            t.Apply();
            return Save(name, t, true, TextureWrapMode.Clamp);
        }

        /// <summary>Hanging beard lichen (Bryoria/Usnea type): a tuft of fine grey-green strands falling from the top edge (v = 1 at the top).</summary>
        public static Texture2D Beard(string name, int seed)
        {
            const int W = 128, H = 256;
            var rnd = new System.Random(seed);
            var t = New(W, H, new Color(.5f, .52f, .45f, 0));
            for (int i = 0; i < 70; i++)
            {
                float x = W * (.2f + .6f * (float)rnd.NextDouble()), len = H * (.35f + .6f * (float)rnd.NextDouble());
                Vector2 a = new Vector2(x, H - 2), p = a;
                float sway = ((float)rnd.NextDouble() - .5f) * 1.2f;
                int seg = 8;
                var col = Color.Lerp(new Color(.42f, .45f, .38f, 1), new Color(.62f, .64f, .55f, 1), (float)rnd.NextDouble());
                for (int k = 0; k < seg; k++)
                {
                    Vector2 q = p + new Vector2(sway * len / seg * ((float)rnd.NextDouble() - .3f), -len / seg);
                    Stroke(t, p, q, .8f + .5f * (1f - k / (float)seg), col);
                    p = q;
                }
            }
            t.Apply();
            return Save(name, t, true, TextureWrapMode.Clamp);
        }

        /// <summary>Snow resting on a branch: soft white blob with crisp alpha edge.</summary>
        public static Texture2D SnowCard(string name, int seed)
        {
            const int W = 128, H = 256;
            var rnd = new System.Random(seed);
            var t = New(W, H, new Color(1, 1, 1, 0));
            var bumps = new Vector3[40];
            for (int i = 0; i < bumps.Length; i++) bumps[i] = new Vector3(W * (.3f + .4f * (float)rnd.NextDouble()), H * (.05f + .9f * (float)rnd.NextDouble()), 10 + 18 * (float)rnd.NextDouble());
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float f = 0; foreach (var b in bumps) { float d = new Vector2(x - b.x, y - b.y).magnitude / b.z; f += Mathf.Exp(-d * d); }
                    float taper = 1f - Mathf.Pow(y / (float)H, 3);
                    float a = Mathf.Clamp01((f * taper - .55f) * 3f);
                    float shade = .9f + .1f * Mathf.PerlinNoise(x * .08f, y * .08f);
                    t.SetPixel(x, y, new Color(shade, shade + .02f, Mathf.Min(1, shade + .05f), a));
                }
            t.Apply();
            return Save(name, t, true, TextureWrapMode.Clamp);
        }

        /// <summary>Betula pubescens bark: chalk white with dark horizontal lenticels and black fissured patches near the base (v = height).</summary>
        public static Texture2D BirchBark(string name, int seed)
        {
            const int W = 256, H = 512;
            var rnd = new System.Random(seed);
            var t = New(W, H, Color.white);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Mathf.PerlinNoise(x * .02f + seed, y * .004f);
                    float w = .86f + .1f * n;
                    t.SetPixel(x, y, new Color(w, w * .99f, w * .96f, 1));
                }
            for (int i = 0; i < 260; i++)
            {
                float x = (float)rnd.NextDouble() * W, y = (float)rnd.NextDouble() * H, len = 6 + 22 * (float)rnd.NextDouble();
                Stroke(t, new Vector2(x, y), new Vector2(x + len, y + ((float)rnd.NextDouble() - .5f) * 2), .9f + (float)rnd.NextDouble(), new Color(.16f, .14f, .13f, 1));
            }
            for (int i = 0; i < 18; i++)
            {
                float x = (float)rnd.NextDouble() * W, y = (float)rnd.NextDouble() * H;
                for (int k = 0; k < 14; k++) Stroke(t, new Vector2(x + k * 1.5f, y - 10 + (float)rnd.NextDouble() * 20), new Vector2(x + k * 1.5f + 4, y + (float)rnd.NextDouble() * 30), 1.4f, new Color(.08f, .07f, .07f, 1));
            }
            t.Apply();
            return Save(name, t, false, TextureWrapMode.Repeat);
        }

        /// <summary>Cotton tent canvas, "защитного цвета" (khaki olive), plain weave with seams.</summary>
        public static Texture2D Canvas(string name, Color baseColor)
        {
            const int S = 256;
            var t = New(S, S, baseColor);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float weave = ((x % 4 < 2) ^ (y % 4 < 2)) ? .97f : 1.03f;
                    float n = .9f + .2f * Mathf.PerlinNoise(x * .03f, y * .03f);
                    float seam = (x % 128 < 2) ? .82f : 1f;
                    var c = baseColor * weave * n * seam; c.a = 1;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Save(name, t, false, TextureWrapMode.Repeat);
        }

        /// <summary>A tuft of alpine grass on transparent background: the detail card the Elbrus meadows are drawn with.
        /// A dozen blades bending out of one point, a few of them dry and yellow, as they stand around Azau in July.</summary>
        public static Texture2D GrassTuft(string name, int seed, Color dark, Color light, Color dry)
            => GrassTuft(name, seed, dark, light, dry, default, 0f, 16, 1f);

        /// <summary>A tuft of alpine grass. <paramref name="flowerChance"/> 0..1 of the blades end in a small pale head
        /// (bellflower, edelweiss, a dry umbel) painted in <paramref name="flower"/>; <paramref name="reach"/> scales how
        /// far up the card the blades go, so a low sedge and a tall bunch can share one generator.</summary>
        public static Texture2D GrassTuft(string name, int seed, Color dark, Color light, Color dry, Color flower, float flowerChance, int blades, float reach)
        {
            const int W = 256, H = 256;
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var t = New(W, H, new Color(dark.r, dark.g, dark.b, 0));
            for (int b = 0; b < blades; b++)
            {
                float x0 = W * .5f + (R() - .5f) * W * .35f;
                float lean = (R() - .5f) * W * .55f;
                float h = H * reach * (.45f + .5f * R());
                var c = R() < .22f ? dry : Color.Lerp(dark, light, R());
                c.a = 1;
                // a blade is a chain of short strokes that thins and bends towards the tip
                var prev = new Vector2(x0, 2);
                int steps = 9;
                for (int k = 1; k <= steps; k++)
                {
                    float f = k / (float)steps;
                    var next = new Vector2(x0 + lean * f * f, 2 + h * f);
                    Stroke(t, prev, next, Mathf.Lerp(3.2f, .7f, f), Color.Lerp(c * .75f, c, f));
                    prev = next;
                }
                if (flowerChance > 0f && R() < flowerChance)
                {
                    var head = flower; head.a = 1;
                    Stroke(t, prev, prev + new Vector2((R() - .5f) * 2f, 2.2f), 2.4f + R() * 1.2f, head);
                }
            }
            t.Apply();
            return Save(name, t, true, TextureWrapMode.Clamp);
        }

        /// <summary>A tinted copy of a scanned albedo, baked into the generated library as its own PNG.
        /// This exists because the built-in terrain shader (Nature/Terrain/Standard) never reads
        /// <c>TerrainLayer.diffuseRemapMin/Max</c> — that pair is only honoured by the HDRP terrain shader, which is why a
        /// tint set on a layer looks plausible in the inspector and then does exactly nothing in a player build. The tint
        /// has to live in the pixels. <paramref name="desaturate"/> 0..1 first pulls the scan towards its own luminance,
        /// which is what takes the ochre out of a sandstone scan before it is darkened down to andesite.
        /// The JPG is decoded straight from the file, so the imported texture does not need Read/Write enabled.</summary>
        public static Texture2D Tinted(string name, string sourcePath, Color tint, float desaturate = 0f)
        {
            if (!File.Exists(sourcePath)) { Debug.LogWarning("1079 world: missing source texture " + sourcePath); return null; }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(sourcePath)))
            {
                UnityEngine.Object.DestroyImmediate(src);
                Debug.LogWarning("1079 world: could not decode " + sourcePath);
                return null;
            }
            var px = src.GetPixels();
            int w = src.width, h = src.height;
            UnityEngine.Object.DestroyImmediate(src);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float l = c.r * .299f + c.g * .587f + c.b * .114f;
                px[i] = new Color(Mathf.Lerp(c.r, l, desaturate) * tint.r,
                                  Mathf.Lerp(c.g, l, desaturate) * tint.g,
                                  Mathf.Lerp(c.b, l, desaturate) * tint.b, 1f);
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px); t.Apply();
            return Save(name, t, false, TextureWrapMode.Repeat);
        }

        /// <summary>Ground of the lower southern slope in late summer: alpine turf on the meadows and grey lava scree on the
        /// moraines. Both are generated, since the Poly Haven library here has no grass or scree scan.</summary>
        public static Texture2D Ground(string name, Color a, Color b, float grain, int seed, float speckle = .12f)
        {
            const int N = 512;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[N * N];
            var rnd = new System.Random(seed);
            float off = seed * 3.7f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float f = Mathf.PerlinNoise(u * grain + off, v * grain + off) * .6f
                            + Mathf.PerlinNoise(u * grain * 3.1f + 7, v * grain * 3.1f + 3) * .3f
                            + Mathf.PerlinNoise(u * grain * 9f + 13, v * grain * 9f + 19) * .1f;
                    var c = Color.Lerp(a, b, Mathf.Clamp01(f * 1.25f - .1f));
                    // stones and dry tufts: a sparse scatter of lighter and darker specks
                    float s = (float)rnd.NextDouble();
                    if (s < speckle) c = Color.Lerp(c, s < speckle * .45f ? Color.Lerp(c, Color.black, .5f) : Color.Lerp(c, Color.white, .45f), (float)rnd.NextDouble() * .8f);
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return Save(name, t, false, TextureWrapMode.Repeat);
        }

        /// <summary>Flat colour with slight noise (wool, charcoal, gaiter cloth).</summary>
        public static Texture2D Cloth(string name, Color c, float scale = .05f)
        {
            const int S = 128;
            var t = New(S, S, c);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { var k = c * (.85f + .3f * Mathf.PerlinNoise(x * scale * 4, y * scale * 4)); k.a = 1; t.SetPixel(x, y, k); }
            t.Apply();
            return Save(name, t, false, TextureWrapMode.Repeat);
        }
    }
}
