using System.Collections.Generic;
using UnityEngine;
using Height1079.Art;
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
    /// Since the style was settled (docs/ART.md §3) the yard prefers the imported low-poly set in the palette's
    /// colours (<c>Resources/World/Prefabs/Imported</c>, Editor/World/ImportedFactory): the tent, the fire's pile and
    /// its logs, the pines, the rocks and the windfalls, the things lying by the tent. The stove, the diary and the
    /// camera are still lifted out of the game's site tent — no pack has them — and repainted in the same colours,
    /// as are the labaz and its cargo. Without the imported set the yard is what it was.
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
        /// <summary>The second section: the cache in the snow, east of the line from the fire to the tent.</summary>
        public static Vector3 Labaz => Origin + new Vector3(23f, 0f, 2f);
        /// <summary>Where a body is put down to start: beside the fire, facing the tent.</summary>
        public static Vector3 Spawn => Fire + new Vector3(1.3f, 1.2f, 2.1f);
        /// <summary>Top of the snow you see, world y.</summary>
        public const float SnowTop = .105f;
        const string Prefabs = "World/Prefabs/";
        const string Imported = "World/Prefabs/Imported/";

        public enum Cover { Rock, Spruce, Windfall }
        public struct Shelter { public Cover Kind; public Vector3 Pos; public float Height; }
        public static readonly List<Shelter> Shelters = new List<Shelter>();

        public static Transform Root { get; private set; }
        public static Light FireLight { get; private set; }
        public static Transform TentRoot { get; private set; }
        public static Transform LabazRoot { get; private set; }
        /// <summary>The things to bring, by the list's names, as objects lying where they lie; the run moves them.</summary>
        public static readonly Dictionary<string, Transform> Props = new Dictionary<string, Transform>();
        /// <summary>Where each thing lay at the start, so a new run puts it back.</summary>
        public static readonly Dictionary<string, Vector3> Home = new Dictionary<string, Vector3>();
        /// <summary>How tall each thing is, for putting it down on the snow.</summary>
        public static readonly Dictionary<string, float> Heights = new Dictionary<string, float>();
        /// <summary>True when the game's prefabs were there; false means stand-ins.</summary>
        public static bool RealObjects { get; private set; }
        /// <summary>True when the imported set in palette colours is there: the yard is then one drawing.</summary>
        public static bool Palettised { get; private set; }

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
            // the way east, to the labaz, and the labaz's own ring
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
            Palettised = Imp("Tent") != null && Imp("Bonfire") != null;
            RealObjects = Palettised || Resources.Load<GameObject>(Prefabs + "Sites/Site_Camp_31Jan_Fire") != null;
            if (!RealObjects) Debug.LogWarning("1079 sandbox: мир не сгенерирован (нет World/Prefabs) — площадка собрана из заглушек. Меню 1079 → Rebuild world.");
            if (Palettised) { snow = Palette.Flat(Palette.SnowLit); rock = Palette.Flat(Palette.Stone); }
            else Debug.Log("1079 sandbox: нет импортированных моделей (World/Prefabs/Imported) — площадка из объектов игры или заглушек.");

            // the square: a plate under a skin of snow, the same idea as the range's yard
            var ground = Box("HuntGround", Origin + new Vector3(0f, -1f, 0f), new Vector3(Size, 2f, Size), snow);
            ground.AddComponent<Grip>().Hold = 1f;
            var skin = Box("HuntSnow", Origin + new Vector3(0f, .055f, 0f), new Vector3(Size * .995f, .1f, Size * .995f), snow);
            var sc = skin.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Object.Destroy(sc); }
            SandboxTerrainSnow.Skin(new Bounds(Origin + new Vector3(0f, .08f, 0f), new Vector3(Size, .05f, Size)));

            FireAt(Fire);
            TentAt(Tent);
            LabazAt(Labaz);
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
            Dressing();
        }

        public static void Clear()
        {
            Shelters.Clear(); Props.Clear(); Home.Clear(); Heights.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null; FireLight = null; TentRoot = null; LabazRoot = null;
        }

        static GameObject Load(string path) => Resources.Load<GameObject>(Prefabs + path);
        /// <summary>One of the imported set, or null where the set is not built.</summary>
        static GameObject Imp(string name) => Resources.Load<GameObject>(Imported + name);

        /// <summary>How tall an imported prefab was baked, off its mesh: the yard scales it to the height the plan wants.</summary>
        static float BakedHeight(GameObject prefab)
        {
            var mf = prefab.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? Mathf.Max(.01f, mf.sharedMesh.bounds.size.y) : 1f;
        }

        static Transform Put(GameObject prefab, Vector3 at, float yaw, float scale = 1f)
        {
            var t = Object.Instantiate(prefab, at, Quaternion.Euler(0f, yaw, 0f), Root).transform;
            t.name = prefab.name;
            if (scale != 1f) t.localScale = Vector3.one * scale;
            return t;
        }

        /// <summary>An imported thing on the snow, or null where the set is not built.</summary>
        static Transform Prop(string name, Vector3 at, float yaw, float scale = 1f)
        {
            var p = Imp(name);
            return p == null ? null : Put(p, at + Vector3.up * SnowTop, yaw, scale);
        }

        // ── the fire: the start and the finish ──

        static void FireAt(Vector3 at)
        {
            Transform fire;
            var pile = Imp("Bonfire");
            if (pile != null)
            {
                // the pack's pile of logs; the flames, the smoke and the light are the game's, as below
                fire = Put(pile, at + Vector3.up * (SnowTop - .01f), 25f);
                // wood to feed it, stacked beside
                for (int i = 0; i < 4; i++) Prop("WoodLog", at + new Vector3(1.7f + (i % 2) * .24f, (i / 2) * .19f, -.5f + (i % 2) * .08f), 84f + i * 5f);
            }
            else
            {
                var prefab = Load("Sites/Site_Camp_31Jan_Fire");
                fire = prefab != null ? Put(prefab, at + Vector3.up * (SnowTop - .02f), 0f) : FireFallback(at);
            }
            // the tree over it: the fire of the night was under the cedar, and a tree this size is the landmark
            // the base is found by from anywhere on the yard. The pack's pine stands six metres off, so that from
            // the fire it is a cone and not the underside of a bough lit from below, which reads as a leafy crown
            var pine = Imp("Pine_Snow_2");
            if (pine != null) Put(pine, at + new Vector3(-6.5f, 0f, -1f), 40f, 11f / BakedHeight(pine));
            else
            {
                var cedar = Load("Trees/HeroCedar");
                if (cedar != null) Put(cedar, at + new Vector3(-3.2f, 0f, -2.6f), 40f, .75f);
            }
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
            Material rm;
            if (Palettised) rm = Palette.Flat(Palette.SnowShade);
            else { rm = new Material(Shader.Find("Standard")) { color = new Color(.78f, .66f, .48f) }; rm.SetFloat("_Glossiness", 0f); }
            ring.GetComponent<Renderer>().sharedMaterial = rm;
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(ring.GetComponent<Collider>());
        }

        // ── the tent, and the things inside it ──

        /// <summary>The camp tent of 31 January, turned so its doorway faces the fire. The list's things are taken
        /// out of it as objects of their own — the stove off its wires, the diary and the camera from by the door —
        /// and lie where they lay; the rolled tent from the cargo library lies by the entrance. With the imported
        /// set the pack's tent stands in the site's place and the site's canvas goes; the things keep their places
        /// under the new canvas, repainted, and the camp's gear lies about the mouth.</summary>
        static void TentAt(Vector3 at)
        {
            var site = Load("Sites/Site_Camp_31Jan_Tent");
            var tent = Imp("Tent");
            if (site == null && tent == null) { TentFallback(at); return; }
            var toFire = (Fire - at); toFire.y = 0f; toFire.Normalize();
            var side = Vector3.Cross(Vector3.up, toFire);
            Transform siteRoot = null;
            if (site != null)
            {
                // the prefab's +Z is its doorway; the fire is to the south
                siteRoot = Put(site, at + Vector3.up * (SnowTop - .02f), 180f);
                TentRoot = siteRoot;
                Extract("печка", new[] { ("StoveDoorGlow", (string)null), ("Stove", Palette.Metal) }, "Stove", "StoveDoor");
                Extract("дневник", new[] { ("DiaryCover", Palette.Quilt), ("DiaryPages", Palette.Enamel), ("Diary", Palette.Quilt), ("Pencil", Palette.Wood) }, "Diary", "Pencil");
                Extract("фотоаппарат", new[] { ("CamGlass", Palette.SnowShade), ("CamChrome", Palette.Metal), ("CamCase", Palette.Felt), ("Cam", Palette.Black) }, "Cam");
            }
            if (tent != null)
            {
                // the imported tent: baked with its mouth toward +Z, like the site's, so the same turn faces the fire
                TentRoot = Put(tent, at + Vector3.up * (SnowTop - .02f), 180f);
                if (siteRoot != null) Object.Destroy(siteRoot.gameObject);
                Gear(at, toFire, side);
            }
            var roll = Imp("TentRoll") ?? Load("Cargo/Tent");
            if (roll != null)
            {
                var r = Put(roll, at + toFire * 3.4f + side * 1.6f + Vector3.up * SnowTop, 70f);
                if (Palettised) Palette.Recolour(r.gameObject);
                Register("свёрнутая палатка", Wrap(r, "свёрнутая палатка"));
            }
        }

        /// <summary>The way in: the body crawls from the fire to the stove at the back of the tent, so the line from
        /// the mouth to the stove has to stay clear of everything with a collider. Half a body plus the arms, and the
        /// things laid as dressing are pushed out of it (<see cref="Gear"/>) instead of being placed by eye.</summary>
        public const float Corridor = 1.1f;

        /// <summary>Lays a thing where the camp's things lie, but never in the way in: a place inside the corridor is
        /// slid sideways, away from the centre line, until it clears. Dressing must not decide whether the night can
        /// be played — the self-test caught exactly that (the pot and the tins stopped the prone body at the mouth).</summary>
        static Transform Lay(string name, Vector3 at, Vector3 along, Vector3 side, float ahead, float across, float yaw)
        {
            if (Mathf.Abs(across) < Corridor) across = Mathf.Sign(across == 0f ? 1f : across) * Corridor;
            return Prop(name, at + along * ahead + side * across, yaw);
        }

        /// <summary>The camp's things about the mouth of the tent, the way a camp's things lie: the pack against the
        /// canvas, the axe by the wood, the pot and the tins by the door, the bedroll along the wall inside. Dressing,
        /// not the list — and none of it in the way in.</summary>
        static void Gear(Vector3 at, Vector3 toFire, Vector3 side)
        {
            // the rolled tent of the list lies ahead 3.4, across 1.6, and nothing else lies on it
            Lay("Backpack", at, toFire, side, 1.6f, -1.55f, 205f);
            Lay("Axe", at, toFire, side, 2.6f, 2.3f, 30f);
            Lay("WoodLog", at, toFire, side, 3.2f, 2.6f, 100f);
            Lay("WoodLog", at, toFire, side, 3.5f, 2.4f, 96f);
            Lay("Pot", at, toFire, side, 3.0f, -1.35f, 0f);
            Lay("Can", at, toFire, side, 3.3f, -1.75f, 0f);
            Lay("Can", at, toFire, side, 2.9f, -1.6f, 40f);
            Lay("Flashlight", at, toFire, side, 2.5f, -1.15f, 250f);
            Lay("Bedroll", at, toFire, side, -.5f, -1.15f, 0f);
        }

        /// <summary>The labaz: the cache dug into the snow and covered with firewood and fir branches, marked by
        /// one ski. Its things lie around it — rusks and candles in a hand, firewood in both, the spare skis for two.</summary>
        static void LabazAt(Vector3 at)
        {
            var prefab = Load("Sites/Site_Labaz_1959");
            if (prefab != null)
            {
                LabazRoot = Put(prefab, at + Vector3.up * (SnowTop - .02f), 20f);
                if (Palettised) Palette.Recolour(LabazRoot.gameObject);
            }
            else
            {
                var fallback = Box("LabazFallback", at + Vector3.up * (SnowTop + .3f), new Vector3(2.2f, .6f, 1.6f), snow);
                LabazRoot = fallback.transform;
            }
            var toFire = Fire - at; toFire.y = 0f; toFire.Normalize();
            var side = Vector3.Cross(Vector3.up, toFire);
            Cargo("сухари", "Cargo/Rusks", at + toFire * 1.6f + side * .9f, 15f, new Vector3(.3f, .2f, .3f), new Color(.6f, .45f, .3f));
            Cargo("свечи", "Cargo/Candles", at + toFire * 1.9f - side * 1.0f, -30f, new Vector3(.2f, .1f, .2f), new Color(.9f, .85f, .7f));
            if (Imp("WoodLog") != null) Firewood("дрова", at - toFire * 1.2f + side * 1.4f, 60f);
            else Cargo("дрова", "Cargo/Firewood", at - toFire * 1.2f + side * 1.4f, 60f, new Vector3(.5f, .35f, .5f), new Color(.35f, .25f, .15f));
            Cargo("запасные лыжи", "Gear/Ski_Pair_Packed", at - toFire * 1.0f - side * 1.8f, 100f, new Vector3(2f, .12f, .2f), new Color(.5f, .35f, .2f));
        }

        static void Cargo(string item, string path, Vector3 at, float yaw, Vector3 standSize, Color standColour)
        {
            var prefab = Load(path);
            if (prefab != null)
            {
                var t = Put(prefab, at + Vector3.up * SnowTop, yaw);
                if (Palettised) Palette.Recolour(t.gameObject);
                Register(item, Wrap(t, item));
                return;
            }
            var go = Box("Item " + item, at + Vector3.up * (SnowTop + standSize.y * .5f), standSize, new Material(Shader.Find("Standard")) { color = standColour });
            Object.Destroy(go.GetComponent<Collider>());
            var root = new GameObject("Item " + item).transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop;
            go.transform.SetParent(root, true);
            Heights[item] = standSize.y;
            Register(item, root);
        }

        /// <summary>An armful of the pack's logs, three stacked, as one thing to carry.</summary>
        static void Firewood(string item, Vector3 at, float yaw)
        {
            var root = new GameObject("Item " + item).transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop; root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var log = Imp("WoodLog");
            var offsets = new[] { new Vector3(-.13f, 0f, 0f), new Vector3(.13f, 0f, .03f), new Vector3(0f, .17f, -.02f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                var t = Object.Instantiate(log, root).transform;
                t.name = log.name; t.localPosition = offsets[i]; t.localRotation = Quaternion.Euler(0f, (i - 1) * 7f, 0f);
                var c = t.GetComponent<Collider>(); if (c != null) Object.Destroy(c);
            }
            Heights[item] = .38f;
            Register(item, root);
        }

        /// <summary>Lifts the parts of the tent whose names start with <paramref name="prefixes"/> out into an
        /// object of their own, standing where they were, painted in the palette where the set is in use. The site's
        /// parts are named by what they are made of (<c>Stove</c>, <c>DiaryCover</c>, <c>CamBody</c> …), which is what
        /// makes this possible.</summary>
        static void Extract(string item, (string prefix, string colour)[] paint, params string[] prefixes)
        {
            if (TentRoot == null) return;
            var parts = new List<Transform>();
            foreach (var tr in TentRoot.GetComponentsInChildren<Transform>(true))
            {
                if (tr.GetComponent<MeshRenderer>() == null) continue;
                foreach (var p in prefixes) if (tr.name.StartsWith(p)) { parts.Add(tr); break; }
            }
            if (parts.Count == 0) { Debug.LogWarning("1079 sandbox: в палатке нет частей для «" + item + "»"); return; }
            var root = Wrap(parts, "Item " + item);
            if (Palettised) Palette.Recolour(root.gameObject, paint);
            Register(item, root);
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
            var imported = Imp("Rock_Snow_" + (variant % 3 + 1));
            // the brief's boulders are up to two metres; the plan's heights were written for the game's own
            if (imported != null) { Put(imported, at, yaw, Mathf.Min(h, 2f) / BakedHeight(imported)); return; }
            var prefab = Load("Rocks/Boulder_" + Mathf.Clamp(variant, 0, 3));
            if (prefab == null) { RockFallback(at, h, yaw); return; }
            // the library's boulders are knee to waist high; the yard wants a man's height, so they are scaled up
            float[] libHeight = { .7f, .9f, .6f, 1.1f };
            float k = h / libHeight[Mathf.Clamp(variant, 0, 3)];
            Put(prefab, at + Vector3.up * (h * .05f), yaw, k);
        }

        static void Spruce(Vector3 at, float h, float yaw, int variant)
        {
            var imported = Imp("Pine_Snow_" + (variant % 3 + 1));
            if (imported != null) { CoverCollider(Put(imported, at, yaw, h / BakedHeight(imported))); return; }
            var prefab = Load("Trees/Spruce_" + Mathf.Clamp(variant, 0, 4));
            if (prefab == null) { SpruceFallback(at, h); return; }
            var t = Put(prefab, at, yaw, h / 16f);
            CoverCollider(t);
        }

        static void Windfall(Vector3 at, float h, float yaw, int variant)
        {
            var imported = Imp("Windfall");
            // the imported log is three metres and half a metre through as baked; the plan's height is for the
            // game's windfalls with their root plates, and a log scaled to it would be a barrel
            if (imported != null) { CoverCollider(Put(imported, at, yaw, .9f + variant * .1f)); return; }
            var prefab = Load("Trees/Windfall_" + Mathf.Clamp(variant, 0, 2));
            if (prefab == null) { DriftFallback(at, h); return; }
            var t = Put(prefab, at, yaw);
            CoverCollider(t);
        }

        /// <summary>Trees that are scenery and not cover, at the margins of the square where nobody has to pass:
        /// bare birches in snow and dead trees, so the yard reads as a winter wood and not a row of pines. Only with
        /// the imported set; the game's own yard had none.</summary>
        static void Dressing()
        {
            var trees = new (string name, float x, float z, float yaw, float h)[]
            {
                ("Birch_Snow_1",    -21f,  20f,  30f, 7f),
                ("Birch_Snow_2",     22f, -20f,  80f, 8.5f),
                ("DeadTree_Snow_1", -17f, -24f,   0f, 7f),
                ("Birch_Snow_2",    -24f,  -6f, 140f, 8f),
                ("DeadTree_Snow_1",  26f,  24f, 200f, 6.5f),
            };
            foreach (var t in trees)
            {
                var prefab = Imp(t.name);
                if (prefab == null) return;
                Put(prefab, Origin + new Vector3(t.x, 0f, t.z), t.yaw, t.h / BakedHeight(prefab));
            }
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
            Stand("дневник", at + toFire * 1.4f + side2 * .5f, new Vector3(.24f, .05f, .32f), new Color(.36f, .22f, .14f));
            Stand("фотоаппарат", at + toFire * 1.5f - side2 * .5f, new Vector3(.15f, .1f, .09f), new Color(.12f, .12f, .13f));
            Stand("печка", at - toFire * 1.4f, new Vector3(.3f, .3f, .5f), new Color(.45f, .45f, .47f));
            Stand("свёрнутая палатка", at + toFire * 3.4f + side2 * 1.2f, new Vector3(1.6f, .5f, .5f), new Color(.35f, .4f, .32f));
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
