using UnityEngine;
using Height1079.Puppet;

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
    /// same figures the hiker walks with; what is new is that the depth of the snow comes from the sandbox's own
    /// field (<see cref="SandboxTerrainSnow.SinkAt"/>) instead of from the trail the game leaves.</summary>
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

        AudioSource ear;
        AudioClip[] crunch;
        int lastCrunch = -1;

        public Quaternion LookRotation => Quaternion.Euler(Pitch, Yaw, 0f);

        public void Setup(Camera cam)
        {
            Cam = cam;
            ear = cam.gameObject.AddComponent<AudioSource>();
            ear.playOnAwake = false;
            ear.spatialBlend = 0f;              // your own boots are not somewhere else in the room
            crunch = new AudioClip[5];
            for (int i = 0; i < crunch.Length; i++) crunch[i] = Crunch(31 + i * 7);
        }

        /// <summary>The body is thrown away and rebuilt whenever a number changes, so the eye has to be told.</summary>
        public void Bind(Puppet.Puppet p)
        {
            if (body != null) body.Landed -= OnLanded;
            body = p;
            figure = p != null ? p.GetComponent<PuppetFigure>() : null;
            if (body != null) body.Landed += OnLanded;
            stepPhase = 0f; impact = 0f; limpBlend = 0f; lastHalf = int.MinValue;
            snap = true;
            ShowFigure();
        }

        void OnDestroy() { if (body != null) body.Landed -= OnLanded; }

        void OnLanded(float speed)
        {
            // only a landing worth feeling: stepping off a kerb must not shake the picture
            impact = Mathf.Max(impact, Mathf.Clamp01((speed - 4f) / 12f));
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
            float deep = SandboxTerrainSnow.SinkAt(torso.position);
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
        /// phase that moves the head, so what you hear and what you see are the same step.</summary>
        void Step(Transform torso, float speed, float snow)
        {
            int half = Mathf.FloorToInt(stepPhase / Mathf.PI);
            if (half == lastHalf) return;
            lastHalf = half;
            if (speed < .5f || !body.Grounded) return;
            foot = 1 - foot;

            if (ear != null && crunch != null)
            {
                int k; do k = Random.Range(0, crunch.Length); while (k == lastCrunch && crunch.Length > 1);
                lastCrunch = k;
                ear.PlayOneShot(crunch[k], Mathf.Clamp(speed / 3f, .35f, 1f) * Mathf.Lerp(.35f, .6f, Mathf.Clamp01(snow / .45f)));
            }

            var ahead = Vector3.ProjectOnPlane(torso.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-4f) ahead = Vector3.forward;
            ahead.Normalize();
            var side = Vector3.Cross(Vector3.up, ahead) * (foot == 0 ? -.11f : .11f);
            // started just under the capsule, so the body's own hands — which hang there whether or not they are
            // switched on — cannot be mistaken for the ground
            var from = torso.position + side + ahead * .18f + Vector3.down * (body.Tuning.TorsoHeight * .5f + .05f);
            if (!Physics.Raycast(from, Vector3.down, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) return;
            // the mark belongs on the snow you can see, not on the lower surface the legs actually stand on
            SandboxTerrainSnow.Footprint(hit.point + Vector3.up * SandboxTerrainSnow.SinkAt(hit.point), hit.normal, ahead);
        }

        /// <summary>A boot going into cold snow: a short burst of noise chopped into grains (the chop is the squeak —
        /// smooth noise hisses, it does not crunch) over a dull thump of the weight arriving.</summary>
        static AudioClip Crunch(int seed)
        {
            const int rate = 44100, n = rate * 22 / 100;
            var data = new float[n];
            var rnd = new System.Random(seed);
            float squeak = 190f + seed % 11 * 14f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float grain = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float chop = .45f + .55f * Mathf.Abs(Mathf.Sin(t * squeak * Mathf.PI));
                data[i] = grain * Mathf.Exp(-t * 26f) * chop * .55f
                        + Mathf.Sin(t * 88f * Mathf.PI * 2f) * Mathf.Exp(-t * 42f) * .28f;
            }
            var clip = AudioClip.Create("snow-step-" + seed, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
