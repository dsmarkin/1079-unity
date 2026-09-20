using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>A skinned model from a file, worn over the figure's pose: the puppet can wear any skinned model with
    /// a skeleton (tested on hiker-v1.glb) while the physics and the figure's own solve stay exactly what they are,
    /// and this is the seam between the two. <see cref="PuppetFigure"/> keeps solving its pose every frame — feet planted on the
    /// world, knees and elbows from its own two-bone IK, the torso's lean read off the capsule — and this class turns
    /// the bones of an instantiated model to match it. Classic FK aiming: each bone is rotated so that its own
    /// "along" axis points down the segment the figure solved, with the roll pinned by a twist hint (the way the
    /// knee or the elbow bends), and a correction recorded once at bind time takes care of however the model's
    /// author happened to orient the bone in its rest pose. Only the hips (and the root under them) are moved; every
    /// other bone keeps its local position, so the model's own limb lengths survive and nothing stretches. The one
    /// liberty taken is a vertical shift of the whole model so that its soles land where the figure's soles land —
    /// the ankle sits at a different height on every model, and a man whose boots hover or sink is the first thing
    /// anybody sees.
    ///
    /// Bones are found by name, tolerantly: case, separators and rig prefixes ("mixamorig:", "DEF-", "Bip01") are
    /// ignored and each segment has a list of the names it goes by in the rigs we are likely to meet — hiker-v1's
    /// own, Mixamo's, and the Blender / Quaternius / KayKit family (<see cref="Classify"/>). Hips and both legs are
    /// required; everything else is optional and skipped with one warning that lists what was not found. The
    /// model's rest height (lowest point to crown) is scaled to <see cref="PuppetTuning.StandHeight"/>.</summary>
    public sealed class PuppetSkeleton
    {
        enum Bone { Hips, Head, UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR, ThighL, ShinL, FootL, ThighR, ShinR, FootR, Count }

        /// <summary>What a bone name turned out to mean. <see cref="Seg.Leg"/> is the ambiguous one: Mixamo's
        /// "LeftLeg" is the shin (its thigh is "LeftUpLeg"), a six-bone rig's "leg-left" is the whole leg — which of
        /// the two it is can only be said once every name has been read.</summary>
        internal enum Seg { None, Hips, Torso, Head, UpperArm, Forearm, Hand, Thigh, Shin, Foot, Leg }

        readonly Transform[] bone = new Transform[(int)Bone.Count];
        /// <summary>Per bone, the rotation that turns the frame this class aims by into the bone's own rest rotation:
        /// <c>Inverse(Aim(restDir, restHint)) * restRotation</c>, so that <c>Aim(dir, hint) * fix</c> is a bone lying
        /// along <c>dir</c> with the same roll relative to the segment it had in the rest pose.</summary>
        readonly Quaternion[] fix = new Quaternion[(int)Bone.Count];
        /// <summary>Spine, chest, neck — however many the rig has. They are one rigid block with the hips, exactly as
        /// the figure's torso is one mesh.</summary>
        readonly List<Transform> torso = new List<Transform>();
        readonly List<Quaternion> torsoFix = new List<Quaternion>();
        Renderer[] renderers = System.Array.Empty<Renderer>();
        Transform model;
        Quaternion rootFix = Quaternion.identity;
        /// <summary>The hips' position in the model's rest frame, unscaled: where the root has to be put for the hips
        /// to land on the pelvis.</summary>
        Vector3 hipsRest;
        /// <summary>Rest height of the model, metres of its own units, and how high the ankle joint sits above its
        /// sole — averaged over the two feet.</summary>
        float restHeight = 1f, ankleUp;
        float scale = 1f, standHeight;
        /// <summary>The direction the hand was last aimed along, per side, for <see cref="HandPoint"/>.</summary>
        readonly Vector3[] handDir = { Vector3.down, Vector3.down };
        bool visible = true;

        /// <summary>How far from the wrist the middle of the palm is, in the model's own units, for a model about two
        /// metres tall. Where a thing carried in the hand is hung.</summary>
        const float PalmReach = .09f;

        /// <summary>The model's root, parented under the figure once bound.</summary>
        public Transform Root => model;
        /// <summary>Uniform scale the model wears.</summary>
        public float Scale => scale;
        /// <summary>One line on what was found, for a log or a panel.</summary>
        public string Report { get; private set; } = "";

        // ─── binding ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Maps the model's bones, measures it, scales it and parents it under <paramref name="under"/>.
        /// False — with the reason in the log and the model left unparented at the origin — when the hips or a leg
        /// cannot be found; the caller falls back to the sculpted figure.</summary>
        public bool Bind(Transform modelRoot, Transform under, float standHeight)
        {
            if (modelRoot == null) return false;
            model = modelRoot;
            // a clean frame to measure and record the rest pose in: the model at the origin, upright, unscaled.
            // Everything below is then "world" and "model" at once, and the record stays valid however the root is
            // placed afterwards because bones are given world rotations, never local ones.
            model.SetParent(null, false);
            model.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.localScale = Vector3.one;

            // The candidates are the bones some skinned mesh actually hangs on; a mesh node called "Head" must not
            // be taken for one. A rig with no skinned mesh at all offers every transform it has.
            var candidates = new List<Transform>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.bones != null) foreach (var b in smr.bones) if (b != null && !candidates.Contains(b)) candidates.Add(b);
            if (candidates.Count == 0)
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t != model) candidates.Add(t);

            System.Array.Clear(bone, 0, bone.Length);
            torso.Clear(); torsoFix.Clear();
            var leg = new Transform[2];
            var seen = new StringBuilder();
            foreach (var t in candidates)
            {
                if (seen.Length > 0) seen.Append(", ");
                seen.Append(t.name);
                if (!Classify(Normalize(t.name), out var seg, out int side)) continue;
                switch (seg)
                {
                    case Seg.Hips: Take(Bone.Hips, t); break;
                    case Seg.Head: Take(Bone.Head, t); break;
                    case Seg.Torso: torso.Add(t); break;
                    case Seg.UpperArm: Take(side == 0 ? Bone.UpperArmL : Bone.UpperArmR, t); break;
                    case Seg.Forearm: Take(side == 0 ? Bone.ForearmL : Bone.ForearmR, t); break;
                    case Seg.Hand: Take(side == 0 ? Bone.HandL : Bone.HandR, t); break;
                    case Seg.Thigh: Take(side == 0 ? Bone.ThighL : Bone.ThighR, t); break;
                    case Seg.Shin: Take(side == 0 ? Bone.ShinL : Bone.ShinR, t); break;
                    case Seg.Foot: Take(side == 0 ? Bone.FootL : Bone.FootR, t); break;
                    case Seg.Leg: if (leg[side] == null) leg[side] = t; break;
                }
            }
            // "leg" is the shin when the side has a thigh of its own, the thigh when it has not
            for (int s = 0; s < 2; s++)
            {
                if (leg[s] == null) continue;
                if (bone[(int)Thigh(s)] == null) bone[(int)Thigh(s)] = leg[s];
                else if (bone[(int)Shin(s)] == null) bone[(int)Shin(s)] = leg[s];
            }

            var missing = new List<string>();
            foreach (var need in new[] { Bone.Hips, Bone.ThighL, Bone.ShinL, Bone.ThighR, Bone.ShinR })
                if (bone[(int)need] == null) missing.Add(need.ToString());
            if (missing.Count > 0)
            {
                Debug.LogWarning($"PuppetSkeleton: '{model.name}' cannot be worn — no bone for {string.Join(", ", missing)}. Bones seen: {seen}");
                model = null;
                return false;
            }
            var optional = new List<string>();
            foreach (var want in new[] { Bone.Head, Bone.UpperArmL, Bone.ForearmL, Bone.HandL, Bone.UpperArmR, Bone.ForearmR, Bone.HandR, Bone.FootL, Bone.FootR })
                if (bone[(int)want] == null) optional.Add(want.ToString());
            if (torso.Count == 0) optional.Add("Spine");
            if (optional.Count > 0)
                Debug.LogWarning($"PuppetSkeleton: '{model.name}' has no bone for {string.Join(", ", optional)} — those parts stay as the file posed them.");

            // ── measure: the rest height, and the ankle over the sole ──────────────────────────────────────────────
            bool any = false;
            Bounds box = default;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                // a skinned mesh's vertices are in the space its skin was bound in; its renderer's own box is not
                // measured off them, so take the mesh's box through the renderer's frame
                var b = r is SkinnedMeshRenderer smr && smr.sharedMesh != null ? Transformed(smr.sharedMesh.bounds, smr.transform.localToWorldMatrix) : r.bounds;
                if (!any) { box = b; any = true; } else box.Encapsulate(b);
            }
            foreach (var t in bone) if (t != null) { if (!any) { box = new Bounds(t.position, Vector3.zero); any = true; } else box.Encapsulate(t.position); }
            restHeight = Mathf.Max(box.size.y, .1f);
            int feet = 0; ankleUp = 0f;
            for (int s = 0; s < 2; s++)
            {
                var foot = bone[(int)Foot(s)];
                if (foot == null) continue;
                ankleUp += foot.position.y - box.min.y; feet++;
            }
            if (feet > 0) ankleUp /= feet;

            // ── the rest frame: which way the model faces ──────────────────────────────────────────────────────────
            // Not assumed from the file format (glTF says +Z, this very export says −Z) but read off the skeleton: the
            // front is to the left of the line from the left hip to the right one. Y is up in every format we meet.
            var across = bone[(int)Bone.ThighR].position - bone[(int)Bone.ThighL].position;
            if (across.sqrMagnitude < 1e-6f && bone[(int)Bone.UpperArmL] != null && bone[(int)Bone.UpperArmR] != null)
                across = bone[(int)Bone.UpperArmR].position - bone[(int)Bone.UpperArmL].position;
            var restFwd = Vector3.Cross(across, Vector3.up);
            if (restFwd.sqrMagnitude < 1e-6f)
            {
                Debug.LogWarning($"PuppetSkeleton: '{model.name}' — left and right hips coincide, assuming the model faces +Z.");
                restFwd = Vector3.forward;
            }
            restFwd.Normalize();

            // ── the corrections, one per bone ──────────────────────────────────────────────────────────────────────
            var restTorso = Frame(Vector3.up, restFwd);
            rootFix = Quaternion.Inverse(restTorso);                 // the root's own rest rotation is the identity
            hipsRest = bone[(int)Bone.Hips].position;
            fix[(int)Bone.Hips] = Quaternion.Inverse(restTorso) * bone[(int)Bone.Hips].rotation;
            foreach (var t in torso) torsoFix.Add(Quaternion.Inverse(restTorso) * t.rotation);
            if (bone[(int)Bone.Head] != null) fix[(int)Bone.Head] = Quaternion.Inverse(restTorso) * bone[(int)Bone.Head].rotation;
            for (int s = 0; s < 2; s++)
            {
                var thigh = bone[(int)Thigh(s)]; var shin = bone[(int)Shin(s)]; var foot = bone[(int)Foot(s)];
                Vector3 hip = thigh.position, knee = shin.position;
                Vector3 ankle = foot != null ? foot.position : knee + (knee - hip);
                // the way the knee bends at rest, if the rest pose has any bend in it; a straight leg bends forward
                var hint = Vector3.ProjectOnPlane(knee - hip, ankle - hip);
                if (hint.magnitude < .01f) hint = restFwd;
                fix[(int)Thigh(s)] = Quaternion.Inverse(Frame(knee - hip, hint)) * thigh.rotation;
                fix[(int)Shin(s)] = Quaternion.Inverse(Frame(ankle - knee, hint)) * shin.rotation;
                // the foot is taken as pointing forward and flat, whatever the bone inside it does — every rest pose
                // stands on flat feet, and the correction absorbs the bone's own angle
                if (foot != null) fix[(int)Foot(s)] = Quaternion.Inverse(Frame(restFwd, Vector3.up)) * foot.rotation;

                var upper = bone[(int)UpperArm(s)]; var fore = bone[(int)Forearm(s)]; var hand = bone[(int)Hand(s)];
                if (upper == null) continue;
                Vector3 shoulder = upper.position;
                Vector3 elbow = fore != null ? fore.position : shoulder + Vector3.down * .3f;
                Vector3 wrist = hand != null ? hand.position : elbow + (elbow - shoulder);
                var bend = Vector3.ProjectOnPlane(elbow - shoulder, wrist - shoulder);
                if (bend.magnitude < .01f) bend = -restFwd;              // a straight arm bends backward at the elbow
                fix[(int)UpperArm(s)] = Quaternion.Inverse(Frame(elbow - shoulder, bend)) * upper.rotation;
                if (fore != null) fix[(int)Forearm(s)] = Quaternion.Inverse(Frame(wrist - elbow, bend)) * fore.rotation;
                if (hand != null) fix[(int)Hand(s)] = Quaternion.Inverse(Frame(HandAlong(hand, wrist - elbow), bend)) * hand.rotation;
            }

            // ── dress ──────────────────────────────────────────────────────────────────────────────────────────────
            renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                // the box a skinned renderer is culled by is relative to whichever bone it calls root, and the bones
                // are about to be moved far from where the file put them: let it measure itself
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            model.SetParent(under, false);
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
            this.standHeight = standHeight;
            scale = Mathf.Max(standHeight, .2f) / restHeight;
            model.localScale = Vector3.one * scale;
            Report = $"{model.name}: hips, {(torso.Count > 0 ? "spine ×" + torso.Count : "no spine")}, {(bone[(int)Bone.Head] != null ? "head" : "no head")}, "
                   + $"{Arms()} arms, legs, {feet} feet · rest {restHeight:0.00} → ×{scale:0.000}, ankle {ankleUp:0.00}";
            Debug.Log("PuppetSkeleton: wearing " + Report);
            return true;
        }

        string Arms()
        {
            int n = 0;
            for (int s = 0; s < 2; s++) if (bone[(int)UpperArm(s)] != null) n++;
            return n.ToString();
        }

        void Take(Bone slot, Transform t)
        {
            if (bone[(int)slot] == null) bone[(int)slot] = t;
        }

        static Bone Thigh(int s) => s == 0 ? Bone.ThighL : Bone.ThighR;
        static Bone Shin(int s) => s == 0 ? Bone.ShinL : Bone.ShinR;
        static Bone Foot(int s) => s == 0 ? Bone.FootL : Bone.FootR;
        static Bone UpperArm(int s) => s == 0 ? Bone.UpperArmL : Bone.UpperArmR;
        static Bone Forearm(int s) => s == 0 ? Bone.ForearmL : Bone.ForearmR;
        static Bone Hand(int s) => s == 0 ? Bone.HandL : Bone.HandR;

        /// <summary>The hand bone's own "along" axis in the rest pose: toward its children (fingers), when it has
        /// any, else straight on from the forearm.</summary>
        static Vector3 HandAlong(Transform hand, Vector3 forearm)
        {
            var sum = Vector3.zero;
            for (int i = 0; i < hand.childCount; i++) sum += hand.GetChild(i).position - hand.position;
            return sum.magnitude > .01f ? sum : forearm;
        }

        static Bounds Transformed(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                result.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return result;
        }

        // ─── names ──────────────────────────────────────────────────────────────────────────────────────────────────

        static readonly string[] HipsNames = { "hips", "hip", "pelvis" };
        static readonly string[] TorsoNames = { "spine", "chest", "upperchest", "lowerchest", "torso", "neck", "abdomen" };
        static readonly string[] UpperArmNames = { "upperarm", "arm", "armupper", "uparm", "bicep", "humerus" };
        static readonly string[] ForearmNames = { "forearm", "lowerarm", "armlower", "lowarm", "forarm" };
        static readonly string[] HandNames = { "hand", "wrist", "palm" };
        static readonly string[] ThighNames = { "thigh", "upleg", "upperleg", "legupper", "upperthigh", "femur", "hip" };
        static readonly string[] ShinNames = { "shin", "lowerleg", "leglower", "lowleg", "calf" };
        static readonly string[] FootNames = { "foot", "ankle" };
        /// <summary>Rig prefixes that carry no meaning: stripped when the name does not read without them.</summary>
        static readonly string[] Prefixes = { "mixamorig", "bip001", "bip01", "bip", "def", "bone", "b" };

        /// <summary>Lower case, letters and digits only, and nothing before the last ':' or '|' — "mixamorig:LeftArm"
        /// and "Armature|hips" read as "leftarm" and "hips".</summary>
        internal static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            int cut = Mathf.Max(raw.LastIndexOf(':'), raw.LastIndexOf('|'));
            if (cut >= 0) raw = raw.Substring(cut + 1);
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>What a normalized name means and which side it is on (0 left, 1 right, −1 neither). The side is
        /// a word or a single letter at either end, digits around it allowed ("UpperArm.L", "upperarm_01_l",
        /// "LeftUpLeg", "l_thigh"); the rest is looked up per segment. Names that mean nothing here — toes, clavicles,
        /// fingers, a pack — are simply not bones of this skeleton and come out false.</summary>
        internal static bool Classify(string n, out Seg seg, out int side)
        {
            seg = Seg.None; side = -1;
            if (string.IsNullOrEmpty(n)) return false;
            if (ClassifyOne(n, out seg, out side)) return true;
            var trimmed = TrimDigits(n);
            if (trimmed != n && trimmed.Length > 0 && ClassifyOne(trimmed, out seg, out side)) return true;
            foreach (var p in Prefixes)
                if (n.Length > p.Length && n.StartsWith(p) && ClassifyOne(n.Substring(p.Length), out seg, out side)) return true;
            return false;
        }

        static bool ClassifyOne(string n, out Seg seg, out int side)
        {
            seg = Seg.None; side = -1;
            if (n.StartsWith("left") && Limb(TrimDigits(n.Substring(4)), out seg)) { side = 0; return true; }
            if (n.StartsWith("right") && Limb(TrimDigits(n.Substring(5)), out seg)) { side = 1; return true; }
            if (n.EndsWith("left") && Limb(TrimDigits(n.Substring(0, n.Length - 4)), out seg)) { side = 0; return true; }
            if (n.EndsWith("right") && Limb(TrimDigits(n.Substring(0, n.Length - 5)), out seg)) { side = 1; return true; }
            char last = n[n.Length - 1], first = n[0];
            if ((last == 'l' || last == 'r') && Limb(TrimDigits(n.Substring(0, n.Length - 1)), out seg)) { side = last == 'l' ? 0 : 1; return true; }
            if ((first == 'l' || first == 'r') && Limb(TrimDigits(n.Substring(1)), out seg)) { side = first == 'l' ? 0 : 1; return true; }
            var u = TrimDigits(n);
            if (System.Array.IndexOf(HipsNames, u) >= 0) { seg = Seg.Hips; return true; }
            if (u == "head") { seg = Seg.Head; return true; }
            if (System.Array.IndexOf(TorsoNames, u) >= 0) { seg = Seg.Torso; return true; }
            return false;
        }

        static bool Limb(string core, out Seg seg)
        {
            seg = Seg.None;
            if (core.Length == 0) return false;
            if (System.Array.IndexOf(UpperArmNames, core) >= 0) seg = Seg.UpperArm;
            else if (System.Array.IndexOf(ForearmNames, core) >= 0) seg = Seg.Forearm;
            else if (System.Array.IndexOf(HandNames, core) >= 0) seg = Seg.Hand;
            else if (System.Array.IndexOf(ThighNames, core) >= 0) seg = Seg.Thigh;
            else if (System.Array.IndexOf(ShinNames, core) >= 0) seg = Seg.Shin;
            else if (System.Array.IndexOf(FootNames, core) >= 0) seg = Seg.Foot;
            else if (core == "leg") seg = Seg.Leg;
            return seg != Seg.None;
        }

        static string TrimDigits(string s)
        {
            int end = s.Length;
            while (end > 0 && char.IsDigit(s[end - 1])) end--;
            return s.Substring(0, end);
        }

        // ─── posing ─────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Puts the model into the figure's pose. Called by the figure at the end of its own solve, so the
        /// order is the figure's and never a race between two LateUpdates.</summary>
        public void Apply(in PuppetPose p, float wantHeight)
        {
            if (model == null || !p.Valid) return;
            if (!Mathf.Approximately(wantHeight, standHeight))
            {
                standHeight = wantHeight;
                scale = Mathf.Max(wantHeight, .2f) / restHeight;
                model.localScale = Vector3.one * scale;
            }
            var up = p.Body * Vector3.up;
            var torsoAim = Frame(up, p.Body * Vector3.forward);

            // the root goes where the body is, so that whatever the file hung off it and not off a bone (a mesh
            // node, a prop) comes along; the hips are then put on the pelvis outright
            model.rotation = torsoAim * rootFix;
            model.position = p.Pelvis - model.rotation * (hipsRest * scale);
            bone[(int)Bone.Hips].SetPositionAndRotation(p.Pelvis, torsoAim * fix[(int)Bone.Hips]);
            for (int i = 0; i < torso.Count; i++) torso[i].rotation = torsoAim * torsoFix[i];
            var head = bone[(int)Bone.Head];
            if (head != null) head.rotation = Frame(p.HeadRot * Vector3.up, p.HeadRot * Vector3.forward) * fix[(int)Bone.Head];

            // legs, and the shift that lands the soles where the figure's are
            float lift = 0f; int feet = 0;
            for (int s = 0; s < 2; s++)
            {
                var leg = p.Leg(s);
                var thigh = bone[(int)Thigh(s)]; var shin = bone[(int)Shin(s)]; var foot = bone[(int)Foot(s)];
                // the roll of both leg bones follows the knee: the side it sticks out on is the front of the leg
                var hint = Vector3.ProjectOnPlane(leg.Knee - leg.Hip, leg.Ankle - leg.Hip);
                if (hint.sqrMagnitude < 1e-6f) hint = leg.Bend;
                thigh.rotation = Frame(leg.Knee - leg.Hip, hint) * fix[(int)Thigh(s)];
                shin.rotation = Frame(leg.Ankle - leg.Knee, hint) * fix[(int)Shin(s)];
                if (foot == null) continue;
                foot.rotation = Frame(leg.Foot * Vector3.forward, leg.Foot * Vector3.up) * fix[(int)Foot(s)];
                // where this model's ankle has to be for its sole to sit on the figure's sole, against where the
                // aimed leg actually put it. Only the up-and-down of it is taken, and shared between the two feet
                var want = leg.Sole + leg.Foot * (Vector3.up * (ankleUp * scale));
                lift += Vector3.Dot(want - foot.position, up); feet++;
            }
            if (feet > 0) model.position += up * (lift / feet);   // the hips ride under the root and come with it

            for (int s = 0; s < 2; s++)
            {
                var upper = bone[(int)UpperArm(s)];
                if (upper == null) continue;
                var arm = p.Arm(s);
                var hint = Vector3.ProjectOnPlane(arm.Elbow - arm.Shoulder, arm.Wrist - arm.Shoulder);
                if (hint.sqrMagnitude < 1e-6f) hint = arm.Bend;
                upper.rotation = Frame(arm.Elbow - arm.Shoulder, hint) * fix[(int)UpperArm(s)];
                var fore = bone[(int)Forearm(s)];
                if (fore != null) fore.rotation = Frame(arm.Wrist - arm.Elbow, hint) * fix[(int)Forearm(s)];
                var hand = bone[(int)Hand(s)];
                if (hand == null) continue;
                var along = arm.Hand - arm.Wrist;
                if (along.sqrMagnitude < 1e-8f) along = arm.Wrist - arm.Elbow;
                hand.rotation = Frame(along, hint) * fix[(int)Hand(s)];
                handDir[s] = along.normalized;
            }
        }

        /// <summary>The middle of the model's palm, for whatever is carried in the hand. False when the model has no
        /// hand bone on that side.</summary>
        public bool HandPoint(int side, out Vector3 at)
        {
            var hand = bone[(int)Hand(Mathf.Clamp(side, 0, 1))];
            if (model == null || hand == null) { at = Vector3.zero; return false; }
            at = hand.position + handDir[Mathf.Clamp(side, 0, 1)] * (PalmReach * scale);
            return true;
        }

        /// <summary>Renderers on or off; the bones keep being posed either way, as the figure's own do.</summary>
        public void SetVisible(bool on)
        {
            if (visible == on && renderers.Length > 0 && renderers[0] != null && renderers[0].enabled == on) return;
            visible = on;
            foreach (var r in renderers) if (r != null) r.enabled = on;
        }

        /// <summary>A frame with Y along <paramref name="dir"/> and Z toward <paramref name="hint"/>, safe for any
        /// input: <see cref="PuppetFigure.Aim"/> with the zero-length cases handled.</summary>
        static Quaternion Frame(Vector3 dir, Vector3 hint)
        {
            if (dir.sqrMagnitude < 1e-10f) dir = Vector3.down;
            if (hint.sqrMagnitude < 1e-10f) hint = Vector3.forward;
            return PuppetFigure.Aim(dir.normalized, hint);
        }
    }
}
