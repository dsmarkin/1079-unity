using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>The small stuff of the southern slope — what a visitor walks past without looking at it, and what makes
    /// the place feel like a working resort rather than a set: cars and buses on the Azau car park, the queue barriers and
    /// turnstiles in front of the stations, the service yards of Кругозор, Мир and Гара-Баши (drums, crates, gas bottles,
    /// snow-cat sleds, floodlights, antennas) and the line furniture between them (power poles, snow fences, rubble).
    /// <see cref="ElbrusFactory"/> builds the buildings; this file builds everything else, and the plinths the buildings
    /// stand on where the slope falls away under them.
    /// Prefabs go to Resources/World/Prefabs/Elbrus beside the rest. Pivot = the point on the ground the runtime places,
    /// +Z = the front of the thing (or the direction of travel), +X = along a fence or a railing.</summary>
    public static class ElbrusProps
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Elbrus";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Elbrus";
        static int meshCounter;

        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            Directory.CreateDirectory(MeshDir);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string meshName = $"{parent.root.name}_{name}_p{meshCounter++}";
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

        /// <summary>A line of text on a board. The text material ignores the depth buffer, so a board can carry only one
        /// copy — the back one would show through. +Z of a board faces the reader, which is downhill.</summary>
        static GameObject Label(Transform t, string text, Vector3 local, float turn, float size, Color color)
        {
            var go = new GameObject("Label"); go.transform.SetParent(t, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, turn + 180f, 0);
            var tm = go.AddComponent<TextMesh>();
            go.AddComponent<Height1079.Runtime.SignText>();   // paint on a board, not text through walls
            tm.text = text; tm.characterSize = size; tm.fontSize = 80;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            return go;
        }

        // ── materials ─────────────────────────────────────────────────────────────────────────────────────
        // Own names (ElbP*), so this file never fights ElbrusFactory over the same material asset.
        static Material Steel => Materials.Get("ElbPSteel", new Color(.46f, .48f, .5f), smoothness: .55f);
        static Material Alu => Materials.Get("ElbPAlu", new Color(.74f, .76f, .79f), smoothness: .62f);
        static Material Rust => Materials.Get("ElbPRust", new Color(.45f, .3f, .21f), smoothness: .16f);
        static Material Concrete => Materials.Get("ElbPConcrete", new Color(.63f, .62f, .59f), smoothness: .08f);
        static Material ConcreteDark => Materials.Get("ElbPConcreteDark", new Color(.47f, .46f, .44f), smoothness: .07f);
        static Material Glass => Materials.Get("ElbPGlass", new Color(.14f, .2f, .26f), smoothness: .92f);
        static Material Rubber => Materials.Get("ElbPRubber", new Color(.09f, .09f, .1f), smoothness: .12f);
        static Material Plank => Materials.PH("ElbPPlank", "raw_plank_wall", new Color(.74f, .6f, .44f), 1.6f);
        static Material PlankGrey => Materials.PH("ElbPPlankGrey", "raw_plank_wall", new Color(.5f, .48f, .44f), 1.6f);
        static Material Log => Materials.Get("ElbPLog", new Color(.62f, .5f, .36f), smoothness: .06f);
        static Material Orange => Materials.Get("ElbPOrange", new Color(.87f, .5f, .09f), smoothness: .45f);
        static Material Red => Materials.Get("ElbPRed", new Color(.72f, .17f, .14f), smoothness: .4f);
        static Material White => Materials.Get("ElbPWhite", new Color(.89f, .9f, .89f), smoothness: .45f);
        static Material Blue => Materials.Get("ElbPBlue", new Color(.16f, .33f, .56f), smoothness: .45f);
        static Material Green => Materials.Get("ElbPGreen", new Color(.21f, .37f, .27f), smoothness: .35f);
        static Material Khaki => Materials.Get("ElbPKhaki", new Color(.36f, .38f, .28f), smoothness: .35f);
        static Material Silver => Materials.Get("ElbPSilver", new Color(.63f, .65f, .67f), smoothness: .6f);
        static Material Yellow => Materials.Get("ElbPYellow", new Color(.86f, .69f, .13f), smoothness: .4f);
        static Material Tarp => Materials.Get("ElbPTarp", new Color(.82f, .8f, .74f), smoothness: .08f);
        static Material Lamp => Materials.Get("ElbPLampGlass", new Color(.95f, .93f, .82f), smoothness: .85f);

        // ── little helpers ────────────────────────────────────────────────────────────────────────────────
        static void B(MeshBuilder mb, int s, Vector3 c, Vector3 size, float uv = .8f) => mb.Box(s, c, size, Quaternion.identity, uv);
        static void BR(MeshBuilder mb, int s, Vector3 c, Vector3 size, Quaternion rot, float uv = .8f) => mb.Box(s, c, size, rot, uv);

        /// <summary>A post or a pipe, capped at b.</summary>
        static void T(MeshBuilder mb, int s, Vector3 a, Vector3 b, float r, int sides = 8) => mb.Tube(s, a, b, r, r, sides, 1f, 0, true);

        /// <summary>A road wheel across the local X axis, capped on the outside face.</summary>
        static void Wheel(MeshBuilder mb, int s, float x, float y, float z, float r, float w)
        {
            float sgn = x >= 0 ? 1f : -1f;
            mb.Tube(s, new Vector3(x - sgn * w / 2, y, z), new Vector3(x + sgn * w / 2, y, z), r, r, 10, 1f, 0, true);
        }

        /// <summary>A pane on a side wall: a double-sided quad in the plane x = <paramref name="x"/>.</summary>
        static void PaneX(MeshBuilder mb, int s, float x, float y0, float y1, float z0, float z1)
            => mb.Quad(s, new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);

        /// <summary>A pane on an end wall: a double-sided quad in the plane z = <paramref name="z"/>.</summary>
        static void PaneZ(MeshBuilder mb, int s, float z, float y0, float y1, float x0, float x1)
            => mb.Quad(s, new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);

        /// <summary>A four-sided band between two centred rectangles — the taper of a car body or a sled tub.</summary>
        static void RingBox(MeshBuilder mb, int s, float y0, float y1, float w0, float l0, float w1, float l1, float zc = 0f)
        {
            Vector3 A(float y, float w, float l, int sx, int sz) => new Vector3(sx * w, y, zc + sz * l);
            mb.Quad(s, A(y0, w0, l0, 1, 1), A(y0, w0, l0, 1, -1), A(y1, w1, l1, 1, -1), A(y1, w1, l1, 1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, -1), A(y0, w0, l0, -1, 1), A(y1, w1, l1, -1, 1), A(y1, w1, l1, -1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, 1), A(y0, w0, l0, 1, 1), A(y1, w1, l1, 1, 1), A(y1, w1, l1, -1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, 1, -1), A(y0, w0, l0, -1, -1), A(y1, w1, l1, -1, -1), A(y1, w1, l1, 1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        public static void Build()
        {
            Plinth();
            Car("Elb_Car_White", White, false);
            Car("Elb_Car_Silver", Silver, false);
            Car("Elb_Car_Red", Red, false);
            Car("Elb_Car_Suv", Khaki, true);
            Van();
            Bus();
            Fence();
            Barrier();
            Bin();
            BinBarrel();
            InfoBoard();
            FlagPole();
            Turnstile();
            QueueRail();
            Rack("Elb_RentStand", true);
            Rack("Elb_SkiRack", false);
            Woodpile();
            Drum();
            SignPost();
            Trailer();
            Transformer();
            Container();
            Floodlight();
            Banner();
            CrateStack();
            GasCage();
            Sled();
            Deck();
            Skip();
            Windsock();
            Antenna();
            ChainPosts();
            SnowFence();
            Pylon();
            Rubble();
        }

        // ── the plinth every building on a slope needs ────────────────────────────────────────────────────
        /// <summary>A concrete pad under a building: a unit block the runtime scales in X/Z to the footprint and in Y to
        /// the drop it has to close, so nothing stands on one corner and nothing sinks into the hill. Pivot = the middle
        /// of the TOP face (the level the building sits at); the block grows downwards into the ground.
        /// Deliberately untextured: it is scaled by tens of metres and any tiling would smear.</summary>
        static GameObject Plinth()
        {
            var root = new GameObject("Elb_Plinth");
            var t = root.transform;
            var mb = new MeshBuilder(2);
            B(mb, 0, new Vector3(0, -.5f, 0), new Vector3(1f, 1f, 1f), 1f);            // the body, buried on the uphill side
            B(mb, 0, new Vector3(0, -.86f, 0), new Vector3(1.06f, .28f, 1.06f), 1f);   // a stepped footing at the bottom
            B(mb, 1, new Vector3(0, -.05f, 0), new Vector3(1.04f, .1f, 1.04f), 1f);    // the cap the building stands on
            Part(t, "Block", mb, ConcreteDark, Concrete);
            Solid(t, "Block", new Vector3(0, -.5f, 0), new Vector3(1f, 1f, 1f));
            return Save(root);
        }

        // ── the Azau car park ─────────────────────────────────────────────────────────────────────────────
        /// <summary>A car on the meadow: the cheap kind that drives up from Terskol, or a high-clearance one with roof
        /// rails. Pivot on the road under the middle of the car, +Z = the way it faces.</summary>
        static GameObject Car(string name, Material paint, bool suv)
        {
            var root = new GameObject(name);
            var t = root.transform;
            float w = suv ? 1.9f : 1.72f, len = suv ? 4.5f : 4.25f;
            float sill = suv ? .62f : .46f, belt = suv ? 1.34f : 1.06f, roof = suv ? 1.98f : 1.48f;
            float hw = w / 2, hl = len / 2;
            float cabZ = suv ? -.1f : -.25f, cabL = suv ? len * .46f : len * .42f;

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, (sill + belt) / 2, 0), new Vector3(w, belt - sill, len), Quaternion.identity, .6f);
            // bonnet and boot: the same panel line carried forward and back, a little lower than the belt
            body.Box(0, new Vector3(0, belt + .05f, hl - len * .17f), new Vector3(w - .12f, .1f, len * .34f), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, belt + .05f, -hl + len * .14f), new Vector3(w - .12f, .1f, len * .28f), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, (belt + roof) / 2, cabZ), new Vector3(w - .16f, roof - belt, cabL), Quaternion.identity, .6f);
            body.Box(0, new Vector3(0, roof + .02f, cabZ), new Vector3(w - .34f, .06f, cabL - .4f), Quaternion.identity, .6f);
            if (suv)
                for (int i = -1; i <= 1; i += 2)
                    body.Box(0, new Vector3(i * (w / 2 - .26f), roof + .06f, cabZ), new Vector3(.07f, .09f, cabL - .6f), Quaternion.identity, 1f);
            Part(t, "Body", body, paint);

            var glass = new MeshBuilder(1);
            float cabHalf = (w - .16f) / 2;
            for (int i = -1; i <= 1; i += 2)
                PaneX(glass, 0, i * (cabHalf + .02f), belt + .08f, roof - .1f, cabZ - cabL / 2 + .12f, cabZ + cabL / 2 - .12f);
            PaneZ(glass, 0, cabZ + cabL / 2 + .02f, belt + .08f, roof - .1f, -(cabHalf - .1f), cabHalf - .1f);
            PaneZ(glass, 0, cabZ - cabL / 2 - .02f, belt + .08f, roof - .1f, -(cabHalf - .1f), cabHalf - .1f);
            Part(t, "Glass", glass, Glass);

            var dark = new MeshBuilder(1);
            float wr = suv ? .36f : .31f;
            Wheel(dark, 0, -(hw - .12f), wr, hl - len * .22f, wr, .2f);
            Wheel(dark, 0, hw - .12f, wr, hl - len * .22f, wr, .2f);
            Wheel(dark, 0, -(hw - .12f), wr, -hl + len * .2f, wr, .2f);
            Wheel(dark, 0, hw - .12f, wr, -hl + len * .2f, wr, .2f);
            dark.Box(0, new Vector3(0, sill + .06f, hl + .02f), new Vector3(w - .06f, .22f, .12f), Quaternion.identity, 1f);
            dark.Box(0, new Vector3(0, sill + .06f, -hl - .02f), new Vector3(w - .06f, .22f, .12f), Quaternion.identity, 1f);
            Part(t, "Wheels", dark, Rubber);

            var lights = new MeshBuilder(2);
            for (int i = -1; i <= 1; i += 2)
            {
                lights.Box(0, new Vector3(i * (hw - .3f), belt - .1f, hl - .02f), new Vector3(.4f, .17f, .1f), Quaternion.identity, 1f);
                lights.Box(1, new Vector3(i * (hw - .3f), belt - .1f, -hl + .02f), new Vector3(.4f, .17f, .1f), Quaternion.identity, 1f);
            }
            Part(t, "Lights", lights, Lamp, Red);

            Solid(t, "Hull", new Vector3(0, (sill + roof) / 2, 0), new Vector3(w, roof - sill + .4f, len));
            return Save(root);
        }

        /// <summary>«Газель» — the minibus that carries everyone up from the valley: a box on wheels, a band of windows,
        /// a blue stripe along the side and a roof rack of skis and bags.</summary>
        static GameObject Van()
        {
            var root = new GameObject("Elb_Van");
            var t = root.transform;
            const float W = 2.05f, L = 5.6f, Floor = .62f, Roof = 2.62f;
            float hw = W / 2, hl = L / 2;

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, (Floor + Roof) / 2, -.25f), new Vector3(W, Roof - Floor, L - 1.1f), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, (Floor + 1.65f) / 2, hl - .55f), new Vector3(W - .1f, 1.65f - Floor, 1.1f), Quaternion.identity, .5f);  // bonnet
            body.Box(0, new Vector3(0, Roof + .04f, -.25f), new Vector3(W - .12f, .08f, L - 1.3f), Quaternion.identity, .5f);
            Part(t, "Body", body, White);

            var stripe = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                stripe.Box(0, new Vector3(i * (hw + .01f), 1.12f, -.25f), new Vector3(.04f, .26f, L - 1.2f), Quaternion.identity, 1f);
            Part(t, "Stripe", stripe, Blue);

            var glass = new MeshBuilder(1);
            float nose = -.25f + (L - 1.1f) / 2f, tail = -.25f - (L - 1.1f) / 2f;   // the ends of the box body
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 3; k++)
                {
                    float z0 = -hl + .6f + k * 1.35f;
                    PaneX(glass, 0, i * (hw + .02f), 1.55f, 2.25f, z0, z0 + 1.1f);
                }
            PaneZ(glass, 0, nose + .02f, 1.7f, 2.45f, -(hw - .14f), hw - .14f);   // windscreen, above the bonnet
            PaneZ(glass, 0, tail - .02f, 1.55f, 2.3f, -(hw - .18f), hw - .18f);   // the back doors
            Part(t, "Glass", glass, Glass);

            var dark = new MeshBuilder(1);
            Wheel(dark, 0, -(hw - .1f), .4f, hl - 1.1f, .4f, .24f);
            Wheel(dark, 0, hw - .1f, .4f, hl - 1.1f, .4f, .24f);
            Wheel(dark, 0, -(hw - .08f), .4f, -hl + 1.5f, .4f, .38f);
            Wheel(dark, 0, hw - .08f, .4f, -hl + 1.5f, .4f, .38f);
            dark.Box(0, new Vector3(0, .8f, hl + .04f), new Vector3(W - .04f, .3f, .14f), Quaternion.identity, 1f);
            Part(t, "Wheels", dark, Rubber);

            var rack = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                rack.Box(0, new Vector3(i * (hw - .22f), Roof + .16f, -.25f), new Vector3(.06f, .16f, L - 1.8f), Quaternion.identity, 1f);
            for (int k = 0; k < 4; k++)
                rack.Box(0, new Vector3(0, Roof + .16f, -1.6f + k * .95f), new Vector3(W - .34f, .06f, .07f), Quaternion.identity, 1f);
            Part(t, "Rack", rack, Steel);

            var load = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
                load.Box(0, new Vector3(-.35f + k * .2f, Roof + .3f, -.3f), new Vector3(.1f, .04f, 1.75f), Quaternion.Euler(0, 2f * k, 0), 1f);
            Part(t, "Load", load, Tarp);

            Solid(t, "Hull", new Vector3(0, (Floor + Roof) / 2, -.1f), new Vector3(W, Roof - Floor + .6f, L));
            return Save(root);
        }

        /// <summary>The service bus from Терскол: an old city bus that turns round at the end of the car park.</summary>
        static GameObject Bus()
        {
            var root = new GameObject("Elb_Bus");
            var t = root.transform;
            const float W = 2.5f, L = 9.6f, Floor = .72f, Roof = 3.05f;
            float hw = W / 2, hl = L / 2;

            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, (Floor + Roof) / 2, 0), new Vector3(W, Roof - Floor, L), Quaternion.identity, .4f);
            body.Box(0, new Vector3(0, Roof + .05f, 0), new Vector3(W - .16f, .1f, L - .3f), Quaternion.identity, .4f);
            body.Box(0, new Vector3(0, Floor - .12f, 0), new Vector3(W - .2f, .24f, L - .8f), Quaternion.identity, .4f);
            Part(t, "Body", body, White);

            var stripe = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                stripe.Box(0, new Vector3(i * (hw + .01f), 1.25f, 0), new Vector3(.04f, .34f, L - .2f), Quaternion.identity, 1f);
            stripe.Box(0, new Vector3(0, 1.25f, -hl - .01f), new Vector3(W - .1f, .34f, .04f), Quaternion.identity, 1f);
            Part(t, "Stripe", stripe, Red);

            var glass = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 5; k++)
                {
                    float z0 = -hl + .6f + k * 1.7f;
                    PaneX(glass, 0, i * (hw + .02f), 1.72f, 2.62f, z0, z0 + 1.4f);
                }
            PaneZ(glass, 0, hl + .02f, 1.62f, 2.72f, -(hw - .18f), hw - .18f);
            PaneZ(glass, 0, -hl - .02f, 1.72f, 2.62f, -(hw - .18f), hw - .18f);
            Part(t, "Glass", glass, Glass);

            var dark = new MeshBuilder(1);
            Wheel(dark, 0, -(hw - .1f), .5f, hl - 1.6f, .5f, .28f);
            Wheel(dark, 0, hw - .1f, .5f, hl - 1.6f, .5f, .28f);
            for (int k = 0; k < 2; k++)
            {
                Wheel(dark, 0, -(hw - .08f), .5f, -hl + 2f + k * .58f, .5f, .26f);
                Wheel(dark, 0, hw - .08f, .5f, -hl + 2f + k * .58f, .5f, .26f);
            }
            dark.Box(0, new Vector3(0, .95f, hl + .05f), new Vector3(W - .06f, .34f, .12f), Quaternion.identity, 1f);
            Part(t, "Wheels", dark, Rubber);

            var board = new MeshBuilder(1);
            board.Box(0, new Vector3(0, 2.82f, hl + .03f), new Vector3(1.5f, .28f, .06f), Quaternion.identity, 1f);
            Part(t, "Board", board, Steel);
            Label(t, "ТЕРСКОЛ — АЗАУ", new Vector3(0, 2.82f, hl + .08f), 0f, .035f, new Color(.93f, .92f, .86f));

            Solid(t, "Hull", new Vector3(0, (Floor + Roof) / 2, 0), new Vector3(W, Roof - Floor + .7f, L));
            return Save(root);
        }

        /// <summary>Four metres of the car-park fence: tube posts, two rails and a mesh of flats, the paint half gone.
        /// Runs along the local X axis, like <c>Elb_Rail</c>.</summary>
        static GameObject Fence()
        {
            var root = new GameObject("Elb_Fence");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                T(mb, 0, new Vector3(i * 2f, 0, 0), new Vector3(i * 2f, 1.65f, 0), .055f);
            mb.Tube(0, new Vector3(-2f, 1.6f, 0), new Vector3(2f, 1.6f, 0), .04f, .04f, 6, 1f);
            mb.Tube(0, new Vector3(-2f, .25f, 0), new Vector3(2f, .25f, 0), .035f, .035f, 6, 1f);
            for (int k = 0; k < 13; k++)
                mb.Box(0, new Vector3(-1.86f + k * .31f, .92f, 0), new Vector3(.03f, 1.34f, .03f), Quaternion.identity, 1f);
            Part(t, "Fence", mb, Steel);
            Solid(t, "Fence", new Vector3(0, .85f, 0), new Vector3(4f, 1.7f, .1f));
            return Save(root);
        }

        /// <summary>The boom that closes the car park: a counterweighted red-and-white pole on a post, the boom along +X.</summary>
        static GameObject Barrier()
        {
            var root = new GameObject("Elb_Barrier");
            var t = root.transform;
            var post = new MeshBuilder(1);
            post.Box(0, new Vector3(0, .1f, 0), new Vector3(.6f, .2f, .6f), Quaternion.identity, 1f);
            post.Box(0, new Vector3(0, .65f, 0), new Vector3(.34f, .9f, .34f), Quaternion.identity, 1f);
            post.Box(0, new Vector3(-.45f, 1.02f, 0), new Vector3(.6f, .22f, .26f), Quaternion.identity, 1f);   // counterweight
            Part(t, "Post", post, Steel);
            var boom = new MeshBuilder(2);
            for (int k = 0; k < 9; k++)
                boom.Box(k % 2, new Vector3(.35f + k * .5f, 1.06f, 0), new Vector3(.5f, .11f, .11f), Quaternion.identity, 1f);
            Part(t, "Boom", boom, Red, White);
            Solid(t, "Post", new Vector3(0, .6f, 0), new Vector3(.45f, 1.2f, .45f));
            return Save(root);
        }

        /// <summary>A street bin on a post — the square has one every twenty metres and they are always full.</summary>
        static GameObject Bin()
        {
            var root = new GameObject("Elb_Bin");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            T(mb, 0, Vector3.zero, new Vector3(0, .62f, 0), .045f);
            mb.Tube(0, new Vector3(0, .58f, 0), new Vector3(0, 1.08f, 0), .21f, .24f, 10, 1f, 0, true);
            mb.Tube(0, new Vector3(0, 1.06f, 0), new Vector3(0, 1.1f, 0), .26f, .26f, 10, 1f);
            Part(t, "Bin", mb, Green);
            return Save(root);
        }

        /// <summary>A cut-down oil drum used as a bin: the standard rubbish bin of every yard on the mountain.</summary>
        static GameObject BinBarrel()
        {
            var root = new GameObject("Elb_Bin_Barrel");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Tube(0, new Vector3(0, .02f, 0), new Vector3(0, .84f, 0), .295f, .295f, 12, 1f);
            for (int k = 0; k < 2; k++)
                mb.Tube(0, new Vector3(0, .28f + k * .3f, 0), new Vector3(0, .34f + k * .3f, 0), .31f, .31f, 12, 1f);
            mb.Tube(0, new Vector3(0, .0f, 0), new Vector3(0, .06f, 0), .295f, .295f, 12, 1f, 0, true);
            Part(t, "Drum", mb, Rust);
            var junk = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
                junk.Box(0, new Vector3(-.1f + k * .07f, .8f + (k % 2) * .06f, -.08f + k * .05f), new Vector3(.22f, .16f, .2f), Quaternion.Euler(12f * k, 24f * k, 8f * k), 1f);
            Part(t, "Rubbish", junk, Tarp);
            return Save(root);
        }

        /// <summary>The board with the map of the lifts at the foot of the square: a steel frame, a white panel with the
        /// line of the ropeway painted on it and the four stations marked, and the name across the top.</summary>
        static GameObject InfoBoard()
        {
            var root = new GameObject("Elb_InfoBoard");
            var t = root.transform;
            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                T(frame, 0, new Vector3(i * 1.25f, 0, -.18f), new Vector3(i * 1.25f, 2.1f, -.1f), .055f);
            Part(t, "Frame", frame, Steel);

            var panel = new MeshBuilder(1);
            panel.Box(0, new Vector3(0, 1.62f, -.04f), new Vector3(2.9f, 1.8f, .08f), Quaternion.identity, .6f);
            Part(t, "Panel", panel, White);

            var print = new MeshBuilder(2);
            print.Box(0, new Vector3(0, 2.38f, .015f), new Vector3(2.9f, .3f, .03f), Quaternion.identity, 1f);      // title band
            // the line of the ropeway across the board, with a station at every bend
            var stops = new[] { new Vector2(-1.15f, 1.05f), new Vector2(-.35f, 1.35f), new Vector2(.45f, 1.72f), new Vector2(1.15f, 2.02f) };
            for (int k = 0; k < stops.Length; k++)
            {
                print.Box(1, new Vector3(stops[k].x, stops[k].y, .02f), new Vector3(.13f, .13f, .03f), Quaternion.identity, 1f);
                if (k == 0) continue;
                var a = stops[k - 1]; var b = stops[k];
                var mid = (a + b) / 2;
                float len = (b - a).magnitude, ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                print.Box(1, new Vector3(mid.x, mid.y, .02f), new Vector3(len, .035f, .02f), Quaternion.Euler(0, 0, ang), 1f);
            }
            Part(t, "Print", print, Blue, Red);
            Label(t, "КАНАТНЫЕ ДОРОГИ ЭЛЬБРУСА", new Vector3(0, 2.38f, .05f), 0f, .036f, new Color(.95f, .95f, .93f));
            Label(t, "Азау 2350 · Кругозор 3000\nМир 3500 · Гара-Баши 3847", new Vector3(0, .88f, .05f), 0f, .032f, new Color(.12f, .14f, .18f));
            Solid(t, "Board", new Vector3(0, 1.2f, -.06f), new Vector3(2.9f, 2.4f, .3f));
            return Save(root);
        }

        /// <summary>A flagpole of the square: the state flag on a seven-metre aluminium pole in a concrete shoe.</summary>
        static GameObject FlagPole()
        {
            var root = new GameObject("Elb_FlagPole");
            var t = root.transform;
            var pole = new MeshBuilder(1);
            pole.Box(0, new Vector3(0, .12f, 0), new Vector3(.62f, .24f, .62f), Quaternion.identity, 1f);
            pole.Tube(0, new Vector3(0, .2f, 0), new Vector3(0, 7.1f, 0), .075f, .045f, 8, 1f, 0, true);
            Part(t, "Pole", pole, Alu);

            var flag = new MeshBuilder(3);
            for (int s = 0; s < 3; s++)
            {
                float y0 = 6.28f - s * .29f, y1 = y0 - .29f;
                // a lazy wave: the loose edge hangs a little lower and swings back
                flag.Quad(s, new Vector3(.05f, y0, 0), new Vector3(1.5f, y0 - .12f, .28f), new Vector3(1.5f, y1 - .12f, .28f), new Vector3(.05f, y1, 0),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            }
            Part(t, "Flag", flag, White, Blue, Red);
            return Save(root);
        }

        /// <summary>A pair of turnstiles at the station door: the tripod, the card reader and the rail beside it.</summary>
        static GameObject Turnstile()
        {
            var root = new GameObject("Elb_Turnstile");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                mb.Box(0, new Vector3(i * .62f, .5f, 0), new Vector3(.3f, 1f, .72f), Quaternion.identity, 1f);
                mb.Box(0, new Vector3(i * .62f, 1.02f, 0), new Vector3(.36f, .06f, .78f), Quaternion.identity, 1f);
                // the tripod arms, one of them across the gap
                var hub = new Vector3(i * .5f, .96f, 0);
                for (int k = 0; k < 3; k++)
                {
                    float ang = k * 120f + 40f;
                    var dir = new Vector3(-i * Mathf.Sin(ang * Mathf.Deg2Rad), -.1f, Mathf.Cos(ang * Mathf.Deg2Rad)).normalized;
                    mb.Tube(0, hub, hub + dir * .48f, .025f, .025f, 5, 1f, 0, true);
                }
            }
            Part(t, "Frame", mb, Steel);
            var reader = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                reader.Box(0, new Vector3(i * .62f, 1.08f, .22f), new Vector3(.2f, .06f, .2f), Quaternion.Euler(-25f, 0, 0), 1f);
            Part(t, "Reader", reader, Yellow);
            Solid(t, "Left", new Vector3(-.62f, .5f, 0), new Vector3(.34f, 1.05f, .78f));
            Solid(t, "Right", new Vector3(.62f, .5f, 0), new Vector3(.34f, 1.05f, .78f));
            return Save(root);
        }

        /// <summary>Two and a half metres of crowd barrier — the queue in front of every terminal is made of these.</summary>
        static GameObject QueueRail()
        {
            var root = new GameObject("Elb_QueueRail");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                T(mb, 0, new Vector3(i * 1.2f, 0, 0), new Vector3(i * 1.2f, 1.06f, 0), .035f);
                mb.Box(0, new Vector3(i * 1.2f, .03f, 0), new Vector3(.12f, .06f, .5f), Quaternion.identity, 1f);
            }
            mb.Tube(0, new Vector3(-1.2f, 1.03f, 0), new Vector3(1.2f, 1.03f, 0), .03f, .03f, 6, 1f);
            mb.Tube(0, new Vector3(-1.2f, .58f, 0), new Vector3(1.2f, .58f, 0), .025f, .025f, 6, 1f);
            for (int k = 0; k < 7; k++)
                mb.Box(0, new Vector3(-1.05f + k * .35f, .8f, 0), new Vector3(.022f, .45f, .022f), Quaternion.identity, 1f);
            Part(t, "Rail", mb, Alu);
            return Save(root);
        }

        /// <summary>A rack of hire skis and boards. With <paramref name="shop"/> it is the hire stand of the square — the
        /// same rack with a painted sign over it; without, the plain rack that stands by every station door.</summary>
        static GameObject Rack(string name, bool shop)
        {
            var root = new GameObject(name);
            var t = root.transform;
            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                T(frame, 0, new Vector3(i * 1.1f, 0, -.3f), new Vector3(i * 1.1f, 1.5f, -.3f), .04f);
                T(frame, 0, new Vector3(i * 1.1f, 0, .3f), new Vector3(i * 1.1f, 1.1f, .3f), .04f);
                frame.Tube(0, new Vector3(i * 1.1f, 1.48f, -.3f), new Vector3(i * 1.1f, 1.08f, .3f), .03f, .03f, 5, 1f);
            }
            frame.Tube(0, new Vector3(-1.1f, 1.46f, -.3f), new Vector3(1.1f, 1.46f, -.3f), .035f, .035f, 6, 1f);
            frame.Tube(0, new Vector3(-1.1f, 1.06f, .3f), new Vector3(1.1f, 1.06f, .3f), .035f, .035f, 6, 1f);
            Part(t, "Frame", frame, Steel);

            var gear = new MeshBuilder(2);
            for (int k = 0; k < 9; k++)
            {
                float x = -.95f + k * .24f;
                float tilt = 6f + (k % 3) * 3f;
                if (k % 3 == 2)
                    gear.Box(1, new Vector3(x, .78f, .02f), new Vector3(.3f, 1.5f, .03f), Quaternion.Euler(tilt, 0, 0), 1f);      // a board
                else
                    for (int j = 0; j < 2; j++)
                        gear.Box(0, new Vector3(x + j * .06f, .8f, .02f), new Vector3(.08f, 1.65f, .025f), Quaternion.Euler(tilt, 0, 2f * j), 1f);
            }
            Part(t, "Gear", gear, Red, Blue);

            if (shop)
            {
                var sign = new MeshBuilder(1);
                for (int i = -1; i <= 1; i += 2)
                    T(sign, 0, new Vector3(i * 1.3f, 1.4f, -.4f), new Vector3(i * 1.3f, 2.5f, -.4f), .04f);
                sign.Box(0, new Vector3(0, 2.28f, -.4f), new Vector3(2.8f, .55f, .06f), Quaternion.identity, 1f);
                Part(t, "Sign", sign, Blue);
                Label(t, "ПРОКАТ · ЛЫЖИ · СНЕГОХОДЫ", new Vector3(0, 2.28f, -.45f), 180f, .038f, new Color(.95f, .95f, .92f));
            }
            Solid(t, "Rack", new Vector3(0, .75f, 0), new Vector3(2.4f, 1.5f, .8f));
            return Save(root);
        }

        /// <summary>The woodpile behind a café: split birch and pine stacked between two stakes, a tarpaulin on top.</summary>
        static GameObject Woodpile()
        {
            var root = new GameObject("Elb_Woodpile");
            var t = root.transform;
            var logs = new MeshBuilder(1);
            for (int row = 0; row < 5; row++)
                for (int k = 0; k < 8; k++)
                {
                    float y = .14f + row * .23f;
                    float x = -1.4f + k * .4f + (row % 2) * .06f;
                    float r = .1f + ((row + k) % 3) * .015f;
                    var a = new Vector3(x, y, -.52f); var b = new Vector3(x, y, .52f);
                    logs.Tube(0, a, b, r, r, 6, 1f, 0, true);      // both ends capped: a sawn end is what you see
                    logs.Tube(0, b, a, r, r, 6, 1f, 0, true);
                }
            Part(t, "Logs", logs, Log);
            var stakes = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    T(stakes, 0, new Vector3(i * 1.72f, 0, j * .5f), new Vector3(i * 1.72f, 1.3f, j * .5f), .05f, 6);
            Part(t, "Stakes", stakes, Plank);
            var cover = new MeshBuilder(1);
            cover.Quad(0, new Vector3(-1.75f, 1.3f, -.66f), new Vector3(1.75f, 1.3f, -.66f), new Vector3(1.75f, 1.22f, .66f), new Vector3(-1.75f, 1.22f, .66f),
                Vector2.zero, new Vector2(2, 0), new Vector2(2, 1), Vector2.up, true);
            Part(t, "Cover", cover, Tarp);
            Solid(t, "Pile", new Vector3(0, .6f, 0), new Vector3(3.2f, 1.2f, 1.1f));
            return Save(root);
        }

        /// <summary>A 200-litre drum: diesel for the generators, petrol for the snowmobiles. They stand everywhere.</summary>
        static GameObject Drum()
        {
            var root = new GameObject("Elb_Drum");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Tube(0, new Vector3(0, .02f, 0), new Vector3(0, .88f, 0), .295f, .295f, 12, 1f, 0, true);
            mb.Tube(0, new Vector3(0, .0f, 0), new Vector3(0, .05f, 0), .295f, .295f, 12, 1f, 0, true);
            for (int k = 0; k < 2; k++)
                mb.Tube(0, new Vector3(0, .3f + k * .3f, 0), new Vector3(0, .36f + k * .3f, 0), .315f, .315f, 12, 1f);
            Part(t, "Drum", mb, Blue);
            var cap = new MeshBuilder(1);
            cap.Tube(0, new Vector3(.16f, .87f, .04f), new Vector3(.16f, .92f, .04f), .05f, .05f, 6, 1f, 0, true);
            Part(t, "Cap", cap, Rust);
            return Save(root);
        }

        /// <summary>A fingerpost of the square: arms to the stations, to the café row and down the road to Терскол.</summary>
        static GameObject SignPost()
        {
            var root = new GameObject("Elb_SignPost");
            var t = root.transform;
            var post = new MeshBuilder(1);
            post.Box(0, new Vector3(0, .1f, 0), new Vector3(.4f, .2f, .4f), Quaternion.identity, 1f);
            T(post, 0, new Vector3(0, .15f, 0), new Vector3(0, 2.75f, 0), .065f);
            Part(t, "Post", post, Steel);

            var arms = new MeshBuilder(3);
            string[] text = { "КАНАТНАЯ ДОРОГА", "ТЕРСКОЛ 4 км", "КАФЕ · ПРОКАТ" };
            float[] turn = { 0f, 132f, -108f };
            float[] high = { 2.42f, 2.06f, 1.7f };
            for (int k = 0; k < 3; k++)
            {
                var rot = Quaternion.Euler(0, turn[k], 0);
                arms.Box(k, rot * new Vector3(0, high[k], .78f), new Vector3(.08f, .3f, 1.5f), rot, 1f);
                Label(t, text[k], rot * new Vector3(.05f, high[k], .78f), turn[k] + 90f, .026f, new Color(.96f, .96f, .93f));
                Label(t, text[k], rot * new Vector3(-.05f, high[k], .78f), turn[k] - 90f, .026f, new Color(.96f, .96f, .93f));
            }
            Part(t, "Arms", arms, Blue, Green, Red);
            return Save(root);
        }

        /// <summary>The low-bed trailer a snow-cat is hauled down the valley on: a deck at one metre, ramps at the back,
        /// four wheels and a drawbar. The runtime parks a snow-cat on it.</summary>
        static GameObject Trailer()
        {
            var root = new GameObject("Elb_Trailer");
            var t = root.transform;
            const float W = 2.7f, L = 7.2f, Deck = 1f;
            float hw = W / 2, hl = L / 2;

            var frame = new MeshBuilder(1);
            frame.Box(0, new Vector3(0, Deck - .12f, 0), new Vector3(W, .24f, L), Quaternion.identity, .5f);
            for (int i = -1; i <= 1; i += 2)
                frame.Box(0, new Vector3(i * (hw - .08f), Deck + .12f, 0), new Vector3(.14f, .3f, L), Quaternion.identity, .6f);
            frame.Box(0, new Vector3(0, Deck - .18f, hl + .9f), new Vector3(.4f, .22f, 1.8f), Quaternion.identity, .6f);  // drawbar
            frame.Tube(0, new Vector3(0, Deck - .32f, hl + 1.75f), new Vector3(0, Deck - .5f, hl + 1.75f), .09f, .09f, 8, 1f, 0, true);
            for (int i = -1; i <= 1; i += 2)
                frame.Box(0, new Vector3(i * .75f, Deck - .32f, -hl - .7f), new Vector3(.7f, .1f, 1.7f), Quaternion.Euler(24f, 0, 0), .6f);  // ramps
            Part(t, "Frame", frame, Steel);

            var deck = new MeshBuilder(1);
            for (int k = 0; k < 11; k++)
                deck.Box(0, new Vector3(0, Deck - .01f, -hl + .35f + k * .65f), new Vector3(W - .34f, .06f, .56f), Quaternion.identity, .8f);
            Part(t, "Deck", deck, Plank);

            var dark = new MeshBuilder(1);
            for (int k = 0; k < 2; k++)
            {
                Wheel(dark, 0, -(hw - .02f), .45f, -.9f + k * 1.05f, .45f, .26f);
                Wheel(dark, 0, hw - .02f, .45f, -.9f + k * 1.05f, .45f, .26f);
            }
            Part(t, "Wheels", dark, Rubber);

            Solid(t, "Deck", new Vector3(0, Deck - .12f, 0), new Vector3(W, .3f, L));
            return Save(root);
        }

        /// <summary>The transformer kiosk that feeds the station: a concrete box with a steel door, a warning triangle,
        /// insulators on the roof and the cable riser up the back.</summary>
        static GameObject Transformer()
        {
            var root = new GameObject("Elb_Transformer");
            var t = root.transform;
            const float W = 2.9f, D = 2.4f, H = 2.7f;
            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .12f, 0), new Vector3(W + .5f, .24f, D + .5f), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .24f + H / 2, 0), new Vector3(W, H, D), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .24f + H + .08f, 0), new Vector3(W + .35f, .16f, D + .35f), Quaternion.identity, .5f);
            Part(t, "Body", body, Concrete);

            var steel = new MeshBuilder(1);
            steel.Box(0, new Vector3(-.6f, 1.3f, D / 2 + .04f), new Vector3(1.3f, 2.1f, .08f), Quaternion.identity, 1f);       // door
            for (int k = 0; k < 3; k++)
                steel.Box(0, new Vector3(.75f, .9f + k * .5f, D / 2 + .05f), new Vector3(.9f, .3f, .05f), Quaternion.Euler(0, 0, 0), 1f);  // louvres
            steel.Tube(0, new Vector3(-W / 2 - .08f, .3f, -D / 2 + .3f), new Vector3(-W / 2 - .08f, H + .3f, -D / 2 + .3f), .07f, .07f, 6, 1f, 0, true);
            Part(t, "Steel", steel, Steel);

            var top = new MeshBuilder(1);
            for (int k = -1; k <= 1; k++)
                top.Tube(0, new Vector3(k * .7f, H + .32f, -.5f), new Vector3(k * .7f, H + .62f, -.5f), .07f, .05f, 6, 1f, 0, true);
            Part(t, "Insulators", top, White);

            var warn = new MeshBuilder(1);
            warn.Box(0, new Vector3(-.6f, 1.95f, D / 2 + .1f), new Vector3(.34f, .34f, .03f), Quaternion.Euler(0, 0, 45f), 1f);
            Part(t, "Warning", warn, Yellow);
            Solid(t, "Body", new Vector3(0, .24f + H / 2, 0), new Vector3(W, H, D));
            return Save(root);
        }

        /// <summary>A site hut — a six-metre container: stores, the battery room of the station, the drivers' room.</summary>
        static GameObject Container()
        {
            var root = new GameObject("Elb_Container");
            var t = root.transform;
            const float W = 2.5f, L = 6.1f, H = 2.6f;
            float hw = W / 2, hl = L / 2;
            var body = new MeshBuilder(1);
            body.Box(0, new Vector3(0, .12f, 0), new Vector3(W, .24f, L), Quaternion.identity, .5f);
            body.Box(0, new Vector3(0, .24f + H / 2, 0), new Vector3(W, H, L), Quaternion.identity, .5f);
            Part(t, "Body", body, Rust);
            var ribs = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 11; k++)
                    ribs.Box(0, new Vector3(i * (hw + .03f), .24f + H / 2, -hl + .35f + k * .55f), new Vector3(.06f, H - .3f, .12f), Quaternion.identity, 1f);
            for (int k = 0; k < 4; k++)
                ribs.Box(0, new Vector3(0, .24f + H + .04f, -hl + .7f + k * 1.5f), new Vector3(W + .06f, .08f, .12f), Quaternion.identity, 1f);
            Part(t, "Ribs", ribs, Rust);
            var doors = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                doors.Box(0, new Vector3(i * .58f, 1.4f, hl + .05f), new Vector3(1.08f, 2.2f, .08f), Quaternion.identity, 1f);
            for (int i = -1; i <= 1; i += 2)
                doors.Tube(0, new Vector3(i * .2f, .5f, hl + .12f), new Vector3(i * .2f, 2.3f, hl + .12f), .035f, .035f, 6, 1f);
            Part(t, "Doors", doors, Steel);
            Solid(t, "Body", new Vector3(0, .24f + H / 2, 0), new Vector3(W, H + .24f, L));
            return Save(root);
        }

        /// <summary>A floodlight mast over a station yard: nine metres of tube, a cross-arm and three lamps.</summary>
        static GameObject Floodlight()
        {
            var root = new GameObject("Elb_Floodlight");
            var t = root.transform;
            var mast = new MeshBuilder(1);
            mast.Box(0, new Vector3(0, .15f, 0), new Vector3(.7f, .3f, .7f), Quaternion.identity, 1f);
            mast.Tube(0, new Vector3(0, .25f, 0), new Vector3(0, 9f, 0), .13f, .08f, 8, 1f, 0, true);
            mast.Tube(0, new Vector3(-.85f, 8.75f, 0), new Vector3(.85f, 8.75f, 0), .05f, .05f, 6, 1f);
            for (int k = 0; k < 6; k++)
                mast.Tube(0, new Vector3(-.12f, 1.4f + k * 1.2f, 0), new Vector3(.12f, 1.4f + k * 1.2f, 0), .02f, .02f, 4, 1f);  // ladder rungs
            Part(t, "Mast", mast, Steel);
            var heads = new MeshBuilder(1);
            for (int k = -1; k <= 1; k++)
                heads.Box(0, new Vector3(k * .78f, 8.86f, .12f), new Vector3(.42f, .3f, .24f), Quaternion.Euler(28f, k * 12f, 0), 1f);
            Part(t, "Heads", heads, Steel);
            var lens = new MeshBuilder(1);
            for (int k = -1; k <= 1; k++)
                lens.Box(0, new Vector3(k * .78f, 8.8f, .23f), new Vector3(.34f, .22f, .06f), Quaternion.Euler(28f, k * 12f, 0), 1f);
            Part(t, "Lenses", lens, Lamp);
            Solid(t, "Mast", new Vector3(0, 1.6f, 0), new Vector3(.3f, 3.2f, .3f));
            return Save(root);
        }

        /// <summary>A banner strung between two poles over the way into a station yard — a sponsor, a race, a season.</summary>
        static GameObject Banner()
        {
            var root = new GameObject("Elb_Banner");
            var t = root.transform;
            var poles = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                T(poles, 0, new Vector3(i * 3.4f, 0, 0), new Vector3(i * 3.4f, 3.9f, 0), .07f);
                poles.Tube(0, new Vector3(i * 3.4f, 3.4f, 0), new Vector3(i * 2.9f, 3.9f, 0), .035f, .035f, 5, 1f);
            }
            Part(t, "Poles", poles, Steel);
            var cloth = new MeshBuilder(1);
            // a long sag between the poles: three panels, the middle one lower
            float[] y = { 3.75f, 3.6f, 3.6f, 3.75f };
            for (int k = 0; k < 3; k++)
            {
                float x0 = -3.35f + k * 2.23f, x1 = x0 + 2.23f;
                cloth.Quad(0, new Vector3(x0, y[k] - 1.05f, 0), new Vector3(x1, y[k + 1] - 1.05f, 0), new Vector3(x1, y[k + 1], 0), new Vector3(x0, y[k], 0),
                    new Vector2(k / 3f, 0), new Vector2((k + 1) / 3f, 0), new Vector2((k + 1) / 3f, 1), new Vector2(k / 3f, 1), true);
            }
            Part(t, "Cloth", cloth, Red);
            Label(t, "ПРИЭЛЬБРУСЬЕ", new Vector3(0, 3.12f, -.03f), 180f, .09f, new Color(.96f, .95f, .9f));
            return Save(root);
        }

        /// <summary>A stack of wooden crates on a station yard: bottles up, rubbish down.</summary>
        static GameObject CrateStack()
        {
            var root = new GameObject("Elb_CrateStack");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            var rnd = new System.Random(41);
            float[] xs = { -.62f, .62f, -.55f, .6f, .02f, -.5f };
            float[] zs = { -.34f, -.3f, .36f, .38f, -.02f, .3f };
            float[] ys = { .21f, .21f, .21f, .21f, .63f, 1.05f };
            for (int k = 0; k < 6; k++)
            {
                float yaw = (float)(rnd.NextDouble() - .5) * 26f;
                mb.Box(0, new Vector3(xs[k], ys[k], zs[k]), new Vector3(1.1f, .42f, .74f), Quaternion.Euler(0, yaw, 0), 1f);
                var rot = Quaternion.Euler(0, yaw, 0);
                for (int i = -1; i <= 1; i += 2)
                    mb.Box(0, new Vector3(xs[k], ys[k], zs[k]) + rot * new Vector3(i * .54f, 0, 0), new Vector3(.06f, .46f, .78f), rot, 1f);
            }
            Part(t, "Crates", mb, Plank);
            Solid(t, "Stack", new Vector3(0, .65f, 0), new Vector3(2.2f, 1.3f, 1.3f));
            return Save(root);
        }

        /// <summary>The gas cage behind a café: propane bottles in a steel frame, chained, with a warning plate.</summary>
        static GameObject GasCage()
        {
            var root = new GameObject("Elb_GasCage");
            var t = root.transform;
            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    T(frame, 0, new Vector3(i * .85f, 0, j * .48f), new Vector3(i * .85f, 1.7f, j * .48f), .035f, 6);
            for (int k = 0; k < 3; k++)
            {
                float y = .25f + k * .65f;
                frame.Tube(0, new Vector3(-.85f, y, -.48f), new Vector3(.85f, y, -.48f), .022f, .022f, 5, 1f);
                frame.Tube(0, new Vector3(-.85f, y, .48f), new Vector3(.85f, y, .48f), .022f, .022f, 5, 1f);
            }
            frame.Box(0, new Vector3(0, 1.74f, 0), new Vector3(1.9f, .05f, 1.1f), Quaternion.identity, 1f);
            Part(t, "Cage", frame, Steel);
            var bottles = new MeshBuilder(1);
            for (int k = 0; k < 3; k++)
                for (int j = 0; j < 2; j++)
                {
                    float x = -.55f + k * .55f, z = -.2f + j * .42f;
                    bottles.Tube(0, new Vector3(x, .03f, z), new Vector3(x, 1.02f, z), .16f, .16f, 10, 1f, 0, true);
                    bottles.Tube(0, new Vector3(x, 1.02f, z), new Vector3(x, 1.2f, z), .06f, .05f, 6, 1f, 0, true);
                }
            Part(t, "Bottles", bottles, Red);
            Solid(t, "Cage", new Vector3(0, .85f, 0), new Vector3(1.8f, 1.7f, 1.05f));
            return Save(root);
        }

        /// <summary>The plastic sled a snowmobile drags: bags, boxes and people go up to the huts in these.</summary>
        static GameObject Sled()
        {
            var root = new GameObject("Elb_Sled");
            var t = root.transform;
            var tub = new MeshBuilder(1);
            RingBox(tub, 0, .12f, .52f, .42f, .92f, .5f, 1.02f);
            tub.Box(0, new Vector3(0, .13f, 0), new Vector3(.84f, .06f, 1.84f), Quaternion.identity, .8f);
            Part(t, "Tub", tub, Orange);
            var steel = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                steel.Box(0, new Vector3(i * .34f, .05f, 0), new Vector3(.1f, .1f, 2f), Quaternion.identity, 1f);
            steel.Tube(0, new Vector3(-.3f, .3f, 1.02f), new Vector3(0, .34f, 1.62f), .028f, .028f, 5, 1f);
            steel.Tube(0, new Vector3(.3f, .3f, 1.02f), new Vector3(0, .34f, 1.62f), .028f, .028f, 5, 1f);
            Part(t, "Runners", steel, Steel);
            var load = new MeshBuilder(1);
            load.Box(0, new Vector3(0, .5f, -.25f), new Vector3(.7f, .5f, 1.1f), Quaternion.Euler(0, 6f, 0), 1f);
            Part(t, "Load", load, Tarp);
            return Save(root);
        }

        /// <summary>A duckboard platform: the way across the mud and the slush by the station doors, and the floor the
        /// sledges and the crates are kept dry on.</summary>
        static GameObject Deck()
        {
            var root = new GameObject("Elb_Deck");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int k = 0; k < 12; k++)
                mb.Box(0, new Vector3(0, .3f, -1.93f + k * .35f), new Vector3(3f, .06f, .3f), Quaternion.identity, .9f);
            for (int i = -1; i <= 1; i += 2)
                mb.Box(0, new Vector3(i * 1.3f, .14f, 0), new Vector3(.18f, .28f, 4f), Quaternion.identity, .9f);
            mb.Box(0, new Vector3(0, .14f, 0), new Vector3(.18f, .28f, 4f), Quaternion.identity, .9f);
            Part(t, "Deck", mb, PlankGrey);
            Solid(t, "Deck", new Vector3(0, .17f, 0), new Vector3(3f, .34f, 4f));
            return Save(root);
        }

        /// <summary>The wheeled rubbish container of a station yard, lid half open, rust along the bottom edge.</summary>
        static GameObject Skip()
        {
            var root = new GameObject("Elb_Skip");
            var t = root.transform;
            var body = new MeshBuilder(1);
            RingBox(body, 0, .24f, 1.16f, .56f, .52f, .68f, .62f);
            body.Box(0, new Vector3(0, .26f, 0), new Vector3(1.1f, .07f, 1f), Quaternion.identity, .8f);
            Part(t, "Body", body, Green);
            var lid = new MeshBuilder(1);
            lid.Box(0, new Vector3(0, 1.32f, -.28f), new Vector3(1.4f, .06f, 1.3f), Quaternion.Euler(-22f, 0, 0), .8f);
            Part(t, "Lid", lid, Green);
            var dark = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    Wheel(dark, 0, i * .5f, .12f, j * .42f, .12f, .08f);
            dark.Tube(0, new Vector3(-.6f, .9f, .64f), new Vector3(.6f, .9f, .64f), .03f, .03f, 6, 1f);
            Part(t, "Wheels", dark, Rubber);
            Solid(t, "Body", new Vector3(0, .72f, 0), new Vector3(1.36f, .96f, 1.24f));
            return Save(root);
        }

        /// <summary>The wind sock on the station roof line: five metres of pole and an orange-and-white sleeve that says
        /// how hard it is blowing up there today.</summary>
        static GameObject Windsock()
        {
            var root = new GameObject("Elb_Windsock");
            var t = root.transform;
            var pole = new MeshBuilder(1);
            pole.Box(0, new Vector3(0, .1f, 0), new Vector3(.5f, .2f, .5f), Quaternion.identity, 1f);
            pole.Tube(0, new Vector3(0, .18f, 0), new Vector3(0, 5f, 0), .08f, .055f, 8, 1f, 0, true);
            pole.Tube(0, new Vector3(-.05f, 4.9f, 0), new Vector3(.45f, 4.9f, 0), .03f, .03f, 5, 1f);
            pole.Tube(0, new Vector3(.45f, 4.72f, 0), new Vector3(.45f, 5.08f, 0), .21f, .21f, 12, 1f);      // the ring
            Part(t, "Pole", pole, Steel);
            var sock = new MeshBuilder(2);
            // the sleeve streams away downwind in five bands
            for (int k = 0; k < 5; k++)
            {
                float x0 = .5f + k * .38f, x1 = x0 + .38f;
                float r0 = .2f - k * .022f, r1 = .2f - (k + 1) * .022f;
                float y0 = 4.9f - k * .05f, y1 = 4.9f - (k + 1) * .07f;
                sock.Tube(k % 2, new Vector3(x0, y0, 0), new Vector3(x1, y1, 0), r0, r1, 10, 1f);
            }
            Part(t, "Sock", sock, Orange, White);
            return Save(root);
        }

        /// <summary>What sits on a station roof: a dish, a pair of whips and a radio box, all of it guyed down.</summary>
        static GameObject Antenna()
        {
            var root = new GameObject("Elb_Antenna");
            var t = root.transform;
            var mast = new MeshBuilder(1);
            mast.Box(0, new Vector3(0, .06f, 0), new Vector3(.8f, .12f, .8f), Quaternion.identity, 1f);
            mast.Tube(0, new Vector3(0, .1f, 0), new Vector3(0, 3.1f, 0), .06f, .045f, 6, 1f, 0, true);
            for (int k = 0; k < 3; k++)
            {
                float ang = k * 120f;
                var foot = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad)) * .75f;
                mast.Tube(0, foot + new Vector3(0, .05f, 0), new Vector3(0, 2.5f, 0), .012f, .012f, 4, 1f);
            }
            mast.Tube(0, new Vector3(0, 3.05f, 0), new Vector3(.02f, 4.3f, .02f), .014f, .01f, 4, 1f);      // whip
            for (int k = 0; k < 5; k++)
                mast.Tube(0, new Vector3(-.28f, 2.1f + k * .16f, 0), new Vector3(.28f, 2.1f + k * .16f, 0), .01f, .01f, 4, 1f);  // yagi
            Part(t, "Mast", mast, Steel);
            var dish = new MeshBuilder(1);
            dish.Cone(0, new Vector3(.02f, 1.55f, .42f), .48f, -.22f, 12, 1f);
            dish.Tube(0, new Vector3(.02f, 1.5f, .42f), new Vector3(.02f, 1.62f, .08f), .03f, .03f, 5, 1f);
            dish.Tube(0, new Vector3(.02f, 1.33f, .3f), new Vector3(.02f, 1.33f, .5f), .05f, .05f, 6, 1f, 0, true);
            Part(t, "Dish", dish, White);
            var box = new MeshBuilder(1);
            box.Box(0, new Vector3(-.2f, .75f, -.1f), new Vector3(.34f, .5f, .22f), Quaternion.identity, 1f);
            Part(t, "Box", box, Alu);
            return Save(root);
        }

        /// <summary>Three metres of the chain that keeps people on the path: two posts and a chain sagging between them.</summary>
        static GameObject ChainPosts()
        {
            var root = new GameObject("Elb_Chain");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                T(mb, 0, new Vector3(i * 1.5f, 0, 0), new Vector3(i * 1.5f, .92f, 0), .055f);
                mb.Tube(0, new Vector3(i * 1.5f, .92f, 0), new Vector3(i * 1.5f, 1f, 0), .075f, .02f, 8, 1f, 0, true);
            }
            const int seg = 6;
            for (int k = 0; k < seg; k++)
            {
                float t0 = k / (float)seg, t1 = (k + 1) / (float)seg;
                Vector3 a = new Vector3(-1.5f + 3f * t0, .86f - .3f * 4 * t0 * (1 - t0), 0);
                Vector3 b = new Vector3(-1.5f + 3f * t1, .86f - .3f * 4 * t1 * (1 - t1), 0);
                mb.Tube(0, a, b, .018f, .018f, 4, 1f);
            }
            Part(t, "Chain", mb, Steel);
            return Save(root);
        }

        /// <summary>A snow fence: four metres of slatted timber on a braced frame, set across the wind above a road or a
        /// station so the drift builds up short of it. Dozens of them stand on the slope.</summary>
        static GameObject SnowFence()
        {
            var root = new GameObject("Elb_SnowFence");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                mb.Box(0, new Vector3(i * 1.85f, .85f, 0), new Vector3(.12f, 1.7f, .12f), Quaternion.identity, 1f);
                mb.Box(0, new Vector3(i * 1.85f, .7f, .55f), new Vector3(.1f, 1.5f, .1f), Quaternion.Euler(-32f, 0, 0), 1f);   // brace
                mb.Box(0, new Vector3(i * 1.85f, .06f, 1.1f), new Vector3(.3f, .12f, .5f), Quaternion.identity, 1f);
            }
            for (int k = 0; k < 2; k++)
                mb.Box(0, new Vector3(0, .5f + k * .95f, 0), new Vector3(3.9f, .12f, .09f), Quaternion.identity, .9f);
            for (int k = 0; k < 14; k++)
                mb.Box(0, new Vector3(-1.8f + k * .277f, .92f, -.06f), new Vector3(.12f, 1.55f, .05f), Quaternion.identity, .9f);
            Part(t, "Panel", mb, PlankGrey);
            Solid(t, "Panel", new Vector3(0, .85f, 0), new Vector3(4f, 1.7f, .2f));
            return Save(root);
        }

        /// <summary>A power pole of the line that feeds the stations: a concrete trunk, a cross-arm with three insulators
        /// and a stay to the uphill side. The runtime strings the wires between them.</summary>
        static GameObject Pylon()
        {
            var root = new GameObject("Elb_Pylon");
            var t = root.transform;
            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, 4.3f, 0), new Vector3(.34f, 8.9f, .26f), Quaternion.identity, .5f);
            mb.Box(0, new Vector3(0, .2f, 0), new Vector3(.6f, .4f, .5f), Quaternion.identity, .6f);
            Part(t, "Trunk", mb, Concrete);
            var steel = new MeshBuilder(1);
            steel.Box(0, new Vector3(0, 8.35f, 0), new Vector3(2.2f, .1f, .12f), Quaternion.identity, 1f);
            steel.Box(0, new Vector3(0, 8.05f, 0), new Vector3(1.2f, .08f, .1f), Quaternion.identity, 1f);
            steel.Tube(0, new Vector3(-1.05f, 8.3f, 0), new Vector3(-.28f, 8.02f, 0), .025f, .025f, 4, 1f);
            steel.Tube(0, new Vector3(1.05f, 8.3f, 0), new Vector3(.28f, 8.02f, 0), .025f, .025f, 4, 1f);
            steel.Tube(0, new Vector3(0, 8.6f, 0), new Vector3(0, 8.95f, 0), .04f, .04f, 5, 1f, 0, true);
            Part(t, "Arm", steel, Steel);
            var ins = new MeshBuilder(1);
            for (int k = -1; k <= 1; k++)
                ins.Tube(0, new Vector3(k * 1f, 8.4f, 0), new Vector3(k * 1f, 8.66f, 0), .075f, .06f, 6, 1f, 0, true);
            Part(t, "Insulators", ins, White);
            Solid(t, "Trunk", new Vector3(0, 4.3f, 0), new Vector3(.4f, 8.6f, .32f));
            return Save(root);
        }

        /// <summary>What is left on the pastures below the stations after forty years of building: broken blocks, a coil
        /// of rebar, a stack of boards gone grey and a length of pipe.</summary>
        static GameObject Rubble()
        {
            var root = new GameObject("Elb_Rubble");
            var t = root.transform;
            var blocks = new MeshBuilder(1);
            blocks.Box(0, new Vector3(-.75f, .22f, -.35f), new Vector3(1.2f, .44f, .6f), Quaternion.Euler(0, 14f, -4f), .6f);
            blocks.Box(0, new Vector3(-.5f, .62f, -.15f), new Vector3(1.1f, .4f, .55f), Quaternion.Euler(3f, -22f, 6f), .6f);
            blocks.Box(0, new Vector3(.45f, .2f, .5f), new Vector3(.8f, .4f, .5f), Quaternion.Euler(0, 40f, 2f), .6f);
            Part(t, "Blocks", blocks, Concrete);
            var boards = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
                boards.Box(0, new Vector3(.85f, .06f + k * .07f, -.55f), new Vector3(.24f, .06f, 2.1f), Quaternion.Euler(0, 6f * k - 12f, 0), .8f);
            Part(t, "Boards", boards, PlankGrey);
            var bars = new MeshBuilder(1);
            var rnd = new System.Random(77);
            for (int k = 0; k < 7; k++)
            {
                var a = new Vector3(-.2f + (float)rnd.NextDouble() * 1.1f, .05f, .3f + (float)rnd.NextDouble() * .8f);
                var b = a + new Vector3((float)(rnd.NextDouble() - .5) * 1.6f, (float)rnd.NextDouble() * .25f, (float)(rnd.NextDouble() - .5) * 1.4f);
                bars.Tube(0, a, b, .016f, .014f, 4, 1f);
            }
            bars.Tube(0, new Vector3(-1.3f, .12f, .55f), new Vector3(.1f, .12f, .95f), .12f, .12f, 8, 1f, 0, true);   // a length of pipe
            Part(t, "Iron", bars, Rust);
            return Save(root);
        }
    }
}
