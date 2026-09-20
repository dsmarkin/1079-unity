using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Animal trails on the snow (Tools/terrain/tracks.py → tracks.f32): each print is a small quad lying on the terrain,
    /// merged into 64 m chunk meshes with an atlas of print shapes (hare group, canid, elk, mustelid pair, grouse, ptarmigan, squirrel).
    /// Prefab Assets/Generated/World/Prefabs/Tracks/AnimalTracks (75 MB of chunk meshes: game only, outside Resources); runtime fades the material while a blizzard fills them.</summary>
    public static class AnimalTracksFactory
    {
        const string PrefabDir = WorldPaths.Kit + "/Prefabs/Tracks";
        const string MeshDir = WorldPaths.Kit + "/Meshes/Tracks";
        const float Chunk = 64f;
        // atlas cell per kind (4×2), print size (m) along and across the direction of travel
        static readonly int[] Cell = { 0, 1, 2, 3, 4, 5, 6, 1 };
        // in deep powder a print is a hole wider than the paw
        static readonly Vector2[] Size = { new Vector2(.75f, .5f), new Vector2(.24f, .19f), new Vector2(.6f, .36f), new Vector2(.3f, .3f), new Vector2(.2f, .18f), new Vector2(.2f, .17f), new Vector2(.34f, .28f), new Vector2(.32f, .26f) };

        public static void Build(HeightField dem)
        {
            string src = $"{WorldPaths.Source}/tracks.f32";
            if (!File.Exists(src)) { Debug.LogWarning("1079: tracks.f32 missing, no animal tracks"); return; }
            var tracks = Dem.LoadTracks(File.ReadAllBytes(src));
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(MeshDir);
            var mat = Material();

            var chunks = new Dictionary<(int, int), MeshBuilder>();
            foreach (var t in tracks)
            {
                var key = (Mathf.FloorToInt((t.X + HeightField.Half) / Chunk), Mathf.FloorToInt((t.Z + HeightField.Half) / Chunk));
                if (!chunks.TryGetValue(key, out var mb)) chunks[key] = mb = new MeshBuilder(1);
                int k = (int)t.Kind;
                var fwd = Quaternion.Euler(0, t.Yaw, 0) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var sz = Size[k];
                var c = new Vector3(t.X, 0, t.Z);
                Vector3 P(float a, float b)
                {
                    var p = c + fwd * (a * sz.x * .5f) + right * (b * sz.y * .5f);
                    p.y = WorldData.GroundHeight(dem, p.x, p.z) + .045f;
                    return p;
                }
                int cell = Cell[k];
                float u0 = (cell % 4) / 4f, v0 = (cell / 4) / 2f, du = .25f, dv = .5f;
                // quad a-b-c-d with the face up; v runs along the direction of travel
                mb.Quad(0, P(-1, -1), P(1, -1), P(1, 1), P(-1, 1),
                    new Vector2(u0, v0), new Vector2(u0, v0 + dv), new Vector2(u0 + du, v0 + dv), new Vector2(u0 + du, v0), false, Vector3.up);
            }

            var root = new GameObject("AnimalTracks");
            foreach (var kv in chunks)
            {
                string name = $"Tracks_{kv.Key.Item1}_{kv.Key.Item2}";
                var mesh = kv.Value.ToMesh(name);
                string path = $"{MeshDir}/{name}.asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // chunks vanish when far (prints are centimetres)
                var lg = go.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(.3f, new Renderer[] { r }) });   // a 64 m chunk disappears beyond ~190 m
                lg.RecalculateBounds();
            }
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/AnimalTracks.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"1079 world: {tracks.Length} animal prints in {chunks.Count} chunks");
        }

        static Material Material()
        {
            var tex = TextureFactory.Save("animal_tracks", Atlas(), true, TextureWrapMode.Clamp);
            string path = $"{WorldPaths.Generated}/Materials/AnimalTracks.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Standard")) { name = "AnimalTracks", mainTexture = tex, color = Color.white };
            m.SetFloat("_Mode", 2); m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_Glossiness", .1f); m.SetFloat("_Metallic", 0);
            m.renderQueue = 2991;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ atlas

        const int CellPx = 128;

        /// <summary>4×2 atlas: 0 hare group, 1 canid, 2 elk, 3 mustelid pair, 4 grouse, 5 ptarmigan, 6 squirrel group.
        /// In each cell v (0..1) runs along the direction of travel, u across. Prints are dents: blue-grey hollow, bright rim.</summary>
        static Texture2D Atlas()
        {
            var t = new Texture2D(CellPx * 4, CellPx * 2, TextureFormat.RGBA32, false);
            var px = new Color[t.width * t.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(.8f, .85f, .95f, 0);
            for (int cell = 0; cell < 7; cell++)
                for (int y = 0; y < CellPx; y++)
                    for (int x = 0; x < CellPx; x++)
                    {
                        float u = (x + .5f) / CellPx * 2 - 1, v = (y + .5f) / CellPx * 2 - 1;   // v forward
                        float d = Depth(cell, u, v);
                        if (d <= 0) continue;
                        float rim = Mathf.Clamp01(1 - Mathf.Abs(d - .06f) * 14f) * .3f;
                        float hollow = Mathf.Clamp01((d - .06f) * 3f);
                        // the hole is in its own shadow: blue-grey, darkest on the far wall
                        float shade = Mathf.Lerp(.6f, .22f, hollow) * (.9f + .1f * Mathf.PerlinNoise(x * .3f + cell * 10, y * .3f)) * Mathf.Lerp(1f, .8f, (v + 1) * .5f);
                        var c = new Color(shade * .78f, shade * .86f, shade * 1.05f, Mathf.Clamp01(hollow * 1.2f));
                        if (rim > c.a) c = new Color(.97f, .98f, 1f, rim);
                        // atlas cell origin: column = cell % 4, row = cell / 4; texture x = across (u), y = along (v), as in the quad UVs
                        int tx = (cell % 4) * CellPx + x, ty = (cell / 4) * CellPx + y;
                        px[ty * t.width + tx] = c;
                    }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static float Blob(float u, float v, float cu, float cv, float ru, float rv)
        {
            float a = (u - cu) / ru, b = (v - cv) / rv;
            return Mathf.Clamp01(1 - (a * a + b * b));
        }

        static float Toe(float u, float v, float cu, float cv, float ang, float len, float w)
        {
            // thin stroke from (cu,cv) in direction ang (0 = forward)
            float du = u - cu, dv = v - cv;
            float along = du * Mathf.Sin(ang) + dv * Mathf.Cos(ang), across = du * Mathf.Cos(ang) - dv * Mathf.Sin(ang);
            if (along < 0 || along > len) return 0;
            return Mathf.Clamp01(1 - Mathf.Abs(across) / w) * Mathf.Clamp01(1 - along / len * .6f);
        }

        static float Depth(int cell, float u, float v)
        {
            switch (cell)
            {
                case 0: // hare: two long hind prints side by side in front, two small fore prints one behind the other
                    return Mathf.Max(Mathf.Max(Blob(u, v, -.38f, .45f, .22f, .42f), Blob(u, v, .38f, .45f, .22f, .42f)),
                                     Mathf.Max(Blob(u, v, -.05f, -.3f, .16f, .17f), Blob(u, v, .08f, -.7f, .16f, .17f)));
                case 1: // canid: oval pad with four toes
                    return Mathf.Max(Blob(u, v, 0, -.25f, .45f, .4f),
                        Mathf.Max(Mathf.Max(Blob(u, v, -.25f, .45f, .17f, .2f), Blob(u, v, .25f, .45f, .17f, .2f)),
                                  Mathf.Max(Blob(u, v, -.55f, .1f, .15f, .18f), Blob(u, v, .55f, .1f, .15f, .18f))));
                case 2: // elk: a deep hole with the split hoof at the front
                    return Mathf.Max(Blob(u, v, 0, -.1f, .75f, .85f) * 1.2f, Mathf.Max(Blob(u, v, -.3f, .7f, .25f, .25f), Blob(u, v, .3f, .7f, .25f, .25f)));
                case 3: // mustelid: two roundish prints side by side, one slightly ahead
                    return Mathf.Max(Blob(u, v, -.45f, .15f, .4f, .5f), Blob(u, v, .45f, -.15f, .4f, .5f));
                case 4: // grouse: three toes forward, one back
                    return Mathf.Max(Toe(u, v, 0, -.2f, 0, 1f, .12f), Mathf.Max(Toe(u, v, 0, -.2f, -.7f, .8f, .12f),
                        Mathf.Max(Toe(u, v, 0, -.2f, .7f, .8f, .12f), Toe(u, v, 0, -.2f, Mathf.PI, .6f, .1f))));
                case 5: // ptarmigan: feathered toes, blurred
                    return Mathf.Max(Blob(u, v, 0, 0, .45f, .6f) * .8f, Mathf.Max(Toe(u, v, 0, -.3f, -.5f, .9f, .25f), Toe(u, v, 0, -.3f, .5f, .9f, .25f)));
                case 6: // squirrel: two long hind in front, two small fore just behind
                    return Mathf.Max(Mathf.Max(Blob(u, v, -.45f, .35f, .25f, .45f), Blob(u, v, .45f, .35f, .25f, .45f)),
                                     Mathf.Max(Blob(u, v, -.2f, -.45f, .15f, .2f), Blob(u, v, .2f, -.45f, .15f, .2f)));
            }
            return 0;
        }
    }
}
