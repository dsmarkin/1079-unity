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
        /// <summary>Trigger name for spaces that can only be entered on all fours (the 31 Jan tent: ridge 1.05 m).</summary>
        public const string LowSpaceName = "LowSpace_TentInterior";
        const float StandHeight = 1.8f, CrawlHeight = .8f, StandEye = 1.72f, CrawlEye = .6f;

        public readonly NetworkVariable<FixedString64Bytes> Name = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Held = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> TorchOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Which light is in hand: 0 = tube flashlight on two cells, 1 = hand-dynamo "жучок".</summary>
        public readonly NetworkVariable<byte> TorchKind = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> TorchLevel = new NetworkVariable<byte>(255, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Action = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 0 idle, 1 cold, 2 kindle

        public string DisplayName => Name.Value.Length > 0 ? Name.Value.ToString() : "Путник";

        Rigidbody body;
        CapsuleCollider capsule;
        SnowTrail trail;
        Equipment equipment;
        public Equipment Gear => equipment;
        Camera cam;
        Transform head;
        float yaw, pitch = .08f, orbit = 6f;
        bool firstPerson = true, grounded, kindling, paused, placed;
        Vector3 groundNormal = Vector3.up;
        float lastVerticalSpeed, stumbleUntil;
        public bool Stumbling => Time.time < stumbleUntil;
        public bool Paused => paused;
        public bool FirstPerson => firstPerson;
        public bool Grounded => grounded;
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
        }

        public override void OnNetworkSpawn()
        {
            body.isKinematic = !IsOwner;
            if (!IsOwner) return;
            Name.Value = new FixedString64Bytes(Bootstrap.PlayerName);
            head = new GameObject("Head").transform; head.SetParent(transform, false); head.localPosition = new Vector3(0, 1.72f, 0);
            cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera"; cam.nearClipPlane = .1f; cam.farClipPlane = 4500f; cam.fieldOfView = 60f;
            yaw = Mathf.Atan2(transform.position.x - WorldData.Tent.X, transform.position.z - WorldData.Tent.Z) * Mathf.Rad2Deg + 180f;
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
            if (!IsOwner) return;
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
            if (!paused && !finished && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) SetCursor(true);
            if (paused && Input.GetMouseButtonDown(0) && !Bootstrap.PointerOverUi()) { paused = false; SetCursor(true); }
            if (Controls.ToggleView) firstPerson = !firstPerson;
            if (!paused && !finished) equipment.HandleInput();
            if (finished && Cursor.lockState == CursorLockMode.Locked) SetCursor(false);

            if (Cursor.lockState == CursorLockMode.Locked && !paused && !finished)
            {
                yaw += Input.GetAxis("Mouse X") * 2.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -65f, 65f);
            }
            orbit = Mathf.Clamp(orbit - Input.mouseScrollDelta.y * .6f, 3f, 14f);

            bool wantKindle = !paused && !finished && WorldData.NearCamp(transform.position.x, transform.position.z)
                && (Controls.Kindle || Bootstrap.AutoKindle) && session != null && session.FireRemaining.Value <= 0f
                && new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude < .3f;
            if (wantKindle != kindling)
            {
                kindling = wantKindle;
                session?.KindleRpc(kindling);
                if (!kindling) Bootstrap.AutoKindle = false;
            }
            Action.Value = (byte)(kindling ? 2 : session != null && session.Heat < 40f ? 1 : 0);
            UpdateCamera();
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;
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

            bool locked = paused || finished || Stumbling;
            float f = locked ? 0f : (Controls.Forward ? 1f : 0f) - (Controls.Back ? 1f : 0f);
            float r = locked ? 0f : (Controls.Right ? 1f : 0f) - (Controls.Left ? 1f : 0f);
            Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward, right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var wish = (forward * f + right * r); if (wish.sqrMagnitude > 1f) wish.Normalize();
            float speed = Controls.Run && !Crawling ? RunSpeed : WalkSpeed;
            speed *= Mathf.Lerp(1f, .35f, crouch);
            // Cold slows the legs: clarity/heat below 40 costs up to 35 % of speed.
            if (session != null) speed *= Mathf.Lerp(.65f, 1f, Mathf.Clamp01(session.Heat / 40f));
            // Trail-breaking: virgin powder is slow, a path already trodden by the group is fast.
            if (SnowFx.Instance != null && Bootstrap.Dem != null)
            {
                var p = transform.position;
                speed *= SnowFx.Instance.SpeedFactor(p.x, p.z, TerrainBuilder.Height(Bootstrap.Dem, p.x, p.z));
            }

            float slope = Vector3.Angle(groundNormal, Vector3.up);
            if (grounded && slope <= SlopeLimit)
            {
                var along = Vector3.ProjectOnPlane(wish, groundNormal).normalized * wish.magnitude * speed;
                var target = new Vector3(along.x, v.y, along.z);
                body.linearVelocity = Vector3.Lerp(v, target, 1f - Mathf.Exp(-12f * Time.fixedDeltaTime));
                if (wish.sqrMagnitude < .01f) body.linearVelocity = new Vector3(v.x * .6f, v.y, v.z * .6f);
                if (v.y < .5f) body.AddForce(-groundNormal * 30f, ForceMode.Acceleration); // keep the feet on steep snow
            }
            else if (grounded)
            {
                // Too steep: slide down the fall line, players can only steer a little.
                var down = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                body.AddForce(down * 9f + wish * 2f, ForceMode.Acceleration);
            }
            else body.AddForce(wish * 1.5f, ForceMode.Acceleration);

            if (wish.sqrMagnitude > .01f)
            {
                var face = Quaternion.LookRotation(new Vector3(wish.x, 0, wish.z));
                body.MoveRotation(Quaternion.Slerp(body.rotation, face, 1f - Mathf.Exp(-14f * Time.fixedDeltaTime)));
            }
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

        [Rpc(SendTo.Server)]
        void StumbleRpc(RpcParams rpc = default)
        {
            var run = NightSession.Instance?.Run;
            if (run == null || !run.Players.TryGetValue("c" + rpc.Receive.SenderClientId, out var p) || p.Outcome != Outcome.None) return;
            p.Heat = Mathf.Max(0f, p.Heat - 6f); p.Clarity = Mathf.Max(0f, p.Clarity - 8f);
            run.Record($"{p.Name} падает на склоне.");
        }

        void UpdateCamera()
        {
            if (WorldDressing.UpdateView(cam, Bootstrap.Dem)) return;
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
                float sunk = (trail != null ? trail.Sink : 0f) * (1f - crouch); // the tent floor is trampled
                cam.transform.SetPositionAndRotation(head.position + Vector3.up * (shake - sunk), rot);
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
