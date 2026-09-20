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
            Run = new HuntRun(fire.x, fire.z, tent.x, tent.z, seconds, Random.Range(1, 100000), errand: Errand.Stove(fire.x, fire.z, tent.x, tent.z));
            Run.Join(Me);
            Report = null; Dead = false; Crouch = Prone = false;
            trackMeter = 0f;
            Night.Want = 1f; Night.Storm = 0f;
            menkView = SandboxHuntMenk.Create(transform);
            LayOut();
            boot.Revive(SandboxHuntYard.Spawn);
            lastPos = boot.Body.Torso.position;
            SandboxHud.Say("Ночь. В палатке впереди — печка. Принеси её к костру. F — фонарик, C — присесть, Z — лечь, R — взять/положить.");
        }

        /// <summary>Back to the day, the yard left standing.</summary>
        public void End()
        {
            Run = null; Report = null; Dead = false; Crouch = Prone = false;
            Night.Want = 0f; Night.Storm = 0f;
            if (menkView != null) Destroy(menkView.gameObject);
            menkView = null;
            // the yard's things go back where they lay; only stand-ins made here are thrown away
            foreach (var kv in props)
            {
                if (kv.Value == null) continue;
                if (SandboxHuntYard.Props.TryGetValue(kv.Key.Name, out var yardProp) && yardProp == kv.Value)
                {
                    kv.Value.gameObject.SetActive(true);
                    kv.Value.SetParent(SandboxHuntYard.Root, true);
                    kv.Value.position = SandboxHuntYard.Home[kv.Key.Name]; kv.Value.rotation = Quaternion.identity;
                }
                else Destroy(kv.Value.gameObject);
            }
            props.Clear();
        }

        /// <summary>The run clock jumps ahead — for the shots and the self-test, which cannot wait ten minutes for a front.</summary>
        public void Skip(float seconds) => Run?.Skip(seconds);

        /// <summary>The things to bring are the yard's own objects (the camp's stove, diary and Zorkiy out of the
        /// tent, the rolled tent from the cargo library): a run puts each back where it lay and tells the list where
        /// that is. A yard without them (no generated world) gets a block per thing, and says so in the log.</summary>
        void LayOut()
        {
            foreach (var item in Run.Errand.Items)
            {
                if (SandboxHuntYard.Props.TryGetValue(item.Name, out var prop) && prop != null)
                {
                    prop.gameObject.SetActive(true);
                    prop.SetParent(SandboxHuntYard.Root, true);
                    prop.position = SandboxHuntYard.Home[item.Name];
                    prop.rotation = Quaternion.identity;
                    item.X = prop.position.x; item.Z = prop.position.z;
                    props[item] = prop;
                    continue;
                }
                Debug.LogWarning("1079 sandbox: нет объекта для «" + item.Name + "» — ставлю заглушку");
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Item " + item.Name;
                go.transform.SetParent(transform, true);
                go.transform.localScale = new Vector3(.3f, .2f, .3f);
                go.transform.position = new Vector3(item.X, SandboxHuntYard.SnowTop + .1f, item.Z);
                Destroy(go.GetComponent<Collider>());
                props[item] = go.transform;
            }
        }

        float HeightOf(ErrandItem item) => SandboxHuntYard.Heights.TryGetValue(item.Name, out float h) ? h : .2f;

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
            ErrandItem nearest = null; float nearD = Reach;
            foreach (var i in Run.Errand.Items)
            {
                if (!i.OnSnow) continue;
                float d = HuntRules.Dist(i.X, i.Z, feet.x, feet.z);
                if (d < nearD) { nearD = d; nearest = i; }
            }
            if (nearest == null) { SandboxHud.Say("здесь ничего нет"); return; }
            Take(nearest);
        }

        /// <summary>An arm's reach and a step: how far a thing may lie to be picked up.</summary>
        public const float Reach = 2.6f;

        /// <summary>Take this thing, if it lies within reach (the self-test names the thing; R takes the nearest).</summary>
        public bool Take(ErrandItem nearest)
        {
            if (!Active || Dead || nearest == null) return false;
            var body = boot.Body;
            var feet = body.Torso.position + Vector3.down * boot.Tuning.HoverHeight;
            float d = HuntRules.Dist(nearest.X, nearest.Z, feet.x, feet.z);
            if (d > Reach) { SandboxHud.Say($"{nearest.Name} — не дотянуться ({d:0.0} м)"); return false; }
            string why = Run.Take(Me, nearest);
            if (why != null) { SandboxHud.Say(why + (nearest.Carry == Carry.Pair ? " — свёрнутую палатку несут вдвоём" : "")); return false; }
            if (!Errand.MayTorch(nearest.Carry) && Torch) boot.Gear.ToggleTorch();
            if (!Errand.MayLie(nearest.Carry)) Prone = false;
            Hold(nearest);
            SandboxHud.Say("в руках: " + nearest.Name + (nearest.Carry == Carry.Heavy ? " — шагом, без фонаря" : ""));
            return true;
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
            // it lands where the noise says it does: on the snow four metres ahead
            Place(item, new Vector3(land.x, 0f, land.z), false);
        }

        /// <summary>Into the kit, at hand: the thing is in a slot with its name, the way everything carried is.</summary>
        void Hold(ErrandItem item)
        {
            var kit = boot.Gear != null ? boot.Gear.Kit : null;
            if (kit == null) return;
            var id = KitId(item);
            if (id == ItemId.None) return;
            if (kit.SlotOf(id) == Kit.Nothing) kit.Give(new ItemStack(id));
            int slot = kit.SlotOf(id);
            if (slot != Kit.Nothing && kit.Selected != slot) kit.Select(slot);
        }

        void Unhold(ErrandItem item)
        {
            var kit = boot.Gear != null ? boot.Gear.Kit : null;
            if (kit == null) return;
            var id = KitId(item);
            int slot = id == ItemId.None ? Kit.Nothing : kit.SlotOf(id);
            if (slot != Kit.Nothing) kit.Consume(slot);
        }

        static ItemId KitId(ErrandItem item)
        {
            switch (item.Name)
            {
                case "печка": return ItemId.Stove;
                case "дрова": return ItemId.Firewood;
                case "сухари": return ItemId.Rusks;
                case "свечи": return ItemId.Candles;
                case "свёрнутая палатка": return ItemId.Tent;
                default: return ItemId.None;
            }
        }

        void Place(ErrandItem item, Vector3 at, bool home)
        {
            Unhold(item);
            if (!props.TryGetValue(item, out var p) || p == null) return;
            if (home) { p.gameObject.SetActive(false); props.Remove(item); return; }
            p.SetParent(SandboxHuntYard.Root, true);
            // the object's foot is at its origin (the yard wraps every thing that way): the foot goes on the snow
            p.position = new Vector3(at.x, SandboxHuntYard.SnowTop, at.z);
            p.rotation = Quaternion.Euler(0f, boot.Body != null ? boot.Body.FacingYaw : 0f, 0f);
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
                if (Report == null)
                {
                    Report = Run.Report();
                    if (!Dead) boot.ShowEnd("ПЕЧКА У КОСТРА", "Донёс. Ночь окончена: " + HuntRules.Clock(Run.Elapsed) + " от выхода до костра.");
                }
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
            // the origin is the thing's foot: carried, its middle is in front of the chest
            float lift = HeightOf(item) * .5f;
            if (boot.FirstPerson)
                p.SetPositionAndRotation(cam.position + cam.forward * (heavy ? .6f : .45f) + cam.right * (heavy ? 0f : .28f) - cam.up * ((heavy ? .35f : .3f) + lift),
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.forward, Vector3.up), Vector3.up));
            else
                p.SetPositionAndRotation(body.Torso.position + body.Facing * new Vector3(heavy ? 0f : .25f, (heavy ? -.1f : .05f) - lift, .45f), body.Facing);
        }

        /// <summary>For the F1 panel only: the state the Menk reads off the body.</summary>
        public string Status()
        {
            if (Run == null) return "";
            var held = Held;
            return (held != null ? "в руках: " + held.Name : "руки свободны") + (Torch ? " · фонарь" : "")
                 + (boot.Body != null && boot.Body.Prone ? " · лёжа" : boot.Body != null && boot.Body.Low ? " · присев" : "")
                 + (Covered ? " · за укрытием" : "") + (Run.Wall ? " · СТЕНА" : Run.Storm > .05f ? " · ветер крепчает" : "");
        }
    }
}
