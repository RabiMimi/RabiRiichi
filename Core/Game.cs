﻿using Microsoft.Extensions.DependencyInjection;
using RabiRiichi.Actions;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Core {

    public class Game {
        public const int HAND_SIZE = 13;

        private readonly ServiceProvider diContainer;

        public readonly GameInfo info;
        public GameConfig config => info.config;
        public readonly Wall wall;
        public readonly EventBus eventBus;
        public readonly Player[] players;
        public readonly InitGameEvent initialEvent;
        public readonly EventQueue mainQueue;
        public readonly ProtoGraph protoGraph;

        public Game(GameConfig config) {
            if (config.actionCenter == null) {
                throw new ArgumentException("config.actionCenter must be provided");
            }
            initialEvent = new(this);
            var rand = new RabiRand(config.seed ?? (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            var serviceCollection = new ServiceCollection();

            // Existing instances
            serviceCollection.AddSingleton(this);
            serviceCollection.AddSingleton(rand);
            serviceCollection.AddSingleton(config);

            // Core utils
            serviceCollection.AddSingleton<EventBus>();
            serviceCollection.AddSingleton<EventListenerFactory>();

            // Protobuf
            serviceCollection.AddSingleton<ProtoGraph>();

            // Game related
            serviceCollection.AddSingleton<Wall>();
            serviceCollection.AddSingleton<GameInfo>();
            serviceCollection.AddSingleton<PatternResolver>();

            // Custom setup
            config.setup.Inject(this, serviceCollection);

            // Build DI container
            diContainer = serviceCollection.BuildServiceProvider();

            // Get instances
            eventBus = Get<EventBus>();
            info = Get<GameInfo>();
            wall = Get<Wall>();
            mainQueue = new EventQueue(eventBus, true);
            protoGraph = Get<ProtoGraph>();

            // Init Players
            players = new Player[config.playerCount];
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player(i, this);
            }

            // Queue initial event
            mainQueue.Queue(initialEvent);

            // Custom setup
            config.setup.Setup(diContainer);
        }

        #region Internal
        public T Get<T>() => diContainer.GetService<T>();
        public bool TryGet<T>(out T service) => (service = Get<T>()) != null;
        #endregion

        #region GameUtil
        public bool IsYaku(Tile tile) {
            return tile.IsSangen || tile.IsSame(Tile.From(info.wind));
        }
        public Player GetPlayer(int index) => players[index];
        public Player Dealer => GetPlayer(info.dealer);
        public Player[] PlayersByRank =>
            players.OrderBy(p => -p.points * players.Length + p.id).ToArray();
        public int Time => info.timeStamp;
        #endregion

        #region Start

        public async Task Start(CancellationToken? token = null) {
            // 开始游戏
            info.phase = GamePhase.Running;

            // 游戏逻辑
            await mainQueue.ProcessQueue(token);

            // 结束游戏
            info.phase = GamePhase.Finished;
        }
        #endregion

        #region Communication
        public Task<bool> SyncGameStateToPlayer(int playerId) {
            return eventBus.Process(new SyncGameStateEvent(initialEvent, playerId), true);
        }

        private readonly Mutex messageMutex = new();
        private const int MESSAGE_TIMEOUT = 60 * 1000;
        public void SendInquiry(MultiPlayerInquiry inquiry) {
            using (messageMutex.Lock(MESSAGE_TIMEOUT)) {
                config.actionCenter.OnInquiry(inquiry);
            }
        }

        public void SendEvent(int playerId, EventBase ev) {
            using (messageMutex.Lock(MESSAGE_TIMEOUT)) {
                config.actionCenter.OnEvent(playerId, ev);
            }
        }

        public T SerializeProto<T>(object obj, int playerId) where T : class {
            try {
                return protoGraph.Build()
                    .SetInput(obj)
                    .SetInput(ProtoConverters.PLAYER_ID, playerId)
                    .Execute<T>();
            } catch (ArgumentException) {
                return null;
            }
        }
        #endregion

        #region Player
        public int NextPlayerId(int id) => id == players.Length - 1 ? 0 : id + 1;

        public int PrevPlayerId(int id) => id == 0 ? players.Length - 1 : id - 1;

        public Player NextPlayer(int id) => players[NextPlayerId(id)];

        public Player PrevPlayer(int id) => players[PrevPlayerId(id)];

        /// <summary> 计算rhs玩家是lhs玩家后的第几个 </summary>
        public int Dist(int lhsId, int rhsId) {
            int dist = rhsId - lhsId;
            if (dist < 0) {
                dist += config.playerCount;
            }
            return dist;
        }
        #endregion

        #region Game Turns
        /// <summary> 是否是第一巡 </summary>
        public bool IsFirstJun => players.All(p => p.hand.jun <= 1 && p.hand.menzen && p.hand.called.Count == 0);
        #endregion
    }
}
