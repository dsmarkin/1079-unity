using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Rigidbody hiker: capsule on the slope, slope sliding, stumble on hard landings, first/third-person camera, kindling intent.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class HikerController : NetworkBehaviour
    {
        public const float WalkSpeed = 2.8f, RunSpeed = 5.8f, SlopeLimit = 42f, HardLanding = 7f;
        /// <summary>Game pace on top of <see cref="Skiing.Speed"/>. The snow rules are measured in real metres per second — a
        /// loaded man walks 1.45 m/s on firm ground — but the night runs some thirteen hours in twenty minutes and the climb to
        /// the tent is 1.7 km, so the legs run at the same multiple the rest of the game does. Only the pace is scaled: the
        /// ratios between тропёжка, a лыжня, a crust and a descent are the core's, untouched.</summary>
        public const float Pace = 1.9f;
        /// <summary>Nobody outruns this on 1950s boards in the dark, m/s.</summary>
        public const float TopGlide = 8f;
        /// <summary>Trigger name for spaces that can only be entered on all fours (the 31 Jan tent: ridge 1.05 m).</summary>
        public const string LowSpaceName = "LowSpace_TentInterior";
        const float StandHeight = 1.8f, CrawlHeight = .8f, StandEye = 1.72f, CrawlEye = .6f;

        public readonly NetworkVariable<FixedString64Bytes> Name = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Held = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> TorchOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Which light is in hand: 0 = tube flashlight on two cells, 1 = hand-dynamo "жучок".</summary>
        public readonly NetworkVariable<byte> TorchKind = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Where the camera looks (yaw, degrees): the server needs it to know whose torch is on the Menk.</summary>
        public readonly NetworkVariable<short> LookYaw = new NetworkVariable<short>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> TorchLevel = new NetworkVariable<byte>(255, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Action = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 0 idle, 1 cold, 2 kindle
        /// <summary>How this hiker is getting through the snow (<see cref="Travel"/>). The host writes it: a change of gear is
        /// asked for by RPC and granted in <see cref="NightSession.TravelRpc"/>. The group left the лабаз on skis, so a night
        /// starts with the boards on the feet.</summary>
        public readonly NetworkVariable<byte> Mode = new NetworkVariable<byte>((byte)Travel.Skis);
        /// <summary>What is left in the legs, 0..255. The host spends it through <see cref="Skiing.Effort"/> and gives it back
        /// when a hiker stands still (<see cref="NightSession.TickSkisServer"/>).</summary>
        public readonly NetworkVariable<byte> Strength = new NetworkVariable<byte>(255);
        /// <summary>Everything the mountain is doing to this one on the southern slope of Elbrus: the frostbite pools,
        /// the sickness, the heart, the kit in the rucksack and what the rules make of it. The host writes it — it is
        /// the only one that runs <see cref="Ascent.Tick"/> — and both the legs and the HUD read it
        /// (<see cref="ClimbGear"/>). Untouched on Kholat Syakhl.</summary>
        public readonly NetworkVariable<ClimbNet> Climb = new NetworkVariable<ClimbNet>();
        /// <summary>Carried by a cabin, a chair or a snow-cat. The owner writes it, because only the owner knows
        /// (<see cref="Ride"/> is a local transform); the host needs it to stop charging a passenger for the cold and
        /// to know the moment a snow-cat puts somebody down at 5 100 m.</summary>
        public readonly NetworkVariable<bool> Riding = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>What the hiker carries in the hands out of a rucksack (Core.ItemId); written by the host's PackWorld.</summary>
        public readonly NetworkVariable<StackNet> Carried = new NetworkVariable<StackNet>();
        /// <summary>How far along the guide's programme this one is (<see cref="Programme"/>). The host ticks the
        /// steps — it is the only side that can see every condition — and publishes the answer here for the card, the
        /// compass and the two maps (<see cref="Programmes"/>). Empty on Kholat Syakhl, where there is no programme.</summary>
        public readonly NetworkVariable<PlanNet> Plan = new NetworkVariable<PlanNet>();
        public static readonly System.Collections.Generic.List<HikerController> All = new System.Collections.Generic.List<HikerController>();

        public static HikerController ByClient(ulong clientId)
        {
            foreach (var h in All) if (h != null && h.OwnerClientId == clientId) return h;
            return null;
        }

        public string DisplayName => Name.Value.Length > 0 ? Name.Value.ToString() : "Путник";

        Rigidbody body;
        CapsuleCollider capsule;
        SnowTrail trail;
        Equipment equipment;
        SkiGear skis;
        ClimbGear climb;
        public Equipment Gear => equipment;
        /// <summary>Skis, poles and the волокуша, and the snow figures under the feet.</summary>
        public SkiGear Skis => skis;
        /// <summary>The mountain under the boots on the southern slope of Elbrus; silent on Kholat Syakhl.</summary>
        public ClimbGear Climbing => climb;
        Camera cam;
        Transform head;
        float yaw, pitch = .08f, orbit = 6f;
        bool firstPerson = true, grounded, kindling, paused, placed, packUi;
        float stuckFor; Vector3 stuckAt;
        Vector3 groundNormal = Vector3.up;
        float lastVerticalSpeed, stumbleUntil, impact;
        /// <summary>A catch of the toe in the crust or a tip crossed: not a fall, half a second of nothing under you.</summary>
        float tripUntil, gait;
        public bool Stumbling => Time.time < stumbleUntil;
        public bool Tripping => Time.time < tripUntil;
        public bool Paused => paused;
        public bool FirstPerson => firstPerson;
        public bool Grounded => grounded;
        /// <summary>Normal of the ground under the feet — the skis and the лыжня both lie along it.</summary>
        public Vector3 GroundNormal => groundNormal;
        public float Yaw => yaw;
        /// <summary>On all fours (inside the tent): lower eye, short capsule, slow.</summary>
        public bool Crawling { get; private set; }
        float crouch;
        static readonly Collider[] probe = new Collider[16];
        public float Speed => body != null ? body.linearVelocity.magnitude : 0f;

        public static HikerController For(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<HikerController>();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.mass = 80f;
            capsule.height = 1.8f; capsule.radius = .32f; capsule.center = new Vector3(0, .9f, 0);
            var mat = new PhysicsMaterial("hiker") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            capsule.material = mat;
            trail = GetComponent<SnowTrail>();
            if (trail == null) trail = gameObject.AddComponent<SnowTrail>();
            equipment = GetComponent<Equipment>();
            if (equipment == null) equipment = gameObject.AddComponent<Equipment>();
            skis = GetComponent<SkiGear>();
            if (skis == null) skis = gameObject.AddComponent<SkiGear>();
            climb = GetComponent<ClimbGear>();
            if (climb == null) climb = gameObject.AddComponent<ClimbGear>();
        }

        public override void OnNetworkDespawn() { All.Remove(this); }

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this)) All.Add(this);
            body.isKinematic = !IsOwner;
            // the group came up the Auspiya on skis and camped on them; the southern slope of Elbrus is walked and ridden
            if (IsServer) Mode.Value = (byte)(Height1079.Core.World.IsElbrus ? Travel.Foot : Travel.Skis);
            if (!IsOwner) return;
            Name.Value = new FixedString64Bytes(Bootstrap.PlayerName);
            head = new GameObject("Head").transform; head.SetParent(transform, false); head.localPosition = new Vector3(0, 1.72f, 0);
            cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera"; cam.nearClipPlane = .1f; cam.farClipPlane = 4500f; cam.fieldOfView = 60f;
            float goalX = Height1079.Core.World.IsElbrus ? Elbrus.WestSummit.X : WorldData.Tent.X;
            float goalZ = Height1079.Core.World.IsElbrus ? Elbrus.WestSummit.Z : WorldData.Tent.Z;
            yaw = Mathf.Atan2(transform.position.x - goalX, transform.position.z - goalZ) * Mathf.Rad2Deg + 180f;
            SetCursor(true);
            if (IsServer) NightSession.Instance?.RenameServer(OwnerClientId, DisplayName); else NameRpc(Bootstrap.PlayerName);
        }

        /// <summary>If the night already knows this participant (player object spawned after the session), place it on its spawn point.</summary>
        void Start()
        {
            if (!IsServer) return;
            var run = NightSession.Instance?.Run;
            if (run != null && run.Players.TryGetValue("c" + OwnerClientId, out var p)) TeleportServer(p.X, p.Z);
        }

        [Rpc(SendTo.Server)]
        void NameRpc(FixedString64Bytes name, RpcParams rpc = default) => NightSession.Instance?.RenameServer(rpc.Receive.SenderClientId, name.ToString());

        /// <summary>Server places the participant on the spawn ring; the owner's transform authority picks it up through the teleport RPC.</summary>
        public void TeleportServer(float x, float z)
        {
            float y = TerrainBuilder.Height(Bootstrap.Dem, x, z) + .05f;
            placed = true;
            if (IsOwner) Place(new Vector3(x, y, z)); else TeleportRpc(new Vector3(x, y, z), RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void TeleportRpc(Vector3 pos, RpcParams rpc) { placed = true; Place(pos); }

        /// <summary>Owner-side hard placement: rigidbody, transform and the network transform all agree, velocity cleared.</summary>
        void Place(Vector3 pos)
        {
            var nt = GetComponent<NetworkTransform>();
            if (nt != null && nt.CanCommitToTransform) nt.Teleport(pos, transform.rotation, transform.localScale);
            body.position = pos; transform.position = pos; body.linearVelocity = Vector3.zero;
            lastVerticalSpeed = 0f;
        }

        /// <summary>The player object can spawn before the night session exists (host) — keep asking until the run knows us.
        /// Also a safety net: anything that ends up under the slope is put back on it.</summary>
        void EnsurePlaced()
        {
            if (!IsOwner || Ride != null) return;
            if (!placed && IsServer)
            {
                var run = NightSession.Instance?.Run;
                if (run != null && run.Players.TryGetValue("c" + OwnerClientId, out var p)) TeleportServer(p.X, p.Z);
            }
            float x = transform.position.x, z = transform.position.z, g = TerrainBuilder.Height(Bootstrap.Dem, x, z);
            if (transform.position.y < g - 3f) Place(new Vector3(x, g + .05f, z));
        }

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (!IsOwner || cam == null) return;
            var session = NightSession.Instance;
            bool finished = session != null && session.MyOutcome != Outcome.None;
            if (Controls.Pause) { paused = true; SetCursor(false); }
            if (!paused && !finished && !Backpacks.UiOpen && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) SetCursor(true);
            if (paused && Input.GetMouseButtonDown(0) && !Bootstrap.PointerOverUi()) { paused = false; SetCursor(true); }
            if (Controls.ToggleView) firstPerson = !firstPerson;
            if (!paused && !finished) { equipment.HandleInput(); Backpacks.HandleInput(this); skis.HandleInput(); climb.HandleInput(); Camps.HandleInput(this); Programmes.HandleInput(this); }
            if (Backpacks.UiOpen != packUi)
            {
                packUi = Backpacks.UiOpen;
                if (!paused && !finished) SetCursor(!packUi);
            }
            if (finished && Cursor.lockState == CursorLockMode.Locked) SetCursor(false);

            if (Controls.TurnLeft) yaw -= 15f;
            if (Controls.TurnRight) yaw += 15f;
            if (Cursor.lockState == CursorLockMode.Locked && !paused && !finished)
            {
                yaw += Input.GetAxis("Mouse X") * 2.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -65f, 65f);
            }
            orbit = Mathf.Clamp(orbit - Input.mouseScrollDelta.y * .6f, 3f, 14f);

            // E works on whatever is in reach: the saw and the axe on a tree or a branch, firewood into the fire, otherwise kindling
            bool holdE = !paused && !finished && (Controls.Kindle || Bootstrap.AutoWork || Bootstrap.AutoKindle);
            var job = Backpacks.WorkHere(this);
            Backpacks.HandleWork(this, holdE && job != WorkKind.None);
            if (job != WorkKind.None) Bootstrap.AutoKindle = false;

            bool wantKindle = job == WorkKind.None && holdE && !Height1079.Core.World.IsElbrus && WorldData.NearCamp(transform.position.x, transform.position.z)
                && session != null && session.FireRemaining.Value <= 0f
                && new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude < .3f;
            if (wantKindle != kindling)
            {
                kindling = wantKindle;
                session?.KindleRpc(kindling);
                if (!kindling) Bootstrap.AutoKindle = false;
            }
            Action.Value = (byte)(kindling || Backpacks.Working ? 2 : session != null && session.Heat < 40f ? 1 : 0);
            short look = (short)Mathf.RoundToInt(Mathf.Repeat(yaw, 360f));
            if (Mathf.Abs(look - LookYaw.Value) > 1) LookYaw.Value = look;
            impact = Mathf.MoveTowards(impact, 0f, Time.deltaTime * 1.4f);
            if (Controls.JumpRoute) JumpAlongRoute();
        }

        /// <summary>The nine stages of the summit route, for jumping between them with F4 on Elbrus. The climb takes
        /// eight hours of play from the bottom, so testing what the saddle looks like by walking to it is not testing.
        /// Off the mountain the key does nothing.</summary>
        static readonly (string Name, float S)[] RouteStops =
        {
            ("Гара-Баши, 3847", 0f), ("Приют 11, 4050", 1096f), ("Скалы Пастухова, 4650", 3035f),
            ("Выход на 5100", 4031f), ("Косая полка, 5290", 4511f), ("Седловина, 5382", 5451f),
            ("Вершинный взлёт, 5450", 5806f),
            // eighty metres short of the top: standing on the summit itself ends the run the moment you land, and the
            // point of the key is to look around up there, not to win
            ("Вершинное плато, 5630", 6600f), ("Поляна Азау, 2350", -1f),
        };
        int routeStop = -1;

        /// <summary>Owner only: step to the next stage of the route and stand there.</summary>
        void JumpAlongRoute()
        {
            if (!IsOwner || !Height1079.Core.World.IsElbrus || Bootstrap.Dem == null) return;
            routeStop = (routeStop + 1) % RouteStops.Length;
            var stop = RouteStops[routeStop];
            float x, z;
            if (stop.S < 0f) { x = Elbrus.Start.x; z = Elbrus.Start.z; }
            else { var p = Elbrus.PointAt(Elbrus.SummitRoute, stop.S); x = p.x; z = p.z; }
            if (Ride != null) LeaveRide(new Vector3(x, Bootstrap.Dem.Sample(x, z) + .1f, z));
            else Place(new Vector3(x, Bootstrap.Dem.Sample(x, z) + .6f, z));
            stumbleUntil = 0f; tripUntil = 0f; impact = 0f; stuckFor = 0f; stuckAt = transform.position;
            // and arrive able to walk: kitted out, crampons on, acclimatised (NightSession.DebugOutfit)
            NightSession.Instance?.DebugOutfit();
            Bootstrap.Hud?.SetStatus($"F4: {stop.Name} · снаряжение выдано");
        }

        /// <summary>A cabin moves its own transform in <c>Update</c>, and the passenger used to be snapped to the seat in
        /// <c>FixedUpdate</c>: fifty times a second against a frame rate of sixty-odd, and at eight times speed the car
        /// covers most of a metre between two physics steps. The rider lagged behind the seat and caught up in jerks, and
        /// the camera, read a frame earlier still, shook with him. Follow the seat here instead — after every Update has
        /// run, so the seat is where it will be drawn — and move the camera last of all.</summary>
        void LateUpdate()
        {
            if (IsOwner && Ride != null)
            {
                var seat = Ride.position;
                body.position = seat; transform.position = seat;
                body.rotation = Ride.rotation; transform.rotation = Ride.rotation;
            }
            if (IsOwner) UpdateCamera();
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;
            if (Ride != null)
            {
                // carried by a cabin, a chair or a snow-cat: the vehicle owns the position (see LateUpdate), the legs do nothing
                body.linearVelocity = Vector3.zero;
                lastVerticalSpeed = 0f;
                // the HUD still wants to know where on the mountain the cabin has got to
                if (climb != null && Height1079.Core.World.IsElbrus) climb.Sample(Vector3.zero);
                return;
            }
            EnsurePlaced();
            UpdateLowSpace();
            var session = NightSession.Instance;
            bool finished = session != null && session.MyOutcome != Outcome.None;
            // Ground probe: capsule cast a little below the feet.
            grounded = Physics.SphereCast(transform.position + Vector3.up * .5f, .3f, Vector3.down, out var hit, .45f, ~0, QueryTriggerInteraction.Ignore);
            groundNormal = grounded ? hit.normal : Vector3.up;
            // the fat probe can graze a wall or a doorway jamb (tent): trust the floor right under the feet if it is walkable
            if (grounded && Vector3.Angle(groundNormal, Vector3.up) > SlopeLimit
                && Physics.Raycast(transform.position + Vector3.up * .3f, Vector3.down, out var under, .6f, ~0, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(under.normal, Vector3.up) <= SlopeLimit)
                groundNormal = under.normal;
            var v = body.linearVelocity;
            if (grounded && lastVerticalSpeed < -HardLanding) { StumbleRpc(); stumbleUntil = Time.time + 1.2f; }
            lastVerticalSpeed = v.y;

            // the southern slope of Elbrus: the mountain has a say about the next step before the legs do
            bool climbing = climb != null && Height1079.Core.World.IsElbrus;
            bool locked = paused || finished || Stumbling || (skis != null && skis.Busy)
                || (climbing && (climb.Busy || climb.Sliding));
            float f = locked ? 0f : (Controls.Forward ? 1f : 0f) - (Controls.Back ? 1f : 0f);
            float r = locked ? 0f : (Controls.Right ? 1f : 0f) - (Controls.Left ? 1f : 0f);
            Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward, right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var wish = (forward * f + right * r); if (wish.sqrMagnitude > 1f) wish.Normalize();
            // what is carried: heavy loads slow the legs and running needs a light pack
            float load = Backpacks.CarriedKg(this);
            // read the ground under the boots and the pools the host keeps, then let the gear gate cut the wish down:
            // above the rocks without the kit the uphill component is simply gone
            if (climbing) { climb.Sample(wish); wish = climb.Allow(wish); }
            // above 4 600 m nobody runs. Not "running costs more" — the move is gone (Ascent.MayRun)
            bool hurry = Controls.Run && !Crawling && load < SurvivalRules.RunLimitKg && (!climbing || climb.MayRun);
            bool snow = skis != null && Bootstrap.Dem != null && !Height1079.Core.World.IsElbrus;
            var mode = snow ? skis.Mode : Travel.Foot;
            bool gliding = snow && (mode == Travel.Skis || mode == Travel.Hauling);
            float sunk = 0f, speed;
            if (snow)
            {
                // the whole difference between walking and skiing lives in this one call: how deep you are, what the surface
                // bears, whether somebody has been here before you, and which way the ground tilts under the next step
                skis.Sample();
                sunk = skis.Sink;
                float grade = skis.SlopeAlong(wish.sqrMagnitude > .01f ? wish : transform.forward);
                float hands = session != null ? session.Hands : 100f;
                speed = Skiing.Speed(mode, skis.Depth, skis.Crust, skis.Packed, grade, load, hands) * Pace;
                if (hurry) speed *= gliding ? 1.3f : 1.9f;
                // what the host says is left in the legs
                speed *= Mathf.Lerp(.5f, 1f, Mathf.Clamp01(Strength.Value / 255f / .35f));
                // a stumble is a chance per metre, so it is rolled against the metres actually covered
                float step = new Vector2(v.x, v.z).magnitude * Time.fixedDeltaTime;
                if (grounded && step > 0f && !Tripping
                    && Random.value < Skiing.Stumble(mode, skis.Depth, skis.Crust, skis.Packed, grade, load, hands) * step)
                    tripUntil = Time.time + .5f;
                if (Tripping) speed *= .3f;
            }
            else
            {
                speed = hurry ? RunSpeed : WalkSpeed;
                speed *= SurvivalRules.LoadSpeedFactor(load);
                // surface × thin air × mountain sickness × drowsiness, and nothing at all while the heart is making
                // him stand and breathe (Ascent.SpeedFactor, Ascent.Report.MustStop)
                if (climbing) speed *= climb.SpeedFactor;
            }
            speed *= Mathf.Lerp(1f, .35f, crouch);
            // Cold slows the legs: clarity/heat below 40 costs up to 35 % of speed.
            if (session != null) speed *= Mathf.Lerp(.65f, 1f, Mathf.Clamp01(session.Heat / 40f));

            float slope = Vector3.Angle(groundNormal, Vector3.up);
            if (grounded && climbing && climb.Sliding)
            {
                // the feet went and the axe did not hold: nothing the player does matters until the run-out is spent.
                // On the косая полка that is three to six hundred metres down the line of the water (Ascent.RunoutM)
                var fall = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                body.AddForce(fall * ClimbGear.SlidePull, ForceMode.Acceleration);
                var run = body.linearVelocity;
                var flat = new Vector2(run.x, run.z);
                if (flat.magnitude > ClimbGear.SlideTopMs)
                {
                    flat = flat.normalized * ClimbGear.SlideTopMs;
                    body.linearVelocity = new Vector3(flat.x, run.y, flat.y);
                }
                climb.Slid(flat.magnitude * Time.fixedDeltaTime);
                if (!climb.Sliding) stumbleUntil = Time.time + 1.6f;
            }
            else if (grounded && slope <= SlopeLimit && Stumbling && impact > 0f)
            {
                // thrown by a blow: let the body fly and skid, only gravity and snow drag it
                body.linearVelocity = new Vector3(v.x * .96f, v.y, v.z * .96f);
            }
            else if (grounded && slope <= SlopeLimit)
            {
                bool pushing = wish.sqrMagnitude >= .01f;
                bool wedged = Wedged(pushing);
                if (pushing && !wedged) StepOver(wish);
                // Boots hold on a slope a man can walk. The capsule is frictionless on purpose — with friction it catches on
                // walls, doorways and the lip of every step — so nothing in the physics opposes the pull down the fall line:
                // gravity put a little downhill speed into the body between fixed steps, the damping below took only part of
                // it back, and the hiker crept downhill for ever however still the player stood. Cancel that pull here; the
                // skis, further down, put back the part of it that gets through an edge.
                // While wedged the hold is let go and the velocity below is left alone, so depenetration and gravity can
                // work the body out of the seam instead of being overwritten fifty times a second.
                if (!wedged) body.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, groundNormal), ForceMode.Acceleration);
                else { body.AddForce(Vector3.up * 5.5f - wish * 2.5f, ForceMode.Acceleration); return; }
                if (gliding && !pushing)
                {
                    // stop pushing and the boards run on: metres of it on a crust or in a made лыжня, one stride in powder
                    float keep = Mathf.Exp(-(.5f + 7f * Mathf.Clamp01(sunk / .3f)) * Time.fixedDeltaTime);
                    body.linearVelocity = new Vector3(v.x * keep, v.y, v.z * keep);
                }
                else
                {
                    var along = Vector3.ProjectOnPlane(wish, groundNormal).normalized * wish.magnitude * speed;
                    var target = new Vector3(along.x, v.y, along.z);
                    // a boot bites, a board does not: skis take their time to come up to speed, and deep snow eats the step
                    // before it is finished, so a man wading has little to push against either
                    float grab = gliding ? 3.2f : Mathf.Lerp(12f, 6.5f, Mathf.Clamp01(sunk / .45f));
                    body.linearVelocity = Vector3.Lerp(v, target, 1f - Mathf.Exp(-grab * Time.fixedDeltaTime));
                    if (!pushing)
                    {
                        // let go of the keys and the legs stop: a hand's width of run-out, then stand still. Without the
                        // floor the last centimetres per second never die and the view drifts while the player is reading.
                        float x = v.x * .6f, z = v.z * .6f;
                        if (x * x + z * z < .04f) { x = 0f; z = 0f; }
                        body.linearVelocity = new Vector3(x, v.y, z);
                    }
                }
                if (gliding)
                {
                    // the fall line has hold of a waxed pair: they run away downhill and slip back on a hard climb, which is
                    // why a steep blown-clear slope is walked and not skied
                    var downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);   // its length is sin(slope)
                    float hold = Mathf.Lerp(.22f, .85f, skis.Crust) * (1f - .7f * Mathf.Clamp01(sunk / .25f)) * (1f - .35f * skis.Packed);
                    // standing, a skier sets his edges across the fall line and holds: only a slope worth the name takes him
                    float edge = pushing ? 1f : Mathf.InverseLerp(9f, 20f, slope);
                    body.AddForce(downhill * (9.81f * hold * edge), ForceMode.Acceleration);
                    var run = body.linearVelocity;
                    var flat = new Vector2(run.x, run.z);
                    if (flat.magnitude > TopGlide) { flat = flat.normalized * TopGlide; body.linearVelocity = new Vector3(flat.x, run.y, flat.y); }
                }
                if (v.y < .5f) body.AddForce(-groundNormal * 30f, ForceMode.Acceleration); // keep the feet on steep snow
            }
            else if (grounded)
            {
                // Too steep: slide down the fall line, players can only steer a little.
                var down = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                body.AddForce(down * 9f + wish * 2f, ForceMode.Acceleration);
            }
            else body.AddForce(wish * 1.5f, ForceMode.Acceleration);

            // the wind of the funnel and the ataxia of the mountain sickness both push the same way — toward the fall
            // line, which on the shelf is where they kill (Ascent.Report.DriftMs)
            if (grounded && climbing && !climb.Sliding)
            {
                var push = climb.Drift(forward);
                if (push.sqrMagnitude > 1e-4f)
                {
                    var now = body.linearVelocity;
                    body.linearVelocity = new Vector3(now.x + push.x, now.y, now.z + push.z);
                }
            }

            if (wish.sqrMagnitude > .01f)
            {
                var face = Quaternion.LookRotation(new Vector3(wish.x, 0, wish.z));
                body.MoveRotation(Quaternion.Slerp(body.rotation, face, 1f - Mathf.Exp(-14f * Time.fixedDeltaTime)));
            }
        }

        /// <summary>Nothing in the physics lifts a capsule over a kerb: a doorstep, the lip of a porch, a rail or a pipe
        /// stops the hiker dead, and with the boots holding him on the slope he cannot even slither off it. Probe at ankle
        /// height along the way he is pushing; if something is there, the same line a step higher is clear and the top of
        /// it is walkable, set him on it. This is what the buildings at Azau and the huts above are made of.</summary>
        const float StepUp = .45f;

        void StepOver(Vector3 wish)
        {
            var dir = new Vector3(wish.x, 0f, wish.z);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            var foot = transform.position + Vector3.up * .12f;
            float reach = capsule.radius + .3f;
            if (!Physics.Raycast(foot, dir, reach, ~0, QueryTriggerInteraction.Ignore)) return;
            if (Physics.Raycast(transform.position + Vector3.up * (StepUp + .15f), dir, reach + .05f, ~0, QueryTriggerInteraction.Ignore)) return;
            var above = transform.position + dir * reach + Vector3.up * (StepUp + .35f);
            if (!Physics.Raycast(above, Vector3.down, out var top, StepUp + .5f, ~0, QueryTriggerInteraction.Ignore)) return;
            float rise = top.point.y - transform.position.y;
            if (rise < .04f || rise > StepUp) return;
            if (Vector3.Angle(top.normal, Vector3.up) > SlopeLimit) return;
            var lifted = transform.position + Vector3.up * (rise + .06f);
            body.position = lifted; transform.position = lifted;
            lastVerticalSpeed = 0f;
        }

        /// <summary>True when the player is asking to move and the body has not gone anywhere for a while — jammed in a
        /// seam between two props, or in a corner the capsule cannot leave on its own.</summary>
        bool Wedged(bool pushing)
        {
            if (!pushing) { stuckFor = 0f; stuckAt = transform.position; return false; }
            if ((transform.position - stuckAt).sqrMagnitude > .09f) { stuckFor = 0f; stuckAt = transform.position; return false; }
            stuckFor += Time.fixedDeltaTime;
            return stuckFor > .45f;
        }

        bool InLowSpace()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .3f, .25f, probe, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++) if (probe[i].isTrigger && probe[i].name == LowSpaceName) return true;
            return false;
        }

        bool HeadBlocked()
        {
            var p = transform.position;
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * (CrawlHeight + .05f), p + Vector3.up * (StandHeight - .3f), .26f, probe, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (probe[i].attachedRigidbody != body && !(probe[i] is TerrainCollider)) return true;
            return false;
        }

        void UpdateLowSpace()
        {
            bool low = InLowSpace() || (Crawling && HeadBlocked());
            Crawling = low;
            crouch = Mathf.MoveTowards(crouch, low ? 1f : 0f, Time.fixedDeltaTime * 2.5f);
            float h = Mathf.Lerp(StandHeight, CrawlHeight, crouch);
            capsule.height = h; capsule.center = new Vector3(0, h / 2, 0);
            capsule.radius = Mathf.Lerp(.32f, .28f, crouch);
            if (head != null) head.localPosition = new Vector3(0, Mathf.Lerp(StandEye, CrawlEye, crouch), 0);
        }

        /// <summary>Seat of the vehicle carrying this hiker (cabin, chair, snow-cat), or null when on foot.</summary>
        public Transform Ride { get; private set; }

        /// <summary>Owner only: step into a vehicle. The hiker stops colliding with the world and follows the seat.</summary>
        public void BoardRide(Transform seat)
        {
            if (!IsOwner || seat == null || Ride != null) return;
            Ride = seat;
            Riding.Value = true;
            climb?.StopSlide();
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            // interpolation draws a kinematic body where it was a physics step ago; on a moving cabin that is a lag of
            // its own on top of everything else, and at speed it reads as a shudder
            body.interpolation = RigidbodyInterpolation.None;
            capsule.enabled = false;
        }

        /// <summary>Owner only: step out onto the given point.</summary>
        public void LeaveRide(Vector3 pos)
        {
            if (!IsOwner || Ride == null) return;
            Ride = null;
            Riding.Value = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            capsule.enabled = true;
            Place(pos);
        }

        /// <summary>Owner only: a blow throws the hiker, knocks them down for <paramref name="stun"/> seconds and shakes the view.</summary>
        public void Knock(Vector3 impulse, float stun)
        {
            if (!IsOwner || body == null) return;
            body.AddForce(impulse, ForceMode.VelocityChange);
            stumbleUntil = Time.time + stun;
            impact = 1f;
            pitch = Mathf.Clamp(pitch + Random.Range(-20f, 25f), -65f, 65f);
            yaw += Random.Range(-35f, 35f);
        }

        [Rpc(SendTo.Server)]
        void StumbleRpc(RpcParams rpc = default)
        {
            var run = NightSession.Instance?.Run;
            if (run == null || !run.Players.TryGetValue("c" + rpc.Receive.SenderClientId, out var p) || p.Outcome != Outcome.None) return;
            p.Heat = Mathf.Max(0f, p.Heat - 6f); p.Clarity = Mathf.Max(0f, p.Clarity - 8f);
            run.Record($"{p.Name} падает на склоне.");
        }

        /// <summary>How the head moves with the gait — the thing that has to feel different before anything else does.
        /// On foot every stride is a leg pulled out of a hole and put back into one: the head heaves up and down, rolls, and
        /// the deeper the snow the heavier it gets. On skis nothing is lifted at all: the body sways along the boards, rolls a
        /// little from ski to ski, and the view stays level. <paramref name="heave"/> is metres up or down,
        /// <paramref name="drift"/> the small push and glide along the way you are going.</summary>
        Quaternion Gait(out float heave, out Vector3 drift)
        {
            heave = 0f; drift = Vector3.zero;
            if (skis == null || body == null || Height1079.Core.World.IsElbrus) return Quaternion.identity;
            float v = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
            bool glide = skis.OnSkis;
            float cadence = glide ? Mathf.Clamp(v / 1.9f, 0f, 1.5f) : Mathf.Clamp(v / .85f, 0f, 2.6f);
            gait += Time.deltaTime * cadence * Mathf.PI;
            if (gait > 2048f) gait -= 2048f;
            float drive = Mathf.Clamp01(v / (glide ? 2.2f : 1.4f)) * (1f - crouch);
            if (drive < .01f) return Quaternion.identity;
            float wave = Mathf.Sin(gait);
            if (glide)
            {
                heave = Mathf.Sin(gait * 2f) * .009f * drive;
                drift = transform.forward * (wave * .035f * drive);
                return Quaternion.Euler(Mathf.Sin(gait * 2f) * .6f * drive, 0f, wave * 1.4f * drive);
            }
            float deep = Mathf.Clamp01(skis.Sink / .45f);
            heave = -Mathf.Abs(wave) * Mathf.Lerp(.012f, .06f, deep) * drive;
            return Quaternion.Euler(Mathf.Sin(gait * 2f) * Mathf.Lerp(.8f, 2.2f, deep) * drive,
                                    wave * .8f * drive,
                                    wave * Mathf.Lerp(1f, 3.4f, deep) * drive);
        }

        void UpdateCamera()
        {
            if (WorldDressing.UpdateView(cam, Bootstrap.Dem)) return;
            if (MenkView.Watch(cam)) return;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                if (visual.gameObject.activeSelf == firstPerson) visual.gameObject.SetActive(!firstPerson);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(Mathf.Max(Stumbling ? 38f : 0f, 70f * crouch), 0f, 0f), 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            if (firstPerson)
            {
                float shake = NightSession.Instance != null ? (100f - NightSession.Instance.Hands) * .00015f * Mathf.Sin(Time.time * 9f) : 0f;
                // how far down into the snow you actually are (the tent floor is trampled, so crouching cancels it)
                float sunk = Mathf.Max(trail != null ? trail.Sink : 0f, skis != null ? skis.Sink : 0f) * (1f - crouch);
                // a blow or the giant's steps close by shake the view
                float jolt = impact * impact * 9f + MenkView.Tremor * 1.2f;
                if (jolt > .01f) rot *= Quaternion.Euler((Mathf.PerlinNoise(Time.time * 23f, 1f) - .5f) * jolt, (Mathf.PerlinNoise(Time.time * 19f, 7f) - .5f) * jolt, (Mathf.PerlinNoise(Time.time * 17f, 3f) - .5f) * jolt * 1.5f);
                float drop = Stumbling ? .9f * Mathf.Clamp01((stumbleUntil - Time.time) * 1.5f) * impact : 0f;
                rot *= Gait(out float heave, out Vector3 drift);
                if (Tripping)
                {
                    float catchUp = Mathf.Clamp01((tripUntil - Time.time) * 2.2f);
                    rot *= Quaternion.Euler(8f * catchUp, 0f, 6f * catchUp);
                    drop += .10f * catchUp;
                }
                cam.transform.SetPositionAndRotation(head.position + Vector3.up * (shake - sunk - drop + heave) + drift, rot);
                return;
            }
            var pivot = transform.position + Vector3.up * Mathf.Lerp(1.35f, .55f, crouch);
            var desired = pivot - rot * Vector3.forward * orbit;
            if (Physics.SphereCast(pivot, .25f, (desired - pivot).normalized, out var hit, orbit, ~0, QueryTriggerInteraction.Ignore))
                desired = pivot + (desired - pivot).normalized * Mathf.Max(1f, hit.distance - .1f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
            cam.transform.LookAt(pivot);
        }
    }
}
