#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It has to live in
// Height1079.Runtime and not in the location's own assembly because a partial class cannot be split across
// assemblies, and because the rest of Height1079.Runtime's Elbrus partials name what is in here.
using System.Text;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The guide's programme on this client: what the local climber has ticked off, what the card in the
    /// corner says, what the compass and the two maps point at, and the text of the folded sheet in the rucksack.
    ///
    /// The same shape as <see cref="Camps"/> and <see cref="Backpacks"/>, and for the same reason. The host owns the
    /// progress — it is the only side that runs <see cref="Programme.Advance"/> — and publishes it per hiker in
    /// <see cref="HikerController.Plan"/>; everything here only reads it and turns it into Russian.
    ///
    /// <b>No text is invented here.</b> Every line comes out of <see cref="Programme"/>: the day heads and profiles,
    /// the step lines, the «зачем» and the «как засчитывается». What this file adds is the marks beside them, the
    /// bearing, and the order things are shown in.
    ///
    /// Silent off the southern slope: on Kholat Syakhl <see cref="On"/> is false and every entry point below returns
    /// nothing at all (<see cref="Programme.AllowedIn"/>).</summary>
    public static class Programmes
    {
        /// <summary>Whether the programme means anything here.</summary>
        public static bool On => Climb.On && Programme.AllowedIn(Height1079.Core.World.Current);

        public static NightSession Session => NightSession.Instance;

        // ── what the local climber has done ───────────────────────────────────────────────────────────────

        /// <summary>The progress of one hiker, as the host last published it.</summary>
        public static Progress Of(HikerController h)
            => !On || h == null ? Progress.None : h.Plan.Value.ToProgress();

        /// <summary>The progress of the player at this keyboard.</summary>
        public static Progress Mine => Of(Bootstrap.LocalHiker);

        // A client never builds a Standing and never asks a condition anything: Programme.Advance runs on the host,
        // which is the only side that can see every one of them (NightSession.Plan). What arrives here is the answer.

        /// <summary>The step this hiker's card is pointing at.</summary>
        public static ProgrammeStep Current(HikerController me) => !On ? ProgrammeStep.Empty : Programme.Current(Of(me));

        /// <summary>Where the current step sends this hiker from where he stands. <see cref="Bearing.Has"/> is false
        /// when the step is about the rucksack and not about a place.</summary>
        public static Bearing Aim(HikerController me)
        {
            if (!On || me == null) return Bearing.None;
            var step = Current(me);
            if (step.IsEmpty) return Bearing.None;
            var pos = me.transform.position;
            return Programme.Aim(step, pos.x, pos.z);
        }

        // ── how a step is shown ───────────────────────────────────────────────────────────────────────────

        /// <summary>What the marks on the sheet and on the map mean. A step is never «failed»: it is done, it is the
        /// one being pointed at, it is still ahead, or the party walked past it (<see cref="Programme.LeftBehind"/>).</summary>
        public enum Mark : byte { Future, Current, Done, Passed }

        public static Mark MarkOf(in Progress p, int index)
        {
            if (p.IsDone(index)) return Mark.Done;
            if (Programme.LeftBehind(p, index)) return Mark.Passed;
            var cur = Programme.Current(p);
            return !cur.IsEmpty && cur.Index == index ? Mark.Current : Mark.Future;
        }

        /// <summary>The pencil mark itself, one character wide so the sheet stays a column.</summary>
        public static string Glyph(Mark mark) => mark switch
        {
            Mark.Done => "+",
            Mark.Current => "›",
            Mark.Passed => "~",
            _ => "·",
        };

        /// <summary>Ink for a mark: done in faded pencil, the current one in the red the map's route is drawn in,
        /// what was skipped in grey, what is still ahead in the paper's own ink.</summary>
        public static Color Tint(Mark mark) => mark switch
        {
            Mark.Done => new Color(.31f, .45f, .35f),
            Mark.Current => new Color(.72f, .16f, .12f),
            Mark.Passed => new Color(.47f, .47f, .45f),
            _ => new Color(.2f, .24f, .31f),
        };

        // ── the card in the corner ────────────────────────────────────────────────────────────────────────

        /// <summary>«День 6 · скалы Пастухова» — the head of the card, or the closing line when there is nothing left
        /// to point at.</summary>
        public static string Head(in Progress p)
        {
            var step = Programme.Current(p);
            if (step.IsEmpty) return Programme.Walked(p) ? "Программа пройдена" : "Программа кончилась";
            return Programme.DayOf(step.Day).Head;
        }

        /// <summary>The bearing as a line of a compass book: «Приют 11, 4050 · 1,2 км · азимут 24°».</summary>
        public static string AimLine(in Bearing aim)
            => !aim.Has ? "" : $"{aim.Label} · {Programme.Far(aim.DistanceM)} · азимут {Mathf.RoundToInt(aim.HeadingDeg) % 360}°";

        // ── the folded sheet ──────────────────────────────────────────────────────────────────────────────

        /// <summary>The left-hand page: the step being walked now, with the reason it is in the programme and the
        /// condition that ticks it. This is the «по запросу» half of the card — the long text never sits on screen.</summary>
        public static string Page(in Progress p, in Bearing aim)
        {
            var step = Programme.Current(p);
            var sb = new StringBuilder();
            if (step.IsEmpty)
            {
                sb.Append(Programme.Walked(p)
                    ? "Программа пройдена: восемь дней и Западная вершина."
                    : "Программа пройдена насквозь, часть дней осталась несделанной. Дальше — по своему усмотрению.");
                return sb.ToString();
            }
            var day = Programme.DayOf(step.Day);
            sb.Append(day.Head).Append(" — ").Append(day.Profile).Append("\n\n");
            sb.Append(step.Line).Append('\n');
            if (aim.Has) sb.Append(AimLine(aim)).Append('\n');
            sb.Append("\nЗачем\n").Append(step.Why).Append('\n');
            sb.Append("\nЗасчитывается\n").Append(step.Test).Append('\n');
            var next = Programme.Next(p);
            if (!next.IsEmpty) sb.Append("\nДальше: ").Append(next.Line);
            return sb.ToString();
        }

        /// <summary>The heading printed across the top of the sheet — the first line of
        /// <see cref="Programme.SheetText"/>.</summary>
        public static string Title => "Программа восхождения · южный склон · " + Programme.LastDay + " дней";

        /// <summary>The right-hand page: all nine days as the sheet prints them
        /// (<see cref="Programme.SheetText"/>), with a mark against every line.</summary>
        public static string Sheet(in Progress p)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var day in Programme.Days())
            {
                if (!first) sb.Append('\n');
                first = false;
                sb.Append(day.Head).Append(" — ").Append(day.Profile).Append('\n');
                foreach (var step in Programme.StepsOf(day.Number))
                    sb.Append(' ').Append(Glyph(MarkOf(p, step.Index))).Append(' ').Append(step.Line).Append('\n');
                if (day.Note.Length > 0) sb.Append("   ").Append(day.Note).Append('\n');
            }
            sb.Append("\n+ сделано   › сейчас   ~ пропущено   · впереди");
            return sb.ToString();
        }

        /// <summary>The footer of the sheet: how much of the programme is behind, and the pace against the date.</summary>
        public static string Foot(in Progress p)
        {
            var sb = new StringBuilder();
            sb.Append("Пройдено ").Append(Mathf.RoundToInt(Programme.Share(p) * 100f)).Append(" % программы");
            int nights = Programme.Nights(p);
            if (nights > 0) sb.Append(" · ночей на горе: ").Append(nights);
            string pace = Programme.PaceText(p, MountainDay.Date);
            if (pace.Length > 0) sb.Append(" · ").Append(pace);
            return sb.ToString();
        }

        // ── the sheet in the hands ────────────────────────────────────────────────────────────────────────

        /// <summary>Is the sheet out of the rucksack and unfolded in the hands right now? The sheet is an ordinary
        /// item (<see cref="ItemId.ProgrammeSheet"/>): holding it is what reading it means, exactly as with the map.</summary>
        public static bool Unfolded(HikerController me)
            => On && me != null && me.Carried.Value.Stack.Id == ItemId.ProgrammeSheet;

        /// <summary>Where the sheet is: in the hands, in this rucksack (and at which shelf), or nowhere.</summary>
        public static int IndexInPack(HikerController me, out int packId)
        {
            packId = 0;
            if (!On || me == null) return -1;
            packId = Backpacks.MyPackId(me);
            if (packId == 0) return -1;
            var contents = Backpacks.Contents(packId);
            for (int i = 0; i < contents.Count; i++) if (contents[i].Id == ItemId.ProgrammeSheet) return i;
            return -1;
        }

        /// <summary>Take the sheet out and unfold it, or fold it back into the rucksack. This is what the F3 key and
        /// the button in the rucksack window both do.</summary>
        public static void Toggle(HikerController me)
        {
            var s = Session;
            if (!On || s == null || me == null) return;
            if (Unfolded(me)) { s.RequestPack(PackAction.Stow, Backpacks.MyPackId(me)); return; }
            // hands busy with something else: the sheet waits, the same way the compass does
            if (me.Carried.Value.Item != 0) { Backpacks.Message = "Руки заняты."; Backpacks.MessageUntil = Time.time + 2f; return; }
            int at = IndexInPack(me, out int pack);
            if (at < 0) { Backpacks.Message = "Листа программы нет в рюкзаке."; Backpacks.MessageUntil = Time.time + 2.5f; return; }
            s.RequestPack(PackAction.Take, pack, at);
            // the sheet fills the screen; the rucksack window underneath it would only be in the way
            Backpacks.Close();
        }

        /// <summary>Owner key: F3 unfolds the sheet and folds it again. Letters do not reach the game under UI
        /// automation and every digit is taken, so the sheet takes the one function key the southern slope leaves
        /// free (F1…F3 belong to the Kholat night: <see cref="Controls.Sheet"/>), and the rucksack window has the
        /// same thing as a button.</summary>
        public static void HandleInput(HikerController me)
        {
            if (!On || me == null) return;
            // a counter with its window up owns the keyboard (UiWindows)
            if (Backpacks.UiOpen || UiWindows.AnyOpen) return;
            if (Controls.Sheet) Toggle(me);
        }

        // ── what the host just ticked ─────────────────────────────────────────────────────────────────────

        /// <summary>The last step the host reported as done, and how long the HUD should keep saying so. One line and
        /// a few seconds: a programme step is worth noticing and not worth a fanfare.</summary>
        public static string Done = "";
        public static float DoneUntil;
        public const float DoneSeconds = 6.5f;

        public static void Ticked(in ProgrammeStep step)
        {
            if (step.IsEmpty) return;
            Done = Programme.DayOf(step.Day).Head + " · " + step.Title + " — сделано";
            DoneUntil = Time.time + DoneSeconds;
        }

        /// <summary>How solid the notice should be drawn, 0…1: it holds, then fades out over the last second.</summary>
        public static float DoneFade => Done.Length == 0 ? 0f : Mathf.Clamp01(DoneUntil - Time.time);
    }
}
#endif
