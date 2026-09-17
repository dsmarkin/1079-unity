using System.IO;
using UnityEditor;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    /// <summary>The Menk — a forest giant of Mansi tales, the game's own fiction. ~4.3 m, gaunt and hunched, arms to the ground,
    /// bark-dark shaggy hide, a crown of dry branches on the skull and shoulders: with its arms raised it reads as a dead spruce.
    /// Built as a hierarchy of joints (no skinning) that Runtime/MenkView animates by rotating the named bones.</summary>
    public static class CreatureFactory
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Creatures";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Creatures";
        const int Bark = 0, Hide = 1, Eyes = 2, SnowSub = 3;

        static System.Random rnd;
        static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(MeshDir);
            rnd = new System.Random(1959);
            var bark = Materials.PH("MenkBark", "bark_brown_02", new Color(.3f, .27f, .25f), 1.6f, .04f);
            var hide = Materials.Get("MenkHide", new Color(.075f, .066f, .06f), smoothness: .04f);
            var eyes = Materials.Get("MenkEyes", new Color(.02f, .02f, .018f), smoothness: .95f);
            eyes.EnableKeyword("_EMISSION"); eyes.SetColor("_EmissionColor", new Color(.16f, .18f, .12f));
            EditorUtility.SetDirty(eyes);
            var mats = new[] { bark, hide, eyes, Materials.Snow };

            var root = new GameObject("Menk");
            var hips = Bone(root.transform, "Hips", new Vector3(0, 2.05f, 0));
            var spine = Bone(hips, "Spine", new Vector3(0, .1f, 0));
            var chest = Bone(spine, "Chest", new Vector3(0, .7f, 0));
            var neck = Bone(chest, "Neck", new Vector3(0, .55f, .05f));
            var head = Bone(neck, "Head", new Vector3(0, .36f, .1f));

            // pelvis
            var mb = New();
            Surf.Lump(mb, Hide, new Vector3(0, -.02f, 0), new Vector3(.36f, .26f, .26f), 3, .3f, full: true);
            Shag(mb, 26, y => new Vector3(0, y, 0), -.2f, .1f, .36f, .27f, .3f, .55f);
            Part(hips, mb, mats);

            // belly and ribs: gaunt, slightly twisted like a trunk
            mb = New();
            Limb(mb, Hide, new[] { Vector3.zero, new Vector3(0, .36f, .03f), new Vector3(0, .72f, 0) }, t => Mathf.Lerp(.34f, .4f, t), 1.3f, .85f, 7);
            Shag(mb, 70, y => new Vector3(0, y, 0), 0f, .72f, .4f, .32f, .3f, .6f);
            Part(spine, mb, mats);

            // chest, shoulders, branches growing out of the back
            mb = New();
            Limb(mb, Hide, new[] { Vector3.zero, new Vector3(0, .3f, -.03f), new Vector3(0, .6f, .02f) }, t => t < .7f ? Mathf.Lerp(.4f, .5f, t / .7f) : Mathf.Lerp(.5f, .26f, (t - .7f) / .3f), 1.35f, .8f, 11);
            Surf.Lump(mb, Hide, new Vector3(-.5f, .46f, 0), new Vector3(.2f, .18f, .2f), 5, .35f, full: true);
            Surf.Lump(mb, Hide, new Vector3(.5f, .46f, 0), new Vector3(.2f, .18f, .2f), 6, .35f, full: true);
            Shag(mb, 90, y => new Vector3(0, y, 0), 0f, .62f, .5f, .38f, .35f, .75f);
            Branch(mb, new Vector3(-.3f, .55f, -.25f), new Vector3(-.5f, 1.1f, -.6f), .07f, 3);
            Branch(mb, new Vector3(.25f, .5f, -.3f), new Vector3(.7f, 1.3f, -.45f), .08f, 4);
            Branch(mb, new Vector3(.05f, .3f, -.36f), new Vector3(-.1f, .9f, -1.05f), .05f, 2);
            Surf.Lump(mb, SnowSub, new Vector3(-.45f, .64f, 0), new Vector3(.18f, .05f, .16f), 7, .3f);
            Surf.Lump(mb, SnowSub, new Vector3(.42f, .62f, -.04f), new Vector3(.2f, .05f, .15f), 8, .3f);
            Part(chest, mb, mats);

            // neck
            mb = New();
            Limb(mb, Hide, new[] { Vector3.zero, new Vector3(0, .2f, .06f), new Vector3(0, .38f, .1f) }, t => Mathf.Lerp(.17f, .13f, t), 1f, 1f, 13);
            Shag(mb, 24, y => new Vector3(0, y, y * .25f), 0f, .36f, .17f, .17f, .25f, .45f);
            Part(neck, mb, mats);

            // head: long skull, heavy brow, deep sockets with a faint wet glint, a crown of dry branches
            mb = New();
            Surf.Lump(mb, Hide, new Vector3(0, .12f, .06f), new Vector3(.19f, .27f, .24f), 17, .32f, full: true);
            Surf.Lump(mb, Hide, new Vector3(0, .02f, .24f), new Vector3(.12f, .12f, .13f), 18, .3f, full: true);
            Surf.Lump(mb, Bark, new Vector3(0, .19f, .22f), new Vector3(.17f, .05f, .07f), 19, .4f, full: true);
            foreach (var sx in new[] { -1f, 1f })
                Surf.Lump(mb, Eyes, new Vector3(sx * .075f, .135f, .275f), new Vector3(.022f, .018f, .015f), 20, .05f, full: true);
            Shag(mb, 30, y => new Vector3(0, y, 0), .05f, .36f, .2f, .22f, .25f, .6f);
            Branch(mb, new Vector3(-.08f, .34f, 0), new Vector3(-.55f, 1.05f, -.1f), .05f, 3);
            Branch(mb, new Vector3(.09f, .33f, -.02f), new Vector3(.45f, 1.15f, .05f), .05f, 3);
            Branch(mb, new Vector3(0, .36f, -.08f), new Vector3(.05f, 1.2f, -.35f), .045f, 2);
            Branch(mb, new Vector3(-.12f, .25f, -.1f), new Vector3(-.75f, .55f, -.3f), .03f, 2);
            Surf.Lump(mb, SnowSub, new Vector3(0, .38f, 0), new Vector3(.12f, .035f, .12f), 21, .3f);
            Part(head, mb, mats);

            foreach (var side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "L" : "R";
                var arm = Bone(chest, "Arm" + s, new Vector3(side * .56f, .44f, 0));
                var fore = Bone(arm, "Forearm" + s, new Vector3(0, -1.35f, 0));
                var hand = Bone(fore, "Hand" + s, new Vector3(0, -1.3f, 0));

                mb = New();
                Limb(mb, Hide, new[] { Vector3.zero, new Vector3(side * -.02f, -.7f, .03f), new Vector3(0, -1.35f, 0) }, t => Mathf.Lerp(.15f, .1f, t) * (1f + .25f * Mathf.Exp(-Mathf.Pow((t - .9f) * 8f, 2))), 1f, 1f, 30 + (int)side);
                Shag(mb, 40, y => new Vector3(0, y, 0), -1.25f, 0f, .15f, .15f, .25f, .5f);
                Part(arm, mb, mats);

                mb = New();
                Limb(mb, Bark, new[] { Vector3.zero, new Vector3(side * .02f, -.65f, .03f), new Vector3(0, -1.3f, 0) }, t => Mathf.Lerp(.1f, .065f, t), 1f, 1f, 40 + (int)side);
                Shag(mb, 18, y => new Vector3(0, y, 0), -.8f, 0f, .1f, .1f, .15f, .35f);
                Branch(mb, new Vector3(side * .06f, -.4f, -.05f), new Vector3(side * .45f, -.1f, -.25f), .025f, 1);
                Part(fore, mb, mats);

                // hand: bony palm and five long twig fingers
                mb = New();
                Surf.Lump(mb, Bark, new Vector3(0, -.1f, .02f), new Vector3(.08f, .13f, .05f), 50 + (int)side, .3f, full: true);
                for (int f = 0; f < 5; f++)
                {
                    float spread = (f - 2) * .035f;
                    bool thumb = f == 0;
                    var a = new Vector3(side * (thumb ? .07f : spread), thumb ? -.1f : -.2f, .03f);
                    float len = thumb ? .3f : R(.5f, .62f);
                    var dir = new Vector3(side * (thumb ? .5f : spread * 3f), -1f, thumb ? .5f : .25f).normalized;
                    var mid = a + dir * len * .55f + new Vector3(0, 0, .06f);
                    var tip = a + dir * len + new Vector3(0, .02f, .18f);
                    Surf.Sweep(mb, Bark, new[] { a, mid, tip }, (t, ang) => Mathf.Lerp(.022f, .005f, t) * (1f + .3f * Mathf.Exp(-Mathf.Pow((t - .5f) * 6f, 2))), 5, 6,
                        new Surf.Options { UvScale = new Vector2(.3f, 2f) }, capStart: false, capEnd: true);
                }
                Part(hand, mb, mats);

                var thigh = Bone(hips, "Thigh" + s, new Vector3(side * .26f, -.05f, 0));
                var shin = Bone(thigh, "Shin" + s, new Vector3(0, -1.03f, 0));
                var foot = Bone(shin, "Foot" + s, new Vector3(0, -1.0f, 0));

                mb = New();
                Limb(mb, Hide, new[] { Vector3.zero, new Vector3(0, -.5f, .04f), new Vector3(0, -1.03f, 0) }, t => Mathf.Lerp(.18f, .11f, t), 1f, 1.1f, 60 + (int)side);
                Shag(mb, 40, y => new Vector3(0, y, 0), -1f, 0f, .18f, .19f, .25f, .55f);
                Part(thigh, mb, mats);

                mb = New();
                Limb(mb, Bark, new[] { Vector3.zero, new Vector3(0, -.5f, -.03f), new Vector3(0, -1f, 0) }, t => Mathf.Lerp(.11f, .075f, t), 1f, 1.1f, 70 + (int)side);
                Shag(mb, 20, y => new Vector3(0, y, 0), -.6f, 0f, .11f, .12f, .2f, .45f);
                Part(shin, mb, mats);

                // foot: long, root-like toes splayed over the snow
                mb = New();
                Surf.Lump(mb, Bark, new Vector3(0, .02f, .1f), new Vector3(.11f, .08f, .22f), 80 + (int)side, .35f, full: true);
                for (int f = 0; f < 4; f++)
                {
                    float spread = (f - 1.5f) * .045f;
                    var a = new Vector3(spread, .02f, .25f);
                    var tip = a + new Vector3(spread * 2.5f, -.03f, R(.18f, .28f));
                    Surf.Sweep(mb, Bark, new[] { a, (a + tip) * .5f + Vector3.up * .02f, tip }, (t, ang) => Mathf.Lerp(.03f, .008f, t), 5, 4,
                        new Surf.Options { UvScale = new Vector2(.3f, 2f) }, capStart: false, capEnd: true);
                }
                Part(foot, mb, mats);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Menk.prefab");
            Object.DestroyImmediate(root);
        }

        static MeshBuilder New() => new MeshBuilder(4);

        static Transform Bone(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static void Part(Transform bone, MeshBuilder mb, Material[] mats)
        {
            string name = bone.name;
            var mesh = mb.ToMesh("Menk_" + name);
            string path = $"{MeshDir}/Menk_{name}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            bone.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = bone.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Knobbly limb along a path: radius with bark-like ridges and knots.</summary>
        static void Limb(MeshBuilder mb, int sub, Vector3[] path, System.Func<float, float> radius, float sx, float sz, int seed)
        {
            var off = new Vector3(seed * 1.7f, seed * .3f, seed * 2.9f);
            Surf.Sweep(mb, sub, path, (t, ang) =>
            {
                var d = new Vector3(Mathf.Cos(ang), t * 4f, Mathf.Sin(ang));
                float n = 1f + .16f * Noise.Fbm(d * 2.2f + off, 3) + .07f * Mathf.Abs(Noise.N(new Vector3(ang * 4f, t * 22f, 0) + off));
                return radius(t) * n;
            }, 12, 14, new Surf.Options { UvScale = new Vector2(1f, 1.5f) }, crossSection: (t, ang) => new Vector2(Mathf.Cos(ang) * sx, Mathf.Sin(ang) * sz));
        }

        /// <summary>Hanging shaggy hair: thin tapering strands pointing outward and down around a vertical body segment.</summary>
        static void Shag(MeshBuilder mb, int count, System.Func<float, Vector3> axis, float y0, float y1, float rx, float rz, float minLen, float maxLen)
        {
            for (int i = 0; i < count; i++)
            {
                float y = R(Mathf.Min(y0, y1), Mathf.Max(y0, y1)), a = R(0f, Mathf.PI * 2f);
                var c = axis(y);
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var p = c + new Vector3(outward.x * rx, 0, outward.z * rz) * .95f;
                var dir = (outward * R(.25f, .6f) + Vector3.down + new Vector3(R(-.2f, .2f), 0, R(-.2f, .2f))).normalized;
                float len = R(minLen, maxLen);
                var mid = p + dir * len * .5f + outward * .03f;
                mb.Tube(Hide, p, mid, R(.03f, .05f), .02f, 4, 1f);
                mb.Tube(Hide, mid, mid + (dir + Vector3.down * .4f).normalized * len * .5f, .02f, .002f, 4, 1f);
            }
        }

        /// <summary>A dry crooked branch with a few side twigs.</summary>
        static void Branch(MeshBuilder mb, Vector3 from, Vector3 to, float r0, int twigs)
        {
            var mid = Vector3.Lerp(from, to, .5f) + new Vector3(R(-.12f, .12f), R(-.05f, .1f), R(-.12f, .12f));
            var path = new[] { from, mid, to + new Vector3(R(-.08f, .08f), 0, R(-.08f, .08f)) };
            Surf.Sweep(mb, Bark, path, (t, ang) => Mathf.Lerp(r0, r0 * .15f, t), 6, 8, new Surf.Options { UvScale = new Vector2(.4f, 2f) }, capStart: false, capEnd: true);
            for (int i = 0; i < twigs; i++)
            {
                float t = R(.3f, .8f);
                var p = Surf.CatmullPath(path, t);
                var side = Vector3.Cross((to - from).normalized, new Vector3(R(-1f, 1f), R(-.3f, .3f), R(-1f, 1f))).normalized;
                var tip = p + (side + (to - from).normalized * .6f).normalized * (to - from).magnitude * R(.25f, .45f);
                Surf.Sweep(mb, Bark, new[] { p, (p + tip) * .5f + Vector3.up * .03f, tip }, (u, ang) => Mathf.Lerp(r0 * .45f * (1f - t * .5f), .003f, u), 4, 4,
                    new Surf.Options { UvScale = new Vector2(.3f, 2f) }, capStart: false, capEnd: true);
            }
        }
    }
}
