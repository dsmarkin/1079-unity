using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>The places on the route that have a name: the moraine shelters at 4 050–4 200 m, Pastukhov rocks and
    /// the corridor between their two ridges, and the saddle — the Red Fox hut, the fumarole nineteen metres from it
    /// and the Soviet huts that are not there any more.
    ///
    /// Continues <see cref="ElbrusAscent"/>; everything lands in the same Elb_Ascent prefab.</summary>
    public static partial class ElbrusAscent
    {
        // ── Pastukhov rocks ───────────────────────────────────────────────────────────────────────────────
        /// <summary>The band of lava the route crosses, by our own height field.</summary>
        const float PastukhovFromEle = 4550f, PastukhovToEle = 4700f;
        /// <summary>Half the corridor. The real POI of the rocks sits 38 m to the right of the line, so the eastern
        /// ridge is put where the map says it is and the western one mirrored across: a gap about 75 m wide with the
        /// wands running down the middle of it.</summary>
        const float PastukhovGateM = 38f;

        // ── the saddle ────────────────────────────────────────────────────────────────────────────────────
        /// <summary>The hut, relative to the saddle POI. Nineteen metres off the line of the route and a little above
        /// it, where it stands.</summary>
        const float HutEastM = 25f, HutNorthM = 12f;
        const float HutLen = 7.4f, HutWide = 3.44f, HutFloor = .35f;
        /// <summary>Panel thickness of a honeycomb sandwich: the gap between <see cref="HutOuter"/> and
        /// <see cref="HutInner"/>, which is what the shell is actually made of.</summary>
        const float HutSkin = .12f;

        static int rockCount, moraineProps, ruinPieces;

        /// <summary>Footprints as <see cref="Height1079.Runtime.ElbrusWorld"/> lists them, so a hut dropped from here
        /// sits on the slope exactly the way the same hut dropped from there does.</summary>
        static readonly Vector2 HutSmall = new Vector2(3.9f, 5f), BarrelPad = new Vector2(2.7f, 6.4f);

        // ── materials ─────────────────────────────────────────────────────────────────────────────────────
        /// <summary>The shell of the hut: honeycomb sandwich panel, painted the red the maker paints them.</summary>
        static Material Panel => Materials.Get("ElbAPanel", new Color(.74f, .19f, .13f), smoothness: .42f);
        static Material PanelRoof => Materials.Get("ElbAPanelRoof", new Color(.79f, .8f, .82f), smoothness: .5f);
        /// <summary>Panel seams, the door frame, the hatch rim — the dark line between two panels.</summary>
        static Material Seam => Materials.Get("ElbASeam", new Color(.22f, .23f, .25f), smoothness: .4f);
        static Material HutSteel => Materials.Get("ElbAHutSteel", new Color(.48f, .5f, .53f), smoothness: .58f);
        static Material Ply => Materials.PH("ElbAPly", "raw_plank_wall", new Color(.68f, .56f, .41f), 1.6f);
        static Material OldPlank => Materials.PH("ElbAOldPlank", "raw_plank_wall", new Color(.45f, .43f, .4f), 1.6f);
        static Material Rust => Materials.Get("ElbARust", new Color(.42f, .27f, .18f), smoothness: .18f);
        /// <summary>The lava of Pastukhov rocks and the warm stone in the fumarole: near-black basalt, dry and matt.</summary>
        static Material Lava => Materials.PH("ElbALava", "rock_face_03", new Color(.3f, .28f, .27f), 1.4f, .08f);
        static Material Stone => Materials.PH("ElbAStone", "lichen_rock", new Color(.6f, .59f, .57f), .8f, .1f);
        static Material Char => Materials.PH("ElbAChar", "burned_ground_01", new Color(.3f, .26f, .23f), 1.2f, .05f);
        /// <summary>Sulphur round the lip of the fumarole: what the gas leaves behind as it cools.</summary>
        static Material Sulphur => Materials.Get("ElbASulphur", new Color(.85f, .76f, .22f), smoothness: .2f);
        static Material Concrete => Materials.Get("ElbAConcrete", new Color(.6f, .59f, .56f), smoothness: .07f);

        static Material steam;
        /// <summary>Steam off the fumarole. Lit (Particles/Standard Surface), never unlit: at four in the morning an
        /// unlit puff is a white lamp on the saddle (CLAUDE.md).</summary>
        static Material Steam
        {
            get
            {
                if (steam != null) return steam;
                var tex = Materials.OptionalTex($"{TextureFactory.Dir}/mist.png");
                var m = new Material(Shader.Find("Particles/Standard Surface")) { name = "ElbASteam", color = new Color(.86f, .88f, .9f, .75f) };
                if (tex != null) m.mainTexture = tex;
                m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Mode", 2); m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.SetFloat("_BlendOp", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 3000;
                string path = $"{WorldPaths.Generated}/Materials/ElbASteam.mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                return steam = m;
            }
        }

        /// <summary>Drops a prefab another factory already built (<see cref="ElbrusFactory"/>, <see cref="ElbrusProps"/>)
        /// onto the slope by the rules of <see cref="Height1079.Runtime.ElbrusWorld"/>: a building on the highest corner
        /// of its footprint, a loose thing at the middle of it, anything on runners laid on the slope plane.</summary>
        static GameObject Drop(string prefab, Transform parent, float x, float z, float yaw, Vector2 size,
            bool building = false, bool lies = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefab}.prefab");
            if (asset == null) { Debug.LogWarning("1079 Эльбрус: нет префаба " + prefab); return null; }
            float y = building ? Stand(x, z, yaw, size) : Flat(x, z, yaw, size);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), lies ? Lay(x, z, yaw) : Quaternion.Euler(0, yaw, 0));
            moraineProps++;
            return go;
        }

        /// <summary>A guy line from a point on a wagon down to a peg in the ice — everything up here is tied down.</summary>
        static void Guy(MeshBuilder mb, int sub, Vector3 top, Vector3 dir, float reach)
        {
            var peg = top + dir * reach;
            peg.y = Ground(peg.x, peg.z) + .1f;
            mb.Tube(sub, top, peg, .008f, .008f, 4, 1f, 0, false);
            mb.Tube(sub, peg + Vector3.up * .08f, peg - Vector3.up * .35f, .016f, .014f, 5, 1f, 0, true);
        }

        // ── 7. Pastukhov rocks ────────────────────────────────────────────────────────────────────────────
        /// <summary>Скалы Пастухова, 4 550–4 700 m: outcrops of lava, the one dark thing on a white slope and the
        /// landmark the whole descent is steered by. What matters is not the rocks but the gap: the right way down
        /// runs BETWEEN two ridges of them, and missing that corridor is what kills most of the people who die on this
        /// mountain. So there are two ridges here and a clear seventy-five metres between them, with the wand line
        /// down the middle; the blocks are biggest at the two ends of the corridor, so from above and from below you
        /// can see where the gate is. Two cairns stand at the top of it, which is what people build.</summary>
        static void PastukhovRocks(Transform root)
        {
            var group = new GameObject("Pastukhov").transform; group.SetParent(root, false);
            var ch = new Chunks("Pastukhov", 100f, Lava, Snow, Bamboo);   // RockFactory.Rock gives 0 stone, 1 snow on top
            float s0 = ArcAt(Up, PastukhovFromEle), s1 = ArcAt(Up, PastukhovToEle, s0);
            var rng = new System.Random(4650);

            for (int side = -1; side <= 1; side += 2)
                for (float s = s0; s <= s1; s += 9f)
                {
                    float t = Mathf.InverseLerp(s0, s1, s);
                    // biggest at the mouth and at the head of the corridor — that is what makes the gate readable —
                    // but never so small in the middle that the ridge stops being a ridge
                    float gate = 1f + .45f * Mathf.Cos(t * Mathf.PI * 2f);
                    int n = 1 + (rng.NextDouble() < .55 ? 1 : 0);
                    for (int k = 0; k < n; k++)
                    {
                        float off = side * (PastukhovGateM + (float)rng.NextDouble() * 26f);
                        float ds = (float)(rng.NextDouble() - .5) * 8f;
                        var p = On(Up, Mathf.Clamp(s + ds, 0f, upLength), off);
                        float scale = (1.1f + (float)rng.NextDouble() * 2.3f) * gate;
                        var rock = RockFactory.Rock(rng.Next(1, 9999), 2, new Vector3(scale * 1.25f, scale * .8f, scale));
                        var m = Matrix4x4.TRS(new Vector3(p.x, p.y - scale * .22f, p.z),
                            Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one);
                        ch.At(s).Append(rock, m);
                        rockCount++;
                    }
                }

            // two cairns at the head of the corridor, one on each side of the line
            for (int side = -1; side <= 1; side += 2)
            {
                var p = On(Up, s1 - 12f, side * 11f);
                Cairn(ch.At(s1), p, 1010 + side);
            }
            ch.Emit(group, collide: true);
        }

        /// <summary>A тур: the pile of stones somebody built at the top of the corridor so the next party would find it.</summary>
        static void Cairn(MeshBuilder mb, Vector3 foot, int seed)
        {
            var rng = new System.Random(seed);
            for (int k = 0; k < 9; k++)
            {
                float t = k / 8f;
                float r = .45f * (1f - t * .78f);
                float ang = (float)rng.NextDouble() * 6.28f;
                var c = foot + new Vector3(Mathf.Cos(ang) * r * .4f, .07f + t * 1.05f, Mathf.Sin(ang) * r * .4f);
                var rock = RockFactory.Rock(seed * 13 + k, 1, new Vector3(r, r * .5f, r * .85f));
                mb.Append(rock, Matrix4x4.TRS(c, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one));
            }
            // and the bamboo somebody left in the top of it
            mb.Tube(2, foot + Vector3.up * 1.0f, foot + Vector3.up * 1.75f, .022f, .018f, 5, 1f, 0, true);
        }

        // ── 8. the moraine shelters, 4 050–4 200 m ────────────────────────────────────────────────────────
        /// <summary>The last roofs on the mountain (<see cref="AscentRoute.LastHutEle"/>): «Приют 11» — the 1939 hotel
        /// that burnt down in 1998 when a primus went over, now a hostel on the old foundation with the ruin of the
        /// hotel lying beside it — «Приют 88», «Мария», «Орлиное гнездо» and the rescuers' wagons, where a night costs
        /// nothing. <see cref="Height1079.Runtime.ElbrusWorld"/> already stands the huts themselves on their POIs;
        /// this fills in what a working camp on a moraine actually has round it: barrel wagons on stone, a diesel
        /// shed with its exhaust, drums of fuel, a crate stack, gas bottles, a sled, the flags, guy lines on
        /// everything, and the toilet a long way downwind of the lot.</summary>
        static void Moraine(Transform root)
        {
            var group = new GameObject("Moraine").transform; group.SetParent(root, false);
            var p = Elbrus.Priut;
            float fall = FaceDownhill(p.X, p.Z, 25f);
            float c = Mathf.Cos(fall * Mathf.Deg2Rad), s = Mathf.Sin(fall * Mathf.Deg2Rad);
            // u across the slope, v up it
            Vector2 At(float u, float v) => new Vector2(p.X + u * c - v * s, p.Z - u * s - v * c);
            float Face(Vector2 q) => FaceDownhill(q.x, q.y, 14f);

            var ch = new Chunks("Moraine", 400f, HutSteel, Rust, Ply, Concrete, Char, Snow);
            var mb = ch.At(0);

            // the ruin of the 1939 hotel: the foundation it stood on, the burnt ends of the frame still in it
            var ruin = At(36f, 8f);
            Priut11Ruin(mb, new Vector3(ruin.x, Ground(ruin.x, ruin.y), ruin.y), Face(ruin));

            // «Мария» and «Орлиное гнездо» — two more boxes on the same moraine
            var maria = At(-31f, -17f);
            Drop("Elb_Hut_Small", group, maria.x, maria.y, Face(maria), HutSmall, building: true);
            var nest = At(27f, 41f);
            Drop("Elb_Hut_Small", group, nest.x, nest.y, Face(nest) + 24f, HutSmall, building: true);

            // the rescuers' wagons: two barrels you can sleep in for nothing, guyed down like everything else
            for (int k = 0; k < 2; k++)
            {
                var q = At(-15f + k * 4.2f, 23f + k * 9f);
                float yaw = Face(q);
                Drop("Elb_Barrel", group, q.x, q.y, yaw, BarrelPad, building: true);
                float y = Stand(q.x, q.y, yaw, BarrelPad);
                for (int g = 0; g < 4; g++)
                {
                    float a = 45f + g * 90f;
                    var dir = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad));
                    Guy(mb, 0, new Vector3(q.x, y + 2.5f, q.y) + dir * 1.3f, dir, 3.4f);
                }
            }

            // the diesel shed — the reason there is light in the hut at all, and the reason it is never quiet
            var gen = At(10f, -12f);
            DieselShed(mb, new Vector3(gen.x, Ground(gen.x, gen.y), gen.y), Face(gen));

            // drums of fuel beside it, laid on their sides on a pallet, and more standing
            for (int k = 0; k < 7; k++)
            {
                var q = At(15f + (k % 4) * 1.15f, -9f - (k / 4) * 1.3f);
                Drop("Elb_Drum", group, q.x, q.y, Face(q) + k * 37f, new Vector2(.65f, .65f));
            }
            var crates = At(-6f, -6f);
            Drop("Elb_CrateStack", group, crates.x, crates.y, Face(crates) + 18f, new Vector2(2.4f, 1.6f));
            var gas = At(4f, 14f);
            Drop("Elb_GasCage", group, gas.x, gas.y, Face(gas), new Vector2(1.9f, 1.1f));
            var sled = At(-9f, 12f);
            Drop("Elb_Sled", group, sled.x, sled.y, Face(sled) + 200f, new Vector2(1f, 2.2f), lies: true);
            var skip = At(20f, -20f);
            Drop("Elb_Skip", group, skip.x, skip.y, Face(skip), new Vector2(1.4f, 1.3f));
            for (int k = 0; k < 2; k++)
            {
                var flag = At(-2f + k * 5f, 4f);
                Drop("Elb_FlagPole", group, flag.x, flag.y, Face(flag), new Vector2(.7f, .7f));
            }
            // the toilet: downhill and on the far side of the camp from the route, which is where it always is
            var wc = At(34f, -38f);
            Drop("Elb_Toilet", group, wc.x, wc.y, Face(wc), new Vector2(4.8f, 2.8f), building: true);

            ch.Emit(group);
        }

        /// <summary>What is left of «Приют одиннадцати»: the stone-and-concrete foundation the 1939 hotel stood on,
        /// the burnt stubs of its frame still standing in it, a sheet of roof iron folded over, and snow in the
        /// corners. People climb up here looking for the famous hut and find this.</summary>
        static void Priut11Ruin(MeshBuilder mb, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var rng = new System.Random(1998);
            Vector3 L(float x, float y, float z) => at + rot * new Vector3(x, y, z);
            const float W = 9.5f, D = 5.4f;
            // the perimeter: four low walls with the top broken off at different heights
            for (int side = 0; side < 4; side++)
            {
                bool along = side < 2;
                float sgn = (side % 2 == 0) ? 1f : -1f;
                int n = along ? 7 : 4;
                for (int k = 0; k < n; k++)
                {
                    float t = (k + .5f) / n - .5f;
                    float h = .55f + (float)rng.NextDouble() * .7f;
                    var c = along ? L(t * W, h * .5f - .2f, sgn * D * .5f) : L(sgn * W * .5f, h * .5f - .2f, t * D);
                    var size = along ? new Vector3(W / n + .05f, h, .5f) : new Vector3(.5f, h, D / n + .05f);
                    mb.Box(3, c, size, rot, .7f);
                }
            }
            // the floor slab, and the stubs of the frame burnt off at the ankle
            mb.Box(3, L(0, -.12f, 0), new Vector3(W, .3f, D), rot, .6f);
            for (int k = 0; k < 9; k++)
            {
                float x = -W * .42f + k * (W * .84f / 8f);
                float z = (k % 2 == 0 ? -1f : 1f) * D * .33f;
                float h = .5f + (float)rng.NextDouble() * 1.5f;
                var a = L(x, 0f, z);
                var b = L(x + (float)(rng.NextDouble() - .5) * .5f, h, z + (float)(rng.NextDouble() - .5) * .4f);
                mb.Box(4, (a + b) * .5f, new Vector3(.16f, h, .16f), Quaternion.FromToRotation(Vector3.up, (b - a).normalized) * rot, 1f);
                ruinPieces++;
            }
            // a sheet of the roof iron, folded where it fell
            for (int k = 0; k < 3; k++)
            {
                float ang = 20f + k * 47f;
                var c = L(-W * .3f + k * 2.6f, .18f, D * .62f + k * .6f);
                mb.Box(1, c, new Vector3(2.3f, .04f, 1.3f), rot * Quaternion.Euler(9f + k * 5f, ang, k * 7f), 1f);
                ruinPieces++;
            }
            // and the snow that has got into it and stayed
            Surf.Lump(mb, 5, L(W * .2f, -.1f, -D * .2f), new Vector3(2.2f, .55f, 1.8f), 11, .3f);
            Surf.Lump(mb, 5, L(-W * .28f, -.1f, D * .15f), new Vector3(1.9f, .48f, 2.1f), 12, .3f);
        }

        /// <summary>The diesel: a plywood shed with the set inside it, the exhaust out of the roof and the drum it
        /// feeds from beside the door. The «Дизель-хат» at 4 050 m is named after this thing.</summary>
        static void DieselShed(MeshBuilder mb, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 L(float x, float y, float z) => at + rot * new Vector3(x, y, z);
            mb.Box(2, L(0, 1.05f, 0), new Vector3(2.4f, 2.1f, 3.1f), rot, .7f);          // the shed
            mb.Box(0, L(0, 2.17f, 0), new Vector3(2.7f, .14f, 3.4f), rot, .7f);          // the roof
            mb.Box(1, L(0, 1.0f, 1.58f), new Vector3(1.0f, 1.9f, .08f), rot, 1f);        // the door
            mb.Tube(0, L(.8f, 2.2f, -1.1f), L(.8f, 3.5f, -1.1f), .075f, .07f, 8, .6f, 0, true);   // exhaust
            mb.Box(0, L(.8f, 3.55f, -1.1f), new Vector3(.2f, .06f, .2f), rot, 1f);
            for (int k = 0; k < 3; k++)                                                   // louvres
                mb.Box(0, L(-1.22f, .8f + k * .34f, 0), new Vector3(.03f, .16f, 1.9f), rot * Quaternion.Euler(18f, 0, 0), 1f);
            mb.Tube(1, L(1.24f, .35f, .9f), L(2.0f, .35f, .9f), .022f, .022f, 5, 1f, 0, false);   // the fuel line to the drum
            mb.Tube(1, L(2.05f, .0f, .9f), L(2.05f, .88f, .9f), .29f, .29f, 12, 1f, 0, true);
        }

        // ── 4–6. the saddle ───────────────────────────────────────────────────────────────────────────────
        /// <summary>The saddle at ≈5 382 m by the radar (5 416 on the signpost): the Red Fox hut, the fumarole exactly
        /// <see cref="AscentRoute.FumaroleM"/> from it, and the remains of the Soviet huts that are not shelter
        /// (<see cref="AscentRoute.RuinsShelter"/>).</summary>
        static void SaddleCamp(Transform root)
        {
            var group = new GameObject("Saddle").transform; group.SetParent(root, false);
            var sad = Elbrus.Saddle;
            float hx = sad.X + HutEastM, hz = sad.Z + HutNorthM;

            // The hut lies ALONG the wind. On the saddle the wind funnels through the gap between the two summits, so
            // the long axis is the line from the east summit to the west one and the ends take the blast, not the sides.
            var w = Elbrus.WestSummit; var e = Elbrus.Get("eastSummit");
            float yaw = Mathf.Atan2(w.X - e.X, w.Z - e.Z) * Mathf.Rad2Deg;
            // and the tambour goes on whichever long side the route comes up, or nobody would ever find the door
            var arrive = On(Up, NearestOn(Up, hx, hz));
            var across = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            if (Vector3.Dot(new Vector3(arrive.x - hx, 0, arrive.z - hz), across) < 0) yaw += 180f;

            RedFoxHut(group, hx, hz, yaw);
            HutSnow(group, hx, hz, yaw);
            Fumarole(group, hx, hz, yaw);
            SovietRemains(group, hx, hz, yaw);
        }

        /// <summary>The section of the hut, half of it: bottom centre up to the top centre. Flat honeycomb panels on a
        /// frame, chamfered at every chine, so the wind goes round it instead of pushing on it — the first one was
        /// simply blown off the saddle, and this one is built to stand 80 m/s
        /// (<see cref="AscentRoute.SaddleHutWindMs"/>).</summary>
        static readonly Vector2[] HutOuter =
        {
            new Vector2(0f, -.10f), new Vector2(1.06f, .08f), new Vector2(1.56f, .56f),
            new Vector2(1.72f, 1.40f), new Vector2(1.52f, 2.22f), new Vector2(.96f, 2.66f), new Vector2(0f, 2.78f),
        };
        static readonly Vector2[] HutInner =
        {
            new Vector2(0f, .04f), new Vector2(.94f, .21f), new Vector2(1.43f, .67f),
            new Vector2(1.59f, 1.40f), new Vector2(1.40f, 2.12f), new Vector2(.86f, 2.54f), new Vector2(0f, 2.64f),
        };
        const int HutRings = 12, HutStations = 16;
        /// <summary>The doorway in the shell, measured along the hut. Two stations wide: a hatch you step through, not
        /// a door you walk through.</summary>
        const float DoorZ0 = .40f, DoorZ1 = 1.46f;

        /// <summary>The Red Fox hut on the saddle — the only roof above 4 200 m on the whole route, 1 440 m of climb
        /// above the last one, and the highest rescue shelter in Europe. Opened in 2012; the first one the wind took
        /// off the mountain, so this is the second, built to hold 80 m/s: honeycomb sandwich panels on a truss dug
        /// into the ice and weighted with stone, steel guys to anchors, a sealed door with a wheel on it like a
        /// submarine's, a tambour in front of that door so the blast never reaches it, and a hatch in the roof wide
        /// enough for a stretcher. Six lying, twelve sitting, and it is for emergencies. Inside there are bunks and
        /// nothing else at all.</summary>
        static void RedFoxHut(Transform group, float x, float z, float yaw)
        {
            var size = new Vector2(HutWide + 3.2f, HutLen);
            var root = new GameObject("Hut_RedFox5300");
            root.transform.SetParent(group, false);
            root.transform.SetPositionAndRotation(new Vector3(x, Stand(x, z, yaw, size), z), Quaternion.Euler(0, yaw, 0));
            var t = root.transform;
            float hl = HutLen * .5f;

            Vector3 Shell(Vector2[] sec, int j, int i)
            {
                var pt = j <= 6 ? sec[j] : sec[12 - j];
                float sx = j <= 6 ? 1f : -1f;
                float zt = (i / (float)HutStations) * 2f - 1f;
                float k = Mathf.Sqrt(Mathf.Max(.05f, 1f - .8f * zt * zt * zt * zt));
                return new Vector3(sx * pt.x * k, pt.y * (.58f + .42f * k), zt * hl);
            }
            bool Doorway(int j, int i)
            {
                if (j < 1 || j > 3) return false;                     // the +X waist only
                float z0 = (i / (float)HutStations) * 2f - 1f, z1 = ((i + 1) / (float)HutStations) * 2f - 1f;
                return z0 * hl > DoorZ0 - .05f && z1 * hl < DoorZ1 + .05f;
            }

            var skin = new MeshBuilder(2);                             // 0 painted panel, 1 roof
            var lining = new MeshBuilder(1);
            for (int j = 0; j < HutRings; j++)
                for (int i = 0; i < HutStations; i++)
                {
                    if (Doorway(j, i)) continue;
                    int sub = j >= 4 && j <= 8 ? 1 : 0;                // the top panels are the pale ones
                    skin.Quad(sub, Shell(HutOuter, j, i), Shell(HutOuter, j + 1, i), Shell(HutOuter, j + 1, i + 1), Shell(HutOuter, j, i + 1),
                        Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1));
                    lining.Quad(0, Shell(HutInner, j, i), Shell(HutInner, j + 1, i), Shell(HutInner, j + 1, i + 1), Shell(HutInner, j, i + 1),
                        Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1), true);
                }
            // the ends, closed with a fan of panels
            for (int end = 0; end <= 1; end++)
            {
                int i = end == 0 ? 0 : HutStations;
                var centre = new Vector3(0, 1.1f * .58f, (end == 0 ? -1f : 1f) * hl);
                for (int j = 0; j < HutRings; j++)
                {
                    var a = Shell(HutOuter, j, i); var b = Shell(HutOuter, j + 1, i);
                    // double-sided: the end panel is also the inside of the end wall
                    skin.Quad(0, centre, a, b, b, Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1), true);
                }
            }
            Part(t, "Shell", skin, Panel, PanelRoof);
            Part(t, "Lining", lining, Ply);

            var frame = new MeshBuilder(1);
            // the panel seams: a thin rib down every chine, the length of the hut
            for (int j = 1; j < HutRings; j++)
                for (int i = 0; i < HutStations; i++)
                    frame.Tube(0, Shell(HutOuter, j, i) * 1.005f, Shell(HutOuter, j, i + 1) * 1.005f, .016f, .016f, 4, 1f, 0, false);
            // and a hoop every metre and a half: the truss the panels are hung on
            for (int i = 0; i <= HutStations; i += 4)
                for (int j = 0; j < HutRings; j++)
                    frame.Tube(0, Shell(HutOuter, j, i) * 1.02f, Shell(HutOuter, j + 1, i) * 1.02f, .026f, .026f, 4, 1f, 0, false);
            Part(t, "Seams", frame, Seam);

            var steel = new MeshBuilder(1);
            // the truss under the floor, dug into the ice
            for (int k = -2; k <= 2; k++)
            {
                float zk = k * hl * .45f;
                steel.Tube(0, new Vector3(-1.25f, -.55f, zk), new Vector3(1.25f, -.05f, zk), .035f, .035f, 5, 1f, 0, true);
                steel.Tube(0, new Vector3(-1.25f, -.05f, zk), new Vector3(1.25f, -.55f, zk), .035f, .035f, 5, 1f, 0, true);
            }
            steel.Tube(0, new Vector3(-1.2f, -.2f, -hl), new Vector3(-1.2f, -.2f, hl), .045f, .045f, 6, 1f, 0, true);
            steel.Tube(0, new Vector3(1.2f, -.2f, -hl), new Vector3(1.2f, -.2f, hl), .045f, .045f, 6, 1f, 0, true);
            // six steel guys to anchors in the ice, each with its turnbuckle
            for (int k = 0; k < 6; k++)
            {
                float ang = 30f + k * 60f;
                var dir = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var top = new Vector3(dir.x * 1.1f, 2.35f, dir.z * 2.4f);
                var anchor = new Vector3(dir.x * 4.4f, -.35f, dir.z * 5.6f);
                steel.Tube(0, top, anchor, .014f, .014f, 4, 1f, 0, false);
                var mid = Vector3.Lerp(top, anchor, .55f);
                steel.Box(0, mid, new Vector3(.05f, .24f, .05f), Quaternion.FromToRotation(Vector3.up, (anchor - top).normalized), 1f);
                steel.Tube(0, anchor + Vector3.up * .25f, anchor - Vector3.up * .5f, .03f, .028f, 6, 1f, 0, true);
            }
            // the emergency hatch in the roof: a stretcher goes out through this one
            var hatch = new Vector3(0, 2.48f, -hl * .55f);
            steel.Tube(0, hatch, hatch + Vector3.up * .09f, .42f, .42f, 14, 1f, 0, false);
            steel.Tube(0, hatch + Vector3.up * .09f, hatch + Vector3.up * .14f, .44f, .4f, 14, 1f, 0, true);
            Part(t, "Steelwork", steel, HutSteel);

            // ── the doorway: the coaming, the frame and the door leaf standing open on its wheel ────────────
            var door = new MeshBuilder(2);      // 0 frame, 1 the leaf
            float dz = (DoorZ0 + DoorZ1) * .5f, dw = DoorZ1 - DoorZ0;
            float wallX = 1.64f;
            door.Box(0, new Vector3(wallX, .22f, dz), new Vector3(.3f, .3f, dw + .24f), Quaternion.identity, 1f);      // coaming
            door.Box(0, new Vector3(wallX, 2.26f, dz), new Vector3(.3f, .22f, dw + .24f), Quaternion.identity, 1f);    // lintel
            for (int k = -1; k <= 1; k += 2)
                door.Box(0, new Vector3(wallX, 1.25f, dz + k * (dw * .5f + .1f)), new Vector3(.3f, 2f, .2f), Quaternion.identity, 1f);
            // the leaf, swung back against the shell, with the wheel on it
            var leafRot = Quaternion.Euler(0, 62f, 0);
            var leafAt = new Vector3(wallX + .42f, 1.22f, dz + dw * .5f + .5f);
            door.Box(1, leafAt, new Vector3(.1f, 1.78f, dw), leafRot, 1f);
            var hub = leafAt + leafRot * new Vector3(.08f, 0, 0);
            door.Tube(1, hub, hub + leafRot * new Vector3(.07f, 0, 0), .05f, .05f, 8, 1f, 0, true);
            for (int k = 0; k < 4; k++)                                 // the кремальера, four spokes
            {
                float a = k * 45f * Mathf.Deg2Rad;
                var arm = leafRot * new Vector3(.1f, Mathf.Cos(a) * .24f, Mathf.Sin(a) * .24f);
                door.Tube(1, hub + leafRot * new Vector3(.1f, 0, 0), leafAt + arm, .018f, .016f, 4, 1f, 0, true);
            }
            Part(t, "Door", door, Seam, HutSteel);

            // ── the tambour: the box in front of the door, its own opening turned ninety degrees, so the wind
            // never gets a straight run at the seal ────────────────────────────────────────────────────────
            var amb = new MeshBuilder(2);       // 0 panel, 1 seam
            const float TX0 = 1.5f, TX1 = 3.1f, TZ0 = .28f, TZ1 = 1.82f, TTop = 2.45f, TDoor0 = 2.0f, TDoor1 = 2.95f;
            void Wall(Vector3 c, Vector3 s2) => amb.Box(0, c, s2, Quaternion.identity, .7f);
            Wall(new Vector3((TX0 + TX1) * .5f, HutFloor * .5f, (TZ0 + TZ1) * .5f), new Vector3(TX1 - TX0, HutFloor, TZ1 - TZ0));   // floor
            Wall(new Vector3((TX0 + TX1) * .5f, (HutFloor + TTop) * .5f, TZ0), new Vector3(TX1 - TX0, TTop - HutFloor, .11f));      // back
            Wall(new Vector3(TX1, (HutFloor + TTop) * .5f, (TZ0 + TZ1) * .5f), new Vector3(.11f, TTop - HutFloor, TZ1 - TZ0));      // outer side
            Wall(new Vector3((TX0 + TDoor0) * .5f, (HutFloor + TTop) * .5f, TZ1), new Vector3(TDoor0 - TX0, TTop - HutFloor, .11f));
            Wall(new Vector3((TDoor1 + TX1) * .5f, (HutFloor + TTop) * .5f, TZ1), new Vector3(TX1 - TDoor1, TTop - HutFloor, .11f));
            Wall(new Vector3((TDoor0 + TDoor1) * .5f, TTop - .08f, TZ1), new Vector3(TDoor1 - TDoor0, .28f, .11f));                 // lintel
            Wall(new Vector3((TX0 + TX1) * .5f, TTop + .06f, (TZ0 + TZ1) * .5f), new Vector3(TX1 - TX0 + .16f, .12f, TZ1 - TZ0 + .16f));
            amb.Box(1, new Vector3((TDoor0 + TDoor1) * .5f, HutFloor + .07f, TZ1), new Vector3(TDoor1 - TDoor0, .14f, .2f), Quaternion.identity, 1f);
            // three steps dug down through the drift to the sill, each one a stride the walking capsule takes
            for (int k = 0; k < 5; k++)
            {
                var c = new Vector3((TDoor0 + TDoor1) * .5f, HutFloor - .16f - k * .16f, TZ1 + .35f + k * .55f);
                var s2 = new Vector3(1.3f, 1.2f, .6f);
                amb.Box(1, c - Vector3.up * .55f, s2, Quaternion.identity, .6f);
                Solid(t, "StepSolid" + k, c - Vector3.up * .55f, s2);
            }
            Part(t, "Tambour", amb, Panel, Seam);

            var plate = new GameObject("Plate"); plate.transform.SetParent(t, false);
            plate.transform.localPosition = new Vector3(TX1 + .07f, 1.75f, (TZ0 + TZ1) * .5f);
            plate.transform.localRotation = Quaternion.Euler(0, 270f, 0);
            var tm = plate.AddComponent<TextMesh>();
            plate.AddComponent<Height1079.Runtime.SignText>();
            tm.text = "5300"; tm.characterSize = .07f; tm.fontSize = 80;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(.94f, .93f, .9f);

            // ── inside: the floor, and the bunks, and nothing whatever else ────────────────────────────────
            // The shell pulls in hard at both ends, so the floor is lofted to it rather than laid as one slab: a
            // rectangle wide enough to walk on in the middle would come out through the nose.
            var inside = new MeshBuilder(1);
            float FloorHalf(int i)
            {
                float zt = (i / (float)HutStations) * 2f - 1f;
                return 1.0f * Mathf.Sqrt(Mathf.Max(.05f, 1f - .8f * zt * zt * zt * zt));
            }
            Vector3 FloorPt(int i, int side) => new Vector3(side * FloorHalf(i), HutFloor, ((i / (float)HutStations) * 2f - 1f) * hl);
            for (int i = 1; i < HutStations - 1; i++)
            {
                // along first, across second, or the floor faces the ice
                inside.Quad(0, FloorPt(i, -1), FloorPt(i + 1, -1), FloorPt(i + 1, 1), FloorPt(i, 1),
                    Vector2.zero, new Vector2(0, 1), Vector2.one, new Vector2(1, 0), true);
            }
            // two tiers of bunks down both sides, cantilevered off the frame the way they are: six lying, twelve
            // sitting (AscentRoute.SaddleHutLying / SaddleHutSitting). The aisle between them is 1.26 m.
            float bunkHalf = HutLen * .5f - 1.5f;
            for (int side = -1; side <= 1; side += 2)
                for (int tier = 0; tier < 2; tier++)
                {
                    float by = HutFloor + .42f + tier * .92f;
                    inside.Box(0, new Vector3(side * .98f, by, 0), new Vector3(.7f, .08f, bunkHalf * 2f), Quaternion.identity, .8f);
                    for (int k = -1; k <= 1; k += 2)
                        inside.Tube(0, new Vector3(side * .68f, HutFloor, k * (bunkHalf - .3f)),
                            new Vector3(side * .68f, by, k * (bunkHalf - .3f)), .035f, .035f, 5, 1f, 0, false);
                }
            Part(t, "Bunks", inside, Ply);

            // the stone that holds the whole thing down
            var ballast = new MeshBuilder(2);
            var rng = new System.Random(5300);
            for (int k = 0; k < 22; k++)
            {
                float ang = (float)rng.NextDouble() * 6.28f;
                float rr = 1.7f + (float)rng.NextDouble() * .8f;
                var c = new Vector3(Mathf.Sin(ang) * rr, -.1f, Mathf.Cos(ang) * (rr + 1.7f));
                float sc = .3f + (float)rng.NextDouble() * .45f;
                ballast.Append(RockFactory.Rock(rng.Next(1, 9999), 1, new Vector3(sc, sc * .6f, sc * .85f)),
                    Matrix4x4.TRS(c, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one));
            }
            Part(t, "Ballast", ballast, Stone, Stone);

            // ── colliders: the shell in pieces, so the doorway stays open ──────────────────────────────────
            Solid(t, "FloorSolid", new Vector3(0, HutFloor - .12f, 0), new Vector3(2.8f, .3f, HutLen));
            Solid(t, "WallWest", new Vector3(-1.62f, 1.35f, 0), new Vector3(.3f, 2.5f, HutLen));
            Solid(t, "WallEastBack", new Vector3(1.62f, 1.35f, (DoorZ0 - HutLen * .5f) * .5f - .05f),
                new Vector3(.3f, 2.5f, HutLen * .5f + DoorZ0));
            Solid(t, "WallEastFront", new Vector3(1.62f, 1.35f, (DoorZ1 + HutLen * .5f) * .5f + .05f),
                new Vector3(.3f, 2.5f, HutLen * .5f - DoorZ1));
            Solid(t, "WallEastSill", new Vector3(1.62f, .18f, (DoorZ0 + DoorZ1) * .5f), new Vector3(.3f, .36f, dw));
            Solid(t, "WallEastLintel", new Vector3(1.62f, 2.5f, (DoorZ0 + DoorZ1) * .5f), new Vector3(.3f, .7f, dw));
            // the ends are walled off where the shell has narrowed to less than a shoulder: the tapered nose is
            // structure, not room
            Solid(t, "EndNorth", new Vector3(0, 1.2f, hl - .7f), new Vector3(3.2f, 2.4f, .5f));
            Solid(t, "EndSouth", new Vector3(0, 1.2f, -hl + .7f), new Vector3(3.2f, 2.4f, .5f));
            Solid(t, "CeilingSolid", new Vector3(0, 2.4f, 0), new Vector3(3.2f, .3f, HutLen));
            for (int side = -1; side <= 1; side += 2)
                Solid(t, side < 0 ? "BunkWest" : "BunkEast", new Vector3(side * 1.12f, HutFloor + .85f, 0),
                    new Vector3(1.0f, 1.7f, bunkHalf * 2f));
            Solid(t, "TambourFloor", new Vector3((TX0 + TX1) * .5f, HutFloor * .5f, (TZ0 + TZ1) * .5f), new Vector3(TX1 - TX0, HutFloor, TZ1 - TZ0));
            Solid(t, "TambourBack", new Vector3((TX0 + TX1) * .5f, (HutFloor + TTop) * .5f, TZ0), new Vector3(TX1 - TX0, TTop, .2f));
            Solid(t, "TambourSide", new Vector3(TX1, (HutFloor + TTop) * .5f, (TZ0 + TZ1) * .5f), new Vector3(.2f, TTop, TZ1 - TZ0));
            Solid(t, "TambourFrontW", new Vector3((TX0 + TDoor0) * .5f, (HutFloor + TTop) * .5f, TZ1), new Vector3(TDoor0 - TX0, TTop, .2f));
            Solid(t, "TambourFrontE", new Vector3((TDoor1 + TX1) * .5f, (HutFloor + TTop) * .5f, TZ1), new Vector3(TX1 - TDoor1, TTop, .2f));
            Solid(t, "TambourLintel", new Vector3((TDoor0 + TDoor1) * .5f, TTop, TZ1), new Vector3(TDoor1 - TDoor0, .4f, .2f));
            Solid(t, "TambourRoof", new Vector3((TX0 + TX1) * .5f, TTop + .06f, (TZ0 + TZ1) * .5f), new Vector3(TX1 - TX0, .2f, TZ1 - TZ0));
        }

        /// <summary>The drift. Outside, the hut is buried to the door on the windward side and along both ends; the
        /// only thing dug clear is the trench down to the tambour. Built in world space so every bank sits on the real
        /// ground and not on the hut's own datum.</summary>
        static void HutSnow(Transform group, float x, float z, float yaw)
        {
            var go = new GameObject("Hut_Drift"); go.transform.SetParent(group, false);
            var mb = new MeshBuilder(1);
            var rot = Quaternion.Euler(0, yaw, 0);
            var rng = new System.Random(80);
            void Bank(float lx, float lz, Vector3 size, int seed)
            {
                var p = new Vector3(x, 0, z) + rot * new Vector3(lx, 0, lz);
                p.y = Ground(p.x, p.z) - .25f;
                Surf.Lump(mb, 0, p, size, seed, .26f);
            }
            // the windward long side, buried to the eaves
            for (int k = -2; k <= 2; k++) Bank(-2.4f - (float)rng.NextDouble() * .5f, k * 1.7f, new Vector3(2.0f, 1.5f, 1.5f), 200 + k);
            // both ends, where the wind wraps round
            Bank(0f, HutLen * .5f + 1.4f, new Vector3(2.6f, 1.25f, 2.0f), 210);
            Bank(0f, -HutLen * .5f - 1.4f, new Vector3(2.6f, 1.35f, 2.1f), 211);
            // and the banks either side of the trench that was dug to the door
            Bank(3.3f, 2.9f, new Vector3(1.7f, 1.05f, 1.5f), 212);
            Bank(3.3f, -.9f, new Vector3(1.7f, 1.15f, 1.6f), 213);
            Part(go.transform, "Drift", mb, Snow);
        }

        /// <summary>The fumarole, exactly <see cref="AscentRoute.FumaroleM"/> from the hut and on the diagonal. A crack
        /// in the saddle that never freezes: a melt hollow in the snow, warm rock bare at the bottom of it, a thread of
        /// steam, and sulphur crusted yellow round the lip. Three things at once — somewhere warm, the one landmark
        /// that will still be there in a white-out when the hut is not, and sulphur dioxide, which is why nobody sleeps
        /// in it. The hollow is built as a rim standing over the snow rather than a hole cut into it, for the same
        /// reason as everything else on this mountain: a prefab cannot cut a height field.</summary>
        static void Fumarole(Transform group, float hutX, float hutZ, float hutYaw)
        {
            // of the four diagonals of the hut, the one that points nearest the route: a landmark you walk into
            var route = On(Up, NearestOn(Up, hutX, hutZ));
            var toRoute = new Vector3(route.x - hutX, 0, route.z - hutZ).normalized;
            float best = -2f, bearing = hutYaw + 45f;
            for (int k = 0; k < 4; k++)
            {
                float a = hutYaw + 45f + k * 90f;
                var dir = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad));
                float dot = Vector3.Dot(dir, toRoute);
                if (dot > best) { best = dot; bearing = a; }
            }
            float fx = hutX + Mathf.Sin(bearing * Mathf.Deg2Rad) * AscentRoute.FumaroleM;
            float fz = hutZ + Mathf.Cos(bearing * Mathf.Deg2Rad) * AscentRoute.FumaroleM;

            var go = new GameObject("Fumarole"); go.transform.SetParent(group, false);
            // 0 warm rock, 1 snow, 2 sulphur — in that order because RockFactory.Rock puts its stone in submesh 0
            var mb = new MeshBuilder(3);
            const int Rings = 6, Sect = 20;
            const float R = 2.9f;
            var rng = new System.Random(19);
            float wob = (float)rng.NextDouble() * 6.28f;
            Vector3 P(int i, int j)
            {
                float ang = j / (float)Sect * Mathf.PI * 2;
                float t = i / (float)Rings;
                float r = R * t * (.8f + .2f * Mathf.Sin(ang * 2.6f + wob));
                float px = fx + Mathf.Cos(ang) * r, pz = fz + Mathf.Sin(ang) * r;
                // bare rock flat in the middle, a bank of snow round the outside of it that comes back down to the
                // saddle at the rim — the thaw patch is built upward, like every other hollow on this mountain
                float lift = .02f + (t < .4f ? 0f : .6f * Mathf.Sin((t - .4f) / .6f * Mathf.PI));
                return new Vector3(px, Ground(px, pz) + lift, pz);
            }
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Sect; j++)
                {
                    int sub = i < 2 ? 0 : i == 2 ? 2 : 1;
                    mb.Quad(sub, P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j),
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            // the rock itself, broken and dark, with the crack in the middle of it
            for (int k = 0; k < 7; k++)
            {
                float ang = (float)rng.NextDouble() * 6.28f, rr = (float)rng.NextDouble() * .95f;
                var c = new Vector3(fx + Mathf.Cos(ang) * rr, 0, fz + Mathf.Sin(ang) * rr);
                c.y = Ground(c.x, c.z) - .05f;
                float sc = .22f + (float)rng.NextDouble() * .38f;
                mb.Append(WarmStone(rng.Next(1, 9999), sc), Matrix4x4.TRS(c, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one));
            }
            Part(go.transform, "Hollow", mb, Lava, Snow, Sulphur);

            var vent = new GameObject("Steam", typeof(ParticleSystem));
            vent.transform.SetParent(go.transform, false);
            vent.transform.position = new Vector3(fx, Ground(fx, fz) + .1f, fz);
            var ps = vent.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 6f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.startColor = Color.white;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 7f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .8f; shape.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            // x, y and z all TwoConstants: Unity refuses a velocity curve whose axes are in different modes
            vel.x = new ParticleSystem.MinMaxCurve(.4f, 1.6f);
            vel.y = new ParticleSystem.MinMaxCurve(.1f, .5f);
            vel.z = new ParticleSystem.MinMaxCurve(-.4f, .9f);
            var grow = ps.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .5f, 1, 2.4f));
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.55f, .2f), new GradientAlphaKey(0, 1) });
            fade.color = grad;
            var noise = ps.noise; noise.enabled = true; noise.strength = .35f; noise.frequency = .5f; noise.scrollSpeed = .3f;
            var r2 = ps.GetComponent<ParticleSystemRenderer>();
            r2.renderMode = ParticleSystemRenderMode.Billboard;
            r2.sharedMaterial = Steam;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
        }

        /// <summary>A block of the warm lava in the vent, rounded by the steam rather than by the wind.</summary>
        static MeshBuilder WarmStone(int seed, float scale)
            => RockFactory.Rock(seed, 1, new Vector3(scale * 1.2f, scale * .7f, scale));

        /// <summary>What is left of the Soviet huts on the saddle — the ones put up in the thirties and forties and
        /// long since gone. Boards and sheet iron under the snow, the corner of a frame, a stove pipe bent flat. People
        /// climb up here looking for them, because the guidebooks still mention them, and what they find is this:
        /// <see cref="AscentRoute.RuinsShelter"/> is false, and the world says so plainly.</summary>
        static void SovietRemains(Transform group, float hutX, float hutZ, float hutYaw)
        {
            var go = new GameObject("SovietRuins"); go.transform.SetParent(group, false);
            var mb = new MeshBuilder(3);        // 0 grey board, 1 rusted iron, 2 snow over them
            var rng = new System.Random(1939);

            for (int cluster = 0; cluster < 2; cluster++)
            {
                float bearing = hutYaw + (cluster == 0 ? 128f : 214f);
                float reach = cluster == 0 ? 34f : 47f;
                float cx = hutX + Mathf.Sin(bearing * Mathf.Deg2Rad) * reach;
                float cz = hutZ + Mathf.Cos(bearing * Mathf.Deg2Rad) * reach;
                float turn = (float)rng.NextDouble() * 360f;
                var rot = Quaternion.Euler(0, turn, 0);
                Vector3 L(float dx, float dz, float up)
                {
                    var p = new Vector3(cx, 0, cz) + rot * new Vector3(dx, 0, dz);
                    return new Vector3(p.x, Ground(p.x, p.z) + up, p.z);
                }
                // boards, mostly buried, tipped at whatever angle the snow left them
                for (int k = 0; k < 11; k++)
                {
                    float dx = (float)(rng.NextDouble() - .5) * 5.5f, dz = (float)(rng.NextDouble() - .5) * 4.5f;
                    float lean = 6f + (float)rng.NextDouble() * 38f;
                    float len = .9f + (float)rng.NextDouble() * 1.7f;
                    var c = L(dx, dz, .04f + Mathf.Sin(lean * Mathf.Deg2Rad) * len * .35f);
                    mb.Box(0, c, new Vector3(.19f, .035f, len), rot * Quaternion.Euler(lean, (float)rng.NextDouble() * 180f, 0), 1f);
                    ruinPieces++;
                }
                // the corner of the frame still standing, and a sheet of iron folded over it
                mb.Box(0, L(1.5f, -1.2f, .5f), new Vector3(.12f, 1.1f, .12f), rot * Quaternion.Euler(12f, 0, 7f), 1f);
                mb.Box(0, L(1.5f, -1.2f, .95f), new Vector3(1.4f, .11f, .11f), rot * Quaternion.Euler(0, 0, 9f), 1f);
                for (int k = 0; k < 3; k++)
                {
                    var c = L(-1.1f + k * 1.3f, .9f + k * .4f, .09f);
                    mb.Box(1, c, new Vector3(1.6f, .03f, 1.0f), rot * Quaternion.Euler(7f + k * 6f, k * 41f, 4f), 1f);
                    ruinPieces++;
                }
                // the stove pipe, flattened
                mb.Tube(1, L(-2.2f, -1.6f, .06f), L(-1.3f, -1.1f, .12f), .07f, .065f, 7, 1f, 0, true);
                mb.Tube(1, L(-1.3f, -1.1f, .12f), L(-.6f, -1.4f, .05f), .065f, .05f, 7, 1f, 0, true);
                // and the snow that has taken the rest
                for (int k = 0; k < 4; k++)
                {
                    float dx = (float)(rng.NextDouble() - .5) * 5f, dz = (float)(rng.NextDouble() - .5) * 4f;
                    Surf.Lump(mb, 2, L(dx, dz, -.2f), new Vector3(1.3f + (float)rng.NextDouble(), .45f, 1.1f + (float)rng.NextDouble()), 900 + cluster * 10 + k, .3f);
                }
            }
            Part(go.transform, "Remains", mb, OldPlank, Rust, Snow);
        }
    }
}
