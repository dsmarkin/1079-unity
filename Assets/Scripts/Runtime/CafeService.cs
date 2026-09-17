using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Height1079.Runtime
{
    /// <summary>The counter of a café on the slope. Walk up to the <c>Counter</c> marker inside <c>Elb_Cafe</c>, press E
    /// and a small window opens with the board of <see cref="Refreshments"/>: a bowl of shurpa, a khychin, a pizza with
    /// coffee. Money leaves the purse, the hiker stands at the table while the portion is eaten, and the host applies the
    /// warmth and the strength it gives back. «Взять с собой» wraps the portion into the rucksack instead.
    /// Elbrus only — the night on Kholat has no cafés, and the component switches itself off there.</summary>
    public sealed class CafeService : MonoBehaviour
    {
        /// <summary>How close the hiker has to be to order.</summary>
        public const float Reach = Refreshments.CounterReach;
        const int PageSize = 9;
        /// <summary>A rucksack id nothing will ever have. While the order window is open it sits in
        /// <see cref="Backpacks.OpenPack"/>, which is the only way to tell <see cref="HikerController"/> that a window
        /// owns the mouse, so the cursor stays free and the buttons are clickable. Backpacks clears it every frame and
        /// <see cref="LateUpdate"/> puts it back, so the rucksack panel itself never appears.</summary>
        const int UiHold = -1;

        public static CafeService Instance { get; private set; }

        /// <summary>What the local hiker has left to spend. One purse per client: nobody else's money is at stake.</summary>
        public static Wallet Purse = Wallet.Start();

        readonly List<Transform> counters = new List<Transform>();
        static Dish[] menu;

        Transform near;          // the counter within reach right now
        bool open;               // the order window is up
        int page;
        DishId ordered = DishId.None;
        bool orderedAway;
        int paid;
        float serveAt;
        Vector3 orderedAt;
        string note = "";
        float noteUntil;
        bool promptMine;
        NightSession subscribed;

        // ── setting up ───────────────────────────────────────────────────────────────────────────────────
        /// <summary>Puts the service into the world. Call it once after the Elbrus prefabs are placed; it finds every
        /// <c>Counter</c> inside an <c>Elb_Cafe*</c> under <paramref name="parent"/> (or in the whole scene when that is
        /// null). Safe to call twice — the second call only rescans.</summary>
        public static CafeService Create(Transform parent = null)
        {
            if (!Height1079.Core.World.IsElbrus) return null;
            var service = Ensure(parent);
            service.Scan(parent);
            return service;
        }

        /// <summary>Registers one café that has just been placed: its <c>Counter</c> child is where orders are taken.
        /// For a caller that puts the prefabs down one by one; <see cref="Create"/> finds them all at once instead.</summary>
        public static CafeService Attach(GameObject cafe)
        {
            if (cafe == null || !Height1079.Core.World.IsElbrus) return null;
            var service = Ensure(cafe.transform.parent);
            service.Register(FindCounter(cafe.transform) ?? cafe.transform);
            return service;
        }

        static CafeService Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CafeService");
            if (parent != null) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<CafeService>();
            Purse = Wallet.Start();
            return Instance;
        }

        static Transform FindCounter(Transform cafe)
        {
            foreach (var t in cafe.GetComponentsInChildren<Transform>(true))
                if (t.name == "Counter") return t;
            return null;
        }

        /// <summary>A café counter and not the plank of a souvenir stall: the marker has to sit inside an Elb_Cafe prefab.</summary>
        static bool InCafe(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.StartsWith("Elb_Cafe")) return true;
            return false;
        }

        void Register(Transform counter)
        {
            if (counter == null || counters.Contains(counter)) return;
            counters.Add(counter);
        }

        /// <summary>Walks the hierarchy for counters. Called from <see cref="Create"/> and once more from Start, so the
        /// cafés are found even if nobody registered them.</summary>
        void Scan(Transform parent)
        {
            var roots = new List<Transform>();
            if (parent != null) roots.Add(parent);
            else
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    roots.Add(go.transform);
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Counter" && InCafe(t)) Register(t);
        }

        void Start()
        {
            if (counters.Count == 0) Scan(transform.parent);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) subscribed.CafeServed -= Served;
            if (open) Close();
        }

        // ── every frame ──────────────────────────────────────────────────────────────────────────────────
        void Update()
        {
            if (!Height1079.Core.World.IsElbrus) { Destroy(gameObject); return; }
            var session = NightSession.Instance;
            if (subscribed != session)
            {
                if (subscribed != null) subscribed.CafeServed -= Served;
                if (session != null) session.CafeServed += Served;
                subscribed = session;
            }
            var me = Bootstrap.LocalHiker;
            if (me == null || !me.IsOwner || session == null || session.MyOutcome != Outcome.None || me.Ride != null)
            {
                near = null;
                if (open) Close();
                return;
            }
            near = Nearest(me.transform.position, Reach);
            if (ordered != DishId.None) { Serving(me); return; }
            if (open)
            {
                if (near == null) { Close(); return; }
                Keys();
                return;
            }
            if (near != null && Controls.Board) Open();
        }

        Transform Nearest(Vector3 from, float reach)
        {
            Transform best = null; float bd = reach;
            for (int i = counters.Count - 1; i >= 0; i--)
            {
                var c = counters[i];
                if (c == null) { counters.RemoveAt(i); continue; }
                float d = Vector3.Distance(from, c.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Waiting for the portion: the hiker has to stay at the counter, otherwise the order is given up and the
        /// money comes back.</summary>
        void Serving(HikerController me)
        {
            if (Vector3.Distance(me.transform.position, orderedAt) > Refreshments.LeaveReach)
            {
                Purse.Refund(paid);
                Say($"Заказ отменён · {Purse.Text}");
                ordered = DishId.None; paid = 0;
                return;
            }
            if (Time.time < serveAt) return;
            var dish = ordered; bool away = orderedAway;
            ordered = DishId.None; paid = 0;
            var session = NightSession.Instance;
            if (session != null) session.OrderRefreshment(dish, away);
            else Say("Кухня не отвечает");
        }

        /// <summary>The host has served (or refused) the portion.</summary>
        void Served(DishId dish, bool takeAway, bool served)
        {
            var spec = Refreshments.Get(dish);
            if (!served)
            {
                Purse.Refund(spec.Roubles);
                Say(takeAway ? "С собой не влезает · деньги назад" : "Не подали · деньги назад");
                return;
            }
            Say(takeAway ? $"{Items.Spec(spec.TakeAway).Name} — в рюкзак · {Purse.Text}" : $"{spec.Name}. {spec.Note}");
        }

        void Say(string text) { note = text; noteUntil = Time.time + 5f; }

        // ── ordering ─────────────────────────────────────────────────────────────────────────────────────
        static Dish[] Menu => menu ?? (menu = Refreshments.Menu());

        void Open()
        {
            open = true; page = 0;
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

        /// <summary>Digits pick a line, 0 turns the page, Esc closes. Letter keys never reach the game under automation,
        /// which is why every one of these has a button in the window as well.</summary>
        void Keys()
        {
            if (Controls.Pause) { Close(); return; }
            if (DigitDown(0)) { page = page == 0 && Menu.Length > PageSize ? 1 : 0; Refresh(); return; }
            for (int k = 1; k <= PageSize; k++)
                if (DigitDown(k)) { Order(page * PageSize + k - 1, false); return; }
        }

        void Order(int index, bool takeAway)
        {
            if (!open || index < 0 || index >= Menu.Length) return;
            var dish = Menu[index];
            var me = Bootstrap.LocalHiker;
            if (me == null) return;
            if (takeAway && !dish.Portable) { Say("Это едят за столом"); Refresh(); return; }
            if (!Purse.Pay(dish.Roubles)) { Say("Не хватает денег"); Refresh(); return; }
            paid = dish.Roubles;
            ordered = dish.Id;
            orderedAway = takeAway;
            orderedAt = me.transform.position;
            serveAt = Time.time + (takeAway ? Mathf.Min(10f, dish.Seconds * .3f) : dish.Seconds);
            Close();
        }

        // ── the window, built from code like the rest of the UI ──────────────────────────────────────────
        static Font font;
        Canvas canvas;
        RectTransform panel;
        Text title, purseLine;
        Button pageButton;
        readonly List<Button> rows = new List<Button>();
        readonly List<Button> takeRows = new List<Button>();

        static readonly Color Ink = new Color(.93f, .95f, .96f), PanelBg = new Color(.04f, .11f, .16f, .95f),
            RowBg = new Color(.12f, .2f, .25f), TakeBg = new Color(.2f, .29f, .34f), Amber = new Color(.91f, .79f, .6f);

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            return t;
        }

        Button ButtonUi(string name, Transform parent, Vector2 pos, Vector2 size, string text, Color bg, int fontSize, TextAnchor align, System.Action click)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), pos, size);
            rt.gameObject.AddComponent<Image>().color = bg;
            var b = rt.gameObject.AddComponent<Button>();
            var t = Label("Text", rt, Vector2.zero, size, fontSize, Ink, align);
            t.text = text;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
            b.onClick.AddListener(() => click());
            return b;
        }

        void Build()
        {
            if (panel != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = new GameObject("CafeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;                 // above the night HUD
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 660f, RowH = 34f;
            float h = 116f + PageSize * RowH + 78f;
            panel = Rect("CafeOrder", canvas.transform, new Vector2(0, .5f), new Vector2(40, h / 2f), new Vector2(W, h));
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            title = Label("Title", panel, new Vector2(20, -16), new Vector2(W - 40, 28), 22, Ink);
            purseLine = Label("Purse", panel, new Vector2(20, -48), new Vector2(W - 40, 22), 14, Amber);
            Label("Head", panel, new Vector2(20, -74), new Vector2(W - 40, 20), 12, new Color(.67f, .76f, .8f)).text
                = "Цифра — съесть здесь · кнопка справа — взять с собой";

            for (int i = 0; i < PageSize; i++)
            {
                int slot = i;
                float y = -96f - i * RowH;
                rows.Add(ButtonUi("Row" + i, panel, new Vector2(20, y), new Vector2(460, RowH - 4f), "", RowBg, 15, TextAnchor.MiddleLeft,
                    () => Order(page * PageSize + slot, false)));
                takeRows.Add(ButtonUi("Take" + i, panel, new Vector2(490, y), new Vector2(150, RowH - 4f), "с собой", TakeBg, 13, TextAnchor.MiddleCenter,
                    () => Order(page * PageSize + slot, true)));
            }
            float footer = -104f - PageSize * RowH;
            pageButton = ButtonUi("Page", panel, new Vector2(20, footer), new Vector2(300, 30), "Ещё (0)", TakeBg, 15, TextAnchor.MiddleCenter,
                () => { page = page == 0 && Menu.Length > PageSize ? 1 : 0; Refresh(); });
            ButtonUi("Close", panel, new Vector2(330, footer), new Vector2(310, 30), "Закрыть (Esc)", new Color(.32f, .2f, .2f), 15, TextAnchor.MiddleCenter, Close);
            Label("Fine", panel, new Vector2(20, footer - 34f), new Vector2(W - 40, 20), 12, new Color(.67f, .76f, .8f)).text
                = "Наличными. Цены курортные, как на склоне.";
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (panel == null) return;
            title.text = "Кафе · заказ";
            purseLine.text = $"В кошельке {Purse.Text}";
            int pages = (Menu.Length + PageSize - 1) / PageSize;
            if (page >= pages) page = 0;
            pageButton.gameObject.SetActive(pages > 1);
            for (int i = 0; i < rows.Count; i++)
            {
                int index = page * PageSize + i;
                bool show = index < Menu.Length;
                if (rows[i].gameObject.activeSelf != show) rows[i].gameObject.SetActive(show);
                if (takeRows[i].gameObject.activeSelf != (show && Menu[index].Portable)) takeRows[i].gameObject.SetActive(show && Menu[index].Portable);
                if (!show) continue;
                var d = Menu[index];
                string warm = d.Warmth >= 5f ? $" · тепло +{Mathf.RoundToInt(d.Warmth)}" : "";
                rows[i].GetComponentInChildren<Text>().text = $"{i + 1}. {d.Name} — {d.Roubles} ₽{warm}";
                rows[i].GetComponent<Image>().color = Purse.CanAfford(d.Roubles) ? RowBg : new Color(.14f, .14f, .16f);
            }
        }

        // ── HUD line, and holding the mouse free while the window is up ──────────────────────────────────
        void LateUpdate()
        {
            if (open)
            {
                // HikerController grabs the cursor back on the next click unless a window says it owns it
                Backpacks.OpenPack = UiHold;
                if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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
            if (ordered != DishId.None)
            {
                var d = Refreshments.Get(ordered);
                int left = Mathf.CeilToInt(Mathf.Max(0f, serveAt - Time.time));
                return orderedAway ? $"{d.Name} · заворачивают, ещё {left} с" : $"{d.Name} · ещё {left} с · стойте у стойки";
            }
            if (open) return "Кафе · цифры 1–9 — заказать · 0 — ещё · Esc — закрыть";
            if (near != null) return $"Кафе · E — заказать · в кошельке {Purse.Text}";
            return "";
        }

        // ── digits (Controls covers only 0…3, and the window needs all ten) ──────────────────────────────
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

    /// <summary>The café counter on the host. Money is the client's own business, but the warmth a portion gives back is
    /// the night's, so the order comes here: the host feeds the participant through <see cref="Refreshments"/> and puts
    /// a parcel taken away into the rucksack it keeps.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Local: what came of our last café order (dish, taken away, actually served).</summary>
        public event System.Action<DishId, bool, bool> CafeServed;

        /// <summary>Owner asks a café to serve one portion, eaten at the table or wrapped for the rucksack.</summary>
        public void OrderRefreshment(DishId dish, bool takeAway) => CafeOrderRpc((byte)dish, takeAway);

        [Rpc(SendTo.Server)]
        void CafeOrderRpc(byte dish, bool takeAway, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            var spec = Refreshments.Get((DishId)dish);
            string token = Token(sender);
            bool served = false;
            if (!spec.IsEmpty && run != null && Height1079.Core.World.IsElbrus
                && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None)
            {
                if (takeAway)
                {
                    served = spec.Portable && packs != null && packs.Receive(token, new ItemStack(spec.TakeAway)) == PackResult.Ok;
                    if (served)
                    {
                        SyncPacks();
                        run.Record($"{p.Name} берёт с собой: {Items.Spec(spec.TakeAway).Name}.");
                    }
                }
                else
                {
                    var meal = Refreshments.Eat((DishId)dish, p);
                    served = !meal.IsEmpty;
                    if (served) run.Record($"{p.Name}: {meal.Text}");
                }
            }
            CafeServedRpc(dish, takeAway, served, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void CafeServedRpc(byte dish, bool takeAway, bool served, RpcParams rpc) => CafeServed?.Invoke((DishId)dish, takeAway, served);
    }
}
