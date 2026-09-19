using UnityEngine;

namespace Height1079.Sandbox
{
    /// <summary>Boots in snow, rendered from noise when the sandbox starts.
    ///
    /// The game ships no audio samples — everything it plays is synthesised by its own `Synth` at start-up — and the
    /// sandbox assembly cannot see the game, so this is a small copy of the same idea holding only what a footfall
    /// needs: an RBJ biquad, an envelope with a real attack, and two banks of short clips.
    ///
    /// It is worth writing down what the first version got wrong, because both mistakes are easy to make again. It
    /// ended every step with a sine at 88 Hz under an exponential decay — that is not "a thump", that is the entire
    /// recipe for a kick drum — and it chopped its noise with a rectified sine at a fixed 190 Hz, which is a buzz at a
    /// musical pitch. A man walking therefore played a drum machine, and no amount of randomising around it would have
    /// helped. So: there is no oscillator anywhere in a footstep here. The weight of the boot arriving is low-passed
    /// noise, which has body without having a note, and nothing is modulated at a fixed rate.
    ///
    /// What snow actually does under a boot is two separate events, and they are the two layers:
    ///
    /// * <see cref="Bite"/> — the crust giving way. A dense burst of random grains that thins out over about a tenth
    ///   of a second, through a wide band-pass high up. This is the "crunch", and on bare rock or ice it is all there
    ///   is: nothing gives, so nothing packs.
    /// * <see cref="Pack"/> — the boot settling into what is underneath, and the squeak. Snow squeaks by stick-slip —
    ///   crystals catch under the load and let go, the same mechanism that makes a door creak — so the squeak here is
    ///   a high-Q resonance kicked by random releases, not a tone that is switched on. The releases come fast at first
    ///   and settle, the resonance slides downward as the boot stops moving, and its centre waivers a few per cent.
    ///   Irregular by construction: a squeak that repeated would be a whistle.
    ///
    /// How deep the snow is decides the balance between the two, how long the step is and how low it sits: stone is
    /// <see cref="Bite"/> alone and pitched up, wind-packed crust is <see cref="Bite"/> with almost nothing under it,
    /// dry and short, and a drift over the boot top is mostly <see cref="Pack"/>, dull and half a second long. The
    /// pitch grades with depth as well, so no two depths are the same length either.
    ///
    /// Nothing else in the sandbox may play this: <see cref="SandboxCameraRig"/> owns the decision of when a foot
    /// lands, and one call here is one footfall.</summary>
    public sealed class SandboxSoundSnow : MonoBehaviour
    {
        public const int Rate = 44100;

        /// <summary>Snow this deep is as deep as it matters. The same 0.45 m the gait grades the head heave off, so
        /// what is heard and what the eye does come off one number and cannot drift apart.</summary>
        const float FullDepth = .45f;

        /// <summary>Clips per layer. Enough that the ear never hears a pair close enough together to match them;
        /// small enough that building the lot is a few milliseconds at start-up.</summary>
        const int Bank = 7;

        /// <summary>Sources to play them through. One step takes two (a layer each) and clips can be a third of a
        /// second long, so six covers three steps overlapping — which only ever happens at a run downhill.</summary>
        const int Voices = 6;

        AudioClip[] bite, pack;
        AudioSource[] voices;
        int voice, lastBite = -1, lastPack = -1;

        /// <summary>Built on its own object rather than on the camera: the ear is in the head and these are at the
        /// feet, and that metre is the whole of why the sound reads as coming from underneath you.</summary>
        public static SandboxSoundSnow Create(Transform under)
        {
            var go = new GameObject("SnowSteps");
            if (under != null) go.transform.SetParent(under, false);
            return go.AddComponent<SandboxSoundSnow>();
        }

        void Awake()
        {
            bite = new AudioClip[Bank];
            pack = new AudioClip[Bank];
            // fixed seeds: the sandbox is a measuring instrument and two runs of it should sound the same
            for (int i = 0; i < Bank; i++) { bite[i] = Bite(101 + i * 13); pack[i] = Pack(211 + i * 17); }

            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.dopplerLevel = 0f;          // a source that walks with you must not bend its own pitch as well
                s.bypassReverbZones = true;
                // Neither flat nor fully placed. Your own boots can never be occluded, walked away from or lost
                // behind you — so most of this is 2D and the volume does not depend on the camera — but a third of
                // it is placed, which is what says "down there" instead of "somewhere in the room". minDistance is
                // wide enough that the third-person camera four metres back hears them at full strength too.
                s.spatialBlend = .35f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2.5f;
                s.maxDistance = 60f;
                voices[i] = s;
            }
        }

        /// <summary>Where the boots are this frame. Driven from the camera rig, which is already finding the body in
        /// LateUpdate and has the number to hand.</summary>
        public void Follow(Vector3 feet) => transform.position = feet;

