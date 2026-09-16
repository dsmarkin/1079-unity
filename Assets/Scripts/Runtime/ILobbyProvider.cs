using System;

namespace Height1079.Runtime
{
    /// <summary>Optional platform lobby (Steam). Registered by the Height1079.Steam assembly when it is compiled in; absent otherwise.</summary>
    public interface ILobbyProvider
    {
        string Label { get; }
        bool Available { get; }
        /// <summary>Create a friends-only lobby and start hosting through the platform transport.</summary>
        void Host(string playerName, Action<string> status);
        /// <summary>Open the platform invite overlay for the current lobby.</summary>
        void Invite();
        void Leave();
    }
}
