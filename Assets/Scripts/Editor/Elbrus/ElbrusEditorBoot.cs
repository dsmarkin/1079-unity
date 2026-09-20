using System.IO;
using UnityEditor;
using Height1079.Core;
using Height1079.Runtime;

namespace Height1079.EditorTools.World
{
    /// <summary>Where the southern slope joins the editor. Nothing in <c>Height1079.Editor</c> names Elbrus any more,
    /// so the map has to put itself into the two lists the editor keeps: the registry of places
    /// (<see cref="Locations"/>, so the generated scene and the factories know it exists) and the world pipeline
    /// (<see cref="WorldImporter.Extras"/>, so <b>1079 → Rebuild world</b> builds it).
    ///
    /// With HEIGHT1079_NO_ELBRUS this whole assembly is left out, nothing subscribes, and the pipeline builds
    /// Холатчахль alone (docs/ELBRUS.md).</summary>
    [InitializeOnLoad]
    static class ElbrusEditorBoot
    {
        static ElbrusEditorBoot() { Hook(); }

        /// <summary>Also as a method, because <c>[InitializeOnLoad]</c> on a static class runs its constructor only
        /// once per domain and a hand-run menu item may get there first. Both are idempotent.</summary>
        [InitializeOnLoadMethod]
        static void Hook()
        {
            ElbrusLocation.Register();

            WorldImporter.Extras -= Build;
            WorldImporter.Extras += Build;
            WorldImporter.ExtrasBuilt -= IsBuilt;
            WorldImporter.ExtrasBuilt += IsBuilt;
        }

        static void Build() => ElbrusImporter.Build();

        /// <summary>A checkout without the raster has nothing to build, and that has always counted as built.</summary>
        static bool IsBuilt() => !ElbrusImporter.SourcePresent || File.Exists(ElbrusImporter.TerrainAsset);
    }
}
