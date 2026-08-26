using System;
using System.Collections.Generic;
using System.Linq;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuServer
{
    internal static class HiddenPlayerConfig
    {
        private const string HiddenSteamIdsConvar = "vmenu_hidden_steam_ids";
        private static readonly char[] HiddenSteamIdSeparators = { ',', ';', ' ', '\r', '\n', '\t' };

        public static HashSet<string> GetHiddenSteamIdentifiers()
        {
            var hiddenSteamIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredSteamIds = GetConvar(HiddenSteamIdsConvar, string.Empty);

            foreach (var configuredSteamId in configuredSteamIds.Split(HiddenSteamIdSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var normalizedSteamId = NormalizeSteamIdentifier(configuredSteamId);
                if (!string.IsNullOrEmpty(normalizedSteamId))
                {
                    hiddenSteamIds.Add(normalizedSteamId);
                }
            }

            return hiddenSteamIds;
        }

        public static string NormalizeSteamIdentifier(string identifier)
        {
            var normalizedIdentifier = identifier?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedIdentifier))
            {
                return null;
            }

            if (!normalizedIdentifier.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
            {
                normalizedIdentifier = $"steam:{normalizedIdentifier}";
            }

            return normalizedIdentifier;
        }

        public static string GetSteamIdentifier(Player player)
        {
            return player?.Identifiers
                .FirstOrDefault(identifier => !string.IsNullOrEmpty(identifier) && identifier.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
                ?.ToLowerInvariant();
        }

        public static bool IsHiddenSteamIdentifier(string steamIdentifier)
        {
            var normalizedSteamIdentifier = NormalizeSteamIdentifier(steamIdentifier);
            return !string.IsNullOrEmpty(normalizedSteamIdentifier) &&
                GetHiddenSteamIdentifiers().Contains(normalizedSteamIdentifier);
        }

        public static bool IsHiddenPlayer(Player player, HashSet<string> hiddenSteamIds = null)
        {
            hiddenSteamIds ??= GetHiddenSteamIdentifiers();
            if (hiddenSteamIds.Count == 0)
            {
                return false;
            }

            var steamIdentifier = GetSteamIdentifier(player);
            return !string.IsNullOrEmpty(steamIdentifier) && hiddenSteamIds.Contains(steamIdentifier);
        }

        public static HashSet<int> GetHiddenPlayerServerIds(IEnumerable<Player> players)
        {
            var hiddenServerIds = new HashSet<int>();
            var hiddenSteamIds = GetHiddenSteamIdentifiers();

            if (hiddenSteamIds.Count == 0)
            {
                return hiddenServerIds;
            }

            foreach (var player in players)
            {
                if (!int.TryParse(player.Handle, out var serverId))
                {
                    continue;
                }

                if (IsHiddenPlayer(player, hiddenSteamIds))
                {
                    hiddenServerIds.Add(serverId);
                }
            }

            return hiddenServerIds;
        }
    }
}
