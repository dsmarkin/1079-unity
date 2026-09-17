using System;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>Cheap 3-D value noise from Unity's Perlin (−1…1) and fractal sums — for displacing organic props.</summary>
    public static class Noise
    {
        public static float N(float x, float y, float z)
        {
            float a = Mathf.PerlinNoise(x + 31.7f, y + 12.3f), b = Mathf.PerlinNoise(y + 5.1f, z + 71.9f), c = Mathf.PerlinNoise(z + 43.3f, x + 9.7f);
            return (a + b + c) / 1.5f - 1f;
        }

        public static float N(Vector3 p) => N(p.x, p.y, p.z);

        public static float Fbm(Vector3 p, int octaves = 3)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += N(p) * amp; norm += amp; p = p * 2.03f + new Vector3(17.1f, 3.3f, 8.9f); amp *= .5f; }
            return sum / norm;
        }

        public static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
    }

    /// <summary>Parametric surfaces for the prop library: a (U+1)×(V+1) grid P(u, v) with normals from the grid itself
    /// (Cross(∂P/∂u, ∂P/∂v) is the front side), optional wrap in u, optional snow shell resting on the upward faces.</summary>
    public static class Surf
    {
        public sealed class Options
        {
            public Matrix4x4 M = Matrix4x4.identity;
            public Vector2 UvScale = Vector2.one;
            public bool ClosedU;
            public bool Flip;
            public bool DoubleSided;
            /// <summary>Snow resting on this surface: builder (1 submesh used), depth in metres, seed.</summary>
            public MeshBuilder Snow; public float SnowDepth; public int SnowSeed;
            /// <summary>Fallback centre for degenerate normals (poles).</summary>
            public Vector3? Centre;
        }

        public static void Emit(MeshBuilder mb, int s, int U, int V, Func<float, float, Vector3> P, Options o)
        {
            int cols = U + 1, rows = V + 1;
            var pos = new Vector3[cols * rows];
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    float u = o.ClosedU && i == U ? 0f : i / (float)U;
                    pos[j * cols + i] = o.M.MultiplyPoint3x4(P(u, j / (float)V));
                }
            Vector3 centre = o.Centre.HasValue ? o.M.MultiplyPoint3x4(o.Centre.Value) : Average(pos);
            var nor = new Vector3[pos.Length];
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int iu0 = i - 1, iu1 = i + 1;
                    if (o.ClosedU) { if (iu0 < 0) iu0 = U - 1; if (iu1 > U) iu1 = 1; }
                    else { iu0 = Mathf.Max(0, iu0); iu1 = Mathf.Min(U, iu1); }
                    int jv0 = Mathf.Max(0, j - 1), jv1 = Mathf.Min(V, j + 1);
                    Vector3 du = pos[j * cols + iu1] - pos[j * cols + iu0];
                    Vector3 dv = pos[jv1 * cols + i] - pos[jv0 * cols + i];
                    // at a pole the u-derivative vanishes: borrow it from the neighbouring row
                    if (du.sqrMagnitude < 1e-10f)
                    {
                        int jj = j == 0 ? 1 : j - 1;
                        du = pos[jj * cols + iu1] - pos[jj * cols + iu0];
                    }
                    Vector3 n = Vector3.Cross(du, dv);
                    if (n.sqrMagnitude < 1e-12f) n = pos[j * cols + i] - centre;
                    n.Normalize();
                    if (o.Flip) n = -n;
                    nor[j * cols + i] = n;
                }
            Grid(mb, s, cols, rows, pos, nor, o, false);
            if (o.DoubleSided)
            {
                var back = new Vector3[nor.Length];
                for (int k = 0; k < nor.Length; k++) back[k] = -nor[k];
                Grid(mb, s, cols, rows, pos, back, o, true);
            }
            if (o.Snow != null && o.SnowDepth > 0f) SnowShell(o.Snow, cols, rows, pos, nor, o);
        }

        static Vector3 Average(Vector3[] p) { var c = Vector3.zero; foreach (var x in p) c += x; return c / Mathf.Max(1, p.Length); }

        static void Grid(MeshBuilder mb, int s, int cols, int rows, Vector3[] pos, Vector3[] nor, Options o, bool reversed)
        {
            int start = mb.VertexCount;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                    mb.Vert(pos[j * cols + i], nor[j * cols + i], new Vector2(i / (float)(cols - 1) * o.UvScale.x, j / (float)(rows - 1) * o.UvScale.y));
            bool flip = o.Flip ^ reversed;
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < cols - 1; i++)
                {
                    int a = start + j * cols + i, b = a + cols, c = a + 1, d = b + 1;
                    if (!flip) { mb.Tri(s, a, c, b); mb.Tri(s, c, d, b); }
                    else { mb.Tri(s, a, b, c); mb.Tri(s, c, b, d); }
                }
        }

        /// <summary>Second skin lifted off the upward-facing part of the surface; where the face turns sideways the skin sinks just under it, so the snow ends softly.</summary>
        static void SnowShell(MeshBuilder snow, int cols, int rows, Vector3[] pos, Vector3[] nor, Options o)
        {
            var sp = new Vector3[pos.Length]; var sn = new Vector3[pos.Length];
            for (int k = 0; k < pos.Length; k++)
            {
                var n = nor[k]; var p = pos[k];
                float cover = Noise.Smooth(.35f, .8f, n.y);
                float lumpy = .55f + .45f * Noise.Fbm(p * 6f + Vector3.one * o.SnowSeed, 2);
                float t = o.SnowDepth * cover * Mathf.Clamp01(lumpy);
                sp[k] = t > .002f ? p + (n * .35f + Vector3.up * .65f) * t : p - n * .004f;
                sn[k] = Vector3.Slerp(n, Vector3.up, .6f).normalized;
            }
            int start = snow.VertexCount;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    var p = sp[j * cols + i];
                    snow.Vert(p, sn[j * cols + i], new Vector2(p.x + p.y, p.z) * .8f);
                }
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < cols - 1; i++)
                {
                    int a = start + j * cols + i, b = a + cols, c = a + 1, d = b + 1;
                    if (!o.Flip) { snow.Tri(0, a, c, b); snow.Tri(0, c, d, b); }
                    else { snow.Tri(0, a, b, c); snow.Tri(0, c, b, d); }
                }
        }

        // ------------------------------------------------------------------ shapes built on Emit

        /// <summary>Irregular lump (snow, cloth heap, soft bag): a displaced ellipsoid dome, bottom skirt sunk into the ground.</summary>
        public static void Lump(MeshBuilder mb, int s, Vector3 c, Vector3 size, int seed, float rough = .22f, MeshBuilder snow = null, float snowDepth = 0f, bool full = false, Matrix4x4? m = null, float skirt = .35f)
        {
            var off = new Vector3(seed * 1.37f, seed * .71f, seed * 2.11f);
            float lean = (Noise.N(off) * .5f);
            Emit(mb, s, 20, full ? 14 : 8, (u, v) =>
            {
                float th = u * Mathf.PI * 2, ph = full ? (v - .5f) * Mathf.PI : v * Mathf.PI * .5f;
                var d = new Vector3(Mathf.Cos(th) * Mathf.Cos(ph), Mathf.Sin(ph), Mathf.Sin(th) * Mathf.Cos(ph));
                float k = 1f + rough * Noise.Fbm(d * 1.6f + off, 3) + rough * .35f * Noise.N(d * 5f + off);
                var p = new Vector3(d.x * size.x, d.y * size.y, d.z * size.z) * k;
                p.x += lean * p.y * .4f;
                if (!full && v <= 0f) p.y -= skirt;
                return c + p;
            }, new Options { M = m ?? Matrix4x4.identity, ClosedU = true, Flip = true, UvScale = new Vector2(size.x + size.z, size.y) * 2f, Centre = c, Snow = snow, SnowDepth = snowDepth, SnowSeed = seed });
        }

        /// <summary>Tube along a smooth path (Catmull-Rom through <paramref name="path"/>) with radius r(t, angle); parallel-transport frames; caps on submesh <paramref name="capSub"/>.</summary>
        public static void Sweep(MeshBuilder mb, int s, Vector3[] path, Func<float, float, float> radius, int sides, int segments, Options o,
            bool capStart = true, bool capEnd = true, int capSub = -1, Func<float, float, Vector2> crossSection = null)
        {
            var pts = new Vector3[segments + 1]; var tan = new Vector3[segments + 1]; var nrm = new Vector3[segments + 1]; var bin = new Vector3[segments + 1];
            for (int k = 0; k <= segments; k++) pts[k] = CatmullPath(path, k / (float)segments);
            for (int k = 0; k <= segments; k++) tan[k] = (pts[Mathf.Min(segments, k + 1)] - pts[Mathf.Max(0, k - 1)]).normalized;
            Vector3 refUp = Mathf.Abs(Vector3.Dot(tan[0], Vector3.up)) > .9f ? Vector3.right : Vector3.up;
            nrm[0] = Vector3.Cross(tan[0], refUp).normalized; bin[0] = Vector3.Cross(nrm[0], tan[0]);
            for (int k = 1; k <= segments; k++)
            {
                var q = Quaternion.FromToRotation(tan[k - 1], tan[k]);
                nrm[k] = (q * nrm[k - 1]).normalized; bin[k] = Vector3.Cross(nrm[k], tan[k]);
            }
            float length = 0; for (int k = 1; k <= segments; k++) length += (pts[k] - pts[k - 1]).magnitude;
            var so = new Options { M = o.M, ClosedU = true, UvScale = new Vector2(o.UvScale.x, o.UvScale.y * length), Flip = !o.Flip, Snow = o.Snow, SnowDepth = o.SnowDepth, SnowSeed = o.SnowSeed, Centre = null };
            Vector3 Ring(float u, int k)
            {
                float t = k / (float)segments, ang = u * Mathf.PI * 2;
                var cs = crossSection != null ? crossSection(t, ang) : new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float r = radius(t, ang);
                return pts[k] + (nrm[k] * cs.x + bin[k] * cs.y) * r;
            }
            Emit(mb, s, sides, segments, (u, v) => Ring(u, Mathf.RoundToInt(v * segments)), so);
            int cs0 = capSub < 0 ? s : capSub;
            if (capStart) Cap(mb, cs0, pts[0], -tan[0], sides, u => Ring(u, 0), o.M, radius(0, 0));
            if (capEnd) Cap(mb, cs0, pts[segments], tan[segments], sides, u => Ring(u, segments), o.M, radius(1, 0));
        }

        static void Cap(MeshBuilder mb, int s, Vector3 centre, Vector3 dir, int sides, Func<float, Vector3> ring, Matrix4x4 m, float r)
        {
            var c = m.MultiplyPoint3x4(centre); var n = m.MultiplyVector(dir).normalized;
            int ci = mb.Vert(c, n, new Vector2(.5f, .5f));
            var idx = new int[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                float u = i / (float)sides, a = u * Mathf.PI * 2;
                idx[i] = mb.Vert(m.MultiplyPoint3x4(ring(i == sides ? 0 : u)), n, new Vector2(.5f + .5f * Mathf.Cos(a), .5f + .5f * Mathf.Sin(a)));
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = m.MultiplyPoint3x4(ring(i / (float)sides)) - c, b = m.MultiplyPoint3x4(ring((i + 1) / (float)sides)) - c;
                if (Vector3.Dot(Vector3.Cross(a, b), n) > 0) mb.Tri(s, ci, idx[i], idx[i + 1]);
                else mb.Tri(s, ci, idx[i + 1], idx[i]);
            }
        }

        /// <summary>Piecewise-linear profile sampled by arc length (for <see cref="Lathe"/>).</summary>
        public static Func<float, Vector2> Poly(params Vector2[] pts)
        {
            var acc = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) acc[i] = acc[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            float total = acc[pts.Length - 1];
            return t =>
            {
                float d = Mathf.Clamp01(t) * total;
                for (int i = 1; i < pts.Length; i++)
                    if (d <= acc[i] || i == pts.Length - 1)
                    {
                        float seg = acc[i] - acc[i - 1];
                        return Vector2.Lerp(pts[i - 1], pts[i], seg < 1e-6f ? 0 : (d - acc[i - 1]) / seg);
                    }
                return pts[pts.Length - 1];
            };
        }

        public static void BoxM(MeshBuilder mb, int s, Matrix4x4 m, Vector3 center, Vector3 size, Quaternion rot, float uv = 1f)
        {
            var tmp = new MeshBuilder(s + 1);
            tmp.Box(s, center, size, rot, uv);
            mb.Append(tmp, m);
        }

        /// <summary>Closed polygon cap (convex-ish, fan from its centroid) facing <paramref name="normal"/> (local).</summary>
        public static void Fan(MeshBuilder mb, int s, Matrix4x4 m, Vector3[] ring, Vector3 normal, float uv = 20f)
        {
            var c = Vector3.zero; foreach (var p in ring) c += p; c /= ring.Length;
            var n = m.MultiplyVector(normal).normalized;
            var wc = m.MultiplyPoint3x4(c);
            int ci = mb.Vert(wc, n, new Vector2(c.x, c.z) * uv);
            var idx = new int[ring.Length];
            for (int i = 0; i < ring.Length; i++) idx[i] = mb.Vert(m.MultiplyPoint3x4(ring[i]), n, new Vector2(ring[i].x, ring[i].z) * uv);
            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                var a = m.MultiplyPoint3x4(ring[i]) - wc; var b = m.MultiplyPoint3x4(ring[j]) - wc;
                if (Vector3.Dot(Vector3.Cross(a, b), n) > 0) mb.Tri(s, ci, idx[i], idx[j]); else mb.Tri(s, ci, idx[j], idx[i]);
            }
        }

        public static Vector3 CatmullPath(Vector3[] p, float t)
        {
            if (p.Length == 1) return p[0];
            if (p.Length == 2) return Vector3.Lerp(p[0], p[1], t);
            float f = t * (p.Length - 1); int i = Mathf.Min(p.Length - 2, Mathf.FloorToInt(f)); float x = f - i;
            Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(p.Length - 1, i + 2)];
            return .5f * (2 * p1 + (-p0 + p2) * x + (2 * p0 - 5 * p1 + 4 * p2 - p3) * x * x + (-p0 + 3 * p1 - 3 * p2 + p3) * x * x * x);
        }

        /// <summary>Surface of revolution around +Y: profile(v) → (radius, height); dents from noise.</summary>
        public static void Lathe(MeshBuilder mb, int s, Func<float, Vector2> profile, int sides, int steps, Options o, float dents = 0f, int seed = 0)
        {
            Emit(mb, s, sides, steps, (u, v) =>
            {
                var rh = profile(v); float a = u * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float r = rh.x * (1f + dents * Noise.Fbm(d * 2f + new Vector3(seed, rh.y * 4f, 0), 2));
                return d * r + Vector3.up * rh.y;
            }, new Options { M = o.M, ClosedU = true, UvScale = o.UvScale, Flip = !o.Flip, Snow = o.Snow, SnowDepth = o.SnowDepth, SnowSeed = seed });
        }

        /// <summary>Flat 2-D outline (XY, counter-clockwise) extruded along Z with thickness(x, y); both faces plus rim.</summary>
        public static void Extrude(MeshBuilder mb, int s, Vector2[] outline, Func<Vector2, float> thickness, Matrix4x4 m)
        {
            var c = Vector2.zero; foreach (var p in outline) c += p; c /= outline.Length;
            foreach (int side in new[] { 1, -1 })
            {
                var n = m.MultiplyVector(Vector3.forward * side).normalized;
                int ci = mb.Vert(m.MultiplyPoint3x4(new Vector3(c.x, c.y, side * thickness(c) / 2)), n, c);
                var idx = new int[outline.Length];
                for (int i = 0; i < outline.Length; i++) idx[i] = mb.Vert(m.MultiplyPoint3x4(new Vector3(outline[i].x, outline[i].y, side * thickness(outline[i]) / 2)), n, outline[i] * 4f);
                for (int i = 0; i < outline.Length; i++)
                {
                    int a = idx[i], b = idx[(i + 1) % outline.Length];
                    if (side > 0) mb.Tri(s, ci, a, b); else mb.Tri(s, ci, b, a);
                }
            }
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                var e = (b - a).normalized; var out2 = new Vector2(e.y, -e.x);
                var n = m.MultiplyVector(new Vector3(out2.x, out2.y, 0)).normalized;
                int i0 = mb.Vert(m.MultiplyPoint3x4(new Vector3(a.x, a.y, thickness(a) / 2)), n, Vector2.zero);
                int i1 = mb.Vert(m.MultiplyPoint3x4(new Vector3(b.x, b.y, thickness(b) / 2)), n, Vector2.right);
                int i2 = mb.Vert(m.MultiplyPoint3x4(new Vector3(b.x, b.y, -thickness(b) / 2)), n, Vector2.one);
                int i3 = mb.Vert(m.MultiplyPoint3x4(new Vector3(a.x, a.y, -thickness(a) / 2)), n, Vector2.up);
                // face must point along n
                var fa = m.MultiplyPoint3x4(new Vector3(a.x, a.y, thickness(a) / 2)); var fb = m.MultiplyPoint3x4(new Vector3(b.x, b.y, thickness(b) / 2)); var fd = m.MultiplyPoint3x4(new Vector3(a.x, a.y, -thickness(a) / 2));
                if (Vector3.Dot(Vector3.Cross(fb - fa, fd - fa), n) > 0) { mb.Tri(s, i0, i1, i2); mb.Tri(s, i0, i2, i3); }
                else { mb.Tri(s, i0, i2, i1); mb.Tri(s, i0, i3, i2); }
            }
        }

        /// <summary>Flat ribbon (strap, belt) along a path, with a small thickness; normal = <paramref name="faceUp"/> side.</summary>
        public static void Ribbon(MeshBuilder mb, int s, Vector3[] path, float width, float thick, Vector3 faceUp, Matrix4x4 m, int segments = 12)
        {
            Emit(mb, s, 1, segments, (u, v) =>
            {
                var p = CatmullPath(path, v);
                var t = (CatmullPath(path, Mathf.Min(1, v + .01f)) - CatmullPath(path, Mathf.Max(0, v - .01f))).normalized;
                var side = Vector3.Cross(t, faceUp).normalized;
                return p + side * (u - .5f) * width + faceUp * thick * .5f;
            }, new Options { M = m, DoubleSided = true, UvScale = new Vector2(1, 4) });
        }
    }
}
