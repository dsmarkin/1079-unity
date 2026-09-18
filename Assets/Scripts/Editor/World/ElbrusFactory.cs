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

        /// <summary>A material that glows on its own (a bulb, the open door of a stove). No GI: the emission is only
        /// what the surface itself shows, which is all a daylight map needs.</summary>
        static Material Emissive(string name, Color baseColor, Color emission, float smoothness = .3f)
        {
            var m = Materials.Get(name, baseColor, smoothness: smoothness);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Window glass you can see through, unlike the dark mirrored <see cref="Glass"/> of the stations.
        /// The renderers that use it have their shadows switched off, so the July sun reaches the café tables.</summary>
        static Material GlassClear
        {
            get
            {
                var m = Materials.Get("ElbGlassClear", new Color(.78f, .85f, .88f, .22f), smoothness: .95f);
                m.SetFloat("_Mode", 3);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        /// <summary>The glazing of a gondola cabin, and it is not glass: the Diamond is glazed in polycarbonate, and after
        /// twenty seasons of blown ice it is scratched to a haze — riders on Elbrus complain you cannot take a decent
        /// photograph through it. So this is smoky, half matt and never a mirror; a bright pane would read as a city tram.</summary>
        static Material CabinGlass
        {
            get
            {
                var m = Materials.Get("ElbCabinGlass", new Color(.56f, .6f, .62f, .44f), smoothness: .42f);
                m.SetFloat("_Mode", 3);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        // ── materials ─────────────────────────────────────────────────────────────────────────────────────
        static Material Steel => Materials.Get("ElbSteel", new Color(.46f, .48f, .5f), smoothness: .55f);
        static Material Paint => Materials.Get("ElbPaint", new Color(.82f, .28f, .2f), smoothness: .45f);
        static Material PaintBlue => Materials.Get("ElbPaintBlue", new Color(.15f, .35f, .58f), smoothness: .45f);
        static Material Cabin => Materials.Get("ElbCabin", new Color(.92f, .93f, .95f), smoothness: .5f);
        /// <summary>Anthracite: the blank skirt under the windows of a Diamond cabin and the plinths that go with it.</summary>
        static Material CabinDark => Materials.Get("ElbCabinDark", new Color(.2f, .21f, .23f), smoothness: .42f);
        /// <summary>Half a century of glacier wind on the red of the jig-back car: chalky, and a long way off fresh paint.</summary>
        static Material PaintWorn => Materials.Get("ElbPaintWorn", new Color(.64f, .23f, .17f), smoothness: .2f);
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
        static Material Chalkboard => Materials.Get("ElbChalkboard", new Color(.08f, .12f, .1f), smoothness: .06f);
        static Material LampGlow => Emissive("ElbLampGlow", new Color(.98f, .93f, .82f), new Color(1f, .82f, .55f) * 2.4f, .4f);
        static Material StoveGlow => Emissive("ElbStoveGlow", new Color(.35f, .16f, .09f), new Color(1f, .38f, .1f) * 1.8f, .15f);

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
            Cafe("Elb_Cafe", 8f, 10f, 3f);             // the shashlyk place of Azau and the café of every station
            Cafe("Elb_Cafe_Hall", 11f, 12f, 3.2f);     // the bigger hall of Mir, Krugozor and Gara-Bashi
            CafeFood();
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

        // ── rounded bodies ────────────────────────────────────────────────────────────────────────────────
        /// <summary>The plan of a cabin: a rectangle whose four vertical edges are rounded off with radius
        /// <paramref name="r"/>. Points run anticlockwise in x–z from the +x+z corner and the straights between the four
        /// arcs close the loop, so lofting two of these gives a shell with soft corners and flat sides — which is the
        /// whole difference between a gondola cabin and a packing case.</summary>
        static Vector3[] RoundedRect(float hx, float hz, float r, int seg, float y)
        {
            r = Mathf.Max(.01f, Mathf.Min(r, Mathf.Min(hx, hz) - .01f));
            var pts = new Vector3[4 * (seg + 1)];
            int w = 0;
            for (int c = 0; c < 4; c++)
            {
                float sx = c == 0 || c == 3 ? 1f : -1f, sz = c < 2 ? 1f : -1f;
                float cx = sx * (hx - r), cz = sz * (hz - r);
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                    pts[w++] = new Vector3(cx + Mathf.Cos(a) * r, y, cz + Mathf.Sin(a) * r);
                }
            }
            return pts;
        }

        /// <summary>Skins the band between two outlines of equal length — one storey of a cabin wall, or a flat ring when
        /// both outlines share a height. Faces outward (downward, for a ring read inner → outer); <paramref name="both"/>
        /// adds the inner face, which is what keeps the daylight out of the wall when the passenger sits inside it.</summary>
        static void Loft(MeshBuilder mb, int s, Vector3[] lo, Vector3[] hi, float uv = 1f, bool both = false)
        {
            float u = 0f;
            for (int i = 0; i < lo.Length; i++)
            {
                int j = (i + 1) % lo.Length;
                float du = Vector3.Distance(lo[i], lo[j]) * uv, h = Vector3.Distance(lo[i], hi[i]) * uv;
                mb.Quad(s, lo[j], lo[i], hi[i], hi[j],
                    new Vector2(u + du, 0), new Vector2(u, 0), new Vector2(u, h), new Vector2(u + du, h), both);
                u += du;
            }
        }

        /// <summary>The same outline shrunk about the body axis and moved to <paramref name="y"/> — one ring of a domed roof.</summary>
        static Vector3[] Shrink(Vector3[] ring, float k, float y)
        {
            var o = new Vector3[ring.Length];
            for (int i = 0; i < ring.Length; i++) o[i] = new Vector3(ring[i].x * k, y, ring[i].z * k);
            return o;
        }

        /// <summary>Closes an outline with a fan: a floor pan, a ceiling, the crown of a dome.</summary>
        static void Deck(MeshBuilder mb, int s, Vector3[] ring, bool up)
            => Surf.Fan(mb, s, Matrix4x4.identity, ring, up ? Vector3.up : Vector3.down, 1f);

        /// <summary>A four-sided band between two centred rectangles — the square-cornered taper of an old car body,
        /// where the rounded plan above would be wrong.</summary>
        static void RingBox(MeshBuilder mb, int s, float y0, float y1, float w0, float l0, float w1, float l1)
        {
            Vector3 A(float y, float w, float l, int sx, int sz) => new Vector3(sx * w, y, sz * l);
            mb.Quad(s, A(y0, w0, l0, 1, 1), A(y0, w0, l0, 1, -1), A(y1, w1, l1, 1, -1), A(y1, w1, l1, 1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, -1), A(y0, w0, l0, -1, 1), A(y1, w1, l1, -1, 1), A(y1, w1, l1, -1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, -1, 1), A(y0, w0, l0, 1, 1), A(y1, w1, l1, 1, 1), A(y1, w1, l1, -1, 1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            mb.Quad(s, A(y0, w0, l0, 1, -1), A(y0, w0, l0, -1, -1), A(y1, w1, l1, -1, -1), A(y1, w1, l1, 1, -1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>A shallow dome on top of an outline: two rings and a crown, 2–3 courses is all a roof this size needs.</summary>
        static void Dome(MeshBuilder mb, int s, Vector3[] eave, float rise)
        {
            float y = eave[0].y;
            var r1 = Shrink(eave, .80f, y + rise * .62f);
            var r2 = Shrink(eave, .46f, y + rise);
            Loft(mb, s, eave, r1); Loft(mb, s, r1, r2); Deck(mb, s, r2, true);
        }

        // ── cars ──────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Sigma Diamond C8 — the cabin of all three Poma gondolas of Elbrus (Azau–Krugozor, Krugozor–Mir and
        /// Mir–Gara-Bashi carry 58, 58 and 35 of them), eight seats, 730 kg empty, a 900 mm door. Three things make it
        /// itself and are worth the triangles: a body that is very nearly a 1.9 m cube with all four vertical edges
        /// rounded off to a radius of 280 mm; a metre of glazing that runs right around those corners without a break,
        /// which is what Sigma sold the Diamond on; and a shallow domed roof with a drip that overhangs the sides. Below
        /// the glass the shell is a blank anthracite skirt with vent slits, under that a rubber bumper takes the knocks
        /// of the station. The two sliding doors are glazed in the same line as the band, so a closed cabin reads as one
        /// ribbon of plastic and only the shut lines, the bottom guide rail and the step give them away. Sigma hangs the
        /// cabin on struts that pass through the roof into the saloon: the box in the ceiling and the two posts down to
        /// the seat backs are those struts, and they are the first thing a passenger notices inside.
        /// Pivot = the point <see cref="Ropeway.Hang"/> under the haul rope, where <c>RopewayRig</c> puts the car; the
        /// floor sits exactly <see cref="Ropeway.Drop"/> under the rope, the height the line profile and the station
        /// platforms are computed from. +Z = travel, doors on ±X.</summary>
        static GameObject GondolaCabin()
        {
            var root = new GameObject("Elb_Cabin_Gondola");
            var t = root.transform;

            float rope = Ropeway.Hang(RopewayKind.Gondola);                   // the haul rope runs this far above the pivot
            float floor = rope - Ropeway.Drop(RopewayKind.Gondola);           // and the floor this far below the rope
            const float HX = .95f, HZ = .95f, R = .28f;                       // 1.9 × 1.9 m in plan, corners of 280 mm
            const int Seg = 5;                                                // segments per rounded corner → 24 points a ring
            const float Sill = -.06f, Belt = .78f, Head = 1.78f, Cant = 1.88f;
            float Y(float h) => floor + h;

            var sill = RoundedRect(HX, HZ, R, Seg, Y(Sill));
            var belt = RoundedRect(HX, HZ, R, Seg, Y(Belt));
            var head = RoundedRect(HX - .012f, HZ - .012f, R, Seg, Y(Head));  // the sides lean in a little going up
            var cant = RoundedRect(HX - .02f, HZ - .02f, R, Seg, Y(Cant));

            // ── the blank skirt, the floor pan and the strake under the glass ─────────────────────────────
            var skirt = new MeshBuilder(1);
            Loft(skirt, 0, sill, belt, 1f, true);
            Loft(skirt, 0, RoundedRect(HX, HZ, R, Seg, Y(.66f)), RoundedRect(HX + .014f, HZ + .014f, R, Seg, Y(.68f)));
            Loft(skirt, 0, RoundedRect(HX + .014f, HZ + .014f, R, Seg, Y(.68f)), RoundedRect(HX, HZ, R, Seg, Y(Belt)));
            Deck(skirt, 0, RoundedRect(HX, HZ, R, Seg, floor), true);
            Part(t, "Skirt", skirt, CabinDark);

            // ── the shell above the glass, the ceiling and the domed roof ─────────────────────────────────
            var shell = new MeshBuilder(1);
            Loft(shell, 0, head, cant, 1f, true);
            Deck(shell, 0, Shrink(cant, 1f, Y(1.855f)), false);                                   // the ceiling, seen from the seat
            var eaveLo = RoundedRect(HX + .045f, HZ + .045f, R + .045f, Seg, Y(1.80f));
            var eaveHi = RoundedRect(HX + .045f, HZ + .045f, R + .045f, Seg, Y(1.90f));
            Loft(shell, 0, RoundedRect(HX - .014f, HZ - .014f, R, Seg, Y(1.80f)), eaveLo);        // under the drip
            Loft(shell, 0, eaveLo, eaveHi);                                                        // the drip itself
            Dome(shell, 0, eaveHi, .155f);
            Part(t, "Shell", shell, Cabin);

            // ── the glazing band: one ribbon, corners and doors included ──────────────────────────────────
            var glassMb = new MeshBuilder(1);
            Loft(glassMb, 0, belt, head, 1f, true);
            var pane = Part(t, "Windows", glassMb, CabinGlass);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ── rubber: the bumper right round the bottom, the vent slits, the shut lines of the doors ────
            var rubber = new MeshBuilder(1);
            var bumpLo = RoundedRect(HX + .035f, HZ + .035f, R + .035f, Seg, Y(-.155f));
            var bumpHi = RoundedRect(HX + .035f, HZ + .035f, R + .035f, Seg, Y(-.035f));
            Loft(rubber, 0, bumpLo, bumpHi);
            Loft(rubber, 0, bumpHi, RoundedRect(HX, HZ, R, Seg, Y(-.035f)));
            Deck(rubber, 0, bumpLo, false);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int k = -1; k <= 1; k += 2)                              // the door slides between these two lines
                    rubber.Box(0, new Vector3(i * (HX + .012f), Y(.87f), k * .45f), new Vector3(.03f, 1.72f, .022f), Quaternion.identity, 1f);
                rubber.Box(0, new Vector3(i * (HX + .012f), Y(1.74f), 0), new Vector3(.03f, .022f, .92f), Quaternion.identity, 1f);
                for (int k = 0; k < 4; k++)                                   // vents low in the blank skirt
                    rubber.Box(0, new Vector3(0, Y(.12f + k * .055f), i * (HZ + .008f)), new Vector3(.92f, .022f, .026f), Quaternion.identity, 1f);
            }
            Part(t, "Rubber", rubber, Rubber);

            // ── the running gear, the struts through the roof and the hanger to the grip ──────────────────
            var gear = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int k = -1; k <= 1; k += 2)                              // rollers that hold the cabin to the station rail
                    gear.Tube(0, new Vector3(k * .45f, Y(-.15f), i * (HZ + .045f)), new Vector3(k * .45f, Y(-.03f), i * (HZ + .045f)), .055f, .055f, 6, 1f, 0, true);
                gear.Box(0, new Vector3(i * (HX + .03f), Y(.02f), 0), new Vector3(.06f, .07f, 1.04f), Quaternion.identity, 1f);      // door rail
                gear.Box(0, new Vector3(i * (HX + .06f), Y(-.03f), 0), new Vector3(.12f, .035f, .94f), Quaternion.identity, 1f);     // the step under the door
                gear.Box(0, new Vector3(i * (HX + .035f), Y(1.02f), .34f), new Vector3(.05f, .05f, .2f), Quaternion.identity, 1f);   // door handle
                gear.Box(0, new Vector3(0, Y(1.28f), i * .62f), new Vector3(.11f, 1.02f, .11f), Quaternion.identity, 1f);            // strut inside the saloon
            }
            gear.Box(0, new Vector3(0, Y(1.815f), 0), new Vector3(.17f, .09f, 1.84f), Quaternion.identity, 1f);  // and the box they hang from
            gear.Box(0, new Vector3(.24f, Y(2.04f), .06f), new Vector3(.26f, .12f, .3f), Quaternion.identity, 1f); // the collar where the arm leaves the roof

            // the hanger is not a stick: a goose-neck that leaves the roof off-centre, bows out and comes back
            // vertical under the grip. Roof to rope is about 2.2 m here, set by Drop, not by the cabin.
            var path = new Vector3[7];
            for (int k = 0; k < path.Length; k++)
            {
                float u = k / (float)(path.Length - 1), e = u * u * (3f - 2f * u);
                path[k] = new Vector3(Mathf.Lerp(.24f, 0f, e), Mathf.Lerp(Y(1.86f), .16f, u), Mathf.Lerp(.06f, 0f, e));
            }
            Surf.Sweep(gear, 0, path, (a, b) => .05f, 8, 14, new Surf.Options { UvScale = new Vector2(.5f, 1f) }, capStart: false, capEnd: true);
            gear.Box(0, new Vector3(0, .19f, 0), new Vector3(.13f, .1f, .46f), Quaternion.identity, 1f);          // the shelf under the grip
            gear.Box(0, new Vector3(0, .3f, 0), new Vector3(.15f, .15f, .4f), Quaternion.identity, 1f);
            gear.Tube(0, new Vector3(0, .3f, -.21f), new Vector3(0, .3f, .21f), .085f, .085f, 8, 1f, 0, true);    // the spring pack
            gear.Box(0, new Vector3(0, rope, 0), new Vector3(.22f, .1f, .3f), Quaternion.identity, 1f);           // jaws closed on the haul rope
            gear.Box(0, new Vector3(0, .5f, 0), new Vector3(.1f, .05f, .36f), Quaternion.identity, 1f);
            for (int k = -1; k <= 1; k += 2)                                   // the rollers that open the grip in the station
                gear.Tube(0, new Vector3(-.06f, .51f, k * .14f), new Vector3(.06f, .51f, k * .14f), .055f, .055f, 8, 1f, 0, true);
            Part(t, "Gear", gear, Steel);

            // ── ski racks outside, hand rails inside ──────────────────────────────────────────────────────
            var rails = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                float z0 = i * (HZ - .01f), z1 = i * (HZ + .11f);
                foreach (float y in new[] { .30f, .58f })
                {
                    rails.Tube(0, new Vector3(-.62f, Y(y), z1), new Vector3(.62f, Y(y), z1), .018f, .018f, 5, 1f, 0, true);
                    for (int k = -1; k <= 1; k += 2)
                        rails.Tube(0, new Vector3(k * .58f, Y(y), z0), new Vector3(k * .58f, Y(y), z1), .016f, .016f, 4, 1f, 0, true);
                }
                for (int k = 0; k < 5; k++)                                    // four slots a side: four pairs of skis
                    rails.Tube(0, new Vector3(-.6f + k * .3f, Y(.3f), z1), new Vector3(-.6f + k * .3f, Y(.58f), z1), .012f, .012f, 4, 1f, 0, true);
                rails.Tube(0, new Vector3(i * (HX - .07f), Y(.88f), -.52f), new Vector3(i * (HX - .07f), Y(1.72f), -.52f), .022f, .022f, 5, 1f);
            }
            var run = RoundedRect(HX - .06f, HZ - .06f, R, 3, Y(.88f));        // the rail follows the rounded plan
            for (int i = 0; i < run.Length; i++)
                rails.Tube(0, run[i], run[(i + 1) % run.Length], .022f, .022f, 5, 1f);
            Part(t, "Rails", rails, Alu);

            // ── two benches of four, facing each other across a metre of legroom ──────────────────────────
            var seats = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int k = 0; k < 4; k++)
                    seats.Box(0, new Vector3(-.6f + k * .4f, Y(.38f), i * .7f), new Vector3(.37f, .08f, .4f), Quaternion.identity, 1f);
                seats.Box(0, new Vector3(0, Y(.6f), i * .87f), new Vector3(1.62f, .35f, .06f), Quaternion.identity, 1f);   // a low back, up to the glass
                seats.Box(0, new Vector3(0, Y(.2f), i * .84f), new Vector3(1.62f, .3f, .06f), Quaternion.identity, 1f);
            }
            Part(t, "Interior", seats, Materials.Get("ElbSeat", new Color(.2f, .23f, .28f), smoothness: .2f));

            // the floor: the rider's eyes then sit in the glazing, a hand under the top rail, as they do in a C8
            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, floor, 0);
            var box = new GameObject("Hull"); box.transform.SetParent(t, false);
            box.transform.localPosition = new Vector3(0, Y(.9f), 0);
            var bc = box.AddComponent<BoxCollider>(); bc.size = new Vector3(1.95f, 2.05f, 1.95f);
            return Save(root);
        }

        /// <summary>«Эльбрус-1» — the jig-back car of 1968–70 by Ceretti &amp; Tanfani, some twenty-five aboard, and for
        /// everyone who has ridden it simply «красный вагончик»: the red is the thing to get right. Rounded corners,
        /// sides that fall away inwards as they rise, a ribbon of big square windows with rounded corners nearly all the
        /// way round, one wide sliding door a side and the conductor's box in the end. Over the roof a trapezoid frame
        /// carries it to the carriage, which rides the track rope on two balanced bogies. Half a century of wind off the
        /// glacier has rubbed the paint dull. Pivot = the carriage on the track rope; the floor sits
        /// <see cref="Ropeway.Drop"/> below it, about five metres, as on the real line.</summary>
        static GameObject PendulumCar()
        {
            var root = new GameObject("Elb_Car_Pendulum");
            var t = root.transform;

            float rope = Ropeway.Hang(RopewayKind.Pendulum);
            float floor = rope - Ropeway.Drop(RopewayKind.Pendulum);           // −4.02 m: the floor of the car
            const float HX = 1.1f, HZ = 1.9f, R = .3f;                         // 2.2 × 3.8 m in plan
            const int Seg = 4;
            const float Sill = -.1f, Belt = .95f, Head = 1.95f, Cant = 2.12f;
            const float TopX = .99f, TopZ = 1.81f, CapX = .95f, CapZ = 1.78f;  // the sides lean in by 110 mm over the windows
            float Y(float h) => floor + h;

            var sill = RoundedRect(HX, HZ, R, Seg, Y(Sill));
            var belt = RoundedRect(HX, HZ, R, Seg, Y(Belt));
            var head = RoundedRect(TopX, TopZ, R, Seg, Y(Head));
            var cant = RoundedRect(CapX, CapZ, R, Seg, Y(Cant));

            var body = new MeshBuilder(1);
            Loft(body, 0, sill, belt, 1f, true);
            Loft(body, 0, head, cant, 1f, true);
            Deck(body, 0, RoundedRect(HX, HZ, R, Seg, floor), true);                              // the floor
            Deck(body, 0, Shrink(cant, 1f, Y(2.09f)), false);                                     // the ceiling
            Deck(body, 0, RoundedRect(HX, HZ, R, Seg, Y(Sill)), false);                           // and the underside
            var eaveLo = RoundedRect(CapX + .06f, CapZ + .06f, R + .06f, Seg, Y(2.06f));
            var eaveHi = RoundedRect(CapX + .06f, CapZ + .06f, R + .06f, Seg, Y(2.14f));
            Loft(body, 0, RoundedRect(CapX - .01f, CapZ - .01f, R, Seg, Y(2.06f)), eaveLo);
            Loft(body, 0, eaveLo, eaveHi);
            Dome(body, 0, eaveHi, .2f);
            Part(t, "Body", body, PaintWorn);

            var glassMb = new MeshBuilder(1);
            Loft(glassMb, 0, belt, head, 1f, true);
            var pane = Part(t, "Windows", glassMb, CabinGlass);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // the pillars that cut the ribbon into windows; they lean with the side, so the boxes lean too
            var posts = new MeshBuilder(1);
            float leanX = Mathf.Atan2(HX - TopX, Head - Belt) * Mathf.Rad2Deg;
            float leanZ = Mathf.Atan2(HZ - TopZ, Head - Belt) * Mathf.Rad2Deg;
            float midX = (HX + TopX) / 2f, midZ = (HZ + TopZ) / 2f, midY = Y((Belt + Head) / 2f);
            for (int i = -1; i <= 1; i += 2)
            {
                foreach (float z in new[] { .65f, 1.12f, 1.6f })                                   // door jamb, then two mullions
                    for (int k = -1; k <= 1; k += 2)
                        posts.Box(0, new Vector3(i * midX, midY, k * z), new Vector3(.06f, 1.06f, .1f), Quaternion.Euler(0, 0, i * leanX), 1f);
                posts.Box(0, new Vector3(.38f, midY, i * midZ), new Vector3(.1f, 1.06f, .06f), Quaternion.Euler(i * leanZ, 0, 0), 1f); // the conductor's corner
            }
            // the waist strake, and the wide sliding door on each side
            Loft(posts, 0, RoundedRect(HX, HZ, R, Seg, Y(.86f)), RoundedRect(HX + .025f, HZ + .025f, R, Seg, Y(.9f)));
            Loft(posts, 0, RoundedRect(HX + .025f, HZ + .025f, R, Seg, Y(.9f)), RoundedRect(HX, HZ, R, Seg, Y(Belt)));
            for (int i = -1; i <= 1; i += 2)
            {
                posts.Box(0, new Vector3(i * (HX + .03f), Y(.14f), 0), new Vector3(.07f, .08f, 1.36f), Quaternion.identity, 1f);   // door rail
                posts.Box(0, new Vector3(i * (HX + .07f), Y(.06f), 0), new Vector3(.16f, .05f, 1.2f), Quaternion.identity, 1f);    // the step
                posts.Box(0, new Vector3(i * (HX + .04f), Y(1.05f), .5f), new Vector3(.06f, .06f, .26f), Quaternion.identity, 1f); // handle
            }
            Part(t, "Trim", posts, Steel);

            // ── inside: standing room, poles, and the conductor's box in the uphill end ───────────────────
            var inside = new MeshBuilder(1);
            foreach (float x in new[] { -.62f, .62f })                                             // poles to hold on to, out of the gangway
                foreach (float z in new[] { -1.15f, -.4f, .4f, 1.15f })
                    inside.Tube(0, new Vector3(x, Y(.02f), z), new Vector3(x, Y(2.08f), z), .028f, .028f, 5, 1f);
            var hold = RoundedRect(HX - .08f, HZ - .08f, R, 3, Y(.95f));
            for (int i = 0; i < hold.Length; i++)
                inside.Tube(0, hold[i], hold[(i + 1) % hold.Length], .026f, .026f, 5, 1f);
            inside.Box(0, new Vector3(.52f, Y(.5f), 1.42f), new Vector3(.9f, 1f, .06f), Quaternion.identity, 1f);      // the conductor's counter
            inside.Box(0, new Vector3(.95f, Y(1.05f), 1.6f), new Vector3(.06f, 2.05f, .74f), Quaternion.identity, 1f); // and his partition
            Part(t, "Interior", inside, Alu);

            // ── the trapezoid frame and the carriage on the track rope ────────────────────────────────────
            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    frame.Tube(0, new Vector3(i * .78f, Y(2.02f), k * 1.3f), new Vector3(i * .17f, .04f, k * .44f), .062f, .05f, 6, .5f);
                    frame.Tube(0, new Vector3(i * .78f, Y(2.02f), k * 1.3f), new Vector3(i * .5f, Y(2.02f), 0), .04f, .04f, 5, .5f);
                }
            float tie = (Y(2.02f) + .04f) * .5f;                                // ties across the frame, half way up
            for (int k = -1; k <= 1; k += 2)
                frame.Tube(0, new Vector3(-.48f, tie, k * .87f), new Vector3(.48f, tie, k * .87f), .034f, .034f, 5, .5f);
            frame.Box(0, new Vector3(0, .1f, 0), new Vector3(.46f, .14f, 1.04f), Quaternion.identity, .5f);
            frame.Tube(0, new Vector3(0, .1f, 0), new Vector3(0, .99f, 0), .07f, .07f, 8, .5f);                        // the king pin
            frame.Box(0, new Vector3(0, .99f, 0), new Vector3(.2f, .12f, 1.85f), Quaternion.identity, .5f);            // the balance beam
            for (int k = -1; k <= 1; k += 2)
            {
                frame.Box(0, new Vector3(0, .85f, k * .8f), new Vector3(.26f, .1f, .9f), Quaternion.identity, .5f);    // a bogie either end
                foreach (float z in new[] { .35f, 1.05f })
                    frame.Tube(0, new Vector3(-.075f, rope + .2f, k * z), new Vector3(.075f, rope + .2f, k * z), .2f, .2f, 12, .5f, 0, true); // running on the rope
            }
            Part(t, "Carriage", frame, Steel);

            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, floor, 0);
            var box = new GameObject("Hull"); box.transform.SetParent(t, false);
            box.transform.localPosition = new Vector3(0, Y(1.05f), 0);
            var bc = box.AddComponent<BoxCollider>(); bc.size = new Vector3(2.2f, 2.3f, 3.8f);
            return Save(root);
        }

        /// <summary>The single fixed-grip chair of 1981 between Mir and Gara-Bashi, Transporta Chrudim: 1.5 m/s, sixteen
        /// minutes, and nothing between you and the glacier. One bent steel tube runs from the grip down and forward to a
        /// narrow painted seat with a low back; the bar folds down in front and the footrest hangs off it. Pivot = the
        /// grip; the footrest is <see cref="Ropeway.Drop"/> below the rope, which is what the line is built to clear.</summary>
        static GameObject ChairSeat()
        {
            var root = new GameObject("Elb_Chair");
            var t = root.transform;
            float rope = Ropeway.Hang(RopewayKind.Chair);
            float low = rope - Ropeway.Drop(RopewayKind.Chair);                 // −2.28 m: the footrest, the lowest thing on the line
            float pan = low + .48f;                                             // the seat, a knee above it

            var mb = new MeshBuilder(1);
            mb.Box(0, new Vector3(0, rope, 0), new Vector3(.17f, .13f, .34f), Quaternion.identity, 1f);      // the grip on the rope
            mb.Box(0, new Vector3(0, rope - .13f, 0), new Vector3(.12f, .14f, .2f), Quaternion.identity, 1f);
            // the hanger: one tube, bent, not a pair of straight sticks
            var path = new[]
            {
                new Vector3(0, rope - .18f, 0),
                new Vector3(0, Mathf.Lerp(rope - .18f, pan + .42f, .33f), -.03f),
                new Vector3(0, Mathf.Lerp(rope - .18f, pan + .42f, .7f), .02f),
                new Vector3(0, pan + .42f, .16f), new Vector3(0, pan + .2f, .3f),
            };
            Surf.Sweep(mb, 0, path, (a, b) => .042f, 6, 10, new Surf.Options { UvScale = new Vector2(.5f, 1f) }, capStart: false, capEnd: false);
            for (int i = -1; i <= 1; i += 2)                                     // the fork that carries the seat
            {
                mb.Tube(0, new Vector3(0, pan + .2f, .3f), new Vector3(i * .21f, pan - .05f, .26f), .03f, .026f, 5, 1f);
                mb.Tube(0, new Vector3(i * .21f, pan - .05f, .26f), new Vector3(i * .21f, pan - .06f, .62f), .026f, .026f, 5, 1f);
            }
            Part(t, "Hanger", mb, Steel);

            var seat = new MeshBuilder(1);
            seat.Box(0, new Vector3(0, pan - .025f, .44f), new Vector3(.48f, .05f, .42f), Quaternion.identity, 1f);            // 450 mm of seat
            seat.Box(0, new Vector3(0, pan + .19f, .21f), new Vector3(.48f, .44f, .05f), Quaternion.Euler(-9f, 0, 0), 1f);     // and a low back
            Part(t, "Seat", seat, Paint);

            var bar = new MeshBuilder(1);
            bar.Tube(0, new Vector3(-.28f, pan + .3f, .8f), new Vector3(.28f, pan + .3f, .8f), .022f, .022f, 6, 1f);           // the bar, down
            for (int i = -1; i <= 1; i += 2)
            {
                bar.Tube(0, new Vector3(i * .28f, pan + .3f, .8f), new Vector3(i * .28f, pan + .36f, .24f), .022f, .022f, 6, 1f);
                bar.Tube(0, new Vector3(i * .22f, pan + .26f, .78f), new Vector3(i * .22f, low + .04f, .7f), .022f, .022f, 6, 1f); // to the footrest
            }
            bar.Box(0, new Vector3(0, low + .02f, .7f), new Vector3(.54f, .05f, .16f), Quaternion.identity, 1f);
            Part(t, "Bar", bar, Steel);

            var ride = new GameObject("Seat"); ride.transform.SetParent(t, false);
            ride.transform.localPosition = new Vector3(0, pan, .4f);
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

        /// <summary>A café you can walk into. Plank walls with a real doorway (1.6 m wide, 2.2 m high, the floor only
        /// 0.15 m over the ground) and glazed windows that let the July sun in; inside, a counter with a coffee machine
        /// and a display case, tables with benches, a stove in the corner, shelves of crockery, the chalk board with the
        /// menu taken straight out of <see cref="Refreshments"/>, a coat rack by the door and lamps under the ceiling.
        /// The terrace with its parasols stays in front of the door as it always was.
        /// Pivot = the ground under the middle of the building, +Z = the front with the door and the terrace.
        /// The empty child <c>Counter</c> marks where the customer stands to order — <c>CafeService</c> finds it by that
        /// name, so it must not be renamed.</summary>
        static GameObject Cafe(string name, float W, float L, float H)
        {
            var root = new GameObject(name);
            var t = root.transform;
            float hw = W / 2f, hl = L / 2f;
            const float T = .22f;                       // wall thickness
            const float F = .15f;                       // floor over the ground: the step into the doorway
            const float DW = 1.6f, DH = 2.2f;           // doorway
            const float Terrace = 3f;                   // depth of the deck in front of the door
            float top = F + H;                          // ceiling
            float dx = -hw + T + 1f + DW / 2f;          // the door sits toward the left corner
            float dL = dx - DW / 2f, dR = dx + DW / 2f;
            float wy0 = F + 1f, wy1 = F + 2.1f;         // band of the side windows
            float fy0 = F + .95f, fy1 = F + 2.25f;      // the big front window
            float fx0 = dR + .55f, fx1 = hw - .55f;
            float wallY = F + H / 2f;                   // centre of a full-height wall

            // ── floor, terrace deck and the ramp up onto it ───────────────────────────────────────────────
            var floor = new MeshBuilder(1);
            floor.Box(0, new Vector3(0, F / 2f, 0), new Vector3(W, F, L), Quaternion.identity, .5f);
            floor.Box(0, new Vector3(0, F / 2f, hl + Terrace / 2f), new Vector3(W + 2f, F, Terrace), Quaternion.identity, .5f);
            Part(t, "Floor", floor, Plank);

            // Steps down from the terrace: five of 0.15 m, the height a walking capsule takes without noticing. They
            // reach 0.6 m below the pivot, so the door is still walkable when the slope has put the building up on a
            // plinth; on flat ground all but the first are buried and never show.
            var steps = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
            {
                var block = new Vector3(0, -k * .15f - .7f, hl + Terrace + .2f + k * .4f);
                var size = new Vector3(W, 1.4f, .4f);
                steps.Box(0, block, size, Quaternion.identity, .6f);
                Solid(t, "StepSolid" + k, block, size);
            }
            Part(t, "Steps", steps, Stone);

            // ── walls: the front one is built around the door and the window, the sides around their openings ──
            var walls = new MeshBuilder(1);
            float zf = hl - T / 2f, zb = -hl + T / 2f;
            // front: pier beside the door, lintel over it, then sill / header / jambs around the window
            walls.Box(0, new Vector3((-hw + dL) / 2f, wallY, zf), new Vector3(dL + hw, H, T), Quaternion.identity, .5f);
            walls.Box(0, new Vector3(dx, F + (DH + H) / 2f, zf), new Vector3(DW, H - DH, T), Quaternion.identity, .5f);
            walls.Box(0, new Vector3((dR + hw) / 2f, (F + fy0) / 2f, zf), new Vector3(hw - dR, fy0 - F, T), Quaternion.identity, .5f);
            walls.Box(0, new Vector3((dR + hw) / 2f, (fy1 + top) / 2f, zf), new Vector3(hw - dR, top - fy1, T), Quaternion.identity, .5f);
            walls.Box(0, new Vector3((dR + fx0) / 2f, (fy0 + fy1) / 2f, zf), new Vector3(fx0 - dR, fy1 - fy0, T), Quaternion.identity, .5f);
            walls.Box(0, new Vector3((fx1 + hw) / 2f, (fy0 + fy1) / 2f, zf), new Vector3(hw - fx1, fy1 - fy0, T), Quaternion.identity, .5f);
            // back: solid
            walls.Box(0, new Vector3(0, wallY, zb), new Vector3(W, H, T), Quaternion.identity, .5f);
            // sides: a band of windows down both of them
            int n = Mathf.Max(2, Mathf.RoundToInt(L / 3.4f));
            float pitch = L / n, ow = pitch * .55f;
            for (int i = -1; i <= 1; i += 2)
            {
                float xs = i * (hw - T / 2f);
                walls.Box(0, new Vector3(xs, (F + wy0) / 2f, 0), new Vector3(T, wy0 - F, L), Quaternion.identity, .5f);
                walls.Box(0, new Vector3(xs, (wy1 + top) / 2f, 0), new Vector3(T, top - wy1, L), Quaternion.identity, .5f);
                for (int k = 0; k <= n; k++)
                {
                    float z0 = k == 0 ? -hl : -hl + pitch * (k - .5f) + ow / 2f;
                    float z1 = k == n ? hl : -hl + pitch * (k + .5f) - ow / 2f;
                    if (z1 - z0 < .05f) continue;
                    walls.Box(0, new Vector3(xs, (wy0 + wy1) / 2f, (z0 + z1) / 2f), new Vector3(T, wy1 - wy0, z1 - z0), Quaternion.identity, .5f);
                }
            }
            Part(t, "Walls", walls, Plank);

            // ── glazing: clear panes that do not cast shadows, so the sun reaches the tables ───────────────
            var glass = new MeshBuilder(1);
            glass.Quad(0, new Vector3(fx0, fy0, zf), new Vector3(fx1, fy0, zf), new Vector3(fx1, fy1, zf), new Vector3(fx0, fy1, zf),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            for (int i = -1; i <= 1; i += 2)
            {
                float xs = i * (hw - T / 2f);
                for (int k = 0; k < n; k++)
                {
                    float c = -hl + pitch * (k + .5f);
                    glass.Quad(0, new Vector3(xs, wy0, c - ow / 2f), new Vector3(xs, wy0, c + ow / 2f),
                        new Vector3(xs, wy1, c + ow / 2f), new Vector3(xs, wy1, c - ow / 2f),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
                }
            }
            var pane = Part(t, "Glazing", glass, GlassClear);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ── frames, mullions, the door lining ─────────────────────────────────────────────────────────
            var trim = new MeshBuilder(1);
            for (int k = 1; k * 1.9f < fx1 - fx0; k++)
                trim.Box(0, new Vector3(fx0 + k * 1.9f, (fy0 + fy1) / 2f, zf), new Vector3(.08f, fy1 - fy0, T + .02f), Quaternion.identity, 1f);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < n; k++)
                    trim.Box(0, new Vector3(i * (hw - T / 2f), (wy0 + wy1) / 2f, -hl + pitch * (k + .5f)), new Vector3(T + .02f, wy1 - wy0, .08f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(dL - .06f, F + DH / 2f, zf), new Vector3(.12f, DH, T + .04f), Quaternion.identity, 1f);   // door jambs
            trim.Box(0, new Vector3(dR + .06f, F + DH / 2f, zf), new Vector3(.12f, DH, T + .04f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(dx, F + DH + .06f, zf), new Vector3(DW + .24f, .12f, T + .04f), Quaternion.identity, 1f); // lintel
            Part(t, "Trim", trim, PlankDark);

            // ── roof, gable ends and the ceiling seen from inside ─────────────────────────────────────────
            var roof = new MeshBuilder(1);
            Gable(roof, 0, new Vector3(0, top, 0), W, L, 1.5f, .75f);
            GableEnd(roof, 0, hl, 1f, hw, top, 1.5f);
            GableEnd(roof, 0, -hl, -1f, hw, top, 1.5f);
            Part(t, "Roof", roof, RoofRust);

            var ceiling = new MeshBuilder(1);
            ceiling.Box(0, new Vector3(0, top - .08f, 0), new Vector3(W - 2 * T, .16f, L - 2 * T), Quaternion.identity, .6f);
            Part(t, "Ceiling", ceiling, PlankDark);

            // ── the counter: bar, steel top, display case, coffee machine, shelves of crockery ─────────────
            float barZ = -hl + T + .5f;
            float barX0 = -hw + T, barX1 = hw - T - 2f;
            float barW = barX1 - barX0, barX = (barX0 + barX1) / 2f;

            var bar = new MeshBuilder(1);
            bar.Box(0, new Vector3(barX, F + .5f, barZ), new Vector3(barW, 1f, .9f), Quaternion.identity, .8f);
            bar.Box(0, new Vector3(barX, F + 1.04f, barZ), new Vector3(barW + .14f, .08f, 1.02f), Quaternion.identity, .8f);
            for (int k = 0; k < 3; k++)                                    // shelves on the back wall, clear of the display case
                bar.Box(0, new Vector3((barX0 + barX) / 2f, F + 1.72f + k * .4f, -hl + T + .16f), new Vector3(barW / 2f, .05f, .3f), Quaternion.identity, .8f);
            Part(t, "Bar", bar, PlankDark);

            var steel = new MeshBuilder(1);
            float machineX = barX1 - .75f;
            steel.Box(0, new Vector3(machineX, F + 1.32f, barZ - .1f), new Vector3(.62f, .48f, .5f), Quaternion.identity, 1f);       // coffee machine
            steel.Box(0, new Vector3(machineX, F + 1.6f, barZ - .1f), new Vector3(.5f, .08f, .42f), Quaternion.identity, 1f);
            for (int k = -1; k <= 1; k += 2)
                steel.Tube(0, new Vector3(machineX + k * .16f, F + 1.12f, barZ + .16f), new Vector3(machineX + k * .16f, F + 1.26f, barZ + .16f), .035f, .045f, 6, 1f, 0, true);
            steel.Box(0, new Vector3(machineX - .62f, F + 1.2f, barZ - .12f), new Vector3(.3f, .24f, .3f), Quaternion.identity, 1f); // grinder
            for (int k = 0; k < 8; k++)                                    // cups on the shelves
            {
                float cx = barX0 + .35f + k * (barW / 2f - .5f) / 7f;
                steel.Tube(0, new Vector3(cx, F + 1.75f, -hl + T + .16f), new Vector3(cx, F + 1.84f, -hl + T + .16f), .04f, .042f, 8, 1f, 0, true);
                steel.Tube(0, new Vector3(cx, F + 2.15f, -hl + T + .16f), new Vector3(cx, F + 2.26f, -hl + T + .16f), .045f, .047f, 8, 1f, 0, true);
            }
            Part(t, "Counterware", steel, Alu);

            var showcase = new MeshBuilder(1);                              // the display case with the khychiny
            float caseX = barX0 + barW * .28f;
            showcase.Box(0, new Vector3(caseX, F + 1.34f, barZ), new Vector3(barW * .42f, .5f, .78f), Quaternion.identity, 1f);
            var glassCase = Part(t, "Showcase", showcase, GlassClear);
            glassCase.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var food = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
            {
                float fx = caseX - barW * .16f + k * barW * .08f;
                food.Tube(0, new Vector3(fx, F + 1.14f, barZ - .1f), new Vector3(fx, F + 1.18f, barZ - .1f), .12f, .115f, 10, 1f, 0, true);
                food.Tube(0, new Vector3(fx, F + 1.4f, barZ + .12f), new Vector3(fx, F + 1.44f, barZ + .12f), .1f, .095f, 10, 1f, 0, true);
            }
            Part(t, "Pastry", food, Bamboo);

            // ── the chalk board over the counter ──────────────────────────────────────────────────────────
            var board = new MeshBuilder(1);
            float boardX = barX + barW * .27f, boardZ = -hl + T + .05f;
            board.Box(0, new Vector3(boardX, F + 1.98f, boardZ), new Vector3(1.9f, 1.15f, .07f), Quaternion.identity, 1f);
            Part(t, "Board", board, Chalkboard);
            var menu = new GameObject("Menu"); menu.transform.SetParent(t, false);
            menu.transform.localPosition = new Vector3(boardX, F + 1.98f, boardZ + .06f);
            var menuText = menu.AddComponent<TextMesh>();
            menu.AddComponent<Height1079.Runtime.SignText>();   // paint on a board, not text through walls
            menuText.text = Refreshments.Board(9);
            menuText.characterSize = .035f; menuText.fontSize = 60;
            menuText.anchor = TextAnchor.MiddleCenter; menuText.alignment = TextAlignment.Left;
            menuText.color = new Color(.93f, .93f, .88f);

            // ── tables, benches, the stove and the coat rack ──────────────────────────────────────────────
            float tz0 = barZ + 1.9f, tz1 = tz0 + 2.3f, tx = hw - T - 1.15f;
            var furniture = new MeshBuilder(1);
            var legs = new MeshBuilder(1);
            for (int r = 0; r < 2; r++)
                for (int i = -1; i <= 1; i += 2)
                {
                    float cx = i * tx, cz = r == 0 ? tz0 : tz1;
                    furniture.Box(0, new Vector3(cx, F + .76f, cz), new Vector3(.95f, .07f, .95f), Quaternion.identity, 1f);
                    legs.Tube(0, new Vector3(cx, F, cz), new Vector3(cx, F + .74f, cz), .045f, .04f, 8, 1f, 0, true);
                    legs.Tube(0, new Vector3(cx, F + .01f, cz), new Vector3(cx, F + .05f, cz), .28f, .26f, 10, 1f, 0, true);
                    for (int b = -1; b <= 1; b += 2)                       // a bench on each side
                    {
                        furniture.Box(0, new Vector3(cx + b * .78f, F + .44f, cz), new Vector3(.32f, .06f, 1f), Quaternion.identity, 1f);
                        furniture.Box(0, new Vector3(cx + b * .78f, F + .22f, cz - .38f), new Vector3(.28f, .44f, .07f), Quaternion.identity, 1f);
                        furniture.Box(0, new Vector3(cx + b * .78f, F + .22f, cz + .38f), new Vector3(.28f, .44f, .07f), Quaternion.identity, 1f);
                    }
                }
            // the coat rack by the door
            float rackX = dR + .18f, rackZ = hl - T - .35f;
            legs.Tube(0, new Vector3(rackX, F, rackZ), new Vector3(rackX, F + 1.85f, rackZ), .05f, .04f, 8, 1f, 0, true);
            legs.Tube(0, new Vector3(rackX - .45f, F + 1.78f, rackZ), new Vector3(rackX + .45f, F + 1.78f, rackZ), .028f, .028f, 6, 1f);
            for (int k = -2; k <= 2; k++)
                legs.Tube(0, new Vector3(rackX + k * .22f, F + 1.78f, rackZ), new Vector3(rackX + k * .22f, F + 1.68f, rackZ + .06f), .018f, .016f, 5, 1f, 0, true);
            Part(t, "Furniture", furniture, Plank);

            // the stove in the corner beside the counter, its pipe out through the roof
            float sx = hw - T - .85f, sz = -hl + T + .8f;
            legs.Tube(0, new Vector3(sx, F + .16f, sz), new Vector3(sx, F + .92f, sz), .31f, .3f, 12, 1f, 0, true);
            legs.Box(0, new Vector3(sx, F + .96f, sz), new Vector3(.74f, .06f, .74f), Quaternion.identity, 1f);
            for (int k = 0; k < 4; k++)
                legs.Tube(0, new Vector3(sx + ((k & 1) == 0 ? -.2f : .2f), F, sz + (k < 2 ? -.2f : .2f)),
                    new Vector3(sx + ((k & 1) == 0 ? -.22f : .22f), F + .17f, sz + (k < 2 ? -.22f : .22f)), .03f, .03f, 5, 1f, 0, true);
            legs.Tube(0, new Vector3(sx, F + .96f, sz), new Vector3(sx, top + 1.6f, sz), .1f, .09f, 8, 1f, 0, true);
            Part(t, "Steelwork", legs, Steel);

            var fire = new MeshBuilder(1);                                   // the open firebox door
            fire.Box(0, new Vector3(sx, F + .5f, sz + .3f), new Vector3(.34f, .3f, .04f), Quaternion.identity, 1f);
            Part(t, "Firebox", fire, StoveGlow);

            // ── lamps under the ceiling, and the one real light ───────────────────────────────────────────
            var shades = new MeshBuilder(1);
            var bulbs = new MeshBuilder(1);
            for (int k = 0; k < 3; k++)
            {
                float lz = -hl + L * (k + 1) / 4f;
                shades.Tube(0, new Vector3(0, top - .16f, lz), new Vector3(0, top - .44f, lz), .015f, .015f, 5, 1f);
                shades.Cone(0, new Vector3(0, top - .62f, lz), .21f, .19f, 10, 1f);
                bulbs.Tube(0, new Vector3(0, top - .66f, lz), new Vector3(0, top - .63f, lz), .15f, .15f, 10, 1f, 0, true);
            }
            Part(t, "Shades", shades, Steel);
            Part(t, "Bulbs", bulbs, LampGlow);

            var lampGo = new GameObject("CafeLight", typeof(Light));
            lampGo.transform.SetParent(t, false);
            lampGo.transform.localPosition = new Vector3(0, top - .75f, barZ + L * .3f);
            var lamp = lampGo.GetComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, .86f, .64f);
            lamp.range = Mathf.Max(W, L) * .8f;    // kept short: without shadows a longer range would spill onto the terrace
            lamp.intensity = 2.4f;                 // the day outside is bright: a dim lamp would not read through the windows
            lamp.shadows = LightShadows.None;      // eight cafés on the slope, one shadowed point light each would cost too much
            lamp.renderMode = LightRenderMode.ForcePixel;

            // ── terrace: tables under parasols and the low rail, as before ────────────────────────────────
            var deck = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
            {
                var c = new Vector3(k * (hw * .55f), F, hl + 1.1f);
                deck.Tube(0, c, c + new Vector3(0, .74f, 0), .05f, .05f, 6, 1f, 0, true);
                deck.Box(0, c + new Vector3(0, .76f, 0), new Vector3(1.1f, .07f, 1.1f), Quaternion.identity, 1f);
                deck.Tube(0, c, c + new Vector3(0, 2.3f, 0), .035f, .03f, 6, 1f);
            }
            for (int k = 0; k < 9; k++)
                deck.Box(0, new Vector3(-hw - 1f + k * (W + 2f) / 8f, F + .45f, hl + Terrace - .12f), new Vector3(.07f, .9f, .07f), Quaternion.identity, 1f);
            deck.Box(0, new Vector3(0, F + .92f, hl + Terrace - .12f), new Vector3(W + 2f, .08f, .1f), Quaternion.identity, 1f);
            Part(t, "Terrace", deck, Plank);

            var shade2 = new MeshBuilder(1);
            for (int k = -1; k <= 1; k += 2)
                shade2.Cone(0, new Vector3(k * (hw * .55f), F + 2.32f, hl + 1.1f), 1.5f, -.45f, 8, 1f);
            Part(t, "Parasols", shade2, TarpRed);

            // ── where the customer stands to order ────────────────────────────────────────────────────────
            var counter = new GameObject("Counter");
            counter.transform.SetParent(t, false);
            counter.transform.localPosition = new Vector3(barX, F, barZ + 1.05f);
            counter.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // facing the bar

            // ── colliders: the walls in pieces so the doorway stays open ──────────────────────────────────
            Solid(t, "FloorSolid", new Vector3(0, F / 2f, 0), new Vector3(W, F, L));
            Solid(t, "DeckSolid", new Vector3(0, F / 2f, hl + Terrace / 2f), new Vector3(W + 2f, F, Terrace));
            Solid(t, "WallFrontLeft", new Vector3((-hw + dL) / 2f, top / 2f, zf), new Vector3(dL + hw, top, T));
            Solid(t, "WallFrontRight", new Vector3((dR + hw) / 2f, top / 2f, zf), new Vector3(hw - dR, top, T));
            Solid(t, "WallFrontLintel", new Vector3(dx, F + (DH + H) / 2f, zf), new Vector3(DW, H - DH, T));
            Solid(t, "WallBack", new Vector3(0, top / 2f, zb), new Vector3(W, top, T));
            Solid(t, "WallWest", new Vector3(-(hw - T / 2f), top / 2f, 0), new Vector3(T, top, L));
            Solid(t, "WallEast", new Vector3(hw - T / 2f, top / 2f, 0), new Vector3(T, top, L));
            Solid(t, "CeilingSolid", new Vector3(0, top - .08f, 0), new Vector3(W, .16f, L));
            Solid(t, "BarSolid", new Vector3(barX, F + .55f, barZ), new Vector3(barW, 1.1f, .95f));
            Solid(t, "StoveSolid", new Vector3(sx, F + .5f, sz), new Vector3(.7f, 1f, .7f));
            for (int r = 0; r < 2; r++)
                for (int i = -1; i <= 1; i += 2)
                    Solid(t, $"TableSolid{r}{(i < 0 ? "W" : "E")}", new Vector3(i * tx, F + .4f, r == 0 ? tz0 : tz1), new Vector3(1f, .8f, 1f));
            return Save(root);
        }

        /// <summary>The triangle that closes a gable end. <paramref name="nz"/> is +1 for the wall that faces +Z.</summary>
        static void GableEnd(MeshBuilder mb, int sub, float z, float nz, float halfWidth, float y0, float rise)
        {
            var n = new Vector3(0, 0, nz);
            int a = mb.Vert(new Vector3(-halfWidth, y0, z), n, Vector2.zero);
            int b = mb.Vert(new Vector3(halfWidth, y0, z), n, new Vector2(1, 0));
            int c = mb.Vert(new Vector3(0, y0 + rise, z), n, new Vector2(.5f, 1));
            if (nz > 0) mb.Tri(sub, a, c, b); else mb.Tri(sub, a, b, c);
        }

        /// <summary>What the counter hands over the top (Core.ItemId): the foil parcel, the roll in lavash, the pizza box
        /// and the paper cup. They are rucksack items, so their models go next to the rest of the cargo — built here
        /// because the café that sells them is built here, and because this runs after CargoFactory.</summary>
        static void CafeFood()
        {
            var foil = Materials.Get("ElbFoil", new Color(.82f, .84f, .86f), smoothness: .68f);
            var dough = Materials.Get("ElbDough", new Color(.85f, .72f, .48f), smoothness: .12f);
            var card = Materials.Get("ElbCarton", new Color(.72f, .6f, .43f), smoothness: .05f);
            var cup = Materials.Get("ElbPaperCup", new Color(.92f, .9f, .86f), smoothness: .1f);

            var a = new MeshBuilder(1);
            a.Tube(0, Vector3.zero, new Vector3(0, .04f, 0), .115f, .105f, 14, 1f, 0, true);
            a.Tube(0, new Vector3(0, .04f, 0), new Vector3(.02f, .06f, .01f), .04f, .015f, 6, 1f, 0, true);
            SaveCargo(ItemId.Khychin.ToString(), a, foil);

            var b = new MeshBuilder(1);
            b.Tube(0, new Vector3(0, .05f, -.13f), new Vector3(0, .05f, .13f), .05f, .045f, 10, 1f, 0, true);
            b.Tube(0, new Vector3(0, .05f, -.13f), new Vector3(0, .05f, -.135f), .05f, .05f, 10, 1f, 0, true);
            SaveCargo(ItemId.Shashlyk.ToString(), b, dough);

            var c = new MeshBuilder(1);
            c.Box(0, new Vector3(0, .025f, 0), new Vector3(.34f, .05f, .34f), Quaternion.identity, 1f);
            c.Box(0, new Vector3(0, .055f, 0), new Vector3(.345f, .012f, .345f), Quaternion.identity, 1f);
            SaveCargo(ItemId.PizzaBox.ToString(), c, card);

            var d = new MeshBuilder(1);
            d.Tube(0, Vector3.zero, new Vector3(0, .12f, 0), .032f, .042f, 12, 1f, 0, true);
            d.Tube(0, new Vector3(0, .12f, 0), new Vector3(0, .135f, 0), .044f, .041f, 12, 1f, 0, true);
            SaveCargo(ItemId.HotCup.ToString(), d, cup);
        }

        static void SaveCargo(string name, MeshBuilder mb, Material mat)
        {
            string dir = WorldPaths.Generated + "/Prefabs/Cargo";
            Directory.CreateDirectory(dir);
            var root = new GameObject(name);
            Part(root.transform, "Body", mb, mat);
            PrefabUtility.SaveAsPrefabAsset(root, $"{dir}/{name}.prefab");
            Object.DestroyImmediate(root);
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

            // named "Stall", not "Counter": CafeService looks up café counters by that name
            Solid(t, "Stall", new Vector3(0, .5f, -D / 2 + .35f), new Vector3(W, 1f, .7f));
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
            label.AddComponent<Height1079.Runtime.SignText>();   // paint on a board, not text through walls
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
            // one face only: the text material draws over everything, so a second copy on the back shows through the board
            var label = new GameObject("Label"); label.transform.SetParent(t, false);
            label.transform.localPosition = new Vector3(0, 2.15f, .07f);
            var tm = label.AddComponent<TextMesh>();
            label.AddComponent<Height1079.Runtime.SignText>();   // paint on a board, not text through walls
            tm.text = "Эльбрус"; tm.characterSize = .06f; tm.fontSize = 90; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center; tm.color = new Color(.06f, .08f, .1f);
            return Save(root);
        }
    }
}
