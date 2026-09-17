using Unity.Netcode.Components;

namespace Height1079.Runtime
{
    /// <summary>Owner-driven movement so the local player feels responsive; the server still decides the night.
    /// Lives in its own file: Unity only serialises a MonoBehaviour into a prefab when the file name matches the class name
    /// (otherwise the build logs "The referenced script on this Behaviour (Game Object 'Hiker') is missing!").</summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
