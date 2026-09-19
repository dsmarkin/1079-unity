using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Snow;

namespace Height1079.EditorTools.World
{
    /// <summary>Snow effects library: soft flake sprites for the snowfall, boot prints and trodden-snow decals with fade levels (snow slowly fills them).
    /// Materials are saved as assets so the transparent shader variants ship in builds. Runtime: Height1079.Runtime.SnowFx (the snowfall)
    /// and Height1079.Snow.SnowPrints (the prints, shared with the sandbox).</summary>
    public static class SnowFxFactory
    {
        public const int FadeLevels = SnowPrints.FadeLevels;
        const string Dir = WorldPaths.Generated + "/Materials/SnowFx";

        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            var flakes = TextureFactory.Save("snow_flakes", Flakes(), true, TextureWrapMode.Clamp);
            var print = TextureFactory.Save("footprint", SnowPrintArt.Footprint(), true, TextureWrapMode.Clamp);
            var trod = TextureFactory.Save("trodden_snow", SnowPrintArt.Trodden(), true, TextureWrapMode.Clamp);

            ParticleFade("Snowflakes", flakes, new Color(1f, 1f, 1f, 1f), lit: true);
            ParticleFade("SnowPuff", flakes, new Color(.95f, .97f, 1f, .8f), lit: true);
            ParticleAdd("Flame", TextureFactory.Save("flame", Flame(), true, TextureWrapMode.Clamp));
            var smoke = TextureFactory.Save("smoke", Smoke(), true, TextureWrapMode.Clamp);
            ParticleFade("Smoke", smoke, new Color(.62f, .62f, .64f, .9f), lit: true);
            // ground mist between the trunks: lit, soft against the snow and the trees, fades near the camera
            var mist = ParticleFade("Mist", TextureFactory.Save("mist", Mist(), true, TextureWrapMode.Clamp), new Color(.78f, .82f, .88f, 1f), lit: true);
            mist.EnableKeyword("_SOFTPARTICLES_ON"); mist.SetFloat("_SoftParticlesEnabled", 1);
            mist.SetFloat("_SoftParticlesNearFadeDistance", 0f); mist.SetFloat("_SoftParticlesFarFadeDistance", 2.5f);
            mist.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 2.5f, 0, 0));
            mist.EnableKeyword("_FADING_ON"); mist.SetFloat("_CameraFadingEnabled", 1);
            mist.SetFloat("_CameraNearFadeDistance", 1.5f); mist.SetFloat("_CameraFarFadeDistance", 6f);
            mist.SetVector("_CameraFadeParams", new Vector4(1.5f, 1f / 4.5f, 0, 0));
            EditorUtility.SetDirty(mist);
            for (int i = 0; i < FadeLevels; i++)
            {
                float a = 1f - i / (float)FadeLevels;
                LitFade($"Footprint_{i}", print, new Color(1, 1, 1, a));
                LitFade($"Trodden_{i}", trod, new Color(1, 1, 1, a * .75f));
            }
        }

        static Material Save(Material m, string name)
        {
            string path = $"{Dir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Particles/Standard Unlit (or Surface when lit — snow and smoke must stay dark at night and catch the torch beam) in Fade mode.</summary>
        static Material ParticleFade(string name, Texture2D tex, Color c, bool lit = false)
        {
            var m = new Material(Shader.Find(lit ? "Particles/Standard Surface" : "Particles/Standard Unlit")) { name = name, mainTexture = tex, color = c };
            if (lit) { m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f); }
            m.SetFloat("_Mode", 2); m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0); m.SetFloat("_BlendOp", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            m.enableInstancing = true;
            return Save(m, name);
        }

        /// <summary>Particles/Standard Unlit in Additive mode (flames, sparks).</summary>
        static Material ParticleAdd(string name, Texture2D tex)
        {
            var m = new Material(Shader.Find("Particles/Standard Unlit")) { name = name, mainTexture = tex, color = Color.white };
            m.SetFloat("_Mode", 4); m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0); m.SetFloat("_BlendOp", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            return Save(m, name);
        }

        /// <summary>Very soft, wide, uneven blob for ground mist.</summary>
        static Texture2D Mist()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v * 2.2f);
                    float n = Mathf.PerlinNoise(x * .05f, y * .05f) * .6f + Mathf.PerlinNoise(x * .13f + 7, y * .13f) * .4f;
                    float a = Mathf.Clamp01(1 - r) * (.55f + .45f * n);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Flame tongue: bright core, soft teardrop fading upward, wavy edge.</summary>
        static Texture2D Flame()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S;
                    float width = .75f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1, v * 1.1f + .02f)), .8f) * (1 - v * .55f);
                    float wave = .08f * Mathf.Sin(v * 11 + Mathf.PerlinNoise(x * .05f, y * .05f) * 3);
                    float d = Mathf.Abs(u - wave) / Mathf.Max(.02f, width);
                    float a = Mathf.Clamp01(1 - d); a = a * a * Mathf.Clamp01((1 - v) * 1.6f);
                    float core = Mathf.Clamp01(1 - d * 1.8f) * Mathf.Clamp01(1 - v * 1.4f);
                    t.SetPixel(x, y, new Color(1, .75f + .25f * core, .5f + .5f * core, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Standard (lit) in Fade mode, so prints darken at night with the rest of the scene. The recipe is
        /// <see cref="SnowPrintArt.LitFade"/>, which the run time also uses when these assets are missing.</summary>
        static Material LitFade(string name, Texture2D tex, Color c) => Save(SnowPrintArt.LitFade(name, tex, c), name);

        /// <summary>2×2 atlas of snowflakes: soft round blob, slightly irregular clump, tiny crystal-ish star, blurred large flake.</summary>
        static Texture2D Flakes()
        {
            const int S = 256, H = S / 2;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var rnd = new System.Random(7);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cell = (x >= H ? 1 : 0) + (y >= H ? 2 : 0);
                    float u = (x % H) / (float)H * 2 - 1, v = (y % H) / (float)H * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u), a;
                    switch (cell)
                    {
                        case 0: a = Mathf.Clamp01(1 - r * 1.25f); a = a * a * (3 - 2 * a); break;
                        case 1:
                        {
                            float lobes = .78f + .12f * Mathf.Sin(ang * 5 + 1) + .07f * Mathf.Sin(ang * 3);
                            a = Mathf.Clamp01((lobes - r) * 3.2f);
                            a *= .75f + .25f * Mathf.PerlinNoise(u * 4 + 3, v * 4);
                            break;
                        }
                        case 2:
                        {
                            float arms = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3)), 10);
                            a = Mathf.Clamp01((1 - r) * 1.4f) * (.35f + .65f * arms) + Mathf.Clamp01(1 - r * 4f);
                            a = Mathf.Clamp01(a);
                            break;
                        }
                        default: a = Mathf.Exp(-r * r * 3.2f) * Mathf.Clamp01((1 - r) * 4f); break;
                    }
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Wood smoke puff: soft round blob with billowy noise.</summary>
        static Texture2D Smoke()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                    float n = .55f + .45f * Mathf.PerlinNoise(x * .07f + 3, y * .07f) * Mathf.PerlinNoise(x * .15f, y * .15f + 5) * 2f;
                    float a = Mathf.Clamp01(1 - r) ; a = a * a * (3 - 2 * a) * Mathf.Clamp01(n);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }
    }
}
