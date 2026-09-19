using System.Collections.Generic;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>The snow the range lies in, and the daylight mountain around it.
    ///
    /// The yard the measured stands sit on stays a flat plate on purpose — the self-test reads support height and
    /// slope off it, and a ruler that rolls is not a ruler. Everything that has to feel like ground instead of a floor
    /// is built here, north of the yard: a field that rolls underfoot, drifts deep enough to swallow a boot, a flank
    /// that gets steeper the higher it is walked, and a ditch cut into the ground.
    ///
    /// The field is two meshes off one heightfield, and the difference between them is the whole trick: the one you
    /// see stands at <see cref="Height"/>, the one the body walks on stands at <see cref="Height"/> minus
    /// <see cref="Sink"/>. The feet are therefore planted below the visible surface, the drifts swallow them, and the
    /// legs never have to learn what snow is. The camera reads the same <see cref="Sink"/> to drop the head and
    /// deepen the gait, which is how the game does it with the depth of the trail.</summary>
    public static class SandboxTerrainSnow
    {
        // The field: north of the yard (the yard is x ±100, z ±30, top at y = 0) and flush with it along the seam.
        public const float MinX = -52f, MaxX = 58f, MinZ = 30f, MaxZ = 96f;
        const float Cell = 1f;
        /// <summary>How far the rim of the field is smoothed back to the yard's level. Nothing interesting is built
        /// within this distance of an edge, so it only ever flattens the rolling.</summary>
        const float Rim = 7f;

        // ── the three things the yard cannot show ────────────────────────────────────────────────────────────────
        /// <summary>Drifts: x, z, radius, height. Heights run from ankle-deep to over the waist so one walk across
        /// them shows the whole range of what deep snow does to a stride.</summary>
        static readonly Vector4[] Mounds =
        {
            new Vector4(-40f, 40f, 4.0f, .45f),
            new Vector4(-33f, 47f, 4.6f, .80f),
            new Vector4(-25f, 41f, 5.2f, 1.15f),
            new Vector4(-18f, 49f, 5.8f, 1.55f),
            new Vector4(-31f, 57f, 6.5f, .95f),
        };

        // The flank: a hill whose front gets steeper the higher you are on it — flat at the foot, 30° half way,
        // about 48° in the last few metres, which is exactly where the boots give up. Its sides are steeper still and
        // are meant to be: a slide has to be somewhere. The back is short, for coming down.
        const float FlankX = 10f, FlankFoot = 36f, FlankCrest = 72f, FlankBack = 96f, FlankTop = 15f;

        // The ditch: a trench across the field, chest deep, with sides at about 36° and easier ends — enough that
        // climbing out of it is a decision and not a step.
        const float DitchX = 43f, DitchZ = 45f, DitchDepth = 1.5f;

        /// <summary>Where a body should be dropped to start walking into the drifts / up the flank / into the ditch.
        /// Read by the range for its stands and by the shot script for its framing.</summary>
        public static Vector3 DriftFoot => Spawn(-33f, 34f);
        public static Vector3 FlankFootAt => Spawn(FlankX, FlankFoot - 2f);
        public static Vector3 DitchFoot => Spawn(DitchX, DitchZ - 9f);

        static Vector3 Spawn(float x, float z) => new Vector3(x, Height(x, z) + 1.2f, z);

        /// <summary>Unity's <c>Mathf.SmoothStep</c> eases a value between two ends; this is the other one — the eased
        /// 0…1 ramp across a range, which is what every blend here actually wants.</summary>
        static float Ramp(float from, float to, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, v));

        public static bool Inside(float x, float z) => x > MinX && x < MaxX && z > MinZ && z < MaxZ;

        /// <summary>The visible surface of the snow, metres above the yard.</summary>
        public static float Height(float x, float z)
        {
            if (!Inside(x, z)) return 0f;
            float h = Rolling(x, z) + Drifts(x, z) + Flank(x, z) + Ditch(x, z);
            return h * Edge(x, z);
        }

        /// <summary>How far under the visible surface the boots go. A crust everywhere, and most of a boot-top in the
        /// drifts and at the bottom of the ditch, where blown snow collects.</summary>
        public static float Sink(float x, float z)
        {
            if (!Inside(x, z)) return 0f;
            float s = .05f;
            foreach (var m in Mounds)
            {
                float r = Vector2.Distance(new Vector2(x, z), new Vector2(m.x, m.y));
                if (r >= m.z) continue;
                s += Mathf.Min(.42f, m.w * .45f) * Ramp(m.z, m.z * .25f, r);
            }
            s += .18f * DitchShape(x, z);
            return Mathf.Min(s, .55f) * Edge(x, z);
        }


        static float Edge(float x, float z)
            => Ramp(0f, Rim, Mathf.Min(Mathf.Min(x - MinX, MaxX - x), Mathf.Min(z - MinZ, MaxZ - z)));

        /// <summary>Three sines of different lengths: enough for the ground to read as ground, gentle enough
        /// (nowhere past about 7°) that it never becomes a stand of its own.</summary>
        static float Rolling(float x, float z)
            => .38f * Mathf.Sin(x * .085f) * Mathf.Cos(z * .062f)
             + .17f * Mathf.Sin(x * .31f + z * .19f)
             + .09f * Mathf.Cos(x * .47f - z * .38f);

        static float Drifts(float x, float z)
        {
            float h = 0f;
            foreach (var m in Mounds)
            {
                float r = Vector2.Distance(new Vector2(x, z), new Vector2(m.x, m.y));
                if (r >= m.z) continue;
                // flat at the crown, flat at the rim, steepest half way out: the shape wind actually leaves
                h += m.w * Ramp(m.z, 0f, r);
            }
            return h;
        }

        static float Flank(float x, float z)
        {
            float across = 1f - Ramp(9f, 34f, Mathf.Abs(x - FlankX));
            if (across <= 0f) return 0f;
            float rise;
            if (z <= FlankFoot) rise = 0f;
            else if (z <= FlankCrest)
            {
                // a cube law, so the foot is a stroll and the last ten metres are at the limit of what boots hold
                float u = Mathf.InverseLerp(FlankFoot, FlankCrest, z);
                rise = FlankTop * u * u * u;
            }
            else rise = FlankTop * (1f - Ramp(FlankCrest, FlankBack, z));
            return rise * across;
        }

        static float Ditch(float x, float z) => -DitchDepth * DitchShape(x, z);

        static float DitchShape(float x, float z)
            => (1f - Ramp(6f, 9.5f, Mathf.Abs(x - DitchX))) * (1f - Ramp(.8f, 3.4f, Mathf.Abs(z - DitchZ)));

        // ── building it ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The field: one mesh to look at, a second, lower one to stand on.</summary>
        public static void BuildField(Material snow)
        {
            var seen = new GameObject("SnowField", typeof(MeshFilter), typeof(MeshRenderer));
            seen.GetComponent<MeshFilter>().sharedMesh = Grid(false);
            seen.GetComponent<MeshRenderer>().sharedMaterial = snow;

            // the surface the legs find: no renderer, so nobody ever sees that it is not where the snow looks
            var solid = new GameObject("SnowFieldFooting", typeof(MeshCollider));
            solid.GetComponent<MeshCollider>().sharedMesh = Grid(true);
            solid.AddComponent<Grip>().Hold = 1f;
        }

        static Mesh Grid(bool footing)
        {
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell), nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell);
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[verts.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = MinX + i * Cell, z = MinZ + j * Cell;
                    float y = Height(x, z);
                    if (footing) y -= Sink(x, z);
                    int k = j * (nx + 1) + i;
                    verts[k] = new Vector3(x, y, z);
                    uv[k] = new Vector2(x * .25f, z * .25f);
                }
            var tris = new int[nx * nz * 6];
            int t = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    tris[t++] = k; tris[t++] = k + nx + 1; tris[t++] = k + 1;
                    tris[t++] = k + 1; tris[t++] = k + nx + 1; tris[t++] = k + nx + 2;
                }
            var m = new Mesh { name = footing ? "snow-footing" : "snow-field" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = verts; m.uv = uv; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>What is past the range: a snowfield to the horizon and a ring of ridges on it. Without them the
        /// yard is a plate in a void, and no amount of good snow underfoot reads as a mountain.</summary>
        public static void BuildHorizon(Material snow, Material distant)
        {
            // the plain, as slabs laid around the yard and the field — never under them, because the ditch needs the
            // ground below it to be empty
            float[][] apron =
            {
                new[] { -400f, 400f, -400f, -30f },
                new[] { -400f, 400f, MaxZ, 400f },
                new[] { -400f, -100f, -30f, MaxZ },
                new[] { 100f, 400f, -30f, MaxZ },
                new[] { -100f, MinX, MinZ, MaxZ },
                new[] { MaxX, 100f, MinZ, MaxZ },
            };
            foreach (var a in apron)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Plain";
                // two centimetres under the yard: no seam anybody can see, and no two surfaces fighting for the same pixel
                go.transform.position = new Vector3((a[0] + a[1]) * .5f, -.52f, (a[2] + a[3]) * .5f);
                go.transform.localScale = new Vector3(a[1] - a[0], 1f, a[3] - a[2]);
                go.GetComponent<Renderer>().sharedMaterial = snow;
            }
            Ridges(distant);
        }

        /// <summary>A ring of peaks at three hundred metres, three sines deep so no two are alike. Seen from inside,
        /// so the faces point at the middle.</summary>
        static void Ridges(Material distant)
        {
            const int N = 96;
            const float R = 320f;
            var verts = new Vector3[N * 2];
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                float h = Mathf.Max(8f, 26f + 16f * Mathf.Sin(a * 3f + .7f) + 11f * Mathf.Sin(a * 7f + 2.1f) + 6f * Mathf.Sin(a * 13f + 4.4f));
                float cx = Mathf.Cos(a) * R, cz = Mathf.Sin(a) * R;
                verts[i * 2] = new Vector3(cx, -12f, cz);
                verts[i * 2 + 1] = new Vector3(cx, h, cz);
            }
            var tris = new int[N * 6];
            for (int i = 0; i < N; i++)
            {
                int b0 = i * 2, t0 = i * 2 + 1, b1 = (i + 1) % N * 2, t1 = (i + 1) % N * 2 + 1;
                int k = i * 6;
                tris[k] = b0; tris[k + 1] = b1; tris[k + 2] = t0;
                tris[k + 3] = t0; tris[k + 4] = b1; tris[k + 5] = t1;
            }
            var m = new Mesh { name = "ridges" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject("Ridges", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = distant;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ── the snow you see and the ground you stand on ─────────────────────────────────────────────────────────
        // The field is two meshes and the difference between them is Sink. The yard and its stands are the same idea
        // in another shape: a collider under a skin of snow with no collider of its own (SandboxRange.Cap). Either
        // way a boot that has found a collider is some way under the snow it can see, and that distance is what the
        // prints, the sound and the gait all want. The prints themselves are the game's (Height1079.Snow.SnowPrints):
        // the sandbox only says where the visible snow is.

        static readonly List<Bounds> skins = new List<Bounds>();

        /// <summary>Called when the range is rebuilt: the old skins went with the old scene.</summary>
        public static void ClearSkins() => skins.Clear();

        /// <summary>A slab of snow lying on a collider, looked at and never stood on.</summary>
        public static void Skin(Bounds slab) => skins.Add(slab);

        /// <summary>How far under the visible snow a point on a collider is: the field's <see cref="Sink"/>, or the
        /// thickness of the skin lying over the yard and its stands, or nothing on bare rock and ice.</summary>
        public static float SinkAt(Vector3 on)
        {
            if (Inside(on.x, on.z)) return Sink(on.x, on.z);
            float deepest = 0f;
            foreach (var b in skins)
            {
                if (on.x < b.min.x || on.x > b.max.x || on.z < b.min.z || on.z > b.max.z) continue;
                float under = b.max.y - on.y;
                // a little tolerance below, for a probe that stopped a hair short; and nothing from a slab that is
                // a level above or below the point, because the stands are stacked
                if (under > -.03f && under < .4f) deepest = Mathf.Max(deepest, under);
            }
            return deepest;
        }
    }
}
