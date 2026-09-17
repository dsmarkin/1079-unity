using UnityEditor;
using UnityEngine;
using Height1079.Core;
using static Height1079.EditorTools.World.Surf;

namespace Height1079.EditorTools.World
{
    public static partial class SiteFactory
    {
        static GameObject Off(GameObject go, Vector3 offset) { go.transform.localPosition += offset; return go; }

        public const string TentInteriorName = Height1079.Runtime.HikerController.LowSpaceName;

        /// <summary>The group's tent on its forest pad, morning of 1 Feb. Local: +Z = entrance (toward the fire), −X = uphill.
        /// Same tent and pitching as on the slope; the stove pipe leaves through the rear sleeve with the ring of raw bars. Outside: the nine
        /// rucksacks as they were dropped while packing (upright, slumped, fallen, dusted with snow), the eight free poles and the spare pair of skis.</summary>
        public static void Camp31Tent()
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight;
            var root = new GameObject("Site_Camp_31Jan_Tent");
            var t = root.transform;
            var k = new Kit();
            var id = Matrix4x4.identity;

            // trampled pad (lumpy, edges sinking) and the dug wall round it, open toward the fire
            var snow = k.B("PadSnow", Materials.Snow);
            Emit(snow, 0, 22, 34, (u, v) =>
            {
                float x = (u - .5f) * (W + 3.6f), z = (v - .5f) * (L + 5f) + .5f;
                float edge = Mathf.Max(Noise.Smooth(.8f, 1f, Mathf.Abs(2 * u - 1)), Noise.Smooth(.86f, 1f, Mathf.Abs(2 * v - 1)));
                float trodden = .025f * Noise.Fbm(new Vector3(x * 3f, 0, z * 3f), 3) + .015f * Mathf.Abs(Mathf.Sin(x * 9 + Mathf.Sin(z * 4) * 2));
                return new Vector3(x, .012f + trodden - .3f * edge, z);
            }, new Options { UvScale = new Vector2(W + 3.6f, L + 5f) * .8f });
            Bank(snow, id, new Vector3(0, 0, .45f), W / 2 + 2.3f, L / 2 + 2.6f, Mathf.PI / 2, .42f, .55f, 1.2f, 31);

            TentBody(t, 1.25f, open: true, roofSnow: .025f);

