#if !HEIGHT1079_NO_ELBRUS
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Everything the HUD draws for the southern slope of Elbrus and for no other map: the ascent block, the
    /// amber line the mountain takes over, the two hand buttons, and the map half of the rucksack panel.
    ///
    /// This is a partial of <see cref="HudController"/> and therefore has to stay in <c>Height1079.Runtime</c> — a
    /// partial class cannot be split across assemblies. What makes it optional is the <c>#if</c> above: with
    /// HEIGHT1079_NO_ELBRUS the file is not compiled, the <c>partial void</c> hooks declared in the shared file have
    /// no implementation, and every call to them disappears at compile time (docs/ELBRUS.md).</summary>
    public sealed partial class HudController
    {
        /// <summary>The ascent block. Four lines at most, and it says nothing it does not have to: the clock and the
        /// turn-round time are always there, the ground and the air appear where they start to matter, the body speaks
        /// only when something is going wrong with it, and the last line is whatever there is to DO about it right now.
        /// Everything is read off the state the host publishes (<see cref="ClimbNet"/>) and the position, which both
        /// sides have — the HUD asks <see cref="Ascent"/> and <see cref="AscentCold"/> for the words.</summary>
        partial void UpdateClimb(HikerController me, NightSession s)
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

        /// <summary>The camp, the programme sheet and the SOS button: the rucksack's map half.</summary>
        partial void RefreshPackMap(HikerController me)
        {
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
        }

        // ── the hooks the shared HUD calls ────────────────────────────────────────────────────────────

        /// <summary>The amber line: the mountain speaks over the night whenever it has anything to say.</summary>
        partial void MapHint(HikerController me, ref string line)
        {
            if (World.IsElbrus) line = ClimbHint(me);
        }

        partial void MapCompassLine(HikerController me, ref string line)
        {
            var aim = Programmes.Aim(me);
            if (aim.Has) line = Programmes.AimLine(aim);
        }

        partial void MapPrompt(HikerController me, ref string line)
        {
            if (me.Climbing != null) line = me.Climbing.Prompt();
        }

        partial void MapCampPrompt(HikerController me, ref string line) => line = Camps.Prompt(me);

        partial void CampClicked() => NightSession.Instance?.RequestCamp();
        partial void SleepClicked() => NightSession.Instance?.RequestSleep();
        partial void SosClicked() => NightSession.Instance?.SendSos();
        partial void SheetClicked() => Programmes.Toggle(Bootstrap.LocalHiker);

        partial void CramponsClicked()
        {
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            if (s != null && me != null && me.Climbing != null) s.RequestCrampons(!me.Climbing.CramponsOn);
        }

        partial void ThermosClicked() => NightSession.Instance?.RequestSip();
    }
}
#endif
