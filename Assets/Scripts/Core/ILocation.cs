using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Everything the shared rules need to know about the place a session is played in. One implementation
    /// per <see cref="Place"/>; shared code reaches them only through <see cref="Locations"/> and <see cref="World"/>,
    /// so a location can live in its own assembly and be left out of a build without any caller noticing.
    ///
    /// The contract is deliberately narrow: it is exactly what <see cref="World"/> used to answer with an
    /// <c>if (IsElbrus)</c>, plus the two snow hooks <see cref="SnowCover"/> needed and the starting kit
    /// <see cref="Items"/> needed. A location that wants more than this should not widen the interface — it should
    /// keep the extra rules in its own assembly, where only its own code can see them.</summary>
    public interface ILocation
    {
        /// <summary>Which member of the save-format enum this location answers for.</summary>
        Place Place { get; }

        /// <summary>Menu line, Russian, as the player reads it.</summary>
        string Title { get; }

        /// <summary>Half the side of the playable square, metres.</summary>
        float Half { get; }

        /// <summary>Nodes per side of the height grid.</summary>
        int Resolution { get; }

        /// <summary>Metres between two neighbouring nodes of that grid.</summary>
        float GridStep { get; }

        /// <summary>Range of the height raster, metres above sea level. Must match what the terrain tool wrote.</summary>
        float HeightMin { get; }
        float HeightMax { get; }

        /// <summary>Name of the TerrainData asset; the game finds it through WorldAssets (Assets/Generated/World).</summary>
        string TerrainAsset { get; }

        /// <summary>Name of the height raster; the game finds it through WorldAssets (Assets/Generated/World).</summary>
        string HeightAsset { get; }

        /// <summary>Ground height at a point: the bare DEM, or the DEM with whatever the map carves into it.</summary>
        float Ground(HeightField dem, float x, float z);

        /// <summary>What a run in this place is about — start, shelter, goal, clock (<see cref="NightRun.Scenario"/>).</summary>
        NightRun.Scenario Scenario { get; }

        /// <summary>Metres of snow above the ground. Called by <see cref="SnowCover.Depth"/> after the measured
        /// raster hook has had its say, so an implementation may assume there is no raster.</summary>
        float SnowDepth(HeightField dem, float x, float z);

        /// <summary>How well that snow carries a walker: 0 — powder to go through, 1 — wind crust to walk on top of.
        /// Called by <see cref="SnowCover.Crust"/>.</summary>
        float SnowCrust(HeightField dem, float x, float z);

        /// <summary>How much canopy stands over a point, 0..1, when no measured canopy mask is wired up. Called by
        /// <see cref="SnowCover.Forest"/>; <paramref name="y"/> is the ground height there, already sampled.</summary>
        float Canopy(float x, float z, float y);

        /// <summary>The rucksack a participant starts with. <paramref name="index"/> is the player's number in the
        /// party, which is what decides who carries the axe and who the saw.</summary>
        List<ItemStack> Starter(int index);
    }

    /// <summary>The places this build knows about. Kholat Syakhl is part of the core and is always here; every other
    /// location registers itself when its assembly is compiled in, the way the Steam layer does
    /// (Assets/Scripts/Steam). Nothing in the shared code names a location: it asks the registry.
    ///
    /// Who calls <see cref="Register"/> depends on where the code is running — a location's own assembly wires this
    /// up once, and the call must be safe to repeat:
    /// <list type="bullet">
    /// <item>in a player: <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c>;</item>
    /// <item>in the editor: <c>[InitializeOnLoadMethod]</c>;</item>
    /// <item>in a test assembly: <c>[SetUpFixture]</c> with <c>[OneTimeSetUp]</c>;</item>
    /// <item>in the dotnet runner (Tools/CoreTests): <c>[ModuleInitializer]</c> in Shim.cs.</item>
    /// </list>
    /// The first two need UnityEngine, so they cannot live in an engine-free assembly: a location's engine-free half
    /// exposes a plain <c>Register()</c> and its runtime half calls it from the attribute.</summary>
    public static class Locations
    {
        static readonly List<ILocation> Known = new List<ILocation>();

        /// <summary>The location that is always in the build, and the one every lookup falls back to.</summary>
        public static readonly ILocation Default = KholatLocation.Instance;

        static Locations() { Known.Add(Default); }

        /// <summary>Add a location, or replace the one already registered for the same <see cref="Place"/>.
        /// Idempotent on purpose: the same assembly may be initialised from a player hook, an editor hook and a test
        /// fixture in one process, and registering twice must not give two Elbruses.</summary>
        public static void Register(ILocation location)
        {
            if (location == null) return;
            for (int i = 0; i < Known.Count; i++)
                if (Known[i].Place == location.Place) { Known[i] = location; return; }
            Known.Add(location);
        }

        /// <summary>Everything this build can play, <see cref="Default"/> first. The menu reads this list instead of
        /// listing the <see cref="Place"/> enum, so a location left out of the build is simply not offered.</summary>
        public static IReadOnlyList<ILocation> Available => Known;

        /// <summary>Whether this build has that place in it at all.</summary>
        public static bool Has(Place place)
        {
            foreach (var l in Known) if (l.Place == place) return true;
            return false;
        }

        /// <summary>The location for a place. Never null: a place that is not in this build reads as
        /// <see cref="Default"/>, so an old save naming it cannot take the game down. <see cref="World.Current"/>
        /// refuses such a place outright, which is what keeps the two answers consistent.</summary>
        public static ILocation Of(Place place)
        {
            foreach (var l in Known) if (l.Place == place) return l;
            return Default;
        }

        /// <summary>The location the session is in.</summary>
        public static ILocation Active => Of(World.Current);
    }
}
