using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The figure you actually see, posed every frame from what the physics body is doing. The physics is one
    /// capsule floating on a leg spring; this layer is what makes it read as a person walking.
    ///
    /// It is no longer built from Unity's primitives. Every part is a mesh turned in code by <see cref="PuppetMesh"/>
    /// from a hand-drawn silhouette, and all of them are painted from one texture drawn in code by
    /// <see cref="PuppetSkinTexture"/> — a face with eyes and brows and the pack's straps are picture, not geometry.
    /// The clothes are one skinned mesh over the bones posed here (torso, sleeves, seat and trouser legs in a single
    /// surface, bending at the joints); <see cref="PuppetSkin"/> turns that set once and every body in the scene
    /// shares it. The arms are one tube each from the shoulder to the fingertips, rebuilt every frame along the line
    /// this file solves for it (<see cref="PuppetArm"/>), because an arm made of pieces read as sausages.
    ///
    /// Proportions follow the study of PEAK (docs/PHYSICS.md): a head about a quarter of the height and shaped like a
    /// drop, no neck at all, limbs that taper and carry no marked elbow or knee, big hands and boots, flat saturated
    /// colour and no gloss. Boxes are still deliberately absent — hard edges are what made the first version read as a
    /// crate on sticks.
    ///
    /// Three things here decide how the man reads, and each of them is a whole paragraph rather than a number:
    ///
    /// <b>The stature is one number seen from two sides.</b> <see cref="PuppetTuning.StandHeight"/> says how tall the
    /// climber is, sole to crown, and <see cref="PuppetTuning.HoverHeight"/> is the physics half of it — where the
    /// capsule rides over the ground, and therefore where the pelvis has to be. Everything below is cut for the hover
    /// this file was drawn against and then scaled by whatever the tuning actually says, so moving one slider moves the
    /// whole body instead of leaving the feet through the floor. The figure lost its height in the legs: the trunk came
    /// down a little, the head not at all, and nothing at all came off the width. Short and wide is the look.
    ///
    /// <b>The stride is long and slow.</b> Cadence barely rises with speed and the stride does nearly all of the
    /// growing, which is how people walk — the old figure did the exact opposite (a fixed 0.78 m cycle, so almost four
    /// steps a second at walking pace) and scurried. A foot is put down on a point of the world and stays on it while
    /// the body rides over it, so there is a real stance phase and no skating; the pelvis dips at each footfall and
    /// rises again over the standing leg, which is what buys the leg enough reach to take a long step at all.
    ///
    /// <b>The body does not turn to face its own feet.</b> It faces where the player looks — <see cref="Puppet.Facing"/>
    /// is the capsule's own heading and the capsule is turned by the camera and nothing else (a camera outside the body
    /// may leave a standing man where he is until his next step: <see cref="PuppetInput.FreeLook"/>) — and the feet go wherever
    /// <see cref="Puppet.Drift"/> says the body is drifting in its own frame. Sideways that comes out as a side-step:
    /// the legs open and close, the boots turn out, the hips lead a little and the shoulders stay on the target.
    ///
    /// The legs are the point of all of it: the feet are planted in turn, the knee is solved with two bones, and the
    /// stride is measured in metres of ground rather than by a timer — so walking, climbing a grade and slipping all
    /// look different without a single animation clip. A handful of transforms solved in world space, two arms
    /// redrawn from scratch every frame, and the clothes skinned to those same transforms as their bones.</summary>
    public sealed class PuppetFigure : MonoBehaviour
    {
        Puppet owner;
        Transform root, body, head;
        SkinnedMeshRenderer clothes;
        /// <summary>0…1 of "in the air with the legs tucked and the arms up". See where it is updated.</summary>
        float airPose;
        readonly PuppetArm[] arm = new PuppetArm[2];
        /// <summary>The upper arm as a bone. It draws nothing and, since the sleeve became the arm's own tube
        /// (<see cref="PuppetArm"/>), nothing is skinned to it either; it is kept so the clothes' bone list stays
        /// as <see cref="PuppetSkin"/> drew it.</summary>
        readonly Transform[] armUp = new Transform[2];
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], boot = new Transform[2];
        Renderer[] parts = System.Array.Empty<Renderer>();
        bool visible = true;
        /// <summary>The first person's own hands: the figure is off, the two arms stay on and are put in front of
        /// the eye by <see cref="PoseFromEye"/> instead of at the body's sides. See there.</summary>
        bool eyeArms;
        /// <summary>What the last body-space pose worked out for the arms, kept for the eye's pose: the arm swing
        /// per side (forward positive, already faded by the gait and sized by the stride), the direction of travel
        /// in the body's frame, how far the jump pose is up, and the figure's scale.</summary>
        readonly float[] swing = new float[2];
        Vector2 stepSeen;
        float armLift, figScale = 1f;
        /// <summary>Where each hand actually is in front of the eye, chasing where it is wanted.</summary>
        readonly Vector3[] eyeHand = new Vector3[2];
        bool eyePosed;
        /// <summary>Where each hand was last drawn, and the frame it was drawn in — for whatever is carried in it.
        /// The eye's pose and the body's write the same two entries, so a thing hung on the hand follows it into
        /// and out of the first person without knowing which of the two put it there.</summary>
        readonly Vector3[] handAt = new Vector3[2];
        readonly Quaternion[] handGrip = { Quaternion.identity, Quaternion.identity };
        bool handsKnown;
        /// <summary>The pose as solved this frame, for whatever wears it (<see cref="PuppetSkeleton"/>).</summary>
        PuppetPose solved;
        /// <summary>A skinned model from a file worn over this pose, once <see cref="Wear"/> has bound one, and
        /// whether it is the figure being shown (<see cref="WearModel"/>): the sculpted parts hide while it is.</summary>
        PuppetSkeleton skeleton;
        bool worn;

        /// <summary>The hand as drawn this frame: 0 left, 1 right. <paramref name="grip"/> is the palm's frame —
        /// its up runs back along the forearm, its right is the thin axis of the palm. False until the figure has
        /// been posed once.</summary>
        public bool Hand(int side, out Vector3 at, out Quaternion grip)
        {
            side = Mathf.Clamp(side, 0, 1);
            at = handAt[side]; grip = handGrip[side];
            // with a model worn its hand is the one on the screen, and a thing carried belongs in that one
            if (Worn && !eyeArms && skeleton.HandPoint(side, out var model)) at = model;
            return handsKnown;
        }

        // ─── the figure, in metres ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The hover height these numbers were drawn against. Everything below is a length at that hover, and
        /// the tuning's own hover scales the lot (see <see cref="LateUpdate"/>): the pelvis has to sit where the
        /// physics holds it or the legs reach past the ground, and one ratio keeps the whole body agreeing with it.</summary>
        const float HoverBuilt = .95f;
        /// <summary>Pelvis above the ground with the capsule at rest, and the head's centre above the pelvis. They add
        /// up with the head's own half-height (0.20) to 1.62 m of climber — <see cref="PuppetTuning.StandHeight"/>.
        /// The old figure was 1.79 and read as a man on stilts; the 17 cm came almost entirely out of the legs, which
        /// is why the pelvis dropped 13 cm and the trunk only 4.</summary>
        const float PelvisRide = .78f, HeadRise = .70f;
        /// <summary>Bone lengths, metres — and they must match the meshes turned in <see cref="PuppetSkin"/>, because
        /// a bone is drawn at its built length and only stretches under protest. The two legs add up to 0.73 against a
        /// hip that stands 0.711 above the sole: a standing climber therefore carries about 9 cm of knee, and that
        /// bend is not slack — it is the headroom every long step is taken out of. Straighten the standing leg and the
        /// foot can go no more than 10 cm from under the hip without the leg tearing off it.</summary>
        // Shorter than the drop from pelvis to sole on purpose. The capsule rides 7 cm below its nominal hover under
        // its own weight, and a leg cut to the nominal height has to eat that in a permanently bent knee — which is
        // what made a standing figure look like it was crouching to jump. Cut to the height the body actually stands
        // at and the knee keeps only the soft bend a person has.
        internal const float ThighLen = .352f, ShinLen = .334f, UpperArm = .26f, Forearm = .24f;
        /// <summary>Where the boot's origin sits above the ground it is standing on. The mesh hangs 6 cm below its own
        /// origin, so the sole ends up a centimetre into the ground — deliberately, because a sole exactly on a probed
        /// plane shows daylight under it on every ridge and stone.</summary>
        const float SoleUp = .05f;
        /// <summary>Hip joints and the shoulders. The line the feet walk on is not here: it is
        /// <see cref="PuppetTuning.StanceWidth"/>, a tuning, because it was the number that most wanted trying by
        /// hand. The first figure walked on a 0.20 m track — "people walk very nearly in one line" — and that was
        /// true and looked wrong: legs stuck together and a march. The hips went out with it, from 0.12 to 0.16,
        /// and the figure became a cowboy: trouser legs 0.24 m thick on hips 0.32 m apart on a 0.42 m stance. They
        /// are back at 0.13 with the trouser legs cut to 0.20 m, which leaves a few centimetres between the legs
        /// and keeps their tops under the seat of the trousers, 0.24 m to each side.</summary>
        // Shared with PuppetSkin: these are also where the clothes' bones sit in the rest pose the cloth is drawn in.
        internal const float HipOut = .13f, HipSag = .019f, ShoulderOut = .28f, ShoulderUp = .414f;

        // ─── the walk ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Full cycles per second: a base plus a little per metre per second. This is the whole cure for the
        /// scurry. A person crossing ground faster lengthens the stride and barely quickens the step — from 1 m/s to
        /// 3 m/s the stride here doubles while the cadence goes from 0.99 to 1.35 cycles a second.</summary>
        const float Cadence = .81f, CadenceGain = .18f;
        /// <summary>How far ahead of the hip the foot is put down, as a share of the cycle. A quarter would mean the
        /// foot leaving exactly under the other one; a shade more gives the moment of double support a slow walk has.</summary>
        const float StrideShare = .26f;
        /// <summary>The furthest a foot may be put from under its own hip, forward, back and sideways. Not one number:
        /// a leg swings furthest straight ahead, less behind, least of all sideways. These are what the hip's dip is
        /// sized against, and they are also the only thing stopping a side-step from doing the splits.</summary>
        const float AheadMax = .27f, BackMax = .24f, SideMax = .19f;
        /// <summary>Share of the cycle one foot is on the ground. Both ends matter: above a half the two feet overlap
        /// and the figure is walking, below a third there is a moment with neither foot down and it is running — which
        /// at three metres a second on legs this short is simply the truth.</summary>
        const float DutyMin = .28f, DutyMax = .62f;
        /// <summary>How far the pelvis dips at a footfall, as a share of the step it is taking. The dip is not
        /// decoration: it is what lets a straight-ish leg reach a foot placed a quarter of a metre away.</summary>
        /// <summary>Metres the pelvis drops into each footfall — ZERO, deliberately. A real pelvis does dip into
        /// every step, and drawing that was what made the walk look like it had the shakes: on a screen, with a camera
        /// riding the same body, any vertical motion of the whole figure reads as judder rather than as gait. The
        /// legs alone carry the walk now and the body travels level. Left as a number because it is one line to try
        /// again if a shallow version ever looks right.</summary>
        const float BobShare = 0f;
        /// <summary>How high the swinging foot is carried, metres: a walking foot clears the ground by a couple of
        /// centimetres and a running one is picked right up. It goes with the pace and not with the length of the
        /// step — see <c>pace</c> in <see cref="LateUpdate"/> — or a brisk walk comes out as a march.</summary>
        const float LiftLow = .045f, LiftHigh = .15f;
        /// <summary>The furthest a foot that is not striding may be left behind the body it belongs to, metres — a
        /// skid, a shove or a slow creep drags it along at this and no more, which is about all the leg can reach.</summary>
        const float Trail = .22f;
        /// <summary>Degrees the hips turn into a side-step. Small on purpose: the shoulders are supposed to stay on
        /// whatever the player is looking at, and a body that turns the whole way is the bug this replaced.</summary>
        const float LeadYaw = 18f;
        /// <summary>How far behind itself a foot swings while the body is going sideways, metres. Past a slow shuffle
        /// the feet of a side-step have to pass each other — a step covers more ground than the width of a stance, and
        /// a body that keeps facing its target cannot turn out of the problem — so the swinging one goes round the
        /// back of the standing one. That is the step people actually take, and it is the only way two boots on one
        /// line do not go through each other.</summary>
        const float SideCross = .20f;
        /// <summary>The boot's roll through a step, degrees, and the two points of the sole it turns on. Pitching a
        /// boot about its own origin drives the toe through the ground; pitched about the toe going off and about the
        /// heel coming down, the part touching the ground stays exactly where it was put.</summary>
        const float HeelStrike = 13f, ToeOff = 22f, ToePivot = .16f, HeelPivot = .07f;

        /// <summary>Where in the stride we are, 0…1 of a full cycle, advanced by metres of ground covered rather than
        /// by a clock: a body held against a wall stops stepping, and a body shoved along steps whether it meant to.</summary>
        float phase;
        /// <summary>0…1 of the walk being switched on — so that starting and stopping fades the bounce, the lift and
        /// the arm swing instead of snapping them on.</summary>
        float gait;
        /// <summary>Share of the cycle a foot is on the ground, carried between frames so it can be rate-limited.</summary>
        float duty = .52f;
        /// <summary>Two thresholds, not one: a single one flickers at a crawl and the legs judder on the boundary.</summary>
        bool striding;
        /// <summary>Set once the first pose has been solved, so the very first frame does not walk the feet in from
        /// the world origin.</summary>
        bool posed;
        /// <summary>The world point each foot is standing on, whether that is a stride's plant or the spot a stopped
        /// body left it, and where each foot was actually drawn last frame.</summary>
        readonly Vector3[] plant = new Vector3[2], last = new Vector3[2];
        /// <summary>This foot owns its plant point: it is on the ground rather than in the air.</summary>
        readonly bool[] down = new bool[2];
        /// <summary>How far behind its own hip the foot was when it left the ground, metres — the start of the swing,
        /// measured in the body's frame rather than the world's. Swinging a foot between two world points instead
        /// lets the hip run out from under it: at a run the body covers a metre and a half while one leg is in the
        /// air, and the leg is left trailing at full stretch. Beside it, how far into the swing that was: the leg does
        /// not always enter its swing at the beginning of one — setting off puts a foot into the middle of a swing —
        /// and a swing measured from the start would then teleport it.</summary>
        readonly float[] swingFrom = new float[2], swingAt = new float[2];
        /// <summary>Where this foot's next footfall is off its own line — sideways and along — drawn fresh each time
        /// the foot leaves the ground (<see cref="PuppetTuning.StepScatter"/>). It is what stops the feet landing on
        /// two rails. Kept while the foot is down and while the body stands, so a foot rests where it landed.</summary>
        readonly Vector2[] scatter = new Vector2[2];

        public void Bind(Puppet p)
        {
            owner = p;
            PuppetSkin.Ensure();
            root = new GameObject("Figure").transform;
            root.SetParent(p.transform, false);

            // The body, thighs, shins and upper arms draw nothing of their own: the trunk is the clothes' torso, the
            // legs are inside the trousers and the arm is PuppetArm's tube. They are still solved every frame,
            // because they are the bones the cloth hangs on.
            body = Pivot("Body");
            head = Piece(PuppetSkin.Head, "Head");
            for (int s = 0; s < 2; s++)
            {
                arm[s] = new PuppetArm(root, "Arm" + s, s == 0 ? -1f : 1f, PuppetSkin.Skin);
                armUp[s] = Pivot("ArmUp" + s);
                thigh[s] = Pivot("Thigh" + s);
                shin[s] = Pivot("Shin" + s);
                boot[s] = Piece(PuppetSkin.Boot, "Boot" + s);
            }
            // The clothes: one skinned surface over seven of those transforms, in the order PuppetSkin drew it in.
            // Its bounds are given by hand and generously — Unity would otherwise recompute them from every vertex
            // every frame, or cull the figure the moment its root bone left the box.
            var cloth = new GameObject("Clothes");
            cloth.transform.SetParent(root, false);
            clothes = cloth.AddComponent<SkinnedMeshRenderer>();
            clothes.sharedMesh = PuppetSkin.Clothes;
            clothes.sharedMaterial = PuppetSkin.Skin;
            clothes.bones = new[] { body, thigh[0], shin[0], thigh[1], shin[1], armUp[0], armUp[1] };
            clothes.rootBone = body;
            clothes.localBounds = new Bounds(new Vector3(0f, -.15f, 0f), new Vector3(2.2f, 2.8f, 2.2f));
            clothes.quality = SkinQuality.Bone4;
            clothes.updateWhenOffscreen = false;
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

        /// <summary>Keep the arms on screen while the figure is hidden — the view from inside the head. What is shown
        /// is the same two arms the body walks with, put where a person sees their own: forward and apart, low in
        /// the frame, and swinging with the stride the way they swing at the body's sides. Only a hint of a body,
        /// but the hint is what says the eye belongs to somebody who is walking rather than to a camera on rails.
        /// While this is on, the body-space pose leaves the arms alone and whoever places the camera must call
        /// <see cref="PoseFromEye"/> after it — the arms are hung off the eye, and hung off last frame's eye they
        /// shiver.</summary>
        public void ShowEyeArms(bool on)
        {
            if (eyeArms == on) return;
            eyeArms = on;
            eyePosed = false;
            Show();
        }

        public bool EyeArms => eyeArms;

        /// <summary>The pose solved this frame, world space, read-only: what a worn model is posed from.</summary>
        public PuppetPose Pose => solved;
        /// <summary>A model has been bound with <see cref="Wear"/>.</summary>
        public bool HasModel => skeleton != null;
        /// <summary>A model is bound and is the figure being shown; the sculpted parts are hidden while it is.</summary>
        public bool Worn => skeleton != null && worn;
        /// <summary>What the bound model's skeleton reported, for a panel.</summary>
        public string ModelReport => skeleton != null ? skeleton.Report : "";

        /// <summary>Bind a skinned model from a file. Its bones are mapped by name and, while it is shown
        /// (<paramref name="show"/> now, <see cref="WearModel"/> later), posed from this figure's solve every frame
        /// (<see cref="PuppetSkeleton"/>) with the sculpted parts hidden; the physics and the arms the eye sees in
        /// first person stay exactly what they were. False — reason in the log, the model left where it was — when
        /// it has no hips or legs to speak of; the figure then carries on as it is. A model already bound is
        /// destroyed first.</summary>
        public bool Wear(Transform modelRoot, bool show = true)
        {
            if (owner == null || root == null || modelRoot == null) return false;
            var sk = new PuppetSkeleton();
            if (!sk.Bind(modelRoot, root, owner.Tuning.StandHeight)) return false;
            if (skeleton != null && skeleton.Root != null) PuppetRig.Kill(skeleton.Root.gameObject);
            skeleton = sk;
            worn = show;
            if (worn && solved.Valid) skeleton.Apply(in solved, owner.Tuning.StandHeight);
            Show();
            return true;
        }

        /// <summary>Show the bound model (true) or the sculpted figure (false). Nothing happens without a model.</summary>
        public void WearModel(bool on)
        {
            if (skeleton == null || worn == on) return;
            worn = on;
            // posed before it is shown, so it never spends a frame in the pose it was hidden in
            if (on && solved.Valid && owner != null) skeleton.Apply(in solved, owner.Tuning.StandHeight);
            Show();
        }

        void OnDestroy()
        {
            for (int s = 0; s < 2; s++) arm[s]?.Release();
        }

        void Show()
        {
            bool sculpted = visible && !Worn;
            for (int i = 0; i < parts.Length; i++) if (parts[i] != null) parts[i].enabled = sculpted;
            // the arm renderers are in `parts` too; they alone stay on for the eye
            for (int s = 0; s < 2; s++)
                if (arm[s] != null && arm[s].Renderer != null) arm[s].Renderer.enabled = sculpted || eyeArms;
            skeleton?.SetVisible(visible && worn);
        }

        Transform Piece(Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = PuppetSkin.Skin;
            return go.transform;
        }

        /// <summary>A bare transform: a bone the clothes hang on, with nothing of its own to draw.</summary>
        Transform Pivot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        void LateUpdate()
        {
            if (owner == null || owner.Torso == null) return;
            var t = owner.Tuning;
            var rb = owner.Torso.transform;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            // ── how big this body is ───────────────────────────────────────────────────────────────────────────────
            // One ratio and the whole figure follows the tuning. The hover height is where the physics holds the
            // capsule over the ground, so it is also where the pelvis is and how far the legs have to reach; read
            // rather than repeated, the drawn body and the physical one cannot drift apart. The clamp is for a slider
            // dragged somewhere silly — the limb meshes stretch to their bones, and past about a quarter that shows.
            float scale = Mathf.Clamp(t.HoverHeight / HoverBuilt, .80f, 1.25f);
            // and the pieces are scaled with it, not just the joints they hang between: a torso drawn for one stature
            // with the shoulders of another leaves the sleeves off the body
            if (!Mathf.Approximately(body.localScale.x, scale))
            {
                var uni = Vector3.one * scale;
                body.localScale = uni; head.localScale = uni;
                for (int s = 0; s < 2; s++) boot[s].localScale = uni;
            }
            // A worn model is solved for its own build (PuppetSkeleton measures it): its hip and shoulder joints,
            // its bone lengths, where its pelvis rides over its soles and how high its ankles sit. Aimed along
            // segments solved for those, its bones end exactly at the solved joints — the feet land, nothing
            // stretches, and a model with longer legs than the sculpted figure's does not walk in a crouch.
            bool model = Worn;
            var build = model ? skeleton.Proportions : default;
            model = model && build.Valid;
            float hipDrop = t.HoverHeight - (model ? build.PelvisRide : PelvisRide * scale);   // pelvis below the capsule's centre

            // ── the ground, and the body's heading over it ─────────────────────────────────────────────────────────
            float ride = float.IsInfinity(owner.GroundDistance) ? t.HoverHeight : owner.GroundDistance;
            var groundPoint = rb.position + Vector3.down * ride;          // what the leg probe found under the capsule
            var normal = owner.GroundNormal.y > .35f ? owner.GroundNormal : Vector3.up;

            // The heading comes off the capsule, which Puppet turns to the camera and to nothing else, so the figure
            // keeps facing where the player looks. The drift is the same body's velocity read in that frame: y ahead,
            // x to its right. A side-step is all x, a retreat is a negative y, and the feet follow the drift while the
            // shoulders stay where they were pointed.
            var look = owner.Facing;
            var drift = owner.Drift;
            float speed = drift.magnitude;
            var step = speed > 1e-3f ? drift / speed : Vector2.zero;
            float lateral = step.x, forward = step.y;

            // A man whose feet have gone does not take steps. The feet are still on the ground through a slide — they
            // are skidding, and the pose Puppet has already sat him back on his heels for is the rest of that — so a
            // slide takes the stride away without taking the ground away.
            bool afoot = owner.Grounded && !owner.Limp;
            bool wasStriding = striding;
            striding = afoot && !owner.Sliding && (striding ? speed > .22f : speed > .42f);
            gait = Mathf.MoveTowards(gait, striding ? 1f : 0f, dt / .16f);

            // ── the stride ─────────────────────────────────────────────────────────────────────────────────────────
            float cadence = Cadence + CadenceGain * speed;                // full cycles per second
            float cycle = speed / cadence;                                // metres of ground per cycle
            float ahead = Ellipse(step, forward >= 0f ? AheadMax : BackMax, SideMax) * scale;
            float back = Ellipse(step, forward >= 0f ? BackMax : AheadMax, SideMax) * scale;
            float half = Mathf.Min(cycle * StrideShare, ahead);           // how far ahead of the hip a foot lands
            // Stance lasts exactly as long as the foot can stay under the body: from `half` in front to `back` behind.
            // Faster than the legs can manage that becomes a run — the floor on the duty is what says how much of a
            // moment with neither foot down is allowed before the foot is dragged instead. It is also rate-limited,
            // because the duty is where the swing ends: let it jump and a leg that had just left the ground finds
            // itself already standing, a stride away from where it is.
            float want = Mathf.Clamp(cycle > 1e-3f ? (half + back) / cycle : .52f, DutyMin, DutyMax);
            duty = posed ? Mathf.MoveTowards(duty, want, dt * .6f) : want;
            float span = duty * cycle;                                    // ground covered while one foot is down
            float backHit = Mathf.Clamp(span - half, 0f, back);           // how far behind the hip the foot really gets
            float slip = Mathf.Max(0f, span - half - backHit);            // and the ground it has to give up at a sprint
            if (striding) phase = Frac(phase + speed * dt / Mathf.Max(cycle, .05f));
            float stride = Mathf.Clamp01(half / Mathf.Max(ahead, 1e-3f)); // 0 = shuffling, 1 = the longest step it has
            // How hard the body is going, which is not the same question as how long the step is: the stride is at its
            // limit from a brisk walk upward, and picking a foot up the way a runner does at that pace would be a
            // march. Foot clearance goes with the pace instead, and is a couple of centimetres at a walk.
            float pace = Mathf.Clamp01(speed / Mathf.Max(t.RunSpeed, .1f));

            // The pelvis dips at every footfall and comes back up over the standing leg. It is the dip that makes the
            // reach: a leg with 9 cm of knee in it cannot put a foot a quarter-metre away with the hip held at full
            // height. Taken from whichever leg is on the ground, it also falls out right for a run, where the hip is
            // carried high through the moment neither foot is down.
            float dip = Mathf.Max(Dip(Frac(phase), duty), Dip(Frac(phase + .5f), duty));
            float bob = gait * BobShare * scale * dip;

            // The airborne pose is held, not sampled: taken straight off the climb rate it vanishes at the top of
            // the arc, exactly where a jump is worth looking at. It rises with the launch and decays over a third of
            // a second, so the tuck and the raised arms carry through the apex and ease out on the way down.
            float wantAir = owner.Limp || owner.Grounded ? 0f : Mathf.Clamp01(owner.Torso.linearVelocity.y / 2.6f);
            // eased in as well as out, and not at the same rate: arms that snap up the frame the feet leave the
            // ground look like a twitch, and spamming the jump key turned the figure into a flicker. Up over a fifth
            // of a second, down over a third.
            airPose = Mathf.MoveTowards(airPose, wantAir, dt / (wantAir > airPose ? .21f : .34f));

            var hipPoint = rb.position + Vector3.down * (hipDrop + bob);

            // ── how the body sits on all that ──────────────────────────────────────────────────────────────────────
            // The hips lead a side-step a little and the shoulders come only part of the way with them; the head is
            // left on the heading outright, so a man stepping sideways keeps watching what he was watching.
            float lead = gait * lateral * LeadYaw;
            var level = Quaternion.AngleAxis(lead, Vector3.up) * look;    // the frame the feet are laid out in
            var tilt = Quaternion.FromToRotation(Vector3.up, Quaternion.Inverse(level) * (rb.rotation * Vector3.up));
            // The torso really leans: Puppet tilts the whole capsule into its own acceleration, so the figure draws
            // the physics instead of a cosmetic angle worked out from speed. Limp is the physics too — the body has
            // been thrown over and the capsule is lying down, so the pose is simply the capsule's own rotation.
            var pose = owner.Limp ? rb.rotation : level * tilt;
            // the one thing the physics does not say: a man whose feet have gone sits back on his heels
            if (owner.Sliding && !owner.Limp) pose *= Quaternion.Euler(-12f, 0f, 0f);

            // hips, chest and pack are one mesh now: they never moved relative to each other anyway
            body.SetPositionAndRotation(hipPoint, pose);
            // no neck: the head sits straight on the shoulders, the way it does in PEAK. The hat is part of it.
            // the head keeps some of the lean and rights itself against the rest, the way a person's does
            head.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, HeadRise * scale, 0f),
                owner.Limp ? rb.rotation : look * Quaternion.Slerp(Quaternion.identity, tilt, .35f));

            // ── legs ───────────────────────────────────────────────────────────────────────────────────────────────
            var dirWorld = look * new Vector3(step.x, 0f, step.y);        // where the feet are going, in the world
            // where the ankle — the leg's target — sits over the ground: the boot's origin, or the model's ankle joint
            float soleUp = model ? build.AnkleUp : SoleUp * scale;
            var soleBase = groundPoint + Vector3.up * soleUp;
            // The track is a real width, not a share of the drawn body: it is the one number of the stance the player
            // of the sandbox turns by hand, and a slider that read in metres and then got scaled would lie.
            float footOut = t.StanceWidth * .5f;

            // Setting off. The foot that is already in front stays where it is and the other one steps, or the figure
            // lurches into its first stride. The cycle is started with the staying foot at the middle of its stance,
            // which is the one place that puts the other exactly halfway through a swing whatever the duty is — start
            // it at a lift-off instead and the second leg finds itself at the very end of a swing it never took, and
            // has to cross a stride's worth of ground in a frame.
            if (striding && !wasStriding)
            {
                float a = Vector3.Dot(plant[0] - soleBase, dirWorld), b = Vector3.Dot(plant[1] - soleBase, dirWorld);
                phase = a >= b ? duty * .5f : Frac(duty * .5f + .5f);
            }

            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                float thighLen = model ? build.Thigh(s) : ThighLen * scale, shinLen = model ? build.Shin(s) : ShinLen * scale;
                float legLen = thighLen + shinLen, legMax = legLen * .985f;
                var hipJoint = hipPoint + pose * (model ? build.Hip(s) : new Vector3(sign * HipOut * scale, -HipSag * scale, 0f));
                // this foot's line on the ground, and this footfall's wander off it
                var track = level * new Vector3(sign * footOut + scatter[s].x, 0f, scatter[s].y);
                var home = OnGround(soleBase, track, normal);
                float q = Frac(phase + (s == 0 ? 0f : .5f));
                float pitch = 0f;
                Vector3 foot;

                if (!afoot)
                {
                    // Off the ground or thrown: the legs hang with a little bend and nothing is planted, so whatever
                    // they come down on next is where the next plant is taken. A body in the air hangs its legs
                    // toward the ground; one that has been thrown over hangs them along itself, or a man lying on his
                    // back has his feet through the floor.
                    down[s] = false;
                    var hang = owner.Limp ? pose * Vector3.down : Vector3.down;
                    // rising: knees come up and out, the way anybody leaving the ground does it. Falling: they go
                    // back down and reach for whatever is coming. Taken from the climb rate, so it reads on a jump,
                    // on a drop off a ledge and on the top of an arc where the two meet.
                    float spring = airPose;
                    foot = hipJoint
                         + pose * new Vector3(sign * .13f * spring * scale, 0f, (-.04f - .16f * spring) * scale)
                         + hang * (legLen * (.93f - .30f * spring));
                }
                else if (striding)
                {
                    if (q < duty)
                    {
                        // Stance. The foot owns a point of the world and the body rides over it — this is the phase
                        // the old figure had no equivalent of, and its absence is what read as scurrying. It lands
                        // where it actually is, which in the ordinary way round is exactly the step ahead its swing
                        // was aiming at; when the stride is being re-cut underneath it — slowing down lengthens the
                        // stance and can call a foot down early — it lands where it had got to rather than teleport.
                        if (!down[s])
                        {
                            var at = posed ? last[s] : OnGround(soleBase, track + dirWorld * half, normal);
                            plant[s] = OnGround(soleBase, new Vector3(at.x - soleBase.x, 0f, at.z - soleBase.z), normal);
                            down[s] = true;
                        }
                        float u = q / Mathf.Max(duty, 1e-3f);
                        // at a sprint the body outruns what the leg can span; the shortfall is given up late in the
                        // stance, as a slide under a foot that is already rolling off, where it barely shows
                        foot = plant[s] + dirWorld * (slip * u * u);
                        pitch = StanceRoll(u);
                    }
                    else
                    {
                        // Swing, carried in the body's frame: the foot goes from wherever it left the ground to a
                        // step ahead of the hip, and the hip may travel as far as it likes underneath without ever
                        // leaving the leg behind. Whatever is left of the swing is what the move is fitted into, so
                        // a leg that joins one halfway — which is what setting off from standing does — hurries
                        // rather than jumps.
                        float u = (q - duty) / Mathf.Max(1f - duty, 1e-3f);
                        if (down[s])
                        {
                            swingFrom[s] = Vector3.Dot(last[s] - (soleBase + track), dirWorld);
                            swingAt[s] = u;
                            down[s] = false;
                            // a new step, a new place for it: a little in or out, a little short or long. Drawn once
                            // at lift-off and not every frame, so the foot in the air is going somewhere definite.
                            // Along the step it is worth less than across it — a long step is what the stride is
                            // for, and it must not be cut into by chance; a foot off its line is the thing missing.
                            scatter[s] = new Vector2(Random.Range(-1f, 1f), Random.Range(-.6f, .6f)) * t.StepScatter;
                            track = level * new Vector3(sign * footOut + scatter[s].x, 0f, scatter[s].y);
                        }
                        // one progress for all three — where the foot is, how high it is carried and how the boot is
                        // held — so that a leg joining a swing late starts all of them from where it stands
                        float w = Ease(swingAt[s], 1f, u);
                        float go = Mathf.Lerp(swingFrom[s], half, w);
                        float arc = Mathf.Sin(w * Mathf.PI);
                        float round = Mathf.Abs(lateral) * SideCross * arc * gait * scale;
                        // and it is picked up further as well while it goes round: a crossing step is a stepped-over
                        // one, and the boots are wide
                        float clear = Mathf.Lerp(LiftLow, LiftHigh, Mathf.Max(pace, Mathf.Abs(lateral) * .5f));
                        foot = OnGround(soleBase, track + dirWorld * go - level * new Vector3(0f, 0f, round), normal)
                             + Vector3.up * (arc * clear * scale);
                        pitch = SwingRoll(w);
                    }
                    pitch *= gait * Mathf.Lerp(.45f, 1f, stride);
                }
                else
                {
                    // Standing, or drifting too slowly to be walking. The foot keeps the ground it was left on and is
                    // walked home rather than snapped there — the old version latched the foot outright and then
                    // stretched the leg whenever anything nudged the body. The rate is proportional to how far out it
                    // is and there is a dead band around home: a body that has stopped is standing dead still, and
                    // one creeping too slowly to have a stride drags its feet along smoothly instead of twitching
                    // them after itself every few frames.
                    if (!down[s]) { plant[s] = posed ? last[s] : home; down[s] = true; }
                    float far = (plant[s] - home).magnitude;
                    // and it may not be left further behind than the leg can reach: a body carried off by a slide
                    // would otherwise be dragging its feet on the ends of two straight legs
                    if (far > Trail) { plant[s] = home + (plant[s] - home) * (Trail / far); far = Trail; }
                    if (far > .035f) plant[s] = Vector3.MoveTowards(plant[s], home, far * 3.2f * dt);
                    foot = plant[s];
                }

                // Whatever the gait asked for, a leg is only so long. Clamping the drawn foot rather than letting the
                // solver stretch the shin keeps the boot on the end of the leg on broken ground and in a stumble.
                var reach = foot - hipJoint;
                if (reach.sqrMagnitude > legMax * legMax) foot = hipJoint + reach.normalized * legMax;
                last[s] = foot;

                // The boot is turned out by the stance (PuppetTuning.ToeOut), turns further into a side-step — hard
                // on the leading foot, barely on the trailing one — and rolls heel to toe through the stance. It is
                // pitched about the part of the sole that is on the ground, so the toe never goes through it — and
                // the ankle, which is the leg's target, rises with the roll the way an ankle does when the heel
                // comes up, so that a model's foot pitched about its own ankle joint keeps its toe on the ground too.
                float yaw = sign * t.ToeOut + (afoot ? gait * lateral * (sign * lateral > 0f ? 26f : 12f) : 0f);
                var flat = afoot ? level * Quaternion.AngleAxis(yaw, Vector3.up) : pose;
                var pivot = new Vector3(0f, 0f, (pitch > 0f ? ToePivot : -HeelPivot) * scale);
                var bootRot = flat * Quaternion.Euler(pitch, 0f, 0f);
                var ankle = foot + flat * pivot - bootRot * pivot;

                // The knee bends the way the toe points, not dead ahead: turned out with the boot, a standing leg
                // reads as a leg and not as a piston. Two straight knees pointing forward on a broad track was the
                // "robot on parade" the wider stance alone did not cure.
                var kneeWay = pose * (Quaternion.AngleAxis(sign * t.ToeOut, Vector3.up) * Vector3.forward);
                // the trouser leg is skinned to these two bones, so the cloth bends with the knee and stretches
                // with the bone inside it — same line AND same stretch as the bone inside: a trouser leg that keeps
                // its built length while the bone is scaled hangs past the boot, and the figure measures a head
                // taller than it is
                var knee = Limb(thigh[s], shin[s], hipJoint, ankle, thighLen, shinLen, kneeWay, ThighLen, ShinLen, pose * Vector3.forward, scale);
                boot[s].SetPositionAndRotation(ankle + flat * new Vector3(0f, 0f, .015f * scale), bootRot);
                // written down for whatever wears this pose: the sole sits a centimetre into the ground under the
                // ankle, as the boot mesh's underside does, so that nothing standing on it shows daylight
                var legPose = new PuppetLegPose
                {
                    Hip = hipJoint, Knee = knee, Ankle = ankle, Foot = bootRot,
                    Sole = ankle + bootRot * new Vector3(0f, -(soleUp + .012f * scale), 0f),
                    Bend = kneeWay,
                };
                if (s == 0) solved.LegL = legPose; else solved.LegR = legPose;
            }

            // ── arms ───────────────────────────────────────────────────────────────────────────────────────────────
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                bool modelArm = model && build.HasArm(s);
                var shoulder = hipPoint + pose * (modelArm ? build.Shoulder(s) : new Vector3(sign * ShoulderOut * scale, ShoulderUp * scale, .01f));
                float upperArm = modelArm ? build.UpperArm(s) : UpperArm * scale;
                // the arm reaches for the middle of the palm; a model's forearm ends at the wrist, half a palm short of it
                float forearm = modelArm ? build.Forearm(s) + PuppetArm.PalmHalf * scale : Forearm * scale;
                var physical = s == 0 ? owner.Left : owner.Right;
                // An arm answers the leg on its own side: forward when that leg is back, on the same clock. The
                // swing is the drift, not the pace — walking backwards swings it the other way, and a side-step
                // swings the arms across the body and pushes both hands out a little to balance it.
                float q = Frac(phase + (s == 0 ? 0f : .5f));
                float drive = -Mathf.Cos(q * Mathf.PI * 2f) * Mathf.Lerp(.08f, .26f, stride) * gait;
                // remembered for the eye's arms, which swing on the same clock from a different place
                swing[s] = drive; stepSeen = step; armLift = afoot ? 0f : airPose; figScale = scale;
                Vector3 wrist;
                if (owner.HandsEnabled && physical != null) wrist = physical.transform.position;
                else
                {
                    // Not hanging dead at the seams: the hands rest a little forward of the hips with the elbow
                    // softly bent, which is where a person's arms actually are — arms straight down read as a doll.
                    wrist = shoulder + pose * new Vector3(
                        (sign * (.10f + Mathf.Abs(lateral) * .06f * gait)) * scale + drive * lateral * .45f,
                        -(upperArm + forearm) * .82f,
                        (.085f * scale) + drive * forward);
                    // and in the air they go up: fully by the top of a launch, back down as the body starts to fall
                    float lift = afoot ? 0f : airPose;
                    if (lift > 0f)
                        wrist = Vector3.Lerp(wrist, shoulder + pose * new Vector3(
                            sign * .26f * scale, (upperArm + forearm) * .28f, .06f * scale), lift);
                }
                // the elbow is solved as for a leg, but no bones are laid on it: the arm is one tube through the
                // three points, hand and all
                var elbow = Joint(shoulder, wrist, upperArm, forearm, pose * Vector3.back);
                var grip = Wrist(elbow, wrist, pose);
                // seen from inside the head the arms hang off the eye instead (PoseFromEye), and a tube built here
                // as well would flash at the body's sides on whichever frames it was built last
                if (!eyeArms)
                {
                    arm[s].Pose(shoulder, elbow, wrist, grip, scale);
                    handAt[s] = wrist; handGrip[s] = grip; handsKnown = true;
                }
                // the upper arm as a bone, for the sleeve skinned to it: same line as the tube, rolled to the body
                Bone(armUp[s], shoulder, elbow, UpperArm, pose * Vector3.forward, scale);
                // the sleeve sits on the shoulder and runs down the upper arm — same line, its own length
                // and for whatever wears this pose: `wrist` here is the middle of the palm (PuppetArm), the joint
                // itself sits half a palm back along the forearm
                var toHand = wrist - elbow;
                var along = toHand.sqrMagnitude > 1e-8f ? toHand.normalized : pose * Vector3.down;
                var armPose = new PuppetArmPose
                {
                    Shoulder = shoulder, Elbow = elbow, Wrist = wrist - along * (PuppetArm.PalmHalf * scale),
                    Hand = wrist, Grip = grip, Bend = pose * Vector3.back,
                };
                if (s == 0) solved.ArmL = armPose; else solved.ArmR = armPose;
            }
            solved.Valid = true; solved.Scale = scale; solved.Limp = owner.Limp;
            solved.Pelvis = hipPoint; solved.Body = pose;
            solved.Head = head.position; solved.HeadRot = head.rotation;
            // the worn model, if any, is posed from all of the above — here, so the order is never a race
            if (worn && skeleton != null) skeleton.Apply(in solved, t.StandHeight);
            posed = true;
        }

        // ─── the arms as the eye sees them ──────────────────────────────────────────────────────────────────────

        /// <summary>Where the eye's shoulders and hands sit, in the eye's frame (x to the right, y up, z ahead) at
        /// the built stature. The hands are held forward and apart, low in the picture — the pose of somebody
        /// walking on snow with the arms out a little for balance, not somebody carrying a rifle. At the sandbox's
        /// sixty degrees the frame at 0.40 m is 0.46 m tall and 0.74 m wide, so the two palms sit in the lower
        /// corners with the forearms running out of the picture toward the elbows, which are outside it.
        ///
        /// The shoulders are not where the body's are. A hand 0.4 m in front of the eye is out of reach of a
        /// shoulder 0.4 m below it, and a hand near enough to reach fills a quarter of the picture — the first
        /// try, at 0.31 m, was two palms the size of dinner plates. So the shoulders are brought forward under the
        /// eye instead; nothing above the elbow is ever in the picture, and the arm's length is what it was.</summary>
        const float EyeShoulderOut = .20f, EyeShoulderDown = .34f, EyeShoulderAhead = .16f;
        const float EyeHandOut = .28f, EyeHandDown = .20f, EyeHandAhead = .46f;
        /// <summary>Which way the palms face: toward each other, tipped this many degrees down. Palms flat down
        /// showed the eye four fingers laid out toward the horizon; a hand carried with the palm in and the fingers
        /// loosely closed is seen from the thumb side, the way a person sees their own hands walking.</summary>
        const float EyePalmDown = 60f;
        /// <summary>How far, m, a hand may lag behind where the eye wants it. The lag is what makes the hands
        /// yours rather than painted on the lens — they hang back a fraction when the head turns and bob against
        /// the head's bob — and the cap keeps a fast turn from dragging them out of the picture.</summary>
        const float EyeLag = .06f;

        /// <summary>Puts the two arms in front of the eye. Called by whoever placed the camera, after placing it,
        /// with the eye's position and rotation as drawn this frame; while <see cref="EyeArms"/> is off it does
        /// nothing. The swing comes from the last body-space pose (same clock, same size), so the hands in the
        /// picture answer the legs exactly as they do when watched from outside; a jump lifts them; with the physics
        /// hands switched on each arm reaches for its own hand body instead, because that is what is gripping.</summary>
        public void PoseFromEye(Vector3 eye, Quaternion look, float dt)
        {
            if (!eyeArms || owner == null) return;
            float k = figScale;
            float upperArm = UpperArm * k, forearm = Forearm * k;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var shoulder = eye + look * new Vector3(sign * EyeShoulderOut * k, -EyeShoulderDown * k, EyeShoulderAhead * k);
                var physical = s == 0 ? owner.Left : owner.Right;
                Vector3 want;
                if (owner.HandsEnabled && physical != null) want = physical.transform.position;
                else
                {
                    float sw = swing[s];
                    // forward and back with the stride, out a little on a side-step, and a hand swung forward
                    // rises a little, the way a swinging arm does about the shoulder. A fraction of the body's
                    // swing: the hands are half a metre from the lens, and the full 26 cm put one of them on it.
                    var local = new Vector3(sign * EyeHandOut * k + sw * stepSeen.x * .30f,
                                            -EyeHandDown * k + sw * stepSeen.y * .15f,
                                            EyeHandAhead * k + sw * stepSeen.y * .30f);
                    // and in the air they go up and out
                    if (armLift > 0f)
                        local = Vector3.Lerp(local, new Vector3(sign * .36f * k, .04f * k, .28f * k), armLift);
                    want = eye + look * local;
                }
                if (!eyePosed) eyeHand[s] = want;
                else
                {
                    eyeHand[s] = Vector3.Lerp(eyeHand[s], want, 1f - Mathf.Exp(-16f * dt));
                    var off = eyeHand[s] - want;
                    if (off.sqrMagnitude > EyeLag * EyeLag) eyeHand[s] = want + off.normalized * EyeLag;
                }
                var hand = eyeHand[s];
                // the elbow goes down and out, below the bottom of the picture
                var elbow = Joint(shoulder, hand, upperArm, forearm, look * new Vector3(sign * .6f, -1f, -.3f));
                var grip = EyeGrip(elbow, hand, look, sign);
                arm[s].Pose(shoulder, elbow, hand, grip, k, PuppetSkin.EyeDigits);
                handAt[s] = hand; handGrip[s] = grip; handsKnown = true;
                Bone(armUp[s], shoulder, elbow, UpperArm, look * Vector3.forward, k);
            }
            eyePosed = true;
        }

        /// <summary>The hand's frame in front of the eye, built outright rather than rolled from <see cref="Wrist"/>.
        /// That one sits the hand on the forearm with its front toward a frame's forward, which is right for an arm
        /// hanging by the body and turns over on a forearm rising toward the eye (the look's forward, projected off
        /// a forearm nearly along it, points down): thumbs outside, palms up. Here the frame is written down from
        /// what is wanted — the grip's up is back along the forearm, as PuppetArm expects, its right is the thin
        /// axis of the palm, and the palm side is <c>−sign·right</c> (the digits are drawn for a right hand whose
        /// palm faces the body, and mirrored for the left), so the palm is pointed in toward the other hand and a
        /// little down, and the rest of the frame follows from it.</summary>
        static Quaternion EyeGrip(Vector3 elbow, Vector3 hand, Quaternion look, float sign)
        {
            var along = elbow - hand;
            var up = along.sqrMagnitude > 1e-8f ? along.normalized : look * Vector3.down;
            float a = EyePalmDown * Mathf.Deg2Rad;
            var palm = Vector3.ProjectOnPlane(look * new Vector3(-sign * Mathf.Cos(a), -Mathf.Sin(a), 0f), up);
            if (palm.sqrMagnitude < 1e-6f) palm = Vector3.ProjectOnPlane(look * Vector3.down, up);
            var right = -sign * palm.normalized;
            return Quaternion.LookRotation(Vector3.Cross(right, up), up);
        }

        /// <summary>How much the pelvis wants to be dropped by the leg whose cycle stands at <paramref name="q"/>: all
        /// of it at the two instants that leg is against the ground — the moment it lands and the moment it leaves —
        /// and none of it in between or while the leg is in the air.
        ///
        /// Those are the two instants the foot is furthest from under the hip, so they are the two the leg's reach has
        /// to be bought for. Hung on the stance rather than on a plain cosine of the cycle, it still lands on the right
        /// instants when the stance is short: in a run the hip is then carried high through the part of the cycle with
        /// no foot down at all, which is what running looks like. Both events are smooth bumps and not a switch,
        /// because a pelvis that steps by five centimetres between two frames is worse than no dip at all.</summary>
        static float Dip(float q, float duty)
        {
            float r = Mathf.Max(duty * .5f, .08f);      // half a stance wide: the two bumps meet at mid-stance
            return Mathf.Max(Bump(Round(q), r), Bump(Round(q - duty), r));
        }

        /// <summary>Distance to a point of the cycle the short way round, 0…0.5.</summary>
        static float Round(float d) { d = Frac(d); return Mathf.Min(d, 1f - d); }

        /// <summary>1 at the middle, 0 at <paramref name="r"/> and flat at both ends, so nothing built on it steps.</summary>
        static float Bump(float d, float r)
        {
            if (d >= r) return 0f;
            float c = Mathf.Cos(d / r * Mathf.PI * .5f);
            return c * c;
        }

        /// <summary>Degrees the boot is pitched toe-down at <paramref name="u"/> of a stance: it lands on its heel,
        /// rolls flat under the body and leaves on its toe. Without this the foot is a plank and every step looks like
        /// it is being stamped.</summary>
        static float StanceRoll(float u) => -HeelStrike * (1f - Ease(0f, .35f, u)) + ToeOff * Ease(.55f, 1f, u);

        /// <summary>And through the swing: off the toe, level, then the toe comes up ready for the next heel.</summary>
        static float SwingRoll(float u) => Mathf.Lerp(ToeOff, -HeelStrike, Ease(0f, .62f, u));

        /// <summary>Smoothstep between two edges — Unity's own takes a pair of values and a t, which is the other
        /// thing entirely.</summary>
        static float Ease(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>How far a foot may be put from under its hip in a given direction. An ellipse and not a circle,
        /// because a leg swings furthest straight ahead, less behind it and least of all out to the side.</summary>
        static float Ellipse(Vector2 dir, float ahead, float side)
        {
            float x = dir.x / Mathf.Max(side, 1e-3f), y = dir.y / Mathf.Max(ahead, 1e-3f);
            float q = x * x + y * y;
            return q > 1e-6f ? 1f / Mathf.Sqrt(q) : ahead;
        }

        /// <summary>A point on the ground a step away from the body. Only one plane is known — the one the leg probe
        /// found under the capsule — so the step follows that plane's slope; on a ramp a foot put a quarter-metre
        /// uphill is a couple of centimetres higher, and taking it as level walks the figure into the hill.</summary>
        static Vector3 OnGround(Vector3 under, Vector3 offset, Vector3 n)
        {
            float fall = n.y > .35f ? (offset.x * n.x + offset.z * n.z) / n.y : 0f;
            return under + offset - Vector3.up * fall;
        }

        /// <summary>Two bones between a joint and an end point, bent the right way; returns where the joint between
        /// them ended up. The law of cosines, nothing more — but the reach is clamped at both ends now: pulled past
        /// full stretch the limb used to detach, and pulled into the shoulder it used to shoot out sideways.
        /// <paramref name="builtA"/> and <paramref name="builtB"/> are the lengths the meshes were actually turned at,
        /// which is not the same as the bone when the whole figure is scaled to a tuning.</summary>
        static Vector3 Limb(Transform upper, Transform lower, Vector3 from, Vector3 to, float a, float b,
                            Vector3 bendToward, float builtA, float builtB, Vector3 roll, float scale)
        {
            var joint = Joint(from, to, a, b, bendToward);
            Bone(upper, from, joint, builtA, roll, scale);
            Bone(lower, joint, to, builtB, roll, scale);
            return joint;
        }

        /// <summary>The joint alone — where the elbow or the knee ends up between <paramref name="from"/> and
        /// <paramref name="to"/> for bones of lengths <paramref name="a"/> and <paramref name="b"/>.</summary>
        static Vector3 Joint(Vector3 from, Vector3 to, float a, float b, Vector3 bendToward)
        {
            var delta = to - from;
            float reach = a + b, near = Mathf.Min(Mathf.Abs(a - b) * 1.02f + .01f, reach * .9f);
            float d = Mathf.Clamp(delta.magnitude, near, reach * .999f);
            var dir = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.down;
            float along = (a * a - b * b + d * d) / (2f * d);
            float out_ = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var side = Vector3.ProjectOnPlane(bendToward, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return from + dir * along + side.normalized * out_;
        }

        /// <summary>Puts a bone mesh on the line from <paramref name="from"/> to <paramref name="to"/>. The mesh is
        /// turned at its built length and the joint IK hands over very nearly that, so normally there is next to no
        /// scaling at all and the rounded ends keep their shape. Only a limb scaled to another stature, or the last
        /// bone of an over-reaching arm, stretches — and a little squash-and-stretch there reads better than a gap at
        /// the wrist.</summary>
        static void Bone(Transform bone, Vector3 from, Vector3 to, float built, Vector3 roll, float scale)
        {
            var d = to - from;
            float len = d.magnitude;
            bone.SetPositionAndRotation(from, len > 1e-4f ? Aim(d / len, roll) : Aim(Vector3.down, roll));
            float k = built > 1e-4f ? Mathf.Clamp(len / built, .75f, 1.5f) : 1f;
            var s = bone.localScale;
            if (!Mathf.Approximately(s.y, k) || !Mathf.Approximately(s.x, scale)) bone.localScale = new Vector3(scale, k, scale);
        }

        /// <summary>A bone's frame: Y along <paramref name="dir"/>, Z as near <paramref name="roll"/> as it can be.
        /// The old way — the shortest turn from "up" to the bone's direction — has no opinion about the roll, and for
        /// a bone pointing nearly straight down that opinion swings right round as the leg passes through vertical.
        /// A rigid capsule does not care; a skinned sleeve, half of whose vertices follow the arm and half the body,
        /// is wrung like a rag by it. Pinned to the body's forward the frame is the same one the clothes were drawn
        /// in (see <see cref="PuppetSkin.BindPoses"/>).</summary>
        internal static Quaternion Aim(Vector3 dir, Vector3 roll)
        {
            var z = Vector3.ProjectOnPlane(roll, dir);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return Quaternion.LookRotation(z.normalized, dir);
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
