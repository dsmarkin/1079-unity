using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>A lathe — and the whole answer to "where does an arbitrary shape come from without a 3D editor".
    ///
    /// A mesh in Unity is four plain lists and nothing else: points in space, triangles that join them by index, one
    /// normal per point (which way the surface faces, so light lands correctly) and one UV per point (where that point
    /// sits on the texture). Hand them to <see cref="Mesh"/> and it draws. No file, no importer, no modelling package
    /// — which is why the sandbox can build a body while the game runs and throw it away again.
    ///
    /// Writing thousands of points by hand is hopeless, so every part of the climber is turned the way a wooden toy is:
    /// an outline is drawn on paper — a flat curve of (radius, height) — and spun around the vertical axis.
    /// <see cref="Revolve"/> samples that spin at `sides` steps and sews neighbouring rings into quads. A head, a hat,
    /// a thigh and a boot are the same operation with different outlines, and an outline is only a handful of control
    /// points with a spline run through them (<see cref="Spline"/>) — so the silhouette stays something a person can
    /// nudge by eye while the surface stays smooth.
    ///
    /// The UV falls out of the sweep for free: u runs around the shape, v runs along the outline measured in metres of
    /// arc, so a band painted on the texture keeps its real width on the body — a 6 cm stripe is 6 cm of sock. And
    /// u = 0.5 always looks along +Z, which is how a face painted in the middle of a texture tile lands on the front of
    /// the head instead of behind the ear.</summary>
    public sealed class PuppetMesh
    {
        readonly List<Vector3> pos = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();

        public int VertexCount => pos.Count;
        public int TriangleCount => tri.Count / 3;

        public int Vert(Vector3 p, Vector3 n, Vector2 t) { pos.Add(p); nrm.Add(n); uv.Add(t); return pos.Count - 1; }

        /// <summary>Quad a-b-c-d in order; the face looks along Cross(b - a, d - a) — the same convention the world's
        /// MeshBuilder uses, so a patch wound the wrong way vanishes outright instead of going subtly wrong.</summary>
        public void Quad(int a, int b, int c, int d) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d); }

        /// <summary>Copies another piece in, moved by <paramref name="m"/>. A mirrored matrix turns a surface inside
        /// out, so the winding is flipped back — that is how the left hand is made out of the right one.</summary>
        public void Append(PuppetMesh other, Matrix4x4 m)
        {
            if (other == null || other == this) return;
            int off = pos.Count;
            bool flip = m.determinant < 0f;
            for (int i = 0; i < other.pos.Count; i++)
            {
                pos.Add(m.MultiplyPoint3x4(other.pos[i]));
                var n = m.MultiplyVector(other.nrm[i]);
                nrm.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
                uv.Add(other.uv[i]);
            }
            for (int i = 0; i + 2 < other.tri.Count; i += 3)
            {
                tri.Add(other.tri[i] + off);
                tri.Add(other.tri[flip ? i + 2 : i + 1] + off);
                tri.Add(other.tri[flip ? i + 1 : i + 2] + off);
            }
        }

        /// <summary>The meshes are built at run time and belong to no scene, so they carry HideAndDontSave: without it
        /// Unity offers to save them into the sandbox scene and then throws them away on the next load.</summary>
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(pos); m.SetNormals(nrm); m.SetUVs(0, uv);
            m.SetTriangles(tri, 0, true);
            return m;
        }

        /// <summary>Spins <paramref name="outline"/> — points of (radius, height), ordered bottom to top, which is what
        /// makes the faces point outwards — around the Y axis. <paramref name="xz"/> squashes the circle into an
        /// ellipse (a torso is not round, a foot is long), and <paramref name="uvRect"/> is the patch of the shared
        /// texture this piece is painted from.</summary>
        public void Revolve(Vector2[] outline, int sides, Vector2 xz, Rect uvRect)
        {
            int n = outline == null ? 0 : outline.Length;
            if (n < 2 || sides < 3) return;
            xz = new Vector2(Mathf.Max(xz.x, 1e-3f), Mathf.Max(xz.y, 1e-3f));

            // v in metres of outline, then normalised — this is what keeps painted bands the right width on the body
            var along = new float[n];
            float total = 0f;
            for (int i = 1; i < n; i++) { total += (outline[i] - outline[i - 1]).magnitude; along[i] = total; }
            if (total > 1e-6f) for (int i = 0; i < n; i++) along[i] /= total;

            // the outward normal of a lathe is exact: turn the outline's tangent a quarter turn. No smoothing pass,
            // no seams between separately generated pieces, and a pole closes as a dome rather than a spike.
            var flat = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var tan = outline[Mathf.Min(i + 1, n - 1)] - outline[Mathf.Max(i - 1, 0)];
                if (tan.sqrMagnitude < 1e-12f) tan = Vector2.up;
                flat[i] = new Vector2(tan.y, -tan.x).normalized;
            }

            int start = pos.Count, ring = sides + 1;    // the seam ring is duplicated so u can actually reach 1
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= sides; j++)
                {
                    float u = j / (float)sides;
                    float a = (u - .5f) * Mathf.PI * 2f + Mathf.PI * .5f;   // u = 0.5 looks along +Z: the face goes there
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    var p = new Vector3(outline[i].x * c * xz.x, outline[i].y, outline[i].x * s * xz.y);
                    // a non-uniform squash tilts normals the other way round, hence the divide
                    var nv = new Vector3(flat[i].x * c / xz.x, flat[i].y, flat[i].x * s / xz.y);
                    Vert(p, nv.sqrMagnitude > 1e-12f ? nv.normalized : Vector3.up,
                         new Vector2(Mathf.Lerp(uvRect.xMin, uvRect.xMax, u), Mathf.Lerp(uvRect.yMin, uvRect.yMax, along[i])));
                }
            for (int i = 0; i + 1 < n; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = start + i * ring + j;
                    Quad(a0, a0 + ring, a0 + ring + 1, a0 + 1);
                }
        }

        // ── outlines ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Catmull-Rom through the control points: the silhouette stays a short list a person can read and
        /// move, the surface comes out smooth. The ends are doubled so the curve starts and finishes exactly on the
        /// first and last point, and a radius is never allowed below zero (the spline can overshoot).</summary>
        public static Vector2[] Spline(Vector2[] keys, int perSegment)
        {
            if (keys == null || keys.Length < 2) return keys;
            perSegment = Mathf.Max(1, perSegment);
            var outp = new List<Vector2>((keys.Length - 1) * perSegment + 1);
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                Vector2 p0 = keys[Mathf.Max(i - 1, 0)], p1 = keys[i], p2 = keys[i + 1], p3 = keys[Mathf.Min(i + 2, keys.Length - 1)];
                for (int k = 0; k < perSegment; k++)
                {
                    float t = k / (float)perSegment, t2 = t * t, t3 = t2 * t;
                    var p = .5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    if (p.x < 0f) p.x = 0f;
                    outp.Add(p);
                }
            }
            outp.Add(keys[keys.Length - 1]);
            return outp.ToArray();
        }

        /// <summary>A limb: a shaft tapering from <paramref name="rA"/> to <paramref name="rB"/>, capped with a
        /// hemisphere centred exactly on the joint. That is the whole trick behind "no elbow, no knee": a capped bone
        /// is a capsule, and a capsule contains a complete ball of its own radius around each of its ends, so the next
        /// bone can start anywhere inside that ball and the two read as one surface at any bend.
        ///
        /// Which is why <paramref name="openStart"/> exists. The second bone of a chain — a shin, a forearm — must NOT
        /// cap its near end: that cap would be a second surface on the very same sphere the first bone already drew,
        /// the two would be equally far from the camera everywhere they touch, and the joint would crawl with speckle.
        /// Left open and started a shade thinner, that end sits inside the first bone's ball where nothing can see it.
        ///
        /// <paramref name="swell"/> fattens the middle, which is what makes a thigh look soft instead of machined.</summary>
        public static Vector2[] Bone(float len, float rA, float rB, float swell = 0f, int shaft = 8, int cap = 5, bool openStart = false)
        {
            shaft = Mathf.Max(1, shaft); cap = Mathf.Max(1, cap);
            var o = new List<Vector2>(cap * 2 + shaft + 2);
            if (openStart) o.Add(new Vector2(rA * .96f, 0f));
            else
                for (int i = 0; i <= cap; i++)
                {
                    float a = -Mathf.PI * .5f + Mathf.PI * .5f * i / cap;
                    o.Add(new Vector2(rA * Mathf.Cos(a), rA * Mathf.Sin(a)));
                }
            for (int i = 1; i <= shaft; i++)
            {
                float t = i / (float)shaft;
                o.Add(new Vector2(Mathf.Lerp(rA, rB, Mathf.SmoothStep(0f, 1f, t)) * (1f + swell * Mathf.Sin(t * Mathf.PI)), len * t));
            }
            for (int i = 1; i <= cap; i++)
            {
                float a = Mathf.PI * .5f * i / cap;
                o.Add(new Vector2(rB * Mathf.Cos(a), len + rB * Mathf.Sin(a)));
            }
            return o.ToArray();
        }

        /// <summary>The head: a sphere pulled into a drop. r = R·sinθ·(1 − droop·cosθ) narrows the crown and widens the
        /// jaw with no kink anywhere on the curve, so the widest point sits below the middle. This one formula is the
        /// difference between a ball on sticks and a PEAK head.</summary>
        public static Vector2[] Drop(float height, float radius, float droop, int seg = 20)
        {
            seg = Mathf.Max(3, seg);
            var o = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * (1f - i / (float)seg);    // π at the bottom, 0 at the top: the outline runs upwards
                o[i] = new Vector2(Mathf.Max(0f, radius * Mathf.Sin(th) * (1f - droop * Mathf.Cos(th))), height * .5f * Mathf.Cos(th));
            }
            return o;
        }

        /// <summary>A plain rounded lump — the pack, a palm, the parts of a boot.</summary>
        public static Vector2[] Blob(float height, float radius, int seg = 14) => Drop(height, radius, 0f, seg);

        /// <summary>A closed loop spun into a ring: the brim of the hat. The loop runs outer edge → top → inner edge →
        /// bottom, which keeps the faces outwards for the same reason an outline has to run upwards.</summary>
        public static Vector2[] Ring(float radius, float tube, float flat, int seg = 12)
        {
            seg = Mathf.Max(4, seg);
            var o = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                o[i] = new Vector2(Mathf.Max(0f, radius + tube * Mathf.Cos(a)), tube * flat * Mathf.Sin(a));
            }
            return o;
        }
    }
}
