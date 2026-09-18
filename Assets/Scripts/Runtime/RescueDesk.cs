using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Height1079.Runtime
{
    /// <summary>The desk of the Эльбрусский высокогорный поисково-спасательный отряд МЧС, inside the base on the Azau
    /// meadow (<c>Elb_RescueDesk</c>), and the SOS button that goes with it.
    ///
    /// Walk up, press E, and the same small window the hire counter uses opens with the slip: who is going, where,
    /// and by when they undertake to be back. The defaults are the ones the southern side runs on — turn round at
    /// 13:00, back in camp by 16:00 — and both can be moved, because undertaking to be back at 14:00 and undertaking
    /// to be back at 18:00 are two different decisions and the mountain charges for both.
    ///
    /// <b>What the slip buys is that somebody notices.</b> Miss the hour, plus the grace, and the duty officer starts
    /// looking. Walk past this desk and nobody ever does — and that is not the game punishing anybody, it is what
    /// «не зарегистрировался» means. The window says so in as many words, and so does the HUD, once, above the last
    /// roof.
    ///
    /// The <b>SOS</b> is the other half and it lives on a key (9) and on a button in the rucksack, because a party
    /// that needs it is not standing at a desk. Above 5 250 m there is no network on this mountain
    /// (<see cref="Rescue.SignalCeiling"/>): the call is not sent late, it is not sent at all, and the refusal says
    /// exactly how many metres of descent would change that.
    ///
    /// Everything here is a request. The host owns the paperwork, the alarm and the plan
    /// (<see cref="NightSession.RescueTick"/>); this only reads what it publishes.</summary>
    public sealed class RescueDesk : MonoBehaviour
    {
        public const float Reach = Rental.CounterReach;
        const int UiHold = -1;

        public static RescueDesk Instance { get; private set; }
        /// <summary>The slip window is up: its digits belong to it and not to the mountain.</summary>
        public static bool Filing => Instance != null && Instance.open;

        // ── what the host says about us ──────────────────────────────────────────────────────────────────

        /// <summary>The last word from the host about this client's paperwork. Written by the RPC, read by the HUD.</summary>
        public static RescueNet Mine { get; private set; }
        public static bool Known { get; private set; }

        public static void Take(RescueNet net) { Mine = net; Known = true; }
        public static void Forget() { Mine = default; Known = false; }

        /// <summary>The line the HUD shows when there is an operation, or "".
        ///
        /// <see cref="Rescue.Line"/>'s own last branch («Вы внизу») belongs to a runtime that carries a casualty off
        /// the mountain, and this one does not: what is modelled here is the <em>waiting</em>, which is the honest
        /// half. So once the rescuers are with you the line stops there and says so.</summary>
        public static string Line(float hour)
        {
            if (!Known) return "";
            var m = Mine.Mission;
            if (!m.Coming) return "";
            return m.Arrived(hour) ? m.Title + ": спасатели рядом." : Rescue.Line(m, hour);
        }

        /// <summary>The routes offered at the desk. The first is the one nine parties in ten write down.</summary>
        static readonly string[] Routes =
        {
            Rescue.DefaultRoute,
            "Восточная вершина по южному склону",
            "Акклиматизация до скал Пастухова",
            "Акклиматизация до косой полки",
        };

        readonly List<Transform> desks = new List<Transform>();
        Transform near;
        bool open;
        bool promptMine;
        int people = 1, route;
        float control = Rescue.ControlHour, back = Rescue.BackHour;
        string note = "";
        float noteUntil;

        // ── setting up ───────────────────────────────────────────────────────────────────────────────────
        public static RescueDesk Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            if (Instance == null)
            {
                var go = new GameObject("RescueDesk");
                if (parent != null) go.transform.SetParent(parent, false);
                Instance = go.AddComponent<RescueDesk>();
            }
            Instance.Scan(parent);
            Forget();
            return Instance;
        }

        void Scan(Transform parent)
        {
            desks.Clear();
            var roots = new List<Transform>();
            if (parent != null) roots.Add(parent);
            else
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    roots.Add(go.transform);
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Elb_RescueDesk" && !desks.Contains(t)) desks.Add(t);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Forget();
        }

        // ── every frame ──────────────────────────────────────────────────────────────────────────────────
        void Update()
        {
            if (!Climb.On) { Destroy(gameObject); return; }
            var session = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            if (me == null || !me.IsOwner || session == null || session.MyOutcome != Outcome.None)
            {
                near = null;
                if (open) Close();
                return;
            }
            // the SOS is not a counter: it works wherever the party is, which is the whole point of it
            if (!Backpacks.UiOpen && !open && Controls.Sos) session.SendSos();

            near = Nearest(me.transform.position);
            if (open)
            {
                if (near == null) { Close(); return; }
                Keys(session);
                return;
            }
            if (near != null && Controls.Board) Open();
        }

        Transform Nearest(Vector3 from)
        {
            Transform best = null; float bd = Reach;
            for (int i = desks.Count - 1; i >= 0; i--)
            {
                if (desks[i] == null) { desks.RemoveAt(i); continue; }
                float d = Vector3.Distance(from, desks[i].position);
                if (d < bd) { bd = d; best = desks[i]; }
            }
            return best;
        }

        /// <summary>1 — file the slip, 2/3 — fewer/more people, 4/5 — the turn-round half an hour earlier or later,
        /// 6/7 — the same for the hour they undertake to be back, 8 — the next route, Esc — close. Digits only:
        /// letter keys never reach the game under UI automation, and every one of them is a button as well.</summary>
        void Keys(NightSession session)
        {
            if (Controls.Pause) { Close(); return; }
            if (DigitDown(1)) { File(session); return; }
            if (DigitDown(2)) { people = Mathf.Max(1, people - 1); Refresh(); }
            if (DigitDown(3)) { people = Mathf.Min(12, people + 1); Refresh(); }
            if (DigitDown(4)) { control = Shift(control, -.5f); Refresh(); }
            if (DigitDown(5)) { control = Shift(control, .5f); Refresh(); }
            if (DigitDown(6)) { back = Shift(back, -.5f); Refresh(); }
            if (DigitDown(7)) { back = Shift(back, .5f); Refresh(); }
            if (DigitDown(8)) { route = (route + 1) % Routes.Length; Refresh(); }
        }

        static float Shift(float hour, float by)
            => Mathf.Clamp(hour + by, AscentRoute.RunStartHour + 1f, Forecast.LastHour);

        void File(NightSession session)
        {
            if (back < control) back = control;
            session.FileRescue(people, Routes[route], control, back);
            Say("Записано.");
            Close();
        }

        void Say(string text) { note = text; noteUntil = Time.time + 6f; }

        void Open()
        {
            open = true;
            if (Known && Mine.Filed)
            {
                people = Mathf.Max(1, Mine.People);
                control = Mine.ControlHour;
                back = Mine.BackHour;
                for (int i = 0; i < Routes.Length; i++) if (Routes[i] == Mine.Route.ToString()) route = i;
            }
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

        // ── the window ───────────────────────────────────────────────────────────────────────────────────
        static Font font;
        RectTransform panel;
        Text title, slipLine, warnLine, fineLine;
        Button fileButton, peopleButton, controlButton, backButton, routeButton;

        static readonly Color Ink = new Color(.93f, .95f, .96f), PanelBg = new Color(.05f, .1f, .13f, .95f),
            RowBg = new Color(.12f, .2f, .25f), Good = new Color(.24f, .36f, .3f), Amber = new Color(.91f, .79f, .6f);

        RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var rt = Rect(name, parent, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            return t;
        }

        Button ButtonUi(string name, Transform parent, Vector2 pos, Vector2 size, string text, Color bg, System.Action click)
        {
            var rt = Rect(name, parent, pos, size);
            rt.gameObject.AddComponent<Image>().color = bg;
            var b = rt.gameObject.AddComponent<Button>();
            var t = Label("Text", rt, Vector2.zero, size, 15, Ink);
            t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(8, 0); t.rectTransform.offsetMax = new Vector2(-8, 0);
            b.onClick.AddListener(() => click());
            return b;
        }

        void Build()
        {
            if (panel != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("RescueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 620f, H = 360f;
            panel = Rect("Rescue", canvas.transform, new Vector2(40, H / 2f), new Vector2(W, H));
            panel.anchorMin = new Vector2(0, .5f); panel.anchorMax = new Vector2(0, .5f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            title = Label("Title", panel, new Vector2(20, -16), new Vector2(W - 40, 28), 21, Ink);
            slipLine = Label("Slip", panel, new Vector2(20, -48), new Vector2(W - 40, 44), 14, Amber);
            peopleButton = ButtonUi("People", panel, new Vector2(20, -100), new Vector2(W - 40, 30), "", RowBg,
                () => { people = people >= 12 ? 1 : people + 1; Refresh(); });
            routeButton = ButtonUi("Route", panel, new Vector2(20, -134), new Vector2(W - 40, 30), "", RowBg,
                () => { route = (route + 1) % Routes.Length; Refresh(); });
            controlButton = ButtonUi("Control", panel, new Vector2(20, -168), new Vector2(W - 40, 30), "", RowBg,
                () => { control = Shift(control, .5f); if (control > back) back = control; Refresh(); });
            backButton = ButtonUi("Back", panel, new Vector2(20, -202), new Vector2(W - 40, 30), "", RowBg,
                () => { back = Shift(back, .5f); Refresh(); });
            fileButton = ButtonUi("File", panel, new Vector2(20, -242), new Vector2(W - 40, 34), "1 — записать группу", Good,
                () => File(NightSession.Instance));
            ButtonUi("Close", panel, new Vector2(20, -282), new Vector2(W - 40, 28), "Закрыть (Esc)", new Color(.32f, .2f, .2f), Close);
            warnLine = Label("Warn", panel, new Vector2(20, -314), new Vector2(W - 40, 22), 13, new Color(.9f, .66f, .58f));
            fineLine = Label("Fine", panel, new Vector2(20, -336), new Vector2(W - 40, 20), 12, new Color(.67f, .76f, .8f));
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (panel == null) return;
            title.text = "Эльбрусский высокогорный ПСО МЧС";
            slipLine.text = Known && Mine.Filed ? "Записаны: " + Mine.Reg.Line : Rescue.CounterText(Rescue.NotFiled);
            peopleButton.GetComponentInChildren<Text>().text = $"Состав: {people} чел.   (2 / 3 — меньше, больше)";
            routeButton.GetComponentInChildren<Text>().text = $"Маршрут: {Routes[route]}   (8 — другой)";
            controlButton.GetComponentInChildren<Text>().text = $"Контрольное время (разворот): {AscentRoute.Clock(control)}   (4 / 5)";
            backButton.GetComponentInChildren<Text>().text = $"Возвращение в лагерь: {AscentRoute.Clock(back)}   (6 / 7)";
            warnLine.text = back + Rescue.GraceHours > Forecast.LastHour
                ? "Поздно: искать начнут уже в темноте."
                : $"Не вернётесь к {AscentRoute.Clock(back)} — поиск с {AscentRoute.Clock(back + Rescue.GraceHours)}.";
            fineLine.text = $"Связь на горе до {Rescue.SignalCeiling:0} м. Выше SOS не уходит — придётся сбрасывать высоту.";
        }

        // ── the line under the crosshair ─────────────────────────────────────────────────────────────────
        void LateUpdate()
        {
            if (open)
            {
                Backpacks.OpenPack = UiHold;
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            var hud = Bootstrap.Hud;
            if (hud == null) return;
            string line = Prompt();
            if (line.Length > 0) { hud.SetPrompt(line); promptMine = true; }
            else if (promptMine) { hud.SetPrompt(""); promptMine = false; }
        }

        string Prompt()
        {
            if (Time.time < noteUntil && note.Length > 0) return note;
            if (open) return "Регистрация · 1 — записать · 2/3 состав · 4/5 разворот · 6/7 возвращение · 8 маршрут · Esc";
            if (near == null) return "";
            return Known && Mine.Filed
                ? $"Спасатели · записаны до {AscentRoute.Clock(Mine.BackHour)} · E — переписать"
                : "Стойка ЭВПСО МЧС · E — записать группу. Без регистрации вас никто не ищет";
        }

        // ── digits ───────────────────────────────────────────────────────────────────────────────────────
        static readonly KeyCode[] DigitCodes =
        {
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };
#if ENABLE_INPUT_SYSTEM
        static readonly Key[] DigitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };
#endif

        static bool DigitDown(int digit)
        {
            if (digit < 0 || digit > 9) return false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k[DigitKeys[digit]].wasPressedThisFrame) return true;
#endif
            try { return Input.GetKeyDown(DigitCodes[digit]); }
            catch (System.InvalidOperationException) { return false; }
        }
    }
}
