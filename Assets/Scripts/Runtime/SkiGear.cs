using UnityEngine;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Everything one hiker wears and drags through the snow, and the snow itself under their feet.
    ///
    /// The group walked out of the Auspiya on skis, so that is how a night starts: boards on the feet, poles in the hands.
    /// Take the skis off and they do not vanish — the pair is lashed base to base with the poles alongside and rides on the
    /// rucksack (<c>Ski_Pair_Packed</c>). Take only the poles out and the two boards stand on the pack while the poles are in
    /// the hands. Hitch the rucksack to a spare pair and it becomes a волокуша that follows on a rope, and the back carries
    /// nothing (<see cref="Skiing.ShoulderKg"/>).
    ///
    /// This component is on every hiker, local and remote, and is purely a view: which way a hiker is travelling is
    /// <see cref="HikerController.Mode"/>, written by the host (<see cref="NightSession.TravelRpc"/>). It also keeps the snow
    /// figures under the feet — depth, crust, лыжня, sinking — because the legs (<see cref="HikerController"/>) and the HUD
    /// both want them every frame and <see cref="SnowCover"/> costs a dozen height samples.</summary>
    [DisallowMultipleComponent]
    public sealed class SkiGear : MonoBehaviour
    {
        /// <summary>A change of gear costs a stop: bindings are undone with mittens on.</summary>
        public const float SwapSeconds = 2.2f;
        /// <summary>Waist to the sled's hitch, metres.</summary>
        public const float RopeLength = 2.6f;
        /// <summary>How often the snow under the feet is looked at again (it is a dozen height samples).</summary>
        const float SampleSeconds = .2f, SampleMetres = .45f;

        const string GearDir = "World/Prefabs/Gear/";

        static bool warned;
        static AudioClip swish;
        static Material ropeMaterial;

        HikerController hiker;
        Transform skiL, skiR, poleL, poleR, bundle, sled, hitch, loadBed, visual;
        LineRenderer rope;
        AudioSource glide;

        float busyUntil, stride, nextSwish, pace;
        Vector3 sledPos, wasAt;
        bool sledPlaced, walked;

        float depth, crust, packed, sink, sampledAt = -99f;
        Vector3 sampledFrom;

        /// <summary>Metres of snow lying here.</summary>
        public float Depth => depth;
        /// <summary>How well the surface bears weight, 0 powder … 1 wind crust.</summary>
        public float Crust => crust;
        /// <summary>How trodden this spot is, 0 virgin … 1 a road (<see cref="SkiTrack.Packed"/>).</summary>
        public float Packed => packed;
        /// <summary>How deep this hiker is in the snow right now, metres (<see cref="Skiing.Sink"/>).</summary>
        public float Sink => sink;
        /// <summary>Standing in somebody's лыжня (including one's own, on the way back).</summary>
        public bool OnTrack => packed > .25f;

        /// <summary>The way this hiker is travelling, as the host has it.</summary>
        public Travel Mode => hiker != null ? Clamp(hiker.Mode.Value) : Travel.Foot;
        public bool OnSkis { get { var m = Mode; return m == Travel.Skis || m == Travel.Hauling; } }
        /// <summary>Hands busy with the bindings: the legs are locked until it is done.</summary>
        public bool Busy => Time.time < busyUntil;
        /// <summary>Where a rucksack sits when it is towed rather than carried — the sled's own Load marker, or null when
        /// there is no sled. PackView can hang the pack here instead of on the back while <see cref="Mode"/> is
        /// <see cref="Travel.Hauling"/>.</summary>
        public Transform SledLoad => Mode == Travel.Hauling && sled != null && sled.gameObject.activeSelf ? loadBed : null;
        /// <summary>Ground speed, m/s. Taken from the transform, not the rigidbody: a companion's body is kinematic here and
        /// its velocity is always zero, and their poles have to swing too.</summary>
        public float Pace => pace;

        static Travel Clamp(byte v) => v <= (byte)Travel.Hauling ? (Travel)v : Travel.Foot;

        /// <summary>Snow is a Kholat Syakhl matter for now; the southern slope of Elbrus has lifts and its own rules.</summary>
        static bool Off => Height1079.Core.World.IsElbrus;

        void Awake() { hiker = GetComponent<HikerController>(); }

        void Start()
        {
            visual = transform.Find("Visual");
            if (Off) return;
            skiL = Spawn("Ski_Left"); skiR = Spawn("Ski_Right");
            poleL = Spawn("Pole_Bamboo"); poleR = Spawn("Pole_Bamboo");
            bundle = Spawn("Ski_Pair_Packed");
            sled = Spawn("Ski_Sled", true);
            if (sled != null) { hitch = sled.Find("Hitch"); loadBed = sled.Find("Load"); }
            BuildRope();
            if (hiker != null && hiker.IsOwner) BuildGlide();
        }

        Transform Spawn(string name, bool inWorld = false)
        {
            var prefab = Resources.Load<GameObject>(GearDir + name);
            if (prefab == null)
            {
                if (!warned) { warned = true; Debug.LogWarning("Ski gear prefabs missing (" + GearDir + name + ") — call SkiFactory.Build from the world pipeline."); }
                return null;
            }
            var go = Instantiate(prefab, inWorld ? null : transform);
            go.name = name;
            go.SetActive(false);
            return go.transform;
        }

        void BuildRope()
        {
            if (sled == null) return;
            var go = new GameObject("TowRope");
            go.transform.SetParent(transform, false);
            rope = go.AddComponent<LineRenderer>();
            rope.useWorldSpace = true;
            rope.positionCount = RopePoints;
            rope.startWidth = rope.endWidth = .012f;
            rope.numCapVertices = 2;
            rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rope.receiveShadows = false;
            rope.sharedMaterial = RopeMaterial();
            go.SetActive(false);
        }

        const int RopePoints = 5;

        /// <summary>A lit opaque material for the tow rope. <c>Resources/Flat</c> is generated by ProjectSetup precisely so the
        /// Standard shader and its variants ship in builds; a copy of it keeps the rope out of the night's unlit white.</summary>
        static Material RopeMaterial()
        {
            if (ropeMaterial != null) return ropeMaterial;
            var template = Resources.Load<Material>("Flat");
            ropeMaterial = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            ropeMaterial.name = "TowRope";
            ropeMaterial.color = new Color(.52f, .45f, .33f);
            ropeMaterial.SetFloat("_Glossiness", .05f);
            ropeMaterial.SetFloat("_Metallic", 0f);
            return ropeMaterial;
        }

        void BuildGlide()
        {
            if (swish == null) swish = Synth.Swoosh(4211);
            glide = gameObject.AddComponent<AudioSource>();
            glide.spatialBlend = 0f;
            glide.playOnAwake = false;
        }

        void OnDestroy()
        {
            // the sled lives in the world, not under the hiker: it has to be cleared up by hand
            if (sled != null) Destroy(sled.gameObject);
        }

        // ---------------------------------------------------------------- the snow underfoot

        /// <summary>Look at the snow here again, at most a few times a second. Cheap to call every frame.</summary>
        public void Sample()
        {
            if (Off || Bootstrap.Dem == null) return;
            var p = transform.position;
            if (Time.time - sampledAt < SampleSeconds
                && (p - sampledFrom).sqrMagnitude < SampleMetres * SampleMetres) return;
            sampledAt = Time.time; sampledFrom = p;
            depth = SnowCover.Depth(Bootstrap.Dem, p.x, p.z);
            crust = SnowCover.Crust(Bootstrap.Dem, p.x, p.z);
            // the snow AHEAD, not the print under the boot — see SkiTrailFx.PackedAhead
            var look = hiker != null ? hiker.transform.forward : transform.forward;
            packed = SkiTrailFx.Instance != null ? SkiTrailFx.Instance.PackedAhead(p, look) : 0f;
            sink = Skiing.Sink(Mode, depth, crust, packed, Backpacks.CarriedKg(hiker));
        }

        /// <summary>Slope in the direction of travel, degrees, positive uphill — the sign <see cref="Skiing.Speed"/> wants.</summary>
        public float SlopeAlong(Vector3 dir)
        {
            if (Bootstrap.Dem == null) return 0f;
            var f = new Vector2(dir.x, dir.z);
            if (f.sqrMagnitude < 1e-4f) return 0f;
            f.Normalize();
            const float span = 3f;
            var p = transform.position;
            float here = TerrainBuilder.Height(Bootstrap.Dem, p.x, p.z);
            float ahead = TerrainBuilder.Height(Bootstrap.Dem, p.x + f.x * span, p.z + f.y * span);
            return Mathf.Atan2(ahead - here, span) * Mathf.Rad2Deg;
        }

        // ---------------------------------------------------------------- owner input

        /// <summary>Owner keys, called from <see cref="HikerController.Update"/>. The host decides for real; this only asks.</summary>
        public void HandleInput()
        {
            if (Off || hiker == null || !hiker.IsOwner) return;
            var session = NightSession.Instance;
            if (session == null) return;
            var mode = Mode;
            var want = mode;
            if (Controls.SkiToggle) want = OnSkis ? Travel.Foot : Travel.Skis;
            else if (Controls.PoleToggle) want = mode == Travel.Poles ? Travel.Foot : mode == Travel.Foot ? Travel.Poles : mode;
            else if (Controls.HaulToggle) want = mode == Travel.Hauling ? Travel.Skis : Travel.Hauling;
            else if (Controls.TravelCycle) want = Next(mode);
            if (want == mode || Busy) return;
            busyUntil = Time.time + SwapSeconds;
            session.RequestTravel(want);
        }

        static Travel Next(Travel mode)
        {
            for (int i = 0; i < Skiing.Modes.Length; i++)
                if (Skiing.Modes[i] == mode) return Skiing.Modes[(i + 1) % Skiing.Modes.Length];
            return Travel.Foot;
        }

        /// <summary>A short line for the HUD while the bindings are being done up.</summary>
        public string BusyNote()
        {
            if (!Busy) return "";
            return Mode == Travel.Foot ? "Снимаете лыжи · стойте"
                : Mode == Travel.Poles ? "Достаёте палки · стойте"
                : Mode == Travel.Hauling ? "Вяжете волокушу · стойте"
                : "Встаёте на лыжи · стойте";
        }

        // ---------------------------------------------------------------- the view

        void LateUpdate()
        {
            if (Off || hiker == null) return;
            Sample();
            Measure();
            float speed = pace;
            // one stride per half turn of the phase; on skis the push is longer than a step
            float cadence = OnSkis ? Mathf.Clamp(speed / 1.9f, 0f, 1.6f) : Mathf.Clamp(speed / .85f, 0f, 2.6f);
            stride += Time.deltaTime * cadence;
            if (stride > 1024f) stride -= 1024f;

            var mode = Mode;
            bool onSkis = mode == Travel.Skis || mode == Travel.Hauling;
            bool inHands = onSkis || mode == Travel.Poles;
            bool onBack = visual != null && visual.gameObject.activeSelf;   // the body is hidden in the first person

            var n = Normal(transform.position);
            var fwd = Vector3.ProjectOnPlane(transform.forward, n);
            if (fwd.sqrMagnitude < 1e-5f) fwd = transform.forward;
            var lie = Quaternion.LookRotation(fwd.normalized, n);
            var foot = transform.position - Vector3.up * sink;
            float swing = Mathf.Sin(stride * Mathf.PI);

            PlaceSkis(onSkis, onBack, lie, foot, swing);
            PlacePoles(inHands, lie, foot, swing);
            Show(bundle, mode == Travel.Foot && onBack);
            if (bundle != null && bundle.gameObject.activeSelf && visual != null)
            {
                if (bundle.parent != visual) bundle.SetParent(visual, false);
                bundle.localPosition = new Vector3(0f, .34f, -.42f);
                bundle.localRotation = Quaternion.Euler(-7f, 0f, 2f);
            }
            PlaceSled(mode == Travel.Hauling);
            Glide(speed, onSkis);
        }

        /// <summary>Ground speed from where we were a frame ago; a teleport does not count as a sprint.</summary>
        void Measure()
        {
            var p = transform.position;
            if (!walked) { walked = true; wasAt = p; pace = 0f; return; }
            float d = new Vector2(p.x - wasAt.x, p.z - wasAt.z).magnitude;
            wasAt = p;
            float v = Time.deltaTime > 0f ? d / Time.deltaTime : 0f;
            if (v > 20f) v = 0f;
            pace = Mathf.Lerp(pace, v, 1f - Mathf.Exp(-9f * Time.deltaTime));
        }

        void PlaceSkis(bool onSkis, bool onBack, Quaternion lie, Vector3 foot, float swing)
        {
            if (skiL == null || skiR == null) return;
            var mode = Mode;
            bool lashed = mode == Travel.Poles && onBack;    // the boards stand on the pack, the poles are in the hands
            Show(skiL, onSkis || lashed); Show(skiR, onSkis || lashed);
            if (!skiL.gameObject.activeSelf) return;
            if (onSkis)
            {
                // one board runs a little ahead of the other with every push; both lie on the snow, not on the ground
                float lead = swing * .10f;
                Free(skiL); Free(skiR);
                skiL.SetPositionAndRotation(foot + lie * new Vector3(-.135f, 0f, .05f + lead), lie);
                skiR.SetPositionAndRotation(foot + lie * new Vector3(.135f, 0f, .05f - lead), lie);
                return;
            }
            // lashed upright to the pack, tips over the shoulder: the pivot is under the binding, 0.95 m above the tail
            if (visual == null) return;
            if (skiL.parent != visual) skiL.SetParent(visual, false);
            if (skiR.parent != visual) skiR.SetParent(visual, false);
            skiL.localPosition = new Vector3(-.085f, 1.30f, -.40f);
            skiR.localPosition = new Vector3(.085f, 1.30f, -.40f);
            skiL.localRotation = Quaternion.Euler(-90f, 0f, -4f);
            skiR.localRotation = Quaternion.Euler(-90f, 0f, 4f);
        }

        void PlacePoles(bool inHands, Quaternion lie, Vector3 foot, float swing)
        {
            if (poleL == null || poleR == null) return;
            Show(poleL, inHands); Show(poleR, inHands);
            if (!poleL.gameObject.activeSelf) return;
            Free(poleL); Free(poleR);
            // the ferrule is the pivot, so a pole set on the snow puts its grip at about hand height by itself
            float tilt = 9f, reach = swing * 9f;
            poleL.SetPositionAndRotation(foot + lie * new Vector3(-.44f, 0f, .12f + swing * .16f), lie * Quaternion.Euler(tilt + reach, 0f, -7f));
            poleR.SetPositionAndRotation(foot + lie * new Vector3(.44f, 0f, .12f - swing * .16f), lie * Quaternion.Euler(tilt - reach, 0f, 7f));
        }

        void PlaceSled(bool hauling)
        {
            if (sled == null) return;
            Show(sled, hauling);
            if (rope != null && rope.gameObject.activeSelf != hauling) rope.gameObject.SetActive(hauling);
            if (!hauling) { sledPlaced = false; return; }
            var me = transform.position;
            if (!sledPlaced) { sledPos = me - transform.forward * RopeLength; sledPlaced = true; }
            var d = sledPos - me; d.y = 0f;
            float len = d.magnitude;
            var back = len > 1e-3f ? d / len : -transform.forward;
            // the rope only pulls: the sled is dragged up to the knot and is otherwise left where it lies
            if (len > RopeLength) sledPos = me + back * RopeLength;
            else if (len < 1.1f) sledPos = me + back * 1.1f;
            sledPos.y = Ground(sledPos.x, sledPos.z);
            var toMe = me - sledPos; toMe.y = 0f;
            var look = toMe.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toMe.normalized, Vector3.up) : sled.rotation;
            sled.SetPositionAndRotation(sledPos, look);
            DrawRope(me, look);
        }

        void DrawRope(Vector3 me, Quaternion sledRot)
        {
            if (rope == null) return;
            var waist = me + Vector3.up * .95f - transform.forward * .16f;
            var knot = hitch != null ? hitch.position : sledPos + sledRot * new Vector3(0f, .13f, 1.2f);
            for (int i = 0; i < RopePoints; i++)
            {
                float t = i / (float)(RopePoints - 1);
                var p = Vector3.Lerp(waist, knot, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * .07f;   // the rope is taut, not tight
                rope.SetPosition(i, p);
            }
        }

        void Glide(float speed, bool onSkis)
        {
            if (glide == null || swish == null) return;
            if (!onSkis || speed < .9f) return;
            if (Time.time < nextSwish) return;
            // one hiss per push, pitched well down: bamboo and wood on snow, not an arm in the air
            glide.pitch = Mathf.Lerp(.36f, .58f, Mathf.Clamp01(speed / 4f)) * Random.Range(.94f, 1.06f);
            float grip = 1f - Mathf.Clamp01(sink / .25f);
            glide.PlayOneShot(swish, Mathf.Clamp01(.05f + .11f * grip) * Mathf.Clamp01(speed / 2.5f));
            nextSwish = Time.time + Mathf.Clamp(1.9f / Mathf.Max(.5f, speed), .35f, 1.6f);
        }

        void Free(Transform t) { if (t.parent != transform) t.SetParent(transform, true); }

        static void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        /// <summary>Normal of the snow surface here, so a ski lies along the slope instead of through it. Read off the terrain
        /// rather than the hiker's own ground probe, which only runs for whoever owns the body.</summary>
        static Vector3 Normal(Vector3 p)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return Vector3.up;
            var tp = terrain.transform.position; var size = terrain.terrainData.size;
            return terrain.terrainData.GetInterpolatedNormal((p.x - tp.x) / size.x, (p.z - tp.z) / size.z);
        }

        static float Ground(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            return Bootstrap.Dem != null ? TerrainBuilder.Height(Bootstrap.Dem, x, z) : 0f;
        }
    }
}
