using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace vMenuServer
{
    // Adapter only: code6_selfmanage owns resolution, HTTP and last-known identities.
    public class NameSyncService : BaseScript
    {
        public static NameSyncService Current { get; private set; }
        private readonly Dictionary<int, string> _serverNameMap = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _serverTerminalMap = new Dictionary<int, string>();
        private readonly Dictionary<int, IdentitySnapshot> _lastKnown = new Dictionary<int, IdentitySnapshot>();
        private readonly Dictionary<int, string> _sessions = new Dictionary<int, string>();
        private bool _providerWarning;

        public NameSyncService()
        {
            Current = this;
            Exports.Add("GetDisplayName", new Func<int, string>(GetDisplayName));
            Exports.Add("GetDisplayNameBySteam", new Func<string, string>(GetDisplayNameBySteam));
            Exports.Add("GetAllDisplayNames", new Func<IDictionary<int, string>>(() => GetVisibleServerNameMap()));
            Exports.Add("GetCurrentTerminal", new Func<int, string>(GetCurrentTerminal));
            Exports.Add("GetAllCurrentTerminals", new Func<IDictionary<int, string>>(() => GetVisibleServerTerminalMap()));
            Exports.Add("GetFullInfoBySteam", new Func<string, IDictionary<string, string>>(GetFullInfoBySteam));
            Exports.Add("GetAllFullInfo", new Func<IDictionary<string, IDictionary<string, string>>>(GetAllFullInfo));
            EventHandlers["onResourceStart"] += new Action<string>(OnResourceStart);
            EventHandlers["playerJoining"] += new Action<Player>(OnPlayerJoining);
            EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);
            EventHandlers["code6_selfmanage:identitiesUpdated"] += new Action(() => { _ = RebuildAndBroadcast(); });
            EventHandlers["vMenu:RequestNameMap"] += new Action<Player>(OnRequestNameMap);
            API.RegisterCommand("vmenu_refreshnames", new Action<int, List<object>, string>((src, args, raw) =>
            {
                if (src == 0) _ = RebuildAndBroadcast(); // Re-read local exports; never fetch the feed.
            }), false);
        }

        private void OnResourceStart(string resource)
        {
            if (resource == API.GetCurrentResourceName()) _ = RefreshLoop();
            else if (resource == "code6_selfmanage") _ = RebuildAndBroadcast();
        }

        private void OnPlayerJoining([FromSource] Player player) { _ = RebuildAndBroadcast(); }
        private void OnPlayerDropped([FromSource] Player player, string reason)
        {
            if (int.TryParse(player.Handle, out var sid))
            {
                _serverNameMap.Remove(sid);
                _serverTerminalMap.Remove(sid);
                _lastKnown.Remove(sid);
                _sessions.Remove(sid);
            }
            // The dropped player can still appear in Players during this event.
            _ = RebuildAfterDrop();
        }
        private async Task RebuildAfterDrop() { await Delay(0); await RebuildAndBroadcast(); }

        private IdentitySnapshot ReadIdentity(Player player)
        {
            var sid = int.Parse(player.Handle);
            var fingerprint = string.Join("\n", player.Identifiers.Select(id => id.ToLowerInvariant()).OrderBy(id => id));
            if (!_sessions.TryGetValue(sid, out var oldSession) || oldSession != fingerprint)
            {
                _lastKnown.Remove(sid);
                _serverTerminalMap.Remove(sid);
                _sessions[sid] = fingerprint;
            }
            try
            {
                object value = Exports["code6_selfmanage"].GetIdentity(sid);
                // Cfx Lua exports marshal tables as dynamic dictionaries, not this C# DTO.
                var identity = JsonConvert.DeserializeObject<IdentitySnapshot>(JsonConvert.SerializeObject(value));
                if (identity == null || string.IsNullOrWhiteSpace(identity.display_name))
                    throw new InvalidOperationException("Invalid identity snapshot");
                _providerWarning = false;
                if (identity.resolved) _lastKnown[sid] = identity;
                else if (_lastKnown.TryGetValue(sid, out var known)) return known;
                return identity;
            }
            catch
            {
                if (!_providerWarning)
                {
                    Debug.WriteLine("[vMenu:NameSync] GetIdentity unavailable; retaining known identities until provider recovers.");
                    _providerWarning = true;
                }
                return _lastKnown.TryGetValue(sid, out var known) ? known :
                    new IdentitySnapshot { plain_name = player.Name, display_name = player.Name };
            }
        }

        private string ReadTerminal(int sid)
        {
            try
            {
                var entry = ReadTerminalEntry(sid);
                if (entry != null)
                {
                    if (entry.TryGetValue("current_terminal", out var terminal) && !string.IsNullOrWhiteSpace(terminal?.ToString())) return terminal.ToString().Trim();
                    if (entry.TryGetValue("panel_version", out var panel)) return panel?.ToString()?.Trim() ?? "";
                }
            }
            catch { if (_serverTerminalMap.TryGetValue(sid, out var previous)) return previous; }
            return "";
        }

        private Dictionary<string, object> ReadTerminalEntry(int sid)
        {
            object value = Exports["code6_selfmanage"].GetTerminalEntryForSource(sid);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(value));
        }

        internal static string FormatDisplay(IdentitySnapshot identity)
        {
            var name = vMenuShared.ScaleformText.Clean(identity.plain_name ?? identity.display_name);
            if (!identity.resolved) return name;
            var markers = new[] { "", "[D]", "[S1]", "[S2]", "[L]" };
            if (identity.boosty_level >= 1 && identity.boosty_level <= 4) name += " " + markers[identity.boosty_level];
            return name + " | " + Math.Floor(identity.rating + 0.5).ToString("#,0", CultureInfo.InvariantCulture);
        }

        public static string GetPlayerDisplayName(Player player)
        {
            // Server-only notifications may address hidden players; public snapshots still filter them.
            return player == null ? "Player" : Current == null ? player.Name : FormatDisplay(Current.ReadIdentity(player));
        }

        private string GetDisplayName(int sid)
        {
            var player = FindPlayer(sid);
            return player == null || IsHidden(player) ? null : GetPlayerDisplayName(player);
        }
        private Player FindPlayer(int sid) => Players.FirstOrDefault(p => p.Handle == sid.ToString());
        private Player FindSteamPlayer(string steam) => string.IsNullOrWhiteSpace(steam) ? null :
            Players.FirstOrDefault(p => p.Identifiers.Any(id => string.Equals(id, steam, StringComparison.OrdinalIgnoreCase)));
        private static bool IsHidden(Player player) => player.Identifiers.Any(HiddenPlayerConfig.IsHiddenSteamIdentifier);
        private string GetDisplayNameBySteam(string steam)
        {
            var player = FindSteamPlayer(steam);
            return player == null || IsHidden(player) ? null : GetPlayerDisplayName(player);
        }

        // Legacy exports now describe connected players only; no independent offline feed dictionary.
        private IDictionary<string, string> GetFullInfoBySteam(string steam)
        {
            var player = FindSteamPlayer(steam);
            if (player == null || IsHidden(player)) return null;
            var identity = ReadIdentity(player);
            Dictionary<string, object> entry = null;
            try { entry = ReadTerminalEntry(int.Parse(player.Handle)); } catch { }
            string Metadata(string key, string fallback = "") => entry != null && entry.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
            return new Dictionary<string, string>
            {
                ["name"] = FormatDisplay(identity), ["plain_name"] = identity.plain_name ?? "",
                ["display_name"] = identity.display_name ?? "", ["tag"] = identity.tag ?? "",
                ["rating"] = identity.rating.ToString(CultureInfo.InvariantCulture),
                ["boosty_level"] = identity.boosty_level.ToString(), ["level"] = Metadata("level", "0"),
                ["sublevel"] = Metadata("sublevel"), ["resolved"] = identity.resolved.ToString(),
                ["updated_at"] = identity.updated_at.ToString(CultureInfo.InvariantCulture),
                ["current_terminal"] = ReadTerminal(int.Parse(player.Handle)), ["panel_version"] = Metadata("panel_version")
            };
        }
        private IDictionary<string, IDictionary<string, string>> GetAllFullInfo()
        {
            var result = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in Players.Where(p => !IsHidden(p)))
                foreach (var id in player.Identifiers.Where(id => id.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)))
                    result[id] = GetFullInfoBySteam(id);
            return result;
        }

        public static string GetCurrentTerminalForServerId(int sid) => Current?.GetCurrentTerminal(sid);
        private string GetCurrentTerminal(int sid)
        {
            var player = FindPlayer(sid);
            return player == null || IsHidden(player) ? null : ReadTerminal(sid);
        }

        private async Task RefreshLoop()
        {
            while (true)
            {
                await RebuildAndBroadcast();
                await Delay(60_000); // Local recovery/terminal refresh; identities update immediately on feed events.
            }
        }
        private void RebuildLocalMap()
        {
            var live = new HashSet<int>();
            foreach (var player in Players)
            {
                if (!int.TryParse(player.Handle, out var sid)) continue;
                live.Add(sid);
                if (IsHidden(player)) { _serverNameMap.Remove(sid); _serverTerminalMap.Remove(sid); continue; }
                _serverNameMap[sid] = GetPlayerDisplayName(player);
                _serverTerminalMap[sid] = ReadTerminal(sid);
            }
            foreach (var sid in _sessions.Keys.Concat(_serverNameMap.Keys).Distinct().Where(id => !live.Contains(id)).ToArray())
            {
                _serverNameMap.Remove(sid); _serverTerminalMap.Remove(sid); _lastKnown.Remove(sid); _sessions.Remove(sid);
            }
        }
        private Dictionary<int, string> VisibleMap(Dictionary<int, string> map)
        {
            var hidden = HiddenPlayerConfig.GetHiddenPlayerServerIds(Players);
            return map.Where(kv => !hidden.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        private Dictionary<int, string> GetVisibleServerNameMap() => VisibleMap(_serverNameMap);
        private Dictionary<int, string> GetVisibleServerTerminalMap() => VisibleMap(_serverTerminalMap);
        private Dictionary<int, object> BuildPlayerInfoMap()
        {
            var map = new Dictionary<int, object>();
            foreach (var kv in GetVisibleServerNameMap())
            {
                var player = FindPlayer(kv.Key);
                if (player == null) continue;
                var identity = ReadIdentity(player);
                map[kv.Key] = new Dictionary<string, object>
                {
                    ["name"] = kv.Value, ["plain_name"] = identity.plain_name ?? identity.display_name,
                    ["display_name"] = identity.display_name, ["rating"] = identity.rating,
                    ["boosty_level"] = identity.boosty_level, ["tag"] = identity.tag ?? "",
                    ["resolved"] = identity.resolved, ["updated_at"] = identity.updated_at,
                    ["current_terminal"] = _serverTerminalMap.TryGetValue(kv.Key, out var terminal) ? terminal : ""
                };
            }
            return map;
        }
        private void OnRequestNameMap([FromSource] Player player)
        {
            RebuildLocalMap();
            player?.TriggerEvent("vMenu:NameMapUpdated", GetVisibleServerNameMap());
            player?.TriggerEvent("vMenu:PlayerInfoMapUpdated", BuildPlayerInfoMap());
        }
        private async Task RebuildAndBroadcast()
        {
            try
            {
                RebuildLocalMap();
                var names = GetVisibleServerNameMap();
                var info = BuildPlayerInfoMap();
                foreach (var player in Players)
                {
                    player.TriggerEvent("vMenu:NameMapUpdated", names);
                    player.TriggerEvent("vMenu:PlayerInfoMapUpdated", info);
                }
            }
            catch (Exception e) { Debug.WriteLine($"[vMenu:NameSync] Snapshot update failed: {e.Message}"); }
            await Delay(0);
        }

        internal sealed class IdentitySnapshot
        {
            public string plain_name { get; set; }
            public string display_name { get; set; }
            public double rating { get; set; }
            public int boosty_level { get; set; }
            public string tag { get; set; }
            public bool resolved { get; set; }
            public double updated_at { get; set; }
        }
    }
}
