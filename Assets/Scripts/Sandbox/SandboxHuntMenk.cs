using UnityEngine;
using Height1079.Art;
using Height1079.Core;
using Height1079.Night;

namespace Height1079.Sandbox
{
    /// <summary>The yard's Menk as it is seen and heard: the game's own jointed model posed by the shared
    /// <see cref="MenkPuppet"/> off what the <see cref="HunterBrain"/> is doing, the same synthesised footfalls,
    /// growl and roar. There is no session under it — the brain runs in the sandbox itself — so this reads the
    /// brain directly where the night's <c>MenkView</c> reads network values. With no generated world (a fresh
    /// clone) a dark giant of primitives stands in for the model; the rules are the same either way.</summary>
    public sealed class SandboxHuntMenk : MonoBehaviour
    {
        MenkPuppet puppet;
        Transform model;
        AudioSource feet, voice, shout;
        AudioClip thud, growl, roar, swoosh;
        HunterBrain.State state = HunterBrain.State.Patrol;
        float phase, speed, shownYaw, stateTime, swayPhase, lastSign, treeW;
        Vector3 shown, last;
        bool placed;
        /// <summary>0..1 how much the ground shakes at the camera from its steps (the rig may read it).</summary>
        public float Tremor { get; private set; }
        public Transform Model => model;

        public static SandboxHuntMenk Create(Transform parent)
        {
            var go = new GameObject("HuntMenk", typeof(SandboxHuntMenk));
            go.transform.SetParent(parent, false);
            return go.GetComponent<SandboxHuntMenk>();
        }

        void Awake()
        {
            puppet = MenkPuppet.Load(transform);
            if (puppet != null)
            {
                model = puppet.Model;
                // a line of sight is checked against the yard, never against the creature itself
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            }
            else model = Fallback();

            voice = model.gameObject.AddComponent<AudioSource>();
            feet = model.gameObject.AddComponent<AudioSource>();
            shout = model.gameObject.AddComponent<AudioSource>();
            foreach (var a in new[] { voice, feet, shout })
            {
                a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 6f; a.maxDistance = 160f; a.dopplerLevel = 0f; a.playOnAwake = false;
            }
            thud = Synth.Thud(61); growl = Synth.Growl(62, 4f, 46f, false); roar = Synth.Growl(63, 2.6f, 70f, true); swoosh = Synth.Swoosh(65);
            voice.clip = growl; voice.loop = true;
        }

        /// <summary>No model in Resources: a giant made of a capsule, a head and two arms hanging to the ground.</summary>
        Transform Fallback()
        {
            var root = new GameObject("MenkFallback").transform;
            root.SetParent(transform, false);
            // «Чёрный: уголь, Менк» is the sheet's colour for exactly this, and the flat material is shared, so it
            // is taken as it comes: tuning its gloss here would re-tune every black thing on the map
            var hide = Palette.Flat(Palette.Black);
            GameObject Part(PrimitiveType type, Vector3 at, Vector3 size, string name)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name; go.transform.SetParent(root, false);
                go.transform.localPosition = at; go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = hide;
                Destroy(go.GetComponent<Collider>());
                return go;
            }
            Part(PrimitiveType.Capsule, new Vector3(0f, 2.3f, 0f), new Vector3(1.1f, 1.35f, .9f), "Body");
            Part(PrimitiveType.Sphere, new Vector3(0f, 3.85f, .15f), new Vector3(.6f, .55f, .6f), "Head");
            Part(PrimitiveType.Capsule, new Vector3(-.65f, 1.9f, .1f), new Vector3(.28f, 1.05f, .28f), "ArmL");
            Part(PrimitiveType.Capsule, new Vector3(.65f, 1.9f, .1f), new Vector3(.28f, 1.05f, .28f), "ArmR");
            Part(PrimitiveType.Capsule, new Vector3(-.28f, .75f, 0f), new Vector3(.36f, .8f, .36f), "LegL");
            Part(PrimitiveType.Capsule, new Vector3(.28f, .75f, 0f), new Vector3(.36f, .8f, .36f), "LegR");
            return root;
        }

