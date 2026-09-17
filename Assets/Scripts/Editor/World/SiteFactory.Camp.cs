using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Height1079.Core;
using static Height1079.EditorTools.World.Surf;

namespace Height1079.EditorTools.World
{
    /// <summary>The 31 Jan camp as a hand-dressed set: organic props (PropKit) instead of boxes and tubes — sagging canvas, irregular snow banks,
    /// slumped and fallen rucksacks with snow on them, a Zorkiy rangefinder in its case, quilted blankets, felt and leather boots, sooty buckets,
    /// closed logs with end grain. Also the shared tent body and ski poles used by the slope tent.</summary>
    public static partial class SiteFactory
    {
        // ------------------------------------------------------------------ kit: one builder per material, flushed as parts
        sealed class Kit
        {
            readonly List<(string name, MeshBuilder mb, Material[] mats)> list = new List<(string, MeshBuilder, Material[])>();
            readonly Dictionary<string, int> index = new Dictionary<string, int>();
            public MeshBuilder B(string name, params Material[] mats)
            {
                if (!index.TryGetValue(name, out var i)) { i = list.Count; index[name] = i; list.Add((name, new MeshBuilder(mats.Length), mats)); }
                return list[i].mb;
            }
            public MeshBuilder Snow => B("Snow", Materials.Snow);
            public void Flush(Transform t) { foreach (var e in list) if (e.mb.VertexCount > 0) Part(t, e.name, e.mb, e.mats); }
        }

        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static Material Mat(string key, Func<Material> make) { if (!matCache.TryGetValue(key, out var m) || m == null) matCache[key] = m = make(); return m; }
        static Material RuckKhaki => Mat("RuckKhaki", () => PropTextures.RuckCanvas("RuckKhaki", new Color(.47f, .45f, .31f), 1));
        static Material RuckGrey => Mat("RuckGrey", () => PropTextures.RuckCanvas("RuckGrey", new Color(.43f, .45f, .42f), 2));
        static Material RuckBrown => Mat("RuckBrown", () => PropTextures.RuckCanvas("RuckBrown", new Color(.5f, .4f, .28f), 3));
        static Material LeatherBrown => Mat("LeatherBrown", () => PropTextures.Leather("LeatherBrown", new Color(.36f, .22f, .12f), 4));
        static Material LeatherDark => Mat("LeatherDark", () => PropTextures.Leather("LeatherDark", new Color(.13f, .09f, .07f), 5));
        static Material LeatherGreen => Mat("LeatherGreen", () => PropTextures.Leather("OilclothGreen", new Color(.17f, .24f, .18f), 6));
        static Material QuiltBlue => Mat("QuiltBlue", () => PropTextures.Quilt("QuiltBlue", new Color(.2f, .27f, .42f), true, 6, 7));
        static Material QuiltMaroon => Mat("QuiltMaroon", () => PropTextures.Quilt("QuiltMaroon", new Color(.42f, .14f, .13f), true, 6, 8));
        static Material QuiltGreen => Mat("QuiltGreen", () => PropTextures.Quilt("QuiltGreen", new Color(.22f, .32f, .22f), true, 5, 9));
        static Material Vatnik => Mat("Vatnik", () => PropTextures.Quilt("Vatnik", new Color(.13f, .14f, .16f), false, 7, 10));
        static Material FeltGrey => Mat("FeltGrey", () => PropTextures.Felt("FeltGrey", new Color(.3f, .29f, .27f), 11));
        static Material FeltBlack => Mat("FeltBlack", () => PropTextures.Felt("FeltBlack", new Color(.1f, .095f, .09f), 12));
        static Material KnitRed => Mat("KnitRed", () => PropTextures.Knit("KnitRed", new Color(.55f, .12f, .1f), 13));
        static Material KnitGrey => Mat("KnitGrey", () => PropTextures.Knit("KnitGrey", new Color(.62f, .6f, .55f), 14));
        static Material Tin => Mat("Tin", () => PropTextures.SootyTin("SootyTin", 15));
        static Material EndGrain => Mat("EndGrain", () => PropTextures.EndGrain("EndGrain", false));
        static Material EndGrainChar => Mat("EndGrainChar", () => PropTextures.EndGrain("EndGrainCharred", true));
        static Material Charred => Mat("Charred", () => PropTextures.Char("CharredWood"));
        static Material SkiWood => Mat("SkiWood", () => PropTextures.Varnish("SkiVarnish", new Color(.62f, .38f, .2f), 16));
        static Material HandleWood => Mat("HandleWood", () => PropTextures.Varnish("HandleWood", new Color(.78f, .63f, .44f), 17));
        static Material BambooMat => Mat("Bamboo", () => PropTextures.Bamboo("Bamboo"));
        static Material Paper => Mat("Paper", () => PropTextures.PaperEdge("PaperEdge"));
        static Material VulcaniteMat => Mat("Vulcanite", () => PropTextures.Vulcanite("Vulcanite"));
        static Material ChromeMat => Mat("Chrome", () => PropTextures.Chrome);
        static Material SteelMat => Mat("Steel", () => PropTextures.DarkSteel);
        static Material GlassMat => Mat("Glass", () => PropTextures.Glass);
        static Material Embers => Mat("Embers", () =>
        {
            var m = Materials.Get("Embers", new Color(.3f, .08f, .02f), smoothness: .1f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, .35f, .08f) * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m);
            return m;
        });

        static Matrix4x4 TRS(Vector3 p, Quaternion r, float s = 1f) => Matrix4x4.TRS(p, r, Vector3.one * s);
        static Quaternion Yaw(float deg) => Quaternion.Euler(0, deg, 0);
        static float Sgn(float x) => x < 0 ? -1f : 1f;
        static float SPow(float x, float p) => Sgn(x) * Mathf.Pow(Mathf.Abs(x), p);

        /// <summary>Old snow mound, now irregular (kept for the other sites).</summary>
        static void Mound(MeshBuilder mb, int s, Vector3 c, Vector3 size, int seed) => Lump(mb, s, c, new Vector3(size.x * 1.1f, size.y * .8f, size.z * 1.1f), seed, .4f);

        // ------------------------------------------------------------------ snow banks

        /// <summary>Wall of dug and trampled snow around a pad: an elliptic ring with a gap, steep cut inside, spoil sloping outside, lumpy blocks on the rim.</summary>
        static void Bank(MeshBuilder mb, Matrix4x4 m, Vector3 c, float rx, float rz, float gapAngle, float gapHalf, float height, float width, int seed)
        {
            float span = Mathf.PI * 2 - gapHalf * 2;
            Vector3 At(float u, float v, bool jitter)
            {
                float th = gapAngle + gapHalf + u * span;
                float ends = Noise.Smooth(0f, .07f, u) * Noise.Smooth(1f, .93f, u);
                var q = new Vector3(Mathf.Cos(th) * 1.3f, Mathf.Sin(th) * 1.3f, seed);
                float h = height * (.7f + .6f * Mathf.Clamp01(.5f + Noise.Fbm(q, 3))) * ends;
                float prof = v < .3f ? Noise.Smooth(0f, .3f, v) : 1f - Mathf.Pow(Noise.Smooth(.3f, 1f, v), 1.4f);
                float d = Mathf.Lerp(-width * .4f, width * .6f, v) + (jitter ? .12f * Noise.N(q * 2f + Vector3.one * v) : 0);
                float y = h * prof * (1f + .18f * Noise.Fbm(q * 3f + new Vector3(0, v * 4, 0), 2)) - .14f * (1 - prof);
                return c + new Vector3(Mathf.Cos(th) * (rx + d), y, Mathf.Sin(th) * (rz + d));
            }
            Emit(mb, 0, 64, 7, (u, v) => At(u, v, true), new Options { M = m, UvScale = new Vector2(12, 2) });
            var rnd = new System.Random(seed);
            for (int k = 0; k < 16; k++)
            {
                float u = (float)rnd.NextDouble();
                var p = At(u, .38f, false);
                float sz = .16f + .2f * (float)rnd.NextDouble();
                Lump(mb, 0, p + Vector3.down * sz * .35f, new Vector3(sz * 1.4f, sz * .55f, sz), seed * 10 + k, .45f, m: m, skirt: .2f);
            }
        }

