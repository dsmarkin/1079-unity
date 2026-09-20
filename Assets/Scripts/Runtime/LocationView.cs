using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The engine side of a map, the way <see cref="ILocation"/> is its rules side. Everything here needs
    /// UnityEngine, so it cannot live beside the rules; everything here is still an <em>answer</em> the shell asks the
    /// map for, never a branch the shell takes on a map's name.
    ///
    /// A map that adds nothing to the shell registers no view. The shell then runs its own path, which is the night of
    /// 1–2 February 1959 on Холатчахль: that map is the game, and its dressing lives in <see cref="Bootstrap"/> and
    /// <see cref="WorldDressing"/> rather than behind this interface.
    ///
    /// The point of the seam is that an optional map can be left out of a build (HEIGHT1079_NO_ELBRUS,
    /// docs/ELBRUS.md): with its assembly gone nothing registers a view, every call below finds none, and no file in
    /// the shell has to mention the map that is missing.</summary>
    public interface ILocationView
    {
        /// <summary>Which map this view speaks for.</summary>
        Place Place { get; }

        /// <summary>Metres below the lowest node of the height raster the terrain object is anchored at. Must match
        /// what the editor importer wrote for this map.</summary>
        float GroundMargin { get; }

        /// <summary>The map's own look at the terrain the shell has just built — a splat map that did not survive the
        /// import is the kind of thing that is worth saying out loud here.</summary>
        void Inspect(Terrain terrain);

        /// <summary>Everything that stands on the terrain, and the light it is all seen by. Called instead of the
        /// shell's own dressing; the returned light becomes the sun of the session.</summary>
        Light Build(HeightField dem);

        /// <summary>Whether the machinery of the 1959 night runs here: falling snow, ski trails, forest mist, the Menk
        /// and the star dome. False on a map that is played in daylight.</summary>
        bool RunsNight { get; }

        /// <summary>How hard this map's own weather is blowing, 0…1, on top of whatever the night is doing. A map with
        /// no weather of its own answers 0 and the wind is the night's alone.</summary>
        float DayWind { get; }

        /// <summary>The bearing the map wants held right now — the line a guide's programme is on — for the brass
        /// index of the compass. False: no bearing, and the compass shows nothing but north.</summary>
        bool Course(HikerController hiker, out float headingDeg);

        /// <summary>Put the menu camera where this map looks best. False: the shell places it its own way.</summary>
        bool MenuCamera(Camera cam, HeightField dem);

        /// <summary>The map's own frame — light, fog, film. True: it has taken the frame over, and
        /// <paramref name="darkness"/> and <paramref name="storm"/> are what the shell should show.</summary>
        bool Frame(out float darkness, out float storm);

        /// <summary>What «Продолжить» has to offer here: a line for the button, or null when there is nothing saved.
        /// A map that cannot be saved leaves this null and the button is simply not there.</summary>
        string Saved { get; }

        /// <summary>Pick up the newest save and hold it for the session that is about to start. Returns the status
        /// line to show, or null when there was nothing to pick up.</summary>
        string Resume();

        /// <summary>Drop a save that was picked but never applied — hosting failed, or the player left the menu.</summary>
        void Drop();
    }

    /// <summary>The views this build has, one per map at most. A map's own assembly registers its view as it starts
    /// (<c>[RuntimeInitializeOnLoadMethod]</c> in a player, <c>[InitializeOnLoadMethod]</c> in the editor), exactly as
    /// it registers its <see cref="ILocation"/> with <see cref="Locations"/>.</summary>
    public static class LocationViews
    {
        static readonly System.Collections.Generic.List<ILocationView> Known = new System.Collections.Generic.List<ILocationView>();

        /// <summary>Add a view, or replace the one already registered for the same place. Idempotent, for the same
        /// reason <see cref="Locations.Register"/> is: one process can run the player hook, the editor hook and a test
        /// fixture and must still end up with one view.</summary>
        public static void Register(ILocationView view)
        {
            if (view == null) return;
            for (int i = 0; i < Known.Count; i++)
                if (Known[i].Place == view.Place) { Known[i] = view; return; }
            Known.Add(view);
        }

        /// <summary>The view for a place, or null when that map brings none (and when it is not in this build).</summary>
        public static ILocationView Of(Place place)
        {
            foreach (var v in Known) if (v.Place == place) return v;
            return null;
        }

        /// <summary>The view of the map the session is in, or null.</summary>
        public static ILocationView Active => Of(World.Current);

        /// <summary>Whether the 1959 night machinery runs where we are. A map with no view of its own is Холатчахль,
        /// and there it does.</summary>
        public static bool RunsNight
        {
            get { var v = Active; return v == null || v.RunsNight; }
        }

        /// <summary>How hard the map's own weather is blowing where we are, 0…1.</summary>
        public static float DayWind
        {
            get { var v = Active; return v == null ? 0f : v.DayWind; }
        }

        /// <summary>Metres the terrain is anchored below the lowest node of its height raster.</summary>
        public static float GroundMargin
        {
            get { var v = Active; return v == null ? 4f : v.GroundMargin; }
        }

        /// <summary>Forget every save that was picked in the menu and never applied, on every map. Called when a
        /// session ends: a save held for a place nobody is playing any more must not leak into the next one.</summary>
        public static void DropAll()
        {
            foreach (var v in Known) v.Drop();
        }
    }
}
