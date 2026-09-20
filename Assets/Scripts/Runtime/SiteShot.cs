using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Stills of the event sites, written to disk from inside the player.
    ///
    /// The site viewer (F3) already walks the camera round every place in turn; this only saves what it is
    /// looking at. Started with `-shots` and otherwise completely inert, so it cannot fire during play: an
    /// accidental frame grab in the middle of a night would cost a visible hitch.
    ///
    /// Rendered by hand into a texture rather than through ScreenCapture — the project does not carry the
    /// screen-capture module, and a camera render leaves the HUD out of the picture, which is what a picture
    /// of a place wants. The same approach as <see cref="Height1079.Sandbox.SandboxShots"/>, which proved it.
    ///
    /// Files land in Shots/ beside the player (Builds/mac), numbered in view order so they sort the way they
    /// were taken.</summary>
    public sealed class SiteShot : MonoBehaviour
    {
        public const int Width = 1600, Height = 1000;

        static bool? asked;
        /// <summary>`1079 -shots`: the only thing that turns any of this on.</summary>
        public static bool Asked
        {
            get
            {
                if (asked == null)
                {
                    asked = false;
                    foreach (var a in Environment.GetCommandLineArgs()) if (a == "-shots") { asked = true; break; }
                }
                return asked.Value;
            }
        }

        static SiteShot instance;
        static int taken;

        /// <summary>Where the pictures go: beside the player, or beside the project in the editor.</summary>
        public static string Folder()
        {
            string data = Application.dataPath;
            string root;
            if (Application.isEditor) root = Directory.GetCurrentDirectory();
            else
            {
                // …/1079.app/Contents/Resources/Data → …/Builds/mac
                var dir = new DirectoryInfo(data);
                for (int i = 0; i < 4 && dir.Parent != null; i++) dir = dir.Parent;
                root = dir.FullName;
            }
            string shots = Path.Combine(root, "Shots");
            Directory.CreateDirectory(shots);
            return shots;
        }

        /// <summary>Take one, once the camera has had a moment to arrive and the site view has settled.</summary>
        public static void Grab(string id, float delay = 1.1f)
        {
            if (!Asked || string.IsNullOrEmpty(id)) return;
            if (instance == null)
            {
                var go = new GameObject("SiteShot");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SiteShot>();
            }
            instance.StartCoroutine(instance.Shoot(id, delay));
        }

        IEnumerator Shoot(string id, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            yield return new WaitForEndOfFrame();

            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("1079 shots: нет камеры"); yield break; }

            string safe = id;
            foreach (var bad in Path.GetInvalidFileNameChars()) safe = safe.Replace(bad, '-');
            string path = Path.Combine(Folder(), $"{++taken:00}-{safe}.png");

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var was = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = was;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Debug.Log("1079 shots: " + path);
        }
    }
}
