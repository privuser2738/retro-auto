using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroAuto
{
    public class GamePlaylist
    {
        private readonly string romDirectory;
        private readonly string playlistFile;
        private readonly string progressFile;
        private List<string> allGames;
        private List<string> remainingGames;

        public GamePlaylist(string romDirectory)
        {
            this.romDirectory = romDirectory;
            this.playlistFile = Path.Combine(romDirectory, "games.txt");
            this.progressFile = Path.Combine(romDirectory, "games_progress.txt");
            this.allGames = new List<string>();
            this.remainingGames = new List<string>();
        }

        public void Initialize()
        {
            // Check if games.txt exists, if not create and randomize
            if (!File.Exists(playlistFile))
            {
                Console.WriteLine("Creating new randomized playlist...");
                CreateRandomizedPlaylist();
            }
            else
            {
                Console.WriteLine("Loading existing playlist...");
                allGames = File.ReadAllLines(playlistFile).ToList();
            }

            // Load progress or start fresh
            if (File.Exists(progressFile))
            {
                Console.WriteLine("Resuming from previous session...");
                remainingGames = File.ReadAllLines(progressFile).ToList();
            }
            else
            {
                Console.WriteLine("Starting fresh playlist...");
                remainingGames = new List<string>(allGames);
                SaveProgress();
            }
        }

        public void CreateRandomizedPlaylist()
        {
            // Scan for .bin files
            var binFiles = Directory.GetFiles(romDirectory, "*.bin", SearchOption.TopDirectoryOnly);

            if (binFiles.Length == 0)
            {
                throw new Exception($"No .bin files found in {romDirectory}");
            }

            // Randomize the list
            var random = new Random();
            allGames = binFiles.OrderBy(x => random.Next()).ToList();

            // Save to games.txt
            File.WriteAllLines(playlistFile, allGames);
            Console.WriteLine($"Created playlist with {allGames.Count} games");

            // Initialize progress
            remainingGames = new List<string>(allGames);
            SaveProgress();
        }

        public void ResetPlaylist()
        {
            Console.WriteLine("Resetting and re-randomizing playlist...");
            CreateRandomizedPlaylist();
        }

        public void RestartPlaylist()
        {
            Console.WriteLine("Restarting playlist from beginning...");

            // Load existing games.txt if it exists
            if (File.Exists(playlistFile))
            {
                allGames = File.ReadAllLines(playlistFile).ToList();
            }
            else
            {
                // If no playlist exists, create one
                CreateRandomizedPlaylist();
                return;
            }

            // Reset progress to start from beginning
            remainingGames = new List<string>(allGames);
            SaveProgress();
            Console.WriteLine($"Restarting {allGames.Count} games from the beginning");
        }

        public string? GetNextGame()
        {
            if (remainingGames.Count == 0)
            {
                Console.WriteLine("Playlist completed!");
                return null;
            }

            var game = remainingGames[0];
            return game;
        }

        public void MarkGamePlayed()
        {
            if (remainingGames.Count > 0)
            {
                remainingGames.RemoveAt(0);
                SaveProgress();
            }
        }

        private void SaveProgress()
        {
            File.WriteAllLines(progressFile, remainingGames);
        }

        public int TotalGames => allGames.Count;
        public int RemainingGames => remainingGames.Count;
        public int PlayedGames => TotalGames - RemainingGames;
        public string RomDirectory => romDirectory;
    }
}
