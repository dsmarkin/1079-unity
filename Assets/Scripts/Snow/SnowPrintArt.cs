using UnityEngine;

namespace Height1079.Snow
{
    /// <summary>The pictures the prints are made of, painted in code. The editor's factory bakes them into assets so
    /// the transparent shader variants ship in builds; <see cref="SnowPrints"/> paints them again at run time when
    /// those assets are missing (a fresh clone that has not generated the world yet), so nobody ever walks on snow
    /// that takes no print.</summary>
    public static class SnowPrintArt
    {
        /// <summary>Boot print pressed into snow (toe up = +v): dark bluish shadow on the lit side, pale rim, soft edge.</summary>
        public static Texture2D Footprint()
        {
            const int W = 64, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "footprint" };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W * 2 - 1, v = (y + .5f) / H * 2 - 1;
                    // sole: wider toe, narrow waist, heel
                    float half = v > .05f ? .82f - .35f * Mathf.Pow((v - .05f) / .95f, 3) : v > -.35f ? .58f + .2f * (v + .35f) / .4f : .7f * Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Pow((v + .35f) / .65f, 2)));
                    float lenMask = Mathf.Clamp01((1 - Mathf.Abs(v)) * 8f);
                    float d = Mathf.Abs(u) / Mathf.Max(.05f, half);
                    float inside = Mathf.Clamp01((1 - d) * 5f) * lenMask;
                    float rim = Mathf.Clamp01(1 - Mathf.Abs(d - 1.05f) * 6f) * lenMask * .45f;
                    float tread = .85f + .15f * Mathf.Sign(Mathf.Sin(v * 40f));
                    float shade = Mathf.Lerp(.55f, .8f, (u + 1) * .5f) * tread;
                    var c = new Color(shade * .86f, shade * .92f, shade, inside * .85f);
                    if (rim > c.a) c = new Color(.97f, .98f, 1f, rim);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>Compacted, trodden snow: soft greyish-blue blob with churned texture.</summary>
        public static Texture2D Trodden()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "trodden_snow" };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1;
                    float r = Mathf.Sqrt(u * u * 1.6f + v * v);
                    float n = Mathf.PerlinNoise(x * .09f, y * .09f), n2 = Mathf.PerlinNoise(x * .3f + 11, y * .3f);
                    float a = Mathf.Clamp01((1 - r) * 2.2f) * (.45f + .35f * n);
                    float shade = .72f + .18f * n2;
                    t.SetPixel(x, y, new Color(shade * .9f, shade * .95f, shade, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Standard (lit) in Fade mode, so a print darkens at night with the rest of the scene and reads in
        /// daylight as a dent and not a sticker. The same recipe the factory bakes; kept here so the two cannot drift.</summary>
        public static Material LitFade(string name, Texture2D tex, Color c)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, mainTexture = tex, color = c };
            m.SetFloat("_Mode", 2); m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_Glossiness", .15f); m.SetFloat("_Metallic", 0);
            m.renderQueue = 2990;
            m.enableInstancing = true;
            return m;
        }
    }
}
