using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The forecast board at the rescue base (<c>Elb_WeatherBoard</c>): today and the next two days, by the
    /// four heights that matter — the barrels, the rocks, the saddle and the summit.
    ///
    /// The slate was chalked in the editor with a board of its own so that it is a real board and not lorem ipsum;
    /// this rewrites it from the session's own seed the moment there is one, so the thing on the wall is the thing the
    /// mountain is actually going to do (<see cref="MountainDay"/>). Standing at it and pressing E opens the same
    /// board as text, because a slate two metres away in a dark room is not a thing anybody should have to squint at.
    ///
    /// <b>The board is allowed to be wrong.</b> <see cref="Forecast.MissChance"/> is a fifth at one day and a third at
    /// two, and a missed forecast is not noise — it is a perfectly plausible day drawn from a shifted seed, so the
    /// board never prints nonsense, it just prints somebody else's weather. <see cref="WeatherBoard.Missed"/> is
    /// deliberately never read here: a forecast that told you its own reliability would not be a forecast, and the
    /// flag exists for the protocol after the fact and for the tests.</summary>
    public sealed class WeatherBoards : MonoBehaviour
    {
        /// <summary>How close you have to stand to read it.</summary>
        public const float Reach = 3.5f;
        /// <summary>Days ahead on the slate itself (it is sized for two panels) and in the window.</summary>
        const int SlateDays = 1, WindowDays = 2;
        const int UiHold = -1;

        public static WeatherBoards Instance { get; private set; }
        public static bool Reading => Instance != null && Instance.open;

        readonly List<Transform> boards = new List<Transform>();
        readonly List<TextMesh> slates = new List<TextMesh>();
        Transform near;
        bool open, promptMine;
        string chalked = "";

        public static WeatherBoards Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            if (Instance == null)
            {
                var go = new GameObject("WeatherBoards");
                if (parent != null) go.transform.SetParent(parent, false);
                Instance = go.AddComponent<WeatherBoards>();
            }
            Instance.Scan(parent);
            return Instance;
        }

        void Scan(Transform parent)
        {
            boards.Clear(); slates.Clear(); chalked = "";
            var roots = new List<Transform>();
            if (parent != null) roots.Add(parent);
            else
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    roots.Add(go.transform);
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Elb_WeatherBoard" || boards.Contains(t)) continue;
                    boards.Add(t);
                    // the chalked text is a sibling of the marker inside the same prefab
                    var room = t.parent != null ? t.parent : t;
                    foreach (var text in room.GetComponentsInChildren<TextMesh>(true))
                        if (text.name == "Forecast") slates.Add(text);
                }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!Climb.On) { Destroy(gameObject); return; }
            Chalk();
            var me = Bootstrap.LocalHiker;
            var session = NightSession.Instance;
            if (me == null || !me.IsOwner || session == null)
            {
                near = null;
                if (open) Close();
                return;
            }
            near = Nearest(me.transform.position);
            if (open)
            {
                if (near == null || Controls.Pause) { Close(); return; }
                if (Controls.Board) Close();
                return;
            }
            if (near != null && Controls.Board) Open();
        }

        /// <summary>Rewrites the slate whenever the day the host published changes — which is once at the start and
        /// once after every night.</summary>
        void Chalk()
        {
            if (slates.Count == 0 || !MountainDay.Known) return;
            string text = MountainDay.BoardText(SlateDays);
            if (text == chalked) return;
            chalked = text;
            foreach (var s in slates) if (s != null) s.text = text;
            if (open) Refresh();
        }

        Transform Nearest(Vector3 from)
        {
            Transform best = null; float bd = Reach;
            for (int i = boards.Count - 1; i >= 0; i--)
            {
                if (boards[i] == null) { boards.RemoveAt(i); continue; }
                float d = Vector3.Distance(from, boards[i].position);
                if (d < bd) { bd = d; best = boards[i]; }
            }
            return best;
        }

        // ── the window ───────────────────────────────────────────────────────────────────────────────────
        static Font font;
        RectTransform panel;
        Text body;

        void Open()
        {
            open = true;
            Build();
            Refresh();
            if (panel != null) panel.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Backpacks.OpenPack = UiHold;
        }

        void Close()
        {
            open = false;
            if (panel != null) panel.gameObject.SetActive(false);
            if (Backpacks.OpenPack == UiHold) Backpacks.OpenPack = 0;
        }

        void Build()
        {
            if (panel != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("BoardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 760f, H = 600f;
            var go = new GameObject("Board", typeof(RectTransform));
            panel = go.GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false);
            panel.anchorMin = new Vector2(.5f, .5f); panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(W, H);
            panel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .09f, .96f);

            var rt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(26, 44); rt.offsetMax = new Vector2(-26, -20);
            body = rt.gameObject.AddComponent<Text>();
            body.font = font; body.fontSize = 15; body.color = new Color(.93f, .94f, .9f);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap; body.verticalOverflow = VerticalWrapMode.Overflow;
            body.supportRichText = false;

            var foot = new GameObject("Foot", typeof(RectTransform)).GetComponent<RectTransform>();
            foot.SetParent(panel, false);
            foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(.5f, 0);
            foot.anchoredPosition = new Vector2(0, 14); foot.sizeDelta = new Vector2(-52, 22);
            var f = foot.gameObject.AddComponent<Text>();
            f.font = font; f.fontSize = 13; f.color = new Color(.67f, .76f, .8f);
            f.alignment = TextAnchor.MiddleLeft;
            f.text = "Мелом, от руки. Синоптик не бог: на сутки вперёд угадывают четыре раза из пяти. E или Esc — отойти.";
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (body == null) return;
            body.text = MountainDay.Known
                ? MountainDay.BoardText(WindowDays)
                : "Доска пустая: сегодня ещё не писали.";
        }

        void LateUpdate()
        {
            if (open)
            {
                Backpacks.OpenPack = UiHold;
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            var hud = Bootstrap.Hud;
            if (hud == null) return;
            string line = open ? "Доска погоды · E или Esc — отойти"
                : near != null ? "Доска погоды ЭВПСО · E — прочитать" + (MountainDay.Known ? " · " + MountainDay.Verdict : "")
                : "";
            if (line.Length > 0) { hud.SetPrompt(line); promptMine = true; }
            else if (promptMine) { hud.SetPrompt(""); promptMine = false; }
        }
    }
}
