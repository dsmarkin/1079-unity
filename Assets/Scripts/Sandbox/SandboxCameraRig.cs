using UnityEngine;
using Height1079.Puppet;
using Height1079.Snow;

namespace Height1079.Sandbox
{
    /// <summary>The eye, and the ears that hang off it.
    ///
    /// The sandbox used to watch the body from outside, which is a fine way to read a leg spring and a bad way to
    /// judge whether walking feels like anything. The game is played from inside the head, so this is too: first
    /// person by default, V (or F7, or F3) to step out behind the body and back.
    ///
    /// The numbers here are copied out of the game's own camera rather than shared with it — the sandbox assembly
    /// cannot see the game, and that separation is the point of the sandbox. Head heave, roll and cadence are the
    /// same figures the hiker walks with. The depth of the snow comes from the sandbox's own ground
    /// (<see cref="SandboxTerrainSnow.SinkAt"/>); what the boots leave in it, and how a trodden path is shallower
    /// than virgin snow, is the game's own <see cref="SnowPrints"/>, the same module the hiker's trail uses.
    ///
    /// The ear rides in the head (the listener is on the camera) but the boots do not: the footfalls come out of
    /// <see cref="SandboxSoundSnow"/>, which sits at the feet and follows them. This class decides <em>when</em> a
    /// foot lands and what it lands on; what that sounds like is all over there.</summary>
    public sealed class SandboxCameraRig : MonoBehaviour
    {
        /// <summary>From the middle of the floating capsule up to the eyes. A constant, not a share of
        /// <c>TorsoHeight</c>: the figure's bones are constants too, so the head does not move when the capsule is
        /// retuned. With the default hover this puts the eye at about 1.65 m over the snow — the hiker's 1.72 m less
        /// the fact that this body stands in it rather than on it.</summary>
        const float EyeUp = .60f, EyeForward = .10f;

        public Camera Cam;
        /// <summary>First person is the default, because the game's is.</summary>
        public bool FirstPerson = true;
        /// <summary>Head bob. On, because it is the single thing that makes walking read as walking — and switchable,
        /// because it also hides small errors in the leg spring that the sandbox exists to find.</summary>
        public bool Bob = true;
        public float Yaw, Pitch = 8f, Orbit = 4.5f;

        Puppet.Puppet body;
        PuppetFigure figure;
        float impact, limpBlend, stepPhase;
        bool snap = true;
        int lastHalf = int.MinValue, foot;

        /// <summary>The boots. Their own object at the feet, not a source on the camera — see
        /// <see cref="SandboxSoundSnow"/>.</summary>
        SandboxSoundSnow boots;
        /// <summary>When the last footfall was heard, in unscaled seconds, and whether the body is currently taking
        /// steps at all. Together they are what stops the snow stuttering: see <see cref="Step"/>.</summary>
        float lastFall = -99f;
        bool striding;
        /// <summary>How far the trodden path under the body stands above the collider, eased. See <see cref="Aim"/>.</summary>
        float lift;

        /// <summary>Two footfalls closer together than this are never two steps. They are the stride phase jumping —
        /// a frame lost to a hitch, the body picked up and put down somewhere else, the time scale changed — and the
        /// sound of that is a rattle. Well under the gap between two real steps at a run, so it never sets cadence;
        /// cadence belongs to the gait.</summary>
        const float MinGap = .16f;
        /// <summary>Speed at which the body is walking, and the lower speed at which it has stopped. Two numbers and
        /// not one: with a single threshold a body drifting along at exactly that speed flickers in and out of
        /// walking and plays a step every other frame.</summary>
        const float StrideOn = .60f, StrideOff = .35f;

        public Quaternion LookRotation => Quaternion.Euler(Pitch, Yaw, 0f);

        public void Setup(Camera cam)
        {
            Cam = cam;
            // Under this component's own object, which is the sandbox root: it never moves, so putting the boots in
            // world space every frame is a plain assignment, and it dies with the sandbox when the game is returned to.
            boots = SandboxSoundSnow.Create(transform);
        }

