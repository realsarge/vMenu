using System;
using System.Collections.Generic;

using CitizenFX.Core;

using Newtonsoft.Json;

namespace vMenuClient
{
    public class NameMapClient : BaseScript
    {
        public static readonly Dictionary<int, string> NameMap = new Dictionary<int, string>();
        public static readonly Dictionary<int, string> TerminalMap = new Dictionary<int, string>();
        public static readonly Dictionary<int, string> HeaderNameMap = new Dictionary<int, string>();
        public static readonly Dictionary<int, string> TagMap = new Dictionary<int, string>();
        public static event Action OnUpdated;

        public static string ResolveName(int serverId, string fallback) =>
            vMenuShared.ScaleformText.Clean(NameMap.TryGetValue(serverId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : fallback);

        public NameMapClient()
        {
            EventHandlers["vMenu:NameMapUpdated"] += new Action<object>(OnNameMapUpdated);
            EventHandlers["vMenu:PlayerInfoMapUpdated"] += new Action<object>(OnPlayerInfoMapUpdated);
            RequestSnapshot();
        }

        private void OnNameMapUpdated(object mapObj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(mapObj);
                var any = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (any == null)
                {
                    return;
                }

                var freshNameMap = new Dictionary<int, string>();
                foreach (var kv in any)
                {
                    if (int.TryParse(kv.Key, out var sid))
                    {
                        freshNameMap[sid] = vMenuShared.ScaleformText.Clean(kv.Value?.ToString());
                    }
                }

                NameMap.Clear();
                foreach (var kv in freshNameMap)
                {
                    NameMap[kv.Key] = kv.Value;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[NameMapClient] normalize error: {e.Message}");
            }

            OnUpdated?.Invoke();
        }

        private void OnPlayerInfoMapUpdated(object mapObj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(mapObj);
                var any = JsonConvert.DeserializeObject<Dictionary<string, SyncedPlayerInfo>>(json);
                if (any == null)
                {
                    return;
                }

                var freshNameMap = new Dictionary<int, string>();
                var freshTerminalMap = new Dictionary<int, string>();
                var freshHeaderMap = new Dictionary<int, string>();
                var freshTagMap = new Dictionary<int, string>();
                foreach (var kv in any)
                {
                    if (int.TryParse(kv.Key, out var sid))
                    {
                        freshNameMap[sid] = vMenuShared.ScaleformText.Clean(kv.Value?.name);
                        freshTerminalMap[sid] = kv.Value?.current_terminal ?? string.Empty;
                        freshHeaderMap[sid] = vMenuShared.ScaleformText.Clean(kv.Value?.plain_name ?? kv.Value?.name);
                        freshTagMap[sid] = kv.Value?.tag ?? string.Empty;
                    }
                }

                NameMap.Clear();
                TerminalMap.Clear();
                HeaderNameMap.Clear();
                TagMap.Clear();
                foreach (var kv in freshNameMap)
                {
                    NameMap[kv.Key] = kv.Value;
                }
                foreach (var kv in freshTerminalMap)
                {
                    TerminalMap[kv.Key] = kv.Value;
                }
                foreach (var kv in freshHeaderMap) HeaderNameMap[kv.Key] = kv.Value;
                foreach (var kv in freshTagMap) TagMap[kv.Key] = kv.Value;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[NameMapClient] normalize info error: {e.Message}");
            }

            OnUpdated?.Invoke();
        }

        public static void RequestSnapshot()
        {
            BaseScript.TriggerServerEvent("vMenu:RequestNameMap");
        }

        private sealed class SyncedPlayerInfo
        {
            public string name { get; set; }
            public string plain_name { get; set; }
            public string tag { get; set; }
            public string current_terminal { get; set; }
        }
    }
}
