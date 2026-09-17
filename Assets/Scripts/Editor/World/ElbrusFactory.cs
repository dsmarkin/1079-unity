using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Everything man-made on the Elbrus map, built in code the same way the event sites are: ropeway terminals,
    /// towers, cabins, the barrel huts of Garabashi, the shelters, a snow-cat and the route wands.
    /// Prefabs go to Resources/World/Prefabs/Elbrus. Pivot = the point the runtime places (ground for buildings,
    /// the haul rope for cars); +Z = the direction of travel or uphill.</summary>
    public static class ElbrusFactory
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Elbrus";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Elbrus";
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

        static void Solid(Transform t, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ── materials ─────────────────────────────────────────────────────────────────────────────────────
        static Material Steel => Materials.Get("ElbSteel", new Color(.46f, .48f, .5f), smoothness: .55f);
        static Material Paint => Materials.Get("ElbPaint", new Color(.82f, .28f, .2f), smoothness: .45f);
        static Material PaintBlue => Materials.Get("ElbPaintBlue", new Color(.15f, .35f, .58f), smoothness: .45f);
        static Material Cabin => Materials.Get("ElbCabin", new Color(.92f, .93f, .95f), smoothness: .5f);
        static Material Glass => Materials.Get("ElbGlass", new Color(.14f, .2f, .26f), smoothness: .92f);
        static Material Concrete => Materials.Get("ElbConcrete", new Color(.62f, .61f, .58f), smoothness: .08f);
        static Material Rubber => Materials.Get("ElbRubber", new Color(.09f, .09f, .1f), smoothness: .12f);
        static Material Alu => Materials.Get("ElbAlu", new Color(.72f, .74f, .77f), smoothness: .65f);
        static Material Flag => Materials.Get("ElbFlag", new Color(.88f, .18f, .14f), smoothness: .1f);
        static Material Bamboo => Materials.Get("ElbBamboo", new Color(.78f, .71f, .45f), smoothness: .25f);
        static Material Plank => Materials.PH("ElbPlank", "raw_plank_wall", new Color(.74f, .6f, .44f), 1.6f);
        static Material PlankDark => Materials.PH("ElbPlankDark", "raw_plank_wall", new Color(.42f, .32f, .24f), 1.6f);
        static Material Stone => Materials.PH("ElbStone", "lichen_rock", new Color(.72f, .7f, .68f), .7f, .1f);
        static Material Plaster => Materials.Get("ElbPlaster", new Color(.86f, .83f, .76f), smoothness: .06f);
        static Material RoofGreen => Materials.Get("ElbRoofGreen", new Color(.18f, .31f, .24f), smoothness: .42f);
        static Material RoofRust => Materials.Get("ElbRoofRust", new Color(.46f, .24f, .16f), smoothness: .35f);
        static Material Tarp => Materials.Get("ElbCanvas", new Color(.86f, .84f, .78f), smoothness: .08f);
        static Material TarpRed => Materials.Get("ElbCanvasRed", new Color(.72f, .2f, .18f), smoothness: .08f);
        static Material Rebar => Materials.Get("ElbRebar", new Color(.42f, .3f, .22f), smoothness: .3f);

        public static void Build()
        {
            Terminal("Elb_Terminal_Azau", 22f, 14f, 8.5f, PaintBlue, true);
            Terminal("Elb_Terminal_Krugozor", 17f, 11f, 7.5f, Paint, false);
            Terminal("Elb_Terminal_Mir", 20f, 12f, 8f, Paint, true);
            Terminal("Elb_Terminal_Garabashi", 18f, 11f, 7.5f, PaintBlue, true);
            Terminal("Elb_Terminal_Small", 12f, 8f, 6f, Steel, false);
            TowerTubular("Elb_Tower_Gondola", .55f, .34f, Ropeway.Gauge, 2.6f);
            TowerTubular("Elb_Tower_Chair", .34f, .22f, 2.2f, 1.8f);
            foreach (int h in new[] { 16, 22, 30, 40 }) TowerLattice("Elb_Tower_Lattice_" + h, h);
            GondolaCabin();
            PendulumCar();
            ChairSeat();
            Barrel();
            CapsuleHut();
            DieselHut();
            SmallHut();
            Ratrak();
            Wand();
            Sign();
            OldStation();
            Hotel("Elb_Hotel", 18f, 11f, 3, RoofGreen);
            Hotel("Elb_Chalet", 12f, 8f, 2, RoofRust);
            Cafe();
            Kiosk();
            TicketOffice();
            Toilets();
            Monument();
            WagonExhibit();
            Railing();
            Foundation();
            PostBox();
            Bench();
            Lamp();
            Snowmobile();
        }

        // ── ropeway terminal ──────────────────────────────────────────────────────────────────────────────
        /// <summary>A station hall: concrete apron, a platform, a steel-and-glass shed open at both ends so the rope runs
        /// through it, and the drive gantry over the platform. Pivot = the rope point on the ground, +Z = uphill.</summary>
        static GameObject Terminal(string name, float length, float width, float height, Material paint, bool glazed)
        {
            var root = new GameObject(name);
            var t = root.transform;
            float hw = width / 2, hl = length / 2;

            var slab = new MeshBuilder(1);
            slab.Box(0, new Vector3(0, -.45f, 0), new Vector3(width + 4f, .9f, length + 6f), Quaternion.identity, .3f);
            slab.Box(0, new Vector3(0, .35f, -hl - 3.5f), new Vector3(width + 1f, .7f, 5f), Quaternion.identity, .3f); // arrival apron
            Part(t, "Apron", slab, Concrete);

            var frame = new MeshBuilder(1);
            // columns
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 5; k++)
                {
                    float z = -hl + k * (length / 4f);
                    frame.Tube(0, new Vector3(i * hw, 0, z), new Vector3(i * hw, height, z), .19f, .16f, 8, .5f, 0, true);
                }
            // roof beams and purlins
            for (int k = 0; k < 5; k++)
            {
                float z = -hl + k * (length / 4f);
                frame.Tube(0, new Vector3(-hw, height, z), new Vector3(hw, height, z), .15f, .15f, 6, .5f);
                frame.Tube(0, new Vector3(-hw, height - 1.1f, z), new Vector3(0, height, z), .07f, .07f, 5, .5f);
                frame.Tube(0, new Vector3(hw, height - 1.1f, z), new Vector3(0, height, z), .07f, .07f, 5, .5f);
            }
            for (int i = -1; i <= 1; i += 2)
                frame.Tube(0, new Vector3(i * hw, height, -hl), new Vector3(i * hw, height, hl), .12f, .12f, 6, .5f);
            // the rope gantry: two sheave beams over the platform, on the axis of the line
            frame.Tube(0, new Vector3(0, height - 1.5f, -hl - 2f), new Vector3(0, height - 1.5f, hl + 2f), .22f, .22f, 8, .5f);
            for (int k = 0; k < 8; k++)
            {
                float z = -hl - 1f + k * (length + 2f) / 7f;
                for (int i = -1; i <= 1; i += 2)
                    frame.Tube(0, new Vector3(i * Ropeway.Gauge / 2, height - 1.5f, z), new Vector3(i * Ropeway.Gauge / 2, height - 1.9f, z), .28f, .28f, 10, .5f, 0, true);
            }
            Part(t, "Frame", frame, Steel);

            var shell = new MeshBuilder(1);
            shell.Box(0, new Vector3(0, height + .22f, 0), new Vector3(width + 1.4f, .35f, length + 1.6f), Quaternion.identity, .4f); // roof slab
            shell.Box(0, new Vector3(-hw - .1f, height / 2, 0), new Vector3(.25f, height, length), Quaternion.identity, .5f);
            shell.Box(0, new Vector3(hw + .1f, height / 2, 0), new Vector3(.25f, height, length), Quaternion.identity, .5f);
            Part(t, "Shell", shell, paint);

            if (glazed)
            {
                var glassMb = new MeshBuilder(1);
                for (int i = -1; i <= 1; i += 2)
                    for (int k = 0; k < 4; k++)
                    {
                        float z0 = -hl + k * (length / 4f) + .35f, z1 = z0 + length / 4f - .7f;
                        float y0 = 1.6f, y1 = height - .6f;
                        float x = i * (hw - .06f);
                        glassMb.Quad(0, new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
                            Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                    }
                Part(t, "Glazing", glassMb, Glass);
            }

            var deck = new MeshBuilder(1);
            deck.Box(0, new Vector3(0, .95f, 0), new Vector3(width - 1.2f, .25f, length - 1.5f), Quaternion.identity, .6f);
            // the gap the cars run through
            deck.Box(0, new Vector3(0, 1.35f, -hl + .8f), new Vector3(width - 3.5f, .55f, .3f), Quaternion.identity, .6f);
            Part(t, "Platform", deck, Alu);

            Solid(t, "Wall_W", new Vector3(-hw - .1f, height / 2, 0), new Vector3(.4f, height, length));
            Solid(t, "Wall_E", new Vector3(hw + .1f, height / 2, 0), new Vector3(.4f, height, length));
            Solid(t, "Deck", new Vector3(0, .95f, 0), new Vector3(width - 1.2f, .25f, length - 1.5f));
            Solid(t, "Apron", new Vector3(0, -.45f, 0), new Vector3(width + 4f, .9f, length + 6f));
            return Save(root);
        }

        // ── towers ────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Tubular tower of a modern gondola or of the old single chair. "Mast" is 1 m tall and is scaled in Y by
        /// the runtime; "Head" carries the cross-arm and the sheave trains and is lifted to the top.</summary>
        static GameObject TowerTubular(string name, float rBottom, float rTop, float gauge, float armDrop)
        {
            var root = new GameObject(name);
            var t = root.transform;

            var mastGo = new GameObject("Mast"); mastGo.transform.SetParent(t, false);
            var mast = new MeshBuilder(1);
            mast.Tube(0, Vector3.zero, Vector3.up, rBottom, rTop, 12, 1f, 0, false);
            var mf = mastGo.AddComponent<MeshFilter>(); mastGo.AddComponent<MeshRenderer>().sharedMaterial = Steel;
            Directory.CreateDirectory(MeshDir);
            var mm = mast.ToMesh(name + "_mast");
            AssetDatabase.DeleteAsset($"{MeshDir}/{name}_mast.asset"); AssetDatabase.CreateAsset(mm, $"{MeshDir}/{name}_mast.asset");
            mf.sharedMesh = mm;

            var headGo = new GameObject("Head"); headGo.transform.SetParent(t, false);
            var head = new MeshBuilder(1);
            float g = gauge / 2;
            head.Tube(0, new Vector3(-g - .5f, 0, 0), new Vector3(g + .5f, 0, 0), .16f, .16f, 8, .5f);   // cross-arm
            head.Tube(0, new Vector3(0, -1.2f, 0), new Vector3(0, 0, 0), .2f, .18f, 10, .5f);
            for (int i = -1; i <= 1; i += 2)
            {
                float x = i * g;
                head.Tube(0, new Vector3(x, 0, 0), new Vector3(x, -armDrop * .35f, 0), .1f, .1f, 6, .5f);  // sheave hanger
                head.Box(0, new Vector3(x, -armDrop * .45f, 0), new Vector3(.24f, .22f, 2.6f), Quaternion.identity, .5f); // sheave train beam
                for (int k = 0; k < 6; k++)
                {
                    float z = -1.05f + k * .42f;
                    head.Tube(0, new Vector3(x - .09f, -armDrop * .45f - .28f, z), new Vector3(x + .09f, -armDrop * .45f - .28f, z), .16f, .16f, 10, .5f, 0, true);
                }
            }
            var hf = headGo.AddComponent<MeshFilter>(); headGo.AddComponent<MeshRenderer>().sharedMaterial = Steel;
            var hm = head.ToMesh(name + "_head");
            AssetDatabase.DeleteAsset($"{MeshDir}/{name}_head.asset"); AssetDatabase.CreateAsset(hm, $"{MeshDir}/{name}_head.asset");
            hf.sharedMesh = hm;

            var col = new GameObject("Trunk"); col.transform.SetParent(t, false);
            col.transform.localPosition = new Vector3(0, 5f, 0);
            var cc = col.AddComponent<CapsuleCollider>(); cc.radius = rBottom; cc.height = 10f;
            return Save(root);
        }

        /// <summary>Lattice tower of the 1969 jig-back: four legs, rungs and diagonals, built at its true height.</summary>
        static GameObject TowerLattice(string name, float height)
        {
            var root = new GameObject(name);
            var t = root.transform;
            var mb = new MeshBuilder(1);
            float baseHalf = height * .12f + 1.1f, topHalf = 1.0f;
            Vector3 Leg(int sx, int sz, float y)
            {
                float k = Mathf.Lerp(baseHalf, topHalf, y / height);
                return new Vector3(sx * k, y, sz * k);
            }
            int bays = Mathf.Max(4, Mathf.RoundToInt(height / 2.6f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    for (int b = 0; b < bays; b++)
                    {
                        float y0 = height * b / bays, y1 = height * (b + 1) / bays;
                        mb.Tube(0, Leg(sx, sz, y0), Leg(sx, sz, y1), .11f, .105f, 6, .5f);
                    }
            for (int b = 0; b <= bays; b++)
            {
                float y = height * b / bays;
                mb.Tube(0, Leg(-1, -1, y), Leg(1, -1, y), .06f, .06f, 5, .5f);
                mb.Tube(0, Leg(1, -1, y), Leg(1, 1, y), .06f, .06f, 5, .5f);
                mb.Tube(0, Leg(1, 1, y), Leg(-1, 1, y), .06f, .06f, 5, .5f);
                mb.Tube(0, Leg(-1, 1, y), Leg(-1, -1, y), .06f, .06f, 5, .5f);
            }
            for (int b = 0; b < bays; b++)
            {
                float y0 = height * b / bays, y1 = height * (b + 1) / bays;
                bool flip = b % 2 == 0;
                mb.Tube(0, Leg(-1, flip ? -1 : 1, y0), Leg(1, flip ? 1 : -1, y1), .05f, .05f, 4, .5f);
                mb.Tube(0, Leg(flip ? -1 : 1, 1, y0), Leg(flip ? 1 : -1, -1, y1), .05f, .05f, 4, .5f);
            }
            // head: one saddle per track rope, plus the haul-rope sheave between them
            float g = Ropeway.Gauge / 2 + .8f;
            mb.Tube(0, new Vector3(-g - .7f, height, 0), new Vector3(g + .7f, height, 0), .17f, .17f, 8, .5f);
            for (int i = -1; i <= 1; i += 2)
            {
                float x = i * g;
                mb.Box(0, new Vector3(x, height + .35f, 0), new Vector3(.5f, .5f, 3.4f), Quaternion.identity, .5f);
                mb.Tube(0, new Vector3(x, height, 0), new Vector3(x, height + .2f, 0), .12f, .12f, 6, .5f);
            }
            mb.Tube(0, new Vector3(-.16f, height - .55f, 0), new Vector3(.16f, height - .55f, 0), .3f, .3f, 12, .5f, 0, true);
            Part(t, "Lattice", mb, Steel);
            var col = new GameObject("Trunk"); col.transform.SetParent(t, false);
            col.transform.localPosition = new Vector3(0, height / 2, 0);
            var cc = col.AddComponent<CapsuleCollider>(); cc.radius = .9f; cc.height = height;
            return Save(root);
        }

        // ── cars ──────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Eight-seat detachable cabin. Pivot = the grip on the haul rope; the body hangs 2.4 m below it, +Z = travel.</summary>
        static GameObject GondolaCabin()
        {
            var root = new GameObject("Elb_Cabin_Gondola");
            var t = root.transform;
            float bodyY = -2.65f, w = 1.05f, l = 1.15f, h = 1.05f;

            var arm = new MeshBuilder(1);
            arm.Box(0, new Vector3(0, -.12f, 0), new Vector3(.5f, .42f, .8f), Quaternion.identity, 1f);   // grip
            arm.Tube(0, new Vector3(0, -.3f, 0), new Vector3(0, bodyY + h + .1f, 0), .07f, .07f, 8, 1f);   // hanger
            arm.Box(0, new Vector3(0, bodyY + h + .12f, 0), new Vector3(.3f, .16f, 1.3f), Quaternion.identity, 1f);
            Part(t, "Hanger", arm, Steel);

            var body = new MeshBuilder(1);
            // rounded shell: a stack of rings, widest at seat height
            int rings = 9;
            for (int k = 0; k < rings; k++)
            {
                float u0 = k / (float)rings, u1 = (k + 1) / (float)rings;
                float y0 = bodyY - h + 2 * h * u0, y1 = bodyY - h + 2 * h * u1;
                float r0 = Mathf.Sin(Mathf.PI * (0.12f + 0.76f * u0)), r1 = Mathf.Sin(Mathf.PI * (0.12f + 0.76f * u1));
                RingBox(body, 0, y0, y1, w * r0, l * r0, w * r1, l * r1);
            }
            Part(t, "Shell", body, Cabin);

            var glassMb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                glassMb.Quad(0, new Vector3(i * (w * .985f), bodyY - .55f, -l * .7f), new Vector3(i * (w * .985f), bodyY - .55f, l * .7f),
                    new Vector3(i * (w * .985f), bodyY + .55f, l * .62f), new Vector3(i * (w * .985f), bodyY + .55f, -l * .62f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            for (int i = -1; i <= 1; i += 2)
                glassMb.Quad(0, new Vector3(-w * .68f, bodyY - .5f, i * (l * .985f)), new Vector3(w * .68f, bodyY - .5f, i * (l * .985f)),
                    new Vector3(w * .6f, bodyY + .6f, i * (l * .985f)), new Vector3(-w * .6f, bodyY + .6f, i * (l * .985f)),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Windows", glassMb, Glass);

            var seats = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                seats.Box(0, new Vector3(0, bodyY - .72f, i * .45f), new Vector3(1.7f, .12f, .45f), Quaternion.identity, 1f);
                seats.Box(0, new Vector3(0, bodyY - .45f, i * .68f), new Vector3(1.7f, .5f, .1f), Quaternion.identity, 1f);
            }
            seats.Box(0, new Vector3(0, bodyY - 1.05f, 0), new Vector3(1.85f, .1f, 2.1f), Quaternion.identity, 1f);  // floor
            Part(t, "Interior", seats, Materials.Get("ElbSeat", new Color(.2f, .23f, .28f), smoothness: .2f));

            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, bodyY - 1.0f, 0);
            var box = new GameObject("Hull"); box.transform.SetParent(t, false);
            box.transform.localPosition = new Vector3(0, bodyY, 0);
            var bc = box.AddComponent<BoxCollider>(); bc.size = new Vector3(2.1f, 2.1f, 2.3f);
            return Save(root);
        }

        static void RingBox(MeshBuilder mb, int s, float y0, float y1, float w0, float l0, float w1, float l1)
        {
            Vector3 A(float y, float w, float l, int sx, int sz) => new Vector3(sx * w, y, sz * l);
            mb.Quad(s, A(y0, w0, l0, 1, 1), A(y0, w0, l0, 1, -1), A(y1, w1, l1, 1, -1), A(y1, w1, l1, 1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, -1), A(y0, w0, l0, -1, 1), A(y1, w1, l1, -1, 1), A(y1, w1, l1, -1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, 1), A(y0, w0, l0, 1, 1), A(y1, w1, l1, 1, 1), A(y1, w1, l1, -1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, 1, -1), A(y0, w0, l0, -1, -1), A(y1, w1, l1, -1, -1), A(y1, w1, l1, 1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>Twenty-place jig-back car of 1969. Pivot = the carriage on the track rope.</summary>
        static GameObject PendulumCar()
        {
            var root = new GameObject("Elb_Car_Pendulum");
            var t = root.transform;
            float bodyY = -3.1f, w = 1.25f, l = 2.3f, h = 1.3f;

            var carriage = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    carriage.Tube(0, new Vector3(i * .13f, .05f, k * .7f), new Vector3(i * .13f, .05f, k * .7f + i * .01f), .26f, .26f, 10, .5f, 0, true);
            carriage.Box(0, new Vector3(0, -.28f, 0), new Vector3(.7f, .4f, 2.2f), Quaternion.identity, .5f);
            carriage.Tube(0, new Vector3(0, -.48f, 0), new Vector3(0, bodyY + h + .15f, 0), .11f, .11f, 8, .5f);
            carriage.Box(0, new Vector3(0, bodyY + h + .16f, 0), new Vector3(.45f, .2f, 2.4f), Quaternion.identity, .5f);
            Part(t, "Carriage", carriage, Steel);

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, bodyY, 0), new Vector3(w * 2, h * 2, l * 2), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, bodyY + h + .12f, 0), new Vector3(w * 2 + .2f, .25f, l * 2 + .25f), Quaternion.identity, .6f);
            Part(t, "Body", body, Paint);

            var glassMb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                glassMb.Quad(0, new Vector3(i * (w + .01f), bodyY - .25f, -l + .25f), new Vector3(i * (w + .01f), bodyY - .25f, l - .25f),
                    new Vector3(i * (w + .01f), bodyY + .8f, l - .25f), new Vector3(i * (w + .01f), bodyY + .8f, -l + .25f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            for (int i = -1; i <= 1; i += 2)
                glassMb.Quad(0, new Vector3(-w + .2f, bodyY - .2f, i * (l + .01f)), new Vector3(w - .2f, bodyY - .2f, i * (l + .01f)),
                    new Vector3(w - .2f, bodyY + .8f, i * (l + .01f)), new Vector3(-w + .2f, bodyY + .8f, i * (l + .01f)),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Windows", glassMb, Glass);

            var inside = new MeshBuilder(1);
            inside.Box(0, new Vector3(0, bodyY - h + .08f, 0), new Vector3(w * 2 - .2f, .14f, l * 2 - .2f), Quaternion.identity, 1f);
            for (int i = -1; i <= 1; i += 2)
                inside.Tube(0, new Vector3(i * (w - .22f), bodyY + .35f, -l + .3f), new Vector3(i * (w - .22f), bodyY + .35f, l - .3f), .035f, .035f, 6, 1f);
            Part(t, "Interior", inside, Alu);

            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, bodyY - h + .2f, 0);
            var box = new GameObject("Hull"); box.transform.SetParent(t, false);
            box.transform.localPosition = new Vector3(0, bodyY, 0);
            var bc = box.AddComponent<BoxCollider>(); bc.size = new Vector3(w * 2, h * 2, l * 2);
            return Save(root);
        }

        /// <summary>The old single fixed-grip chair between Mir and Garabashi: a seat, a footrest and a bar.</summary>
        static GameObject ChairSeat()
        {
            var root = new GameObject("Elb_Chair");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, -.1f, 0), new Vector3(.3f, .3f, .5f), Quaternion.identity, 1f);
            mb.Tube(0, new Vector3(0, -.25f, 0), new Vector3(0, -1.9f, -.05f), .045f, .045f, 6, 1f);
            mb.Tube(0, new Vector3(0, -1.9f, -.05f), new Vector3(0, -2.0f, .3f), .045f, .045f, 6, 1f);
            Part(t, "Hanger", mb, Steel);
            var seat = new MeshBuilder(1);
            seat.Box(0, new Vector3(0, -2.05f, .32f), new Vector3(.56f, .09f, .5f), Quaternion.identity, 1f);
            seat.Box(0, new Vector3(0, -1.72f, .58f), new Vector3(.56f, .58f, .08f), Quaternion.identity, 1f);
            Part(t, "Seat", seat, PaintBlue);
            var bar = new MeshBuilder(1);
            bar.Tube(0, new Vector3(-.3f, -1.55f, .05f), new Vector3(.3f, -1.55f, .05f), .03f, .03f, 6, 1f);
            bar.Tube(0, new Vector3(-.3f, -1.55f, .05f), new Vector3(-.3f, -2.35f, .18f), .03f, .03f, 6, 1f);
            bar.Tube(0, new Vector3(.3f, -1.55f, .05f), new Vector3(.3f, -2.35f, .18f), .03f, .03f, 6, 1f);
            bar.Box(0, new Vector3(0, -2.4f, .2f), new Vector3(.7f, .06f, .22f), Quaternion.identity, 1f);
            Part(t, "Bar", bar, Steel);
            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, -2.0f, .32f);
            return Save(root);
        }

        // ── shelters ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>«Бочка» — a wagon barrel of the Garabashi camp: a 6 m cylinder on a timber cradle, door at the downhill end.</summary>
        static GameObject Barrel()
        {
            var root = new GameObject("Elb_Barrel");
            var t = root.transform;
            float r = 1.35f, len = 6f;
            var shell = new MeshBuilder(1);
            shell.Tube(0, new Vector3(0, r + .55f, -len / 2), new Vector3(0, r + .55f, len / 2), r, r, 18, .5f, 0, true);
            // the far end cap
            shell.Tube(0, new Vector3(0, r + .55f, -len / 2 - .02f), new Vector3(0, r + .55f, -len / 2), r, r, 18, .5f, 0, true);
            Part(t, "Shell", shell, Materials.Get("ElbBarrel", new Color(.86f, .87f, .88f), smoothness: .35f));

            var trim = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
            {
                float z = -len / 2 + .8f + k * 1.45f;
                trim.Tube(0, new Vector3(0, r + .55f, z - .04f), new Vector3(0, r + .55f, z + .04f), r + .03f, r + .03f, 18, .5f);
            }
            trim.Box(0, new Vector3(0, .35f, 0), new Vector3(2.4f, .3f, len + .3f), Quaternion.identity, .6f);   // cradle
            for (int k = -1; k <= 1; k += 2)
                for (int j = 0; j < 3; j++)
                    trim.Box(0, new Vector3(k * 1.0f, .1f, -2f + j * 2f), new Vector3(.35f, .5f, .5f), Quaternion.identity, .6f);
            trim.Tube(0, new Vector3(.7f, r * 2 + .5f, -1.4f), new Vector3(.7f, r * 2 + 1.5f, -1.4f), .07f, .065f, 8, .5f, 0, true); // stove pipe
            Part(t, "Trim", trim, Steel);

            var door = new MeshBuilder(1);
            door.Box(0, new Vector3(0, 1.45f, len / 2 + .02f), new Vector3(.85f, 1.9f, .08f), Quaternion.identity, 1f);
            Part(t, "Door", door, Materials.Get("ElbDoor", new Color(.35f, .45f, .55f), smoothness: .3f));
            var win = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                win.Quad(0, new Vector3(i * (r - .02f), 1.55f, -1.4f), new Vector3(i * (r - .02f), 1.55f, -.35f),
                    new Vector3(i * (r - .02f), 2.35f, -.35f), new Vector3(i * (r - .02f), 2.35f, -1.4f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Windows", win, Glass);

            var steps = new MeshBuilder(1);
            for (int k = 0; k < 3; k++) steps.Box(0, new Vector3(0, .2f + k * .25f, len / 2 + .9f - k * .3f), new Vector3(1.1f, .22f, .5f), Quaternion.identity, 1f);
            Part(t, "Steps", steps, Materials.Planks);

            Solid(t, "Hull", new Vector3(0, r + .55f, 0), new Vector3(r * 2, r * 2, len));
            Solid(t, "Cradle", new Vector3(0, .35f, 0), new Vector3(2.4f, .7f, len + .3f));
            return Save(root);
        }

        /// <summary>LeapRus-style capsule: a white half-cylinder pod on legs with a glazed downhill end.</summary>
        static GameObject CapsuleHut()
        {
            var root = new GameObject("Elb_Hut_Capsule");
            var t = root.transform;
            float r = 2.2f, len = 12f;
            var mb = new MeshBuilder(1);
            int seg = 16;
            for (int k = 0; k < seg; k++)
            {
                float a0 = Mathf.PI * k / seg, a1 = Mathf.PI * (k + 1) / seg;
                Vector3 p0 = new Vector3(-Mathf.Cos(a0) * r, Mathf.Sin(a0) * r + .9f, -len / 2);
                Vector3 p1 = new Vector3(-Mathf.Cos(a1) * r, Mathf.Sin(a1) * r + .9f, -len / 2);
                Vector3 q0 = p0 + Vector3.forward * len, q1 = p1 + Vector3.forward * len;
                mb.Quad(0, p0, q0, q1, p1, Vector2.zero, new Vector2(len * .3f, 0), new Vector2(len * .3f, .3f), new Vector2(0, .3f));
            }
            Part(t, "Pod", mb, Materials.Get("ElbCapsule", new Color(.93f, .94f, .96f), smoothness: .45f));

            var ends = new MeshBuilder(1);
            for (int e = -1; e <= 1; e += 2)
            {
                float z = e * len / 2;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = Mathf.PI * k / seg, a1 = Mathf.PI * (k + 1) / seg;
                    Vector3 p0 = new Vector3(-Mathf.Cos(a0) * r, Mathf.Sin(a0) * r + .9f, z);
                    Vector3 p1 = new Vector3(-Mathf.Cos(a1) * r, Mathf.Sin(a1) * r + .9f, z);
                    Vector3 b0 = new Vector3(p0.x, .9f, z), b1 = new Vector3(p1.x, .9f, z);
                    if (e > 0) ends.Quad(0, b0, p0, p1, b1, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    else ends.Quad(0, b1, p1, p0, b0, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
            Part(t, "Ends", ends, Glass);

            var legs = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 4; k++)
                    legs.Tube(0, new Vector3(i * (r - .4f), 0, -len / 2 + 1.2f + k * 3.2f), new Vector3(i * (r - .4f), .95f, -len / 2 + 1.2f + k * 3.2f), .12f, .12f, 8, .5f, 0, true);
            legs.Box(0, new Vector3(0, .85f, 0), new Vector3(r * 2 - .4f, .18f, len), Quaternion.identity, .5f);
            Part(t, "Frame", legs, Steel);

            Solid(t, "Hull", new Vector3(0, r * .55f + .9f, 0), new Vector3(r * 2, r * 1.1f, len));
            return Save(root);
        }

        /// <summary>The «Дизель-хат» at 4 050 m and its neighbours: a boxy two-storey hut clad in sheet metal.</summary>
        static GameObject DieselHut()
        {
            var root = new GameObject("Elb_Hut_Diesel");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, 2.1f, 0), new Vector3(7.5f, 4.2f, 11f), Quaternion.identity, .5f);
            Part(t, "Walls", mb, Materials.Get("ElbHutWall", new Color(.55f, .58f, .62f), smoothness: .4f));
            var roof = new MeshBuilder(1);
            roof.Quad(0, new Vector3(-4.1f, 4.2f, -5.8f), new Vector3(0, 5.6f, -5.8f), new Vector3(0, 5.6f, 5.8f), new Vector3(-4.1f, 4.2f, 5.8f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            roof.Quad(0, new Vector3(0, 5.6f, -5.8f), new Vector3(4.1f, 4.2f, -5.8f), new Vector3(4.1f, 4.2f, 5.8f), new Vector3(0, 5.6f, 5.8f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Roof", roof, Paint);
            var det = new MeshBuilder(1);
            det.Box(0, new Vector3(0, 1.1f, 5.55f), new Vector3(1.1f, 2.2f, .12f), Quaternion.identity, 1f);
            det.Tube(0, new Vector3(2.6f, 5.4f, -3.2f), new Vector3(2.6f, 7.2f, -3.2f), .1f, .095f, 8, .5f, 0, true);
            for (int k = 0; k < 3; k++) det.Box(0, new Vector3(0, .35f - k * .25f, 6.2f + k * .45f), new Vector3(1.6f, .22f, .55f), Quaternion.identity, 1f);
            Part(t, "Details", det, Steel);
            var win = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 4; k++)
                    win.Quad(0, new Vector3(i * 3.78f, 2.3f, -4f + k * 2.4f), new Vector3(i * 3.78f, 2.3f, -3.1f + k * 2.4f),
                        new Vector3(i * 3.78f, 3.3f, -3.1f + k * 2.4f), new Vector3(i * 3.78f, 3.3f, -4f + k * 2.4f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Windows", win, Glass);
            Solid(t, "Hull", new Vector3(0, 2.1f, 0), new Vector3(7.5f, 4.2f, 11f));
            return Save(root);
        }

        /// <summary>A small shelter — the RedFox hut, the Garabashi hut, the emergency box on the saddle.</summary>
        static GameObject SmallHut()
        {
            var root = new GameObject("Elb_Hut_Small");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, 1.3f, 0), new Vector3(3.4f, 2.6f, 4.6f), Quaternion.identity, .7f);
            Part(t, "Walls", mb, Materials.Get("ElbHutSmall", new Color(.78f, .4f, .22f), smoothness: .35f));
            var roof = new MeshBuilder(1);
            roof.Box(0, new Vector3(0, 2.72f, 0), new Vector3(3.9f, .2f, 5f), Quaternion.identity, .7f);
            Part(t, "Roof", roof, Steel);
            var det = new MeshBuilder(1);
            det.Box(0, new Vector3(0, 1f, 2.35f), new Vector3(.9f, 2f, .1f), Quaternion.identity, 1f);
            Part(t, "Door", det, Materials.Planks);
            Solid(t, "Hull", new Vector3(0, 1.3f, 0), new Vector3(3.4f, 2.6f, 4.6f));
            return Save(root);
        }

        // ── snow-cat ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Snow-cat of the Garabashi drivers: tracks, cab, front blade and rear tiller. Pivot on the snow, +Z forward.</summary>
        static GameObject Ratrak()
        {
            var root = new GameObject("Elb_Ratrak");
            var t = root.transform;
            var tracks = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                float x = i * 1.35f;
                for (int k = 0; k < 24; k++)
                {
                    float a = Mathf.PI * 2 * k / 24;
                    float z = Mathf.Cos(a) * 2.2f, y = .55f + Mathf.Sin(a) * .45f;
                    float z2 = Mathf.Cos(a + Mathf.PI * 2 / 24) * 2.2f, y2 = .55f + Mathf.Sin(a + Mathf.PI * 2 / 24) * .45f;
                    tracks.Box(0, new Vector3(x, (y + y2) / 2, (z + z2) / 2), new Vector3(.95f, .12f, .6f),
                        Quaternion.LookRotation(new Vector3(0, y2 - y, z2 - z).normalized, Vector3.up), 1f);
                }
            }
            Part(t, "Tracks", tracks, Rubber);

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, 1.15f, -.3f), new Vector3(2.6f, .9f, 4.2f), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, 2.05f, .35f), new Vector3(2.2f, .95f, 2.1f), Quaternion.identity, .6f);
            Part(t, "Body", body, Materials.Get("ElbRatrak", new Color(.88f, .58f, .1f), smoothness: .45f));

            var win = new MeshBuilder(1);
            win.Quad(0, new Vector3(-1.05f, 1.7f, 1.42f), new Vector3(1.05f, 1.7f, 1.42f), new Vector3(1.05f, 2.45f, 1.28f), new Vector3(-1.05f, 2.45f, 1.28f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            for (int i = -1; i <= 1; i += 2)
                win.Quad(0, new Vector3(i * 1.12f, 1.7f, -.6f), new Vector3(i * 1.12f, 1.7f, 1.35f), new Vector3(i * 1.12f, 2.45f, 1.25f), new Vector3(i * 1.12f, 2.45f, -.6f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Cab", win, Glass);

            var gear = new MeshBuilder(1);
            gear.Box(0, new Vector3(0, .95f, 3.3f), new Vector3(4.6f, 1.1f, .22f), Quaternion.Euler(-12, 0, 0), .6f);   // blade
            for (int i = -1; i <= 1; i += 2)
                gear.Tube(0, new Vector3(i * .8f, 1.1f, 1.9f), new Vector3(i * .5f, .95f, 3.2f), .1f, .1f, 6, .5f);
            gear.Box(0, new Vector3(0, .65f, -3.1f), new Vector3(4.2f, .7f, .8f), Quaternion.identity, .6f);            // tiller
            for (int i = -1; i <= 1; i += 2)
                gear.Tube(0, new Vector3(i * .9f, 1.25f, -2.2f), new Vector3(i * .9f, .8f, -2.9f), .09f, .09f, 6, .5f);
            gear.Tube(0, new Vector3(.8f, 2.55f, -.5f), new Vector3(.8f, 3.1f, -.5f), .07f, .06f, 6, .5f, 0, true);      // exhaust
            Part(t, "Gear", gear, Steel);

            var lamps = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2) lamps.Box(0, new Vector3(i * .75f, 2.55f, .9f), new Vector3(.3f, .16f, .22f), Quaternion.identity, 1f);
            Part(t, "Lamps", lamps, Materials.Get("ElbLamp", new Color(.95f, .93f, .8f), smoothness: .8f));

            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, 1.75f, .3f);
            Solid(t, "Hull", new Vector3(0, 1.3f, 0), new Vector3(3.4f, 2.4f, 6.4f));
            return Save(root);
        }


        // ── the village and the station squares ───────────────────────────────────────────────────────────
        /// <summary>Gable roof over a rectangular block: two slopes, two gable ends, an eaves overhang.</summary>
        static void Gable(MeshBuilder mb, int sub, Vector3 center, float width, float length, float rise, float overhang)
        {
            float hw = width / 2 + overhang, hl = length / 2 + overhang;
            var ridgeA = center + new Vector3(0, rise, -hl);
            var ridgeB = center + new Vector3(0, rise, hl);
            for (int i = -1; i <= 1; i += 2)
            {
                var eaveA = center + new Vector3(i * hw, 0, -hl);
                var eaveB = center + new Vector3(i * hw, 0, hl);
                if (i < 0) mb.Quad(sub, eaveA, ridgeA, ridgeB, eaveB, Vector2.zero, new Vector2(1, 0), new Vector2(1, length / 2), new Vector2(0, length / 2), true);
                else mb.Quad(sub, eaveB, ridgeB, ridgeA, eaveA, Vector2.zero, new Vector2(1, 0), new Vector2(1, length / 2), new Vector2(0, length / 2), true);
            }
        }

        /// <summary>A band of windows down both long sides of a block.</summary>
        static void Windows(MeshBuilder mb, int sub, float width, float length, float y0, float y1, int count, float gap = .6f)
        {
            float hw = width / 2, hl = length / 2, pitch = length / count;
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < count; k++)
                {
                    float z0 = -hl + k * pitch + gap / 2, z1 = z0 + pitch - gap;
                    float x = i * (hw + .03f);
                    mb.Quad(sub, new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
        }

        /// <summary>The 1969 pendulum station: a concrete box with a wide mouth for the rope, a ribbon of windows,
        /// a flat roof with a parapet and an outside stair. Azau and Krugozor still work out of these.</summary>
        static GameObject OldStation()
        {
            var root = new GameObject("Elb_Terminal_Old");
            var t = root.transform;
            const float L = 24f, W = 14f, H = 9.5f;
            float hw = W / 2, hl = L / 2;

            var shell = new MeshBuilder(1);
            shell.Box(0, new Vector3(0, -.5f, 0), new Vector3(W + 5f, 1f, L + 7f), Quaternion.identity, .3f);      // apron
            shell.Box(0, new Vector3(-hw + .3f, H / 2, 0), new Vector3(.6f, H, L), Quaternion.identity, .5f);
            shell.Box(0, new Vector3(hw - .3f, H / 2, 0), new Vector3(.6f, H, L), Quaternion.identity, .5f);
            // both ends are open on the rope axis; above the opening the concrete carries on to the roof
            for (int e = -1; e <= 1; e += 2)
            {
                shell.Box(0, new Vector3(0, H - 1.4f, e * (hl - .3f)), new Vector3(W, 2.8f, .6f), Quaternion.identity, .5f);
                for (int i = -1; i <= 1; i += 2)
                    shell.Box(0, new Vector3(i * (hw / 2 + 1.8f), H / 2, e * (hl - .3f)), new Vector3(W / 2 - 3.6f, H, .6f), Quaternion.identity, .5f);
            }
            shell.Box(0, new Vector3(0, H + .3f, 0), new Vector3(W + 1.2f, .6f, L + 1.2f), Quaternion.identity, .4f); // roof slab
            shell.Box(0, new Vector3(0, H + 1.1f, hl + .3f), new Vector3(W + 1.2f, 1f, .4f), Quaternion.identity, .4f); // parapet
            shell.Box(0, new Vector3(0, H + 1.1f, -hl - .3f), new Vector3(W + 1.2f, 1f, .4f), Quaternion.identity, .4f);
            for (int i = -1; i <= 1; i += 2)
                shell.Box(0, new Vector3(i * (hw + .3f), H + 1.1f, 0), new Vector3(.4f, 1f, L + 1.2f), Quaternion.identity, .4f);
            // outside stair to the platform on the east wall
            for (int k = 0; k < 7; k++)
                shell.Box(0, new Vector3(hw + 1.6f, .3f + k * .32f, -hl + 2f + k * .5f), new Vector3(3f, .32f, .5f), Quaternion.identity, .4f);
            Part(t, "Shell", shell, Concrete);

            var glass = new MeshBuilder(1);
            Windows(glass, 0, W - .1f, L - 3f, 4.2f, 7.2f, 7, .7f);
            Part(t, "Glazing", glass, Glass);

            var steel = new MeshBuilder(1);
            steel.Tube(0, new Vector3(0, H - 2.2f, -hl - 3.5f), new Vector3(0, H - 2.2f, hl + 3.5f), .26f, .26f, 8, .5f);  // rope gantry
            for (int k = 0; k < 7; k++)
            {
                float z = -hl - 2f + k * (L + 4f) / 6f;
                for (int i = -1; i <= 1; i += 2)
                    steel.Tube(0, new Vector3(i * 2.6f, H - 2.2f, z), new Vector3(i * 2.6f, H - 2.7f, z), .34f, .34f, 10, .5f, 0, true);
            }
            steel.Box(0, new Vector3(-hw / 2 - .6f, 1.4f, 0), new Vector3(W / 2 - 1.4f, .3f, L - 2f), Quaternion.identity, .6f);   // platform, both sides of the track
            steel.Box(0, new Vector3(hw / 2 + .6f, 1.4f, 0), new Vector3(W / 2 - 1.4f, .3f, L - 2f), Quaternion.identity, .6f);
            Part(t, "Steel", steel, Steel);

            var paint = new MeshBuilder(1);
            for (int e = -1; e <= 1; e += 2)
                paint.Box(0, new Vector3(0, H - 1.4f, e * (hl - .62f)), new Vector3(W - 1.5f, 1.6f, .12f), Quaternion.identity, 1f); // name band
            Part(t, "Band", paint, PaintBlue);

            Solid(t, "Wall_W", new Vector3(-hw + .3f, H / 2, 0), new Vector3(.8f, H, L));
            Solid(t, "Wall_E", new Vector3(hw - .3f, H / 2, 0), new Vector3(.8f, H, L));
            Solid(t, "Apron", new Vector3(0, -.5f, 0), new Vector3(W + 5f, 1f, L + 7f));
            Solid(t, "Deck_W", new Vector3(-hw / 2 - .6f, 1.4f, 0), new Vector3(W / 2 - 1.4f, .3f, L - 2f));
            Solid(t, "Deck_E", new Vector3(hw / 2 + .6f, 1.4f, 0), new Vector3(W / 2 - 1.4f, .3f, L - 2f));
            return Save(root);
        }

        /// <summary>A hotel of the Azau meadow: stone plinth, plastered walls, wooden balconies and a steep metal roof.
        /// Two or three floors, the ground one usually a café or a rental shop with a glazed front.</summary>
        static GameObject Hotel(string name, float length, float width, int floors, Material roof)
        {
            var root = new GameObject(name);
            var t = root.transform;
            float hw = width / 2, hl = length / 2, h = floors * 3.1f;

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .35f, 0), new Vector3(width + .8f, .7f, length + .8f), Quaternion.identity, .5f);
            Part(t, "Plinth", body, Stone);

            var walls = new MeshBuilder(1);
            walls.Box(0, new Vector3(0, .7f + h / 2, 0), new Vector3(width, h, length), Quaternion.identity, .4f);
            Part(t, "Walls", walls, Plaster);

            var glass = new MeshBuilder(1);
            for (int f = 0; f < floors; f++)
                Windows(glass, 0, width + .06f, length - 1.6f, 1.6f + f * 3.1f, 3.6f + f * 3.1f, Mathf.Max(2, Mathf.RoundToInt(length / 3.2f)), .9f);
            Part(t, "Windows", glass, Glass);

            var wood = new MeshBuilder(1);
            for (int f = 1; f < floors; f++)
            {
                float y = .7f + f * 3.1f;
                wood.Box(0, new Vector3(0, y, hl + .55f), new Vector3(width - .6f, .14f, 1.1f), Quaternion.identity, .8f);
                for (int k = 0; k < 9; k++)
                    wood.Box(0, new Vector3(-width / 2 + .4f + k * (width - .8f) / 8f, y + .5f, hl + 1.05f), new Vector3(.08f, 1f, .08f), Quaternion.identity, 1f);
                wood.Box(0, new Vector3(0, y + 1.02f, hl + 1.05f), new Vector3(width - .6f, .1f, .14f), Quaternion.identity, .8f);
            }
            Part(t, "Balcony", wood, Plank);

            var roofMb = new MeshBuilder(1);
            Gable(roofMb, 0, new Vector3(0, .7f + h, 0), width, length, 2.6f, .7f);
            roofMb.Box(0, new Vector3(-width / 4, .7f + h + 2.9f, -hl + 2f), new Vector3(.8f, 1.4f, .8f), Quaternion.identity, 1f); // chimney
            Part(t, "Roof", roofMb, roof);

            Solid(t, "Body", new Vector3(0, .7f + h / 2, 0), new Vector3(width, h, length));
            return Save(root);
        }

        /// <summary>A café: a log-and-plank hut with a stove pipe, a terrace in front with two tables under umbrellas.
        /// The same building serves as a shashlyk place at Azau and as the café at Krugozor, Mir and Gara-Bashi.</summary>
        static GameObject Cafe()
        {
            var root = new GameObject("Elb_Cafe");
            var t = root.transform;
            const float L = 9f, W = 6.5f, H = 3.1f;

            var walls = new MeshBuilder(1);
            walls.Box(0, new Vector3(0, .18f, 0), new Vector3(W + 3.4f, .36f, L + 3f), Quaternion.identity, .5f);      // deck
            walls.Box(0, new Vector3(0, .36f + H / 2, -1.2f), new Vector3(W, H, L - 2.4f), Quaternion.identity, .5f);
            Part(t, "Walls", walls, PlankDark);

            var glass = new MeshBuilder(1);
            glass.Quad(0, new Vector3(-2.4f, 1.1f, L / 2 - 2.35f), new Vector3(2.4f, 1.1f, L / 2 - 2.35f),
                new Vector3(2.4f, 2.7f, L / 2 - 2.35f), new Vector3(-2.4f, 2.7f, L / 2 - 2.35f),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Windows(glass, 0, W + .06f, L - 4f, 1.2f, 2.6f, 2, 1.2f);
            Part(t, "Windows", glass, Glass);

            var roof = new MeshBuilder(1);
            Gable(roof, 0, new Vector3(0, .36f + H, -1.2f), W, L - 2.4f, 1.5f, .8f);
            Part(t, "Roof", roof, RoofRust);

            var pipe = new MeshBuilder(1);
            pipe.Tube(0, new Vector3(1.6f, .36f + H + .6f, -2.6f), new Vector3(1.6f, .36f + H + 2.4f, -2.6f), .11f, .1f, 8, 1f, 0, true);
            Part(t, "Pipe", pipe, Steel);

            // terrace: two tables with parasols and a low rail
            var furn = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
            {
                var c = new Vector3(k * 2.2f, .36f, L / 2 - .4f);
                furn.Tube(0, c, c + new Vector3(0, .74f, 0), .05f, .05f, 6, 1f, 0, true);
                furn.Box(0, c + new Vector3(0, .76f, 0), new Vector3(1.1f, .07f, 1.1f), Quaternion.identity, 1f);
                furn.Tube(0, c, c + new Vector3(0, 2.3f, 0), .035f, .03f, 6, 1f);
            }
            for (int k = 0; k < 8; k++)
                furn.Box(0, new Vector3(-W / 2 - 1.5f + k * (W + 3f) / 7f, .36f + .45f, L / 2 + 1.3f), new Vector3(.07f, .9f, .07f), Quaternion.identity, 1f);
            furn.Box(0, new Vector3(0, .36f + .92f, L / 2 + 1.3f), new Vector3(W + 3f, .08f, .1f), Quaternion.identity, 1f);
            Part(t, "Terrace", furn, Plank);

            var shade = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
                shade.Cone(0, new Vector3(k * 2.2f, .36f + 2.32f, L / 2 - .4f), 1.5f, -.45f, 8, 1f);
            Part(t, "Parasols", shade, TarpRed);

            Solid(t, "Body", new Vector3(0, .36f + H / 2, -1.2f), new Vector3(W, H, L - 2.4f));
            Solid(t, "Deck", new Vector3(0, .18f, 0), new Vector3(W + 3.4f, .36f, L + 3f));
            return Save(root);
        }

        /// <summary>A stall of the souvenir market: a frame, a striped awning, a counter with wool hats, hides and honey.</summary>
        static GameObject Kiosk()
        {
            var root = new GameObject("Elb_Kiosk");
            var t = root.transform;
            const float W = 2.6f, D = 2f, H = 2.3f;

            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    frame.Tube(0, new Vector3(i * W / 2, 0, j * D / 2), new Vector3(i * W / 2, H, j * D / 2), .045f, .04f, 6, 1f, 0, true);
            frame.Box(0, new Vector3(0, .85f, -D / 2 + .35f), new Vector3(W, .08f, .7f), Quaternion.identity, 1f);      // counter
            frame.Box(0, new Vector3(0, .42f, -D / 2 + .6f), new Vector3(W - .2f, .84f, .08f), Quaternion.identity, 1f);
            Part(t, "Frame", frame, Plank);

            var awning = new MeshBuilder(1);
            awning.Quad(0, new Vector3(-W / 2 - .35f, H + .35f, D / 2), new Vector3(W / 2 + .35f, H + .35f, D / 2),
                new Vector3(W / 2 + .35f, H - .05f, -D / 2 - .7f), new Vector3(-W / 2 - .35f, H - .05f, -D / 2 - .7f),
                Vector2.zero, new Vector2(3, 0), new Vector2(3, 1), Vector2.up, true);
            Part(t, "Awning", awning, TarpRed);

            var goods = new MeshBuilder(1);
            for (int k = 0; k < 6; k++)
                goods.Box(0, new Vector3(-W / 2 + .3f + k * (W - .6f) / 5f, .95f, -D / 2 + .35f), new Vector3(.3f, .14f, .42f), Quaternion.Euler(0, k * 17f, 0), 1f);
            Part(t, "Goods", goods, Tarp);

            Solid(t, "Counter", new Vector3(0, .5f, -D / 2 + .35f), new Vector3(W, 1f, .7f));
            return Save(root);
        }

        /// <summary>The ticket office: a small glazed pavilion with two windows and a board of prices — everyone on the
        /// meadow starts here before the first cabin.</summary>
        static GameObject TicketOffice()
        {
            var root = new GameObject("Elb_Booth");
            var t = root.transform;
            const float W = 5.5f, D = 3.2f, H = 2.9f;
            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .12f, 0), new Vector3(W + 1f, .24f, D + 1f), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .24f + H / 2, 0), new Vector3(W, H, D), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .24f + H + .12f, .35f), new Vector3(W + 1.2f, .24f, D + 1.4f), Quaternion.identity, .5f);
            Part(t, "Body", body, Plaster);

            var glass = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
                glass.Quad(0, new Vector3(k * 1.35f - .75f, 1.1f, D / 2 + .03f), new Vector3(k * 1.35f + .75f, 1.1f, D / 2 + .03f),
                    new Vector3(k * 1.35f + .75f, 2.3f, D / 2 + .03f), new Vector3(k * 1.35f - .75f, 2.3f, D / 2 + .03f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Windows", glass, Glass);

            var band = new MeshBuilder(1);
            band.Box(0, new Vector3(0, 2.75f, D / 2 + .06f), new Vector3(W - .4f, .5f, .1f), Quaternion.identity, 1f);
            Part(t, "Band", band, PaintBlue);

            var label = new GameObject("Label"); label.transform.SetParent(t, false);
            label.transform.localPosition = new Vector3(0, 2.75f, D / 2 + .13f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = "КАССЫ"; tm.characterSize = .05f; tm.fontSize = 90; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center; tm.color = new Color(.95f, .95f, .93f);

            Solid(t, "Body", new Vector3(0, .24f + H / 2, 0), new Vector3(W, H, D));
            return Save(root);
        }

        /// <summary>The toilets: a pair of containers on a frame, the shabbiest and the most photographed thing at every
        /// station above 3 000 m.</summary>
        static GameObject Toilets()
        {
            var root = new GameObject("Elb_Toilet");
            var t = root.transform;
            const float W = 4.4f, D = 2.4f, H = 2.5f;
            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .25f, 0), new Vector3(W + .4f, .5f, D + .4f), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .5f + H / 2, 0), new Vector3(W, H, D), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, .5f + H + .1f, 0), new Vector3(W + .5f, .2f, D + .5f), Quaternion.identity, .5f);
            Part(t, "Body", body, PaintBlue);
            var doors = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
                doors.Box(0, new Vector3(k * 1.05f, 1.5f, D / 2 + .04f), new Vector3(.9f, 2f, .08f), Quaternion.identity, 1f);
            Part(t, "Doors", doors, PlankDark);
            Solid(t, "Body", new Vector3(0, .5f + H / 2, 0), new Vector3(W, H, D));
            return Save(root);
        }

        /// <summary>«Героям обороны Приэльбрусья»: a concrete stele with a star and a plaque. The one at Mir stands on the
        /// open ground above the station, looking down the Baksan valley the German mountain troops came up in 1942.</summary>
        static GameObject Monument()
        {
            var root = new GameObject("Elb_Monument");
            var t = root.transform;
            var stone = new MeshBuilder(1);
            stone.Box(0, new Vector3(0, .2f, 0), new Vector3(4.4f, .4f, 4.4f), Quaternion.identity, .5f);
            stone.Box(0, new Vector3(0, .55f, 0), new Vector3(3.2f, .3f, 3.2f), Quaternion.identity, .5f);
            stone.Box(0, new Vector3(0, .7f + 2.3f, 0), new Vector3(1.5f, 4.6f, .9f), Quaternion.Euler(0, 0, 3f), .6f);
            Part(t, "Stele", stone, Concrete);
            var star = new MeshBuilder(1);
            star.Box(0, new Vector3(0, 4.4f, .5f), new Vector3(.9f, .9f, .12f), Quaternion.Euler(0, 0, 45f), 1f);
            star.Box(0, new Vector3(0, 1.5f, .48f), new Vector3(1f, .7f, .06f), Quaternion.identity, 1f);            // plaque
            Part(t, "Star", star, Alu);
            Solid(t, "Stele", new Vector3(0, 2.4f, 0), new Vector3(1.6f, 4.8f, 1f));
            return Save(root);
        }

        /// <summary>The old cable car at Krugozor: a wagon of the 1969 jig-back set up on a plinth beside the station,
        /// with its red paint and its round windows, as a monument to the line that carried everyone here for forty years.</summary>
        static GameObject WagonExhibit()
        {
            var root = new GameObject("Elb_Exhibit_Wagon");
            var t = root.transform;
            var plinth = new MeshBuilder(1);
            plinth.Box(0, new Vector3(0, .35f, 0), new Vector3(3.4f, .7f, 6.4f), Quaternion.identity, .5f);
            plinth.Box(0, new Vector3(0, .9f, 0), new Vector3(.5f, .5f, 4.4f), Quaternion.identity, .5f);
            Part(t, "Plinth", plinth, Concrete);

            var body = new MeshBuilder(1);
            RingBox(body, 0, 1.15f, 1.5f, 2.2f, 4.6f, 2.5f, 5.2f);
            body.Box(0, new Vector3(0, 2.15f, 0), new Vector3(2.5f, 1.3f, 5.2f), Quaternion.identity, .5f);
            RingBox(body, 0, 2.8f, 3.1f, 2.5f, 5.2f, 1.9f, 4.2f);
            Part(t, "Body", body, Paint);

            var glass = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 3; k++)
                {
                    float z0 = -1.9f + k * 1.4f, z1 = z0 + 1.1f;
                    float x = i * 1.27f;
                    glass.Quad(0, new Vector3(x, 1.75f, z0), new Vector3(x, 1.75f, z1), new Vector3(x, 2.65f, z1), new Vector3(x, 2.65f, z0),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
            Part(t, "Glazing", glass, Glass);
            Solid(t, "Body", new Vector3(0, 2.1f, 0), new Vector3(2.6f, 2f, 5.3f));
            Solid(t, "Plinth", new Vector3(0, .35f, 0), new Vector3(3.4f, .7f, 6.4f));
            return Save(root);
        }

        /// <summary>Eight metres of the viewing-platform railing: tube posts, two rails, the paint half gone.</summary>
        static GameObject Railing()
        {
            var root = new GameObject("Elb_Rail");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int k = 0; k <= 4; k++)
                mb.Tube(0, new Vector3(-4f + k * 2f, 0, 0), new Vector3(-4f + k * 2f, 1.05f, 0), .045f, .04f, 6, 1f, 0, true);
            mb.Tube(0, new Vector3(-4f, 1.02f, 0), new Vector3(4f, 1.02f, 0), .045f, .045f, 6, 1f);
            mb.Tube(0, new Vector3(-4f, .55f, 0), new Vector3(4f, .55f, 0), .035f, .035f, 6, 1f);
            Part(t, "Rail", mb, Steel);
            Solid(t, "Rail", new Vector3(0, .55f, 0), new Vector3(8f, 1.1f, .12f));
            return Save(root);
        }

        /// <summary>Unfinished concrete: the columns and the slab of a building started at Mir and abandoned, rebar still
        /// sticking out of the tops. There is a lot of this on the slope.</summary>
        static GameObject Foundation()
        {
            var root = new GameObject("Elb_Foundation");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, .15f, 0), new Vector3(9f, .3f, 6f), Quaternion.identity, .5f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = -3.5f + i * 3.5f, z = -2f + j * 4f;
                    float h = 1.2f + ((i + j) % 3) * .7f;
                    mb.Box(0, new Vector3(x, .3f + h / 2, z), new Vector3(.55f, h, .55f), Quaternion.identity, .6f);
                }
            Part(t, "Concrete", mb, Concrete);
            var bars = new MeshBuilder(1);
            var rnd = new System.Random(12);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = -3.5f + i * 3.5f, z = -2f + j * 4f;
                    float h = 1.2f + ((i + j) % 3) * .7f;
                    for (int k = 0; k < 4; k++)
                    {
                        float ox = ((k & 1) == 0 ? -.18f : .18f), oz = (k < 2 ? -.18f : .18f);
                        var a = new Vector3(x + ox, .3f + h, z + oz);
                        var b = a + new Vector3((float)(rnd.NextDouble() - .5) * .3f, .55f + (float)rnd.NextDouble() * .35f, (float)(rnd.NextDouble() - .5) * .3f);
                        bars.Tube(0, a, b, .016f, .014f, 4, 1f);
                    }
                }
            Part(t, "Rebar", bars, Rebar);
            Solid(t, "Slab", new Vector3(0, .15f, 0), new Vector3(9f, .3f, 6f));
            return Save(root);
        }

        /// <summary>The highest post box in Russia, on the wall of the Gara-Bashi café: people send themselves a card
        /// from 3 847 m.</summary>
        static GameObject PostBox()
        {
            var root = new GameObject("Elb_Postbox");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Tube(0, Vector3.zero, new Vector3(0, 1.15f, 0), .05f, .045f, 6, 1f, 0, true);
            mb.Box(0, new Vector3(0, 1.45f, 0), new Vector3(.45f, .62f, .28f), Quaternion.identity, 1f);
            mb.Box(0, new Vector3(0, 1.78f, 0), new Vector3(.5f, .06f, .33f), Quaternion.identity, 1f);
            Part(t, "Box", mb, Paint);
            return Save(root);
        }

        /// <summary>A bench of two planks on stone blocks — every viewing point has one.</summary>
        static GameObject Bench()
        {
            var root = new GameObject("Elb_Bench");
            var t = root.transform;
            var stone = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                stone.Box(0, new Vector3(i * .75f, .22f, 0), new Vector3(.4f, .44f, .5f), Quaternion.identity, .8f);
            Part(t, "Legs", stone, Stone);
            var planks = new MeshBuilder(1);
            planks.Box(0, new Vector3(0, .47f, 0), new Vector3(2.1f, .07f, .45f), Quaternion.identity, 1f);
            Part(t, "Seat", planks, Plank);
            Solid(t, "Seat", new Vector3(0, .3f, 0), new Vector3(2.1f, .6f, .5f));
            return Save(root);
        }

        /// <summary>A lamp post of the Azau square: a steel pole with a bent arm and a flat lantern.</summary>
        static GameObject Lamp()
        {
            var root = new GameObject("Elb_Lamp");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Tube(0, Vector3.zero, new Vector3(0, 5.2f, 0), .09f, .06f, 8, 1f, 0, true);
            mb.Tube(0, new Vector3(0, 5.1f, 0), new Vector3(0, 5.5f, .9f), .055f, .05f, 6, 1f);
            mb.Box(0, new Vector3(0, 5.44f, 1.15f), new Vector3(.32f, .12f, .55f), Quaternion.identity, 1f);
            Part(t, "Pole", mb, Steel);
            return Save(root);
        }

        /// <summary>A snowmobile of the Gara-Bashi carriers: skis in front, a track behind, a fuel can strapped on the back.</summary>
        static GameObject Snowmobile()
        {
            var root = new GameObject("Elb_Snowmobile");
            var t = root.transform;
            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .62f, -.1f), new Vector3(.85f, .5f, 2.1f), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, .95f, .55f), new Vector3(.7f, .3f, .8f), Quaternion.Euler(-18f, 0, 0), .6f);
            Part(t, "Body", body, Paint);
            var black = new MeshBuilder(1);
            black.Box(0, new Vector3(0, .25f, -.65f), new Vector3(.62f, .42f, 1.5f), Quaternion.identity, .6f);        // track
            black.Box(0, new Vector3(0, .92f, -.55f), new Vector3(.72f, .18f, .9f), Quaternion.identity, .8f);         // seat
            for (int i = -1; i <= 1; i += 2)
                black.Box(0, new Vector3(i * .42f, .09f, 1f), new Vector3(.16f, .08f, 1.3f), Quaternion.identity, .8f); // skis
            Part(t, "Track", black, Rubber);
            var bars = new MeshBuilder(1);
            bars.Tube(0, new Vector3(-.35f, 1.22f, .5f), new Vector3(.35f, 1.22f, .5f), .025f, .025f, 6, 1f);
            bars.Box(0, new Vector3(0, .95f, -1.25f), new Vector3(.32f, .42f, .3f), Quaternion.identity, 1f);          // fuel can
            Part(t, "Bars", bars, Steel);
            Solid(t, "Body", new Vector3(0, .6f, -.1f), new Vector3(.9f, 1.1f, 3.2f));
            return Save(root);
        }

        // ── route furniture ───────────────────────────────────────────────────────────────────────────────
        /// <summary>A route wand: a bamboo pole with a red flag, the only marking above Garabashi.</summary>
        static GameObject Wand()
        {
            var root = new GameObject("Elb_Wand");
            var t = root.transform;
            var pole = new MeshBuilder(1);
            pole.Tube(0, Vector3.zero, new Vector3(.04f, 1.75f, .02f), .022f, .018f, 6, 1f, 0, true);
            Part(t, "Pole", pole, Bamboo);
            var flag = new MeshBuilder(1);
            flag.Quad(0, new Vector3(.04f, 1.38f, .02f), new Vector3(.34f, 1.44f, .06f), new Vector3(.34f, 1.7f, .06f), new Vector3(.04f, 1.72f, .02f),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Flag", flag, Flag);
            return Save(root);
        }

        /// <summary>Station board: the name and the altitude on a steel post.</summary>
        static GameObject Sign()
        {
            var root = new GameObject("Elb_Sign");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Tube(0, Vector3.zero, new Vector3(0, 2.3f, 0), .06f, .055f, 8, 1f, 0, true);
            mb.Box(0, new Vector3(0, 2.15f, 0), new Vector3(2.4f, .7f, .08f), Quaternion.identity, 1f);
            Part(t, "Post", mb, Steel);
            var label = new GameObject("Label"); label.transform.SetParent(t, false);
            label.transform.localPosition = new Vector3(0, 2.15f, -.07f);
            label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var tm = label.AddComponent<TextMesh>();
            tm.text = "Эльбрус"; tm.characterSize = .06f; tm.fontSize = 90; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center; tm.color = new Color(.06f, .08f, .1f);
            return Save(root);
        }
    }
}
