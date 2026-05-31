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
        public static event Action OnUpdated;

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

                NameMap.Clear();
                if (any != null)
                {
                    foreach (var kv in any)
                    {
                        if (int.TryParse(kv.Key, out var sid))
                        {
                            NameMap[sid] = kv.Value?.ToString() ?? string.Empty;
                        }
                    }
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

                NameMap.Clear();
                TerminalMap.Clear();
                if (any != null)
                {
                    foreach (var kv in any)
                    {
                        if (int.TryParse(kv.Key, out var sid))
                        {
                            NameMap[sid] = kv.Value?.name ?? string.Empty;
                            TerminalMap[sid] = kv.Value?.current_terminal ?? string.Empty;
                        }
                    }
                }
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
            public string current_terminal { get; set; }
        }
    }
}