            // stove pipe outside: horizontal out of the sleeve, a joint, then a short rise; sooty end
            var tin = k.B("Pipe", Materials.Get("PipeSoot", new Color(.2f, .19f, .18f), smoothness: .25f));
            var p0 = new Vector3(0, .7f, -L / 2 - .05f); var p1 = new Vector3(0, .72f, -L / 2 - 1.0f); var p2 = new Vector3(0, .78f, -L / 2 - 1.12f); var p3 = new Vector3(0, 1.34f, -L / 2 - 1.16f);
            Sweep(tin, 0, new[] { p0, p1 }, (tt, a) => .05f, 14, 4, new Options(), false, false);
            Sweep(tin, 0, new[] { p1 + Vector3.forward * .01f, p2, p3 }, (tt, a) => .049f, 14, 8, new Options(), false, false);
            Sweep(tin, 0, new[] { p3 - Vector3.up * .2f, p3 }, (tt, a) => .045f, 14, 2, new Options { Flip = true }, false, false);
            Fan(tin, 0, id, Ring(p3 - Vector3.up * .15f, .046f, 14), Vector3.up);
            foreach (var jz in new[] { .35f, .7f }) Sweep(tin, 0, new[] { Vector3.Lerp(p0, p1, jz) - Vector3.forward * .015f, Vector3.Lerp(p0, p1, jz) + Vector3.forward * .015f }, (tt, a) => .056f, 14, 1, new Options(), true, true);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                var c = new Vector3(Mathf.Cos(a) * .16f, .7f + Mathf.Sin(a) * .16f, -L / 2 - .22f);
                var tan = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0) * Sites.Camp31.RingBarLength * .45f;
                Stick(k, id, c - tan, c + tan, .016f, 500 + i);
            }
            Stick(k, id, new Vector3(-.32f, -.25f, -L / 2 - .9f), new Vector3(.14f, .86f, -L / 2 - .93f), .02f, 520);
            Stick(k, id, new Vector3(.32f, -.25f, -L / 2 - .9f), new Vector3(-.14f, .86f, -L / 2 - .93f), .02f, 521);
            var smoke = new GameObject("StovePipeSmoke"); smoke.transform.SetParent(t, false); smoke.transform.localPosition = p3 + Vector3.up * .05f;

            // fir branches in front of the entrance
            var mat = new MeshBuilder(3);
            var rnd = new System.Random(311);
            for (int i = 0; i < 26; i++)
            {
                var a = new Vector3(-1.0f + (float)rnd.NextDouble() * 3.2f, .03f, L / 2 + .45f + (float)rnd.NextDouble() * 1.9f);
                var b = a + Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0) * Vector3.forward * (.7f + (float)rnd.NextDouble() * .4f);
                FirBranch(mat, a, b, .55f);
            }
            Part(t, "FirMat", mat, Materials.Bark, Materials.Find("NeedlesFir"), Materials.Find("SnowOnBranches"));

            // the nine rucksacks, dropped as they were being packed
            var poses = new[]
            {
                new RuckPose { At = new Vector3(.95f, 0, L / 2 + .8f), Yaw = 200, Tilt = -6, Full = .9f, Snow = .008f, Canvas = RuckKhaki },     // Slobodin's
                new RuckPose { At = new Vector3(1.38f, 0, L / 2 + .62f), Yaw = 165, Tilt = 9, Roll = 8, Full = 1f, Snow = .012f, Canvas = RuckGrey },
                new RuckPose { At = new Vector3(1.55f, 0, L / 2 + 1.75f), Yaw = 110, Lying = 1, Full = .95f, Snow = .03f, Canvas = RuckBrown },
                new RuckPose { At = new Vector3(.72f, 0, L / 2 + 1.55f), Yaw = 230, Tilt = 4, Full = .6f, Snow = .004f, Canvas = RuckKhaki },
                new RuckPose { At = new Vector3(2.2f, 0, L / 2 + .55f), Yaw = 70, Lying = 2, Full = .9f, Snow = .022f, Canvas = RuckGrey },
                new RuckPose { At = new Vector3(2.0f, 0, L / 2 + 1.2f), Yaw = 140, Tilt = 14, Roll = -6, Full = .95f, Snow = .015f, Canvas = RuckKhaki },
                new RuckPose { At = new Vector3(.9f, 0, L / 2 + 2.25f), Yaw = 300, Tilt = -3, Full = .45f, Snow = 0f, Canvas = RuckBrown },
                new RuckPose { At = new Vector3(-1.25f, 0, L / 2 + .95f), Yaw = 20, Lying = 1, Full = 1f, Snow = .045f, Sink = .05f, Canvas = RuckGrey },
                new RuckPose { At = new Vector3(-.9f, 0, L / 2 + 1.85f), Yaw = 250, Lying = 3, Full = .85f, Snow = .02f, Canvas = RuckKhaki },
            };
            for (int i = 0; i < poses.Length; i++)
                Rucksack(k, RuckMatrix(poses[i], .02f), poses[i].Full, poses[i].Snow, 400 + i * 7, poses[i].Canvas);
            // Slobodin's rucksack as on the 31 Jan photo: felt boots and an axe tied on
            var m0 = RuckMatrix(poses[0], .02f);
            float h0 = RuckH * Mathf.Lerp(.78f, 1f, poses[0].Full);
            FeltBoot(k, m0 * TRS(new Vector3(.22f, h0 + .045f, .04f), Quaternion.Euler(0, 0, 90)), 610, FeltBlack, .01f);
            FeltBoot(k, m0 * TRS(new Vector3(.2f, h0 + .1f, -.06f), Quaternion.Euler(0, 180, 0) * Quaternion.Euler(0, 0, -90) * Quaternion.Euler(0, 0, 0)), 611, FeltBlack, .012f);
            Axe(k, m0 * TRS(new Vector3(-.02f, h0 * .98f, RuckD * .42f), Quaternion.Euler(0, 90, 6)), .5f, false);
            var cord = k.B("RuckCord", Materials.Rope);
            Sweep(cord, 0, new[] { new Vector3(-.2f, h0 + .05f, .02f), new Vector3(0, h0 + .13f, 0), new Vector3(.2f, h0 + .05f, -.02f) }, (tt, a) => .004f, 4, 8, new Options { M = m0 }, false, false);

            // eight free ski poles in the snow left of the entrance, not quite in pairs; the spare pair of skis stuck upright
            rnd = new System.Random(733);
            for (int i = 0; i < Sites.Camp31.PolesLeftFree; i++)
            {
                var foot = new Vector3(-W / 2 - .7f - (float)rnd.NextDouble() * .25f, -.3f, L / 2 - .5f + i * .24f + (float)rnd.NextDouble() * .1f);
                var lean = new Vector3(((float)rnd.NextDouble() - .5f) * .35f, 1.42f, ((float)rnd.NextDouble() - .5f) * .3f);
                SkiPole(k, foot, foot + lean, 700 + i);
            }
            for (int i = 0; i < 2; i++)
                Ski(k, TRS(new Vector3(-W / 2 - 1.3f + i * .09f, .72f, L / 2 + 1.4f + i * .05f), Quaternion.Euler(0, 15 + i * 8, 0) * Quaternion.Euler(-90 + 4 - i * 7, 0, -3 + i * 5)), 720 + i, true, .015f);

            // walkable pad and the tent shell as colliders: side walls, roof slopes, rear end, doorway jambs; nothing across the doorway
            // (the ground under the pad is levelled in the terrain itself — no extra floor collider, it would make a step toward the lower fire circle)
            float wallH = Hr - Mathf.Sqrt(Sites.Tent.SlopeLength * Sites.Tent.SlopeLength - W * W / 4);
            float roofAngle = Mathf.Atan2(W / 2, Hr - wallH) * Mathf.Rad2Deg;
            foreach (int side in new[] { -1, 1 })
            {
                Solid(t, "WallCollider", new Vector3(side * (W / 2 + .03f), wallH / 2 + .02f, 0), new Vector3(.06f, wallH, L), Vector3.zero);
                Solid(t, "RoofCollider", new Vector3(side * (W / 4 + .02f), (wallH + Hr) / 2 + .02f, 0), new Vector3(.05f, Sites.Tent.SlopeLength, L), new Vector3(0, 0, side * roofAngle));
                Solid(t, "DoorJamb", new Vector3(side * (DoorHalf + (W / 2 - DoorHalf) / 2), wallH / 2 + .02f, L / 2 + .03f), new Vector3(W / 2 - DoorHalf, wallH, .06f), Vector3.zero);
            }
            Solid(t, "RearCollider", new Vector3(0, Hr / 2, -L / 2 - .03f), new Vector3(W, Hr, .06f), Vector3.zero);
            var inside = new GameObject(TentInteriorName); inside.transform.SetParent(t, false);
            var trig = inside.AddComponent<BoxCollider>(); trig.isTrigger = true;
            trig.center = new Vector3(0, .7f, .55f); trig.size = new Vector3(W + .3f, 1.4f, L + 1.3f);

            Camp31Interior(t);
            k.Flush(t);
            Save(root);
        }

        static Vector3[] Ring(Vector3 c, float r, int n)
        {
            var ring = new Vector3[n];
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; ring[i] = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            return ring;
        }

        /// <summary>Inside the tent, morning of 1 Feb. From the slope-tent inventory: Dyatlov's stove on the ridge rope with its pipe to the rear sleeve,
        /// 9 quilted blankets (there 2 spread, 7 crumpled), 8 quilted jackets, 8 pairs of ski boots and felt boots near the entrance, stove wood,
        /// Dyatlov's flashlight, the group diary, a Zorkiy camera. People slept across the tent, heads to the uphill side (−X) — assumed.</summary>
        static void Camp31Interior(Transform t)
        {
            float L = Sites.Tent.Length, W = Sites.Tent.Width, Hr = Sites.Tent.RidgeHeight, y0 = .045f;
            var root = new GameObject("Interior").transform; root.SetParent(t, false);
            var k = new Kit();
            var id = Matrix4x4.identity;
            var rnd = new System.Random(59);
            float R() => (float)rnd.NextDouble();

            // ridge rope, drying line, stove on wires
            var rope = k.B("Ropes", Materials.Rope);
            Sweep(rope, 0, new[] { new Vector3(0, Hr - .035f, -L / 2), new Vector3(0, Hr - .05f, 0), new Vector3(0, Hr - .035f, L / 2) }, (tt, a) => .006f, 5, 12, new Options(), false, false);
            Sweep(rope, 0, new[] { new Vector3(-.35f, .8f, -L / 2 + .15f), new Vector3(-.35f, .74f, -.4f), new Vector3(-.35f, .78f, .5f) }, (tt, a) => .004f, 4, 12, new Options(), false, false);
            float sz0 = -L / 2 + .42f, sz1 = sz0 + Sites.Camp31.StoveLength, sy = .47f, sh = Sites.Camp31.StoveHeight, sw = Sites.Camp31.StoveWidth;
            foreach (float wz in new[] { sz0 + .04f, sz1 - .04f })
                foreach (int side in new[] { -1, 1 })
                    Sweep(rope, 0, new[] { new Vector3(side * .1f, sy + sh, wz), new Vector3(0, Hr - .045f, wz) }, (tt, a) => .0022f, 3, 1, new Options(), false, false);

            var stove = k.B("Stove", Tin);
            BoxM(stove, 0, id, new Vector3(0, sy + sh / 2, (sz0 + sz1) / 2), new Vector3(sw, sh, Sites.Camp31.StoveLength), Quaternion.identity, 3);
            foreach (var zz in new[] { sz0 + .005f, sz1 - .005f })
                BoxM(stove, 0, id, new Vector3(0, sy + sh / 2, zz), new Vector3(sw + .008f, sh + .008f, .006f), Quaternion.identity, 3); // folded seams
            BoxM(k.B("CamBlack", SteelMat), 0, id, new Vector3(.06f, sy + .07f, sz1 + .006f), new Vector3(.03f, .02f, .01f), Quaternion.identity);
            // the door: a steel plate a little smaller than its opening, so the fire shows as a glowing rim and through three vent slots
            var door = new MeshBuilder(1);
            door.Quad(0, new Vector3(-.075f, sy + .045f, sz1 + .0045f), new Vector3(.075f, sy + .045f, sz1 + .0045f), new Vector3(.075f, sy + .145f, sz1 + .0045f), new Vector3(-.075f, sy + .145f, sz1 + .0045f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            var plate = k.B("StoveDoor", SteelMat);
            BoxM(plate, 0, id, new Vector3(0, sy + .122f, sz1 + .0075f), new Vector3(.142f, .042f, .003f), Quaternion.identity);
            BoxM(plate, 0, id, new Vector3(0, sy + .067f, sz1 + .0075f), new Vector3(.142f, .038f, .003f), Quaternion.identity);
            for (int i = 0; i < 4; i++) BoxM(plate, 0, id, new Vector3(-.066f + i * .044f, sy + .094f, sz1 + .0075f), new Vector3(.01f, .016f, .003f), Quaternion.identity);
            var glowMat = Materials.Get("StoveDoorGlow", new Color(.2f, .06f, .02f), smoothness: .2f);
            glowMat.EnableKeyword("_EMISSION"); glowMat.SetColor("_EmissionColor", new Color(1f, .33f, .07f) * 1.3f);
            glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(glowMat);
            Part(root, "StoveDoorGlow", door, glowMat);
            Sweep(stove, 0, new[] { new Vector3(0, sy + sh - .01f, sz0 + .07f), new Vector3(0, .69f, sz0 + .02f), new Vector3(0, .7f, -L / 2 + .02f) }, (tt, a) => .05f, 12, 6, new Options(), false, false);
            Lathe(k.B("Asbestos", Materials.Get("Asbestos", new Color(.8f, .78f, .74f), smoothness: .02f)), 0,
                Poly(new Vector2(.05f, 0), new Vector2(.135f, 0), new Vector2(.135f, .012f), new Vector2(.05f, .012f)), 18, 4,
                new Options { M = TRS(new Vector3(0, .7f, -L / 2 + .045f), Quaternion.Euler(90, 0, 0)) }, .03f, 3);
            Solid(root, "StoveCollider", new Vector3(0, sy + sh / 2, (sz0 + sz1) / 2), new Vector3(sw, sh, Sites.Camp31.StoveLength), Vector3.zero);

            // stove wood by the rear end
            for (int i = 0; i < 10; i++)
            {
                var a = new Vector3(.45f + (i % 2) * .12f, y0 + .03f + (i / 4) * .045f, -L / 2 + .12f + (i % 5) * .065f);
                Log(k, id, a, a + new Vector3(.28f, (R() - .5f) * .01f, .02f), .022f + R() * .006f, 800 + i, false, 0f, 0);
            }

            // blankets: two spread across the tent, seven thrown back in heaps
            var quilts = new[] { QuiltBlue, QuiltMaroon, QuiltGreen };
            BlanketSpread(k, TRS(new Vector3(.05f, y0, -.55f), Yaw(2)), W - .12f, 1.25f, 900, QuiltBlue);
            BlanketSpread(k, TRS(new Vector3(0, y0 + .006f, .62f), Yaw(-3)), W - .1f, 1.2f, 901, QuiltGreen);
            for (int i = 0; i < Sites.Camp31.Blankets - 2; i++)
            {
                float z = -1.5f + i * .43f;
                BlanketHeap(k, TRS(new Vector3(-.2f + (R() - .5f) * .6f, y0 + .02f, z), Yaw(R() * 360)), .22f + R() * .08f, .1f + R() * .06f, 910 + i, quilts[i % 3]);
            }

            // quilted jackets rolled as pillows along the uphill wall
            for (int i = 0; i < 8; i++)
                VatnikRoll(k, TRS(new Vector3(-W / 2 + .22f, y0 + .01f, -1.75f + i * .46f), Yaw((R() - .5f) * 30)), 930 + i);

            // footwear inside the doorway: 8 pairs of ski boots (a few knocked over), felt boots by the stove
            for (int i = 0; i < 16; i++)
            {
                int side = i < 8 ? -1 : 1, j = i % 8;
                var c = new Vector3(side * (DoorHalf + .09f + (j % 4) * .11f), y0 + .005f, L / 2 - .25f - (j / 4) * .34f);
                bool tipped = R() < .25f;
                var rot = Yaw(side * 90 + (R() - .5f) * 50) * (tipped ? Quaternion.Euler(0, 0, 80) : Quaternion.identity);
                SkiBoot(k, TRS(c + (tipped ? Vector3.up * .04f : Vector3.zero), rot), 950 + i);
            }
            for (int i = 0; i < 5; i++)
            {
                bool fallen = i == 4;
                var pos = new Vector3(-.4f + i * .15f, y0 + (fallen ? .055f : 0), -L / 2 + 1.05f + (fallen ? .25f : 0));
                FeltBoot(k, TRS(pos, fallen ? Quaternion.Euler(0, 60, 0) * Quaternion.Euler(-88, 0, 0) : Yaw((R() - .5f) * 40)), 980 + i, i % 2 == 0 ? FeltGrey : FeltBlack);
            }

            // socks and a pair of mittens on the drying line
            for (int i = 0; i < 6; i++)
                Sock(k, TRS(new Vector3(-.35f, .77f - Mathf.Sin(Mathf.PI * i / 6f) * .04f, -L / 2 + .45f + i * .36f), Yaw(90 + (R() - .5f) * 30)), 990 + i);
            Mitten(k, TRS(new Vector3(-.35f, .735f, .25f), Yaw(90) * Quaternion.Euler(180, 0, 0)), 996);
            Mitten(k, TRS(new Vector3(-.35f, .74f, .38f), Yaw(95) * Quaternion.Euler(180, 0, 0)), 997);

            // small things near the doorway: the camera in its case, the diary with a pencil, Dyatlov's flashlight
            ZorkiyCamera(k, TRS(new Vector3(.62f, y0 + .004f, 1.42f), Yaw(25)));
            Diary(k, TRS(new Vector3(.02f, y0 + .002f, 1.42f), Yaw(-12)));
            Flashlight(k, TRS(new Vector3(-.56f, y0 + .026f, 1.3f), Quaternion.Euler(0, 70, 0) * Quaternion.Euler(0, 0, -90)));

            // light: the stove's glow and daylight through the canvas
            var stoveLight = new GameObject("StoveLight", typeof(Light)).GetComponent<Light>();
            stoveLight.transform.SetParent(root, false); stoveLight.transform.localPosition = new Vector3(0, sy + .1f, sz1 + .25f);
            stoveLight.type = LightType.Point; stoveLight.color = new Color(1f, .55f, .25f); stoveLight.range = 3.2f; stoveLight.intensity = 1.4f; stoveLight.shadows = LightShadows.None;
            var canvasLight = new GameObject("CanvasLight", typeof(Light)).GetComponent<Light>();
            canvasLight.transform.SetParent(root, false); canvasLight.transform.localPosition = new Vector3(0, .85f, .4f);
            canvasLight.type = LightType.Point; canvasLight.color = new Color(.95f, .92f, .75f); canvasLight.range = 3.4f; canvasLight.intensity = .7f; canvasLight.shadows = LightShadows.None;
            k.Flush(root);
        }

        /// <summary>Fire and kitchen: "костёр на брёвнах" — a raft of green logs on the snow (diary 31.01) with a half-burnt stack on it; two buckets on a
        /// crossbar, two pots, felt boots and mittens drying on sticks (the diary of 30.01 records burnt mittens and a quilted jacket), firewood from
        /// "frail damp firs", the saw on a half-cut log, one axe in the chopping block, the small one in its case. The runtime lights it.</summary>
        public static void Camp31Fire()
        {
            var fire = new GameObject("Site_Camp_31Jan_Fire");
            var f = fire.transform;
            var k = new Kit();
            var id = Matrix4x4.identity;
            var rnd = new System.Random(1959);
            float R() => (float)rnd.NextDouble();

            var snow = k.B("FireSnow", Materials.Snow);
            Emit(snow, 0, 30, 10, (u, v) =>
            {
                float th = u * Mathf.PI * 2, r = v * 2.8f;
                float y = .01f + .03f * Noise.Fbm(new Vector3(Mathf.Cos(th) * r, 0, Mathf.Sin(th) * r) * 1.5f, 3) - .12f * Noise.Smooth(.2f, 0f, v) - .25f * Noise.Smooth(.85f, 1f, v);
                return new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
            }, new Options { ClosedU = true, UvScale = new Vector2(8, 3) });
            Bank(snow, id, Vector3.zero, 3.0f, 3.0f, -Mathf.PI / 2, .5f, .38f, .9f, 50);

            // raft of green logs, the burnt stack on it, ash and embers
            for (int i = 0; i < 5; i++)
                Log(k, id, new Vector3(-.75f, .09f, -.4f + i * .2f), new Vector3(.75f, .09f + (i % 2) * .01f, -.38f + i * .2f), .09f + R() * .015f, 1000 + i, false, .01f, 2);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2 + R() * .4f;
                var outer = new Vector3(Mathf.Cos(a) * .62f, .2f, Mathf.Sin(a) * .5f);
                Log(k, id, outer, new Vector3(Mathf.Cos(a) * .06f, .5f + R() * .08f, Mathf.Sin(a) * .06f), .045f + R() * .015f, 1010 + i, true, 0f, 0, .03f);
            }
            Lump(k.B("Ash", Materials.Get("Ash", new Color(.4f, .39f, .38f), smoothness: .02f)), 0, new Vector3(0, .19f, 0), new Vector3(.4f, .05f, .32f), 1020, .3f, null, 0, false, null, .02f);
            var embers = k.B("Embers", Embers);
            for (int i = 0; i < 7; i++) Lump(embers, 0, new Vector3((R() - .5f) * .35f, .21f, (R() - .5f) * .3f), new Vector3(.05f, .02f, .04f), 1030 + i, .3f, null, 0, true);

            // crossbar on two forked stakes, buckets hanging on wire hooks, pots on the snow
            foreach (int sx in new[] { -1, 1 })
            {
                Stick(k, id, new Vector3(sx * 1.1f, -.3f, 0), new Vector3(sx * 1.08f, 1.48f, 0), .03f, 1040 + sx);
                Stick(k, id, new Vector3(sx * 1.08f, 1.32f, 0), new Vector3(sx * 1.2f, 1.62f, .02f), .017f, 1044 + sx);
            }
            Stick(k, id, new Vector3(-1.4f, 1.5f, 0), new Vector3(1.4f, 1.52f, 0), .027f, 1046);
            var wire = k.B("Wire", SteelMat);
            foreach (int sx in new[] { -1, 1 })
            {
                var b = new Vector3(sx * .32f, .86f, 0);
                Bucket(k, TRS(b, Yaw(sx * 20)), .26f, .15f, .12f, 1050 + sx, .72f, true);
                Sweep(wire, 0, new[] { b + Vector3.up * (.26f * .86f + .16f + .15f * 1.05f), new Vector3(sx * .32f, 1.5f, 0) }, (tt, a) => .0022f, 4, 2, new Options(), false, false);
            }
            Bucket(k, TRS(new Vector3(.95f, .02f, 1.35f), Yaw(40)), .16f, .12f, .11f, 1053, .6f, true);
            Bucket(k, TRS(new Vector3(1.28f, .02f, 1.15f), Yaw(-20)), .14f, .1f, .095f, 1054, 0f, true, true);

            // drying: felt boots upside down on sticks, mittens
            for (int i = 0; i < 4; i++)
            {
                float a = (-40 + i * 22) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Sin(a) * 1.55f, -.3f, -Mathf.Cos(a) * 1.55f);
                var top = new Vector3(Mathf.Sin(a) * 1.3f, 1.05f, -Mathf.Cos(a) * 1.3f);
                Stick(k, id, foot, top, .015f, 1060 + i);
                var dir = (top - foot).normalized;
                if (i < 3) FeltBoot(k, TRS(top + dir * .06f, Quaternion.FromToRotation(Vector3.up, -dir) * Yaw(a * Mathf.Rad2Deg + 180)), 1064 + i, i == 1 ? FeltBlack : FeltGrey);
                else Mitten(k, TRS(top + dir * .02f, Quaternion.FromToRotation(Vector3.up, -dir)), 1068);
            }
            Stick(k, id, new Vector3(-1.05f, -.3f, .75f), new Vector3(-.9f, 1.02f, .6f), .013f, 1069);
            Mitten(k, TRS(new Vector3(-.9f, 1.03f, .6f), Quaternion.Euler(180, 30, 8)), 1070);

            // seats, woodpile, chopping block with the large axe, the saw on a half-cut log, the small axe in its case
            Log(k, id, new Vector3(-1.95f, .12f, -1.0f), new Vector3(-1.5f, .13f, 1.0f), .14f, 1080, false, .02f, 2);
            Log(k, id, new Vector3(-.95f, .12f, -1.95f), new Vector3(.9f, .13f, -1.85f), .13f, 1081, false, .015f, 1);
            for (int i = 0; i < 11; i++)
            {
                var a = new Vector3(2.0f + (R() - .5f) * .05f, .065f + (i / 4) * .11f, -1.15f + (i % 4) * .13f + (i / 4) * .06f);
                Log(k, id, a, a + new Vector3(.12f + (R() - .5f) * .1f, (R() - .5f) * .03f, .85f + R() * .15f), .05f + R() * .012f, 1090 + i, false, i >= 8 ? .02f : .004f, 1);
            }
            Log(k, id, new Vector3(1.7f, -.2f, 1.9f), new Vector3(1.7f, .32f, 1.9f), .19f, 1110, false, .01f, 0, .18f);
            Axe(k, TRS(new Vector3(1.74f, .39f, 1.9f), Yaw(30) * Quaternion.Euler(0, 0, -120)), .62f, false);
            Log(k, id, new Vector3(-.4f, .1f, 2.2f), new Vector3(1.1f, .12f, 2.4f), .1f, 1111, false, .015f, 2);
            Saw(k, TRS(new Vector3(.35f, .255f, 2.285f), Yaw(-8)));
            Axe(k, TRS(new Vector3(2.15f, .47f, -.6f), Yaw(20) * Quaternion.Euler(90, 0, 0)), .38f, true);

            k.Flush(f);
            Save(fire);
        }
    }
}
