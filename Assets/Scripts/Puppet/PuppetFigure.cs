using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The figure you actually see, posed every frame from what the physics body is doing. The physics is one
    /// capsule floating on a leg spring; this layer is what makes it read as a person walking.
    ///
    /// It is no longer built from Unity's primitives. Every part is a mesh turned in code by <see cref="PuppetMesh"/>
    /// from a hand-drawn silhouette, and all of them are painted from one texture drawn in code by
    /// <see cref="PuppetSkinTexture"/> — a face with eyes and brows, shorts, socks, sleeves and the pack's straps are
    /// picture, not geometry. <see cref="PuppetSkin"/> turns that set once and every body in the scene shares it.
    ///
    /// Proportions follow the study of PEAK (docs/PHYSICS.md): a head about a quarter of the height and shaped like a
    /// drop, no neck at all, limbs that taper and carry no marked elbow or knee, big hands and boots, flat saturated
    /// colour and no gloss. Boxes are still deliberately absent — hard edges are what made the first version read as a
    /// crate on sticks.
    ///
    /// The legs are the point of it: the feet are planted in turn, the knee is solved with two bones, and the stride
    /// comes from distance travelled rather than a timer — so walking, climbing a grade and slipping all look
    /// different without a single animation clip. Fourteen transforms, no skeleton and no skinning.</summary>
    public sealed class PuppetFigure : MonoBehaviour
    {
        Puppet owner;
        Transform root, body, head;
        readonly Transform[] armUp = new Transform[2], armLow = new Transform[2], hand = new Transform[2];
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], boot = new Transform[2];
        Renderer[] parts = System.Array.Empty<Renderer>();
        bool visible = true;

        /// <summary>Bone lengths, metres — and they must match the meshes turned in <see cref="PuppetSkin"/>, because
        /// a bone is now drawn at its built length and only stretches under protest. The legs add up to 0.86 on
        /// purpose: the default hover puts the sole 0.85 m below the pelvis, so a standing climber has a slightly bent
        /// knee and nothing is stretched. Sole to crown is about 1.73 m, of which the head is a quarter — a man of the
        /// right height whose head is far too big, which is exactly the trick.</summary>
        const float ThighLen = .44f, ShinLen = .42f, UpperArm = .30f, Forearm = .28f;
        const float HipDrop = .14f;    // pelvis below the floating capsule's centre

        float stride;
        readonly Vector3[] plant = new Vector3[2];
        readonly bool[] planted = new bool[2];

        public void Bind(Puppet p)
        {
            owner = p;
            PuppetSkin.Ensure();
            root = new GameObject("Figure").transform;
            root.SetParent(p.transform, false);

            body = Piece(PuppetSkin.Body, "Body");
            head = Piece(PuppetSkin.Head, "Head");
            for (int s = 0; s < 2; s++)
            {
                armUp[s] = Piece(PuppetSkin.UpperArm, "ArmUp" + s);
                armLow[s] = Piece(PuppetSkin.Forearm, "ArmLow" + s);
                hand[s] = Piece(s == 0 ? PuppetSkin.HandL : PuppetSkin.HandR, "Hand" + s);
                thigh[s] = Piece(PuppetSkin.Thigh, "Thigh" + s);
                shin[s] = Piece(PuppetSkin.Shin, "Shin" + s);
                boot[s] = Piece(PuppetSkin.Boot, "Boot" + s);
            }
            parts = root.GetComponentsInChildren<Renderer>(true);
            Show();     // SetVisible may have been called before the body existed
        }

        /// <summary>Show or hide the whole climber. The first person needs it: your own body must not fill the camera.
        /// Renderers are switched rather than the object, so the pose keeps being solved and nothing has to be caught
        /// up when the view changes back. Calling it with the state it already has costs nothing.</summary>
        public void SetVisible(bool on)
        {
            if (visible == on) return;
            visible = on;
            Show();
        }

        public bool Visible => visible;

        void Show()
        {
            for (int i = 0; i < parts.Length; i++) if (parts[i] != null) parts[i].enabled = visible;
        }

        Transform Piece(Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = PuppetSkin.Skin;
            return go.transform;
        }

        void LateUpdate()
        {
            if (owner == null || owner.Torso == null) return;
            var t = owner.Tuning;
            var rb = owner.Torso.transform;
            var v = owner.Torso.linearVelocity;
            var flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;

            float ride = float.IsInfinity(owner.GroundDistance) ? t.HoverHeight : owner.GroundDistance;
            var hipPoint = rb.position + Vector3.down * HipDrop;
            var face = speed > .2f
                ? Quaternion.LookRotation(flat / speed, Vector3.up)
                : Quaternion.LookRotation(Vector3.ProjectOnPlane(rb.forward, Vector3.up), Vector3.up);
            // The torso really leans: Puppet tilts the whole capsule into its own acceleration, so the figure draws
            // the physics instead of a cosmetic angle worked out from speed. Limp is the physics too — the body has
            // been thrown over and the capsule is lying down, so the pose is simply the capsule's own rotation.
            if (owner.Limp) face = rb.rotation;
            var tilt = Quaternion.FromToRotation(Vector3.up, Quaternion.Inverse(face) * (rb.rotation * Vector3.up));
            var pose = face * tilt;
            // the one thing the physics does not say: a man whose feet have gone sits back on his heels
            if (owner.Sliding && !owner.Limp) pose *= Quaternion.Euler(-12f, 0f, 0f);

            // hips, chest and pack are one mesh now: they never moved relative to each other anyway
            body.SetPositionAndRotation(hipPoint, pose);
            // no neck: the head sits straight on the shoulders, the way it does in PEAK. The hat is part of it.
            // the head keeps some of the lean and rights itself against the rest, the way a person's does
            head.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, .68f, .01f),
                face * Quaternion.Slerp(Quaternion.identity, tilt, .35f));

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
                Limb(thigh[s], shin[s], hipSide, foot, ThighLen, ShinLen, pose * Vector3.forward);
                boot[s].SetPositionAndRotation(foot + pose * new Vector3(0f, 0f, .02f), pose);
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
                var elbow = Limb(armUp[s], armLow[s], shoulder, wrist, UpperArm, Forearm, pose * Vector3.back);
                hand[s].SetPositionAndRotation(wrist, Wrist(elbow, wrist, pose));
            }
        }

        /// <summary>Two bones between a joint and an end point, bent the right way; returns where the joint between
        /// them ended up. The law of cosines, nothing more — but the reach is clamped at both ends now: pulled past
        /// full stretch the limb used to detach, and pulled into the shoulder it used to shoot out sideways.</summary>
        static Vector3 Limb(Transform upper, Transform lower, Vector3 from, Vector3 to, float a, float b, Vector3 bendToward)
        {
            var delta = to - from;
            float reach = a + b, near = Mathf.Min(Mathf.Abs(a - b) * 1.02f + .01f, reach * .9f);
            float d = Mathf.Clamp(delta.magnitude, near, reach * .999f);
            var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.down;
            float along = (a * a - b * b + d * d) / (2f * d);
            float out_ = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var side = Vector3.ProjectOnPlane(bendToward, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, dir);
            var joint = from + dir * along + side.normalized * out_;
            Bone(upper, from, joint, a);
            Bone(lower, joint, to, b);
            return joint;
        }

        /// <summary>Puts a bone mesh on the line from <paramref name="from"/> to <paramref name="to"/>. The mesh is
        /// turned at its built length and the joint IK hands over exactly that, so normally there is no scaling at all
        /// and the rounded ends keep their shape. Only the last bone of an over-reaching arm stretches, and a little
        /// squash-and-stretch there reads better than a gap at the wrist.</summary>
        static void Bone(Transform bone, Vector3 from, Vector3 to, float built)
        {
            var d = to - from;
            float len = d.magnitude;
            bone.SetPositionAndRotation(from, len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity);
            float k = built > 1e-4f ? Mathf.Clamp(len / built, .75f, 1.5f) : 1f;
            var s = bone.localScale;
            if (!Mathf.Approximately(s.y, k)) bone.localScale = new Vector3(1f, k, 1f);
        }

        /// <summary>Sits the hand on the end of the forearm — fingers on along the bone, palm still facing the way the
        /// body does. Bolted to the torso's rotation instead, a hand twists off the wrist whenever the arm swings.</summary>
        static Quaternion Wrist(Vector3 elbow, Vector3 wrist, Quaternion pose)
        {
            var along = wrist - elbow;
            if (along.sqrMagnitude < 1e-6f) return pose;
            var up = -along.normalized;
            var fwd = Vector3.ProjectOnPlane(pose * Vector3.forward, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(pose * Vector3.up, up);
            return fwd.sqrMagnitude < 1e-6f ? pose : Quaternion.LookRotation(fwd.normalized, up);
        }
    }
}
