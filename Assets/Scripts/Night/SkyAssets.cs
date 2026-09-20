using UnityEngine;

namespace Height1079.Night
{
    /// <summary>How the sky finds its dome, its stars, its moon and its clouds.
    ///
    /// The rest of the generated world lives outside Resources and is reached through the kit the game's scene
    /// carries (<c>Runtime/WorldKit</c>), because Unity packs all of Resources into EVERY player and the small map's
    /// player has no use for a mountain. The sky is the exception: both players draw it, so it is generated into
    /// <c>Assets/Resources/World</c> (<c>Editor/World/SkyFactory</c>) and both find it by name, with no kit and no
    /// scene to ask.
    ///
    /// The materials matter for more than the picture: a shader reaches a build only if a material in Resources
    /// refers to it (CLAUDE.md §4), and these five materials are what keep <c>Height1079/Sky*</c> in the player.</summary>
    public static class SkyAssets
    {
        public const string Dir = "World/";

        /// <summary>The asset filed under this path, or null — the callers warn and go on without a sky.</summary>
        public static T Load<T>(string path) where T : Object => Resources.Load<T>(Dir + path);
    }
}
