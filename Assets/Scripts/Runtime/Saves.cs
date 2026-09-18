using System;
using System.IO;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>The engine's half of saving: where the files live on this machine, what this player's key is, and the
    /// save that the menu has picked and the session has not applied yet. Every rule about the format, the schema and
    /// the atomic write lives in <see cref="SaveStore"/> and <see cref="SaveGame"/>, which are engine-free and tested
    /// in dotnet; this file only knows about Unity.</summary>
    public static class Saves
    {
        /// <summary>Where a save goes: <c>&lt;persistentDataPath&gt;/saves</c>, beside the player's own data and not
        /// inside the game folder, so it survives a reinstall and a rebuild.</summary>
        public static string Dir
        {
            get
            {
                try { return Path.Combine(Application.persistentDataPath, SaveStore.Folder); }
                catch { return SaveStore.Folder; }
            }
        }

        const string InstallKey = "1079.install.id";

        /// <summary>A stable id for this copy of the game, made once and kept in the player prefs. It is the fallback
        /// key for somebody who never typed a name: per machine, so two players never collide, and unchanging, so a
        /// player who always leaves the field alone still comes back to his own acclimatisation.</summary>
        public static string InstallId
        {
            get
            {
                try
                {
                    string id = PlayerPrefs.GetString(InstallKey, "");
                    if (id.Length > 0) return id;
                    id = SaveKeys.Guest(Guid.NewGuid().GetHashCode());
                    PlayerPrefs.SetString(InstallKey, id);
                    PlayerPrefs.Save();
                    return id;
                }
                catch { return SaveKeys.Guest(Environment.TickCount); }
            }
        }

        /// <summary>This player's key: the name from the menu, or the install id when there is no name.</summary>
        public static string KeyFor(string name) => SaveKeys.For(name, InstallId);

        /// <summary>The key of whoever is playing on this machine right now.</summary>
        public static string MyKey => KeyFor(Bootstrap.PlayerName);

        public static SaveSlot Newest(Place place) => SaveStore.Newest(Dir, place);
        public static bool Any(Place place) => !Newest(place).IsEmpty;

        /// <summary>Writes a save and returns its path, or null. Never throws: a full disk must not end a session.</summary>
        public static string Write(SaveGame save)
        {
            try { return SaveStore.Write(Dir, save); }
            catch (Exception e) { Debug.LogWarning("1079: сейв не записан — " + e.Message); return null; }
        }

        public static SaveGame Read(string path)
        {
            try { return SaveStore.Read(path); }
            catch (Exception e) { Debug.LogWarning("1079: сейв не прочитан — " + e.Message); return null; }
        }

        /// <summary>The save the menu chose with «Продолжить». The host's <see cref="NightSession"/> takes it on
        /// spawn and clears it; a session that starts any other way never sees it.</summary>
        public static SaveGame Pending { get; private set; }

        public static void Hold(SaveGame save) => Pending = save;

        public static SaveGame TakePending()
        {
            var s = Pending;
            Pending = null;
            return s;
        }

        public static void Forget() => Pending = null;
    }
}
