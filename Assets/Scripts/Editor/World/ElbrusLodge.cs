using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>The rooms of the southern slope: the two kinds of building a climber walks <b>into</b> rather than past.
    ///
    /// <list type="number">
    /// <item><b>The base on the Azau meadow (2 350 m)</b>, <c>Elb_Base_Azau</c> — a low timber-and-profiled-sheet shed
    /// with a porch, and the one room the whole week starts in. Three corners: the hire counter with its racks of
    /// crampons, axes, helmets, harnesses, down jackets and boots and the heap of gear handed back; the desk of the
    /// Эльбрусский высокогорный поисково-спасательный отряд МЧС with the register of parties, the rules board, the map
    /// of the district with the route and the control time on it, the radio set and the emblem; and the weather board
    /// with the forecast chalked up for 3 800 / 4 800 / 5 400 / 5 642 m, a thermometer and a barometer beside it. Round
    /// them: benches where rucksacks are unpacked, a drying frame, a kettle in the corner, the guides' notices and a
    /// board of summit photographs.</item>
    /// <item><b>The shelters above</b> — the same four prefabs the mountain already carries, opened up: the barrels of
    /// Гара-Баши (<c>Elb_Barrel</c>, six places on two tiers), the small приют (<c>Elb_Hut_Small</c>, a room of four),
    /// the LeapRus capsule (<c>Elb_Hut_Capsule</c>), the hostel on the foundation of Приют 11
    /// (<c>Elb_Hut_Diesel</c>) — plus <c>Elb_Hut_Natspark</c>, the modern приют of the national park, and
    /// <c>Elb_Genset</c>, the diesel that keeps the camp alive. A climber lives here for half a week between
    /// acclimatisation walks, so every one of them has what he comes for: bunks he lies down on, warmth, a rack for
    /// wet boots and somewhere to melt snow (<see cref="Lodging"/>).</item>
    /// </list>
    ///
    /// <b>Four prefabs are re-issued under the names ElbrusFactory already uses</b> — <c>Elb_Barrel</c>,
    /// <c>Elb_Hut_Small</c>, <c>Elb_Hut_Capsule</c>, <c>Elb_Hut_Diesel</c>. The name is the contract with
    /// <see cref="Height1079.Runtime.ElbrusWorld.Footprint"/>, with the barrel rows of the camp and with the moraine of
    /// <see cref="ElbrusAscent"/>, so the outside dimensions of all four are kept to the millimetre and only the inside
    /// is new: nothing that places them has to change. <b>This file must therefore run after
    /// <c>ElbrusFactory.Build()</c></b> and before anything drops those prefabs on the mountain.
    ///
    /// Going in is the mechanism the station cafés already use and not a second one: a real hole in the wall (the wall
    /// is built as the pieces round the opening, never as one box), a floor lifted over the pivot so the hillside
    /// cannot poke up through it, colliders in pieces so the doorway stays open, and a step or two outside up to the
    /// sill. The empty child <c>Elb_RentStand_Desk</c> is what <see cref="Height1079.Runtime.RentalService"/> finds by
    /// name, so the hire window works inside the base with no runtime change at all; <c>Elb_RescueDesk</c>,
    /// <c>Elb_WeatherBoard</c> and <c>Elb_Bunk</c> are the same kind of marker for the three rules that are written in
    /// Core and not yet called from the runtime (<see cref="Rescue"/>, <see cref="Forecast"/>,
    /// <see cref="Lodging"/>).
    ///
    /// Nothing is unlit: at four in the morning the only light in a barrel is a head torch, and an unlit material would
    /// burn white (CLAUDE.md, «Ночь»). Bulbs are emissive Standard, which is dark until it is lit and bright when it
    /// is.</summary>
    public static class ElbrusLodge
    {
        const string PrefabDir = WorldPaths.Kit + "/Prefabs/Elbrus";
        const string MeshDir = WorldPaths.Kit + "/Meshes/Lodge";
        static int meshCounter;
        static int buildings, triangles;

        /// <summary>The seed and the day the forecast board is chalked with when it is baked. The runtime replaces the
        /// text of the <c>Forecast</c> child from the session's own seed; this is what stands on it until it does, and
        /// it is a real board and not lorem ipsum.</summary>
        const int BoardSeed = 1079;
        static readonly DateTime BoardDay = new DateTime(2026, 7, 18);

        public static void Build()
        {
            buildings = 0; triangles = 0;
            BaseAzau();
            Barrel();
            SmallHut();
            Natspark();
            Capsule();
            Hostel();
            Genset();
            Debug.Log($"1079 Эльбрус: {buildings} зданий с интерьером, {triangles} треугольников (ElbrusLodge)");
        }

        // ── little helpers, the same ones every factory here has ──────────────────────────────────────────
        static GameObject Part(Transform parent, string name, MeshBuilder mb, params Material[] mats)
        {
            Directory.CreateDirectory(MeshDir);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string meshName = $"Lodge_{parent.root.name}_{name}_{meshCounter++}";
            var mesh = mb.ToMesh(meshName);
            string path = $"{MeshDir}/{meshName}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            triangles += mb.TriangleCount;
            return go;
        }

        static GameObject Save(GameObject root)
        {
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            buildings++;
            return prefab;
        }

        static void Solid(Transform t, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>An empty child the runtime looks for by name: where the player stands to be served, or the bunk he
        /// lies down on. +Z of the marker faces the thing it belongs to, the way <c>Counter</c> does in a café.</summary>
        static GameObject Marker(Transform t, string name, Vector3 at, float yaw)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        /// <summary>Text painted on a board. A TextMesh faces its own +Z, so <paramref name="yaw"/> is the direction the
        /// reader looks <em>from</em>; <see cref="Height1079.Runtime.SignText"/> keeps it behind whatever stands in
        /// front of it instead of shining through the wall.</summary>
        static GameObject Sign(Transform t, string name, string text, Vector3 at, float yaw, float size, Color color,
            TextAlignment align = TextAlignment.Left, int font = 70)
        {
            var go = new GameObject(name); go.transform.SetParent(t, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var tm = go.AddComponent<TextMesh>();
            go.AddComponent<Height1079.Runtime.SignText>();
            tm.text = text; tm.characterSize = size; tm.fontSize = font;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = align; tm.color = color;
            return go;
        }

        /// <summary>A surface that shows its own light — a bulb, the open door of a stove. No GI: what it shows is all
        /// a lit map needs, and it stays dark under a torch beam the way an unlit material never would.</summary>
        static Material Emissive(string name, Color baseColor, Color emission, float smoothness = .3f)
        {
            var m = Materials.Get(name, baseColor, smoothness: smoothness);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material clearGlass;
        /// <summary>Window glass you can see through and that casts no shadow, so a low sun still reaches the benches.</summary>
        static Material GlassClear
        {
            get
            {
                if (clearGlass != null) return clearGlass;
                var m = Materials.Get("ElbLGlass", new Color(.78f, .85f, .88f, .24f), smoothness: .95f);
                m.SetFloat("_Mode", 3);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                EditorUtility.SetDirty(m);
                return clearGlass = m;
            }
        }

        // ── materials (own ElbL* names, so this file never fights the other factories over an asset) ───────
        static Material Plank => Materials.PH("ElbLPlank", "raw_plank_wall", new Color(.72f, .58f, .42f), 1.6f);
        static Material PlankDark => Materials.PH("ElbLPlankDark", "raw_plank_wall", new Color(.4f, .31f, .23f), 1.6f);
        /// <summary>The inside lining: planed board, lighter than the outside and a great deal cleaner.</summary>
        static Material Ply => Materials.PH("ElbLPly", "raw_plank_wall", new Color(.82f, .71f, .55f), 1.9f);
        /// <summary>Профлист — the corrugated sheet half of Prielbrusye is clad in.</summary>
        static Material Sheet => Materials.Get("ElbLSheet", new Color(.62f, .65f, .68f), smoothness: .48f);
        static Material SheetGreen => Materials.Get("ElbLSheetGreen", new Color(.22f, .36f, .3f), smoothness: .44f);
        static Material RoofRust => Materials.Get("ElbLRoofRust", new Color(.45f, .25f, .17f), smoothness: .35f);
        static Material Steel => Materials.Get("ElbLSteel", new Color(.45f, .47f, .5f), smoothness: .55f);
        static Material Alu => Materials.Get("ElbLAlu", new Color(.73f, .75f, .78f), smoothness: .64f);
        static Material Rubber => Materials.Get("ElbLRubber", new Color(.1f, .1f, .11f), smoothness: .14f);
        static Material Concrete => Materials.Get("ElbLConcrete", new Color(.62f, .61f, .58f), smoothness: .08f);
        static Material Chalkboard => Materials.Get("ElbLChalkboard", new Color(.08f, .12f, .1f), smoothness: .06f);
        static Material Paper => Materials.Get("ElbLPaper", new Color(.9f, .88f, .82f), smoothness: .05f);
        static Material Orange => Materials.Get("ElbLOrange", new Color(.86f, .45f, .09f), smoothness: .4f);
        static Material Blue => Materials.Get("ElbLBlue", new Color(.13f, .3f, .55f), smoothness: .42f);
        static Material Red => Materials.Get("ElbLRed", new Color(.74f, .18f, .14f), smoothness: .4f);
        static Material White => Materials.Get("ElbLWhite", new Color(.9f, .91f, .9f), smoothness: .45f);
        static Material Snow => Materials.PH("ElbLSnow", "snow_02", new Color(.96f, .97f, 1f), 2f, .3f, .7f);
        static Material Mattress => Materials.Cloth("elbl_mattress", new Color(.36f, .4f, .48f));
        static Material Blanket => Materials.Cloth("elbl_blanket", new Color(.5f, .22f, .18f));
        static Material Jacket => Materials.Cloth("elbl_jacket", new Color(.2f, .34f, .46f));
        static Material Foam => Materials.Get("ElbLFoam", new Color(.84f, .83f, .78f), smoothness: .2f);
        static Material LampGlow => Emissive("ElbLLampGlow", new Color(.98f, .93f, .82f), new Color(1f, .82f, .55f) * 2.4f, .4f);
        static Material StoveGlow => Emissive("ElbLStoveGlow", new Color(.35f, .16f, .09f), new Color(1f, .38f, .1f) * 1.8f, .15f);

        // ── walls with holes in them ──────────────────────────────────────────────────────────────────────
        /// <summary>One flat wall panel with at most one rectangular hole in it, built as the four pieces round the
        /// opening — the piece under it, the piece over it and the two piers beside it — which is the only way a wall
        /// made of boxes can have a door in it. <paramref name="alongX"/> puts the panel in the plane z =
        /// <paramref name="plane"/> and measures <c>a</c> along x; otherwise it is the plane x = plane and <c>a</c>
        /// runs along z. Pass a hole with hy1 &lt;= hy0 for a solid wall.</summary>
        static void Panel(MeshBuilder mb, int s, bool alongX, float plane, float T,
            float a0, float a1, float y0, float y1, float ha0, float ha1, float hy0, float hy1, float uv = .5f)
        {
            void B(float ca0, float ca1, float cy0, float cy1)
            {
                float sa = ca1 - ca0, sy = cy1 - cy0;
                if (sa < 1e-3f || sy < 1e-3f) return;
                var c = alongX ? new Vector3((ca0 + ca1) * .5f, (cy0 + cy1) * .5f, plane)
                               : new Vector3(plane, (cy0 + cy1) * .5f, (ca0 + ca1) * .5f);
                var sz = alongX ? new Vector3(sa, sy, T) : new Vector3(T, sy, sa);
                mb.Box(s, c, sz, Quaternion.identity, uv);
            }
            if (hy1 <= hy0 || ha1 <= ha0) { B(a0, a1, y0, y1); return; }
            B(a0, a1, y0, Mathf.Clamp(hy0, y0, y1));
            B(a0, a1, Mathf.Clamp(hy1, y0, y1), y1);
            B(a0, Mathf.Clamp(ha0, a0, a1), Mathf.Max(y0, hy0), Mathf.Min(y1, hy1));
            B(Mathf.Clamp(ha1, a0, a1), a1, Mathf.Max(y0, hy0), Mathf.Min(y1, hy1));
        }

        /// <summary>A pane filling a hole in a z-plane wall, seen from both sides.</summary>
        static void PaneZ(MeshBuilder mb, int s, float z, float x0, float x1, float y0, float y1)
            => mb.Quad(s, new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);

        /// <summary>A pane filling a hole in an x-plane wall, seen from both sides.</summary>
        static void PaneX(MeshBuilder mb, int s, float x, float z0, float z1, float y0, float y1)
            => mb.Quad(s, new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);

        /// <summary>Gable roof over a rectangular block: ridge along z, two slopes falling to ±x, eaves all round.</summary>
        static void Gable(MeshBuilder mb, int s, Vector3 centre, float width, float length, float rise, float overhang)
        {
            float hw = width * .5f + overhang, hl = length * .5f + overhang;
            var ridgeA = centre + new Vector3(0, rise, -hl);
            var ridgeB = centre + new Vector3(0, rise, hl);
            for (int i = -1; i <= 1; i += 2)
            {
                var eaveA = centre + new Vector3(i * hw, 0, -hl);
                var eaveB = centre + new Vector3(i * hw, 0, hl);
                if (i < 0) mb.Quad(s, eaveA, ridgeA, ridgeB, eaveB, Vector2.zero, new Vector2(1, 0), new Vector2(1, length * .5f), new Vector2(0, length * .5f), true);
                else mb.Quad(s, eaveB, ridgeB, ridgeA, eaveA, Vector2.zero, new Vector2(1, 0), new Vector2(1, length * .5f), new Vector2(0, length * .5f), true);
            }
        }

        /// <summary>The triangle that closes a gable end. <paramref name="nz"/> is +1 for the wall that faces +Z.</summary>
        static void GableEnd(MeshBuilder mb, int s, float z, float nz, float halfWidth, float y0, float rise)
        {
            var n = new Vector3(0, 0, nz);
            int a = mb.Vert(new Vector3(-halfWidth, y0, z), n, Vector2.zero);
            int b = mb.Vert(new Vector3(halfWidth, y0, z), n, new Vector2(1, 0));
            int c = mb.Vert(new Vector3(0, y0 + rise, z), n, new Vector2(.5f, 1));
            if (nz > 0) mb.Tri(s, a, c, b); else mb.Tri(s, a, b, c);
        }

        /// <summary>The flat end of a barrel or a capsule: a disc in the plane z = <paramref name="plane"/> with one
        /// rectangular hole in it. Built as vertical slats between the two arcs of the circle, so the hole comes out
        /// square and the rim stays round; both faces, because an end wall is seen from inside as well as out.</summary>
        static void DiscWall(MeshBuilder mb, int s, float plane, float cy, float r, int slices,
            float hx0, float hx1, float hy0, float hy1, float yMin = float.NegativeInfinity)
        {
            void Slat(float x0, float x1, float y0a, float y1a, float y0b, float y1b)
            {
                if (y1a - y0a < 1e-3f && y1b - y0b < 1e-3f) return;
                if (y1a < y0a) y1a = y0a;
                if (y1b < y0b) y1b = y0b;
                mb.Quad(s, new Vector3(x0, y0a, plane), new Vector3(x1, y0b, plane), new Vector3(x1, y1b, plane), new Vector3(x0, y1a, plane),
                    Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1), true);
            }
            for (int i = 0; i < slices; i++)
            {
                float x0 = -r + 2f * r * i / slices, x1 = -r + 2f * r * (i + 1) / slices;
                float h0 = Mathf.Sqrt(Mathf.Max(0f, r * r - x0 * x0)), h1 = Mathf.Sqrt(Mathf.Max(0f, r * r - x1 * x1));
                // the floor of a half-cylinder cuts the disc off at yMin; a whole barrel leaves it at −∞
                float b0 = Mathf.Max(cy - h0, yMin), b1 = Mathf.Max(cy - h1, yMin);
                float u0 = cy + h0, u1 = cy + h1;
                if (u0 <= b0 && u1 <= b1) continue;
                bool crosses = hy1 > hy0 && x1 > hx0 && x0 < hx1;
                if (!crosses) { Slat(x0, x1, b0, u0, b1, u1); continue; }
                Slat(x0, x1, b0, Mathf.Min(hy0, u0), b1, Mathf.Min(hy0, u1));
                Slat(x0, x1, Mathf.Max(hy1, b0), u0, Mathf.Max(hy1, b1), u1);
            }
        }

        /// <summary>A cylinder wall round the z axis at height <paramref name="cy"/>, facing out or in.</summary>
        static void Cylinder(MeshBuilder mb, int s, float cy, float z0, float z1, float r, int sides, bool inward, float uv = .5f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, z0);
                var p1 = new Vector3(Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, z0);
                var q0 = new Vector3(p0.x, p0.y, z1);
                var q1 = new Vector3(p1.x, p1.y, z1);
                float u0 = i / (float)sides * uv * r * 6.28f, u1 = (i + 1) / (float)sides * uv * r * 6.28f, v = (z1 - z0) * uv;
                // Quad's face is Cross(b - a, d - a): p0-p1-q1-q0 points out of the cylinder, p0-q0-q1-p1 into it
                if (inward) mb.Quad(s, p0, q0, q1, p1, new Vector2(u0, 0), new Vector2(u0, v), new Vector2(u1, v), new Vector2(u1, 0));
                else mb.Quad(s, p0, p1, q1, q0, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, v), new Vector2(u0, v));
            }
        }

        // ── what is inside every one of them ──────────────────────────────────────────────────────────────
        /// <summary>Нары: a bunk of one or two tiers against a wall. <paramref name="foot"/> is the middle of its
        /// footprint on the floor, the bunk lies along z and is <paramref name="depth"/> deep along x;
        /// <paramref name="face"/> is +1 when the open side is towards +x. Frame in steel, deck and ladder in board,
        /// a mattress, a folded blanket at the foot and a pillow at the head — this is what the night is spent on, so
        /// it is a bed and not a shelf.</summary>
        static void Bunk(MeshBuilder wood, MeshBuilder steel, MeshBuilder cloth, Vector3 foot, float len, float depth,
            int tiers, int face, int seed)
        {
            var rng = new System.Random(seed);
            float hz = len * .5f, hx = depth * .5f;
            for (int tier = 0; tier < tiers; tier++)
            {
                float y = foot.y + .44f + tier * .92f;
                wood.Box(0, new Vector3(foot.x, y, foot.z), new Vector3(depth, .06f, len), Quaternion.identity, .9f);
                // the mattress, and a blanket somebody half folded at the foot of it
                cloth.Box(0, new Vector3(foot.x, y + .085f, foot.z), new Vector3(depth - .1f, .11f, len - .12f), Quaternion.identity, 1f);
                float bz = foot.z + (rng.Next(2) == 0 ? -1f : 1f) * (hz - .36f);
                cloth.Box(1, new Vector3(foot.x - face * .03f, y + .19f, bz), new Vector3(depth - .16f, .1f, .62f),
                    Quaternion.Euler(0, (float)(rng.NextDouble() - .5) * 9f, 0), 1f);
                cloth.Box(1, new Vector3(foot.x + face * .04f, y + .19f, foot.z - (bz - foot.z) * .92f), new Vector3(depth * .6f, .09f, .34f),
                    Quaternion.Euler(0, (float)(rng.NextDouble() - .5) * 14f, 0), 1f);   // the pillow at the other end
            }
            // the frame: four legs, a rail along the open side of the upper tier and a ladder at one end
            float top = foot.y + .44f + (tiers - 1) * .92f;
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    steel.Tube(0, new Vector3(foot.x + i * (hx - .05f), foot.y, foot.z + k * (hz - .06f)),
                        new Vector3(foot.x + i * (hx - .05f), top + (tiers > 1 ? .5f : .06f), foot.z + k * (hz - .06f)), .026f, .026f, 5, 1f, 0, true);
            if (tiers > 1)
            {
                steel.Tube(0, new Vector3(foot.x + face * (hx - .05f), top + .46f, foot.z - hz + .06f),
                    new Vector3(foot.x + face * (hx - .05f), top + .46f, foot.z + hz - .06f), .02f, .02f, 5, 1f);
                for (int k = 0; k < 3; k++)
                    steel.Tube(0, new Vector3(foot.x + face * (hx - .05f), foot.y + .62f + k * .26f, foot.z + hz - .1f),
                        new Vector3(foot.x + face * (hx - .55f), foot.y + .62f + k * .26f, foot.z + hz - .1f), .016f, .016f, 4, 1f);
            }
        }

        /// <summary>A small stove: the drum, the plate on top, four legs, the pipe out through the roof and the
        /// firebox door standing open.</summary>
        static void StoveSmall(MeshBuilder steel, MeshBuilder glow, Vector3 at, float pipeTop, int face = 1)
        {
            steel.Tube(0, at + new Vector3(0, .16f, 0), at + new Vector3(0, .88f, 0), .29f, .28f, 12, 1f, 0, true);
            steel.Box(0, at + new Vector3(0, .92f, 0), new Vector3(.7f, .06f, .7f), Quaternion.identity, 1f);
            for (int k = 0; k < 4; k++)
                steel.Tube(0, at + new Vector3((k & 1) == 0 ? -.19f : .19f, 0, k < 2 ? -.19f : .19f),
                    at + new Vector3((k & 1) == 0 ? -.21f : .21f, .17f, k < 2 ? -.21f : .21f), .028f, .028f, 5, 1f, 0, true);
            steel.Tube(0, at + new Vector3(0, .94f, 0), new Vector3(at.x, pipeTop, at.z), .085f, .08f, 8, 1f, 0, true);
            glow.Box(0, at + new Vector3(0, .48f, face * .29f), new Vector3(.3f, .27f, .04f), Quaternion.identity, 1f);
        }

        /// <summary>Сушилка: a frame of hot pipes with boots under it and wet things over it. The one thing every
        /// приют is judged on, and the reason <see cref="Lodging.Dry"/> exists.</summary>
        static void DryRack(MeshBuilder steel, MeshBuilder cloth, MeshBuilder rubber, Vector3 at, float w, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = -1; i <= 1; i += 2)
                steel.Tube(0, at + new Vector3(i * w * .5f, 0, 0), at + new Vector3(i * w * .5f, 1.72f, 0), .03f, .03f, 6, 1f, 0, true);
            for (int k = 0; k < 3; k++)
            {
                float y = .66f + k * .5f;
                steel.Tube(0, at + new Vector3(-w * .5f, y, 0), at + new Vector3(w * .5f, y, 0), .022f, .022f, 6, 1f);
            }
            int n = Mathf.Max(2, Mathf.RoundToInt(w / .55f));
            for (int k = 0; k < n; k++)
            {
                float x = -w * .5f + (k + .5f) * w / n;
                if (rng.Next(3) > 0)
                    cloth.Box(2, at + new Vector3(x, 1.34f, .02f), new Vector3(.36f, .52f, .12f),
                        Quaternion.Euler(0, 0, (float)(rng.NextDouble() - .5) * 8f), 1f);
                // a pair of boots on the floor, toes out
                for (int b = -1; b <= 1; b += 2)
                {
                    var p = at + new Vector3(x + b * .09f, .07f, .18f + (float)rng.NextDouble() * .1f);
                    rubber.Box(0, p, new Vector3(.12f, .14f, .3f), Quaternion.Euler(0, (float)(rng.NextDouble() - .5) * 20f, 0), 1f);
                    rubber.Box(0, p + new Vector3(0, .16f, -.05f), new Vector3(.12f, .2f, .16f), Quaternion.identity, 1f);
                }
            }
        }

        /// <summary>Open shelving against a wall: uprights and boards, <paramref name="levels"/> of them.</summary>
        static void Shelves(MeshBuilder wood, Vector3 at, float w, float d, int levels, float pitch, float y0 = .25f, bool alongZ = false)
        {
            Vector3 Along(float v) => alongZ ? new Vector3(0, 0, v) : new Vector3(v, 0, 0);
            var size = alongZ ? new Vector3(d, .04f, w) : new Vector3(w, .04f, d);
            var post = alongZ ? new Vector3(d, y0 + levels * pitch, .06f) : new Vector3(.06f, y0 + levels * pitch, d);
            for (int i = -1; i <= 1; i += 2)
                wood.Box(0, at + Along(i * (w * .5f - .03f)) + new Vector3(0, y0 + levels * pitch * .5f, 0), post, Quaternion.identity, .8f);
            for (int k = 0; k <= levels; k++)
                wood.Box(0, at + new Vector3(0, y0 + k * pitch, 0), size, Quaternion.identity, .8f);
        }

        /// <summary>A bulb in a tin shade under the ceiling, and the one real light that goes with it. Short range and
        /// no shadows: there are a dozen of these rooms on the slope and every shadowed point light is paid for.</summary>
        static void CeilingLamp(Transform t, MeshBuilder steel, MeshBuilder glow, Vector3 at, float range, float intensity, string name)
        {
            steel.Tube(0, at, at + new Vector3(0, -.24f, 0), .012f, .012f, 5, 1f);
            steel.Cone(0, at + new Vector3(0, -.42f, 0), .18f, .17f, 9, 1f);
            glow.Tube(0, at + new Vector3(0, -.46f, 0), at + new Vector3(0, -.43f, 0), .12f, .12f, 9, 1f, 0, true);
            if (intensity <= 0f) return;                 // a shade and a bulb, but no second real light in the room
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(t, false);
            go.transform.localPosition = at + new Vector3(0, -.5f, 0);
            var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, .87f, .66f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>A table on tube legs.</summary>
        static void TableTop(MeshBuilder wood, MeshBuilder steel, Vector3 at, float w, float l, float h)
        {
            wood.Box(0, at + new Vector3(0, h, 0), new Vector3(w, .05f, l), Quaternion.identity, 1f);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    steel.Tube(0, at + new Vector3(i * (w * .5f - .09f), 0, k * (l * .5f - .09f)),
                        at + new Vector3(i * (w * .5f - .09f), h, k * (l * .5f - .09f)), .022f, .022f, 5, 1f, 0, true);
        }

        /// <summary>A bench: the seat people change on, with or without a back.</summary>
        static void BenchSeat(MeshBuilder wood, Vector3 at, float len, float depth, float h, bool back)
        {
            wood.Box(0, at + new Vector3(0, h, 0), new Vector3(len, .05f, depth), Quaternion.identity, .9f);
            for (int i = -1; i <= 1; i += 2)
                wood.Box(0, at + new Vector3(i * (len * .5f - .14f), h * .5f, 0), new Vector3(.08f, h, depth - .06f), Quaternion.identity, .9f);
            if (back) wood.Box(0, at + new Vector3(0, h + .3f, -depth * .5f + .04f), new Vector3(len, .55f, .05f), Quaternion.identity, .9f);
        }

        /// <summary>A gas bottle — the blue five-litre one every kitchen above the road runs on.</summary>
        static void GasBottle(MeshBuilder mb, int s, Vector3 at, float h)
        {
            mb.Tube(s, at + new Vector3(0, .02f, 0), at + new Vector3(0, h - .1f, 0), .155f, .15f, 10, 1f, 0, true);
            mb.Tube(s, at + new Vector3(0, h - .1f, 0), at + new Vector3(0, h, 0), .14f, .05f, 10, 1f, 0, true);
            mb.Tube(s, at + new Vector3(0, h, 0), at + new Vector3(0, h + .07f, 0), .03f, .03f, 6, 1f, 0, true);
        }

        /// <summary>A pot or a kettle on a plate.</summary>
        static void Pot(MeshBuilder mb, int s, Vector3 at, float r, float h)
        {
            mb.Tube(s, at, at + new Vector3(0, h, 0), r, r * .96f, 10, 1f, 0, true);
            mb.Tube(s, at + new Vector3(0, h, 0), at + new Vector3(0, h + .02f, 0), r * .99f, r * .92f, 10, 1f, 0, true);
        }

        /// <summary>A line strung under the ceiling with clothes over it — the way everything in a hut is stored.</summary>
        static void ClothLine(MeshBuilder steel, MeshBuilder cloth, Vector3 a, Vector3 b, int n, int seed)
        {
            steel.Tube(0, a, b, .008f, .008f, 4, 1f);
            var rng = new System.Random(seed);
            for (int k = 0; k < n; k++)
            {
                float t = (k + .5f) / n;
                var p = Vector3.Lerp(a, b, t);
                cloth.Box(2, p + new Vector3(0, -.24f, 0), new Vector3(.3f + (float)rng.NextDouble() * .14f, .44f, .1f),
                    Quaternion.Euler(0, (float)(rng.NextDouble() - .5) * 26f, (float)(rng.NextDouble() - .5) * 7f), 1f);
            }
        }

        // ── 1. the base on the Azau meadow ────────────────────────────────────────────────────────────────
        /// <summary>«Спасотряд и прокат», поляна Азау, 2 350 м — the room the week starts in. A low building of timber
        /// and profiled sheet on a plank floor, a porch with a sign over it, a stove pipe out of the roof: an
        /// Prielbrusye shed, not an office. Inside, three corners and the life between them.
        ///
        /// <b>Hire</b> (the right-hand half): a counter with the shop behind it — racks of crampons, ice axes, helmets,
        /// harnesses, down jackets and plastic boots, the price list of <see cref="Rental"/> chalked on the wall by the
        /// counter, and the heap of kit handed back that morning and not yet sorted.
        ///
        /// <b>The ЭВПСО МЧС desk</b> (the left-hand half, by the back wall): the register of parties open on it, the
        /// radio set beside it with its whip, the rules board over it with the control time
        /// (<see cref="Rescue.ControlHour"/>, <see cref="Rescue.BackHour"/>, <see cref="Rescue.SignalCeiling"/>), the
        /// map of the district on the side wall with the southern route drawn up it, and the emblem.
        ///
        /// <b>The weather board</b> (on the front wall, where the light is): <see cref="Forecast.BoardText"/> chalked
        /// for 3 800 / 4 800 / 5 400 / 5 642 m for today and tomorrow, with a thermometer and an aneroid beside it.
        ///
        /// And what makes it a room: benches to unpack a rucksack on, a drying frame with boots under it, a kettle in
        /// the corner, the guides' notices, a board of summit photographs and the price list of the bunks above
        /// (<see cref="Lodging.BoardText"/>).
        ///
        /// Pivot = the ground under the middle of the building, +Z = the front with the porch and the door.</summary>
        static GameObject BaseAzau()
        {
            const float W = 13f, L = 10f, H = 2.9f, T = .2f, F = .22f, Porch = 2.6f;
            float hw = W * .5f, hl = L * .5f, top = F + H;
            float zf = hl - T * .5f, zb = -hl + T * .5f, xw = -hw + T * .5f, xe = hw - T * .5f;
            float inF = hl - T, inB = -hl + T, inW = -hw + T, inE = hw - T;      // the inner faces of the four walls
            const float DW = 1.5f, DH = 2.15f, dx = -3.6f;
            float dL = dx - DW * .5f, dR = dx + DW * .5f;
            float wy0 = F + 1f, wy1 = F + 2.15f;                                  // the band the windows sit in

            var root = new GameObject("Elb_Base_Azau");
            var t = root.transform;

            // ── floor, porch deck and the steps down off it ───────────────────────────────────────────────
            var floor = new MeshBuilder(1);
            floor.Box(0, new Vector3(0, F * .5f, 0), new Vector3(W, F, L), Quaternion.identity, .5f);
            floor.Box(0, new Vector3(0, F * .5f, hl + Porch * .5f), new Vector3(W - 2f, F, Porch), Quaternion.identity, .5f);
            Part(t, "Floor", floor, Plank);

            // five steps of 0.16 m off the deck, reaching 0.6 m below the pivot: on a plinth the door is still
            // walkable, on flat ground all but the first are buried and never show (the cafés do the same)
            var steps = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
            {
                var c = new Vector3(dx, -k * .16f - .7f, hl + Porch + .2f + k * .42f);
                var s = new Vector3(3.2f, 1.4f, .42f);
                steps.Box(0, c, s, Quaternion.identity, .6f);
                Solid(t, "StepSolid" + k, c, s);
            }
            Part(t, "Steps", steps, Concrete);

            // ── the four walls, built as the pieces round their openings ─────────────────────────────────
            var walls = new MeshBuilder(1);
            Panel(walls, 0, true, zf, T, -hw, -2f, 0f, top, dL, dR, F, F + DH);              // front: the door
            Panel(walls, 0, true, zf, T, -2f, .8f, 0f, top, -1.8f, .6f, wy0, wy1);           // front: the window
            Panel(walls, 0, true, zf, T, .8f, hw, 0f, top, 0f, 0f, 0f, 0f);                  // front: solid, the boards hang here
            Panel(walls, 0, true, zb, T, -hw, hw, 0f, top, 0f, 0f, 0f, 0f);                  // back: solid
            Panel(walls, 0, false, xw, T, -hl, hl, 0f, top, 2f, 3.6f, wy0, wy1);             // west: one window by the door
            Panel(walls, 0, false, xe, T, -hl, hl, 0f, top, -.4f, 1.4f, wy0, wy1);           // east: one over the counter
            Part(t, "Walls", walls, Plank);

            // the outside is clad in profiled sheet to the sill and boarded above it, the way half the meadow is
            var clad = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                clad.Box(0, new Vector3(i * (hw + .03f), (F + .95f) * .5f, 0), new Vector3(.06f, F + .95f, L + .04f), Quaternion.identity, .9f);
            clad.Box(0, new Vector3(0, (F + .95f) * .5f, -hl - .03f), new Vector3(W + .1f, F + .95f, .06f), Quaternion.identity, .9f);
            clad.Box(0, new Vector3((.8f + hw) * .5f, (F + .95f) * .5f, hl + .03f), new Vector3(hw - .8f, F + .95f, .06f), Quaternion.identity, .9f);
            clad.Box(0, new Vector3((-hw - 2f) * .5f, (F + .95f) * .5f, hl + .03f), new Vector3(hw - 2f, F + .95f, .06f), Quaternion.identity, .9f);
            Part(t, "Cladding", clad, SheetGreen);

            var glass = new MeshBuilder(1);
            PaneZ(glass, 0, zf, -1.8f, .6f, wy0, wy1);
            PaneX(glass, 0, xw, 2f, 3.6f, wy0, wy1);
            PaneX(glass, 0, xe, -.4f, 1.4f, wy0, wy1);
            var pane = Part(t, "Glazing", glass, GlassClear);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ── roof, ceiling, chimney ───────────────────────────────────────────────────────────────────
            var roof = new MeshBuilder(1);
            Gable(roof, 0, new Vector3(0, top, 0), W, L, 1.5f, .65f);
            GableEnd(roof, 0, hl + .65f, 1f, hw + .65f, top, 1.5f);
            GableEnd(roof, 0, -hl - .65f, -1f, hw + .65f, top, 1.5f);
            Part(t, "Roof", roof, RoofRust);

            var ceiling = new MeshBuilder(1);
            ceiling.Box(0, new Vector3(0, top - .07f, 0), new Vector3(W - 2f * T, .14f, L - 2f * T), Quaternion.identity, .6f);
            Part(t, "Ceiling", ceiling, PlankDark);

            // ── the porch: four posts, a shed roof, the sign and the lamp over the door ───────────────────
            var porch = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 2; k++)
                {
                    float px = dx + i * 1.9f, pz = hl + .5f + k * (Porch - 1f);
                    porch.Box(0, new Vector3(px, F + 1.25f, pz), new Vector3(.13f, 2.5f, .13f), Quaternion.identity, 1f);
                }
            porch.Box(0, new Vector3(dx, F + 2.56f, hl + Porch * .5f), new Vector3(4.1f, .12f, Porch), Quaternion.identity, 1f);
            porch.Box(0, new Vector3(dx, F + 2.66f, hl + Porch * .5f), new Vector3(4.4f, .08f, Porch + .3f), Quaternion.Euler(0, 0, 0), 1f);
            // rails down the two sides of the porch, and nothing across the front: the steps come up there
            for (int i = -1; i <= 1; i += 2)
            {
                porch.Box(0, new Vector3(dx + i * 1.9f, F + .92f, hl + Porch * .5f), new Vector3(.08f, .07f, Porch - 1f), Quaternion.identity, 1f);
                porch.Box(0, new Vector3(dx + i * 1.9f, F + .55f, hl + Porch * .5f), new Vector3(.06f, .06f, Porch - 1f), Quaternion.identity, 1f);
            }
            Part(t, "Porch", porch, PlankDark);

            var board = new MeshBuilder(1);
            board.Box(0, new Vector3(dx, F + 2.28f, hl + Porch - .02f), new Vector3(4f, .52f, .06f), Quaternion.identity, 1f);
            Part(t, "SignBoard", board, Blue);
            Sign(t, "SignName", "ЭВПСО МЧС · ПРОКАТ СНАРЯЖЕНИЯ · 2350 м",
                new Vector3(dx, F + 2.28f, hl + Porch + .03f), 0f, .034f, new Color(.95f, .95f, .92f), TextAlignment.Center);

            // the door leaf, standing open against the wall, and the frame round the hole
            var trim = new MeshBuilder(1);
            trim.Box(0, new Vector3(dL - .07f, F + DH * .5f, zf), new Vector3(.14f, DH, T + .05f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(dR + .07f, F + DH * .5f, zf), new Vector3(.14f, DH, T + .05f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(dx, F + DH + .07f, zf), new Vector3(DW + .28f, .14f, T + .05f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(dR + .52f, F + DH * .5f - .05f, hl + .48f), new Vector3(.86f, DH - .1f, .06f), Quaternion.Euler(0, 62f, 0), 1f);
            Part(t, "Trim", trim, PlankDark);

            // ── the hire counter and the shop behind it ──────────────────────────────────────────────────
            const float barZ = -2.4f, barX0 = .2f, barX1 = 6.3f;
            float barX = (barX0 + barX1) * .5f, barW = barX1 - barX0;
            var joinery = new MeshBuilder(1);            // everything made of board in this room
            var metal = new MeshBuilder(1);              // everything made of steel
            var cloth = new MeshBuilder(3);              // 0 mattress, 1 blanket, 2 hanging clothes
            var rubber = new MeshBuilder(1);
            var glow = new MeshBuilder(1);
            var paper = new MeshBuilder(1);

            joinery.Box(0, new Vector3(barX, F + .5f, barZ), new Vector3(barW, 1f, .72f), Quaternion.identity, .8f);
            joinery.Box(0, new Vector3(barX, F + 1.04f, barZ), new Vector3(barW + .14f, .08f, .86f), Quaternion.identity, .8f);
            Shelves(joinery, new Vector3(3.2f, F, -4.52f), 5.8f, .52f, 4, .44f);

            // what is on those racks: the thirteen lines of Rental, as objects
            for (int k = 0; k < 7; k++)                                              // helmets, in a row on the top shelf
                metal.Tube(0, new Vector3(.9f + k * .72f, F + 2.05f, -4.62f), new Vector3(.9f + k * .72f, F + 2.18f, -4.62f), .13f, .1f, 9, 1f, 0, true);
            for (int k = 0; k < 8; k++)                                              // crampons, paired and hooked
            {
                float cx = .7f + k * .66f;
                metal.Box(0, new Vector3(cx, F + 1.63f, -4.62f), new Vector3(.12f, .04f, .3f), Quaternion.Euler(0, 0, 0), 1f);
                for (int p = 0; p < 5; p++)
                    metal.Box(0, new Vector3(cx - .05f + p * .025f, F + 1.57f, -4.72f + p * .05f), new Vector3(.02f, .09f, .02f), Quaternion.identity, 1f);
            }
            for (int k = 0; k < 6; k++)                                              // ice axes, leaning in the corner rack
            {
                var a = new Vector3(inE - .55f + (k % 3) * .16f, F, -3.9f + (k / 3) * .22f);
                metal.Tube(0, a, a + new Vector3(.16f, 1.62f, .04f), .016f, .014f, 5, 1f, 0, true);
                metal.Box(0, a + new Vector3(.17f, 1.63f, .04f), new Vector3(.26f, .05f, .05f), Quaternion.Euler(0, 0, 14f), 1f);
            }
            for (int k = 0; k < 6; k++)                                              // harnesses on their pegs
                cloth.Box(2, new Vector3(1.1f + k * .9f, F + 1.18f, -4.55f), new Vector3(.3f, .34f, .1f), Quaternion.Euler(0, 0, (k % 2) * 6f - 3f), 1f);
            for (int k = 0; k < 5; k++)                                              // down jackets on a rail
                cloth.Box(2, new Vector3(1.4f + k * .84f, F + .72f, -4.5f), new Vector3(.42f, .78f, .16f), Quaternion.Euler(0, 0, (k % 2 == 0 ? 4f : -5f)), 1f);
            for (int k = 0; k < 10; k++)                                             // plastic boots, on the bottom shelf
            {
                float bx = .6f + k * .58f;
                rubber.Box(0, new Vector3(bx, F + .36f, -4.62f), new Vector3(.14f, .16f, .32f), Quaternion.Euler(0, (k % 3) * 7f - 7f, 0), 1f);
                rubber.Box(0, new Vector3(bx, F + .5f, -4.7f), new Vector3(.14f, .16f, .17f), Quaternion.identity, 1f);
            }
            // and the pile handed back this morning, dumped inside the door of the shop and not yet sorted
            var rng = new System.Random(2350);
            for (int k = 0; k < 9; k++)
            {
                var c = new Vector3(1.1f + (float)rng.NextDouble() * 1.6f, F + .1f + (k % 3) * .16f, -1.5f + (float)rng.NextDouble() * .7f);
                cloth.Box(2, c, new Vector3(.36f, .2f, .5f), Quaternion.Euler(0, (float)rng.NextDouble() * 180f, (float)(rng.NextDouble() - .5) * 12f), 1f);
            }
            for (int k = 0; k < 4; k++)
                rubber.Box(0, new Vector3(2.9f + (float)rng.NextDouble() * .8f, F + .09f, -1.3f + (float)rng.NextDouble() * .8f),
                    new Vector3(.15f, .17f, .33f), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), 1f);

            var priceBack = new MeshBuilder(1);
            priceBack.Box(0, new Vector3(inE - .04f, F + 1.62f, -1.6f), new Vector3(.05f, 1.5f, 1.35f), Quaternion.identity, 1f);
            Part(t, "PriceBoard", priceBack, Chalkboard);
            Sign(t, "RentalBoard", "ПРОКАТ · за сутки\n\n" + Rental.BoardText() + $"\n\nВесь комплект — {Rental.SetRoubles} ₽",
                new Vector3(inE - .08f, F + 1.62f, -1.6f), -90f, .014f, new Color(.93f, .93f, .88f));
            Marker(t, "Elb_RentStand_Desk", new Vector3(barX - .4f, F, barZ + 1.15f), 180f);

            // ── the desk of the rescue service ───────────────────────────────────────────────────────────
            const float deskZ = -3.6f;
            joinery.Box(0, new Vector3(-4.1f, F + .74f, deskZ), new Vector3(4.2f, .06f, .8f), Quaternion.identity, .9f);
            joinery.Box(0, new Vector3(-6.05f, F + .37f, deskZ), new Vector3(.1f, .74f, .78f), Quaternion.identity, .9f);
            joinery.Box(0, new Vector3(-2.15f, F + .37f, deskZ), new Vector3(.1f, .74f, .78f), Quaternion.identity, .9f);
            joinery.Box(0, new Vector3(-5.1f, F + .34f, deskZ - .2f), new Vector3(1.5f, .68f, .4f), Quaternion.identity, .9f);   // the drawers
            BenchSeat(joinery, new Vector3(-4.2f, F, deskZ - .85f), .5f, .44f, .45f, true);                                      // the duty officer's chair

            // the register, open, with a pen across it
            paper.Box(0, new Vector3(-4.6f, F + .79f, deskZ + .02f), new Vector3(.62f, .03f, .42f), Quaternion.Euler(0, 6f, 0), 1f);
            paper.Box(0, new Vector3(-4.94f, F + .82f, deskZ + .03f), new Vector3(.3f, .02f, .4f), Quaternion.Euler(0, 6f, -6f), 1f);
            metal.Tube(0, new Vector3(-4.42f, F + .81f, deskZ - .08f), new Vector3(-4.3f, F + .81f, deskZ + .06f), .006f, .006f, 4, 1f, 0, true);
            Sign(t, "RegisterCard", "ЖУРНАЛ РЕГИСТРАЦИИ ГРУПП", new Vector3(-4.6f, F + .82f, deskZ - .17f), 0f, .012f,
                new Color(.25f, .24f, .22f), TextAlignment.Center);

            // the radio set on the end of the desk, with its whip up into the corner
            metal.Box(0, new Vector3(-2.7f, F + .91f, deskZ - .06f), new Vector3(.44f, .28f, .34f), Quaternion.identity, 1f);
            metal.Tube(0, new Vector3(-2.7f, F + 1.05f, deskZ - .14f), new Vector3(-2.66f, F + 2.5f, deskZ - .2f), .008f, .006f, 4, 1f, 0, true);
            for (int k = -1; k <= 1; k += 2)
                metal.Tube(0, new Vector3(-2.7f + k * .12f, F + .86f, deskZ + .11f), new Vector3(-2.7f + k * .12f, F + .86f, deskZ + .13f), .035f, .035f, 8, 1f, 0, true);
            glow.Box(0, new Vector3(-2.56f, F + .98f, deskZ + .115f), new Vector3(.05f, .03f, .01f), Quaternion.identity, 1f);

            // the rules board over the desk, and the emblem beside it
            var boards = new MeshBuilder(1);
            boards.Box(0, new Vector3(-4.3f, F + 1.95f, inB + .03f), new Vector3(2.9f, 1.15f, .05f), Quaternion.identity, 1f);
            Part(t, "RulesBoard", boards, Paper);
            Sign(t, "RulesText",
                "ЭВПСО МЧС РОССИИ · РЕГИСТРАЦИЯ ГРУПП\n"
                + "Состав, маршрут, контрольное время возвращения.\n"
                + $"Маршрут: {Rescue.DefaultRoute}.\n"
                + $"Разворот — {AscentRoute.Clock(Rescue.ControlHour)}, возвращение в лагерь — {AscentRoute.Clock(Rescue.BackHour)}.\n"
                + $"Не вернулись — поиск начинается в {AscentRoute.Clock(Rescue.BackHour + Rescue.GraceHours)}.\n"
                + $"Связи выше {Rescue.SignalCeiling:0} м нет: SOS — только ниже.\n"
                + "Без регистрации никто не узнает, что вас нет.",
                new Vector3(-4.3f, F + 1.95f, inB + .06f), 0f, .022f, new Color(.2f, .19f, .18f));

            var emblem = new MeshBuilder(2);            // 0 the disc, 1 the star on it
            emblem.Tube(0, new Vector3(-1.5f, F + 2.05f, inB + .02f), new Vector3(-1.5f, F + 2.05f, inB + .06f), .3f, .3f, 14, 1f, 0, true);
            for (int k = 0; k < 8; k++)
                emblem.Box(1, new Vector3(-1.5f, F + 2.05f, inB + .07f), new Vector3(.055f, .46f, .012f), Quaternion.Euler(0, 0, k * 22.5f), 1f);
            Part(t, "Emblem", emblem, Blue, Orange);

            // the map of the district on the west wall, with the southern route drawn up it
            var map = new MeshBuilder(1);
            map.Box(0, new Vector3(inW + .03f, F + 1.62f, -1.5f), new Vector3(.05f, 1.3f, 3.4f), Quaternion.identity, 1f);
            Part(t, "Map", map, Paper);
            var route = new MeshBuilder(2);             // 0 the line of the route, 1 the marks on it
            // (along the board, up the board) — kept inside the 3.4 × 1.3 m sheet
            var pts = new[]
            {
                new Vector2(-1.5f, -.28f), new Vector2(-1.15f, -.1f), new Vector2(-.78f, .02f),
                new Vector2(-.4f, .12f), new Vector2(-.05f, .22f), new Vector2(.72f, .32f), new Vector2(1.5f, .38f),
            };
            for (int k = 1; k < pts.Length; k++)
            {
                var a = pts[k - 1]; var b = pts[k];
                var mid = (a + b) * .5f; var d = b - a;
                route.Box(0, new Vector3(inW + .06f, F + 1.62f + mid.y, -1.5f + mid.x),
                    new Vector3(.012f, .035f, d.magnitude), Quaternion.Euler(-Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f, 0f), 1f);
            }
            foreach (var p in new[] { pts[0], pts[2], pts[4], pts[6] })
                route.Box(1, new Vector3(inW + .07f, F + 1.62f + p.y, -1.5f + p.x), new Vector3(.012f, .075f, .075f), Quaternion.identity, 1f);
            Part(t, "MapRoute", route, Red, Blue);
            Sign(t, "MapTitle", "ЮЖНЫЙ СКЛОН · МАРШРУТ И КОНТРОЛЬНОЕ ВРЕМЯ",
                new Vector3(inW + .08f, F + 2.16f, -1.5f), 90f, .022f, new Color(.2f, .2f, .22f), TextAlignment.Center);
            Sign(t, "MapClock",
                $"Азау 2350 — Гара-Баши 3847 — бочки 3710\nПастухова 4650 — седловина 5416 — запад. 5642\n"
                + $"Разворот {AscentRoute.Clock(Rescue.ControlHour)} · возвращение {AscentRoute.Clock(Rescue.BackHour)}",
                new Vector3(inW + .08f, F + 1.16f, -1.5f), 90f, .02f, new Color(.24f, .22f, .2f), TextAlignment.Center);
            Marker(t, "Elb_RescueDesk", new Vector3(-4.1f, F, deskZ + 1.2f), 180f);

            // ── the weather board on the front wall ──────────────────────────────────────────────────────
            var slate = new MeshBuilder(1);
            slate.Box(0, new Vector3(2.6f, F + 1.62f, inF - .03f), new Vector3(2.9f, 1.62f, .06f), Quaternion.identity, 1f);
            Part(t, "WeatherSlate", slate, Chalkboard);
            Sign(t, "Forecast", Forecast.BoardText(BoardSeed, BoardDay, 1),
                new Vector3(2.6f, F + 1.62f, inF - .07f), 180f, .018f, new Color(.93f, .94f, .9f));
            // an aneroid and a thermometer beside it, which is what a hut board always has
            var gauges = new MeshBuilder(2);            // 0 case, 1 face
            gauges.Tube(0, new Vector3(4.65f, F + 1.95f, inF - .02f), new Vector3(4.65f, F + 1.95f, inF - .09f), .17f, .17f, 14, 1f, 0, true);
            gauges.Tube(1, new Vector3(4.65f, F + 1.95f, inF - .095f), new Vector3(4.65f, F + 1.95f, inF - .1f), .15f, .15f, 14, 1f, 0, true);
            gauges.Box(0, new Vector3(4.65f, F + 1.95f, inF - .11f), new Vector3(.02f, .19f, .01f), Quaternion.Euler(0, 0, 34f), 1f);
            gauges.Box(0, new Vector3(4.65f, F + 1.2f, inF - .04f), new Vector3(.07f, .5f, .04f), Quaternion.identity, 1f);
            gauges.Box(1, new Vector3(4.65f, F + 1.2f, inF - .065f), new Vector3(.03f, .44f, .01f), Quaternion.identity, 1f);
            Part(t, "Gauges", gauges, Steel, Paper);
            Marker(t, "Elb_WeatherBoard", new Vector3(2.6f, F, inF - 1.4f), 0f);

            // ── what makes it a room ─────────────────────────────────────────────────────────────────────
            BenchSeat(joinery, new Vector3(-4.2f, F, -.6f), 4f, .46f, .45f, false);
            BenchSeat(joinery, new Vector3(-4.2f, F, .9f), 4f, .46f, .45f, false);
            for (int k = 0; k < 5; k++)                                                    // packs and boots under them
            {
                var c = new Vector3(-5.9f + k * .95f, F + .16f, -.6f + (k % 2) * 1.5f);
                cloth.Box(2, c, new Vector3(.36f, .3f, .28f), Quaternion.Euler(0, (k * 37) % 90 - 45f, 0), 1f);
            }
            DryRack(metal, cloth, rubber, new Vector3(-5.1f, F, 2.5f), 2.2f, 7);
            StoveSmall(metal, glow, new Vector3(-.9f, F, -4.1f), top + 1.5f);
            joinery.Box(0, new Vector3(-.9f, F + .06f, -3.35f), new Vector3(1.1f, .12f, .6f), Quaternion.identity, 1f);   // the woodbox by it

            // the kettle corner: a table, a ring, a kettle, mugs and a shelf over it
            TableTop(joinery, metal, new Vector3(5.5f, F, 4.05f), 1.4f, .72f, .78f);
            Pot(metal, 0, new Vector3(5.15f, F + .81f, 4.05f), .13f, .22f);
            metal.Box(0, new Vector3(5.15f, F + .8f, 4.05f), new Vector3(.34f, .04f, .34f), Quaternion.identity, 1f);
            GasBottle(metal, 0, new Vector3(6f, F, 4.45f), .52f);
            for (int k = 0; k < 6; k++)
                metal.Tube(0, new Vector3(5.6f + (k % 3) * .17f, F + .8f, 3.86f + (k / 3) * .18f),
                    new Vector3(5.6f + (k % 3) * .17f, F + .89f, 3.86f + (k / 3) * .18f), .04f, .042f, 8, 1f, 0, true);
            Shelves(joinery, new Vector3(5.5f, F + 1.25f, inF - .26f), 1.4f, .3f, 1, .34f, .2f);

            // the guides' notices, and the board of summit photographs by the door
            var pin = new MeshBuilder(1);
            pin.Box(0, new Vector3(inE - .03f, F + 1.7f, 2.9f), new Vector3(.04f, 1f, 1.5f), Quaternion.identity, 1f);
            pin.Box(0, new Vector3(-5.5f, F + 1.75f, inF - .03f), new Vector3(1.7f, 1.1f, .04f), Quaternion.identity, 1f);
            Part(t, "Pinboard", pin, PlankDark);
            for (int k = 0; k < 7; k++)                                                    // slips of paper, pinned crooked
                paper.Box(0, new Vector3(inE - .06f, F + 1.32f + (k % 3) * .38f, 2.42f + (k / 3) * .46f),
                    new Vector3(.01f, .26f, .34f), Quaternion.Euler(0, 0, (k * 13 % 11) - 5f), 1f);
            for (int k = 0; k < 8; k++)                                                    // photographs, the same
                paper.Box(0, new Vector3(-6.14f + (k % 4) * .44f, F + 1.5f + (k / 4) * .46f, inF - .06f),
                    new Vector3(.34f, .26f, .01f), Quaternion.Euler(0, 0, (k * 17 % 13) - 6f), 1f);
            Sign(t, "NoticeTitle", "ГИДЫ · ОБЪЯВЛЕНИЯ", new Vector3(inE - .07f, F + 2.28f, 2.9f), -90f, .016f,
                new Color(.9f, .89f, .84f), TextAlignment.Center);
            Sign(t, "PhotoTitle", "ВЗОШЛИ", new Vector3(-5.5f, F + 2.24f, inF - .07f), 180f, .018f,
                new Color(.9f, .89f, .84f), TextAlignment.Center);

            // the price of a bunk up there, which is a decision taken down here
            var bunkBoard = new MeshBuilder(1);
            bunkBoard.Box(0, new Vector3(inW + .03f, F + 1.5f, 1.1f), new Vector3(.05f, .95f, 1.7f), Quaternion.identity, 1f);
            Part(t, "BunkBoard", bunkBoard, Chalkboard);
            Sign(t, "LodgingBoard", "НОЧЁВКА НАВЕРХУ\n\n" + Lodging.BoardText(),
                new Vector3(inW + .07f, F + 1.5f, 1.1f), 90f, .019f, new Color(.92f, .93f, .89f));

            CeilingLamp(t, metal, glow, new Vector3(-3.5f, top - .1f, -2f), 9f, 2.2f, "BaseLight");
            CeilingLamp(t, metal, glow, new Vector3(3f, top - .1f, -1.4f), 0f, 0f, "BaseShade1");
            CeilingLamp(t, metal, glow, new Vector3(0f, top - .1f, 2.6f), 0f, 0f, "BaseShade2");

            Part(t, "Joinery", joinery, Plank);
            Part(t, "Steelwork", metal, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Rubberware", rubber, Rubber);
            Part(t, "Paperwork", paper, Paper);
            Part(t, "Glowing", glow, LampGlow);

            // ── the drift against the shaded wall. Azau is bare grass in July, and the importer checks that it is,
            // so the bank is built and switched off; a winter runtime turns the child on ───────────────────
            var drift = new GameObject("WinterDrift"); drift.transform.SetParent(t, false);
            var snow = new MeshBuilder(1);
            Surf.Lump(snow, 0, new Vector3(-2f, .1f, -hl - .5f), new Vector3(5f, .8f, 1.3f), 41, .3f);
            Surf.Lump(snow, 0, new Vector3(3.4f, .1f, -hl - .45f), new Vector3(3.4f, .65f, 1.1f), 42, .3f);
            Part(drift.transform, "Bank", snow, Snow);
            drift.SetActive(false);

            // ── colliders: the walls in pieces, so the doorway stays open ────────────────────────────────
            Solid(t, "FloorSolid", new Vector3(0, F * .5f, 0), new Vector3(W, F, L));
            Solid(t, "DeckSolid", new Vector3(0, F * .5f, hl + Porch * .5f), new Vector3(W - 2f, F, Porch));
            Solid(t, "WallFrontLeft", new Vector3((-hw + dL) * .5f, top * .5f, zf), new Vector3(dL + hw, top, T));
            Solid(t, "WallFrontRight", new Vector3((dR + hw) * .5f, top * .5f, zf), new Vector3(hw - dR, top, T));
            Solid(t, "WallFrontLintel", new Vector3(dx, (F + DH + top) * .5f, zf), new Vector3(DW, top - F - DH, T));
            Solid(t, "WallBack", new Vector3(0, top * .5f, zb), new Vector3(W, top, T));
            Solid(t, "WallWest", new Vector3(xw, top * .5f, 0), new Vector3(T, top, L));
            Solid(t, "WallEast", new Vector3(xe, top * .5f, 0), new Vector3(T, top, L));
            Solid(t, "CeilingSolid", new Vector3(0, top - .07f, 0), new Vector3(W, .14f, L));
            Solid(t, "CounterSolid", new Vector3(barX, F + .55f, barZ), new Vector3(barW, 1.1f, .9f));
            Solid(t, "RackSolid", new Vector3(3.2f, F + 1.1f, -4.55f), new Vector3(5.8f, 2.2f, .62f));
            Solid(t, "AxeRackSolid", new Vector3(inE - .42f, F + .9f, -3.8f), new Vector3(.76f, 1.8f, .9f));
            Solid(t, "DeskSolid", new Vector3(-4.1f, F + .4f, deskZ), new Vector3(4.2f, .8f, .9f));
            Solid(t, "StoveSolid", new Vector3(-.9f, F + .5f, -4.1f), new Vector3(.7f, 1f, .7f));
            Solid(t, "DrySolid", new Vector3(-5.1f, F + .86f, 2.5f), new Vector3(2.2f, 1.72f, .5f));
            Solid(t, "TeaSolid", new Vector3(5.5f, F + .4f, 4.05f), new Vector3(1.4f, .8f, .8f));
            Solid(t, "BenchSolidA", new Vector3(-4.2f, F + .25f, -.6f), new Vector3(4f, .5f, .5f));
            Solid(t, "BenchSolidB", new Vector3(-4.2f, F + .25f, .9f), new Vector3(4f, .5f, .5f));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "PorchRailW" : "PorchRailE", new Vector3(dx + i * 1.9f, F + .6f, hl + Porch * .5f), new Vector3(.16f, 1.2f, Porch - 1f));
            return Save(root);
        }

        // ── 2. the barrels of Гара-Баши, opened ───────────────────────────────────────────────────────────
        /// <summary>«Бочка» — a wagon barrel of the Гара-Баши camp at 3 710 m, and the base camp of this mountain since
        /// the 1980s. A six-metre cylinder on a timber cradle with the door in the downhill end, and now with the
        /// inside it always had: two tiers of bunks down the wall, <see cref="Lodging"/>'s six places in three stacks,
        /// a little table under the window in the far end, the stove whose pipe already went out through the roof, the
        /// gas kept by the door and everything anybody owns hung on a line over the aisle. The diesel outside is
        /// <see cref="Genset"/>; the drums, the crates and the boards between the barrels are already there
        /// (<c>ElbrusDressing.Camp</c>), and each barrel brings its own guys and its own wand.
        ///
        /// The outside is the one <see cref="ElbrusFactory"/> built, to the millimetre — the camp rows, the footprint
        /// table and the rescuers' wagons of the moraine all place this prefab by name and none of them changes.</summary>
        static GameObject Barrel()
        {
            const float R = 1.35f, Len = 6f, CY = R + .55f, FY = 1f;     // shell radius, length, axis height, floor
            float hz = Len * .5f;
            var root = new GameObject("Elb_Barrel");
            var t = root.transform;

            // ── the shell, inside and out, and the two ends ──────────────────────────────────────────────
            // skinned both ways in the same painted metal: inside a бочка is the same sheet as outside, and a lining
            // would put plywood in front of the two side lights
            var skin = new MeshBuilder(1);
            Cylinder(skin, 0, CY, -hz, hz, R, 18, false, .5f);
            Cylinder(skin, 0, CY, -hz, hz, R - .02f, 18, true, .7f);
            DiscWall(skin, 0, hz, CY, R, 12, -.44f, .44f, FY, 2.9f);                   // the door end
            DiscWall(skin, 0, -hz, CY, R, 12, -.4f, .4f, CY + .1f, CY + .8f);          // the window end
            Part(t, "Shell", skin, Materials.Get("ElbLBarrel", new Color(.86f, .87f, .88f), smoothness: .35f));

            var pane = new MeshBuilder(1);
            PaneZ(pane, 0, -hz + .02f, -.4f, .4f, CY + .1f, CY + .8f);
            for (int i = -1; i <= 1; i += 2)                                           // the two side lights it always had
                PaneX(pane, 0, i * (R - .04f), -1.4f, -.35f, CY - .35f, CY + .45f);
            var glazing = Part(t, "Windows", pane, GlassClear);
            glazing.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ── the outside: the hoops, the cradle, the pipe, the guys and the wand ──────────────────────
            var trim = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
            {
                float z = -hz + .8f + k * 1.45f;
                trim.Tube(0, new Vector3(0, CY, z - .04f), new Vector3(0, CY, z + .04f), R + .03f, R + .03f, 18, .5f);
            }
            trim.Box(0, new Vector3(0, .35f, 0), new Vector3(2.4f, .3f, Len + .3f), Quaternion.identity, .6f);
            for (int k = -1; k <= 1; k += 2)
                for (int j = 0; j < 3; j++)
                    trim.Box(0, new Vector3(k * 1f, .1f, -2f + j * 2f), new Vector3(.35f, .5f, .5f), Quaternion.identity, .6f);
            trim.Tube(0, new Vector3(.7f, R * 2 + .5f, -1.4f), new Vector3(.7f, R * 2 + 1.5f, -1.4f), .07f, .065f, 8, .5f, 0, true);
            trim.Box(0, new Vector3(.7f, R * 2 + 1.55f, -1.4f), new Vector3(.2f, .05f, .2f), Quaternion.identity, 1f);
            for (int g = 0; g < 4; g++)                                                // four guys down to pegs in the ice
            {
                float ang = 40f + g * 90f;
                var dir = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var a = new Vector3(dir.x * R * .8f, CY + R * .6f, dir.z * (hz - .8f));
                var peg = new Vector3(dir.x * 2.7f, .06f, dir.z * (hz + 1.1f));
                trim.Tube(0, a, peg, .008f, .008f, 4, 1f, 0, false);
                trim.Tube(0, peg + Vector3.up * .1f, peg - Vector3.up * .35f, .016f, .014f, 5, 1f, 0, true);
            }
            Part(t, "Trim", trim, Steel);

            var wand = new MeshBuilder(2);                                             // 0 bamboo, 1 rag
            wand.Tube(0, new Vector3(-1.5f, -.1f, hz - .4f), new Vector3(-1.56f, 1.5f, hz - .35f), .011f, .009f, 5, 1f, 0, true);
            wand.Box(1, new Vector3(-1.62f, 1.38f, hz - .35f), new Vector3(.18f, .12f, .02f), Quaternion.Euler(0, 14f, 0), 1f);
            Part(t, "Wand", wand, Materials.Get("ElbLBamboo", new Color(.78f, .71f, .45f), smoothness: .25f), Red);

            var door = new MeshBuilder(1);                                             // the leaf, swung back on the shell
            door.Box(0, new Vector3(.86f, (FY + 2.9f) * .5f, hz + .38f), new Vector3(.9f, 1.86f, .06f), Quaternion.Euler(0, 58f, 0), 1f);
            door.Tube(0, new Vector3(1.12f, 1.9f, hz + .17f), new Vector3(1.22f, 1.9f, hz + .24f), .03f, .03f, 6, 1f, 0, true);
            Part(t, "Door", door, Materials.Get("ElbLDoor", new Color(.34f, .44f, .54f), smoothness: .3f));

            var steps = new MeshBuilder(1);
            for (int k = 0; k < 5; k++)
            {
                var c = new Vector3(0, FY - .2f - k * .2f, hz + .35f + k * .4f);
                var s = new Vector3(1.2f, .4f, .42f);
                steps.Box(0, c, s, Quaternion.identity, .8f);
                Solid(t, "StepSolid" + k, c, s);
            }
            Part(t, "Steps", steps, Materials.Planks);

            // ── inside ──────────────────────────────────────────────────────────────────────────────────
            var wood = new MeshBuilder(1);
            var steel = new MeshBuilder(1);
            var cloth = new MeshBuilder(3);
            var rubber = new MeshBuilder(1);
            var glow = new MeshBuilder(1);

            wood.Box(0, new Vector3(0, FY - .05f, 0), new Vector3(1.95f, .1f, Len - .2f), Quaternion.identity, .9f);
            // three stacks of two: four places down the west wall, two down the east — Lodging's six
            Bunk(wood, steel, cloth, new Vector3(-.66f, FY, -1.75f), 1.85f, .62f, 2, 1, 3710);
            Bunk(wood, steel, cloth, new Vector3(-.66f, FY, 1.55f), 1.85f, .62f, 2, 1, 3711);
            Bunk(wood, steel, cloth, new Vector3(.66f, FY, 1.55f), 1.85f, .62f, 2, -1, 3712);
            // the little table under the window in the far end, the stove, the gas and the shelf over it
            TableTop(wood, steel, new Vector3(.62f, FY, -2.35f), .56f, .78f, .66f);
            StoveSmall(steel, glow, new Vector3(.7f, FY, -1.4f), R * 2 + 1.5f, -1);
            GasBottle(steel, 0, new Vector3(.74f, FY, -.35f), .5f);
            GasBottle(steel, 0, new Vector3(.74f, FY, -.02f), .5f);
            Pot(steel, 0, new Vector3(.7f, FY + .95f, -1.4f), .12f, .18f);
            Shelves(wood, new Vector3(.55f, FY + 1.35f, -2.45f), .8f, .26f, 1, .28f, .1f);
            ClothLine(steel, cloth, new Vector3(-.15f, FY + 1.96f, -2.5f), new Vector3(-.15f, FY + 1.96f, 2.4f), 6, 3713);
            for (int k = 0; k < 4; k++)                                                // boots, kicked off by the door
                rubber.Box(0, new Vector3(-.2f + k * .22f, FY + .08f, 2.35f + (k % 2) * .18f), new Vector3(.13f, .16f, .31f),
                    Quaternion.Euler(0, k * 23f - 30f, 0), 1f);
            CeilingLamp(t, steel, glow, new Vector3(0, CY + R - .18f, .2f), 5f, 1.9f, "BarrelLight");

            Part(t, "Joinery", wood, Plank);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Rubberware", rubber, Rubber);
            Part(t, "Glowing", glow, LampGlow);
            Marker(t, "Elb_Bunk", new Vector3(0, FY, -1.6f), -90f);

            // ── colliders: the shell in pieces, so the door stays open ──────────────────────────────────
            Solid(t, "FloorSolid", new Vector3(0, FY - .05f, 0), new Vector3(1.95f, .12f, Len));
            Solid(t, "Cradle", new Vector3(0, .35f, 0), new Vector3(2.4f, .7f, Len + .3f));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "WallWest" : "WallEast", new Vector3(i * 1.22f, 1.85f, 0), new Vector3(.3f, 1.8f, Len));
            Solid(t, "CeilingSolid", new Vector3(0, 3.1f, 0), new Vector3(2.6f, .4f, Len));
            Solid(t, "EndFar", new Vector3(0, CY, -hz - .06f), new Vector3(2.7f, 2.6f, .22f));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "EndDoorWest" : "EndDoorEast", new Vector3(i * .9f, CY, hz + .06f), new Vector3(.92f, 2.6f, .22f));
            Solid(t, "EndDoorLintel", new Vector3(0, 3.05f, hz + .06f), new Vector3(.92f, .4f, .22f));
            for (int k = 0; k < 3; k++)
                Solid(t, "BunkSolid" + k, new Vector3(k == 2 ? .66f : -.66f, FY + .9f, k == 0 ? -1.75f : 1.55f), new Vector3(.66f, 1.8f, 1.9f));
            Solid(t, "StoveSolid", new Vector3(.7f, FY + .5f, -1.4f), new Vector3(.68f, 1f, .68f));
            return Save(root);
        }

        // ── 3. the small приют, opened ────────────────────────────────────────────────────────────────────
        /// <summary>A hut of the moraine — «Приют 88», RedFox, «Мария», «Орлиное гнездо»: a boarded box with a metal
        /// roof and one room of four in it, which is what a приют on this mountain actually is. Two bunks of two tiers
        /// down the side walls with a 1.8 m aisle between them, a stove with its pipe out through the roof, a gas ring
        /// and a pot in the corner, a drying frame with the boots under it, a shelf, a line over the aisle, and a
        /// window each side so there is daylight to pack by.
        ///
        /// The outside is ElbrusFactory's, unchanged: 3.4 × 4.6 × 2.6 m under a 3.9 × 5.0 m roof, door in the +Z
        /// end.</summary>
        static GameObject SmallHut()
        {
            const float W = 3.4f, L = 4.6f, H = 2.6f, T = .12f, F = .18f;
            float hw = W * .5f, hl = L * .5f, inW = -hw + T, inE = hw - T, inF = hl - T, inB = -hl + T;
            const float DW = .9f, DH = 1.95f;
            float wy0 = F + 1.02f, wy1 = F + 1.72f;

            var root = new GameObject("Elb_Hut_Small");
            var t = root.transform;

            var floor = new MeshBuilder(1);
            floor.Box(0, new Vector3(0, F * .5f, 0), new Vector3(W, F, L), Quaternion.identity, .7f);
            floor.Box(0, new Vector3(0, F * .5f, hl + .35f), new Vector3(1.6f, F, .7f), Quaternion.identity, .7f);   // the landing
            Part(t, "Floor", floor, Materials.Planks);

            var walls = new MeshBuilder(1);
            Panel(walls, 0, true, hl - T * .5f, T, -hw, hw, 0f, H, -DW * .5f, DW * .5f, F, F + DH);
            Panel(walls, 0, true, -hl + T * .5f, T, -hw, hw, 0f, H, 0f, 0f, 0f, 0f);
            Panel(walls, 0, false, -hw + T * .5f, T, -hl, hl, 0f, H, .2f, 1.3f, wy0, wy1);
            Panel(walls, 0, false, hw - T * .5f, T, -hl, hl, 0f, H, .2f, 1.3f, wy0, wy1);
            Part(t, "Walls", walls, Materials.Get("ElbLHutSmall", new Color(.78f, .4f, .22f), smoothness: .35f));

            var pane = new MeshBuilder(1);
            PaneX(pane, 0, inW - .01f, .2f, 1.3f, wy0, wy1);
            PaneX(pane, 0, inE + .01f, .2f, 1.3f, wy0, wy1);
            var glazing = Part(t, "Windows", pane, GlassClear);
            glazing.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var roof = new MeshBuilder(1);
            roof.Box(0, new Vector3(0, H + .12f, 0), new Vector3(W + .5f, .2f, L + .4f), Quaternion.identity, .7f);
            Part(t, "Roof", roof, Steel);

            var ceiling = new MeshBuilder(1);
            ceiling.Box(0, new Vector3(0, H - .06f, 0), new Vector3(W - 2f * T, .12f, L - 2f * T), Quaternion.identity, .8f);
            Part(t, "Ceiling", ceiling, Ply);

            var trim = new MeshBuilder(1);
            trim.Box(0, new Vector3(0, F + DH + .06f, hl - T * .5f), new Vector3(DW + .2f, .12f, T + .05f), Quaternion.identity, 1f);
            for (int i = -1; i <= 1; i += 2)
                trim.Box(0, new Vector3(i * (DW * .5f + .06f), F + DH * .5f, hl - T * .5f), new Vector3(.12f, DH, T + .05f), Quaternion.identity, 1f);
            trim.Box(0, new Vector3(.86f, F + DH * .5f, hl + .26f), new Vector3(.88f, DH - .06f, .05f), Quaternion.Euler(0, 55f, 0), 1f);
            var step = new Vector3(0, F * .5f - .1f, hl + .82f);
            trim.Box(0, step, new Vector3(1.4f, .5f, .5f), Quaternion.identity, .8f);
            Solid(t, "StepSolid", step, new Vector3(1.4f, .5f, .5f));
            Part(t, "Trim", trim, Materials.Planks);

            var wood = new MeshBuilder(1);
            var steel = new MeshBuilder(1);
            var cloth = new MeshBuilder(3);
            var rubber = new MeshBuilder(1);
            var glow = new MeshBuilder(1);

            Bunk(wood, steel, cloth, new Vector3(inW + .34f, F, -1.18f), 1.85f, .68f, 2, 1, 88);
            Bunk(wood, steel, cloth, new Vector3(inE - .34f, F, -1.18f), 1.85f, .68f, 2, -1, 89);
            StoveSmall(steel, glow, new Vector3(inW + .42f, F, 1.35f), H + .5f, 1);
            TableTop(wood, steel, new Vector3(inE - .42f, F, 1.35f), .6f, .86f, .72f);
            Pot(steel, 0, new Vector3(inE - .42f, F + .74f, 1.2f), .13f, .2f);
            GasBottle(steel, 0, new Vector3(inE - .4f, F, 1.95f), .5f);
            Shelves(wood, new Vector3(inE - .24f, F + 1.26f, 1.35f), .8f, .26f, 1, .3f, .12f, true);
            // the сушилка of a small hut is two rails over the stove, and the boots stand along the wall under them
            for (int k = 0; k < 2; k++)
                steel.Tube(0, new Vector3(inW + .08f, F + 1.26f + k * .3f, .78f), new Vector3(inW + .08f, F + 1.26f + k * .3f, 1.92f), .02f, .02f, 5, 1f);
            for (int k = 0; k < 4; k++)
                steel.Tube(0, new Vector3(inW + .02f, F + 1.2f + (k % 2) * .3f, .82f + (k / 2) * 1.04f),
                    new Vector3(inW + .2f, F + 1.32f + (k % 2) * .3f, .82f + (k / 2) * 1.04f), .018f, .018f, 4, 1f);
            for (int k = 0; k < 3; k++)
                cloth.Box(2, new Vector3(inW + .24f, F + .94f, .9f + k * .42f), new Vector3(.16f, .5f, .34f),
                    Quaternion.Euler(0, 0, k * 4f - 4f), 1f);
            for (int k = 0; k < 4; k++)
                rubber.Box(0, new Vector3(inW + .16f + (k % 2) * .2f, F + .08f, .75f + (k / 2) * .38f), new Vector3(.13f, .16f, .31f),
                    Quaternion.Euler(0, k * 17f - 24f, 0), 1f);
            ClothLine(steel, cloth, new Vector3(0, H - .34f, -1.9f), new Vector3(0, H - .34f, .3f), 3, 92);
            CeilingLamp(t, steel, glow, new Vector3(0, H - .16f, -.4f), 4.2f, 1.8f, "HutLight");

            Part(t, "Joinery", wood, Plank);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Rubberware", rubber, Rubber);
            Part(t, "Glowing", glow, LampGlow);
            Marker(t, "Elb_Bunk", new Vector3(0, F, -1.1f), -90f);

            Solid(t, "FloorSolid", new Vector3(0, F * .5f, 0), new Vector3(W, F, L));
            Solid(t, "LandingSolid", new Vector3(0, F * .5f, hl + .35f), new Vector3(1.6f, F, .7f));
            Solid(t, "WallBack", new Vector3(0, H * .5f, -hl + T * .5f), new Vector3(W, H, T));
            Solid(t, "WallWest", new Vector3(-hw + T * .5f, H * .5f, 0), new Vector3(T, H, L));
            Solid(t, "WallEast", new Vector3(hw - T * .5f, H * .5f, 0), new Vector3(T, H, L));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "WallFrontWest" : "WallFrontEast",
                    new Vector3(i * (hw + DW * .5f) * .5f, H * .5f, hl - T * .5f), new Vector3(hw - DW * .5f, H, T));
            Solid(t, "WallFrontLintel", new Vector3(0, (F + DH + H) * .5f, hl - T * .5f), new Vector3(DW, H - F - DH, T));
            Solid(t, "CeilingSolid", new Vector3(0, H - .06f, 0), new Vector3(W, .12f, L));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "BunkWest" : "BunkEast", new Vector3(i * (hw - T - .34f), F + .9f, -1.18f), new Vector3(.7f, 1.8f, 1.9f));
            Solid(t, "StoveSolid", new Vector3(-hw + T + .42f, F + .5f, 1.35f), new Vector3(.68f, 1f, .68f));
            Solid(t, "TableSolid", new Vector3(hw - T - .42f, F + .4f, 1.35f), new Vector3(.64f, .8f, .9f));
            return Save(root);
        }

        // ── 4. приют «Нацпарк» ────────────────────────────────────────────────────────────────────────────
        /// <summary>Приют «Нацпарк», ≈3 900 m — the modern one: a clad box on a frame with a corridor down the middle,
        /// rooms of four off it, heating and, the thing everybody mentions, sockets. A drying room inside the door, a
        /// kitchen and a long table at the far end, and a diesel convector that runs all night
        /// (<see cref="Lodging"/> prices it at 1 500 ₽ and gives it a сушилка, which is why it has one).
        /// Pivot = the ground under the middle, +Z = the door.</summary>
        static GameObject Natspark()
        {
            const float W = 8f, L = 13f, H = 2.6f, T = .16f, F = .25f;
            float hw = W * .5f, hl = L * .5f, inW = -hw + T, inE = hw - T, inF = hl - T;
            const float DW = 1.2f, DH = 2.05f, dx = -1.7f;
            float wy0 = F + 1.05f, wy1 = F + 1.95f, top = F + H;

            var root = new GameObject("Elb_Hut_Natspark");
            var t = root.transform;

            var floor = new MeshBuilder(1);
            floor.Box(0, new Vector3(0, F * .5f, 0), new Vector3(W, F, L), Quaternion.identity, .6f);
            floor.Box(0, new Vector3(dx, F * .5f, hl + .6f), new Vector3(2.6f, F, 1.2f), Quaternion.identity, .6f);
            Part(t, "Floor", floor, Materials.Planks);

            var walls = new MeshBuilder(1);
            Panel(walls, 0, true, hl - T * .5f, T, -hw, .4f, 0f, top, dx - DW * .5f, dx + DW * .5f, F, F + DH);
            Panel(walls, 0, true, hl - T * .5f, T, .4f, hw, 0f, top, 1.2f, 2.8f, wy0, wy1);
            Panel(walls, 0, true, -hl + T * .5f, T, -hw, hw, 0f, top, -1.4f, .6f, wy0, wy1);
            Panel(walls, 0, false, -hw + T * .5f, T, -hl, -.4f, 0f, top, -4.6f, -3f, wy0, wy1);
            Panel(walls, 0, false, -hw + T * .5f, T, -.4f, hl, 0f, top, 2.4f, 4f, wy0, wy1);
            Panel(walls, 0, false, hw - T * .5f, T, -hl, -.4f, 0f, top, -4.6f, -3f, wy0, wy1);
            Panel(walls, 0, false, hw - T * .5f, T, -.4f, hl, 0f, top, 2.4f, 4f, wy0, wy1);
            Part(t, "Walls", walls, Sheet);

            var glass = new MeshBuilder(1);
            PaneZ(glass, 0, hl - T * .5f, 1.2f, 2.8f, wy0, wy1);
            PaneZ(glass, 0, -hl + T * .5f, -1.4f, .6f, wy0, wy1);
            foreach (float x in new[] { inW - .01f, inE + .01f })
            {
                PaneX(glass, 0, x, -4.6f, -3f, wy0, wy1);
                PaneX(glass, 0, x, 2.4f, 4f, wy0, wy1);
            }
            var pane = Part(t, "Glazing", glass, GlassClear);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var roof = new MeshBuilder(1);
            Gable(roof, 0, new Vector3(0, top, 0), W, L, 1.2f, .5f);
            GableEnd(roof, 0, hl + .5f, 1f, hw + .5f, top, 1.2f);
            GableEnd(roof, 0, -hl - .5f, -1f, hw + .5f, top, 1.2f);
            Part(t, "Roof", roof, SheetGreen);

            var ceiling = new MeshBuilder(1);
            ceiling.Box(0, new Vector3(0, top - .06f, 0), new Vector3(W - 2f * T, .12f, L - 2f * T), Quaternion.identity, .7f);
            Part(t, "Ceiling", ceiling, Ply);

            // ── the corridor and the four rooms off it ──────────────────────────────────────────────────
            var part = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
            {
                float x = i * 1.1f;
                Panel(part, 0, false, x, .1f, -1.5f, 1.5f, F, top, -.5f, .5f, F, F + 2f);        // the near room's door
                Panel(part, 0, false, x, .1f, 1.5f, 4.5f, F, top, 2.4f, 3.4f, F, F + 2f);        // the far room's door
            }
            Panel(part, 0, true, 1.5f, .1f, inW, -1.1f, F, top, 0f, 0f, 0f, 0f);                 // between the two west rooms
            Panel(part, 0, true, 1.5f, .1f, 1.1f, inE, F, top, 0f, 0f, 0f, 0f);
            Panel(part, 0, true, 4.55f, .1f, inW, inE, F, top, -1.1f, 1.1f, F, F + 2.05f);   // the mouth of the corridor
            Panel(part, 0, true, -1.55f, .1f, inW, -1.1f, F, top, 0f, 0f, 0f, 0f);
            Panel(part, 0, true, -1.55f, .1f, 1.1f, inE, F, top, 0f, 0f, 0f, 0f);
            Part(t, "Partitions", part, Ply);

            var wood = new MeshBuilder(1);
            var steel = new MeshBuilder(1);
            var cloth = new MeshBuilder(3);
            var rubber = new MeshBuilder(1);
            var glow = new MeshBuilder(1);
            var white = new MeshBuilder(1);

            int seed = 3900;
            foreach (int side in new[] { -1, 1 })
                foreach (float rz in new[] { 3.0f, -.05f })
                {
                    float outer = side < 0 ? inW + .42f : inE - .42f;
                    float inner = side < 0 ? -1.1f - .52f : 1.1f + .52f;
                    Bunk(wood, steel, cloth, new Vector3(outer, F, rz), 2f, .84f, 2, -side, seed++);
                    Bunk(wood, steel, cloth, new Vector3(inner, F, rz), 2f, .84f, 2, side, seed++);
                    // a shelf between them, and the socket block everybody comes for
                    Shelves(wood, new Vector3(side * 2.2f, F + .9f, rz + 1.35f), 1.2f, .26f, 1, .3f, .12f);
                    white.Box(0, new Vector3(side < 0 ? inW + .05f : inE - .05f, F + .38f, rz - 1.1f), new Vector3(.06f, .16f, .22f), Quaternion.identity, 1f);
                    white.Box(0, new Vector3(side < 0 ? inW + .05f : inE - .05f, F + .38f, rz - .82f), new Vector3(.06f, .16f, .22f), Quaternion.identity, 1f);
                    Solid(t, $"BunkSolid{seed}", new Vector3(outer, F + .9f, rz), new Vector3(.86f, 1.8f, 2.05f));
                    Solid(t, $"BunkSolidIn{seed}", new Vector3(inner, F + .9f, rz), new Vector3(.86f, 1.8f, 2.05f));
                }

            // the drying room, inside the door where wet things are taken off
            DryRack(steel, cloth, rubber, new Vector3(1.6f, F, 5.3f), 2.4f, 61);
            BenchSeat(wood, new Vector3(-2.6f, F, 5.3f), 2.2f, .44f, .45f, true);
            for (int k = 0; k < 6; k++)
                rubber.Box(0, new Vector3(-3.4f + k * .32f, F + .08f, 4.85f), new Vector3(.13f, .16f, .31f),
                    Quaternion.Euler(0, k * 11f - 30f, 0), 1f);

            // the kitchen and the long table at the far end
            wood.Box(0, new Vector3(inW + .32f, F + .88f, -3.6f), new Vector3(.64f, .06f, 3.2f), Quaternion.identity, .9f);
            wood.Box(0, new Vector3(inW + .32f, F + .44f, -3.6f), new Vector3(.6f, .84f, 3.1f), Quaternion.identity, .9f);
            for (int k = 0; k < 3; k++)
                Pot(steel, 0, new Vector3(inW + .3f, F + .92f, -4.6f + k * 1f), .15f, .2f);
            for (int k = 0; k < 2; k++)
                steel.Box(0, new Vector3(inW + .3f, F + .9f, -4.6f + k * 1f), new Vector3(.4f, .05f, .4f), Quaternion.identity, 1f);
            GasBottle(steel, 0, new Vector3(inW + .34f, F, -1.75f), .55f);
            Shelves(wood, new Vector3(inW + .2f, F + 1.32f, -3.6f), 3f, .26f, 1, .32f, .12f, true);
            for (int k = 0; k < 10; k++)
                steel.Tube(0, new Vector3(inW + .2f, F + 1.46f, -4.9f + k * .3f), new Vector3(inW + .2f, F + 1.55f, -4.9f + k * .3f), .04f, .042f, 8, 1f, 0, true);
            TableTop(wood, steel, new Vector3(.9f, F, -3.7f), 1.1f, 3.4f, .74f);
            for (int i = -1; i <= 1; i += 2)
                BenchSeat(wood, new Vector3(.9f + i * .95f, F, -3.7f), .4f, 3.2f, .44f, false);
            // the diesel convector the приют is warm by, and its flue
            steel.Box(0, new Vector3(inE - .3f, F + .42f, -5.3f), new Vector3(.5f, .84f, 1.1f), Quaternion.identity, 1f);
            steel.Tube(0, new Vector3(inE - .3f, F + .84f, -5.3f), new Vector3(inE - .3f, top + 1.3f, -5.3f), .07f, .065f, 8, 1f, 0, true);
            glow.Box(0, new Vector3(inE - .56f, F + .5f, -5.3f), new Vector3(.02f, .26f, .5f), Quaternion.identity, 1f);
            for (int k = 0; k < 3; k++)
                white.Box(0, new Vector3(inE - .05f, F + .38f, -4.2f + k * .3f), new Vector3(.06f, .16f, .22f), Quaternion.identity, 1f);

            CeilingLamp(t, steel, glow, new Vector3(0, top - .1f, -3.6f), 7f, 2f, "HutLight");
            CeilingLamp(t, steel, glow, new Vector3(0, top - .1f, 1.4f), 0f, 0f, "HutShade1");
            CeilingLamp(t, steel, glow, new Vector3(0, top - .1f, 5f), 0f, 0f, "HutShade2");

            var price = new MeshBuilder(1);
            price.Box(0, new Vector3(-3.3f, F + 1.75f, inF - .03f), new Vector3(1.3f, .8f, .05f), Quaternion.identity, 1f);
            Part(t, "PriceCard", price, Paper);
            var bunkRow = Lodging.Get("natspark");
            Sign(t, "PriceText", "ПРИЮТ «НАЦПАРК»\n" + bunkRow.Line + "\nКомнаты по 4 · сушилка · кухня · розетки",
                new Vector3(-3.3f, F + 1.75f, inF - .07f), 180f, .014f, new Color(.22f, .21f, .2f), TextAlignment.Center);

            Part(t, "Joinery", wood, Plank);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Rubberware", rubber, Rubber);
            Part(t, "Fittings", white, White);
            Part(t, "Glowing", glow, LampGlow);
            Marker(t, "Elb_Bunk", new Vector3(0, F, 1.4f), -90f);

            var steps = new MeshBuilder(1);
            for (int k = 0; k < 4; k++)
            {
                var c = new Vector3(dx, F * .5f - .12f - k * .19f, hl + 1.35f + k * .4f);
                var sz = new Vector3(2.2f, .6f, .4f);
                steps.Box(0, c, sz, Quaternion.identity, .7f);
                Solid(t, "StepSolid" + k, c, sz);
            }
            Part(t, "Steps", steps, Concrete);

            Solid(t, "FloorSolid", new Vector3(0, F * .5f, 0), new Vector3(W, F, L));
            Solid(t, "PorchSolid", new Vector3(dx, F * .5f, hl + .6f), new Vector3(2.6f, F, 1.2f));
            Solid(t, "WallBack", new Vector3(0, top * .5f, -hl + T * .5f), new Vector3(W, top, T));
            Solid(t, "WallWest", new Vector3(-hw + T * .5f, top * .5f, 0), new Vector3(T, top, L));
            Solid(t, "WallEast", new Vector3(hw - T * .5f, top * .5f, 0), new Vector3(T, top, L));
            Solid(t, "WallFrontWest", new Vector3((-hw + dx - DW * .5f) * .5f, top * .5f, hl - T * .5f), new Vector3(dx - DW * .5f + hw, top, T));
            Solid(t, "WallFrontEast", new Vector3((dx + DW * .5f + hw) * .5f, top * .5f, hl - T * .5f), new Vector3(hw - dx - DW * .5f, top, T));
            Solid(t, "WallFrontLintel", new Vector3(dx, (F + DH + top) * .5f, hl - T * .5f), new Vector3(DW, top - F - DH, T));
            Solid(t, "CeilingSolid", new Vector3(0, top - .06f, 0), new Vector3(W, .12f, L));
            for (int i = -1; i <= 1; i += 2)
            {
                Solid(t, i < 0 ? "CorridorW0" : "CorridorE0", new Vector3(i * 1.1f, (F + top) * .5f, -1f), new Vector3(.12f, top - F, 1f));
                Solid(t, i < 0 ? "CorridorW1" : "CorridorE1", new Vector3(i * 1.1f, (F + top) * .5f, 1.45f), new Vector3(.12f, top - F, 1.9f));
                Solid(t, i < 0 ? "CorridorW2" : "CorridorE2", new Vector3(i * 1.1f, (F + top) * .5f, 3.95f), new Vector3(.12f, top - F, 1.1f));
                Solid(t, i < 0 ? "CrossW" : "CrossE", new Vector3(i * 2.47f, (F + top) * .5f, 1.5f), new Vector3(2.74f, top - F, .12f));
                Solid(t, i < 0 ? "BackW" : "BackE", new Vector3(i * 2.47f, (F + top) * .5f, -1.55f), new Vector3(2.74f, top - F, .12f));
                Solid(t, i < 0 ? "HallW" : "HallE", new Vector3(i * 2.47f, (F + top) * .5f, 4.55f), new Vector3(2.74f, top - F, .12f));
            }
            Solid(t, "HallLintel", new Vector3(0, (F + 2.05f + top) * .5f, 4.55f), new Vector3(2.2f, top - F - 2.05f, .12f));
            Solid(t, "KitchenSolid", new Vector3(inW + .32f, F + .45f, -3.6f), new Vector3(.66f, .9f, 3.2f));
            Solid(t, "TableSolid", new Vector3(.9f, F + .4f, -3.7f), new Vector3(1.1f, .8f, 3.4f));
            Solid(t, "HeaterSolid", new Vector3(inE - .3f, F + .45f, -5.3f), new Vector3(.55f, .9f, 1.15f));
            Solid(t, "DrySolid", new Vector3(1.6f, F + .86f, 5.3f), new Vector3(2.4f, 1.72f, .5f));
            return Save(root);
        }

        // ── 5. LeapRus, 3 912 m ───────────────────────────────────────────────────────────────────────────
        /// <summary>LeapRus — the capsule bivouac: three streamlined modules on legs, the downhill end all glass. This
        /// one is opened at the uphill end, where the door is, and inside it is exactly what a capsule hotel is: six
        /// berths tucked under the curve of the shell, three a side, each with its own board between it and the next
        /// and its own little light, a 2.4 m aisle down the middle, and at the glazed end a table and two seats where
        /// people sit and look down the Baksan until it gets dark.
        ///
        /// The pod, the legs and the deck are ElbrusFactory's, unchanged; only the uphill end is now a wall with a
        /// door in it instead of a second sheet of glass.</summary>
        static GameObject Capsule()
        {
            const float R = 2.2f, Len = 12f, Deck = .9f, FY = .96f;
            float hz = Len * .5f;
            var root = new GameObject("Elb_Hut_Capsule");
            var t = root.transform;

            // the shell: a half cylinder, skinned outside and lined inside
            var pod = new MeshBuilder(1);
            var lining = new MeshBuilder(1);
            const int Seg = 16;
            for (int k = 0; k < Seg; k++)
            {
                float a0 = Mathf.PI * k / Seg, a1 = Mathf.PI * (k + 1) / Seg;
                Vector3 P(float a, float z, float r) => new Vector3(-Mathf.Cos(a) * r, Mathf.Sin(a) * r + Deck, z);
                pod.Quad(0, P(a0, -hz, R), P(a0, hz, R), P(a1, hz, R), P(a1, -hz, R),
                    Vector2.zero, new Vector2(Len * .3f, 0), new Vector2(Len * .3f, .3f), new Vector2(0, .3f));
                lining.Quad(0, P(a0, -hz + .05f, R - .06f), P(a1, -hz + .05f, R - .06f), P(a1, hz - .05f, R - .06f), P(a0, hz - .05f, R - .06f),
                    Vector2.zero, new Vector2(.3f, 0), new Vector2(.3f, Len * .3f), new Vector2(0, Len * .3f));
            }
            Part(t, "Pod", pod, Materials.Get("ElbLCapsule", new Color(.93f, .94f, .96f), smoothness: .45f));
            Part(t, "Lining", lining, Foam);

            var endWall = new MeshBuilder(1);
            DiscWall(endWall, 0, -hz, Deck, R, 14, -.45f, .45f, FY, 2.5f, FY - .02f);
            Part(t, "EndUphill", endWall, Materials.Get("ElbLCapsule", new Color(.93f, .94f, .96f), smoothness: .45f));

            var view = new MeshBuilder(1);
            DiscWall(view, 0, hz, Deck, R, 14, 0f, 0f, 0f, 0f, FY - .02f);
            var glazed = Part(t, "EndGlazed", view, GlassClear);
            glazed.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var frame = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = 0; k < 4; k++)
                    frame.Tube(0, new Vector3(i * (R - .4f), 0, -hz + 1.2f + k * 3.2f), new Vector3(i * (R - .4f), .95f, -hz + 1.2f + k * 3.2f), .12f, .12f, 8, .5f, 0, true);
            frame.Box(0, new Vector3(0, .85f, 0), new Vector3(R * 2 - .4f, .18f, Len), Quaternion.identity, .5f);
            for (int k = 0; k < 4; k++)                                     // the steps up to the door in the uphill end
            {
                var c = new Vector3(0, FY - .22f - k * .22f, -hz - .4f - k * .42f);
                var sz = new Vector3(1.3f, .5f, .44f);
                frame.Box(0, c, sz, Quaternion.identity, .7f);
                Solid(t, "StepSolid" + k, c, sz);
            }
            Part(t, "Frame", frame, Steel);

            var wood = new MeshBuilder(1);
            var steel = new MeshBuilder(1);
            var cloth = new MeshBuilder(3);
            var glow = new MeshBuilder(1);

            wood.Box(0, new Vector3(0, FY - .04f, 0), new Vector3(R * 2 - .5f, .08f, Len - .3f), Quaternion.identity, .8f);
            int seed = 3912;
            foreach (int side in new[] { -1, 1 })
                foreach (float bz in new[] { -4.2f, -1.9f, .4f })
                {
                    Bunk(wood, steel, cloth, new Vector3(side * 1.65f, FY, bz), 2f, .85f, 1, -side, seed++);
                    wood.Box(0, new Vector3(side * 1.65f, FY + .3f, bz + 1.1f), new Vector3(.85f, .6f, .05f), Quaternion.identity, .8f);
                    glow.Box(0, new Vector3(side * 1.55f, FY + .78f, bz - .7f), new Vector3(.09f, .05f, .16f), Quaternion.identity, 1f);
                }
            // the end everybody sits in
            TableTop(wood, steel, new Vector3(0, FY, 4.1f), .8f, 1.1f, .68f);
            for (int i = -1; i <= 1; i += 2)
                BenchSeat(wood, new Vector3(i * .95f, FY, 4.1f), .5f, 1f, .45f, false);
            steel.Box(0, new Vector3(-1.5f, FY + .3f, 2.2f), new Vector3(.24f, .6f, .9f), Quaternion.identity, 1f);      // the heater
            glow.Box(0, new Vector3(-1.37f, FY + .3f, 2.2f), new Vector3(.02f, .34f, .6f), Quaternion.identity, 1f);
            for (int k = 0; k < 5; k++)                                     // hooks down the aisle, and something on each
                cloth.Box(2, new Vector3(-1.2f, FY + 1.42f, -4.6f + k * 1.3f), new Vector3(.14f, .5f, .32f), Quaternion.Euler(0, 6f - k * 3f, 0), 1f);
            CeilingLamp(t, steel, glow, new Vector3(0, Deck + R - .16f, -1.6f), 6f, 1.8f, "CapsuleLight");
            CeilingLamp(t, steel, glow, new Vector3(0, Deck + R - .16f, 3f), 0f, 0f, "CapsuleShade");

            Part(t, "Joinery", wood, Plank);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Glowing", glow, LampGlow);
            Marker(t, "Elb_Bunk", new Vector3(0, FY, -1.9f), -90f);

            Solid(t, "FloorSolid", new Vector3(0, FY - .06f, 0), new Vector3(R * 2 - .5f, .14f, Len));
            // the aisle is where a 1.8 m capsule fits under the curve: the berths lie outside it, under the eaves
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "SideWest" : "SideEast", new Vector3(i * 1.3f, FY + .95f, 0), new Vector3(.32f, 1.9f, Len));
            Solid(t, "RoofSolid", new Vector3(0, Deck + R + .1f, 0), new Vector3(R * 2, .4f, Len));
            Solid(t, "EndGlass", new Vector3(0, FY + .9f, hz + .08f), new Vector3(R * 2, 1.9f, .2f));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "EndDoorWest" : "EndDoorEast", new Vector3(i * 1.42f, FY + .9f, -hz - .08f), new Vector3(1.94f, 1.9f, .2f));
            Solid(t, "EndDoorLintel", new Vector3(0, 2.7f, -hz - .08f), new Vector3(.9f, .5f, .2f));
            return Save(root);
        }

        // ── 6. the hostel on the foundation of Приют 11 ───────────────────────────────────────────────────
        /// <summary>«Приют одиннадцати» / «Дизель-хат», 4 050 m — the last roof on the mountain
        /// (<see cref="AscentRoute.LastHutEle"/>). The 1939 hotel burnt in 1998 when a primus went over; the ruin of it
        /// lies on the moraine beside this (<c>ElbrusAscent.Priut11Ruin</c> — this does not repeat it), and what stands
        /// now is a clad two-storey hostel. The ground floor is open: a hall with the drying room in it, a kitchen
        /// along the wall with the big pots that live on it, the long table everybody sits round at six in the evening
        /// before a summit day, the diesel convector the place is named after, the stairs to the floor above — and one
        /// room off it with eight places on two tiers.</summary>
        static GameObject Hostel()
        {
            const float W = 7.5f, L = 11f, H = 4.2f, T = .18f, F = .2f, CH = 2.4f;
            float hw = W * .5f, hl = L * .5f, inW = -hw + T, inE = hw - T, inF = hl - T;
            const float DW = 1.1f, DH = 2.1f;
            float wy0 = F + 1.02f, wy1 = F + 1.92f;

            var root = new GameObject("Elb_Hut_Diesel");
            var t = root.transform;

            var floor = new MeshBuilder(1);
            floor.Box(0, new Vector3(0, F * .5f, 0), new Vector3(W, F, L), Quaternion.identity, .6f);
            Part(t, "Floor", floor, Materials.Planks);

            var walls = new MeshBuilder(1);
            Panel(walls, 0, true, hl - T * .5f, T, -hw, hw, 0f, H, -DW * .5f, DW * .5f, F, F + DH);
            Panel(walls, 0, true, -hl + T * .5f, T, -hw, hw, 0f, H, 0f, 0f, 0f, 0f);
            foreach (int i in new[] { -1, 1 })
            {
                float x = i * (hw - T * .5f);
                Panel(walls, 0, false, x, T, -hl, -1.6f, 0f, H, -3.8f, -2.5f, wy0, wy1);
                Panel(walls, 0, false, x, T, -1.6f, hl, 0f, H, .6f, 1.9f, wy0, wy1);
            }
            Part(t, "Walls", walls, Materials.Get("ElbLHutWall", new Color(.55f, .58f, .62f), smoothness: .4f));

            var roof = new MeshBuilder(1);
            roof.Quad(0, new Vector3(-4.1f, H, -5.8f), new Vector3(0, H + 1.4f, -5.8f), new Vector3(0, H + 1.4f, 5.8f), new Vector3(-4.1f, H, 5.8f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            roof.Quad(0, new Vector3(0, H + 1.4f, -5.8f), new Vector3(4.1f, H, -5.8f), new Vector3(4.1f, H, 5.8f), new Vector3(0, H + 1.4f, 5.8f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, true);
            Part(t, "Roof", roof, Materials.Get("ElbLRoofRed", new Color(.72f, .26f, .2f), smoothness: .45f));

            var det = new MeshBuilder(1);
            det.Tube(0, new Vector3(2.6f, H + 1.2f, -3.2f), new Vector3(2.6f, H + 3f, -3.2f), .1f, .095f, 8, .5f, 0, true);
            for (int k = 0; k < 3; k++)
            {
                var c = new Vector3(0, F * .5f - .12f - k * .2f, hl + .55f + k * .45f);
                var sz = new Vector3(1.6f, .55f, .45f);
                det.Box(0, c, sz, Quaternion.identity, .8f);
                Solid(t, "StepSolid" + k, c, sz);
            }
            det.Box(0, new Vector3(0, F + DH + .1f, hl - T * .5f), new Vector3(DW + .3f, .14f, T + .06f), Quaternion.identity, 1f);
            det.Box(0, new Vector3(.94f, F + DH * .5f, hl + .2f), new Vector3(1.06f, DH - .08f, .06f), Quaternion.Euler(0, 58f, 0), 1f);
            Part(t, "Details", det, Steel);

            var glass = new MeshBuilder(1);
            foreach (int i in new[] { -1, 1 })                                   // the upper floor, as it always was
                for (int k = 0; k < 4; k++)
                    PaneX(glass, 0, i * 3.78f, -4f + k * 2.4f, -3.1f + k * 2.4f, 2.3f, 3.3f);
            foreach (int i in new[] { -1, 1 })
            {
                PaneX(glass, 0, i * (hw - T * .5f), -3.8f, -2.5f, wy0, wy1);
                PaneX(glass, 0, i * (hw - T * .5f), .6f, 1.9f, wy0, wy1);
            }
            var pane = Part(t, "Windows", glass, GlassClear);
            pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ceiling = new MeshBuilder(1);
            ceiling.Box(0, new Vector3(0, CH - .08f, 0), new Vector3(W - 2f * T, .16f, L - 2f * T), Quaternion.identity, .7f);
            Part(t, "Ceiling", ceiling, Ply);

            // the partitions: the bunk room on the west half, the hall across the door end
            var part = new MeshBuilder(1);
            Panel(part, 0, false, -.4f, .12f, -hl + T, -1f, F, CH, 0f, 0f, 0f, 0f);
            Panel(part, 0, true, -1f, .12f, inW, -.4f, F, CH, -2.2f, -1.4f, F, F + 2f);   // the room door, at the mouth of its aisle
            Panel(part, 0, true, 3.4f, .12f, inW, inE, F, CH, -1f, .3f, F, F + 2f);
            Part(t, "Partitions", part, Ply);

            var wood = new MeshBuilder(1);
            var steel = new MeshBuilder(1);
            var cloth = new MeshBuilder(3);
            var rubber = new MeshBuilder(1);
            var glow = new MeshBuilder(1);

            // eight places on two tiers, two stacks against the outer wall and two against the partition
            int seed = 4050;
            foreach (float bz in new[] { -4.1f, -2.05f })
            {
                Bunk(wood, steel, cloth, new Vector3(inW + .44f, F, bz), 2f, .88f, 2, 1, seed++);
                Bunk(wood, steel, cloth, new Vector3(-.98f, F, bz), 2f, .88f, 2, -1, seed++);
                Solid(t, $"BunkW{seed}", new Vector3(inW + .44f, F + .9f, bz), new Vector3(.9f, 1.8f, 2.05f));
                Solid(t, $"BunkE{seed}", new Vector3(-.98f, F + .9f, bz), new Vector3(.9f, 1.8f, 2.05f));
            }
            Shelves(wood, new Vector3(-1.9f, F + 1.1f, -5.1f), 1.8f, .28f, 1, .32f, .12f);

            // the kitchen along the east wall, with the pots that never come off it
            wood.Box(0, new Vector3(inE - .34f, F + .9f, -2.3f), new Vector3(.68f, .06f, 3.6f), Quaternion.identity, .9f);
            wood.Box(0, new Vector3(inE - .34f, F + .45f, -2.3f), new Vector3(.64f, .86f, 3.5f), Quaternion.identity, .9f);
            for (int k = 0; k < 3; k++)
            {
                steel.Box(0, new Vector3(inE - .34f, F + .93f, -3.6f + k * 1.2f), new Vector3(.42f, .05f, .42f), Quaternion.identity, 1f);
                Pot(steel, 0, new Vector3(inE - .34f, F + .95f, -3.6f + k * 1.2f), .17f, .23f);
            }
            GasBottle(steel, 0, new Vector3(inE - .38f, F, -.1f), .55f);
            GasBottle(steel, 0, new Vector3(inE - .38f, F, .28f), .55f);
            Shelves(wood, new Vector3(inE - .2f, F + 1.36f, -2.3f), 3.4f, .28f, 1, .32f, .12f, true);
            for (int k = 0; k < 12; k++)
                steel.Tube(0, new Vector3(inE - .2f, F + 1.5f, -3.8f + k * .28f), new Vector3(inE - .2f, F + 1.59f, -3.8f + k * .28f), .04f, .042f, 8, 1f, 0, true);

            // the long table, and the convector that gives the place its name
            TableTop(wood, steel, new Vector3(1.35f, F, -2.1f), 1.1f, 3.2f, .74f);
            for (int i = -1; i <= 1; i += 2)
                BenchSeat(wood, new Vector3(1.35f + i * .95f, F, -2.1f), .4f, 3f, .44f, false);
            steel.Box(0, new Vector3(.4f, F + .45f, 1.1f), new Vector3(.55f, .9f, 1.2f), Quaternion.identity, 1f);
            steel.Tube(0, new Vector3(.4f, F + .9f, 1.1f), new Vector3(.4f, CH + .4f, 1.1f), .08f, .075f, 8, 1f, 0, true);
            glow.Box(0, new Vector3(.11f, F + .55f, 1.1f), new Vector3(.02f, .3f, .6f), Quaternion.identity, 1f);

            // the stairs to the floor above, blocked at the top: the rooms up there are not built
            for (int k = 0; k < 7; k++)
                wood.Box(0, new Vector3(2.55f, F + .12f + k * .22f, 1.4f + k * .26f), new Vector3(1.1f, .1f, .28f), Quaternion.identity, .9f);
            wood.Box(0, new Vector3(2.55f, F + 1.56f, 3.1f), new Vector3(1.1f, .1f, .7f), Quaternion.identity, .9f);       // the landing
            wood.Box(0, new Vector3(2.55f, CH - .2f, 3.1f), new Vector3(1f, .08f, .8f), Quaternion.identity, .9f);         // the hatch, shut
            steel.Tube(0, new Vector3(2f, F + .9f, 1.4f), new Vector3(2f, F + 2f, 3.2f), .025f, .025f, 5, 1f);
            Solid(t, "StairSolid", new Vector3(2.55f, F + 1.1f, 2.2f), new Vector3(1.2f, 2.2f, 2.6f));

            // the drying room, in the hall where wet things come off
            DryRack(steel, cloth, rubber, new Vector3(-1.9f, F, 4.35f), 2.6f, 71);
            BenchSeat(wood, new Vector3(2f, F, 4.3f), 2.2f, .44f, .45f, true);
            for (int k = 0; k < 8; k++)
                rubber.Box(0, new Vector3(1f + k * .3f, F + .08f, 4.9f), new Vector3(.13f, .16f, .31f), Quaternion.Euler(0, k * 13f - 40f, 0), 1f);

            CeilingLamp(t, steel, glow, new Vector3(1.2f, CH - .12f, -2.2f), 7f, 2f, "HostelLight");
            CeilingLamp(t, steel, glow, new Vector3(-1.9f, CH - .12f, -3f), 0f, 0f, "HostelShade1");
            CeilingLamp(t, steel, glow, new Vector3(0, CH - .12f, 4.2f), 0f, 0f, "HostelShade2");

            var card = new MeshBuilder(1);
            card.Box(0, new Vector3(2.4f, F + 1.75f, inF - .03f), new Vector3(1.4f, .8f, .05f), Quaternion.identity, 1f);
            Part(t, "PriceCard", card, Paper);
            Sign(t, "PriceText", "ПРИЮТ 11 · ДИЗЕЛЬ-ХАТ · 4050 м\n" + Lodging.Get("priut11").Line
                + "\nКухня · сушилка · дизель до утра\nСтарая гостиница 1939 г. сгорела в 1998-м —\nеё фундамент рядом.",
                new Vector3(2.4f, F + 1.75f, inF - .07f), 180f, .0125f, new Color(.22f, .21f, .2f), TextAlignment.Center);

            Part(t, "Joinery", wood, Plank);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Soft", cloth, Mattress, Blanket, Jacket);
            Part(t, "Rubberware", rubber, Rubber);
            Part(t, "Glowing", glow, LampGlow);
            Marker(t, "Elb_Bunk", new Vector3(-.98f, F, -3f), 0f);

            Solid(t, "FloorSolid", new Vector3(0, F * .5f, 0), new Vector3(W, F, L));
            Solid(t, "WallBack", new Vector3(0, H * .5f, -hl + T * .5f), new Vector3(W, H, T));
            Solid(t, "WallWest", new Vector3(-hw + T * .5f, H * .5f, 0), new Vector3(T, H, L));
            Solid(t, "WallEast", new Vector3(hw - T * .5f, H * .5f, 0), new Vector3(T, H, L));
            for (int i = -1; i <= 1; i += 2)
                Solid(t, i < 0 ? "WallFrontWest" : "WallFrontEast",
                    new Vector3(i * (hw + DW * .5f) * .5f, H * .5f, hl - T * .5f), new Vector3(hw - DW * .5f, H, T));
            Solid(t, "WallFrontLintel", new Vector3(0, (F + DH + H) * .5f, hl - T * .5f), new Vector3(DW, H - F - DH, T));
            Solid(t, "CeilingSolid", new Vector3(0, CH - .08f, 0), new Vector3(W, .16f, L));
            Solid(t, "PartRoomSide", new Vector3(-.4f, (F + CH) * .5f, -3.16f), new Vector3(.14f, CH - F, 4.32f));
            Solid(t, "PartRoomEndW", new Vector3(-2.885f, (F + CH) * .5f, -1f), new Vector3(1.37f, CH - F, .14f));
            Solid(t, "PartRoomEndE", new Vector3(-.9f, (F + CH) * .5f, -1f), new Vector3(1f, CH - F, .14f));
            Solid(t, "PartRoomLintel", new Vector3(-1.8f, (F + 2f + CH) * .5f, -1f), new Vector3(.8f, CH - F - 2f, .14f));
            Solid(t, "PartHallWest", new Vector3(-2.16f, (F + CH) * .5f, 3.4f), new Vector3(2.57f, CH - F, .14f));
            Solid(t, "PartHallEast", new Vector3(1.83f, (F + CH) * .5f, 3.4f), new Vector3(3.09f, CH - F, .14f));
            Solid(t, "KitchenSolid", new Vector3(inE - .34f, F + .48f, -2.3f), new Vector3(.7f, .96f, 3.6f));
            Solid(t, "TableSolid", new Vector3(1.35f, F + .4f, -2.1f), new Vector3(1.1f, .8f, 3.2f));
            Solid(t, "HeaterSolid", new Vector3(.4f, F + .48f, 1.1f), new Vector3(.6f, .96f, 1.25f));
            Solid(t, "DrySolid", new Vector3(-1.9f, F + .86f, 4.35f), new Vector3(2.6f, 1.72f, .5f));
            return Save(root);
        }

        // ── 7. the diesel that keeps the camp alive ───────────────────────────────────────────────────────
        /// <summary>The generator of the barrel camp: a set on a skid under a lean-to of profiled sheet, the radiator
        /// and its grille towards the wind, the exhaust up through the roof with a cap on it, the panel with its one
        /// green lamp, and the drum it feeds from beside it with the line run across. It runs all evening and stops
        /// about ten, and everything in <see cref="Lodging"/> that says «дизель» means this.
        /// Pivot on the ground, +Z = the panel side.</summary>
        static GameObject Genset()
        {
            var root = new GameObject("Elb_Genset");
            var t = root.transform;
            var steel = new MeshBuilder(1);
            var paint = new MeshBuilder(1);
            var glow = new MeshBuilder(1);

            steel.Box(0, new Vector3(0, .14f, 0), new Vector3(1.3f, .28f, 2.1f), Quaternion.identity, .8f);        // the skid
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    steel.Box(0, new Vector3(i * .55f, .04f, k * .9f), new Vector3(.3f, .08f, .3f), Quaternion.identity, 1f);
            paint.Box(0, new Vector3(0, .62f, -.35f), new Vector3(1f, .68f, 1.1f), Quaternion.identity, .8f);      // the engine
            paint.Tube(0, new Vector3(0, .62f, .3f), new Vector3(0, .62f, .95f), .3f, .3f, 12, 1f, 0, true);       // the alternator
            steel.Box(0, new Vector3(0, .66f, -1f), new Vector3(.94f, .8f, .14f), Quaternion.identity, 1f);        // the radiator
            for (int k = 0; k < 7; k++)
                steel.Box(0, new Vector3(0, .36f + k * .1f, -1.08f), new Vector3(.9f, .04f, .04f), Quaternion.identity, 1f);
            steel.Tube(0, new Vector3(.38f, .95f, -.35f), new Vector3(.38f, 2.55f, -.35f), .055f, .05f, 8, 1f, 0, true);   // the exhaust
            steel.Tube(0, new Vector3(.38f, 2.55f, -.35f), new Vector3(.38f, 2.62f, -.35f), .09f, .07f, 8, 1f, 0, true);
            paint.Box(0, new Vector3(-.4f, 1.08f, .62f), new Vector3(.36f, .42f, .16f), Quaternion.identity, 1f);  // the panel
            glow.Box(0, new Vector3(-.4f, 1.18f, .71f), new Vector3(.05f, .05f, .02f), Quaternion.identity, 1f);
            for (int k = 0; k < 2; k++)
                steel.Tube(0, new Vector3(-.46f + k * .12f, .98f, .7f), new Vector3(-.46f + k * .12f, .98f, .73f), .025f, .025f, 6, 1f, 0, true);

            // the drum beside it and the line across to the engine
            steel.Tube(0, new Vector3(-1.05f, 0, .5f), new Vector3(-1.05f, .88f, .5f), .29f, .29f, 12, 1f, 0, true);
            steel.Tube(0, new Vector3(-1.05f, .78f, .5f), new Vector3(-.58f, .6f, .1f), .014f, .014f, 4, 1f);
            steel.Tube(0, new Vector3(-.58f, .6f, .1f), new Vector3(-.5f, .6f, -.3f), .014f, .014f, 4, 1f);

            // the lean-to over the lot, and the snow boards up the windward side
            var sheet = new MeshBuilder(1);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    sheet.Box(0, new Vector3(i * 1.15f, 1.15f, k * 1.35f), new Vector3(.1f, 2.3f, .1f), Quaternion.identity, 1f);
            sheet.Box(0, new Vector3(0, 2.34f, 0), new Vector3(2.6f, .1f, 3.1f), Quaternion.Euler(0, 0, 4f), .8f);
            sheet.Box(0, new Vector3(0, 1.2f, -1.4f), new Vector3(2.4f, 2f, .08f), Quaternion.identity, .8f);
            sheet.Box(0, new Vector3(-1.2f, 1.2f, -.4f), new Vector3(.08f, 2f, 1.9f), Quaternion.identity, .8f);
            Part(t, "Shelter", sheet, Sheet);
            Part(t, "Steelwork", steel, Steel);
            Part(t, "Paint", paint, Orange);
            Part(t, "Glowing", glow, Emissive("ElbLPanelGlow", new Color(.15f, .3f, .17f), new Color(.2f, 1f, .35f) * 1.6f, .4f));

            Solid(t, "Set", new Vector3(0, .7f, -.1f), new Vector3(1.3f, 1.4f, 2.2f));
            Solid(t, "Drum", new Vector3(-1.05f, .45f, .5f), new Vector3(.6f, .9f, .6f));
            Solid(t, "ShelterBack", new Vector3(0, 1.2f, -1.4f), new Vector3(2.4f, 2.4f, .16f));
            Solid(t, "ShelterSide", new Vector3(-1.2f, 1.2f, -.4f), new Vector3(.16f, 2.4f, 1.9f));
            return Save(root);
        }
    }
}
