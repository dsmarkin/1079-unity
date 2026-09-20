using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Height1079.Runtime
{
    /// <summary>A night in a приют: walk up to the bunk, press E, pay, and wake up warm, dry and a day further into
    /// the acclimatisation.
    ///
    /// This is the same counter as the hire stand — a marker, a key, a small window, digits — and deliberately so.
    /// What it sells is different: a bunk in a heated room dries the boots, fills the thermos off the hut's own stove,
    /// thaws frostbite further than a sleeping bag does and gives the legs back more than a двойка on the snow
    /// (<see cref="Lodging"/>). What it does <b>not</b> sell is a better mountain: the acclimatisation is
    /// <see cref="Camp.Sleep"/> at the bunk's own height, the same rule and the same code a tent night uses.
    ///
    /// And, like a tent night, it <b>writes the save</b>. That is the whole reason it exists: the real ascent from
    /// Гара-Баши is eight to eleven hours, so it is played in pieces — you live in a barrel, you walk up, you come
    /// back down, you sleep, and tomorrow the weather is a different day.
    ///
    /// Elbrus only; the component switches itself off on Kholat Syakhl.</summary>
    public sealed class LodgeService : MonoBehaviour
    {
        /// <summary>The same hold on the rucksack the hire counter and the café take while their window is up.</summary>
        const int UiHold = -1;

        public static LodgeService Instance { get; private set; }
        /// <summary>The bunk window is up: its digits belong to it and not to the mountain.</summary>
        public static bool Booking => Instance != null && Instance.open;

        bool open;
        Bunk near;
        /// <summary>What was handed over before the host was asked, so it can be handed back if the host refuses.</summary>
        int paid;
        bool promptMine;
        string note = "";
        float noteUntil;
        NightSession subscribed;

        public static LodgeService Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            if (Instance == null)
            {
                var go = new GameObject("LodgeService");
                if (parent != null) go.transform.SetParent(parent, false);
                Instance = go.AddComponent<LodgeService>();
            }
            Lodges.Scan(parent);
            return Instance;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) subscribed.Slept -= Slept;
            Lodges.Forget();
        }

        void Update()
        {
            if (!Climb.On) { Destroy(gameObject); return; }
            var session = NightSession.Instance;
            if (subscribed != session)
            {
                if (subscribed != null) subscribed.Slept -= Slept;
                if (session != null) session.Slept += Slept;
                subscribed = session;
            }
            var me = Bootstrap.LocalHiker;
            if (me == null || !me.IsOwner || session == null || session.MyOutcome != Outcome.None || me.Ride != null)
            {
                near = default;
                if (open) Close();
                return;
            }
            near = Lodges.Here(me.transform.position);
            if (open)
            {
                if (near.IsEmpty) { Close(); return; }
                if (Controls.Pause) { Close(); return; }
                if (DigitDown(1)) Book();
                return;
            }
            if (!near.IsEmpty && Controls.Board) Open();
        }

        void Slept(bool ok, string text)
        {
            if (!ok) { Purse.Money.Refund(paid); note = text + " · деньги назад"; }
            else note = text;
            noteUntil = Time.time + 6f;
            paid = 0;
            if (ok) Close();
        }

        // ── the order ────────────────────────────────────────────────────────────────────────────────────

        void Book()
        {
            var s = NightSession.Instance;
            if (s == null || near.IsEmpty) return;
            if (paid > 0) return;
            if (!Lodging.Book(ref Purse.Money, near))
            {
                Say($"Не хватает денег: койка {near.Roubles} ₽, в кошельке {Purse.Money.Text}");
                Refresh();
                return;
            }
            paid = near.Roubles;
            s.RequestBunk();
            Say("Расплатились. Ложитесь.");
            Refresh();
        }

        void Say(string text) { note = text; noteUntil = Time.time + 5f; }

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

        // ── the window ───────────────────────────────────────────────────────────────────────────────────
        static Font font;
        RectTransform panel;
        Text title, priceLine, whatLine, fineLine;
        Button bookButton;

        static readonly Color Ink = new Color(.93f, .95f, .96f), PanelBg = new Color(.05f, .1f, .13f, .95f),
            Warm = new Color(.3f, .34f, .45f), Poor = new Color(.14f, .14f, .16f), Amber = new Color(.91f, .79f, .6f);

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
            t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
            b.onClick.AddListener(() => click());
            return b;
        }

        void Build()
        {
            if (panel != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("LodgeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 560f, H = 280f;
            panel = Rect("Lodge", canvas.transform, new Vector2(40, H / 2f), new Vector2(W, H));
            panel.anchorMin = new Vector2(0, .5f); panel.anchorMax = new Vector2(0, .5f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            title = Label("Title", panel, new Vector2(20, -16), new Vector2(W - 40, 28), 22, Ink);
            priceLine = Label("Price", panel, new Vector2(20, -50), new Vector2(W - 40, 22), 15, Amber);
            whatLine = Label("What", panel, new Vector2(20, -78), new Vector2(W - 40, 90), 13, new Color(.78f, .85f, .88f));
            bookButton = ButtonUi("Book", panel, new Vector2(20, -180), new Vector2(W - 40, 34), "", Warm, Book);
            ButtonUi("Close", panel, new Vector2(20, -220), new Vector2(W - 40, 30), "Закрыть (Esc)", new Color(.32f, .2f, .2f), Close);
            fineLine = Label("Fine", panel, new Vector2(20, -254), new Vector2(W - 40, 20), 12, new Color(.67f, .76f, .8f));
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (panel == null || near.IsEmpty) return;
            title.text = near.Name;
            priceLine.text = near.Free
                ? $"{near.Ele:0} м · денег не берут · в кошельке {Purse.Money.Text}"
                : $"{near.Ele:0} м · {near.Roubles} ₽ за койку · в кошельке {Purse.Money.Text}";
            var what = new System.Text.StringBuilder();
            what.Append($"Мест: {near.Beds}. Силы к утру — {Mathf.RoundToInt(Lodging.RestFactor(near) * 100f)} % (в палатке {Mathf.RoundToInt(Lodging.RestFactor(Lodging.Tent) * 100f)} %).");
            if (near.Dries) what.Append("\nСушилка: мокрое к утру высохнет.");
            if (near.MeltsSnow) what.Append("\nКухня: термос нальют, свой газ тратить не надо.");
            what.Append($"\nОбморожение до {Lodging.ThawBelow(near) * 100f:0} % отойдёт за ночь. Настоящее — нет.");
            what.Append("\nАкклиматизация считается по высоте койки — как в палатке.");
            whatLine.text = what.ToString();
            bool can = near.Free || Purse.Money.CanAfford(near.Roubles);
            bookButton.GetComponent<Image>().color = can ? Warm : Poor;
            bookButton.GetComponentInChildren<Text>().text = near.Free
                ? "1 — переночевать (бесплатно)"
                : $"1 — переночевать за {near.Roubles} ₽";
            fineLine.text = "Ночёвка закрывает выход и сохраняет игру.";
        }

        // ── the line under the crosshair ─────────────────────────────────────────────────────────────────
        void LateUpdate()
        {
            if (open)
            {
                Backpacks.OpenPack = UiHold;
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Refresh();
            }
            var hud = Bootstrap.Hud;
            if (hud == null) return;
            string line = Line();
            if (line.Length > 0) { hud.SetPrompt(line); promptMine = true; }
            else if (promptMine) { hud.SetPrompt(""); promptMine = false; }
        }

        string Line()
        {
            if (Time.time < noteUntil && note.Length > 0) return note;
            if (open) return "Приют · 1 — переночевать · Esc — закрыть";
            if (near.IsEmpty) return "";
            return near.Free
                ? $"{near.Name} · E — лечь (бесплатно)"
                : $"{near.Name} · E — лечь · {near.Roubles} ₽ · в кошельке {Purse.Money.Text}";
        }

        // ── digits ───────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Digit 1, and nothing else: letter keys never reach the game under UI automation, and the window
        /// has a button for the mouse.</summary>
        static bool DigitDown(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k[Key.Digit1].wasPressedThisFrame) return true;
#endif
            try { return Input.GetKeyDown(KeyCode.Alpha1); }
            catch (System.InvalidOperationException) { return false; }
        }
    }
}
