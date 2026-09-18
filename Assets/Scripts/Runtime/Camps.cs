using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The camp on this client: which tent is within reach, whether there is one in the rucksack to put up,
    /// and the two keys that ask the host to do either. The host keeps the truth
    /// (<see cref="NightSession.CampList"/>); everything here only reads it.
    ///
    /// The same shape as <see cref="Backpacks"/>, and for the same reason: the window and the keys must agree with the
    /// rules without asking the host anything.</summary>
    public static class Camps
    {
        /// <summary>Whether camps mean anything here at all. On Kholat Syakhl they do not
        /// (<see cref="Camp.AllowedIn"/>), and every entry point below is quiet.</summary>
        public static bool On => Climb.On && Camp.AllowedIn(Height1079.Core.World.Current);

        public static NightSession Session => NightSession.Instance;

        /// <summary>The nearest camp and how far it is, or 0.</summary>
        public static int NearbyId(HikerController me, out float metres)
        {
            metres = float.MaxValue;
            var s = Session;
            if (!On || s == null || me == null) return 0;
            int best = 0;
            var at = new Vector2(me.transform.position.x, me.transform.position.z);
            for (int i = 0; i < s.CampList.Count; i++)
            {
                var c = s.CampList[i];
                float d = Vector2.Distance(at, new Vector2(c.Pos.x, c.Pos.z));
                if (d < metres) { metres = d; best = c.Id; }
            }
            return best;
        }

        /// <summary>The camp you are standing at, or 0.</summary>
        public static int Here(HikerController me)
        {
            int id = NearbyId(me, out float metres);
            return id != 0 && metres <= Camp.SleepReachM ? id : 0;
        }

        /// <summary>Is there a двойка in the rucksack to put up?</summary>
        public static bool HaveTent(HikerController me)
        {
            if (!On || me == null) return false;
            if (me.Carried.Value.Stack.Id == ItemId.Tent) return true;
            int pack = Backpacks.MyPackId(me);
            if (pack == 0) return false;
            foreach (var s in Backpacks.Contents(pack)) if (s.Id == ItemId.Tent) return true;
            return false;
        }

        /// <summary>Owner keys: B (or 7) puts the tent up here and takes down the one you are standing at,
        /// P (or 8) is the night in it. Both are asks — the host decides, and both take time standing still.
        ///
        /// Ignored whenever a window owns the cursor. The hire counter and the café order by digits 1…9, and the
        /// rucksack window is where the same two actions have their buttons, so a key press while any of them is up
        /// belongs to the window and not to the mountain.</summary>
        public static void HandleInput(HikerController me)
        {
            var s = Session;
            if (!On || s == null || me == null) return;
            if (Backpacks.UiOpen || RentalService.Ordering || CafeService.Ordering
                || LodgeService.Booking || RescueDesk.Filing || Ratraks.Hailing) return;
            if (Controls.CampToggle) s.RequestCamp();
            if (Controls.CampSleep) s.RequestSleep();
        }

        /// <summary>What the button in the rucksack window says, and what the key would do.</summary>
        public static string ToggleLabel(HikerController me)
            => Here(me) != 0 ? "Свернуть лагерь" : "Поставить лагерь";

        /// <summary>The line under the crosshair when a camp is the thing to think about, or "".</summary>
        public static string Prompt(HikerController me)
        {
            if (!On || me == null) return "";
            if (Here(me) != 0) return "Лагерь · P/8 — ночёвка и сохранение · B/7 — свернуть";
            if (!HaveTent(me)) return "";
            var gear = me.Climbing;
            if (gear == null) return "";
            // do not nag down on the meadow: the tent is worth mentioning where a party would actually stop
            if (gear.Where.Ele < Elbrus.Barrels.Ele - 120f) return "";
            return "B/7 — поставить лагерь (привал и сохранение)";
        }
    }
}
