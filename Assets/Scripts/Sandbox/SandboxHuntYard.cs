using System.Collections.Generic;
using UnityEngine;
using Height1079.Art;
using Height1079.Core;
using Height1079.Night;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>The yard of the brief (docs/SANDBOX.md §3), made of **one** set of things: the imported low-poly
    /// models in the palette's colours (<c>Resources/World/Prefabs/Imported</c>, Editor/World/ImportedFactory) — the
    /// tent, the fire's pile of logs, the pines, the rocks, the windfalls, the things lying at the tent mouth, the
    /// stove, the diary, the Zorkiy, the labaz and its cargo.
    ///
    /// It used to be a mixture, and that was the defect the owner saw in the built player: the tent and the pines came
    /// flat and low-poly while the stove, the diary, the camera, the labaz and its cargo were lifted out of the game's
    /// site prefabs, which carry PolyHaven scans. Repainting them (<c>Palette.Recolour</c>) could not help: it leaves
    /// a textured material alone on purpose, so the photographs stayed. Two logs side by side, one flat, one
    /// photoreal. Nothing on this map is loaded from the game's world library any more — see docs/SANDBOX.md §12 for
    /// the rule and for the line the log prints when something breaks it. Without the imported set (a clone that has
    /// generated nothing) the yard falls back to shapes drawn here, in palette colours, and the log says so.
    ///
    /// The square is 60 m, far south of the physics range so neither is in the other's picture. The fire is at the
    /// south edge — the start, and the finish: put a thing down inside its ring and it is home. The tent is at the
    /// north edge, its mouth toward the fire, and the things to bring lie inside it, where they lay in 1959. Between
    /// them the cover, staggered so from any piece the next is a short dash and the middle line is never bare; the
    /// last row stands inside the Menk's ring round the tent, which is where the hiding has to happen.
    ///
    /// Where everything stands is not written here but read: <c>StreamingAssets/sandbox/yard.json</c>
    /// (<see cref="SandboxYardLayout"/>) holds the plan of the cover, the three places, the gear at the tent mouth,
    /// the width of the way in and the size of the square. The defaults in that class are exactly the numbers this
    /// yard was built with, so a player with no file is the yard as it was; a player with one is a yard someone
    /// rearranged without opening Unity.</summary>
    public static class SandboxHuntYard
    {
        /// <summary>The arrangement in play — the file, or the built-in defaults.</summary>
        public static SandboxYardLayout Layout => SandboxYardLayout.Current;

        public static float Size => Layout.size;
        /// <summary>Centre of the square, well south of the range (z −250): the two are not in each other's view.</summary>
        public static Vector3 Origin => new Vector3(Layout.origin.x, 0f, Layout.origin.z);
        public static Vector3 Fire => Spot(Layout.fire);
        public static Vector3 Tent => Spot(Layout.tent);
        /// <summary>The second section: the cache in the snow, east of the line from the fire to the tent.</summary>
        public static Vector3 Labaz => Spot(Layout.labaz);
        /// <summary>Where a body is put down to start: beside the fire, facing the tent.</summary>
        public static Vector3 Spawn => Fire + new Vector3(Layout.spawn.x, Layout.spawn.y, Layout.spawn.z);

        static Vector3 Spot(YardSpot s) => Origin + new Vector3(s.x, 0f, s.z);
        /// <summary>Top of the snow you see, world y.</summary>
        public const float SnowTop = .105f;
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
        /// <summary>True when the imported set in palette colours is there: the yard is then one drawing. False means
        /// a clone that has generated nothing, and the yard is the shapes drawn at the foot of this file.</summary>
        public static bool Palettised { get; private set; }

        static Material snow, rock;

        public static void Build(Material snowMat, Material rockMat)
        {
            Clear();
            // the arrangement is read here, once, and kept: a run cannot have the yard change under it
            var plan = Layout;
            Debug.Log("1079 sandbox: двор — " + (SandboxYardLayout.Source ?? "по умолчанию из кода") + ", укрытий " + plan.cover.Count + ", вещей у входа " + plan.gear.Count);
            // the ground of the yard is the range's own snow, handed in: one material, so walking off the yard onto
            // the field crosses no edge at all (docs/SANDBOX.md §12)
            snow = snowMat; rock = rockMat;
            Root = new GameObject("HuntYard").transform;
            Palettised = Imp("Tent") != null && Imp("Bonfire") != null;
            if (!Palettised) Debug.LogWarning("1079 sandbox: нет импортированных моделей (World/Prefabs/Imported) — площадка собрана из заглушек. Меню 1079 → Rebuild world.");

            // the square: a plate under a skin of snow, the same idea as the range's yard
            var ground = Box("HuntGround", Origin + new Vector3(0f, -1f, 0f), new Vector3(Size, 2f, Size), snow);
            ground.AddComponent<Grip>().Hold = 1f;
            var skin = Box("HuntSnow", Origin + new Vector3(0f, .055f, 0f), new Vector3(Size * .995f, .1f, Size * .995f), snow);
            var sc = skin.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Object.Destroy(sc); }
            SandboxTerrainSnow.Skin(new Bounds(Origin + new Vector3(0f, .08f, 0f), new Vector3(Size, .05f, Size)));

            FireAt(Fire);
            TentAt(Tent);
            LabazAt(Labaz);
            var rnd = new System.Random(plan.seed);
            foreach (var s in plan.cover)
            {
                if (!SandboxYardLayout.TryKind(s.kind, out var kind)) continue;
                var at = Origin + new Vector3(s.x, 0f, s.z);
                Shelters.Add(new Shelter { Kind = kind, Pos = at, Height = s.height });
                float yaw = (float)rnd.NextDouble() * 360f;
                switch (kind)
                {
                    case Cover.Rock: Rock(at, s.height, yaw, rnd.Next(4)); break;
                    case Cover.Spruce: Spruce(at, s.height, yaw, rnd.Next(5)); break;
                    case Cover.Windfall: Windfall(at, s.height, yaw, rnd.Next(3)); break;
                }
            }
            Dressing(plan);
        }

        public static void Clear()
        {
            Shelters.Clear(); Props.Clear(); Home.Clear(); Heights.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null; FireLight = null; TentRoot = null; LabazRoot = null;
        }

        /// <summary>One of the imported set, or null where the set is not built. The only place this map gets a model
        /// from: there is deliberately no path back to <c>World/Prefabs</c>, where the game's scanned site prefabs
        /// live (docs/SANDBOX.md §12).</summary>
        static GameObject Imp(string name) => Resources.Load<GameObject>(Imported + name);

        /// <summary>A thing to carry has no collider: the body picks it up by distance, and a collider on it would
        /// only stand in the way in (<see cref="Corridor"/>) — which is exactly what the site's parts never did,
        /// because they had none.</summary>
        static Transform NoCollider(Transform t)
        {
            if (t == null) return null;
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return t;
        }

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
                // the pack's pile of logs, turned the way the file says; the flames, the smoke and the light are the
                // game's, as below
                fire = Put(pile, at + Vector3.up * (SnowTop - .01f), Layout.fire.yaw);
                // wood to feed it, stacked beside
                for (int i = 0; i < 4; i++) Prop("WoodLog", at + new Vector3(1.7f + (i % 2) * .24f, (i / 2) * .19f, -.5f + (i % 2) * .08f), 84f + i * 5f);
            }
            else fire = FireFallback(at);
            // the tree over it: the fire of the night was under the cedar, and a tree this size is the landmark
            // the base is found by from anywhere on the yard. The pack's pine stands six metres off, so that from
            // the fire it is a cone and not the underside of a bough lit from below, which reads as a leafy crown
            var pine = Imp("Pine_Snow_2");
            if (pine != null) Put(pine, at + new Vector3(-6.5f, 0f, -1f), 40f, 11f / BakedHeight(pine));
            else SpruceFallback(at + new Vector3(-6.5f, 0f, -1f), 11f);
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
            // a deliberate marking, not an accident of two materials: the snow of the ring is the sheet's shaded snow
            // on the sheet's lit snow, so it reads as trodden ground and never as an edge between two grounds
            ring.GetComponent<Renderer>().sharedMaterial = Palette.Flat(Palette.SnowShade);
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(ring.GetComponent<Collider>());
        }

        // ── the tent, and the things inside it ──

        /// <summary>The camp tent, turned so its doorway faces the fire, and the things of the list lying where they
        /// lay in 1959: the stove at the back (the errand's heavy one — the body crawls the length of the tent to it),
        /// the diary and the Zorkiy by the door, the rolled tent outside by the entrance, and the camp's gear about
        /// the mouth as dressing.
        ///
        /// All four are the imported set now. The stove, the diary and the camera used to be lifted out of the game's
        /// site tent (<c>Site_Camp_31Jan_Tent</c>) and repainted, which is how PolyHaven scans got onto this map:
        /// repainting skips a textured material on purpose. They are the nearest shapes of the CC0 packs instead —
        /// a metal box with a pipe, a dark notebook, a black body with a lens — and none of them carries a collider,
        /// exactly as the lifted parts did not, so nothing new stands in the way in.</summary>
        static void TentAt(Vector3 at)
        {
            var tent = Imp("Tent");
            var toFire = (Fire - at); toFire.y = 0f; toFire.Normalize();
            var side = Vector3.Cross(Vector3.up, toFire);
            if (tent == null) { TentFallback(at); return; }
            // the imported tent is baked with its mouth toward +Z, like the site's, so the file's turn faces the fire
            TentRoot = Put(tent, at + Vector3.up * (SnowTop - .02f), Layout.tent.yaw);
            Gear(at, toFire, side);
            Stove(at - toFire * 1.4f, Layout.tent.yaw);
            Register("дневник", Wrap(NoCollider(Prop("Diary", at + toFire * 1.4f + side * .5f, Layout.tent.yaw + 12f)), "дневник"));
            Register("фотоаппарат", Wrap(NoCollider(Prop("PhotoCamera", at + toFire * 1.5f - side * .5f, Layout.tent.yaw - 25f)), "фотоаппарат"));
            var roll = Prop("TentRoll", at + toFire * 3.4f + side * 1.6f, 70f);
            Register("свёрнутая палатка", Wrap(NoCollider(roll), "свёрнутая палатка"));
        }

        /// <summary>The stove: a sheet-metal box with its pipe standing on it, as one thing to carry. No pack has a
        /// 1959 tent stove, so it is the pack's box and the pack's cylinder in «Металл» — the shape reads at three
        /// metres under a torch, which is the only test this map asks of a prop (docs/ART.md §3).</summary>
        static void Stove(Vector3 at, float yaw)
        {
            var box = Imp("Stove");
            if (box == null) return;
            var root = new GameObject("Item печка").transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop; root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var body = Object.Instantiate(box, root).transform;
            body.name = box.name; body.localPosition = Vector3.zero;
            var pipe = Imp("StovePipe");
            if (pipe != null)
            {
                // 0.4 m of pipe on a 0.5 m box: 0.9 m in all, well under the tent's 1.5 m ridge, so the stove stands
                // inside the canvas instead of through it
                var p = Object.Instantiate(pipe, root).transform;
                p.name = pipe.name; p.localPosition = new Vector3(0f, .5f, -.16f); p.localScale = Vector3.one * .45f;
            }
            NoCollider(root);
            // the whole thing, box and pipe: what is carried rides at half this height in front of the chest
            Heights["печка"] = pipe != null ? .9f : .52f;
            Register("печка", root);
        }

        /// <summary>The way in: the body crawls from the fire to the stove at the back of the tent, so the line from
        /// the mouth to the stove has to stay clear of everything with a collider. Half a body plus the arms, and the
        /// things laid as dressing are pushed out of it (<see cref="Gear"/>) instead of being placed by eye.</summary>
        public static float Corridor => Layout.corridor;

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
        /// not the list — and none of it in the way in. The row of them is the file's (<c>gear</c>).</summary>
        static void Gear(Vector3 at, Vector3 toFire, Vector3 side)
        {
            // the rolled tent of the list lies ahead 3.4, across 1.6, and nothing else lies on it
            foreach (var g in Layout.gear) Lay(g.name, at, toFire, side, g.ahead, g.across, g.yaw);
        }

        /// <summary>The labaz: the cache dug into the snow and covered with firewood and fir branches, marked by
        /// one ski. Its things lie around it — rusks and candles in a hand, firewood in both, the spare skis for two.</summary>
        static void LabazAt(Vector3 at)
        {
            var toFire = Fire - at; toFire.y = 0f; toFire.Normalize();
            var side = Vector3.Cross(Vector3.up, toFire);
            LabazRoot = Mound(at, Layout.labaz.yaw);
            Cargo("сухари", "Rusks", at + toFire * 1.6f + side * .9f, 15f, new Vector3(.3f, .2f, .3f), Palette.Wood);
            Cargo("свечи", "Candles", at + toFire * 1.9f - side * 1.0f, -30f, new Vector3(.2f, .18f, .2f), Palette.Enamel);
            // the wood: the pack's bundle if the set has it, three of its single logs stacked if not, a block if
            // neither — Armful must never be called without the log it stacks
            var wood = at - toFire * 1.2f + side * 1.4f;
            if (Imp("Firewood") == null && Imp("WoodLog") != null) Armful("дрова", wood, 60f);
            else Cargo("дрова", "Firewood", wood, 60f, new Vector3(.5f, .35f, .5f), Palette.Wood);
            Cargo("запасные лыжи", "Skis", at - toFire * 1.0f - side * 1.8f, 100f, new Vector3(2f, .12f, .2f), Palette.Wood);
        }

        /// <summary>The labaz itself: a cache dug into the snow, covered over with firewood and fir branches and
        /// marked by one ski standing in it. No pack has such a thing and none should — it is three drifts of the
        /// pack's rock painted snow, four of its logs laid over them and a board upright. Built, not loaded: the
        /// prefab this used to be (<c>Site_Labaz_1959</c>) is one of the game's scanned sites.</summary>
        static Transform Mound(Vector3 at, float yaw)
        {
            var root = new GameObject("Labaz").transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop; root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var drift = Imp("SnowMound");
            if (drift == null)
            {
                var fallback = Box("LabazFallback", at + Vector3.up * (SnowTop + .3f), new Vector3(2.2f, .6f, 1.6f), snow);
                fallback.transform.SetParent(root, true);
                return root;
            }
            // three lumps in a line, flattened: a cache is long and low, and one rock scaled up is a boulder
            var lumps = new[] { new Vector3(-.75f, 0f, .1f), new Vector3(0f, 0f, -.15f), new Vector3(.8f, 0f, .05f) };
            for (int i = 0; i < lumps.Length; i++)
            {
                var m = Object.Instantiate(drift, root).transform;
                m.name = drift.name; m.localPosition = lumps[i];
                m.localRotation = Quaternion.Euler(0f, i * 57f, 0f);
                m.localScale = new Vector3(1.5f, .7f + i % 2 * .12f, 1.2f);
            }
            var log = Imp("WoodLog");
            if (log != null)
                for (int i = 0; i < 4; i++)
                {
                    var l = Object.Instantiate(log, root).transform;
                    l.name = log.name;
                    l.localPosition = new Vector3(-.7f + i * .5f, .55f, i % 2 * .14f - .07f);
                    l.localRotation = Quaternion.Euler(0f, 78f + i * 6f, 0f);
                    NoCollider(l);
                }
            // the one ski standing up: how a labaz was found again after the snow had gone over it
            var ski = Imp("Skis");
            if (ski != null)
            {
                var s = Object.Instantiate(ski, root).transform;
                s.name = ski.name;
                s.localPosition = new Vector3(1.15f, 0f, -.2f);
                s.localRotation = Quaternion.Euler(-78f, 20f, 0f);
                s.localScale = new Vector3(.5f, 1f, .5f);
                NoCollider(s);
            }
            return root;
        }

        /// <summary>One thing of the cache: an imported model, or a block of snow-coloured stand-in where the set is
        /// not built. Either way it is one object with its foot at its origin, which is what the run moves.</summary>
        static void Cargo(string item, string name, Vector3 at, float yaw, Vector3 standSize, string standColour)
        {
            var t = Prop(name, at, yaw);
            if (t != null) { Register(item, Wrap(NoCollider(t), item)); return; }
            var go = Box("Item " + item, at + Vector3.up * (SnowTop + standSize.y * .5f), standSize, Palette.Flat(standColour));
            Object.Destroy(go.GetComponent<Collider>());
            var root = new GameObject("Item " + item).transform;
            root.SetParent(Root, true); root.position = at + Vector3.up * SnowTop;
            go.transform.SetParent(root, true);
            Heights[item] = standSize.y;
            Register(item, root);
        }

        /// <summary>An armful of the pack's logs, three stacked, as one thing to carry — for a set that has the single
        /// log but not the bundle.</summary>
        static void Armful(string item, Vector3 at, float yaw)
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

        /// <summary>The same for one placed model — and nothing at all for one the set did not have, so a missing
        /// model is a thing the run lays out a stand-in for and not an exception here.</summary>
        static Transform Wrap(Transform single, string item)
        {
            if (single == null) return null;
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
            RockFallback(at, h, yaw);
        }

        static void Spruce(Vector3 at, float h, float yaw, int variant)
        {
            var imported = Imp("Pine_Snow_" + (variant % 3 + 1));
            if (imported != null) { CoverCollider(Put(imported, at, yaw, h / BakedHeight(imported))); return; }
            SpruceFallback(at, h);
        }

        static void Windfall(Vector3 at, float h, float yaw, int variant)
        {
            var imported = Imp("Windfall");
            // the imported log is three metres and half a metre through as baked; the plan's height is for the
            // game's windfalls with their root plates, and a log scaled to it would be a barrel
            if (imported != null) { CoverCollider(Put(imported, at, yaw, .9f + variant * .1f)); return; }
            DriftFallback(at, h);
        }

        /// <summary>Trees that are scenery and not cover, at the margins of the square where nobody has to pass:
        /// bare birches in snow and dead trees, so the yard reads as a winter wood and not a row of pines. Only with
        /// the imported set; the game's own yard had none.</summary>
        static void Dressing(SandboxYardLayout plan)
        {
            foreach (var t in plan.dressing)
            {
                var prefab = Imp(t.name);
                if (prefab == null) return;
                Put(prefab, Origin + new Vector3(t.x, 0f, t.z), t.yaw, t.height / BakedHeight(prefab));
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

        // ── stand-ins for a clone that has generated nothing ──
        // Shapes, not paint: every one of them is a colour of the sheet (Palette.Flat), so even the degraded map
        // keeps the one rule this map has — no colour from anywhere but the palette (docs/SANDBOX.md §12).

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
            for (int i = 0; i < 3; i++)
            {
                var l = Box("Log" + i, root.position + new Vector3(0f, .16f, 0f), new Vector3(.16f, .16f, 1.1f), Palette.Flat(Palette.Bark));
                l.transform.SetParent(root, true); l.transform.rotation = Quaternion.Euler(0f, i * 60f + 20f, 0f);
                Object.Destroy(l.GetComponent<Collider>());
            }
            // the embers do not glow here: the flames and the light over them are the game's (CampSmoke, FireLight),
            // and a lit material of its own would be the one colour on this map not out of the sheet
            var ember = Box("Embers", root.position + new Vector3(0f, .12f, 0f), new Vector3(.7f, .06f, .7f), Palette.Flat(Palette.FireCore));
            ember.transform.SetParent(root, true);
            Object.Destroy(ember.GetComponent<Collider>());
            return root;
        }

        static void TentFallback(Vector3 at)
        {
            var canvas = Palette.Flat(Palette.Canvas);
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
            Stand("дневник", at + toFire * 1.4f + side2 * .5f, new Vector3(.24f, .05f, .32f), Palette.Quilt);
            Stand("фотоаппарат", at + toFire * 1.5f - side2 * .5f, new Vector3(.15f, .1f, .09f), Palette.Black);
            Stand("печка", at - toFire * 1.4f, new Vector3(.3f, .3f, .5f), Palette.Metal);
            Stand("свёрнутая палатка", at + toFire * 3.4f + side2 * 1.2f, new Vector3(1.6f, .5f, .5f), Palette.Canvas);
        }

        static void Stand(string item, Vector3 at, Vector3 size, string colour)
        {
            var go = Box("Item " + item, at + Vector3.up * (SnowTop + size.y * .5f), size, Palette.Flat(colour));
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
            var spruce = Palette.Flat(Palette.NeedlesShade);
            var trunk = Palette.Flat(Palette.Bark);
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
