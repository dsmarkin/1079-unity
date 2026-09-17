using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Height1079.EditorTools.World
{
    /// <summary>Small mesh assembly helper for the procedural art library (trees, rocks, site props). Several submeshes = several materials.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] tris;

        public MeshBuilder(int submeshes)
        {
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        public int VertexCount => v.Count;

        public int Vert(Vector3 p, Vector3 normal, Vector2 t) { v.Add(p); n.Add(normal); uv.Add(t); return v.Count - 1; }
        public void Tri(int s, int a, int b, int c) { tris[s].Add(a); tris[s].Add(b); tris[s].Add(c); }

        /// <summary>Quad a-b-c-d; the face points along Cross(b - a, d - a) (Unity: a-b-c-d appear clockwise from that side). Double-sided adds the back face with a flipped normal (cards).</summary>
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, bool doubleSided = false, Vector3? normalOverride = null)
        {
            Vector3 nn = normalOverride ?? Vector3.Cross(b - a, d - a).normalized;
            int i0 = Vert(a, nn, ua), i1 = Vert(b, nn, ub), i2 = Vert(c, nn, uc), i3 = Vert(d, nn, ud);
            Tri(s, i0, i1, i2); Tri(s, i0, i2, i3);
            if (!doubleSided) return;
            int j0 = Vert(a, -nn, ua), j1 = Vert(b, -nn, ub), j2 = Vert(c, -nn, uc), j3 = Vert(d, -nn, ud);
            Tri(s, j0, j2, j1); Tri(s, j0, j3, j2);
        }

        public void Box(int s, Vector3 center, Vector3 size, Quaternion rot, float uvScale = 1f)
        {
            Vector3 h = size / 2;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            void F(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float w, float hh) =>
                Quad(s, a, b, c, d, Vector2.zero, new Vector2(w * uvScale, 0), new Vector2(w * uvScale, hh * uvScale), new Vector2(0, hh * uvScale));
            F(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), size.x, size.y);
            F(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), size.x, size.y);
            F(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), size.z, size.y);
            F(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), size.z, size.y);
            F(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), size.x, size.z);
            F(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), size.x, size.z);
        }

        /// <summary>Tapered open tube from a to b (bark UVs: u around, v along in metres × uvScale). Optional end cap at b.</summary>
        public void Tube(int s, Vector3 a, Vector3 b, float r0, float r1, int sides, float uvScale = 1f, float v0 = 0f, bool capEnd = false)
        {
            Vector3 dir = b - a; float len = dir.magnitude; if (len < 1e-4f) return;
            dir /= len;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > .95f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            int start = v.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                Vector3 c = ring == 0 ? a : b; float r = ring == 0 ? r0 : r1;
                for (int i = 0; i <= sides; i++)
                {
                    float ang = i / (float)sides * Mathf.PI * 2;
                    Vector3 o = side * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                    Vert(c + o * r, o, new Vector2(i / (float)sides * Mathf.Max(1, Mathf.Round(r0 * 6.28f * uvScale)), (v0 + ring * len) * uvScale));
                }
            }
            int k = sides + 1;
            for (int i = 0; i < sides; i++) { Tri(s, start + i, start + k + i, start + i + 1); Tri(s, start + i + 1, start + k + i, start + k + i + 1); }
            if (!capEnd || r1 < 1e-4f) return;
            int center = Vert(b, dir, new Vector2(.5f, .5f));
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2, a1 = (i + 1) / (float)sides * Mathf.PI * 2;
                int p0 = Vert(b + (side * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * r1, dir, new Vector2(.5f + .5f * Mathf.Cos(a0), .5f + .5f * Mathf.Sin(a0)));
                int p1 = Vert(b + (side * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * r1, dir, new Vector2(.5f + .5f * Mathf.Cos(a1), .5f + .5f * Mathf.Sin(a1)));
                Tri(s, center, p1, p0);
            }
        }

        /// <summary>Closed cone (apex up) — used by far LODs and simple props.</summary>
        public void Cone(int s, Vector3 baseCenter, float radius, float height, int sides, float uvScale = 1f)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2, a1 = (i + 1) / (float)sides * Mathf.PI * 2;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                Vector3 nn = Vector3.Cross(apex - p0, p1 - p0).normalized; if (nn.y < 0) nn = -nn;
                int i0 = Vert(p0, nn, new Vector2(i / (float)sides * uvScale, 0)), i1 = Vert(p1, nn, new Vector2((i + 1) / (float)sides * uvScale, 0)), i2 = Vert(apex, Vector3.up, new Vector2((i + .5f) / sides * uvScale, height * uvScale));
                Tri(s, i0, i2, i1);
                int b0 = Vert(p0, Vector3.down, Vector2.zero), b1 = Vert(p1, Vector3.down, Vector2.zero), bc = Vert(baseCenter, Vector3.down, Vector2.zero);
                Tri(s, bc, b0, b1);
            }
        }

        /// <summary>Appends another builder's geometry transformed by <paramref name="m"/> (same submesh layout).</summary>
        public void Append(MeshBuilder other, Matrix4x4 m)
        {
            int off = v.Count;
            for (int i = 0; i < other.v.Count; i++) { v.Add(m.MultiplyPoint3x4(other.v[i])); n.Add(m.MultiplyVector(other.n[i]).normalized); uv.Add(other.uv[i]); }
            for (int s = 0; s < tris.Length && s < other.tris.Length; s++) foreach (var t in other.tris[s]) tris[s].Add(t + off);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = tris.Length;
            for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s, false);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        public int TriangleCount { get { int c = 0; foreach (var t in tris) c += t.Count / 3; return c; } }
    }
}
