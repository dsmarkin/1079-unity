using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;
using Height1079.Night;
using Height1079.Snow;

namespace Height1079.Sandbox
{
    /// <summary>One run of the yard, single player: out from the fire, into the tent for the things, past the Menk
    /// and back before the blizzard walls the yard. The rules are all in Core (<see cref="HuntRun"/>,
    /// <see cref="HunterBrain"/>, <see cref="Errand"/>); this is the engine side of them — what the body is doing
    /// this frame as the Menk would read it, whether a rock stands between them, the things as objects in hands
    /// and in the snow, the torch, the night and the debrief.
    ///
    /// N starts a run (the night comes down, the Menk appears, the list is laid out in the tent) and N again
    /// abandons it. The torch is the kit's own flashlight (<see cref="SandboxGear"/>, the game's tube on F); what
    /// the run adds is that a heavy thing in the hands puts it out. The run ends by itself: home at the fire when the wall comes, or dead — by the Menk, or by the
    /// cold outside the fire. There is no timer on the screen, on purpose: the front is heard and seen coming.</summary>
    public sealed class SandboxHunt : MonoBehaviour
    {
        public const string Me = "Путник";
        public static SandboxHunt Instance { get; private set; }

        public HuntRun Run { get; private set; }
        /// <summary>A run is on and not over.</summary>
        public bool Active => Run != null && !Run.Over;
        /// <summary>The run is over and the debrief is up (until the next F11).</summary>
        public List<string> Report { get; private set; }
        public bool Dead { get; private set; }
        /// <summary>Minutes a run lasts. The brief's fifteen; shorter for finding things out.</summary>
        public float RunMinutes = 15f;

        /// <summary>The kit's flashlight is in the hands and lit — what the Menk sees from forty metres.</summary>
        public bool Torch => boot != null && boot.Gear != null && boot.Gear.TorchOn && boot.Gear.TorchInHand && !boot.Dead;
        public bool Crouch, Prone;
        public ErrandItem Held => Run?.Errand.Held(Me);
        /// <summary>What the Menk is going on right now, for the panel.</summary>
        public HunterBrain Menk => Run?.Menk;
        /// <summary>Whether something stood between the Menk's eyes and the body this frame.</summary>
        public bool Covered { get; private set; }
        public SandboxNight Night { get; private set; }

        SandboxBoot boot;
        SandboxHuntMenk menkView;
        readonly Dictionary<ErrandItem, Transform> props = new Dictionary<ErrandItem, Transform>();
        readonly List<HuntSeen> seen = new List<HuntSeen>(1);
        float trackMeter, wasAliveCheck;
        Vector3 lastPos;
        bool running, walking;

        public static SandboxHunt Create(SandboxBoot boot)
        {
            var go = new GameObject("SandboxHunt", typeof(SandboxHunt));
            go.transform.SetParent(boot.transform, false);
            var h = go.GetComponent<SandboxHunt>();
            h.boot = boot;
            h.Night = SandboxNight.Create(go.transform, boot.Cam);
            return h;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── a run ──

        public void Begin(float seconds = -1f)
        {
            if (Run != null) End();
            if (seconds <= 0f) seconds = RunMinutes * 60f;
            var fire = SandboxHuntYard.Fire; var tent = SandboxHuntYard.Tent;
            Run = new HuntRun(fire.x, fire.z, tent.x, tent.z, seconds, Random.Range(1, 100000));
            Run.Join(Me);
            Report = null; Dead = false; Crouch = Prone = false;
            trackMeter = 0f;
            Night.Want = 1f; Night.Storm = 0f;
            menkView = SandboxHuntMenk.Create(transform);
            LayOut();
            boot.Revive(SandboxHuntYard.Spawn);
            lastPos = boot.Body.Torso.position;
            SandboxHud.Say("Ночь. Вещи в палатке впереди; принеси их к костру. F — фонарик, C — присесть, Z — лечь, R — взять и положить, X — бросить.");
        }

        /// <summary>Back to the day, the yard left standing.</summary>
        public void End()
        {
            Run = null; Report = null; Dead = false; Crouch = Prone = false;
            Night.Want = 0f; Night.Storm = 0f;
            if (menkView != null) Destroy(menkView.gameObject);
            menkView = null;
            foreach (var p in props.Values) if (p != null) Destroy(p.gameObject);
            props.Clear();
        }

        /// <summary>The run clock jumps ahead — for the shots and the self-test, which cannot wait ten minutes for a front.</summary>
        public void Skip(float seconds) => Run?.Skip(seconds);

        /// <summary>The things, as objects: each a small block of its own colour on the snow where the list says.</summary>
        void LayOut()
        {
            foreach (var item in Run.Errand.Items)
            {
                var go = GameObject.CreatePrimitive(item.Carry == Carry.Pair ? PrimitiveType.Capsule : PrimitiveType.Cube);
                go.name = "Item " + item.Name;
                go.transform.SetParent(transform, true);
                Color c; Vector3 size;
                switch (item.Name)
                {
                    case "дневник": c = new Color(.36f, .22f, .14f); size = new Vector3(.24f, .05f, .32f); break;
                    case "фотоаппарат": c = new Color(.12f, .12f, .13f); size = new Vector3(.15f, .1f, .09f); break;
                    case "печка": c = new Color(.45f, .45f, .47f); size = new Vector3(.5f, .42f, .5f); break;
                    default: c = new Color(.35f, .4f, .32f); size = new Vector3(.5f, .8f, .5f); break;
                }
                var m = new Material(Shader.Find("Standard")) { color = c };
                m.SetFloat("_Glossiness", item.Name == "фотоаппарат" ? .5f : .1f);
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.transform.localScale = size;
                go.transform.position = new Vector3(item.X, SandboxHuntYard.SnowTop + size.y * .5f - .03f, item.Z);
                if (item.Carry == Carry.Pair) go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = item.Carry == Carry.Light ? 1f : item.Carry == Carry.Heavy ? 14f : 20f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // a label, so the thing is readable in torchlight from a few metres
                var tag = new GameObject("Label", typeof(TextMesh)).GetComponent<TextMesh>();
                tag.transform.SetParent(go.transform, false);
                tag.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z) * .012f;
                tag.transform.localPosition = new Vector3(0f, .5f + .35f / size.y, 0f);
                tag.text = item.Name; tag.characterSize = 1f; tag.fontSize = 48; tag.anchor = TextAnchor.LowerCenter;
                tag.color = new Color(.9f, .9f, .85f, .85f);
                props[item] = go.transform;
            }
        }

