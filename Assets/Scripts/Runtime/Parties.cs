using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Other people on the mountain.
    ///
    /// This is the cheapest thing in the whole ascent and very nearly the loudest. At three in the morning the
    /// southern slope is not empty: parties leave the barrels between two and four
    /// (<see cref="AscentRoute.StartFromHour"/>), and from below they are a string of head torches going up the snow,
    /// visible for a kilometre and a half because there is nothing else to look at. By day the same parties are dots
    /// on the firn. And some of them turn round before you do — which tells a player more about this mountain than any
    /// hint ever will.
    ///
    /// <b>There is no AI here and there is nothing on the wire.</b> Every group is a pure function of the save's seed,
    /// the day and the hour of the run clock — the same three numbers <see cref="MountainDay"/> is built on, all three
    /// of them already agreed between the host and every client. So both machines draw the same lamps in the same
    /// places without a byte being sent, exactly as they both work out the ground for themselves
    /// (<see cref="Climb.Point"/>). A group walks <see cref="Elbrus.SummitRoute"/> at its own pace with its own
    /// scatter, turns round at its own hour or when the weather breaks, and goes back down at
    /// <see cref="Ascent.DescentSpeed"/> of that pace.
    ///
    /// Elbrus only: on Kholat Syakhl nobody else is on the mountain, and that is the point of Kholat Syakhl.</summary>
    public sealed class Parties : MonoBehaviour
    {
        /// <summary>How many groups are on the slope on a good day. Six is what the barrels hold on a summit window,
        /// and thirty little figures is nothing at all to draw.</summary>
        public const int Groups = 6;
        /// <summary>Head torches show while it is dark, and for a while after — the last of them are switched off on
        /// the shelf long after the sun is up.</summary>
        public const float LampsUntilHour = AscentRoute.DawnHour + .6f;
        /// <summary>A lamp is drawn at a constant angular size so that it is still a point of light a kilometre and a
        /// half away, the way a head torch on a black slope actually is.</summary>
        public const float LampAngular = .0055f, LampMinM = .16f, LampMaxM = 5f, LampFarM = 2500f;

        sealed class Group
        {
            public Transform Root;
            public Transform[] Bodies;
            public Transform[] Lamps;
            public Light Lantern;
            public float StartHour, PaceMs, TurnHour, TopS;
            public float[] Along, Across;
            public bool Out;
        }

        readonly List<Group> groups = new List<Group>();
        static Material coat, glow;
        static Mesh capsule, quad;
        int builtSeed, builtDay = int.MinValue;
        float routeLength;

        public static Parties Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            var go = new GameObject("Parties");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<Parties>();
        }

        void Awake() { routeLength = Elbrus.Length(Elbrus.SummitRoute); }

        void Update()
        {
            if (!Climb.On) { Destroy(gameObject); return; }
            if (!MountainDay.Known) return;
            int day = Forecast.DayIndex(MountainDay.Date);
            if (MountainDay.Seed != builtSeed || day != builtDay) Build(MountainDay.Seed, day);
            // These are other people on the mountain — scenery, and slow scenery at that: a party covers a couple of
            // centimetres between frames. Walking them five times a second instead of sixty takes about two thousand
            // square roots a frame off the budget (every body is a fresh walk along the 49-segment route polyline,
            // Elbrus.PointAt), and nobody can see the difference.
            if (Time.time < walkNext) return;
            walkNext = Time.time + WalkStep;
            Walk(Climb.Hour());
        }

        /// <summary>Seconds between one placement of the parties and the next.</summary>
        const float WalkStep = .2f;
        float walkNext;

        // ── who is out today ─────────────────────────────────────────────────────────────────────────────

        static float U(int seed, int day, int salt) => Forecast.Unit(seed, day, salt);

        void Build(int seed, int day)
        {
            foreach (var g in groups) if (g.Root != null) Destroy(g.Root.gameObject);
            groups.Clear();
            builtSeed = seed; builtDay = day;
            if (Bootstrap.Dem == null) return;

            var today = MountainDay.Day;
            bool window = Forecast.Window(today);
            // a morning with no window empties the barrels: half the parties do not even rope up
            int count = window ? Groups : Groups / 2;

            for (int i = 0; i < count; i++)
            {
                int salt = 100 + i * 8;
                var g = new Group
                {
                    // parties leave the barrels between two and four in the morning
                    StartHour = Mathf.Lerp(AscentRoute.StartFromHour, AscentRoute.StartToHour, U(seed, day, salt)),
                    // 6 694 m of route in seven to eleven hours is 0.17…0.27 m/s, and that is what guides walk
                    PaceMs = Mathf.Lerp(.16f, .28f, U(seed, day, salt + 1)),
                };
                // their own turn-round time, a little before or a little after the one everybody undertakes
                float ownTurn = AscentRoute.TurnaroundHour - 1.2f + 1.8f * U(seed, day, salt + 2);
                // the weather turns them round too, and earlier than the clock does
                g.TurnHour = Mathf.Min(ownTurn, today.BreakHour);
                // and one party in three has less in the legs than it thought and stops at the rocks or the зеркало
                float share = U(seed, day, salt + 3);
                float capS = share < .18f ? 3035f : share < .36f ? 4026f : routeLength;
                g.TopS = Mathf.Min(capS, g.PaceMs * 3600f * Mathf.Max(0f, g.TurnHour - g.StartHour));

                int size = 2 + Mathf.FloorToInt(U(seed, day, salt + 4) * 4f);
                g.Root = new GameObject($"Party{i}").transform;
                g.Root.SetParent(transform, false);
                g.Bodies = new Transform[size];
                g.Lamps = new Transform[size];
                g.Along = new float[size];
                g.Across = new float[size];
                for (int k = 0; k < size; k++)
                {
                    // a party on a fixed line walks in file, a few metres apart and half a metre off the trail
                    g.Along[k] = -k * Mathf.Lerp(6f, 16f, U(seed, day, salt + 5 + k));
                    g.Across[k] = (U(seed, day, salt + 5 + k) - .5f) * 3.5f;
                    g.Bodies[k] = Body(g.Root, k);
                    g.Lamps[k] = Lamp(g.Bodies[k]);
                }
                var light = new GameObject("Lantern", typeof(Light));
                light.transform.SetParent(g.Bodies[0], false);
                light.transform.localPosition = new Vector3(0, .9f, 0);
                g.Lantern = light.GetComponent<Light>();
                g.Lantern.type = LightType.Point;
                g.Lantern.color = new Color(1f, .85f, .62f);
                g.Lantern.range = 14f;
                g.Lantern.intensity = 1.4f;
                g.Lantern.shadows = LightShadows.None;
                g.Lantern.enabled = false;
                groups.Add(g);
            }
        }

        // ── where they are now ───────────────────────────────────────────────────────────────────────────

        void Walk(float hour)
        {
            var dem = Bootstrap.Dem;
            if (dem == null) return;
            bool dark = hour < LampsUntilHour || MountainDay.SkyNow >= SkyState.Blizzard;
            var cam = Camera.main;
            var eye = cam != null ? cam.transform.position : Vector3.zero;

            foreach (var g in groups)
            {
                float s = ArcAt(g, hour);
                bool out_ = s > 2f;
                if (g.Out != out_)
                {
                    g.Out = out_;
                    g.Root.gameObject.SetActive(out_);
                }
                if (!out_) continue;

                var (x, z) = Elbrus.PointAt(Elbrus.SummitRoute, s);
                var ahead = Elbrus.PointAt(Elbrus.SummitRoute, Mathf.Min(routeLength, s + 12f));
                var back = Elbrus.PointAt(Elbrus.SummitRoute, Mathf.Max(0f, s - 12f));
                var f = new Vector2(ahead.x - back.x, ahead.z - back.z);
                if (f.sqrMagnitude < 1e-4f) f = new Vector2(0, 1);
                f.Normalize();
                var right = new Vector2(f.y, -f.x);
                var face = Quaternion.LookRotation(new Vector3(f.x, 0, f.y));

                for (int k = 0; k < g.Bodies.Length; k++)
                {
                    float sk = Mathf.Clamp(s + g.Along[k], 0f, routeLength);
                    var (bx, bz) = Elbrus.PointAt(Elbrus.SummitRoute, sk);
                    bx += right.x * g.Across[k]; bz += right.y * g.Across[k];
                    var at = new Vector3(bx, dem.Sample(bx, bz) + .85f, bz);
                    g.Bodies[k].SetPositionAndRotation(at, face);

                    var lamp = g.Lamps[k];
                    if (lamp.gameObject.activeSelf != dark) lamp.gameObject.SetActive(dark);
                    if (!dark) continue;
                    float dist = Vector3.Distance(eye, lamp.position);
                    float size = Mathf.Clamp(dist * LampAngular, LampMinM, LampMaxM);
                    lamp.localScale = new Vector3(size, size, size);
                    if (cam != null) lamp.rotation = Quaternion.LookRotation(lamp.position - eye);
                }
                bool lit = dark && Vector3.Distance(eye, g.Bodies[0].position) < 140f;
                if (g.Lantern.enabled != lit) g.Lantern.enabled = lit;
            }
        }

        /// <summary>Metres along <see cref="Elbrus.SummitRoute"/> this group has walked by this hour: up at its own
        /// pace until it turns, then down at <see cref="Ascent.DescentSpeed"/> of it, then home.</summary>
        float ArcAt(Group g, float hour)
        {
            if (hour <= g.StartHour) return 0f;
            float up = Mathf.Min(g.TopS, g.PaceMs * 3600f * (hour - g.StartHour));
            float turned = g.StartHour + (g.PaceMs > 0f ? g.TopS / (g.PaceMs * 3600f) : 0f);
            turned = Mathf.Min(turned, g.TurnHour);
            if (hour <= turned) return up;
            float downMs = g.PaceMs * Ascent.DescentSpeed;
            return Mathf.Max(0f, g.TopS - downMs * 3600f * (hour - turned));
        }

        // ── what they are made of ────────────────────────────────────────────────────────────────────────

        Transform Body(Transform parent, int index)
        {
            if (capsule == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }
            if (coat == null)
            {
                var template = Resources.Load<Material>("Flat");
                coat = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
                coat.color = new Color(.16f, .18f, .22f);
                coat.SetFloat("_Glossiness", .12f);
            }
            var go = new GameObject("Climber" + index, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(.46f, .86f, .46f);
            go.GetComponent<MeshFilter>().sharedMesh = capsule;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = coat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        Transform Lamp(Transform body)
        {
            if (quad == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }
            if (glow == null)
            {
                // the emissive Standard material the lodge bulbs use: dark under a torch beam, bright by itself, and
                // its shader variant is already in the build because the huts reference it (CLAUDE.md, «Ночь»)
                var baked = Resources.Load<Material>("World/Materials/ElbLLampGlow");
                glow = baked != null ? new Material(baked) : new Material(Shader.Find("Standard"));
                glow.color = new Color(1f, .93f, .78f);
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", new Color(1f, .86f, .6f) * 3.4f);
                glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            var go = new GameObject("Lamp", typeof(MeshFilter), typeof(MeshRenderer));
            // the head torch sits where a head is, and the body is scaled, so the lamp hangs off the parent unscaled
            go.transform.SetParent(body.parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        void LateUpdate()
        {
            // the lamp follows the head of the body it belongs to; it is a child of the group, not of the body, so
            // that the body's own scale does not squash it
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (!g.Out) continue;
                for (int k = 0; k < g.Lamps.Length; k++)
                    if (g.Lamps[k].gameObject.activeSelf)
                        g.Lamps[k].position = g.Bodies[k].position + Vector3.up * .82f;
            }
        }
    }
}
