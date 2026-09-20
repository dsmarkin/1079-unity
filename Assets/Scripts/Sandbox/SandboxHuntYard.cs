using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;
using Height1079.Night;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>The yard of the brief (docs/SANDBOX.md §3), made of the game's own things: the camp fire of 31
    /// January on its raft of logs (<c>Site_Camp_31Jan_Fire</c>), the camp tent with the stove, the diary and the
    /// Zorkiy inside it (<c>Site_Camp_31Jan_Tent</c>), the forest's boulders and spruces for cover, windfalls to lie
    /// behind, and the rolled tent from the cargo library. Nothing here is drawn from primitives while the world
    /// exists; a clone that has not generated its world yet gets stand-ins, and the log says so.
    ///
    /// The square is 60 m, far south of the physics range so neither is in the other's picture. The fire is at the
    /// south edge — the start, and the finish: put a thing down inside its ring and it is home. The tent is at the
    /// north edge, its mouth toward the fire, and the things to bring lie inside it, where they lay in 1959. Between
    /// them the cover, staggered so from any piece the next is a short dash and the middle line is never bare; the
    /// last row stands inside the Menk's ring round the tent, which is where the hiding has to happen.</summary>
    public static class SandboxHuntYard
    {
        public const float Size = HuntRules.YardSize;
        /// <summary>Centre of the square, well south of the range (z −250): the two are not in each other's view.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, 0f, -250f);
        public static Vector3 Fire => Origin + new Vector3(0f, 0f, -Size * .5f + 5f);
        public static Vector3 Tent => Origin + new Vector3(0f, 0f, Size * .5f - 5f);
        /// <summary>Where a body is put down to start: beside the fire, facing the tent.</summary>
        public static Vector3 Spawn => Fire + new Vector3(1.3f, 1.2f, 2.1f);
        /// <summary>Top of the snow you see, world y.</summary>
        public const float SnowTop = .105f;
        const string Prefabs = "World/Prefabs/";

        public enum Cover { Rock, Spruce, Windfall }
        public struct Shelter { public Cover Kind; public Vector3 Pos; public float Height; }
        public static readonly List<Shelter> Shelters = new List<Shelter>();

        public static Transform Root { get; private set; }
        public static Light FireLight { get; private set; }
        public static Transform TentRoot { get; private set; }
        /// <summary>The things to bring, by the list's names, as objects lying where they lie; the run moves them.</summary>
        public static readonly Dictionary<string, Transform> Props = new Dictionary<string, Transform>();
        /// <summary>Where each thing lay at the start, so a new run puts it back.</summary>
        public static readonly Dictionary<string, Vector3> Home = new Dictionary<string, Vector3>();
        /// <summary>How tall each thing is, for putting it down on the snow.</summary>
        public static readonly Dictionary<string, float> Heights = new Dictionary<string, float>();
        /// <summary>True when the game's prefabs were there; false means stand-ins.</summary>
        public static bool RealObjects { get; private set; }

        static Material snow, rock;

        /// <summary>The cover, as x/z offsets from the centre and a height. Rows between the fire (south, z −25) and
        /// the tent (north, z +25). Rocks and spruces hide a standing body; a windfall only one that is down.</summary>
        static readonly (Cover kind, float x, float z, float h)[] Plan =
        {
            (Cover.Rock,     -5f, -18f, 2.0f),
            (Cover.Windfall,  6f, -12f, 1.1f),
            (Cover.Spruce,  -12f,  -9f, 11f),
            (Cover.Rock,      0f,  -4f, 2.2f),
            (Cover.Windfall, 11f,  -1f, 1.2f),
            (Cover.Spruce,   -8f,   3f, 12f),
            (Cover.Rock,      3f,   8f, 1.9f),
            (Cover.Windfall,-13f,  11f, 1.0f),
            (Cover.Spruce,   12f,  12f, 10f),
            (Cover.Rock,     -3f,  17f, 2.1f),
            (Cover.Spruce,    8f,  21f, 11f),
            (Cover.Rock,     -9f,  23f, 1.8f),
            // the east side, so the way back can be taken wide of the ring
            (Cover.Windfall, 18f, -14f, 1.0f),
            (Cover.Rock,     17f,  -7f, 2.0f),
            (Cover.Spruce,   27f,  -8f, 11f),
            (Cover.Rock,     28f,  10f, 1.9f),
            (Cover.Windfall, 17f,   6f, 1.1f),
        };

        public static void Build(Material snowMat, Material rockMat)
        {
            Clear();
            snow = snowMat; rock = rockMat;
            Root = new GameObject("HuntYard").transform;
            RealObjects = Resources.Load<GameObject>(Prefabs + "Sites/Site_Camp_31Jan_Fire") != null;
            if (!RealObjects) Debug.LogWarning("1079 sandbox: мир не сгенерирован (нет World/Prefabs) — площадка собрана из заглушек. Меню 1079 → Rebuild world.");

            // the square: a plate under a skin of snow, the same idea as the range's yard
            var ground = Box("HuntGround", Origin + new Vector3(0f, -1f, 0f), new Vector3(Size, 2f, Size), snow);
            ground.AddComponent<Grip>().Hold = 1f;
            var skin = Box("HuntSnow", Origin + new Vector3(0f, .055f, 0f), new Vector3(Size * .995f, .1f, Size * .995f), snow);
            var sc = skin.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Object.Destroy(sc); }
            SandboxTerrainSnow.Skin(new Bounds(Origin + new Vector3(0f, .08f, 0f), new Vector3(Size, .05f, Size)));

            FireAt(Fire);
            TentAt(Tent);
            var rnd = new System.Random(1959);
            foreach (var s in Plan)
            {
                var at = Origin + new Vector3(s.x, 0f, s.z);
                Shelters.Add(new Shelter { Kind = s.kind, Pos = at, Height = s.h });
                float yaw = (float)rnd.NextDouble() * 360f;
                switch (s.kind)
                {
                    case Cover.Rock: Rock(at, s.h, yaw, rnd.Next(4)); break;
                    case Cover.Spruce: Spruce(at, s.h, yaw, rnd.Next(5)); break;
                    case Cover.Windfall: Windfall(at, s.h, yaw, rnd.Next(3)); break;
                }
            }
        }

        public static void Clear()
        {
            Shelters.Clear(); Props.Clear(); Home.Clear(); Heights.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null; FireLight = null; TentRoot = null;
        }

        static GameObject Load(string path) => Resources.Load<GameObject>(Prefabs + path);

        static Transform Put(GameObject prefab, Vector3 at, float yaw, float scale = 1f)
        {
            var t = Object.Instantiate(prefab, at, Quaternion.Euler(0f, yaw, 0f), Root).transform;
            t.name = prefab.name;
            if (scale != 1f) t.localScale = Vector3.one * scale;
            return t;
        }

        // ── the fire: the start and the finish ──

        static void FireAt(Vector3 at)
        {
            var prefab = Load("Sites/Site_Camp_31Jan_Fire");
            Transform fire = prefab != null ? Put(prefab, at + Vector3.up * (SnowTop - .02f), 0f) : FireFallback(at);
            // the cedar over it: the fire of the night was under the cedar, and a tree this size is the landmark
            // the base is found by from anywhere on the yard
            var cedar = Load("Trees/HeroCedar");
            if (cedar != null) Put(cedar, at + new Vector3(-3.2f, 0f, -2.6f), 40f, .75f);
            // the half-burnt stack and the embers are part of the site; the game lights them the same way
            var embers = fire.Find("Embers");
            if (embers != null) { var r = embers.GetComponent<Renderer>(); if (r != null) r.enabled = true; }
            CampSmoke.CreateFlames(fire, new Vector3(0f, .22f, 0f));
            CampSmoke.Create("FireSmoke", fire, new Vector3(0f, .9f, 0f), 6f, 1.1f, .9f);
            var lightGo = new GameObject("FireLight", typeof(Light));
            lightGo.transform.SetParent(fire, false); lightGo.transform.localPosition = new Vector3(0f, .9f, 0f);
            FireLight = lightGo.GetComponent<Light>();
            FireLight.type = LightType.Point; FireLight.color = new Color(1f, .64f, .28f); FireLight.range = 24f; FireLight.intensity = 3.2f;
            FireLight.shadows = LightShadows.Soft;
            // the finish, drawn on the snow: the ring a thing has to be put down inside
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "FireRadius";
            ring.transform.SetParent(Root, true);
            ring.transform.position = at + Vector3.up * (SnowTop + .006f);
            ring.transform.localScale = new Vector3(HuntRules.FireRadius * 2f, .003f, HuntRules.FireRadius * 2f);
            var rm = new Material(Shader.Find("Standard")) { color = new Color(.78f, .66f, .48f) };
            rm.SetFloat("_Glossiness", 0f);
            ring.GetComponent<Renderer>().sharedMaterial = rm;
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(ring.GetComponent<Collider>());
        }

        // ── the tent, and the things inside it ──

        /// <summary>The camp tent of 31 January, turned so its doorway faces the fire. The list's things are taken
        /// out of it as objects of their own — the stove off its wires, the diary and the camera from by the door —
        /// and lie where they lay; the rolled tent from the cargo library lies by the entrance.</summary>
        static void TentAt(Vector3 at)
        {
            var prefab = Load("Sites/Site_Camp_31Jan_Tent");
            if (prefab == null) { TentFallback(at); return; }
            // the prefab's +Z is its doorway; the fire is to the south
            TentRoot = Put(prefab, at + Vector3.up * (SnowTop - .02f), 180f);
            // the stove comes out of the tent as a thing of its own, standing where it hung; the diary and the
            // Zorkiy stay where they lay — the night is about the stove
            Extract("печка", "Stove", "StoveDoor");
        }

        /// <summary>Lifts the parts of the tent whose names start with <paramref name="prefixes"/> out into an
        /// object of their own, standing where they were. The site's parts are named by what they are made of
        /// (<c>Stove</c>, <c>DiaryCover</c>, <c>CamBody</c> …), which is what makes this possible.</summary>
        static void Extract(string item, params string[] prefixes)
        {
            if (TentRoot == null) return;
            var parts = new List<Transform>();
            foreach (var tr in TentRoot.GetComponentsInChildren<Transform>(true))
            {
                if (tr.GetComponent<MeshRenderer>() == null) continue;
                foreach (var p in prefixes) if (tr.name.StartsWith(p)) { parts.Add(tr); break; }
            }
            if (parts.Count == 0) { Debug.LogWarning("1079 sandbox: в палатке нет частей для «" + item + "»"); return; }
            Register(item, Wrap(parts, "Item " + item));
        }

        /// <summary>One object at the foot of a set of parts, the parts under it, world positions kept.</summary>
        static Transform Wrap(List<Transform> parts, string name)
        {
            Bounds b = default; bool any = false;
            foreach (var t in parts) { var r = t.GetComponent<Renderer>(); if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            var root = new GameObject(name).transform;
            root.SetParent(Root, true);
            root.position = new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var t in parts) t.SetParent(root, true);
            Heights[name.StartsWith("Item ") ? name.Substring(5) : name] = b.size.y;
            return root;
        }

        static Transform Wrap(Transform single, string item)
        {
            var parts = new List<Transform>();
            foreach (var r in single.GetComponentsInChildren<Renderer>(true)) parts.Add(r.transform);
            if (parts.Count == 0) return single;
            var root = Wrap(parts, "Item " + item);
            single.SetParent(root, true);
            return root;
        }

        static void Register(string item, Transform root)
        {
            if (root == null) return;
            Props[item] = root;
            Home[item] = root.position;
            if (!Heights.ContainsKey(item)) Heights[item] = .3f;
        }

        // ── cover ──

        static void Rock(Vector3 at, float h, float yaw, int variant)
        {
            var prefab = Load("Rocks/Boulder_" + Mathf.Clamp(variant, 0, 3));
            if (prefab == null) { RockFallback(at, h, yaw); return; }
            // the library's boulders are knee to waist high; the yard wants a man's height, so they are scaled up
            float[] libHeight = { .7f, .9f, .6f, 1.1f };
            float k = h / libHeight[Mathf.Clamp(variant, 0, 3)];
            Put(prefab, at + Vector3.up * (h * .05f), yaw, k);
        }

        static void Spruce(Vector3 at, float h, float yaw, int variant)
        {
            var prefab = Load("Trees/Spruce_" + Mathf.Clamp(variant, 0, 4));
            if (prefab == null) { SpruceFallback(at, h); return; }
            var t = Put(prefab, at, yaw, h / 16f);
            CoverCollider(t);
        }

        static void Windfall(Vector3 at, float h, float yaw, int variant)
        {
            var prefab = Load("Trees/Windfall_" + Mathf.Clamp(variant, 0, 2));
            if (prefab == null) { DriftFallback(at, h); return; }
            var t = Put(prefab, at, yaw);
            CoverCollider(t);
        }

        /// <summary>Trees carry a trunk capsule and nothing for the boughs, so a line of sight went straight
        /// through a spruce. A convex hull of the nearest LOD, as a trigger: it stops the Menk's eye and not the
        /// body — a man pushes through boughs, he does not walk through a line of sight.</summary>
        static void CoverCollider(Transform t)
        {
            MeshFilter best = null;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.EndsWith("LOD0") || best == null) best = mf;
            if (best == null || best.sharedMesh == null) return;
            var mc = best.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = best.sharedMesh; mc.convex = true; mc.isTrigger = true;
        }

        /// <summary>The nearest cover to a point, for the panel and the shots.</summary>
        public static Shelter Nearest(Vector3 p)
        {
            Shelter best = default; float bestD = float.MaxValue;
            foreach (var s in Shelters) { float d = Vector3.Distance(new Vector3(s.Pos.x, 0, s.Pos.z), new Vector3(p.x, 0, p.z)); if (d < bestD) { bestD = d; best = s; } }
            return best;
        }

        // ── stand-ins for a clone without a generated world ──

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

        static Transform FireFallback(Vector3 at)
        {
            var root = new GameObject("FireFallback").transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop;
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                Lump("Stone" + i, root.position + new Vector3(Mathf.Cos(a) * .95f, .08f, Mathf.Sin(a) * .95f), new Vector3(.34f, .22f, .28f), rock).transform.SetParent(root, true);
            }
            var log = new Material(Shader.Find("Standard")) { color = new Color(.2f, .14f, .1f) };
            for (int i = 0; i < 3; i++)
            {
                var l = Box("Log" + i, root.position + new Vector3(0f, .16f, 0f), new Vector3(.16f, .16f, 1.1f), log);
                l.transform.SetParent(root, true); l.transform.rotation = Quaternion.Euler(0f, i * 60f + 20f, 0f);
                Object.Destroy(l.GetComponent<Collider>());
            }
            var ember = Box("Embers", root.position + new Vector3(0f, .12f, 0f), new Vector3(.7f, .06f, .7f), new Material(Shader.Find("Standard")) { color = new Color(.9f, .35f, .08f) });
            ember.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
            ember.GetComponent<Renderer>().sharedMaterial.SetColor("_EmissionColor", new Color(1.6f, .5f, .1f));
            ember.transform.SetParent(root, true);
            Object.Destroy(ember.GetComponent<Collider>());
            return root;
        }

        static void TentFallback(Vector3 at)
        {
            var canvas = new Material(Shader.Find("Standard")) { color = new Color(.42f, .45f, .38f) };
            TentRoot = new GameObject("TentFallback").transform;
            TentRoot.SetParent(Root, true); TentRoot.position = at;
            const float len = 4.3f, half = 1.0f, ridge = 1.05f;
            float slope = Mathf.Sqrt(half * half + ridge * ridge), tilt = Mathf.Atan2(half, ridge) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var slab = Box("TentSide", at + new Vector3(side * half * .5f, ridge * .5f + SnowTop, 0f), new Vector3(.05f, slope, len), canvas);
                slab.transform.SetParent(TentRoot, true); slab.transform.rotation = Quaternion.Euler(0f, 0f, -side * tilt);
            }
            var back = Box("TentBack", at + new Vector3(0f, ridge * .45f + SnowTop, len * .5f), new Vector3(half * 2f, ridge * .9f, .05f), canvas);
            back.transform.SetParent(TentRoot, true);
            // the things, as blocks, where the list puts them
            var toFire = (Fire - at); toFire.y = 0f; toFire.Normalize();
            var side2 = Vector3.Cross(Vector3.up, toFire);
            Stand("печка", at - toFire * 1.4f, new Vector3(.3f, .3f, .5f), new Color(.45f, .45f, .47f));
        }

        static void Stand(string item, Vector3 at, Vector3 size, Color c)
        {
            var go = Box("Item " + item, at + Vector3.up * (SnowTop + size.y * .5f), size, new Material(Shader.Find("Standard")) { color = c });
            Object.Destroy(go.GetComponent<Collider>());
            var root = new GameObject("Item " + item).transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop;
            go.transform.SetParent(root, true);
            Heights[item] = size.y;
            Register(item, root);
        }

        static void RockFallback(Vector3 at, float h, float yaw)
        {
            var main = Lump("Rock", at + new Vector3(0f, h * .42f, 0f), new Vector3(2.6f, h, 2.0f), rock);
            main.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cap = Lump("RockSnow", at + new Vector3(0f, h * .78f, 0f), new Vector3(2.0f, .3f, 1.5f), snow);
            Object.Destroy(cap.GetComponent<Collider>());
        }

        static void SpruceFallback(Vector3 at, float h)
        {
            var spruce = new Material(Shader.Find("Standard")) { color = new Color(.07f, .13f, .085f) };
            var trunk = new Material(Shader.Find("Standard")) { color = new Color(.24f, .18f, .13f) };
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Trunk"; t.transform.SetParent(Root, true);
            t.transform.position = at + Vector3.up * (h * .5f); t.transform.localScale = new Vector3(.36f, h * .5f, .36f);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            float[] skirtY = { .25f, h * .36f, h * .62f }; float[] skirtR = { h * .30f, h * .24f, h * .16f }; float[] skirtH = { h * .42f, h * .38f, h * .40f };
            for (int i = 0; i < 3; i++)
            {
                var c = new GameObject("Skirt" + i, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                c.transform.SetParent(Root, true); c.transform.position = at + Vector3.up * skirtY[i];
                var m = Cone(skirtR[i], skirtH[i], 14);
                c.GetComponent<MeshFilter>().sharedMesh = m; c.GetComponent<MeshRenderer>().sharedMaterial = spruce;
                var mc = c.GetComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true; mc.isTrigger = true;
            }
        }

        static Mesh Cone(float radius, float height, int segments)
        {
            var verts = new Vector3[segments + 2]; var tris = new int[segments * 6];
            verts[0] = Vector3.zero; verts[segments + 1] = Vector3.up * height;
            for (int i = 0; i < segments; i++) { float a = i / (float)segments * Mathf.PI * 2f; verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius); }
            for (int i = 0; i < segments; i++)
            {
                int a = i + 1, b = (i + 1) % segments + 1;
                tris[i * 6] = 0; tris[i * 6 + 1] = a; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = segments + 1; tris[i * 6 + 4] = b; tris[i * 6 + 5] = a;
            }
            var m = new Mesh { name = "cone" }; m.vertices = verts; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void DriftFallback(Vector3 at, float h)
        {
            var d = Lump("Drift", at + new Vector3(0f, -h * .15f, 0f), new Vector3(7.5f, h * 2.3f, 4.6f), snow);
            d.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            d.AddComponent<Grip>().Hold = 1f;
        }
    }
}
