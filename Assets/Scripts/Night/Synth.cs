using UnityEngine;

namespace Height1079.Night
{
    /// <summary>Offline sound synthesis for the night: every clip is rendered from noise and oscillators when the game starts,
    /// so the build ships no audio samples. Loops are cross-faded end-to-start so they repeat without a click.</summary>
    public static class Synth
    {
        public const int Rate = 44100;

        /// <summary>RBJ biquad; coefficients can be re-tuned while running (sweeping howl, squeaks).</summary>
        public struct Biquad
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

        static float Env(float t, float attack, float release, float length)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = release > 0f ? Mathf.Clamp01((length - t) / release) : 1f;
            return Mathf.SmoothStep(0f, 1f, a) * Mathf.SmoothStep(0f, 1f, r);
        }

        static float Rnd(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Periodic smooth random curve in 0..1 with the given number of cycles over the loop (sum of harmonics with fixed random phases).</summary>
        static float Wave(float phase01, int baseCycles, float[] phases)
        {
            float v = 0f, norm = 0f;
            for (int k = 0; k < phases.Length; k++)
            {
                float amp = 1f / (k + 1);
                v += amp * Mathf.Sin(2f * Mathf.PI * (phase01 * baseCycles * (k + 1) + phases[k]));
                norm += amp;
            }
            return .5f + .5f * v / norm;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Renders <paramref name="seconds"/> + a tail and cross-fades the tail into the head, so the clip loops seamlessly.</summary>
        static AudioClip Loop(string name, float seconds, System.Func<int, float[]> render, float peak)
        {
            int n = (int)(seconds * Rate), fade = Rate / 2;
            var raw = render(n + fade);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                d[i] = raw[i] * k + raw[n + i] * (1f - k);
            }
            Normalize(d, peak);
            return Clip(name, d);
        }

        static AudioClip Clip(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        // ---------------------------------------------------------------- loops

        /// <summary>Deep wind bed: brown noise rumble plus a hiss of blown snow, both swelling in slow gusts.</summary>
        public static AudioClip WindBed(int seed)
        {
            const float len = 14f;
            var rnd = new System.Random(seed);
            var ph = new float[4]; for (int i = 0; i < ph.Length; i++) ph[i] = (float)rnd.NextDouble();
            return Loop("wind_bed", len, total =>
            {
                var d = new float[total];
                var low = Biquad.LowPass(380f); var hiss = Biquad.BandPass(2400f, .7f);
                float brown = 0f;
                for (int i = 0; i < total; i++)
                {
                    float t = (float)i / Rate, w = Rnd(rnd);
                    brown = brown * .995f + w * .06f;
                    float gust = .35f + .65f * Wave(t / len, 2, ph);
                    d[i] = low.Run(brown) * (.6f + .6f * gust) + hiss.Run(w) * .05f * gust * gust;
                }
                return d;
            }, .9f);
        }

        /// <summary>The howl: noise through narrow resonances that slide up and down, the whistle of wind over the ridge and through the trees.</summary>
        public static AudioClip Howl(int seed)
        {
            const float len = 24f;
            var rnd = new System.Random(seed);
            var pa = new float[3]; var pb = new float[3]; var pg = new float[4];
            for (int i = 0; i < 3; i++) { pa[i] = (float)rnd.NextDouble(); pb[i] = (float)rnd.NextDouble(); }
            for (int i = 0; i < 4; i++) pg[i] = (float)rnd.NextDouble();
            return Loop("wind_howl", len, total =>
            {
                var d = new float[total];
                var v1 = Biquad.BandPass(300f, 18f); var v2 = Biquad.BandPass(450f, 22f); var v3 = Biquad.BandPass(700f, 14f);
                for (int i = 0; i < total; i++)
                {
                    float t = (float)i / Rate, u = t / len;
                    if (i % 64 == 0)
                    {
                        float a = Wave(u, 3, pa), b = Wave(u, 2, pb);
                        v1.SetBandPass(190f + 260f * a, 18f);
                        v2.SetBandPass(320f + 380f * b, 22f);
                        v3.SetBandPass(560f + 420f * a * b, 14f);
                    }
                    float gust = Wave(u, 3, pg);
                    gust = gust * gust * gust;
                    float w = Rnd(rnd);
                    d[i] = (v1.Run(w) * 1.2f + v2.Run(w) * (.4f + gust) + v3.Run(w) * .35f * gust) * (.15f + gust);
                }
                return d;
            }, .9f);
        }

        /// <summary>Camp fire: low roar with pops and hiss.</summary>
        public static AudioClip FireLoop(int seed)
        {
            const float len = 9f;
            var rnd = new System.Random(seed);
            return Loop("fire", len, total =>
            {
                var d = new float[total];
                var roar = Biquad.LowPass(220f); var pop = Biquad.BandPass(1800f, 1.5f); var hiss = Biquad.HighPass(3000f);
                float popEnv = 0f, hissEnv = 0f;
                for (int i = 0; i < total; i++)
                {
                    float w = Rnd(rnd);
                    if (rnd.NextDouble() < 9.0 / Rate) popEnv = .6f + (float)rnd.NextDouble();
                    if (rnd.NextDouble() < .4 / Rate) hissEnv = .25f;
                    popEnv *= .9975f; hissEnv *= .99995f;
                    float crackle = rnd.NextDouble() < 120.0 / Rate ? Rnd(rnd) * 3f : 0f;
                    d[i] = roar.Run(w) * .9f + pop.Run(w * popEnv + crackle) * 1.2f + hiss.Run(w) * hissEnv;
                }
                return d;
            }, .8f);
        }

        // ---------------------------------------------------------------- one-shots

        /// <summary>A trunk creaking in the wind: stick-slip pulses through wood resonances.</summary>
        public static AudioClip Creak(int seed)
        {
            var rnd = new System.Random(seed);
            float len = 1.2f + (float)rnd.NextDouble() * 1.4f, f0 = 18f + (float)rnd.NextDouble() * 22f, f1 = 40f + (float)rnd.NextDouble() * 50f;
            float r1 = 380f + (float)rnd.NextDouble() * 300f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var body = Biquad.BandPass(170f, 4f); var res1 = Biquad.BandPass(r1, 9f); var res2 = Biquad.BandPass(r1 * 2.3f, 7f);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = t / len;
                float rate = Mathf.Lerp(f0, f1, Mathf.Sin(u * Mathf.PI)) * (1f + .15f * Mathf.Sin(t * 7f));
                phase += rate / Rate;
                float x = 0f;
                if (phase >= 1f) { phase -= 1f; x = 1f + Rnd(rnd) * .3f; }
                x += Rnd(rnd) * .02f;
                d[i] = (body.Run(x) * .8f + res1.Run(x) * 1.3f + res2.Run(x) * .5f) * Env(t, .25f, .35f, len);
            }
            Normalize(d, .9f);
            return Clip("creak", d);
        }

        /// <summary>Owl calls. kind 0: eagle owl (филин), a deep two-note "ух-хуу"; kind 1: Ural owl (длиннохвостая неясыть), a hollow quavering series.</summary>
        public static AudioClip Owl(int kind, int seed)
        {
            var rnd = new System.Random(seed);
            // (start, duration, f start, f end)
            var notes = kind == 0
                ? new[] { (0f, .16f, 330f, 305f), (.34f, .62f, 355f, 300f), (2.6f, .16f, 325f, 300f), (2.94f, .6f, 350f, 298f) }
                : new[] { (0f, .5f, 420f, 395f), (1.9f, .22f, 410f, 400f), (2.25f, .12f, 430f, 420f), (2.42f, .12f, 425f, 415f), (2.59f, .12f, 420f, 410f), (2.8f, .7f, 430f, 380f) };
            float len = 0f; foreach (var nt in notes) len = Mathf.Max(len, nt.Item1 + nt.Item2);
            len += .3f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var breath = Biquad.BandPass(400f, 3f);
            foreach (var (start, dur, fa, fb) in notes)
            {
                double ph = 0;
                int i0 = (int)(start * Rate), cnt = (int)(dur * Rate);
                breath.SetBandPass((fa + fb) * .5f, 3f);
                for (int j = 0; j < cnt && i0 + j < n; j++)
                {
                    float t = (float)j / Rate, u = t / dur;
                    float f = Mathf.Lerp(fa, fb, u * u) * (1f + .012f * Mathf.Sin(t * 2f * Mathf.PI * 6f));
                    ph += f / Rate;
                    float s = Mathf.Sin((float)(ph * 2 * System.Math.PI)) + .18f * Mathf.Sin((float)(ph * 4 * System.Math.PI));
                    float e = Env(t, dur * .25f, dur * .45f, dur);
                    d[i0 + j] += (s + breath.Run(Rnd(rnd)) * 2.5f) * e;
                }
            }
            Normalize(d, .9f);
            return Clip(kind == 0 ? "owl_eagle" : "owl_ural", d);
        }

        /// <summary>Distant wolves: two voices gliding up, holding with vibrato, falling away.</summary>
        public static AudioClip Wolves(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = 6.5f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var voices = new[] { (0f, 4.6f, 330f, 610f, 470f), (1.5f, 4.4f, 380f, 700f, 540f) };
            var breath = Biquad.BandPass(600f, 4f);
            foreach (var (start, dur, fLow, fHigh, fEnd) in voices)
            {
                double ph = 0;
                int i0 = (int)(start * Rate), cnt = (int)(dur * Rate);
                float vib = 4.5f + (float)rnd.NextDouble();
                for (int j = 0; j < cnt && i0 + j < n; j++)
                {
                    float t = (float)j / Rate, u = t / dur;
                    float f = u < .18f ? Mathf.Lerp(fLow, fHigh, Mathf.SmoothStep(0, 1, u / .18f))
                        : u < .7f ? fHigh * (1f - .03f * (u - .18f))
                        : Mathf.Lerp(fHigh * .985f, fEnd, (u - .7f) / .3f);
                    f *= 1f + .01f * Mathf.Sin(t * 2f * Mathf.PI * vib) * Mathf.Clamp01((u - .2f) * 4f);
                    ph += f / Rate;
                    float p = (float)(ph * 2 * System.Math.PI);
                    float s = Mathf.Sin(p) + .3f * Mathf.Sin(2 * p) + .12f * Mathf.Sin(3 * p);
                    if (j % 256 == 0) breath.SetBandPass(f, 4f);
                    d[i0 + j] += (s + breath.Run(Rnd(rnd)) * 1.5f) * Env(t, .35f, 1.4f, dur);
                }
            }
            Normalize(d, .9f);
            return Clip("wolves", d);
        }

        /// <summary>A branch snapping: sharp crack, splintering tail, dull thud.</summary>
        public static AudioClip Snap(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = .9f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var crack = Biquad.HighPass(900f); var splinter = Biquad.BandPass(2600f, 1.2f); var thud = Biquad.LowPass(140f);
            float tail = .18f + (float)rnd.NextDouble() * .2f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, w = Rnd(rnd);
                float c = t < .006f ? w * 3f : 0f;
                float sp = rnd.NextDouble() < 300.0 / Rate * Mathf.Exp(-t / tail) ? Rnd(rnd) * 2f : 0f;
                float th = t > .05f ? w * Mathf.Exp(-(t - .05f) / .09f) : 0f;
                d[i] = crack.Run(c) + splinter.Run(sp) * .8f + thud.Run(th) * 2.5f;
            }
            Normalize(d, .95f);
            return Clip("snap", d);
        }

