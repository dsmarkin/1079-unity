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
    /// The last two are the ones worth sending to somebody who asked what the sandbox looks like now: the view out of
    /// the body's own eyes, and what the drifts do to a man walking through them.</summary>
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
            yield return ShootHere(boot, body, "10-полоска-и-слоты");

            Debug.Log("shots: папка " + Folder);
            yield return new WaitForSeconds(.4f);
            Application.Quit(0);
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
