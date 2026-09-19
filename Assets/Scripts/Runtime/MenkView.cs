using System.Collections.Generic;
using UnityEngine;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Draws the Menk on every client from the session's network values: procedural animation of its joints
    /// (a dead tree with raised crooked arms; a hunched stride; hammer blows), heavy steps that shake the view,
    /// its growl and roar, blows on the tent canvas.</summary>
    public sealed class MenkView : MonoBehaviour
    {
        /// <summary>0..1: how much the ground shakes at the camera (read by the first-person camera).</summary>
        public static float Tremor { get; private set; }
        /// <summary>F4: the camera watches the Menk from a few metres in front of it (for checking the creature).</summary>
        public static bool Watching { get; private set; }
        static MenkView instance;

        /// <summary>Called by the player camera; returns true when it placed the camera.</summary>
        public static bool Watch(Camera cam)
        {
            if (!Watching || instance == null || instance.model == null || !instance.model.gameObject.activeSelf) return false;
            var m = instance.model;
            var eye = m.position + m.forward * 9f + m.right * 3f + Vector3.up * 2.2f;
            eye.y = Mathf.Max(eye.y, TerrainBuilder.Height(Bootstrap.Dem, eye.x, eye.z) + 1.6f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, eye, 1f - Mathf.Exp(-4f * Time.deltaTime));
            cam.transform.LookAt(m.position + Vector3.up * 2.4f);
            return true;
        }

        Transform model;
        MenkPuppet puppet;
        AudioSource voice, feet, shout;
        AudioClip thud, growl, roar, canvas, swoosh, creak;
        MenkBrain.State state = MenkBrain.State.Tree;
        float stateTime, treeW = 1f, phase, speed, lastBlowTime = -10f, swayPhase;
        byte blows;
        Vector3 shownPos, lastPos;
        float shownYaw;
        bool placed, subscribed;
        Transform tent; Quaternion tentRest; float tentShake;
        NightSession session;

        public static MenkView Create()
        {
            var go = new GameObject("MenkView", typeof(MenkView));
            DontDestroyOnLoad(go);
            return go.GetComponent<MenkView>();
        }

        void Awake()
        {
            instance = this;
            puppet = MenkPuppet.Load(transform);
            if (puppet == null) { Debug.LogWarning("1079: Menk prefab missing — menu 1079 → Rebuild world"); enabled = false; return; }
            model = puppet.Model;
            model.gameObject.SetActive(false);

            voice = model.gameObject.AddComponent<AudioSource>();
            feet = model.gameObject.AddComponent<AudioSource>();
            shout = model.gameObject.AddComponent<AudioSource>();
            foreach (var a in new[] { voice, feet, shout })
            {
                a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 6f; a.maxDistance = 220f; a.dopplerLevel = 0f; a.playOnAwake = false;
            }
            thud = Synth.Thud(61); growl = Synth.Growl(62, 4f, 46f, false); roar = Synth.Growl(63, 2.6f, 70f, true);
            canvas = Synth.CanvasHit(64); swoosh = Synth.Swoosh(65); creak = Synth.Creak(66);
            voice.clip = growl; voice.loop = true;
        }

        void Update()
        {
            if (model == null) return;
            if (Controls.MenkWatch) Watching = !Watching;
            var s = NightSession.Instance;
            if (s != session)
            {
                if (session != null && subscribed) session.MenkHitMe -= OnHitMe;
                session = s; subscribed = false; placed = false;
            }
            if (s == null) { model.gameObject.SetActive(false); Tremor = 0f; return; }
            if (!subscribed) { s.MenkHitMe += OnHitMe; subscribed = true; }
            if (!model.gameObject.activeSelf) model.gameObject.SetActive(true);

            // follow the server
            var target = s.MenkPos.Value;
            if (!placed || (target - shownPos).sqrMagnitude > 400f) { shownPos = target; shownYaw = s.MenkYaw.Value; lastPos = target; placed = true; }
            shownPos = Vector3.Lerp(shownPos, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            shownYaw = Mathf.LerpAngle(shownYaw, s.MenkYaw.Value, 1f - Mathf.Exp(-8f * Time.deltaTime));
            model.SetPositionAndRotation(shownPos, Quaternion.Euler(0, shownYaw, 0));
            float moved = new Vector2(shownPos.x - lastPos.x, shownPos.z - lastPos.z).magnitude;
            lastPos = shownPos;
            speed = Mathf.Lerp(speed, Time.deltaTime > 0 ? moved / Time.deltaTime : 0f, 1f - Mathf.Exp(-6f * Time.deltaTime));

            var newState = (MenkBrain.State)s.MenkState.Value;
            if (newState != state) OnState(state, newState);
            state = newState;
            stateTime += Time.deltaTime;
            if (s.MenkBlows.Value != blows) { blows = s.MenkBlows.Value; OnBlow(); }

            Animate();
            Sound();
            ShakeTent();
        }

        void OnState(MenkBrain.State from, MenkBrain.State to)
        {
            stateTime = 0f;
            switch (to)
            {
                case MenkBrain.State.Waking:
                    feet.pitch = .55f; feet.PlayOneShot(creak, 1f);
                    shout.pitch = .9f; shout.PlayOneShot(growl, .7f);
                    break;
                case MenkBrain.State.Hunting:
                    if (from != MenkBrain.State.Striking && from != MenkBrain.State.Frozen) { shout.pitch = Random.Range(.9f, 1.05f); shout.PlayOneShot(roar, 1f); }
                    break;
                case MenkBrain.State.Striking:
                    feet.pitch = .8f; feet.PlayOneShot(swoosh, .8f);
                    break;
                case MenkBrain.State.Frozen:
                case MenkBrain.State.Tree:
                    feet.pitch = Random.Range(.5f, .7f); feet.PlayOneShot(creak, .7f);
                    break;
            }
        }

        void OnBlow()
        {
            lastBlowTime = Time.time;
            feet.pitch = Random.Range(.9f, 1.05f);
            feet.PlayOneShot(canvas, 1f);
            feet.PlayOneShot(swoosh, .4f);
            tentShake = 1f;
            if (Random.value < .5f) shout.PlayOneShot(growl, .6f);
        }

        void OnHitMe(Vector3 dir, float damage)
        {
            shout.pitch = 1.1f; shout.PlayOneShot(roar, 1f);
            feet.pitch = .7f; feet.PlayOneShot(thud, 1f);
            NightFilm.Instance?.Flash(damage > 40f ? 1f : .6f);
        }

        // ------------------------------------------------------------------ animation

        void Animate()
        {
            bool still = state == MenkBrain.State.Tree || state == MenkBrain.State.Frozen;
            float treeRate = state == MenkBrain.State.Frozen ? 2.2f : state == MenkBrain.State.Waking ? .45f : 1.2f;
            treeW = Mathf.MoveTowards(treeW, still ? 1f : 0f, Time.deltaTime * treeRate);

            float stride = 2.1f;
            phase += speed * Time.deltaTime / stride * Mathf.PI;
            float gait = Mathf.Clamp01(speed / 1.5f);
            bool hunting = state == MenkBrain.State.Hunting;

            // wind in the "branches" while it pretends; a shudder while it wakes
            swayPhase += Time.deltaTime * (.6f + Weather.Wind);
            float wind = Weather.Wind * 3f;
            float shudder = state == MenkBrain.State.Waking ? (Mathf.PerlinNoise(Time.time * 18f, 0) - .5f) * 10f * (1f - stateTime / 3.2f) : 0f;
            float tremble = state == MenkBrain.State.Frozen ? (Mathf.PerlinNoise(Time.time * 25f, 4f) - .5f) * 1.5f : 0f;

            // strike / blow timing (0..1.4 s)
            float st = state == MenkBrain.State.Striking ? stateTime : state == MenkBrain.State.Beating ? Mathf.Repeat(stateTime, 1.5f) : -1f;
            MenkPuppet.Swing(st, out float raise, out float slam);

            puppet.Apply(new MenkPuppet.Pose
            {
                Tree = treeW, Gait = gait, Phase = phase,
                Hunch = hunting ? 30f : 20f,
                Raise = raise, Slam = slam, TwoHands = state == MenkBrain.State.Beating,
                Sway = Mathf.Sin(swayPhase) * wind + tremble, Shudder = shudder,
                HeadTurn = Mathf.Sin(Time.time * .7f) * 12f * (1f - gait),
            });
        }

        // ------------------------------------------------------------------ sound and shaking

        float lastStepSign;

        void Sound()
        {
            var cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, shownPos) : 999f;
            // footfalls: one per half stride
            float sign = Mathf.Sign(Mathf.Sin(phase));
            if (sign != lastStepSign && speed > .3f)
            {
                feet.pitch = Random.Range(.85f, 1.05f);
                feet.PlayOneShot(thud, Mathf.Clamp01(.5f + speed * .2f));
                Tremor = Mathf.Max(Tremor, Mathf.Clamp01(1f - dist / 35f));
            }
            lastStepSign = sign;
            if (Time.time - lastBlowTime < .2f) Tremor = Mathf.Max(Tremor, Mathf.Clamp01(1f - dist / 25f));
            Tremor = Mathf.MoveTowards(Tremor, 0f, Time.deltaTime * 3f);

            // breathing growl while it moves or hunts; silence while it pretends
            bool loud = state == MenkBrain.State.Hunting || state == MenkBrain.State.ToTent || state == MenkBrain.State.Beating || state == MenkBrain.State.Retreat;
            float vol = loud ? (state == MenkBrain.State.Hunting ? .75f : .45f) : 0f;
            if (vol > 0f && !voice.isPlaying) { voice.pitch = Random.Range(.85f, 1f); voice.Play(); }
            voice.volume = Mathf.MoveTowards(voice.volume, vol, Time.deltaTime * (vol > voice.volume ? .8f : 3f));
            if (voice.volume <= .001f && voice.isPlaying) voice.Stop();
        }

        void ShakeTent()
        {
            if (tent == null)
            {
                var go = GameObject.Find("Site_Camp_31Jan_Tent(Clone)");
                if (go == null) return;
                tent = go.transform; tentRest = tent.localRotation;
            }
            tentShake = Mathf.MoveTowards(tentShake, 0f, Time.deltaTime * 1.2f);
            float k = tentShake * tentShake;
            tent.localRotation = tentRest * Quaternion.Euler(Mathf.Sin(Time.time * 31f) * 1.8f * k, 0, Mathf.Sin(Time.time * 23f + 1f) * 2.4f * k);
        }

        void OnDestroy()
        {
            if (session != null && subscribed) session.MenkHitMe -= OnHitMe;
        }
    }
}
