using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The night's sound, all synthesised at start-up (see <see cref="Synth"/>): a wind bed and a howl that swell in gusts,
    /// creaking trunks, owls, far wolves, snapping branches and snow sliding off spruces placed around the listener in 3D,
    /// squeaking footsteps, the fire, the player's own breath and — alone in the dark without light — a heartbeat.</summary>
    public sealed class Ambience : MonoBehaviour
    {
        AudioSource wind, howl, fire, body, heart;
        AudioLowPassFilter windLow, howlLow;
        AudioClip[] steps, creaks, owls, cracks, breaths;
        AudioClip wolves, heartClip;
        Emitter[] emitters;
        float stride, nextEvent = 12f, nextBreath, nextBeat, gustSeed;
        int lastStep = -1;
        Vector3 lastPos;

        sealed class Emitter
        {
            public AudioSource Src;
            public AudioLowPassFilter Low;
            public Transform T;
        }

        public static Ambience Create()
        {
            var go = new GameObject("Ambience", typeof(Ambience));
            DontDestroyOnLoad(go);
            return go.GetComponent<Ambience>();
        }

        void Awake()
        {
            gustSeed = Random.value * 100f;
            wind = Loop(Synth.WindBed(1959), out windLow, 600f);
            howl = Loop(Synth.Howl(201), out howlLow, 2400f);
            var fireGo = new GameObject("FireSound"); fireGo.transform.SetParent(transform, false);
            fire = fireGo.AddComponent<AudioSource>();
            fire.clip = Synth.FireLoop(7); fire.loop = true; fire.volume = 0f; fire.spatialBlend = 1f;
            fire.rolloffMode = AudioRolloffMode.Logarithmic; fire.minDistance = 2.5f; fire.maxDistance = 60f; fire.Play();

            steps = new AudioClip[6]; for (int i = 0; i < steps.Length; i++) steps[i] = Synth.Step(100 + i);
            creaks = new AudioClip[4]; for (int i = 0; i < creaks.Length; i++) creaks[i] = Synth.Creak(300 + i);
            owls = new[] { Synth.Owl(0, 11), Synth.Owl(1, 12) };
            cracks = new[] { Synth.Snap(21), Synth.Snap(22), Synth.SnowFall(23) };
            breaths = new[] { Synth.Breath(31), Synth.Breath(32) };
            wolves = Synth.Wolves(41);
            heartClip = Synth.Heart();

            body = gameObject.AddComponent<AudioSource>(); body.spatialBlend = 0f; body.playOnAwake = false;
            heart = gameObject.AddComponent<AudioSource>(); heart.spatialBlend = 0f; heart.playOnAwake = false;

            emitters = new Emitter[4];
            for (int i = 0; i < emitters.Length; i++)
            {
                var go = new GameObject("NightEmitter" + i); go.transform.SetParent(transform, false);
                var e = new Emitter { T = go.transform, Src = go.AddComponent<AudioSource>(), Low = go.AddComponent<AudioLowPassFilter>() };
                e.Src.spatialBlend = 1f; e.Src.playOnAwake = false; e.Src.dopplerLevel = 0f;
                e.Src.rolloffMode = AudioRolloffMode.Logarithmic; e.Src.minDistance = 12f; e.Src.maxDistance = 600f;
                var echo = go.AddComponent<AudioReverbFilter>(); echo.reverbPreset = AudioReverbPreset.Mountains;
                emitters[i] = e;
            }
        }

        AudioSource Loop(AudioClip clip, out AudioLowPassFilter low, float cutoff)
        {
            var go = new GameObject(clip.name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.volume = 0f; s.spatialBlend = 0f;
            s.time = Random.Range(0f, clip.length * .9f);
            s.Play();
            low = go.AddComponent<AudioLowPassFilter>(); low.cutoffFrequency = cutoff;
            return s;
        }

        static float Ease(float current, float target, float rate) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

        void Update()
        {
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            float blizzard = Weather.Storm;
            bool storm = blizzard > .5f;
            bool active = me != null && !me.Paused && s != null && s.MyOutcome == Outcome.None;
            float dark = Bootstrap.Darkness;
            bool inside = me != null && me.Crawling; // crawling only happens inside the tent
            var listener = Camera.main != null ? Camera.main.transform : transform;

            // wind follows the weather: gusts swell the bed, the howl rises in pitch, a blizzard roars and screams
            float gust = Weather.Gust, strength = Weather.Wind;
            float windTarget = !active ? 0f : Mathf.Clamp01(.25f + .75f * strength) * (inside ? .45f : 1f);
            float howlTarget = !active ? 0f : Mathf.Clamp01(.06f + .3f * gust * gust + blizzard * (.45f + .5f * gust)) * (inside ? .35f : 1f) * (.4f + .6f * dark);
            wind.volume = Ease(wind.volume, windTarget, 2f);
            howl.volume = Ease(howl.volume, howlTarget, 1.5f);
            howl.pitch = .9f + .12f * gust + .12f * blizzard * gust;
            windLow.cutoffFrequency = Ease(windLow.cutoffFrequency, inside ? 260f : 450f + 700f * gust + 2200f * blizzard * (.4f + .6f * gust), 3f);
            howlLow.cutoffFrequency = Ease(howlLow.cutoffFrequency, inside ? 500f : 3000f, 3f);

            float fireOn = s != null && s.FireRemaining.Value > 0f ? 1f : 0f;
            fire.transform.position = CampPoint();
            fire.volume = Ease(fire.volume, active ? fireOn * .8f : 0f, 2f);
            if (!active) return;

            // footsteps
            var pos = me.transform.position;
            float speed = Time.deltaTime > 0 ? new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude / Time.deltaTime : 0f;
            if (speed > 20f) speed = 0f; // teleport
            lastPos = pos;
            stride += speed * Time.deltaTime;
            float strideLength = speed > 4f ? 1.25f : .8f;
            if (stride > strideLength && me.Grounded)
            {
                stride = 0f;
                int k; do k = Random.Range(0, steps.Length); while (k == lastStep && steps.Length > 1);
                lastStep = k;
                body.pitch = Random.Range(.9f, 1.1f);
                body.PlayOneShot(steps[k], (inside ? .25f : .5f) * Mathf.Clamp(speed / 3f, .5f, 1.2f));
            }

            // own breath: heavier when running or cold
            float cold = 1f - Mathf.Clamp01(s.Heat / 100f);
            if (Time.time > nextBreath && (speed > 3.5f || cold > .45f || dark > .8f))
            {
                float vol = .08f + .25f * Mathf.Clamp01(speed / RunSpeedGuess) + .2f * cold;
                body.PlayOneShot(breaths[Random.Range(0, breaths.Length)], vol);
                nextBreath = Time.time + Mathf.Lerp(4.2f, 2.3f, Mathf.Clamp01(speed / RunSpeedGuess + cold * .5f)) + Random.Range(0f, .6f);
            }

            // heartbeat: deep night, no light of your own, away from the fire
            bool alone = dark > .85f && !me.TorchOn.Value && !inside && (fireOn <= 0f || Vector3.Distance(pos, CampPoint()) > 14f);
            if (alone && Time.time > nextBeat)
            {
                heart.PlayOneShot(heartClip, .22f);
                nextBeat = Time.time + Mathf.Lerp(.95f, .62f, cold);
            }

            // the forest around: rarer before full dark, often in it
            if (Time.time > nextEvent)
            {
                SpawnEvent(listener, dark, storm, inside);
                nextEvent = Time.time + Random.Range(7f, 20f) * Mathf.Lerp(2.2f, 1f, dark) * (storm ? .55f : 1f);
            }
        }

        const float RunSpeedGuess = HikerController.RunSpeed;

        static Vector3 CampPoint()
        {
            var (cx, cz) = Height1079.Core.World.IsElbrus ? (Elbrus.Start.x, Elbrus.Start.z) : WorldData.Camp;
            return new Vector3(cx, TerrainBuilder.Height(Bootstrap.Dem, cx, cz) + .8f, cz);
        }

        void SpawnEvent(Transform listener, float dark, bool storm, bool inside)
        {
            Emitter e = null;
            foreach (var em in emitters) if (!em.Src.isPlaying) { e = em; break; }
            if (e == null) return;

            float roll = Random.value;
            AudioClip clip; float near, far, vol, pitch = Random.Range(.93f, 1.07f);
            bool behind = false;
            if (storm) roll = roll < .55f ? roll * (.38f / .55f) : roll < .85f ? .6f + (roll - .55f) * (.2f / .3f) : .95f; // blizzard: trees and branches, no animals
            if (roll < .38f) { clip = creaks[Random.Range(0, creaks.Length)]; near = 8f; far = 35f; vol = .6f; }
            else if (roll < .6f) { clip = owls[Random.Range(0, owls.Length)]; near = 60f; far = 160f; vol = .8f; pitch = Random.Range(.97f, 1.03f); }
            else if (roll < .8f) { clip = cracks[Random.Range(0, cracks.Length)]; near = 10f; far = 40f; vol = .9f; behind = Random.value < .6f; }
            else if (roll < .9f && dark > .6f) { clip = wolves; near = 250f; far = 500f; vol = 1f; pitch = Random.Range(.95f, 1.05f); }
            else { clip = creaks[Random.Range(0, creaks.Length)]; near = 6f; far = 18f; vol = .7f; behind = true; pitch = Random.Range(.8f, .95f); }

            float angle = behind ? listener.eulerAngles.y + 180f + Random.Range(-50f, 50f) : Random.Range(0f, 360f);
            float dist = Random.Range(near, far);
            var dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var p = listener.position + dir * dist;
            p.y = TerrainBuilder.Height(Bootstrap.Dem, p.x, p.z) + (clip == owls[0] || clip == owls[1] ? 12f : 2f);
            e.T.position = p;
            // air absorbs the highs with distance; the canvas muffles everything inside
            e.Low.cutoffFrequency = Mathf.Clamp(9000f * Mathf.Exp(-dist / 140f), 700f, 9000f) * (inside ? .35f : 1f) * (storm ? .6f : 1f);
            e.Src.minDistance = Mathf.Max(12f, dist * .35f);
            e.Src.clip = clip; e.Src.pitch = pitch; e.Src.volume = vol * (inside ? .6f : 1f);
            e.Src.Play();
        }
    }
}
