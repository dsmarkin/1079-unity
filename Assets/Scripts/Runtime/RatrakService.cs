using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Height1079.Runtime
{
    /// <summary>The snow-cat as the drivers of Гара-Баши actually sell it: two drop points, a price, a weather the
    /// driver will not go out in, and no acclimatisation whatever.
    ///
    /// Until now it was transport — get in, get out at the top of the lane. The rules were already in
    /// <see cref="AscentRoute"/>; this is the counter in front of them.
    ///
    /// <list type="bullet">
    /// <item><b>Two stops</b> (<see cref="AscentRoute.RatrakStops"/>): 4 800 m, and 5 100 m at the top of the groomed
    /// lane. Forty minutes of engine against five to six hours on foot.</item>
    /// <item><b>A price per seat</b>, the bands the drivers quote: 5 000–7 000 ₽ to 4 800, 10 000–12 000 ₽ to 5 100,
    /// 3 500 and 6 000 ₽ back down. Which end of the band a machine asks is drawn from the save's own seed, so the
    /// three machines on the road are not one machine three times and the price is the same for both players.</item>
    /// <item><b>A driver who says no.</b> Over 20 m/s of wind or under 200 m of visibility the cats turn round at
    /// Pastukhov rocks (<see cref="AscentRoute.RatrakDropEle"/>): 4 650 m, and that is where you get out.</item>
    /// <item><b>No acclimatisation.</b> Height a body was carried to is not height it climbed — the host freezes the
    /// sortie's high point at the drop (<see cref="AscentRoute.RatrakGivesAcclimatisation"/>).</item>
    /// <item><b>And the cold.</b> Stepping out of a warm cab onto the windiest part of the route with a cold body
    /// costs half again the usual rate for ten minutes (<see cref="AscentRoute.ColdShock"/>), which the host already
    /// charges.</item>
    /// </list></summary>
    public static class Ratraks
    {
        // The price list of the Гара-Баши drivers, roubles per seat. The rules the ride obeys live in AscentRoute;
        // only the money is here, because money is a thing of the resort and not of the mountain.
        public const int Up4800Low = 5000, Up4800High = 7000;
        public const int Up5100Low = 10000, Up5100High = 12000;
        public const int Down4800 = 3500, Down5100 = 6000;
        /// <summary>Prices are quoted in round money.</summary>
        public const int Step = 500;

        /// <summary>The hail window is up: its digits belong to it and not to the mountain.</summary>
        public static bool Hailing => RatrakService.Instance != null && RatrakService.Instance.Open;

        /// <summary>Arc-length along <see cref="Elbrus.RatrakRoute"/> at which the road first reaches this height.
        /// Measured off the same DEM both machines have, so nobody has to send it — and the answer never changes for
        /// a given height, so it is worked out once and kept.</summary>
        public static float ArcFor(float ele)
        {
            int key = Mathf.RoundToInt(ele);
            if (arcs.TryGetValue(key, out var cached)) return cached;
            var dem = Bootstrap.Dem;
            float length = Elbrus.Length(Elbrus.RatrakRoute);
            float found = length;
            if (dem != null)
                for (float s = 0f; s < length; s += 20f)
                {
                    var (x, z) = Elbrus.PointAt(Elbrus.RatrakRoute, s);
                    if (dem.Sample(x, z) < ele) continue;
                    found = s;
                    break;
                }
            arcs[key] = found;
            return found;
        }

        static readonly System.Collections.Generic.Dictionary<int, float> arcs =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>Switching maps throws the road away with everything else.</summary>
        public static void Forget() => arcs.Clear();

        /// <summary>Height of the road at this arc-length.</summary>
        public static float EleAt(float s)
        {
            var dem = Bootstrap.Dem;
            var (x, z) = Elbrus.PointAt(Elbrus.RatrakRoute, s);
            return dem != null ? dem.Sample(x, z) : 0f;
        }

        /// <summary>The air at Pastukhov rocks, which is what the driver looks at before he agrees to anything.</summary>
        public static MountainAir AtTheRocks()
        {
            float ele = AscentRoute.RatrakFallbackEle;
            var (x, z) = Elbrus.PointAt(Elbrus.RatrakRoute, ArcFor(ele));
            return Climb.Air(ele, x, z, Weather.Storm, 0f);
        }

        /// <summary>Where a machine asked for <paramref name="asked"/> metres will actually stop today.</summary>
        public static float DropFor(float asked)
        {
            var air = AtTheRocks();
            return AscentRoute.RatrakDropEle(asked, air.WindMs, air.VisibilityM);
        }

        /// <summary>Is the driver refusing the upper road at all today.</summary>
        public static bool TurnsBack => DropFor(AscentRoute.RatrakStops[1]) <= AscentRoute.RatrakFallbackEle + 1f;

        /// <summary>Why, in Russian, or "".</summary>
        public static string WhyTurnsBack()
        {
            var air = AtTheRocks();
            if (air.WindMs > AscentRoute.RatrakTurnWindMs)
                return $"Ветер на скалах {air.WindMs:0} м/с. Выше {AscentRoute.RatrakFallbackEle:0} не поеду.";
            if (air.VisibilityM < AscentRoute.RatrakTurnVisibilityM)
                return $"Видимость {air.VisibilityM:0} м. Выше {AscentRoute.RatrakFallbackEle:0} не поеду.";
            return "";
        }

        /// <summary>What this machine asks for a lift to this height. The end of the band is drawn from the save's own
        /// seed and the machine's place in the row, so it holds for the day and both players hear the same number.</summary>
        public static int PriceUp(int machine, float dropEle)
        {
            bool high = dropEle > AscentRoute.RatrakStops[0] + 60f;
            int lo = high ? Up5100Low : Up4800Low, hi = high ? Up5100High : Up4800High;
            float u = Forecast.Unit(MountainDay.Seed == 0 ? 1079 : MountainDay.Seed,
                Forecast.DayIndex(MountainDay.Date), 40 + machine);
            int price = Mathf.RoundToInt(Mathf.Lerp(lo, hi, u) / Step) * Step;
            return Mathf.Clamp(price, lo, hi);
        }

        /// <summary>And back down, by the height you get in at.</summary>
        public static int PriceDown(float fromEle) => fromEle > AscentRoute.RatrakStops[0] + 60f ? Down5100 : Down4800;
    }

    /// <summary>The window at the snow-cat: where it will take you today, and what it costs. The same shape as the
    /// hire counter and the café — walk up, E, digits — because it is the same kind of transaction.</summary>
    public sealed class RatrakService : MonoBehaviour
    {
        const int UiHold = -1;

        public static RatrakService Instance { get; private set; }
        public bool Open => open;

        bool open;
        RatrakRide cat;
        int machine;
        string note = "";
        float noteUntil;

        public static RatrakService Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            if (Instance != null) return Instance;
            var go = new GameObject("RatrakService");
            if (parent != null) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<RatrakService>();
            return Instance;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Ratraks.Forget();
        }

        /// <summary>Called by <see cref="ElbrusRides"/> when a standing machine is within reach. Returns the machine
        /// the player has just got into, or null.</summary>
        public RatrakRide Hail(HikerController me, RatrakRide near, int index, HudController hud)
        {
            cat = near; machine = index;
            bool down = near.AtTheTop;
            if (open && (near == null || Controls.Pause)) { Close(); return null; }
            if (!open)
            {
                hud?.SetPrompt(Prompt(down));
                if (Controls.Board) OpenWindow();
                return null;
            }
            Backpacks.OpenPack = UiHold;
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            hud?.SetPrompt(Time.time < noteUntil && note.Length > 0 ? note : "Ратрак · 1 / 2 — поехали · Esc — отойти");
            Refresh(down);
            if (down)
            {
                if (DigitDown(1)) return Buy(me, 0f, true);
                return null;
            }
            if (DigitDown(1)) return Buy(me, AscentRoute.RatrakStops[0], false);
            if (DigitDown(2)) return Buy(me, AscentRoute.RatrakStops[1], false);
            return null;
        }

        /// <summary>Nobody is near a machine any more.</summary>
        public void Drop() { if (open) Close(); cat = null; }

        string Prompt(bool down)
        {
            if (Time.time < noteUntil && note.Length > 0) return note;
            if (cat == null) return "";
            if (down) return $"Ратрак · вниз до бочек — {Ratraks.PriceDown(cat.Altitude)} ₽ · E — договориться";
            float drop = Ratraks.DropFor(AscentRoute.RatrakStops[1]);
            return Ratraks.TurnsBack
                ? $"Ратрак · сегодня только до скал ({drop:0} м) · E — договориться"
                : $"Ратрак · 4800 или 5100 · E — договориться · в кошельке {CafeService.Purse.Text}";
        }

        RatrakRide Buy(HikerController me, float askEle, bool down)
        {
            var session = NightSession.Instance;
            if (cat == null || me == null || session == null) return null;
            int price = down ? Ratraks.PriceDown(cat.Altitude) : Ratraks.PriceUp(machine, Ratraks.DropFor(askEle));
            if (!CafeService.Purse.Pay(price))
            {
                Say($"Не хватает: {price} ₽, в кошельке {CafeService.Purse.Text}");
                return null;
            }
            Close();
            me.BoardRide(cat.Seat);
            if (down) cat.Home();
            else
            {
                float drop = Ratraks.DropFor(askEle);
                cat.Go(Ratraks.ArcFor(drop));
                Say($"Поехали до {drop:0} м · {price} ₽");
            }
            return cat;
        }

        void Say(string text) { note = text; noteUntil = Time.time + 5f; }

        void OpenWindow()
        {
            open = true;
            Build();
            Refresh(cat != null && cat.AtTheTop);
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
        Text title, driverLine, fineLine;
        Button lowButton, highButton;

        static readonly Color Ink = new Color(.93f, .95f, .96f), PanelBg = new Color(.05f, .1f, .13f, .95f),
            RowBg = new Color(.12f, .2f, .25f), Poor = new Color(.14f, .14f, .16f), Amber = new Color(.91f, .79f, .6f);

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

        Button ButtonUi(string name, Transform parent, Vector2 pos, Vector2 size, Color bg, System.Action click)
        {
            var rt = Rect(name, parent, pos, size);
            rt.gameObject.AddComponent<Image>().color = bg;
            var b = rt.gameObject.AddComponent<Button>();
            var t = Label("Text", rt, Vector2.zero, size, 15, Ink);
            t.alignment = TextAnchor.MiddleLeft;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(12, 0); t.rectTransform.offsetMax = new Vector2(-12, 0);
            b.onClick.AddListener(() => click());
            return b;
        }

        void Build()
        {
            if (panel != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("RatrakCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 600f, H = 260f;
            panel = Rect("Ratrak", canvas.transform, new Vector2(40, H / 2f), new Vector2(W, H));
            panel.anchorMin = new Vector2(0, .5f); panel.anchorMax = new Vector2(0, .5f);
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            title = Label("Title", panel, new Vector2(20, -16), new Vector2(W - 40, 26), 21, Ink);
            driverLine = Label("Driver", panel, new Vector2(20, -46), new Vector2(W - 40, 40), 14, Amber);
            lowButton = ButtonUi("Low", panel, new Vector2(20, -94), new Vector2(W - 40, 32), RowBg,
                () => Buy(Bootstrap.LocalHiker, AscentRoute.RatrakStops[0], cat != null && cat.AtTheTop));
            highButton = ButtonUi("High", panel, new Vector2(20, -130), new Vector2(W - 40, 32), RowBg,
                () => Buy(Bootstrap.LocalHiker, AscentRoute.RatrakStops[1], false));
            ButtonUi("Close", panel, new Vector2(20, -170), new Vector2(W - 40, 28), new Color(.32f, .2f, .2f), Close)
                .GetComponentInChildren<Text>().text = "Закрыть (Esc)";
            fineLine = Label("Fine", panel, new Vector2(20, -206), new Vector2(W - 40, 46), 12, new Color(.67f, .76f, .8f));
            panel.gameObject.SetActive(false);
        }

        void Refresh(bool down)
        {
            if (panel == null || cat == null) return;
            title.text = "Ратрак · Гара-Баши";
            if (down)
            {
                int price = Ratraks.PriceDown(cat.Altitude);
                driverLine.text = $"Стоим на {cat.Altitude:0} м. В кошельке {CafeService.Purse.Text}.";
                lowButton.gameObject.SetActive(true);
                highButton.gameObject.SetActive(false);
                lowButton.GetComponentInChildren<Text>().text = $"1. Вниз к бочкам — {price} ₽";
                lowButton.GetComponent<Image>().color = CafeService.Purse.CanAfford(price) ? RowBg : Poor;
                fineLine.text = "Вниз ратраком — это сорок минут вместо трёх часов по раскисшему снегу.";
                return;
            }
            string why = Ratraks.WhyTurnsBack();
            driverLine.text = why.Length > 0 ? "Водитель: " + why
                : $"Водитель кивает. В кошельке {CafeService.Purse.Text}.";
            float low = Ratraks.DropFor(AscentRoute.RatrakStops[0]);
            float high = Ratraks.DropFor(AscentRoute.RatrakStops[1]);
            int lowPrice = Ratraks.PriceUp(machine, low), highPrice = Ratraks.PriceUp(machine, high);
            lowButton.gameObject.SetActive(true);
            lowButton.GetComponentInChildren<Text>().text = $"1. До {low:0} м — {lowPrice} ₽";
            lowButton.GetComponent<Image>().color = CafeService.Purse.CanAfford(lowPrice) ? RowBg : Poor;
            bool offerHigh = !Ratraks.TurnsBack;
            highButton.gameObject.SetActive(offerHigh);
            if (offerHigh)
            {
                highButton.GetComponentInChildren<Text>().text = $"2. До {high:0} м — {highPrice} ₽";
                highButton.GetComponent<Image>().color = CafeService.Purse.CanAfford(highPrice) ? RowBg : Poor;
            }
            fineLine.text = "Сорок минут вместо пяти часов — и ни грамма акклиматизации.\n"
                + "На 5100 выходишь непрогретым в самое ветреное место: первые десять минут холод бьёт в полтора раза сильнее.";
        }

        // ── digits ───────────────────────────────────────────────────────────────────────────────────────
        static bool DigitDown(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k[digit == 1 ? Key.Digit1 : Key.Digit2].wasPressedThisFrame) return true;
#endif
            try { return Input.GetKeyDown(digit == 1 ? KeyCode.Alpha1 : KeyCode.Alpha2); }
            catch (System.InvalidOperationException) { return false; }
        }
    }
}
