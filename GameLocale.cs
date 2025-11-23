using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RetroAuto
{
    /// <summary>
    /// Handles game locale/region detection and filtering
    /// </summary>
    public static class GameLocale
    {
        // 2-character locale codes
        public const string English = "en";      // USA, Europe, World, UK, Australia
        public const string Japanese = "jp";     // Japan
        public const string European = "eu";     // Europe only (not USA)
        public const string All = "*";           // Wildcard - all regions

        // Region patterns found in filenames
        private static readonly Dictionary<string, string[]> LocalePatterns = new()
        {
            { English, new[] { "(USA)", "(US)", "(World)", "(UK)", "(Australia)", "(En)", "(English)" } },
            { Japanese, new[] { "(Japan)", "(JP)", "(Ja)", "(Japanese)", "(J)" } },
            { European, new[] { "(Europe)", "(EU)", "(Germany)", "(France)", "(Spain)", "(Italy)", "(Netherlands)", "(Sweden)", "(De)", "(Fr)", "(Es)", "(It)" } }
        };

        // World is a wildcard - matches all locales
        private static readonly string[] WorldPatterns = { "(World)" };

        /// <summary>
        /// Detects the locale of a game from its filename
        /// </summary>
        public static string DetectLocale(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return All;

            // Check for World first (wildcard)
            if (WorldPatterns.Any(p => fileName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return All;

            // Check each locale
            foreach (var kvp in LocalePatterns)
            {
                if (kvp.Value.Any(pattern => fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                    return kvp.Key;
            }

            // Default to unknown/all
            return All;
        }

        /// <summary>
        /// Checks if a game matches the requested locale filter
        /// </summary>
        public static bool MatchesLocale(string fileName, string? localeFilter)
        {
            // No filter or wildcard = match all
            if (string.IsNullOrEmpty(localeFilter) || localeFilter == All)
                return true;

            string gameLocale = DetectLocale(fileName);

            // World games match any locale filter
            if (gameLocale == All)
                return true;

            // English filter also matches European games (they're often in English)
            if (localeFilter == English)
            {
                return gameLocale == English ||
                       gameLocale == European ||
                       gameLocale == All;
            }

            return gameLocale == localeFilter;
        }

        /// <summary>
        /// Filters a list of games by locale
        /// </summary>
        public static IEnumerable<T> FilterByLocale<T>(IEnumerable<T> games, string? localeFilter, Func<T, string> getFileName)
        {
            if (string.IsNullOrEmpty(localeFilter) || localeFilter == All)
                return games;

            return games.Where(g => MatchesLocale(getFileName(g), localeFilter));
        }

        /// <summary>
        /// Gets a display name for a locale code
        /// </summary>
        public static string GetLocaleName(string? locale)
        {
            return locale switch
            {
                English => "English (USA/Europe/World)",
                Japanese => "Japanese",
                European => "European",
                All or null or "" => "All Regions",
                _ => locale
            };
        }

        /// <summary>
        /// Parses locale from command line args (--locale=XX or -l XX)
        /// </summary>
        public static string? ParseLocaleFromArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower();

                // --locale=XX format
                if (arg.StartsWith("--locale="))
                {
                    return arg.Substring("--locale=".Length).ToLower();
                }

                // -l XX format
                if ((arg == "-l" || arg == "--locale") && i + 1 < args.Length)
                {
                    return args[i + 1].ToLower();
                }
            }

            return null;
        }

        /// <summary>
        /// Validates a locale code
        /// </summary>
        public static bool IsValidLocale(string? locale)
        {
            if (string.IsNullOrEmpty(locale) || locale == All)
                return true;

            return locale == English || locale == Japanese || locale == European;
        }
    }
}
