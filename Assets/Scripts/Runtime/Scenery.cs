using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>What it costs to draw the standing scenery, and the two cheap ways of paying less for it.
    ///
    /// The measurement that made this file: on the Azau meadow, looking uphill away from the village, a frame took
    /// 20 ms; turning round to face the village — hotels, cafés, kiosks, lamps, cars — took 34. A third of the
    /// pixels changed nothing and the quality preset changed nothing, so neither fill nor the grass, tree and
    /// shadow distances were the limit. What is left is the count: about two hundred and fifty prefabs, each built
    /// out of thirty to forty-six separate <c>MeshRenderer</c>s by <c>ElbrusProps.Part</c>, is some fifteen hundred
    /// draw calls with a material change on most of them.
    ///
    /// Two answers, neither of which touches how anything looks up close:
    /// <list type="bullet">
    /// <item><b>Static batching</b> (<see cref="Batch"/>) merges every mesh of a group that shares a material into
    /// one buffer, so the renderers stay but the draws are sorted by material and the state changes collapse. The
    /// objects must never move afterwards — which is why the ropeways, the snow-cats and the other parties are not
    /// batched, and the buildings are.</item>
    /// <item><b>A layer with a cull distance</b> (<see cref="Props"/>): a bin, a bench, a chain post or a lamp is
    /// three metres of nothing at four hundred, so the camera stops drawing that layer beyond
    /// <see cref="PropsCullM"/>. This costs nothing per frame — the camera's own culling does it — and it is the
    /// only kind of LOD that removes the draw call as well as the triangles.</item>
    /// </list>
    ///
    /// The layer is used by index and not by name: this project keeps no <c>TagManager.asset</c> in the repository
    /// (everything is generated), and Unity is perfectly happy with an unnamed layer. <see cref="Name"/> gives it a
    /// name in the editor for whoever opens the inspector.</summary>
    public static class Scenery
    {
        /// <summary>Small standing dressing: lamps, benches, bins, chains, turnstiles, flag poles, drums, cars.</summary>
        public const int Props = 8;

        /// <summary>Metres beyond which that layer is not drawn at all. Chosen by eye on the Azau square: the far
        /// end of the souvenir rows is about ninety metres, the car park about two hundred.</summary>
        public const float PropsCullM = 260f;

        /// <summary>The camera does the culling, so this is set once per camera rather than every frame. Safe to call
        /// again: the array is rebuilt from scratch each time.</summary>
        public static void Cull(Camera cam)
        {
            if (cam == null) return;
            var far = new float[32];
            for (int i = 0; i < far.Length; i++) far[i] = 0f;   // 0 = the camera's own far plane
            far[Props] = PropsCullM;
            cam.layerCullDistances = far;
            // spherical, so a prop does not pop in when the camera turns towards it: the distance is to the camera,
            // not to the near plane
            cam.layerCullSpherical = true;
        }

        /// <summary>Puts a whole subtree on a layer.</summary>
        public static void Layer(Transform root, int layer)
        {
            if (root == null) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) Layer(root.GetChild(i), layer);
        }

        /// <summary>Combines the meshes under one root by material. Call it once, after the group is finished and
        /// before anything is looking at it; nothing under it may move afterwards.</summary>
        public static void Batch(Transform root)
        {
            if (root == null) return;
            try { StaticBatchingUtility.Combine(root.gameObject); }
            catch (System.Exception e) { Debug.LogWarning("1079: не удалось сбатчить " + root.name + " — " + e.Message); }
        }
    }
}
