using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Synthesised sound around the local player: no samples, no microphone. Port of public/sound.js. Snowfall lives in SnowFx.</summary>
    public sealed class Ambience : MonoBehaviour
    {
        AudioSource wind, steps;
        AudioLowPassFilter lowPass;
        AudioClip noise;
        float stride, crackle;
        Vector3 lastPos;

        public static Ambience Create()
        {
            var go = new GameObject("Ambience", typeof(Ambience));
            DontDestroyOnLoad(go);
            return go.GetComponent<Ambience>();
        }

        void Awake()
        {
            // Two seconds of white noise, looped and low-passed, is the wind; short bursts of the same noise are footsteps and fire.
            const int rate = 22050;
            var data = new float[rate * 2];
            var rnd = new System.Random(1959);
            for (int i = 0; i < data.Length; i++) data[i] = (float)(rnd.NextDouble() * 2 - 1);
            noise = AudioClip.Create("noise", data.Length, 1, rate, false);
            noise.SetData(data, 0);

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = noise; wind.loop = true; wind.volume = 0f; wind.spatialBlend = 0f; wind.Play();
            lowPass = gameObject.AddComponent<AudioLowPassFilter>(); lowPass.cutoffFrequency = 420f;
            steps = gameObject.AddComponent<AudioSource>(); steps.clip = noise; steps.loop = false; steps.spatialBlend = 0f; steps.volume = .22f;

        }

        void Update()
        {
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            bool storm = s != null && s.Storm.Value;
            bool active = me != null && !me.Paused && s != null && s.MyOutcome == Core.Outcome.None;

            float target = active ? (storm ? .3f : .08f) * (1f + Mathf.Sin(Time.time * .7f) * .15f) : 0f;
            wind.volume = Mathf.Lerp(wind.volume, target * .42f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, storm ? 900f : 420f, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (!active) return;

            var pos = me.transform.position;
            float speed = Time.deltaTime > 0 ? new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude / Time.deltaTime : 0f;
            lastPos = pos;
            stride += speed * Time.deltaTime;
            if (stride > .85f) { stride = 0f; Pulse(.22f, .17f); }
            float fire = s.FireRemaining.Value > 0f ? Mathf.Clamp01(1f - Core.WorldData.Distance(pos.x, pos.z, Core.WorldData.Camp.x, Core.WorldData.Camp.z) / 18f) : 0f;
            crackle += Time.deltaTime;
            if (fire > 0f && crackle > .15f + Random.value * .4f) { crackle = 0f; Pulse(.07f * fire, .06f); }
        }

        void Pulse(float volume, float duration)
        {
            steps.volume = volume;
            steps.time = Random.Range(0f, noise.length - duration - .01f);
            steps.Play();
            steps.SetScheduledEndTime(AudioSettings.dspTime + duration);
        }
    }
}
