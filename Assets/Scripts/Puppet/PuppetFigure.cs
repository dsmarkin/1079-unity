using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The figure you actually see, posed every frame from what the physics body is doing. The physics is one
    /// capsule floating on a leg spring; this layer is what makes it read as a person walking.
    ///
    /// Proportions and shapes follow the study of PEAK (docs/PHYSICS.md): a head about a quarter of the height,
    /// no neck at all, round tapering limbs with no elbow or knee marked, big hands and boots, flat saturated colour
    /// and no gloss. Boxes are deliberately not used — hard edges are what made the first version read as a crate on
    /// sticks, and they also tear an outline pass later on.
    ///
    /// The legs are the point of it: the feet are planted in turn, the knee is solved with two bones, and the stride
    /// comes from distance travelled rather than a timer — so walking, climbing a grade and slipping all look
    /// different without a single animation clip.</summary>
    public sealed class PuppetFigure : MonoBehaviour
    {
        Puppet owner;
        Transform root, hips, chest, head, hat, pack;
        readonly Transform[] armUp = new Transform[2], armLow = new Transform[2], hand = new Transform[2];
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], boot = new Transform[2];
        readonly Transform[] elbow = new Transform[2], knee = new Transform[2];

        /// <summary>Bone lengths, metres. Legs 0.78 + boot 0.13 + torso 0.48 + head 0.39 ≈ 1.78 m — a man of the
        /// right height whose head is far too big, which is exactly the trick.</summary>
        const float ThighLen = .40f, ShinLen = .38f, UpperArm = .30f, Forearm = .28f;
        const float HipDrop = .14f;    // pelvis below the floating capsule's centre

        float stride;
        readonly Vector3[] plant = new Vector3[2];
        readonly bool[] planted = new bool[2];

        static Material skin, coat, trouser, boots, packMat;

        public void Bind(Puppet p)
        {
            owner = p;
            Palette();
            root = new GameObject("Figure").transform;
            root.SetParent(p.transform, false);

            hips = Ball(root, "Hips", new Vector3(.34f, .26f, .28f), coat);
            chest = Pill(root, "Chest", new Vector3(.46f, .48f, .34f), coat);
            head = Ball(root, "Head", new Vector3(.42f, .39f, .40f), skin);
            hat = Ball(root, "Hat", new Vector3(.44f, .17f, .42f), trouser);
            pack = Pill(root, "Pack", new Vector3(.36f, .42f, .24f), packMat);

            for (int s = 0; s < 2; s++)
            {
                armUp[s] = Pill(root, "ArmUp" + s, new Vector3(.15f, UpperArm, .15f), coat);
                armLow[s] = Pill(root, "ArmLow" + s, new Vector3(.13f, Forearm, .13f), coat);
                elbow[s] = Ball(root, "Elbow" + s, Vector3.one * .14f, coat);
                hand[s] = Ball(root, "Hand" + s, new Vector3(.16f, .17f, .08f), skin);
                thigh[s] = Pill(root, "Thigh" + s, new Vector3(.19f, ThighLen, .19f), trouser);
                shin[s] = Pill(root, "Shin" + s, new Vector3(.16f, ShinLen, .16f), trouser);
                knee[s] = Ball(root, "Knee" + s, Vector3.one * .18f, trouser);
                boot[s] = Ball(root, "Boot" + s, new Vector3(.17f, .13f, .28f), boots);
            }
        }

        /// <summary>Flat, saturated, matte. A climber has to be findable against snow, rock and sky, and gloss is what
        /// made the first version look like plastic.</summary>
        static void Palette()
        {
            if (coat != null) return;
            skin = Mat(new Color(.93f, .74f, .56f));
            coat = Mat(new Color(.90f, .33f, .16f));
            trouser = Mat(new Color(.20f, .25f, .34f));
            boots = Mat(new Color(.13f, .12f, .12f));
            packMat = Mat(new Color(.45f, .50f, .28f));
        }

        static Material Mat(Color c)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", 0f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>A capsule: round in section, rounded at both ends, so two of them meeting look like a limb and not
        /// like two sticks. Unity's capsule is two units long, hence the halving in <see cref="Bone"/>.</summary>
        static Transform Pill(Transform parent, string name, Vector3 size, Material m)
            => Prim(PrimitiveType.Capsule, parent, name, new Vector3(size.x, size.y * .5f, size.z), m);

        static Transform Ball(Transform parent, string name, Vector3 size, Material m)
            => Prim(PrimitiveType.Sphere, parent, name, size, m);

        static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            PuppetRig.Kill(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            return go.transform;
        }

        void LateUpdate()
        {
            if (owner == null || owner.Torso == null) return;
            var t = owner.Tuning;
            var body = owner.Torso.transform;
            var v = owner.Torso.linearVelocity;
            var flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;

            float ride = float.IsInfinity(owner.GroundDistance) ? t.HoverHeight : owner.GroundDistance;
            var hipPoint = body.position + Vector3.down * HipDrop;
            var face = speed > .2f
                ? Quaternion.LookRotation(flat / speed, Vector3.up)
                : Quaternion.LookRotation(Vector3.ProjectOnPlane(body.forward, Vector3.up), Vector3.up);
            float lean = owner.Limp ? 0f : Mathf.Clamp(speed * 2.2f, 0f, 14f);
            if (owner.Sliding) lean = -22f;
            if (owner.Limp) { face = body.rotation; lean = 0f; }
            var pose = face * Quaternion.Euler(lean, 0f, 0f);

            hips.SetPositionAndRotation(hipPoint, pose);
            chest.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, .26f, .01f), pose);
            pack.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, .28f, -.24f), pose);
            var headRot = face * Quaternion.Euler(lean * .3f, 0f, 0f);
            // no neck: the head sits straight on the shoulders, the way it does in PEAK
            head.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, .68f, .01f), headRot);
            hat.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, .83f, .01f), headRot);

            // ── legs ───────────────────────────────────────────────────────────────────────────────────────────────
            stride += speed * Time.deltaTime;
            const float step = .78f;                       // metres per full cycle
            float phase = stride / step * Mathf.PI * 2f;
            bool walking = speed > .25f && owner.Grounded && !owner.Limp;
            float swing = Mathf.Clamp01(speed / Mathf.Max(t.RunSpeed, .1f));

            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var hipSide = hipPoint + pose * new Vector3(sign * .12f, -.02f, 0f);
                float stand = ride - HipDrop - .06f;       // from the pelvis down to the sole
                Vector3 foot;
                if (walking)
                {
                    float p = phase + (s == 0 ? 0f : Mathf.PI);
                    float ahead = Mathf.Sin(p) * Mathf.Lerp(.22f, .46f, swing);
                    float lift = Mathf.Max(0f, Mathf.Cos(p)) * Mathf.Lerp(.08f, .24f, swing);
                    foot = hipSide + pose * new Vector3(0f, 0f, ahead) + Vector3.down * (stand - lift);
                    planted[s] = false;
                }
                else if (owner.Grounded && !owner.Limp)
                {
                    if (!planted[s]) { plant[s] = hipSide + Vector3.down * stand; planted[s] = true; }
                    foot = plant[s];
                }
                else
                {
                    foot = hipSide + pose * new Vector3(0f, 0f, sign * .03f) + Vector3.down * (ThighLen + ShinLen);
                    planted[s] = false;
                }
                var kneeAt = Limb(thigh[s], shin[s], hipSide, foot, ThighLen, ShinLen, pose * Vector3.forward);
                knee[s].position = kneeAt;
                boot[s].position = foot + Vector3.up * .06f + pose * new Vector3(0f, 0f, .04f);
                boot[s].rotation = pose;
            }

            // ── arms ───────────────────────────────────────────────────────────────────────────────────────────────
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var shoulder = hipPoint + pose * new Vector3(sign * .28f, .44f, .01f);
                var physical = s == 0 ? owner.Left : owner.Right;
                Vector3 wrist;
                if (owner.HandsEnabled && physical != null) wrist = physical.transform.position;
                else
                {
                    float p = phase + (s == 0 ? Mathf.PI : 0f);
                    float ahead = walking ? Mathf.Sin(p) * Mathf.Lerp(.12f, .30f, swing) : 0f;
                    wrist = shoulder + pose * new Vector3(sign * .09f, -(UpperArm + Forearm) * .90f, ahead);
                }
                var elbowAt = Limb(armUp[s], armLow[s], shoulder, wrist, UpperArm, Forearm, pose * Vector3.back);
                elbow[s].position = elbowAt;
                hand[s].position = wrist;
                hand[s].rotation = pose;
                if (physical != null)
                {
                    var view = physical.transform.Find("View");
                    if (view != null && view.gameObject.activeSelf != owner.HandsEnabled) view.gameObject.SetActive(owner.HandsEnabled);
                }
            }
        }

        /// <summary>Two bones between a joint and an end point, bent the right way; returns where the joint between
        /// them ended up, so a ball can be dropped there and hide the seam. The law of cosines, nothing more.</summary>
        static Vector3 Limb(Transform upper, Transform lower, Vector3 from, Vector3 to, float a, float b, Vector3 bendToward)
        {
            var delta = to - from;
            float d = Mathf.Clamp(delta.magnitude, .01f, (a + b) * .999f);
            var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.down;
            float along = (a * a - b * b + d * d) / (2f * d);
            float out_ = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var side = Vector3.ProjectOnPlane(bendToward, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, dir);
            var joint = from + dir * along + side.normalized * out_;
            Bone(upper, from, joint);
            Bone(lower, joint, to);
            return joint;
        }

        static void Bone(Transform bone, Vector3 from, Vector3 to)
        {
            var mid = (from + to) * .5f;
            var d = to - from;
            float len = d.magnitude;
            bone.position = mid;
            bone.rotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            var s = bone.localScale;
            bone.localScale = new Vector3(s.x, Mathf.Max(len * .5f, .02f), s.z);   // Unity's capsule is 2 units long
        }
    }
}
