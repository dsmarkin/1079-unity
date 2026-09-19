using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>What the player sees over the range: the game's own two corners, as in PEAK.
    ///
    /// Bottom left, one bar of strength. The pale part is what the body has; hunger, cold and sleep are pieces bitten
    /// off its right end, each in its own colour with its name over it; a bar of chocolate sticks a green piece on
    /// past the end, and that piece goes first. Bottom right, three hands' worth of slots and the rucksack. All of
    /// it is drawn from code on a canvas, no assets.
    ///
    /// The panel of every number the body is made of is still here, but off the screen: F1 brings it up. It is a
    /// tool for finding the feel of the body, not a thing the player looks at.</summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        /// <summary>The tuning panel. Hidden unless asked for.</summary>
        public static bool Panel;
        static string message = "";
        static float messageUntil;
        public static void Say(string s) { message = s; messageUntil = Time.unscaledTime + 4f; }

        // ── the bar ─────────────────────────────────────────────────────────────────────────────────────────────────
        const float BarW = 420f, BarH = 28f, Margin = 36f;
        /// <summary>The white edge round every piece of the screen, in UI pixels. Three: two read as a hairline.</summary>
        const float Rim = 3f;
        static readonly Color RimColour = new Color(1f, 1f, 1f, .92f);
        static readonly Color Cream = new Color(.98f, .94f, .80f);
        static readonly Color Bonus = new Color(.56f, .86f, .40f);
        static Color BiteColour(Bite b)
        {
            switch (b)
            {
                case Bite.Hunger: return new Color(.96f, .62f, .20f);
                case Bite.Cold: return new Color(.55f, .80f, 1f);
                default: return new Color(.74f, .62f, .94f);
            }
        }
        static string BiteName(Bite b)
        {
            switch (b)
            {
                case Bite.Hunger: return "ГОЛОД";
                case Bite.Cold: return "ХОЛОД";
                default: return "СОН";
            }
        }

        Canvas canvas;
        Font font;
        RectTransform fill;
        RectTransform[] bites;
        Text[] biteLabels;
        RectTransform extraFrame, extraFill;
        Text toast;

        // ── the slots ───────────────────────────────────────────────────────────────────────────────────────────────
        const float Slot = 76f, SlotGap = 10f, PackGap = 26f;

        void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var boot = SandboxBoot.Instance;
            // drawn by the sandbox camera, not as an overlay: the overlay never reaches a camera render, and the
            // shot script photographs the range through the camera, so this is the only way the bar is in the picture
            canvas = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = boot.Cam;
            // just past the near plane: the canvas is geometry in the world, and anything nearer than it — the
            // body's own hood, from inside the head — would be drawn over it
            canvas.planeDistance = boot.Cam.nearClipPlane + .02f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            BuildBar();
            BuildSlots();
            toast = Label("toast", canvas.transform, new Vector2(.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 24f), 14, new Color(1f, 1f, 1f, .85f), TextAnchor.MiddleCenter);
        }

        void BuildBar()
        {
            var root = Rect("bar", canvas.transform, new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(BarW, BarH));
            Rounded(root, RimColour, BarH / 2f);                                             // rim
            var back = Rect("back", root, new Vector2(0f, 0f), new Vector2(Rim, Rim), new Vector2(BarW - 2f * Rim, BarH - 2f * Rim));
            var backImage = Rounded(back, new Color(0f, 0f, 0f, .62f), (BarH - 2f * Rim) / 2f);
            // the mask is what keeps the flat pieces inside the pill
            var mask = back.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            backImage.raycastTarget = false;
            fill = Rect("fill", back, new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, BarH - 2f * Rim));
            Flat(fill, Cream);
            bites = new RectTransform[Condition.Kinds.Length];
            biteLabels = new Text[Condition.Kinds.Length];
            for (int i = 0; i < Condition.Kinds.Length; i++)
            {
                var b = Condition.Kinds[i];
                bites[i] = Rect("bite-" + b, back, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BarH - 2f * Rim));
                bites[i].pivot = new Vector2(1f, 0f);
                Flat(bites[i], BiteColour(b));
                // the name sits over the bar, outside the mask, in the piece's own colour
                biteLabels[i] = Label("name-" + b, root, new Vector2(1f, 1f), Vector2.zero, new Vector2(120f, 18f), 13, BiteColour(b), TextAnchor.LowerCenter, FontStyle.Bold);
                biteLabels[i].rectTransform.pivot = new Vector2(.5f, 0f);
                biteLabels[i].text = BiteName(b);
                biteLabels[i].enabled = false;
            }
            // the chocolate: its own small pill stuck on the right end, grows and shrinks with what is left of it
            extraFrame = Rect("extra", canvas.transform, Vector2.zero, new Vector2(Margin + BarW + 6f, Margin), new Vector2(0f, BarH));
            Rounded(extraFrame, RimColour, BarH / 2f);
            var extraBack = Rect("back", extraFrame, Vector2.zero, new Vector2(Rim, Rim), new Vector2(0f, BarH - 2f * Rim));
            extraBack.anchorMax = Vector2.one; extraBack.offsetMin = new Vector2(Rim, Rim); extraBack.offsetMax = new Vector2(-Rim, -Rim);
            Rounded(extraBack, new Color(0f, 0f, 0f, .62f), (BarH - 2f * Rim) / 2f);
            float inset = Rim + 1f;
            extraFill = Rect("fill", extraFrame, Vector2.zero, new Vector2(inset, inset), new Vector2(0f, BarH - 2f * inset));
            extraFill.anchorMax = Vector2.one; extraFill.offsetMin = new Vector2(inset, inset); extraFill.offsetMax = new Vector2(-inset, -inset);
            Rounded(extraFill, Bonus, (BarH - 2f * inset) / 2f);
            extraFrame.gameObject.SetActive(false);
        }

        void BuildSlots()
        {
            // three slots, right to left, then the rucksack further left
            for (int i = 0; i < 3; i++)
            {
                float x = -Margin - Slot - (2 - i) * (Slot + SlotGap);
                var slot = SlotFrame("slot-" + (i + 1), new Vector2(x, Margin));
                var n = Label("n", slot, new Vector2(0f, 1f), new Vector2(8f, -5f), new Vector2(20f, 16f), 12, new Color(1f, 1f, 1f, .6f), TextAnchor.UpperLeft, FontStyle.Bold);
                n.rectTransform.pivot = new Vector2(0f, 1f);
                n.text = (i + 1).ToString();
            }
            var pack = SlotFrame("pack", new Vector2(-Margin - Slot - 3f * (Slot + SlotGap) - PackGap + SlotGap, Margin));
            // the rucksack, drawn: a body with a flap over it and a strap either side
            var body = Rect("body", pack, new Vector2(.5f, .5f), new Vector2(0f, -4f), new Vector2(30f, 36f));
            body.pivot = new Vector2(.5f, .5f);
            Rounded(body, new Color(1f, 1f, 1f, .45f), 7f);
            var flap = Rect("flap", pack, new Vector2(.5f, .5f), new Vector2(0f, 14f), new Vector2(24f, 12f));
            flap.pivot = new Vector2(.5f, .5f);
            Rounded(flap, new Color(1f, 1f, 1f, .6f), 5f);
            var strapL = Rect("strap-l", pack, new Vector2(.5f, .5f), new Vector2(-19f, -2f), new Vector2(5f, 26f));
            strapL.pivot = new Vector2(.5f, .5f); Rounded(strapL, new Color(1f, 1f, 1f, .35f), 2.5f);
            var strapR = Rect("strap-r", pack, new Vector2(.5f, .5f), new Vector2(19f, -2f), new Vector2(5f, 26f));
            strapR.pivot = new Vector2(.5f, .5f); Rounded(strapR, new Color(1f, 1f, 1f, .35f), 2.5f);
        }

        RectTransform SlotFrame(string name, Vector2 pos)
        {
            var slot = Rect(name, canvas.transform, new Vector2(1f, 0f), pos, new Vector2(Slot, Slot));
            slot.pivot = new Vector2(0f, 0f);
            Rounded(slot, RimColour, 12f);
            var back = Rect("back", slot, Vector2.zero, new Vector2(Rim, Rim), new Vector2(Slot - 2f * Rim, Slot - 2f * Rim));
            Rounded(back, new Color(0f, 0f, 0f, .55f), 12f - Rim);
            return slot;
        }

        void LateUpdate()
        {
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null || fill == null) return;
            var b = boot.Body;
            var t = boot.Tuning;
            var c = boot.Vitals != null ? boot.Vitals.Condition : null;
            float inner = BarW - 2f * Rim;
            float per = t.Stamina > 0f ? inner / t.Stamina : 0f;
            fill.sizeDelta = new Vector2(Mathf.Clamp(b.Stamina * per, 0f, inner), fill.sizeDelta.y);

            // the bites, right to left. Their share of the bar is a share of the whole bar, not of what is left.
            float edge = 0f;
            for (int i = 0; i < bites.Length; i++)
            {
                float w = c != null ? c.Of(Condition.Kinds[i]) / Condition.Max * inner : 0f;
                bites[i].anchoredPosition = new Vector2(-edge, 0f);
                bites[i].sizeDelta = new Vector2(w, bites[i].sizeDelta.y);
                bool shown = w >= 1f;
                biteLabels[i].enabled = shown;
                if (shown) biteLabels[i].rectTransform.anchoredPosition = new Vector2(-edge - w / 2f, 2f);
                edge += w;
            }

            // the chocolate
            float extra = Mathf.Clamp(b.Extra * per, 0f, inner);
            bool any = extra >= 1f;
            if (extraFrame.gameObject.activeSelf != any) extraFrame.gameObject.SetActive(any);
            if (any) extraFrame.sizeDelta = new Vector2(Mathf.Max(extra + 2f * (Rim + 1f), BarH), BarH);

            toast.text = Time.unscaledTime < messageUntil ? message : "";
        }

        // ── building blocks ─────────────────────────────────────────────────────────────────────────────────────────
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Image Flat(RectTransform rt, Color c)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.color = c; i.raycastTarget = false;
            return i;
        }

        /// <summary>A rectangle with corners rounded by <paramref name="radius"/> UI pixels. One sprite serves every
        /// size: it is nine-sliced, and the slice scale is what sets the radius.</summary>
        static Image Rounded(RectTransform rt, Color c, float radius)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = Pill(); i.type = Image.Type.Sliced;
            i.pixelsPerUnitMultiplier = PillRadius / Mathf.Max(radius, 1f);
            i.color = c; i.raycastTarget = false;
            return i;
        }

        Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            // a dark edge under every letter: the ground is snow, and pale words on white are not words
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .7f); shadow.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        const int PillRadius = 32;
        static Sprite pill;
        static Sprite Pill()
        {
            if (pill != null) return pill;
            const int S = PillRadius * 2;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float r = new Vector2(x + .5f - PillRadius, y + .5f - PillRadius).magnitude;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(PillRadius - r)));
                }
            tex.Apply();
            pill = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(PillRadius, PillRadius, PillRadius, PillRadius));
            return pill;
        }

        // ── the tuning panel (F1) ───────────────────────────────────────────────────────────────────────────────────
        Vector2 scroll;
        GUIStyle head, small;

        void Styles()
        {
            if (head != null) return;
            head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        void OnGUI()
        {
            if (!Panel) return;
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null) return;
            Styles();
            var b = boot.Body;
            var t = boot.Tuning;

            GUILayout.BeginArea(new Rect(Screen.width - 340, 10, 330, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("физика тела — правится вживую (F1 прячет)", head);
            GUILayout.Label($"стенд: {boot.StandName} · силы {b.Stamina:0}+{b.Extra:0} из {b.StaminaCap:0} · скорость {b.Torso.linearVelocity.magnitude:0.0} м/с · уклон {b.SlopeAngle:0}°", small);
            GUILayout.Label($"файл: {PuppetTuning.PathFor(t.Name)}", small);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("масса и сложение", head);
            t.TorsoMass = Row("масса тела, кг", t.TorsoMass, 30f, 140f);
            t.HandMass = Row("масса кисти, кг", t.HandMass, .5f, 12f);
            // the agreed stature. It drives nothing by itself — the body is made of the three numbers under it —
            // but it is what the self-test measures the drawn figure against, so it belongs where they are set
            t.StandHeight = Row("рост от подошв, м", t.StandHeight, 1.2f, 2.1f);
            t.TorsoHeight = Row("высота капсулы, м", t.TorsoHeight, .6f, 1.8f);
            t.TorsoRadius = Row("радиус капсулы, м", t.TorsoRadius, .15f, .5f);
            t.ShoulderUp = Row("плечо выше центра, м", t.ShoulderUp, .1f, .8f);
            t.ShoulderOut = Row("плечо в сторону, м", t.ShoulderOut, .05f, .5f);

            GUILayout.Label("ноги: опора и склоны", head);
            t.HoverHeight = Row("высота парения, м", t.HoverHeight, .5f, 1.6f);
            t.LegProbe = Row("щуп ниже ног, м", t.LegProbe, .1f, 1.2f);
            t.LegSpring = Row("жёсткость ног", t.LegSpring, 20f, 400f);
            t.LegDamper = Row("гашение ног", t.LegDamper, 1f, 60f);
            t.LegMaxAccel = Row("предел ног, м/с²", t.LegMaxAccel, 10f, 200f);
            t.LegRise = Row("ноги встают за, с", t.LegRise, 0f, 1.5f);
            t.LegLift = Row("подъём ног не быстрее, м/с", t.LegLift, .5f, 8f);
            t.FootGrip = Row("держат до, °", t.FootGrip, 20f, 80f);
            t.UphillSpeed = Row("в гору от шага, доля", t.UphillSpeed, .1f, 1f);
            t.DownhillSpeed = Row("под гору от шага, доля", t.DownhillSpeed, 1f, 1.8f);
            t.SlideFriction = Row("скольжение: трение", t.SlideFriction, 0f, .95f);
            t.SlideTop = Row("скольжение: предел, м/с", t.SlideTop, 2f, 25f);
            t.SlideControl = Row("скольжение: управление", t.SlideControl, 0f, 8f);
            t.SlideLift = Row("скольжение: доля ног", t.SlideLift, 0f, 1f);
            t.SquashFrom = Row("приседать от удара, м/с", t.SquashFrom, 0f, 10f);
            t.SquashGive = Row("приседание на 1 м/с, м", t.SquashGive, 0f, .15f);
            t.SquashMax = Row("приседание не глубже, м", t.SquashMax, 0f, .45f);
            t.SquashTime = Row("разгибается за, с", t.SquashTime, .05f, 2f);

            GUILayout.Label("стойка", head);
            t.UprightSpring = Row("держать вертикаль", t.UprightSpring, 10f, 400f);
            t.UprightDamper = Row("гашение вертикали", t.UprightDamper, 1f, 60f);
            t.TurnSpring = Row("поворот к взгляду", t.TurnSpring, 5f, 200f);
            t.TurnDamper = Row("гашение поворота", t.TurnDamper, 1f, 40f);
            t.LeanInto = Row("наклон в разгон, доля", t.LeanInto, 0f, 1.5f);
            t.LeanMax = Row("наклон не больше, °", t.LeanMax, 0f, 40f);
            t.LeanLag = Row("наклон запаздывает, с", t.LeanLag, 0f, .8f);

            GUILayout.Label("падение и вставание", head);
            t.TripFrom = Row("спотыкание от, м/с", t.TripFrom, 1f, 12f);
            t.TripTime = Row("спотыкание длится, с", t.TripTime, 0f, 2f);
            t.TripHold = Row("в спотыкании управление", t.TripHold, 0f, 1f);
            t.LimpFrom = Row("падение от, м/с", t.LimpFrom, 2f, 20f);
            t.LimpTime = Row("лежит, с", t.LimpTime, .1f, 4f);
            t.FallSpin = Row("заваливает, °/с", t.FallSpin, 0f, 500f);
            t.LimpFriction = Row("трение лежащего", t.LimpFriction, 0f, 1f);
            t.GetUp = Row("встаёт за, с", t.GetUp, .05f, 4f);

            GUILayout.Label("шаг", head);
            t.WalkSpeed = Row("шаг, м/с", t.WalkSpeed, .5f, 6f);
            t.RunSpeed = Row("бег, м/с", t.RunSpeed, 1f, 10f);
            t.StartAccel = Row("трогается с места", t.StartAccel, 1f, 60f);
            t.GroundAccel = Row("разгон на ходу", t.GroundAccel, 4f, 80f);
            t.BrakeAccel = Row("торможение", t.BrakeAccel, 1f, 60f);
            t.AirAccel = Row("управление в воздухе", t.AirAccel, 0f, 14f);
            t.StanceWidth = Row("ноги врозь, м", t.StanceWidth, .10f, .60f);
            t.ToeOut = Row("носки наружу, °", t.ToeOut, 0f, 35f);
            t.StepScatter = Row("разброс шага, м", t.StepScatter, 0f, .12f);

            GUILayout.Label("прыжок", head);
            t.JumpHeight = Row("высота прыжка, м", t.JumpHeight, 0f, 1.5f);
            t.JumpBuffer = Row("помнить нажатие, с", t.JumpBuffer, 0f, .5f);
            t.JumpClear = Row("ноги отпущены, с", t.JumpClear, .02f, .6f);
            t.JumpCost = Row("цена прыжка, сил", t.JumpCost, 0f, 30f);

            GUILayout.Label(b.HandsEnabled ? "руки (черновик, F8)" : "руки выключены — F8, чтобы включить", head);
            if (b.HandsEnabled)
            {
            t.ArmReach = Row("длина руки, м", t.ArmReach, .4f, 1.3f);
            t.ArmSpring = Row("рука тянется", t.ArmSpring, 100f, 3000f);
            t.ArmDamper = Row("гашение руки", t.ArmDamper, 5f, 200f);
            t.GripSpring = Row("тело к хвату", t.GripSpring, 200f, 8000f);
            t.GripDamper = Row("гашение подтяга", t.GripDamper, 5f, 400f);
            t.GripBreakForce = Row("хват рвётся при, Н", t.GripBreakForce, 500f, 20000f);
            t.GrabRadius = Row("ладонь, м", t.GrabRadius, .08f, .5f);
            t.PullIn = Row("подтягивание, м", t.PullIn, .05f, .7f);
            }

            GUILayout.Label("силы (одна полоска)", head);
            t.Stamina = Row("всего", t.Stamina, 20f, 300f);
            t.HangCost = Row("вис, /с", t.HangCost, 0f, 40f);
            t.GripCost = Row("хват с опорой, /с", t.GripCost, 0f, 20f);
            t.PullCost = Row("подтягивание, /с", t.PullCost, 0f, 60f);
            t.RunCost = Row("бег, /с", t.RunCost, 0f, 30f);
            t.Recovery = Row("восстановление, /с", t.Recovery, 0f, 60f);
            t.RestDelay = Row("пауза до отдыха, с", t.RestDelay, 0f, 4f);
            t.ExhaustLock = Row("отказ рук, с", t.ExhaustLock, 0f, 6f);
            t.ExtraCap = Row("бонус от еды не больше", t.ExtraCap, 0f, 150f);

            GUILayout.Label("решатель", head);
            int rate = Mathf.RoundToInt(Row("шаг физики, Гц", t.PhysicsRate, 30f, 200f));
            int iter = Mathf.RoundToInt(Row("итераций", t.SolverIterations, 4f, 40f));
            if (rate != t.PhysicsRate || iter != t.SolverIterations)
            {
                t.PhysicsRate = rate; t.SolverIterations = iter;
                SandboxBoot.Instance.ApplySolver();
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("выйти в игру (F9)")) SandboxBoot.Instance.LeaveToGame();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("сохранить (F5)")) { t.Save("sandbox"); Say("сохранено"); }
            if (GUILayout.Button("сброс к заводским")) { SandboxBoot.Instance.Tuning = new PuppetTuning { Name = "sandbox" }; SandboxBoot.Instance.ApplySolver(); SandboxBoot.Instance.Spawn(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            for (int i = 0; i < SandboxRange.Stands.Count; i++)
                if (GUILayout.Button(SandboxRange.Stands[i].Name)) SandboxBoot.Instance.GoTo(i);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        float Row(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, small, GUILayout.Width(170));
            GUILayout.Label(value.ToString(max > 100f ? "0" : "0.00"), small, GUILayout.Width(46));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
