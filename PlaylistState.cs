using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RetroAuto
{
    /// <summary>
    /// Manages playlist state persistence - tracks shuffled order and progress
    /// Allows resuming where you left off without re-randomizing
    /// </summary>
    public class PlaylistState
    {
        private readonly string stateFilePath;
        private PlaylistData data;

        public PlaylistState(string directory, string stateFileName = "playlist_state.json")
        {
            Directory.CreateDirectory(directory);
            stateFilePath = Path.Combine(directory, stateFileName);
            data = new PlaylistData();
        }

        /// <summary>
        /// Gets the ordered list of games (in shuffled order)
        /// </summary>
        public List<string> ShuffledOrder => data.ShuffledOrder;

        /// <summary>
        /// Gets the current position in the playlist (next game to play)
        /// </summary>
        public int CurrentIndex => data.CurrentIndex;

        /// <summary>
        /// Gets the number of games played in this session/playlist
        /// </summary>
        public int GamesPlayed => data.CurrentIndex;

        /// <summary>
        /// Gets total games in the playlist
        /// </summary>
        public int TotalGames => data.ShuffledOrder.Count;

        /// <summary>
        /// Gets remaining games in the playlist
        /// </summary>
        public int RemainingGames => Math.Max(0, data.ShuffledOrder.Count - data.CurrentIndex);

        /// <summary>
        /// Returns true if we have a valid saved state
        /// </summary>
        public bool HasSavedState => File.Exists(stateFilePath);

        /// <summary>
        /// Initializes or loads the playlist state
        /// If games list has changed, preserves progress for games that still exist
        /// </summary>
        public void Initialize(IEnumerable<string> currentGames, bool forceReset = false, bool resetProgressOnly = false)
        {
            var currentGamesList = currentGames.ToList();

            if (forceReset)
            {
                // Full reset - delete old state and create completely new random order
                DeleteState();
                CreateNewPlaylist(currentGamesList);
                Save();
                return;
            }

            if (!HasSavedState)
            {
                // No saved state - create new random order
                CreateNewPlaylist(currentGamesList);
                Save();
                return;
            }

            // Try to load existing state
            if (!TryLoad())
            {
                CreateNewPlaylist(currentGamesList);
                Save();
                return;
            }

            if (resetProgressOnly)
            {
                // Keep the same order, just reset progress to beginning
                data.CurrentIndex = 0;
                data.LastPlayed = null;
                Save();
                return;
            }

            // Check if games list has changed
            var savedSet = new HashSet<string>(data.ShuffledOrder, StringComparer.OrdinalIgnoreCase);
            var currentSet = new HashSet<string>(currentGamesList, StringComparer.OrdinalIgnoreCase);

            if (savedSet.SetEquals(currentSet))
            {
                // Same games - continue where we left off
                return;
            }

            // Games changed - reconcile the lists
            ReconcileLists(currentGamesList);
            Save();
        }

        /// <summary>
        /// Creates a new randomized playlist
        /// </summary>
        private void CreateNewPlaylist(List<string> games)
        {
            var random = new Random();
            data = new PlaylistData
            {
                ShuffledOrder = games.OrderBy(x => random.Next()).ToList(),
                CurrentIndex = 0,
                CreatedAt = DateTime.Now,
                LastPlayed = null
            };
        }

        /// <summary>
        /// Reconciles saved list with current games - removes missing, adds new games at end
        /// </summary>
        private void ReconcileLists(List<string> currentGames)
        {
            var currentSet = new HashSet<string>(currentGames, StringComparer.OrdinalIgnoreCase);
            var savedSet = new HashSet<string>(data.ShuffledOrder, StringComparer.OrdinalIgnoreCase);

            // Keep existing games in their order
            var reconciledList = data.ShuffledOrder
                .Where(g => currentSet.Contains(g))
                .ToList();

            // Adjust current index if games before it were removed
            int removedBeforeIndex = data.ShuffledOrder
                .Take(data.CurrentIndex)
                .Count(g => !currentSet.Contains(g));
            data.CurrentIndex = Math.Max(0, data.CurrentIndex - removedBeforeIndex);

            // Add new games (shuffled) at the end
            var newGames = currentGames
                .Where(g => !savedSet.Contains(g))
                .OrderBy(x => new Random().Next())
                .ToList();

            reconciledList.AddRange(newGames);
            data.ShuffledOrder = reconciledList;
        }

        /// <summary>
        /// Gets the next game to play without advancing
        /// </summary>
        public string? PeekNext()
        {
            if (data.CurrentIndex >= data.ShuffledOrder.Count)
                return null;
            return data.ShuffledOrder[data.CurrentIndex];
        }

        /// <summary>
        /// Gets the next game and advances the index
        /// </summary>
        public string? GetNext()
        {
            if (data.CurrentIndex >= data.ShuffledOrder.Count)
                return null;

            var game = data.ShuffledOrder[data.CurrentIndex];
            data.CurrentIndex++;
            data.LastPlayed = game;
            Save();
            return game;
        }

        /// <summary>
        /// Peeks at game after the next one (for "Up Next" display)
        /// </summary>
        public string? PeekAfterNext()
        {
            int nextIndex = data.CurrentIndex + 1;
            if (nextIndex >= data.ShuffledOrder.Count)
                return null;
            return data.ShuffledOrder[nextIndex];
        }

        /// <summary>
        /// Marks the current game as played and advances
        /// </summary>
        public void MarkPlayed()
        {
            // Only advance if we haven't already
            // GetNext already advances, so this is for cases where we peek then mark
            Save();
        }

        /// <summary>
        /// Skips the current game (moves to next without marking played differently)
        /// </summary>
        public void Skip()
        {
            if (data.CurrentIndex < data.ShuffledOrder.Count)
            {
                data.CurrentIndex++;
                Save();
            }
        }

        /// <summary>
        /// Resets progress to beginning while keeping the same order
        /// </summary>
        public void ResetProgress()
        {
            data.CurrentIndex = 0;
            data.LastPlayed = null;
            Save();
        }

        /// <summary>
        /// Fully resets with a new random order
        /// </summary>
        public void FullReset(IEnumerable<string> games)
        {
            CreateNewPlaylist(games.ToList());
            Save();
        }

        /// <summary>
        /// Gets remaining games from current position
        /// </summary>
        public List<string> GetRemainingGames()
        {
            if (data.CurrentIndex >= data.ShuffledOrder.Count)
                return new List<string>();
            return data.ShuffledOrder.Skip(data.CurrentIndex).ToList();
        }

        /// <summary>
        /// Saves the current state to file
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save playlist state: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to load state from file
        /// </summary>
        private bool TryLoad()
        {
            try
            {
                if (!File.Exists(stateFilePath))
                    return false;

                var json = File.ReadAllText(stateFilePath);
                var loaded = JsonSerializer.Deserialize<PlaylistData>(json);
                if (loaded != null && loaded.ShuffledOrder.Count > 0)
                {
                    data = loaded;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load playlist state: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Deletes the state file
        /// </summary>
        public void DeleteState()
        {
            try
            {
                if (File.Exists(stateFilePath))
                    File.Delete(stateFilePath);
            }
            catch { }
        }

        private class PlaylistData
        {
            public List<string> ShuffledOrder { get; set; } = new();
            public int CurrentIndex { get; set; }
            public DateTime CreatedAt { get; set; }
            public string? LastPlayed { get; set; }
        }
    }
}