        /// <summary>Snow sliding off a spruce and landing.</summary>
        public static AudioClip SnowFall(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = 1.8f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var sweep = Biquad.LowPass(900f); var thump = Biquad.LowPass(110f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, w = Rnd(rnd);
                d[i] = sweep.Run(w) * Env(t, .5f, .5f, 1.2f) * .6f + thump.Run(w) * (t > 1.1f ? 3.5f * Mathf.Exp(-(t - 1.1f) / .12f) : 0f);
            }
            Normalize(d, .8f);
            return Clip("snowfall", d);
        }

        /// <summary>A footstep in frozen snow at −25 °C: a crunch with the squeak of packing crystals.</summary>
        public static AudioClip Step(int seed)
        {
            var rnd = new System.Random(seed);
            float len = .26f + (float)rnd.NextDouble() * .08f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var grain = Biquad.BandPass(2200f + (float)rnd.NextDouble() * 900f, 1.1f); var body = Biquad.LowPass(260f);
            var squeak = Biquad.BandPass(1900f, 14f);
            bool squeaky = rnd.NextDouble() < .7;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = t / len, w = Rnd(rnd);
                float density = Mathf.Lerp(900f, 60f, u);
                float click = rnd.NextDouble() < density / Rate ? Rnd(rnd) * 2f : 0f;
                if (squeaky && i % 128 == 0) squeak.SetBandPass(1700f + 900f * Mathf.Sin(u * Mathf.PI), 14f);
                float e = Env(t, .015f, len * .6f, len);
                d[i] = (grain.Run(click + w * .15f) + body.Run(w) * .6f * (1f - u) + (squeaky ? squeak.Run(w) * .9f * Mathf.Sin(u * Mathf.PI) : 0f)) * e;
            }
            Normalize(d, .9f);
            return Clip("step", d);
        }

        /// <summary>One slow breath, in and out, through a scarf.</summary>
        public static AudioClip Breath(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = 2.6f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var inhale = Biquad.BandPass(1100f, 1.1f); var exhale = Biquad.BandPass(650f, 1.3f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, w = Rnd(rnd);
                d[i] = inhale.Run(w) * Env(t, .35f, .45f, .95f) * .7f + exhale.Run(w) * Env(t - 1.15f, .15f, .8f, 1.3f);
            }
            Normalize(d, .8f);
            return Clip("breath", d);
        }

        /// <summary>Heartbeat, "lub-dub".</summary>
        public static AudioClip Heart()
        {
            const float len = .6f;
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float a = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t / .06f);
                float t2 = t - .26f;
                float b = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 44f * t2) * Mathf.Exp(-t2 / .07f) * .7f : 0f;
                d[i] = (a + b) * Mathf.Clamp01(t / .004f);
            }
            Normalize(d, .9f);
            return Clip("heart", d);
        }

        // ---------------------------------------------------------------- the giant

        /// <summary>A heavy footfall in deep snow: a low body thump with a muffled crunch.</summary>
        public static AudioClip Thud(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = .7f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var low = Biquad.LowPass(90f, 1.2f); var crunch = Biquad.BandPass(900f, .8f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, w = Rnd(rnd);
                float body = Mathf.Sin(2f * Mathf.PI * (48f - 20f * t) * t) * Mathf.Exp(-t / .12f);
                d[i] = body * 1.4f + low.Run(w) * Mathf.Exp(-t / .1f) * 3f + crunch.Run(w) * Mathf.Exp(-t / .07f) * .5f;
            }
            Normalize(d, .95f);
            return Clip("thud", d);
        }

        /// <summary>Breathing growl of something very large: a low buzzing voice through the resonances of a huge chest and throat.</summary>
        public static AudioClip Growl(int seed, float len, float pitch, bool roar)
        {
            var rnd = new System.Random(seed);
            int n = (int)(len * Rate);
            var d = new float[n];
            var f1 = Biquad.BandPass(260f, 5f); var f2 = Biquad.BandPass(620f, 6f); var f3 = Biquad.BandPass(1500f, 4f);
            var chest = Biquad.LowPass(180f); var rasp = Biquad.BandPass(2400f, 1.2f);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = t / len;
                float swell = roar ? Mathf.Sin(Mathf.PI * Mathf.Pow(u, .6f)) : .6f + .4f * Mathf.Sin(u * Mathf.PI * 3f);
                float f0 = pitch * (1f + (roar ? .5f * Mathf.Sin(Mathf.PI * u) : .06f * Mathf.Sin(t * 5f))) * (1f + .03f * Rnd(rnd));
                ph += f0 / Rate;
                float frac = (float)(ph - System.Math.Floor(ph));
                // pulse train with jitter: vocal folds slapping
                float src = (frac < .12f ? 1f - frac / .12f : 0f) * 2f - .25f + Rnd(rnd) * (roar ? .9f : .5f);
                if (i % 128 == 0) { f1.SetBandPass(240f + 60f * swell, 5f); f2.SetBandPass(560f + 160f * swell, 6f); }
                float v = f1.Run(src) * 1.2f + f2.Run(src) * .8f + f3.Run(src) * (roar ? .5f : .15f) + chest.Run(src) * .9f + rasp.Run(Rnd(rnd)) * (roar ? .25f : .06f) * swell;
                d[i] = v * swell * Env(t, roar ? .15f : .4f, roar ? .8f : .5f, len);
            }
            Normalize(d, .95f);
            return Clip(roar ? "roar" : "growl", d);
        }

        /// <summary>A fist hitting the tent: a boom of the canvas, poles rattling, cloth flapping.</summary>
        public static AudioClip CanvasHit(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = 1.4f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var boom = Biquad.LowPass(140f, 2f); var cloth = Biquad.BandPass(700f, .9f); var wood = Biquad.BandPass(1300f, 8f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, w = Rnd(rnd);
                float b = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / .09f);
                float flap = t < .9f && rnd.NextDouble() < 60.0 / Rate ? Rnd(rnd) * 3f : 0f;
                float knock = (t > .03f && t < .05f) || (t > .16f && t < .175f) ? w * 2f : 0f;
                d[i] = b * 1.6f + boom.Run(w) * Mathf.Exp(-t / .12f) * 3f + cloth.Run(w * Mathf.Exp(-t / .3f) + flap) * .9f + wood.Run(knock) * 1.2f;
            }
            Normalize(d, .95f);
            return Clip("canvas_hit", d);
        }

        /// <summary>A huge arm cutting the air.</summary>
        public static AudioClip Swoosh(int seed)
        {
            var rnd = new System.Random(seed);
            const float len = .6f;
            int n = (int)(len * Rate);
            var d = new float[n];
            var bp = Biquad.BandPass(300f, 2f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = t / len;
                if (i % 64 == 0) bp.SetBandPass(250f + 900f * Mathf.Sin(Mathf.PI * u), 2f);
                d[i] = bp.Run(Rnd(rnd)) * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * u);
            }
            Normalize(d, .9f);
            return Clip("swoosh", d);
        }
    }
}
