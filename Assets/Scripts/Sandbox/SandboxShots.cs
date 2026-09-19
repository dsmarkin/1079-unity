using System.Collections;
using System.IO;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>`1079-sandbox -shot -shotdir &lt;path&gt;` — puts the body in a few telling poses, photographs each one
    /// and quits. The point is to be able to look at the figure without anybody having to sit at the machine:
    /// a build, four pictures, done.</summary>
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
            boot.Scripted = true;
            SandboxHud.Panel = false;
            Directory.CreateDirectory(Folder);
            var body = boot.Body;
            yield return new WaitForSeconds(1f);

            boot.GoTo(0);
            yield return new WaitForSeconds(1.5f);
            yield return Shoot(boot, body, "1-стоит", new Vector3(2.6f, .4f, 2.2f));

            // caught mid-stride, from the side
            float t = 0f;
            while (t < 1.6f) { body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity }); t += Time.deltaTime; Frame(boot, body, new Vector3(3.2f, .3f, 0f)); yield return null; }
            yield return Shoot(boot, body, "2-шаг", new Vector3(3.2f, .3f, 0f), keepDriving: true);
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

            Debug.Log("shots: папка " + Folder);
            yield return new WaitForSeconds(.4f);
            Application.Quit(0);
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
