using UnityEngine;

namespace Height1079.Torchlight
{
    /// <summary>The beam of a hand torch — one module for the night game's hiker (<c>Runtime.Equipment</c>) and the
    /// sandbox's body (<c>Sandbox.SandboxGear</c>), so that the light in the sandbox is the game's light and not a
    /// lookalike. It is a spot light with the cookie of a cheap reflector and the rule for how bright it burns at a
    /// given charge: the tube on two cells dims and flickers as the cells go, the hand dynamo ("жучок") shimmers with
    /// its flywheel. Whoever owns the torch decides on, off and the charge; this turns that into light and into the
    /// glow of the lens.
    ///
    /// Its own assembly, with nothing but UnityEngine in it, because the sandbox cannot see the game's runtime and
    /// the game must not depend on the sandbox — the same reason the snow underfoot lives in <c>Height1079.Snow</c>.</summary>
    public sealed class TorchBeam
    {
        /// <summary>Two round cells, about twelve minutes of light in the frost.</summary>
        public const float BatterySeconds = 12f * 60f;
        /// <summary>The dynamo: squeezing winds it up at this rate per second, let go it runs down at that.</summary>
        public const float DynamoWind = 1.4f, DynamoRunDown = .35f;

        public readonly Light Light;
        public Transform Transform => Light != null ? Light.transform : null;
        public bool Lit => Light != null && Light.enabled;
        /// <summary>0…1 of how bright the beam burns this frame, flicker included. The lens glows by it.</summary>
        public float Power { get; private set; }
        /// <summary>What the lens material's emission should be set to: the beam's colour, by its power.</summary>
        public Color LensEmission => Light != null ? Light.color * (Power * 2.2f) : Color.black;

        TorchBeam(Light light) { Light = light; }

        public static TorchBeam Create(string name = "Beam")
        {
            var go = new GameObject(name, typeof(Light));
            var l = go.GetComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 38f; l.innerSpotAngle = 12f; l.range = 38f;
            l.color = new Color(1f, .83f, .6f); l.shadows = LightShadows.Soft; l.shadowStrength = .9f;
            l.renderMode = LightRenderMode.ForcePixel; l.shadowNearPlane = .3f;
            l.cookie = Cookie();
            l.enabled = false;
            return new TorchBeam(l);
        }

        /// <summary>Hang the light off the lens of whichever torch is in hand.</summary>
        public void Attach(Transform origin)
        {
            if (Light == null || origin == null) return;
            Light.transform.SetParent(origin, false);
            Light.transform.localPosition = Vector3.zero;
            Light.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Set the beam for this frame. <paramref name="level"/> is the charge, 0…1: what is left of the
        /// cells, or how fast the dynamo's flywheel is turning.</summary>
        public void Set(bool on, float level, bool dynamo)
        {
            if (Light == null) return;
            float power = on ? Mathf.Clamp01(level) : 0f;
            float k;
            if (dynamo)
            {
                // dynamo: brightness follows the speed of the flywheel, a fast shimmer, wide dim beam
                k = power <= 0f ? 0f : Mathf.Sqrt(power) * (.9f + .1f * Mathf.Sin(Time.time * 55f));
                Light.spotAngle = 58f; Light.innerSpotAngle = 20f;
                Light.intensity = 2.6f * k;
                Light.range = Mathf.Lerp(7f, 24f, k);
                Light.color = Color.Lerp(new Color(.85f, .45f, .22f), new Color(1f, .8f, .55f), power);
            }
            else
            {
                float flicker = power > 0f && power < .15f ? (Mathf.PerlinNoise(Time.time * 9f, 0) > .35f ? 1f : .2f) : 1f;
                k = power <= 0f ? 0f : Mathf.Sqrt(power) * flicker;
                Light.spotAngle = 42f; Light.innerSpotAngle = 12f;
                Light.intensity = 4.2f * k;
                Light.range = Mathf.Lerp(14f, 48f, k);
                Light.color = Color.Lerp(new Color(.9f, .55f, .3f), new Color(1f, .84f, .62f), Mathf.Clamp01(power * 1.5f));
            }
            Power = k;
            Light.enabled = k > .01f;
        }

        public void Destroy()
        {
            if (Light != null) Object.Destroy(Light.gameObject);
        }

        static Texture2D cookie;

        /// <summary>The beam of a cheap reflector: a hot centre, a brighter ring where the reflector focuses, a dim
        /// uneven halo.</summary>
        static Texture2D Cookie()
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
