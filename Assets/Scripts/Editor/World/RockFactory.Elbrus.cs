#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It stays in
// Height1079.Editor because it is built out of the private parts of its own factory, and a partial class
// cannot be split across assemblies; the location's editor assembly calls it from there.
using System.Collections.Generic;
using UnityEngine;

namespace Height1079.EditorTools.World
{
    public static partial class RockFactory
    {
        /// <summary>The same boulders in bare stone, for the summer moraines of Elbrus: what lies on the upward faces
        /// there is grey lichen and dust, not snow.</summary>
        public static List<GameObject> BuildElbrusLibrary()
            => BuildLibrary("Boulder_Elb", Materials.Get("ElbBoulderTop", new Color(.66f, .64f, .6f), smoothness: .08f));
    }
}
#endif