        /// <summary>The body is thrown away and rebuilt whenever a number changes, so the eye has to be told.</summary>
        public void Bind(Puppet.Puppet p)
        {
            if (body != null) body.Landed -= OnLanded;
            body = p;
            figure = p != null ? p.GetComponent<PuppetFigure>() : null;
            if (body != null) body.Landed += OnLanded;
            stepPhase = 0f; impact = 0f; limpBlend = 0f; lastHalf = int.MinValue; lift = 0f;
            striding = false; lastFall = -99f;
            snap = true;
            ShowFigure();
        }

        void OnDestroy() { if (body != null) body.Landed -= OnLanded; }

        void OnLanded(float speed)
        {
            // only a landing worth feeling: stepping off a kerb must not shake the picture
            impact = Mathf.Max(impact, Mathf.Clamp01((speed - 4f) / 12f));

            // ...and only a landing worth hearing. An arrival off a jump or a ledge is a footfall with the whole
            // body behind it rather than half of it, so it is the same two layers weighing more. It shares the gap
            // with the stride, or a landing at the end of a run doubles up with the step that was already due.
            if (boots == null || body == null || body.Torso == null) return;
            if (speed < 2.2f || Time.unscaledTime - lastFall < MinGap) return;
            lastFall = Time.unscaledTime;
            bool found = Physics.Raycast(body.Torso.position, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore);
            bool hard = found && HardGround(hit.collider);
            // both feet arrive at once: neither of the two boot voices, so it is given the one that did not just step
            boots.Footfall(Mathf.Min(speed, 4f), found && !hard ? Packed(hit.point, out _) : 0f, hard, 1 - foot,
                           Mathf.Clamp(speed / 3f, 1f, 2f));
        }

        public void SetView(bool first)
        {
            if (FirstPerson == first) return;
            FirstPerson = first;
            snap = true;
            ShowFigure();
        }

        public void Toggle() => SetView(!FirstPerson);

        /// <summary>The body was picked up and put somewhere else: be there too, instead of sailing across the range
        /// to catch up with it.</summary>
        public void Snap() { snap = true; lastHalf = int.MinValue; }

        /// <summary>Your own figure must not be in your own eye. <c>PuppetFigure.SetVisible</c> turns the drawn body
        /// off and leaves the physics alone, the way the game hides its "Visual" child in first person.</summary>
        void ShowFigure()
        {
            if (figure == null && body != null) figure = body.GetComponent<PuppetFigure>();
            figure?.SetVisible(!FirstPerson);
        }

        public void Look(Vector2 delta)
        {
            Yaw += delta.x * .12f;
            Pitch = Mathf.Clamp(Pitch - delta.y * .12f, -80f, 80f);
        }

        public void Zoom(float scroll) => Orbit = Mathf.Clamp(Orbit - scroll * .004f, 1.5f, 14f);

        /// <summary>Called from <c>LateUpdate</c>, after the physics has been drawn where it will be seen: a camera
        /// placed in <c>Update</c> is always a frame behind the body and the whole picture shivers.</summary>
        public void Aim()
        {
            if (Cam == null || body == null || body.Torso == null) return;
            var torso = body.Torso.transform;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            impact = Mathf.MoveTowards(impact, 0f, dt * 1.4f);
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            // the stride is counted whichever way the body is being watched: boots and footprints belong to the body,
            // not to the camera, and going round behind it must not put the snow on mute
            var v = body.Torso.linearVelocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            stepPhase += Time.deltaTime * Mathf.Clamp(speed / .85f, 0f, 2.6f) * Mathf.PI;
            if (stepPhase > 2048f) stepPhase -= 2048f;
            // How deep the snow is under the body, and how much of it is still there to sink into. The trail map —
            // the same one the game's hiker reads — says how many boots have been through this cell, and a path
            // walked three times keeps only a third of the depth. The rest is given to the physics (Puppet.GroundLift)
            // and not to the picture: the legs then hold the body that much higher off the collider, so the feet,
            // the hands and the eye all stand on the path together. Eased, the way the game eases its sink.
            var ground = torso.position + Vector3.down * (float.IsInfinity(body.GroundDistance) ? body.Tuning.HoverHeight : body.GroundDistance + Mathf.Max(0f, body.GroundLift));
            float sunk = Packed(ground, out float virgin);
            lift = Mathf.Lerp(lift, virgin - sunk, 1f - Mathf.Exp(-6f * dt));
            body.GroundLift = lift;
            float deep = virgin - lift;
            // the boots go where the boots are, in both views: the ear is in the head and the sound is a metre below
            // it, and that metre is the only thing saying the noise is yours and is underneath you
            boots?.Follow(torso.position + Vector3.down * body.Tuning.HoverHeight);
            Step(torso, speed, deep);

            if (!FirstPerson)
            {
                var pivot = torso.position + Vector3.up * .3f;
                var want = pivot - rot * Vector3.forward * Orbit;
                if (Physics.SphereCast(pivot, .2f, (want - pivot).normalized, out var hit, Orbit, ~0, QueryTriggerInteraction.Ignore))
                    want = pivot + (want - pivot).normalized * Mathf.Max(.8f, hit.distance - .1f);
                Cam.transform.position = snap ? want : Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-16f * dt));
                Cam.transform.LookAt(pivot);
                snap = false;
                return;
            }