        // ── the player's verbs ──

        /// <summary>F, as the kit does it — unless both hands are under a stove.</summary>
        public void ToggleTorch()
        {
            if (!Active || boot.Gear == null) return;
            var held = Held;
            if (held != null && !Errand.MayTorch(held.Carry)) { SandboxHud.Say("руки заняты: " + held.Name + " несут двумя руками"); return; }
            boot.Gear.ToggleTorch();
        }

        public void ToggleCrouch() { if (!Active) return; Crouch = !Crouch; Prone = false; }

        public void ToggleProne()
        {
            if (!Active) return;
            var held = Held;
            if (!Prone && held != null && !Errand.MayLie(held.Carry)) { SandboxHud.Say("с " + held.Name + " в руках не лечь"); return; }
            Prone = !Prone; Crouch = false;
        }

        /// <summary>E: take what lies within reach, or put down what is held. Put down inside the fire's radius,
        /// the thing is home and gone.</summary>
        public void TakeOrPutDown()
        {
            if (!Active || Dead) return;
            var body = boot.Body;
            var feet = body.Torso.position + Vector3.down * boot.Tuning.HoverHeight;
            if (Held != null)
            {
                var item = Held;
                var down = feet + Vector3.ProjectOnPlane(body.Facing * Vector3.forward, Vector3.up).normalized * .7f;
                bool home = Run.PutDown(Me, down.x, down.z);
                Place(item, down, home);
                if (home) SandboxHud.Say("у костра: " + item.Name + $" ({Run.Errand.Delivered} из {Run.Errand.Total})");
                if (Prone && !Errand.MayLie(item.Carry)) Prone = false;
                return;
            }
            ErrandItem nearest = null; float nearD = 2.2f;
            foreach (var i in Run.Errand.Items)
            {
                if (!i.OnSnow) continue;
                float d = HuntRules.Dist(i.X, i.Z, feet.x, feet.z);
                if (d < nearD) { nearD = d; nearest = i; }
            }
            if (nearest == null) { SandboxHud.Say("здесь ничего нет"); return; }
            string why = Run.Take(Me, nearest);
            if (why != null) { SandboxHud.Say(why + (nearest.Carry == Carry.Pair ? " — свёрнутую палатку несут вдвоём" : "")); return; }
            if (!Errand.MayTorch(nearest.Carry) && Torch) boot.Gear.ToggleTorch();
            if (!Errand.MayLie(nearest.Carry)) Prone = false;
            Hold(nearest);
            SandboxHud.Say("в руках: " + nearest.Name + (nearest.Carry == Carry.Heavy ? " — шагом, без фонаря" : ""));
        }

        /// <summary>X: thrown a few metres ahead; the snow hears it from thirty.</summary>
        public void Throw()
        {
            if (!Active || Dead || Held == null) return;
            var item = Held;
            var cam = boot.Cam.transform;
            var from = boot.Body.Torso.position + Vector3.up * .3f + cam.forward * .6f;
            var land = from + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 4f;
            Run.Throw(Me, land.x, land.z);
            if (props.TryGetValue(item, out var p) && p != null)
            {
                p.SetParent(transform, true);
                p.position = from;
                var rb = p.GetComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.linearVelocity = cam.forward * 6.5f + Vector3.up * 2f;
            }
        }

