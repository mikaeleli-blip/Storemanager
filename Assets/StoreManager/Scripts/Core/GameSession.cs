using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StoreManager
{
    /// <summary>
    /// New game, save and load. Saves are text files in Application.persistentDataPath/Saves:
    /// a quick-save slot (F5 / F9), three manual slots and a nightly autosave.
    /// Starting a new game or loading swaps in a clean scene, so nothing from the old store is left behind.
    /// </summary>
    public static class GameSession
    {
        public const string Quick = "quicksave", Auto = "autosave";
        public static readonly string[] Slots = { Quick, "slot1", "slot2", "slot3", Auto };

        /// <summary>Runs after a new or loaded game has been built (the UI Toolkit layer re-creates itself).</summary>
        public static event Action Booted;

        public static bool Busy { get; private set; }
        static string pending;
        static int scenes;

        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "Saves"); } }
        static string PathOf(string slot) { return Path.Combine(Folder, slot + ".txt"); }

        public static string SlotName(string slot)
        {
            switch (slot)
            {
                case Quick: return "Quick save (F5)";
                case Auto: return "Autosave (every night)";
                default: return "Slot " + slot.Substring(4);
            }
        }

        /// <summary>Writes the store to a slot. Returns null, or what went wrong.</summary>
        public static string Save(string slot)
        {
            var s = Store.I;
            if (s == null) return "Nothing to save.";
            if (!s.StartChosen) return "Pick how to start first.";
            try
            {
                Directory.CreateDirectory(Folder);
                string path = PathOf(slot), tmp = path + ".tmp";
                File.WriteAllText(tmp, s.WriteSave());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { return "Couldn't save: " + e.Message; }
            return null;
        }

        /// <summary>The save's summary line (day, cash, level), or null for an empty slot.</summary>
        public static string Summary(string slot)
        {
            try
            {
                string path = PathOf(slot);
                if (!File.Exists(path)) return null;
                using (var r = new StreamReader(path)) return Store.PeekSummary(r.ReadLine());
            }
            catch (Exception) { return null; }
        }

        public static string SavedAt(string slot)
        {
            try
            {
                string path = PathOf(slot);
                return File.Exists(path) ? File.GetLastWriteTime(path).ToString("d MMM HH:mm") : "";
            }
            catch (Exception) { return ""; }
        }

        /// <summary>Loads a slot (the current game is thrown away). Returns null, or why it couldn't.</summary>
        public static string Load(string slot)
        {
            string text;
            try { text = File.ReadAllText(PathOf(slot)); }
            catch (Exception) { return "There's no save in that slot."; }
            if (Store.PeekSummary(text) == null) return "That file isn't a Storemanager save.";
            Restart(text);
            return null;
        }

        /// <summary>Loads a save from text (tests, or a file picked elsewhere).</summary>
        public static string LoadText(string text)
        {
            if (Store.PeekSummary(text) == null) return "That isn't a Storemanager save.";
            Restart(text);
            return null;
        }

        public static void NewGame() { Restart(null); }

        static void Restart(string save)
        {
            if (Busy) return;
            Busy = true;
            pending = save;
            Time.timeScale = 1f;
            var old = SceneManager.GetActiveScene();
            var fresh = SceneManager.CreateScene("Storemanager " + (++scenes));
            SceneManager.SetActiveScene(fresh);
            var op = SceneManager.UnloadSceneAsync(old);
            if (op == null) Boot();
            else op.completed += _ => Boot();
        }

        static void Boot()
        {
            Busy = false;
            Bootstrap.Start();
            if (Booted != null) Booted();
            if (pending == null) return;
            string why = Store.I.ApplySave(pending);
            pending = null;
            if (why != null) { Store.I.Message(why); Hud.Toast(why); }
            else Hud.Toast("Game loaded: " + Store.I.SaveSummary);
        }
    }
}
