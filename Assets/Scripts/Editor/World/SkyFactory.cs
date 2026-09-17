using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Sky assets for Runtime/SkyDome: materials on the Height1079/Sky* shaders, a dome mesh, the moon quad and the star mesh
    /// built from Assets/Data/Sky/stars.f32 (Yale Bright Star Catalogue to V 6, equinox 1959; Tools/sky/stars.py).</summary>
    public static class SkyFactory
    {
        const string MatDir = WorldPaths.Generated + "/Materials/Sky";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Sky";

        public static void Build()
        {
            Directory.CreateDirectory(MatDir); Directory.CreateDirectory(MeshDir);
            foreach (var (name, shader) in new[] { ("Sky", "Height1079/Sky"), ("Stars", "Height1079/SkyStars"), ("Moon", "Height1079/SkyMoon"), ("Clouds", "Height1079/SkyClouds"), ("Aurora", "Height1079/SkyAurora") })
            {
                var sh = Shader.Find(shader);
                if (sh == null) { Debug.LogError("1079 sky: shader missing " + shader); continue; }
                string path = $"{MatDir}/{name}.mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(new Material(sh) { name = name }, path);
            }
            Save(Dome(), "Dome");
            Save(Quad(), "MoonQuad");
            Save(Stars(), "Stars");
        }

        static void Save(Mesh m, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }

        /// <summary>Unit sphere (the shaders cull front faces / use the direction only).</summary>
        static Mesh Dome()
        {
            const int U = 48, V = 24;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int j = 0; j <= V; j++)
                for (int i = 0; i <= U; i++)
                {
                    float th = i / (float)U * Mathf.PI * 2, ph = (j / (float)V - .5f) * Mathf.PI;
                    v.Add(new Vector3(Mathf.Cos(ph) * Mathf.Cos(th), Mathf.Sin(ph), Mathf.Cos(ph) * Mathf.Sin(th)));
                }
            for (int j = 0; j < V; j++)
                for (int i = 0; i < U; i++)
                {
                    int a = j * (U + 1) + i, b = a + 1, c = a + U + 1, d = c + 1;
                    t.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "SkyDome" };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            return m;
        }

        static Mesh Quad()
        {
            var m = new Mesh { name = "MoonQuad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }

        /// <summary>Star colour from the effective temperature (rough blackbody fit, kept pale: the eye sees stars almost white).</summary>
        static Color StarColor(float kelvin)
        {
            float t = kelvin / 100f;
            float r = t <= 66 ? 1f : Mathf.Clamp01(1.2929f * Mathf.Pow(t - 60, -.1332f));
            float g = t <= 66 ? Mathf.Clamp01(.3901f * Mathf.Log(t) - .6318f) : Mathf.Clamp01(1.1298f * Mathf.Pow(t - 60, -.0755f));
            float b = t >= 66 ? 1f : t <= 19 ? 0f : Mathf.Clamp01(.5432f * Mathf.Log(t - 10) - 1.1962f);
            return Color.Lerp(Color.white, new Color(r, g, b), .55f);
        }

        static Mesh Stars()
        {
            var raw = File.ReadAllBytes($"{WorldPaths.Source.Replace("/World", "/Sky")}/stars.f32");
            int n = raw.Length / 20;
            var v = new List<Vector3>(n * 4); var corner = new List<Vector2>(n * 4); var star = new List<Vector3>(n * 4); var col = new List<Color>(n * 4); var tri = new List<int>(n * 6);
            var rnd = new System.Random(59);
            for (int k = 0; k < n; k++)
            {
                int o = k * 20;
                float x = System.BitConverter.ToSingle(raw, o), y = System.BitConverter.ToSingle(raw, o + 4), z = System.BitConverter.ToSingle(raw, o + 8);
                float mag = System.BitConverter.ToSingle(raw, o + 12), kelvin = System.BitConverter.ToSingle(raw, o + 16);
                // the equatorial frame is right-handed, the game frame left-handed: store y negated (Runtime/SkyDome builds the rotation to match)
                var p = new Vector3(x, -y, z);
                float bright = Mathf.Pow(10f, -.4f * (mag - 1f));                 // relative flux, 1 at V = 1
                float size = Mathf.Lerp(.0011f, .0042f, Mathf.Clamp01((6.2f - mag) / 7.5f));   // tan of the half-size angle
                float b = Mathf.Clamp(Mathf.Pow(bright, .5f) * 1.1f, .05f, 2.2f);
                var c = StarColor(kelvin);
                float seed = (float)rnd.NextDouble();
                int i0 = v.Count;
                foreach (var cr in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                {
                    v.Add(p); corner.Add(cr); star.Add(new Vector3(size, b, seed)); col.Add(c);
                }
                tri.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var m = new Mesh { name = "Stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetUVs(0, corner); m.SetUVs(1, star); m.SetColors(col); m.SetTriangles(tri, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            return m;
        }
    }
}
