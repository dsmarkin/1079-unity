#if STEAM_FACEPUNCH
using System;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Height1079.Runtime;

namespace Height1079.Steam
{
    /// <summary>Steam friends-only lobby on top of the community Facepunch transport. Compiled only with the STEAM_FACEPUNCH define
    /// (see unity/README.md → Steam). Untested until the transport package and a Steam client are present.</summary>
    public sealed class SteamLobby : MonoBehaviour, ILobbyProvider
    {
        public string Label => "Steam";
        public bool Available => SteamClient.IsValid;

        Lobby? lobby;
        Action<string> status = _ => { };
        FacepunchTransport transport;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var go = new GameObject("SteamLobby", typeof(SteamLobby));
            DontDestroyOnLoad(go);
            Bootstrap.Lobby = go.GetComponent<SteamLobby>();
        }

        void Awake()
        {
            try { if (!SteamClient.IsValid) SteamClient.Init(480, false); }
            catch (Exception e) { Debug.LogWarning("Steam unavailable: " + e.Message); }
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested;
        }

        void OnDestroy()
        {
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested -= OnJoinRequested;
        }

        void Update() { if (SteamClient.IsValid && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)) SteamClient.RunCallbacks(); }

        FacepunchTransport Transport()
        {
            if (transport == null)
            {
                transport = NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>() ?? NetworkManager.Singleton.gameObject.AddComponent<FacepunchTransport>();
            }
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
            return transport;
        }

        public async void Host(string playerName, Action<string> report)
        {
            status = report ?? status;
            Bootstrap.PlayerName = playerName;
            if (!Available) { status("Steam не запущен."); return; }
            Transport();
            var created = await SteamMatchmaking.CreateLobbyAsync(4);
            if (!created.HasValue) { status("Steam не создал лобби."); return; }
            var l = created.Value;
            l.SetFriendsOnly(); l.SetJoinable(true); l.SetData("game", "1079");
            lobby = l;
            if (NetworkManager.Singleton.StartHost()) { status($"Лобби создано. Пригласите друзей через оверлей Steam (Shift+Tab)."); Bootstrap.Hud.ShowMenu(false); }
            else status("Не удалось запустить хост.");
        }

        public void Invite() { if (lobby.HasValue) SteamFriends.OpenGameInviteOverlay(lobby.Value.Id); }

        public void Leave() { lobby?.Leave(); lobby = null; }

        async void OnJoinRequested(Lobby l, SteamId friend)
        {
            var result = await l.Join();
            if (result != RoomEnter.Success) status("Не удалось войти в лобби: " + result);
        }

        void OnLobbyEntered(Lobby l)
        {
            lobby = l;
            if (NetworkManager.Singleton.IsHost) return; // we created it
            var t = Transport();
            t.targetSteamId = l.Owner.Id;
            if (NetworkManager.Singleton.StartClient()) { status("Подключаемся к " + l.Owner.Name + "…"); Bootstrap.Hud.ShowMenu(false); }
        }
    }
}
#endif
