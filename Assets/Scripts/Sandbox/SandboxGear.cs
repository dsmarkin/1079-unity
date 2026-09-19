using UnityEngine;
using Height1079.Core;
using Height1079.Torchlight;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>What the body carries on the range, the PEAK way: three slots at hand and the rucksack on the back
    /// (<see cref="Kit"/>, with the game's own <see cref="Backpack"/> and items inside it), and the one thing in the
    /// kit that changes the picture — the tube flashlight. The model is the game's prefab (ItemsFactory writes it
    /// into Resources) and the beam is the game's <see cref="TorchBeam"/>, so what is in the hand here is the very
    /// flashlight the night is played with, with the same twelve minutes in its cells.
    ///
    /// The day starts with the flashlight in slot 1, a bar of chocolate in slot 2 and the night's starter kit in
    /// the rucksack. 1, 2, 3 put a slot in the hands; F lights the torch (and takes it into the hands if it is
    /// anywhere in the kit); E eats a chocolate wherever it is; a click uses what is held; Tab is the rucksack.</summary>
    // after the boot's LateUpdate, which places the eye and hangs the hands off it: the torch is put in a hand as
    // drawn this frame, and put there a frame late it shivers against the fingers
    [DefaultExecutionOrder(200)]
    public sealed class SandboxGear : MonoBehaviour
    {
        public Kit Kit { get; private set; } = new Kit();
        /// <summary>The switch. Stays where it was put while the torch is stowed, as the real switch does.</summary>
        public bool TorchOn { get; private set; }
        public float Battery { get; private set; } = 1f;
        public bool TorchInHand => Kit.InHand.Id == ItemId.Flashlight;

        Transform torch, beamOrigin;
        Material lens;
        TorchBeam beam;
        Puppet.Puppet seen;
        PuppetFigure figure;

        void Awake()
        {
            Refill();
            torch = BuildTorch(transform, out beamOrigin, out lens);
            torch.gameObject.SetActive(false);
            beam = TorchBeam.Create("TorchBeam");
            beam.Attach(beamOrigin);
        }

        void OnDestroy()
        {
            beam?.Destroy();
            if (lens != null) Destroy(lens);
        }

        /// <summary>A new day's kit.</summary>
        public void Refill()
        {
            Kit = new Kit();
            Kit.Give(0, new ItemStack(ItemId.Flashlight));
            Kit.Give(1, new ItemStack(ItemId.Chocolate));
            foreach (var s in Items.StarterFor(0)) Kit.Pack.Put(s);
            Kit.Pack.Put(ItemId.Chocolate);
            Kit.Pack.Put(ItemId.Chocolate);
            TorchOn = false; Battery = 1f;
        }

        /// <summary>1, 2, 3: the slot goes into the hands; pressed again it is put away.</summary>
        public void Select(int slot)
        {
            Kit.Select(slot);
            var held = Kit.InHand;
            SandboxHud.Say(held.IsEmpty ? (Kit.Slot(slot).IsEmpty ? "слот " + (slot + 1) + " пуст · Tab — взять из рюкзака" : "убрано")
                                        : "в руках: " + held.Describe() + Hint(held.Id));
        }

        public void PutAway() { Kit.PutAway(); }

        static string Hint(ItemId id)
        {
            switch (id)
            {
                case ItemId.Flashlight: return " · F или клик — свет";
                case ItemId.Chocolate: return " · E или клик — съесть";
                default: return "";
            }
        }

        /// <summary>A click: use what is in the hands.</summary>
        public void Use()
        {
            var item = Kit.InHand;
            switch (item.Id)
            {
                case ItemId.Flashlight: ToggleTorch(); break;
                case ItemId.Chocolate: EatFrom(Kit.Selected); break;
                case ItemId.None: SandboxHud.Say("руки пусты · 1, 2, 3 — слоты · Tab — рюкзак"); break;
                default: SandboxHud.Say(item.Describe() + " — здесь ни к чему"); break;
            }
        }

        /// <summary>F: the light. The flashlight comes into the hands from wherever it is — a slot, or the rucksack
        /// if a slot is free for it — the way the game's F takes the torch in hand before switching it.</summary>
        public void ToggleTorch()
        {
            int slot = Kit.SlotOf(ItemId.Flashlight);
            if (slot == Kit.Nothing)
            {
                int i = Kit.Pack.IndexOf(ItemId.Flashlight);
                if (i >= 0 && Kit.Draw(i)) slot = Kit.SlotOf(ItemId.Flashlight);
            }
            if (slot == Kit.Nothing)
            {
                SandboxHud.Say(Kit.Pack.IndexOf(ItemId.Flashlight) >= 0 ? "фонарик в рюкзаке, а слоты заняты: освободи слот (Tab)" : "фонарика нет ни в руках, ни в рюкзаке");
                return;
            }
            if (Kit.Selected != slot) Kit.Select(slot);
            TorchOn = !TorchOn;
            SandboxHud.Say(!TorchOn ? "фонарик выключен (F)" : Battery > .02f ? "фонарик включён (F)" : "фонарик включён, но батарея села");
        }

        /// <summary>E: a bar of chocolate from wherever it is — the hands, a slot, the bottom of the rucksack.</summary>
        public void Eat()
        {
            int slot = Kit.SlotOf(ItemId.Chocolate);
            if (slot != Kit.Nothing) { EatFrom(slot); return; }
            int i = Kit.Pack.IndexOf(ItemId.Chocolate);
            if (i >= 0) { Kit.Pack.TakeAt(i); Eaten(); return; }
            SandboxHud.Say("шоколада не осталось");
        }

        void EatFrom(int slot)
        {
            if (Kit.Consume(slot).IsEmpty) return;
            Eaten();
        }

        static void Eaten() => SandboxBoot.Instance?.Vitals?.Eat();

        void LateUpdate()
        {
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null || torch == null) return;
            var body = boot.Body;
            if (seen != body) { seen = body; figure = body.GetComponent<PuppetFigure>(); }

            // a dead body's hands are in the snow: whatever they held goes out of the picture with them
            bool show = TorchInHand && !body.Dead;
            if (torch.gameObject.activeSelf != show) torch.gameObject.SetActive(show);
            bool first = boot.FirstPerson;
            if (show)
            {
                // Held the way the game holds it: along the look from inside the head, and where the body faces,
                // tipped a little down, from outside — in the fist that is drawn, whichever of the two the eye is in.
                // From inside the head the tube is turned in and down a little, so its side shows and not just a
                // lens seen end-on, and it is pushed out ahead of the fingers: held on its middle the whole tube
                // sat inside the sculpted hand and the picture showed nothing. From outside the fist closes near
                // the tail and the head stands clear of the fingertips.
                var rot = first && boot.Eye != null ? boot.Eye.LookRotation * Quaternion.Euler(8f, -12f, 0f)
                                                    : body.Facing * Quaternion.Euler(12f, 0f, 0f);
                Vector3 at;
                if (figure != null && figure.Hand(1, out var hand, out _)) at = hand;
                else at = boot.Cam.transform.TransformPoint(new Vector3(.2f, -.2f, .42f));
                torch.SetPositionAndRotation(at + rot * Vector3.forward * (first ? .16f : .09f), rot);
            }

            bool on = show && TorchOn;
            if (on) Battery = Mathf.Max(0f, Battery - Time.deltaTime / TorchBeam.BatterySeconds);
            beam.Set(on, Battery, false);
            if (lens != null) lens.SetColor("_EmissionColor", beam.LensEmission);
            // the beam goes where the player looks (a hand keeps the light on what the eyes watch)
            if (beam.Lit && beamOrigin != null)
                beam.Transform.rotation = first ? boot.Cam.transform.rotation * Quaternion.Euler(4f, 0f, 0f) : beamOrigin.rotation;
        }

        /// <summary>The game's tube flashlight out of Resources. A project that has not generated its world yet has
        /// no prefab there, and the sandbox must still come up: then the same tube is turned out of primitives —
        /// nickel body, wider head, a lens — so the light is in the hand either way.</summary>
        static Transform BuildTorch(Transform parent, out Transform origin, out Material lens)
        {
            var prefab = Resources.Load<GameObject>("World/Prefabs/Items/Flashlight");
            if (prefab != null)
            {
                var t = Instantiate(prefab, parent).transform;
                t.name = "Flashlight";
                foreach (var c in t.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                var lensT = t.Find("Lens");
                origin = t.Find("Lens/BeamOrigin") ?? t;
                var r = lensT != null ? lensT.GetComponent<MeshRenderer>() : null;
                lens = r != null ? r.material : null;
                return t;
            }
            var root = new GameObject("Flashlight").transform;
            root.SetParent(parent, false);
            var nickel = new Material(Shader.Find("Standard")) { color = new Color(.72f, .72f, .7f) };
            nickel.SetFloat("_Metallic", .85f); nickel.SetFloat("_Glossiness", .78f);
            Cylinder(root, "Body", new Vector3(0f, 0f, -.02f), .0165f, .16f, nickel);
            Cylinder(root, "Head", new Vector3(0f, 0f, .085f), .027f, .04f, nickel);
            lens = new Material(Shader.Find("Standard")) { color = new Color(.95f, .93f, .85f) };
            lens.EnableKeyword("_EMISSION");
            Cylinder(root, "Lens", new Vector3(0f, 0f, .102f), .024f, .004f, lens);
            origin = new GameObject("BeamOrigin").transform;
            origin.SetParent(root, false);
            origin.localPosition = new Vector3(0f, 0f, .11f);
            return root;
        }

        /// <summary>A cylinder along +Z, centred at <paramref name="at"/>.</summary>
        static void Cylinder(Transform parent, string name, Vector3 at, float radius, float length, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(radius * 2f, length * .5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }
}
