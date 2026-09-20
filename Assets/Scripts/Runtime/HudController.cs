using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>All UI is built from code so the project stays text-only: menu, night HUD and the field protocol.</summary>
    public sealed partial class HudController : MonoBehaviour
    {
        float fps = 60f;
        static Font font;
        Canvas canvas;
        GameObject menu, hud, protocol;
        Button kholatButton, elbrusButton, hostButton, continueButton;
        Text kicker, lede;
        static readonly Color PlaceOff = new Color(.13f, .22f, .27f), PlaceOn = new Color(.28f, .45f, .5f);
        InputField nameField, addressField;
        Text status, clock, direction, party, hint, outcomeTitle, outcomeNote, summary, pairOutcome, events, prompt, keys, travel;
        Slider heat, hands, clarity;
        Button fireButton, workButton;
        /// <summary>The ascent block: the clock and the turn-round time, what is underfoot, what the air is doing, what
        /// is going wrong with the body, and what to do about it. Elbrus only, and quiet when nothing is the matter.</summary>
        Text climbLine;
        Button cramponButton, thermosButton;
        RectTransform hudBox;
        float workStarted, workNeeded;
        WorkKind workKind;
        Text debug;
        float debugNext, panelNext;
        StringBuilder partyText;
        // navigation
        RectTransform miniRoot, miniMap, miniArrow, miniCone, bigRoot, bigMark;
        Image miniConeImage, bigConeImage;
        RawImage miniImage, bigImage;
        Text bigNote, heldLabel, miniScale;
        // rucksack window
        RectTransform packPanel, packRows;
        Text packTitle, packLoad, packHandLine;
        Button packWearButton, packStowButton, packDropButton, packPickButton, packCampButton, packSleepButton, packLeaveButton, packSosButton;
        readonly System.Collections.Generic.List<Button> packRowPool = new System.Collections.Generic.List<Button>();
        string packShown = "";
        Texture2D mapTex;
        bool miniOn = true;
        /// <summary>Diameter of the mini-map window in metres. The Elbrus frame is three times the Kholat one, so the
        /// window opens up with it: on the southern slope you want the next station in the circle, not the next boulder.</summary>
        /// <summary>How much ground the round window shows, across its diameter.
        ///
        /// 436 m is not a round number on purpose: it is where the Kholat sheet is drawn 1:1. The sheet is
        /// 2048 px over 4096 m — half a pixel per metre — and the window inside its rim is 218 px, so 218 / 0.5
        /// is 436 m and anything closer is Unity inventing pixels that are not in the raster. At 700 m the
        /// thirty metres you can actually see at night came to nine pixels, all of them under the arrow.
        ///
        /// Elbrus keeps 1400: its own sheet is 2048 px over 12 288 m, a sixth of a pixel per metre, so 1307 m
        /// is its 1:1 and 1400 is already as close as that sheet goes.
        ///
        /// Zooming further would need more than a number. The labels are printed into the raster, so they
        /// magnify with it — past this point the word «лабаз» starts filling the window instead of naming a
        /// place, and no filtering setting helps with that.</summary>
        static float MiniMetres => World.IsElbrus ? 1400f : 436f;
        /// <summary>Side of the unfolded map sheet, in reference pixels of the canvas (1600 × 900).</summary>
        const float BigSheet = 820f;
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
            BuildPack();
            BuildPlan();
            BuildProtocol();
            BuildSettings();
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

        /// <summary>Picks the place before the session starts. Both players have to pick the same one:
        /// the location is not negotiated over the network yet (docs/BACKLOG.md).</summary>
        void SetPlace(Place place)
        {
            Bootstrap.SetPlace(place);
            bool elbrus = place == Place.Elbrus;
            if (kholatButton != null) kholatButton.GetComponent<Image>().color = elbrus ? PlaceOff : PlaceOn;
            if (elbrusButton != null) elbrusButton.GetComponent<Image>().color = elbrus ? PlaceOn : PlaceOff;
            if (kicker != null) kicker.text = elbrus ? "ПРИЭЛЬБРУСЬЕ / UNITY-ПРОТОТИП" : "СЕВЕРНЫЙ УРАЛ / UNITY-ПРОТОТИП";
            if (lede != null) lede.text = elbrus
                ? "Поляна Азау, 2350 м. Канатная дорога до Гара-Баши,\nратрак до 5100 и пешком на вершину 5642."
                : "Одна ночь. Холод. Дорога наверх.\nДоберитесь от леса до укрытия прежде, чем рассветёт.";
            var label = hostButton != null ? hostButton.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = elbrus ? "НАЧАТЬ ПОДЪЁМ" : "СОЗДАТЬ НОЧЬ";
            RefreshContinue();
            ApplyPlace();
        }

        /// <summary>«Продолжить» appears only when this place has a save, and says which one:
        /// «ПРОДОЛЖИТЬ · косая полка, 5290 м · 08:40 · вчера». The night on Kholat Syakhl is one night and never has
        /// one (<see cref="Camp.AllowedIn"/>), so the button simply is not there.</summary>
        void RefreshContinue()
        {
            if (continueButton == null) return;
            var slot = Saves.Newest(World.Current);
            bool show = !slot.IsEmpty;
            if (continueButton.gameObject.activeSelf != show) continueButton.gameObject.SetActive(show);
            if (!show) return;
            string where = string.IsNullOrEmpty(slot.Where) ? "склон" : slot.Where;
            continueButton.GetComponentInChildren<Text>().text =
                $"ПРОДОЛЖИТЬ · {where} · {AscentRoute.Clock(slot.Hour)} · {SaveStore.When(slot.SavedUtc.ToLocalTime(), System.DateTime.Now)}";
        }

        /// <summary>Everything the HUD draws differently in the two places. The HUD is built once and lives through
        /// every menu round, so the place is applied here as well as at build time — otherwise a player who picks
        /// Elbrus in the menu would climb it with the Kholat tracing paper in his hands.</summary>
        void ApplyPlace()
        {
            mapTex = Resources.Load<Texture2D>(World.IsElbrus ? "World/Textures/elbrus_map" : "World/Textures/kalka_1959");
            var tint = mapTex != null ? Color.white : new Color(.8f, .82f, .8f);
            if (miniImage != null) { miniImage.texture = mapTex; miniImage.color = tint; }
            if (bigImage != null) { bigImage.texture = mapTex; bigImage.color = tint; }
            if (miniScale != null) miniScale.text = MiniMetres >= 1000f ? $"{MiniMetres / 1000f:0.#} км по диаметру" : $"{MiniMetres:0} м по диаметру";
            if (keys != null) keys.text = World.IsElbrus
                ? "WASD · Shift бег (до 4600 м) · V вид · Esc пауза\nE — кабина, ратрак, кафе, прокат, спасатели, доска, нары\nC/F1 кошки · T/F2 термос · B/7 лагерь · P/8 ночёвка · 9 SOS\n1 компас · 3/M карта · N мини-карта · U/F3 программа · Tab рюкзак"
                : "WASD · Shift бег · V вид · E костёр · Esc пауза\n1 компас · 2 фонарик (F — свет) · 3/M карта · Q убрать · N мини-карта\nTab рюкзак · G снять/надеть · R взять/убрать · X бросить\nK/4 лыжи · L/5 палки · H/6 волокуша · F1 — следующий способ";
            if (travel != null) travel.gameObject.SetActive(!World.IsElbrus);
            if (climbLine != null) climbLine.gameObject.SetActive(World.IsElbrus);
            ApplyPlacePlan();
            // the ascent block is four lines where the snow line was one, so the panel grows with it
            if (hudBox != null) hudBox.sizeDelta = new Vector2(360, World.IsElbrus ? 426f : 316f);
            if (keys != null) keys.rectTransform.anchoredPosition = new Vector2(22, World.IsElbrus ? -356f : -248f);
            // on the mountain the amber line runs to two lines ("Дыхания не хватает…") and used to print through the
            // two hand buttons. They move under the ascent block instead, where they read as what to do about it.
            if (hint != null) hint.rectTransform.sizeDelta = new Vector2(320, World.IsElbrus ? 44f : 26f);
            if (cramponButton != null) cramponButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(22, World.IsElbrus ? -320f : -136f);
            if (thermosButton != null) thermosButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(212, World.IsElbrus ? -320f : -136f);
        }

        // ---- screens ---------------------------------------------------------
        void BuildMenu()
        {
            menu = Rect("Menu", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            PanelImage(menu.GetComponent<RectTransform>(), new Color(.07f, .14f, .18f, .92f));
            var card = Centered("Card", menu.transform, new Vector2(440, 752));
            PanelImage(card, new Color(.05f, .1f, .14f, .85f));
            kicker = Label("Kicker", card, new Vector2(32, -30), new Vector2(256, 20), 12, new Color(.71f, .79f, .81f));
            // the quality knobs, reachable before a night starts as well as inside one (F10)
            ButtonUi("Settings", card, new Vector2(300, -32), new Vector2(108, 26), "НАСТРОЙКИ",
                new Color(.13f, .23f, .27f), Ink, () => ShowSettings(!SettingsShown)).GetComponentInChildren<Text>().fontSize = 12;
            Label("Title", card, new Vector2(32, -60), new Vector2(380, 90), 84, Ink, TextAnchor.UpperLeft, FontStyle.Bold).text = "1079";
            Label("Sub", card, new Vector2(34, -150), new Vector2(380, 24), 15, Ink).text = "В Ы С О Т А";
            lede = Label("Lede", card, new Vector2(32, -190), new Vector2(380, 60), 15, new Color(.74f, .8f, .83f));
            Label("PlaceLabel", card, new Vector2(32, -256), new Vector2(380, 18), 12, new Color(.67f, .76f, .8f)).text = "Где играем";
            kholatButton = ButtonUi("PlaceKholat", card, new Vector2(32, -278), new Vector2(186, 40), "ХОЛАТЧАХЛЬ", PlaceOff, Ink, () => SetPlace(Place.Kholat));
            elbrusButton = ButtonUi("PlaceElbrus", card, new Vector2(222, -278), new Vector2(186, 40), "ЭЛЬБРУС", PlaceOff, Ink, () => SetPlace(Place.Elbrus));
            Label("NameLabel", card, new Vector2(32, -332), new Vector2(380, 18), 12, new Color(.67f, .76f, .8f)).text = "Ваше имя";
            nameField = Field("Name", card, new Vector2(32, -354), new Vector2(376, 40), "Путник");
            Label("AddrLabel", card, new Vector2(32, -406), new Vector2(380, 18), 12, new Color(.67f, .76f, .8f)).text = "Адрес хоста (для подключения)";
            addressField = Field("Address", card, new Vector2(32, -428), new Vector2(376, 40), "127.0.0.1");
            hostButton = ButtonUi("Host", card, new Vector2(32, -486), new Vector2(180, 44), "СОЗДАТЬ НОЧЬ", new Color(.86f, .9f, .91f), new Color(.09f, .16f, .19f), () => Bootstrap.Host(nameField.text));
            ButtonUi("Join", card, new Vector2(228, -486), new Vector2(180, 44), "ПРИСОЕДИНИТЬСЯ", new Color(.2f, .29f, .34f), Ink, () => Bootstrap.Join(nameField.text, addressField.text));
            // «Продолжить» stands beside «Создать ночь» and says where and when the ascent was left, because that is
            // the only thing worth knowing before pressing it. No save for this place: no button (RefreshContinue).
            continueButton = ButtonUi("Continue", card, new Vector2(32, -538), new Vector2(376, 40), "ПРОДОЛЖИТЬ",
                new Color(.26f, .4f, .34f), Ink, () => Bootstrap.Continue(nameField.text));
            continueButton.GetComponentInChildren<Text>().fontSize = 14;
            continueButton.gameObject.SetActive(false);
            if (Bootstrap.Lobby != null)
            {
                ButtonUi("SteamHost", card, new Vector2(32, -586), new Vector2(180, 40), "НОЧЬ ЧЕРЕЗ STEAM", new Color(.11f, .2f, .29f), Ink, () => Bootstrap.Lobby.Host(nameField.text, SetStatus));
                ButtonUi("SteamInvite", card, new Vector2(228, -586), new Vector2(180, 40), "ПРИГЛАСИТЬ ДРУЗЕЙ", new Color(.11f, .2f, .29f), Ink, () => Bootstrap.Lobby.Invite());
            }
            else
            {
                // films the demo reel: eight shots over both places, straight out of the running world
                ButtonUi("Demo", card, new Vector2(32, -586), new Vector2(376, 36), "СНЯТЬ ДЕМО-РОЛИК", new Color(.13f, .23f, .27f), Ink,
                    () => { if (!DemoReel.Running) DemoReel.Shoot(); });
            }
            // the physics sandbox: the body, a test range and every number it is made of, with no world, no night and
            // no network in the way (docs/PHYSICS.md). Coming back from it rebuilds the game from scratch.
            ButtonUi("Sandbox", card, new Vector2(32, -630), new Vector2(376, 36), "ПЕСОЧНИЦА",
                new Color(.24f, .2f, .3f), Ink, Bootstrap.OpenSandbox).GetComponentInChildren<Text>().fontSize = 13;
            SetPlace(World.Current);
            status = Label("Status", card, new Vector2(32, -674), new Vector2(380, 40), 12, new Color(.69f, .76f, .78f));
            status.text = Bootstrap.Lobby != null && Bootstrap.Lobby.Available ? "Steam подключён. Друзья заходят через приглашение или список друзей." : "Хост открывает порт 7777. Steam-лобби включается сборкой со Steam (см. README).";
        }

        void BuildHud()
        {
            hud = Rect("Night", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var box = Rect("Box", hud.transform, Vector2.zero, Vector2.zero, new Vector2(20, 20), new Vector2(360, 316));
            PanelImage(box, Panel);
            clock = Label("Clock", box, new Vector2(22, -14), new Vector2(320, 44), 34, Ink);
            direction = Label("Direction", box, new Vector2(22, -58), new Vector2(320, 24), 16, Ink);
            party = Label("Party", box, new Vector2(22, -82), new Vector2(320, 20), 12, new Color(.69f, .76f, .8f));
            hint = Label("Hint", box, new Vector2(22, -106), new Vector2(320, 26), 15, Amber);
            fireButton = ButtonUi("Fire", box, new Vector2(22, -136), new Vector2(150, 30), "Разжечь костёр", new Color(.67f, .47f, .29f), Color.white, () => Bootstrap.AutoKindle = !Bootstrap.AutoKindle);
            workButton = ButtonUi("Work", box, new Vector2(182, -136), new Vector2(156, 30), "Работать", new Color(.32f, .45f, .5f), Color.white, () => Bootstrap.AutoWork = !Bootstrap.AutoWork);
            // the two things a climber does with his hands. Letter keys (C, T) do not reach the game under UI
            // automation, so both have a button as well as a spare function key — the same rule G/R/X follow.
            cramponButton = ButtonUi("Crampons", box, new Vector2(22, -136), new Vector2(180, 30), "Надеть кошки", new Color(.29f, .4f, .47f), Color.white,
                () => { var s = NightSession.Instance; var me = Bootstrap.LocalHiker; if (s != null && me != null && me.Climbing != null) s.RequestCrampons(!me.Climbing.CramponsOn); });
            thermosButton = ButtonUi("Thermos", box, new Vector2(212, -136), new Vector2(126, 30), "Термос", new Color(.4f, .33f, .27f), Color.white,
                () => NightSession.Instance?.RequestSip());
            cramponButton.gameObject.SetActive(false);
            thermosButton.gameObject.SetActive(false);
            heat = Meter("ТЕПЛО", box, new Vector2(22, -178));
            hands = Meter("РУКИ", box, new Vector2(132, -178));
            clarity = Meter("ЯСНОСТЬ", box, new Vector2(242, -178));
            // how you are getting through the snow, how deep it is under you, and what is left in the legs
            travel = Label("Travel", box, new Vector2(22, -212), new Vector2(330, 34), 12, Amber);
            // and, on the southern slope, what the mountain is doing to you instead
            climbLine = Label("Climb", box, new Vector2(22, -212), new Vector2(330, 104), 12, Amber);
            keys = Label("Keys", box, new Vector2(22, -248), new Vector2(330, 62), 11, new Color(.69f, .76f, .8f));
            hudBox = box;
            var promptBox = Rect("PromptBox", hud.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-300, 34), new Vector2(600, 34));
            promptBox.pivot = new Vector2(0, 0); PanelImage(promptBox, new Color(0, 0, 0, .45f));
            prompt = Label("Prompt", promptBox, Vector2.zero, new Vector2(600, 34), 16, new Color(1f, .93f, .78f), TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = Vector2.zero; prompt.rectTransform.anchorMax = Vector2.one; prompt.rectTransform.sizeDelta = Vector2.zero; prompt.rectTransform.anchoredPosition = Vector2.zero;
            promptBox.gameObject.SetActive(false);
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

        static Sprite cone;
        static float coneDegrees;
        /// <summary>The field of view as a wedge opening upward (+y) with its apex at the sprite's centre, so
        /// rotating the sprite turns the wedge about the point where the hiker stands. Its angle is the camera's
        /// real horizontal field of view; its length is not a distance and does not pretend to be one — at night
        /// with a torch you see thirty metres, which on the folded sheet is six pixels. It is drawn long enough to
        /// be read and faded out at the end so it is not mistaken for a range.
        ///
        /// A heading is the one thing a paper map cannot give you. The compass gives it as a number — «курс 232°» —
        /// and a number has to be converted into «that way» in the player's head every time. The wedge is the same
        /// fact, already converted, which is why nearly every game with a map draws one.</summary>
        static Sprite Cone(float degrees)
        {
            if (cone != null && Mathf.Abs(coneDegrees - degrees) < 1f) return cone;
            coneDegrees = degrees;
            const int S = 256;
            float half = degrees * .5f * Mathf.Deg2Rad;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float dx = x + .5f - S / 2f, dy = y + .5f - S / 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (S / 2f);
                    float a = 0f;
                    if (dy > 0f && r <= 1f)
                    {
                        float ang = Mathf.Atan2(Mathf.Abs(dx), dy);
                        a = Mathf.Clamp01((half - ang) * 12f)          // soft sides
                          * Mathf.Clamp01((1f - r) * 2.2f)             // fades out with distance
                          * Mathf.Clamp01(r * 10f);                    // and is not a blob on the apex
                    }
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            cone = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
            return cone;
        }

        /// <summary>Horizontal field of view of the hiker's camera, which is what the wedge has to match: the
        /// vertical 60° of <see cref="HikerController"/> widened by the aspect of the window it is drawn in.</summary>
        static float ViewDegrees()
        {
            var cam = Camera.main;
            float vert = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            return Mathf.Clamp(2f * Mathf.Atan(Mathf.Tan(vert * .5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg, 30f, 140f);
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
            // Kholat: the tracing paper the group copied their route onto. Elbrus: the printed tourist sheet of the
            // southern slope (ElbrusMapFactory). Both cover their whole frame, so world position maps to uv linearly.
            // Mini-map: a round window onto the sheet, north up, top-right.
            miniRoot = Rect("MiniMap", hud.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -44), new Vector2(230, 230));
            miniRoot.pivot = new Vector2(1, 1);
            var rim = PanelImage(miniRoot, new Color(.05f, .1f, .14f, .9f)); rim.sprite = Circle();
            var maskRt = Rect("Mask", miniRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            maskRt.offsetMin = new Vector2(6, 6); maskRt.offsetMax = new Vector2(-6, -6);
            var maskImg = PanelImage(maskRt, Color.white); maskImg.sprite = Circle();
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            miniMap = Rect("Map", maskRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            miniImage = miniMap.gameObject.AddComponent<RawImage>();
            // Inside the mask and after the sheet in the hierarchy: it has to be clipped by the round window
            // and drawn over the paper, not under it.
            miniCone = Rect("View", maskRt, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 150));
            miniCone.pivot = new Vector2(.5f, .5f);
            miniConeImage = miniCone.gameObject.AddComponent<Image>();
            miniConeImage.sprite = Cone(ViewDegrees());
            miniConeImage.color = new Color(.72f, .17f, .13f, .26f);
            miniConeImage.raycastTarget = false;
            // texture and scale come from ApplyPlace at the end of the method
            // The arrow sits on a drawn sheet, not on a dark game map: at 30 px it covered the name of
            // whatever the player was standing next to. Smaller, on a pale backing of its own shape, so it
            // separates from the paper without swallowing what is printed under it.
            miniArrow = Rect("You", miniRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(24, 24));
            miniArrow.pivot = new Vector2(.5f, .5f);
            var arrowBack = Rect("Halo", miniArrow, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(31, 31));
            arrowBack.pivot = new Vector2(.5f, .5f);
            var halo = arrowBack.gameObject.AddComponent<Image>();
            halo.sprite = Triangle(); halo.color = new Color(.96f, .95f, .90f, .85f); halo.raycastTarget = false;
            var arrowRt = Rect("Mark", miniArrow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var arrow = arrowRt.gameObject.AddComponent<Image>();
            arrow.sprite = Triangle(); arrow.color = new Color(.62f, .09f, .07f); arrow.raycastTarget = false;
            var n = Label("N", miniRoot, new Vector2(0, -2), new Vector2(40, 30), 24, new Color(.93f, .9f, .82f), TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(.5f, 1f));
            n.rectTransform.pivot = new Vector2(.5f, 1f); n.font = Hand; n.text = "С";
            miniScale = Label("Scale", miniRoot, new Vector2(0, -8), new Vector2(200, 20), 12, new Color(.75f, .8f, .82f), TextAnchor.UpperCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            miniScale.rectTransform.pivot = new Vector2(.5f, 1f);

            // Full map: the sheet unfolded in the hands, never quite straight.
            bigRoot = Rect("BigMap", hud.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PanelImage(bigRoot, new Color(.02f, .05f, .07f, .72f)).raycastTarget = false;
            var sheet = Rect("Sheet", bigRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(BigSheet, BigSheet));
            sheet.pivot = new Vector2(.5f, .5f); sheet.localRotation = Quaternion.Euler(0, 0, -1.2f);
            bigSheet = sheet;
            bigImage = sheet.gameObject.AddComponent<RawImage>(); bigImage.raycastTarget = false;
            // Where you are, and which way you are facing — one mark, turned by the hiker's yaw, the same
            // shape the mini-map uses. The whole group rotates, so the wedge and the arrowhead stay together.
            bigMark = Rect("Mark", sheet, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(150, 150));
            bigMark.pivot = new Vector2(.5f, .5f);
            var bigCone = Rect("View", bigMark, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 150));
            bigCone.pivot = new Vector2(.5f, .5f);
            bigConeImage = bigCone.gameObject.AddComponent<Image>();
            bigConeImage.sprite = Cone(ViewDegrees());
            bigConeImage.color = new Color(.70f, .16f, .12f, .30f);
            bigConeImage.raycastTarget = false;
            var youHalo = Rect("Halo", bigMark, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(33, 33));
            youHalo.pivot = new Vector2(.5f, .5f);
            var youHaloImg = youHalo.gameObject.AddComponent<Image>();
            youHaloImg.sprite = Triangle(); youHaloImg.color = new Color(.96f, .95f, .90f, .88f); youHaloImg.raycastTarget = false;
            var you = Rect("You", bigMark, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(26, 26));
            you.pivot = new Vector2(.5f, .5f);
            var youImg = you.gameObject.AddComponent<Image>();
            youImg.sprite = Triangle(); youImg.color = new Color(.62f, .09f, .07f); youImg.raycastTarget = false;
            bigNote = Label("Note", bigRoot, new Vector2(0, 36), new Vector2(900, 30), 22, new Color(.9f, .88f, .8f), TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            bigNote.rectTransform.pivot = new Vector2(.5f, 0f); bigNote.font = Hand;
            bigRoot.gameObject.SetActive(false);

            heldLabel = Label("Held", hud.transform, new Vector2(0, 28), new Vector2(600, 24), 16, new Color(.9f, .88f, .8f), TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(.5f, 0f));
            heldLabel.rectTransform.pivot = new Vector2(.5f, 0f);

            ApplyPlace();
        }

        /// <summary>The wedge is built when the HUD is, and the HUD is built before the hiker's camera exists,
        /// so the first sprite is made from the fallback aspect. This corrects it once the real camera is up —
        /// and then does nothing, because the guard matches and the sprite is never reassigned again.</summary>
        void RefreshCone()
        {
            float deg = ViewDegrees();
            if (Mathf.Abs(deg - coneDegrees) < 1f) return;
            var sp = Cone(deg);
            if (bigConeImage != null) bigConeImage.sprite = sp;
            if (miniConeImage != null) miniConeImage.sprite = sp;
        }

        void UpdateNavigation(HikerController me, float x, float z)
        {
            if (Controls.MiniMap) miniOn = !miniOn;
            var held = (HeldItem)me.Held.Value;
            bool big = held == HeldItem.Map;
            miniRoot.gameObject.SetActive(miniOn && !big);
            float W = World.Size;
            if (miniOn && !big)
            {
                float span = MiniMetres / W;
                miniImage.uvRect = new Rect((x + W / 2) / W - span / 2, (z + W / 2) / W - span / 2, span, span);
                miniArrow.localRotation = Quaternion.Euler(0, 0, -me.Yaw);
                miniCone.localRotation = Quaternion.Euler(0, 0, -me.Yaw);
            }
            bigRoot.gameObject.SetActive(big);
            if (miniOn || big) RefreshCone();
            if (big)
            {
                bigMark.anchoredPosition = new Vector2((x + W / 2) / W * BigSheet, (z + W / 2) / W * BigSheet);
                bigMark.localRotation = Quaternion.Euler(0, 0, -me.Yaw);
                float heading = (me.Yaw % 360f + 360f) % 360f;
                // On Elbrus the sheet is printed and folded, with a grid and a height on it, so the × is simply where
                // you are; on Kholat it is a tracing paper with no grid, and the cross is a guess from memory.
                bigNote.text = World.IsElbrus
                    ? $"Стрелка — вы, клин — куда смотрите · курс {heading:0}° · высота {me.transform.position.y:0} м · кружки — программа, красный — сейчас · 3/M — сложить карту"
                    : $"Стрелка — где вы, по памяти и ориентирам · клин — куда смотрите · курс {heading:0}° · 3/M — сложить карту";
            }
            var gear = me.Gear;
            heldLabel.text = held == HeldItem.Compass ? CompassLine(me)
                : held == HeldItem.Flashlight && gear != null && gear.IsZhuchok ? (me.TorchOn.Value ? "«Жучок» · жмите F, пока нужен свет · 2 — другой фонарь" : "«Жучок» (динамо) · держите F, чтобы светить · 2 — трубчатый фонарик")
                : held == HeldItem.Flashlight ? (me.TorchOn.Value ? (gear != null && gear.Battery <= 0f ? "Фонарик включён, батарея села" : $"Фонарик · батарея {Mathf.CeilToInt((gear != null ? gear.Battery : 1f) * 100f)}% · F — выключить") : "Фонарик · F — включить · 2 — «жучок»")
                : UnderCrosshair(me);
        }

        /// <summary>The compass in the hand. It always says what a compass says — where the needle points and how far
        /// that is from true north — and, on the southern slope, the azimuth the programme's current line was taken
        /// on, which is the other half of a bearing: the needle finds north, the reading finds the goal.</summary>
        string CompassLine(HikerController me)
        {
            const string card = "Компас · склонение +19° к востоку";
            var aim = Programmes.Aim(me);
            return aim.Has ? card + " · " + Programmes.AimLine(aim) : card + " · стрелка на магнитный север";
        }

        /// <summary>The line under the crosshair. The mountain takes it whenever it has something urgent to say —
        /// crampons going on, a slide, the gate at the rocks — and the rucksack has it the rest of the time.</summary>
        string UnderCrosshair(HikerController me)
        {
            string climb = me.Climbing != null ? me.Climbing.Prompt() : "";
            if (climb.Length > 0) return climb;
            string pack = Backpacks.Prompt(me);
            if (pack.Length > 0) return pack;
            return Camps.Prompt(me);
        }

        /// <summary>The line about the snow: how you are getting through it, how deep you are in it, whether somebody has
        /// already been this way, and what is left in the legs. The strength comes from the host
        /// (<see cref="NightSession.TickSkisServer"/>); everything else is read off the ground under the feet.</summary>
        void UpdateTravel(HikerController me)
        {
            if (travel == null) return;
            var gear = me.Skis;
            if (World.IsElbrus || gear == null) { travel.text = ""; return; }
            string track = gear.OnTrack ? " · по лыжне" : "";
            int left = Mathf.RoundToInt(me.Strength.Value / 255f * 100f);
            string legs = left < 30 ? $" · силы {left}% — надо отдышаться" : $" · силы {left}%";
            travel.text = $"{Skiing.Title(gear.Mode)} · {Skiing.SinkTitle(gear.Sink).ToLowerInvariant()}{track}{legs}";
        }

        /// <summary>The ascent block. Four lines at most, and it says nothing it does not have to: the clock and the
        /// turn-round time are always there, the ground and the air appear where they start to matter, the body speaks
        /// only when something is going wrong with it, and the last line is whatever there is to DO about it right now.
        /// Everything is read off the state the host publishes (<see cref="ClimbNet"/>) and the position, which both
        /// sides have — the HUD asks <see cref="Ascent"/> and <see cref="AscentCold"/> for the words.</summary>
        void UpdateClimb(HikerController me, NightSession s)
        {
            if (climbLine == null) return;
            if (!World.IsElbrus || me.Climbing == null)
            {
                climbLine.text = "";
                if (cramponButton != null) cramponButton.gameObject.SetActive(false);
                if (thermosButton != null) thermosButton.gameObject.SetActive(false);
                return;
            }
            var gear = me.Climbing;
            var net = me.Climb.Value;
            var p = gear.Where;
            var sb = new StringBuilder();

            // the clock, the turn-round time, and how much of the mountain the body has taken in so far
            float hour = Climb.Hour(s.Elapsed.Value);
            sb.Append(AscentRoute.Clock(hour));
            if (AscentRoute.PastTurnaround(hour)) sb.Append(" · контрольное время прошло — вниз");
            else
            {
                float left = Climb.SecondsUntil(AscentRoute.TurnaroundHour, s.Elapsed.Value);
                sb.Append(left < 900f
                    ? $" · до разворота {Mathf.CeilToInt(left / 60f)} мин"
                    : $" · разворот в {AscentRoute.Clock(AscentRoute.TurnaroundHour)}");
            }
            sb.Append($" · акклиматизация {Mathf.RoundToInt(net.Acclim / 255f * 100f)} %");
            int legs = Mathf.RoundToInt(me.Strength.Value / 255f * 100f);
            if (legs < 60) sb.Append($" · силы {legs} %");

            // the slip at the ЭВПСО desk. A control time nobody holds you to is not a control time, so the line only
            // appears when there is one — and the silence when there is not is itself the message (Rescue.Watched).
            if (RescueDesk.Known && RescueDesk.Mine.Filed)
            {
                float backHour = RescueDesk.Mine.BackHour;
                if (hour >= backHour) sb.Append($" · не вернулись к {AscentRoute.Clock(backHour)}");
                else if (hour >= backHour - 1f) sb.Append($" · в лагерь к {AscentRoute.Clock(backHour)}");
            }
            else if (p.Ele >= AscentRoute.LastHutEle)
                sb.Append(" · в ЭВПСО не записаны");

            // and what is being done about it, once something is
            string help = RescueDesk.Line(hour);
            if (help.Length > 0) sb.Append('\n').Append(help);

            // what is underfoot and what the air is doing, from where it starts to matter
            float wind = net.WindMs;
            if (p.Ele >= Ascent.FirnFromEle - 200f || wind >= AscentCold.DriftFromMs)
            {
                sb.Append($"\n{Ascent.SurfaceTitle(p.Ele)} · ощущается {net.Feels} °C · ветер {wind:0} м/с");
                string gale = AscentCold.WindTitle(wind);
                if (gale.Length > 0) sb.Append(", ").Append(gale.ToLowerInvariant());
            }

            // the body, and only when it has something to say
            var body = new StringBuilder();
            Add(body, AscentCold.FrostbiteWarning(Limb.Hands, net.Hands / 255f));
            Add(body, AscentCold.FrostbiteWarning(Limb.Feet, net.Feet / 255f));
            Add(body, AscentCold.FrostbiteWarning(Limb.Face, net.Face / 255f));
            if (net.Phase != Ams.None) Add(body, Ascent.PhaseTitle(net.Phase));
            if (net.Blind >= 191) Add(body, "Снежная слепота");
            else if (net.Blind >= 128) Add(body, "Режет глаза от снега");
            if (net.Dry >= 153) Add(body, "Обезвоживание");
            if (net.Sleep >= 128) Add(body, "Клонит в сон");
            if (net.Has(ClimbNet.Mark.MustStop)) Add(body, "Стоять и дышать");
            if (body.Length > 0) sb.Append('\n').Append(body);

            // and what there is to do about it here and now
            string act = Act(gear, net, p);
            if (act.Length > 0) sb.Append('\n').Append(act);
            climbLine.text = sb.ToString();

            // the buttons: crampons from where the ice starts to matter, the thermos whenever there is one
            bool wantsCrampons = Climb.WantsCrampons(p) || net.Has(ClimbNet.Mark.Crampons);
            bool haveCrampons = (net.Gear & Height1079.Core.Gear.Crampons) != 0;
            bool showCrampons = haveCrampons && wantsCrampons;
            if (cramponButton.gameObject.activeSelf != showCrampons) cramponButton.gameObject.SetActive(showCrampons);
            if (showCrampons)
            {
                var job = (ClimbJob)net.Job;
                string label = net.Has(ClimbNet.Mark.Crampons) ? "Снять кошки" : "Надеть кошки";
                if (job == ClimbJob.CramponsOn || job == ClimbJob.CramponsOff)
                    label += $" {Mathf.Clamp(Mathf.RoundToInt(net.Progress / 255f * 100f), 0, 99)} %";
                cramponButton.GetComponentInChildren<Text>().text = label;
            }
            bool showThermos = (net.Gear & Height1079.Core.Gear.Thermos) != 0;
            if (thermosButton.gameObject.activeSelf != showThermos) thermosButton.gameObject.SetActive(showThermos);
            if (showThermos)
                thermosButton.GetComponentInChildren<Text>().text = (ClimbJob)net.Job == ClimbJob.Sip
                    ? $"Термос {Mathf.Clamp(Mathf.RoundToInt(net.Progress / 255f * 100f), 0, 99)} %"
                    : $"Термос ({net.Sips})";
        }

        /// <summary>The amber line of the ascent. <see cref="Ascent.Warning"/> is written so that every phase of the
        /// mountain sickness is announced before the next one arrives and the first word comes before there is any
        /// phase at all, so a player always has time to turn round; it takes the line whenever it has one.</summary>
        string ClimbHint(HikerController me)
        {
            var gear = me.Climbing;
            if (gear == null) return "Южный склон. Наверх — канаткой, ратраком или пешком.";
            string warn = Ascent.Warning(gear.Mirror, gear.Where);
            if (warn.Length > 0) return warn;
            if (gear.Where.Ele < Ascent.FirnFromEle) return "Южный склон. Наверх — канаткой, ратраком или пешком.";
            return "Шаг — вдох. Здесь спешка кончается вынужденной остановкой.";
        }

        static void Add(StringBuilder sb, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(line);
        }

        /// <summary>The one thing the player should be doing about the mountain right now, or "".</summary>
        string Act(ClimbGear gear, ClimbNet net, RoutePoint p)
        {
            if (gear.Sliding) return "Срыв! Вас несёт вниз";
            // the thing that kills seven parties in ten on this side: coming down off the saddle past the corridor
            // between the two lava ridges of Pastukhov rocks (AscentRoute.MissedTheGate)
            if (net.Has(ClimbNet.Mark2.MissedGate) && (net.Way == Going.Down || Mathf.Abs(p.OffRouteM) > AscentRoute.GateHalfWidthM * 2f))
                return net.Way == Going.Down
                    ? $"Коридор между грядами в стороне: {Mathf.RoundToInt(Mathf.Abs(p.OffRouteM))} м от линии. Траверс к вешкам, вниз отсюда нельзя"
                    : $"Вы мимо коридора скал Пастухова, {Mathf.RoundToInt(Mathf.Abs(p.OffRouteM))} м вбок";
            if (net.Has(ClimbNet.Mark.Lost))
                return net.Wander != 0
                    ? $"Маршрут потерян, сносит с линии ({Mathf.Abs(net.WanderPerMetre) * 100f:0} см на метр). Лучше встать и копать траншею, чем идти"
                    : "Маршрут потерян: ни вешки, ни троса, ни колеи. Лучше встать и копать траншею, чем идти";
            // the gate is announced from the top station on, a good eight hundred metres of height before it, so
            // that turning round is a choice and not a surprise
            if (net.Has(ClimbNet.Mark.Barred))
                return p.Ele >= ClimbGear.GateFromEle
                    ? "Выше скал Пастухова не пускают · " + Climb.GateLine(net.Gear)
                    : p.Ele >= Ascent.GearGateEle - 850f
                        ? $"Без снаряжения выше скал Пастухова (4650) не пустят · {Climb.GateLine(net.Gear)} · прокат внизу, {Rental.BundleRoubles(net.Gear)} ₽"
                        : "";
            if (Climb.WantsCrampons(p) && !net.Has(ClimbNet.Mark.Crampons)) return "Наденьте кошки (C/F1) — стоя, 12 секунд";
            if (net.Has(ClimbNet.Mark.OnRope)) return "Перила МЧС в руках · пристёгнуты усом, руки мёрзнут";
            if (AscentRoute.HasFixedRope(p.Ele) && (net.Gear & Height1079.Core.Gear.Harness) != 0 && Mathf.Abs(p.OffRouteM) < 40f)
                return "Тросовые перила рядом — держитесь их";
            if (net.Sleep >= 128 && net.Sips > 0) return "Глоток из термоса (T/F2) прогоняет сон";
            return "";
        }

        /// <summary>The E-work button: it shows what E would do here and how far along the job is.</summary>
        void UpdateWorkButton(HikerController me, NightSession s)
        {
            var kind = Backpacks.WorkHere(me);
            bool show = kind != WorkKind.None;
            if (workButton.gameObject.activeSelf != show) workButton.gameObject.SetActive(show);
            if (!show) { Bootstrap.AutoWork = false; workKind = WorkKind.None; return; }
            var tool = me.Carried.Value.Stack.Spec.Tool;
            if (kind != workKind || !Backpacks.Working)
            {
                workKind = kind;
                workStarted = Time.time;
                workNeeded = Woodwork.Seconds(kind, tool, s.Hands);
            }
            string label = Woodwork.Title(kind, tool);
            if (Backpacks.Working && workNeeded > 0f)
            {
                int percent = Mathf.Clamp(Mathf.RoundToInt((Time.time - workStarted) / workNeeded * 100f), 0, 99);
                label += $" {percent}%";
            }
            workButton.GetComponentInChildren<Text>().text = label;
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

        /// <summary>The rucksack window: what is inside, how full it is, and buttons to take things out, stow, drop or pick up
        /// (the same things the Tab/G/R/X keys do, so the window is enough on its own).</summary>
        void BuildPack()
        {
            packPanel = Rect("Pack", hud.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24, 0), new Vector2(340, 724));
            packPanel.pivot = new Vector2(1, .5f);
            PanelImage(packPanel, Panel);
            packTitle = Label("Title", packPanel, new Vector2(20, -16), new Vector2(300, 26), 20, Ink);
            packLoad = Label("Load", packPanel, new Vector2(20, -44), new Vector2(300, 34), 13, new Color(.71f, .79f, .81f));
            packRows = Rect("Rows", packPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -80), new Vector2(308, 400));
            packHandLine = Label("Hand", packPanel, new Vector2(20, -486), new Vector2(300, 20), 13, Amber);
            packWearButton = ButtonUi("Wear", packPanel, new Vector2(20, -510), new Vector2(148, 28), "Надеть", new Color(.2f, .29f, .34f), Ink, () =>
            {
                var s = NightSession.Instance;
                var me = Bootstrap.LocalHiker;
                if (s == null || me == null) return;
                if (Backpacks.MyPackId(me) == Backpacks.OpenPack) { s.RequestPack(PackAction.Drop); Backpacks.Close(); }
                else s.RequestPack(PackAction.Wear, Backpacks.OpenPack);
            });
            packStowButton = ButtonUi("Stow", packPanel, new Vector2(176, -510), new Vector2(148, 28), "Убрать в рюкзак", new Color(.2f, .29f, .34f), Ink, () =>
            {
                var s = NightSession.Instance;
                if (s != null) s.RequestPack(PackAction.Stow, Backpacks.OpenPack);
            });
            packDropButton = ButtonUi("DropHand", packPanel, new Vector2(20, -542), new Vector2(148, 28), "Бросить из рук", new Color(.2f, .29f, .34f), Ink, () =>
            {
                var s = NightSession.Instance;
                if (s != null) s.RequestPack(PackAction.DropHand);
            });
            packPickButton = ButtonUi("PickUp", packPanel, new Vector2(176, -542), new Vector2(148, 28), "Поднять", new Color(.2f, .29f, .34f), Ink, () =>
            {
                var s = NightSession.Instance;
                var me = Bootstrap.LocalHiker;
                if (s == null || me == null) return;
                int l = Backpacks.NearbyLooseId(me, out _);
                if (l != 0) s.RequestPack(PackAction.PickUp, l);
            });
            // the camp lives here as well as on B/7 and P/8: letter keys never reach the game under UI automation,
            // and the rucksack window is where the tent is anyway
            packCampButton = ButtonUi("Camp", packPanel, new Vector2(20, -574), new Vector2(148, 28), "Поставить лагерь", new Color(.26f, .4f, .34f), Ink,
                () => NightSession.Instance?.RequestCamp());
            packSleepButton = ButtonUi("Sleep", packPanel, new Vector2(176, -574), new Vector2(148, 28), "Ночёвка", new Color(.3f, .34f, .45f), Ink,
                () => NightSession.Instance?.RequestSleep());
            // and the other half of playing in pieces: a way out that is not killing the process. The save is already
            // on disk by then, and «Продолжить» will be waiting in the menu
            // the call for help. It has the key 9 as well, but a button is the only thing that is certain to reach the
            // game under UI automation, and this is the one button nobody should have to find twice
            packSosButton = ButtonUi("Sos", packPanel, new Vector2(20, -606), new Vector2(304, 28), "SOS — вызвать спасателей (9)", new Color(.45f, .2f, .18f), Ink,
                () => NightSession.Instance?.SendSos());
            packLeaveButton = ButtonUi("Leave", packPanel, new Vector2(20, -638), new Vector2(304, 28), "Выйти в меню", new Color(.28f, .22f, .22f), Ink,
                Bootstrap.Leave);
            // the folded sheet of the guide's programme: an ordinary item in this very rucksack, so the button only
            // does what clicking its row would do — take it out and unfold it (Programmes.Toggle)
            packSheetButton = ButtonUi("Sheet", packPanel, new Vector2(20, -670), new Vector2(304, 28), "Программа восхождения (U/F3)", new Color(.34f, .3f, .22f), Ink,
                () => Programmes.Toggle(Bootstrap.LocalHiker));
            packPanel.gameObject.SetActive(false);
        }

        void UpdatePack(HikerController me)
        {
            bool open = Backpacks.UiOpen && Backpacks.TryPack(Backpacks.OpenPack, out _);
            if (packPanel.gameObject.activeSelf != open) packPanel.gameObject.SetActive(open);
            if (!open) { packShown = ""; return; }
            Backpacks.TryPack(Backpacks.OpenPack, out var pack);
            bool mine = pack.Wearer == me.OwnerClientId;
            bool ground = pack.Wearer == PackNet.NoWearer;
            string owner = "Рюкзак напарника";
            if (!mine && !ground)
            {
                var h = HikerController.ByClient(pack.Wearer);
                if (h != null) owner = "Рюкзак: " + h.DisplayName;
            }
            packTitle.text = mine ? "Ваш рюкзак" : ground ? "Рюкзак в снегу" : owner;
            var contents = Backpacks.Contents(Backpacks.OpenPack);
            float litres = 0f, kg = Backpack.OwnKg;
            foreach (var i in contents) { litres += i.Litres; kg += i.Kg; }
            packLoad.text = $"{litres:0.#} из {Backpack.CapacityLitres:0} л · {kg:0.0} кг · вместе с руками {Backpacks.CarriedKg(me):0.0} кг";
            var carried = me.Carried.Value.Stack;
            packHandLine.text = !carried.IsEmpty ? "В руках: " + carried.Describe() : "Руки свободны · Tab — закрыть";
            packStowButton.gameObject.SetActive(!carried.IsEmpty);
            packDropButton.gameObject.SetActive(!carried.IsEmpty);
            int nearby = Backpacks.NearbyLooseId(me, out var nearbyItem);
            packPickButton.gameObject.SetActive(nearby != 0);
            if (nearby != 0) packPickButton.GetComponentInChildren<Text>().text = "Поднять: " + nearbyItem.Describe();
            bool canWear = Backpacks.MyPackId(me) == Backpacks.OpenPack || ground;
            packWearButton.gameObject.SetActive(canWear);
            packWearButton.GetComponentInChildren<Text>().text = mine ? "Снять рюкзак" : "Надеть";

            // the camp: put one up where the mountain allows it, take down the one you are standing at, spend the
            // night in it. Elbrus only — on Kholat Syakhl there is one night and it is not saved
            int atCamp = Camps.Here(me);
            bool camping = Camps.On;
            if (packCampButton.gameObject.activeSelf != camping) packCampButton.gameObject.SetActive(camping);
            bool canSleep = camping && atCamp != 0;
            if (packSleepButton.gameObject.activeSelf != canSleep) packSleepButton.gameObject.SetActive(canSleep);
            if (camping) packCampButton.GetComponentInChildren<Text>().text = Camps.ToggleLabel(me);
            if (packLeaveButton.gameObject.activeSelf != camping) packLeaveButton.gameObject.SetActive(camping);
            if (packSosButton.gameObject.activeSelf != camping) packSosButton.gameObject.SetActive(camping);
            bool sheet = Programmes.On;
            if (packSheetButton.gameObject.activeSelf != sheet) packSheetButton.gameObject.SetActive(sheet);
            if (sheet) packSheetButton.GetComponentInChildren<Text>().text = Programmes.Unfolded(me)
                ? "Сложить программу (U/F3)" : "Программа восхождения (U/F3)";
            if (camping)
                packSosButton.GetComponentInChildren<Text>().text = RescueDesk.Known && RescueDesk.Mine.Mission.Coming
                    ? "Помощь вызвана (9 — повторить)"
                    : "SOS — вызвать спасателей (9)";

            // rows are rebuilt only when the contents change
            var key = new StringBuilder().Append(Backpacks.OpenPack).Append(':');
            foreach (var i in contents) key.Append((int)i.Id).Append('/').Append(i.Amount).Append('/').Append(i.Wet).Append(',');
            if (key.ToString() == packShown) return;
            packShown = key.ToString();
            for (int i = packRowPool.Count; i < contents.Count; i++)
            {
                int index = i;
                var b = ButtonUi("Row" + i, packRows, new Vector2(0, -i * 25), new Vector2(308, 23), "", new Color(.12f, .2f, .25f), Ink, () =>
                {
                    var s = NightSession.Instance;
                    if (s != null) s.RequestPack(PackAction.Take, Backpacks.OpenPack, index);
                });
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
                t.fontSize = 14;
                packRowPool.Add(b);
            }
            for (int i = 0; i < packRowPool.Count; i++)
            {
                bool show = i < contents.Count;
                if (packRowPool[i].gameObject.activeSelf != show) packRowPool[i].gameObject.SetActive(show);
                if (!show) continue;
                var stack = contents[i];
                packRowPool[i].GetComponentInChildren<Text>().text = $"{stack.Describe()}   {stack.Kg:0.0#} кг · {stack.Litres:0.#} л";
            }
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
            if (settingsCard != null) settingsCard.gameObject.SetActive(false);
            ResetPlan();
            // coming back from a run there may be a save that was not there when the menu was last up
            if (on) RefreshContinue();
        }

        public void SetStatus(string text) { if (status != null) status.text = text; }

        /// <summary>A line at the bottom of the screen for what is within reach right now (the ropeway, a snow-cat).
        /// Empty text hides the panel.</summary>
        public void SetPrompt(string text)
        {
            if (prompt == null) return;
            prompt.text = text ?? "";
            var box = prompt.transform.parent as RectTransform;
            if (box != null) box.gameObject.SetActive(!string.IsNullOrEmpty(prompt.text));
        }

        void Update()
        {
            // the settings sheet answers F10 in the menu too, so it comes before the early return
            TickSettings();
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            if (menu.activeSelf || s == null || me == null) return;
            float x = me.transform.position.x, z = me.transform.position.z;
            // the debug strip is fifteen substitutions and two height samples, and because the fps in it changes every
            // frame it used to mark the whole canvas dirty every frame with it. Four times a second is as fast as a
            // number can be read anyway.
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), .05f);
            if (debug != null && Camera.main != null && Time.unscaledTime >= debugNext)
            {
                debugNext = Time.unscaledTime + .25f;
                var c = Camera.main.transform.position;
                debug.text = $"{fps:0} fps · cam {c.x:0},{c.y:0},{c.z:0} · ground under cam {TerrainBuilder.Height(Bootstrap.Dem, c.x, c.z):0} · me {x:0},{me.transform.position.y:0},{z:0} · ground {TerrainBuilder.Height(Bootstrap.Dem, x, z):0} · fog {RenderSettings.fogDensity:0.0000} · v {me.Speed:0.0} · keys {Controls.Debug} · {(me.FirstPerson ? "1st" : "3rd")} · grounded {me.Grounded}";
            }
            float gx = World.IsElbrus ? Elbrus.WestSummit.X : WorldData.Tent.X, gz = World.IsElbrus ? Elbrus.WestSummit.Z : WorldData.Tent.Z;
            float dist = WorldData.Distance(x, z, gx, gz);
            bool nearFire = !World.IsElbrus && WorldData.NearCamp(x, z);
            float fire = s.FireRemaining.Value;
            fireButton.gameObject.SetActive(nearFire && fire <= 0f);
            UpdateWorkButton(me, s);

            // The written part of the HUD — the clock, the bearing, the party, the amber line, the three meters and
            // the whole ascent block — is about twenty interpolated strings and two StringBuilders. Writing to a
            // uGUI Text marks the canvas dirty and rebuilds its batch, so doing all of it every frame cost a canvas
            // rebuild every frame for numbers that a person reads ten times a second at most.
            if (Time.unscaledTime >= panelNext)
            {
                panelNext = Time.unscaledTime + .1f;
                clock.text = World.IsElbrus ? $"{me.transform.position.y:0} м" : SurvivalRules.NightTime(s.Elapsed.Value);
                float bearing = Mathf.DeltaAngle(me.Yaw, Mathf.Atan2(gx - x, gz - z) * Mathf.Rad2Deg);
                string goal = World.IsElbrus ? "Западная вершина" : "Верхнее укрытие";
                direction.text = $"{(Mathf.Abs(bearing) < 17f ? "↑" : bearing > 0 ? "→" : "←")} {goal} · {(dist > 1500f ? $"{dist / 1000f:0.0} км" : $"{Mathf.RoundToInt(dist)} м")}";
                if (partyText == null) partyText = new StringBuilder(); else partyText.Clear();
                foreach (var p in s.Party) if (!p.you) partyText.Append(partyText.Length > 0 ? " · " : "").Append($"{p.name}: {(p.outcome != Outcome.None ? SurvivalRules.Describe(p.outcome).Title.ToLowerInvariant() : p.online ? "на склоне" : "без связи")}");
                party.text = partyText.ToString();
                fireButton.GetComponentInChildren<Text>().text = Bootstrap.AutoKindle ? "Прекратить розжиг" : "Разжечь костёр";
                string kindle = s.KindleNeeded > 0 ? $" {Mathf.Min(99, Mathf.RoundToInt(s.KindleProgress / s.KindleNeeded * 100f))}%" : "";
                UpdateTravel(me);
                UpdateClimb(me, s);
                hint.text = !NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost ? "Связь потеряна · ночь идёт на сервере"
                    : me.Paused ? "Пауза · ночь продолжается, нажмите на сцену"
                    : me.Skis != null && me.Skis.Busy ? me.Skis.BusyNote()
                    : me.Crawling ? "Палатка · внутри только ползком"
                    : nearFire ? (fire > 0 ? $"У огня · ещё {Mathf.CeilToInt(fire)} с" : s.KindleNeeded > 0 ? $"Разжигаете… держите E{kindle}" : "Кострище · стойте и удерживайте E")
                    : World.IsElbrus ? ClimbHint(me)
                    : s.Heat < 30 ? "Холод мешает думать. Вернитесь к огню." : s.Storm.Value ? "Метель. Держитесь рядом." : "Свет уходит. Выбирайте путь.";
                heat.value = s.Heat; hands.value = s.Hands; clarity.value = s.Clarity;
            }
            UpdateNavigation(me, x, z);
            UpdatePack(me);
            UpdatePlan(me, x, z);

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
                    // the last two hundred lines, not all of them: one uGUI Text is capped at 65 535 vertices, and a
                    // long night walks straight into that and then logs an error every frame instead of drawing
                    var eb = new StringBuilder();
                    int from = Mathf.Max(0, s.Events.Count - 200);
                    if (from > 0) eb.Append("… раньше — ещё ").Append(from).Append(" записей\n");
                    for (int i = from; i < s.Events.Count; i++) eb.Append(i + 1).Append(". ").Append(SurvivalRules.NightTime(s.Events[i].Time)).Append(" — ").Append(s.Events[i].Text).Append('\n');
                    events.text = eb.ToString(); shownEvents = s.Events.Count;
                }
            }
        }
    }
}
