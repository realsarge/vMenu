using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuClient
{
    public interface IPlayer
    {
        int Handle { get; }
        int ServerId { get; }
        Ped Character { get; }
        bool IsLocal { get; }
        bool IsActive { get; }
        string Name { get; }
        string CurrentTerminal { get; }
    }

    public interface IPlayerList : IEnumerable<IPlayer>
    {
        void RequestPlayerList();

        void ReceivedPlayerList(IList<object> players);

        Task WaitRequested();
    }

    public class NativePlayer : IPlayer
    {
        private readonly Player player;

        public NativePlayer(Player player)
        {
            this.player = player;
        }

        public int Handle => player.Handle;
        public int ServerId => player.ServerId;
        public Ped Character => player.Character;
        public bool IsLocal => player == Game.Player;
        public bool IsActive => NetworkIsPlayerActive(player.Handle);
        public string Name => player.Name;
        public string CurrentTerminal =>
            NameMapClient.TerminalMap.TryGetValue(ServerId, out var terminal) ? terminal : string.Empty;
    }

    public class NativePlayerList : IPlayerList
    {
        private readonly PlayerList playerList;

        public NativePlayerList(PlayerList playerList)
        {
            this.playerList = playerList;
        }

        public IEnumerator<IPlayer> GetEnumerator()
        {
            foreach (var player in playerList)
            {
                yield return new NativePlayer(player);
            }
        }

        public void RequestPlayerList()
        {
            // we are a local-only player list
        }

        public void ReceivedPlayerList(IList<object> players)
        {

        }

        public Task WaitRequested()
        {
            // we instantly complete, as we always have all players
            return Task.FromResult(0);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var player in playerList)
            {
                yield return new NativePlayer(player);
            }
        }
    }

    public class InfinityPlayer : IPlayer
    {
        public InfinityPlayer(int serverId, string name, string currentTerminal = "")
        {
            ServerId = serverId;
            Name = name;
            CurrentTerminal = currentTerminal ?? string.Empty;
        }

        public int Handle => GetPlayerFromServerId(ServerId);

        public int ServerId { get; }

        public Ped Character
        {
            get
            {
                if (Handle >= 0)
                {
                    var ped = GetPlayerPed(Handle);

                    if (ped > 0)
                    {
                        return new Ped(ped);
                    }
                }

                return null;
            }
        }

        public bool IsLocal => ServerId == GetPlayerServerId(PlayerId());
        public bool IsActive => Handle != -1 && NetworkIsPlayerActive(Handle);

        public string Name { get; }
        public string CurrentTerminal { get; }
    }

    public class InfinityPlayerList : IPlayerList
    {
        private readonly PlayerList playerList;
        private readonly Dictionary<int, InfinityPlayer> remotePlayerList;

        private int updatingPlayerList;

        public InfinityPlayerList(PlayerList playerList)
        {
            this.playerList = playerList;
            this.remotePlayerList = new Dictionary<int, InfinityPlayer>();
        }

        private IEnumerator<IPlayer> GetEnumeratorInternal()
        {
            var nearPlayers = new HashSet<int>();

            foreach (var player in playerList)
            {
                yield return new NativePlayer(player);
                nearPlayers.Add(player.ServerId);
            }

            foreach (var player in remotePlayerList)
            {
                if (!nearPlayers.Contains(player.Value.ServerId))
                {
                    yield return player.Value;
                }
            }
        }

        public IEnumerator<IPlayer> GetEnumerator()
        {
            return GetEnumeratorInternal();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumeratorInternal();
        }

        public void RequestPlayerList()
        {
            updatingPlayerList++;
            BaseScript.TriggerServerEvent("vMenu:RequestPlayerList");
        }

        public void ReceivedPlayerList(IList<object> players)
        {
            remotePlayerList.Clear();

            foreach (var playerPair in players)
            {
                if (playerPair is IDictionary<string, object> playerDict)
                {
                    if (playerDict.TryGetValue("n", out var nameObj) && playerDict.TryGetValue("s", out var serverIdObj))
                    {
                        if (nameObj is string name)
                        {
                            var serverId = Convert.ToInt32(serverIdObj);
                            var currentTerminal = playerDict.TryGetValue("t", out var terminalObj)
                                ? terminalObj?.ToString() ?? string.Empty
                                : string.Empty;

                            remotePlayerList[serverId] = new InfinityPlayer(serverId, name, currentTerminal);
                        }
                    }
                }
            }

            updatingPlayerList--;
        }

        public async Task WaitRequested()
        {
            while (updatingPlayerList > 0)
            {
                await BaseScript.Delay(0);
            }
        }
    }
}
