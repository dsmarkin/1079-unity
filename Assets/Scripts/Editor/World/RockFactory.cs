using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Boulders of the stone ridges (курумы, "каменные гряды"): noise-displaced icospheres, lichen rock on the sides, snow on faces that look up.</summary>
    public static partial class RockFactory
    {
        const string MeshDir = WorldPaths.Generated + "/Meshes/Rocks";
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Rocks";

        static List<Vector3> verts; static List<int> tris;

        static void Icosphere(int subdiv)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    verts.Add(((verts[a] + verts[b]) / 2).normalized); cache[key] = verts.Count - 1; return verts.Count - 1;
                }
                var next = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
        }

        public static MeshBuilder Rock(int seed, int subdiv, Vector3 scale)
        {
            Icosphere(subdiv);
            float ox = seed * 3.1f, oz = seed * 7.3f;
            var p = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float n = Mathf.PerlinNoise(v.x * 1.3f + ox, v.z * 1.3f + v.y + oz) * .55f + Mathf.PerlinNoise(v.x * 3.7f + oz, v.y * 3.7f + ox) * .2f;
                var q = v * (.75f + n);
                q = new Vector3(q.x * scale.x, q.y * scale.y, q.z * scale.z);
                if (q.y < -.15f * scale.y) q.y = -.15f * scale.y + (q.y + .15f * scale.y) * .2f; // flat base sunk into the ground
                p[i] = q;
            }
            var mb = new MeshBuilder(2); // 0 rock, 1 snow
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = p[tris[i]], b = p[tris[i + 1]], c = p[tris[i + 2]];
                Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
                bool inward = Vector3.Dot(nn, a + b + c) < 0;
                if (inward) nn = -nn; // Unity front face: numeric Cross(b - a, c - a) points at the viewer side
                int s = nn.y > .55f ? 1 : 0;
                int ia = mb.Vert(a, nn, new Vector2(a.x + a.z, a.y) * .5f), ib = mb.Vert(b, nn, new Vector2(b.x + b.z, b.y) * .5f), ic = mb.Vert(c, nn, new Vector2(c.x + c.z, c.y) * .5f);
                if (inward) mb.Tri(s, ia, ic, ib); else mb.Tri(s, ia, ib, ic);
            }
            return mb;
        }

        public static List<GameObject> BuildLibrary() => BuildLibrary("Boulder", Materials.Snow);

        static List<GameObject> BuildLibrary(string name, Material top)
        {
            Directory.CreateDirectory(MeshDir); Directory.CreateDirectory(PrefabDir);
            var list = new List<GameObject>();
            var shapes = new[] { new Vector3(1.2f, .7f, 1f), new Vector3(1.6f, .9f, 1.1f), new Vector3(.9f, .6f, 1.3f), new Vector3(2.2f, 1.1f, 1.5f) };
            for (int k = 0; k < shapes.Length; k++)
            {
                var root = new GameObject($"{name}_{k}");
                var r = new List<Renderer>();
                var lods = new List<LOD>();
                for (int lod = 0; lod < 2; lod++)
                {
                    var mesh = Rock(k + 1, lod == 0 ? 3 : 1, shapes[k]).ToMesh($"{name}_{k}_LOD{lod}");
                    string path = $"{MeshDir}/{name}_{k}_LOD{lod}.asset";
                    AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
                    var go = new GameObject($"LOD{lod}", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Materials.Rock, top };
                    lods.Add(new LOD(lod == 0 ? .08f : .01f, new Renderer[] { go.GetComponent<MeshRenderer>() }));
                }
                var lg = root.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
                var col = root.AddComponent<SphereCollider>(); col.radius = Mathf.Min(shapes[k].x, shapes[k].z) * .8f;
                list.Add(PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}_{k}.prefab"));
                Object.DestroyImmediate(root);
            }
            return list;
        }
    }
}
