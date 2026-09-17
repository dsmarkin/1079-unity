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
        Transform compass, torch, needle, beamOrigin;
        Light beam;
        Material lensMat;
        float needleAngle, needleVel;
        public float Battery { get; private set; } = 1f;

        void Awake() { hiker = GetComponent<HikerController>(); }

        void Start()
        {
            compass = Spawn("Compass");
            torch = Spawn("Flashlight");
            if (compass != null) needle = compass.Find("Needle");
            if (torch != null)
            {
                beamOrigin = torch.Find("Lens/BeamOrigin");
                if (beamOrigin == null) beamOrigin = torch;
                var lens = torch.Find("Lens");
                if (lens != null) lensMat = lens.GetComponent<MeshRenderer>().material;
                var go = new GameObject("Beam", typeof(Light));
                go.transform.SetParent(beamOrigin, false);
                beam = go.GetComponent<Light>();
                beam.type = LightType.Spot; beam.spotAngle = 38f; beam.innerSpotAngle = 12f; beam.range = 38f;
                beam.color = new Color(1f, .83f, .6f); beam.shadows = LightShadows.Soft; beam.shadowStrength = .85f;
                beam.enabled = false;
            }
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
            if (Controls.ItemTorch) Toggle(HeldItem.Flashlight);
            if (Controls.ItemMap) Toggle(HeldItem.Map);
            if (Controls.ItemNone) hiker.Held.Value = (byte)HeldItem.None;
            if (Controls.TorchSwitch)
            {
                if ((HeldItem)hiker.Held.Value != HeldItem.Flashlight) hiker.Held.Value = (byte)HeldItem.Flashlight;
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
                    if (firstPerson) Place(compass, cam.transform, new Vector3(-.02f, -.16f + bob, .34f), Quaternion.Euler(-58f, 0, 0));
                    else Place(compass, transform, new Vector3(.12f, 1.12f, .32f), Quaternion.Euler(-35f, 0, 0));
                    UpdateNeedle();
                }
            }
            if (torch != null)
            {
                bool show = held == HeldItem.Flashlight;
                if (torch.gameObject.activeSelf != show) torch.gameObject.SetActive(show);
                if (show)
                {
                    if (firstPerson) Place(torch, cam.transform, new Vector3(.2f, -.2f + bob, .42f), Quaternion.Euler(2f, -4f, 0));
                    else if (mine && cam != null) Place(torch, transform, new Vector3(.26f, 1.2f, .3f), Quaternion.Inverse(transform.rotation) * Quaternion.Euler(cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, 0));
                    else Place(torch, transform, new Vector3(.26f, 1.2f, .3f), Quaternion.Euler(12f, 0, 0));
                }
                UpdateBeam(show && hiker.TorchOn.Value, mine ? Battery : hiker.TorchLevel.Value / 255f);
            }
        }

        void OnDestroy()
        {
            // Items can be parented to the shared camera; they leave with their owner.
            if (compass != null) Destroy(compass.gameObject);
            if (torch != null) Destroy(torch.gameObject);
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

        void UpdateBeam(bool on, float battery)
        {
            if (beam == null) return;
            float power = on ? Mathf.Clamp01(battery) : 0f;
            float flicker = power > 0f && power < .15f ? (Mathf.PerlinNoise(Time.time * 9f, 0) > .35f ? 1f : .2f) : 1f;
            float k = power <= 0f ? 0f : Mathf.Sqrt(power) * flicker;
            beam.enabled = k > .01f;
            beam.intensity = 2.6f * k;
            beam.range = Mathf.Lerp(12f, 38f, k);
            beam.color = Color.Lerp(new Color(.9f, .55f, .3f), new Color(1f, .84f, .62f), Mathf.Clamp01(power * 1.5f));
            if (lensMat != null) lensMat.SetColor("_EmissionColor", beam.color * (k * 2.2f));
        }
    }
}
