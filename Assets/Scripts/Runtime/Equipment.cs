using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    public enum HeldItem : byte { None = 0, Compass = 1, Flashlight = 2, Map = 3 }

    /// <summary>What a hiker holds and whether the flashlight is on. Owner: keys, battery; everyone: models in hand, the beam, the needle.
    /// Compass: magnetic declination here is 19°05′ E (KAN 2012 map), so the needle points 19° east of true north.
    /// Flashlight: two round cells, ~12 minutes of light in the frost, dims and flickers before it dies; the switch stays on
    /// (the flashlight found 450 m below the tent was switched on with a dead battery).</summary>
    public sealed class Equipment : MonoBehaviour
    {
        public const float Declination = 19.08f;
        public const float BatterySeconds = 12f * 60f;

        HikerController hiker;
        Transform compass, needle;
        readonly Transform[] torches = new Transform[2];
        readonly Transform[] beamOrigins = new Transform[2];
        readonly Material[] lensMats = new Material[2];
        Light beam;
        int beamKind = -1;
        /// <summary>Dynamo charge of the "жучок": squeezing (F held) winds it up, it runs down in a few seconds.</summary>
        public float Dynamo { get; private set; }
        public const float DynamoWind = 1.4f, DynamoRunDown = .35f;
        public bool IsZhuchok => hiker != null && hiker.TorchKind.Value == 1;
        float needleAngle, needleVel;
        public float Battery { get; private set; } = 1f;

        void Awake() { hiker = GetComponent<HikerController>(); }

        void Start()
        {
            compass = Spawn("Compass");
            torches[0] = Spawn("Flashlight");
            torches[1] = Spawn("FlashlightZhuchok");
            if (compass != null) needle = compass.Find("Needle");
            for (int i = 0; i < 2; i++)
            {
                if (torches[i] == null) continue;
                beamOrigins[i] = torches[i].Find("Lens/BeamOrigin") ?? torches[i];
                var lens = torches[i].Find("Lens");
                if (lens != null) lensMats[i] = lens.GetComponent<MeshRenderer>().material;
            }
            var go = new GameObject("Beam", typeof(Light));
            beam = go.GetComponent<Light>();
            beam.type = LightType.Spot; beam.spotAngle = 38f; beam.innerSpotAngle = 12f; beam.range = 38f;
            beam.color = new Color(1f, .83f, .6f); beam.shadows = LightShadows.Soft; beam.shadowStrength = .9f;
            beam.renderMode = LightRenderMode.ForcePixel; beam.shadowNearPlane = .3f;
            beam.cookie = BeamCookie();
            beam.enabled = false;
        }

        Transform Spawn(string name)
        {
            var p = Resources.Load<GameObject>("World/Prefabs/Items/" + name);
            if (p == null) { Debug.LogWarning("Item prefab missing: " + name); return null; }
            var t = Instantiate(p, transform).transform;
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>Owner input, called from HikerController.Update.</summary>
        public void HandleInput()
        {
            if (Controls.ItemCompass) Toggle(HeldItem.Compass);
            if (Controls.ItemTorch)
            {
                // 2 takes a light; pressed again with a light in hand, it swaps the tube flashlight and the "жучок"
                if ((HeldItem)hiker.Held.Value == HeldItem.Flashlight) { hiker.TorchKind.Value = (byte)(1 - hiker.TorchKind.Value); hiker.TorchOn.Value = false; }
                else hiker.Held.Value = (byte)HeldItem.Flashlight;
            }
            if (Controls.ItemMap) Toggle(HeldItem.Map);
            if (Controls.ItemNone) hiker.Held.Value = (byte)HeldItem.None;
            bool holding = (HeldItem)hiker.Held.Value == HeldItem.Flashlight;
            if (IsZhuchok)
            {
                // squeeze to light it: F held winds the dynamo, released it runs down
                if (Controls.TorchSwitch && !holding) hiker.Held.Value = (byte)HeldItem.Flashlight;
                bool squeeze = holding && Controls.TorchHold;
                Dynamo = Mathf.Clamp01(Dynamo + (squeeze ? DynamoWind : -DynamoRunDown) * Time.deltaTime);
                bool lit = holding && Dynamo > .02f;
                if (hiker.TorchOn.Value != lit) hiker.TorchOn.Value = lit;
                byte lv = (byte)Mathf.RoundToInt(Dynamo * 255f);
                if (hiker.TorchLevel.Value != lv) hiker.TorchLevel.Value = lv;
                return;
            }
            if (Controls.TorchSwitch)
            {
                if (!holding) hiker.Held.Value = (byte)HeldItem.Flashlight;
                hiker.TorchOn.Value = !hiker.TorchOn.Value;
            }
            if (hiker.TorchOn.Value && (HeldItem)hiker.Held.Value == HeldItem.Flashlight)
            {
                Battery = Mathf.Max(0f, Battery - Time.deltaTime / BatterySeconds);
                byte level = (byte)Mathf.RoundToInt(Battery * 255f);
                if (hiker.TorchLevel.Value != level) hiker.TorchLevel.Value = level;
            }
        }

        void Toggle(HeldItem item) => hiker.Held.Value = (byte)((HeldItem)hiker.Held.Value == item ? HeldItem.None : item);

        void LateUpdate()
        {
            if (hiker == null) return;
            var held = (HeldItem)hiker.Held.Value;
            bool mine = hiker.IsOwner;
            var cam = mine ? Camera.main : null;
            bool firstPerson = mine && hiker.FirstPerson && cam != null && WorldDressing.ViewIndex < 0;
            bool sway = firstPerson && hiker.Speed > .3f;
            float bob = sway ? Mathf.Sin(Time.time * 9f) * .006f : 0f;

            if (compass != null)
            {
                bool show = held == HeldItem.Compass;
                if (compass.gameObject.activeSelf != show) compass.gameObject.SetActive(show);
                if (show)
                {
                    if (firstPerson) Place(compass, cam.transform, new Vector3(-.03f, -.105f + bob, .26f), Quaternion.Euler(-62f, 0, 0));
                    else Place(compass, transform, new Vector3(.12f, 1.12f, .32f), Quaternion.Euler(-35f, 0, 0));
                    UpdateNeedle();
                }
            }
            int kind = hiker.TorchKind.Value == 1 ? 1 : 0;
            for (int i = 0; i < 2; i++)
            {
                var torch = torches[i];
                if (torch == null) continue;
                bool show = held == HeldItem.Flashlight && i == kind;
                if (torch.gameObject.activeSelf != show) torch.gameObject.SetActive(show);
                if (!show) continue;
                // the tube is held like a pistol grip, the "жучок" upright in the palm with the lever toward the fingers
                var fp = i == 0 ? new Vector3(.2f, -.2f + bob, .42f) : new Vector3(.19f, -.19f + bob + (hiker.TorchOn.Value ? Mathf.Sin(Time.time * 40f) * .001f : 0f), .36f);
                if (firstPerson) Place(torch, cam.transform, fp, Quaternion.Euler(2f, -4f, 0));
                else if (mine && cam != null) Place(torch, transform, new Vector3(.26f, 1.2f, .3f), Quaternion.Inverse(transform.rotation) * Quaternion.Euler(cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, 0));
                else Place(torch, transform, new Vector3(.26f, 1.2f, .3f), Quaternion.Euler(12f, 0, 0));
            }
            if (beam != null && torches[kind] != null && beamKind != kind)
            {
                beam.transform.SetParent(beamOrigins[kind], false);
                beamKind = kind;
                for (int i = 0; i < 2; i++) if (lensMats[i] != null) lensMats[i].SetColor("_EmissionColor", Color.black);
            }
            bool on = held == HeldItem.Flashlight && hiker.TorchOn.Value;
            float level = kind == 1 ? (mine ? Dynamo : hiker.TorchLevel.Value / 255f) : (mine ? Battery : hiker.TorchLevel.Value / 255f);
            UpdateBeam(on, level, kind);
            // the beam goes where the player looks (a hand keeps the light on what the eyes watch); others see it follow their torch
            if (beam != null && beam.enabled && beamOrigins[kind] != null)
                beam.transform.rotation = mine && cam != null ? cam.transform.rotation * Quaternion.Euler(4f, 0f, 0f) : beamOrigins[kind].rotation;
        }

        void OnDestroy()
        {
            // Items can be parented to the shared camera; they leave with their owner.
            if (compass != null) Destroy(compass.gameObject);
            foreach (var t in torches) if (t != null) Destroy(t.gameObject);
            if (beam != null) Destroy(beam.gameObject);
        }

        static void Place(Transform item, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            if (item.parent != parent) item.SetParent(parent, false);
            item.localPosition = localPos; item.localRotation = localRot;
        }

        void UpdateNeedle()
        {
            if (needle == null) return;
            // Target: magnetic north in the compass plane, as an angle around the compass up axis.
            var up = compass.up;
            var magNorth = Quaternion.Euler(0, Declination, 0) * Vector3.forward;
            var onFace = Vector3.ProjectOnPlane(magNorth, up);
            if (onFace.sqrMagnitude < 1e-4f) return;
            float target = Vector3.SignedAngle(Vector3.ProjectOnPlane(compass.forward, up), onFace, up);
            // Damped swing of a liquid-free needle; the arretir is released while the compass is in hand.
            float err = Mathf.DeltaAngle(needleAngle, target);
            needleVel += (err * 38f - needleVel * 5.5f) * Time.deltaTime;
            needleAngle += needleVel * Time.deltaTime;
            needle.localRotation = Quaternion.Euler(0, needleAngle, 0);
        }

        void UpdateBeam(bool on, float level, int kind)
        {
            if (beam == null) return;
            float power = on ? Mathf.Clamp01(level) : 0f;
            float k;
            if (kind == 1)
            {
                // dynamo: brightness follows the speed of the flywheel, a fast shimmer, wide dim beam
                k = power <= 0f ? 0f : Mathf.Sqrt(power) * (.9f + .1f * Mathf.Sin(Time.time * 55f));
                beam.spotAngle = 58f; beam.innerSpotAngle = 20f;
                beam.intensity = 2.6f * k;
                beam.range = Mathf.Lerp(7f, 24f, k);
                beam.color = Color.Lerp(new Color(.85f, .45f, .22f), new Color(1f, .8f, .55f), power);
            }
            else
            {
                float flicker = power > 0f && power < .15f ? (Mathf.PerlinNoise(Time.time * 9f, 0) > .35f ? 1f : .2f) : 1f;
                k = power <= 0f ? 0f : Mathf.Sqrt(power) * flicker;
                beam.spotAngle = 42f; beam.innerSpotAngle = 12f;
                beam.intensity = 4.2f * k;
                beam.range = Mathf.Lerp(14f, 48f, k);
                beam.color = Color.Lerp(new Color(.9f, .55f, .3f), new Color(1f, .84f, .62f), Mathf.Clamp01(power * 1.5f));
            }
            beam.enabled = k > .01f;
            if (lensMats[kind] != null) lensMats[kind].SetColor("_EmissionColor", beam.color * (k * 2.2f));
        }

        static Texture2D cookie;

        /// <summary>The beam of a cheap reflector: a hot centre, a brighter ring where the reflector focuses, a dim uneven halo.</summary>
        static Texture2D BeamCookie()
        {
            if (cookie != null) return cookie;
            const int n = 128;
            cookie = new Texture2D(n, n, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp, name = "beam_cookie" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                    float core = Mathf.Exp(-r * r / .03f);
                    float ring = .55f * Mathf.Exp(-(r - .3f) * (r - .3f) / .004f);
                    float halo = .32f * (1f - Mathf.SmoothStep(.2f, .95f, r)) * (.8f + .2f * Mathf.PerlinNoise(ang * 2f + 3f, r * 6f));
                    float a = Mathf.Clamp01(core + ring + halo) * (1f - Mathf.SmoothStep(.9f, 1f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            cookie.SetPixels32(px); cookie.Apply();
            return cookie;
        }
    }
}
