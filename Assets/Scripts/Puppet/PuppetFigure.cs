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
    /// is the capsule's own heading and the capsule is turned by the camera and nothing else — and the feet go wherever
    /// <see cref="Puppet.Drift"/> says the body is drifting in its own frame. Sideways that comes out as a side-step:
    /// the legs open and close, the boots turn out, the hips lead a little and the shoulders stay on the target.
    ///
    /// The legs are the point of all of it: the feet are planted in turn, the knee is solved with two bones, and the
    /// stride is measured in metres of ground rather than by a timer — so walking, climbing a grade and slipping all
    /// look different without a single animation clip. Fourteen transforms, no skeleton and no skinning.</summary>
    public sealed class PuppetFigure : MonoBehaviour
    {
        Puppet owner;
        Transform root, body, head;
        Transform shorts;
        /// <summary>0…1 of "in the air with the legs tucked and the arms up". See where it is updated.</summary>
        float airPose;
        readonly Transform[] sleeve = new Transform[2], sock = new Transform[2];
        readonly Transform[] armUp = new Transform[2], armLow = new Transform[2], hand = new Transform[2];
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], boot = new Transform[2];
        Renderer[] parts = System.Array.Empty<Renderer>();
        bool visible = true;

        // ─── the figure, in metres ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The hover height these numbers were drawn against. Everything below is a length at that hover, and
        /// the tuning's own hover scales the lot (see <see cref="LateUpdate"/>): the pelvis has to sit where the
        /// physics holds it or the legs reach past the ground, and one ratio keeps the whole body agreeing with it.</summary>
        const float HoverBuilt = .95f;
        /// <summary>Pelvis above the ground with the capsule at rest, and the head's centre above the pelvis. They add
        /// up with the head's own half-height (0.20) to 1.62 m of climber — <see cref="PuppetTuning.StandHeight"/>.
        /// The old figure was 1.79 and read as a man on stilts; the 17 cm came almost entirely out of the legs, which
        /// is why the pelvis dropped 13 cm and the trunk only 4.</summary>
        const float PelvisRide = .78f, HeadRise = .64f;
        /// <summary>Bone lengths, metres — and they must match the meshes turned in <see cref="PuppetSkin"/>, because
        /// a bone is drawn at its built length and only stretches under protest. The two legs add up to 0.73 against a
        /// hip that stands 0.711 above the sole: a standing climber therefore carries about 9 cm of knee, and that
        /// bend is not slack — it is the headroom every long step is taken out of. Straighten the standing leg and the
        /// foot can go no more than 10 cm from under the hip without the leg tearing off it.</summary>
        // Shorter than the drop from pelvis to sole on purpose. The capsule rides 7 cm below its nominal hover under
        // its own weight, and a leg cut to the nominal height has to eat that in a permanently bent knee — which is
        // what made a standing figure look like it was crouching to jump. Cut to the height the body actually stands
        // at and the knee keeps only the soft bend a person has.
        const float ThighLen = .352f, ShinLen = .334f, UpperArm = .26f, Forearm = .24f;
        /// <summary>Where the boot's origin sits above the ground it is standing on. The mesh hangs 6 cm below its own
        /// origin, so the sole ends up a centimetre into the ground — deliberately, because a sole exactly on a probed
        /// plane shows daylight under it on every ridge and stone.</summary>
        const float SoleUp = .05f;
        /// <summary>Hip joints, the line the feet walk on, and the shoulders. The feet are set narrower than the hips:
        /// people walk very nearly in one track, and legs dropped straight down off the hips read as a waddle.</summary>
        const float HipOut = .12f, HipSag = .019f, FootOut = .10f, ShoulderOut = .28f, ShoulderUp = .414f;

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
        /// <summary>Metres the pelvis drops into each footfall. It used to be a SHARE of the stride, which was
        /// harmless at the old scurrying cycle and became a 23 cm pogo once the stride grew to two metres — the
        /// shaking the playtest called judder. A person's pelvis moves a few centimetres however long the step is.</summary>
        const float BobShare = .045f;
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
        const float HeelStrike = 13f, ToeOff = 22f, ToePivot = .12f, HeelPivot = .05f;

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
            // worn over the top, each its own piece with its own colour: a sleeve with a cuff, shorts with a hem,
            // socks with a roll. Painted geometry would have no edge, and the edge is what says "clothing".
            shorts = Piece(PuppetSkin.Shorts, "Shorts", PuppetSkin.ShortsCloth);
            for (int s = 0; s < 2; s++)
            {
                sleeve[s] = Piece(PuppetSkin.Sleeve, "Sleeve" + s, PuppetSkin.ShirtCloth);
                sock[s] = Piece(PuppetSkin.Sock, "Sock" + s, PuppetSkin.SockCloth);
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

        Transform Piece(Mesh mesh, string name) => Piece(mesh, name, PuppetSkin.Skin);

        Transform Piece(Mesh mesh, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
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
            float thighLen = ThighLen * scale, shinLen = ShinLen * scale;
            float legLen = thighLen + shinLen, legMax = legLen * .985f;
            float upperArm = UpperArm * scale, forearm = Forearm * scale;
            float hipDrop = t.HoverHeight - PelvisRide * scale;   // pelvis below the capsule's centre

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
            float wantAir = owner.Limp || owner.Grounded ? 0f : Mathf.Clamp01(rb.GetComponent<Rigidbody>() != null ? owner.Torso.linearVelocity.y / 2.6f : 0f);
            airPose = wantAir > airPose ? wantAir : Mathf.MoveTowards(airPose, owner.Grounded || owner.Limp ? 0f : wantAir, dt / .34f);

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
            shorts.SetPositionAndRotation(hipPoint, pose);
            // no neck: the head sits straight on the shoulders, the way it does in PEAK. The hat is part of it.
            // the head keeps some of the lean and rights itself against the rest, the way a person's does
            head.SetPositionAndRotation(hipPoint + pose * new Vector3(0f, HeadRise * scale, 0f),
                owner.Limp ? rb.rotation : look * Quaternion.Slerp(Quaternion.identity, tilt, .35f));

            // ── legs ───────────────────────────────────────────────────────────────────────────────────────────────
            var dirWorld = look * new Vector3(step.x, 0f, step.y);        // where the feet are going, in the world
            var soleBase = groundPoint + Vector3.up * (SoleUp * scale);

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
                var hipJoint = hipPoint + pose * new Vector3(sign * HipOut * scale, -HipSag * scale, 0f);
                var track = level * new Vector3(sign * FootOut * scale, 0f, 0f);   // this foot's line on the ground
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

                var knee = Limb(thigh[s], shin[s], hipJoint, foot, thighLen, shinLen, pose * Vector3.forward, ThighLen, ShinLen);
                // the sock stands on the boot and points at the knee, so it stays on the shin at any bend
                var up = knee - foot;
                sock[s].SetPositionAndRotation(foot + Vector3.up * (.055f * scale),
                    up.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(Vector3.up, up.normalized) : pose);

                // The boot turns out into a side-step — hard on the leading foot, barely on the trailing one — and
                // rolls heel to toe through the stance. It is pitched about the part of the sole that is on the
                // ground, so the toe never goes through it.
                float yaw = afoot ? gait * lateral * (sign * lateral > 0f ? 26f : 12f) + sign * 5f : 0f;
                var flat = afoot ? level * Quaternion.AngleAxis(yaw, Vector3.up) : pose;
                var pivot = new Vector3(0f, 0f, (pitch > 0f ? ToePivot : -HeelPivot) * scale);
                var bootRot = flat * Quaternion.Euler(pitch, 0f, 0f);
                boot[s].SetPositionAndRotation(
                    foot + flat * (pivot + new Vector3(0f, 0f, .015f * scale)) - bootRot * pivot, bootRot);
                last[s] = foot;
            }

            // ── arms ───────────────────────────────────────────────────────────────────────────────────────────────
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var shoulder = hipPoint + pose * new Vector3(sign * ShoulderOut * scale, ShoulderUp * scale, .01f);
                var physical = s == 0 ? owner.Left : owner.Right;
                Vector3 wrist;
                if (owner.HandsEnabled && physical != null) wrist = physical.transform.position;
                else
                {
                    // An arm answers the leg on its own side: forward when that leg is back, on the same clock. The
                    // swing is the drift, not the pace — walking backwards swings it the other way, and a side-step
                    // swings the arms across the body and pushes both hands out a little to balance it.
                    float q = Frac(phase + (s == 0 ? 0f : .5f));
                    float drive = -Mathf.Cos(q * Mathf.PI * 2f) * Mathf.Lerp(.08f, .26f, stride) * gait;
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
                            sign * .30f * scale, (upperArm + forearm) * .72f, .02f * scale), lift);
                }
                var elbow = Limb(armUp[s], armLow[s], shoulder, wrist, upperArm, forearm, pose * Vector3.back, UpperArm, Forearm);
                hand[s].SetPositionAndRotation(wrist, Wrist(elbow, wrist, pose));
                // the sleeve sits on the shoulder and runs down the upper arm — same line, its own length
                sleeve[s].SetPositionAndRotation(armUp[s].position, armUp[s].rotation);
            }
            posed = true;
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
                            Vector3 bendToward, float builtA, float builtB)
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
            Bone(upper, from, joint, builtA);
            Bone(lower, joint, to, builtB);
            return joint;
        }

        /// <summary>Puts a bone mesh on the line from <paramref name="from"/> to <paramref name="to"/>. The mesh is
        /// turned at its built length and the joint IK hands over very nearly that, so normally there is next to no
        /// scaling at all and the rounded ends keep their shape. Only a limb scaled to another stature, or the last
        /// bone of an over-reaching arm, stretches — and a little squash-and-stretch there reads better than a gap at
        /// the wrist.</summary>
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