        void Hold(ErrandItem item)
        {
            if (!props.TryGetValue(item, out var p) || p == null) return;
            var rb = p.GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        void Place(ErrandItem item, Vector3 at, bool home)
        {
            if (!props.TryGetValue(item, out var p) || p == null) return;
            if (home) { Destroy(p.gameObject); props.Remove(item); return; }
            p.SetParent(transform, true);
            p.position = at + Vector3.up * (p.localScale.y * .5f + SandboxHuntYard.SnowTop);
            p.rotation = item.Carry == Carry.Pair ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            var rb = p.GetComponent<Rigidbody>();
            rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        }

        // ── every frame ──

        void Update()
        {
            if (Run == null || boot.Body == null) return;
            var body = boot.Body;
            float dt = Time.deltaTime;
            Night.Storm = Run.Storm;
            // a stove in both hands leaves none for the switch: F while carrying it is refused above, and a torch
            // lit before the stove was picked up goes out here
            var carrying = Held;
            if (carrying != null && !Errand.MayTorch(carrying.Carry) && Torch) boot.Gear.ToggleTorch();

            if (Run.Over)
            {
                if (Report == null) Report = Run.Report();
                menkView?.Show(Run.Menk, dt);
                CarryHeld();
                return;
            }

            // what the body is doing, as the Menk would read it
            var pos = body.Torso.position;
            var v = body.Torso.linearVelocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            running = speed > 4.2f;
            walking = speed > .5f && body.Grounded;
            var eye = MenkEye();
            Covered = LineBlocked(eye, pos + Vector3.up * .15f, body);
            var me = Run.Find(Me);
            seen.Clear();
            seen.Add(new HuntSeen
            {
                Name = Me, X = pos.x, Z = pos.z, LookYaw = body.FacingYaw,
                Torch = Torch, Running = running, Walking = walking, Low = body.Low, Covered = Covered, Alive = me != null && me.Alive && !Dead,
            });
            Run.Tick(dt, seen);
            menkView?.Show(Run.Menk, dt);

            // marks in the snow: one every stride and a half
            if (walking)
            {
                trackMeter += Vector3.ProjectOnPlane(pos - lastPos, Vector3.up).magnitude;
                if (trackMeter >= 1.5f) { trackMeter = 0f; Run.Tracks.Leave(0, pos.x, pos.z, Run.Elapsed); }
            }
            lastPos = pos;

            if (me != null && !me.Alive && !Dead) Die();
            CarryHeld();
        }

        void Die()
        {
            Dead = true; Crouch = Prone = false;
            var body = boot.Body;
            var feet = body.Torso.position + Vector3.down * boot.Tuning.HoverHeight;
            var item = Run.DropOnDeath(Me, feet.x, feet.z);
            if (item != null) Place(item, feet + body.Facing * Vector3.forward * .5f, false);
            var me = Run.Find(Me);
            bool cold = me != null && me.Cause != null && me.Cause.Contains("замёрз");
            boot.Die(cold ? "ЗАМЁРЗ В ПУРГЕ" : "МЕНК ДОГНАЛ",
                     cold ? "Стена пурги пришла, а до костра было далеко. Тело легло в снег." : "Один удар. Тело легло в снег там, где стояло; что было в руках, лежит рядом.");
        }

        /// <summary>The Menk's eyes: high over where it stands.</summary>
        Vector3 MenkEye()
        {
            var b = Run.Menk;
            float ground = menkView != null && menkView.Model != null ? menkView.Model.position.y : 0f;
            return new Vector3(b.X, ground + MenkPuppet.Height * .85f, b.Z);
        }

        /// <summary>Whether something of the yard stands between two points — the body's own capsule and hands do
        /// not count, and neither does the creature (its model carries no colliders).</summary>
        static bool LineBlocked(Vector3 from, Vector3 to, Puppet.Puppet body)
        {
            if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.attachedRigidbody == body.Torso || body.IsOwnHand(hit.collider)) return false;
            return true;
        }

        /// <summary>What is held rides in front of the eye, low and to the right — or at the chest, seen from outside.</summary>
        void CarryHeld()
        {
            var item = Held;
            if (item == null || !props.TryGetValue(item, out var p) || p == null) return;
            var cam = boot.Cam.transform;
            var body = boot.Body;
            bool heavy = item.Carry != Carry.Light;
            if (boot.FirstPerson)
                p.SetPositionAndRotation(cam.position + cam.forward * (heavy ? .55f : .45f) + cam.right * (heavy ? 0f : .28f) - cam.up * (heavy ? .35f : .3f),
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.forward, Vector3.up), Vector3.up));
            else
                p.SetPositionAndRotation(body.Torso.position + body.Facing * new Vector3(heavy ? 0f : .25f, heavy ? -.1f : .05f, .4f), body.Facing);
        }

        // ── for the panel ──
        public string Status()
        {
            if (Run == null) return "";
            var held = Held;
            string s = "ночь · " + (held != null ? "в руках: " + held.Name : "руки свободны") + (Torch ? " · фонарь" : "")
                     + (boot.Body != null && boot.Body.Prone ? " · лёжа" : boot.Body != null && boot.Body.Low ? " · присев" : "")
                     + (Covered ? " · за укрытием" : "");
            s += $"\nпринесли {Run.Errand.Delivered} из {Run.Errand.Total}";
            if (Run.Wall) s += " · ПУРГА: к костру!";
            else if (Run.Storm > .05f) s += " · ветер крепчает";
            return s;
        }
    }
}
