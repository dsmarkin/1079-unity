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

        static Texture2D Save(string name, Texture2D tex, bool alpha, TextureWrapMode wrap)
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
        public static Texture2D Twigs(string name, int seed)
        {
            const int W = 256, H = 256;
            var rnd = new System.Random(seed);
            var t = New(W, H, new Color(.3f, .22f, .2f, 0));
            void Branch(Vector2 a, float ang, float len, float width, int depth)
            {
                if (depth > 5 || len < 4) return;
                Vector2 b = a + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * len;
                Stroke(t, a, b, width, new Color(.29f + .05f * depth, .2f, .19f, 1));
                int kids = 2 + rnd.Next(2);
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
