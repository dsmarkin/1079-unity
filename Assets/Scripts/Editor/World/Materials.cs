using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    public static class WorldPaths
    {
        /// <summary>Everything the world pipeline writes lives here (git-ignored, rebuilt by menu 1079 → Rebuild world).</summary>
        public const string Generated = "Assets/Resources/World";
        public const string Source = "Assets/Data/World";
        public const string PolyHaven = "Assets/Art/ThirdParty/PolyHaven";
        public const string Sketchfab = "Assets/Art/ThirdParty/Sketchfab";
        public static string PH(string id, string map) => $"{PolyHaven}/Textures/{id}/{id}_{map}_1k.jpg";
    }

    /// <summary>Material library (built-in render pipeline, Standard shader). One place to swap looks.</summary>
    public static class Materials
    {
        const string Dir = WorldPaths.Generated + "/Materials";
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static void ResetCache() => cache.Clear();
        static Material C(string key, Func<Material> make) { if (!cache.TryGetValue(key, out var m) || m == null) cache[key] = m = make(); return m; }

        public static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning("1079 world: missing texture " + path);
            return t;
        }

        public static Material Get(string name, Color color, Texture2D albedo = null, Texture2D normal = null, float smoothness = .1f, bool cutout = false, float cutoff = .45f, Vector2? tiling = null, float normalScale = 1f)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = m == null;
            if (fresh) m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.mainTexture = albedo;
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            m.SetFloat("_Glossiness", smoothness); m.SetFloat("_Metallic", 0);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            if (cutout)
            {
                m.SetFloat("_Mode", 1); m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetInt("_SrcBlend", 1); m.SetInt("_DstBlend", 0); m.SetInt("_ZWrite", 1);
                m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_Cutoff", cutoff);
                m.renderQueue = 2450;
            }
            m.enableInstancing = true;
            if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>An existing library material, untouched (null if the tree library was not built yet).</summary>
        public static Material Find(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{name}.mat");

        public static Material PH(string name, string id, Color tint, float tile, float smoothness = .15f, float normalScale = 1f)
            => Get(name, tint, Tex(WorldPaths.PH(id, "diff")), Tex(WorldPaths.PH(id, "nor_gl")), smoothness, tiling: new Vector2(tile, tile), normalScale: normalScale);

        // Shared looks
        public static Material Snow => C("Snow", () => PH("Snow", "snow_02", new Color(.97f, .98f, 1f), 1f, .35f, .6f));
        public static Material Bark => C("Bark", () => PH("BarkConifer", "pine_bark", new Color(.78f, .74f, .7f), 1f));
        public static Material BarkCedar => C("BarkCedar", () => PH("BarkCedar", "knotted_pine_bark", new Color(.72f, .68f, .64f), 1f));
        public static Material Rock => C("Rock", () => PH("RockLichen", "lichen_rock", new Color(.85f, .86f, .86f), 1f, .12f));
        public static Material Planks => C("Planks", () => PH("Planks", "raw_plank_wall", Color.white, 1f));
        public static Material Wood => C("Wood", () => Get("CutWood", new Color(.78f, .66f, .5f), smoothness: .05f));
        public static Material Charcoal => C("Charcoal", () => Get("Charcoal", new Color(.07f, .065f, .06f), smoothness: .02f));
        public static Material Ski => C("Ski", () => Get("SkiWood", new Color(.55f, .38f, .22f), smoothness: .35f));
        public static Material Rope => C("Rope", () => Get("Rope", new Color(.72f, .66f, .52f)));
        public static Material Metal => C("Metal", () => Get("Metal", new Color(.35f, .36f, .38f), smoothness: .55f));
        public static Material BirchBark => C("BirchBark", () => Get("BirchBark", Color.white, TextureFactory.BirchBark("birch_bark", 7), null, .08f));
        public static Material Canvas => C("Canvas", () => Get("TentCanvas", Color.white, TextureFactory.Canvas("tent_canvas", new Color(.47f, .47f, .34f)), OptionalTex(WorldPaths.PH("book_pattern", "nor_gl")), .05f, tiling: new Vector2(3, 3), normalScale: .8f));
        /// <summary>Texture if present (third-party scans are optional: the library still builds without them).</summary>
        public static Texture2D OptionalTex(string path) => System.IO.File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
        public static Material Sheet => C("Sheet", () => Get("WhiteSheet", new Color(.9f, .9f, .87f), smoothness: .05f));
        public static Material BirchCardboard => C("BirchCardboard", () => Get("BirchBarkFloor", new Color(.86f, .8f, .7f), TextureFactory.BirchBark("birch_bark_peeled", 11), null, .05f));
        public static Material Cloth(string name, Color c) => C("Cloth_" + name, () => Get("Cloth_" + name, Color.white, TextureFactory.Cloth("cloth_" + name, c), null, .03f));
        public static Material Marker(Color c) => C("Marker_" + ColorUtility.ToHtmlStringRGB(c), () => Get("Marker_" + ColorUtility.ToHtmlStringRGB(c), c, smoothness: .2f));
    }
}