        /// <summary>One footfall, and only ever one: whoever calls this has already decided that a foot landed.
        ///
        /// <paramref name="speed"/> is how fast the body is travelling, m/s — how hard the boot arrives.
        /// <paramref name="sink"/> is how far it goes under the surface, m, straight out of the snow field.
        /// <paramref name="hard"/> says the boot found stone, ice or a ledge instead of snow.
        /// <paramref name="foot"/> is 0 or 1: two boots on two patches of ground are never quite the same sound.
        /// <paramref name="weight"/> scales the lot, for an arrival that is more than a step (a landing).</summary>
        public void Footfall(float speed, float sink, bool hard, int foot, float weight = 1f)
        {
            if (voices == null || bite == null || pack == null) return;

            // 0 = bare stone or snow swept down to the crust, 1 = over the boot top.
            float deep = hard ? 0f : Mathf.Clamp01(sink / FullDepth);
            // What the step is like to stand in, which is not quite the same thing: even the swept yard gives a few
            // millimetres, and that give is most of what says it is snow and not a floor. This grades the pitch.
            float soft = hard ? 0f : Mathf.Lerp(.25f, 1f, deep);

            // Quiet on purpose. These are your own boots half a metre away and they play two or three times a second
            // for the whole session; anything that reads as "loud enough" on the first step is unbearable by the
            // hundredth. The range is walking-to-running, not silent-to-loud.
            float loud = Mathf.Lerp(.13f, .30f, Mathf.InverseLerp(.6f, 3.4f, speed)) * Mathf.Max(weight, 0f);
            loud *= Random.Range(.84f, 1.16f);

            // Deep snow is a bigger, slower event: longer and lower. Pitch does both in one move, which is why the
            // banks are not rendered once per depth.
            float pitch = Mathf.Lerp(1.10f, .84f, soft)
                        * Random.Range(.94f, 1.07f)
                        * (foot == 0 ? .985f : 1.015f);

            // The crust layer thins as the snow deepens but never goes: something always breaks first.
            Play(bite[Pick(Bank, ref lastBite)], loud * Mathf.Lerp(1f, .5f, deep), pitch * (hard ? 1.05f : 1f));
            // The packing layer, on the other hand, is squared: a boot only packs snow once there is snow to pack,
            // and it is three times the length of the crust layer, so at equal gain it drowns it. Linear here and
            // walking the swept yard sounds the same as wading a drift.
            float packGain = loud * deep * deep * 1.5f;
            if (packGain > .005f) Play(pack[Pick(Bank, ref lastPack)], packGain, pitch);
        }

        void Play(AudioClip c, float gain, float pitch)
        {
            if (c == null || gain <= 0f) return;
            var s = voices[voice];
            voice = (voice + 1) % voices.Length;
            s.clip = c;
            s.volume = Mathf.Clamp01(gain);
            s.pitch = Mathf.Clamp(pitch, .5f, 2f);
            s.Play();
        }

        /// <summary>A clip from the bank that is not the one before it. Two identical steps in a row is the single
        /// thing that gives a bank away as a bank, and the ear catches it long before it catches a repeat at four.</summary>
        static int Pick(int n, ref int last)
        {
            if (n <= 1) return 0;
            int k;
            do k = Random.Range(0, n); while (k == last);
            last = k;
            return k;
        }

