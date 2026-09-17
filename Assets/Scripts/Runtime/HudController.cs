using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>All UI is built from code so the project stays text-only: menu, night HUD and the field protocol.</summary>
    public sealed class HudController : MonoBehaviour
    {
        static Font font;
        Canvas canvas;
        GameObject menu, hud, protocol;
        InputField nameField, addressField;
        Text status, clock, direction, party, hint, outcomeTitle, outcomeNote, summary, pairOutcome, events;
        Slider heat, hands, clarity;
        Button fireButton;
        Text debug;
        // navigation
        RectTransform miniRoot, miniMap, miniArrow, bigRoot, bigMark;
        RawImage miniImage;
        Text bigNote, heldLabel;
        Texture2D mapTex;
        bool miniOn = true;
        const float MiniMetres = 700f;
        bool protocolShown;
        int shownEvents;

        static readonly Color Ink = new Color(.93f, .95f, .96f), Paper = new Color(.86f, .89f, .89f), Panel = new Color(.04f, .11f, .16f, .93f), Amber = new Color(.91f, .79f, .6f);

        public static HudController Create()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD", typeof(HudController));
            return go.GetComponent<HudController>();
        }

        void Awake()
        {
            canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvas.transform.SetParent(transform, false);
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            BuildMenu();
            BuildHud();
            BuildNavigation();
            BuildProtocol();
            ShowMenu(true);
        }

        // ---- building blocks -------------------------------------------------
        RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>A panel centred on the screen whose children lay out from its top-left corner.</summary>
        RectTransform Centered(string name, Transform parent, Vector2 size)
        {
            var rt = Rect(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
            rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(-size.x / 2f, size.y / 2f);
            return rt;
        }

        Image PanelImage(RectTransform rt, Color c) { var i = rt.gameObject.AddComponent<Image>(); i.color = c; return i; }

        Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal, Vector2? anchors = null)
        {
            var a = anchors ?? new Vector2(0, 1);
            var rt = Rect(name, parent, a, a, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = anchor; t.fontStyle = style; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button ButtonUi(string name, Transform parent, Vector2 pos, Vector2 size, string text, Color bg, Color fg, System.Action onClick, Vector2? anchors = null)
        {
            var a = anchors ?? new Vector2(0, 1);
            var rt = Rect(name, parent, a, a, pos, size);
            PanelImage(rt, bg);
            var b = rt.gameObject.AddComponent<Button>();
            var t = Label("Text", rt, Vector2.zero, size, 18, fg, TextAnchor.MiddleCenter);
            t.text = text;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero; t.rectTransform.anchoredPosition = Vector2.zero;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        InputField Field(string name, Transform parent, Vector2 pos, Vector2 size, string value)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            PanelImage(rt, new Color(.08f, .17f, .21f));
            var f = rt.gameObject.AddComponent<InputField>();
            var t = Label("Text", rt, new Vector2(10, -6), new Vector2(size.x - 20, size.y - 12), 18, Ink, TextAnchor.MiddleLeft);
            t.supportRichText = false;
            f.textComponent = t; f.text = value; f.characterLimit = 24;
            return f;
        }

        // ---- screens ---------------------------------------------------------
        void BuildMenu()
        {
            menu = Rect("Menu", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            PanelImage(menu.GetComponent<RectTransform>(), new Color(.07f, .14f, .18f, .92f));
            var card = Centered("Card", menu.transform, new Vector2(440, 560));
            PanelImage(card, new Color(.05f, .1f, .14f, .85f));
            Label("Kicker", card, new Vector2(32, -30), new Vector2(380, 20), 12, new Color(.71f, .79f, .81f)).text = "СЕВЕРНЫЙ УРАЛ / UNITY-ПРОТОТИП";
            Label("Title", card, new Vector2(32, -60), new Vector2(380, 90), 84, Ink, TextAnchor.UpperLeft, FontStyle.Bold).text = "1079";
            Label("Sub", card, new Vector2(34, -150), new Vector2(380, 24), 15, Ink).text = "В Ы С О Т А";
            Label("Lede", card, new Vector2(32, -190), new Vector2(380, 60), 15, new Color(.74f, .8f, .83f)).text = "Одна ночь. Холод. Дорога наверх.\nДоберитесь от леса до укрытия прежде, чем рассветёт.";
            Label("NameLabel", card, new Vector2(32, -262), new Vector2(380, 18), 12, new Color(.67f, .76f, .8f)).text = "Ваше имя";
            nameField = Field("Name", card, new Vector2(32, -284), new Vector2(376, 40), "Путник");
            Label("AddrLabel", card, new Vector2(32, -336), new Vector2(380, 18), 12, new Color(.67f, .76f, .8f)).text = "Адрес хоста (для подключения)";
            addressField = Field("Address", card, new Vector2(32, -358), new Vector2(376, 40), "127.0.0.1");
            ButtonUi("Host", card, new Vector2(32, -416), new Vector2(180, 44), "СОЗДАТЬ НОЧЬ", new Color(.86f, .9f, .91f), new Color(.09f, .16f, .19f), () => Bootstrap.Host(nameField.text));
            ButtonUi("Join", card, new Vector2(228, -416), new Vector2(180, 44), "ПРИСОЕДИНИТЬСЯ", new Color(.2f, .29f, .34f), Ink, () => Bootstrap.Join(nameField.text, addressField.text));
            if (Bootstrap.Lobby != null)
            {
                ButtonUi("SteamHost", card, new Vector2(32, -470), new Vector2(180, 40), "НОЧЬ ЧЕРЕЗ STEAM", new Color(.11f, .2f, .29f), Ink, () => Bootstrap.Lobby.Host(nameField.text, SetStatus));
                ButtonUi("SteamInvite", card, new Vector2(228, -470), new Vector2(180, 40), "ПРИГЛАСИТЬ ДРУЗЕЙ", new Color(.11f, .2f, .29f), Ink, () => Bootstrap.Lobby.Invite());
            }
            status = Label("Status", card, new Vector2(32, -520), new Vector2(380, 40), 12, new Color(.69f, .76f, .78f));
            status.text = Bootstrap.Lobby != null && Bootstrap.Lobby.Available ? "Steam подключён. Друзья заходят через приглашение или список друзей." : "Хост открывает порт 7777. Steam-лобби включается сборкой со Steam (см. README).";
        }

        void BuildHud()
        {
            hud = Rect("Night", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var box = Rect("Box", hud.transform, Vector2.zero, Vector2.zero, new Vector2(20, 20), new Vector2(360, 250));
            PanelImage(box, Panel);
            clock = Label("Clock", box, new Vector2(22, -14), new Vector2(320, 44), 34, Ink);
            direction = Label("Direction", box, new Vector2(22, -58), new Vector2(320, 24), 16, Ink);
            party = Label("Party", box, new Vector2(22, -82), new Vector2(320, 20), 12, new Color(.69f, .76f, .8f));
            hint = Label("Hint", box, new Vector2(22, -106), new Vector2(320, 26), 15, Amber);
            fireButton = ButtonUi("Fire", box, new Vector2(22, -136), new Vector2(150, 30), "Разжечь костёр", new Color(.67f, .47f, .29f), Color.white, () => Bootstrap.AutoKindle = !Bootstrap.AutoKindle);
            heat = Meter("ТЕПЛО", box, new Vector2(22, -178));
            hands = Meter("РУКИ", box, new Vector2(132, -178));
            clarity = Meter("ЯСНОСТЬ", box, new Vector2(242, -178));
            Label("Keys", box, new Vector2(22, -214), new Vector2(330, 32), 11, new Color(.69f, .76f, .8f)).text = "WASD · Shift бег · V вид · E костёр · Esc пауза\n1 компас · 2 фонарик (F — свет) · 3/M карта · Q убрать · N мини-карта";
            var strip = Rect("DebugStrip", hud.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 26));
            strip.pivot = new Vector2(0, 1); PanelImage(strip, new Color(0, 0, 0, .55f));
            debug = Label("Debug", strip, new Vector2(12, -5), new Vector2(1200, 18), 14, new Color(1f, .95f, .8f));
        }


        static Sprite circle;
        static Sprite Circle()
        {
            if (circle != null) return circle;
            const int S = 256;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float r = new Vector2(x + .5f - S / 2f, y + .5f - S / 2f).magnitude / (S / 2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - r) * S / 2f)));
                }
            t.Apply();
            circle = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
            return circle;
        }

        static Sprite triangle;
        /// <summary>Heading marker pointing up (+y) in its own space; the HUD rotates it by the hiker's yaw.</summary>
        static Sprite Triangle()
        {
            if (triangle != null) return triangle;
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float v = (y + .5f) / S, u = Mathf.Abs((x + .5f) / S - .5f) * 2f;
                    float edge = (1f - v) * .9f - u;          // apex at the top
                    float notch = v < .25f ? Mathf.Abs(u) - (.25f - v) * 2.2f : 1f;
                    float a = Mathf.Clamp01(edge * 40f) * Mathf.Clamp01(notch * 40f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            triangle = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
            return triangle;
        }

        static Font handFont;
        static Font Hand
        {
            get
            {
                if (handFont == null) handFont = Resources.Load<Font>("World/Fonts/Caveat-700");
                return handFont != null ? handFont : font;
            }
        }

        void BuildNavigation()
        {
            mapTex = Resources.Load<Texture2D>("World/Textures/kalka_1959");
            // Mini-map: a round window onto the kalka, north up, top-right.
            miniRoot = Rect("MiniMap", hud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -44), new Vector2(230, 230));
            miniRoot.pivot = new Vector2(1, 1);
            var rim = PanelImage(miniRoot, new Color(.05f, .1f, .14f, .9f)); rim.sprite = Circle();
            var maskRt = Rect("Mask", miniRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            maskRt.offsetMin = new Vector2(6, 6); maskRt.offsetMax = new Vector2(-6, -6);
            var maskImg = PanelImage(maskRt, Color.white); maskImg.sprite = Circle();
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            miniMap = Rect("Map", maskRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            miniImage = miniMap.gameObject.AddComponent<RawImage>();
            miniImage.texture = mapTex; miniImage.color = mapTex != null ? Color.white : new Color(.8f, .82f, .8f);
            miniArrow = Rect("You", miniRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(30, 30));
            miniArrow.pivot = new Vector2(.5f, .5f);
            var arrow = miniArrow.gameObject.AddComponent<Image>();
            arrow.sprite = Triangle(); arrow.color = new Color(.65f, .1f, .08f); arrow.raycastTarget = false;
            var n = Label("N", miniRoot, new Vector2(0, -2), new Vector2(40, 30), 24, new Color(.93f, .9f, .82f), TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(.5f, 1f));
            n.rectTransform.pivot = new Vector2(.5f, 1f); n.font = Hand; n.text = "С";
            var scale = Label("Scale", miniRoot, new Vector2(0, -8), new Vector2(200, 20), 12, new Color(.75f, .8f, .82f), TextAnchor.UpperCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            scale.rectTransform.pivot = new Vector2(.5f, 1f); scale.text = $"{MiniMetres:0} м по диаметру";

            // Full map: the kalka unfolded in the hands.
            bigRoot = Rect("BigMap", hud.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PanelImage(bigRoot, new Color(.02f, .05f, .07f, .72f)).raycastTarget = false;
            var sheet = Rect("Sheet", bigRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(820, 820));
            sheet.pivot = new Vector2(.5f, .5f); sheet.localRotation = Quaternion.Euler(0, 0, -1.2f);
            var img = sheet.gameObject.AddComponent<RawImage>(); img.texture = mapTex; img.raycastTarget = false;
            bigMark = Rect("Mark", sheet, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(44, 44));
            bigMark.pivot = new Vector2(.5f, .5f);
            var mark = Label("X", bigMark, Vector2.zero, new Vector2(44, 44), 40, new Color(.12f, .15f, .35f), TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, .5f));
            mark.rectTransform.pivot = new Vector2(.5f, .5f); mark.font = Hand; mark.text = "×";
            bigNote = Label("Note", bigRoot, new Vector2(0, 36), new Vector2(900, 30), 22, new Color(.9f, .88f, .8f), TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            bigNote.rectTransform.pivot = new Vector2(.5f, 0f); bigNote.font = Hand;
            bigRoot.gameObject.SetActive(false);

            heldLabel = Label("Held", hud.transform, new Vector2(0, 28), new Vector2(600, 24), 16, new Color(.9f, .88f, .8f), TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            heldLabel.rectTransform.pivot = new Vector2(.5f, 0f);
        }

        void UpdateNavigation(HikerController me, float x, float z)
        {
            if (Controls.MiniMap) miniOn = !miniOn;
            var held = (HeldItem)me.Held.Value;
            bool big = held == HeldItem.Map;
            miniRoot.gameObject.SetActive(miniOn && !big);
            float W = WorldData.Size;
            if (miniOn && !big)
            {
                float span = MiniMetres / W;
                miniImage.uvRect = new Rect((x + W / 2) / W - span / 2, (z + W / 2) / W - span / 2, span, span);
                miniArrow.localRotation = Quaternion.Euler(0, 0, -me.Yaw);
            }
            bigRoot.gameObject.SetActive(big);
            if (big)
            {
                bigMark.anchoredPosition = new Vector2((x + W / 2) / W * 820f, (z + W / 2) / W * 820f);
                float heading = (me.Yaw % 360f + 360f) % 360f;
                bigNote.text = $"× — где вы, по памяти и ориентирам · курс {heading:0}° · 3/M — сложить карту";
            }
            var gear = me.Gear;
            heldLabel.text = held == HeldItem.Compass ? "Компас: стрелка на магнитный север, склонение +19° (к востоку)"
                : held == HeldItem.Flashlight ? (me.TorchOn.Value ? (gear != null && gear.Battery <= 0f ? "Фонарик включён, батарея села" : $"Фонарик · батарея {Mathf.CeilToInt((gear != null ? gear.Battery : 1f) * 100f)}% · F — выключить") : "Фонарик · F — включить")
                : "";
        }

        Slider Meter(string title, Transform parent, Vector2 pos)
        {
            Label(title, parent, pos, new Vector2(100, 14), 10, new Color(.69f, .76f, .8f)).text = title;
            var rt = Rect(title + "Bar", parent, new Vector2(0, 1), new Vector2(0, 1), pos + new Vector2(0, -18), new Vector2(96, 8));
            PanelImage(rt, new Color(.2f, .27f, .31f));
            var fillRt = Rect("Fill", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fillRt.pivot = Vector2.zero;
            var fill = PanelImage(fillRt, Ink);
            var s = rt.gameObject.AddComponent<Slider>();
            s.minValue = 0; s.maxValue = 100; s.interactable = false; s.transition = Selectable.Transition.None;
            s.fillRect = fillRt; s.targetGraphic = null;
            fill.type = Image.Type.Simple;
            return s;
        }

        void BuildProtocol()
        {
            protocol = Rect("Protocol", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            PanelImage(protocol.GetComponent<RectTransform>(), new Color(.03f, .07f, .11f, .92f));
            var sheet = Centered("Sheet", protocol.transform, new Vector2(640, 660));
            PanelImage(sheet, Paper);
            var inkDark = new Color(.13f, .19f, .24f);
            Label("Kicker", sheet, new Vector2(40, -36), new Vector2(560, 18), 11, inkDark).text = "1079 / ПОЛЕВОЙ ПРОТОКОЛ";
            outcomeTitle = Label("Title", sheet, new Vector2(40, -60), new Vector2(560, 44), 34, inkDark);
            outcomeNote = Label("Note", sheet, new Vector2(40, -110), new Vector2(560, 60), 14, inkDark);
            summary = Label("Summary", sheet, new Vector2(40, -176), new Vector2(560, 22), 14, inkDark);
            pairOutcome = Label("Pair", sheet, new Vector2(40, -202), new Vector2(560, 44), 14, new Color(.55f, .42f, .22f));
            events = Label("Events", sheet, new Vector2(40, -252), new Vector2(560, 300), 13, inkDark);
            ButtonUi("Again", sheet, new Vector2(40, -580), new Vector2(180, 40), "Новая попытка", new Color(.14f, .23f, .29f), Color.white, Bootstrap.Leave);
            Label("Fine", sheet, new Vector2(40, -630), new Vector2(560, 20), 11, new Color(.35f, .42f, .47f)).text = "Игровая история вымышленных участников. Исходы не являются историческими выводами.";
        }

        public void ShowMenu(bool on)
        {
            menu.SetActive(on); hud.SetActive(!on); protocol.SetActive(false); protocolShown = false; shownEvents = 0; events.text = "";
        }

        public void SetStatus(string text) { if (status != null) status.text = text; }

        void Update()
        {
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            if (menu.activeSelf || s == null || me == null) return;
            float x = me.transform.position.x, z = me.transform.position.z;
            if (debug != null && Camera.main != null)
            {
                var c = Camera.main.transform.position;
                debug.text = $"cam {c.x:0},{c.y:0},{c.z:0} · ground under cam {TerrainBuilder.Height(Bootstrap.Dem, c.x, c.z):0} · me {x:0},{me.transform.position.y:0},{z:0} · ground {TerrainBuilder.Height(Bootstrap.Dem, x, z):0} · fog {RenderSettings.fogDensity:0.0000} · v {me.Speed:0.0} · keys {Controls.Debug} · {(me.FirstPerson ? "1st" : "3rd")} · grounded {me.Grounded}";
            }
            clock.text = SurvivalRules.NightTime(s.Elapsed.Value);
            float dist = WorldData.Distance(x, z, WorldData.Tent.X, WorldData.Tent.Z);
            float bearing = Mathf.DeltaAngle(me.Yaw, Mathf.Atan2(WorldData.Tent.X - x, WorldData.Tent.Z - z) * Mathf.Rad2Deg);
            direction.text = $"{(Mathf.Abs(bearing) < 17f ? "↑" : bearing > 0 ? "→" : "←")} Верхнее укрытие · {Mathf.RoundToInt(dist)} м";
            var sb = new StringBuilder();
            foreach (var p in s.Party) if (!p.you) sb.Append(sb.Length > 0 ? " · " : "").Append($"{p.name}: {(p.outcome != Outcome.None ? SurvivalRules.Describe(p.outcome).Title.ToLowerInvariant() : p.online ? "на склоне" : "без связи")}");
            party.text = sb.ToString();
            bool nearFire = WorldData.NearCamp(x, z);
            float fire = s.FireRemaining.Value;
            fireButton.gameObject.SetActive(nearFire && fire <= 0f);
            fireButton.GetComponentInChildren<Text>().text = Bootstrap.AutoKindle ? "Прекратить розжиг" : "Разжечь костёр";
            string kindle = s.KindleNeeded > 0 ? $" {Mathf.Min(99, Mathf.RoundToInt(s.KindleProgress / s.KindleNeeded * 100f))}%" : "";
            hint.text = !NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost ? "Связь потеряна · ночь идёт на сервере"
                : me.Paused ? "Пауза · ночь продолжается, нажмите на сцену"
                : nearFire ? (fire > 0 ? $"У огня · ещё {Mathf.CeilToInt(fire)} с" : s.KindleNeeded > 0 ? $"Разжигаете… держите E{kindle}" : "Кострище · стойте и удерживайте E")
                : s.Heat < 30 ? "Холод мешает думать. Вернитесь к огню." : s.Storm.Value ? "Метель. Держитесь рядом." : "Свет уходит. Выбирайте путь.";
            heat.value = s.Heat; hands.value = s.Hands; clarity.value = s.Clarity;
            UpdateNavigation(me, x, z);

            if (s.MyOutcome != Outcome.None && !protocolShown)
            {
                protocolShown = true; protocol.SetActive(true);
                var text = SurvivalRules.Describe(s.MyOutcome);
                outcomeTitle.text = text.Title; outcomeNote.text = text.Note;
                summary.text = $"{SurvivalRules.NightTime(s.Elapsed.Value)} · пройдено {Mathf.RoundToInt(s.Elapsed.Value)} с · до укрытия {Mathf.RoundToInt(dist)} м · на открытом ветру {Mathf.RoundToInt(s.Exposure)} с";
            }
            if (protocolShown)
            {
                var others = s.Party.FindAll(p => !p.you);
                var room = (Outcome)s.RoomOutcome.Value;
                pairOutcome.text = others.Count == 0 ? "" : room != Outcome.None
                    ? $"Итог ночи для комнаты: {SurvivalRules.Describe(room).Title}. {SurvivalRules.Describe(room).Note}"
                    : "Ночь комнаты продолжается: " + string.Join(", ", others.ConvertAll(p => $"{p.name} — {(p.outcome != Outcome.None ? SurvivalRules.Describe(p.outcome).Title.ToLowerInvariant() : p.online ? "ещё на склоне" : "без связи")}")) + ".";
                if (shownEvents != s.Events.Count)
                {
                    var eb = new StringBuilder();
                    for (int i = 0; i < s.Events.Count; i++) eb.Append(i + 1).Append(". ").Append(SurvivalRules.NightTime(s.Events[i].Time)).Append(" — ").Append(s.Events[i].Text).Append('\n');
                    events.text = eb.ToString(); shownEvents = s.Events.Count;
                }
            }
        }
    }
}
