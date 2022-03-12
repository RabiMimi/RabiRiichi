﻿using Microsoft.Extensions.DependencyInjection;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Event;
using RabiRiichi.Event.InGame;
using RabiRiichi.Interact;
using RabiRiichi.Pattern;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Riichi {

    public class Game {
        public const int HandSize = 13;
        public readonly ServiceProvider diContainer;

        public readonly GameInfo info;
        public GameConfig config => info.config;
        public readonly Wall wall;
        public readonly EventBus eventBus;
        public readonly Player[] players;


        public Game(GameConfig config) {
            var rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
            players = new Player[config.playerCount];
            var serviceCollection = new ServiceCollection();

            // Existing instances
            serviceCollection.AddSingleton(this);
            serviceCollection.AddSingleton(rand);
            serviceCollection.AddSingleton(config);

            // Core utils
            serviceCollection.AddSingleton<EventBus>();
            serviceCollection.AddSingleton<EventListenerFactory>();
            serviceCollection.AddSingleton<JsonStringify>();

            // Game related
            serviceCollection.AddSingleton<Wall>();
            serviceCollection.AddSingleton<GameInfo>();
            serviceCollection.AddSingleton<ActionManager>();
            serviceCollection.AddSingleton<PatternResolver>();

            // Custom setup
            config.setup.Inject(this, serviceCollection);

            // Build DI container
            diContainer = serviceCollection.BuildServiceProvider();

            // Get instances
            eventBus = diContainer.GetService<EventBus>();
            info = diContainer.GetService<GameInfo>();
            wall = diContainer.GetService<Wall>();

            // Custom setup
            config.setup.Setup(diContainer);
        }

        #region Internal
        public T Get<T>() => diContainer.GetService<T>();
        #endregion

        #region GameUtil
        public bool IsYaku(Tile tile) {
            return tile.IsSangen || tile.IsSame(Tile.From(info.wind));
        }
        public Player GetPlayer(int index) => players[index];
        public int Time => info.timeStamp;
        #endregion

        #region Start

        public async Task Start() {
            // 开始游戏
            info.phase = GamePhase.Running;

            // 初始化玩家
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player(i, this) {
                    wind = (Wind)i,
                };
            }

            // 游戏逻辑
            eventBus.Queue(new BeginGameEvent(this, Wind.E, 0, 0));
            await eventBus.ProcessQueue();

            // 结束游戏
            info.phase = GamePhase.Finished;
        }
        #endregion

        #region Player
        public int NextPlayerId(int id) => id == players.Length - 1 ? 0 : id + 1;

        public int PrevPlayerId(int id) => id == 0 ? players.Length - 1 : id - 1;

        public Player NextPlayer(int id) => players[NextPlayerId(id)];

        public Player PrevPlayer(int id) => players[PrevPlayerId(id)];

        public IEnumerable<GameTile> AllDiscardedTiles =>
            players.SelectMany(player => player.hand.discarded);
        #endregion

        #region Game Turns
        public void ResetIppatsu() {
            foreach (var player in players) {
                // TODO: Send an event instead
                player.hand.ippatsu = false;
            }
        }
        #endregion
    }
}