        // ------------------------------------------------------------------ wood

        /// <summary>A log with bark, a slight bend, knots and branch stubs, closed at both ends with end grain.</summary>
        static void Log(Kit k, Matrix4x4 m, Vector3 a, Vector3 b, float r, int seed, bool charred = false, float snow = 0f, int stubs = 1, float r1 = -1f)
        {
            var mb = charred ? k.B("LogsCharred", Charred, EndGrainChar) : k.B("Logs", Materials.Bark, EndGrain);
            var off = new Vector3(seed * .77f, seed * 1.9f, seed * .31f);
            var axis = b - a; float len = axis.magnitude; var dir = axis / len;
            var perp = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : Vector3.up).normalized;
            perp = Quaternion.AngleAxis(Noise.N(off) * 180f, dir) * perp;
            var mid = Vector3.Lerp(a, b, .5f) + perp * len * .025f * Noise.N(off * 2f);
            if (r1 < 0) r1 = r * .9f;
            Sweep(mb, 0, new[] { a, mid, b }, (t, ang) =>
            {
                float rr = Mathf.Lerp(r, r1, t);
                return rr * (1f + .055f * Noise.Fbm(new Vector3(Mathf.Cos(ang) * .9f, t * len * 1.6f, Mathf.Sin(ang) * .9f) + off, 3));
            }, 12, Mathf.Clamp(Mathf.RoundToInt(len * 8), 3, 16), new Options { M = m, UvScale = new Vector2(Mathf.Max(1, Mathf.Round(r * 12)), 1.4f), Snow = k.Snow, SnowDepth = snow, SnowSeed = seed }, true, true, 1);
            var rnd = new System.Random(seed);
            for (int i = 0; i < stubs; i++)
            {
                float tt = .2f + .6f * (float)rnd.NextDouble();
                var around = Quaternion.AngleAxis((float)rnd.NextDouble() * 360f, dir) * perp;
                var basePt = Vector3.Lerp(a, b, tt) + around * r * .8f;
                var tip = basePt + around * (.03f + .05f * (float)rnd.NextDouble()) + dir * .02f;
                float sr = r * (.25f + .15f * (float)rnd.NextDouble());
                Sweep(mb, 0, new[] { basePt, tip }, (t, ang) => Mathf.Lerp(sr * 1.3f, sr, t), 6, 1, new Options { M = m }, false, true, 1);
            }
        }

        /// <summary>A straight cut stick (stakes, crossbars), bark all round.</summary>
        static void Stick(Kit k, Matrix4x4 m, Vector3 a, Vector3 b, float r, int seed) => Log(k, m, a, b, r, seed, false, 0f, 0, r * .8f);

        // ------------------------------------------------------------------ skis and poles

        /// <summary>1950s wooden touring ski along +Z (tail −1.0, turned-up tip +1.05), base at y = 0; optional toe iron and leather strap.</summary>
        static void Ski(Kit k, Matrix4x4 m, int seed, bool binding, float snow = 0f)
        {
            var wood = k.B("Skis", SkiWood);
            var path = new[] { new Vector3(0, .011f, -1.0f), new Vector3(0, .011f, -.2f), new Vector3(0, .011f, .6f), new Vector3(0, .016f, .85f), new Vector3(0, .05f, .98f), new Vector3(0, .1f, 1.04f) };
            Sweep(wood, 0, path, (t, ang) => 1f, 8, 22, new Options { M = m, UvScale = new Vector2(1, 1), Snow = k.Snow, SnowDepth = snow, SnowSeed = seed }, true, true, -1,
                (t, ang) =>
                {
                    float w = t < .08f ? Mathf.Lerp(.064f, .068f, t / .08f) : t < .82f ? Mathf.Lerp(.068f, .074f, (t - .08f) / .74f) : .074f * Mathf.Sqrt(Mathf.Max(.02f, 1f - (t - .82f) / .18f));
                    float th = t < .8f ? .022f : Mathf.Lerp(.022f, .008f, (t - .8f) / .2f);
                    return new Vector2(SPow(Mathf.Cos(ang), .25f) * w / 2, SPow(Mathf.Sin(ang), .25f) * th / 2);
                });
            if (!binding) return;
            var steel = k.B("SkiIrons", SteelMat); var strap = k.B("SkiStraps", LeatherBrown);
            BoxM(steel, 0, m, new Vector3(0, .026f, .08f), new Vector3(.095f, .004f, .07f), Quaternion.identity);
            foreach (int s in new[] { -1, 1 }) BoxM(steel, 0, m, new Vector3(s * .045f, .045f, .08f), new Vector3(.004f, .04f, .06f), Quaternion.identity);
            Ribbon(strap, 0, new[] { new Vector3(-.046f, .06f, .09f), new Vector3(0, .1f, .1f), new Vector3(.046f, .06f, .09f) }, .025f, .003f, Vector3.forward, m, 8);
            Ribbon(strap, 0, new[] { new Vector3(-.04f, .025f, -.02f), new Vector3(0, .03f, -.2f), new Vector3(.04f, .025f, -.02f) }, .012f, .003f, Vector3.up, m, 8); // heel strap lying slack
        }

        /// <summary>Bamboo ski pole with nodes, leather grip and wrist loop, cane-and-leather basket, steel tip.</summary>
        static void SkiPole(Kit k, Vector3 foot, Vector3 top, int seed = 0)
        {
            var bamboo = k.B("Poles", BambooMat); var leather = k.B("PoleLeather", LeatherBrown); var steel = k.B("PoleTips", SteelMat);
            var dir = (top - foot).normalized; float len = (top - foot).magnitude;
            Sweep(bamboo, 0, new[] { foot, top }, (t, ang) =>
            {
                float metres = t * len;
                float node = Mathf.Pow(Mathf.Abs(Mathf.Cos(metres / .3f * Mathf.PI)), 40);
                return Mathf.Lerp(.0115f, .0095f, t) * (1f + .12f * node);
            }, 7, 16, new Options { UvScale = new Vector2(1, 1f / .3f / 2f) }, true, true);
            Sweep(leather, 0, new[] { top - dir * .16f, top + dir * .005f }, (t, ang) => .0135f, 7, 2, new Options());
            var side = Vector3.Cross(dir, Vector3.up).sqrMagnitude > .01f ? Vector3.Cross(dir, Vector3.up).normalized : Vector3.right;
            Ribbon(leather, 0, new[] { top - dir * .04f, top - dir * .12f + side * .05f, top - dir * .22f + side * .02f, top - dir * .1f }, .014f, .002f, Vector3.Cross(side, dir).normalized, Matrix4x4.identity, 10);
            var ringC = foot + dir * .11f;
            var ringPath = new Vector3[13];
            var e1 = Vector3.Cross(dir, side).normalized;
            for (int i = 0; i <= 12; i++) { float a = i / 12f * Mathf.PI * 2; ringPath[i] = ringC + (side * Mathf.Cos(a) + e1 * Mathf.Sin(a)) * .055f; }
            Sweep(leather, 0, ringPath, (t, a) => .0045f, 5, 24, new Options(), false, false);
            foreach (var sp in new[] { side, e1 })
                Sweep(leather, 0, new[] { ringC - sp * .055f, ringC + sp * .055f }, (t, a) => .0025f, 4, 2, new Options(), true, true);
            Sweep(steel, 0, new[] { foot + dir * .04f, foot - dir * .01f }, (t, a) => Mathf.Lerp(.0085f, .001f, t), 6, 2, new Options(), true, true);
        }

        // ------------------------------------------------------------------ tent

        /// <summary>Canvas (sagging, wrinkled), 8 pairs of skis under the floor, ski-pole stands and guys, middle stand of one pair of skis, ice axe.
        /// Shared by the slope tent and the forest camp. <paramref name="skiRow"/> = centre of each ski row from the middle (larger pushes the tips out);
        /// <paramref name="open"/>: doorway (±<see cref="DoorHalf"/>) with the sheet rolled up.</summary>
        static void TentBody(Transform t, float skiRow, bool open = false, float roofSnow = 0f)
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight, wall = Hr - Mathf.Sqrt(Sites.Tent.SlopeLength * Sites.Tent.SlopeLength - W * W / 4);
            var k = new Kit();
            var id = Matrix4x4.identity;

            // skis under the floor: two rows along the tent, tips out at the ends
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 8; i++)
                {
                    float x = -W / 2 + .12f + i * (W - .24f) / 7f, z = (row == 0 ? -1 : 1) * skiRow;
                    Ski(k, TRS(new Vector3(x, -.03f, z), Yaw(row == 0 ? 180 + (i % 2) * 1.2f : (i % 2) * -1.2f)), 200 + row * 10 + i, false);
                }

            // canvas: floor + one sheet floor→wall→ridge→wall→floor, sagging between the stands, wrinkled by the guys
            float y0 = .04f;
            var cv = k.B("Canvas", Materials.Canvas, Materials.Sheet);
            cv.Quad(0, new Vector3(-W / 2, y0, -L / 2), new Vector3(-W / 2, y0, L / 2), new Vector3(W / 2, y0, L / 2), new Vector3(W / 2, y0, -L / 2), Vector2.zero, new Vector2(0, L), new Vector2(W, L), new Vector2(W, 0));
            var section = new[] { new Vector2(-W / 2, y0), new Vector2(-W / 2 + .02f, wall), new Vector2(0, Hr), new Vector2(W / 2 - .02f, wall), new Vector2(W / 2, y0) };
            var prof = Poly(section);
            float roofA = (section[1] - section[0]).magnitude, total = roofA * 2 + (section[2] - section[1]).magnitude * 2;
            Emit(cv, 0, 26, 30, (u, v) =>
            {
                var p2 = prof(v); float z = -L / 2 + u * L;
                float along = Mathf.Sin(Mathf.PI * u);
                float d = v * total, half = total / 2; bool roof = d > roofA && d < total - roofA;
                float roofT = roof ? Mathf.Sin(Mathf.PI * (d < half ? Mathf.InverseLerp(roofA, half, d) : Mathf.InverseLerp(half, total - roofA, d))) : 0f;
                float sag = (roof ? .055f * along * roofT : .02f * along * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0, roofA, Mathf.Min(d, total - d))));
                float wrinkle = .012f * Noise.Fbm(new Vector3(u * 7f, v * 5f, 3f), 3) + .006f * Mathf.Sin(u * L * 11f + v * 9f) * (1 - along);
                var inward = roof ? new Vector3(-Mathf.Sign(p2.x) * .25f, -1f, 0).normalized : new Vector3(-Mathf.Sign(p2.x), 0, 0);
                var p = new Vector3(p2.x, p2.y, z) + inward * (sag + wrinkle) * (p2.y > y0 + .01f ? 1 : 0);
                return p;
            }, new Options { DoubleSided = true, UvScale = new Vector2(L, total), Snow = roofSnow > 0 ? k.Snow : null, SnowDepth = roofSnow, SnowSeed = 77 });
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * L / 2;
                if (open && end > 0)
                {
                    float roofAtDoor = Hr - (Hr - wall) * DoorHalf / (W / 2);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 o = new Vector3(side * W / 2, y0, z), d0 = new Vector3(side * DoorHalf, y0, z), d1 = new Vector3(side * DoorHalf, roofAtDoor, z), w = new Vector3(side * (W / 2 - .02f), wall, z);
                        Emit(cv, 0, 4, 6, (u, v) => Vector3.Lerp(Vector3.Lerp(o, d0, u), Vector3.Lerp(w, d1, u), v) + Vector3.forward * .015f * Noise.N(u * 4, v * 4, side) * u, new Options { DoubleSided = true, UvScale = new Vector2(.6f, .8f) });
                    }
                    // the entrance sheet rolled up above the doorway, tied with two tapes
                    Sweep(cv, 1, new[] { new Vector3(-DoorHalf - .03f, roofAtDoor - .01f, z + .045f), new Vector3(0, roofAtDoor + .01f, z + .05f), new Vector3(DoorHalf + .03f, roofAtDoor - .01f, z + .045f) },
                        (tt, a) => .045f * (1f + .15f * Noise.N(tt * 9f, a, 2f)), 9, 10, new Options { UvScale = new Vector2(1, 2) });
                    continue;
                }
                Vector3 bl = new Vector3(-W / 2, y0, z), br = new Vector3(W / 2, y0, z), tl = new Vector3(-W / 2 + .02f, wall, z), tr = new Vector3(W / 2 - .02f, wall, z), ap = new Vector3(0, Hr, z);
                cv.Quad(0, bl, br, tr, tl, new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, wall), new Vector2(0, wall), true);
                int a0 = cv.Vert(tl, Vector3.forward * end, new Vector2(0, wall)), b0 = cv.Vert(tr, Vector3.forward * end, new Vector2(W, wall)), c0 = cv.Vert(ap, Vector3.forward * end, new Vector2(W / 2, Hr));
                cv.Tri(0, a0, c0, b0); cv.Tri(0, a0, b0, c0);
            }
            if (!open) cv.Quad(1, new Vector3(-.35f, y0, L / 2 + .03f), new Vector3(.35f, y0, L / 2 + .03f), new Vector3(.22f, .92f, L / 2 + .05f), new Vector3(-.22f, .92f, L / 2 + .05f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            // back end (-Z): the pipe sleeve, gathered
            Sweep(cv, 0, new[] { new Vector3(0, .7f, -L / 2 + .01f), new Vector3(.01f, .69f, -L / 2 - .14f), new Vector3(.02f, .66f, -L / 2 - .28f) },
                (tt, a) => Mathf.Lerp(.15f, .11f, tt) * (1f + .12f * Noise.N(tt * 6, a * 2, 5)), 12, 6, new Options { DoubleSided = true, UvScale = new Vector2(1, 1) }, false, false);

            // stands and guys: crossed ski poles at both ends, ropes to ski poles in the snow; a pair of skis as the middle stand
            var rope = k.B("Guys", Materials.Rope);
            int ps = 0;
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * (L / 2 + .06f);
                SkiPole(k, new Vector3(-.32f, -.12f, z), new Vector3(.08f, Hr + .22f, z), ps++);
                SkiPole(k, new Vector3(.32f, -.12f, z), new Vector3(-.08f, Hr + .22f, z), ps++);
                Vector3 anchor = new Vector3(0, .95f, end * (L / 2 + 1.7f));
                SkiPole(k, anchor + new Vector3(0, -1.05f, 0), anchor + new Vector3(0, .2f, -end * .12f), ps++);
                Sweep(rope, 0, new[] { new Vector3(0, Hr + .02f, end * L / 2), Vector3.Lerp(new Vector3(0, Hr + .02f, end * L / 2), anchor + Vector3.up * .15f, .5f) + Vector3.down * .03f, anchor + Vector3.up * .15f }, (tt, a) => .005f, 4, 6, new Options(), false, false);
                foreach (int side in new[] { -1, 1 })
                {
                    var stake = new Vector3(side * (W / 2 + .9f), .5f, end * (L / 2 - .3f));
                    SkiPole(k, stake + new Vector3(0, -.62f, 0), stake + new Vector3(side * .1f, .55f, 0), ps++);
                    var from = new Vector3(side * W / 2, wall, end * (L / 2 - .3f)); var to = stake + Vector3.up * .3f;
                    Sweep(rope, 0, new[] { from, Vector3.Lerp(from, to, .5f) + Vector3.down * .02f, to }, (tt, a) => .004f, 4, 6, new Options(), false, false);
                }
            }
            foreach (int s in new[] { -1, 1 })
                Ski(k, TRS(new Vector3(s * .05f, Hr / 2 + .03f, .012f), Quaternion.Euler(-90, 0, 0)) * Matrix4x4.Scale(new Vector3(1, 1, Hr / 2.05f)), 260 + s, false);

            // ice axe at the entrance (found there on 26 Feb): long ash shaft, steel pick and adze, spike
            IceAxe(k, TRS(new Vector3(.8f, .62f, L / 2 + .42f), Quaternion.Euler(0, 20, -3)));
            k.Flush(t);
        }

        static void IceAxe(Kit k, Matrix4x4 m)
        {
            Sweep(k.B("IceAxeShaft", HandleWood), 0, new[] { new Vector3(0, 0, 0), new Vector3(0, -.88f, 0) }, (t, a) => .0165f, 8, 3, new Options { M = m, UvScale = new Vector2(1, 1) }, true, true);
            var steel = k.B("IceAxeSteel", SteelMat);
            var head = new[] { new Vector2(-.14f, -.005f), new Vector2(-.02f, -.02f), new Vector2(.02f, -.02f), new Vector2(.11f, -.012f), new Vector2(.13f, .004f), new Vector2(.02f, .025f), new Vector2(-.02f, .025f), new Vector2(-.15f, .012f) };
            Extrude(steel, 0, head, p => Mathf.Lerp(.02f, .004f, Mathf.Abs(p.x) / .15f), m * Matrix4x4.TRS(new Vector3(0, .005f, 0), Quaternion.identity, Vector3.one));
            Sweep(steel, 0, new[] { new Vector3(0, -.86f, 0), new Vector3(0, -.95f, 0) }, (t, a) => Mathf.Lerp(.014f, .002f, t), 6, 2, new Options { M = m }, true, true);
        }

        // ------------------------------------------------------------------ rucksack

        sealed class RuckPose
        {
            public Vector3 At; public float Yaw, Tilt, Roll, Full = 1f, Snow, Sink; public Material Canvas; public float Scale = 1f;
            /// <summary>0 upright, 1 on its side, 2 on its back (straps down), 3 face down.</summary>
            public int Lying;
        }

        const float RuckW = .36f, RuckH = .56f, RuckD = .25f;

        /// <summary>Soft 1950s canvas rucksack: gathered neck under a draped flap, two leather shoulder straps with buckles, side pockets, drawstring.
        /// Local: bottom centre at the origin, +Y up, +Z = back (straps), −Z = front (flap).</summary>
        static void Rucksack(Kit k, Matrix4x4 m, float full, float snow, int seed, Material canvas)
        {
            var sack = k.B("Ruck_" + canvas.name, canvas);
            var leather = k.B("RuckLeather", LeatherBrown);
            var steel = k.B("RuckBuckles", SteelMat);
            var off = new Vector3(seed * .91f, seed * .37f, seed * 1.3f);
            float W = RuckW, D = RuckD, H = RuckH * Mathf.Lerp(.78f, 1f, full);
            float lean = Noise.N(off) * .08f;
            float Prof(float v)
            {
                float closeB = Mathf.Pow(Noise.Smooth(0f, .16f, v), .5f);
                float gather = Noise.Smooth(.72f, .97f, v);
                float closeT = 1f - Noise.Smooth(.95f, 1f, v);
                float bulge = 1f + .15f * Mathf.Sin(Mathf.PI * Mathf.Min(1, v * 1.15f)) * (.6f + .4f * full) + (1 - full) * .22f * (1 - v);
                return closeB * Mathf.Lerp(1f, .32f, gather) * closeT * bulge;
            }
            Vector3 Body(float u, float v)
            {
                float th = u * Mathf.PI * 2, c = Mathf.Cos(th), s = Mathf.Sin(th);
                float gather = Noise.Smooth(.72f, .97f, v);
                float wr = .035f * gather * Mathf.Sin(th * 11 + seed) + .03f * Noise.Fbm(new Vector3(c * 1.5f, v * 3f, s * 1.5f) + off, 3)
                         + (1 - full) * .06f * Noise.N(new Vector3(c * 3, v * 5, s * 3) + off) + .02f * Mathf.Abs(Noise.N(new Vector3(c * 6, v * 1.2f, s * 6) + off)) * (1 - gather);
                float pr = Prof(v) * (1 + wr);
                float x = W / 2 * SPow(c, .7f) * pr, z = D / 2 * SPow(s, .7f) * pr;
                if (z > 0) z *= .72f;
                float y = v * H;
                return new Vector3(x + lean * y * y / H, y, z);
            }
            Emit(sack, 0, 30, 22, Body, new Options { M = m, ClosedU = true, Flip = true, UvScale = new Vector2(2, 1.2f), Snow = k.Snow, SnowDepth = snow, SnowSeed = seed, Centre = new Vector3(0, H * .5f, 0) });

            // flap: back of the neck → over the top → down the front
            var fpath = new[] { new Vector3(lean * .6f, H * .86f, D * .3f), new Vector3(lean * .9f, H * 1.02f, 0), new Vector3(lean * .8f, H * .95f, -D * .48f), new Vector3(lean * .5f, H * .64f, -D * .6f) };
            Emit(sack, 0, 12, 14, (u, v) =>
            {
                var p = CatmullPath(fpath, v);
                float across = (u - .5f) * W * (.95f + .12f * v);
                float droop = Mathf.Pow(Mathf.Abs(2 * u - 1), 2f) * H * .2f * (1 - Mathf.Abs(v - .35f));
                float wrinkle = .01f * Noise.Fbm(new Vector3(u * 3, v * 3, 0) + off, 2);
                return p + new Vector3(across, -droop + wrinkle, (v > .6f ? -1 : 0) * Mathf.Pow(Mathf.Abs(2 * u - 1), 2) * .01f);
            }, new Options { M = m, DoubleSided = true, UvScale = new Vector2(1, 1), Snow = k.Snow, SnowDepth = snow * 1.2f, SnowSeed = seed + 1 });
            // flap straps and buckles
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * W * .22f + lean * .4f;
                float zf = -D / 2 * Prof(.45f) - .006f;
                Ribbon(leather, 0, new[] { new Vector3(x, H * .66f, -D * .6f), new Vector3(x, H * .56f, zf - .004f), new Vector3(x, H * .42f, zf) }, .022f, .003f, Vector3.back, m, 6);
                BoxM(steel, 0, m, new Vector3(x, H * .45f, zf - .004f), new Vector3(.03f, .026f, .004f), Quaternion.identity);
            }
            // shoulder straps with buckles, standing off the back
            foreach (int s in new[] { -1, 1 })
            {
                float zb = D / 2 * .72f;
                var path = new[] { new Vector3(s * W * .15f, H * .9f, zb * .8f), new Vector3(s * W * .19f, H * .65f, zb + .06f), new Vector3(s * W * .24f, H * .33f, zb + .05f), new Vector3(s * W * .3f, H * .08f, zb * .9f) };
                Ribbon(leather, 0, path, .042f, .004f, Vector3.forward, m, 12);
                BoxM(steel, 0, m, CatmullPath(path, .72f) + Vector3.forward * .006f, new Vector3(.046f, .032f, .005f), Quaternion.identity);
            }
            // side pockets with their own little flaps
            foreach (int s in new[] { -1, 1 })
            {
                var pc = new Vector3(s * W / 2 * Prof(.3f) * .95f, H * .28f, -.01f);
                Lump(sack, 0, pc, new Vector3(.05f, .11f, .085f), seed * 3 + s, .12f, k.Snow, snow * .7f, true, m);
                Lump(sack, 0, pc + new Vector3(s * .012f, .1f, 0), new Vector3(.045f, .025f, .09f), seed * 5 + s, .1f, null, 0f, true, m);
            }
            // drawstring round the neck
            var neck = new Vector3[17];
            float nr = Prof(.88f);
            for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.PI * 2; neck[i] = new Vector3(W / 2 * SPow(Mathf.Cos(a), .7f) * nr + lean * .7f, H * .88f, D / 2 * SPow(Mathf.Sin(a), .7f) * nr * (Mathf.Sin(a) > 0 ? .72f : 1f)); }
            Sweep(k.B("RuckCord", Materials.Rope), 0, neck, (t, a) => .005f, 5, 32, new Options { M = m }, false, false);
        }

        static Matrix4x4 RuckMatrix(RuckPose p, float ground)
        {
            float H = RuckH * Mathf.Lerp(.78f, 1f, p.Full) * p.Scale;
            Quaternion lie; float lift;
            switch (p.Lying)
            {
                case 1: lie = Quaternion.Euler(0, 0, 88); lift = RuckW * .5f; break;    // on its side
                case 2: lie = Quaternion.Euler(88, 0, 0); lift = RuckD * .5f; break;    // on its back, straps down
                case 3: lie = Quaternion.Euler(-86, 0, 0); lift = RuckD * .6f; break;   // face down
                default: lie = Quaternion.identity; lift = 0; break;
            }
            var rot = Yaw(p.Yaw) * Quaternion.Euler(p.Tilt, 0, p.Roll) * lie;
            var centre = p.Lying == 0 ? Vector3.zero : new Vector3(0, H / p.Scale / 2, 0);
            var pos = new Vector3(p.At.x, ground + lift * p.Scale - p.Sink, p.At.z);
            return Matrix4x4.TRS(pos, rot, Vector3.one * p.Scale) * Matrix4x4.Translate(-centre);
        }

        // ------------------------------------------------------------------ footwear, clothes

        /// <summary>Valenok: felted boot, shaft up (+Y), toe to +Z, sole at y≈0; open top with a dark lining.</summary>
        static void FeltBoot(Kit k, Matrix4x4 m, int seed, Material felt, float snow = 0f)
        {
            var mb = k.B("Felt_" + felt.name, felt);
            var off = new Vector3(seed, seed * .3f, 0);
            var path = new[] { new Vector3(0, .47f, -.02f), new Vector3(0, .3f, -.02f), new Vector3(0, .12f, -.01f), new Vector3(0, .06f, .05f), new Vector3(0, .052f, .15f), new Vector3(0, .05f, .22f) };
            Sweep(mb, 0, path, (t, a) =>
            {
                float r = t < .55f ? Mathf.Lerp(.06f, .052f, t / .55f) : Mathf.Lerp(.052f, .047f, (t - .55f) / .45f);
                if (t > .9f) r *= Mathf.Sqrt(Mathf.Max(.05f, 1f - (t - .9f) / .1f));
                return r * (1f + .05f * Noise.Fbm(new Vector3(Mathf.Cos(a), t * 6, Mathf.Sin(a)) + off, 2));
            }, 12, 18, new Options { M = m, UvScale = new Vector2(1, 1.6f), Snow = k.Snow, SnowDepth = snow, SnowSeed = seed }, false, true, -1,
            (t, a) => new Vector2(Mathf.Cos(a) * Mathf.Lerp(1f, .72f, Noise.Smooth(.45f, .7f, t)), Mathf.Sin(a)));
            var lining = k.B("Felt_" + FeltBlack.name, FeltBlack);
            Sweep(lining, 0, new[] { new Vector3(0, .468f, -.02f), new Vector3(0, .34f, -.02f) }, (t, a) => .055f, 12, 2, new Options { M = m, Flip = true }, false, false);
            var bottom = new Vector3[12];
            for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; bottom[i] = new Vector3(Mathf.Cos(a) * .056f, .34f, -.02f + Mathf.Sin(a) * .056f); }
            Fan(lining, 0, m, bottom, Vector3.up);
        }

        /// <summary>Leather ski boot (size ~42): sole at y = 0, toe to +Z, ankle collar, tongue and laces.</summary>
        static void SkiBoot(Kit k, Matrix4x4 m, int seed)
        {
            var upper = k.B("BootUpper", LeatherBrown); var sole = k.B("BootSole", LeatherDark);
            var off = new Vector3(seed * .5f, 0, seed);
            Emit(upper, 0, 18, 16, (u, v) =>
            {
                float a = u * Mathf.PI * 2, env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(.02f, .98f, v)), .4f);
                float z = Mathf.Lerp(-.14f, .15f, v);
                float hw = .05f * env * (v > .55f ? 1f + .1f * Mathf.Sin((v - .55f) * 5f) : .92f);
                float top = Mathf.Lerp(.115f, .045f, Noise.Smooth(.2f, 1f, v)) * env;
                float x = hw * SPow(Mathf.Cos(a), .8f);
                float yb = Mathf.Sin(a) >= 0 ? Mathf.Pow(Mathf.Sin(a), .9f) * top : Mathf.Sin(a) * .006f;
                float crease = .004f * Noise.Fbm(new Vector3(u * 4, v * 6, 0) + off, 2);
                return new Vector3(x * (1 + crease * 8), .018f + yb + crease, z);
            }, new Options { M = m, ClosedU = true, UvScale = new Vector2(2, 2) });
            var outline = new Vector2[20];
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2, zz = Mathf.Sin(a);
                float w = zz > 0 ? Mathf.Lerp(.05f, .056f, zz) : .044f;
                outline[i] = new Vector2(Mathf.Cos(a) * w, zz > 0 ? zz * .16f : zz * .15f);
            }
            Extrude(sole, 0, outline, p => .018f, m * Matrix4x4.TRS(new Vector3(0, .009f, 0), Quaternion.Euler(90, 0, 0), Vector3.one));
            Sweep(upper, 0, new[] { new Vector3(0, .1f, -.1f), new Vector3(0, .165f, -.105f) }, (t, a) => .04f * (1f + .06f * Noise.N(a, t * 3, seed)), 12, 2, new Options { M = m, DoubleSided = true }, false, false,
                -1, (t, a) => new Vector2(Mathf.Cos(a) * .85f, Mathf.Sin(a) * 1.15f));
            var lace = k.B("Laces", Materials.Rope);
            for (int i = 0; i < 4; i++)
            {
                float z = -.07f + i * .035f, y = .15f - i * .022f;
                Ribbon(lace, 0, new[] { new Vector3(-.018f, y, z), new Vector3(0, y + .004f, z + .012f), new Vector3(.018f, y - .003f, z + .02f) }, .004f, .002f, Vector3.up, m, 4);
            }
        }

        static void Mitten(Kit k, Matrix4x4 m, int seed)
        {
            var mb = k.B("Mittens", KnitRed);
            Lump(mb, 0, new Vector3(0, .06f, 0), new Vector3(.05f, .07f, .018f), seed, .12f, null, 0, true, m);
            Lump(mb, 0, new Vector3(.045f, .03f, 0), new Vector3(.016f, .035f, .014f), seed + 1, .1f, null, 0, true, m * Matrix4x4.Rotate(Quaternion.Euler(0, 0, -25)));
            Sweep(mb, 0, new[] { new Vector3(0, -.01f, 0), new Vector3(0, -.05f, 0) }, (t, a) => .036f, 10, 2, new Options { M = m, DoubleSided = true }, false, false, -1, (t, a) => new Vector2(Mathf.Cos(a) * 1.3f, Mathf.Sin(a) * .5f));
        }

        /// <summary>Wool sock hanging from a line at the origin, heel bending toward +Z.</summary>
        static void Sock(Kit k, Matrix4x4 m, int seed)
        {
            var mb = k.B("Socks", KnitGrey);
            Sweep(mb, 0, new[] { new Vector3(0, 0, 0), new Vector3(0, -.12f, .005f), new Vector3(0, -.2f, .01f), new Vector3(0, -.235f, .05f), new Vector3(0, -.245f, .13f) },
                (t, a) => .033f * (1f + .08f * Noise.N(a, t * 5, seed)) * (t > .9f ? Mathf.Sqrt(Mathf.Max(.05f, 1f - (t - .9f) / .1f)) : 1f), 10, 14,
                new Options { M = m, DoubleSided = true, UvScale = new Vector2(1, 2) }, false, true, -1, (t, a) => new Vector2(Mathf.Cos(a) * .45f, Mathf.Sin(a)));
        }

        /// <summary>Quilted blanket spread flat on the floor, edges lifting over the bedding.</summary>
        static void BlanketSpread(Kit k, Matrix4x4 m, float w, float l, int seed, Material quilt)
        {
            var mb = k.B("Blanket_" + quilt.name, quilt);
            var off = new Vector3(seed, 0, seed * .3f);
            Emit(mb, 0, 24, 18, (u, v) =>
            {
                float x = (u - .5f) * w, z = (v - .5f) * l;
                float fold = .035f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(z * 7f + Noise.N(off + new Vector3(x, 0, z)) * 2f)), 3)
                           + .03f * Noise.Fbm(new Vector3(x * 2.2f, 0, z * 2.2f) + off, 3);
                float edge = Mathf.Max(Noise.Smooth(.8f, 1f, Mathf.Abs(2 * u - 1)), Noise.Smooth(.85f, 1f, Mathf.Abs(2 * v - 1)));
                return new Vector3(x, .012f + fold + edge * .03f * (1 + Noise.N(off * 2 + new Vector3(u * 5, 0, v * 5))), z);
            }, new Options { M = m, DoubleSided = true, UvScale = new Vector2(w, l) * 1.1f });
        }

        /// <summary>Blanket thrown back into a heap: folds radiating from a soft mound, a flat tongue of cloth spilling out.</summary>
        static void BlanketHeap(Kit k, Matrix4x4 m, float radius, float height, int seed, Material quilt)
        {
            var mb = k.B("Blanket_" + quilt.name, quilt);
            var off = new Vector3(seed * .7f, seed * .2f, seed);
            Emit(mb, 0, 30, 12, (u, v) =>
            {
                float th = u * Mathf.PI * 2;
                float R = radius * (1f + .35f * Noise.Fbm(new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0) + off, 2));
                float r = v * R;
                float y = height * Mathf.Pow(Mathf.Max(0, 1 - v * v), .8f) * (1f + .3f * Noise.N(new Vector3(Mathf.Cos(th) * 2, v * 2, Mathf.Sin(th) * 2) + off))
                        + .035f * Mathf.Sin(th * 6 + v * 9 + seed) * v * (1 - v) * 2 + .008f;
                return new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
            }, new Options { M = m, ClosedU = true, UvScale = new Vector2(radius * 6, radius * 2) });
            BlanketSpread(k, m * Matrix4x4.TRS(new Vector3(radius * .9f, 0, 0), Yaw(10), Vector3.one), radius * 1.3f, radius * 1.6f, seed + 5, quilt);
        }

        /// <summary>Quilted jacket rolled into a pillow, along +Z.</summary>
        static void VatnikRoll(Kit k, Matrix4x4 m, int seed)
        {
            var mb = k.B("Vatniks", Vatnik);
            Sweep(mb, 0, new[] { new Vector3(0, .07f, -.19f), new Vector3(.012f, .072f, 0), new Vector3(0, .068f, .19f) },
                (t, a) => .07f * (1f + .05f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 8f)) + .06f * Noise.Fbm(new Vector3(Mathf.Cos(a), t * 3, Mathf.Sin(a)) + Vector3.one * seed, 2)) * (t < .05f || t > .95f ? .85f : 1f),
                14, 12, new Options { M = m, UvScale = new Vector2(1.2f, 1.5f) }, true, true, -1, (t, a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .8f));
        }

        // ------------------------------------------------------------------ tins

        static void Bucket(Kit k, Matrix4x4 m, float h, float rTop, float rBot, int seed, float fill, bool bail, bool lid = false)
        {
            var tin = k.B("Tins", Tin);
            var prof = Poly(new Vector2(0, 0), new Vector2(rBot * .92f, 0), new Vector2(rBot, .012f), new Vector2(rTop, h), new Vector2(rTop + .007f, h + .004f),
                new Vector2(rTop + .003f, h + .011f), new Vector2(rTop - .004f, h + .005f), new Vector2(rTop - .005f, h - .006f), new Vector2(rBot - .005f, .014f), new Vector2(0, .014f));
            Lathe(tin, 0, prof, 28, 40, new Options { M = m, UvScale = new Vector2(3, 1) }, .02f, seed);
            if (fill > 0) Lump(k.Snow, 0, new Vector3(0, h * fill, 0), new Vector3(Mathf.Lerp(rBot, rTop, fill) - .006f, .025f, Mathf.Lerp(rBot, rTop, fill) - .006f), seed, .15f, null, 0, false, m, .02f);
            if (lid) Lathe(tin, 0, Poly(new Vector2(0, h + .02f), new Vector2(rTop * .6f, h + .016f), new Vector2(rTop + .006f, h + .004f), new Vector2(rTop + .006f, h - .004f)), 24, 6, new Options { M = m, UvScale = new Vector2(3, .3f) }, .01f, seed + 1);
            if (!bail) return;
            var arc = new Vector3[9];
            for (int i = 0; i <= 8; i++) { float a = i / 8f * Mathf.PI; arc[i] = new Vector3(-Mathf.Cos(a) * (rTop + .01f), h * .86f + Mathf.Sin(a) * (rTop * 1.05f + h * .14f), 0); }
            Sweep(k.B("Wire", SteelMat), 0, arc, (t, a) => .0025f, 5, 16, new Options { M = m }, true, true);
            foreach (int s in new[] { -1, 1 }) BoxM(k.B("Wire", SteelMat), 0, m, new Vector3(s * (rTop + .002f), h * .86f, 0), new Vector3(.006f, .03f, .025f), Quaternion.identity);
        }

        // ------------------------------------------------------------------ tools

        /// <summary>Axe: forged head (blade toward +X, in the XY plane) on a curved birch handle running down −Y.</summary>
        static void Axe(Kit k, Matrix4x4 m, float handle, bool sheath)
        {
            var steel = k.B("AxeSteel", SteelMat);
            var head = new[] { new Vector2(-.045f, -.028f), new Vector2(-.015f, -.03f), new Vector2(.05f, -.045f), new Vector2(.09f, -.075f), new Vector2(.112f, -.07f),
                               new Vector2(.12f, -.01f), new Vector2(.116f, .04f), new Vector2(.1f, .052f), new Vector2(.05f, .034f), new Vector2(-.015f, .03f), new Vector2(-.045f, .028f) };
            Extrude(steel, 0, head, p => Mathf.Lerp(.042f, .003f, Noise.Smooth(-.01f, .115f, p.x)), m);
            Sweep(k.B("AxeHandle", HandleWood), 0, new[] { new Vector3(-.028f, .045f, 0), new Vector3(-.028f, -.1f, 0), new Vector3(-.02f, -handle * .55f, 0), new Vector3(-.04f, -handle, 0) },
                (t, a) => Mathf.Lerp(.016f, .019f, Noise.Smooth(.8f, 1f, t)), 9, 10, new Options { M = m, UvScale = new Vector2(1, 1) }, true, true, -1, (t, a) => new Vector2(Mathf.Cos(a) * 1.35f, Mathf.Sin(a)));
            if (sheath) Lump(k.B("AxeSheath", LeatherBrown), 0, new Vector3(.045f, -.01f, 0), new Vector3(.085f, .07f, .03f), 44, .08f, null, 0, true, m);
        }

        /// <summary>Two-man crosscut saw: 1 m blade with M-teeth along −Y, wooden handles through the ends. Blade in the XY plane.</summary>
        static void Saw(Kit k, Matrix4x4 m)
        {
            var pts = new List<Vector2>();
            int teeth = 40;
            for (int i = 0; i <= teeth; i++) { float x = -.5f + i / (float)teeth; pts.Add(new Vector2(x, i % 2 == 0 ? -.045f : -.062f)); }
            for (int i = 10; i >= 0; i--) { float x = -.5f + i / 10f; pts.Add(new Vector2(x, .055f - .02f * (1 - Mathf.Sin(Mathf.PI * i / 10f)))); }
            Extrude(k.B("SawBlade", SteelMat), 0, pts.ToArray(), p => .0016f, m);
            foreach (int s in new[] { -1, 1 })
                Sweep(k.B("AxeHandle", HandleWood), 0, new[] { new Vector3(s * .47f, -.02f, 0), new Vector3(s * .47f, .2f, 0) }, (t, a) => .017f, 8, 2, new Options { M = m }, true, true);
        }

        // ------------------------------------------------------------------ small things

        /// <summary>Stadium outline (half-length a, end radius r) in XZ; u walks round it.</summary>
        static Vector3 Stadium(float u, float a, float r)
        {
            float s = a - r, P = 4 * s + 2 * Mathf.PI * r, d = (u - Mathf.Floor(u)) * P;
            if (d < 2 * s) return new Vector3(-s + d, 0, -r);
            d -= 2 * s;
            if (d < Mathf.PI * r) { float ang = -Mathf.PI / 2 + d / r; return new Vector3(s + Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r); }
            d -= Mathf.PI * r;
            if (d < 2 * s) return new Vector3(s - d, 0, r);
            d -= 2 * s;
            float b = Mathf.PI / 2 + d / r; return new Vector3(-s + Mathf.Cos(b) * r, 0, Mathf.Sin(b) * r);
        }

        static void Prism(MeshBuilder mb, Matrix4x4 m, float a, float r, float y0, float y1, bool top, bool bottom, bool doubleSided = false)
        {
            Emit(mb, 0, 48, 1, (u, v) => Stadium(u, a, r) + Vector3.up * Mathf.Lerp(y0, y1, v), new Options { M = m, ClosedU = true, Flip = true, DoubleSided = doubleSided, UvScale = new Vector2(3, .3f) });
            var ring = new Vector3[48];
            for (int i = 0; i < 48; i++) ring[i] = Stadium(i / 48f, a, r);
            if (top) Fan(mb, 0, m, Array.ConvertAll(ring, p => p + Vector3.up * y1), Vector3.up);
            if (bottom) Fan(mb, 0, m, Array.ConvertAll(ring, p => p + Vector3.up * y0), Vector3.down);
        }

        /// <summary>Zorkiy (1948–56, the group's cameras were Zorkiy № 486963 and № 488797): a Leica II type rangefinder — vulcanite body 136 × 64 × 33 mm,
        /// chrome top and bottom plates, rewind and wind knobs, accessory shoe, two rangefinder windows and the finder window, collapsible Industar-22 50/3.5.
        /// Sits in the bottom half of its brown ever-ready case; the strap trails off. Local: base at y = 0, lens toward −Z.</summary>
        static void ZorkiyCamera(Kit k, Matrix4x4 m)
        {
            var chrome = k.B("CamChrome", ChromeMat); var body = k.B("CamBody", VulcaniteMat); var glass = k.B("CamGlass", GlassMat); var black = k.B("CamBlack", SteelMat);
            const float a = .068f, r = .0165f;
            Prism(chrome, m, a, r, 0f, .004f, false, true);
            Prism(body, m, a - .0005f, r - .0004f, .004f, .05f, false, false);
            Prism(chrome, m, a, r + .0002f, .05f, .064f, true, false);
            // knobs: rewind (left), wind with speed dial and release button (right)
            Lathe(chrome, 0, Poly(new Vector2(0, .064f), new Vector2(.0075f, .064f), new Vector2(.0075f, .0715f), new Vector2(.0068f, .073f), new Vector2(0, .073f)), 20, 6, new Options { M = m * Matrix4x4.Translate(new Vector3(-.046f, 0, 0)) }, 0f);
            Lathe(chrome, 0, Poly(new Vector2(0, .064f), new Vector2(.0115f, .064f), new Vector2(.0115f, .0665f), new Vector2(.0085f, .0668f), new Vector2(.0085f, .0745f), new Vector2(.0078f, .0755f), new Vector2(.0025f, .0755f), new Vector2(.0025f, .078f), new Vector2(0, .078f)), 22, 10, new Options { M = m * Matrix4x4.Translate(new Vector3(.044f, 0, 0)) }, 0f);
            BoxM(chrome, 0, m, new Vector3(-.012f, .0655f, 0), new Vector3(.022f, .003f, .018f), Quaternion.identity);
            BoxM(black, 0, m, new Vector3(.044f, .0668f, -.011f), new Vector3(.002f, .0005f, .003f), Quaternion.identity); // speed index
            // front windows (rangefinder ×2, finder)
            foreach (var wx in new[] { -.05f, .012f })
            {
                Lathe(chrome, 0, Poly(new Vector2(0, 0), new Vector2(.0055f, 0), new Vector2(.0055f, .0008f), new Vector2(0, .0008f)), 16, 3, new Options { M = m * Matrix4x4.TRS(new Vector3(wx, .057f, -r - .0002f), Quaternion.Euler(-90, 0, 0), Vector3.one) }, 0f);
                Lathe(glass, 0, Poly(new Vector2(0, .0011f), new Vector2(.0042f, .0009f)), 16, 1, new Options { M = m * Matrix4x4.TRS(new Vector3(wx, .057f, -r - .0002f), Quaternion.Euler(-90, 0, 0), Vector3.one) }, 0f);
            }
            BoxM(glass, 0, m, new Vector3(-.024f, .057f, -r - .0004f), new Vector3(.009f, .006f, .001f), Quaternion.identity);
            // lens: mount, collapsible tube, front barrel with focusing tab, glass
            var lensM = m * Matrix4x4.TRS(new Vector3(.002f, .029f, -r), Quaternion.Euler(-90, 0, 0), Vector3.one);
            Lathe(chrome, 0, Poly(new Vector2(0, 0), new Vector2(.0185f, 0), new Vector2(.0185f, .004f), new Vector2(.0128f, .0042f), new Vector2(.0128f, .022f),
                new Vector2(.0147f, .0222f), new Vector2(.0147f, .0322f), new Vector2(.0112f, .033f), new Vector2(.0112f, .0335f)), 28, 14, new Options { M = lensM }, 0f);
            Lathe(black, 0, Poly(new Vector2(.0112f, .0333f), new Vector2(.0104f, .0333f), new Vector2(.0104f, .031f)), 24, 2, new Options { M = lensM }, 0f);
            Lathe(glass, 0, Poly(new Vector2(0, .0322f), new Vector2(.0104f, .031f)), 24, 3, new Options { M = lensM }, 0f);
            BoxM(chrome, 0, lensM, new Vector3(.016f, .026f, 0), new Vector3(.008f, .004f, .003f), Quaternion.identity);
            // ever-ready case: bottom half and the strap
            var caseMat = k.B("CamCase", LeatherBrown);
            Prism(caseMat, m, a + .004f, r + .004f, -.004f, .03f, false, true, true);
            Ribbon(caseMat, 0, new[] { new Vector3(a + .003f, .026f, 0), new Vector3(a + .03f, .004f, .01f), new Vector3(a + .12f, -.002f, .05f), new Vector3(a + .2f, -.002f, -.02f), new Vector3(a + .26f, -.002f, .06f) }, .014f, .0025f, Vector3.up, m, 16);
            Ribbon(caseMat, 0, new[] { new Vector3(-a - .003f, .026f, 0), new Vector3(-a - .03f, .004f, -.01f), new Vector3(-a - .1f, -.002f, -.06f), new Vector3(-a - .16f, -.002f, .01f) }, .014f, .0025f, Vector3.up, m, 12);
        }

        /// <summary>Chrome tube flashlight (Dyatlov's was Chinese-made), lying along +Y before <paramref name="m"/>.</summary>
        static void Flashlight(Kit k, Matrix4x4 m)
        {
            Lathe(k.B("CamChrome", ChromeMat), 0, Poly(new Vector2(0, 0), new Vector2(.013f, 0), new Vector2(.0165f, .004f), new Vector2(.0165f, .11f), new Vector2(.019f, .116f),
                new Vector2(.0235f, .14f), new Vector2(.0235f, .152f), new Vector2(.02f, .153f)), 24, 16, new Options { M = m }, 0f);
            Lathe(k.B("CamGlass", GlassMat), 0, Poly(new Vector2(0, .1545f), new Vector2(.02f, .153f)), 24, 2, new Options { M = m }, 0f);
            BoxM(k.B("CamBlack", SteelMat), 0, m, new Vector3(0, .07f, -.017f), new Vector3(.007f, .018f, .004f), Quaternion.identity);
        }

        /// <summary>The group diary (a general notebook in an oilcloth cover) with a pencil.</summary>
        static void Diary(Kit k, Matrix4x4 m)
        {
            var cover = k.B("DiaryCover", LeatherGreen);
            var outline = new Vector2[16];
            for (int i = 0; i < 16; i++) { var p = Stadium(i / 16f, .085f, .006f); outline[i] = new Vector2(p.x, p.z * 16f); }
            var rect = new[] { new Vector2(-.085f, -.105f), new Vector2(.085f, -.105f), new Vector2(.085f, .105f), new Vector2(-.085f, .105f) };
            Extrude(cover, 0, rect, p => .0018f, m * Matrix4x4.TRS(new Vector3(0, .001f, 0), Quaternion.Euler(90, 0, 0), Vector3.one));
            Extrude(cover, 0, rect, p => .0018f, m * Matrix4x4.TRS(new Vector3(.001f, .0155f, -.001f), Quaternion.Euler(90, 2, 0), Vector3.one));
            BoxM(k.B("DiaryPages", Paper), 0, m, new Vector3(.002f, .0083f, 0), new Vector3(.164f, .0125f, .204f), Quaternion.identity);
            Sweep(k.B("Pencil", SkiWood), 0, new[] { new Vector3(.03f, .0205f, -.07f), new Vector3(.07f, .0205f, .07f) }, (t, a) => .0038f, 6, 1, new Options { M = m }, true, false);
            Sweep(k.B("Pencil", SkiWood), 0, new[] { new Vector3(.07f, .0205f, .07f), new Vector3(.076f, .0205f, .09f) }, (t, a) => Mathf.Lerp(.0038f, .0006f, t), 6, 1, new Options { M = m }, false, true);
        }
    }
}
