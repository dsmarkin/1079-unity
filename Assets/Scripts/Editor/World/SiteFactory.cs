using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Hand-authored (in code) reconstructions of the event sites, dimensioned from <see cref="Sites"/>. Each becomes a prefab in
    /// Resources/World/Prefabs/Sites; the runtime places them by coordinates. Pivot = ground point, +Z = the site's reference direction.</summary>
    public static class SiteFactory
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Sites";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Sites";
        static int meshCounter;

        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            Directory.CreateDirectory(MeshDir);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string meshName = $"{parent.root.name}_{name}_{meshCounter++}";
            var mesh = mb.ToMesh(meshName);
            string path = $"{MeshDir}/{meshName}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }

        static GameObject Save(GameObject root)
        {
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Soft snow mound (flattened, 10×6 sphere) — drifts and pit walls.</summary>
        static void Mound(MeshBuilder mb, int s, Vector3 c, Vector3 size, int seed)
        {
            var rnd = new System.Random(seed);
            const int U = 12, V = 6;
            var idx = new int[U + 1, V + 1];
            for (int i = 0; i <= U; i++)
                for (int j = 0; j <= V; j++)
                {
                    float th = i / (float)U * Mathf.PI * 2, ph = j / (float)V * Mathf.PI * .5f;
                    var n = new Vector3(Mathf.Cos(th) * Mathf.Cos(ph), Mathf.Sin(ph), Mathf.Sin(th) * Mathf.Cos(ph));
                    float wob = 1f + ((float)rnd.NextDouble() - .5f) * .12f * (j < V ? 1 : 0);
                    var p = c + new Vector3(n.x * size.x, n.y * size.y, n.z * size.z) * wob;
                    if (j == 0) p.y -= .4f; // skirt into the ground
                    idx[i, j] = mb.Vert(p, new Vector3(n.x / size.x, n.y / size.y, n.z / size.z).normalized, new Vector2(p.x + p.z, p.y) * .5f);
                }
            for (int i = 0; i < U; i++)
                for (int j = 0; j < V; j++)
                {
                    // theta runs toward +z at i+1 (Sin), phi upward at j+1: numeric Cross points outward for (i,j),(i,j+1),(i+1,j)
                    mb.Tri(s, idx[i, j], idx[i, j + 1], idx[i + 1, j]);
                    mb.Tri(s, idx[i + 1, j], idx[i, j + 1], idx[i + 1, j + 1]);
                }
        }

        static void SkiPole(MeshBuilder wood, MeshBuilder metal, Vector3 foot, Vector3 top)
        {
            wood.Tube(0, foot, top, .011f, .009f, 5, 1f);
            var ring = foot + (top - foot).normalized * .12f;
            metal.Tube(0, ring - Vector3.up * .004f, ring + Vector3.up * .004f, .055f, .055f, 8, 1f, 0, true);
        }

        /// <summary>Canvas, skis under the floor, ski-pole stands and guys, middle stand, ice axe — shared by the slope tent and the forest camp.
        /// <paramref name="skiRow"/> = centre of each ski row from the middle (1.07 keeps the skis under the floor; larger pushes the tips out at the ends).</summary>
        static void TentBody(Transform t, float skiRow)
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight, wall = Hr - Mathf.Sqrt(Sites.Tent.SlopeLength * Sites.Tent.SlopeLength - W * W / 4);
            // 8 pairs of skis under the floor: two rows along the tent
            var skis = new MeshBuilder(1);
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 8; k++)
                {
                    float x = -W / 2 + .12f + k * (W - .24f) / 7f, z = (row == 0 ? -1 : 1) * skiRow;
                    skis.Box(0, new Vector3(x, .012f, z), new Vector3(.075f, .022f, 2.05f), Quaternion.Euler(0, (k % 2) * 1.5f, 0), 2f);
                    skis.Box(0, new Vector3(x, .03f, z + 1.02f * (row == 0 ? -1 : 1)), new Vector3(.07f, .02f, .12f), Quaternion.Euler((row == 0 ? 25 : -25), 0, 0), 2f); // curled tips
                }
            Part(t, "SkisUnderFloor", skis, Materials.Ski);

            // canvas: floor, side walls, roof with slight sag, gable ends
            var cv = new MeshBuilder(2); // 0 canvas, 1 sheet
            float y0 = .04f;
            cv.Quad(0, new Vector3(-W / 2, y0, -L / 2), new Vector3(-W / 2, y0, L / 2), new Vector3(W / 2, y0, L / 2), new Vector3(W / 2, y0, -L / 2), Vector2.zero, new Vector2(0, L), new Vector2(W, L), new Vector2(W, 0));
            const int seg = 6;
            for (int i = 0; i < seg; i++)
            {
                float z0 = -L / 2 + L * i / seg, z1 = -L / 2 + L * (i + 1) / seg;
                float sag0 = .06f * Mathf.Sin(Mathf.PI * i / seg), sag1 = .06f * Mathf.Sin(Mathf.PI * (i + 1) / seg);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 wb0 = new Vector3(side * W / 2, y0, z0), wb1 = new Vector3(side * W / 2, y0, z1);
                    Vector3 wt0 = new Vector3(side * (W / 2 - .02f), wall - sag0 * .5f, z0), wt1 = new Vector3(side * (W / 2 - .02f), wall - sag1 * .5f, z1);
                    Vector3 r0 = new Vector3(0, Hr - sag0, z0), r1 = new Vector3(0, Hr - sag1, z1);
                    if (side > 0)
                    {
                        cv.Quad(0, wb0, wb1, wt1, wt0, new Vector2(z0, 0), new Vector2(z1, 0), new Vector2(z1, wall), new Vector2(z0, wall), true);
                        cv.Quad(0, wt0, wt1, r1, r0, new Vector2(z0, wall), new Vector2(z1, wall), new Vector2(z1, 1.6f), new Vector2(z0, 1.6f), true);
                    }
                    else
                    {
                        cv.Quad(0, wb1, wb0, wt0, wt1, new Vector2(z1, 0), new Vector2(z0, 0), new Vector2(z0, wall), new Vector2(z1, wall), true);
                        cv.Quad(0, wt1, wt0, r0, r1, new Vector2(z1, wall), new Vector2(z0, wall), new Vector2(z0, 1.6f), new Vector2(z1, 1.6f), true);
                    }
                }
            }
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * L / 2;
                Vector3 bl = new Vector3(-W / 2, y0, z), br = new Vector3(W / 2, y0, z), tl = new Vector3(-W / 2 + .02f, wall, z), tr = new Vector3(W / 2 - .02f, wall, z), ap = new Vector3(0, Hr, z);
                cv.Quad(0, bl, br, tr, tl, new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, wall), new Vector2(0, wall), true);
                int a = cv.Vert(tl, Vector3.forward * end, new Vector2(0, wall)), b = cv.Vert(tr, Vector3.forward * end, new Vector2(W, wall)), c = cv.Vert(ap, Vector3.forward * end, new Vector2(W / 2, Hr));
                cv.Tri(0, a, c, b); cv.Tri(0, a, b, c);
            }
            // entrance (+Z): sleeve draped with a white sheet
            cv.Quad(1, new Vector3(-.35f, y0, L / 2 + .03f), new Vector3(.35f, y0, L / 2 + .03f), new Vector3(.22f, .92f, L / 2 + .05f), new Vector3(-.22f, .92f, L / 2 + .05f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            // back end (-Z): round sleeve opening
            cv.Tube(0, new Vector3(0, .7f, -L / 2), new Vector3(.02f, .66f, -L / 2 - .28f), .14f, .12f, 10, 2f);
            Part(t, "Canvas", cv, Materials.Canvas, Materials.Sheet);

            // stands and guys: crossed ski poles at both ends, ropes to ski poles in the snow; a pair of skis as the middle stand
            var wood = new MeshBuilder(1); var metal = new MeshBuilder(1); var rope = new MeshBuilder(1);
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * (L / 2 + .06f);
                SkiPole(wood, metal, new Vector3(-.32f, -.12f, z), new Vector3(.08f, Hr + .22f, z));
                SkiPole(wood, metal, new Vector3(.32f, -.12f, z), new Vector3(-.08f, Hr + .22f, z));
                Vector3 anchor = new Vector3(0, .95f, end * (L / 2 + 1.7f));
                SkiPole(wood, metal, anchor + new Vector3(0, -1.05f, 0), anchor + new Vector3(0, .2f, -end * .12f));
                rope.Tube(0, new Vector3(0, Hr + .02f, end * L / 2), anchor + Vector3.up * .15f, .006f, .006f, 4);
                foreach (int side in new[] { -1, 1 })
                {
                    var stake = new Vector3(side * (W / 2 + .9f), .5f, end * (L / 2 - .3f));
                    SkiPole(wood, metal, stake + new Vector3(0, -.62f, 0), stake + new Vector3(side * .1f, .55f, 0));
                    rope.Tube(0, new Vector3(side * W / 2, wall, end * (L / 2 - .3f)), stake + Vector3.up * .3f, .005f, .005f, 4);
                }
            }
            Part(t, "SkiPoles", wood, Materials.Ski);
            Part(t, "PoleBaskets", metal, Materials.Metal);
            Part(t, "Guys", rope, Materials.Rope);
            var mid = new MeshBuilder(1);
            mid.Box(0, new Vector3(.05f, Hr / 2, 0), new Vector3(.07f, Hr, .02f), Quaternion.identity, 2);
            mid.Box(0, new Vector3(-.05f, Hr / 2, 0), new Vector3(.07f, Hr, .02f), Quaternion.identity, 2);
            Part(t, "MiddleStandSkis", mid, Materials.Ski);

            // ice axe at the entrance (found there on 26 Feb)
            var axeWood = new MeshBuilder(1); var axeHead = new MeshBuilder(1);
            Vector3 foot = new Vector3(.75f, -.25f, L / 2 + .45f), top = new Vector3(.8f, .62f, L / 2 + .42f);
            axeWood.Tube(0, foot, top, .016f, .016f, 6);
            axeHead.Box(0, top + Vector3.up * .02f, new Vector3(.03f, .035f, .3f), Quaternion.Euler(0, 0, 3), 1);
            Part(t, "IceAxeShaft", axeWood, Materials.Wood);
            Part(t, "IceAxeHead", axeHead, Materials.Metal);

        }

        // ------------------------------------------------------------------ tent
        /// <summary>The tent on the evening of 1 Feb 1959. Local frame: +Z = entrance end (toward the pass), +X = downslope side (where the cuts were made).</summary>
        public static GameObject Tent()
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight, wall = Hr - Mathf.Sqrt(Sites.Tent.SlopeLength * Sites.Tent.SlopeLength - W * W / 4);
            var root = new GameObject("Site_Tent_1959");
            var t = root.transform;

            // levelled platform: the snow bank below, the cut wall above (uphill = -X)
            var snow = new MeshBuilder(1);
            snow.Box(0, new Vector3(0, -.14f, 0), new Vector3(W + .5f, .3f, L + .5f), Quaternion.identity, .5f);
            for (int k = 0; k < 6; k++) Mound(snow, 0, new Vector3(W / 2 + .55f, -.25f, -2.5f + k * 1.0f), new Vector3(.95f, .32f, .85f), 30 + k); // spoil on the downslope side
            snow.Box(0, new Vector3(-W / 2 - .75f, Sites.Tent.CutDepth / 2 - .1f, 0), new Vector3(.6f, Sites.Tent.CutDepth + .2f, Sites.Tent.CutLength + 1.2f), Quaternion.Euler(0, 0, -12), .5f);
            for (int k = 0; k < 5; k++) Mound(snow, 0, new Vector3(-W / 2 - 1.2f, .15f, -2.2f + k * 1.1f), new Vector3(.9f, .35f, .8f), 10 + k);
            Mound(snow, 0, new Vector3(-.3f, .05f, -L / 2 - .9f), new Vector3(1.4f, .3f, .7f), 20);
            Part(t, "SnowPlatform", snow, Materials.Snow);

            TentBody(t, 1.07f);

            // search state (26 Feb): cuts on the downslope roof, snow on the northern part, flashlight on the roof — disabled by default
            var search = new GameObject("SearchState_1959-02-26"); search.transform.SetParent(t, false);
            var cuts = new MeshBuilder(1);
            float[] zc = { -1.2f, 0.1f, 1.25f };
            for (int i = 0; i < 3; i++)
            {
                float len = Sites.Tent.Cuts[i];
                Vector3 c0 = Vector3.Lerp(new Vector3(W / 2 - .02f, wall, zc[i]), new Vector3(0, Hr, zc[i]), .35f) + new Vector3(.012f, .01f, 0);
                Vector3 dirUp = (new Vector3(-W / 2, Hr - wall, 0)).normalized;
                Vector3 p0 = c0 - dirUp * len * .5f, p1 = c0 + dirUp * len * .5f, side = Vector3.forward * .025f;
                cuts.Quad(0, p0 - side, p1 - side, p1 + side, p0 + side, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            }
            Part(search.transform, "Cuts", cuts, Materials.Charcoal);
            var drift = new MeshBuilder(1);
            Mound(drift, 0, new Vector3(0, Hr * .55f, -L / 4), new Vector3(W * .6f, Sites.Tent.SnowOnTentFound * 3, L * .3f), 31);
            Part(search.transform, "SnowOnTent", drift, Materials.Snow);
            var torch = new MeshBuilder(1);
            torch.Tube(0, new Vector3(-.2f, Hr + .02f, .6f), new Vector3(-.2f, Hr + .02f, .78f), .018f, .022f, 8, 1, 0, true);
            Part(search.transform, "Flashlight", torch, Materials.Metal);
            search.SetActive(false);

            AddBoxCollider(root, new Vector3(0, Hr / 2, 0), new Vector3(W, Hr, L));
            return Save(root);
        }

        static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size) { var c = go.AddComponent<BoxCollider>(); c.center = center; c.size = size; }

        // ------------------------------------------------------------------ labaz
        public static GameObject Labaz(GameObject branchesModel, bool morning = false)
        {
            // morning of 1 Feb: pit dug and floored, the goods laid out beside it, cover material stacked on the other side, no marker yet
            var root = new GameObject(morning ? "Site_Labaz_1959_Morning" : "Site_Labaz_1959");
            Vector3 goods = morning ? new Vector3(0, 0, 2.5f) : Vector3.zero, cover = morning ? new Vector3(.2f, -.28f, -2.6f) : Vector3.zero;
            var t = root.transform;
            float Lf = Sites.Labaz.FloorLength, Wf = Sites.Labaz.FloorWidth, D = Sites.Labaz.PitDepth;
            var snow = new MeshBuilder(1);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2;
                Mound(snow, 0, new Vector3(Mathf.Cos(a) * (Lf * .5f + .75f), -.1f, Mathf.Sin(a) * (Wf * .5f + .75f)), new Vector3(.9f, D * .75f, .8f), 40 + k);
            }
            Part(t, "SnowPit", snow, Materials.Snow);
            var floor = new MeshBuilder(1);
            for (int k = 0; k < 7; k++) floor.Box(0, new Vector3(-Lf / 2 + .11f + k * (Lf - .22f) / 6, .01f, 0), new Vector3(.24f, .012f, Wf * (.92f + .08f * (k % 2))), Quaternion.Euler(0, (k - 3) * 2f, 0), 2);
            Part(t, "BirchBarkFloor", floor, Materials.BirchCardboard);

            // food: canvas bags and boxes (≈55 kg), mandolin, spare boots
            var bags = new MeshBuilder(1);
            for (int k = 0; k < 6; k++) Mound(bags, 0, new Vector3(-.55f + k * .22f, .05f, (k % 2 == 0 ? -.22f : .2f)), new Vector3(.14f, .22f, .16f), 60 + k);
            Off(Part(t, "FoodBags", bags, Materials.Cloth("bag", new Color(.63f, .6f, .5f))), goods);
            var boxes = new MeshBuilder(1);
            boxes.Box(0, new Vector3(.45f, .12f, -.2f), new Vector3(.34f, .24f, .26f), Quaternion.Euler(0, 8, 0), 3);
            boxes.Box(0, new Vector3(.47f, .09f, .18f), new Vector3(.3f, .18f, .24f), Quaternion.Euler(0, -5, 0), 3);
            Off(Part(t, "Boxes", boxes, Materials.Cloth("carton", new Color(.62f, .5f, .36f))), goods);
            var mandolin = new MeshBuilder(1);
            Mound(mandolin, 0, new Vector3(.1f, .04f, .36f), new Vector3(.16f, .08f, .12f), 70);
            mandolin.Box(0, new Vector3(.1f, .09f, .36f + .26f), new Vector3(.045f, .02f, .34f), Quaternion.identity, 4);
            Off(Part(t, "Mandolin", mandolin, Materials.Get("MandolinVarnish", new Color(.45f, .22f, .1f), smoothness: .7f)), goods);
            var boots = new MeshBuilder(1);
            boots.Tube(0, new Vector3(-.2f, .02f, .38f), new Vector3(-.2f, .02f, .38f) + new Vector3(.4f, .05f, 0), .06f, .055f, 8, 3, 0, true);
            boots.Tube(0, new Vector3(-.2f, .02f, .5f), new Vector3(-.2f, .02f, .5f) + new Vector3(.4f, .05f, .02f), .06f, .055f, 8, 3, 0, true);
            Off(Part(t, "FeltBoots", boots, Materials.Cloth("felt", new Color(.2f, .19f, .18f))), goods);

            // cover: firewood, boards, fir branches
            var logs = new MeshBuilder(1);
            for (int k = 0; k < 5; k++) logs.Tube(0, new Vector3(-.85f, .3f + (k % 2) * .06f, -.45f + k * .22f), new Vector3(.85f, .32f, -.42f + k * .22f), .07f, .065f, 7, 1, 0, true);
            Off(Part(t, "Firewood", logs, Materials.Bark), cover);
            var planks = new MeshBuilder(1);
            planks.Box(0, new Vector3(-.1f, .42f, -.15f), new Vector3(1.8f, .02f, .2f), Quaternion.Euler(0, 6, 2), .5f);
            planks.Box(0, new Vector3(.05f, .43f, .2f), new Vector3(1.7f, .02f, .18f), Quaternion.Euler(0, -4, -1), .5f);
            Off(Part(t, "Boards", planks, Materials.Planks), cover);
            var branches = new MeshBuilder(3);
            var rnd = new System.Random(77);
            for (int k = 0; k < 9; k++)
            {
                var a = new Vector3(-.9f + (float)rnd.NextDouble() * 1.8f, .47f, -.6f + (float)rnd.NextDouble() * 1.2f);
                var b = a + Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0) * Vector3.forward * (.8f + (float)rnd.NextDouble() * .5f);
                FirBranch(branches, a, b, .6f);
            }
            Off(Part(t, "FirBranches", branches, Materials.Bark, Materials.Find("NeedlesFir"), Materials.Find("SnowOnBranches")), cover);
            if (branchesModel != null)
            {
                var brush = Object.Instantiate(branchesModel, t); brush.name = "Brushwood_PolyHaven";
                brush.transform.localPosition = new Vector3(1.6f, 0, .7f) + cover; brush.transform.localRotation = Quaternion.Euler(0, 35, 0);
            }

            if (morning) return Save(root);
            // marker: one ski stuck in the snow with a torn gaiter
            var ski = new MeshBuilder(1);
            ski.Box(0, new Vector3(-1.2f, Sites.Labaz.MarkerSkiLength / 2 - .35f, -.9f), new Vector3(.075f, Sites.Labaz.MarkerSkiLength, .022f), Quaternion.Euler(3, 0, -4), 2);
            Part(t, "MarkerSki", ski, Materials.Ski);
            var gaiter = new MeshBuilder(1);
            gaiter.Tube(0, new Vector3(-1.13f, 1.3f, -.9f), new Vector3(-1.1f, 1.05f, -.88f), .07f, .09f, 8, 3);
            gaiter.Quad(0, new Vector3(-1.1f, 1.05f, -.84f), new Vector3(-1.08f, 1.05f, -.96f), new Vector3(-1.02f, .78f, -.98f), new Vector3(-1.05f, .8f, -.82f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "TornGaiter", gaiter, Materials.Cloth("gaiter", new Color(.23f, .3f, .45f)));
            return Save(root);
        }

        // ------------------------------------------------------------------ night camp of 31 Jan, morning of 1 Feb
        static GameObject Off(GameObject go, Vector3 offset) { go.transform.localPosition += offset; return go; }

        /// <summary>Canvas rucksack of the 1950s: soft sack, top flap, two leather shoulder straps, two side pockets. Local +Z = back (straps).</summary>
        static void Rucksack(MeshBuilder sack, MeshBuilder leather, Vector3 at, float yaw, float size, int seed)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Mound(sack, 0, at + Vector3.up * .02f, new Vector3(.19f, .5f, .14f) * size, seed);
            Mound(sack, 0, at + q * new Vector3(0, .44f, .01f) * size, new Vector3(.2f, .09f, .15f) * size, seed + 1); // flap
            foreach (int side in new[] { -1, 1 })
            {
                Mound(sack, 0, at + q * new Vector3(side * .19f, .12f, 0) * size, new Vector3(.06f, .2f, .09f) * size, seed + 2 + side);
                Vector3 top = at + q * new Vector3(side * .07f, .43f, .14f) * size, bottom = at + q * new Vector3(side * .12f, .08f, .15f) * size;
                leather.Tube(0, top, Vector3.Lerp(top, bottom, .5f) + q * new Vector3(0, 0, .07f) * size, .012f, .012f, 4, 1);
                leather.Tube(0, Vector3.Lerp(top, bottom, .5f) + q * new Vector3(0, 0, .07f) * size, bottom, .012f, .012f, 4, 1);
            }
            leather.Box(0, at + q * new Vector3(0, .36f, -.14f) * size, new Vector3(.03f, .12f, .01f) * size, q, 1); // flap strap
        }

        /// <summary>Tin bucket with a wire bail, filled with snow being melted. Open top at +Y.</summary>
        static void Bucket(MeshBuilder tin, MeshBuilder snow, Vector3 bottom, float height, float rTop, float rBottom)
        {
            tin.Tube(0, bottom + Vector3.up * height, bottom, rTop, rBottom, 14, 1, 0, true);
            tin.Tube(0, bottom + Vector3.up * .01f, bottom + Vector3.up * (height - .005f), rBottom - .004f, rTop - .004f, 14, 1); // inner wall, reversed
            var c = bottom + Vector3.up * height * .8f; float r = Mathf.Lerp(rBottom, rTop, .8f) - .004f;
            for (int i = 0; i < 12; i++)
            {
                float a0 = i / 12f * Mathf.PI * 2, a1 = (i + 1) / 12f * Mathf.PI * 2;
                int k0 = snow.Vert(c, Vector3.up, Vector2.zero), k1 = snow.Vert(c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r, Vector3.up, Vector2.zero), k2 = snow.Vert(c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, Vector3.up, Vector2.zero);
                snow.Tri(0, k0, k1, k2);
            }
            Vector3 prev = bottom + new Vector3(-rTop, height, 0);
            for (int i = 1; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI;
                var p = bottom + new Vector3(-Mathf.Cos(a) * rTop, height + Mathf.Sin(a) * rTop * 1.1f, 0);
                tin.Tube(0, prev, p, .003f, .003f, 4, 1); prev = p;
            }
        }

        /// <summary>Valenok: shaft plus foot, as a tube pair; <paramref name="down"/> = shaft direction.</summary>
        static void FeltBoot(MeshBuilder felt, Vector3 heel, Vector3 down, Vector3 toe)
        {
            felt.Tube(0, heel, heel + down * .42f, .055f, .06f, 8, 1, 0, true);
            felt.Tube(0, heel, heel + toe * .24f, .05f, .045f, 8, 1, 0, true);
        }

        /// <summary>The group's tent on its forest pad, morning of 1 Feb. Local: +Z = entrance (toward the fire), -X = uphill.
        /// Same tent and pitching as on the slope (canvas, 8 pairs of skis under the floor, ski-pole stands, middle stand of one pair of skis);
        /// the stove is hung inside and its pipe leaves through the rear sleeve with the ring of raw bars. Outside: nine rucksacks being packed on
        /// fir branches, the eight free poles and the spare pair of skis in the snow.</summary>
        public static void Camp31Tent()
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight;
            var root = new GameObject("Site_Camp_31Jan_Tent");
            var t = root.transform;

            var pad = new MeshBuilder(1);
            pad.Box(0, new Vector3(0, -.4f, .5f), new Vector3(W + 1.6f, .82f, L + 3.2f), Quaternion.identity, .5f);
            for (int k = 0; k < 26; k++)
            {
                float a = k / 26f * Mathf.PI * 2;
                if (Mathf.Sin(a) > .8f) continue; // opening toward the fire
                Mound(pad, 0, new Vector3(Mathf.Cos(a) * (W / 2 + 1.8f), -.15f, .5f + Mathf.Sin(a) * (L / 2 + 2.2f)), new Vector3(.62f, Sites.Camp31.PadDepth * .8f, .55f), 300 + k);
            }
            Part(t, "TrampledPad", pad, Materials.Snow);

            TentBody(t, 1.25f); // ski tips show at both ends

            // stove pipe: out of the rear sleeve, horizontal, then a short rise; ring of raw bars round the sleeve; two cut sticks hold the pipe
            var pipe = new MeshBuilder(1);
            Vector3 p0 = new Vector3(0, .7f, -L / 2 + .3f), p1 = new Vector3(0, .72f, -L / 2 - 1.05f), p2 = new Vector3(0, 1.32f, -L / 2 - 1.15f);
            pipe.Tube(0, p0, p1, .05f, .05f, 10, 1); pipe.Tube(0, p1 + Vector3.back * .02f, p2, .05f, .048f, 10, 1);
            for (int k = 1; k < 3; k++) pipe.Tube(0, Vector3.Lerp(p0, p1, k / 3f) - Vector3.forward * .02f, Vector3.Lerp(p0, p1, k / 3f) + Vector3.forward * .02f, .056f, .056f, 10, 1, 0, true); // joints
            Part(t, "StovePipe", pipe, Materials.Metal);
            var ring = new MeshBuilder(1);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2;
                var c = new Vector3(Mathf.Cos(a) * .15f, .7f + Mathf.Sin(a) * .15f, -L / 2 - .22f);
                var tan = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0) * Sites.Camp31.RingBarLength * .5f;
                ring.Tube(0, c - tan * .6f, c + tan * .6f, .018f, .018f, 5, 1, 0, true);
            }
            var sticks = new MeshBuilder(1);
            sticks.Tube(0, new Vector3(-.3f, -.2f, -L / 2 - .9f), new Vector3(.12f, .82f, -L / 2 - .92f), .02f, .015f, 5, 1);
            sticks.Tube(0, new Vector3(.3f, -.2f, -L / 2 - .9f), new Vector3(-.12f, .82f, -L / 2 - .92f), .02f, .015f, 5, 1);
            Part(t, "PipeRingBars", ring, Materials.Wood);
            Part(t, "PipeSticks", sticks, Materials.Bark);
            var smoke = new GameObject("StovePipeSmoke"); smoke.transform.SetParent(t, false); smoke.transform.localPosition = p2 + Vector3.up * .05f;

            // fir branches in front of the entrance, rucksacks on them
            var mat = new MeshBuilder(3);
            var rnd = new System.Random(311);
            for (int k = 0; k < 22; k++)
            {
                var a = new Vector3(-1.2f + (float)rnd.NextDouble() * 2.4f, .02f, L / 2 + .5f + (float)rnd.NextDouble() * 1.6f);
                var b = a + Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0) * Vector3.forward * (.7f + (float)rnd.NextDouble() * .4f);
                FirBranch(mat, a, b, .55f);
            }
            Part(t, "FirMat", mat, Materials.Bark, Materials.Find("NeedlesFir"), Materials.Find("SnowOnBranches"));

            var khaki = new MeshBuilder(1); var grey = new MeshBuilder(1); var brown = new MeshBuilder(1); var leather = new MeshBuilder(1);
            var sacks = new[] { khaki, grey, khaki, brown, grey, khaki, brown, khaki, grey };
            for (int i = 0; i < Sites.Camp31.Rucksacks; i++)
            {
                bool back = i >= 5;
                float x = back ? -.75f + (i - 5) * .5f : -1.0f + i * .5f, z = L / 2 + (back ? 1.95f : 1.2f);
                Rucksack(sacks[i], leather, new Vector3(x, .03f, z), 180 + (i * 37 % 30) - 15, .95f + (i % 3) * .05f, 400 + i * 7);
            }
            // Slobodin's rucksack as on the 31 Jan photo: felt boots and an axe tied on top (the first in the front row)
            var felt = new MeshBuilder(1);
            Vector3 top0 = new Vector3(-1.0f, .03f + .52f, L / 2 + 1.2f);
            FeltBoot(felt, top0 + new Vector3(-.12f, .02f, .02f), Vector3.right, Vector3.back);
            FeltBoot(felt, top0 + new Vector3(-.12f, .1f, -.03f), Vector3.right, Vector3.back);
            var axe = new MeshBuilder(1); var axeHead = new MeshBuilder(1);
            axe.Tube(0, top0 + new Vector3(-.05f, -.25f, .2f), top0 + new Vector3(.02f, .28f, .19f), .016f, .016f, 6, 1);
            axeHead.Box(0, top0 + new Vector3(.02f, .28f, .19f), new Vector3(.03f, .09f, .16f), Quaternion.Euler(0, 90, 0), 1);
            Part(t, "RucksacksKhaki", khaki, Materials.Cloth("ruck_khaki", new Color(.44f, .43f, .3f)));
            Part(t, "RucksacksGrey", grey, Materials.Cloth("ruck_grey", new Color(.42f, .44f, .42f)));
            Part(t, "RucksacksBrown", brown, Materials.Cloth("ruck_brown", new Color(.45f, .36f, .26f)));
            Part(t, "RucksackStraps", leather, Materials.Cloth("leather", new Color(.27f, .17f, .1f)));
            Part(t, "SlobodinFeltBoots", felt, Materials.Cloth("felt", new Color(.2f, .19f, .18f)));
            Part(t, "SlobodinAxeHandle", axe, Materials.Wood);
            Part(t, "SlobodinAxeHead", axeHead, Materials.Metal);

            // eight free ski poles in pairs and the spare pair of skis, stuck in the snow left of the entrance
            var wood = new MeshBuilder(1); var baskets = new MeshBuilder(1);
            for (int k = 0; k < Sites.Camp31.PolesLeftFree; k++)
            {
                var foot = new Vector3(-W / 2 - .7f - (k % 2) * .08f, -.35f, L / 2 - .4f + (k / 2) * .45f);
                SkiPole(wood, baskets, foot, foot + new Vector3(-.05f + (k % 2) * .1f, 1.45f, .03f));
            }
            Part(t, "FreePoles", wood, Materials.Ski);
            Part(t, "FreePoleBaskets", baskets, Materials.Metal);
            var spare = new MeshBuilder(1);
            for (int k = 0; k < 2; k++)
            {
                var c = new Vector3(-W / 2 - 1.25f, .6f, L / 2 + 1.4f + k * .1f);
                spare.Box(0, c, new Vector3(.075f, 2.0f, .022f), Quaternion.Euler(3 - k * 5, 0, -3 + k * 4), 2);
                spare.Box(0, c + new Vector3(0, 1.02f, -.03f), new Vector3(.07f, .12f, .02f), Quaternion.Euler(-25, 0, 0), 2);
            }
            Part(t, "SpareSkis", spare, Materials.Ski);

            AddBoxCollider(root, new Vector3(0, Hr / 2, 0), new Vector3(W, Hr, L));
            Save(root);
        }

        /// <summary>Fire and kitchen: "костёр на брёвнах" — a raft of green logs on the snow (diary 31.01); two buckets on a crossbar over it,
        /// two pots, felt boots and mittens drying on sticks (the diary of 30.01 records burnt mittens and a quilted jacket), firewood from
        /// "frail damp firs", the saw and two of the three axes. The runtime adds the burning stack, the light and the smoke.</summary>
        public static void Camp31Fire()
        {
            var fire = new GameObject("Site_Camp_31Jan_Fire");
            var f = fire.transform;
            var ring = new MeshBuilder(1);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2;
                if (Mathf.Sin(a) < -.8f) continue; // path to the tent (local -Z)
                Mound(ring, 0, new Vector3(Mathf.Cos(a) * 2.7f, -.18f, Mathf.Sin(a) * 2.7f), new Vector3(.6f, .3f, .5f), 330 + k);
            }
            ring.Box(0, new Vector3(0, -.3f, 0), new Vector3(4.6f, .6f, 4.6f), Quaternion.identity, .5f);
            Part(f, "TrampledRing", ring, Materials.Snow);
            var raft = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
                raft.Tube(0, new Vector3(-.75f, .09f, -.4f + k * .2f), new Vector3(.75f, .09f + (k % 2) * .01f, -.38f + k * .2f), .095f, .085f, 8, 1, 0, true);
            Part(f, "GreenLogRaft", raft, Materials.Bark);
            var ash = new MeshBuilder(1);
            Mound(ash, 0, new Vector3(0, .15f, 0), new Vector3(.45f, .06f, .38f), 350);
            Part(f, "Ash", ash, Materials.Charcoal);

            // crossbar on two forked stakes, buckets on wire hooks, pots on the snow
            var stakes = new MeshBuilder(1);
            foreach (int sx in new[] { -1, 1 })
            {
                var foot = new Vector3(sx * 1.1f, -.3f, 0);
                stakes.Tube(0, foot, new Vector3(sx * 1.08f, 1.5f, 0), .03f, .025f, 6, 1);
                stakes.Tube(0, new Vector3(sx * 1.08f, 1.35f, 0), new Vector3(sx * 1.2f, 1.62f, .02f), .018f, .014f, 5, 1); // fork
            }
            stakes.Tube(0, new Vector3(-1.35f, 1.5f, 0), new Vector3(1.35f, 1.52f, 0), .028f, .024f, 6, 1);
            Part(f, "Crossbar", stakes, Materials.Bark);
            var tin = new MeshBuilder(1); var melt = new MeshBuilder(1);
            foreach (int sx in new[] { -1, 1 })
            {
                var b = new Vector3(sx * .32f, .88f, 0);
                Bucket(tin, melt, b, .26f, .15f, .12f);
                tin.Tube(0, b + new Vector3(0, .26f + .16f, 0), new Vector3(sx * .32f, 1.5f, 0), .003f, .003f, 4, 1);
            }
            Bucket(tin, melt, new Vector3(.95f, .02f, 1.35f), .16f, .12f, .11f);
            Bucket(tin, melt, new Vector3(1.25f, .02f, 1.2f), .14f, .1f, .095f);
            tin.Tube(0, new Vector3(1.25f, .17f, 1.2f), new Vector3(1.25f, .165f, 1.2f), .11f, .11f, 14, 1, 0, true); // lid
            Part(f, "BucketsAndPots", tin, Materials.Metal);
            Part(f, "SnowInBuckets", melt, Materials.Snow);

            // drying: felt boots upside down on sticks, mittens
            var sticks = new MeshBuilder(1); var felt = new MeshBuilder(1); var wool = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
            {
                float a = (-40 + k * 22) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Sin(a) * 1.55f, -.3f, -Mathf.Cos(a) * 1.55f);
                var top = new Vector3(Mathf.Sin(a) * 1.3f, 1.05f, -Mathf.Cos(a) * 1.3f);
                sticks.Tube(0, foot, top, .015f, .012f, 5, 1);
                if (k < 3) FeltBoot(felt, top + Vector3.up * .04f, (foot - top).normalized, new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
                else wool.Box(0, top + new Vector3(0, -.05f, 0), new Vector3(.11f, .2f, .04f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 10), 1);
            }
            wool.Box(0, new Vector3(-.9f, .95f, .6f), new Vector3(.1f, .18f, .04f), Quaternion.Euler(0, 30, -8), 1);
            sticks.Tube(0, new Vector3(-1.05f, -.3f, .75f), new Vector3(-.9f, 1.02f, .6f), .014f, .011f, 5, 1);
            Part(f, "DryingSticks", sticks, Materials.Bark);
            Part(f, "FeltBootsDrying", felt, Materials.Cloth("felt_grey", new Color(.36f, .34f, .31f)));
            Part(f, "Mittens", wool, Materials.Cloth("mitten", new Color(.5f, .18f, .14f)));

            // seats, firewood, chopping block with the large axe, the saw on a half-cut log, the small axe in its leather case
            var logs = new MeshBuilder(2);
            logs.Tube(0, new Vector3(-1.9f, .12f, -1.0f), new Vector3(-1.5f, .13f, 1.0f), .14f, .12f, 9, 1, 0, true);
            logs.Tube(0, new Vector3(-.9f, .12f, -1.95f), new Vector3(.9f, .13f, -1.85f), .13f, .12f, 9, 1, 0, true);
            for (int k = 0; k < 9; k++)
            {
                var a = new Vector3(2.0f, .07f + (k / 3) * .13f, -1.1f + (k % 3) * .15f);
                logs.Tube(0, a, a + new Vector3(.15f, .01f, .9f), .055f, .05f, 7, 1, 0, true);
                logs.Box(1, a + new Vector3(.15f, 0, .92f), new Vector3(.1f, .1f, .01f), Quaternion.Euler(0, 10, 0), .3f);
            }
            logs.Tube(0, new Vector3(1.7f, -.2f, 1.9f), new Vector3(1.7f, .32f, 1.9f), .19f, .18f, 10, 1, 0, false);
            logs.Cone(1, new Vector3(1.7f, .32f, 1.9f), .18f, .02f, 10);
            logs.Tube(0, new Vector3(-.4f, .1f, 2.2f), new Vector3(1.1f, .12f, 2.4f), .1f, .09f, 8, 1, 0, true); // log being sawn
            Part(f, "Firewood", logs, Materials.Bark, Materials.Wood);
            var steel = new MeshBuilder(1); var handles = new MeshBuilder(1); var caseM = new MeshBuilder(1);
            steel.Box(0, new Vector3(.35f, .26f, 2.28f), new Vector3(1.1f, .1f, .004f), Quaternion.Euler(0, -8, 80), 1); // crosscut saw blade on the log
            handles.Tube(0, new Vector3(-.22f, .2f, 2.2f), new Vector3(-.24f, .42f, 2.2f), .018f, .018f, 6, 1, 0, true);
            handles.Tube(0, new Vector3(.93f, .2f, 2.36f), new Vector3(.95f, .42f, 2.36f), .018f, .018f, 6, 1, 0, true);
            handles.Tube(0, new Vector3(1.6f, .33f, 1.85f), new Vector3(1.35f, .75f, 1.72f), .018f, .016f, 6, 1); // large axe in the block
            steel.Box(0, new Vector3(1.66f, .33f, 1.88f), new Vector3(.03f, .1f, .17f), Quaternion.Euler(0, 62, 60), 1);
            caseM.Box(0, new Vector3(2.1f, .5f, -.6f), new Vector3(.2f, .05f, .12f), Quaternion.Euler(0, 20, 0), 1);
            handles.Tube(0, new Vector3(2.1f, .5f, -.6f), new Vector3(2.35f, .52f, -.2f), .014f, .013f, 6, 1);
            Part(f, "SawAndAxeSteel", steel, Materials.Metal);
            Part(f, "Handles", handles, Materials.Wood);
            Part(f, "SmallAxeCase", caseM, Materials.Cloth("leather", new Color(.27f, .17f, .1f)));
            Save(fire);
        }

        /// <summary>A cut fir branch (лапник): stick plus needle cards; submeshes bark/needles/snow.</summary>
        static void FirBranch(MeshBuilder mb, Vector3 a, Vector3 b, float width)
        {
            mb.Tube(0, a, b, .018f, .006f, 4, 2);
            Vector3 along = b - a, side = Vector3.Cross(along.normalized, Vector3.up).normalized * width * .5f;
            mb.Quad(1, a - side * .3f, a + side * .3f, b + side, b - side, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), true, Vector3.up);
        }

        // ------------------------------------------------------------------ cedar and fire
        public static GameObject CedarSite(GameObject heroCedar, Vector2 towardTent)
        {
            var root = new GameObject("Site_Cedar_1959");
            var t = root.transform;
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(heroCedar, t);
            tree.transform.localPosition = Vector3.zero;
            Vector3 toTent = new Vector3(towardTent.x, 0, towardTent.y).normalized;
            Vector3 fire = toTent * Sites.Cedar.FireOffset;

            // fire pit melted into the snow
            var pit = new MeshBuilder(1);
            for (int k = 0; k < 9; k++) { float a = k / 9f * Mathf.PI * 2; Mound(pit, 0, fire + new Vector3(Mathf.Cos(a) * .85f, -.08f, Mathf.Sin(a) * .85f), new Vector3(.55f, .18f, .5f), 80 + k); }
            Part(t, "FirePitSnow", pit, Materials.Snow);
            var ash = new MeshBuilder(2);
            ash.Quad(0, fire + new Vector3(-.45f, -.12f, -.45f), fire + new Vector3(-.45f, -.12f, .45f), fire + new Vector3(.45f, -.12f, .45f), fire + new Vector3(.45f, -.12f, -.45f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            var rnd = new System.Random(1959);
            for (int k = 0; k < 7; k++)
            {
                // brands up to 80 mm thick, burnt through in the middle
                float ang = k / 7f * Mathf.PI * 2 + (float)rnd.NextDouble() * .4f;
                Vector3 inner = fire + new Vector3(Mathf.Cos(ang) * .08f, -.08f, Mathf.Sin(ang) * .08f), outer = fire + new Vector3(Mathf.Cos(ang) * (.45f + (float)rnd.NextDouble() * .35f), -.02f, Mathf.Sin(ang) * (.45f + (float)rnd.NextDouble() * .35f));
                ash.Tube(0, inner, Vector3.Lerp(inner, outer, .45f), .02f, .035f, 6, 2, 0, true);
                ash.Tube(1, Vector3.Lerp(inner, outer, .45f), outer, .038f, .04f, 6, 2, 0, true);
            }
            Part(t, "BurntBrands", ash, Materials.Charcoal, Materials.Bark);

            // cut spruce branches around (more than ten, cut with a knife) and dry twigs snapped off the cedar
            var cut = new MeshBuilder(3);
            for (int k = 0; k < 13; k++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2, r = 1.2f + (float)rnd.NextDouble() * 2.2f;
                var a = new Vector3(Mathf.Cos(ang) * r, .03f, Mathf.Sin(ang) * r);
                FirBranch(cut, a, a + Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0) * Vector3.forward * (.6f + (float)rnd.NextDouble() * .7f), .45f);
            }
            for (int k = 0; k < 16; k++)
            {
                float ang = Mathf.Atan2(toTent.z, toTent.x) + ((float)rnd.NextDouble() - .5f) * 2f, r = .6f + (float)rnd.NextDouble() * 2.5f;
                var a = new Vector3(Mathf.Cos(ang) * r, .02f, Mathf.Sin(ang) * r);
                cut.Tube(0, a, a + Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0) * Vector3.forward * (.3f + (float)rnd.NextDouble() * .6f), .015f, .006f, 4, 2);
            }
            Part(t, "CutBranchesAndTwigs", cut, Materials.Bark, Materials.Find("NeedlesSpruce"), Materials.Find("SnowOnBranches"));
            var trampled = new MeshBuilder(1);
            Mound(trampled, 0, fire + toTent * .6f + Vector3.down * .25f, new Vector3(1.8f, .28f, 1.6f), 90);
            Part(t, "TrampledSnow", trampled, Materials.Get("SnowTrampled", new Color(.86f, .88f, .9f), Materials.Tex(WorldPaths.PH("snow_03", "diff")), Materials.Tex(WorldPaths.PH("snow_03", "nor_gl")), .25f));
            return Save(root);
        }

        // ------------------------------------------------------------------ den
        /// <summary>The floor of branches in the stream bed. Local +Z = downstream.</summary>
        public static GameObject Den()
        {
            var root = new GameObject("Site_Den_1959");
            var t = root.transform;
            float L = Sites.Den.Length, W = Sites.Den.Width;
            var walls = new MeshBuilder(1);
            for (int k = 0; k < 12; k++) { float a = k / 12f * Mathf.PI * 2; Mound(walls, 0, new Vector3(Mathf.Cos(a) * (W * .5f + 1.1f), -.2f, Mathf.Sin(a) * (L * .5f + 1.1f)), new Vector3(1.3f, .9f, 1.2f), 100 + k); }
            Part(t, "DugSnowWalls", walls, Materials.Snow);
            var trunks = new MeshBuilder(2);
            var rnd = new System.Random(15);
            for (int k = 0; k < Sites.Den.FirTrunks + Sites.Den.BirchTrunks; k++)
            {
                float x = -W / 2 + .06f + k * (W - .12f) / (Sites.Den.FirTrunks + Sites.Den.BirchTrunks - 1);
                float len = 1.3f + (float)rnd.NextDouble() * (Sites.Den.TrunkMaxLength - 1.3f);
                bool birch = k == 7;
                trunks.Tube(birch ? 1 : 0, new Vector3(x, .04f, -len / 2), new Vector3(x + ((float)rnd.NextDouble() - .5f) * .06f, .04f, len / 2), .045f, .025f, 6, 2, 0, true);
            }
            Part(t, "FirTopsAndBirch", trunks, Materials.Bark, Materials.BirchBark);
            var layer = new MeshBuilder(3);
            for (int k = 0; k < 16; k++)
            {
                var a = new Vector3(-W / 2 + (float)rnd.NextDouble() * W, .09f + k * .01f, -L / 2 + (float)rnd.NextDouble() * .6f);
                FirBranch(layer, a, a + new Vector3(((float)rnd.NextDouble() - .5f) * .6f, 0, 1.1f + (float)rnd.NextDouble() * .5f), .55f);
            }
            Part(t, "BranchLayer", layer, Materials.Bark, Materials.Find("NeedlesFir"), Materials.Find("SnowOnBranches"));
            // four clothing items laid in the corners (protocol, May 1959)
            var items = new (string name, Color c, Vector3 size)[]
            {
                ("trouser_leg_black", new Color(.08f, .08f, .09f), new Vector3(.2f, .05f, .75f)),
                ("sweater_brown", new Color(.36f, .24f, .15f), new Vector3(.5f, .06f, .55f)),
                ("sweater_white", new Color(.86f, .85f, .8f), new Vector3(.5f, .06f, .55f)),
                ("trousers_brown", new Color(.33f, .25f, .18f), new Vector3(.38f, .05f, .9f)),
            };
            for (int k = 0; k < 4; k++)
            {
                var mb = new MeshBuilder(1);
                float sx = k % 2 == 0 ? -1 : 1, sz = k < 2 ? -1 : 1;
                mb.Box(0, new Vector3(sx * (W / 2 - .3f), .2f, sz * (L / 2 - .35f)), items[k].size, Quaternion.Euler(0, sx * sz * 12, 0), 2);
                Part(t, "Clothing_" + items[k].name, mb, Materials.Cloth(items[k].name, items[k].c));
            }
            // tops cut about 15 m away (direction not documented): young fir stumps
            var stumps = new MeshBuilder(1);
            for (int k = 0; k < 6; k++) { var p = new Vector3(-14f + k * .9f, 0, -3f + (k % 3) * 1.2f); stumps.Tube(0, p + Vector3.down * .3f, p + Vector3.up * (.4f + k * .05f), .05f, .04f, 6, 2, 0, true); }
            Part(t, "CutFirStumps_15m", stumps, Materials.Bark);
            return Save(root);
        }

        // ------------------------------------------------------------------ markers and flags
        public static GameObject Marker(string name, Color color)
        {
            var root = new GameObject(name);
            var post = new MeshBuilder(1);
            post.Box(0, new Vector3(0, .75f, 0), new Vector3(.09f, 1.5f, .09f), Quaternion.identity, 2);
            Part(root.transform, "Post", post, Materials.Wood);
            var plate = new MeshBuilder(1);
            plate.Box(0, new Vector3(0, 1.42f, .06f), new Vector3(.9f, .22f, .03f), Quaternion.identity, 1);
            Part(root.transform, "Plate", plate, Materials.Marker(color));
            var label = new GameObject("Label", typeof(TextMesh));
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, 1.42f, .08f);
            label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var tm = label.GetComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.anchor = TextAnchor.MiddleCenter; tm.characterSize = .012f; tm.fontSize = 48; tm.color = new Color(.1f, .08f, .06f);
            return Save(root);
        }

        public static GameObject RouteFlag(string name, Color color)
        {
            var root = new GameObject(name);
            var pole = new MeshBuilder(1);
            pole.Tube(0, Vector3.down * .3f, Vector3.up * 1.7f, .012f, .01f, 5);
            Part(root.transform, "Pole", pole, Materials.Wood);
            var flag = new MeshBuilder(1);
            flag.Quad(0, new Vector3(0, 1.45f, 0), new Vector3(0, 1.45f, .32f), new Vector3(0, 1.68f, .3f), new Vector3(0, 1.68f, 0), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(root.transform, "Flag", flag, Materials.Marker(color));
            return Save(root);
        }

        public static GameObject P4Stone(GameObject boulder)
        {
            var root = new GameObject("Site_P4_Stone");
            var b = (GameObject)PrefabUtility.InstantiatePrefab(boulder, root.transform);
            b.transform.localScale = new Vector3(.8f, .7f, .8f);
            return Save(root);
        }

        public static IEnumerable<string> All => new[] { "Site_Tent_1959", "Site_Labaz_1959", "Site_Cedar_1959", "Site_Den_1959", "Site_P4_Stone" };
    }
}
