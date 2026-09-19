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
    /// a boot are the same operation with different outlines, and an outline is only a handful of control points with
    /// a spline run through them (<see cref="Spline"/>) — so the silhouette stays something a person can nudge by eye
    /// while the surface stays smooth.
    ///
    /// Clothing needs one thing more than a lathe gives: a sleeve is a tube that comes OUT of the shoulder, and a
    /// trouser leg comes out of the seat, which no shape spun round one axis can do. <see cref="Sweep"/> is the lathe
    /// with the axis let loose — a chain of rings, each with its own centre, direction and radius, sewn into one
    /// surface. Run the first rings inside the torso and the rest down the arm and the sleeve grows out of the body
    /// the way a sewn one does. Each ring also names which bones it follows (<see cref="Station.Weight"/>), so the
    /// same surface can be skinned: vertices near a joint are pulled by both bones and the cloth bends instead of
    /// breaking. That is how every game does clothing, and it costs nothing here that the lathe did not already pay.
    ///
    /// Two texture coordinates come out of the sweep for free. The first (UV0) picks a patch of the shared atlas: u
    /// runs around the shape, v along it measured in metres of arc, so a band painted on the texture keeps its real
    /// width on the body — a 3 cm cuff is 3 cm of cuff. The second (UV1) is the same pair in plain metres, wrapped a
    /// whole number of times round the ring; the fabric weave is tiled through it, so a stitch is the same size on a
    /// sleeve as on a trouser leg and the seam never shows. u = 0.5 always looks along +Z, which is how a face
    /// painted in the middle of a texture tile lands on the front of the head instead of behind the ear.</summary>
    public sealed class PuppetMesh
    {
        /// <summary>One ring of a <see cref="Sweep"/>: where it is, which way the tube is going there, how wide it is,
        /// and which bones it belongs to.</summary>
        public struct Station
        {
            public Vector3 Centre;
            /// <summary>The direction of travel — the ring lies in the plane at right angles to it.</summary>
            public Vector3 Axis;
            public float Radius;
            /// <summary>Squashes the ring into an ellipse: x across, y along the ring's own "forward".</summary>
            public Vector2 Squash;
            /// <summary>Metres of rounding on the corners of a rectangular ring. Zero — the default — keeps the ring
            /// an ellipse; anything above turns it into a rounded rectangle with half-sides Radius·Squash and corners
            /// of this radius (capped at the shorter half-side, so a small ring near a pole is simply round). It is
            /// what a pack is made of: a lathe only ever turns things that are round, and a rucksack is a box.</summary>
            public float Corner;
            public BoneWeight Weight;
        }

        /// <summary>Metres per repeat of the fabric weave tiled through UV1.</summary>
        public const float DetailTile = .04f;

        readonly List<Vector3> pos = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector2> uv1 = new List<Vector2>();
        readonly List<BoneWeight> weight = new List<BoneWeight>();
        readonly List<int> tri = new List<int>();

        public int VertexCount => pos.Count;
        public int TriangleCount => tri.Count / 3;

        public static BoneWeight Rigid(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        /// <summary>Two bones sharing a vertex: <paramref name="b"/> gets <paramref name="wb"/> of it.</summary>
        public static BoneWeight Blend(int a, int b, float wb)
        {
            wb = Mathf.Clamp01(wb);
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - wb, boneIndex1 = b, weight1 = wb };
        }

        public static BoneWeight Blend(int a, float wa, int b, float wb, int c, float wc)
        {
            float sum = Mathf.Max(wa + wb + wc, 1e-6f);
            return new BoneWeight { boneIndex0 = a, weight0 = wa / sum, boneIndex1 = b, weight1 = wb / sum, boneIndex2 = c, weight2 = wc / sum };
        }

        public int Vert(Vector3 p, Vector3 n, Vector2 t) => Vert(p, n, t, Vector2.zero, Rigid(0));

        public int Vert(Vector3 p, Vector3 n, Vector2 t, Vector2 t1, BoneWeight w)
        {
            pos.Add(p); nrm.Add(n); uv.Add(t); uv1.Add(t1); weight.Add(w);
            return pos.Count - 1;
        }

        /// <summary>Quad a-b-c-d in order; the face looks along Cross(b - a, d - a) — the same convention the world's
        /// MeshBuilder uses, so a patch wound the wrong way vanishes outright instead of going subtly wrong.</summary>
        public void Quad(int a, int b, int c, int d) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(d); }

        /// <summary>Copies another piece in, moved by <paramref name="m"/>. A mirrored matrix turns a surface inside
        /// out, so the winding is flipped back — that is how the left hand's fingers are made out of the right one's.</summary>
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
                uv1.Add(other.uv1[i]);
                weight.Add(other.weight[i]);
            }
            for (int i = 0; i + 2 < other.tri.Count; i += 3)
            {
                tri.Add(other.tri[i] + off);
                tri.Add(other.tri[flip ? i + 2 : i + 1] + off);
                tri.Add(other.tri[flip ? i + 1 : i + 2] + off);
            }
        }

        /// <summary>The meshes are built at run time and belong to no scene, so they carry HideAndDontSave: without it
        /// Unity offers to save them into the sandbox scene and then throws them away on the next load.
        /// With <paramref name="bindposes"/> the mesh is a skinned one: the weights every ring was given are written
        /// out, and the caller hands it to a SkinnedMeshRenderer whose bones are in the same order.</summary>
        public Mesh ToMesh(string name, Matrix4x4[] bindposes = null)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(pos); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetUVs(1, uv1);
            if (bindposes != null) { m.boneWeights = weight.ToArray(); m.bindposes = bindposes; }
            m.SetTriangles(tri, 0, true);
            return m;
        }

        public void Clear() { pos.Clear(); nrm.Clear(); uv.Clear(); uv1.Clear(); weight.Clear(); tri.Clear(); }

        /// <summary>Writes the lists into a mesh that already exists — the way a part rebuilt every frame (an arm)
        /// gets to the screen without a new Mesh object each time. Only cleared first when the vertex count changed:
        /// the old index list would point past the new vertices for the moment between the two calls.</summary>
        public void Fill(Mesh m)
        {
            if (m.vertexCount != pos.Count) m.Clear(false);
            m.SetVertices(pos); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetUVs(1, uv1);
            m.SetTriangles(tri, 0, true);
        }

        /// <summary>One cross-section of a hand-placed sweep (<see cref="Sweep(List{Section}, Quaternion, int, Rect)"/>):
        /// where its centre is, which way the tube is travelling there, how wide it is and how far from round (x and
        /// z of the ring's own frame, 1 = a circle). The skinned cousin is <see cref="Station"/>.</summary>
        public struct Section
        {
            public Vector3 Centre, Tangent;
            public float Radius;
            public Vector2 Squash;
            public Section(Vector3 centre, Vector3 tangent, float radius) : this(centre, tangent, radius, Vector2.one) { }
            public Section(Vector3 centre, Vector3 tangent, float radius, Vector2 squash)
            { Centre = centre; Tangent = tangent; Radius = radius; Squash = squash; }
        }

        /// <summary>A lathe bent along a path — a hose — for a part placed by hand every frame rather than skinned.
        /// <see cref="Revolve"/> spins an outline round a straight axis; this sews the same rings along any line of
        /// centres, so one surface can run from the shoulder round the elbow and out into the palm with no joint in
        /// it anywhere (<see cref="PuppetArm"/>).
        ///
        /// <paramref name="frame"/> is the rotation of the piece the tube ends in (a hand): its right and forward are
        /// the x and z of every ring, turned by the least rotation that takes the frame's <b>down</b> onto the ring's
        /// tangent — a sweep travels along −y of its frame, the way fingers point away from the wrist. Every ring's
        /// frame is found from that one, never from the ring before it, so nothing accumulates and a tube that is
        /// straight one frame and bent the next does not twist. The winding runs the other way from Revolve's for the
        /// same reason: there the outline climbs +y, here it descends.
        ///
        /// A ring of radius 0 is a pole: put one at the end and the tube closes in a dome, the normal computed from
        /// the outline exactly as on a lathe.
        ///
        /// v normally runs 0…1 from the first ring to the last. With <paramref name="fromEnd"/> set it is laid out
        /// in metres instead, counted back from the END of the tube: v = 1 at the last ring and one tile of
        /// <paramref name="fromEnd"/> metres reaching back from it. That is for a part whose far end is the fixed one
        /// — a hand on an arm whose elbow bends and whose shoulder is buried: measured from the fingertips, a cuff
        /// painted at the wrist stays on the wrist whatever the arm is doing. The weave's coordinate (UV1) is laid
        /// out in metres as on the lathe, so the cloth of a sleeve matches the cloth of the jacket it comes out of.</summary>
        public void Sweep(List<Section> rings, Quaternion frame, int sides, Rect uvRect, float fromEnd = 0f)
        {
            int n = rings == null ? 0 : rings.Count;
            if (n < 2 || sides < 3) return;

            var along = new float[n];
            float total = 0f, rMean = 0f;
            for (int i = 1; i < n; i++) { total += (rings[i].Centre - rings[i - 1].Centre).magnitude; along[i] = total; }
            for (int i = 0; i < n; i++) rMean += rings[i].Radius * (rings[i].Squash.x + rings[i].Squash.y) * .5f;
            rMean /= n;
            int reps = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * rMean / DetailTile));
            var metres = (float[])along.Clone();
            var flat = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                int lo = Mathf.Max(i - 1, 0), hi = Mathf.Min(i + 1, n - 1);
                var tan = new Vector2(rings[hi].Radius - rings[lo].Radius, along[hi] - along[lo]);
                if (tan.sqrMagnitude < 1e-12f) tan = Vector2.up;
                flat[i] = new Vector2(tan.y, -tan.x).normalized;
            }
            if (fromEnd > 1e-6f) for (int i = 0; i < n; i++) along[i] = Mathf.Clamp01(1f - (total - along[i]) / fromEnd);
            else if (total > 1e-6f) for (int i = 0; i < n; i++) along[i] /= total;

            var travel = frame * Vector3.down;
            int start = pos.Count, ring = sides + 1;
            for (int i = 0; i < n; i++)
            {
                var r = rings[i];
                var t = r.Tangent.sqrMagnitude > 1e-12f ? r.Tangent.normalized : travel;
                var q = Quaternion.FromToRotation(travel, t) * frame;
                Vector3 x = q * Vector3.right, z = q * Vector3.forward;
                float sx = Mathf.Max(r.Squash.x, 1e-3f), sz = Mathf.Max(r.Squash.y, 1e-3f);
                for (int j = 0; j <= sides; j++)
                {
                    float u = j / (float)sides;
                    float a = (u - .5f) * Mathf.PI * 2f + Mathf.PI * .5f;   // u = 0.5 looks along the frame's +Z
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    var p = r.Centre + x * (r.Radius * c * sx) + z * (r.Radius * s * sz);
                    var nv = x * (flat[i].x * c / sx) + z * (flat[i].x * s / sz) + t * flat[i].y;
                    Vert(p, nv.sqrMagnitude > 1e-12f ? nv.normalized : t,
                         new Vector2(Mathf.Lerp(uvRect.xMin, uvRect.xMax, u), Mathf.Lerp(uvRect.yMin, uvRect.yMax, along[i])),
                         new Vector2(u * reps, metres[i] / DetailTile), Rigid(0));
                }
            }
            for (int i = 0; i + 1 < n; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = start + i * ring + j;
                    Quad(a0, a0 + 1, a0 + ring + 1, a0 + ring);
                }
        }

        /// <summary>A short curved tube, open where it starts and rounded where it ends — a finger. The curve is a
        /// quadratic Bézier: it leaves <paramref name="from"/> towards <paramref name="via"/> and arrives at
        /// <paramref name="to"/> from it, which is exactly the curl of a relaxed finger. The open start is meant to
        /// be buried in a palm, where the tube simply comes out of the bigger surface.</summary>
        public void Digit(Vector3 from, Vector3 via, Vector3 to, float rFrom, float rTo, int steps, int sides, Rect uvRect)
        {
            steps = Mathf.Max(2, steps);
            var rings = new List<Section>(steps + 5);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                rings.Add(new Section(Bezier(from, via, to, t), BezierTangent(from, via, to, t), Mathf.Lerp(rFrom, rTo, t)));
            }
            var tip = BezierTangent(from, via, to, 1f);
            for (int i = 1; i <= 4; i++)
            {
                float a = Mathf.PI * .5f * i / 4;
                rings.Add(new Section(to + tip * (rTo * Mathf.Sin(a)), tip, rTo * Mathf.Cos(a)));
            }
            Sweep(rings, Quaternion.identity, sides, uvRect);
        }

        public static Vector3 Bezier(Vector3 a, Vector3 ctrl, Vector3 b, float t)
        {
            float s = 1f - t;
            return a * (s * s) + ctrl * (2f * s * t) + b * (t * t);
        }

        /// <summary>Direction of travel along the same curve, unit length; falls back to the chord where the curve
        /// has no direction (all three points in one place).</summary>
        public static Vector3 BezierTangent(Vector3 a, Vector3 ctrl, Vector3 b, float t)
        {
            var d = (ctrl - a) * (2f * (1f - t)) + (b - ctrl) * (2f * t);
            if (d.sqrMagnitude < 1e-12f) d = b - a;
            return d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.down;
        }

        /// <summary>Spins <paramref name="outline"/> — points of (radius, height) — around the Y axis. The outline may
        /// run either way: the faces are turned outwards whichever way it goes. <paramref name="xz"/> squashes the
        /// circle into an ellipse (a torso is not round, a foot is long), <paramref name="uvRect"/> is the patch of
        /// the shared texture this piece is painted from, and <paramref name="weightAt"/>, given a point of the
        /// outline, says which bones that ring follows — left out, the piece is rigid on bone 0.</summary>
        public void Revolve(Vector2[] outline, int sides, Vector2 xz, Rect uvRect,
                            System.Func<Vector2, BoneWeight> weightAt = null, float weave = 1f)
        {
            int n = outline == null ? 0 : outline.Length;
            if (n < 2) return;
            var st = new Station[n];
            // one frame for every ring, whichever way the outline travels: a closed loop (the hat's brim) goes up one
            // side and down the other, and rings framed by their own direction would twist against each other
            for (int i = 0; i < n; i++)
            {
                st[i] = new Station
                {
                    Centre = new Vector3(0f, outline[i].y, 0f),
                    Axis = Vector3.up,
                    Radius = Mathf.Max(0f, outline[i].x),
                    Squash = xz,
                    Weight = weightAt != null ? weightAt(outline[i]) : Rigid(0),
                };
            }
            Sweep(st, sides, uvRect, weave);
        }

        /// <summary>A tube along a chain of rings. Every ring is sampled at <paramref name="sides"/> steps and sewn to
        /// the next; a ring of zero radius closes the tube as a pole. Normals come off the surface itself — the
        /// cross of "along the tube" and "around it" at each vertex — so a bend, a flare and an inward-turned hem all
        /// shade right without a smoothing pass, and the seam ring is duplicated so u can reach 1. The faces are
        /// turned outwards whichever way the chain runs.</summary>
        public void Sweep(Station[] st, int sides, Rect uvRect, float weave = 1f)
        {
            int n = st == null ? 0 : st.Length;
            if (n < 2 || sides < 3) return;
            int ring = sides + 1;

            // v in metres of tube, then normalised: this is what keeps painted bands the right width on the body.
            // A change of radius counts as length too, so a hem turned inwards still travels down the texture.
            var along = new float[n];
            float total = 0f, rMean = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    var d = st[i].Centre - st[i - 1].Centre;
                    total += Mathf.Sqrt(d.sqrMagnitude + Mathf.Pow(st[i].Radius - st[i - 1].Radius, 2f));
                }
                along[i] = total;
                rMean += st[i].Radius * (Mathf.Abs(st[i].Squash.x) + Mathf.Abs(st[i].Squash.y)) * .5f;
            }
            rMean /= n;
            // the weave goes round a whole number of times, so its seam meets itself at the back
            int reps = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * rMean * weave / DetailTile));

            var p = new Vector3[n * ring];
            for (int i = 0; i < n; i++)
            {
                var axis = st[i].Axis.sqrMagnitude > 1e-12f ? st[i].Axis.normalized : Vector3.up;
                // the ring's own frame: "forward" is as much of +Z as is at right angles to the axis, so u = 0.5
                // looks the same way on every ring and a texture drawn on the front stays on the front
                var fwd = Vector3.ProjectOnPlane(Vector3.forward, axis);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.up, axis);
                fwd.Normalize();
                var side = Vector3.Cross(axis, fwd);
                float sx = Mathf.Max(Mathf.Abs(st[i].Squash.x), 1e-3f), sz = Mathf.Max(Mathf.Abs(st[i].Squash.y), 1e-3f);
                for (int j = 0; j <= sides; j++)
                {
                    float u = j / (float)sides;
                    Vector2 q;
                    if (st[i].Corner > 0f) q = RoundedRect(u, st[i].Radius * sx, st[i].Radius * sz, st[i].Corner);
                    else
                    {
                        float a = (u - .5f) * Mathf.PI * 2f + Mathf.PI * .5f;
                        q = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a) * sz) * st[i].Radius;
                    }
                    p[i * ring + j] = st[i].Centre + q.x * side + q.y * fwd;
                }
            }

            // Which way round the surface is: summed over every ring, "along × around" says whether the surface
            // faces out of the tube or into it, and the winding follows. An outline run top to bottom or a tube
            // swept downwards therefore comes out the same as one run upwards. The whole surface is asked and not
            // the first ring, because a piece may well START on its own underside — the knitted hat begins inside
            // the head and runs out under its fold before it turns up — and on that first patch alone the two
            // faces are indistinguishable from the axis; over the whole piece the outside always wins.
            float sign = 1f, dot = 0f;
            for (int i = 0; i < n; i++)
            {
                if (st[i].Radius < 1e-4f) continue;
                for (int j = 0; j < sides; j++)
                {
                    var alongV = p[Mathf.Min(i + 1, n - 1) * ring + j] - p[Mathf.Max(i - 1, 0) * ring + j];
                    var around = p[i * ring + (j + 1) % sides] - p[i * ring + (j + sides - 1) % sides];
                    dot += Vector3.Dot(Vector3.Cross(alongV, around), p[i * ring + j] - st[i].Centre);
                }
            }
            if (dot < 0f) sign = -1f;

            int start = pos.Count;
            for (int i = 0; i < n; i++)
            {
                float v = total > 1e-6f ? along[i] / total : 0f;
                var travel = st[Mathf.Min(i + 1, n - 1)].Centre - st[Mathf.Max(i - 1, 0)].Centre;
                if (travel.sqrMagnitude < 1e-12f) travel = st[i].Axis;
                travel = travel.sqrMagnitude > 1e-12f ? travel.normalized : Vector3.up;
                for (int j = 0; j <= sides; j++)
                {
                    float u = j / (float)sides;
                    Vector3 normal;
                    if (st[i].Radius < 1e-4f)
                        normal = i == 0 ? -travel : travel;                       // a pole faces away from the tube
                    else
                    {
                        var alongV = p[Mathf.Min(i + 1, n - 1) * ring + j] - p[Mathf.Max(i - 1, 0) * ring + j];
                        var around = p[i * ring + (j + 1) % sides] - p[i * ring + (j + sides - 1) % sides];
                        normal = Vector3.Cross(alongV, around) * sign;
                        if (normal.sqrMagnitude < 1e-12f) normal = p[i * ring + j] - st[i].Centre;
                        normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : travel;
                    }
                    Vert(p[i * ring + j], normal,
                         new Vector2(Mathf.Lerp(uvRect.xMin, uvRect.xMax, u), Mathf.Lerp(uvRect.yMin, uvRect.yMax, v)),
                         new Vector2(u * reps, along[i] * weave / DetailTile),
                         st[i].Weight);
                }
            }
            for (int i = 0; i + 1 < n; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = start + i * ring + j;
                    if (sign > 0f) Quad(a0, a0 + ring, a0 + ring + 1, a0 + 1);
                    else Quad(a0, a0 + 1, a0 + ring + 1, a0 + ring);
                }
        }

        /// <summary>A point on a rounded rectangle, walked by arc length: <paramref name="u"/> = 0.5 is the middle of
        /// the +Z side (where the ellipse has its u = 0.5 too), and the walk goes the same way round as the ellipse,
        /// so a rectangular ring can be sewn to a round one. Even in arc length rather than in angle, so a strap
        /// painted three centimetres wide is three centimetres wide on a side and on a corner alike.</summary>
        public static Vector2 RoundedRect(float u, float hx, float hz, float rc)
        {
            hx = Mathf.Max(hx, 1e-4f); hz = Mathf.Max(hz, 1e-4f);
            rc = Mathf.Clamp(rc, 0f, Mathf.Min(hx, hz));
            float ex = hx - rc, ez = hz - rc, qc = Mathf.PI * .5f * rc;
            float per = 4f * (ex + ez) + 4f * qc;
            float s = (u - .5f) - Mathf.Floor(u - .5f);                 // 0 at the front, ½ at the back seam
            s *= per;
            // the walk: half the front side toward −x, then round the four corners and back
            if (s < ex) return new Vector2(-s, hz);
            s -= ex;
            if (s < qc) return Turn(-ex, ez, rc, Mathf.PI * .5f + s / rc);
            s -= qc;
            if (s < 2f * ez) return new Vector2(-hx, ez - s);
            s -= 2f * ez;
            if (s < qc) return Turn(-ex, -ez, rc, Mathf.PI + s / rc);
            s -= qc;
            if (s < 2f * ex) return new Vector2(-ex + s, -hz);
            s -= 2f * ex;
            if (s < qc) return Turn(ex, -ez, rc, Mathf.PI * 1.5f + s / rc);
            s -= qc;
            if (s < 2f * ez) return new Vector2(hx, -ez + s);
            s -= 2f * ez;
            if (s < qc) return Turn(ex, ez, rc, s / rc);
            s -= qc;
            return new Vector2(Mathf.Max(ex - s, 0f), hz);
        }

        static Vector2 Turn(float cx, float cz, float r, float a) => new Vector2(cx + r * Mathf.Cos(a), cz + r * Mathf.Sin(a));

        /// <summary>Metres of tube along a chain of stations, the same measure <see cref="Sweep"/> lays v out by.</summary>
        public static float Arc(Station[] st, int upTo = int.MaxValue)
        {
            float total = 0f;
            for (int i = 1; i < st.Length && i <= upTo; i++)
            {
                var d = st[i].Centre - st[i - 1].Centre;
                total += Mathf.Sqrt(d.sqrMagnitude + Mathf.Pow(st[i].Radius - st[i - 1].Radius, 2f));
            }
            return total;
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
