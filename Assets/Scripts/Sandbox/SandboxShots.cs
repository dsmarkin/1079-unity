using System.Collections;
using System.IO;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>`1079-sandbox -shot -shotdir &lt;path&gt;` — puts the body in a few telling poses, photographs each one
    /// and quits. The point is to be able to look at the figure without anybody having to sit at the machine:
    /// a build, six pictures, done.
    ///
    /// The drift frames are the ones worth sending to somebody who asked what the sandbox looks like now: the view out
    /// of the body's own eyes — standing, with the hands at rest, and mid-stride — and what the drifts do to a man
    /// walking through them.</summary>
    public sealed class SandboxShots : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-shot") return true;
                return false;
            }
        }

        static string Folder
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-shotdir") return args[i + 1];
                return Application.persistentDataPath;
            }
        }

        IEnumerator Start()
        {
            var boot = SandboxBoot.Instance;
            // Scripted stops the keyboard driving the body and, just as importantly, stops the camera rig placing
            // itself: every frame below is framed by hand and has to stay framed until it is read out.
            boot.Scripted = true;
            SandboxHud.Panel = false;
            Directory.CreateDirectory(Folder);
            var body = boot.Body;
            // the first four are of the figure, so the figure has to be drawn
            boot.FirstPerson = false;
            yield return new WaitForSeconds(1f);

            boot.GoTo(0);
            yield return new WaitForSeconds(1.5f);
            yield return Shoot(boot, body, "1-стоит", new Vector3(2.6f, .4f, 2.2f));

            // caught mid-stride, from the side
            float t = 0f;
            while (t < 1.6f) { body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity }); t += Time.deltaTime; Frame(boot, body, new Vector3(3.2f, .3f, 0f)); yield return null; }
            yield return Shoot(boot, body, "2-шаг", new Vector3(3.2f, .3f, 0f), keepDriving: true);
            body.Drive(PuppetInput.Idle);

            // and both again from dead ahead, low: the width of the stance, the turn-out of the boots and where
            // each foot actually lands only show from the front. From the side every gait is one line.
            yield return new WaitForSeconds(1.2f);
            yield return Shoot(boot, body, "8-стоит-спереди", new Vector3(0f, .1f, 3.0f));
            Debug.Log($"shots: стопы {Stance(body):0.00} м врозь стоя");
            // and from behind: the pack is the one thing on the body no other frame shows
            yield return Shoot(boot, body, "10-стоит-сзади", new Vector3(.6f, .3f, -2.8f));
            // walking back south, toward the camera: north of here is the ramp stand, and a camera three metres
            // ahead of a body walking north ends up inside it
            var south = Quaternion.Euler(0f, 180f, 0f);
            var fromSouth = new Vector3(-.4f, .1f, -3.2f);
            t = 0f;
            while (t < 1.9f) { body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = south }); t += Time.deltaTime; Frame(boot, body, fromSouth); yield return null; }
            yield return Shoot(boot, body, "9-шаг-спереди", fromSouth, keepDriving: true);
            Debug.Log($"shots: стопы {Stance(body):0.00} м врозь в шаге");
            body.Drive(PuppetInput.Idle);

            // walking up the thirty-degree ramp
            body.Place(SandboxRange.Ramps[1].Foot);
            yield return new WaitForSeconds(1f);
            t = 0f;
            while (t < 2f) { body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity }); t += Time.deltaTime; Frame(boot, body, new Vector3(3.4f, .6f, .8f)); yield return null; }
            yield return Shoot(boot, body, "3-склон-30", new Vector3(3.4f, .6f, .8f), keepDriving: true);
            body.Drive(PuppetInput.Idle);

            // sliding off something too steep
            body.Place(SandboxRange.Ramps[5].Middle);
            t = 0f;
            // shot from the foot of the ramp: from the side the camera ends up inside the next one
            var from60 = new Vector3(1.2f, 1.2f, -4.2f);
            while (t < .9f) { body.Drive(PuppetInput.Idle); Frame(boot, body, from60); t += Time.deltaTime; yield return null; }
            yield return Shoot(boot, body, "4-соскальзывание", from60);

            // ── the drifts, from inside the head and from outside it ──────────────────────────────────────────────
            // one walk north from the foot of the drift stand crosses two of them; the eye is shot deep in the first
            boot.GoTo(SandboxRange.DriftStand);
            boot.FirstPerson = true;
            boot.Eye.Yaw = 0f; boot.Eye.Pitch = 4f;
            yield return new WaitForSeconds(1f);
            // standing first: the hands at rest, without the swing, is where their pose and symmetry can be read
            yield return ShootHere(boot, body, "5a-от-первого-лица-стоя");
            yield return Walk(boot, body, 4.2f);
            yield return ShootHere(boot, body, "5-от-первого-лица", keepDriving: true);

            // and the same moment from the side, where the snow is visibly over the boots
            boot.FirstPerson = false;
            var fromSide = new Vector3(5.2f, 1.3f, -1.4f);
            yield return Walk(boot, body, 1.4f, fromSide);
            yield return Shoot(boot, body, "6-сугробы", fromSide, keepDriving: true);
            body.Drive(PuppetInput.Idle);

            // caught at the top of a jump: the pose in the air is the whole point of the frame
            boot.GoTo(0);
            yield return new WaitForSeconds(1.2f);
            var jumpFrom = new Vector3(2.8f, .5f, 2.4f);
            float best = float.NegativeInfinity;
            body.Drive(new PuppetInput { Look = Quaternion.identity, Jump = true });
            yield return null;
            for (float w = 0f; w < 1.2f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = Quaternion.identity });
                Frame(boot, body, jumpFrom);
                if (body.Torso.linearVelocity.y < 1.2f && body.Torso.position.y > best) break;
                best = body.Torso.position.y;
                yield return null;
            }
            yield return Shoot(boot, body, "7-прыжок", jumpFrom);

            // the two corners of the screen, from inside the head, with something on the bar to look at: an hour's
            // worth of hunger and cold, a little sleep, a bar of chocolate eaten
            boot.GoTo(0);
            boot.FirstPerson = true;
            var v = boot.Vitals.Condition;
            v.Add(Height1079.Core.Bite.Hunger, 22f); v.Add(Height1079.Core.Bite.Cold, 14f); v.Add(Height1079.Core.Bite.Sleep, 6f);
            body.Extra = 25f;
            yield return new WaitForSeconds(1f);
            yield return ShootHere(boot, body, "11-полоска-и-слоты");

            // the flashlight — the game's own — in the hand and lit, from inside the head and from outside, where
            // the tube has to sit in the drawn fist rather than float beside it
            boot.Gear.Select(0);
            boot.Gear.ToggleTorch();
            yield return new WaitForSeconds(.6f);
            yield return ShootHere(boot, body, "12-фонарик-в-руке");
            boot.FirstPerson = false;
            yield return new WaitForSeconds(.5f);
            yield return Shoot(boot, body, "12a-фонарик-со-стороны", new Vector3(1.4f, .3f, 2.2f));
            boot.FirstPerson = true;
            // the rucksack window over the slots
            boot.TogglePack();
            yield return new WaitForSeconds(.5f);
            yield return ShootHere(boot, body, "13-рюкзак");
            boot.TogglePack();
            // the end of the day: the body down, the screen dark, the words and the button up
            boot.Die();
            yield return new WaitForSeconds(3.4f);
            yield return ShootHere(boot, body, "14-конец-дня");

            // ── the yard at night: the fire, the Menk in the beam, the wall of the blizzard ─────────────────────
            // a short run, so the front arrives while the camera waits; the night takes four seconds to come down
            boot.Hunt.Begin(90f);
            boot.FirstPerson = true;
            yield return new WaitForSeconds(5f);
            body = boot.Body;           // Begin() puts a fresh body at the fire
            // from the start, looking back at the fire — the start and the finish, the one thing to show
            var toFire = SandboxHuntYard.Fire - body.Torso.position; toFire.y = 0f;
            boot.Eye.Yaw = Quaternion.LookRotation(toFire).eulerAngles.y; boot.Eye.Pitch = 12f;
            if (!boot.Hunt.Torch) boot.Hunt.ToggleTorch();
            for (float w = 0f; w < .5f; w += Time.deltaTime) { boot.AimCamera(); yield return null; }
            yield return ShootHere(boot, body, "15-ночь-костёр");
            // and the yard ahead: the way to the tent, torch on
            boot.Eye.Yaw = 0f; boot.Eye.Pitch = 2f;
            for (float w = 0f; w < .3f; w += Time.deltaTime) { boot.AimCamera(); yield return null; }
            yield return ShootHere(boot, body, "15a-ночь-к-палатке");
            // the creature in the beam: the body stands eight metres in front of it, the eye and the torch on it
            var menk = boot.Hunt.Menk;
            {
                var at = new Vector3(menk.X, 0f, menk.Z);
                var ahead = Quaternion.Euler(0f, menk.Yaw, 0f) * Vector3.forward;
                var standAt = at + ahead * 8f + Vector3.Cross(Vector3.up, ahead) * 1.5f;
                boot.PlaceAt(new Vector3(standAt.x, 1.2f, standAt.z));
                var look = at + Vector3.up * 2.4f - new Vector3(standAt.x, 1.6f, standAt.z);
                boot.Eye.Yaw = Quaternion.LookRotation(look).eulerAngles.y; boot.Eye.Pitch = -Mathf.Asin(look.normalized.y) * Mathf.Rad2Deg;
                for (float w = 0f; w < .8f; w += Time.deltaTime) { boot.AimCamera(); yield return null; }
            }
            yield return ShootHere(boot, body, "16-ночь-менк");
            // the second section: the labaz, from six metres with the torch on it
            {
                var lab = SandboxHuntYard.Labaz;
                var from = lab + (SandboxHuntYard.Fire - lab).normalized * 6f;
                boot.PlaceAt(new Vector3(from.x, 1.2f, from.z));
                var look = lab + Vector3.up * .4f - new Vector3(from.x, 1.6f, from.z);
                boot.Eye.Yaw = Quaternion.LookRotation(look).eulerAngles.y; boot.Eye.Pitch = -Mathf.Asin(look.normalized.y) * Mathf.Rad2Deg;
                for (float w = 0f; w < .8f; w += Time.deltaTime) { boot.AimCamera(); yield return null; }
            }
            yield return ShootHere(boot, body, "16a-ночь-лабаз");
            // the wall: the clock jumps to the last minute and the front builds
            boot.Hunt.Skip(75f);
            boot.PlaceAt(SandboxHuntYard.Spawn);
            for (float w = 0f; w < 12f; w += Time.deltaTime) { boot.AimCamera(); yield return null; }
            yield return ShootHere(boot, body, "17-пурга");
            boot.Hunt.End();

            // ── the yard as a drawing: the fire with the tent behind, the things by the tent, cover in the beam ──
            // a fresh run long enough that no front comes while the frames are taken; the night takes four seconds
            boot.Hunt.Begin(1200f);
            boot.FirstPerson = true;
            yield return new WaitForSeconds(5f);
            body = boot.Body;
            if (!boot.Hunt.Torch) boot.Hunt.ToggleTorch();
            // these frames are of the yard, not of the creature: it is stood in the far corner before each, the way
            // the self-test parks it, or its ring round the tent walks it through the picture
            var far = SandboxHuntYard.Origin + new Vector3(-27f, 0f, -27f);
            void Park() { var m = boot.Hunt.Menk; if (m != null) { m.X = far.x; m.Z = far.z; } }
            // from behind the fire, down on the haunches, looking north over it at the cover of the yard (the tent
            // is fifty metres off, beyond the night's twelve, so no frame has the fire and the tent together)
            Park();
            yield return YardFrame(boot, body, "18-двор-костёр-и-укрытия", SandboxHuntYard.Fire + new Vector3(2.4f, 0f, -6.5f), SandboxHuntYard.Fire + Vector3.up * .6f, .95f, low: true);
            // the mouth of the tent from the fire's side, four metres off: the things on the snow in the beam's
            // core, the canvas behind them in its skirt. The beam leaves the hand a third of a metre under the eye,
            // so the eye is aimed a little above what the beam is meant to fall on
            var tent = SandboxHuntYard.Tent;
            Park();
            yield return YardFrame(boot, body, "19-двор-вещи-у-палатки", tent + new Vector3(.6f, 0f, -5.4f), tent + new Vector3(.9f, .5f, -2.9f), .95f, low: true);
            // a boulder seven metres off and a pine twelve, in the beam alone: the fire is out of range here. The
            // creature is stood a few metres behind the boulder for it — a figure over the rock is what the frame is about
            Park();
            var boulder = SandboxHuntYard.Origin + new Vector3(3f, 0f, 8f);
            yield return YardFrame(boot, body, "20-двор-деревья-и-камни-в-луче", SandboxHuntYard.Origin + new Vector3(6f, 0f, 2f), SandboxHuntYard.Origin + new Vector3(5.5f, 2f, 12f), .95f, low: true,
                beforeShot: () => { var m = boot.Hunt.Menk; if (m != null) { m.X = boulder.x - 1.8f; m.Z = boulder.z + 4.5f; m.Yaw = 160f; } });
            // the postcard: the whole camp from behind and above the fire — the fire and its logs, the figure by
            // it, the pine, the cover, the tent at the far end. The night is brought half way back for it: at full
            // night the tent stands fifty metres into the fog and the frame is a black square
            Park();
            boot.Hunt.Night.Want = .45f;
            boot.FirstPerson = false;
            boot.PlaceAt(SandboxHuntYard.Spawn);
            for (float w = 0f; w < 3.5f; w += Time.deltaTime) { body.Drive(new PuppetInput { Look = Quaternion.identity }); yield return null; }
            boot.Cam.transform.position = SandboxHuntYard.Fire + new Vector3(5f, 3f, -9f);
            boot.Cam.transform.LookAt(SandboxHuntYard.Fire + new Vector3(-1f, .8f, 8f));
            yield return Capture(boot, body, "21-лагерь-сумерки", false);
            boot.Hunt.Night.Want = 1f;
            boot.Hunt.End();

            Debug.Log("shots: папка " + Folder);
            yield return new WaitForSeconds(.4f);
            Application.Quit(0);
        }

        /// <summary>Stand the body on the yard, facing a point, and photograph from inside the head — down on the
        /// haunches when <paramref name="low"/>, which is the eye height the yard is meant to be seen from.
        /// <paramref name="eye"/> is where the eye is taken to be, for the pitch. <paramref name="beforeShot"/> runs
        /// once the body has settled, half a second before the picture — for putting something into the frame.</summary>
        IEnumerator YardFrame(SandboxBoot boot, Puppet.Puppet body, string name, Vector3 standAt, Vector3 lookAt, float eye, bool low, System.Action beforeShot = null)
        {
            boot.PlaceAt(new Vector3(standAt.x, 1.2f, standAt.z));
            var look = lookAt - new Vector3(standAt.x, eye, standAt.z);
            var facing = Quaternion.Euler(0f, Quaternion.LookRotation(Vector3.ProjectOnPlane(look, Vector3.up)).eulerAngles.y, 0f);
            boot.Eye.Yaw = facing.eulerAngles.y; boot.Eye.Pitch = -Mathf.Asin(look.normalized.y) * Mathf.Rad2Deg;
            for (float w = 0f; w < 1.2f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = facing, Crouch = low });
                boot.AimCamera();
                yield return null;
            }
            if (beforeShot != null)
            {
                beforeShot();
                for (float w = 0f; w < .6f; w += Time.deltaTime)
                {
                    body.Drive(new PuppetInput { Look = facing, Crouch = low });
                    boot.AimCamera();
                    yield return null;
                }
            }
            yield return ShootHere(boot, body, name);
        }

        /// <summary>Walk the body north for a while, keeping the camera on it. With no offset the camera is the body's
        /// own eye and the rig places it; with one it watches from there.</summary>
        static IEnumerator Walk(SandboxBoot boot, Puppet.Puppet body, float seconds, Vector3? offset = null)
        {
            float t = 0f;
            while (t < seconds)
            {
                body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity });
                t += Time.deltaTime;
                if (offset.HasValue) Frame(boot, body, offset.Value); else boot.AimCamera();
                yield return null;
            }
        }

        /// <summary>Sideways distance between the two drawn boots, m — the width of the stance as it actually came
        /// out, for the log: a frontal photograph shows it, a number lets it be compared between builds.</summary>
        static float Stance(Puppet.Puppet body)
        {
            Transform a = null, b = null;
            foreach (var tr in body.GetComponentsInChildren<Transform>())
            {
                if (tr.name == "Boot0") a = tr; else if (tr.name == "Boot1") b = tr;
            }
            if (a == null || b == null) return float.NaN;
            var d = b.position - a.position;
            return Vector3.ProjectOnPlane(d, body.Facing * Vector3.forward).magnitude;
        }

        static void Frame(SandboxBoot boot, Puppet.Puppet body, Vector3 offset)
        {
            var at = body.Torso.position + Vector3.up * .1f;
            boot.Cam.transform.position = at + offset;
            boot.Cam.transform.LookAt(at);
        }

        IEnumerator Shoot(SandboxBoot boot, Puppet.Puppet body, string name, Vector3 offset, bool keepDriving = false)
        {
            Frame(boot, body, offset);
            return Capture(boot, body, name, keepDriving);
        }

        /// <summary>Photograph what the camera rig is already looking at — the first-person shot, where the eye is
        /// placed by the same code that places it in play.</summary>
        IEnumerator ShootHere(SandboxBoot boot, Puppet.Puppet body, string name, bool keepDriving = false)
        {
            boot.AimCamera();
            return Capture(boot, body, name, keepDriving);
        }

        IEnumerator Capture(SandboxBoot boot, Puppet.Puppet body, string name, bool keepDriving)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(Folder, name + ".png");
            // rendered by hand into a texture rather than through ScreenCapture: this project does not carry the
            // screen-capture module, and a camera render leaves the HUD out of the picture anyway
            const int w = 1280, h = 800;
            var rt = new RenderTexture(w, h, 24);
            var cam = boot.Cam;
            cam.targetTexture = rt;
            cam.Render();
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            if (keepDriving) body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity });
            Debug.Log("shots: " + path);
        }
    }
}
