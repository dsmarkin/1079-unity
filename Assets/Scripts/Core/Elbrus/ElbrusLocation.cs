using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>The southern slope of Elbrus as a location: everything the shared rules used to ask
    /// <c>if (World.IsElbrus)</c> about, gathered behind <see cref="ILocation"/>. The geography itself is
    /// <see cref="Elbrus"/>; the day's rules are <see cref="Ascent"/>, <see cref="Programme"/>, <see cref="Camp"/>
    /// and their neighbours in this folder.
    ///
    /// The location is optional: with HEIGHT1079_NO_ELBRUS this whole assembly is left out of the build and nothing
    /// in the core notices. Somebody has to call <see cref="Register"/> for it to exist — see the list in
    /// <see cref="Locations"/>. This half is engine-free, so the player and editor hooks live in the Elbrus runtime
    /// assembly, not here.</summary>
    public sealed class ElbrusLocation : ILocation
    {
        public static readonly ElbrusLocation Instance = new ElbrusLocation();
        ElbrusLocation() { }

        /// <summary>Put Elbrus on the map. Idempotent: calling it from a player hook, an editor hook and a test
        /// fixture in the same process registers one location, not three.</summary>
        public static void Register() => Locations.Register(Instance);

        public Place Place => Place.Elbrus;
        public string Title => "Эльбрус · южный склон";

        public float Half => Elbrus.Half;
        public int Resolution => Elbrus.Resolution;
        public float GridStep => Elbrus.GridStep;
        public float HeightMin => Elbrus.HeightMin;
        public float HeightMax => Elbrus.HeightMax;

        public string TerrainAsset => "Elbrus";
        public string HeightAsset => "elbrus_height_2049";

        /// <summary>Here the DEM is the ground: nothing is carved into it.</summary>
        public float Ground(HeightField dem, float x, float z) => dem.Sample(x, z);

        public NightRun.Scenario Scenario => Plan;

        // ── shelter ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Huts, barrels and terminals a hiker can shelter in on the Elbrus map.</summary>
        static readonly string[] ShelterIds = { "azau", "krugozor", "mir", "garabashi", "barrels", "redfox", "leaprus", "garabashiHut", "priut11", "priut88" };

        public static bool Sheltered(float x, float z)
        {
            foreach (var id in ShelterIds)
            {
                var p = Elbrus.Get(id);
                if (Elbrus.Distance(x, z, p.X, p.Z) < 16f) return true;
            }
            return false;
        }

        // ── the run ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Day on the southern slope: no February night, no blizzard cycle, and 90 minutes to get up and down.
        /// The cold still bites above the shelf, which is why the profile is not switched off entirely.</summary>
        public static readonly NightRun.Scenario Plan = new NightRun.Scenario
        {
            Id = "elbrus",
            Intro = "Поляна Азау, 2350 м. Наверх — канатной дорогой через Кругозор и Мир до Гара-Баши, дальше ратрак или пешком. Западная вершина — 5642 м.",
            Start = Elbrus.Start,
            NearFireplace = null,
            AtGoal = Elbrus.AtSummit,
            Sheltered = (x, z, y) => Sheltered(x, z),
            Storms = false,
            Profile = SurvivalRules.Profile.Day,
            SpawnRadius = 2.2f,
        };

        // ── snow ──────────────────────────────────────────────────────────────────────────────────────────

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        static float Clamp01(float v) => Clamp(v, 0f, 1f);

        /// <summary>The southern slope, as a sketch until a winter survey is wired in: a wind-worked pack on the lava
        /// ridges and moraine of the lower slope, glacier firn above the shelf — thin and hard on the open ice,
        /// metres deep in the hollows and the crevasse fields that catch the drift (docs/ELBRUS.md).</summary>
        public float SnowDepth(HeightField dem, float x, float z)
        {
            float y = dem.Sample(x, z);
            var (_, _, slopeDeg) = dem.Fall(x, z, SnowCover.Span);
            float bend = Clamp(SnowCover.Hollow(dem, x, z, SnowCover.Span) / SnowCover.BendRelief, -1f, 1f);
            float depth = y < 3000f ? 1f : y < 3800f ? .8f : .55f;
            depth *= 1f - .45f * Math.Min(1f, slopeDeg / 40f);
            depth *= 1f + (bend > 0f ? 1.6f * bend : .55f * bend);
            return Clamp(depth, SnowCover.MinDepth, SnowCover.MaxDepth);
        }

        /// <summary>Firn and wind board carry almost everything above the shelf; lower down the pack is softer.</summary>
        public float SnowCrust(HeightField dem, float x, float z)
        {
            float y = dem.Sample(x, z);
            return Clamp(.55f + .35f * Clamp01((y - 3000f) / 1200f), 0f, 1f);
        }

        /// <summary>Nothing grows above Azau: no canopy anywhere on the playable slope.</summary>
        public float Canopy(float x, float z, float y) => 0f;

        // ── the rucksack ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>What a visitor comes up the Azau ropeway with. Nothing of 1959 is in it — no axe, no saw, no
        /// tinned stew for a fire there is nothing to build — and the two things that make a halt into a camp are:
        /// a двойка and a burner. Everything <see cref="Ascent.Required"/> checks for is hired at the counter below
        /// (<see cref="Rental"/>) and goes into the same rucksack; the two together come to about forty of its fifty
        /// litres, which is the point.</summary>
        public static readonly ItemId[] Starting =
        {
            ItemId.Rusks, ItemId.Chocolate, ItemId.CondensedMilk, ItemId.Flask, ItemId.Socks,
            ItemId.Tent, ItemId.Burner,
            // and the sheet: it is issued with the rucksack the way a real programme is issued at the first briefing
            ItemId.ProgrammeSheet,
        };

        public List<ItemStack> Starter(int index)
        {
            var list = new List<ItemStack>();
            foreach (var id in Starting) list.Add(new ItemStack(id));
            list.Add(new ItemStack(ItemId.Matches, Items.MatchesInBox));
            return list;
        }
    }
}
