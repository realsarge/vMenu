using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using vMenuServer;
using vMenuClient;

public class Provider : DynamicObject
{
    public bool Down;
    public int Reads;
    public Dictionary<int, object> Identities = new Dictionary<int, object>();
    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
        Reads++;
        if (Down) throw new InvalidOperationException("QA unavailable");
        var id = Convert.ToInt32(args[0]);
        result = binder.Name == "GetIdentity" ? Identities[id] :
            new Dictionary<string, object> { ["current_terminal"] = "police", ["panel_version"] = "unit" };
        return true;
    }
}

public static class Program
{
    static int checks;
    static void Equal(object expected, object actual, string label)
    {
        if (!Equals(expected, actual)) throw new Exception(label + ": expected " + expected + ", got " + actual);
        checks++;
    }
    static object Identity(string name, double rating = -1234, int badge = 1, bool resolved = true) =>
        new Dictionary<string, object>
        {
            ["plain_name"] = name, ["display_name"] = resolved ? name + " 🐸" : name,
            ["rating"] = rating, ["boosty_level"] = badge, ["tag"] = "22",
            ["resolved"] = resolved, ["updated_at"] = 100L
        };
    public static void Main()
    {
        var provider = BaseScript.Provider;
        var qa = new Player { Handle = "17", Name = "FiveM QA", Identifiers = new[] { "license:qa", "STEAM:QA" } };
        var hidden = new Player { Handle = "18", Name = "Hidden QA", Identifiers = new[] { "license:hidden", "steam:hidden" } };
        BaseScript.ServerPlayers.Add(qa);
        BaseScript.ServerPlayers.Add(hidden);
        CitizenFX.Core.Native.API.Hidden = "HIDDEN";
        provider.Identities[17] = Identity("QA Name #22");
        provider.Identities[18] = Identity("Hidden Forum #18");
        var service = new NameSyncService();
        var client = new NameMapClient();
        Action Refresh = () => ((Action)service.EventHandlers["code6_selfmanage:identitiesUpdated"])();
        Func<IDictionary<int, string>> Names = () => ((Func<IDictionary<int, string>>)service.Exports.Functions["GetAllDisplayNames"])();
        Func<string, string> BySteam = (Func<string, string>)service.Exports.Functions["GetDisplayNameBySteam"];
        Refresh();
        Equal("QA Name #22 🐸 ⌃ | -1,234", Names()[17], "structured negative rating once");
        Equal(1, Names().Count, "hidden players filtered from snapshot");
        Equal(null, BySteam("STEAM:HIDDEN"), "hidden export filtered");
        Equal("QA Name #22 🐸 ⌃ | -1,234", BySteam("steam:qa"), "mixed-case nonfirst Steam");
        var full = ((Func<IDictionary<string, IDictionary<string, string>>>)service.Exports.Functions["GetAllFullInfo"])();
        Equal(1, full.Count, "full-info hidden filtering");
        Equal("-1234", full["steam:qa"]["rating"], "full-info structured rating");
        ((Action<Player>)service.EventHandlers["vMenu:RequestNameMap"])(qa);
        var info = qa.Events.Last(e => e.Name == "vMenu:PlayerInfoMapUpdated").Payload;
        ((Action<object>)client.EventHandlers["vMenu:PlayerInfoMapUpdated"])(info);
        Equal("QA Name #22", NameMapClient.HeaderNameMap[17], "header takes plain field, never parses rating");
        Equal("22", NameMapClient.TagMap[17], "overhead tag takes structured field");
        Equal("QA Name #22 🐸 ⌃ | -1,234", NameMapClient.ResolveName(17, "FiveM"), "client authoritative name");
        Equal("police", NameMapClient.TerminalMap[17], "terminal remains local provider metadata");
        provider.Identities[17] = Identity("QA Renamed #22", 5000, 4);
        Refresh();
        Equal("QA Renamed #22 🐸 ▮ | 5,000", Names()[17], "online rename/rating/badge same snapshot");
        provider.Down = true;
        Refresh();
        Equal("QA Renamed #22 🐸 ▮ | 5,000", Names()[17], "last-known retained during provider outage");
        provider.Down = false;
        provider.Identities[17] = Identity("Unresolved after provider restart", 0, 0, false);
        Refresh();
        Equal("QA Renamed #22 🐸 ▮ | 5,000", Names()[17], "known identity retained during provider startup");
        qa.Identifiers = new[] { "license:new", "steam:new" };
        provider.Identities[17] = Identity("New FiveM QA", 0, 0, false);
        Refresh();
        Equal("New FiveM QA", Names()[17], "server-id reuse does not leak previous name");
        provider.Identities[17] = Identity("Recovered #23", 0, 0);
        Refresh();
        Equal("Recovered #23 🐸 | 0", Names()[17], "unsubscribed recovery without restart");
        var startup = new Player { Handle = "19", Name = "Startup FiveM", Identifiers = new[] { "steam:startup" } };
        BaseScript.ServerPlayers.Add(startup);
        provider.Down = true;
        Refresh();
        Equal("Startup FiveM", Names()[19], "provider down at initial join uses native only if never resolved");
        provider.Down = false;
        provider.Identities[19] = Identity("Startup Recovered #19", 20, 3);
        Refresh();
        Equal("Startup Recovered #19 🐸 ⌃⌃⌃◡ | 20", Names()[19], "startup recovery reaches online player");
        BaseScript.ServerPlayers.Remove(startup);
        BaseScript.ServerPlayers.Remove(qa);
        ((Action<Player, string>)service.EventHandlers["playerDropped"])(qa, "QA");
        Equal(0, Names().Count, "drop removes map");
        Equal(null, BySteam("steam:new"), "offline lookup no independent dictionary");
        Console.WriteLine(checks + " adapter checks passed (actual server/client source, mocked Cfx boundary).");
    }
}

namespace CitizenFX.Core
{
    public class FromSourceAttribute : Attribute { }
    public class EventMessage { public string Name; public object Payload; }
    public class Player
    {
        public string Handle;
        public string Name;
        public IEnumerable<string> Identifiers;
        public List<EventMessage> Events = new List<EventMessage>();
        public void TriggerEvent(string name, object payload) { Events.Add(new EventMessage { Name = name, Payload = payload }); }
    }
    public class ExportCollection
    {
        public Dictionary<string, Delegate> Functions = new Dictionary<string, Delegate>();
        public dynamic this[string resource] => BaseScript.Provider;
        public void Add(string name, Delegate handler) { Functions[name] = handler; }
    }
    public class BaseScript
    {
        public static Provider Provider = new Provider();
        public static List<Player> ServerPlayers = new List<Player>();
        public List<Player> Players => ServerPlayers;
        public EventCollection EventHandlers = new EventCollection();
        public ExportCollection Exports = new ExportCollection();
        public static Task Delay(int ms) => Task.CompletedTask;
        public static void TriggerServerEvent(string name) { }
    }
    public class EventCollection
    {
        private readonly Dictionary<string, Delegate> handlers = new Dictionary<string, Delegate>();
        public dynamic this[string name]
        {
            get => handlers.TryGetValue(name, out var handler) ? handler : null;
            set => handlers[name] = value;
        }
    }
    public static class Debug { public static void WriteLine(string message) { Console.WriteLine(message); } }
}
namespace CitizenFX.Core.Native
{
    public static class API
    {
        public static string Hidden = "";
        public static string GetConvar(string name, string fallback) => name == "vmenu_hidden_steam_ids" ? Hidden : fallback;
        public static string GetCurrentResourceName() => "vMenu";
        public static void RegisterCommand(string name, Delegate handler, bool restricted) { }
    }
}