            // the depth of the snow only makes the stride heavier: the footing mesh has already put the body down
            // into it, and taking it off the eye as well would sink the head twice
            var gaitRot = Gait(speed, deep, out float heave, out Vector3 drift);

            // a landing shakes the picture
            float jolt = impact * impact * 9f;
            if (jolt > .01f)
                rot *= Quaternion.Euler((Mathf.PerlinNoise(Time.time * 23f, 1f) - .5f) * jolt,
                                        (Mathf.PerlinNoise(Time.time * 19f, 7f) - .5f) * jolt,
                                        (Mathf.PerlinNoise(Time.time * 17f, 3f) - .5f) * jolt * 1.5f);
            rot *= gaitRot;
            // a body gone over goes over with the view; a stagger takes a little under half of the same tilt. Without
            // this a fall reads as the world tipping while your head stays politely level.
            float tilt = body.Limp ? 1f : body.Stumbling ? .45f : 0f;
            limpBlend = Mathf.MoveTowards(limpBlend, tilt, dt * (tilt > limpBlend ? 6f : 2.5f));
            if (limpBlend > .001f)
                rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, torso.up), limpBlend) * rot;

            var eye = torso.position + Vector3.up * EyeUp
                    + Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized * EyeForward;
            Cam.transform.SetPositionAndRotation(eye + Vector3.up * heave + drift, rot);
            snap = false;
        }

        /// <summary>How the head moves with the stride. Every stride on foot is a leg pulled out of a hole and put
        /// back into one: the head heaves, rolls, and the deeper the snow the heavier it gets. Same shape and same
        /// numbers as the game's gait, so a body tuned here feels the same when it is walked over there.</summary>
        Quaternion Gait(float speed, float snow, out float heave, out Vector3 drift)
        {
            heave = 0f; drift = Vector3.zero;
            if (!Bob) return Quaternion.identity;
            // the knees are folded from a landing: the torso is already being pulled down by the leg spring, and a
            // man absorbing a drop does not also bounce his head at walking pace
            float folded = Mathf.Clamp01(body.Crouch / Mathf.Max(body.Tuning.SquashMax, .01f));
            float drive = Mathf.Clamp01(speed / 1.4f) * (1f - folded);
            if (drive < .01f || !body.Grounded) return Quaternion.identity;
            float wave = Mathf.Sin(stepPhase);
            float deep = Mathf.Clamp01(snow / .45f);
            heave = -Mathf.Abs(wave) * Mathf.Lerp(.012f, .06f, deep) * drive;
            drift = body.Torso.transform.forward * (wave * .012f * drive);
            return Quaternion.Euler(Mathf.Sin(stepPhase * 2f) * Mathf.Lerp(.8f, 2.2f, deep) * drive,
                                    wave * .8f * drive,
                                    wave * Mathf.Lerp(1f, 3.4f, deep) * drive);
        }

        // ── boots on snow ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One footfall per half turn of the gait: the sound, and the mark it leaves. Both hang off the same
        /// phase that moves the head, so what you hear and what you see are the same step.
        ///
        /// What is guarded here is that each step fires exactly once and that a body which is not walking is silent.
        /// A footfall out of a body that is standing still, sliding down a slab, lying in the snow or hanging off a
        /// hand is the single loudest way a walk gives itself away as a machine — louder than any fault in the sound
        /// itself, because a sound in the wrong place cannot be mistaken for anything else.</summary>
        void Step(Transform torso, float speed, float snow)
        {
            // a walk starts at StrideOn and does not stop until StrideOff: a body loitering at one threshold would
            // otherwise cross it several times a second and chatter
            striding = striding ? speed > StrideOff : speed > StrideOn;
            if (!striding || !body.Grounded || body.Sliding || body.Limp || body.Hanging)
            {
                // and the stride is forgotten, so that starting to walk again lands a step at once instead of
                // waiting out the remainder of a half turn the body never took
                lastHalf = int.MinValue;
                return;
            }

            int half = Mathf.FloorToInt(stepPhase / Mathf.PI);
            if (half == lastHalf) return;
            // more than one half turn can pass in a frame (a hitch, a low frame rate, a run): that is still one step,
            // not one per turn missed
            lastHalf = half;
            if (Time.unscaledTime - lastFall < MinGap) return;
            lastFall = Time.unscaledTime;
            foot = 1 - foot;

            var ahead = Vector3.ProjectOnPlane(torso.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-4f) ahead = Vector3.forward;
            ahead.Normalize();
            var side = Vector3.Cross(Vector3.up, ahead) * (foot == 0 ? -.11f : .11f);
            // started just under the capsule, so the body's own hands — which hang there whether or not they are
            // switched on — cannot be mistaken for the ground
            var from = torso.position + side + ahead * .18f + Vector3.down * (body.Tuning.TorsoHeight * .5f + .05f);
            bool found = Physics.Raycast(from, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore);

            // the ray is the better answer — it knows which of the two boots is where, and what that boot is standing
            // on — but a step that finds nothing under it is still a step, so the gait's own reading of the snow
            // stands in for it rather than the sound dropping out
            bool hard = found && HardGround(hit.collider);
            float virgin = 0f;
            float sink = !found ? snow : hard ? 0f : Packed(hit.point, out virgin);
            boots?.Footfall(speed, sink, hard, foot);

            if (!found || hard) return;
            // The mark belongs on the snow you can see, not on the collider the legs actually found under it — the
            // print is the picture of the hole. Same call the hiker's trail makes, so it is the same print: a boot
            // every step, a trodden patch every other one, a puff of snow, and one more pass on the trail map.
            SnowPrints.Instance?.Step(hit.point + Vector3.up * virgin, ahead, hit.normal, virgin, foot == 0);
        }

        /// <summary>How deep a boot goes at a point found on a collider: the snow lying over that collider
        /// (<paramref name="virgin"/>, the whole of it), less what has already been trodden there.</summary>
        static float Packed(Vector3 on, out float virgin)
        {
            virgin = SandboxTerrainSnow.SinkAt(on);
            var prints = SnowPrints.Instance;
            return prints != null ? prints.Sink(on.x, on.z, virgin) : virgin;
        }

        /// <summary>Stone, ice or a ledge under the boot instead of snow — drier and shorter, and no squeak, because
        /// nothing is packing. The range paints what a thing is made of and the name of that material is the only
        /// label the sandbox has: the colliders here carry no tags, and the snow field's footing mesh has no renderer
        /// at all, which is itself the answer for the one surface that matters most.</summary>
        static bool HardGround(Collider c)
        {
            if (c == null) return false;
            var r = c.GetComponent<Renderer>();
            var m = r != null ? r.sharedMaterial : null;
            if (m == null) return false;
            // Unity appends " (Instance)" to a material it has copied, so match the front of the name
            string n = m.name;
            return n.StartsWith("rock") || n.StartsWith("ice") || n.StartsWith("ledge") || n.StartsWith("mark");
        }
    }
}
