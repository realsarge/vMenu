using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using CitizenFX.Core;
using CitizenFX.Core.Native;

using Newtonsoft.Json;

using static vMenuShared.ConfigManager;

namespace vMenuServer
{
    public class NameSyncService : BaseScript
    {
        private const string EndpointBase = "https://code6.ru/api/gamesync.php?key=HWN3b73T&no-emoji=1&setting=";

        private Dictionary<string, RemotePlayerInfo> _remoteBySteam =
            new Dictionary<string, RemotePlayerInfo>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<int, string> _serverNameMap = new Dictionary<int, string>();

        static NameSyncService()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | (SecurityProtocolType)0x00000C00;
                ServicePointManager.ServerCertificateValidationCallback = IgnoreBadCerts;
            }
            catch
            {
            }
        }

        public NameSyncService()
        {
            Exports.Add("GetDisplayName", new Func<int, string>(GetDisplayName));
            Exports.Add("GetDisplayNameBySteam", new Func<string, string>(GetDisplayNameBySteam));
            Exports.Add("GetAllDisplayNames", new Func<IDictionary<int, string>>(() =>
                new Dictionary<int, string>(_serverNameMap)));
            Exports.Add("GetFullInfoBySteam", new Func<string, IDictionary<string, string>>(GetFullInfoBySteam));
            Exports.Add("GetAllFullInfo", new Func<IDictionary<string, IDictionary<string, string>>>(GetAllFullInfo));

            EventHandlers["onResourceStart"] += new Action<string>(OnResourceStart);
            EventHandlers["playerJoining"] += new Action<Player>(OnPlayerJoining);
            EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);
            EventHandlers["vMenu:RequestNameMap"] += new Action<Player>(OnRequestNameMap);

            API.RegisterCommand("vmenu_refreshnames", new Action<int, List<object>, string>(OnRefreshCommand), false);
        }

        private static bool IgnoreBadCerts(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        private static string GetEndpoint()
        {
            var locale = (GetSettingsString(Setting.vmenu_namesync_locale, "ru") ?? "ru").Trim().ToLowerInvariant();
            if (locale != "ru" && locale != "us")
            {
                locale = "ru";
            }
            return EndpointBase + locale;
        }

        private void OnResourceStart(string resName)
        {
            if (API.GetCurrentResourceName() == resName)
            {
                _ = RefreshLoop();
            }
        }

        private void OnPlayerJoining([FromSource] Player source)
        {
            _ = RebuildAndBroadcast();
        }

        private void OnPlayerDropped([FromSource] Player source, string reason)
        {
            if (int.TryParse(source.Handle, out var sid))
            {
                _serverNameMap.Remove(sid);
            }
            _ = RebuildAndBroadcast();
        }

        private IDictionary<string, string> GetFullInfoBySteam(string steamHex)
        {
            if (string.IsNullOrWhiteSpace(steamHex))
            {
                return null;
            }

            if (_remoteBySteam.TryGetValue(steamHex, out var info))
            {
                return new Dictionary<string, string>
                {
                    ["name"] = info?.name ?? "",
                    ["sublevel"] = info?.sublevel ?? "",
                    ["tag"] = info?.tag ?? "",
                    ["level"] = info?.level.ToString() ?? "0"
                };
            }

            return null;
        }

        private IDictionary<string, IDictionary<string, string>> GetAllFullInfo()
        {
            var map = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _remoteBySteam)
            {
                map[kv.Key] = new Dictionary<string, string>
                {
                    ["name"] = kv.Value?.name ?? "",
                    ["sublevel"] = kv.Value?.sublevel ?? "",
                    ["tag"] = kv.Value?.tag ?? "",
                    ["level"] = kv.Value?.level.ToString() ?? "0"
                };
            }
            return map;
        }

        private void OnRequestNameMap([FromSource] Player player)
        {
            try
            {
                RebuildLocalMap();
                player?.TriggerEvent("vMenu:NameMapUpdated", _serverNameMap);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[vMenu:NameSync] send snapshot error: {e.Message}");
            }
        }

        private void OnRefreshCommand(int src, List<object> args, string raw)
        {
            if (src == 0)
            {
                _ = ForceRefresh();
                Debug.WriteLine("[vMenu:NameSync] Forced names refresh");
            }
        }

        private async Task RefreshLoop()
        {
            await PullRemote();
            await RebuildAndBroadcast();

            while (true)
            {
                await Delay(60_000);
                await PullRemote();
                await RebuildAndBroadcast();
            }
        }

        private async Task ForceRefresh()
        {
            await PullRemote();
            await RebuildAndBroadcast();
        }

        private static async Task<string> HttpGetAsync(string url)
        {
            using (var wc = new WebClient())
            {
                return await wc.DownloadStringTaskAsync(new Uri(url));
            }
        }

        private async Task PullRemote()
        {
            try
            {
                var json = await HttpGetAsync(GetEndpoint());
                var payload = JsonConvert.DeserializeObject<RemotePayload>(json);

                _remoteBySteam = payload?.identifiers ??
                                 new Dictionary<string, RemotePlayerInfo>(StringComparer.OrdinalIgnoreCase);

                Debug.WriteLine($"[vMenu:NameSync] Pulled {_remoteBySteam.Count} entries from JSON.");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[vMenu:NameSync] PullRemote error: {e.Message}");
            }
        }

        private async Task RebuildAndBroadcast()
        {
            try
            {
                RebuildLocalMap();

                foreach (var player in Players)
                {
                    try
                    {
                        player.TriggerEvent("vMenu:NameMapUpdated", _serverNameMap);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"[vMenu:NameSync] broadcast error: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[vMenu:NameSync] Rebuild error: {e.Message}");
            }

            await Delay(0);
        }

        private void RebuildLocalMap()
        {
            _serverNameMap.Clear();

            foreach (var player in Players)
            {
                if (!int.TryParse(player.Handle, out var sid))
                {
                    continue;
                }

                var steam = GetSteamHexFor(sid);
                if (!string.IsNullOrEmpty(steam) &&
                    _remoteBySteam.TryGetValue(steam, out var info))
                {
                    var display = FormatDisplay(info);
                    _serverNameMap[sid] = !string.IsNullOrWhiteSpace(display) ? display : (player.Name ?? $"Player {sid}");
                }
                else
                {
                    _serverNameMap[sid] = player.Name ?? $"Player {sid}";
                }
            }
        }

        private static string GetSteamHexFor(int serverId)
        {
            try
            {
                var src = serverId.ToString();
                var count = API.GetNumPlayerIdentifiers(src);
                for (int i = 0; i < count; i++)
                {
                    var id = API.GetPlayerIdentifier(src, i);
                    if (!string.IsNullOrEmpty(id) && id.StartsWith("steam:", StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string FormatDisplay(RemotePlayerInfo info)
        {
            var name = info?.name?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private string GetDisplayName(int serverId)
        {
            if (_serverNameMap.TryGetValue(serverId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var player = Players.FirstOrDefault(p => int.TryParse(p.Handle, out var sid) && sid == serverId);
            if (player != null)
            {
                var steam = GetSteamHexFor(serverId);
                if (!string.IsNullOrEmpty(steam) &&
                    _remoteBySteam.TryGetValue(steam, out var info))
                {
                    var display = FormatDisplay(info);
                    if (!string.IsNullOrWhiteSpace(display))
                    {
                        return display;
                    }
                }

                return player.Name ?? $"Player {serverId}";
            }

            return $"Player {serverId}";
        }

        private string GetDisplayNameBySteam(string steamHex)
        {
            if (string.IsNullOrWhiteSpace(steamHex))
            {
                return null;
            }

            if (_remoteBySteam.TryGetValue(steamHex, out var info))
            {
                return FormatDisplay(info);
            }

            return null;
        }

        private sealed class RemotePayload
        {
            public Dictionary<string, RemotePlayerInfo> identifiers { get; set; }
        }

        private sealed class RemotePlayerInfo
        {
            public string name { get; set; }
            public string sublevel { get; set; }
            public int level { get; set; }
            public string tag { get; set; }
        }
    }
}
