using UnityEngine;
using UnityEngine.UI;

namespace Height1079.Night
{
    /// <summary>Screen film under the HUD: a heavy vignette that closes the edges of vision at night, fine moving grain
    /// (the eye straining in the dark) and a cold blue cast. No post-processing package needed.</summary>
    public sealed class NightFilm : MonoBehaviour
    {
        public static NightFilm Instance { get; private set; }

        RawImage vignette, grain, tint, blow;
        float flash;
        float strength;
        float storm;

        public static NightFilm Create(Transform parent = null)
        {
            var go = new GameObject("NightFilm", typeof(Canvas), typeof(NightFilm));
            if (parent != null) go.transform.SetParent(parent, false);
            else DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // under the HUD
            return go.GetComponent<NightFilm>();
        }

        void Awake()
        {
            tint = Layer("Tint", Texture2D.whiteTexture);
            vignette = Layer("Vignette", Vignette());
            grain = Layer("Grain", Grain());
            blow = Layer("Blow", Vignette());
            blow.color = Color.clear;
            Instance = this;
            Set(0f, 0f);
        }

        RawImage Layer(string name, Texture tex)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.texture = tex; img.raycastTarget = false;
            return img;
        }

        public void Set(float night, float isStorm)
        {
            strength = night; storm = isStorm;
            if (vignette == null) return;
            vignette.color = new Color(0f, 0f, 0f, (.9f + .08f * isStorm) * night);
            // a blizzard washes the picture towards a cold grey
            tint.color = Color.Lerp(new Color(.02f, .05f, .12f, .14f * night), new Color(.35f, .4f, .48f, .12f * night), isStorm);
            grain.enabled = night > .05f;
        }

        /// <summary>A blow: the edges go dark red and the view nearly blacks out, then clears.</summary>
        public void Flash(float strength) => flash = Mathf.Max(flash, strength);

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (blow != null)
            {
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * .45f);
                blow.color = new Color(.25f, 0f, 0f, Mathf.Clamp01(flash * 1.6f));
                tint.color = new Color(tint.color.r, tint.color.g, tint.color.b, Mathf.Max(tint.color.a, flash * flash * .85f));
            }
            if (grain == null || !grain.enabled) return;
            // jump the grain every frame so it shimmers rather than sits on the screen
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7f;
            float scale = Screen.height / 256f;
            grain.uvRect = new Rect(Random.value, Random.value, scale * aspect, scale);
            grain.color = new Color(1f, 1f, 1f, Mathf.Lerp(.075f, .11f, storm) * strength);
        }

        static Texture2D Vignette()
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "vignette" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * .8f + v * v * 1.05f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1.25f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Texture2D Grain()
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "grain" };
            var px = new Color32[n * n];
            var rnd = new System.Random(59);
            for (int i = 0; i < px.Length; i++)
            {
                byte g = (byte)rnd.Next(0, 256);
                px[i] = new Color32(g, g, (byte)Mathf.Min(255, g + 12), (byte)rnd.Next(0, 256));
            }
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
