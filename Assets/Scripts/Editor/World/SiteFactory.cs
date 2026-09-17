using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Hand-authored (in code) reconstructions of the event sites, dimensioned from <see cref="Sites"/>. Each becomes a prefab in
    /// Resources/World/Prefabs/Sites; the runtime places them by coordinates. Pivot = ground point, +Z = the site's reference direction.</summary>
    public static partial class SiteFactory
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

        const float DoorHalf = .42f;

        /// <summary>Invisible collider as a child (rotated boxes are not possible on the root).</summary>
        static void Solid(Transform t, string name, Vector3 center, Vector3 size, Vector3 euler)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = center; go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<BoxCollider>().size = size;
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
            Bank(snow, Matrix4x4.identity, Vector3.zero, Lf * .5f + .75f, Wf * .5f + .75f, morning ? Mathf.PI : 0f, morning ? .35f : .05f, D * .9f, 1.1f, 40);
            Part(t, "SnowPit", snow, Materials.Snow);
            var floor = new MeshBuilder(1);
            for (int k = 0; k < 7; k++) floor.Box(0, new Vector3(-Lf / 2 + .11f + k * (Lf - .22f) / 6, .01f, 0), new Vector3(.24f, .012f, Wf * (.92f + .08f * (k % 2))), Quaternion.Euler(0, (k - 3) * 2f, 0), 2);
            Part(t, "BirchBarkFloor", floor, Materials.BirchCardboard);

            // food: canvas bags and boxes (≈55 kg), mandolin, spare boots
            var bags = new MeshBuilder(1);
            for (int k = 0; k < 6; k++) Mound(bags, 0, new Vector3(-.55f + k * .22f, .05f, (k % 2 == 0 ? -.22f : .2f)), new Vector3(.14f, .22f, .16f), 60 + k);
            Off(Part(t, "FoodBags", bags, Hessian), goods);
            // tins among the food: condensed milk and canned meat were in the labaz protocol
            for (int c = 0; c < 4; c++)
                if (PlaceScan(t, "russian_food_cans_01", goods + new Vector3(.35f + c * .09f, .01f, -.42f + (c % 2) * .08f), Yaw(c * 47), 1f, c % 2 == 0 ? "russian_food_cans_01_can_cond" : "russian_food_cans_01_can_fish") == null) break;
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
