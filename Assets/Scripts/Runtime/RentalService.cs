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
    /// <summary>The hire stand at the bottom of the mountain — the only thing standing between a tourist off the
    /// ropeway and the wall at Pastukhov rocks. Walk up to <c>Elb_RentStand</c>, press E, and the same small window
    /// the café uses opens with the board of <see cref="Rental"/>: crampons, an ice axe, a harness with three
    /// screw-gates and a cow's tail, a helmet, goggles, a mask, a down jacket, a balaclava, mittens and spare gloves,
    /// a thermos, a headlamp and a space blanket. Money leaves the same purse the cafés take from; the host puts what
    /// was hired into the rucksack it keeps, and from that moment the rules count it as worn
    /// (<see cref="Rental.Carried"/>).
    ///
    /// The first line of the board is the whole kit at the counter's bundle price, because that is the decision the
    /// game is actually about — most of a visitor's money against being allowed above 4 650 m.
    ///
    /// Elbrus only; the component switches itself off on Kholat Syakhl.</summary>
    public sealed class RentalService : MonoBehaviour
    {
        public const float Reach = Rental.CounterReach;
        /// <summary>Digits 2…9 pick a piece; digit 1 is always the whole kit and digit 0 turns the page.</summary>
        const int PageSize = 8;
        /// <summary>The rucksack id the order window parks in <see cref="Backpacks.OpenPack"/> so the cursor stays
        /// free while it is up. The same trick <see cref="CafeService"/> uses, and the same value, because only one
        /// counter window can ever be open at a time.</summary>
        const int UiHold = -1;

        public static RentalService Instance { get; private set; }

        readonly List<Transform> stands = new List<Transform>();
        static Hire[] board;

        Transform near;
        bool open;
        int page;
        Gear ordered = Gear.None;
        int paid;
        float serveAt;
        Vector3 orderedAt;
        string note = "";
        float noteUntil;
        bool promptMine;
        NightSession subscribed;

        // ── setting up ───────────────────────────────────────────────────────────────────────────────────
        public static RentalService Create(Transform parent = null)
        {
            if (!Climb.On) return null;
            var service = Ensure(parent);
            service.Scan(parent);
            return service;
        }

        static RentalService Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RentalService");
            if (parent != null) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<RentalService>();
            return Instance;
        }

        void Scan(Transform parent)
        {
            var roots = new List<Transform>();
            if (parent != null) roots.Add(parent);
            else
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    roots.Add(go.transform);
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Elb_RentStand") && !stands.Contains(t)) stands.Add(t);
        }

        void Start()
        {
            if (stands.Count == 0) Scan(transform.parent);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) subscribed.Hired -= Served;
            if (open) Close();
        }

        // ── every frame ──────────────────────────────────────────────────────────────────────────────────
        void Update()
        {
            if (!Climb.On) { Destroy(gameObject); return; }
            var session = NightSession.Instance;
            if (subscribed != session)
            {
                if (subscribed != null) subscribed.Hired -= Served;
                if (session != null) session.Hired += Served;
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
            if (ordered != Gear.None) { Serving(me); return; }
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
            for (int i = stands.Count - 1; i >= 0; i--)
            {
                var c = stands[i];
                if (c == null) { stands.RemoveAt(i); continue; }
                float d = Vector3.Distance(from, c.position);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Waiting while the man behind the counter finds your size. Walk off and the order is given up.</summary>
        void Serving(HikerController me)
        {
            if (Vector3.Distance(me.transform.position, orderedAt) > Rental.LeaveReach)
            {
                CafeService.Purse.Refund(paid);
                Say($"Прокат отменён · {CafeService.Purse.Text}");
                ordered = Gear.None; paid = 0;
                return;
            }
            if (Time.time < serveAt) return;
            var want = ordered;
            ordered = Gear.None;
            var session = NightSession.Instance;
            if (session != null) session.HireGear(want);
            else { CafeService.Purse.Refund(paid); paid = 0; Say("Прокат закрыт"); }
        }

        /// <summary>The host has handed over what it could. Anything that did not fit in the rucksack is paid back.</summary>
        void Served(ushort wanted, ushort served)
        {
            var missed = (Gear)wanted & ~(Gear)served;
            if (missed != Gear.None && paid > 0)
            {
                int rest = 0, all = 0;
                foreach (var h in Rental.Board())
                {
                    if (((Gear)wanted & h.Piece) == 0) continue;
                    all += h.Roubles;
                    if ((missed & h.Piece) != 0) rest += h.Roubles;
                }
                int back = all <= 0 ? paid : Mathf.RoundToInt(paid * (float)rest / all);
                CafeService.Purse.Refund(back);
                Say(served == 0
                    ? $"Не влезает в рюкзак · деньги назад · {CafeService.Purse.Text}"
                    : $"Не влезло: {Rental.Titles(missed)} · вернули {back} ₽");
            }
            else if (served != 0) Say($"В рюкзаке: {Rental.Titles((Gear)served)} · {CafeService.Purse.Text}");
            paid = 0;
            Refresh();
        }

        void Say(string text) { note = text; noteUntil = Time.time + 5f; }

        // ── what is already in the rucksack ──────────────────────────────────────────────────────────────
        /// <summary>What the local hiker is carrying, as <see cref="Gear"/>. Read off the rucksack the host publishes,
        /// so the window and the rules agree without asking.</summary>
        public static Gear Have(HikerController me)
        {
            if (me == null) return Gear.None;
            var have = Rental.PieceOf(me.Carried.Value.Stack.Id);
            int pack = Backpacks.MyPackId(me);
            if (pack != 0) have |= Rental.GearOf(Backpacks.Contents(pack));
            return have;
        }

        // ── ordering ─────────────────────────────────────────────────────────────────────────────────────
        static Hire[] Board => board ?? (board = Rental.Board());

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

        /// <summary>1 — the whole kit, 2…9 — one piece, 0 — the other page, Esc — close. Letter keys never reach the
        /// game under automation, which is why there is nothing here but digits and why every one of them is a button
        /// in the window as well.</summary>
        void Keys()
        {
            if (Controls.Pause) { Close(); return; }
            if (DigitDown(0)) { page = page == 0 ? 1 : 0; Refresh(); return; }
            if (DigitDown(1)) { OrderSet(); return; }
            for (int k = 2; k <= PageSize + 1; k++)
                if (DigitDown(k)) { Order(page * PageSize + k - 2); return; }
        }

        void Order(int index)
        {
            if (!open || index < 0 || index >= Board.Length) return;
            var me = Bootstrap.LocalHiker;
            if (me == null) return;
            var piece = Board[index];
            if ((Have(me) & piece.Piece) != 0) { Say("Это уже в рюкзаке"); Refresh(); return; }
            if (!CafeService.Purse.Pay(piece.Roubles)) { Say("Не хватает денег"); Refresh(); return; }
            Begin(me, piece.Piece, piece.Roubles);
        }

        void OrderSet()
        {
            var me = Bootstrap.LocalHiker;
            if (me == null) return;
            var have = Have(me);
            var missing = Ascent.Missing(have);
            if (missing == Gear.None) { Say("Комплект уже собран"); Refresh(); return; }
            int price = Rental.BundleRoubles(have);
            if (!CafeService.Purse.Pay(price)) { Say($"Не хватает денег: нужно {price} ₽"); Refresh(); return; }
            Begin(me, missing, price);
        }

        void Begin(HikerController me, Gear wanted, int price)
        {
            paid = price;
            ordered = wanted;
            orderedAt = me.transform.position;
            serveAt = Time.time + Rental.ServeSeconds;
            Close();
        }

        // ── the window, built from code like the rest of the UI ──────────────────────────────────────────
        static Font font;
        Canvas canvas;
        RectTransform panel;
        Text title, purseLine, missingLine;
        Button setButton, pageButton;
        readonly List<Button> rows = new List<Button>();

        static readonly Color Ink = new Color(.93f, .95f, .96f), PanelBg = new Color(.05f, .1f, .13f, .95f),
            RowBg = new Color(.12f, .2f, .25f), Poor = new Color(.14f, .14f, .16f), SetBg = new Color(.24f, .36f, .3f),
            Amber = new Color(.91f, .79f, .6f);

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
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
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
            canvas = new GameObject("RentalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            const float W = 660f, RowH = 34f;
            float h = 160f + PageSize * RowH + 76f;
            panel = Rect("Rental", canvas.transform, new Vector2(0, .5f), new Vector2(40, h / 2f), new Vector2(W, h));
            panel.gameObject.AddComponent<Image>().color = PanelBg;
            title = Label("Title", panel, new Vector2(20, -16), new Vector2(W - 40, 28), 22, Ink);
            purseLine = Label("Purse", panel, new Vector2(20, -48), new Vector2(W - 40, 22), 14, Amber);
            missingLine = Label("Missing", panel, new Vector2(20, -72), new Vector2(W - 40, 40), 12, new Color(.86f, .72f, .6f));
            setButton = ButtonUi("Set", panel, new Vector2(20, -114), new Vector2(W - 40, 32), "", SetBg, 16, TextAnchor.MiddleLeft, OrderSet);

            for (int i = 0; i < PageSize; i++)
            {
                int slot = i;
                float y = -156f - i * RowH;
                rows.Add(ButtonUi("Row" + i, panel, new Vector2(20, y), new Vector2(W - 40, RowH - 4f), "", RowBg, 15, TextAnchor.MiddleLeft,
                    () => Order(page * PageSize + slot)));
            }
            float footer = -164f - PageSize * RowH;
            pageButton = ButtonUi("Page", panel, new Vector2(20, footer), new Vector2(300, 30), "Ещё (0)", RowBg, 15, TextAnchor.MiddleCenter,
                () => { page = page == 0 ? 1 : 0; Refresh(); });
            ButtonUi("Close", panel, new Vector2(330, footer), new Vector2(310, 30), "Закрыть (Esc)", new Color(.32f, .2f, .2f), 15, TextAnchor.MiddleCenter, Close);
            Label("Fine", panel, new Vector2(20, footer - 34f), new Vector2(W - 40, 20), 12, new Color(.67f, .76f, .8f)).text
                = "Прокат на сутки, наличными. Выше скал Пастухова без этого не пускают.";
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (panel == null) return;
            var me = Bootstrap.LocalHiker;
            var have = Have(me);
            var missing = Ascent.Missing(have);
            title.text = "Прокат снаряжения";
            purseLine.text = $"В кошельке {CafeService.Purse.Text}";
            missingLine.text = missing == Gear.None
                ? "Комплект собран. Наверх можно."
                : "Не хватает: " + Ascent.MissingList(have);
            int bundle = Rental.BundleRoubles(have);
            setButton.gameObject.SetActive(missing != Gear.None);
            if (missing != Gear.None)
            {
                setButton.GetComponentInChildren<Text>().text = $"1. Весь недостающий комплект — {bundle} ₽  ·  {Rental.SetKg:0.0} кг · {Rental.SetLitres:0} л";
                setButton.GetComponent<Image>().color = CafeService.Purse.CanAfford(bundle) ? SetBg : Poor;
            }
            int pages = (Board.Length + PageSize - 1) / PageSize;
            if (page >= pages) page = 0;
            pageButton.gameObject.SetActive(pages > 1);
            for (int i = 0; i < rows.Count; i++)
            {
                int index = page * PageSize + i;
                bool show = index < Board.Length;
                if (rows[i].gameObject.activeSelf != show) rows[i].gameObject.SetActive(show);
                if (!show) continue;
                var h = Board[index];
                bool owned = (have & h.Piece) != 0;
                rows[i].GetComponentInChildren<Text>().text = owned
                    ? $"{i + 2}. {h.Name} — уже в рюкзаке"
                    : $"{i + 2}. {h.Name} — {h.Roubles} ₽ · {h.Kg:0.0#} кг";
                rows[i].GetComponent<Image>().color = owned ? Poor : CafeService.Purse.CanAfford(h.Roubles) ? RowBg : Poor;
            }
        }

        // ── HUD line, and holding the mouse free while the window is up ──────────────────────────────────
        void LateUpdate()
        {
            if (open)
            {
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
            if (ordered != Gear.None) return "Прокат · подбирают размер…";
            if (open) return "Прокат · 1 — весь комплект · 2–9 — по одному · 0 — ещё · Esc — закрыть";
            if (near == null) return "";
            var have = Have(Bootstrap.LocalHiker);
            return Ascent.Missing(have) == Gear.None
                ? $"Прокат · комплект собран · E — открыть · {CafeService.Purse.Text}"
                : $"Прокат снаряжения · E — открыть · комплект {Rental.BundleRoubles(have)} ₽ · в кошельке {CafeService.Purse.Text}";
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

    /// <summary>The hire counter on the host. Money is the client's own business; what goes into the rucksack is the
    /// host's, so the order comes here and the host fills it out of <see cref="Rental"/>.</summary>
    public sealed partial class NightSession
    {
        /// <summary>Local: what came of our last hire (what was asked for, what was actually handed over).</summary>
        public event System.Action<ushort, ushort> Hired;

        /// <summary>Owner asks the counter for a set of pieces.</summary>
        public void HireGear(Gear wanted) => HireRpc((ushort)wanted);

        [Rpc(SendTo.Server)]
        void HireRpc(ushort wanted, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            string token = Token(sender);
            var served = Gear.None;
            if (Climb.On && run != null && packs != null
                && run.Players.TryGetValue(token, out var p) && p.Outcome == Outcome.None)
            {
                var have = Rental.Carried(packs, token);
                foreach (var h in Rental.Board())
                {
                    if (((Gear)wanted & h.Piece) == 0 || (have & h.Piece) != 0) continue;
                    if (packs.Receive(token, new ItemStack(h.Item)) != PackResult.Ok) continue;
                    served |= h.Piece;
                }
                if (served != Gear.None)
                {
                    SyncPacks();
                    run.Record($"{p.Name} берёт в прокате: {Rental.Titles(served)}.");
                }
            }
            HiredRpc(wanted, (ushort)served, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void HiredRpc(ushort wanted, ushort served, RpcParams rpc) => Hired?.Invoke(wanted, served);
    }
}
