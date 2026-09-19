using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>The yard of the brief (docs/SANDBOX.md §3): a flat snow square, the fire at one edge, the tent at
    /// the other, and between them the shelters — rocks a man's height, spruces, drifts — laid out so that from any
    /// one of them the next is a short dash away and no straight line from the tent to the fire is bare.
    ///
    /// South of the range's yard, so the measured stands are untouched. Built once with the range; the night, the
    /// Menk and the things in the tent come and go with each run (<see cref="SandboxHunt"/>).</summary>
    public static class SandboxHuntYard
    {
        public const float Size = HuntRules.YardSize;
        /// <summary>Centre of the square. z runs north; the fire is at the south edge, the tent at the north.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, 0f, -85f);
        public static Vector3 Fire => Origin + new Vector3(0f, 0f, -Size * .5f + 5f);
        public static Vector3 Tent => Origin + new Vector3(0f, 0f, Size * .5f - 5f);
        /// <summary>Where a body is put down to start: beside the fire, facing the tent.</summary>
        public static Vector3 Spawn => Fire + new Vector3(1.5f, 1.2f, 2.5f);
        /// <summary>Top of the snow you see, world y.</summary>
        public const float SnowTop = .105f;

        public enum Cover { Rock, Spruce, Drift }
        public struct Shelter { public Cover Kind; public Vector3 Pos; public float Height; }
        public static readonly List<Shelter> Shelters = new List<Shelter>();

        public static Transform Root { get; private set; }
        public static Light FireLight { get; private set; }
        public static Transform TentRoot { get; private set; }

        static Material snow, rock, canvas, spruce, trunk, log;

        /// <summary>The shelters, as x/z offsets from the yard's centre and a height. Staggered rows between the fire
        /// (south, z −25) and the tent (north, z +25): from each one the next is eight to thirteen metres off, the
        /// middle line always has a rock within fifteen metres, and the last row sits inside the Menk's ring, which
        /// is where the hiding has to happen. Drifts only hide a body that is down; rocks and spruces hide a
        /// standing one.</summary>
        static readonly (Cover kind, float x, float z, float h)[] Plan =
        {
            (Cover.Rock,   -5f, -18f, 2.0f),
            (Cover.Drift,   6f, -12f, 1.3f),
            (Cover.Spruce, -12f, -9f, 5.5f),
            (Cover.Rock,    0f,  -4f, 2.2f),
            (Cover.Drift,  11f,  -1f, 1.4f),
            (Cover.Spruce, -8f,   3f, 6.0f),
            (Cover.Rock,    3f,   8f, 1.9f),
            (Cover.Drift, -13f,  11f, 1.2f),
            (Cover.Spruce, 12f,  12f, 5.2f),
            (Cover.Rock,   -3f,  17f, 2.1f),
            (Cover.Spruce,  8f,  21f, 5.8f),
            (Cover.Rock,   -9f,  23f, 1.8f),
        };

        public static void Build(Material snowMat, Material rockMat)
        {
            Clear();
            snow = snowMat; rock = rockMat;
            canvas = Flat("canvas", new Color(.42f, .45f, .38f), .08f);
            spruce = Flat("spruce", new Color(.07f, .13f, .085f), .05f);
            trunk = Flat("trunk", new Color(.24f, .18f, .13f), .05f);
            log = Flat("log", new Color(.2f, .14f, .1f), .05f);
            Root = new GameObject("HuntYard").transform;

            // the square: a plate under a skin of snow, the same idea as the range's yard
            var ground = Box("HuntGround", Origin + new Vector3(0f, -1f, 0f), new Vector3(Size, 2f, Size), snow);
            ground.AddComponent<Grip>().Hold = 1f;
            var skin = Box("HuntSnow", Origin + new Vector3(0f, .055f, 0f), new Vector3(Size * .995f, .1f, Size * .995f), snow);
            var sc = skin.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Object.Destroy(sc); }
            SandboxTerrainSnow.Skin(new Bounds(Origin + new Vector3(0f, .08f, 0f), new Vector3(Size, .05f, Size)));

            FireAt(Fire);
            TentAt(Tent);
            foreach (var s in Plan)
            {
                var at = Origin + new Vector3(s.x, 0f, s.z);
                Shelters.Add(new Shelter { Kind = s.kind, Pos = at, Height = s.h });
                switch (s.kind)
                {
                    case Cover.Rock: Rock(at, s.h); break;
                    case Cover.Spruce: Spruce(at, s.h); break;
                    case Cover.Drift: Drift(at, s.h); break;
                }
            }
        }

        public static void Clear()
        {
            Shelters.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null; FireLight = null; TentRoot = null;
        }

        static Material Flat(string name, Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = c };
            m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0f);
            return m;
        }

        static GameObject Box(string name, Vector3 centre, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(Root, true);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A sphere primitive scaled into a lump, with a collider that follows the lump instead of the
        /// sphere's widest axis: the body has to be able to lean on the rock it is hiding behind.</summary>
        static GameObject Lump(string name, Vector3 centre, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(Root, true);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            var sphere = go.GetComponent<SphereCollider>();
            if (sphere != null) Object.Destroy(sphere);
            var mesh = go.AddComponent<MeshCollider>();
            mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
            return go;
        }

        static void FireAt(Vector3 at)
        {
            var root = new GameObject("Fire").transform;
            root.SetParent(Root, true); root.position = at;
            // a ring of stones and three logs in the ash
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var stone = Lump("Stone" + i, at + new Vector3(Mathf.Cos(a) * .95f, .08f, Mathf.Sin(a) * .95f), new Vector3(.34f, .22f, .28f), rock);
                stone.transform.SetParent(root, true);
                stone.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            for (int i = 0; i < 3; i++)
            {
                var l = Box("Log" + i, at + new Vector3(0f, .16f, 0f), new Vector3(.16f, .16f, 1.1f), log);
                l.transform.SetParent(root, true);
                l.transform.rotation = Quaternion.Euler(0f, i * 60f + 20f, 0f);
                Object.Destroy(l.GetComponent<Collider>());
            }
            var lightGo = new GameObject("FireLight", typeof(Light));
            lightGo.transform.SetParent(root, true);
            lightGo.transform.position = at + Vector3.up * .8f;
            FireLight = lightGo.GetComponent<Light>();
            FireLight.type = LightType.Point; FireLight.range = 16f; FireLight.intensity = 2.6f;
            FireLight.color = new Color(1f, .62f, .3f); FireLight.shadows = LightShadows.Soft; FireLight.shadowStrength = .8f;
            var ember = Box("Ember", at + new Vector3(0f, .12f, 0f), new Vector3(.7f, .06f, .7f), Flat("ember", new Color(.9f, .35f, .08f), 0f));
            ember.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
            ember.GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", new Color(1.6f, .5f, .1f));
            ember.transform.SetParent(root, true);
            Object.Destroy(ember.GetComponent<Collider>());
            Flames(root, at);
            // the delivery zone, drawn faintly on the snow so the radius is a thing you can see
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "FireRadius";
            ring.transform.SetParent(root, true);
            ring.transform.position = at + Vector3.up * (SnowTop + .005f);
            ring.transform.localScale = new Vector3(HuntRules.FireRadius * 2f, .004f, HuntRules.FireRadius * 2f);
            var rm = Flat("firering", new Color(.75f, .62f, .45f), 0f);
            ring.GetComponent<Renderer>().sharedMaterial = rm;
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(ring.GetComponent<Collider>());
        }

        static void Flames(Transform root, Vector3 at)
        {
            var mat = Resources.Load<Material>("World/Materials/SnowFx/Flame");
            if (mat == null) return;
            var go = new GameObject("Flames", typeof(ParticleSystem));
            go.transform.SetParent(root, true);
            go.transform.position = at + Vector3.up * .2f;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .7f);
            main.startColor = new Color(1f, .8f, .55f, 1f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 40f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = .3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .9f, .6f), 0f), new GradientColorKey(new Color(1f, .35f, .08f), .6f), new GradientColorKey(new Color(.3f, .05f, 0f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A two-man tent, ridge toward the fire, its mouth open to the south. Two slabs leaning on each
        /// other and a floor: enough to be a tent from ten metres and to stand between a body and the Menk.</summary>
        static void TentAt(Vector3 at)
        {
            TentRoot = new GameObject("Tent").transform;
            TentRoot.SetParent(Root, true); TentRoot.position = at;
            const float len = 4.2f, half = 1.35f, ridge = 1.55f;
            float slope = Mathf.Sqrt(half * half + ridge * ridge);
            float tilt = Mathf.Atan2(half, ridge) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var slab = Box("TentSide" + (side < 0 ? "L" : "R"), at + new Vector3(side * half * .5f, ridge * .5f + SnowTop, 0f), new Vector3(.05f, slope, len), canvas);
                slab.transform.SetParent(TentRoot, true);
                slab.transform.rotation = Quaternion.Euler(0f, 0f, -side * tilt);
            }
            var floor = Box("TentFloor", at + new Vector3(0f, SnowTop + .02f, 0f), new Vector3(half * 2f, .04f, len), canvas);
            floor.transform.SetParent(TentRoot, true);
            Object.Destroy(floor.GetComponent<Collider>());
            // the back wall; the front is the mouth
            var back = Box("TentBack", at + new Vector3(0f, ridge * .45f + SnowTop, len * .5f), new Vector3(half * 2f, ridge * .9f, .05f), canvas);
            back.transform.SetParent(TentRoot, true);
            // a couple of poles
            foreach (float z in new[] { -len * .5f, len * .5f })
            {
                var pole = Box("Pole", at + new Vector3(0f, ridge * .5f + SnowTop, z), new Vector3(.05f, ridge, .05f), trunk);
                pole.transform.SetParent(TentRoot, true);
                Object.Destroy(pole.GetComponent<Collider>());
            }
        }

        static void Rock(Vector3 at, float h)
        {
            var rnd = new System.Random(Mathf.RoundToInt(at.x * 7f + at.z * 13f));
            float yaw = (float)rnd.NextDouble() * 180f;
            var main = Lump("Rock", at + new Vector3(0f, h * .42f, 0f), new Vector3(2.6f, h, 2.0f), rock);
            main.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var side = Lump("RockSide", at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.1f, h * .25f, .3f), new Vector3(1.6f, h * .6f, 1.4f), rock);
            side.transform.rotation = Quaternion.Euler(0f, yaw + 25f, 0f);
            var cap = Lump("RockSnow", at + new Vector3(0f, h * .78f, 0f), new Vector3(2.0f, .3f, 1.5f), snow);
            Object.Destroy(cap.GetComponent<Collider>());
        }

        /// <summary>A spruce: a trunk and three skirts of branches, the lowest starting at the snow so a man flat
        /// behind it is under the boughs. The skirts carry colliders — they are what blocks the line of sight.</summary>
        static void Spruce(Vector3 at, float h)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Trunk"; t.transform.SetParent(Root, true);
            t.transform.position = at + Vector3.up * (h * .5f);
            t.transform.localScale = new Vector3(.36f, h * .5f, .36f);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            float[] skirtY = { .25f, h * .36f, h * .62f };
            float[] skirtR = { h * .30f, h * .24f, h * .16f };
            float[] skirtH = { h * .42f, h * .38f, h * .40f };
            for (int i = 0; i < 3; i++)
            {
                var c = new GameObject("Skirt" + i, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                c.transform.SetParent(Root, true);
                c.transform.position = at + Vector3.up * skirtY[i];
                var m = Cone(skirtR[i], skirtH[i], 14);
                c.GetComponent<MeshFilter>().sharedMesh = m;
                c.GetComponent<MeshRenderer>().sharedMaterial = spruce;
                var mc = c.GetComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true;
                // snow on the boughs
                var cap = new GameObject("SkirtSnow" + i, typeof(MeshFilter), typeof(MeshRenderer));
                cap.transform.SetParent(Root, true);
                cap.transform.position = at + Vector3.up * (skirtY[i] + skirtH[i] * .55f);
                cap.GetComponent<MeshFilter>().sharedMesh = Cone(skirtR[i] * .55f, skirtH[i] * .45f, 14);
                cap.GetComponent<MeshRenderer>().sharedMaterial = snow;
            }
        }

        static Mesh Cone(float radius, float height, int segments)
        {
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 6];
            verts[0] = Vector3.zero; verts[segments + 1] = Vector3.up * height;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i + 1, b = (i + 1) % segments + 1;
                tris[i * 6] = 0; tris[i * 6 + 1] = a; tris[i * 6 + 2] = b;               // base (faces down)
                tris[i * 6 + 3] = segments + 1; tris[i * 6 + 4] = b; tris[i * 6 + 5] = a; // side
            }
            var m = new Mesh { name = "cone" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A drift: a long low mound the wind left. Hides a body that is down, not one that stands.</summary>
        static void Drift(Vector3 at, float h)
        {
            var rnd = new System.Random(Mathf.RoundToInt(at.x * 3f + at.z * 5f));
            float yaw = 20f + (float)rnd.NextDouble() * 40f;
            var d = Lump("Drift", at + new Vector3(0f, -h * .15f, 0f), new Vector3(7.5f, h * 2.3f, 4.6f), snow);
            d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            d.AddComponent<Grip>().Hold = 1f;
        }

        /// <summary>The nearest shelter to a point, for the panel and the shots.</summary>
        public static Shelter Nearest(Vector3 p)
        {
            Shelter best = default; float bestD = float.MaxValue;
            foreach (var s in Shelters) { float d = Vector3.Distance(new Vector3(s.Pos.x, 0, s.Pos.z), new Vector3(p.x, 0, p.z)); if (d < bestD) { bestD = d; best = s; } }
            return best;
        }
    }
}
