using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Owner-driven movement so the local player feels responsive; the server still decides the night.</summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }

    /// <summary>Rigidbody hiker: capsule on the slope, slope sliding, stumble on hard landings, first/third-person camera, kindling intent.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class HikerController : NetworkBehaviour
    {
        public const float WalkSpeed = 2.8f, RunSpeed = 5.8f, SlopeLimit = 42f, HardLanding = 7f;

        public readonly NetworkVariable<FixedString64Bytes> Name = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Action = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); // 0 idle, 1 cold, 2 kindle

        public string DisplayName => Name.Value.Length > 0 ? Name.Value.ToString() : "Путник";

        Rigidbody body;
        CapsuleCollider capsule;
        Camera cam;
        Transform head;
        float yaw, pitch = .08f, orbit = 6f;
        bool firstPerson = true, grounded, kindling, paused;
        Vector3 groundNormal = Vector3.up;
        float lastVerticalSpeed, stumbleUntil;
        public bool Stumbling => Time.time < stumbleUntil;
        public bool Paused => paused;
        public bool FirstPerson => firstPerson;
        public float Yaw => yaw;

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
            if (IsOwner) transform.position = new Vector3(x, y, z); else TeleportRpc(new Vector3(x, y, z), RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void TeleportRpc(Vector3 pos, RpcParams rpc)
        {
            GetComponent<NetworkTransform>().Teleport(pos, transform.rotation, transform.localScale);
            body.position = pos;
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
            if (Input.GetKeyDown(KeyCode.Escape)) { paused = true; SetCursor(false); }
            if (!paused && !finished && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) SetCursor(true);
            if (paused && Input.GetMouseButtonDown(0) && !Bootstrap.PointerOverUi()) { paused = false; SetCursor(true); }
            if (Input.GetKeyDown(KeyCode.V)) firstPerson = !firstPerson;
            if (finished && Cursor.lockState == CursorLockMode.Locked) SetCursor(false);

            if (Cursor.lockState == CursorLockMode.Locked && !paused && !finished)
            {
                yaw += Input.GetAxis("Mouse X") * 2.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -65f, 65f);
            }
            orbit = Mathf.Clamp(orbit - Input.mouseScrollDelta.y * .6f, 3f, 14f);

            bool wantKindle = !paused && !finished && WorldData.NearCamp(transform.position.x, transform.position.z)
                && (Input.GetKey(KeyCode.E) || Bootstrap.AutoKindle) && session != null && session.FireRemaining.Value <= 0f
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
            var session = NightSession.Instance;
            bool finished = session != null && session.MyOutcome != Outcome.None;
            // Ground probe: capsule cast a little below the feet.
            grounded = Physics.SphereCast(transform.position + Vector3.up * .5f, .3f, Vector3.down, out var hit, .45f, ~0, QueryTriggerInteraction.Ignore);
            groundNormal = grounded ? hit.normal : Vector3.up;
            var v = body.linearVelocity;
            if (grounded && lastVerticalSpeed < -HardLanding) { StumbleRpc(); stumbleUntil = Time.time + 1.2f; }
            lastVerticalSpeed = v.y;

            bool locked = paused || finished || Stumbling;
            float f = locked ? 0f : (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float r = locked ? 0f : (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward, right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var wish = (forward * f + right * r); if (wish.sqrMagnitude > 1f) wish.Normalize();
            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? RunSpeed : WalkSpeed;
            // Cold slows the legs: clarity/heat below 40 costs up to 35 % of speed.
            if (session != null) speed *= Mathf.Lerp(.65f, 1f, Mathf.Clamp01(session.Heat / 40f));

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
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                if (visual.gameObject.activeSelf == firstPerson) visual.gameObject.SetActive(!firstPerson);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(Stumbling ? 38f : 0f, 0f, 0f), 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            if (firstPerson)
            {
                float shake = NightSession.Instance != null ? (100f - NightSession.Instance.Hands) * .00015f * Mathf.Sin(Time.time * 9f) : 0f;
                cam.transform.SetPositionAndRotation(head.position + Vector3.up * shake, rot);
                return;
            }
            var pivot = transform.position + Vector3.up * 1.35f;
            var desired = pivot - rot * Vector3.forward * orbit;
            if (Physics.SphereCast(pivot, .25f, (desired - pivot).normalized, out var hit, orbit, ~0, QueryTriggerInteraction.Ignore))
                desired = pivot + (desired - pivot).normalized * Mathf.Max(1f, hit.distance - .1f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
            cam.transform.LookAt(pivot);
        }
    }
}
