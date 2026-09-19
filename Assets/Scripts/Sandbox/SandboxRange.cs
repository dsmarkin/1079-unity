using System.Collections.Generic;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>The test range: one flat yard and a row of stands, each of which asks the body one question — can it
    /// walk up this, does it catch on that, what does a hold on ice cost, what happens when it falls from sixteen
    /// metres. Everything is built from primitives at start-up; a stand is reached by its number key.
    ///
    /// North of the yard the ground stops being a measuring device and becomes ground: a rolling snow field with
    /// drifts, a flank and a ditch in it (<see cref="SandboxTerrainSnow"/>). That half exists because a range you can
    /// measure a leg spring on is not the same thing as a place that feels like the mountain the game is set on, and
    /// the sandbox is supposed to be both.</summary>
    public static class SandboxRange
    {
        public struct Stand
        {
            public string Name;
            public Vector3 Spawn;
        }

        public static readonly List<Stand> Stands = new List<Stand>();

        /// <summary>One ramp of the slope stand, with the two places worth standing on it: the foot, to walk up from,
        /// and the middle, to be put on and see whether the boots hold. The self-test reads these.</summary>
        public struct Ramp
        {
            public float Degrees;
            public Vector3 Foot, Middle;
        }

        public static readonly List<Ramp> Ramps = new List<Ramp>();

        static Material rock, ice, ledge, snow, mark, distant;

        /// <summary>Where the three snow stands sit in <see cref="Stands"/>. The shot script and anything else that
        /// wants to put a body in the drifts asks for them by name instead of counting.</summary>
        public static int DriftStand { get; private set; } = -1;
        public static int FlankStand { get; private set; } = -1;
        public static int DitchStand { get; private set; } = -1;

        public static void Build()
        {
            Stands.Clear();
            Ramps.Clear();
            SandboxTerrainSnow.ClearPrints();
            // stone, not office grey: a warm dark rock, a colder ledge, bare ice that reads as ice at a glance,
            // and the ground the whole range stands on is snow
            rock = Mat("rock", new Color(.37f, .35f, .33f), .06f);
            ice = Mat("ice", new Color(.68f, .80f, .86f), .6f);
            ledge = Mat("ledge", new Color(.50f, .47f, .43f), .05f);
            snow = Mat("snow", new Color(.92f, .94f, .97f), .16f);
            distant = Mat("distant", new Color(.80f, .85f, .92f), .04f);
            mark = Mat("mark", new Color(.85f, .55f, .2f), .05f);

            SandboxTerrainSnow.BuildHorizon(snow, distant);
            Ground();
            Steps(new Vector3(-26f, 0f, 0f));
            Slopes(new Vector3(-10f, 0f, 0f));
            Wall(new Vector3(8f, 0f, 0f));
            Overhang(new Vector3(20f, 0f, 0f));
            Chimney(new Vector3(30f, 0f, 0f));
            Boulders(new Vector3(40f, 0f, 0f));
            Drops(new Vector3(52f, 0f, 0f));
            Movables(new Vector3(66f, 0f, 0f));
            Snow();
        }

        static void Ground()
        {
            var g = Box("Ground", new Vector3(0f, -1f, 0f), new Vector3(200f, 2f, 60f), snow);
            g.AddComponent<Grip>().Hold = 1f;
            Stands.Add(new Stand { Name = "1 · ровное место", Spawn = new Vector3(0f, 1.2f, -10f) });
        }

        /// <summary>The snow field north of the yard: drifts, a flank that steepens as it is climbed, and a ditch.
        /// Built last, and its stands added to the end of <see cref="Stands"/>, so nothing that counts the old ones
        /// shifts under it.
        ///
        /// The yard itself stays a flat plate on purpose. It is the ruler the self-test reads support height and
        /// slope off, and ground that rolls under a ruler is not a ruler — walking on the uneven lives out here.</summary>
        static void Snow()
        {
            SandboxTerrainSnow.BuildField(snow);
            DriftStand = Stands.Count;
            Stands.Add(new Stand { Name = "10 · сугробы", Spawn = SandboxTerrainSnow.DriftFoot });
            FlankStand = Stands.Count;
            Stands.Add(new Stand { Name = "11 · снежный склон", Spawn = SandboxTerrainSnow.FlankFootAt });
            DitchStand = Stands.Count;
            Stands.Add(new Stand { Name = "12 · канава", Spawn = SandboxTerrainSnow.DitchFoot });
        }

        /// <summary>Steps of four heights: the legs should absorb the low ones without the player noticing and refuse
        /// the high ones. This is the stand that says whether the hover spring is set right.</summary>
        static void Steps(Vector3 at)
        {
            float[] rise = { .2f, .35f, .5f, .7f };
            float z = -6f;
            for (int i = 0; i < rise.Length; i++)
            {
                float h = 0f;
                for (int s = 0; s < 4; s++)
                {
                    h += rise[i];
                    var size = new Vector3(2.6f, h, 1.2f);
                    var centre = at + new Vector3(i * 3.2f, h * .5f, z + s * 1.2f);
                    Box($"Step{i}_{s}", centre, size, ledge);
                    Cap($"Step{i}_{s}Snow", centre + Vector3.up * (h * .5f), size);
                }
                // off to the side of the lane, not in it: the post has a collider, and a body that walks into it stops
                Sign($"{rise[i]:0.00} м", at + new Vector3(i * 3.2f - 1.75f, 2.2f, z - 1.4f));
            }
            Stands.Add(new Stand { Name = "2 · ступени 0,2…0,7 м", Spawn = at + new Vector3(0f, 1.2f, -9f) });
        }

        /// <summary>Slopes from a walk to a wall, six degrees apart at the point where it stops being walkable.</summary>
        static void Slopes(Vector3 at)
        {
            float[] deg = { 20f, 30f, 40f, 46f, 52f, 60f, 75f };
            for (int i = 0; i < deg.Length; i++)
            {
                var go = Box($"Slope{deg[i]:0}", at + new Vector3(i * 2.6f, 0f, 4f), new Vector3(2.4f, .4f, 9f), rock);
                var q = Quaternion.Euler(-deg[i], 0f, 0f);
                go.transform.rotation = q;
                var centre = at + new Vector3(i * 2.6f, Mathf.Sin(deg[i] * Mathf.Deg2Rad) * 4.5f, 4f);
                go.transform.position = centre;
                var face = centre + q * Vector3.up * .22f;          // the walked surface
                var upSlope = q * Vector3.forward;                   // the way up
                Ramps.Add(new Ramp
                {
                    Degrees = deg[i],
                    Foot = face - upSlope * 4.2f + Vector3.up * 1.15f,
                    Middle = face + Vector3.up * 1.15f,
                });
                Sign($"{deg[i]:0}°", at + new Vector3(i * 2.6f - 1.45f, .6f, -1.6f));
            }
            Stands.Add(new Stand { Name = "3 · склоны 20…75°", Spawn = at + new Vector3(0f, 1.2f, -4f) });
        }

        /// <summary>Twelve metres of wall with holds every metre and a half: the climb itself. The holds get thinner
        /// higher up, and the top three are ice — the stamina should decide whether the last move is possible.</summary>
        static void Wall(Vector3 at)
        {
            Box("WallFace", at + new Vector3(0f, 6f, 2f), new Vector3(10f, 12f, 1f), rock);
            for (int row = 0; row < 8; row++)
            {
                float y = 1.4f + row * 1.4f;
                int holds = row % 2 == 0 ? 3 : 2;
                for (int i = 0; i < holds; i++)
                {
                    float x = (i - (holds - 1) * .5f) * 1.6f + (row % 2 == 0 ? 0f : .8f);
                    float depth = Mathf.Lerp(.32f, .14f, row / 7f);
                    bool icy = row >= 6;
                    var hold = Box($"Hold{row}_{i}", at + new Vector3(x, y, 1.3f), new Vector3(.7f, .16f, depth), icy ? ice : ledge);
                    var g = hold.AddComponent<Grip>();
                    g.Hold = icy ? .45f : Mathf.Lerp(1.3f, .8f, row / 7f);
                    g.Cost = icy ? 2.2f : 1f;
                }
            }
            Box("WallTop", at + new Vector3(0f, 12.1f, 3.4f), new Vector3(10f, .4f, 4f), ledge);
            Stands.Add(new Stand { Name = "4 · стена 12 м с зацепами", Spawn = at + new Vector3(0f, 1.2f, -3f) });
        }

        /// <summary>A roof to hang under: the one thing that cannot be faked by a capsule controller.</summary>
        static void Overhang(Vector3 at)
        {
            Box("OverBase", at + new Vector3(0f, 2.5f, 2f), new Vector3(8f, 5f, 1f), rock);
            var roof = Box("Roof", at + new Vector3(0f, 5.2f, .2f), new Vector3(8f, .5f, 3.4f), rock);
            roof.AddComponent<Grip>().Hold = 1.2f;
            for (int i = 0; i < 5; i++)
            {
                var h = Box($"RoofHold{i}", at + new Vector3(-2.4f + i * 1.2f, 4.9f, -1.1f), new Vector3(.6f, .18f, .5f), ledge);
                h.AddComponent<Grip>().Hold = 1.1f;
            }
            Stands.Add(new Stand { Name = "5 · карниз", Spawn = at + new Vector3(0f, 1.2f, -4f) });
        }

        /// <summary>Two walls a body's width apart — the climb that is done with the back and the knees. It tells you
        /// whether the torso collider fights the hands.</summary>
        static void Chimney(Vector3 at)
        {
            Box("ChimA", at + new Vector3(-.9f, 4f, 0f), new Vector3(.8f, 8f, 3f), rock);
            Box("ChimB", at + new Vector3(.9f, 4f, 0f), new Vector3(.8f, 8f, 3f), rock);
            Box("ChimBack", at + new Vector3(0f, 4f, 1.6f), new Vector3(2.6f, 8f, .4f), rock);
            Stands.Add(new Stand { Name = "6 · камин (щель)", Spawn = at + new Vector3(0f, 1.2f, -3f) });
        }

        /// <summary>Loose ground: boulders to scramble over, the shape a mountain actually has.</summary>
        static void Boulders(Vector3 at)
        {
            var rnd = new System.Random(1079);
            for (int i = 0; i < 26; i++)
            {
                float s = .5f + (float)rnd.NextDouble() * 2.2f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Boulder" + i;
                go.transform.position = at + new Vector3(((float)rnd.NextDouble() - .5f) * 10f, s * .35f, ((float)rnd.NextDouble()) * 9f);
                go.transform.localScale = new Vector3(s, s * .7f, s * 1.1f);
                go.GetComponent<Renderer>().sharedMaterial = rock;
                go.AddComponent<Grip>().Hold = .9f;
            }
            Stands.Add(new Stand { Name = "7 · валуны", Spawn = at + new Vector3(0f, 1.2f, -3f) });
        }

        /// <summary>Platforms at four, eight and sixteen metres: what a fall costs and how the body lands.</summary>
        static void Drops(Vector3 at)
        {
            float[] h = { 4f, 8f, 16f };
            for (int i = 0; i < h.Length; i++)
            {
                Box($"Pillar{i}", at + new Vector3(i * 4f, h[i] * .5f, 0f), new Vector3(1.2f, h[i], 1.2f), rock);
                Box($"Pad{i}", at + new Vector3(i * 4f, h[i] + .2f, 0f), new Vector3(3f, .4f, 3f), ledge);
                Cap($"Pad{i}Snow", at + new Vector3(i * 4f, h[i] + .4f, 0f), new Vector3(3f, .4f, 3f));
                Sign($"{h[i]:0} м", at + new Vector3(i * 4f - 1.9f, h[i] + 1.2f, -1.8f));
            }
            Stands.Add(new Stand { Name = "8 · падения 4/8/16 м", Spawn = at + new Vector3(0f, 4.6f, 0f) });
        }

        /// <summary>Things that move when they are held: a crate, a swinging beam. A grip that only works on static
        /// geometry is not a grip.</summary>
        static void Movables(Vector3 at)
        {
            for (int i = 0; i < 4; i++)
            {
                var crate = Box($"Crate{i}", at + new Vector3(-2f + i * 1.4f, .5f + i * .1f, 0f), new Vector3(.8f, .8f, .8f), ledge);
                var rb = crate.AddComponent<Rigidbody>();
                rb.mass = 12f + i * 8f;
                crate.AddComponent<Grip>().Hold = 1f;
            }
            var post = Box("Post", at + new Vector3(3f, 2.5f, 0f), new Vector3(.3f, 5f, .3f), rock);
            var beam = Box("Beam", at + new Vector3(3f, 4.6f, 1.4f), new Vector3(.25f, .25f, 3f), ledge);
            var beamRb = beam.AddComponent<Rigidbody>();
            beamRb.mass = 30f;
            var hinge = beam.AddComponent<HingeJoint>();
            hinge.connectedBody = null;
            hinge.anchor = new Vector3(0f, 0f, -1.4f);
            hinge.axis = Vector3.right;
            beam.AddComponent<Grip>().Hold = 1.1f;
            Stands.Add(new Stand { Name = "9 · подвижные предметы", Spawn = at + new Vector3(0f, 1.2f, -3f) });
        }

        static GameObject Box(string name, Vector3 centre, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A painted stripe standing where a label would be: readable at a glance, and no font to ship.</summary>
        static void Sign(string text, Vector3 at)
        {
            var go = new GameObject("Sign " + text);
            go.transform.position = at;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.characterSize = .16f; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(.1f, .1f, .12f);
            var post = Box("Post", at - Vector3.up * .5f, new Vector3(.06f, 1f, .06f), mark);
            post.transform.SetParent(go.transform, true);
        }

        static Material Mat(string name, Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = c };
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>Snow lying on top of something, as a skin with no collider. A real slab on a step would raise that
        /// step by its own thickness, and the height of those steps is exactly what the self-test measures — so this
        /// is only ever looked at, never stood on, and the boots go through it the way they go into snow.</summary>
        static void Cap(string name, Vector3 top, Vector3 size)
        {
            var go = Box(name, top + Vector3.up * .025f, new Vector3(size.x * .98f, .05f, size.z * .98f), snow);
            var c = go.GetComponent<Collider>();
            if (c != null) { c.enabled = false; Object.Destroy(c); }
        }
    }
}
