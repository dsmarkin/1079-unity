using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The guide's programme, as the player meets it. Five places and no sixth:
    ///
    /// <list type="number">
    /// <item><b>The card</b> in the bottom corner — the day, the line to do now, and where it is from here. Two lines
    /// and a bearing; the «зачем» and the «как засчитывается» are one key away and never on screen by themselves.</item>
    /// <item><b>The compass</b> (<see cref="Equipment"/>) takes the bearing as a course arrow under the glass, and the
    /// line under the crosshair reads it out as an azimuth, the way a bearing taken off a map is read out.</item>
    /// <item><b>The maps</b>: the mini-map gets the current goal, the unfolded sheet gets the whole programme — what
    /// is done, what is being walked to, what is still ahead, and what the party walked past
    /// (<see cref="Programme.LeftBehind"/>).</item>
    /// <item><b>The sheet in the rucksack</b> (<see cref="ItemId.ProgrammeSheet"/>): nine days, twenty-two lines and
    /// the reason for the one in hand. It is an item, so reading it means taking it out.</item>
    /// <item><b>The notice</b> when a step closes, plus one line in the protocol from the host.</item>
    /// </list>
    ///
    /// And the first half-minute: a man who has just appeared on the Azau meadow is told where he is and what the
    /// first line of the programme is, with its bearing, and then left alone.
    ///
    /// Everything here reads <see cref="Programmes"/>, which reads what the host published. Nothing on this side
    /// evaluates a condition and nothing writes a tick. Off the southern slope it is all switched off in one place
    /// (<see cref="ApplyPlacePlan"/>).</summary>
    public sealed partial class HudController
    {
        // the card
        RectTransform planCard;
        Text planHead, planLine, planAim, planFoot;
        // the notice when a step closes
        RectTransform planNoteBox;
        Text planNote;
        // the first half-minute on the meadow
        RectTransform planIntro;
        Text planIntroText;
        bool introDone;
        float introUntil;
        Vector2 introFrom;
        /// <summary>How long the opening card stands before it goes by itself, seconds.</summary>
        const float IntroSeconds = 28f;
        /// <summary>…or until the party has walked this far from where it appeared.</summary>
        const float IntroWalkM = 220f;
        // the unfolded sheet
        RectTransform planSheet;
        Text sheetTitle, sheetPage, sheetPlan, sheetFoot;
        Button packSheetButton;
        // the mark on the mini-map
        RectTransform miniGoal;
        Image miniGoalImage;
        Text miniGoalLabel;
        // and every step of the programme on the unfolded map
        RectTransform bigSheet;
        readonly List<RectTransform> planMarkPool = new List<RectTransform>();
        readonly List<Text> planMarkLabels = new List<Text>();
        readonly List<Spot> planSpots = new List<Spot>();

        /// <summary>One pencil mark on the map: several steps can share a place (four of the twenty-two happen on the
        /// Azau meadow), so they are grouped by where they are and the most urgent of them wins the colour.</summary>
        struct Spot
        {
            public float X, Z;
            public Programmes.Mark Mark;
            public string Label;
        }

        /// <summary>Two marks closer together than this are one mark.</summary>
        const float SpotMergeM = 120f;

        static readonly Color PaperInk = new Color(.13f, .19f, .24f);

        // ── the hollow pencil ring the marks are drawn with ───────────────────────────────────────────────

        static Sprite ring;
        static Sprite Ring()
        {
            if (ring != null) return ring;
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float r = new Vector2(x + .5f - S / 2f, y + .5f - S / 2f).magnitude / (S / 2f);
                    // a ring of about a fifth of the radius, soft at both edges like a pencil line
                    float a = Mathf.Clamp01((1f - Mathf.Abs(r - .78f) / .22f));
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            ring = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
            return ring;
        }

        // ── building ──────────────────────────────────────────────────────────────────────────────────────

        void BuildPlan()
        {
            // the card, standing on top of the ascent block in the same corner
            planCard = Rect("Plan", hud.transform, Vector2.zero, Vector2.zero, new Vector2(20, 420), new Vector2(360, 134));
            PanelImage(planCard, Panel);
            planHead = Label("Head", planCard, new Vector2(18, -12), new Vector2(326, 18), 13, new Color(.69f, .76f, .8f));
            planLine = Label("Line", planCard, new Vector2(18, -31), new Vector2(326, 56), 15, Ink);
            planAim = Label("Aim", planCard, new Vector2(18, -88), new Vector2(326, 18), 13, Amber);
            planFoot = Label("Foot", planCard, new Vector2(18, -108), new Vector2(326, 18), 11, new Color(.64f, .71f, .75f));
            planCard.gameObject.SetActive(false);

            // the notice: one line under the debug strip, held for a few seconds and then faded out
            planNoteBox = Rect("PlanNote", hud.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -44), new Vector2(620, 34));
            planNoteBox.pivot = new Vector2(.5f, 1);
            PanelImage(planNoteBox, new Color(.05f, .12f, .1f, .88f));
            planNote = Label("Text", planNoteBox, Vector2.zero, new Vector2(620, 34), 17, new Color(.79f, .92f, .78f), TextAnchor.MiddleCenter);
            planNote.rectTransform.anchorMin = Vector2.zero; planNote.rectTransform.anchorMax = Vector2.one;
            planNote.rectTransform.sizeDelta = Vector2.zero; planNote.rectTransform.anchoredPosition = Vector2.zero;
            planNoteBox.gameObject.SetActive(false);

            // the first half-minute
            planIntro = Rect("PlanIntro", hud.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -90), new Vector2(820, 132));
            planIntro.pivot = new Vector2(.5f, 1);
            PanelImage(planIntro, new Color(.04f, .11f, .16f, .9f));
            planIntroText = Label("Text", planIntro, new Vector2(24, -14), new Vector2(772, 104), 14, Ink);
            planIntro.gameObject.SetActive(false);

            BuildPlanSheet();

            // the mark on the mini-map lives inside its window, so the mask clips it at the rim
            miniGoal = Rect("Goal", miniRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(22, 22));
            miniGoal.pivot = new Vector2(.5f, .5f);
            miniGoalImage = miniGoal.gameObject.AddComponent<Image>();
            miniGoalImage.sprite = Ring(); miniGoalImage.color = Programmes.Tint(Programmes.Mark.Current); miniGoalImage.raycastTarget = false;
            // the distance is written beside the mark and not on it: the mark turns into an arrow at the rim, and a
            // number that turned with it would be unreadable
            miniGoalLabel = Label("Far", miniRoot, Vector2.zero, new Vector2(140, 18), 13, new Color(.8f, .22f, .16f), TextAnchor.UpperCenter, FontStyle.Normal, new Vector2(.5f, .5f));
            miniGoalLabel.rectTransform.pivot = new Vector2(.5f, 1f); miniGoalLabel.font = Hand; miniGoalLabel.raycastTarget = false;
            miniGoal.gameObject.SetActive(false);
            miniGoalLabel.gameObject.SetActive(false);
        }

        /// <summary>The sheet itself: a folded piece of paper, opened in both hands. The left half is the line being
        /// walked now with the reason for it, the right half is all nine days with a mark against every one.</summary>
        void BuildPlanSheet()
        {
            planSheet = Rect("PlanSheet", hud.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PanelImage(planSheet, new Color(.02f, .05f, .07f, .74f)).raycastTarget = false;
            var paper = Rect("Paper", planSheet, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1140, 744));
            paper.pivot = new Vector2(.5f, .5f); paper.localRotation = Quaternion.Euler(0, 0, -.5f);
            PanelImage(paper, new Color(.9f, .89f, .83f)).raycastTarget = false;
            // the fold: a sheet issued at a briefing has been in a pocket
            var fold = Rect("Fold", paper, new Vector2(.5f, 0), new Vector2(.5f, 1), Vector2.zero, new Vector2(2, 0));
            fold.pivot = new Vector2(.5f, .5f); fold.offsetMin = new Vector2(-1, 26); fold.offsetMax = new Vector2(1, -26);
            PanelImage(fold, new Color(.76f, .74f, .68f)).raycastTarget = false;

            sheetTitle = Label("Title", paper, new Vector2(44, -26), new Vector2(1052, 30), 23, PaperInk, TextAnchor.UpperLeft, FontStyle.Normal, new Vector2(0, 1));
            sheetTitle.font = Hand;
            sheetPage = Label("Page", paper, new Vector2(44, -68), new Vector2(490, 630), 13, PaperInk);
            sheetPlan = Label("Plan", paper, new Vector2(578, -68), new Vector2(518, 630), 11, PaperInk);
            sheetFoot = Label("Foot", paper, new Vector2(44, -706), new Vector2(1052, 20), 12, new Color(.36f, .4f, .43f));
            planSheet.gameObject.SetActive(false);
        }

        /// <summary>Back in the menu: the next ascent gets its opening card again, and nothing is left saying that a
        /// step of the last one has just closed.</summary>
        void ResetPlan()
        {
            introDone = false; introUntil = 0f;
            Programmes.Done = ""; Programmes.DoneUntil = 0f;
            if (planIntro != null) planIntro.gameObject.SetActive(false);
            if (planNoteBox != null) planNoteBox.gameObject.SetActive(false);
            if (planSheet != null) planSheet.gameObject.SetActive(false);
        }

        /// <summary>Everything the programme draws is Elbrus only: on Kholat Syakhl there is one night, it has its
        /// own script, and none of this exists (<see cref="Programme.AllowedIn"/>).</summary>
        void ApplyPlacePlan()
        {
            if (planCard == null) return;
            bool on = Programmes.On;
            if (!on)
            {
                planCard.gameObject.SetActive(false);
                planNoteBox.gameObject.SetActive(false);
                planIntro.gameObject.SetActive(false);
                planSheet.gameObject.SetActive(false);
                if (miniGoal != null) miniGoal.gameObject.SetActive(false);
                if (miniGoalLabel != null) miniGoalLabel.gameObject.SetActive(false);
                foreach (var m in planMarkPool) if (m != null) m.gameObject.SetActive(false);
                introDone = false;
            }
        }

        // ── the tick ──────────────────────────────────────────────────────────────────────────────────────

        void UpdatePlan(HikerController me, float x, float z)
        {
            if (planCard == null) return;
            if (!Programmes.On) { ApplyPlacePlan(); return; }

            var p = Programmes.Mine;
            var step = Programme.Current(p);
            var aim = Programmes.Aim(me);

            UpdatePlanSheet(me, p, aim);
            UpdatePlanCard(p, step, aim);
            UpdatePlanNote();
            UpdatePlanIntro(x, z, p, step, aim);
            UpdatePlanMarks(x, z, p, aim);
        }

        /// <summary>The card: the day, the line, the bearing, and — only when the two numbers have come apart — how
        /// far behind the programme the trip is (<see cref="Programme.PaceText"/>).</summary>
        void UpdatePlanCard(in Progress p, in ProgrammeStep step, in Bearing aim)
        {
            // it stands down while the sheet or the map is open: they say the same thing at greater length
            bool show = !planSheet.gameObject.activeSelf && !bigRoot.gameObject.activeSelf;
            if (planCard.gameObject.activeSelf != show) planCard.gameObject.SetActive(show);
            if (!show) return;
            planHead.text = Programmes.Head(p);
            planLine.text = step.IsEmpty ? "Дальше — по своему усмотрению." : step.Line;
            planAim.text = Programmes.AimLine(aim);
            string pace = Programme.PaceText(p, MountainDay.Date);
            planFoot.text = pace.Length > 0 ? pace : "U/F3 — программа и зачем";
        }

        /// <summary>A step has closed: one line, a few seconds, and it fades.</summary>
        void UpdatePlanNote()
        {
            float fade = Programmes.DoneFade;
            bool show = fade > 0f;
            if (planNoteBox.gameObject.activeSelf != show) planNoteBox.gameObject.SetActive(show);
            if (!show) return;
            planNote.text = Programmes.Done;
            var box = planNoteBox.GetComponent<Image>();
            box.color = new Color(.05f, .12f, .1f, .88f * fade);
            planNote.color = new Color(.79f, .92f, .78f, fade);
        }

        /// <summary>The first half-minute on the meadow: where you are, what this is, and the first line of the
        /// programme with its bearing. Two sentences, then it gets out of the way — by the clock, by the distance
        /// walked, or the moment the player reaches for the sheet, the map or the rucksack.</summary>
        void UpdatePlanIntro(float x, float z, in Progress p, in ProgrammeStep step, in Bearing aim)
        {
            if (introDone) { if (planIntro.gameObject.activeSelf) planIntro.gameObject.SetActive(false); return; }
            if (introUntil <= 0f)
            {
                // a party that is already somewhere into the programme has met all this before, and a party that
                // continued a save is standing at its tent and not on the meadow
                if (p.Count > 0 || p.Night.Count > 0) { introDone = true; return; }
                if (Elbrus.Distance(x, z, Elbrus.Azau.X, Elbrus.Azau.Z) > 400f) return;
                introUntil = Time.time + IntroSeconds;
                introFrom = new Vector2(x, z);
            }
            bool over = Time.time > introUntil
                || Vector2.Distance(new Vector2(x, z), introFrom) > IntroWalkM
                || Controls.Sheet || Controls.Pause || Controls.PackOpen || Controls.ItemMap;
            if (over)
            {
                introDone = true;
                planIntro.gameObject.SetActive(false);
                return;
            }
            if (!planIntro.gameObject.activeSelf) planIntro.gameObject.SetActive(true);
            var sb = new System.Text.StringBuilder();
            // Two lines, not a lecture: where you stand and how long the thing takes. The why of every step is on the
            // sheet in the rucksack, and that is where a man reads it — not off a card he is waiting to get rid of.
            sb.Append("Поляна Азау, ").Append(Mathf.RoundToInt(Elbrus.Azau.Ele))
              .Append(" м. До вершины ").Append(Mathf.RoundToInt(Elbrus.WestSummit.Ele))
              .Append(" м — девять дней: акклиматизация, четыре ночи в приютах, штурм затемно.\nПрограмма лежит в рюкзаке.\n\n");
            sb.Append(Programmes.Head(p));
            if (!step.IsEmpty) sb.Append(" · ").Append(step.Line);
            if (aim.Has) sb.Append("\n").Append(Programmes.AimLine(aim));
            sb.Append("\n\nU/F3 — программа · 1 — компас · 3/M — карта");
            planIntroText.text = sb.ToString();
        }

        /// <summary>The sheet is out of the rucksack: fill both halves of it.</summary>
        void UpdatePlanSheet(HikerController me, in Progress p, in Bearing aim)
        {
            bool open = Programmes.Unfolded(me);
            if (planSheet.gameObject.activeSelf != open) planSheet.gameObject.SetActive(open);
            if (!open) return;
            sheetTitle.text = Programmes.Title;
            sheetPage.text = Programmes.Page(p, aim);
            sheetPlan.text = Programmes.Sheet(p);
            sheetFoot.text = Programmes.Foot(p) + " · U/F3 — сложить";
        }

        // ── the marks on the two maps ─────────────────────────────────────────────────────────────────────

        void UpdatePlanMarks(float x, float z, in Progress p, in Bearing aim)
        {
            UpdateMiniGoal(x, z, aim);
            if (bigRoot.gameObject.activeSelf) UpdateBigPlan(x, z, p);
            else foreach (var m in planMarkPool) if (m != null && m.gameObject.activeSelf) m.gameObject.SetActive(false);
        }

        /// <summary>The mini-map: one mark, for the goal of the step being walked. Inside the window it is a pencil
        /// ring where the place is; past the rim it becomes the heading arrow at the edge, which is what a bearing
        /// off the edge of a map actually looks like.</summary>
        void UpdateMiniGoal(float x, float z, in Bearing aim)
        {
            bool show = aim.Has && miniRoot.gameObject.activeSelf;
            if (miniGoal.gameObject.activeSelf != show) miniGoal.gameObject.SetActive(show);
            if (miniGoalLabel.gameObject.activeSelf != show) miniGoalLabel.gameObject.SetActive(show);
            if (!show) return;
            // the window is MiniMetres across and the map inside it is the rim inset by 6 px on each side
            float px = (miniRoot.sizeDelta.x - 12f) / MiniMetres;
            var at = new Vector2((aim.X - x) * px, (aim.Z - z) * px);
            float rim = miniRoot.sizeDelta.x * .5f - 24f;
            bool outside = at.magnitude > rim;
            if (outside) at = at.normalized * rim;
            miniGoal.anchoredPosition = at;
            miniGoalImage.sprite = outside ? Triangle() : Ring();
            miniGoal.localRotation = outside ? Quaternion.Euler(0, 0, -aim.HeadingDeg) : Quaternion.identity;
            miniGoal.sizeDelta = outside ? new Vector2(20, 20) : new Vector2(22, 22);
            miniGoalLabel.rectTransform.anchoredPosition = at + new Vector2(0, -13);
            miniGoalLabel.text = Programme.Far(aim.DistanceM);
        }

        /// <summary>The unfolded map: the whole programme on the sheet at once — what is done, what is being walked
        /// to, what is still ahead, and the lines the party ran past (<see cref="Programme.LeftBehind"/>). Several
        /// steps share a place, so the marks are grouped and the most urgent of them takes the colour.</summary>
        void UpdateBigPlan(float x, float z, in Progress p)
        {
            planSpots.Clear();
            for (int i = 0; i < Programme.Count; i++)
            {
                var step = Programme.At(i);
                var aim = Programme.Aim(step, x, z);
                if (!aim.Has) continue;
                var mark = Programmes.MarkOf(p, i);
                int found = -1;
                for (int k = 0; k < planSpots.Count; k++)
                    if (Elbrus.Distance(planSpots[k].X, planSpots[k].Z, aim.X, aim.Z) < SpotMergeM) { found = k; break; }
                var spot = new Spot { X = aim.X, Z = aim.Z, Mark = mark, Label = aim.Label };
                if (found < 0) { planSpots.Add(spot); continue; }
                if (Rank(mark) > Rank(planSpots[found].Mark)) planSpots[found] = spot;
            }

            float W = Height1079.Core.World.Size;
            for (int i = planMarkPool.Count; i < planSpots.Count; i++)
            {
                var rt = Rect("PlanMark" + i, bigSheet, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20, 20));
                rt.pivot = new Vector2(.5f, .5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Ring(); img.raycastTarget = false;
                var label = Label("Name", rt, new Vector2(0, -12), new Vector2(240, 20), 15, PaperInk, TextAnchor.UpperCenter, FontStyle.Normal, new Vector2(.5f, 0f));
                label.rectTransform.pivot = new Vector2(.5f, 1f); label.font = Hand; label.raycastTarget = false;
                planMarkPool.Add(rt);
                planMarkLabels.Add(label);
            }
            for (int i = 0; i < planMarkPool.Count; i++)
            {
                bool show = i < planSpots.Count;
                if (planMarkPool[i].gameObject.activeSelf != show) planMarkPool[i].gameObject.SetActive(show);
                if (!show) continue;
                var spot = planSpots[i];
                planMarkPool[i].anchoredPosition = new Vector2((spot.X + W / 2) / W * BigSheet, (spot.Z + W / 2) / W * BigSheet);
                float size = spot.Mark == Programmes.Mark.Current ? 26f : spot.Mark == Programmes.Mark.Future ? 14f : 18f;
                planMarkPool[i].sizeDelta = new Vector2(size, size);
                planMarkPool[i].GetComponent<Image>().color = Programmes.Tint(spot.Mark);
                // only the line being walked gets its name written beside it: the sheet already prints the places
                string name = spot.Mark == Programmes.Mark.Current ? spot.Label : "";
                if (planMarkLabels[i].text != name) planMarkLabels[i].text = name;
                planMarkLabels[i].color = Programmes.Tint(spot.Mark);
            }
        }

        static int Rank(Programmes.Mark mark) => mark switch
        {
            Programmes.Mark.Current => 3,
            Programmes.Mark.Future => 2,
            Programmes.Mark.Passed => 1,
            _ => 0,
        };
    }
}
