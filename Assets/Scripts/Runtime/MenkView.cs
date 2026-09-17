using System.Collections.Generic;
using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Draws the Menk on every client from the session's network values: procedural animation of its joints
    /// (a dead tree with raised crooked arms; a hunched stride; hammer blows), heavy steps that shake the view,
    /// its growl and roar, blows on the tent canvas.</summary>
    public sealed class MenkView : MonoBehaviour
    {
        /// <summary>0..1: how much the ground shakes at the camera (read by the first-person camera).</summary>
        public static float Tremor { get; private set; }

        Transform model;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        Transform hips; Vector3 hipsRest;
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
            var prefab = Resources.Load<GameObject>("World/Prefabs/Creatures/Menk");
            if (prefab == null) { Debug.LogWarning("1079: Menk prefab missing — menu 1079 → Rebuild world"); enabled = false; return; }
            model = Instantiate(prefab, transform).transform;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) { bones[t.name] = t; rest[t] = t.localRotation; }
            hips = Bone("Hips"); if (hips != null) hipsRest = hips.localPosition;
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

        Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : null;

        void Update()
        {
            if (model == null) return;
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

        static Quaternion E(float x, float y, float z) => Quaternion.Euler(x, y, z);

        void Set(string bone, Quaternion tree, Quaternion alive)
        {
            var t = Bone(bone);
            if (t == null) return;
            t.localRotation = rest[t] * Quaternion.Slerp(alive, tree, treeW);
        }

        void Animate()
        {
            bool still = state == MenkBrain.State.Tree || state == MenkBrain.State.Frozen;
            float treeRate = state == MenkBrain.State.Frozen ? 2.2f : state == MenkBrain.State.Waking ? .45f : 1.2f;
            treeW = Mathf.MoveTowards(treeW, still ? 1f : 0f, Time.deltaTime * treeRate);

            float stride = 2.1f;
            phase += speed * Time.deltaTime / stride * Mathf.PI;
            float sp = Mathf.Sin(phase), cp = Mathf.Cos(phase);
            float gait = Mathf.Clamp01(speed / 1.5f);
            bool hunting = state == MenkBrain.State.Hunting;
            float hunch = hunting ? 30f : 20f;

            // wind in the "branches" while it pretends; a shudder while it wakes
            swayPhase += Time.deltaTime * (.6f + Weather.Wind);
            float wind = Weather.Wind * 3f;
            float shudder = state == MenkBrain.State.Waking ? (Mathf.PerlinNoise(Time.time * 18f, 0) - .5f) * 10f * (1f - stateTime / 3.2f) : 0f;
            float tremble = state == MenkBrain.State.Frozen ? (Mathf.PerlinNoise(Time.time * 25f, 4f) - .5f) * 1.5f : 0f;
            float sway = Mathf.Sin(swayPhase) * wind + tremble;

            // strike / blow timing (0..1.4 s)
            float st = state == MenkBrain.State.Striking ? stateTime : state == MenkBrain.State.Beating ? Mathf.Repeat(stateTime, 1.5f) : -1f;
            float raise = st < 0f ? 0f : st < .45f ? Mathf.SmoothStep(0, 1, st / .45f) : st < .75f ? 1f - Mathf.SmoothStep(0, 1, (st - .45f) / .3f) : 0f;
            float slam = st < 0f ? 0f : st < .45f ? 0f : st < .75f ? Mathf.SmoothStep(0, 1, (st - .45f) / .3f) : 1f - Mathf.SmoothStep(0, 1, (st - .75f) / .65f);
            bool twoHands = state == MenkBrain.State.Beating;

            float bob = gait * Mathf.Abs(sp);
            Set("Spine", E(-2f + sway * .4f, 0, 2f + sway), E(hunch + bob * 3f - raise * 18f + slam * 25f + shudder, sp * 4f * gait, sp * 3f * gait));
            Set("Chest", E(0, 0, -3f + sway * .6f), E(6f - raise * 8f + slam * 12f, -sp * 6f * gait, 0));
            Set("Neck", E(38f, 0, sway * .3f), E(-hunch - 4f + shudder * .5f, 0, 0));
            Set("Head", E(22f, 0, 0), E(-10f + raise * 10f, Mathf.Sin(Time.time * .7f) * 12f * (1f - gait), 0));

            float swingL = -sp * 26f * gait, swingR = sp * 26f * gait;
            Quaternion armRAlive = E(swingR + 6f, 0, 8f);
            armRAlive = Quaternion.Slerp(armRAlive, E(-168f, 0, 18f), raise);
            armRAlive = Quaternion.Slerp(armRAlive, E(-25f, 0, 6f), slam);
            Quaternion armLAlive = E(swingL + 6f, 0, -8f);
            if (twoHands)
            {
                armLAlive = Quaternion.Slerp(armLAlive, E(-168f, 0, -18f), raise);
                armLAlive = Quaternion.Slerp(armLAlive, E(-25f, 0, -6f), slam);
            }
            Set("ArmL", E(-12f, sway * .5f, -146f + sway * 2f), armLAlive);
            Set("ArmR", E(-8f, -sway * .5f, 142f - sway * 2f), armRAlive);
            Set("ForearmL", E(0, 0, 34f + sway * 3f), E(-22f - 10f * gait - raise * 25f, 0, 0));
            Set("ForearmR", E(0, 0, -40f - sway * 3f), E(-22f - 10f * gait - raise * 25f, 0, 0));
            Set("HandL", E(0, 0, 22f), E(-10f, 0, 0));
            Set("HandR", E(0, 0, -18f), E(-10f, 0, 0));

            float kneeL = Mathf.Max(0f, cp) * 50f * gait + 8f, kneeR = Mathf.Max(0f, -cp) * 50f * gait + 8f;
            Set("ThighL", E(0, 0, 2f), E(-sp * 30f * gait - 10f - slam * 8f, 0, 3f));
            Set("ThighR", E(0, 0, -2f), E(sp * 30f * gait - 10f - slam * 8f, 0, -3f));
            Set("ShinL", E(0, 0, 0), E(kneeL + slam * 12f, 0, 0));
            Set("ShinR", E(0, 0, 0), E(kneeR + slam * 12f, 0, 0));
            Set("FootL", E(0, 0, 0), E(-kneeL * .3f, 0, 0));
            Set("FootR", E(0, 0, 0), E(-kneeR * .3f, 0, 0));

            if (hips != null) hips.localPosition = hipsRest + Vector3.up * ((1f - treeW) * (-.18f - bob * .1f - slam * .25f));
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
