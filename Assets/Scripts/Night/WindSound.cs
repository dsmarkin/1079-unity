using UnityEngine;

namespace Height1079.Night
{
    /// <summary>The wind in the ears: the bed (a rumble and the hiss of blown snow) and the howl, both synthesised
    /// at start-up (<see cref="Synth"/>) and both following <see cref="Weather"/> — gusts swell the bed, the howl
    /// rises in pitch, a blizzard roars and screams. The night's <c>Ambience</c> plays the same two loops with the
    /// same mapping among everything else it does; this is just those two, for a scene that has weather and nothing
    /// else, so the sandbox hears the front coming the way the mountain does.</summary>
    public sealed class WindSound : MonoBehaviour
    {
        /// <summary>Master, 0 … 1: what the owner wants to hear of it (0 while there is no night).</summary>
        public float Level = 1f;
        /// <summary>Under canvas: quieter and muffled.</summary>
        public bool Inside;
        /// <summary>0 dusk … 1 full night: the howl belongs to the dark.</summary>
        public float Dark = 1f;

        AudioSource wind, howl;
        AudioLowPassFilter windLow, howlLow;

        public static WindSound Create(Transform parent)
        {
            var go = new GameObject("WindSound", typeof(WindSound));
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<WindSound>();
        }

        void Awake()
        {
            wind = Loop(Synth.WindBed(1959), out windLow, 600f);
            howl = Loop(Synth.Howl(201), out howlLow, 2400f);
        }

        AudioSource Loop(AudioClip clip, out AudioLowPassFilter low, float cutoff)
        {
            var go = new GameObject(clip.name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.playOnAwake = false;
            low = go.AddComponent<AudioLowPassFilter>(); low.cutoffFrequency = cutoff;
            s.Play();
            return s;
        }

        static float Ease(float current, float target, float rate) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

        void Update()
        {
            float blizzard = Weather.Storm, gust = Weather.Gust, strength = Weather.Wind;
            float k = Mathf.Clamp01(Level);
            float windTarget = k * Mathf.Clamp01(.25f + .75f * strength) * (Inside ? .45f : 1f);
            float howlTarget = k * Mathf.Clamp01(.06f + .3f * gust * gust + blizzard * (.45f + .5f * gust)) * (Inside ? .35f : 1f) * (.4f + .6f * Mathf.Clamp01(Dark));
            wind.volume = Ease(wind.volume, windTarget, 2f);
            howl.volume = Ease(howl.volume, howlTarget, 1.5f);
            howl.pitch = .9f + .12f * gust + .12f * blizzard * gust;
            windLow.cutoffFrequency = Ease(windLow.cutoffFrequency, Inside ? 260f : 450f + 700f * gust + 2200f * blizzard * (.4f + .6f * gust), 3f);
            howlLow.cutoffFrequency = Ease(howlLow.cutoffFrequency, Inside ? 500f : 3000f, 3f);
        }
    }
}