        /// <summary>Called every frame with the brain: puts the body where the brain is, at the height of the snow
        /// there, and animates and sounds the change of state.</summary>
        public void Show(HunterBrain b, float dt)
        {
            if (model == null) return;
            float ground = Physics.Raycast(new Vector3(b.X, 8f, b.Z), Vector3.down, out var hit, 16f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            var target = new Vector3(b.X, ground, b.Z);
            if (!placed || (target - shown).sqrMagnitude > 400f) { shown = target; shownYaw = b.Yaw; last = target; placed = true; }
            shown = Vector3.Lerp(shown, target, 1f - Mathf.Exp(-10f * dt));
            shownYaw = Mathf.LerpAngle(shownYaw, b.Yaw, 1f - Mathf.Exp(-8f * dt));
            model.SetPositionAndRotation(shown, Quaternion.Euler(0f, shownYaw, 0f));
            float moved = new Vector2(shown.x - last.x, shown.z - last.z).magnitude;
            last = shown;
            speed = Mathf.Lerp(speed, dt > 0f ? moved / dt : 0f, 1f - Mathf.Exp(-6f * dt));

            if (b.Mode != state) OnState(b.Mode);
            state = b.Mode;
            stateTime = b.StateTime;

            Animate(dt);
            Sound(dt);
        }

        void OnState(HunterBrain.State to)
        {
            switch (to)
            {
                case HunterBrain.State.Chase:
                    shout.pitch = Random.Range(.9f, 1.05f); shout.PlayOneShot(roar, 1f);
                    break;
                case HunterBrain.State.Strike:
                    feet.pitch = .8f; feet.PlayOneShot(swoosh, .8f);
                    break;
                case HunterBrain.State.ToLight:
                case HunterBrain.State.ToNoise:
                    shout.pitch = Random.Range(.85f, 1f); shout.PlayOneShot(growl, .5f);
                    break;
            }
        }

        void Animate(float dt)
        {
            // it never plays tree in the yard; the pose is alive throughout, listening with the head
            treeW = Mathf.MoveTowards(treeW, 0f, dt);
            phase += speed * dt / 2.1f * Mathf.PI;
            float gait = Mathf.Clamp01(speed / 1.5f);
            swayPhase += dt * (.6f + Weather.Wind);
            MenkPuppet.Swing(state == HunterBrain.State.Strike ? stateTime : -1f, out float raise, out float slam);
            bool listening = state == HunterBrain.State.Listen || state == HunterBrain.State.LookAround;
            if (puppet != null)
                puppet.Apply(new MenkPuppet.Pose
                {
                    Tree = treeW, Gait = gait, Phase = phase,
                    Hunch = state == HunterBrain.State.Chase ? 30f : 20f,
                    Raise = raise, Slam = slam, TwoHands = false,
                    Sway = Mathf.Sin(swayPhase) * Weather.Wind * 1.5f, Shudder = 0f,
                    HeadTurn = listening ? Mathf.Sin(Time.time * 1.1f) * 35f : Mathf.Sin(Time.time * .7f) * 12f * (1f - gait),
                });
            else
            {
                // the stand-in: a bob to the stride and arms that swing
                float bob = gait * Mathf.Abs(Mathf.Sin(phase)) * .12f;
                model.localPosition = new Vector3(0f, bob, 0f);
                var l = model.Find("ArmL"); var r = model.Find("ArmR");
                if (l != null) l.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 28f * gait - raise * 150f, 0f, 8f);
                if (r != null) r.localRotation = Quaternion.Euler(-Mathf.Sin(phase) * 28f * gait - raise * 150f, 0f, -8f);
            }
        }

        void Sound(float dt)
        {
            var cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, shown) : 999f;
            float sign = Mathf.Sign(Mathf.Sin(phase));
            if (sign != lastSign && speed > .3f)
            {
                feet.pitch = Random.Range(.85f, 1.05f);
                feet.PlayOneShot(thud, Mathf.Clamp01(.5f + speed * .2f));
                Tremor = Mathf.Max(Tremor, Mathf.Clamp01(1f - dist / 35f));
            }
            lastSign = sign;
            Tremor = Mathf.MoveTowards(Tremor, 0f, dt * 3f);
            bool loud = state == HunterBrain.State.Chase || state == HunterBrain.State.ToLight || state == HunterBrain.State.ToNoise || state == HunterBrain.State.Tracking;
            float vol = loud ? (state == HunterBrain.State.Chase ? .75f : .4f) : 0f;
            if (vol > 0f && !voice.isPlaying) { voice.pitch = Random.Range(.85f, 1f); voice.Play(); }
            voice.volume = Mathf.MoveTowards(voice.volume, vol, dt * (vol > voice.volume ? .8f : 3f));
            if (voice.volume <= .001f && voice.isPlaying) voice.Stop();
        }
    }
}
