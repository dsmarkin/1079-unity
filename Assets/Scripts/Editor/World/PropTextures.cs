using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Procedural albedo + normal textures for the 1959 camp props (no CC0 source for these): rucksack canvas, leather,
    /// quilted blankets and jackets, felt, knitted wool, camera vulcanite, end grain, galvanised tin with soot, varnished ski wood, paper.
    /// Each generator returns a height field too; <see cref="Pair"/> saves both maps.</summary>
    public static class PropTextures
    {
        const int S = 512;

        /// <summary>Albedo from colour(x, y) and a normal map from height(x, y); materials cached by name.</summary>
        public static Material Pair(string name, Func<float, float, Color> color, Func<float, float, float> height, float bump, float smoothness, int size = S, float metallic = 0f)
        {
            var existing = Materials.Find(name);
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var h = new float[size * size];
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    px[y * size + x] = color(u, v);
                    h[y * size + x] = height(u, v);
                }
            albedo.SetPixels(px); albedo.Apply();
            var nrm = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var np = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = h[y * size + (x + size - 1) % size], r = h[y * size + (x + 1) % size];
                    float d = h[((y + size - 1) % size) * size + x], t = h[((y + 1) % size) * size + x];
                    var n = new Vector3((l - r) * bump, (d - t) * bump, 1f).normalized;
                    np[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
            nrm.SetPixels(np); nrm.Apply();
            var a = TextureFactory.Save(name + "_albedo", albedo, false, TextureWrapMode.Repeat);
            var nm = SaveNormal(name + "_normal", nrm);
            var m = Materials.Get(name, Color.white, a, nm, smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D SaveNormal(string name, Texture2D tex)
        {
            Directory.CreateDirectory(TextureFactory.Dir);
            string path = $"{TextureFactory.Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.NormalMap; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float P(float u, float v, float f, float seed = 0) => Mathf.PerlinNoise(u * f + seed, v * f + seed * .7f);
        /// <summary>Tileable fractal noise over the unit square (wraps by blending four offsets).</summary>
        static float T(float u, float v, float f, float seed = 0)
        {
            float a = P(u, v, f, seed), b = P(u - 1, v, f, seed), c = P(u, v - 1, f, seed), d = P(u - 1, v - 1, f, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        static float Fbm(float u, float v, float f, float seed = 0) => (T(u, v, f, seed) + .5f * T(u, v, f * 2, seed + 3) + .25f * T(u, v, f * 4, seed + 7)) / 1.75f;
        static Color Shade(Color c, float k) { var r = c * k; r.a = 1; return r; }

        // ------------------------------------------------------------------ fabrics

        /// <summary>Heavy cotton canvas: 2×2 weave, washed-out blotches, grime towards the bottom (v = 0), darker seams.</summary>
        public static Material RuckCanvas(string name, Color baseColor, int seed)
        {
            float Weave(float u, float v) { float x = u * S / 3f, y = v * S / 3f; return (Mathf.Sin(x * Mathf.PI) * .5f + .5f) * ((int)y % 2 == 0 ? 1 : .6f) + (Mathf.Sin(y * Mathf.PI) * .5f + .5f) * ((int)x % 2 == 0 ? .6f : 1); }
            return Pair(name, (u, v) =>
            {
                float blotch = Fbm(u, v, 4, seed), grime = Mathf.Clamp01(.35f - v) * 1.4f + (1 - blotch) * .25f;
                float seam = (Mathf.Abs(u - .5f) < .006f || Mathf.Abs(v - .82f) < .005f) ? .72f : 1f;
                float w = .92f + .08f * Weave(u, v);
                var c = Color.Lerp(baseColor * (.82f + .36f * blotch), new Color(.2f, .18f, .15f), grime * .5f);
                return Shade(c, w * seam);
            }, (u, v) => Weave(u, v) * .35f + Fbm(u, v, 6, seed) * .6f, 3f, .08f);
        }

        /// <summary>Old leather: pebbled grain, creases, rubbed lighter edges.</summary>
        public static Material Leather(string name, Color baseColor, int seed)
        {
            float Grain(float u, float v) => T(u, v, 90, seed) * .6f + T(u, v, 45, seed + 1) * .4f;
            float Crease(float u, float v) => Mathf.Pow(1 - Mathf.Abs(T(u, v, 6, seed + 5) * 2 - 1), 12);
            return Pair(name, (u, v) => Shade(baseColor, .75f + .45f * Fbm(u, v, 5, seed) - .25f * Crease(u, v) + .08f * Grain(u, v)),
                (u, v) => Grain(u, v) * .5f - Crease(u, v) * .8f, 5f, .32f);
        }

        /// <summary>Wadded cotton quilt (одеяло / ватник): stitched channels or diamonds, puffy between the seams, sateen sheen.</summary>
        public static Material Quilt(string name, Color baseColor, bool diamonds, float cells, int seed)
        {
            float Puff(float u, float v)
            {
                float a = diamonds ? Mathf.Abs(Mathf.Sin((u + v) * cells * Mathf.PI)) * Mathf.Abs(Mathf.Sin((u - v) * cells * Mathf.PI))
                                   : Mathf.Abs(Mathf.Sin(u * cells * Mathf.PI));
                return Mathf.Sqrt(a);
            }
            return Pair(name, (u, v) =>
            {
                float puff = Puff(u, v), wear = Fbm(u, v, 5, seed);
                var c = baseColor * (.7f + .35f * puff) * (.85f + .3f * wear);
                return Shade(Color.Lerp(c, new Color(.5f, .47f, .42f), (1 - wear) * .15f), 1);
            }, (u, v) => Puff(u, v) + T(u, v, 60, seed) * .05f, 6f, .22f);
        }

        /// <summary>Pressed felt (валенки): fuzzy fibres, no weave, dirt at the sole.</summary>
        public static Material Felt(string name, Color baseColor, int seed)
            => Pair(name, (u, v) => Shade(Color.Lerp(baseColor, new Color(.12f, .11f, .1f), Mathf.Clamp01(.2f - v) * 3f), .8f + .35f * T(u, v, 70, seed) * T(u, v, 9, seed + 2)),
                (u, v) => T(u, v, 120, seed) * .6f + T(u, v, 12, seed + 4) * .4f, 2.5f, .02f);

        /// <summary>Hand-knitted wool: rows of V stitches.</summary>
        public static Material Knit(string name, Color baseColor, int seed)
        {
            float St(float u, float v) { float x = u * 24, y = v * 32; float fx = x - Mathf.Floor(x), fy = y - Mathf.Floor(y); float vee = 1 - Mathf.Abs(fx - .5f) * 2; return Mathf.Clamp01(1 - Mathf.Abs(fy - vee * .6f) * 2.2f); }
            return Pair(name, (u, v) => Shade(baseColor, .65f + .45f * St(u, v) + .1f * T(u, v, 20, seed)), St, 5f, .02f);
        }

        // ------------------------------------------------------------------ hard surfaces

        /// <summary>Vulcanite leatherette of a 1950s rangefinder: black, fine pebbles.</summary>
        public static Material Vulcanite(string name)
            => Pair(name, (u, v) => Shade(new Color(.05f, .05f, .05f), .8f + .4f * T(u, v, 160)), (u, v) => T(u, v, 160) * .7f + T(u, v, 80, 3) * .3f, 6f, .35f, 256);

        /// <summary>Galvanised tin bucket: spangle up top, soot and scorch creeping up from the bottom (v = 0 is the bottom).</summary>
        public static Material SootyTin(string name, int seed)
            => Pair(name, (u, v) =>
            {
                float spangle = Mathf.Floor(T(u, v, 30, seed) * 5) / 5f;
                float sootLine = .38f + .12f * T(u, v, 6, seed + 2);
                float soot = Noise.Smooth(sootLine + .08f, sootLine - .08f, v);
                var tin = new Color(.62f, .64f, .63f) * (.85f + .25f * spangle);
                var black = new Color(.05f, .045f, .04f) * (.8f + .5f * T(u, v, 40, seed + 5));
                return Shade(Color.Lerp(tin, black, soot), 1);
            }, (u, v) => T(u, v, 30, seed) * .2f + T(u, v, 5, seed + 9) * .3f, 2f, .45f, S, .55f);

        /// <summary>Saw-cut end grain: growth rings around the pith at (.5, .5), radial checks, bark ring at the edge.</summary>
        public static Material EndGrain(string name, bool charred)
            => Pair(name, (u, v) =>
            {
                float dx = u - .5f, dy = v - .5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2, a = Mathf.Atan2(dy, dx);
                float wobble = r + .03f * Mathf.Sin(a * 3) + .02f * P(u, v, 8);
                float ring = Mathf.Pow(Mathf.Abs(Mathf.Sin(wobble * 38)), 3);
                float check = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 2.5f + P(u, v, 3) * 2)), 60) * Noise.Smooth(.2f, .9f, r);
                var wood = Color.Lerp(new Color(.83f, .7f, .5f), new Color(.62f, .47f, .3f), ring * .6f) * (1 - check * .6f);
                if (r > .9f) wood = new Color(.3f, .23f, .17f);
                if (charred) wood = Color.Lerp(wood * .35f, new Color(.04f, .035f, .03f), Noise.Smooth(.25f, .75f, r));
                return Shade(wood, 1);
            }, (u, v) => { float r = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2; return Mathf.Abs(Mathf.Sin(r * 38)) * .3f; }, 2f, .05f, 256);

        /// <summary>Varnished birch: long streaky grain along v, darker scuffs.</summary>
        public static Material Varnish(string name, Color baseColor, int seed)
            => Pair(name, (u, v) => Shade(baseColor, .78f + .3f * P(u * 30, v * 1.5f, 1, seed) - .2f * Mathf.Pow(T(u, v, 7, seed + 3), 6)),
                (u, v) => P(u * 30, v * 1.5f, 1, seed) * .3f, 2f, .55f);

        /// <summary>Charred log surface: cracked alligator char with grey ash in the cracks.</summary>
        public static Material Char(string name)
            => Pair(name, (u, v) =>
            {
                float cells = T(u, v, 14) * T(u, v, 28, 2); float crack = Mathf.Pow(1 - Mathf.Abs(cells * 2 - .5f), 18);
                return Shade(Color.Lerp(new Color(.035f, .03f, .028f), new Color(.45f, .44f, .42f), crack * .7f), 1);
            }, (u, v) => T(u, v, 14) * T(u, v, 28, 2), 6f, .15f);

        /// <summary>Bamboo culm: yellowish with darker nodes every 0.3 m (v in metres × 1).</summary>
        public static Material Bamboo(string name)
            => Pair(name, (u, v) => { float node = Mathf.Pow(Mathf.Abs(Mathf.Cos(v * Mathf.PI * 2)), 80); return Shade(Color.Lerp(new Color(.78f, .66f, .42f), new Color(.42f, .32f, .18f), node + .15f * T(u, v, 12)), 1); },
                (u, v) => Mathf.Pow(Mathf.Abs(Mathf.Cos(v * Mathf.PI * 2)), 80), 3f, .5f, 256);

        /// <summary>Paper block edge: fine horizontal lines.</summary>
        public static Material PaperEdge(string name)
            => Pair(name, (u, v) => Shade(new Color(.9f, .87f, .78f), .9f + .1f * Mathf.Sin(v * 400)), (u, v) => Mathf.Sin(v * 400) * .5f, 1f, .05f, 256);

        public static Material Chrome => Metal("Chrome", new Color(.78f, .78f, .76f), .9f, .82f);
        public static Material DarkSteel => Metal("DarkSteel", new Color(.22f, .22f, .23f), .8f, .45f);
        public static Material Glass => Metal("LensGlass", new Color(.02f, .03f, .05f), .2f, .96f);

        public static Material Metal(string name, Color c, float metallic, float smoothness)
        {
            var m = Materials.Get(name, c, null, null, smoothness);
            m.SetFloat("_Metallic", metallic); EditorUtility.SetDirty(m);
            return m;
        }
    }
}