        // ── the two layers ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Crust giving way: grains, thinning out. Nothing below 200 Hz survives it — low energy under a
        /// short envelope is exactly what the ear files as "a hit", and this must not be one.</summary>
        static AudioClip Bite(int seed)
        {
            var rnd = new System.Random(seed);
            float len = .10f + (float)rnd.NextDouble() * .055f;
            int n = (int)(len * Rate);
            var d = new float[n];
            // wide and high: the sound of a hundred small things breaking at once, not of one big one
            var air = Biquad.BandPass(2300f + (float)rnd.NextDouble() * 1500f, .85f);
            // a little meat under it, or the whole layer is hiss
            var meat = Biquad.BandPass(900f + (float)rnd.NextDouble() * 400f, 2.2f);
            var cut = Biquad.HighPass(200f);
            // and a lid on it. Without this the band-pass skirts leave half the energy above 6 kHz and the crunch
            // reads as "шшш" rather than as something breaking; with it the weight sits at two to four kilohertz,
            // which is where a boot through crust actually lives.
            var top = Biquad.LowPass(6500f + (float)rnd.NextDouble() * 2000f, .7f);
            float rate0 = 1500f + (float)rnd.NextDouble() * 900f;   // grains per second at the moment of contact
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float w = Rnd(rnd);
                // the crust gives way fast and then there is nothing left to give: a burst that thins is a crunch,
                // a burst that holds is a hiss
                float density = rate0 * Mathf.Exp(-u * 3.4f);
                float grain = rnd.NextDouble() < density / Rate ? Rnd(rnd) * 2.2f : 0f;
                float x = grain + w * .12f;
                // 7 ms of attack, smoothed. Zero attack is a click and a click is the "удар" this replaces; much
                // more than this and the boot sounds like it is being set down deliberately.
                d[i] = top.Run(cut.Run(air.Run(x) + meat.Run(x) * .55f)) * Env(t, .007f, len * .8f, len);
            }
            Normalise(d, .85f);
            return Clip("snow-bite-" + seed, d);
        }

        /// <summary>The boot settling, and the squeak. Low-passed noise for the weight — filtered noise has body
        /// without having a pitch, which is the difference between a footstep and a drum — plus a narrow resonance
        /// kicked by random stick-slip releases, sliding downward as the boot stops moving.</summary>
        static AudioClip Pack(int seed)
        {
            var rnd = new System.Random(seed);
            float len = .24f + (float)rnd.NextDouble() * .15f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var muffle = Biquad.LowPass(500f + (float)rnd.NextDouble() * 300f, .8f);
            var cut = Biquad.HighPass(95f);       // keep the rumble out; it is the other half of sounding like a kick
            const float Q = 11f;                   // narrow enough to ring for a couple of milliseconds per release
            float f0 = 1300f + (float)rnd.NextDouble() * 700f;
            float f1 = f0 * (.60f + (float)rnd.NextDouble() * .20f);   // where it has fallen to by the end
            float wob = (float)rnd.NextDouble() * 6.283f;
            var creak = Biquad.BandPass(f0, Q);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float w = Rnd(rnd);
                // retuned every 64 samples: far below anything audible as a step, and 690 updates a clip is free
                if ((i & 63) == 0)
                    creak.SetBandPass(Mathf.Lerp(f0, f1, u) * (1f + .06f * Mathf.Sin(t * 47f + wob)), Q);
                // stick-slip: crystals let go one after another, fast under the arriving weight and slower as it
                // settles. Each release is an impulse and the resonance rings it — that train of irregular rings is
                // what a creak is. A modulator at a fixed rate here would be a buzz at that rate, which is precisely
                // the noise this whole file exists to get rid of.
                float releases = Mathf.Lerp(300f, 80f, u);
                float slip = rnd.NextDouble() < releases / Rate ? Rnd(rnd) * 2.4f : 0f;
                // the squeak belongs to the middle of the step — while the weight is going on, not at the moment of
                // contact and not once the boot has stopped
                float bell = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                d[i] = (cut.Run(muffle.Run(w)) * .85f + creak.Run(slip + w * .10f) * bell * 2.6f)
                     * Env(t, .022f, len * .72f, len);
            }
            Normalise(d, .80f);
            return Clip("snow-pack-" + seed, d);
        }

        // ── the small amount of DSP all of that needs ────────────────────────────────────────────────────────────

        /// <summary>RBJ biquad, the same one the game's synthesiser uses. Coefficients can be re-set while it runs
        /// (the sliding creak does) and the two state samples carry across, so the filter does not click when it is
        /// retuned.</summary>
        struct Biquad
        {
            float b0, b1, b2, a1, a2, z1, z2;

            public static Biquad BandPass(float f, float q) { var b = new Biquad(); b.SetBandPass(f, q); return b; }
            public static Biquad LowPass(float f, float q = .707f) { var b = new Biquad(); b.SetLowPass(f, q); return b; }
            public static Biquad HighPass(float f, float q = .707f) { var b = new Biquad(); b.SetHighPass(f, q); return b; }

            public void SetBandPass(float f, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, Rate * .45f) / Rate, alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
                b0 = alpha / a0; b1 = 0f; b2 = -alpha / a0; a1 = -2f * Mathf.Cos(w) / a0; a2 = (1f - alpha) / a0;
            }

            public void SetLowPass(float f, float q = .707f)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, Rate * .45f) / Rate, alpha = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + alpha;
                b0 = (1f - c) / 2f / a0; b1 = (1f - c) / a0; b2 = b0; a1 = -2f * c / a0; a2 = (1f - alpha) / a0;
            }

            public void SetHighPass(float f, float q = .707f)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, Rate * .45f) / Rate, alpha = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + alpha;
                b0 = (1f + c) / 2f / a0; b1 = -(1f + c) / a0; b2 = b0; a1 = -2f * c / a0; a2 = (1f - alpha) / a0;
            }

            public float Run(float x)
            {
                float y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }
        }

        /// <summary>Attack and release, both eased. The easing is the point: a linear ramp of 7 ms still has a corner
        /// in it and the ear hears corners as clicks.</summary>
        static float Env(float t, float attack, float release, float length)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = release > 0f ? Mathf.Clamp01((length - t) / release) : 1f;
            return Mathf.SmoothStep(0f, 1f, a) * Mathf.SmoothStep(0f, 1f, r);
        }

        static float Rnd(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Every clip to the same peak, so the gains in <see cref="Footfall"/> mean the same thing whichever
        /// one of the bank comes up.</summary>
        static void Normalise(float[] d, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static AudioClip Clip(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
